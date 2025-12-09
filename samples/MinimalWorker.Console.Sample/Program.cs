using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalWorker;
using MinimalWorker.Shared.Sample;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ChannelService>();

var app = builder.Build();

app.MapBackgroundWorker(async (CancellationToken ct, ChannelService channelService) =>
{
    await foreach (var str in channelService.ReadAllNotificationsAsync(ct))
    {
        Console.WriteLine("Background worker running at {0}", DateTime.Now);
    }
});

app.MapPeriodicBackgroundWorker(TimeSpan.FromSeconds(1), async (CancellationToken ct, ChannelService channelService) =>
{
    Console.WriteLine("Periodic Background worker running at {0}", DateTime.Now);
    await channelService.SendNotificationAsync("Hello from Periodic Background worker!");
});

app.MapCronBackgroundWorker("* * * * *", async (CancellationToken ct, ChannelService channelService) =>
{
    Console.WriteLine("Cron Background worker running at {0}", DateTime.Now);
    await channelService.SendNotificationAsync("Hello from Cron Background worker!");
});

await app.RunAsync();