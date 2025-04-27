using System.Reflection;
using NCrontab;

namespace MinimalWorker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class BackgroundWorkerExtensions
{
    /// <summary>
    /// Maps a background worker that continuously executes the specified delegate while the application is running.
    /// </summary>
    /// <param name="host">The <see cref="IHost"/> to register the background worker on.</param>
    /// <param name="action">
    /// A delegate representing the work to be executed. 
    /// It can return a <see cref="Task"/> for asynchronous work, or <c>void</c> for synchronous work.
    /// </param>
    /// <remarks>
    /// The worker will start when the application starts and run in a continuous loop until shutdown.
    /// Dependency injection is supported for method parameters.
    /// </remarks>
    /// <example>
    /// Example usage:
    /// <code>
    /// host.MapBackgroundWorker(async (CancellationToken token) =>
    /// {
    ///     while (!token.IsCancellationRequested)
    ///     {
    ///         Console.WriteLine("Running background task...");
    ///         await Task.Delay(1000, token);
    ///     }
    /// });
    /// </code>
    /// </example>
    public static void MapBackgroundWorker(this IHost host, Delegate action)
    {
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var parameters = action.Method.GetParameters();
        
        lifetime.ApplicationStarted.Register(() =>
        {
            var token = lifetime.ApplicationStopping;
            _ = Task.Run(async () =>
            {
                using var scope = host.Services.CreateScope();
                var args = GetRequiredArguments(scope.ServiceProvider, parameters, token);

                while (!token.IsCancellationRequested)
                {
                    var result = action.DynamicInvoke(args);

                    if (result is Task task)
                    {
                        await task;
                    }
                }
                
            }, token);
        });
    }
    
    /// <summary>
    /// Maps a periodic background worker that executes the specified delegate at a fixed time interval.
    /// </summary>
    /// <param name="host">The <see cref="IHost"/> to register the background worker on.</param>
    /// <param name="timespan">The <see cref="TimeSpan"/> interval between executions.</param>
    /// <param name="action">
    /// A delegate representing the work to be executed periodically.
    /// It can return a <see cref="Task"/> for asynchronous work, or <c>void</c> for synchronous work.
    /// </param>
    /// <remarks>
    /// The worker starts after the application is started and will execute the action repeatedly based on the specified interval.
    /// Dependency injection is supported for method parameters.
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
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var parameters = action.Method.GetParameters();
        
        lifetime.ApplicationStarted.Register(() =>
        {
            var token = lifetime.ApplicationStopping;
            _ = Task.Run(async () =>
            {
                var timer = new PeriodicTimer(timespan);
                while (await timer.WaitForNextTickAsync(token))
                {
                    using var scope = host.Services.CreateScope();
                    var args = GetRequiredArguments(scope.ServiceProvider, parameters, token);
                    
                    var result = action.DynamicInvoke(args);

                    if (result is Task task)
                    {
                        await task;
                    }
                }
            }, token);
        });
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
    /// It can return a <see cref="Task"/> for asynchronous work, or <c>void</c> for synchronous work.
    /// </param>
    /// <remarks>
    /// The worker schedules the execution based on the next occurrence derived from the cron expression.
    /// Dependency injection is supported for method parameters.
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
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var parameters = action.Method.GetParameters();
        var schedule = CrontabSchedule.Parse(cronExpression);

        lifetime.ApplicationStarted.Register(() =>
        {
            var token = lifetime.ApplicationStopping;
            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var nextRun = schedule.GetNextOccurrence(DateTime.UtcNow);
                    var delay = nextRun - DateTime.UtcNow;

                    if (delay > TimeSpan.Zero)
                    {
                        try
                        {
                            await Task.Delay(delay, token);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }

                    using var scope = host.Services.CreateScope();
                    var args = GetRequiredArguments(scope.ServiceProvider, parameters, token);
                    var result = action.DynamicInvoke(args);

                    if (result is Task task)
                    {
                        await task;
                    }
                }
            }, token);
        });
    }
    
    private static object[] GetRequiredArguments(IServiceProvider serviceProvider, ParameterInfo[] parameters, CancellationToken token)
    {
        var args = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(CancellationToken))
            {
                args[i] = token;
            }
            else
            {
                args[i] = serviceProvider.GetRequiredService(parameters[i].ParameterType);
            }
        }

        return args;
    }
}