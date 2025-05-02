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
app.MapBackgroundWorker(async (MyService service, CancellationToken token) =>
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
app.MapPeriodicBackgroundWorker(TimeSpan.FromMinutes(5), async (MyService service, CancellationToken token) =>
{
    await service.CleanupAsync();
});
```

### Command run on notice (Cron) Background Worker

```csharp
app.MapCronBackgroundWorker("0 0 * * *", async (CancellationToken ct, MyService service) =>
{
    await service.SendDailyProgressReport();
});
```

All methods automatically resolve services from the DI container and inject the `CancellationToken` if it's a parameter.

## 🔧 How It Works

- `MapBackgroundWorker` runs a background task once the application starts, and continues until shutdown.
- `MapPeriodicBackgroundWorker` runs your task repeatedly at a fixed interval using PeriodicTimer.
- `MapCronBackgroundWorker` runs your task repeatedly based on a CRON expression (UTC time), using NCrontab for timing.
- Services and parameters are resolved per execution using `CreateScope()` to support scoped dependencies.