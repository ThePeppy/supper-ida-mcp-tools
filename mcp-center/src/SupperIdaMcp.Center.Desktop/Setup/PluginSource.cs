namespace SupperIdaMcp.Center.Desktop.Setup;

internal enum PluginSourceKind
{
    Repository,
    Bundled
}

internal sealed record PluginSource(string Root, PluginSourceKind Kind)
{
    public string PackagePath => Path.Combine(Root, PluginInstallService.PackageName);
}

internal static class PluginSourceResolver
{
    private static readonly string BundleSourceRoot = Path.Combine("PluginBundle", "ida-plugin", "src");

    public static PluginSource? Resolve(RepositoryPaths paths)
    {
        if (IsValidSourceRoot(paths.IdaPluginSourceRoot))
        {
            return new PluginSource(paths.IdaPluginSourceRoot!, PluginSourceKind.Repository);
        }

        foreach (var candidate in CandidateBundleRoots())
        {
            if (IsValidSourceRoot(candidate))
            {
                return new PluginSource(candidate, PluginSourceKind.Bundled);
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateBundleRoots()
    {
        yield return Path.Combine(AppContext.BaseDirectory, BundleSourceRoot);
        yield return Path.Combine(Environment.CurrentDirectory, BundleSourceRoot);
    }

    private static bool IsValidSourceRoot(string? sourceRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            return false;
        }

        return Directory.Exists(Path.Combine(sourceRoot, PluginInstallService.PackageName))
            && File.Exists(Path.Combine(sourceRoot, PluginInstallService.PackageName, "entry.py"));
    }
}
