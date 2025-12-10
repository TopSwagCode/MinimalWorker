namespace MinimalWorker.Test;

public interface TestDependency
{
    void Increment();
    void Decrement();
}


public interface IADependency
{
    void Increment();
    void Decrement();
}



public interface IBDependency
{
    void Increment();
    void Decrement();
}

public class ADependency : IADependency
{
    private readonly IBDependency _bDependency;

    public ADependency(IBDependency bDependency)
    {
        _bDependency = bDependency;
    }
    
    public void Increment()
    {
        _bDependency.Increment();
    }

    public void Decrement()
    {
        _bDependency.Decrement();
    }
}

public class BDependency : IBDependency
{
    public int IncrementCount { get; private set; }
    public int DecrementCount { get; private set; }
    
    public void Increment()
    {
        IncrementCount++;
    }

    public void Decrement()
    {
        DecrementCount++;
    }
}

public interface IServiceA
{
    void Execute();
}

public interface IServiceB
{
    void Execute();
}

public interface IServiceC
{
    void Execute();
}

public class ServiceA : IServiceA
{
    public int ExecuteCount { get; private set; }
    
    public void Execute()
    {
        ExecuteCount++;
    }
}

public class ServiceB : IServiceB
{
    public int ExecuteCount { get; private set; }
    
    public void Execute()
    {
        ExecuteCount++;
    }
}

public class ServiceC : IServiceC
{
    public int ExecuteCount { get; private set; }
    
    public void Execute()
    {
        ExecuteCount++;
    }
}

// Helper classes for additional edge case tests

public interface IScopedService
{
    Guid Id { get; }
}

public class ScopedService : IScopedService
{
    public Guid Id { get; } = Guid.NewGuid();
}

public class ThreadSafeCounter
{
    private int _count;
    public int Count => _count;
    
    public void Increment()
    {
        Interlocked.Increment(ref _count);
    }
}

public interface IRepository<T>
{
    Task<T> GetAsync();
}

public class StringRepository : IRepository<string>
{
    private int _counter;
    
    public Task<string> GetAsync()
    {
        var count = Interlocked.Increment(ref _counter);
        return Task.FromResult($"Item_{count}");
    }
}

// Used for testing fail-fast behavior when dependencies are missing
public interface IUnregisteredService
{
    void DoWork();
}

public class WorkerSettings
{
    public bool Enabled { get; set; }
    public int Interval { get; set; }
}
