using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalWorker;
using MinimalWorker.Aot.Sample;

Console.WriteLine("Starting AOT-compiled MinimalWorker application...");

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddScoped<IConsoleOutputService, ConsoleOutputService>();

var app = builder.Build();

app.RunBackgroundWorker(async (IConsoleOutputService consoleOutputService, CancellationToken ct) =>
{
    await consoleOutputService.WriteLineAsync($"[{DateTime.Now:HH:mm:ss}] Continuous worker executing (every second)");
    await Task.Delay(1000, ct);
}); // Todo - Add .WithName(); or default work.name = "RunBackgroundWorker"

app.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(2), async (IConsoleOutputService consoleOutputService, CancellationToken ct) =>
{
    await consoleOutputService.WriteLineAsync($"[{DateTime.Now:HH:mm:ss}] Periodic worker executing (every 2 seconds)");
    return Task.CompletedTask;
});

app.RunCronBackgroundWorker("* * * * *", async (IConsoleOutputService consoleOutputService, CancellationToken ct) =>
{
    await consoleOutputService.WriteLineAsync($"[{DateTime.Now:HH:mm:ss}] Cron worker executing (every minute)");
    return Task.CompletedTask;
});

Console.WriteLine("Workers started. Press Ctrl+C to stop.");
await app.RunAsync();

Console.WriteLine("Application stopped.");
