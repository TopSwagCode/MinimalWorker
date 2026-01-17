using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace MinimalWorker.Test;

public class ContinuousWorkerTests
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
                    await Task.Delay(10, token);
                }
            }
            catch (OperationCanceledException)
            {
                cancellationObserved.TrySetResult(true);
            }
        });

        // Act
        await host.StartAsync();
        await Task.Delay(50);
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
        var service = Substitute.For<TestDependency>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(service);
            })
            .Build();

        host.RunBackgroundWorker(async (TestDependency myService, CancellationToken token) =>
        {
            myService.Increment();
            await Task.Delay(10, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100); // Give worker time to execute multiple times
        await host.StopAsync();

        // Assert - Use "at least" to avoid flakiness on different machines
        var callCount = service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "Increment");
        Assert.True(callCount >= 4, $"Expected at least 4 Increment calls, got {callCount}");
    }

    [Fact]
    public async Task BackgroundWorker_Should_Be_Able_To_Handle_Multiple_Tasks_With_Same_Dependencies()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var service = Substitute.For<TestDependency>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(service);
            })
            .Build();

        host.RunBackgroundWorker(async (TestDependency myService, CancellationToken token) =>
        {
            myService.Increment();
            await Task.Delay(10, token);
        });

        host.RunBackgroundWorker(async (TestDependency myService, CancellationToken token) =>
        {
            myService.Decrement();
            await Task.Delay(20, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100); // Give worker time to execute multiple times
        await host.StopAsync();

        // Assert - Use "at least" to avoid flakiness on different machines
        var incrementCalls = service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "Increment");
        var decrementCalls = service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "Decrement");
        Assert.True(incrementCalls >= 3, $"Expected at least 3 Increment calls, got {incrementCalls}");
        Assert.True(decrementCalls >= 2, $"Expected at least 2 Decrement calls, got {decrementCalls}");
    }

    [Fact]
    public async Task BackgroundWorker_Should_Be_Able_To_Handle_Nested_Dependencies()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();

        // Create real nested dependency chain: BDependency <- ADependency
        var bDependency = new BDependency();
        var aDependency = new ADependency(bDependency);

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Register a TestDependency that uses the real ADependency
                var testDep = Substitute.For<TestDependency>();
                testDep.When(x => x.Increment()).Do(x => aDependency.Increment());
                testDep.When(x => x.Decrement()).Do(x => aDependency.Decrement());

                services.AddSingleton(testDep);
            })
            .Build();

        host.RunBackgroundWorker(async (TestDependency myService, CancellationToken token) =>
        {
            myService.Increment(); // -> aDependency.Increment() -> bDependency.Increment()
            myService.Decrement(); // -> aDependency.Decrement() -> bDependency.Decrement()
            await Task.Delay(10, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100); // Give worker time to execute multiple times
        await host.StopAsync();

        // Assert - Verify that the delegation chain works through real classes
        // TestDependency (mock) -> ADependency (real) -> BDependency (real)
        Assert.True(bDependency.IncrementCount >= 4, $"Expected at least 4 increment calls, but got {bDependency.IncrementCount}");
        Assert.True(bDependency.DecrementCount >= 4, $"Expected at least 4 decrement calls, but got {bDependency.DecrementCount}");
    }

    [Fact]
    public async Task BackgroundWorker_Should_Support_Multiple_Workers_With_Different_Dependencies()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();

        var serviceA = new ServiceA();
        var serviceB = new ServiceB();
        var serviceC = new ServiceC();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IServiceA>(serviceA);
                services.AddSingleton<IServiceB>(serviceB);
                services.AddSingleton<IServiceC>(serviceC);
            })
            .Build();

        // Register three different workers, each with a different dependency
        host.RunBackgroundWorker(async (IServiceA svcA, CancellationToken token) =>
        {
            svcA.Execute();
            await Task.Delay(10, token);
        });

        host.RunBackgroundWorker(async (IServiceB svcB, CancellationToken token) =>
        {
            svcB.Execute();
            await Task.Delay(10, token);
        });

        host.RunBackgroundWorker(async (IServiceC svcC, CancellationToken token) =>
        {
            svcC.Execute();
            await Task.Delay(10, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100); // Give workers time to execute multiple times
        await host.StopAsync();

        // Assert - Verify that all three workers executed independently
        Assert.True(serviceA.ExecuteCount >= 4, $"Worker A should execute at least 4 times, but got {serviceA.ExecuteCount}");
        Assert.True(serviceB.ExecuteCount >= 4, $"Worker B should execute at least 4 times, but got {serviceB.ExecuteCount}");
        Assert.True(serviceC.ExecuteCount >= 4, $"Worker C should execute at least 4 times, but got {serviceC.ExecuteCount}");
    }

    [Fact]
    public async Task BackgroundWorker_Should_Execute_Without_Any_Dependencies()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var counter = 0;

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async () =>
        {
            Interlocked.Increment(ref counter);
            await Task.Delay(10);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await host.StopAsync();

        // Assert
        Assert.True(counter >= 3, $"Expected at least 3 executions, got {counter}");
    }

    [Fact]
    public async Task BackgroundWorker_Should_Handle_OperationCanceledException_Gracefully()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var errorHandlerCalled = false;

        using var host = Host.CreateDefaultBuilder()
            .Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(10, token); // This will throw OperationCanceledException on shutdown
                }
            })
            .WithErrorHandler(ex =>
            {
                errorHandlerCalled = true; // Should NOT be called for OperationCanceledException
            });

        // Act
        await host.StartAsync();
        await Task.Delay(50);
        await host.StopAsync();

        // Assert
        Assert.False(errorHandlerCalled, "OnError should not be called for OperationCanceledException");
    }
}
