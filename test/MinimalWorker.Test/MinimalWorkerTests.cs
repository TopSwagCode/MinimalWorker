﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace MinimalWorker.Test;

public class MinimalWorkerTests
{
    [Fact]
    public async Task BackgroundWorker_Should_Respect_CancellationToken()
    {
        // Arrange
        var cancellationObserved = new TaskCompletionSource<bool>();

        using var host = Host.CreateDefaultBuilder()
            .Build();

        host.MapBackgroundWorker(async (CancellationToken token) =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(50, token);
                }
            }
            catch (OperationCanceledException)
            {
                cancellationObserved.TrySetResult(true);
            }
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await host.StopAsync();

        // Assert
        var wasCancelled = await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(wasCancelled, "The background worker did not observe cancellation properly.");
    }

    
    [Fact]
    public async Task BackgroundWorker_Should_Resolve_Dependencies_And_Invoke_Action()
    {
        // Arrange
        var service = Substitute.For<ICounterService>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(service);
            })
            .Build();

        host.MapBackgroundWorker(async (ICounterService myService, CancellationToken token) =>
        {
            myService.Increment();

            await Task.Delay(20, token);
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        await Task.Delay(103); // 3 as buffer
        await host.StopAsync();

        // Assert
        service.Received(5).Increment();
    }
    
    [Fact]
    public async Task PeriodicBackgroundWorker_Should_Invoke_Action_Multiple_Times()
    {
        // Arrange
        var counter = Substitute.For<ICounterService>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(counter);
            })
            .Build();

        host.MapPeriodicBackgroundWorker(TimeSpan.FromMilliseconds(50), (ICounterService svc, CancellationToken token) =>
        {
            svc.Increment();
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        await Task.Delay(303); // 3 as buffer
        await host.StopAsync();

        // Assert
        counter.Received(6).Increment();
    }

    [Fact(Skip = "Slow test, run manually if needed")]
    public async Task CronBackgroundWorker_Should_Invoke_Action_At_Scheduled_Times()
    {
        // Arrange
        var service = Substitute.For<ICounterService>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(service);
            })
            .Build();
        
        host.MapCronBackgroundWorker("* * * * *", (ICounterService svc, CancellationToken token) =>
        {
            svc.Increment();
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        await Task.Delay(61000);
        await host.StopAsync();

        // Assert
        service.Received(1).Increment();
    }
}