namespace MinimalWorker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class BackgroundWorkerExtensions
{
    public static void MapBackgroundWorker(this IHost host, Delegate action)
    {
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        lifetime.ApplicationStarted.Register(() =>
        {
            var token = lifetime.ApplicationStopping;
            _ = Task.Run(async () =>
            {
                using var scope = host.Services.CreateScope();
                var args = GetRequiredArguments(scope.ServiceProvider, action, token);

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

        lifetime.ApplicationStarted.Register(() =>
        {
            var token = lifetime.ApplicationStopping;
            _ = Task.Run(async () =>
            {
                var timer = new PeriodicTimer(timespan);
                while (await timer.WaitForNextTickAsync(token))
                {
                    using var scope = host.Services.CreateScope();
                    var args = GetRequiredArguments(scope.ServiceProvider, action, token);
                    
                    var result = action.DynamicInvoke(args);

                    if (result is Task task)
                    {
                        await task;
                    }
                }
            }, token);
        });
    }

    private static object[] GetRequiredArguments(IServiceProvider serviceProvider, Delegate action, CancellationToken token)
    {
        var parameters = action.Method.GetParameters();
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