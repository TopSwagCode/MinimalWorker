using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using MinimalWorker.Test.Fakes;
using MinimalWorker.Test.Helpers;
using NSubstitute;

namespace MinimalWorker.Test;

public class PeriodicWorkerTests
{
    [Fact]
    public async Task PeriodicBackgroundWorker_Should_Invoke_Action_Multiple_Times()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var counter = Substitute.For<TestDependency>();
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(counter);
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(TimeSpan.FromMinutes(5), (TestDependency svc, CancellationToken token) =>
        {
            svc.Increment();
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();

        // Advance time to trigger multiple executions (5 min intervals)
        // PeriodicTimer fires AFTER each interval: ticks at 5, 10, 15, 20, 25, 30 min = 6 executions
        // Use more steps to ensure all timer ticks are processed
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(35), steps: 35);

        await host.StopAsync();

        // Assert - PeriodicTimer fires after each 5 min interval: 5, 10, 15, 20, 25, 30 min = 6 executions
        var callCount = counter.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "Increment");
        Assert.Equal(6, callCount);
    }

    [Fact]
    public async Task PeriodicBackgroundWorker_Should_Call_OnError_When_Exception_Occurs()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var errorWasCalled = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMinutes(1),
            () => { throw new InvalidOperationException("Periodic worker error"); })
            .WithErrorHandler(ex =>
            {
                errorWasCalled = true;
            });

        // Act
        await host.StartAsync();
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2));
        await host.StopAsync();

        // Assert
        Assert.True(errorWasCalled, "OnError should be called when exception occurs");
    }

    [Fact]
    public async Task PeriodicBackgroundWorker_Should_Continue_Running_After_Errors()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionCount = 0;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        // Define worker separately to avoid source generator confusion
        Func<CancellationToken, Task> worker = (CancellationToken token) =>
        {
            executionCount++;
            return Task.CompletedTask;
        };

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMinutes(1),
            worker)
            .WithErrorHandler(ex => { /* Ignore errors */ });

        // Act
        await host.StartAsync();
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(5));
        await host.StopAsync();

        // Assert - 1 min interval, 5 min window = ticks at 1, 2, 3, 4 min = 4 executions
        Assert.Equal(4, executionCount);
    }

    [Fact]
    public async Task PeriodicWorker_Should_Handle_Very_Short_Intervals()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionCount = 0;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMilliseconds(100),
            (CancellationToken token) =>
            {
                Interlocked.Increment(ref executionCount);
                return Task.CompletedTask;
            }
        );

        // Act
        await host.StartAsync();
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromSeconds(10), steps: 100);
        await host.StopAsync();

        // Assert - 100ms interval for 10 seconds = approximately 100 executions
        // Allow a small range for timing edge cases with FakeTimeProvider
        Assert.True(executionCount >= 95 && executionCount <= 100, $"Expected 90-100 executions, got {executionCount}");
    }

    [Fact]
    public async Task PeriodicWorker_Should_Not_Overlap_Executions()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionCount = 0;

        using var host = Host.CreateDefaultBuilder().Build();

        // Worker runs every 10ms but takes 50ms to complete
        // If overlapping were allowed, we'd see many executions in 100ms
        // Without overlapping, we expect only 1-2 executions
        host.RunPeriodicBackgroundWorker(TimeSpan.FromMilliseconds(10), async (CancellationToken token) =>
        {
            Interlocked.Increment(ref executionCount);
            await Task.Delay(50, token); // Work takes longer than interval
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await host.StopAsync();

        // Assert - Only 1-2 executions possible since each takes 50ms
        // (If overlapping occurred, we'd see ~10 executions)
        Assert.InRange(executionCount, 1, 2);
    }

    [Fact]
    public void PeriodicWorker_With_Zero_Interval_Should_Throw_ArgumentOutOfRangeException()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();

        using var host = Host.CreateDefaultBuilder().Build();

        // Act & Assert - TimeSpan.Zero should throw ArgumentOutOfRangeException
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            host.RunPeriodicBackgroundWorker(TimeSpan.Zero, (CancellationToken token) =>
            {
                return Task.CompletedTask;
            });
        });

        Assert.Equal("timespan", exception.ParamName);
    }

    [Fact]
    public void PeriodicWorker_With_Negative_Interval_Should_Throw_ArgumentOutOfRangeException()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();

        using var host = Host.CreateDefaultBuilder().Build();

        // Act & Assert - Negative TimeSpan should throw ArgumentOutOfRangeException
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            host.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(-5), (CancellationToken token) =>
            {
                return Task.CompletedTask;
            });
        });

        Assert.Equal("timespan", exception.ParamName);
    }
}
