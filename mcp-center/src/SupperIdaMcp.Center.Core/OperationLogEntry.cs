namespace SupperIdaMcp.Center.Core;

public sealed record OperationLogEntry(
    string EntryId,
    DateTimeOffset TimestampUtc,
    string TargetInstanceId,
    string TargetAlias,
    string ToolName,
    string? McpToolName,
    bool Success,
    TimeSpan Elapsed,
    string? Error,
    string? RequestJson,
    string? ResponseJson);
