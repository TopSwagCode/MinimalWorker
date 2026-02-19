using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalWorker;
using MinimalWorker.Desktop.AvaloniaUI.Sample;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<WorkerStateService>();

var host = builder.Build();

// 1) Periodic worker — every 1 second
host.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(1), (WorkerStateService state) =>
{
    state.RecordExecution("Periodic", $"Tick at {DateTime.Now:HH:mm:ss}");
});

// 2) Slow worker — every 5 seconds (simulates heavier work)
host.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(5), async (WorkerStateService state) =>
{
    await Task.Delay(200); // simulate async work
    state.RecordExecution("SlowTask", $"Completed at {DateTime.Now:HH:mm:ss}");
});

// 3) Cron worker — every minute ("* * * * *")
host.RunCronBackgroundWorker("* * * * *", (WorkerStateService state) =>
{
    state.RecordExecution("Cron", $"Fired at {DateTime.Now:HH:mm:ss}");
});

await host.StartAsync();

var workerState = host.Services.GetRequiredService<WorkerStateService>();

AppBuilder.Configure(() => new App { WorkerState = workerState })
    .UsePlatformDetect()
    .WithInterFont()
    .StartWithClassicDesktopLifetime(args);

await host.StopAsync();
