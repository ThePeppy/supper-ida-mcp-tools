namespace SupperIdaMcp.Center.Ida;

public sealed record IdaLaunchRequest(
    string InputPath,
    string? IdaPath,
    IReadOnlyList<string> Arguments);
