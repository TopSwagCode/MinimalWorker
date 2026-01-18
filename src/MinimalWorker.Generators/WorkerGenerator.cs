using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MinimalWorker.Generators;

/// <summary>
/// Incremental source generator for MinimalWorker background workers.
/// Analyzes RunBackgroundWorker calls and generates strongly-typed invokers.
/// </summary>
[Generator]
public class WorkerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all invocations of RunBackgroundWorker, RunPeriodicBackgroundWorker, and RunCronBackgroundWorker
        var workerInvocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsWorkerInvocation(node),
                transform: static (ctx, _) => GetWorkerInvocation(ctx))
            .Where(static m => m is not null);

        // Combine and generate
        context.RegisterSourceOutput(workerInvocations.Collect(), 
            static (spc, workers) => Execute(spc, workers!));
    }

    private static bool IsWorkerInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        // Check both direct invocation and member access (extension method)
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };

        return methodName is "RunBackgroundWorker" or "RunPeriodicBackgroundWorker" or "RunCronBackgroundWorker";
    }

    private static WorkerInvocationModel? GetWorkerInvocation(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        
        // Extract method name from invocation (handles both member access and identifier)
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };

        if (methodName is not ("RunBackgroundWorker" or "RunPeriodicBackgroundWorker" or "RunCronBackgroundWorker"))
            return null;

        // Try to get method symbol, but don't fail if we can't
        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        
        // Verify it's our extension method if we can resolve the symbol
        if (methodSymbol != null &&methodSymbol.ContainingType?.Name != "BackgroundWorkerExtensions")
            return null;

        // Note: WorkerId will be assigned later in Execute() after all workers are collected
        // This ensures deterministic IDs regardless of file processing order
        var model = new WorkerInvocationModel
        {
            Type = methodName switch
            {
                "RunPeriodicBackgroundWorker" => WorkerType.Periodic,
                "RunCronBackgroundWorker" => WorkerType.Cron,
                _ => WorkerType.Continuous
            }
        };

        // Extract delegate parameter
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0)
            return model; // Return even with no args to see if we get here

        // Find the delegate argument - it's the argument for the "action" parameter
        // or the second-to-last if onError is provided, otherwise the last argument
        ArgumentSyntax? delegateArg = null;
        
        // Try to find by argument name first
        foreach (var arg in args)
        {
            if (arg.NameColon?.Name.Identifier.ValueText == "action")
            {
                delegateArg = arg;
                break;
            }
        }
        
        // If not found by name, use position-based logic
        if (delegateArg == null)
        {
            // For Continuous: delegate is at index 0
            // For Periodic/Cron: delegate is at index 1
            var delegateIndex = (model.Type == WorkerType.Continuous) ? 0 : 1;
            if (args.Count > delegateIndex)
            {
                delegateArg = args[delegateIndex];
            }
        }
        
        if (delegateArg == null)
            return model; // Couldn't find delegate argument
        
        // For Periodic and Cron workers, extract schedule argument
        if (model.Type == WorkerType.Periodic && args.Count > 0)
        {
            model.ScheduleArgument = args[0].Expression.ToString();
        }
        else if (model.Type == WorkerType.Cron && args.Count > 0)
        {
            model.ScheduleArgument = args[0].Expression.ToString();
        }

        // Try to analyze the delegate
        if (!AnalyzeDelegate(context, delegateArg.Expression, model))
        {
            // If we can't analyze the delegate, still return the model
            // but it won't have parameter information
            // This allows the generator to continue but the code won't be correct
            return model;
        }

        return model;
    }

    private static bool AnalyzeDelegate(GeneratorSyntaxContext context, ExpressionSyntax delegateExpression, WorkerInvocationModel model)
    {
        var typeInfo = context.SemanticModel.GetTypeInfo(delegateExpression);
        
        // Try to get the actual delegate type, not just the converted type
        INamedTypeSymbol? delegateType = null;
        
        if (typeInfo.Type is INamedTypeSymbol namedType && namedType.DelegateInvokeMethod != null)
        {
            delegateType = namedType;
        }
        else if (typeInfo.ConvertedType is INamedTypeSymbol convertedNamedType && convertedNamedType.DelegateInvokeMethod != null)
        {
            delegateType = convertedNamedType;
        }
        
        if (delegateType == null)
            return false;

        var invokeMethod = delegateType.DelegateInvokeMethod;
        if (invokeMethod == null)
            return false;

        // Extract parameters
        var cancellationTokenCount = 0;
        foreach (var param in invokeMethod.Parameters)
        {
            var paramModel = new ParameterModel
            {
                Name = param.Name,
                Type = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                IsCancellationToken = param.Type.Name == "CancellationToken" && 
                                     param.Type.ContainingNamespace?.ToDisplayString() == "System.Threading"
            };

            if (paramModel.IsCancellationToken)
                cancellationTokenCount++;

            model.Parameters.Add(paramModel);
        }

        // Validate only one CancellationToken
        if (cancellationTokenCount > 1)
            return false;

        // Determine return type
        var returnType = invokeMethod.ReturnType;
        model.IsAsync = returnType.Name == "Task" || returnType.Name == "ValueTask";
        model.ReturnType = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Ensure Task or void for now (simplification)
        if (model.ReturnType != "void" &&
            !model.ReturnType.Contains("Task") &&
            !model.ReturnType.Contains("ValueTask"))
        {
            return false;
        }

        // For async delegates, keep the Task return type
        // For sync delegates, keep void (will be handled as Action in emitter)
        if (model.IsAsync && model.ReturnType == "void")
        {
            // This shouldn't happen - async methods should return Task
            model.ReturnType = "System.Threading.Tasks.Task";
        }

        return true;
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<WorkerInvocationModel> workers)
    {
        // Always generate a marker file to confirm generator is running
        var markerSource = $@"// <auto-generated/>
// MinimalWorker Source Generator
// Generated {workers.Length} background workers
namespace MinimalWorker.Generated
{{
    internal static class GeneratorMarker
    {{
        public const int WorkerCount = {workers.Length};
    }}
}}";
        context.AddSource("MinimalWorker.GeneratorMarker.g.cs", SourceText.From(markerSource, Encoding.UTF8));

        if (workers.IsEmpty)
        {
            // No workers found - generate a diagnostic comment
            var noWorkersSource = @"// <auto-generated/>
// No background workers detected in this assembly
// Make sure you're calling RunBackgroundWorker, RunPeriodicBackgroundWorker, or RunCronBackgroundWorker
";
            context.AddSource("MinimalWorker.NoWorkers.g.cs", SourceText.From(noWorkersSource, Encoding.UTF8));
            return;
        }

        var validWorkers = workers.Where(w => w != null).ToList();
        if (validWorkers.Count == 0)
            return;

        // Assign deterministic WorkerIds after all workers are collected
        // This ensures consistent IDs regardless of file processing order
        for (int i = 0; i < validWorkers.Count; i++)
        {
            var workerId = i + 1;
            validWorkers[i].WorkerId = $"{workerId:D3}";
            validWorkers[i].HandlerMethodName = $"Handler_{workerId:D3}";
            validWorkers[i].InvokerFieldName = $"__Invoker_{workerId:D3}";
        }

        // Generate the worker code
        var source = WorkerEmitter.EmitSource(validWorkers);
        context.AddSource("MinimalWorker.Generated.g.cs", SourceText.From(source, Encoding.UTF8));
    }
}
