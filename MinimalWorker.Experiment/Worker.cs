using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MinimalWorker.Experiment
{
[Generator]
    public class DynamicHostWorkerGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1) Find all invocations named MapBackgroundWorker(...)
            var specs = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, ct) =>
                    {
                        if (node is InvocationExpressionSyntax inv &&
                            inv.Expression is MemberAccessExpressionSyntax member &&
                            member.Name.Identifier.Text == "MapBackgroundWorker")
                        {
                            return true;
                        }
                        return false;
                    },
                    transform: (ctx, ct) =>
                    {
                        var inv = (InvocationExpressionSyntax)ctx.Node;

                        // Expect exactly two args: timespan + lambda
                        if (inv.ArgumentList.Arguments.Count != 2)
                            return null;

                        // Second arg must be a lambda: (A a, B b, ..., CancellationToken ct) => ...
                        if (!(inv.ArgumentList.Arguments[1].Expression is ParenthesizedLambdaExpressionSyntax lambda))
                            return null;

                        var parameters = lambda.ParameterList.Parameters;
                        // Need at least the cancellation token parameter
                        if (parameters.Count < 1)
                            return null;

                        // Last param must be CancellationToken
                        var lastParam = parameters[parameters.Count - 1];
                        if (lastParam.Type == null ||
                            !lastParam.Type.ToString().EndsWith("CancellationToken"))
                        {
                            return null;
                        }

                        // Collect service parameters (all except last)
                        var services = new List<(string TypeName, string ParamName)>();
                        for (int i = 0; i < parameters.Count - 1; i++)
                        {
                            var p = parameters[i];
                            if (p.Type != null)
                                services.Add((p.Type.ToString(), p.Identifier.Text));
                        }

                        // Create a unique hint name
                        var hint = services.Count > 0
                            ? string.Join("_", services.Select(s => s.TypeName.Replace('.', '_')))
                            : "NoServices";

                        return new { Services = services, Hint = hint };
                    })
                .Where(x => x != null)
                .Collect();

            // 2) Generate one extension per unique service-set
            context.RegisterSourceOutput(specs, (spc, allSpecs) =>
            {
                foreach (var grouping in allSpecs.GroupBy(x => x.Hint))
                {
                    var spec = grouping.First();
                    var services = spec.Services;
                    var hint = spec.Hint;

                    var sb = new StringBuilder();
                    sb.AppendLine("using System;");
                    sb.AppendLine("using System.Threading;");
                    sb.AppendLine("using System.Threading.Tasks;");
                    sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
                    sb.AppendLine("using Microsoft.Extensions.Hosting;");
                    sb.AppendLine();
                    //sb.AppendLine("namespace WorkerGenerated");
                    sb.AppendLine("namespace Microsoft.Extensions.Hosting");
                    sb.AppendLine("{");
                    sb.AppendLine("    public static class HostExtensions");
                    sb.AppendLine("    {");

                    // Signature
                    sb.Append("        public static IHost MapBackgroundWorker(");
                    sb.Append("this IHost host, TimeSpan timespan, ");
                    if (services.Count == 0)
                    {
                        sb.Append("Func<CancellationToken, Task> action)");
                    }
                    else
                    {
                        sb.Append("Func<");
                        sb.Append(string.Join(", ", services.Select(s => s.TypeName)));
                        sb.Append(", CancellationToken, Task> action)");
                    }
                    sb.AppendLine();

                    sb.AppendLine("        {");
                    // Resolve services
                    foreach (var svc in services)
                    {
                        sb.AppendLine($"            var {svc.ParamName} = host.Services.GetRequiredService<{svc.TypeName}>();");
                    }
                    sb.AppendLine("            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();");
                    sb.AppendLine("            var ct = lifetime.ApplicationStopping;");
                    sb.AppendLine();
                    sb.AppendLine("            _ = Task.Run(async () =>");
                    sb.AppendLine("            {");
                    sb.AppendLine("                while (!ct.IsCancellationRequested)");
                    sb.AppendLine("                {");
                    sb.Append("                    await action(");
                    if (services.Count == 0)
                    {
                        sb.Append("ct");
                    }
                    else
                    {
                        sb.Append(string.Join(", ", services.Select(s => s.ParamName)) + ", ct");
                    }
                    sb.AppendLine(");");
                    sb.AppendLine("                    await Task.Delay(timespan, ct);");
                    sb.AppendLine("                }");
                    sb.AppendLine("            });");
                    sb.AppendLine("            return host;");
                    sb.AppendLine("        }");
                    sb.AppendLine("    }");
                    sb.AppendLine("}");

                    spc.AddSource($"HostExtensions_{hint}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
                }
            });
        }
    }

}