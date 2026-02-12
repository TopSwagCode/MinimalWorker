namespace MinimalWorker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fluent builder interface for configuring background workers.
/// </summary>
public interface IWorkerBuilder
{
    /// <summary>
    /// Sets a name for the worker. Used in logs, metrics, and traces for easier identification.
    /// </summary>
    /// <param name="name">The name to assign to the worker.</param>
    /// <returns>The builder instance for method chaining.</returns>
    IWorkerBuilder WithName(string name);

    /// <summary>
    /// Sets an error handler for unhandled exceptions in the worker.
    /// If not provided, exceptions will cause the application to terminate.
    /// </summary>
    /// <param name="handler">The error handler delegate.</param>
    /// <returns>The builder instance for method chaining.</returns>
    IWorkerBuilder WithErrorHandler(Action<Exception> handler);
}

/// <summary>
/// Internal implementation of the worker builder.
/// </summary>
internal class WorkerBuilder : IWorkerBuilder
{
    private readonly BackgroundWorkerExtensions.WorkerRegistration _registration;

    internal WorkerBuilder(BackgroundWorkerExtensions.WorkerRegistration registration)
    {
        _registration = registration;
    }

    public IWorkerBuilder WithName(string name)
    {
        _registration.Name = name;
        return this;
    }

    public IWorkerBuilder WithErrorHandler(Action<Exception> handler)
    {
        _registration.OnError = handler;
        return this;
    }
}

/// <summary>
/// Extension methods for registering background workers with source generator-based code generation.
/// No reflection - fully AOT compatible.
/// </summary>
public static partial class BackgroundWorkerExtensions
{
    private static readonly Action<ILogger, string, Exception?> LogWorkerValidationFailed =
        LoggerMessage.Define<string>(LogLevel.Critical, new EventId(100, "WorkerValidationFailed"),
            "FATAL: Worker dependency validation failed: {Message}");

    /// <summary>
    /// Stores worker registrations for source generator processing.
    /// </summary>
    public static readonly List<WorkerRegistration> _registrations = new();
    private static int _registrationCounter = 0;
    private static bool _isInitialized = false;

    #if NET9_0_OR_GREATER
        private static readonly Lock _lock = new();
    #else
        private static readonly object _lock = new();
    #endif

    /// <summary>
    /// Internal flag to control whether to use Environment.Exit on validation failure.
    /// When false (for testing), throws exception instead. Default is true (production behavior).
    /// </summary>
    internal static bool _useEnvironmentExit = true;

    /// <summary>
    /// Stores the host application lifetime for graceful shutdown.
    /// </summary>
    private static IHostApplicationLifetime? _lifetime;

