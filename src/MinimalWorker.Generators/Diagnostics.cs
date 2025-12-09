using Microsoft.CodeAnalysis;

namespace MinimalWorker.Generators;

/// <summary>
/// Diagnostic descriptors for the MinimalWorker source generator.
/// </summary>
internal static class Diagnostics
{
    private const string Category = "MinimalWorker";

    public static readonly DiagnosticDescriptor MultipleCancellationTokens = new(
        id: "MW0001",
        title: "Multiple CancellationToken parameters",
        messageFormat: "Background worker delegate cannot have more than one CancellationToken parameter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidReturnType = new(
        id: "MW0002",
        title: "Invalid return type",
        messageFormat: "Background worker delegate must return void, Task, or ValueTask",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnresolvableParameter = new(
        id: "MW0003",
        title: "Unresolvable parameter",
        messageFormat: "Parameter '{0}' cannot be resolved from dependency injection",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
