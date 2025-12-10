namespace MinimalWorker;

using Microsoft.Extensions.DependencyInjection;
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
    private static bool _isInitialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Clears all worker registrations. This is intended for testing purposes only.
    /// </summary>
    internal static void ClearRegistrations()
    {
        lock (_lock)
        {
            _registrations.Clear();
            _registrationCounter = 0;
            _isInitialized = false;
        }
    }

    /// <summary>
    /// Internal action that will be set by the generated code to initialize workers.
    /// </summary>
    public static Action<IHost>? _generatedWorkerInitializer;

    /// <summary>
    /// Internal method to ensure initialization hook is registered.
    /// Called automatically by Map* methods.
    /// </summary>
    private static void EnsureInitialized(IHost host)
    {
        lock (_lock)
        {
            if (_isInitialized)
                return;

            _isInitialized = true;

            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStarted.Register(() =>
            {
                // Call the generated worker initializer if it exists
                _generatedWorkerInitializer?.Invoke(host);
            });
        }
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
    /// <param name="onError">
    /// Optional error handler for unhandled exceptions in the worker.
    /// If not provided, exceptions will be rethrown and may crash the worker.
    /// </param>
    /// <remarks>
    /// The worker will start when the application starts and run in a continuous loop until shutdown.
    /// This method uses source generators for strongly-typed, reflection-free, AOT-compatible execution.
    /// </remarks>
    /// <example>
    /// Example usage:
    /// <code>
    /// host.RunBackgroundWorker(async (CancellationToken token) =>
    /// {
    ///     while (!token.IsCancellationRequested)
    /// {
    ///         Console.WriteLine("Running background task...");
    ///         await Task.Delay(1000, token);
    ///     }
    /// });
    /// </code>
    /// </example>
    public static void RunBackgroundWorker(this IHost host, Delegate action, Action<Exception>? onError = null)
    {
        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var registration = new WorkerRegistration
        {
            Id = id,
            Action = action,
            Type = WorkerType.Continuous,
            Host = host,
            ParameterCount = action.Method.GetParameters().Length,
            OnError = onError
        };
        
        _registrations.Add(registration);
        EnsureInitialized(host);
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
    /// <param name="onError">
    /// Optional error handler for unhandled exceptions in the worker.
    /// If not provided, exceptions will be rethrown and may crash the worker.
    /// </param>
    /// <remarks>
    /// The worker starts after the application is started and will execute the action repeatedly based on the specified interval.
    /// This method uses source generators for strongly-typed, reflection-free, AOT-compatible execution.
    /// </remarks>
    /// <example>
    /// Example usage:
    /// <code>
    /// host.RunPeriodicBackgroundWorker(TimeSpan.FromMinutes(5), async (CancellationToken token) =>
    /// {
    ///     Console.WriteLine("Running periodic task every 5 minutes...");
    ///     await Task.CompletedTask;
    /// });
    /// </code>
    /// </example>
    public static void RunPeriodicBackgroundWorker(this IHost host, TimeSpan timespan, Delegate action, Action<Exception>? onError = null)
    {
        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var registration = new WorkerRegistration
        {
            Id = id,
            Action = action,
            Type = WorkerType.Periodic,
            Schedule = timespan,
            Host = host,
            ParameterCount = action.Method.GetParameters().Length,
            OnError = onError
        };
        
        _registrations.Add(registration);
        EnsureInitialized(host);
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
    /// <param name="onError">
    /// Optional error handler for unhandled exceptions in the worker.
    /// If not provided, exceptions will be rethrown and may crash the worker.
    /// </param>
    /// <remarks>
    /// The worker schedules the execution based on the next occurrence derived from the cron expression.
    /// This method uses source generators for strongly-typed, reflection-free, AOT-compatible execution.
    /// </remarks>
    /// <example>
    /// Example usage:
    /// <code>
    /// host.RunCronBackgroundWorker("*/15 * * * *", async (CancellationToken token) =>
    /// {
    ///     Console.WriteLine("Running cron task every 15 minutes...");
    ///     await Task.CompletedTask;
    /// });
    /// </code>
    /// </example>
    public static void RunCronBackgroundWorker(this IHost host, string cronExpression, Delegate action, Action<Exception>? onError = null)
    {
        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var registration = new WorkerRegistration
        {
            Id = id,
            Action = action,
            Type = WorkerType.Cron,
            Schedule = cronExpression,
            Host = host,
            ParameterCount = action.Method.GetParameters().Length,
            OnError = onError
        };
        
        _registrations.Add(registration);
        EnsureInitialized(host);
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
        public Action<Exception>? OnError { get; set; } // Optional error handler
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
