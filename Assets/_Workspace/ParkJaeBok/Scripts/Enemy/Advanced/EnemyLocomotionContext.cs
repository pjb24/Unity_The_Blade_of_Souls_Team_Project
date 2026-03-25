using UnityEngine;

/// <summary>
/// 로코모션 정책 평가에 전달하는 입력 컨텍스트 스냅샷입니다.
/// </summary>
public readonly struct EnemyLocomotionContext
{
    public readonly Transform Self; // 현재 Enemy 자신의 트랜스폼 참조입니다.
    public readonly Transform Target; // 현재 추적 대상으로 해석된 타겟 트랜스폼 참조입니다.
    public readonly float TargetDistance; // 타겟까지의 현재 거리 값입니다.
    public readonly float TargetYDelta; // 타겟과 자신의 Y축 고도 차이 값입니다.
    public readonly bool BlockedAhead; // 전방 장애물 감지 여부입니다.
    public readonly bool IsGrounded; // 현재 접지 상태 여부입니다.
    public readonly float LastSwitchTime; // 최근 이동 모드 전환이 발생한 시각입니다.
    public readonly float CurrentTime; // 현재 프레임의 시간 값입니다.
    public readonly bool CanSwitchLocomotion; // 전투 우선순위 기준 이동 모드 전환 허용 여부입니다.
    public readonly float SwitchCooldown; // 이동 모드 전환 최소 쿨다운 시간입니다.
    public readonly bool IsFloating; // 현재 이동 모드가 부유 계열인지 여부입니다.
    public readonly float TimeInCurrentMode; // 현재 이동 모드 진입 후 경과 시간입니다.
    public readonly float MinAirTime; // 공중 상태 최소 체류 시간입니다.
    public readonly float MaxAirTime; // 공중 상태 최대 체류 시간입니다.
    public readonly bool HasValidGroundCandidate; // 현재 착지 가능한 지면 후보 존재 여부입니다.
    public readonly Vector2 GroundCandidatePoint; // 착지 전환 시 사용할 지면 후보 좌표입니다.
    public readonly bool CanReturnToGroundByCombat; // 현재 전투 상태 기준 지상 복귀 허용 여부입니다.

    /// <summary>
    /// 전달받은 입력값으로 로코모션 컨텍스트를 생성합니다.
    /// </summary>
    public EnemyLocomotionContext(
        Transform self,
        Transform target,
        float targetDistance,
        float targetYDelta,
        bool blockedAhead,
        bool isGrounded,
        float lastSwitchTime,
        float currentTime,
        bool canSwitchLocomotion,
        float switchCooldown,
        bool isFloating,
        float timeInCurrentMode,
        float minAirTime,
        float maxAirTime,
        bool hasValidGroundCandidate,
        Vector2 groundCandidatePoint,
        bool canReturnToGroundByCombat)
    {
        Self = self;
        Target = target;
        TargetDistance = targetDistance;
        TargetYDelta = targetYDelta;
        BlockedAhead = blockedAhead;
        IsGrounded = isGrounded;
        LastSwitchTime = lastSwitchTime;
        CurrentTime = currentTime;
        CanSwitchLocomotion = canSwitchLocomotion;
        SwitchCooldown = switchCooldown;
        IsFloating = isFloating;
        TimeInCurrentMode = timeInCurrentMode;
        MinAirTime = minAirTime;
        MaxAirTime = maxAirTime;
        HasValidGroundCandidate = hasValidGroundCandidate;
        GroundCandidatePoint = groundCandidatePoint;
        CanReturnToGroundByCombat = canReturnToGroundByCombat;
    }
}
