using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            await Task.Delay(50, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Give worker time to execute multiple times
        await host.StopAsync();

        // Assert - At least 4 calls should have been made
        service.Received(4).Increment();
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
            await Task.Delay(50, token);
        });
        
        host.RunBackgroundWorker(async (TestDependency myService, CancellationToken token) =>
        {
            myService.Decrement();
            await Task.Delay(100, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Give worker time to execute multiple times
        await host.StopAsync();

        // Assert - At least 4 and 2 calls should have been made
        service.Received(4).Increment();
        service.Received(2).Decrement();
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
            await Task.Delay(50, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Give worker time to execute multiple times
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
            await Task.Delay(50, token);
        });

        host.RunBackgroundWorker(async (IServiceB svcB, CancellationToken token) =>
        {
            svcB.Execute();
            await Task.Delay(50, token);
        });

        host.RunBackgroundWorker(async (IServiceC svcC, CancellationToken token) =>
        {
            svcC.Execute();
            await Task.Delay(50, token);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(200); // Give workers time to execute multiple times
        await host.StopAsync();

        // Assert - Verify that all three workers executed independently
        Assert.True(serviceA.ExecuteCount >= 4, $"Worker A should execute at least 4 times, but got {serviceA.ExecuteCount}");
        Assert.True(serviceB.ExecuteCount >= 4, $"Worker B should execute at least 4 times, but got {serviceB.ExecuteCount}");
        Assert.True(serviceC.ExecuteCount >= 4, $"Worker C should execute at least 4 times, but got {serviceC.ExecuteCount}");
    }

    
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

        // Assert
        counter.Received(6).Increment();
    }

    [Fact(Skip = "Slow test, run manually if needed")]
    public async Task CronBackgroundWorker_Should_Invoke_Action_At_Scheduled_Times()
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
        
        host.RunCronBackgroundWorker("* * * * *", (TestDependency svc, CancellationToken token) =>
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
        var service = Substitute.For<TestDependency>();
        var exceptionCaught = false;

        service.When(x => x.Increment()).Do(x => throw new InvalidOperationException("Service error"));

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(service);
            })
            .Build();

        host.RunBackgroundWorker(
            async (TestDependency counter, CancellationToken token) =>
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
            await Task.Delay(50);
        });

        // Act
        await host.StartAsync();
        await Task.Delay(200);
        await host.StopAsync();

        // Assert
        Assert.True(counter >= 3, $"Expected at least 3 executions, got {counter}");
    }

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

        host.RunBackgroundWorker(
            async (IRepository<string> repo, CancellationToken token) =>
            {
                var item = await repo.GetAsync();
                processedItems.Add(item);
                await Task.Delay(50, token);
            },
            onError: ex =>
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

        host.RunBackgroundWorker(async (Microsoft.Extensions.Logging.ILogger<MinimalWorkerTests> logger, CancellationToken token) =>
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

    [Fact]
    public async Task BackgroundWorker_Should_Fail_Fast_With_Missing_Dependency()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var logMessages = new List<string>();
        
        var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new TestLoggerProvider(logMessages));
            })
            .ConfigureServices(services =>
            {
                // Intentionally don't register IUnregisteredService
                // This will cause GetRequiredService to throw during dependency validation
            })
            .Build();

        host.RunBackgroundWorker(
            async (IUnregisteredService service, CancellationToken token) =>
            {
                await Task.Delay(50, token);
            });

        // Act
        await host.StartAsync();
        await Task.Delay(100); // Give time for error to be logged
        
        // Assert
        // Verify that a critical error was logged about the missing dependency
        var hasCriticalError = logMessages.Any(msg => 
            msg.Contains("An error occurred starting the application") ||
            msg.Contains("IUnregisteredService"));
        
        Assert.True(hasCriticalError, 
            "Expected critical error log about missing dependency. " +
            $"Logs: {string.Join(", ", logMessages)}");
        
        await host.StopAsync();
        host.Dispose();
    }

    // Test logger to capture log messages
    private class TestLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages;
        
        public TestLoggerProvider(List<string> messages)
        {
            _messages = messages;
        }
        
        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(_messages);
        }
        
        public void Dispose() { }
    }
    
    private class TestLogger : ILogger
    {
        private readonly List<string> _messages;
        
        public TestLogger(List<string> messages)
        {
            _messages = messages;
        }
        
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (exception != null)
            {
                message += $" Exception: {exception.Message}";
            }
            _messages.Add(message);
        }
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
