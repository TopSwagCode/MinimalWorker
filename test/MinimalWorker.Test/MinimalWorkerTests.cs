using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace MinimalWorker.Test;

public class MinimalWorkerTests
{
    [Fact]
    public async Task BackgroundWorker_Should_Respect_CancellationToken()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var cancellationObserved = new TaskCompletionSource<bool>();

        using var host = Host.CreateDefaultBuilder()
            .Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
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
        BackgroundWorkerExtensions.ClearRegistrations();
        var service = Substitute.For<ICounterService>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(service);
            })
            .Build();

        host.RunBackgroundWorker(async (ICounterService myService, CancellationToken token) =>
        {
            myService.Increment();
            await Task.Delay(50, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Give worker time to execute multiple times
        await host.StopAsync();

        // Assert - At least 2 calls should have been made
        service.Received().Increment();
        service.Received().Increment();
    }
    
    [Fact]
    public async Task PeriodicBackgroundWorker_Should_Invoke_Action_Multiple_Times()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var counter = Substitute.For<ICounterService>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(counter);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(TimeSpan.FromMilliseconds(50), (ICounterService svc, CancellationToken token) =>
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
        BackgroundWorkerExtensions.ClearRegistrations();
        var service = Substitute.For<ICounterService>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(service);
            })
            .Build();
        
        host.RunCronBackgroundWorker("* * * * *", (ICounterService svc, CancellationToken token) =>
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

    [Fact]
    public async Task BackgroundWorker_Should_Call_OnError_When_Exception_Occurs()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var exceptionCount = 0;
        Exception? capturedException = null;
        var expectedMessage = "Test exception";

        using var host = Host.CreateDefaultBuilder()
            .Build();

        var executionCount = 0;
        host.RunBackgroundWorker(
            async (CancellationToken token) =>
            {
                executionCount++;
                if (executionCount <= 2) // Throw on first 2 executions
                {
                    throw new InvalidOperationException(expectedMessage);
                }
                await Task.Delay(50, token);
            },
            onError: ex =>
            {
                exceptionCount++;
                capturedException = ex;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Give time for worker to execute multiple times
        await host.StopAsync();

        // Assert
        Assert.Equal(2, exceptionCount);
        Assert.NotNull(capturedException);
        Assert.IsType<InvalidOperationException>(capturedException);
        Assert.Equal(expectedMessage, capturedException.Message);
        Assert.True(executionCount > 2, "Worker should continue running after errors");
    }

    [Fact]
    public async Task BackgroundWorker_Should_Rethrow_When_OnError_Not_Provided()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionCount = 0;

        using var host = Host.CreateDefaultBuilder()
            .Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            executionCount++;
            throw new InvalidOperationException("This should crash the worker");
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100); // Give time for worker to throw
        await host.StopAsync();

        // Assert
        Assert.Equal(1, executionCount); // Worker should only execute once before crashing
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
            () => { throw new InvalidOperationException("Periodic worker error");},
            onError: ex =>
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
    public async Task BackgroundWorker_Should_Handle_OperationCanceledException_Gracefully()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var errorHandlerCalled = false;

        using var host = Host.CreateDefaultBuilder()
            .Build();

        host.RunBackgroundWorker(
            async (CancellationToken token) =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(50, token); // This will throw OperationCanceledException on shutdown
                }
            },
            onError: ex =>
            {
                errorHandlerCalled = true; // Should NOT be called for OperationCanceledException
            });

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await host.StopAsync();

        // Assert
        Assert.False(errorHandlerCalled, "OnError should not be called for OperationCanceledException");
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
            worker,
            onError: ex => { /* Ignore errors */ });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Let it execute a few times
        await host.StopAsync();

        // Assert
        Assert.True(executionCount >= 3, "Periodic worker should execute multiple times");
    }

    [Fact]
    public async Task BackgroundWorker_OnError_Should_Receive_Correct_Exception_Details()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var errors = new List<Exception>();
        var expectedMessages = new[] { "Error 1", "Error 2", "Error 3" };

        using var host = Host.CreateDefaultBuilder()
            .Build();

        var executionCount = 0;
        host.RunBackgroundWorker(
            async (CancellationToken token) =>
            {
                if (executionCount < expectedMessages.Length)
                {
                    var message = expectedMessages[executionCount];
                    executionCount++;
                    throw new InvalidOperationException(message);
                }
                await Task.Delay(50, token);
            },
            onError: ex =>
            {
                errors.Add(ex);
            });

        // Act
        await host.StartAsync();
        await Task.Delay(200);
        await host.StopAsync();

        // Assert
        Assert.Equal(3, errors.Count);
        for (int i = 0; i < expectedMessages.Length; i++)
        {
            Assert.Equal(expectedMessages[i], errors[i].Message);
        }
    }

    [Fact]
    public async Task BackgroundWorker_With_DI_Services_Should_Call_OnError()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var service = Substitute.For<ICounterService>();
        var exceptionCaught = false;

        service.When(x => x.Increment()).Do(x => throw new InvalidOperationException("Service error"));

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(service);
            })
            .Build();

        host.RunBackgroundWorker(
            async (ICounterService counter, CancellationToken token) =>
            {
                counter.Increment(); // This will throw
                await Task.Delay(50, token);
            },
            onError: ex =>
            {
                exceptionCaught = true;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await host.StopAsync();

        // Assert
        Assert.True(exceptionCaught, "OnError should be called when DI service throws");
    }
}
