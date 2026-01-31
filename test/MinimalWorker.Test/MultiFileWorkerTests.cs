using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalWorker.Test.Fakes;
using MinimalWorker.Test.Helpers;
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

        // Assert - Both workers should have executed exactly once each
        Assert.Equal(2, serviceA.ExecuteCount);
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

        // Assert - Continuous worker runs exactly once
        Assert.Equal(1, serviceA.ExecuteCount);
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
            await Task.CompletedTask;
        }).WithName("inline-worker-A");

        host.RunBackgroundWorker(async (IServiceB svc, CancellationToken token) =>
        {
            svc.Execute();
            await Task.CompletedTask;
        }).WithName("inline-worker-B");

        // Act
        await host.StartAsync();
        await Task.Delay(200);
        await host.StopAsync();

        // Assert - Each continuous worker runs exactly once
        Assert.Equal(1, serviceA.ExecuteCount);
        Assert.Equal(1, serviceB.ExecuteCount);
    }
}
