using SupperIdaMcp.Center.Core;

namespace SupperIdaMcp.Center.TcpHub;

public sealed class TargetRegistry
{
    private readonly Dictionary<string, TargetInfo> _targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IdaClientConnection> _connections = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public IReadOnlyCollection<TargetInfo> ListTargets()
    {
        lock (_gate)
        {
            return _targets.Values.ToArray();
        }
    }

    public bool TryGetTarget(string instanceId, out TargetInfo? target)
    {
        lock (_gate)
        {
            return _targets.TryGetValue(instanceId, out target);
        }
    }

    public bool TryGetConnection(string instanceId, out IdaClientConnection? connection)
    {
        lock (_gate)
        {
            return _connections.TryGetValue(instanceId, out connection);
        }
    }

    public void Upsert(TargetInfo target, IdaClientConnection connection)
    {
        lock (_gate)
        {
            _targets[target.InstanceId] = target;
            _connections[target.InstanceId] = connection;
        }
    }

    public void UpsertMetadata(TargetMetadata metadata, IdaClientConnection? connection, DateTimeOffset timestampUtc)
    {
        lock (_gate)
        {
            _targets.TryGetValue(metadata.InstanceId, out var current);
            var normalized = NormalizeMetadata(metadata, current, timestampUtc);
            _targets[metadata.InstanceId] = new TargetInfo(
                metadata.InstanceId,
                normalized.Alias,
                normalized.ProcessId,
                normalized.BinaryName,
                normalized.InputPath,
                normalized.DatabasePath,
                normalized.IdaVersion,
                normalized.Platform,
                timestampUtc,
                TargetHealth.Healthy);

            if (connection is not null)
            {
                _connections[metadata.InstanceId] = connection;
            }
        }
    }

    public bool Remove(string instanceId, IdaClientConnection? connection = null)
    {
        lock (_gate)
        {
            if (connection is not null
                && _connections.TryGetValue(instanceId, out var currentConnection)
                && !ReferenceEquals(currentConnection, connection))
            {
                return false;
            }

            _connections.Remove(instanceId);
            return _targets.Remove(instanceId);
        }
    }

    public void MarkHeartbeat(string instanceId, DateTimeOffset timestampUtc)
    {
        lock (_gate)
        {
            if (!_targets.TryGetValue(instanceId, out var target))
            {
                return;
            }

            _targets[instanceId] = target with
            {
                LastSeenUtc = timestampUtc,
                Health = TargetHealth.Healthy
            };
        }
    }

    private static TargetMetadata NormalizeMetadata(TargetMetadata incoming, TargetInfo? current, DateTimeOffset timestampUtc)
    {
        var processId = incoming.ProcessId > 0 ? incoming.ProcessId : current?.ProcessId ?? 0;
        var inputPath = FirstUseful(incoming.InputPath, current?.InputPath);
        var databasePath = FirstUseful(incoming.DatabasePath, current?.DatabasePath);
        var binaryName = FirstUseful(
            IsFallbackName(incoming.BinaryName, processId) ? null : incoming.BinaryName,
            FileNameFromPath(inputPath),
            FileNameFromPath(databasePath),
            IsFallbackName(current?.BinaryName, processId) ? null : current?.BinaryName,
            processId > 0 ? $"ida-{processId}" : null) ?? "ida";
        var alias = FirstUseful(
            IsFallbackName(incoming.Alias, processId) ? null : incoming.Alias,
            AliasFromName(binaryName),
            IsFallbackName(current?.Alias, processId) ? null : current?.Alias,
            processId > 0 ? $"ida-{processId}" : null) ?? "ida";
        var idaVersion = FirstUseful(incoming.IdaVersion, current?.IdaVersion);
        var platform = FirstUseful(incoming.Platform, current?.Platform);

        return new TargetMetadata(
            incoming.InstanceId,
            alias,
            processId,
            binaryName,
            inputPath,
            databasePath,
            idaVersion,
            platform);
    }

    private static string? FirstUseful(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsFallbackName(string? value, int processId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return processId > 0 && value.Equals($"ida-{processId}", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FileNameFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalized = path.Replace('\\', '/');
        var fileName = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }

    private static string AliasFromName(string name)
    {
        var fileName = FileNameFromPath(name) ?? name;
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var source = string.IsNullOrWhiteSpace(withoutExtension) ? fileName : withoutExtension;
        var alias = new string(source
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
            .ToArray())
            .Trim('_');
        return string.IsNullOrWhiteSpace(alias) ? "ida" : alias;
    }
}
