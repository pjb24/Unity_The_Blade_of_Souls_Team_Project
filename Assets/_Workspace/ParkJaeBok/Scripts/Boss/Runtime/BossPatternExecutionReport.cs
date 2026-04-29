/// <summary>
/// Immutable report data sent by a boss pattern when execution finishes, is cancelled, or fails.
/// </summary>
public readonly struct BossPatternExecutionReport
{
    public readonly BossPatternBase Pattern; // Pattern instance that produced this report.
    public readonly E_BossPatternType PatternType; // Pattern type assigned to the reporting pattern.
    public readonly int ExecutionId; // Pattern execution id generated at start.
    public readonly string Reason; // Optional completion, cancellation, or failure reason.

    /// <summary>
    /// Creates a boss pattern execution report.
    /// </summary>
    public BossPatternExecutionReport(BossPatternBase pattern, E_BossPatternType patternType, int executionId, string reason)
    {
        Pattern = pattern;
        PatternType = patternType;
        ExecutionId = executionId;
        Reason = reason;
    }
}
