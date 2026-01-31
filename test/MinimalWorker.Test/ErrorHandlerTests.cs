using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MinimalWorker.Test.Fakes;
using MinimalWorker.Test.Helpers;
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

        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException(expectedMessage);
            })
            .WithErrorHandler(ex =>
            {
                exceptionCount++;
                capturedException = ex;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(100); // Give time for worker to execute
        await host.StopAsync();

        // Assert - Continuous worker runs once and throws once
        Assert.Equal(1, exceptionCount);
        Assert.NotNull(capturedException);
        Assert.IsType<InvalidOperationException>(capturedException);
        Assert.Equal(expectedMessage, capturedException.Message);
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
        await Task.Delay(50); // Give time for worker to throw
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
        var expectedMessage = "Error 1";

        using var host = Host.CreateDefaultBuilder()
            .Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException(expectedMessage);
            })
            .WithErrorHandler(ex =>
            {
                errors.Add(ex);
            });

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await host.StopAsync();

        // Assert - Continuous worker runs once and throws once
        Assert.Single(errors);
        Assert.Equal(expectedMessage, errors[0].Message);
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
                await Task.Delay(10, token);
            })
            .WithErrorHandler(ex =>
            {
                exceptionCaught = true;
            });

        // Act
        await host.StartAsync();
        await Task.Delay(50);
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
                await Task.Delay(10, token);
            });

        // Act & Assert
        // StartAsync should complete, but the exception will be thrown in ApplicationStarted callback
        await host.StartAsync();

        // Give time for the ApplicationStarted callback to execute and throw
        await Task.Delay(50);

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

        // Worker that throws an exception immediately
        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            executionCount++;
            await Task.CompletedTask;
            throw new InvalidOperationException("Simulated runtime error");
        });

        // Act
        await host.StartAsync();

        // Give time for the worker to execute and throw
        await Task.Delay(100);

        // Assert
        // Verify that a critical error was logged about the unhandled exception
        var errorOutput = string.Join("\n", logMessages);
        var hasFatalError = errorOutput.Contains("FATAL") || errorOutput.Contains("Simulated runtime error");

        Assert.True(hasFatalError,
            $"Expected FATAL error about unhandled exception. Logs:\n{errorOutput}");

        Assert.Equal(1, executionCount); // Continuous worker runs exactly once

        await host.StopAsync();
        host.Dispose();
    }

    [Fact]
    public async Task ErrorHandler_That_Throws_Should_Not_Crash_Worker()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        BackgroundWorkerExtensions._useEnvironmentExit = false;
        var workerExecutions = 0;
        var errorHandlerCallCount = 0;

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                Interlocked.Increment(ref workerExecutions);
                await Task.CompletedTask;
                throw new InvalidOperationException("Worker error");
            })
            .WithErrorHandler(ex =>
            {
                Interlocked.Increment(ref errorHandlerCallCount);
                // Error handler itself throws - this should be handled gracefully
                throw new InvalidOperationException("Error handler also throws!");
            });

        // Act
        await host.StartAsync();
        await Task.Delay(TestConstants.StandardTestWindowMs);
        await host.StopAsync();

        // Assert - Worker runs once, error handler called once
        Assert.Equal(1, workerExecutions);
        Assert.Equal(1, errorHandlerCallCount);
    }

    [Fact]
    public async Task Multiple_Workers_One_Fails_Others_Continue()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var healthyWorkerExecutions = 0;
        var failingWorkerExecutions = 0;

        using var host = Host.CreateDefaultBuilder().Build();

        // Healthy worker
        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            Interlocked.Increment(ref healthyWorkerExecutions);
            await Task.CompletedTask;
        }).WithName("healthy-worker");

        // Failing worker with error handler
        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                Interlocked.Increment(ref failingWorkerExecutions);
                await Task.CompletedTask;
                throw new InvalidOperationException("This worker always fails");
            })
            .WithName("failing-worker")
            .WithErrorHandler(ex => { /* Suppress */ });

        // Act
        await host.StartAsync();
        await Task.Delay(TestConstants.StandardTestWindowMs);
        await host.StopAsync();

        // Assert - Each continuous worker runs exactly once
        Assert.Equal(1, healthyWorkerExecutions);
        Assert.Equal(1, failingWorkerExecutions);
    }

    [Fact]
    public async Task OnError_Should_Preserve_InnerException_Details()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        Exception? capturedException = null;

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                await Task.CompletedTask;
                try
                {
                    throw new InvalidOperationException("Inner error");
                }
                catch (Exception inner)
                {
                    throw new ApplicationException("Outer error", inner);
                }
            })
            .WithErrorHandler(ex => capturedException = ex);

        // Act
        await host.StartAsync();
        await Task.Delay(50);
        await host.StopAsync();

        // Assert
        Assert.NotNull(capturedException);
        Assert.IsType<ApplicationException>(capturedException);
        Assert.NotNull(capturedException.InnerException);
        Assert.IsType<InvalidOperationException>(capturedException.InnerException);
        Assert.Equal("Inner error", capturedException.InnerException.Message);
    }
}
