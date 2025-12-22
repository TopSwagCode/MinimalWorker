using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MinimalWorker.Test.NamespaceA;

/// <summary>
/// Worker defined in NamespaceA that uses IServiceA.
/// This tests that workers with the same signature can exist in different namespaces.
/// </summary>
public class WorkerA
{
    public static void RegisterWorker(IHost host, IServiceA serviceA)
    {
        host.RunBackgroundWorker(async (IServiceA svc, CancellationToken token) =>
        {
            svc.Execute();
            await Task.Delay(40, token);
        }).WithName("workerA-from-namespaceA");
    }
}
