namespace MinimalWorker;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Internal hosted service that validates worker dependencies before the application starts.
/// This ensures fail-fast behavior when dependencies are missing.
/// </summary>
internal class WorkerDependencyValidationService : IHostedService
{
    private readonly IHost _host;

    public WorkerDependencyValidationService(IHost host)
    {
        _host = host;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Call the generated worker initializer which will validate all dependencies
        // If any dependency is missing, this will throw and prevent the app from starting
        BackgroundWorkerExtensions._generatedWorkerInitializer?.Invoke(_host);
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
