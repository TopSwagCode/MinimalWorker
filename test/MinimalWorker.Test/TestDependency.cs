using Microsoft.Extensions.Time.Testing;

namespace MinimalWorker.Test;

public interface TestDependency
{
    void Increment();
    void Decrement();
}

/// <summary>
/// Helper class for testing workers with FakeTimeProvider.
/// Advances time automatically to trigger periodic and cron workers without real delays.
/// </summary>
public static class WorkerTestHelper
{
    /// <summary>
    /// Creates a FakeTimeProvider with a fixed start time.
    /// </summary>
    public static FakeTimeProvider CreateTimeProvider()
    {
        return new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// Advances time and allows async work to proceed.
    /// </summary>
    public static async Task AdvanceTimeAsync(FakeTimeProvider timeProvider, TimeSpan amount, int steps = 10)
    {
        var stepSize = TimeSpan.FromTicks(amount.Ticks / steps);
        for (int i = 0; i < steps; i++)
        {
            timeProvider.Advance(stepSize);
            await Task.Delay(1); // Allow async continuations to run
        }
    }
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
    private int _incrementCount;
    private int _decrementCount;

    public int IncrementCount => _incrementCount;
    public int DecrementCount => _decrementCount;

    public void Increment() => Interlocked.Increment(ref _incrementCount);
    public void Decrement() => Interlocked.Increment(ref _decrementCount);
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

// Test logger to capture log messages (thread-safe)
public class TestLoggerProvider : Microsoft.Extensions.Logging.ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentBag<string> _messages;

    public TestLoggerProvider(System.Collections.Concurrent.ConcurrentBag<string> messages)
    {
        _messages = messages;
    }

    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(_messages);
    }

    public void Dispose() { }
}

public class TestLogger : Microsoft.Extensions.Logging.ILogger
{
    private readonly System.Collections.Concurrent.ConcurrentBag<string> _messages;

    public TestLogger(System.Collections.Concurrent.ConcurrentBag<string> messages)
    {
        _messages = messages;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (exception != null)
        {
            message += $" Exception: {exception.Message}";
        }
        _messages.Add(message);
    }
}
