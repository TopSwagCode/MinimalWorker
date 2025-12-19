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
            .AddSource("MinimalWorker") // Subscribe to MinimalWorker ActivitySource
            .SetSampler(new AlwaysOnSampler()) // Sample all traces
            .AddConsoleExporter(options =>
            {
                options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
            })
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317"); // Jaeger OTLP endpoint
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            });
    })
    .WithMetrics(meterProviderBuilder =>
    {
        meterProviderBuilder
            .SetResourceBuilder(resourceBuilder)
            .AddMeter("MinimalWorker") // Subscribe to MinimalWorker Meter
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
            {
                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000; // Export every 5 seconds
            })
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317"); // Prometheus via OTLP
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            })
            .AddPrometheusExporter(); // Also expose /metrics endpoint
    });

// Register services
builder.Services.AddSingleton<IMessageService, MessageService>();
builder.Services.AddSingleton<CounterService>();

var host = builder.Build();

Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║      MinimalWorker with Full OpenTelemetry Stack Sample       ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("📊 Observability Stack:");
Console.WriteLine("  • Logs:    Console + OTLP → Loki (via Collector)");
Console.WriteLine("  • Traces:  Console + OTLP → Jaeger (http://localhost:16686)");
Console.WriteLine("  • Metrics: Console + OTLP → Prometheus (http://localhost:9090)");
Console.WriteLine();
Console.WriteLine("🎯 Dashboards:");
Console.WriteLine("  • Jaeger UI:     http://localhost:16686");
Console.WriteLine("  • Prometheus:    http://localhost:9090");
Console.WriteLine("  • Grafana:       http://localhost:3000 (admin/admin)");
Console.WriteLine();
Console.WriteLine("🔧 Workers Running:");
Console.WriteLine("  • Continuous worker (500ms delay - ~2 executions/sec)");
Console.WriteLine("  • Periodic worker (every 2s)");
Console.WriteLine("  • Cron worker (every minute)");
Console.WriteLine("  • 🎲 Flaky worker (50/50 success/fail - every 3s)");
Console.WriteLine("  • 💤 Slow worker (random delays 1-3s - every 5s)");
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to stop...");
Console.WriteLine("─────────────────────────────────────────────────────────────────");
Console.WriteLine();

// Register background workers using IHost
host.RunBackgroundWorker(async (IMessageService messageService, ILogger<Program> logger) =>
{
    logger.LogInformation("🔄 Continuous worker executing: {Message}", messageService.GetMessage());
    await Task.Delay(500); // Simulate work (faster for demo)
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

// 🎲 Flaky worker - 50/50 chance of success/failure
host.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(3), async (ILogger<Program> logger) =>
    {
        var random = new Random();
        var willFail = random.Next(2) == 0; // 50% chance

        if (willFail)
        {
            logger.LogWarning("🎲 Flaky worker: Attempting operation that might fail...");
            await Task.Delay(100); // Small delay before failure
            throw new InvalidOperationException("🔥 Flaky worker failed! (Simulated random failure)");
        }
        else
        {
            logger.LogInformation("🎲 Flaky worker: ✅ Success! Operation completed.");
            await Task.Delay(200); // Slightly longer delay for success path
        }
    })
    .WithName("flaky-worker")
    .OnError((Exception ex) =>
    {
        // Handle errors gracefully - log but don't crash
        Console.WriteLine($"⚠️  Error in flaky worker (expected): {ex.Message}");
    });

// 💤 Slow worker - Random delays to show performance variance
host.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(5), async (ILogger<Program> logger) =>
{
    var random = new Random();
    var delayMs = random.Next(1000, 3000); // Random delay between 1-3 seconds

    logger.LogInformation("💤 Slow worker: Starting operation (will take ~{DelayMs}ms)...", delayMs);

    await Task.Delay(delayMs); // Simulate slow operation

    logger.LogInformation("💤 Slow worker: ✅ Completed after {DelayMs}ms", delayMs);
}).WithName("slow-worker");

// Run indefinitely (or until Ctrl+C)
await host.RunAsync();

Console.WriteLine("\nGraceful shutdown completed.");

// Service implementations
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
