namespace SupperIdaMcp.Center.Dashboard;

public sealed record DashboardOptions(string Host, int Port)
{
    public static DashboardOptions Default { get; } = new("127.0.0.1", 9400);
}
