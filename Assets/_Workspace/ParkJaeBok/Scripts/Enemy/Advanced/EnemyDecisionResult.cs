using UnityEngine;

/// <summary>
/// 확장 정책이 EnemyBrain에 전달하는 행동 오버라이드 결과 데이터입니다.
/// </summary>
public readonly struct EnemyDecisionResult
{
    public readonly E_EnemyDecisionType DecisionType; // 기본 흐름 대신 적용할 오버라이드 결정 타입입니다.
    public readonly Vector2 RetreatTargetPosition; // 후퇴 결정 시 이동할 목적지 좌표입니다.

    /// <summary>
    /// 지정 결정 타입과 후퇴 좌표로 결과 구조체를 생성합니다.
    /// </summary>
    public EnemyDecisionResult(E_EnemyDecisionType decisionType, Vector2 retreatTargetPosition)
    {
        DecisionType = decisionType;
        RetreatTargetPosition = retreatTargetPosition;
    }

    /// <summary>
    /// 기본 Brain 흐름을 유지하는 결과를 생성합니다.
    /// </summary>
    public static EnemyDecisionResult UseDefault()
    {
        return new EnemyDecisionResult(E_EnemyDecisionType.UseDefault, Vector2.zero);
    }

    /// <summary>
    /// 지정 좌표로 후퇴하는 오버라이드 결과를 생성합니다.
    /// </summary>
    public static EnemyDecisionResult ForceRetreat(Vector2 retreatTargetPosition)
    {
        return new EnemyDecisionResult(E_EnemyDecisionType.ForceRetreat, retreatTargetPosition);
    }
}
