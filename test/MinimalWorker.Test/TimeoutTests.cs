using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalWorker.Test.Helpers;

namespace MinimalWorker.Test;

public class TimeoutTests
{
    [Fact]
    public async Task PeriodicWorker_WithTimeout_Should_Cancel_Long_Running_Execution()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var timeoutOccurred = false;
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
                // This simulates a long-running task that exceeds the timeout
                await timeProvider.Delay(TimeSpan.FromMinutes(30), token);
            })
            .WithTimeout(TimeSpan.FromMinutes(2))
            .WithErrorHandler(ex =>
            {
                if (ex is TimeoutException)
                {
                    timeoutOccurred = true;
                }
            });

        // Act
        await host.StartAsync();
        // Advance time: 1 min for periodic tick, then 2+ min for timeout to trigger
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(4), steps: 8);
        await host.StopAsync();

        // Assert
        Assert.True(timeoutOccurred, "Timeout should have occurred for long-running execution");
    }

    [Fact]
    public async Task PeriodicWorker_WithTimeout_Should_Not_Timeout_Fast_Execution()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionCount = 0;
        var errorCount = 0;
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
                Interlocked.Increment(ref executionCount);
                return Task.CompletedTask; // Fast execution
            })
            .WithTimeout(TimeSpan.FromMinutes(5))
            .WithErrorHandler(ex =>
            {
                Interlocked.Increment(ref errorCount);
            });

        // Act
        await host.StartAsync();
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(5), steps: 10);
        await host.StopAsync();

        // Assert
        Assert.True(executionCount >= 4, $"Expected at least 4 executions, got {executionCount}");
        Assert.Equal(0, errorCount);
    }

    [Fact]
    public void PeriodicWorker_WithTimeout_Should_Throw_For_Zero_Timeout()
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
                .WithTimeout(TimeSpan.Zero);
        });

        Assert.Equal("timeout", exception.ParamName);
    }

    [Fact]
    public void PeriodicWorker_WithTimeout_Should_Throw_For_Negative_Timeout()
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
                .WithTimeout(TimeSpan.FromSeconds(-10));
        });

        Assert.Equal("timeout", exception.ParamName);
    }

    [Fact]
    public async Task CronWorker_WithTimeout_Should_Cancel_Long_Running_Execution()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var timeoutOccurred = false;
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
            async (CancellationToken token) =>
            {
                // Long-running task that will timeout
                await timeProvider.Delay(TimeSpan.FromMinutes(30), token);
            })
            .WithTimeout(TimeSpan.FromMinutes(2))
            .WithErrorHandler(ex =>
            {
                if (ex is TimeoutException)
                {
                    timeoutOccurred = true;
                }
            });

        // Act
        await host.StartAsync();
        // Advance time: 1 min for cron trigger, then 2+ min for timeout
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(4), steps: 8);
        await host.StopAsync();

        // Assert
        Assert.True(timeoutOccurred, "Timeout should have occurred for long-running cron execution");
    }

    [Fact]
    public async Task ContinuousWorker_WithTimeout_Should_Cancel_Long_Running_Execution()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var timeoutOccurred = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                // Long-running task that exceeds timeout
                await timeProvider.Delay(TimeSpan.FromMinutes(30), token);
            })
            .WithTimeout(TimeSpan.FromMinutes(2))
            .WithErrorHandler(ex =>
            {
                if (ex is TimeoutException)
                {
                    timeoutOccurred = true;
                }
            });

        // Act
        await host.StartAsync();
        // Advance time past the timeout
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(3), steps: 6);
        await host.StopAsync();

        // Assert
        Assert.True(timeoutOccurred, "Timeout should have occurred for long-running continuous worker");
    }
}
