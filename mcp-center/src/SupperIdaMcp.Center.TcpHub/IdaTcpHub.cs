using System.Net;
using System.Net.Sockets;

namespace SupperIdaMcp.Center.TcpHub;

public sealed class IdaTcpHub : IAsyncDisposable
{
    private readonly IdaTcpHubOptions _options;
    private readonly TargetRegistry _targetRegistry;
    private readonly List<IdaClientConnection> _connections = [];
    private readonly object _gate = new();
    private TcpListener? _listener;

    public IdaTcpHub(IdaTcpHubOptions options, TargetRegistry targetRegistry)
    {
        _options = options;
        _targetRegistry = targetRegistry;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var address = IPAddress.Parse(_options.Host);
        _listener = new TcpListener(address, _options.Port);
        _listener.Start();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                var connection = new IdaClientConnection(client, _targetRegistry);
                lock (_gate)
                {
                    _connections.Add(connection);
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await connection.RunAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        connection.Dispose();
                        lock (_gate)
                        {
                            _connections.Remove(connection);
                        }
                    }
                }, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    public ValueTask DisposeAsync()
    {
        _listener?.Stop();
        lock (_gate)
        {
            foreach (var connection in _connections.ToArray())
            {
                connection.Dispose();
            }

            _connections.Clear();
        }

        return ValueTask.CompletedTask;
    }
}
