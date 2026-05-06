using SupperIdaMcp.Center.Core;

namespace SupperIdaMcp.Center.TcpHub;

public sealed class TargetRegistry
{
    private readonly Dictionary<string, TargetInfo> _targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public IReadOnlyCollection<TargetInfo> ListTargets()
    {
        lock (_gate)
        {
            return _targets.Values.ToArray();
        }
    }

    public void Upsert(TargetInfo target)
    {
        lock (_gate)
        {
            _targets[target.InstanceId] = target;
        }
    }

    public bool Remove(string instanceId)
    {
        lock (_gate)
        {
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
