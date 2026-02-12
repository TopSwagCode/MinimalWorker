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

        // Use real time since retry delay uses Task.Delay
        using var host = Host.CreateDefaultBuilder().Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMilliseconds(10),
            (CancellationToken token) =>
            {
                Interlocked.Increment(ref attemptCount);
                if (attemptCount < 3)
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                return Task.CompletedTask;
            })
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(500); // Allow time for periodic execution and retries
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

        // Use real time since retry delay uses Task.Delay
        using var host = Host.CreateDefaultBuilder().Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMilliseconds(10),
            (CancellationToken token) =>
            {
                Interlocked.Increment(ref attemptCount);
                throw new InvalidOperationException("Always fails");
#pragma warning disable CS0162 // Unreachable code detected
                return Task.CompletedTask;
#pragma warning restore CS0162
            })
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(500); // Allow time for periodic execution and retries
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

        // Use real time since retry delay uses Task.Delay
        using var host = Host.CreateDefaultBuilder().Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMilliseconds(10),
            (CancellationToken token) =>
            {
                Interlocked.Increment(ref attemptCount);
                throw new InvalidOperationException("Always fails");
#pragma warning disable CS0162 // Unreachable code detected
                return Task.CompletedTask;
#pragma warning restore CS0162
            })
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(500); // Allow time for execution and retries
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

        // Every minute - FakeTimeProvider controls cron scheduling
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
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        // Advance fake time to trigger cron, then give real time for retries
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2), steps: 24);
        await Task.Delay(200); // Real time for retry delays
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

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker((CancellationToken token) =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                return Task.CompletedTask;
            })
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Allow time for retries
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

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker((CancellationToken token) =>
            {
                Interlocked.Increment(ref attemptCount);
                throw new InvalidOperationException("Always fails");
#pragma warning disable CS0162 // Unreachable code detected
                return Task.CompletedTask;
#pragma warning restore CS0162
            })
            .WithRetry(maxAttempts: 2, delay: TimeSpan.FromMilliseconds(10))
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(500); // Allow more time for retries
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

        // Use real time for this test since timeout uses real time
        using var host = Host.CreateDefaultBuilder().Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMilliseconds(10),
            async (CancellationToken token) =>
            {
                attemptCount++;
                // Simulate long-running task that will timeout
                await Task.Delay(TimeSpan.FromSeconds(30), token);
            })
            .WithTimeout(TimeSpan.FromMilliseconds(50))
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(10))
            .WithErrorHandler(ex =>
            {
                caughtException = ex;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Allow timeout to occur
        await host.StopAsync();

        // Assert - Timeout should not be retried (only 1 attempt per timeout)
        Assert.True(caughtException is TimeoutException, $"Should have caught TimeoutException, got {caughtException?.GetType().Name}");
    }
}
