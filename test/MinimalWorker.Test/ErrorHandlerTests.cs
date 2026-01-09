using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace MinimalWorker.Test;

public class ErrorHandlerTests
{
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
        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                executionCount++;
                if (executionCount <= 2) // Throw on first 2 executions
                {
                    throw new InvalidOperationException(expectedMessage);
                }
                await Task.Delay(50, token);
            })
            .WithErrorHandler(ex =>
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
        BackgroundWorkerExtensions._useEnvironmentExit = false; // Disable Environment.Exit for testing

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
    public async Task BackgroundWorker_OnError_Should_Receive_Correct_Exception_Details()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var errors = new List<Exception>();
        var expectedMessages = new[] { "Error 1", "Error 2", "Error 3" };

        using var host = Host.CreateDefaultBuilder()
            .Build();

        var executionCount = 0;
        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                if (executionCount < expectedMessages.Length)
                {
                    var message = expectedMessages[executionCount];
                    executionCount++;
                    throw new InvalidOperationException(message);
                }
                await Task.Delay(50, token);
            })
            .WithErrorHandler(ex =>
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

        host.RunBackgroundWorker(async (TestDependency counter, CancellationToken token) =>
            {
                counter.Increment(); // This will throw
                await Task.Delay(50, token);
            })
            .WithErrorHandler(ex =>
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
    public async Task BackgroundWorker_Should_Fail_Fast_With_Missing_Dependency()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        BackgroundWorkerExtensions._useEnvironmentExit = false; // Disable Environment.Exit for testing

        var logMessages = new System.Collections.Concurrent.ConcurrentBag<string>();

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

        // Act & Assert
        // StartAsync should complete, but the exception will be thrown in ApplicationStarted callback
        await host.StartAsync();

        // Give time for the ApplicationStarted callback to execute and throw
        await Task.Delay(100);

        // Verify that a critical error was logged about the missing dependency
        var errorOutput = string.Join("\n", logMessages);
        var hasExpectedError = errorOutput.Contains("IUnregisteredService");

        Assert.True(hasExpectedError,
            $"Expected error about missing IUnregisteredService dependency. Logs:\n{errorOutput}");

        await host.StopAsync();
        host.Dispose();
    }

    [Fact]
    public async Task BackgroundWorker_Should_Crash_App_On_Unhandled_Runtime_Exception()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        BackgroundWorkerExtensions._useEnvironmentExit = false; // Disable Environment.Exit for testing

        var logMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
        var executionCount = 0;

        var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new TestLoggerProvider(logMessages));
            })
            .Build();

        // Worker that throws an exception on second execution
        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            executionCount++;
            if (executionCount >= 2)
            {
                throw new InvalidOperationException("Simulated runtime error");
            }
            await Task.Delay(50, token);
        });

        // Act
        await host.StartAsync();

        // Give time for the worker to execute twice and throw
        await Task.Delay(300);

        // Assert
        // Verify that a critical error was logged about the unhandled exception
        var errorOutput = string.Join("\n", logMessages);
        var hasFatalError = errorOutput.Contains("FATAL") || errorOutput.Contains("Simulated runtime error");

        Assert.True(hasFatalError,
            $"Expected FATAL error about unhandled exception. Logs:\n{errorOutput}");

        Assert.True(executionCount >= 2, "Worker should have executed at least twice before throwing");

        await host.StopAsync();
        host.Dispose();
    }

    [Fact]
    public async Task BackgroundWorker_Should_Support_DI_In_Error_Handler()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var exceptionCount = 0;
        var loggerWasResolved = false;
        Exception? capturedException = null;
        var expectedMessage = "Test exception with DI";

        using var host = Host.CreateDefaultBuilder()
            .Build();

        var executionCount = 0;
        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                executionCount++;
                if (executionCount <= 2) // Throw on first 2 executions
                {
                    throw new InvalidOperationException(expectedMessage);
                }
                await Task.Delay(50, token);
            })
            .WithErrorHandler((ex, serviceProvider) =>
            {
                exceptionCount++;
                capturedException = ex;

                // Verify we can resolve services from the service provider
                var logger = serviceProvider.GetService<ILogger<ErrorHandlerTests>>();
                loggerWasResolved = logger != null;
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
        Assert.True(loggerWasResolved, "Logger should be resolvable from service provider");
        Assert.True(executionCount > 2, "Worker should continue running after errors");
    }

    [Fact]
    public async Task PeriodicWorker_Should_Support_DI_In_Error_Handler()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var exceptionCount = 0;
        var serviceWasResolved = false;
        var testService = Substitute.For<TestDependency>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddScoped<TestDependency>(_ => testService);
            })
            .Build();

        var executionCount = 0;
        host.RunPeriodicBackgroundWorker(TimeSpan.FromMilliseconds(50), async (CancellationToken token) =>
            {
                executionCount++;
                if (executionCount <= 2)
                {
                    throw new InvalidOperationException("Periodic worker error");
                }
                await Task.CompletedTask;
            })
            .WithErrorHandler((ex, serviceProvider) =>
            {
                exceptionCount++;

                // Verify we can resolve scoped services from the service provider
                var service = serviceProvider.GetService<TestDependency>();
                serviceWasResolved = service != null;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(300);
        await host.StopAsync();

        // Assert
        Assert.Equal(2, exceptionCount);
        Assert.True(serviceWasResolved, "Scoped service should be resolvable from service provider");
    }

    [Fact]
    public async Task CronWorker_Should_Support_DI_In_Error_Handler()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var exceptionCount = 0;
        var serviceProviderWasNotNull = false;

        using var host = Host.CreateDefaultBuilder()
            .Build();

        var executionCount = 0;
        host.RunCronBackgroundWorker("* * * * * *", async (CancellationToken token) =>
            {
                executionCount++;
                if (executionCount <= 1)
                {
                    throw new InvalidOperationException("Cron worker error");
                }
                await Task.CompletedTask;
            })
            .WithErrorHandler((ex, serviceProvider) =>
            {
                exceptionCount++;
                serviceProviderWasNotNull = serviceProvider != null;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(1500); // Wait for at least one cron execution
        await host.StopAsync();

        // Assert
        Assert.Equal(1, exceptionCount);
        Assert.True(serviceProviderWasNotNull, "Service provider should not be null in error handler");
    }
}
