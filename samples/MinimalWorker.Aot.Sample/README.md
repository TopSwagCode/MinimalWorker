# MinimalWorker AOT Sample

This sample demonstrates **Native AOT (Ahead-of-Time) compilation** with MinimalWorker using .NET 9's source generators.

## 🚀 What is Native AOT?

Native AOT compiles your .NET application directly to native machine code, creating a self-contained executable with:
- ⚡ **Fast startup** - No JIT compilation needed
- 📦 **Small footprint** - Single executable (~5MB) with no runtime dependencies
- 🎯 **Zero reflection** - All code is statically analyzed and compiled
- 🔒 **Trimmed** - Only the code you use is included

## ✨ How MinimalWorker Supports AOT

MinimalWorker achieves AOT compatibility through **Roslyn Source Generators**:

1. **Compile-time detection** - The generator analyzes your `MapBackgroundWorker()` calls during compilation
2. **Strongly-typed code generation** - Creates specialized initialization code for each worker signature
3. **No reflection** - All dependency injection and worker invocation uses direct method calls
4. **No dynamic dispatch** - Workers are matched by signature at compile-time, not runtime

## 🏗️ Building and Running

### Build for AOT
```bash
dotnet publish -c Release
```

### Run the native executable
```bash
# macOS/Linux
./bin/Release/net9.0/osx-arm64/publish/MinimalWorker.Aot.Sample

# Windows
.\bin\Release\net9.0\win-x64\publish\MinimalWorker.Aot.Sample.exe
```

## 📝 Key Project Settings

```xml
<PropertyGroup>
  <!-- Enable Native AOT compilation -->
  <PublishAot>true</PublishAot>
  
  <!-- Required for AOT - uses invariant culture -->
  <InvariantGlobalization>true</InvariantGlobalization>
  
  <!-- Optimize for speed -->
  <IlcOptimizationPreference>Speed</IlcOptimizationPreference>
</PropertyGroup>
```

## 🎯 Workers in This Sample

### Continuous Worker
Runs continuously with a 1-second delay between iterations:
```csharp
app.MapBackgroundWorker(async (CancellationToken ct) =>
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Continuous worker executing...");
    await Task.Delay(1000, ct);
});
```

### Periodic Worker
Executes every 2 seconds using `PeriodicTimer`:
```csharp
app.MapPeriodicBackgroundWorker(TimeSpan.FromSeconds(2), (CancellationToken ct) =>
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Periodic worker executing (every 2 seconds)");
    return Task.CompletedTask;
});
```

## 📊 Benefits Over Reflection-Based Approach

| Feature | Reflection | Source Generators (AOT) |
|---------|-----------|-------------------------|
| Startup time | Slower (JIT) | **Instant** |
| Binary size | Larger | **Smaller** |
| Deployment | Runtime required | **Self-contained** |
| Trimming | Limited | **Full** |
| Performance | Good | **Excellent** |
| IL2CPP/Unity | ❌ Not compatible | ✅ Compatible |

## 🔍 Verifying AOT Compatibility

You can verify the published binary has no .NET runtime dependencies:
```bash
# macOS
otool -L bin/Release/net9.0/osx-arm64/publish/MinimalWorker.Aot.Sample

# Linux
ldd bin/Release/net9.0/linux-x64/publish/MinimalWorker.Aot.Sample

# Windows - check file size, should be ~5MB
```

## 🎓 Learn More

- [.NET Native AOT Documentation](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
- [Source Generators](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [MinimalWorker Documentation](../../README.md)
