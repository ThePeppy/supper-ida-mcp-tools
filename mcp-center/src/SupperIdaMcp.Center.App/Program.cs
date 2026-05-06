using SupperIdaMcp.Center.TcpHub;

var registry = new TargetRegistry();
var options = ParseOptions(args);
await using var hub = new IdaTcpHub(options, registry);
using var shutdown = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

Console.WriteLine($"Supper IDA MCP Center listening on {options.Host}:{options.Port}");
Console.WriteLine("Press Ctrl+C to stop.");

var hubTask = hub.RunAsync(shutdown.Token);
while (!shutdown.IsCancellationRequested)
{
    await Task.Delay(TimeSpan.FromSeconds(5), shutdown.Token).ContinueWith(_ => { });
    Console.WriteLine($"Registered targets: {registry.ListTargets().Count}");
}

await hubTask;

static IdaTcpHubOptions ParseOptions(string[] args)
{
    var host = IdaTcpHubOptions.Default.Host;
    var port = IdaTcpHubOptions.Default.Port;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--host" when i + 1 < args.Length:
                host = args[++i];
                break;
            case "--port" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedPort):
                port = parsedPort;
                break;
        }
    }

    return new IdaTcpHubOptions(host, port);
}
