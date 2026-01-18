namespace MinimalWorker.Test.Fakes;

/// <summary>
/// Test service interface A.
/// </summary>
public interface IServiceA
{
    void Execute();
}

/// <summary>
/// Test service interface B.
/// </summary>
public interface IServiceB
{
    void Execute();
}

/// <summary>
/// Test service interface C.
/// </summary>
public interface IServiceC
{
    void Execute();
}

/// <summary>
/// Implementation of IServiceA with execution counter.
/// </summary>
public class ServiceA : IServiceA
{
    private int _executeCount;

    public int ExecuteCount => _executeCount;

    public void Execute()
    {
        Interlocked.Increment(ref _executeCount);
    }
}

/// <summary>
/// Implementation of IServiceB with execution counter.
/// </summary>
public class ServiceB : IServiceB
{
    private int _executeCount;

    public int ExecuteCount => _executeCount;

    public void Execute()
    {
        Interlocked.Increment(ref _executeCount);
    }
}

/// <summary>
/// Implementation of IServiceC with execution counter.
/// </summary>
public class ServiceC : IServiceC
{
    private int _executeCount;

    public int ExecuteCount => _executeCount;

    public void Execute()
    {
        Interlocked.Increment(ref _executeCount);
    }
}
