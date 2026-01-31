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

        // Continuous worker - runs exactly once with a single scope
        host.RunBackgroundWorker(async (IScopedService scopedService, CancellationToken token) =>
        {
            continuousWorkerIds.Add(scopedService.Id);
            await Task.CompletedTask;
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

        // Advance time for periodic worker
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(5));

        // Give continuous worker time to complete
        await Task.Delay(50);

        await host.StopAsync();

        // Assert
        // Continuous worker runs exactly once with a single scope
        Assert.Equal(1, continuousWorkerIds.Count);
        Assert.Single(continuousWorkerIds.Distinct());

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
            await Task.CompletedTask;
        });

        host.RunBackgroundWorker(async (ThreadSafeCounter counter, CancellationToken token) =>
        {
            counter.Increment();
            await Task.CompletedTask;
        });

        host.RunBackgroundWorker(async (ThreadSafeCounter counter, CancellationToken token) =>
        {
            counter.Increment();
            await Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100); // Let continuous workers complete
        await host.StopAsync();

        // Assert - Each continuous worker runs exactly once
        // 3 workers × 1 execution each = 3 total increments
        Assert.Equal(3, sharedCounter.Count);
    }
}
