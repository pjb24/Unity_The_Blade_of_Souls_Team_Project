using UnityEngine;

/// <summary>
/// 전방 장애물/고도 차/쿨다운/착지 유효성 기반으로 이동 모드 전환을 결정하는 기본 로코모션 정책입니다.
/// </summary>
public class LocomotionDecisionPolicy : MonoBehaviour, IEnemyLocomotionDecisionPolicy
{
    [Header("Grounded -> Floating")]
    [Tooltip("목표와의 고도 차이가 이 값 이상이면 부유 전환 후보로 간주합니다.")]
    [SerializeField] private float _targetYDeltaThreshold = 1.5f; // 목표와의 고도 차이가 이 값 이상이면 부유 전환 후보로 간주합니다.
    [Tooltip("부유 전환 시 타겟 고도에 더할 오프셋 값입니다.")]
    [SerializeField] private float _floatingTargetOffsetY = 0.5f; // 부유 전환 시 타겟 고도에 더할 오프셋 값입니다.

    [Header("Runtime Archetype Binding")]
    [Tooltip("아키타입 적용 후 사용할 전환 쿨다운 값입니다.")]
    [SerializeField] private float _switchCooldown = 0.8f; // 아키타입 적용 후 사용할 전환 쿨다운 값입니다.
    [Tooltip("아키타입 적용 후 사용할 최소 공중 체류시간 값입니다.")]
    [SerializeField] private float _minimumAirTime = 0.6f; // 아키타입 적용 후 사용할 최소 공중 체류시간 값입니다.
    [Tooltip("아키타입 적용 후 사용할 최대 공중 체류시간 값입니다.")]
    [SerializeField] private float _maximumAirTime = 4f; // 아키타입 적용 후 사용할 최대 공중 체류시간 값입니다.

    /// <summary>
    /// 입력 컨텍스트를 평가해 이동 모드 유지/전환 결정을 반환합니다.
    /// </summary>
    public EnemyLocomotionDecisionResult Evaluate(in EnemyLocomotionContext context)
    {
        if (!context.CanSwitchLocomotion)
        {
            return EnemyLocomotionDecisionResult.KeepCurrent("SwitchBlockedByCombatPriority");
        }

        if (!context.IsFloating)
        {
            return EvaluateGroundedToFloating(context);
        }

        return EvaluateFloatingToGrounded(context);
    }

    /// <summary>
    /// 아키타입 로코모션 설정값을 정책 런타임 파라미터에 반영합니다.
    /// </summary>
    public void ApplyArchetype(EnemyArchetypeData archetype)
    {
        if (archetype == null)
        {
            return;
        }

        _switchCooldown = archetype.SwitchCooldown;
        _minimumAirTime = archetype.MinAirTime;
        _maximumAirTime = archetype.MaxAirTime;
    }

    /// <summary>
    /// 지상 상태에서 부유 전환 조건을 평가합니다.
    /// </summary>
    private EnemyLocomotionDecisionResult EvaluateGroundedToFloating(in EnemyLocomotionContext context)
    {
        bool meetsObstacleCondition = context.BlockedAhead; // 전방 장애물 조건 충족 여부입니다.
        bool meetsAltitudeCondition = Mathf.Abs(context.TargetYDelta) >= Mathf.Max(0.01f, _targetYDeltaThreshold); // 목표 고도 차 조건 충족 여부입니다.
        if (!meetsObstacleCondition && !meetsAltitudeCondition)
        {
            return EnemyLocomotionDecisionResult.KeepCurrent("NoGroundToFloatTrigger");
        }

        float elapsedSinceLastSwitch = context.CurrentTime - context.LastSwitchTime; // 최근 전환 이후 경과 시간 값입니다.
        float cooldown = Mathf.Max(_switchCooldown, context.SwitchCooldown); // 전환 쿨다운 판정에 사용할 최종 쿨다운 값입니다.
        if (elapsedSinceLastSwitch < cooldown)
        {
            return EnemyLocomotionDecisionResult.KeepCurrent("AirSwitchCooldown");
        }

        float targetAltitude = ResolveFloatingTargetAltitude(context); // 부유 전환 시 사용할 목표 고도 좌표입니다.
        string transitionReason = meetsObstacleCondition ? "ScenarioA_ObstacleDetected" : "ScenarioB_TargetAltitudeRise"; // 시나리오 분류에 사용할 지상->부유 전환 사유입니다.
        return new EnemyLocomotionDecisionResult(E_EnemyLocomotionDecisionType.SwitchToFloating, targetAltitude, Vector2.zero, transitionReason);
    }

    /// <summary>
    /// 부유 상태에서 지상 복귀 조건을 평가합니다.
    /// </summary>
    private EnemyLocomotionDecisionResult EvaluateFloatingToGrounded(in EnemyLocomotionContext context)
    {
        if (!context.HasValidGroundCandidate)
        {
            return EnemyLocomotionDecisionResult.KeepCurrent("ScenarioC_NoGroundCandidate");
        }

        float minimumAirTime = Mathf.Max(_minimumAirTime, context.MinAirTime); // 지상 복귀 최소 체류 판정에 사용할 시간 값입니다.
        if (context.TimeInCurrentMode < minimumAirTime)
        {
            return EnemyLocomotionDecisionResult.KeepCurrent("MinimumAirTime");
        }

        if (!context.CanReturnToGroundByCombat)
        {
            return EnemyLocomotionDecisionResult.KeepCurrent("CombatDisallowGroundReturn");
        }

        float maximumAirTime = Mathf.Max(minimumAirTime, Mathf.Max(_maximumAirTime, context.MaxAirTime)); // 강제 지상 복귀 판단에 사용할 최대 공중 체류 시간 값입니다.
        if (context.TimeInCurrentMode >= maximumAirTime)
        {
            return new EnemyLocomotionDecisionResult(E_EnemyLocomotionDecisionType.SwitchToGrounded, 0f, context.GroundCandidatePoint, "ScenarioC_ForceGroundedByMaxAirTime");
        }

        return new EnemyLocomotionDecisionResult(E_EnemyLocomotionDecisionType.SwitchToGrounded, 0f, context.GroundCandidatePoint, "FloatingToGrounded");
    }

    /// <summary>
    /// 타겟 유무를 고려해 부유 전환 시 사용할 목표 고도를 계산합니다.
    /// </summary>
    private float ResolveFloatingTargetAltitude(in EnemyLocomotionContext context)
    {
        float baseAltitude = context.Target != null ? context.Target.position.y : context.Self.position.y; // 부유 고도 계산의 기준이 되는 Y 좌표입니다.
        return baseAltitude + _floatingTargetOffsetY;
    }
}
