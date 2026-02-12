namespace MinimalWorker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fluent builder interface for configuring background workers.
/// Provides methods for naming workers and handling errors.
/// </summary>
/// <remarks>
/// <para>
/// Use the builder methods to customize worker behavior:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="WithName"/> - Assigns a descriptive name for logs, metrics, and traces</description></item>
/// <item><description><see cref="WithErrorHandler"/> - Provides custom error handling instead of application termination</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// host.RunPeriodicBackgroundWorker(TimeSpan.FromMinutes(5), async (token) => { })
///     .WithName("data-sync")
///     .WithErrorHandler(ex => logger.LogError(ex, "Sync failed"));
/// </code>
/// </example>
public interface IWorkerBuilder
{
    /// <summary>
    /// Sets a descriptive name for the worker. Used in logs, metrics, and distributed traces for easier identification.
    /// </summary>
    /// <param name="name">The name to assign to the worker. Should be unique and descriptive (e.g., "order-processor", "cache-cleanup").</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// If not set, a default name "worker-{id}" is generated.
    /// The name appears in:
    /// <list type="bullet">
    /// <item><description>Log messages (category: MinimalWorker.{name})</description></item>
    /// <item><description>Metrics (worker.name tag)</description></item>
    /// <item><description>Distributed traces (worker.name attribute)</description></item>
    /// </list>
    /// </remarks>
    IWorkerBuilder WithName(string name);

    /// <summary>
    /// Sets an error handler for unhandled exceptions in the worker.
    /// </summary>
    /// <param name="handler">The error handler delegate that receives the exception.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// <b>Important:</b> Without an error handler, unhandled exceptions will terminate the application (fail-fast behavior).
    /// </para>
    /// <para>
    /// The error handler is called for each exception. After the handler returns:
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Periodic/Cron workers:</b> Continue running and will execute on next schedule</description></item>
    /// <item><description><b>Continuous workers:</b> Worker stops (user controls the loop)</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// host.RunPeriodicBackgroundWorker(TimeSpan.FromMinutes(5), async (api, token) =>
    ///     {
    ///         await api.SyncDataAsync(token);
    ///     })
    ///     .WithErrorHandler(ex =>
    ///     {
    ///         telemetry.TrackException(ex);
    ///         // Worker continues on next interval
    ///     });
    /// </code>
    /// </example>
    IWorkerBuilder WithErrorHandler(Action<Exception> handler);

    /// <summary>
    /// Sets a timeout for each worker execution. If the execution exceeds this duration, it will be cancelled.
    /// </summary>
    /// <param name="timeout">The maximum duration for each execution. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The timeout applies to each individual execution, not the worker's total lifetime.
    /// </para>
    /// <para>
    /// When a timeout occurs:
    /// </para>
    /// <list type="bullet">
    /// <item><description>The <see cref="CancellationToken"/> passed to the delegate is cancelled</description></item>
    /// <item><description>A <see cref="TimeoutException"/> is raised (handled by error handler if configured)</description></item>
    /// <item><description><b>Periodic/Cron workers:</b> Continue running and will execute on next schedule</description></item>
    /// <item><description><b>Continuous workers:</b> Worker stops</description></item>
    /// </list>
    /// <para>
    /// <b>Important:</b> The delegate must respect the <see cref="CancellationToken"/> for timeout to work effectively.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Data sync with 4 minute timeout (runs every 5 minutes)
    /// host.RunPeriodicBackgroundWorker(TimeSpan.FromMinutes(5), async (api, token) =>
    ///     {
    ///         await api.SyncDataAsync(token);
    ///     })
    ///     .WithTimeout(TimeSpan.FromMinutes(4))
    ///     .WithErrorHandler(ex =>
    ///     {
    ///         if (ex is TimeoutException)
    ///             logger.LogWarning("Sync timed out, will retry next interval");
    ///         else
    ///             logger.LogError(ex, "Sync failed");
    ///     });
    /// </code>
    /// </example>
    IWorkerBuilder WithTimeout(TimeSpan timeout);

