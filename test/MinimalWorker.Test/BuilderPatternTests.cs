using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using MinimalWorker.Test.Helpers;

namespace MinimalWorker.Test;

/// <summary>
/// Tests for the IWorkerBuilder fluent API (WithName, WithErrorHandler).
/// </summary>
public class BuilderPatternTests
{
    [Fact]
    public async Task WithName_And_WithErrorHandler_Chaining_Works()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var errorHandlerCalled = false;
        var activitiesCollected = new List<Activity>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "MinimalWorker",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity =>
            {
                lock (activitiesCollected)
                {
                    activitiesCollected.Add(activity);
                }
                signal.TrySetResult(true);
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                throw new InvalidOperationException("Test error");
            })
            .WithName("chained-worker")
            .WithErrorHandler(ex => errorHandlerCalled = true);

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TestConstants.SignalTimeout);
        await Task.Delay(50); // Allow error handler to be called
        await host.StopAsync();

        // Assert
        Assert.True(errorHandlerCalled, "Error handler should be called");
        Assert.Contains(activitiesCollected, a =>
            a.GetTagItem("worker.name")?.ToString() == "chained-worker");
    }

    [Fact]
    public async Task WithName_Called_Multiple_Times_Uses_Last_Value()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var activitiesCollected = new List<Activity>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "MinimalWorker",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity =>
            {
                lock (activitiesCollected)
                {
                    activitiesCollected.Add(activity);
                }
                signal.TrySetResult(true);
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                await Task.Delay(5, token);
            })
            .WithName("first-name")
            .WithName("second-name")
            .WithName("final-name");

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TestConstants.SignalTimeout);
        await host.StopAsync();

        // Assert - Last name should win
        Assert.Contains(activitiesCollected, a =>
            a.GetTagItem("worker.name")?.ToString() == "final-name");
    }

    [Fact]
    public async Task WithName_With_Empty_String_Still_Executes()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executed = false;

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            executed = true;
            await Task.Delay(10, token);
        }).WithName("");

        // Act
        await host.StartAsync();
        await Task.Delay(TestConstants.StandardTestWindowMs);
        await host.StopAsync();

        // Assert
        Assert.True(executed, "Worker with empty name should still execute");
    }

    [Fact]
    public async Task WithErrorHandler_Called_Multiple_Times_Uses_Last_Handler()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var firstHandlerCalled = false;
        var secondHandlerCalled = false;
        var thirdHandlerCalled = false;

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
            {
                throw new InvalidOperationException("Test error");
            })
            .WithErrorHandler(ex => firstHandlerCalled = true)
            .WithErrorHandler(ex => secondHandlerCalled = true)
            .WithErrorHandler(ex => thirdHandlerCalled = true);

        // Act
        await host.StartAsync();
        await Task.Delay(TestConstants.StandardTestWindowMs);
        await host.StopAsync();

        // Assert - Only the last handler should be called
        Assert.False(firstHandlerCalled, "First handler should not be called");
        Assert.False(secondHandlerCalled, "Second handler should not be called");
        Assert.True(thirdHandlerCalled, "Third (last) handler should be called");
    }

    [Fact]
    public async Task Builder_Without_Name_Uses_Generated_Name()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var activitiesCollected = new List<Activity>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "MinimalWorker",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity =>
            {
                lock (activitiesCollected)
                {
                    activitiesCollected.Add(activity);
                }
                signal.TrySetResult(true);
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        using var host = Host.CreateDefaultBuilder().Build();

        // Register without calling WithName
        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            await Task.Delay(5, token);
        });

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TestConstants.SignalTimeout);
        await host.StopAsync();

        // Assert - Should have a generated name like "worker-1"
        Assert.NotEmpty(activitiesCollected);
        var workerName = activitiesCollected.First().GetTagItem("worker.name")?.ToString();
        Assert.NotNull(workerName);
        Assert.StartsWith("worker-", workerName);
    }
}
