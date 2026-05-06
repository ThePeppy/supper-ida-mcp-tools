using SupperIdaMcp.Center.Core;
using SupperIdaMcp.Center.Ida;
using SupperIdaMcp.Center.Mcp;
using SupperIdaMcp.Center.Desktop.Setup;
using SupperIdaMcp.Center.TcpHub;

namespace SupperIdaMcp.Center.Desktop;

internal static class RuntimeHolder
{
    public static readonly TargetRegistry TargetRegistry = new();
    public static readonly OperationLogStore OperationLog = new();
    public static readonly ActiveOperationStore ActiveOperations = new();
    public static readonly IdaLocator IdaLocator = new();
    public static readonly IdaProcessRegistry IdaProcessRegistry = new();
    public static readonly IdaLaunchService IdaLaunchService = new(IdaLocator, IdaProcessRegistry);
    public static readonly IdaMcpToolHandler ToolHandler = new(
        TargetRegistry,
        OperationLog,
        ActiveOperations,
        IdaLocator,
        IdaLaunchService);
    public static readonly McpToolCatalog ToolCatalog = new();
    public static readonly IdaTcpHubOptions TcpOptions = new(DesktopSettings.Current.Host, DesktopSettings.Current.TcpPort);
    public static readonly HttpMcpOptions McpOptions = new(DesktopSettings.Current.Host, DesktopSettings.Current.McpPort, HttpMcpOptions.Default.Path);
    public static readonly IdaTcpHub TcpHub = new(TcpOptions, TargetRegistry);
    public static readonly HttpMcpServer McpServer = new(McpOptions, ToolCatalog, ToolHandler);
    public static readonly RepositoryPaths RepositoryPaths = RepositoryPaths.Discover();
    public static readonly PluginInstallService PluginInstallService = new(RepositoryPaths);
    public static readonly AgentConfigService AgentConfigService = new(RepositoryPaths, McpServer.Url);

    private static readonly CancellationTokenSource Shutdown = new();
    private static Task? _tcpTask;
    private static Task? _mcpTask;

    public static bool IsRunning => _tcpTask is not null && _mcpTask is not null;
    public static string TcpEndpoint => $"{TcpOptions.Host}:{TcpOptions.Port}";
    public static string McpEndpoint => McpServer.Url;

    public static void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _tcpTask = TcpHub.RunAsync(Shutdown.Token);
        _mcpTask = McpServer.RunAsync(Shutdown.Token);
    }

    public static async ValueTask DisposeAsync()
    {
        Shutdown.Cancel();
        await TcpHub.DisposeAsync().ConfigureAwait(false);
        await McpServer.DisposeAsync().ConfigureAwait(false);
    }
}
