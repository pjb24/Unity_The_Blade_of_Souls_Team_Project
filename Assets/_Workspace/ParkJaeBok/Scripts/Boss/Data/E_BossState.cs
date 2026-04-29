/// <summary>
/// Defines the high level runtime state used by the boss flow.
/// </summary>
public enum E_BossState
{
    None = 0,
    Spawn = 1,
    Idle = 2,
    PatternExecuting = 3,
    HitReaction = 4,
    Groggy = 5,
    PhaseTransition = 6,
    Dead = 7,
}
