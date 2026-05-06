namespace SupperIdaMcp.Center.Ida;

public sealed class IdaProcessRegistry
{
    private readonly Dictionary<int, LaunchedIdaProcess> _processes = [];
    private readonly object _gate = new();

    public void Upsert(LaunchedIdaProcess process)
    {
        lock (_gate)
        {
            _processes[process.ProcessId] = process;
        }
    }

    public IReadOnlyCollection<LaunchedIdaProcess> List()
    {
        lock (_gate)
        {
            return _processes.Values.ToArray();
        }
    }
}
