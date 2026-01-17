using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace MinimalWorker.Test.Helpers;

/// <summary>
/// Helper class for testing workers with FakeTimeProvider.
/// Advances time automatically to trigger periodic and cron workers without real delays.
/// </summary>
public static class WorkerTestHelper
{
    /// <summary>
    /// Creates a FakeTimeProvider with a fixed start time.
    /// </summary>
    public static FakeTimeProvider CreateTimeProvider()
    {
        return new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// Advances time in steps and allows async work to proceed between each step.
    /// This is more reliable than a single Advance() call as it gives timers and
    /// async continuations time to fire at each intermediate point.
    ///
    /// Note: PeriodicTimer fires AFTER each interval, so a 5-minute interval over
    /// 30 minutes gives 5 executions (at 5, 10, 15, 20, 25 min), not 6.
    /// </summary>
    public static async Task AdvanceTimeAsync(FakeTimeProvider timeProvider, TimeSpan amount, int steps = 10)
    {
        var stepSize = TimeSpan.FromTicks(amount.Ticks / steps);
        for (int i = 0; i < steps; i++)
        {
            timeProvider.Advance(stepSize);
            await Task.Yield(); // Allow async continuations to be scheduled
            await Task.Delay(5); // Give time for async work to complete
        }
    }

    /// <summary>
    /// Creates a standard test host with optional service configuration.
    /// Automatically clears registrations and sets up common defaults.
    /// </summary>
    public static IHost CreateTestHost(Action<IServiceCollection>? configureServices = null)
    {
        BackgroundWorkerExtensions.ClearRegistrations();

        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                configureServices?.Invoke(services);
            })
            .Build();
    }

    /// <summary>
    /// Creates a test host with FakeTimeProvider for deterministic time testing.
    /// Automatically clears registrations.
    /// </summary>
    public static (IHost Host, FakeTimeProvider TimeProvider) CreateTestHostWithTimeProvider(
        Action<IServiceCollection>? configureServices = null)
    {
        BackgroundWorkerExtensions.ClearRegistrations();
        var timeProvider = CreateTimeProvider();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
                configureServices?.Invoke(services);
            })
            .Build();

        return (host, timeProvider);
    }

    /// <summary>
    /// Creates an ActivityListener for capturing telemetry activities.
    /// </summary>
    /// <param name="signalOnFirst">If true, signals completion when first activity is captured.</param>
    /// <returns>A tuple containing the listener, collected activities list, and completion signal.</returns>
    public static (ActivityListener Listener, List<Activity> Activities, TaskCompletionSource<bool> Signal)
        CreateActivityCapture(bool signalOnFirst = true)
    {
        var activities = new List<Activity>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "MinimalWorker",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity =>
            {
                lock (activities)
                {
                    activities.Add(activity);
                }
                if (signalOnFirst)
                {
                    signal.TrySetResult(true);
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        return (listener, activities, signal);
    }

    /// <summary>
    /// Creates an ActivityListener that captures stopped activities (useful for checking status codes).
    /// </summary>
    public static (ActivityListener Listener, List<Activity> Activities, TaskCompletionSource<bool> Signal)
        CreateActivityStoppedCapture(Func<Activity, bool>? signalCondition = null)
    {
        var activities = new List<Activity>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "MinimalWorker",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                lock (activities)
                {
                    activities.Add(activity);
                }
                if (signalCondition == null || signalCondition(activity))
                {
                    signal.TrySetResult(true);
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        return (listener, activities, signal);
    }
}