    /// <summary>
    /// Configures automatic retry behavior for failed worker executions.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of retry attempts. Must be at least 1. Default is 3.</param>
    /// <param name="delay">The delay between retry attempts. Must be greater than <see cref="TimeSpan.Zero"/>. Default is 5 seconds.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Retry behavior varies by worker type:
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Periodic/Cron workers:</b> Retries occur within the current execution window before moving to next scheduled run</description></item>
    /// <item><description><b>Continuous workers:</b> Retries continue until success or max attempts exhausted</description></item>
    /// </list>
    /// <para>
    /// After all retry attempts are exhausted:
    /// </para>
    /// <list type="bullet">
    /// <item><description>The error handler is invoked (if configured)</description></item>
    /// <item><description>Without an error handler, the application terminates (fail-fast)</description></item>
    /// <item><description><b>Periodic/Cron workers:</b> Will run again on the next scheduled interval</description></item>
    /// </list>
    /// <para>
    /// <b>Note:</b> Retries do not occur for <see cref="OperationCanceledException"/> (graceful shutdown) or <see cref="TimeoutException"/> (if timeout is configured).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Retry API calls up to 5 times with 10 second delay
    /// host.RunPeriodicBackgroundWorker(TimeSpan.FromMinutes(5), async (api, token) =>
    ///     {
    ///         await api.SyncDataAsync(token);
    ///     })
    ///     .WithRetry(maxAttempts: 5, delay: TimeSpan.FromSeconds(10))
    ///     .WithErrorHandler(ex => logger.LogError(ex, "Sync failed after all retries"));
    /// </code>
    /// </example>
    IWorkerBuilder WithRetry(int maxAttempts = 3, TimeSpan? delay = null);
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

    public IWorkerBuilder WithTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than zero.");

