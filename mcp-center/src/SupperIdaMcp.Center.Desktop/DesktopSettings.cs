namespace SupperIdaMcp.Center.Desktop;

internal sealed record DesktopSettings(string Host, int TcpPort, int McpPort)
{
    public static DesktopSettings Current { get; private set; } = new("127.0.0.1", 9399, 9401);

    public static void Configure(string[] args)
    {
        var host = Current.Host;
        var tcpPort = Current.TcpPort;
        var mcpPort = Current.McpPort;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host" when i + 1 < args.Length:
                    host = args[++i];
                    break;
                case "--tcp-port" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedTcpPort):
                    tcpPort = parsedTcpPort;
                    break;
                case "--mcp-port" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedMcpPort):
                    mcpPort = parsedMcpPort;
                    break;
            }
        }

        Current = new DesktopSettings(host, tcpPort, mcpPort);
    }
}
