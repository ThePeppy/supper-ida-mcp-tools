namespace SupperIdaMcp.Center.Core;

public sealed class OperationLogStore
{
    private const int DefaultCapacity = 1_000;
    private readonly Queue<OperationLogEntry> _entries = new();
    private readonly object _gate = new();

    public void Add(OperationLogEntry entry)
    {
        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > DefaultCapacity)
            {
                _entries.Dequeue();
            }
        }
    }

    public IReadOnlyCollection<OperationLogEntry> List(int limit = 100)
    {
        lock (_gate)
        {
            return _entries
                .Reverse()
                .Take(Math.Clamp(limit, 1, DefaultCapacity))
                .ToArray();
        }
    }
}
