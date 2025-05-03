using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MinimalWorker.Experiment
{
    [Generator]
public class HostPeriodicWorkerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1) Look for invocation expressions whose member name is "MapPeriodicBackgroundWorker"
        var calls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, ct) =>
                    node is InvocationExpressionSyntax inv &&
                    inv.Expression is MemberAccessExpressionSyntax member &&
                    member.Name.Identifier.Text == "MapPeriodicBackgroundWorker",
                transform: (ctx, ct) => (InvocationExpressionSyntax)ctx.Node
            )
            .Collect();

        // 2) Once we see at least one such call, emit our extension
        context.RegisterSourceOutput(calls, (spc, invocations) =>
        {
            if (invocations.Length == 0)
                return;

            var sb = new StringBuilder(@"
using System;
using Microsoft.Extensions.Hosting;

namespace PeriodicWorkerGenerated
{
    public static class HostExtensions
    {
        /// <summary>
        /// Registers a periodic background action on the host.
        /// </summary>
        public static IHost MapPeriodicBackgroundWorker(
            this IHost host,
            TimeSpan timespan,
            Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> action)
        {
            // you could wire this up to an IHostedService, timers, etc.
            // here’s a dummy implementation:
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                var token = default(System.Threading.CancellationToken);
                while (!token.IsCancellationRequested)
                {
                    await action(token);
                    await System.Threading.Tasks.Task.Delay(timespan, token);
                }
            });
            return host;
        }
    }
}");
            spc.AddSource(
                hintName: "HostExtensions.g.cs",
                sourceText: SourceText.From(sb.ToString(), Encoding.UTF8));
        });
    }
}
}