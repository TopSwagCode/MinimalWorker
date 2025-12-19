# MinimalWorker

[![Publish NuGet Package](https://github.com/TopSwagCode/MinimalWorker/actions/workflows/publish.yml/badge.svg)](https://github.com/TopSwagCode/MinimalWorker/actions/workflows/publish.yml) 
![NuGet Downloads](https://img.shields.io/nuget/dt/MinimalWorker)
![NuGet Version](https://img.shields.io/nuget/v/MinimalWorker)


![Worker](https://raw.githubusercontent.com/TopSwagCode/MinimalWorker/master/assets/worker.png)


**MinimalWorker** is a lightweight .NET library that simplifies background worker registration in ASP.NET Core and .NET applications using the `IHost` interface. It offers three simple extension methods to map background tasks that run continuously or periodically, with support for dependency injection and cancellation tokens.

---

## ✨ Features

- 🚀 Register background workers with a single method call
- ⏱ Support for periodic background tasks
- 🔄 Built-in support for `CancellationToken`
- 🧪 Works seamlessly with dependency injection (`IServiceProvider`)
- 🧼 Minimal and clean API
- 🏎️ AOT Compilation Support

---

## 📦 Installation

Install from NuGet:

```bash
dotnet add package MinimalWorker
```

Or via the NuGet Package Manager:

```powershell
Install-Package MinimalWorker
```

## 🛠 Usage

### Continuous Background Worker

```csharp
app.RunBackgroundWorker(async (MyService service, CancellationToken token) =>
{
    while (!token.IsCancellationRequested)
    {
        await service.DoWorkAsync();
        await Task.Delay(1000, token);
    }
});
```

### Periodic Background Worker

```csharp
app.RunPeriodicBackgroundWorker(TimeSpan.FromMinutes(5), async (MyService service, CancellationToken token) =>
{
    await service.CleanupAsync();
});
```

### Cron-scheduled Background Worker

```csharp
app.RunCronBackgroundWorker("0 0 * * *", async (CancellationToken ct, MyService service) =>
{
    await service.SendDailyProgressReport();
});
```

### Fluent Configuration with Builder Pattern

All worker methods return an `IWorkerBuilder` for fluent configuration of names and error handlers:

```csharp
// Named continuous worker with error handling
app.RunBackgroundWorker(async (OrderService service, CancellationToken token) =>
{
    await service.ProcessOrders();
})
.WithName("order-processor")
.WithErrorHandler(ex => Console.WriteLine($"Order processing failed: {ex.Message}"));

// Named periodic worker
app.RunPeriodicBackgroundWorker(TimeSpan.FromMinutes(30), async (CacheService cache) =>
{
    await cache.Cleanup();
})
.WithName("cache-cleanup");

// Named cron worker with error handling
app.RunCronBackgroundWorker("0 2 * * *", async (ReportService reports) =>
{
    await reports.GenerateDailyReport();
})
.WithName("nightly-report")
.WithErrorHandler(ex => logger.LogError(ex, "Nightly report failed"));
```

Worker names appear in:
- **Logs**: `Worker 'order-processor' started (Type: continuous, Id: 1)`
- **Metrics**: `worker.name="order-processor"` tag
- **Traces**: `worker.name` attribute on spans

If no name is provided, a default name is generated (e.g., `worker-1`).

All methods automatically resolve services from the DI container and inject the `CancellationToken` if it's a parameter.

Workers are automatically initialized and started when the application starts - no additional calls needed!

### Error Handling

Use the `.WithErrorHandler()` builder method for handling exceptions:

```csharp
app.RunBackgroundWorker(async (MyService service, CancellationToken token) =>
{
    await service.DoRiskyWork();
})
.WithErrorHandler(ex =>
{
    // Custom error handling - log, alert, etc.
    Console.WriteLine($"Worker error: {ex.Message}");
    // Worker continues running after error
});
```

**Important**:
- If `.WithErrorHandler()` is **not provided**, exceptions are **rethrown** and may crash the worker
- If `.WithErrorHandler()` **is provided**, the exception is passed to your handler and the worker continues
- `OperationCanceledException` is always handled gracefully during shutdown

#### Using Dependency Injection in Error Handlers

The `.WithErrorHandler()` callback currently does not support dependency injection directly. As a workaround, you can capture services from the service provider:

```csharp
// Capture logger at startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();

app.RunBackgroundWorker(async (CancellationToken token) =>
{
    await DoWork();
})
.WithErrorHandler(ex =>
{
    logger.LogError(ex, "Worker failed");
    // Use the captured logger
});
```

**Note**: This captures singleton services. For scoped services, this approach has limitations. Native DI support for error handlers is being considered for a future release.

#### Startup Dependency Validation

MinimalWorker validates that all required dependencies for your workers are registered **during application startup**. If any dependencies are missing, the application will fail immediately with a clear error message:

```csharp
builder.Services.AddSingleton<IMyService, MyService>();
// Forgot to register IOtherService!

app.RunBackgroundWorker((IMyService myService, IOtherService otherService) =>
{
    // This worker will never run
});

await app.RunAsync(); 
// Application terminates immediately:
// FATAL: Worker dependency validation failed: 
// No service for type 'IOtherService' has been registered.
```

**Behavior**:
- ✅ **Fail-fast** - Application exits immediately during startup (not on first execution)
- ✅ **Clear error messages** - Shows exactly which dependency is missing
- ✅ **Exit code 1** - Proper error code for container orchestrators and CI/CD
- ✅ **Production-safe** - Prevents workers from running with missing dependencies

This ensures you catch configuration errors early, before deploying to production. The validation happens after all services are registered but before workers start executing, using the same dependency resolution mechanism as the workers themselves.

## 🔧 How It Works

- `RunBackgroundWorker` runs a background task once the application starts, and continues until shutdown.
- `RunPeriodicBackgroundWorker` runs your task repeatedly at a fixed interval using PeriodicTimer.
- `RunCronBackgroundWorker` runs your task repeatedly based on a CRON expression (UTC time), using NCrontab for timing.
- Workers are initialized using **source generators** for AOT compatibility - no reflection at runtime!
- Workers automatically start when the application starts via `lifetime.ApplicationStarted.Register()`
- Services and parameters are resolved per execution using `CreateScope()` to support scoped dependencies.

## 📡 Observability & OpenTelemetry

MinimalWorker provides **production-grade observability** out of the box with **zero configuration required**. All workers automatically emit metrics and distributed traces using native .NET APIs (`System.Diagnostics.Activity` and `System.Diagnostics.Metrics`).

### 🎯 What's Automatically Instrumented

**Every worker execution** is automatically instrumented with:

✅ **Distributed Tracing** - Activity spans for each execution  
✅ **Metrics** - Execution count, error count, and duration histograms  
✅ **Tags/Dimensions** - Worker ID, type, iteration count, cron expression  
✅ **Exception Recording** - Full exception details in traces  
✅ **Zero Breaking Changes** - Works with or without OpenTelemetry configured  

### 📈 Available Metrics

| Metric Name | Type | Description | Dimensions |
|-------------|------|-------------|------------|
| `worker.executions` | Counter | Total worker executions | `worker.id`, `worker.name`, `worker.type` |
| `worker.errors` | Counter | Total worker errors | `worker.id`, `worker.name`, `worker.type`, `exception.type` |
| `worker.duration` | Histogram | Execution duration (ms) | `worker.id`, `worker.name`, `worker.type` |
| `worker.active` | Gauge | Active workers (1=running, 0=stopped) | `worker.id`, `worker.name`, `worker.type` |
| `worker.last_success_time` | Gauge | Unix timestamp of last successful execution | `worker.id`, `worker.name`, `worker.type` |
| `worker.consecutive_failures` | Gauge | Number of consecutive failures | `worker.id`, `worker.name`, `worker.type` |

**Worker Types**: `continuous`, `periodic`, `cron`

📊 **For detailed metrics documentation, usage examples, and PromQL queries, see [METRICS.md](docs/METRICS.md)**

### 🔍 Distributed Tracing Tags

Each worker execution creates an Activity span with the following tags:

| Tag | Description | Example |
|-----|-------------|---------|
| `worker.id` | Worker identifier | `"1"`, `"2"`, `"3"` |
| `worker.name` | Worker name (user-provided or generated) | `"order-processor"`, `"worker-1"` |
| `worker.type` | Type of worker | `"continuous"`, `"periodic"`, `"cron"` |
| `worker.iteration` | Execution count (continuous only) | `"1"`, `"2"`, `"3"` |
| `worker.schedule` | Schedule interval (periodic only) | `"00:00:03"` |
| `cron.expression` | Cron expression (cron only) | `"*/5 * * * * *"` |
| `cron.next_run` | Next execution time (cron only) | `"2025-01-15T10:30:00Z"` |
| `exception.type` | Exception type (on error) | `"System.InvalidOperationException"` |
| `exception.message` | Exception message (on error) | `"Operation failed"` |
| `exception.stacktrace` | Full exception details (on error) | Stack trace string |

### 🚀 Quick Start with OpenTelemetry

**1. Install OpenTelemetry packages:**

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.Console
```

**2. Configure OpenTelemetry to subscribe to MinimalWorker:**

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("MinimalWorker") // ✨ Subscribe to MinimalWorker traces
            .AddConsoleExporter();
    })
    .WithMetrics(meterProviderBuilder =>
    {
        meterProviderBuilder
            .AddMeter("MinimalWorker") // ✨ Subscribe to MinimalWorker metrics
            .AddConsoleExporter();
    });

var host = builder.Build();

// Register workers - they're automatically instrumented!
host.RunBackgroundWorker(async (MyService service) =>
{
    await service.DoWork();
});

await host.RunAsync();
```

**That's it!** 🎉 Your workers now export traces and metrics.

### 📤 Export to Popular Backends

#### Prometheus + Grafana

```bash
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
```

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(meterProviderBuilder =>
    {
        meterProviderBuilder
            .AddMeter("MinimalWorker")
            .AddPrometheusExporter(); // Expose /metrics endpoint
    });

// Metrics available at: http://localhost:9090/metrics
```

#### Azure Application Insights

```bash
dotnet add package Azure.Monitor.OpenTelemetry.Exporter
```

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("MinimalWorker")
            .AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = "InstrumentationKey=...";
            });
    })
    .WithMetrics(meterProviderBuilder =>
    {
        meterProviderBuilder
            .AddMeter("MinimalWorker")
            .AddAzureMonitorMetricExporter(options =>
            {
                options.ConnectionString = "InstrumentationKey=...";
            });
    });
```

#### OTLP (OpenTelemetry Protocol)

Compatible with Jaeger, Zipkin, Grafana Tempo, and more:

```bash
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("MinimalWorker")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            });
    });
```

### 🔬 Example: Monitoring Worker Performance

```csharp
// Register a named worker for easy identification
host.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(5), async (MyService service) =>
{
    await service.ProcessData(); // Automatically traced & metered!
}).WithName("data-processor");
```

### 🎓 Best Practices

1. **Name your workers** - Use descriptive names for easier identification in logs and metrics
2. **Always configure OpenTelemetry** in production environments
3. **Use custom error handlers** for non-fatal errors (they're automatically recorded in traces)
4. **Monitor error rate metrics** to detect worker failures
5. **Set up alerts** on `worker_errors_total` or `worker_consecutive_failures > 0`
6. **Monitor `worker_active`** to detect stopped workers
7. **Use distributed tracing** to debug worker execution failures
8. **Check duration histograms** to identify performance bottlenecks

### 📚 Learn More

- See [MinimalWorker.OpenTelemetry.Sample](samples/MinimalWorker.OpenTelemetry.Sample) for a complete example
- Read the [OpenTelemetry .NET documentation](https://opentelemetry.io/docs/languages/net/)
- Explore [Activity API docs](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity)
- Explore [Metrics API docs](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics)

---

## �🚀 AOT Compilation Support

MinimalWorker is fully compatible with .NET Native AOT compilation! The library uses source generators instead of reflection, making it perfect for AOT scenarios.

### Publishing as AOT

To publish your application as a native AOT binary:

```bash
dotnet publish -c Release
```

Make sure your project file includes:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

This will produce a self-contained native executable with:
- **No .NET runtime dependency** - runs on machines without .NET installed
- **Fast startup** - native code execution from the start
- **Small binary size** - approximately 4-5MB for a minimal application
- **AOT-safe** - all worker registration happens via source generators, no reflection

See the [MinimalWorker.Aot.Sample](samples/MinimalWorker.Aot.Sample) project for a complete example.

## 👋

Thank you for reading this far :) Hope you find it usefull. Feel free to open issues, give feedback or just say hi :D