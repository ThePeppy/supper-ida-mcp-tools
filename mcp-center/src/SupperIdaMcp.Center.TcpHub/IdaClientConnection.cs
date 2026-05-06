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
    private string? _instanceId;

    public IdaClientConnection(TcpClient client, TargetRegistry targetRegistry)
    {
        _client = client;
        _targetRegistry = targetRegistry;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var stream = _client.GetStream();
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

    public void Dispose()
    {
        if (_instanceId is not null)
        {
            _targetRegistry.Remove(_instanceId);
        }

        _client.Dispose();
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

        _instanceId = metadata.InstanceId;
        _targetRegistry.Upsert(new TargetInfo(
            metadata.InstanceId,
            metadata.Alias,
            metadata.ProcessId,
            metadata.BinaryName,
            metadata.InputPath,
            metadata.DatabasePath,
            DateTimeOffset.UtcNow,
            TargetHealth.Healthy));
    }

    private void HandleHeartbeat(ProtocolMessage message)
    {
        var instanceId = message.InstanceId ?? _instanceId;
        if (instanceId is null)
        {
            return;
        }

        _targetRegistry.MarkHeartbeat(instanceId, DateTimeOffset.UtcNow);
    }
}
