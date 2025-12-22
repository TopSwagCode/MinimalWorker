using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalWorker.Test.NamespaceA;
using MinimalWorker.Test.NamespaceB;

namespace MinimalWorker.Test;

/// <summary>
/// Tests that verify workers can be defined in multiple files/namespaces.
/// This tests the source generator's ability to handle workers with the same
/// signature discovered across different source files and namespaces.
/// </summary>
public class MultiFileWorkerTests
{
    [Fact]
    public async Task Workers_With_Same_Signature_In_Different_Namespaces_Should_Both_Execute()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var serviceA = new ServiceA();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IServiceA>(serviceA);
            })
            .Build();

        // Register workers from two different namespaces - both use IServiceA with same signature
        WorkerA.RegisterWorker(host, serviceA);
        WorkerB.RegisterWorker(host, serviceA);

        // Act
        await host.StartAsync();
        await Task.Delay(200);
        await host.StopAsync();

        // Assert - Both workers should have executed (each calling Execute on the same service)
        // With 2 workers each running every ~40ms for 200ms, we expect at least 6 total executions
        Assert.True(serviceA.ExecuteCount >= 6,
            $"Expected at least 6 executions from 2 workers, got {serviceA.ExecuteCount}");
    }

    [Fact]
    public async Task Worker_Defined_In_Separate_Namespace_Should_Execute()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var serviceA = new ServiceA();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IServiceA>(serviceA);
            })
            .Build();

        // Register worker from NamespaceA only
        WorkerA.RegisterWorker(host, serviceA);

        // Act
        await host.StartAsync();
        await Task.Delay(150);
        await host.StopAsync();

        // Assert
        Assert.True(serviceA.ExecuteCount >= 3,
            $"Expected at least 3 executions, got {serviceA.ExecuteCount}");
    }

    [Fact]
    public async Task Workers_In_Different_Namespaces_With_Inline_Registration_Should_Both_Execute()
    {
        // Arrange
        BackgroundWorkerExtensions.ClearRegistrations();
        var serviceA = new ServiceA();
        var serviceB = new ServiceB();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IServiceA>(serviceA);
                services.AddSingleton<IServiceB>(serviceB);
            })
            .Build();

        // These inline registrations have different signatures, so they should work
        host.RunBackgroundWorker(async (IServiceA svc, CancellationToken token) =>
        {
            svc.Execute();
            await Task.Delay(40, token);
        }).WithName("inline-worker-A");

        host.RunBackgroundWorker(async (IServiceB svc, CancellationToken token) =>
        {
            svc.Execute();
            await Task.Delay(40, token);
        }).WithName("inline-worker-B");

        // Act
        await host.StartAsync();
        await Task.Delay(200);
        await host.StopAsync();

        // Assert - Both workers should have executed
        Assert.True(serviceA.ExecuteCount >= 3,
            $"ServiceA expected at least 3 executions, got {serviceA.ExecuteCount}");
        Assert.True(serviceB.ExecuteCount >= 3,
            $"ServiceB expected at least 3 executions, got {serviceB.ExecuteCount}");
    }
}
