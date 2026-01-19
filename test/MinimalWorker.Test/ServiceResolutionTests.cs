using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MinimalWorker.Test.Fakes;
using MinimalWorker.Test.Helpers;
using MinimalWorker.Test.TestTypes;

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
                await Task.Delay(10, token);
            })
            .WithErrorHandler(ex =>
            {
                workerException = ex;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await host.StopAsync();

        // Assert
        if (workerException != null)
        {
            throw new Exception($"Worker failed: {workerException.Message}", workerException);
        }
        Assert.InRange(processedItems.Count, TestConstants.MinContinuousExecutions, TestConstants.MaxContinuousExecutions);
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
            await Task.Delay(10, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await host.StopAsync();

        // Assert
        Assert.InRange(logCount, TestConstants.MinContinuousExecutions, TestConstants.MaxContinuousExecutions);
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
                    options.Interval = 10;
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
        await Task.Delay(100);
        await host.StopAsync();

        // Assert
        Assert.InRange(executionCount, TestConstants.MinContinuousExecutions, TestConstants.MaxContinuousExecutions);
    }

    [Fact]
    public async Task BackgroundWorker_Should_Resolve_Transient_Services()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var instanceIds = new System.Collections.Concurrent.ConcurrentBag<Guid>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddTransient<ITransientService, TransientService>();
            })
            .Build();

        host.RunBackgroundWorker(async (ITransientService service, CancellationToken token) =>
        {
            instanceIds.Add(service.InstanceId);
            await Task.Delay(TestConstants.StandardWorkerDelayMs, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(TestConstants.StandardTestWindowMs);
        await host.StopAsync();

        // Assert - Continuous worker resolves dependencies once at startup and reuses them
        // across all iterations. The injected service instance is the same object throughout.
        Assert.True(instanceIds.Count >= TestConstants.MinContinuousExecutions,
            $"Expected at least {TestConstants.MinContinuousExecutions} executions, got {instanceIds.Count}");
        // All iterations use the same injected instance (resolved once when worker started)
        Assert.Single(instanceIds.Distinct());
    }

    [Fact]
    public async Task BackgroundWorker_Should_Resolve_MultiTypeArgument_Generic_Services1()
    {
        // Arrange
        // This test verifies that generic services with multiple type arguments (like IConsumer<TKey, TValue>)
        // are correctly resolved from the DI container and injected into background workers.
        BackgroundWorkerExtensions.ClearRegistrations();
        var consumedItems = new System.Collections.Concurrent.ConcurrentBag<(string Key, string Value)>();
        Exception? workerException = null;

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConsumer<string, string>, StringStringConsumer>();
            })
            .Build();

        host.RunBackgroundWorker(async (IConsumer<string, string> consumer, CancellationToken token) =>
            {
                var item = await consumer.ConsumeAsync(token);
                consumedItems.Add(item);
                await Task.Delay(10, token);
            })
            .WithErrorHandler(ex =>
            {
                workerException = ex;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await host.StopAsync();

        // Assert
        if (workerException != null)
        {
            throw new Exception($"Worker failed: {workerException.Message}", workerException);
        }
        Assert.InRange(consumedItems.Count, 9, 10);
        Assert.All(consumedItems, item =>
        {
            Assert.StartsWith("Key_", item.Key);
            Assert.StartsWith("Value_", item.Value);
        });
    }
    
    [Fact]
    public async Task BackgroundWorker_Should_Resolve_MultiTypeArgument_Generic_Services2()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var consumedItems = new System.Collections.Concurrent.ConcurrentBag<(string Key, string Value, string Extra)>();
        Exception? workerException = null;

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IMultipleConsumer<string, string, string>, StringStringStringConsumer>();
            })
            .Build();

        host.RunBackgroundWorker(async (IMultipleConsumer<string, string, string> consumer, CancellationToken token) =>
            {
                var item = await consumer.ConsumeAsync(token);
                consumedItems.Add(item);
                await Task.Delay(10, token);
            })
            .WithErrorHandler(ex =>
            {
                workerException = ex;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await host.StopAsync();

        // Assert
        if (workerException != null)
        {
            throw new Exception($"Worker failed: {workerException.Message}", workerException);
        }
        Assert.InRange(consumedItems.Count, TestConstants.MinContinuousExecutions, TestConstants.MaxContinuousExecutions);
        Assert.All(consumedItems, item =>
        {
            Assert.StartsWith("Key_", item.Key);
            Assert.StartsWith("Value_", item.Value);
        });
    }
}
