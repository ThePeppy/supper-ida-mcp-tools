namespace SupperIdaMcp.Center.Ida;

public sealed record IdaInstall(
    string Path,
    string DisplayName,
    string Platform,
    string Source,
    bool Exists);
