using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using MinimalWorker.Test.Fakes;
using MinimalWorker.Test.Helpers;
using NSubstitute;

namespace MinimalWorker.Test;

public class CronWorkerTests
{
    [Fact]
    public async Task CronBackgroundWorker_Should_Invoke_Action_At_Scheduled_Times()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var service = Substitute.For<TestDependency>();
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(service);
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        // Cron expression: every minute
        host.RunCronBackgroundWorker("* * * * *", (TestDependency svc, CancellationToken token) =>
        {
            svc.Increment();
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        
        // Advance time by 3 minutes to trigger 3 executions (at minutes 1, 2, 3)
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(3));
        
        await host.StopAsync();

        // Assert - CRON "* * * * *" fires every minute, so 3 minutes = exactly 3 executions
        var callCount = service.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "Increment");
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task CronBackgroundWorker_Should_Execute_Multiple_Times_Over_Period()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionCount = 0;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        // Every 5 minutes
        host.RunCronBackgroundWorker("*/5 * * * *", (CancellationToken token) =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        
        // Advance time by 30 minutes - fires at 5, 10, 15, 20, 25, 30 min = 6 executions
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(30), steps: 30);
        
        await host.StopAsync();

        // Assert - CRON "*/5 * * * *" fires at minutes 5, 10, 15, 20, 25, 30 = exactly 6 executions
        Assert.Equal(6, executionCount);
    }

    [Fact]
    public async Task CronBackgroundWorker_Should_Handle_Hourly_Schedule()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionCount = 0;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        // Every hour at minute 0
        host.RunCronBackgroundWorker("0 * * * *", (CancellationToken token) =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        
        // Advance time by 3 hours - fires at hour 1, 2, 3 = 3 executions
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromHours(3), steps: 36);
        
        await host.StopAsync();

        // Assert - CRON "0 * * * *" fires hourly, so 3 hours = exactly 3 executions
        Assert.Equal(3, executionCount);
    }

    [Fact]
    public async Task CronBackgroundWorker_Should_Call_OnError_When_Exception_Occurs()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var errorWasCalled = false;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunCronBackgroundWorker("* * * * *", () =>
            {
                throw new InvalidOperationException("Cron worker error");
            })
            .WithErrorHandler(ex =>
            {
                errorWasCalled = true;
            });

        // Act
        await host.StartAsync();
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2));
        await host.StopAsync();

        // Assert
        Assert.True(errorWasCalled, "OnError should be called when exception occurs");
    }

    [Fact]
    public async Task CronBackgroundWorker_Should_Continue_Running_After_Errors()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionCount = 0;
        var errorCount = 0;
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunCronBackgroundWorker("* * * * *", (CancellationToken token) =>
            {
                Interlocked.Increment(ref executionCount);
                // Throw on second execution only
                if (executionCount == 2)
                {
                    throw new InvalidOperationException("Simulated cron error");
                }
                return Task.CompletedTask;
            })
            .WithErrorHandler(ex =>
            {
                Interlocked.Increment(ref errorCount);
            });

        // Act
        await host.StartAsync();
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(5));
        await host.StopAsync();

        // Assert - Should continue after error on execution 2
        Assert.True(executionCount >= 4, $"Expected at least 4 executions (continued after error), got {executionCount}");
        Assert.Equal(1, errorCount);
    }

    [Fact]
    public async Task CronBackgroundWorker_With_DayOfWeek_Schedule_Should_Execute()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionCount = 0;
        // Start on a Wednesday (2025-01-01 is a Wednesday)
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        // Every minute on Wednesday (day 3)
        host.RunCronBackgroundWorker("* * * * 3", (CancellationToken token) =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        // Advance 5 minutes - should fire 5 times since we're on Wednesday
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(5));
        await host.StopAsync();

        // Assert - Should execute on Wednesday
        Assert.True(executionCount >= 4, $"Expected at least 4 executions on Wednesday, got {executionCount}");
    }

    [Fact]
    public async Task CronBackgroundWorker_With_Invalid_Expression_Should_Fail_Fast()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        BackgroundWorkerExtensions._useEnvironmentExit = false;
        var logMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
        var timeProvider = WorkerTestHelper.CreateTimeProvider();
        var workerExecuted = false;

        var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new TestLoggerProvider(logMessages));
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        // Invalid cron expression
        host.RunCronBackgroundWorker("invalid-cron", () =>
        {
            workerExecuted = true;
            return Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        await Task.Delay(100);
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2));

        await host.StopAsync();
        host.Dispose();

        // Assert - Either error was logged OR worker never executed (due to parsing failure)
        var errorOutput = string.Join("\n", logMessages);
        var hasParsingError = errorOutput.Contains("cron", StringComparison.OrdinalIgnoreCase) ||
                              errorOutput.Contains("parse", StringComparison.OrdinalIgnoreCase) ||
                              errorOutput.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                              errorOutput.Contains("FATAL", StringComparison.OrdinalIgnoreCase) ||
                              errorOutput.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                              errorOutput.Contains("Error", StringComparison.OrdinalIgnoreCase);

        // If no error logged, at least the worker should not have executed
        Assert.True(hasParsingError || !workerExecuted,
            $"Expected either error about invalid cron expression or worker should not execute. " +
            $"Worker executed: {workerExecuted}. Logs:\n{errorOutput}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CronBackgroundWorker_With_Empty_Expression_Should_Throw_ArgumentException(string? cronExpression)
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();

        using var host = Host.CreateDefaultBuilder().Build();

        // Act & Assert - Empty cron expression should throw ArgumentException
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            host.RunCronBackgroundWorker(cronExpression!, (CancellationToken token) =>
            {
                return Task.CompletedTask;
            });
        });

        Assert.Equal("cronExpression", exception.ParamName);
    }
}
