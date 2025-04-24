using System.Reflection;
using NCrontab;

namespace MinimalWorker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class BackgroundWorkerExtensions
{
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