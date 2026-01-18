namespace MinimalWorker.Test.Fakes;

/// <summary>
/// Test dependency interface for mocking with NSubstitute.
/// </summary>
public interface TestDependency
{
    void Increment();
    void Decrement();
}

/// <summary>
/// Interface for testing nested dependency chains.
/// </summary>
public interface IADependency
{
    void Increment();
    void Decrement();
}

/// <summary>
/// Interface for testing nested dependency chains.
/// </summary>
public interface IBDependency
{
    void Increment();
    void Decrement();
}

/// <summary>
/// Implementation that delegates to IBDependency for testing nested DI.
/// </summary>
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

/// <summary>
/// Leaf dependency with counters for verifying call chains.
/// </summary>
public class BDependency : IBDependency
{
    private int _incrementCount;
    private int _decrementCount;

    public int IncrementCount => _incrementCount;
    public int DecrementCount => _decrementCount;

    public void Increment() => Interlocked.Increment(ref _incrementCount);
    public void Decrement() => Interlocked.Increment(ref _decrementCount);
}
