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
            _targets[metadata.InstanceId] = new TargetInfo(
                metadata.InstanceId,
                metadata.Alias,
                metadata.ProcessId,
                metadata.BinaryName,
                metadata.InputPath,
                metadata.DatabasePath,
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
}
