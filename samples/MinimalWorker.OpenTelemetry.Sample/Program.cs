using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MinimalWorker;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// Configure Logging with OpenTelemetry
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug); // Set to Debug to see more details
builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("MinimalWorker.Sample", serviceVersion: "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = "development",
            ["host.name"] = Environment.MachineName
        }));
    
    options.AddConsoleExporter();
    options.AddOtlpExporter(otlpOptions =>
    {
        otlpOptions.Endpoint = new Uri("http://localhost:4317");
        otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
    });
    
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
});

// Configure OpenTelemetry Tracing and Metrics
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService("MinimalWorker.Sample", serviceVersion: "1.0.0")
    .AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = "development",
        ["host.name"] = Environment.MachineName
    });

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("MinimalWorker.Sample"))
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(resourceBuilder)
            .AddSource("MinimalWorker")
            .SetSampler(new AlwaysOnSampler())
            .AddConsoleExporter(options =>
            {
                options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
            })
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            });
    })
    .WithMetrics(meterProviderBuilder =>
    {
        meterProviderBuilder
            .SetResourceBuilder(resourceBuilder)
            .AddMeter("MinimalWorker")
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
            {
                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000;
            })
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            });
    });

// Register services
builder.Services.AddSingleton<IMessageService, MessageService>();
builder.Services.AddSingleton<CounterService>();

var host = builder.Build();

host.RunBackgroundWorker(async (IMessageService messageService, ILogger<Program> logger) =>
{
    logger.LogInformation("🔄 Continuous worker executing: {Message}", messageService.GetMessage());
    await Task.Delay(500);
}).WithName("message-processor");

host.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(2), async (CounterService counter, ILogger<Program> logger) =>
{
    counter.Increment();
    logger.LogInformation("⏰ Periodic worker executing. Count: {Count}", counter.Count);
    await Task.CompletedTask;
}).WithName("counter-worker");

host.RunCronBackgroundWorker("*/1 * * * *", async (ILogger<Program> logger) =>
{
    logger.LogInformation("📅 Cron worker executing at: {Time:HH:mm:ss}", DateTime.Now);
    await Task.CompletedTask;
}).WithName("cron-reporter");

host.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(3), async (ILogger<Program> logger) =>
    {
        var random = new Random();
        var willFail = random.Next(2) == 0; // 50% chance

        if (willFail)
        {
            logger.LogWarning("🎲 Flaky worker: Attempting operation that might fail...");
            await Task.Delay(100);
            throw new InvalidOperationException("Flaky worker failed! (Simulated random failure)");
        }
        else
        {
            logger.LogInformation("Flaky worker: Success! Operation completed.");
            await Task.Delay(200);
        }
    })
    .WithName("flaky-worker")
    .WithErrorHandler((Exception ex) =>
    {
    });

host.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(5), async (ILogger<Program> logger) =>
{
    var random = new Random();
    var delayMs = random.Next(1000, 3000);

    logger.LogInformation("💤 Slow worker: Starting operation (will take ~{DelayMs}ms)...", delayMs);

    await Task.Delay(delayMs); // Simulate slow operation

    logger.LogInformation("💤 Slow worker: ✅ Completed after {DelayMs}ms", delayMs);
}).WithName("slow-worker");

await host.RunAsync();

public interface IMessageService
{
    string GetMessage();
}

public class MessageService : IMessageService
{
    private int _counter = 0;

    public string GetMessage()
    {
        return $"Message #{Interlocked.Increment(ref _counter)}";
    }
}

public class CounterService
{
    private int _count = 0;

    public int Count => _count;

    public void Increment()
    {
        Interlocked.Increment(ref _count);
    }
}
