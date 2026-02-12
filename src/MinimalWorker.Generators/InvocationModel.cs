using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace MinimalWorker.Generators;

/// <summary>
/// Represents a worker invocation model extracted from syntax analysis.
/// </summary>
internal sealed class WorkerInvocationModel
{
    public string WorkerId { get; set; } = string.Empty;
    public string HandlerMethodName { get; set; } = string.Empty;
    public string InvokerFieldName { get; set; } = string.Empty;
    public List<ParameterModel> Parameters { get; set; } = new();
    public bool IsAsync { get; set; }
    public string ReturnType { get; set; } = "Task";
    public WorkerType Type { get; set; }
    public string? ScheduleArgument { get; set; } // TimeSpan or cron expression

    /// <summary>
    /// Diagnostics collected during analysis. Reported during code generation.
    /// </summary>
    public List<DiagnosticInfo> Diagnostics { get; set; } = new();

    /// <summary>
    /// Indicates whether the model has errors that prevent code generation.
    /// </summary>
    public bool HasErrors { get; set; }
}

/// <summary>
/// Represents a diagnostic to be reported during code generation.
/// </summary>
internal sealed class DiagnosticInfo
{
    public DiagnosticDescriptor Descriptor { get; set; } = null!;
    public Location Location { get; set; } = Location.None;
    public object?[]? MessageArgs { get; set; }
}

internal sealed class ParameterModel
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsCancellationToken { get; set; }
}

internal enum WorkerType
{
    Continuous,
    Periodic,
    Cron
}
