namespace SupperIdaMcp.Center.Desktop.Setup;

internal sealed record PluginInstallStatus(
    string PluginsDirectory,
    string LoaderPath,
    string? SourceRoot,
    bool IsInstalled,
    bool IsOurs,
    bool IsCompatible,
    string? InstalledVersion,
    string ExpectedVersion,
    string Message);
