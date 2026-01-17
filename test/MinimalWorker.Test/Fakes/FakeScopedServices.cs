namespace MinimalWorker.Test.Fakes;

/// <summary>
/// Interface for testing scoped service behavior.
/// Each instance has a unique ID to track scope boundaries.
/// </summary>
public interface IScopedService
{
    Guid Id { get; }
}

/// <summary>
/// Scoped service implementation with unique instance ID.
/// </summary>
public class ScopedService : IScopedService
{
    public Guid Id { get; } = Guid.NewGuid();
}

/// <summary>
/// Thread-safe counter for testing concurrent workers.
/// </summary>
public class ThreadSafeCounter
{
    private int _count;

    public int Count => _count;

    public void Increment()
    {
        Interlocked.Increment(ref _count);
    }

    public void Decrement()
    {
        Interlocked.Decrement(ref _count);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _count, 0);
    }
}

/// <summary>
/// Interface for testing transient service resolution.
/// Each instance should have a unique ID.
/// </summary>
public interface ITransientService
{
    Guid InstanceId { get; }
}

/// <summary>
/// Transient service implementation for testing DI scoping.
/// </summary>
public class TransientService : ITransientService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}
