# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Build
dotnet build MinimalWorker.sln

# Test all frameworks (8.0, 9.0, 10.0)
dotnet test MinimalWorker.sln

# Test specific framework
dotnet test MinimalWorker.sln -f net10.0

# Run single test
dotnet test MinimalWorker.sln --filter "FullyQualifiedName~PeriodicWorkerTests.PeriodicWorker_Should_Execute_Multiple_Times"

# Run samples
dotnet run --project samples/MinimalWorker.Console.Sample

# Pack NuGet
dotnet pack src/MinimalWorker --configuration Release --output ./nupkgs
```

## Architecture

MinimalWorker is a .NET library for simplified background worker registration on `IHost`. It uses **Roslyn source generators** for AOT compatibility (zero reflection at runtime).

### Core Components

**Extension Methods** (`src/MinimalWorker/BackgroundWorkerExtensions.cs`):
- `RunBackgroundWorker(IHost, Delegate)` - Continuous worker (you control the loop)
- `RunPeriodicBackgroundWorker(IHost, TimeSpan, Delegate)` - Runs after each interval
- `RunCronBackgroundWorker(IHost, string, Delegate)` - Runs on cron schedule (UTC)

All return `IWorkerBuilder` for fluent `.WithName()` and `.WithErrorHandler()` configuration.

**Source Generator** (`src/MinimalWorker.Generators/`):
- `WorkerGenerator.cs` - IIncrementalGenerator that scans invocations
- `WorkerEmitter.cs` - Generates strongly-typed worker code with DI resolution and telemetry
- Outputs `MinimalWorker.Generated.g.cs` with handler methods

### Worker Scoping Behavior

| Type | Scope |
|------|-------|
| Continuous | Single scope for entire lifetime |
| Periodic | New scope per execution |
| Cron | New scope per execution |

### Delegate Constraints

- Return type: `void`, `Task`, or `ValueTask` only
- Maximum ONE `CancellationToken` per delegate (auto-injected)
- All other parameters resolved from DI
- Parameter order does not matter

### Built-in Observability

ActivitySource and Meter both named `"MinimalWorker"`:
- `worker.executions` - Counter
- `worker.errors` - Counter (with `exception.type` tag)
- `worker.duration` - Histogram (ms)
- `worker.active` - Gauge (1=running, 0=stopped)

## Testing Patterns

Tests use `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing` for instant time-based testing:

```csharp
BackgroundWorkerExtensions.ClearRegistrations(); // Required before each test

var timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
services.AddSingleton<TimeProvider>(timeProvider);

// Advance time in steps (not one big jump)
for (int i = 0; i < steps; i++)
{
    timeProvider.Advance(stepSize);
    await Task.Yield();
    await Task.Delay(5);
}
```

## Common Anti-patterns

- **Continuous workers**: Run exactly once. Include your own `while` loop if you need repetition
- **Periodic/Cron workers**: Do NOT add loops - framework handles repetition
- **Testing**: Always call `BackgroundWorkerExtensions.ClearRegistrations()` before each test
- **Time advancement**: Advance in small steps with `Task.Yield()` between, not one large jump
