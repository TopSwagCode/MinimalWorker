namespace MinimalWorker.Test.TestTypes;

/// <summary>
/// Configuration class for testing IOptions pattern.
/// </summary>
public class WorkerSettings
{
    public bool Enabled { get; set; }
    public int Interval { get; set; }
}
