/// <summary>
/// Identifies boss presentation-only cues synchronized from authority to clients and host.
/// </summary>
public enum E_BossPresentationCue
{
    None = 0,
    PatternStarted = 1,
    PatternAttack = 2,
    PatternEnded = 3,
    InvincibleStarted = 4,
    InvincibleEnded = 5,
    WeakPointCreated = 6,
    WeakPointDestroyed = 7,
    GroggyStarted = 8,
    GroggyEnded = 9,
    Dead = 10,
}
