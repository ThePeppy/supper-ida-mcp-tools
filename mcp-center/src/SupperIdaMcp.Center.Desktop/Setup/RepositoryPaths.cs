namespace SupperIdaMcp.Center.Desktop.Setup;

internal sealed class RepositoryPaths
{
    public string? Root { get; }
    public string? IdaPluginSourceRoot { get; }
    public string? BridgeProjectPath { get; }

    private RepositoryPaths(string? root)
    {
        Root = root;
        if (root is null)
        {
            return;
        }

        IdaPluginSourceRoot = Path.Combine(root, "ida-plugin", "src");
        BridgeProjectPath = Path.Combine(
            root,
            "mcp-center",
            "src",
            "SupperIdaMcp.Center.Bridge",
            "SupperIdaMcp.Center.Bridge.csproj");
    }

    public static RepositoryPaths Discover()
    {
        foreach (var start in CandidateStarts())
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                var pluginMarker = Path.Combine(current.FullName, "ida-plugin", "src", "supper_ida_plugin");
                var bridgeProject = Path.Combine(
                    current.FullName,
                    "mcp-center",
                    "src",
                    "SupperIdaMcp.Center.Bridge",
                    "SupperIdaMcp.Center.Bridge.csproj");
                if (Directory.Exists(pluginMarker) && File.Exists(bridgeProject))
                {
                    return new RepositoryPaths(current.FullName);
                }

                current = current.Parent;
            }
        }

        return new RepositoryPaths(null);
    }

    private static IEnumerable<string> CandidateStarts()
    {
        yield return Environment.CurrentDirectory;
        yield return AppContext.BaseDirectory;
    }
}
