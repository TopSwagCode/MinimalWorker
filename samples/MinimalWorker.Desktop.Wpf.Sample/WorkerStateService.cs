using System.Collections.Concurrent;
using System.Diagnostics;

namespace MinimalWorker.Desktop.Wpf.Sample;

public class WorkerStateService
{
    private readonly ConcurrentDictionary<string, WorkerInfo> _workers = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public TimeSpan Uptime => _stopwatch.Elapsed;

    /// <summary>
    /// Fired on any worker update. The string argument is the worker name.
    /// </summary>
    public event Action<string>? StateChanged;

    public WorkerInfo GetOrAdd(string workerName) =>
        _workers.GetOrAdd(workerName, name => new WorkerInfo(name));

    public void RecordExecution(string workerName, string message)
    {
        var info = GetOrAdd(workerName);
        info.Record(message);
        StateChanged?.Invoke(workerName);
    }
}
