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