    /// <summary>
    /// Internal method to terminate the application on fatal worker errors.
    /// Can be controlled via _useEnvironmentExit for testing purposes.
    /// </summary>
    public static void TerminateOnFatalError(Exception ex)
    {
        if (_useEnvironmentExit)
        {
            Environment.ExitCode = 1;
            _lifetime?.StopApplication();
        }
        else
        {
            throw new InvalidOperationException("Fatal worker error", ex);
        }
    }

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
            _useEnvironmentExit = true; // Reset to default
            _lifetime = null; // Reset lifetime reference
        }
    }

    /// <summary>
    /// Formats a type name to match the source generator format.
    /// </summary>
    private static string FormatTypeName(Type type)
    {
        // Map common types to their C# keyword equivalents
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(short)) return "short";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(sbyte)) return "sbyte";
        if (type == typeof(uint)) return "uint";
        if (type == typeof(ulong)) return "ulong";
        if (type == typeof(ushort)) return "ushort";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(char)) return "char";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(object)) return "object";

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        // For generic types like IRepository<string>, format as "Namespace.IRepository<string>"
        var genericTypeDef = type.GetGenericTypeDefinition();
        var genericArgs = type.GetGenericArguments();
        var baseName = genericTypeDef.FullName ?? genericTypeDef.Name;

        // Remove the `1, `2 suffix from generic type names
        var tickIndex = baseName.IndexOf('`');
        if (tickIndex > 0)
        {
            baseName = baseName.Substring(0, tickIndex);
        }

        var formattedArgs = string.Join(",", genericArgs.Select(FormatTypeName));
        return $"{baseName}<{formattedArgs}>";
    }

    /// <summary>
    /// Internal action that will be set by the generated code to initialize workers.
    /// </summary>
    public static Action<IHost>? _generatedWorkerInitializer;

    /// <summary>
    /// Internal method to ensure initialization hook is registered.
    /// Called automatically by Run* methods.
    /// </summary>
    private static void EnsureInitialized(IHost host)
    {
        lock (_lock)
        {
            if (_isInitialized)
                return;

            _isInitialized = true;

            var loggerFactory = host.Services.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger("MinimalWorker.BackgroundWorkerExtensions");

            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            _lifetime = lifetime;
            lifetime.ApplicationStarted.Register(() =>
            {
                try
                {
                    // Initialize all workers and validate their dependencies
                    _generatedWorkerInitializer?.Invoke(host);
                }
                catch (Exception ex)
                {
                    if (logger != null)
                    {
                        LogWorkerValidationFailed(logger, ex.Message, ex);
                    }

                    // In production, trigger graceful shutdown with error code
                    // In tests, throw to allow test frameworks to handle it
                    if (_useEnvironmentExit)
                    {
                        Environment.ExitCode = 1;
                        lifetime.StopApplication();
                    }
                    else
                    {
                        throw;
                    }
                }
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
    /// <returns>A builder for configuring additional worker options like name and error handling.</returns>
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
    ///     {
    ///         Console.WriteLine("Running background task...");
    ///         await Task.Delay(1000, token);
    ///     }
    /// }).WithName("order-processor").WithErrorHandler(ex => Console.WriteLine(ex));
    /// </code>
    /// </example>
    public static IWorkerBuilder RunBackgroundWorker(this IHost host, Delegate action)
    {
        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var parameters = action.Method.GetParameters();
        var signature = string.Join(",", parameters.Select(p => FormatTypeName(p.ParameterType)));

        var registration = new WorkerRegistration
        {
            Id = id,
            Name = null,
            Action = action,
            Type = WorkerType.Continuous,
            Host = host,
            ParameterCount = parameters.Length,
            Signature = $"{WorkerType.Continuous}:{signature}",
            OnError = null
        };

        _registrations.Add(registration);
        EnsureInitialized(host);

        return new WorkerBuilder(registration);
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
    /// <returns>A builder for configuring additional worker options like name and error handling.</returns>
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
    /// }).WithName("cache-cleanup");
    /// </code>
    /// </example>
    public static IWorkerBuilder RunPeriodicBackgroundWorker(this IHost host, TimeSpan timespan, Delegate action)
    {
        if (timespan <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timespan), timespan, "TimeSpan must be greater than zero.");

        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var parameters = action.Method.GetParameters();
        var signature = string.Join(",", parameters.Select(p => FormatTypeName(p.ParameterType)));

        var registration = new WorkerRegistration
        {
            Id = id,
            Name = null,
            Action = action,
            Type = WorkerType.Periodic,
            Schedule = timespan,
            Host = host,
            ParameterCount = parameters.Length,
            Signature = $"{WorkerType.Periodic}:{signature}",
            OnError = null
        };

        _registrations.Add(registration);
        EnsureInitialized(host);

        return new WorkerBuilder(registration);
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
    /// <returns>A builder for configuring additional worker options like name and error handling.</returns>
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
    /// }).WithName("nightly-report");
    /// </code>
    /// </example>
    public static IWorkerBuilder RunCronBackgroundWorker(this IHost host, string cronExpression, Delegate action)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression cannot be null or empty.", nameof(cronExpression));

        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var parameters = action.Method.GetParameters();
        var signature = string.Join(",", parameters.Select(p => FormatTypeName(p.ParameterType)));

        var registration = new WorkerRegistration
        {
            Id = id,
            Name = null,
            Action = action,
            Type = WorkerType.Cron,
            Schedule = cronExpression,
            Host = host,
            ParameterCount = parameters.Length,
            Signature = $"{WorkerType.Cron}:{signature}",
            OnError = null
        };

        _registrations.Add(registration);
        EnsureInitialized(host);

        return new WorkerBuilder(registration);
    }

    /// <summary>
    /// Represents a registered background worker.
    /// </summary>
    public class WorkerRegistration
    {
        public int Id { get; set; }
        public string? Name { get; set; } // Optional user-provided name for the worker
        public Delegate Action { get; set; } = null!;
        public WorkerType Type { get; set; }
        public object? Schedule { get; set; }
        public IHost Host { get; set; } = null!;
        public int ParameterCount { get; set; } // Number of parameters in the delegate
        public Action<Exception>? OnError { get; set; } // Optional error handler
        public string Signature { get; set; } = string.Empty; // Unique signature based on parameter types

        /// <summary>
        /// Gets the display name for this worker. Returns the user-provided name if set,
        /// otherwise returns a generated name based on the worker ID.
        /// </summary>
        public string DisplayName => Name ?? $"worker-{Id}";
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
