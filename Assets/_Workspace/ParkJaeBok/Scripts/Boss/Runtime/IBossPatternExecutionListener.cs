/// <summary>
/// Receives boss pattern lifecycle reports through explicit listener registration.
/// </summary>
public interface IBossPatternExecutionListener
{
    /// <summary>
    /// Called when a boss pattern reports normal completion.
    /// </summary>
    void OnBossPatternCompleted(BossPatternExecutionReport report);

    /// <summary>
    /// Called when a boss pattern reports cancellation.
    /// </summary>
    void OnBossPatternCancelled(BossPatternExecutionReport report);

    /// <summary>
    /// Called when a boss pattern reports failure.
    /// </summary>
    void OnBossPatternFailed(BossPatternExecutionReport report);
}
