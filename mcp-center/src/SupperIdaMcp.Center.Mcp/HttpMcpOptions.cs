namespace SupperIdaMcp.Center.Mcp;

public sealed record HttpMcpOptions(string Host, int Port, string Path)
{
    public static HttpMcpOptions Default { get; } = new("127.0.0.1", 9401, "/mcp");
}
