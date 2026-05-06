namespace SupperIdaMcp.Center.Core;

public sealed record ActiveOperation(
    string OperationId,
    DateTimeOffset StartedAtUtc,
    string TargetInstanceId,
    string TargetAlias,
    string ToolName);
