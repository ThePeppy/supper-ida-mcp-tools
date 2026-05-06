namespace SupperIdaMcp.Center.Ida;

public sealed record LaunchedIdaProcess(
    int ProcessId,
    string ExecutablePath,
    string InputPath,
    DateTimeOffset LaunchedAtUtc,
    bool HasExited);
