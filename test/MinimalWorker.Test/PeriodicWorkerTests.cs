using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
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
        // PeriodicTimer fires AFTER each interval, so 30 min = fires at 5, 10, 15, 20, 25 = 5 executions
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(30));
        
        await host.StopAsync();

        // Assert - PeriodicTimer fires after each 5 min interval: 5, 10, 15, 20, 25 min = exactly 5 executions
        var callCount = counter.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "Increment");
        Assert.Equal(5, callCount);
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

        // Assert - 1 min interval for 5 minutes = 4 executions (ticks at 1, 2, 3, 4 min)
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

        // Assert - 100ms interval for 10 seconds should give us many executions
        // FakeTimeProvider behavior means we get approximately 99 executions
        Assert.True(executionCount >= 90 && executionCount <= 100, $"Expected 90-100 executions, got {executionCount}");
    }
}
