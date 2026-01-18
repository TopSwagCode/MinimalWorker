namespace MinimalWorker.Test.Fakes;

/// <summary>
/// Generic repository interface for testing generic service resolution.
/// </summary>
public interface IRepository<T>
{
    Task<T> GetAsync();
}

/// <summary>
/// String repository implementation for testing generic DI.
/// </summary>
public class StringRepository : IRepository<string>
{
    private int _counter;

    public Task<string> GetAsync()
    {
        var count = Interlocked.Increment(ref _counter);
        return Task.FromResult($"Item_{count}");
    }
}

/// <summary>
/// Interface for testing fail-fast behavior when dependencies are missing.
/// This should NOT be registered in DI to test missing dependency detection.
/// </summary>
public interface IUnregisteredService
{
    void DoWork();
}
