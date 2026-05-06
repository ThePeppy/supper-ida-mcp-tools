using SupperIdaMcp.Center.Core;
using SupperIdaMcp.Center.Dashboard;
using SupperIdaMcp.Center.Ida;
using SupperIdaMcp.Center.Mcp;
using SupperIdaMcp.Center.TcpHub;

var registry = new TargetRegistry();
var operationLog = new OperationLogStore();
var activeOperations = new ActiveOperationStore();
var appOptions = ParseOptions(args);
var idaLocator = new IdaLocator(appOptions.IdaPath);
var idaProcessRegistry = new IdaProcessRegistry();
var idaLaunchService = new IdaLaunchService(idaLocator, idaProcessRegistry);
await using var hub = new IdaTcpHub(appOptions.Hub, registry);
await using var dashboard = new DashboardServer(
    appOptions.Dashboard,
    registry,
    operationLog,
    activeOperations,
    idaLocator,
    idaLaunchService);
using var shutdown = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

var hubTask = hub.RunAsync(shutdown.Token);
var dashboardTask = appOptions.Ui
    ? dashboard.RunAsync(shutdown.Token)
    : Task.CompletedTask;
if (appOptions.Stdio)
{
    Console.Error.WriteLine($"Supper IDA MCP Center listening on {appOptions.Hub.Host}:{appOptions.Hub.Port}");
    if (appOptions.Ui)
    {
        Console.Error.WriteLine($"Supper IDA MCP Dashboard listening on http://{appOptions.Dashboard.Host}:{appOptions.Dashboard.Port}/");
    }

    var mcpServer = new StdioMcpServer(
        new McpToolCatalog(),
        new IdaMcpToolHandler(registry, operationLog, activeOperations, idaLocator, idaLaunchService));
    await mcpServer
        .RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput(), shutdown.Token)
        .ConfigureAwait(false);
    shutdown.Cancel();
}
else
{
    Console.WriteLine($"Supper IDA MCP Center listening on {appOptions.Hub.Host}:{appOptions.Hub.Port}");
    if (appOptions.Ui)
    {
        Console.WriteLine($"Supper IDA MCP Dashboard listening on http://{appOptions.Dashboard.Host}:{appOptions.Dashboard.Port}/");
    }

    Console.WriteLine("Press Ctrl+C to stop.");

    while (!shutdown.IsCancellationRequested)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), shutdown.Token).ContinueWith(_ => { });
        Console.WriteLine($"Registered targets: {registry.ListTargets().Count}");
    }
}

await hubTask.ConfigureAwait(false);
await dashboardTask.ConfigureAwait(false);

static AppOptions ParseOptions(string[] args)
{
    var host = IdaTcpHubOptions.Default.Host;
    var port = IdaTcpHubOptions.Default.Port;
    var stdio = false;
    string? idaPath = null;
    var ui = false;
    var uiHost = DashboardOptions.Default.Host;
    var uiPort = DashboardOptions.Default.Port;

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
            case "--ida-path" when i + 1 < args.Length:
                idaPath = args[++i];
                break;
            case "--ui":
                ui = true;
                break;
            case "--ui-host" when i + 1 < args.Length:
                uiHost = args[++i];
                break;
            case "--ui-port" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedUiPort):
                uiPort = parsedUiPort;
                break;
        }
    }

    return new AppOptions(
        new IdaTcpHubOptions(host, port),
        stdio,
        idaPath,
        ui,
        new DashboardOptions(uiHost, uiPort));
}

internal sealed record AppOptions(
    IdaTcpHubOptions Hub,
    bool Stdio,
    string? IdaPath,
    bool Ui,
    DashboardOptions Dashboard);
