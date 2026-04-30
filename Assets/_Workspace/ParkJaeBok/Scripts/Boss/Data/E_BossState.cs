/// <summary>
/// 상위 수준의 보스 실행 상태를 정의한다.
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
