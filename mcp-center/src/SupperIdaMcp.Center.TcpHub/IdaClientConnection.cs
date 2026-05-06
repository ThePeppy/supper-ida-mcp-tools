using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using SupperIdaMcp.Center.Core;
using SupperIdaMcp.Center.Protocol;
using SupperIdaMcp.Center.Protocol.Framing;

namespace SupperIdaMcp.Center.TcpHub;

public sealed class IdaClientConnection : IDisposable
{
    private readonly TcpClient _client;
    private readonly TargetRegistry _targetRegistry;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pendingCalls = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private NetworkStream? _stream;
    private string? _instanceId;

    public IdaClientConnection(TcpClient client, TargetRegistry targetRegistry)
    {
        _client = client;
        _targetRegistry = targetRegistry;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var stream = _client.GetStream();
        _stream = stream;
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await LengthPrefixedJsonFraming
                .ReadAsync<ProtocolMessage>(stream, cancellationToken)
                .ConfigureAwait(false);
            if (message is null)
            {
                break;
            }

            HandleMessage(message);
        }
    }

    public async Task<JsonElement> InvokeToolAsync(
        string toolName,
        JsonElement arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new InvalidOperationException("IDA target is not connected.");
        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingCalls.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException("Unable to register pending IDA call.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var registration = timeoutSource.Token.Register(
            static state => ((TaskCompletionSource<JsonElement>)state!).TrySetCanceled(),
            completion);
        timeoutSource.CancelAfter(timeout);

        try
        {
            var message = ProtocolMessage.Create(
                "tool_call",
                _instanceId,
                new
                {
                    tool = toolName,
                    arguments
                }) with
            {
                Id = requestId
            };

            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await LengthPrefixedJsonFraming
                    .WriteAsync(stream, message, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _writeGate.Release();
            }

            return await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingCalls.TryRemove(requestId, out _);
        }
    }

    public void Dispose()
    {
        if (_instanceId is not null)
        {
            _targetRegistry.Remove(_instanceId, this);
        }

        _client.Dispose();
        foreach (var pendingCall in _pendingCalls.Values)
        {
            pendingCall.TrySetException(new IOException("IDA target disconnected."));
        }
    }

    private void HandleMessage(ProtocolMessage message)
    {
        switch (message.Type)
        {
            case "hello":
                HandleHello(message);
                break;
            case "heartbeat":
                HandleHeartbeat(message);
                break;
            case "tool_result":
                HandleToolResult(message);
                break;
        }
    }

    private void HandleHello(ProtocolMessage message)
    {
        if (message.Payload is null)
        {
            return;
        }

        var metadata = message.Payload.Value.Deserialize<TargetMetadata>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (metadata is null)
        {
            return;
        }

        UpsertMetadata(metadata);
    }

    private void HandleHeartbeat(ProtocolMessage message)
    {
        var instanceId = message.InstanceId ?? _instanceId;
        if (instanceId is null)
        {
            return;
        }

        if (message.Payload is not null)
        {
            var metadata = message.Payload.Value.Deserialize<TargetMetadata>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (metadata is not null)
            {
                UpsertMetadata(metadata);
                return;
            }
        }

        _targetRegistry.MarkHeartbeat(instanceId, DateTimeOffset.UtcNow);
    }

    private void HandleToolResult(ProtocolMessage message)
    {
        if (message.Id is null || !_pendingCalls.TryRemove(message.Id, out var completion))
        {
            return;
        }

        if (message.Payload is null)
        {
            completion.TrySetException(new InvalidOperationException("IDA returned an empty tool result."));
            return;
        }

        completion.TrySetResult(message.Payload.Value.Clone());
    }

    private void UpsertMetadata(TargetMetadata metadata)
    {
        _instanceId = metadata.InstanceId;
        _targetRegistry.UpsertMetadata(metadata, this, DateTimeOffset.UtcNow);
    }
}
