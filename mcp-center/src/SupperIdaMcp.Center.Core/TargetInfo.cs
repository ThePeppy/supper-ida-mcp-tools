namespace SupperIdaMcp.Center.Core;

public sealed record TargetInfo(
    string InstanceId,
    string Alias,
    int ProcessId,
    string BinaryName,
    string? InputPath,
    string? DatabasePath,
    DateTimeOffset LastSeenUtc,
    TargetHealth Health);

public sealed record TargetMetadata(
    string InstanceId,
    string Alias,
    int ProcessId,
    string BinaryName,
    string? InputPath,
    string? DatabasePath,
    string? IdaVersion,
    string? Platform);

public enum TargetHealth
{
    Unknown,
    Healthy,
    Unreachable,
    Closing
}
