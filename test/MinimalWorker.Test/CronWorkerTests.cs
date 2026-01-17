using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace MinimalWorker.Test;

public class CronWorkerTests
{
    [Fact]
    public async Task CronBackgroundWorker_Should_Invoke_Action_At_Scheduled_Times()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var service = Substitute.For<TestDependency>();
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(service);
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        // Cron expression: every minute
        host.RunCronBackgroundWorker("* * * * *", (TestDependency svc, CancellationToken token) =>
        {
            svc.Increment();
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        
        // Advance time by 3 minutes to trigger 3 executions (at minutes 1, 2, 3)
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(3));
        
        await host.StopAsync();

        // Assert - CRON "* * * * *" fires every minute, so 3 minutes = exactly 3 executions
        var callCount = service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "Increment");
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task CronBackgroundWorker_Should_Execute_Multiple_Times_Over_Period()
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

        // Every 5 minutes
        host.RunCronBackgroundWorker("*/5 * * * *", (CancellationToken token) =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        
        // Advance time by 30 minutes - fires at 5, 10, 15, 20, 25, 30 min = 6 executions
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(30), steps: 30);
        
        await host.StopAsync();

        // Assert - CRON "*/5 * * * *" fires at minutes 5, 10, 15, 20, 25, 30 = exactly 6 executions
        Assert.Equal(6, executionCount);
    }

    [Fact]
    public async Task CronBackgroundWorker_Should_Handle_Hourly_Schedule()
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

        // Every hour at minute 0
        host.RunCronBackgroundWorker("0 * * * *", (CancellationToken token) =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        
        // Advance time by 3 hours - fires at hour 1, 2, 3 = 3 executions
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromHours(3), steps: 36);
        
        await host.StopAsync();

        // Assert - CRON "0 * * * *" fires hourly, so 3 hours = exactly 3 executions
        Assert.Equal(3, executionCount);
    }

    [Fact]
    public async Task CronBackgroundWorker_Should_Call_OnError_When_Exception_Occurs()
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

        host.RunCronBackgroundWorker("* * * * *", () =>
            {
                throw new InvalidOperationException("Cron worker error");
            })
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
}
