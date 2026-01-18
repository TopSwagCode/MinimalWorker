using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using MinimalWorker.Test.Fakes;
using MinimalWorker.Test.Helpers;

namespace MinimalWorker.Test;

public class ScopingTests
{
    [Fact]
    public async Task BackgroundWorker_Should_Resolve_Scoped_Services_Per_Execution()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var continuousWorkerIds = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        var periodicWorkerIds = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddScoped<IScopedService, ScopedService>();
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        // Continuous worker - should reuse same scope for all iterations
        host.RunBackgroundWorker(async (IScopedService scopedService, CancellationToken token) =>
        {
            continuousWorkerIds.Add(scopedService.Id);
            await Task.Delay(10, token);
        });

        // Periodic worker - should create new scope per execution
        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMinutes(1),
            (IScopedService scopedService, CancellationToken token) =>
            {
                periodicWorkerIds.Add(scopedService.Id);
                return Task.CompletedTask;
            });

        // Act
        await host.StartAsync();
        
        // Advance time for periodic worker, continuous worker runs immediately
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(5));
        
        // Give continuous worker a bit more real time to accumulate some iterations
        await Task.Delay(50);
        
        await host.StopAsync();

        // Assert
        // Continuous worker should use same scoped instance across all iterations
        Assert.InRange(continuousWorkerIds.Count, TestConstants.MinContinuousExecutions, TestConstants.MaxContinuousExecutions);
        Assert.Single(continuousWorkerIds.Distinct()); // All should be the same ID

        // Periodic worker - 1 min interval, 5 min window = ticks at 1, 2, 3, 4 min = 4 executions
        Assert.Equal(4, periodicWorkerIds.Count);
        Assert.Equal(periodicWorkerIds.Count, periodicWorkerIds.Distinct().Count()); // All unique IDs
    }

    [Fact]
    public async Task Multiple_Workers_Should_Share_Singleton_Service_Safely()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var sharedCounter = new ThreadSafeCounter();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(sharedCounter);
            })
            .Build();

        // Register 3 workers all using the same counter
        host.RunBackgroundWorker(async (ThreadSafeCounter counter, CancellationToken token) =>
        {
            counter.Increment();
            await Task.Delay(10, token);
        });

        host.RunBackgroundWorker(async (ThreadSafeCounter counter, CancellationToken token) =>
        {
            counter.Increment();
            await Task.Delay(10, token);
        });

        host.RunBackgroundWorker(async (ThreadSafeCounter counter, CancellationToken token) =>
        {
            counter.Increment();
            await Task.Delay(10, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100); // Let continuous workers run
        await host.StopAsync();

        // Assert - All three workers should have incremented the shared counter
        // 3 workers × minimum executions each, with upper bound for runaway detection
        Assert.InRange(sharedCounter.Count, 9, TestConstants.MaxContinuousExecutions * 3);
    }
}
