/// <summary>
/// EnemyBrain 기본 판단 흐름을 확장 정책이 덮어쓸 때 사용할 결정 타입입니다.
/// </summary>
public enum E_EnemyDecisionType
{
    UseDefault,
    ForceAttack,
    ForceChase,
    ForceRetreat,
    HoldPosition,
}
