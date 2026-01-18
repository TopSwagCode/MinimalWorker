namespace MinimalWorker.Test.Helpers;

/// <summary>
/// Test constants to avoid magic numbers and improve test readability.
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Minimum expected executions for continuous workers in a 100ms window.
    /// Based on 10ms delay between executions.
    /// </summary>
    public const int MinContinuousExecutions = 3;

    /// <summary>
    /// Maximum expected executions for continuous workers in a 100ms window.
    /// Used to detect runaway workers or timing issues.
    /// </summary>
    public const int MaxContinuousExecutions = 20;

    /// <summary>
    /// Standard short delay for continuous workers (in milliseconds).
    /// </summary>
    public const int StandardWorkerDelayMs = 10;

    /// <summary>
    /// Standard test window duration (in milliseconds).
    /// </summary>
    public const int StandardTestWindowMs = 100;

    /// <summary>
    /// Default timeout for async signal completion.
    /// </summary>
    public static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);
}
