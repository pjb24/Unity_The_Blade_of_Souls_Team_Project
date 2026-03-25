/// <summary>
/// EnemyBrain의 이동 모드(지상/부유/전환) 결정을 확장하는 정책 인터페이스입니다.
/// </summary>
public interface IEnemyLocomotionDecisionPolicy
{
    /// <summary>
    /// 현재 이동 컨텍스트를 평가해 로코모션 전환 결과를 반환합니다.
    /// </summary>
    EnemyLocomotionDecisionResult Evaluate(in EnemyLocomotionContext context);
}
