using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MinimalWorker.Test.NamespaceB;

/// <summary>
/// Worker defined in NamespaceB that uses IServiceA (same signature as WorkerA).
/// This tests that workers with the same signature can exist in different namespaces.
/// </summary>
public class WorkerB
{
    public static void RegisterWorker(IHost host, IServiceA serviceA)
    {
        host.RunBackgroundWorker(async (IServiceA svc, CancellationToken token) =>
        {
            svc.Execute();
            await Task.Delay(95, token);
        }).WithName("workerB-from-namespaceB");
    }
}
