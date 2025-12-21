using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddScoped<IScopedService, ScopedService>();
            })
            .Build();

        // Continuous worker - should reuse same scope for all iterations
        host.RunBackgroundWorker(async (IScopedService scopedService, CancellationToken token) =>
        {
            continuousWorkerIds.Add(scopedService.Id);
            await Task.Delay(50, token);
        });

        // Periodic worker - should create new scope per execution
        host.RunPeriodicBackgroundWorker(
            TimeSpan.FromMilliseconds(50),
            (IScopedService scopedService, CancellationToken token) =>
            {
                periodicWorkerIds.Add(scopedService.Id);
                return Task.CompletedTask;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(250); // Allow multiple executions
        await host.StopAsync();

        // Assert
        // Continuous worker should use same scoped instance across all iterations
        Assert.True(continuousWorkerIds.Count >= 3, "Continuous worker should execute multiple times");
        Assert.Single(continuousWorkerIds.Distinct()); // All should be the same ID

        // Periodic worker should get new scoped instance for each execution
        Assert.True(periodicWorkerIds.Count >= 3, "Periodic worker should execute multiple times");
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
            await Task.Delay(50, token);
        });

        host.RunBackgroundWorker(async (ThreadSafeCounter counter, CancellationToken token) =>
        {
            counter.Increment();
            await Task.Delay(50, token);
        });

        host.RunBackgroundWorker(async (ThreadSafeCounter counter, CancellationToken token) =>
        {
            counter.Increment();
            await Task.Delay(50, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(200);
        await host.StopAsync();

        // Assert - All three workers should have incremented the shared counter
        Assert.True(sharedCounter.Count >= 9, $"Expected at least 9 increments (3 workers × 3 executions), got {sharedCounter.Count}");
    }
}
