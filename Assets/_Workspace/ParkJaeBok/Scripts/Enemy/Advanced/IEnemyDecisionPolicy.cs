/// <summary>
/// EnemyBrain의 기본 전투 판단(추적/공격)을 상황별로 확장하는 정책 인터페이스입니다.
/// </summary>
public interface IEnemyDecisionPolicy
{
    /// <summary>
    /// 현재 문맥을 평가해 기본 Brain 흐름을 유지할지 또는 특정 결정을 강제할지 반환합니다.
    /// </summary>
    EnemyDecisionResult Evaluate(in EnemyBrainContext context);
}