        _registration.Timeout = timeout;
        return this;
    }

    public IWorkerBuilder WithRetry(int maxAttempts = 3, TimeSpan? delay = null)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Max attempts must be at least 1.");

        var actualDelay = delay ?? TimeSpan.FromSeconds(5);
        if (actualDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay must be greater than zero.");

        _registration.RetryMaxAttempts = maxAttempts;
        _registration.RetryDelay = actualDelay;
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
        if (type == typeof(void)) return "void";
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
    /// Registers a continuous background worker that executes the specified delegate once when the application starts.
    /// </summary>
    /// <param name="host">The <see cref="IHost"/> to register the background worker on.</param>
    /// <param name="action">
    /// A delegate representing the work to be executed. Supported signatures:
    /// <list type="bullet">
    /// <item><description><c>void</c> or <c>Task</c> return types</description></item>
    /// <item><description>Zero or more DI-resolved parameters</description></item>
    /// <item><description>At most one <see cref="CancellationToken"/> parameter (auto-injected)</description></item>
    /// </list>
    /// </param>
    /// <returns>An <see cref="IWorkerBuilder"/> for configuring additional worker options.</returns>
    /// <remarks>
    /// <para>
    /// <b>Scoping:</b> A single DI scope is created for the worker's entire lifetime.
    /// </para>
    /// <para>
    /// <b>Important:</b> The delegate executes exactly once. If you need repetition, include your own loop:
    /// </para>
    /// <code>
    /// host.RunBackgroundWorker(async (CancellationToken token) =>
    /// {
    ///     while (!token.IsCancellationRequested)
    ///     {
    ///         // Your work here
    ///         await Task.Delay(1000, token);
    ///     }
    /// });
    /// </code>
    /// <para>
    /// This method uses source generators for strongly-typed, reflection-free, AOT-compatible execution.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// host.RunBackgroundWorker(async (IMessageQueue queue, CancellationToken token) =>
    /// {
    ///     while (!token.IsCancellationRequested)
    ///     {
    ///         var message = await queue.DequeueAsync(token);
    ///         await ProcessMessageAsync(message);
    ///     }
    /// }).WithName("message-processor").WithErrorHandler(ex => logger.LogError(ex, "Processing failed"));
    /// </code>
    /// </example>
    public static IWorkerBuilder RunBackgroundWorker(this IHost host, Delegate action)
    {
        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var parameters = action.Method.GetParameters();
        var paramSignature = string.Join(",", parameters.Select(p => FormatTypeName(p.ParameterType)));
        var returnType = FormatTypeName(action.Method.ReturnType);

        var registration = new WorkerRegistration
        {
            Id = id,
            Name = null,
            Action = action,
            Type = WorkerType.Continuous,
            Host = host,
            ParameterCount = parameters.Length,
            Signature = $"{WorkerType.Continuous}:{paramSignature}:{returnType}",
            OnError = null
        };

        _registrations.Add(registration);
        EnsureInitialized(host);

        return new WorkerBuilder(registration);
    }

    /// <summary>
    /// Registers a periodic background worker that executes the specified delegate at a fixed time interval.
    /// </summary>
    /// <param name="host">The <see cref="IHost"/> to register the background worker on.</param>
    /// <param name="timespan">The interval between executions. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="action">
    /// A delegate representing the work to be executed periodically. Supported signatures:
    /// <list type="bullet">
    /// <item><description><c>void</c> or <c>Task</c> return types</description></item>
    /// <item><description>Zero or more DI-resolved parameters</description></item>
    /// <item><description>At most one <see cref="CancellationToken"/> parameter (auto-injected)</description></item>
    /// </list>
    /// </param>
    /// <returns>An <see cref="IWorkerBuilder"/> for configuring additional worker options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timespan"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    /// <b>Scoping:</b> A new DI scope is created for each execution. Scoped services are disposed after each run.
    /// </para>
    /// <para>
    /// <b>Important:</b> Do NOT add your own loop - the framework handles repetition automatically.
    /// The interval starts <i>after</i> each execution completes.
    /// </para>
    /// <para>
    /// This method uses source generators for strongly-typed, reflection-free, AOT-compatible execution.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Cleanup cache every 5 minutes
    /// host.RunPeriodicBackgroundWorker(TimeSpan.FromMinutes(5), async (ICacheService cache, CancellationToken token) =>
    /// {
    ///     await cache.CleanupExpiredEntriesAsync(token);
    /// }).WithName("cache-cleanup");
    /// </code>
    /// </example>
    public static IWorkerBuilder RunPeriodicBackgroundWorker(this IHost host, TimeSpan timespan, Delegate action)
    {
        if (timespan <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timespan), timespan, "TimeSpan must be greater than zero.");

        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var parameters = action.Method.GetParameters();
        var paramSignature = string.Join(",", parameters.Select(p => FormatTypeName(p.ParameterType)));
        var returnType = FormatTypeName(action.Method.ReturnType);

        var registration = new WorkerRegistration
        {
            Id = id,
            Name = null,
            Action = action,
            Type = WorkerType.Periodic,
            Schedule = timespan,
            Host = host,
            ParameterCount = parameters.Length,
            Signature = $"{WorkerType.Periodic}:{paramSignature}:{returnType}",
            OnError = null
        };

        _registrations.Add(registration);
        EnsureInitialized(host);

        return new WorkerBuilder(registration);
    }

    /// <summary>
    /// Registers a cron-scheduled background worker that executes the specified delegate according to a cron expression.
    /// </summary>
    /// <param name="host">The <see cref="IHost"/> to register the background worker on.</param>
    /// <param name="cronExpression">
    /// A cron expression string defining the schedule (UTC timezone).
    /// Standard 5-field format: minute, hour, day-of-month, month, day-of-week.
    /// Examples:
    /// <list type="bullet">
    /// <item><description><c>"* * * * *"</c> - Every minute</description></item>
    /// <item><description><c>"*/15 * * * *"</c> - Every 15 minutes</description></item>
    /// <item><description><c>"0 * * * *"</c> - Every hour at minute 0</description></item>
    /// <item><description><c>"0 0 * * *"</c> - Daily at midnight</description></item>
    /// <item><description><c>"0 0 * * 0"</c> - Weekly on Sunday at midnight</description></item>
    /// </list>
    /// </param>
    /// <param name="action">
    /// A delegate representing the work to be executed. Supported signatures:
    /// <list type="bullet">
    /// <item><description><c>void</c> or <c>Task</c> return types</description></item>
    /// <item><description>Zero or more DI-resolved parameters</description></item>
    /// <item><description>At most one <see cref="CancellationToken"/> parameter (auto-injected)</description></item>
    /// </list>
    /// </param>
    /// <returns>An <see cref="IWorkerBuilder"/> for configuring additional worker options.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cronExpression"/> is null, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// <b>Scoping:</b> A new DI scope is created for each execution. Scoped services are disposed after each run.
    /// </para>
    /// <para>
    /// <b>Timezone:</b> All cron expressions are evaluated in UTC.
    /// </para>
    /// <para>
    /// <b>Important:</b> Do NOT add your own loop - the framework handles scheduling automatically.
    /// </para>
    /// <para>
    /// This method uses source generators for strongly-typed, reflection-free, AOT-compatible execution.
    /// Uses NCrontab for cron expression parsing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Generate report at 2 AM daily
    /// host.RunCronBackgroundWorker("0 2 * * *", async (IReportService reports, CancellationToken token) =>
    /// {
    ///     await reports.GenerateDailyReportAsync(token);
    /// }).WithName("daily-report");
    /// </code>
    /// </example>
    public static IWorkerBuilder RunCronBackgroundWorker(this IHost host, string cronExpression, Delegate action)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression cannot be null or empty.", nameof(cronExpression));

        var id = System.Threading.Interlocked.Increment(ref _registrationCounter);
        var parameters = action.Method.GetParameters();
        var paramSignature = string.Join(",", parameters.Select(p => FormatTypeName(p.ParameterType)));
        var returnType = FormatTypeName(action.Method.ReturnType);

        var registration = new WorkerRegistration
        {
            Id = id,
            Name = null,
            Action = action,
            Type = WorkerType.Cron,
            Schedule = cronExpression,
            Host = host,
            ParameterCount = parameters.Length,
            Signature = $"{WorkerType.Cron}:{paramSignature}:{returnType}",
            OnError = null
        };

        _registrations.Add(registration);
        EnsureInitialized(host);

        return new WorkerBuilder(registration);
    }

    /// <summary>
    /// Represents a registered background worker with its configuration and metadata.
    /// </summary>
    /// <remarks>
    /// This class is used internally by the source generator to create strongly-typed worker initializers.
    /// It captures all information needed to start and manage a background worker.
    /// </remarks>
    public class WorkerRegistration
    {
        /// <summary>
        /// Gets or sets the unique identifier for this worker registration.
        /// Auto-incremented for each registration within an application.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the optional user-provided name for the worker.
        /// Set via <see cref="IWorkerBuilder.WithName"/>.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the delegate to execute. Contains the actual worker logic.
        /// </summary>
        public Delegate Action { get; set; } = null!;

        /// <summary>
        /// Gets or sets the type of worker (Continuous, Periodic, or Cron).
        /// </summary>
        public WorkerType Type { get; set; }

        /// <summary>
        /// Gets or sets the schedule configuration.
        /// <list type="bullet">
        /// <item><description>For Periodic workers: <see cref="TimeSpan"/> interval</description></item>
        /// <item><description>For Cron workers: <see cref="string"/> cron expression</description></item>
        /// <item><description>For Continuous workers: <c>null</c></description></item>
        /// </list>
        /// </summary>
        public object? Schedule { get; set; }

        /// <summary>
        /// Gets or sets the host this worker is registered on.
        /// </summary>
        public IHost Host { get; set; } = null!;

        /// <summary>
        /// Gets or sets the number of parameters in the delegate.
        /// Used for signature matching during code generation.
        /// </summary>
        public int ParameterCount { get; set; }

        /// <summary>
        /// Gets or sets the optional error handler for unhandled exceptions.
        /// Set via <see cref="IWorkerBuilder.WithErrorHandler"/>.
        /// If null, unhandled exceptions terminate the application.
        /// </summary>
        public Action<Exception>? OnError { get; set; }

        /// <summary>
        /// Gets or sets the optional timeout for each worker execution.
        /// Set via <see cref="IWorkerBuilder.WithTimeout"/>.
        /// If null, no timeout is applied.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of retry attempts on failure.
        /// Set via <see cref="IWorkerBuilder.WithRetry"/>.
        /// If null, no automatic retries are performed.
        /// </summary>
        public int? RetryMaxAttempts { get; set; }

        /// <summary>
        /// Gets or sets the delay between retry attempts.
        /// Set via <see cref="IWorkerBuilder.WithRetry"/>.
        /// Only used when <see cref="RetryMaxAttempts"/> is configured.
        /// </summary>
        public TimeSpan? RetryDelay { get; set; }

        /// <summary>
        /// Gets or sets the unique signature based on worker type and parameter types.
        /// Format: "{WorkerType}:{Param1Type},{Param2Type},...".
        /// Used by the source generator to dispatch to the correct initializer.
        /// </summary>
        public string Signature { get; set; } = string.Empty;

        /// <summary>
        /// Gets the display name for this worker.
        /// Returns the user-provided name if set via <see cref="IWorkerBuilder.WithName"/>,
        /// otherwise returns a generated name "worker-{Id}".
        /// </summary>
        public string DisplayName => Name ?? $"worker-{Id}";
    }

    /// <summary>
    /// Specifies the type of background worker and its execution behavior.
    /// </summary>
    public enum WorkerType
    {
        /// <summary>
        /// A worker that executes once and runs until completion or cancellation.
        /// The delegate is responsible for its own loop if repetition is needed.
        /// Uses a single DI scope for its entire lifetime.
        /// </summary>
        Continuous,

        /// <summary>
        /// A worker that executes repeatedly at a fixed time interval.
        /// The framework manages the execution loop automatically.
        /// Creates a new DI scope for each execution.
        /// </summary>
        Periodic,

        /// <summary>
        /// A worker that executes according to a cron schedule (UTC timezone).
        /// The framework manages the scheduling automatically.
        /// Creates a new DI scope for each execution.
        /// </summary>
        Cron
    }
}
