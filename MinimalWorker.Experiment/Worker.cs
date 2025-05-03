using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
//sb.AppendLine("namespace Microsoft.Extensions.Hosting");

namespace MinimalWorker.Experiment
{
[Generator]
    public class DynamicHostWorkerGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1) Find invocations of MapBackgroundWorker
            var specs = context.SyntaxProvider
                .CreateSyntaxProvider(
                    (node, ct) =>
                        node is InvocationExpressionSyntax inv &&
                        inv.Expression is MemberAccessExpressionSyntax member &&
                        member.Name.Identifier.Text == "MapBackgroundWorker",
                    (ctx, ct) =>
                    {
                        var inv = (InvocationExpressionSyntax)ctx.Node;
                        if (inv.ArgumentList.Arguments.Count != 2)
                            return null;

                        // Expect a parenthesized lambda as 2nd arg
                        if (!(inv.ArgumentList.Arguments[1].Expression is ParenthesizedLambdaExpressionSyntax lambda))
                            return null;

                        var parameters = lambda.ParameterList.Parameters;
                        if (parameters.Count < 1)
                            return null;

                        // Last parameter must be CancellationToken
                        var lastParam = parameters[parameters.Count - 1];
                        var lastTypeInfo = ctx.SemanticModel.GetTypeInfo(lastParam.Type);
                        if (lastTypeInfo.Type == null ||
                            lastTypeInfo.Type.ToDisplayString() != "System.Threading.CancellationToken")
                        {
                            return null;
                        }

                        // Collect service parameters (all except last)
                        var services = new List<(string TypeName, string ParamName)>();
                        for (int i = 0; i < parameters.Count - 1; i++)
                        {
                            var p = parameters[i];
                            var typeInfo = ctx.SemanticModel.GetTypeInfo(p.Type);
                            if (typeInfo.Type is INamedTypeSymbol namedType)
                            {
                                var fullName = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                                    .Replace("global::", "");
                                services.Add((fullName, p.Identifier.Text));
                            }
                        }

                        // Create a unique hint
                        var hint = services.Count > 0
                            ? string.Join("_", services.Select(s => s.TypeName.Replace('.', '_')))
                            : "NoServices";

                        return new { Services = services, Hint = hint };
                    })
                .Where(x => x != null)
                .Collect();

            // 2) Generate extensions
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
                    sb.AppendLine("namespace Microsoft.Extensions.Hosting"); // Should we use MinimalWorker as before????
                    sb.AppendLine("{");
                    sb.AppendLine("    public static class HostExtensions");
                    sb.AppendLine("    {");

                    // Method signature
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

                    spc.AddSource($"HostExtensions_{hint}.g.cs",
                                  SourceText.From(sb.ToString(), Encoding.UTF8));
                }
            });
        }
    }
}