namespace SupperIdaMcp.Center.Core;

public sealed record OperationLogEntry(
    DateTimeOffset TimestampUtc,
    string TargetAlias,
    string ToolName,
    bool Success,
    TimeSpan Elapsed,
    string? Error);
