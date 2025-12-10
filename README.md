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

All methods automatically resolve services from the DI container and inject the `CancellationToken` if it's a parameter.

Workers are automatically initialized and started when the application starts - no additional calls needed!

### Error Handling

All worker methods accept an optional `onError` callback for handling exceptions:

```csharp
app.RunBackgroundWorker(
    async (MyService service, CancellationToken token) =>
    {
        await service.DoRiskyWork();
    },
    onError: ex =>
    {
        // Custom error handling - log, alert, etc.
        Console.WriteLine($"Worker error: {ex.Message}");
        // Worker continues running after error
    }
);
```

**Important**: 
- If `onError` is **not provided**, exceptions are **rethrown** and may crash the worker
- If `onError` **is provided**, the exception is passed to your handler and the worker continues
- `OperationCanceledException` is always handled gracefully during shutdown

#### Using Dependency Injection in Error Handlers

The `onError` callback currently does not support dependency injection directly. As a workaround, you can capture services from the service provider:

```csharp
// Capture logger at startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();

app.RunBackgroundWorker(
    async (CancellationToken token) =>
    {
        await DoWork();
    },
    onError: ex =>
    {
        logger.LogError(ex, "Worker failed");
        // Use the captured logger
    }
);
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

## 🚀 AOT Compilation Support

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