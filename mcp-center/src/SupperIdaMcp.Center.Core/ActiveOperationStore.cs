namespace SupperIdaMcp.Center.Core;

public sealed class ActiveOperationStore
{
    private readonly Dictionary<string, ActiveOperation> _operations = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public ActiveOperation Begin(string targetInstanceId, string targetAlias, string toolName)
    {
        var operation = new ActiveOperation(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            targetInstanceId,
            targetAlias,
            toolName);

        lock (_gate)
        {
            _operations[operation.OperationId] = operation;
        }

        return operation;
    }

    public void End(string operationId)
    {
        lock (_gate)
        {
            _operations.Remove(operationId);
        }
    }

    public IReadOnlyCollection<ActiveOperation> List()
    {
        lock (_gate)
        {
            return _operations.Values
                .OrderBy(operation => operation.StartedAtUtc)
                .ToArray();
        }
    }
}
