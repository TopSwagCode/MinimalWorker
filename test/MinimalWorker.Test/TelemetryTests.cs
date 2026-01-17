using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace MinimalWorker.Test;

public class TelemetryTests
{
    [Fact]
    public async Task Worker_Should_Create_Activity_On_Execution()
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
        });

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        // Assert
        Assert.NotEmpty(activitiesCollected);
        var activity = activitiesCollected.First();
        Assert.Equal("worker.execute", activity.OperationName);
        Assert.NotNull(activity.GetTagItem("worker.id"));
        Assert.NotNull(activity.GetTagItem("worker.name"));
        Assert.Equal("continuous", activity.GetTagItem("worker.type"));
    }

    [Fact]
    public async Task Worker_Should_Set_Activity_Tags_Correctly()
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
        }).WithName("test-worker");

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        // Assert
        Assert.NotEmpty(activitiesCollected);
        var activity = activitiesCollected.First();
        Assert.Equal("test-worker", activity.GetTagItem("worker.name"));
        Assert.Equal("continuous", activity.GetTagItem("worker.type"));
        Assert.Equal(1L, activity.GetTagItem("worker.iteration"));
    }

    [Fact]
    public async Task Worker_Should_Record_Exception_In_Activity()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var activitiesCollected = new List<Activity>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "MinimalWorker",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                lock (activitiesCollected)
                {
                    activitiesCollected.Add(activity);
                }
                if (activity.Status == ActivityStatusCode.Error)
                {
                    signal.TrySetResult(true);
                }
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("Test exception");
        }).WithErrorHandler(ex => { /* Suppress */ });

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        // Assert
        Assert.NotEmpty(activitiesCollected);
        var errorActivity = activitiesCollected.FirstOrDefault(a => a.Status == ActivityStatusCode.Error);
        Assert.NotNull(errorActivity);
        Assert.NotNull(errorActivity.GetTagItem("exception.type"));
    }

    [Fact]
    public async Task Worker_Should_Increment_Execution_Counter()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var executionMeasurements = new List<long>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        const int targetCount = 3;

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "worker.executions" && instrument.Meter.Name == "MinimalWorker")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            lock (executionMeasurements)
            {
                executionMeasurements.Add(measurement);
                if (executionMeasurements.Sum() >= targetCount)
                {
                    signal.TrySetResult(true);
                }
            }
        });
        meterListener.Start();

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            await Task.Delay(10, token);
        });

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        meterListener.RecordObservableInstruments();
        await host.StopAsync();

        // Assert
        Assert.NotEmpty(executionMeasurements);
        Assert.True(executionMeasurements.Sum() >= 3, $"Expected at least 3 executions, got {executionMeasurements.Sum()}");
    }

    [Fact]
    public async Task Worker_Should_Increment_Error_Counter_On_Exception()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var errorMeasurements = new List<(long Value, string? ExceptionType)>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "worker.errors" && instrument.Meter.Name == "MinimalWorker")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            string? exceptionType = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "exception.type")
                {
                    exceptionType = tag.Value?.ToString();
                    break;
                }
            }
            lock (errorMeasurements)
            {
                errorMeasurements.Add((measurement, exceptionType));
            }
            signal.TrySetResult(true);
        });
        meterListener.Start();

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            await Task.CompletedTask;
            throw new ArgumentException("Test error");
        }).WithErrorHandler(ex => { /* Suppress */ });

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        // Assert
        Assert.NotEmpty(errorMeasurements);
        // Verify error counter was incremented with exception type tag
        Assert.True(errorMeasurements.All(m => m.Value == 1), "Each error measurement should be 1");
        Assert.True(errorMeasurements.Any(m => m.ExceptionType != null), "Should have exception type tag");
    }

    [Fact]
    public async Task Worker_Should_Record_Duration_Histogram()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var durationMeasurements = new List<double>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        const int targetCount = 3;

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "worker.duration" && instrument.Meter.Name == "MinimalWorker")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            lock (durationMeasurements)
            {
                durationMeasurements.Add(measurement);
                if (durationMeasurements.Count >= targetCount)
                {
                    signal.TrySetResult(true);
                }
            }
        });
        meterListener.Start();

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            await Task.Delay(10, token); // Delay to create measurable duration
        });

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        // Assert
        Assert.NotEmpty(durationMeasurements);
        Assert.All(durationMeasurements, duration => Assert.True(duration >= 0, "Duration should be non-negative"));
        Assert.True(durationMeasurements.Average() >= 5, $"Average duration should be at least 5ms, got {durationMeasurements.Average()}");
    }

    [Fact]
    public async Task PeriodicWorker_Should_Create_Activity_With_Schedule_Tag()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var activitiesCollected = new List<Activity>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeProvider = WorkerTestHelper.CreateTimeProvider();

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

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);
            })
            .Build();

        host.RunPeriodicBackgroundWorker(TimeSpan.FromMinutes(1), async (CancellationToken token) =>
        {
            await Task.CompletedTask;
        });

        // Act
        await host.StartAsync();
        await WorkerTestHelper.AdvanceTimeAsync(timeProvider, TimeSpan.FromMinutes(2));
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        // Assert
        Assert.NotEmpty(activitiesCollected);
        var activity = activitiesCollected.First();
        Assert.Equal("periodic", activity.GetTagItem("worker.type"));
        Assert.NotNull(activity.GetTagItem("worker.schedule"));
    }

    [Fact]
    public async Task Worker_Execution_Counter_Should_Include_Worker_Tags()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var measurementTags = new List<Dictionary<string, object?>>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "worker.executions" && instrument.Meter.Name == "MinimalWorker")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            var tagDict = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagDict[tag.Key] = tag.Value;
            }
            lock (measurementTags)
            {
                measurementTags.Add(tagDict);
            }
            signal.TrySetResult(true);
        });
        meterListener.Start();

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            await Task.Delay(10, token);
        }).WithName("tagged-worker");

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        // Assert
        Assert.NotEmpty(measurementTags);
        var tags = measurementTags.First();
        Assert.True(tags.ContainsKey("worker.id"), "Should have worker.id tag");
        Assert.Equal("tagged-worker", tags["worker.name"]);
        Assert.Equal("continuous", tags["worker.type"]);
    }

    [Fact]
    public async Task Multiple_Workers_Should_Have_Independent_Metrics()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var workerExecutions = new Dictionary<string, int>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        const int targetCountPerWorker = 2;

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "worker.executions" && instrument.Meter.Name == "MinimalWorker")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            string? workerName = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "worker.name")
                {
                    workerName = tag.Value?.ToString();
                    break;
                }
            }
            if (workerName != null)
            {
                lock (workerExecutions)
                {
                    workerExecutions.TryGetValue(workerName, out var count);
                    workerExecutions[workerName] = count + 1;

                    // Signal when both workers have reached target count
                    if (workerExecutions.TryGetValue("worker-alpha", out var alphaCount) && alphaCount >= targetCountPerWorker &&
                        workerExecutions.TryGetValue("worker-beta", out var betaCount) && betaCount >= targetCountPerWorker)
                    {
                        signal.TrySetResult(true);
                    }
                }
            }
        });
        meterListener.Start();

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            await Task.Delay(10, token);
        }).WithName("worker-alpha");

        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            await Task.Delay(10, token);
        }).WithName("worker-beta");

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        // Assert
        Assert.True(workerExecutions.ContainsKey("worker-alpha"), "Should track worker-alpha");
        Assert.True(workerExecutions.ContainsKey("worker-beta"), "Should track worker-beta");
        Assert.True(workerExecutions["worker-alpha"] >= 2, $"worker-alpha should have at least 2 executions, got {workerExecutions["worker-alpha"]}");
        Assert.True(workerExecutions["worker-beta"] >= 2, $"worker-beta should have at least 2 executions, got {workerExecutions["worker-beta"]}");
    }

    [Fact]
    public async Task Worker_Should_Set_Activity_Status_Ok_On_Success()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var completedActivities = new List<Activity>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "MinimalWorker",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                lock (completedActivities)
                {
                    completedActivities.Add(activity);
                }
                if (activity.Status == ActivityStatusCode.Ok)
                {
                    signal.TrySetResult(true);
                }
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        using var host = Host.CreateDefaultBuilder().Build();

        host.RunBackgroundWorker(async (CancellationToken token) =>
        {
            await Task.Delay(5, token);
        });

        // Act
        await host.StartAsync();
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();

        // Assert
        Assert.NotEmpty(completedActivities);
        var successfulActivity = completedActivities.FirstOrDefault(a => a.Status == ActivityStatusCode.Ok);
        Assert.NotNull(successfulActivity);
    }
}
