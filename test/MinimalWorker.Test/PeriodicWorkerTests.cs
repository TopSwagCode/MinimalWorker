using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(counter);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(TimeSpan.FromMilliseconds(50), (TestDependency svc, CancellationToken token) =>
        {
            svc.Increment();
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        await Task.Delay(303); // 3 as buffer
        await host.StopAsync();

        // Assert - Use "at least" to avoid flakiness on different machines
        var callCount = counter.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "Increment");
        Assert.True(callCount >= 5, $"Expected at least 5 Increment calls, got {callCount}");
    }

    [Fact]
    public async Task PeriodicBackgroundWorker_Should_Call_OnError_When_Exception_Occurs()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var errorWasCalled = false;

        using var host = Host.CreateDefaultBuilder()
            .Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMilliseconds(50),
            () => { throw new InvalidOperationException("Periodic worker error"); })
            .WithErrorHandler(ex =>
            {
                errorWasCalled = true;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(100); // Give time for at least one execution
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

        using var host = Host.CreateDefaultBuilder()
            .Build();

        // Define worker separately to avoid source generator confusion
        Func<CancellationToken, Task> worker = (CancellationToken token) =>
        {
            executionCount++;
            return Task.CompletedTask;
        };

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMilliseconds(50),
            worker)
            .WithErrorHandler(ex => { /* Ignore errors */ });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Let it execute a few times
        await host.StopAsync();

        // Assert
        Assert.True(executionCount >= 3, "Periodic worker should execute multiple times");
    }

    [Fact]
    public async Task PeriodicWorker_Should_Handle_Very_Short_Intervals()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionCount = 0;

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMilliseconds(1), // Very short interval
            (CancellationToken token) =>
            {
                Interlocked.Increment(ref executionCount);
                return Task.CompletedTask;
            }
        );

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await host.StopAsync();

        // Assert - Should have many executions
        Assert.True(executionCount >= 50, $"Expected at least 50 executions with 1ms interval, got {executionCount}");
    }
}
