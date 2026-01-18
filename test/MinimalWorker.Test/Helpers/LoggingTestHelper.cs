using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MinimalWorker.Test.Helpers;

/// <summary>
/// Test logger provider to capture log messages for verification.
/// Thread-safe for use with concurrent workers.
/// </summary>
public class TestLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentBag<string> _messages;

    public TestLoggerProvider(ConcurrentBag<string> messages)
    {
        _messages = messages;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(_messages);
    }

    public void Dispose() { }
}

/// <summary>
/// Test logger that captures all log messages to a thread-safe collection.
/// </summary>
public class TestLogger : ILogger
{
    private readonly ConcurrentBag<string> _messages;

    public TestLogger(ConcurrentBag<string> messages)
    {
        _messages = messages;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (exception != null)
        {
            message += $" Exception: {exception.Message}";
        }
        _messages.Add(message);
    }
}
