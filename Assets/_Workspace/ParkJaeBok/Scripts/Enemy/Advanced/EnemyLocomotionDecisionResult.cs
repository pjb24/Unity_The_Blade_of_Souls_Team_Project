using UnityEngine;

/// <summary>
/// 이동 정책이 EnemyBrain에 반환하는 로코모션 결정 결과 데이터입니다.
/// </summary>
public readonly struct EnemyLocomotionDecisionResult
{
    public readonly E_EnemyLocomotionDecisionType DecisionType; // 적용할 로코모션 전환 결정 타입입니다.
    public readonly float TargetAltitude; // 부유 전환 시 사용할 목표 고도 좌표입니다.
    public readonly Vector2 LandingPoint; // 착지 전환 시 사용할 목표 착지 지점 좌표입니다.
    public readonly string Reason; // 디버그 표시에 사용할 전환 사유 문자열입니다.

    /// <summary>
    /// 지정 결정 타입과 보조 파라미터로 결과 구조체를 생성합니다.
    /// </summary>
    public EnemyLocomotionDecisionResult(E_EnemyLocomotionDecisionType decisionType, float targetAltitude, Vector2 landingPoint, string reason)
    {
        DecisionType = decisionType;
        TargetAltitude = targetAltitude;
        LandingPoint = landingPoint;
        Reason = reason;
    }

    /// <summary>
    /// 현재 이동 상태를 유지하는 결과를 생성합니다.
    /// </summary>
    public static EnemyLocomotionDecisionResult KeepCurrent(string reason = "KeepCurrent")
    {
        return new EnemyLocomotionDecisionResult(E_EnemyLocomotionDecisionType.KeepCurrent, 0f, Vector2.zero, reason);
    }
}
