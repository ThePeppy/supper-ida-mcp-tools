using SupperIdaMcp.Center.Core;
using SupperIdaMcp.Center.Mcp;
using SupperIdaMcp.Center.TcpHub;

var registry = new TargetRegistry();
var operationLog = new OperationLogStore();
var appOptions = ParseOptions(args);
await using var hub = new IdaTcpHub(appOptions.Hub, registry);
using var shutdown = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

var hubTask = hub.RunAsync(shutdown.Token);
if (appOptions.Stdio)
{
    Console.Error.WriteLine($"Supper IDA MCP Center listening on {appOptions.Hub.Host}:{appOptions.Hub.Port}");
    var mcpServer = new StdioMcpServer(
        new McpToolCatalog(),
        new IdaMcpToolHandler(registry, operationLog));
    await mcpServer
        .RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput(), shutdown.Token)
        .ConfigureAwait(false);
    shutdown.Cancel();
}
else
{
    Console.WriteLine($"Supper IDA MCP Center listening on {appOptions.Hub.Host}:{appOptions.Hub.Port}");
    Console.WriteLine("Press Ctrl+C to stop.");

    while (!shutdown.IsCancellationRequested)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), shutdown.Token).ContinueWith(_ => { });
        Console.WriteLine($"Registered targets: {registry.ListTargets().Count}");
    }
}

await hubTask.ConfigureAwait(false);

static AppOptions ParseOptions(string[] args)
{
    var host = IdaTcpHubOptions.Default.Host;
    var port = IdaTcpHubOptions.Default.Port;
    var stdio = false;

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
            case "--stdio":
            case "--mcp-stdio":
                stdio = true;
                break;
        }
    }

    return new AppOptions(new IdaTcpHubOptions(host, port), stdio);
}

internal sealed record AppOptions(IdaTcpHubOptions Hub, bool Stdio);
