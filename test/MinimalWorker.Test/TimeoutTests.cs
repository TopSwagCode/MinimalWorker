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

        // Note: Timeout uses real system time, not FakeTimeProvider
        using var host = Host.CreateDefaultBuilder().Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMilliseconds(10),
            async (CancellationToken token) =>
            {
                // This simulates a long-running task that exceeds the timeout
                await Task.Delay(TimeSpan.FromSeconds(30), token);
            })
            .WithTimeout(TimeSpan.FromMilliseconds(50))
            .WithErrorHandler(ex =>
            {
                if (ex is TimeoutException)
                {
                    timeoutOccurred = true;
                }
            });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Allow timeout to occur
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

        // Every minute - uses FakeTimeProvider for scheduling
        // but timeout uses real time
        host.RunCronBackgroundWorker(
            "* * * * *",
            async (CancellationToken token) =>
            {
                // Long-running task - timeout will use real time
                await Task.Delay(TimeSpan.FromSeconds(30), token);
            })
            .WithTimeout(TimeSpan.FromMilliseconds(50))
            .WithErrorHandler(ex =>
            {
                if (ex is TimeoutException)
                {
                    timeoutOccurred = true;
                }
            });

        // Act
        await host.StartAsync();
        // Advance fake time to trigger cron execution
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2), steps: 24);
        // Give real time for timeout to occur
        await Task.Delay(200);
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

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                // Long-running task that exceeds timeout
                await Task.Delay(TimeSpan.FromSeconds(30), token);
            })
            .WithTimeout(TimeSpan.FromMilliseconds(50))
            .WithErrorHandler(ex =>
            {
                if (ex is TimeoutException)
                {
                    timeoutOccurred = true;
                }
            });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Allow time for timeout to occur
        await host.StopAsync();

        // Assert
        Assert.True(timeoutOccurred, "Timeout should have occurred for long-running continuous worker");
    }
}
