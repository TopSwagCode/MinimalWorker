namespace MinimalWorker.Desktop.Wpf.Sample;

public class WorkerInfo(string name)
{
    private int _executionCount;

    public string Name { get; } = name;
    public int ExecutionCount => _executionCount;
    public DateTime? LastRunTime { get; private set; }
    public string StatusMessage { get; private set; } = "Waiting...";

    internal void Record(string message)
    {
        Interlocked.Increment(ref _executionCount);
        LastRunTime = DateTime.Now;
        StatusMessage = message;
    }
}
