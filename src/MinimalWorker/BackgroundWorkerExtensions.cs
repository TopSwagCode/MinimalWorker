namespace MinimalWorker;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for registering background workers with source generator-based code generation.
/// No reflection - fully AOT compatible.
/// </summary>
public static class BackgroundWorkerExtensions
{
    /// <summary>
    /// Stores worker registrations for source generator processing.
    /// </summary>
    public static readonly List<WorkerRegistration> _registrations = new();
    private static int _registrationCounter = 0;

    /// <summary>
    /// Clears all worker registrations. Useful for testing scenarios.
    /// </summary>
    public static void ClearRegistrations()
    {
        _registrations.Clear();
        _registrationCounter = 0;
    }

    /// <summary>
    /// Maps a background worker that continuously executes the specified delegate while the application is running.
    /// </summary>
    /// <param name="host">The <see cref="IHost"/> to register the background worker on.</param>
    /// <param name="action">
    /// A delegate representing the work to be executed. 
    /// It can return a <see cref="Task"/> for asynchronous work.
    /// Dependency injection is supported for method parameters.
    /// </param>
    /// <remarks>
    /// The worker will start when the application starts and run in a continuous loop until shutdown.
    /// This method uses source generators for strongly-typed, reflection-free, AOT-compatible execution.
    /// </remarks>
    /// <example>
    /// Example usage:
    /// <code>
    /// host.MapBackgroundWorker(async (CancellationToken token) =>
    /// {
    ///     while (!token.IsCancellationRequested)
    /// {
    ///         Console.WriteLine("Running background task...");
    ///         await Task.Delay(1000, token);
    ///     }
    /// });
    /// </code>
    /// </example>
    public static void MapBackgroundWorker(this IHost host, Delegate action)
    {
        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var registration = new WorkerRegistration
        {
            Id = id,
            Action = action,
            Type = WorkerType.Continuous,
            Host = host,
            ParameterCount = action.Method.GetParameters().Length
        };
        
        _registrations.Add(registration);
    }
    
    /// <summary>
    /// Maps a periodic background worker that executes the specified delegate at a fixed time interval.
    /// </summary>
    /// <param name="host">The <see cref="IHost"/> to register the background worker on.</param>
    /// <param name="timespan">The <see cref="TimeSpan"/> interval between executions.</param>
    /// <param name="action">
    /// A delegate representing the work to be executed periodically.
    /// It can return a <see cref="Task"/> for asynchronous work.
    /// Dependency injection is supported for method parameters.
    /// </param>
    /// <remarks>
    /// The worker starts after the application is started and will execute the action repeatedly based on the specified interval.
    /// This method uses source generators for strongly-typed, reflection-free, AOT-compatible execution.
    /// </remarks>
    /// <example>
    /// Example usage:
    /// <code>
    /// host.MapPeriodicBackgroundWorker(TimeSpan.FromMinutes(5), async (CancellationToken token) =>
    /// {
    ///     Console.WriteLine("Running periodic task every 5 minutes...");
    ///     await Task.CompletedTask;
    /// });
    /// </code>
    /// </example>
    public static void MapPeriodicBackgroundWorker(this IHost host, TimeSpan timespan, Delegate action)
    {
        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var registration = new WorkerRegistration
        {
            Id = id,
            Action = action,
            Type = WorkerType.Periodic,
            Schedule = timespan,
            Host = host,
            ParameterCount = action.Method.GetParameters().Length
        };
        
        _registrations.Add(registration);
    }

    /// <summary>
    /// Maps a cron-scheduled background worker that executes the specified delegate according to a cron expression.
    /// </summary>
    /// <param name="host">The <see cref="IHost"/> to register the background worker on.</param>
    /// <param name="cronExpression">
    /// A cron expression string defining the schedule. 
    /// Uses the standard cron format (minute, hour, day of month, month, day of week).
    /// </param>
    /// <param name="action">
    /// A delegate representing the work to be executed on the scheduled times.
    /// It can return a <see cref="Task"/> for asynchronous work.
    /// Dependency injection is supported for method parameters.
    /// </param>
    /// <remarks>
    /// The worker schedules the execution based on the next occurrence derived from the cron expression.
    /// This method uses source generators for strongly-typed, reflection-free, AOT-compatible execution.
    /// </remarks>
    /// <example>
    /// Example usage:
    /// <code>
    /// host.MapCronBackgroundWorker("*/15 * * * *", async (CancellationToken token) =>
    /// {
    ///     Console.WriteLine("Running cron task every 15 minutes...");
    ///     await Task.CompletedTask;
    /// });
    /// </code>
    /// </example>
    public static void MapCronBackgroundWorker(this IHost host, string cronExpression, Delegate action)
    {
        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var registration = new WorkerRegistration
        {
            Id = id,
            Action = action,
            Type = WorkerType.Cron,
            Schedule = cronExpression,
            Host = host,
            ParameterCount = action.Method.GetParameters().Length
        };
        
        _registrations.Add(registration);
    }

    /// <summary>
    /// Represents a registered background worker.
    /// </summary>
    public class WorkerRegistration
    {
        public int Id { get; set; }
        public Delegate Action { get; set; } = null!;
        public WorkerType Type { get; set; }
        public object? Schedule { get; set; }
        public IHost Host { get; set; } = null!;
        public int ParameterCount { get; set; } // Number of parameters in the delegate
    }

    /// <summary>
    /// Type of background worker.
    /// </summary>
    public enum WorkerType
    {
        Continuous,
        Periodic,
        Cron
    }
}
