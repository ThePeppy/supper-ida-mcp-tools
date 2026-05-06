namespace SupperIdaMcp.Center.TcpHub;

public sealed record IdaTcpHubOptions(string Host, int Port)
{
    public static IdaTcpHubOptions Default { get; } = new("127.0.0.1", 9399);
}
