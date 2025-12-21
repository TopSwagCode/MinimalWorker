using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MinimalWorker.Test;

public class ServiceResolutionTests
{
    [Fact]
    public async Task BackgroundWorker_Should_Resolve_Generic_Services()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var processedItems = new System.Collections.Concurrent.ConcurrentBag<string>();
        Exception? workerException = null;

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IRepository<string>, StringRepository>();
            })
            .Build();

        host.RunBackgroundWorker(async (IRepository<string> repo, CancellationToken token) =>
            {
                var item = await repo.GetAsync();
                processedItems.Add(item);
                await Task.Delay(50, token);
            })
            .WithErrorHandler(ex =>
            {
                workerException = ex;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(200);
        await host.StopAsync();

        // Assert
        if (workerException != null)
        {
            throw new Exception($"Worker failed: {workerException.Message}", workerException);
        }
        Assert.True(processedItems.Count >= 3, $"Expected at least 3 items, got {processedItems.Count}");
        Assert.All(processedItems, item => Assert.StartsWith("Item_", item));
    }

    [Fact]
    public async Task BackgroundWorker_Should_Resolve_ILogger()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var logCount = 0;

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
            })
            .Build();

        host.RunBackgroundWorker(async (ILogger<ServiceResolutionTests> logger, CancellationToken token) =>
        {
            logger.LogInformation("Worker executing at {Time}", DateTime.UtcNow);
            Interlocked.Increment(ref logCount);
            await Task.Delay(50, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(200);
        await host.StopAsync();

        // Assert
        Assert.True(logCount >= 3, $"Expected at least 3 log calls, got {logCount}");
    }

    [Fact]
    public async Task BackgroundWorker_Should_Resolve_IOptions()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionCount = 0;

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<WorkerSettings>(options =>
                {
                    options.Enabled = true;
                    options.Interval = 50;
                });
            })
            .Build();

        host.RunBackgroundWorker(async (Microsoft.Extensions.Options.IOptions<WorkerSettings> options, CancellationToken token) =>
        {
            if (options.Value.Enabled)
            {
                Interlocked.Increment(ref executionCount);
            }
            await Task.Delay(options.Value.Interval, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(200);
        await host.StopAsync();

        // Assert
        Assert.True(executionCount >= 3, $"Expected at least 3 executions with enabled=true, got {executionCount}");
    }
}
