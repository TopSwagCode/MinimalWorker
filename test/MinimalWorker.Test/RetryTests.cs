using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalWorker.Test.Helpers;

namespace MinimalWorker.Test;

public class RetryTests
{
    [Fact]
    public async Task PeriodicWorker_WithRetry_Should_Retry_On_Failure()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptCount = 0;
        var errorHandlerCalled = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMinutes(1),
            (CancellationToken token) =>
            {
                Interlocked.Increment(ref attemptCount);
                if (attemptCount < 3)
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                return Task.CompletedTask;
            })
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        // Advance time: 1 min for periodic tick, then enough for retries
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2), steps: 24);
        await host.StopAsync();

        // Assert - Should succeed on 3rd attempt, error handler not called
        Assert.True(attemptCount >= 3, $"Expected at least 3 attempts, got {attemptCount}");
        Assert.False(errorHandlerCalled, "Error handler should not be called when retry succeeds");
    }

    [Fact]
    public async Task PeriodicWorker_WithRetry_Should_Call_ErrorHandler_After_All_Retries_Exhausted()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptCount = 0;
        var errorHandlerCalled = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMinutes(1),
            (CancellationToken token) =>
            {
                Interlocked.Increment(ref attemptCount);
                throw new InvalidOperationException("Always fails");
            })
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        // Advance time: 1 min for periodic tick, then enough for all retries
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2), steps: 24);
        await host.StopAsync();

        // Assert - Should exhaust all retries and call error handler
        Assert.True(attemptCount >= 3, $"Expected at least 3 attempts, got {attemptCount}");
        Assert.True(errorHandlerCalled, "Error handler should be called after all retries exhausted");
    }

    [Fact]
    public void PeriodicWorker_WithRetry_Should_Throw_For_Zero_MaxAttempts()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();

        using var host = Host.CreateDefaultBuilder().Build();

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            host.RunPeriodicBackgroundWorker(
                TimeSpan.FromMinutes(1),
                (CancellationToken token) => Task.CompletedTask)
                .WithRetry(maxAttempts: 0);
        });

        Assert.Equal("maxAttempts", exception.ParamName);
    }

    [Fact]
    public void PeriodicWorker_WithRetry_Should_Throw_For_Negative_Delay()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();

        using var host = Host.CreateDefaultBuilder().Build();

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            host.RunPeriodicBackgroundWorker(
                TimeSpan.FromMinutes(1),
                (CancellationToken token) => Task.CompletedTask)
                .WithRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(-5));
        });

        Assert.Equal("delay", exception.ParamName);
    }

    [Fact]
    public async Task PeriodicWorker_WithRetry_Should_Use_Configured_Attempts()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptCount = 0;
        var errorHandlerCalled = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMinutes(1),
            (CancellationToken token) =>
            {
                Interlocked.Increment(ref attemptCount);
                throw new InvalidOperationException("Always fails");
            })
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        // Advance time for periodic tick and retries
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2), steps: 24);
        await host.StopAsync();

        // Assert - 3 attempts configured
        Assert.True(attemptCount >= 3, $"Expected at least 3 attempts, got {attemptCount}");
        Assert.True(errorHandlerCalled, "Error handler should be called after retries exhausted");
    }

    [Fact]
    public async Task CronWorker_WithRetry_Should_Retry_On_Failure()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptCount = 0;
        var errorHandlerCalled = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        // Every minute
        host.RunCronBackgroundWorker(
            "* * * * *",
            (CancellationToken token) =>
            {
                Interlocked.Increment(ref attemptCount);
                if (attemptCount < 2)
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                return Task.CompletedTask;
            })
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        // Advance time for cron trigger and retries
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2), steps: 24);
        await host.StopAsync();

        // Assert - Should succeed on 2nd attempt
        Assert.True(attemptCount >= 2, $"Expected at least 2 attempts, got {attemptCount}");
        Assert.False(errorHandlerCalled, "Error handler should not be called when retry succeeds");
    }

    [Fact]
    public async Task ContinuousWorker_WithRetry_Should_Retry_On_Failure()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptCount = 0;
        var errorHandlerCalled = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunBackgroundWorker((CancellationToken token) =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                return Task.CompletedTask;
            })
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        // Advance time for retries (2 retries * 10s delay = 20s minimum)
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(1), steps: 12);
        await host.StopAsync();

        // Assert - Should succeed on 3rd attempt
        Assert.True(attemptCount >= 3, $"Expected at least 3 attempts, got {attemptCount}");
        Assert.False(errorHandlerCalled, "Error handler should not be called when retry succeeds");
    }

    [Fact]
    public async Task ContinuousWorker_WithRetry_Should_Call_ErrorHandler_After_All_Retries_Exhausted()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptCount = 0;
        var errorHandlerCalled = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunBackgroundWorker((CancellationToken token) =>
            {
                Interlocked.Increment(ref attemptCount);
                throw new InvalidOperationException("Always fails");
            })
            .WithRetry(maxAttempts: 2, delay: TimeSpan.FromSeconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        // Advance time for retries
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(1), steps: 12);
        await host.StopAsync();

        // Assert
        Assert.True(attemptCount >= 2, $"Expected at least 2 attempts, got {attemptCount}");
        Assert.True(errorHandlerCalled, "Error handler should be called after all retries exhausted");
    }

    [Fact]
    public async Task PeriodicWorker_WithTimeoutAndRetry_Should_Not_Retry_Timeouts()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptCount = 0;
        Exception? caughtException = null;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMinutes(1),
            async (CancellationToken token) =>
            {
                attemptCount++;
                // Simulate long-running task that will timeout
                await timeProvider.Delay(TimeSpan.FromMinutes(30), token);
            })
            .WithTimeout(TimeSpan.FromMinutes(2))
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(10))
            .WithErrorHandler(ex =>
            {
                caughtException = ex;
            });

        // Act
        await host.StartAsync();
        // Advance time: 1 min for periodic tick, then 2+ min for timeout
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(4), steps: 8);
        await host.StopAsync();

        // Assert - Timeout should not be retried (only 1 attempt per timeout)
        Assert.True(caughtException is TimeoutException, $"Should have caught TimeoutException, got {caughtException?.GetType().Name}");
    }

    [Fact]
    public async Task PeriodicWorker_UserThrownOperationCanceledException_Should_Be_Retried_When_Not_Timeout()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptCount = 0;
        var errorHandlerCalled = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMinutes(1),
            async (CancellationToken ct) =>
            {
                Interlocked.Increment(ref attemptCount);
                if (attemptCount < 3)
                {
                    // User code throws OperationCanceledException for its own reasons (not due to timeout)
                    throw new OperationCanceledException("User-initiated cancellation");
                }
                await Task.CompletedTask;
            })
            .WithTimeout(TimeSpan.FromMinutes(10)) // Long timeout that won't trigger
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(5))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        // Advance time: 1 min for periodic tick, then enough for retries
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2), steps: 24);
        await host.StopAsync();

        // Assert - User OperationCanceledException should be retried (not treated as timeout)
        Assert.True(attemptCount >= 3, $"Expected at least 3 attempts (OCE should be retried), got {attemptCount}");
        Assert.False(errorHandlerCalled, "Error handler should not be called when retry succeeds");
    }

    [Fact]
    public async Task PeriodicWorker_UserThrownTimeoutException_Should_Not_Be_Retried_When_Timeout_Configured()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptCount = 0;
        Exception? caughtException = null;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMinutes(1),
            (CancellationToken ct) =>
            {
                Interlocked.Increment(ref attemptCount);
                // User code throws TimeoutException directly (not from framework timeout)
                throw new TimeoutException("User code detected timeout condition");
            })
            .WithTimeout(TimeSpan.FromMinutes(10)) // Long timeout that won't trigger
            .WithRetry(maxAttempts: 5, delay: TimeSpan.FromSeconds(5))
            .WithErrorHandler(ex =>
            {
                caughtException = ex;
            });

        // Act
        await host.StartAsync();
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2), steps: 24);
        await host.StopAsync();

        // Assert - User TimeoutException should NOT be retried when timeout is configured
        Assert.Equal(1, attemptCount); // Only 1 attempt, no retries
        Assert.NotNull(caughtException);
        Assert.IsType<TimeoutException>(caughtException);
        Assert.Equal("User code detected timeout condition", caughtException.Message);
    }

    [Fact]
    public async Task CronWorker_UserThrownOperationCanceledException_Should_Be_Retried_When_Not_Timeout()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptCount = 0;
        var errorHandlerCalled = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        // Every minute
        host.RunCronBackgroundWorker(
            "* * * * *",
            async (CancellationToken ct) =>
            {
                Interlocked.Increment(ref attemptCount);
                if (attemptCount < 2)
                {
                    // User code throws OperationCanceledException for its own reasons
                    throw new OperationCanceledException("User-initiated cancellation");
                }
                await Task.CompletedTask;
            })
            .WithTimeout(TimeSpan.FromMinutes(10))
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(5))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2), steps: 24);
        await host.StopAsync();

        // Assert - User OperationCanceledException should be retried
        Assert.True(attemptCount >= 2, $"Expected at least 2 attempts, got {attemptCount}");
        Assert.False(errorHandlerCalled, "Error handler should not be called when retry succeeds");
    }

    [Fact]
    public async Task ContinuousWorker_UserThrownOperationCanceledException_Should_Be_Retried_When_Not_Timeout()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptCount = 0;
        var errorHandlerCalled = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunBackgroundWorker(async (CancellationToken ct) =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    // User code throws OperationCanceledException for its own reasons
                    throw new OperationCanceledException("User-initiated cancellation");
                }
                await Task.CompletedTask;
            })
            .WithTimeout(TimeSpan.FromMinutes(10))
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(5))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(1), steps: 12);
        await host.StopAsync();

        // Assert - User OperationCanceledException should be retried
        Assert.True(attemptCount >= 3, $"Expected at least 3 attempts, got {attemptCount}");
        Assert.False(errorHandlerCalled, "Error handler should not be called when retry succeeds");
    }

    [Fact]
    public async Task PeriodicWorker_WithRetry_Should_Respect_Delay_Between_Attempts()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var attemptTimestamps = new List<DateTimeOffset>();
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMinutes(1),
            async (CancellationToken ct) =>
            {
                attemptTimestamps.Add(timeProvider.GetUtcNow());
                if (attemptTimestamps.Count < 3)
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                await Task.CompletedTask;
            })
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(30))
            .WithErrorHandler(ex => { });

        // Act
        await host.StartAsync();
        // Advance time: 1 min for periodic tick, then enough for retries with delays
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(3), steps: 36);
        await host.StopAsync();

        // Assert - Verify delay between retry attempts
        Assert.True(attemptTimestamps.Count >= 3, $"Expected at least 3 attempts, got {attemptTimestamps.Count}");

        // Check that there was a delay between attempts
        for (int i = 1; i < Math.Min(attemptTimestamps.Count, 3); i++)
        {
            var gap = attemptTimestamps[i] - attemptTimestamps[i - 1];
            // The gap should be at least the retry delay (30 seconds)
            Assert.True(gap >= TimeSpan.FromSeconds(25),
                $"Expected at least 25s between attempts {i-1} and {i}, got {gap}");
        }
    }
}
