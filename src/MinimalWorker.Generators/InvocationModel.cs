using System.Collections.Generic;

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
