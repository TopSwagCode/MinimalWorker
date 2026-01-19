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
/// Generic interface with multiple type parameters to test signature matching.
/// This simulates interfaces like Kafka's IConsumer&lt;TKey, TValue&gt;.
/// </summary>
public interface IMultipleConsumer<TKey, TValue, TExtra>
{
    Task<(TKey Key, TValue Value, TExtra Extra)> ConsumeAsync(CancellationToken token);
}

/// <summary>
/// Implementation of IMultipleConsumer&lt;string, string, string&gt; for testing multi-type-argument generics.
/// </summary>
public class StringStringStringConsumer : IMultipleConsumer<string, string, string>
{
    private int _counter;

    public Task<(string Key, string Value, string Extra)> ConsumeAsync(CancellationToken token)
    {
        var count = Interlocked.Increment(ref _counter);
        return Task.FromResult(($"Key_{count}", $"Value_{count}", $"Extra_{count}"));
    }
}


/// <summary>
/// Generic interface with multiple type parameters to test signature matching.
/// This simulates interfaces like Kafka's IConsumer&lt;TKey, TValue&gt;.
/// </summary>
public interface IConsumer<TKey, TValue>
{
    Task<(TKey Key, TValue Value)> ConsumeAsync(CancellationToken token);
}

/// <summary>
/// Implementation of IConsumer&lt;string, string&gt; for testing multi-type-argument generics.
/// </summary>
public class StringStringConsumer : IConsumer<string, string>
{
    private int _counter;

    public Task<(string Key, string Value)> ConsumeAsync(CancellationToken token)
    {
        var count = Interlocked.Increment(ref _counter);
        return Task.FromResult(($"Key_{count}", $"Value_{count}"));
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
