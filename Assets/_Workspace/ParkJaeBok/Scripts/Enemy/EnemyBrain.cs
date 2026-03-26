using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D 플랫포머 전투용 Enemy 템플릿의 상태 전환(순찰/추적/공격/후딜/피격/사망)을 관리하는 브레인입니다.
/// </summary>
public class EnemyBrain : MonoBehaviour
{
    private enum E_EnemyState
    {
        Spawn,
        Idle,
        Patrol,
        Chase,
        Attack,
        Recover,
        HitStun,
        Dead,
    }

    private enum E_EnemyMovementState
    {
        Grounded,
        Floating,
        SwitchingToFloating,
        SwitchingToGrounded,
    }

    [Header("References")]
    [Tooltip("상태 판단과 액션 매핑에 사용할 Enemy 데이터 참조입니다.")]
    [SerializeField] private EnemyArchetypeData _archetype; // 상태 판단과 액션 매핑에 사용할 Enemy 데이터 참조입니다.
    [Tooltip("추적/공격 대상으로 사용할 타겟 트랜스폼 참조입니다.")]
    [SerializeField] private Transform _target; // 추적/공격 대상으로 사용할 타겟 트랜스폼 참조입니다.
    [Tooltip("IEnemyTargetProvider 구현체를 담는 타겟 제공자 컴포넌트 참조입니다.")]
    [SerializeField] private MonoBehaviour _targetProviderBehaviour; // IEnemyTargetProvider 구현체를 담는 타겟 제공자 컴포넌트 참조입니다.
    [Tooltip("타겟 제공자 미사용 시 사용할 태그 문자열입니다.")]
    [SerializeField] private string _fallbackTargetTag = "Player"; // 타겟 제공자 미사용 시 사용할 태그 문자열입니다.
    [Tooltip("액션 상태 전환을 전달할 액션 컨트롤러 참조입니다.")]
    [SerializeField] private ActionController _actionController; // 액션 상태 전환을 전달할 액션 컨트롤러 참조입니다.
    [Tooltip("추적/복귀/순찰 이동 실행을 담당할 이동 드라이버 참조입니다.")]
    [SerializeField] private EnemyMovementDriver _movementDriver; // 추적/복귀/순찰 이동 실행을 담당할 이동 드라이버 참조입니다.
    [Tooltip("사망 상태 감지를 위한 체력 컴포넌트 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // 사망 상태 감지를 위한 체력 컴포넌트 참조입니다.
    [Tooltip("순찰 경로 기반 목적지 계산을 제공하는 RouteProvider 컴포넌트 참조입니다.")]
    [SerializeField] private EnemyPatrolRouteProvider _patrolRouteProvider; // 순찰 경로 기반 목적지 계산에 사용할 RouteProvider 컴포넌트 참조입니다.
    [Tooltip("저장/복원에서 Enemy 개체를 안정적으로 식별할 런타임 ID입니다.")]
    [SerializeField] private string _enemyRuntimeId; // 저장/복원 시스템에서 Enemy 개체를 식별할 고유 런타임 ID입니다.

    [Header("Advanced Extensions")]
    [Tooltip("IEnemyDecisionPolicy 구현체를 담는 전술 정책 컴포넌트 참조입니다.")]
    [SerializeField] private MonoBehaviour _decisionPolicyBehaviour; // IEnemyDecisionPolicy 구현체를 담는 전술 정책 컴포넌트 참조입니다.
    [Tooltip("IEnemyLocomotionDecisionPolicy 구현체를 담는 이동 정책 컴포넌트 참조입니다.")]
    [SerializeField] private MonoBehaviour _locomotionDecisionPolicyBehaviour; // IEnemyLocomotionDecisionPolicy 구현체를 담는 이동 정책 컴포넌트 참조입니다.
    [Tooltip("안전한 착지 후보 탐색을 수행할 SafeLandingResolver 컴포넌트 참조입니다.")]
    [SerializeField] private SafeLandingResolver _safeLandingResolver; // 안전한 착지 후보 탐색을 수행할 SafeLandingResolver 컴포넌트 참조입니다.
    [Tooltip("IEnemySkillExecutor 구현체를 담는 스킬 실행기 컴포넌트 참조입니다.")]
    [SerializeField] private MonoBehaviour _skillExecutorBehaviour; // IEnemySkillExecutor 구현체를 담는 스킬 실행기 컴포넌트 참조입니다.
    [Tooltip("IEnemyPatternRunner 구현체를 담는 보스 패턴 러너 컴포넌트 참조입니다.")]
    [SerializeField] private MonoBehaviour _patternRunnerBehaviour; // IEnemyPatternRunner 구현체를 담는 보스 패턴 러너 컴포넌트 참조입니다.
    [Tooltip("IEnemyGimmickModule 구현체를 담는 고급 기믹 모듈 배열입니다.")]
    [SerializeField] private MonoBehaviour[] _gimmickModuleBehaviours = new MonoBehaviour[0]; // IEnemyGimmickModule 구현체를 담는 고급 기믹 모듈 배열입니다.

    [Header("Debug")]
    [Tooltip("현재 전투 상태 디버그 표시 값입니다.")]
    [SerializeField] private E_EnemyState _state = E_EnemyState.Spawn; // 현재 전투 상태 디버그 표시 값입니다.
    [Tooltip("현재 이동 상태 디버그 표시 값입니다.")]
    [SerializeField] private E_EnemyMovementState _movementState = E_EnemyMovementState.Grounded; // 현재 이동 상태 디버그 표시 값입니다.
    [Tooltip("최근 이동 상태 전환 사유 디버그 문자열입니다.")]
    [SerializeField] private string _movementTransitionReason = "Initialized"; // 최근 이동 상태 전환 사유 디버그 문자열입니다.
    [Tooltip("현재 전투 상태 + 이동 상태 + 전환 사유를 합친 디버그 문자열입니다.")]
    [SerializeField] private string _debugStateSummary = ""; // 현재 전투 상태 + 이동 상태 + 전환 사유를 합친 디버그 문자열입니다.
    [Tooltip("마지막 이동 전환 트리거 시각 디버그 값입니다.")]
    [SerializeField] private float _lastTransitionLoggedTime; // 마지막 이동 전환 트리거 시각 디버그 값입니다.
    [Tooltip("마지막 착지 후보 개수 디버그 값입니다.")]
    [SerializeField] private int _lastLandingCandidateCount; // 마지막 착지 후보 개수 디버그 값입니다.
    [Tooltip("마지막 착지 선택 인덱스 디버그 값입니다.")]
    [SerializeField] private int _lastLandingSelectedIndex = -1; // 마지막 착지 선택 인덱스 디버그 값입니다.
    [Tooltip("마지막 fallback 사유 디버그 문자열입니다.")]
    [SerializeField] private string _lastFallbackReason = ""; // 마지막 fallback 사유 디버그 문자열입니다.
    [Header("Patrol Recovery")]
    [Tooltip("순찰 중 진행 정체(stuck) 복구 로직 사용 여부입니다.")]
    [SerializeField] private bool _enablePatrolStuckRecovery = true; // 순찰 중 진행 정체(stuck) 복구 로직 사용 여부입니다.
    [Tooltip("순찰 진행 정체를 판정할 최소 진행 거리 임계값입니다.")]
    [SerializeField] private float _patrolProgressDistanceThreshold = 0.03f; // 순찰 진행 정체를 판정할 최소 진행 거리 임계값입니다.
    [Tooltip("순찰 진행 정체를 판정할 경과 시간(초)입니다.")]
    [SerializeField] private float _patrolStuckTimeThreshold = 0.6f; // 순찰 진행 정체를 판정할 경과 시간(초)입니다.
    [Tooltip("순찰 정체 복구를 재시도하기 전 대기 시간(초)입니다.")]
    [SerializeField] private float _patrolStuckRecoverCooldown = 0.25f; // 순찰 정체 복구를 재시도하기 전 대기 시간(초)입니다.
    [Tooltip("순찰 정체 복구 시 우회 후보 목적지를 유지할 최소 시간(초)입니다.")]
    [SerializeField] private float _patrolBypassHoldTime = 0.2f; // 순찰 정체 복구 시 우회 후보 목적지를 유지할 최소 시간(초)입니다.
    [Tooltip("현재 순찰 정체 지속 시간 디버그 값입니다.")]
    [SerializeField] private float _patrolStuckElapsed; // 현재 순찰 정체 지속 시간 디버그 값입니다.
    [Tooltip("마지막 순찰 거리 진행량(양수면 접근, 음수면 이탈) 디버그 값입니다.")]
    [SerializeField] private float _lastPatrolProgressDelta; // 마지막 순찰 거리 진행량(양수면 접근, 음수면 이탈) 디버그 값입니다.
    [Tooltip("누적된 순찰 정체 복구 시도 횟수 디버그 값입니다.")]
    [SerializeField] private int _patrolStuckRecoverCount; // 누적된 순찰 정체 복구 시도 횟수 디버그 값입니다.
    [Header("Floating Altitude Follow")]
    [Tooltip("Floating 이동 중 전투 이동 목표의 Y 좌표를 추적해 고도 명령을 갱신할지 여부입니다.")]
    [SerializeField] private bool _followMoveTargetYWhenFloating = true; // Floating 이동 중 전투 이동 목표의 Y 좌표를 추적해 고도 명령을 갱신할지 여부입니다.
    [Tooltip("Floating 이동 시 이동 목표 Y 좌표에 더할 추가 오프셋 값입니다.")]
    [SerializeField] private float _floatingMoveTargetYOffset; // Floating 이동 시 이동 목표 Y 좌표에 더할 추가 오프셋 값입니다.
    [Tooltip("현재 프레임에 계산된 Floating 고도 명령 디버그 값입니다.")]
    [SerializeField] private float _resolvedFloatingAltitudeCommand; // 현재 프레임에 계산된 Floating 고도 명령 디버그 값입니다.

    private IEnemyTargetProvider _targetProvider; // 런타임 타겟 획득에 사용할 TargetProvider 인터페이스 참조입니다.
    private IEnemyDecisionPolicy _decisionPolicy; // 런타임 전술 결정 오버라이드에 사용할 정책 인터페이스 참조입니다.
    private IEnemyLocomotionDecisionPolicy _locomotionDecisionPolicy; // 런타임 이동 모드 전환 판단에 사용할 정책 인터페이스 참조입니다.
    private IEnemySkillExecutor _skillExecutor; // 런타임 공격 상태에서 스킬 실행을 위임할 실행기 인터페이스 참조입니다.
    private IEnemyPatternRunner _patternRunner; // 런타임 보스 패턴 오버라이드를 처리할 패턴 러너 인터페이스 참조입니다.

    private readonly List<IEnemyGimmickModule> _gimmickModules = new List<IEnemyGimmickModule>(); // 런타임 프레임 갱신할 기믹 모듈 목록입니다.

    private Vector2 _spawnPosition; // 리쉬 복귀/순찰 기준에 사용할 스폰 기준 좌표입니다.
    private Vector2 _patrolDestination; // 순찰 이동 시 도착해야 할 현재 순찰 목적지 좌표입니다.

    private float _lastAttackTime = -999f; // 공격 쿨다운 계산에 사용할 마지막 공격 요청 시각입니다.
    private float _recoverUntilTime; // Recover 상태 유지 종료 시각입니다.
    private float _hitStunUntilTime; // HitStun 상태 유지 종료 시각입니다.
    private float _nextPatrolPickTime; // 다음 순찰 목적지 재추첨 가능 시각입니다.
    private int _pendingAttackExecutionId = -1; // Recover 시작을 대기 중인 공격 액션 실행 식별자입니다.
    private bool _isWaitingRecoverFromAttack; // 공격 액션 완료 후 Recover 진입 대기 상태 여부입니다.
    private int _lastObservedHitExecutionId = -1; // HitStun 시간 갱신에 이미 반영한 Hit 액션 실행 식별자입니다.

    private bool _didRequestDeadAction; // 사망 액션 중복 요청을 방지하는 플래그입니다.

    private bool _combatWantsMove; // 전투 상태 머신이 이번 프레임 이동을 요청했는지 여부입니다.
    private Vector2 _combatTargetPosition; // 전투 상태 머신이 계산한 이번 프레임 이동 목표 좌표입니다.
    private float _movementStateUntilTime; // 전환 상태 유지 종료 시각입니다.
    private float _lastLocomotionSwitchTime = -999f; // 최근 이동 모드 전환이 발생한 시각입니다.
    private float _movementStateEnteredTime; // 현재 이동 상태에 진입한 시각입니다.
    private float _floatingAltitudeCommand; // 부유 이동 시 사용할 목표 고도 좌표입니다.
    private float _nextLandingResolveTime; // 다음 착지 후보 재탐색이 허용되는 시각입니다.
    private string _lastScenarioLogKey = string.Empty; // 중복 시나리오 로그 출력을 방지하기 위한 마지막 로그 키입니다.
    private float _lastPatrolDistance = float.PositiveInfinity; // 순찰 진행 정체 판정에 사용할 직전 프레임 거리 값입니다.
    private float _lastPatrolProgressTime; // 순찰 진행이 마지막으로 확인된 시각입니다.
    private float _nextPatrolRecoverTime; // 순찰 정체 복구 재시도 허용 시각입니다.

    /// <summary>
    /// 저장/복원에서 사용할 Enemy 런타임 식별자를 반환합니다.
    /// </summary>
    public string EnemyRuntimeId => _enemyRuntimeId;

    /// <summary>
    /// Enemy 아키타입 식별자를 반환합니다.
    /// </summary>
    public string ArchetypeId => _archetype != null ? _archetype.ArchetypeId : string.Empty;

    /// <summary>
    /// Enemy 스폰 기준 좌표를 반환합니다.
    /// </summary>
    public Vector2 SpawnPosition => _spawnPosition;

    /// <summary>
    /// 현재 Enemy가 타겟을 보유 중인지 반환합니다.
    /// </summary>
    public bool HasTarget => _target != null;

    /// <summary>
    /// 현재 Enemy 로코모션 타입을 반환합니다.
    /// </summary>
    public E_EnemyLocomotionType CurrentLocomotionType => _movementState == E_EnemyMovementState.Floating || _movementState == E_EnemyMovementState.SwitchingToFloating
        ? E_EnemyLocomotionType.Floating
        : E_EnemyLocomotionType.Grounded;

    /// <summary>
    /// 초기 참조를 보정하고 스폰 기준 좌표를 기록합니다.
    /// </summary>
    private void Awake()
    {
        EnsureEnemyRuntimeId();
        TryResolveReferences();
        ResolveExtensionModules();

        _spawnPosition = transform.position;
        _patrolDestination = _spawnPosition;

        ApplyArchetypeToLocomotionModules();
    }

    /// <summary>
    /// 인스펙터 값 변경 시 런타임 ID 누락을 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        EnsureEnemyRuntimeId();
    }

    /// <summary>
    /// 활성화 시 초기 상태를 리셋하고 스폰 액션을 요청합니다.
    /// </summary>
    private void OnEnable()
    {
        _didRequestDeadAction = false;
        _recoverUntilTime = 0f;
        _hitStunUntilTime = 0f;
        _nextPatrolPickTime = 0f;
        _pendingAttackExecutionId = -1;
        _isWaitingRecoverFromAttack = false;
        _lastObservedHitExecutionId = -1;

        _state = E_EnemyState.Spawn;
        _movementState = GetDefaultLocomotionType() == E_EnemyLocomotionType.Floating ? E_EnemyMovementState.Floating : E_EnemyMovementState.Grounded;
        _movementTransitionReason = "OnEnable Reset";
        _debugStateSummary = string.Empty;
        _movementStateUntilTime = 0f;
        _movementStateEnteredTime = Time.time;
        _lastLocomotionSwitchTime = -999f;
        _floatingAltitudeCommand = _spawnPosition.y + GetFloatingAltitude();
        _nextLandingResolveTime = 0f;
        _lastTransitionLoggedTime = 0f;
        _lastLandingCandidateCount = 0;
        _lastLandingSelectedIndex = -1;
        _lastFallbackReason = string.Empty;
        _lastScenarioLogKey = string.Empty;
        _patrolStuckElapsed = 0f;
        _lastPatrolProgressDelta = 0f;
        _patrolStuckRecoverCount = 0;
        _lastPatrolDistance = float.PositiveInfinity;
        _lastPatrolProgressTime = Time.time;
        _nextPatrolRecoverTime = 0f;
        RequestActionSafe(GetSpawnAction());
        ResetCombatMovementCommand();
        TryResolveTarget();
        ApplyArchetypeToLocomotionModules();
    }

    /// <summary>
    /// 매 프레임 전투 상태를 계산한 뒤 이동 상태 머신이 최종 이동 명령을 적용합니다.
    /// </summary>
    private void Update()
    {
        if (!TryResolveReferences())
        {
            return;
        }

        ResolveExtensionModules();
        DetectHitStunFromActionRuntime();
        TryResolveTarget();

        EnemyBrainContext context = BuildContext(); // 이번 프레임 확장 모듈 평가에 전달할 문맥 스냅샷입니다.
        TickGimmickModules(context);

        if (ShouldPatternOverride(context))
        {
            return;
        }

        TickCombatStateMachine(context);
        TickMovementStateMachineAndApplyCommand();
        RefreshDebugSummary();
    }

    /// <summary>
    /// 기존 전투 상태 머신 흐름으로 현재 전투 상태와 이동 의도를 계산합니다.
    /// </summary>
    private void TickCombatStateMachine(in EnemyBrainContext context)
    {
        ResetCombatMovementCommand();

        if (IsDead())
        {
            EnterDeadState();
            return;
        }

        if (IsInHitStunWindow())
        {
            EnterHitStunState();
            return;
        }

        TryBeginRecoverFromCompletedAttack();

        if (IsInRecoverWindow())
        {
            EnterRecoverState();
            return;
        }

        if (_target != null)
        {
            EnemyDecisionResult decision = EvaluateDecisionPolicy(context); // 확장 전술 정책이 계산한 현재 프레임 오버라이드 결과입니다.
            if (ApplyDecisionOverride(decision))
            {
                return;
            }

            float distanceToTarget = context.DistanceToTarget; // 기본 전투 상태 전이 계산에 사용할 타겟 거리 값입니다.
            if (CanAttack(distanceToTarget))
            {
                EnterAttackState(context);
                return;
            }

            if (CanChase(distanceToTarget))
            {
                EnterChaseState();
                return;
            }
        }

        if (ShouldReturnToSpawn())
        {
            EnterReturnToSpawnState();
            return;
        }

        if (UsePatrol())
        {
            EnterPatrolState();
            return;
        }

        EnterIdleState();
    }

    /// <summary>
    /// 추적 상태로 전환하고 이동 목적지를 타겟으로 설정합니다.
    /// </summary>
    private void EnterChaseState()
    {
        _state = E_EnemyState.Chase;
        RequestActionSafe(GetMoveAction());
        SetCombatMovementTarget(_target.position);
    }

    /// <summary>
    /// 후퇴 상태로 전환하고 지정된 좌표를 향해 이동합니다.
    /// </summary>
    private void EnterRetreatState(Vector2 retreatTargetPosition)
    {
        _state = E_EnemyState.Chase;
        RequestActionSafe(GetMoveAction());
        SetCombatMovementTarget(retreatTargetPosition);
    }

    /// <summary>
    /// 공격 상태로 전환하고 조건을 만족하면 공격 액션 또는 확장 스킬 실행을 시도한 뒤 Recover 윈도우를 시작합니다.
    /// </summary>
    private void EnterAttackState(in EnemyBrainContext context)
    {
        _state = E_EnemyState.Attack;
        ClearCombatMovementTarget();

        if (Time.time - _lastAttackTime < GetAttackCooldown())
        {
            return;
        }

        bool executedBySkill = false; // 확장 스킬 실행기가 이번 공격 실행을 처리했는지 여부입니다.
        if (_skillExecutor != null)
        {
            executedBySkill = _skillExecutor.TryExecuteSkill(context, _actionController);
        }

        bool accepted = executedBySkill || RequestActionSafe(GetAttackAction()); // 이번 공격 시도의 최종 수락 여부입니다.
        if (!accepted)
        {
            return;
        }

        _lastAttackTime = Time.time;
        StartRecoverPendingFromCurrentAction();
    }

    /// <summary>
    /// 공격 후딜 상태를 유지하며 이동을 정지합니다.
    /// </summary>
    private void EnterRecoverState()
    {
        _state = E_EnemyState.Recover;
        ClearCombatMovementTarget();
        RequestActionSafe(GetRecoverAction());
    }

    /// <summary>
    /// 피격 경직 상태를 유지하며 이동을 정지합니다.
    /// </summary>
    private void EnterHitStunState()
    {
        _state = E_EnemyState.HitStun;
        ClearRecoverPendingFromAttack();
        ClearCombatMovementTarget();
        RequestActionSafe(GetHitStunAction());
    }

    /// <summary>
    /// 스폰 위치로 복귀 이동하는 상태를 처리합니다.
    /// </summary>
    private void EnterReturnToSpawnState()
    {
        _state = E_EnemyState.Chase;
        RequestActionSafe(GetMoveAction());
        SetCombatMovementTarget(_spawnPosition);
    }

    /// <summary>
    /// 순찰 상태를 처리하며 랜덤 목적지 갱신과 도착 대기를 관리합니다.
    /// </summary>
    private void EnterPatrolState()
    {
        _state = E_EnemyState.Patrol;
        RequestActionSafe(GetPatrolAction());

        if (Time.time < _nextPatrolPickTime)
        {
            ClearCombatMovementTarget();
            return;
        }

        float distanceToPatrolDestination = Vector2.Distance(transform.position, _patrolDestination); // 현재 위치와 순찰 목적지 사이 거리 값입니다.
        UpdatePatrolProgressMonitor(distanceToPatrolDestination);
        if (distanceToPatrolDestination <= GetStoppingDistance())
        {
            if (_patrolRouteProvider != null)
            {
                _patrolDestination = _patrolRouteProvider.GetNextPoint(_spawnPosition, GetPatrolRadius(), transform.position);
            }
            else
            {
                _patrolDestination = PickPatrolDestination();
            }

            ResetPatrolProgressMonitor();
            distanceToPatrolDestination = Vector2.Distance(transform.position, _patrolDestination);
            UpdatePatrolProgressMonitor(distanceToPatrolDestination);
        }

        if (TryRecoverPatrolWhenStuck())
        {
            distanceToPatrolDestination = Vector2.Distance(transform.position, _patrolDestination);
            UpdatePatrolProgressMonitor(distanceToPatrolDestination);
        }

        SetCombatMovementTarget(_patrolDestination);
    }

    /// <summary>
    /// 현재 순찰 목적지에 대한 거리 변화량을 기반으로 정체(stuck) 시간을 누적/리셋합니다.
    /// </summary>
    private void UpdatePatrolProgressMonitor(float distanceToPatrolDestination)
    {
        if (float.IsInfinity(_lastPatrolDistance))
        {
            _lastPatrolDistance = distanceToPatrolDestination;
            _lastPatrolProgressTime = Time.time;
            _patrolStuckElapsed = 0f;
            _lastPatrolProgressDelta = 0f;
            return;
        }

        float progressDelta = _lastPatrolDistance - distanceToPatrolDestination; // 직전 프레임 대비 현재 프레임 순찰 목적지 접근 거리 변화량입니다.
        _lastPatrolProgressDelta = progressDelta;
        if (progressDelta > Mathf.Max(0f, _patrolProgressDistanceThreshold))
        {
            _lastPatrolProgressTime = Time.time;
        }

        _patrolStuckElapsed = Time.time - _lastPatrolProgressTime;
        _lastPatrolDistance = distanceToPatrolDestination;
    }

    /// <summary>
    /// 순찰 목적지가 갱신될 때 진행 정체 모니터링 상태를 초기화합니다.
    /// </summary>
    private void ResetPatrolProgressMonitor()
    {
        _lastPatrolDistance = float.PositiveInfinity;
        _lastPatrolProgressTime = Time.time;
        _patrolStuckElapsed = 0f;
        _lastPatrolProgressDelta = 0f;
    }

    /// <summary>
    /// 순찰 정체가 감지되면 엣지 우회 후보 또는 다음 순찰 지점을 사용해 복구를 시도합니다.
    /// </summary>
    private bool TryRecoverPatrolWhenStuck()
    {
        if (!_enablePatrolStuckRecovery || Time.time < _nextPatrolRecoverTime)
        {
            return false;
        }

        if (_patrolStuckElapsed < Mathf.Max(0f, _patrolStuckTimeThreshold))
        {
            return false;
        }

        if (_movementDriver != null && _movementDriver.TryGetEdgeBypassCandidate(out Vector2 bypassCandidate))
        {
            _patrolDestination = bypassCandidate;
            _nextPatrolPickTime = Time.time + Mathf.Max(0f, _patrolBypassHoldTime);
        }
        else if (_patrolRouteProvider != null)
        {
            _patrolDestination = _patrolRouteProvider.GetNextPoint(_spawnPosition, GetPatrolRadius(), transform.position);
        }
        else
        {
            _patrolDestination = PickPatrolDestination();
        }

        _patrolStuckRecoverCount++;
        _nextPatrolRecoverTime = Time.time + Mathf.Max(0f, _patrolStuckRecoverCooldown);
        ResetPatrolProgressMonitor();
        return true;
    }

    /// <summary>
    /// 대기 상태로 전환하고 이동을 정지합니다.
    /// </summary>
    private void EnterIdleState()
    {
        _state = E_EnemyState.Idle;
        ClearCombatMovementTarget();
        RequestActionSafe(GetIdleAction());
    }

    /// <summary>
    /// 사망 상태로 전환하고 이동을 정지한 뒤 사망 액션을 1회 요청합니다.
    /// </summary>
    private void EnterDeadState()
    {
        _state = E_EnemyState.Dead;
        ClearRecoverPendingFromAttack();
        ClearCombatMovementTarget();

        if (_didRequestDeadAction)
        {
            return;
        }

        _didRequestDeadAction = RequestActionSafe(GetDeadAction());
    }

    /// <summary>
    /// 전투 상태 계산 결과를 바탕으로 이동 상태 머신을 갱신하고 최종 이동 명령을 드라이버에 전달합니다.
    /// </summary>
    private void TickMovementStateMachineAndApplyCommand()
    {
        bool canSwitchMovement = CanSwitchMovementState(); // 이번 프레임 이동 모드 전환 가능 여부입니다.
        EnemyLocomotionDecisionResult decision = EvaluateLocomotionDecision(canSwitchMovement); // 이동 정책이 반환한 이번 프레임 전환 결정입니다.
        ApplyLocomotionDecision(decision);

        bool useAltitudeCommand = _movementState == E_EnemyMovementState.Floating || _movementState == E_EnemyMovementState.SwitchingToFloating || _movementState == E_EnemyMovementState.SwitchingToGrounded;
        float altitude = ResolveFloatingAltitudeCommand(useAltitudeCommand); // 부유/전환 상태에서 유지할 기준 고도 좌표입니다.
        _resolvedFloatingAltitudeCommand = altitude;
        float moveSpeed = GetMoveSpeedByMovementState(); // 이동 상태별 최종 이동 속도입니다.

        _movementDriver.ApplyBrainCommand(_combatTargetPosition, _combatWantsMove, moveSpeed, useAltitudeCommand, altitude);
    }

    /// <summary>
    /// 현재 이동 상태와 옵션을 기준으로 이번 프레임 Floating 고도 명령 값을 계산합니다.
    /// </summary>
    private float ResolveFloatingAltitudeCommand(bool useAltitudeCommand)
    {
        if (!useAltitudeCommand)
        {
            return _floatingAltitudeCommand;
        }

        if (!_followMoveTargetYWhenFloating)
        {
            return _floatingAltitudeCommand;
        }

        if (_movementState == E_EnemyMovementState.SwitchingToFloating || _movementState == E_EnemyMovementState.SwitchingToGrounded)
        {
            return _floatingAltitudeCommand;
        }

        if (!_combatWantsMove)
        {
            return _floatingAltitudeCommand;
        }

        _floatingAltitudeCommand = _combatTargetPosition.y + _floatingMoveTargetYOffset;
        return _floatingAltitudeCommand;
    }

    /// <summary>
    /// 전투 우선순위와 아키타입 플래그를 기준으로 이동 모드 전환 가능 여부를 반환합니다.
    /// </summary>
    private bool CanSwitchMovementState()
    {
        if (!GetCanSwitchLocomotion())
        {
            return false;
        }

        if (_state == E_EnemyState.Dead || _state == E_EnemyState.HitStun || _state == E_EnemyState.Recover)
        {
            return false;
        }

        if (_state == E_EnemyState.Attack && !GetAllowMovementTransitionDuringAttack())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 아키타입 로코모션 설정을 Driver/Policy/Resolver에 반영합니다.
    /// </summary>
    private void ApplyArchetypeToLocomotionModules()
    {
        if (_movementDriver != null)
        {
            _movementDriver.ApplyArchetype(_archetype);
        }

        if (_locomotionDecisionPolicy is LocomotionDecisionPolicy locomotionPolicy)
        {
            locomotionPolicy.ApplyArchetype(_archetype);
        }

        if (_safeLandingResolver != null)
        {
            _safeLandingResolver.ApplyArchetype(_archetype);
        }
    }

    /// <summary>
    /// 정책 평가와 전환 완료 상태를 반영해 이번 프레임 로코모션 결정을 계산합니다.
    /// </summary>
    private EnemyLocomotionDecisionResult EvaluateLocomotionDecision(bool canSwitchMovement)
    {
        if (_movementState == E_EnemyMovementState.SwitchingToFloating || _movementState == E_EnemyMovementState.SwitchingToGrounded)
        {
            if (Time.time >= _movementStateUntilTime)
            {
                if (_movementState == E_EnemyMovementState.SwitchingToFloating)
                {
                    EnterMovementState(E_EnemyMovementState.Floating, "SwitchToFloatingCompleted");
                }
                else
                {
                    EnterMovementState(E_EnemyMovementState.Grounded, "SwitchToGroundedCompleted");
                }
            }

            return EnemyLocomotionDecisionResult.KeepCurrent("SwitchingInProgress");
        }

        if (!canSwitchMovement)
        {
            if (_state == E_EnemyState.Dead || _state == E_EnemyState.HitStun || _state == E_EnemyState.Recover)
            {
                return EnemyLocomotionDecisionResult.KeepCurrent("ScenarioD_TransitionBlockedByCombatPriority");
            }

            return EnemyLocomotionDecisionResult.KeepCurrent("TransitionBlockedByAttackArchetypeFlag");
        }

        EnemyLocomotionContext context = BuildLocomotionContext(canSwitchMovement); // 이동 정책 평가에 사용할 입력 컨텍스트입니다.
        if (_locomotionDecisionPolicy != null)
        {
            return _locomotionDecisionPolicy.Evaluate(context);
        }

        return EvaluateFallbackLocomotionDecision(context);
    }

    /// <summary>
    /// 정책 모듈이 없는 경우 사용할 기본 로코모션 결정을 반환합니다.
    /// </summary>
    private EnemyLocomotionDecisionResult EvaluateFallbackLocomotionDecision(in EnemyLocomotionContext context)
    {
        if (!GetUseFloatingMovement())
        {
            return EnemyLocomotionDecisionResult.KeepCurrent("FloatingDisabled");
        }

        if (!context.IsFloating && (context.BlockedAhead || Mathf.Abs(context.TargetYDelta) >= 1f))
        {
            return new EnemyLocomotionDecisionResult(E_EnemyLocomotionDecisionType.SwitchToFloating, _spawnPosition.y + GetFloatingAltitude(), Vector2.zero, "FallbackGroundToFloating");
        }

        if (context.IsFloating && context.HasValidGroundCandidate && context.CanReturnToGroundByCombat)
        {
            return new EnemyLocomotionDecisionResult(E_EnemyLocomotionDecisionType.SwitchToGrounded, 0f, context.GroundCandidatePoint, "FallbackFloatingToGrounded");
        }

        return EnemyLocomotionDecisionResult.KeepCurrent("FallbackKeep");
    }

    /// <summary>
    /// 정책이 반환한 로코모션 결정을 이동 상태 머신에 반영합니다.
    /// </summary>
    private void ApplyLocomotionDecision(in EnemyLocomotionDecisionResult decision)
    {
        if (!string.IsNullOrWhiteSpace(decision.Reason))
        {
            _movementTransitionReason = decision.Reason;
        }

        if (decision.DecisionType == E_EnemyLocomotionDecisionType.SwitchToFloating && _movementState == E_EnemyMovementState.Grounded)
        {
            _floatingAltitudeCommand = decision.TargetAltitude;
            EnterMovementState(E_EnemyMovementState.SwitchingToFloating, decision.Reason);
            _movementStateUntilTime = Time.time + GetMovementSwitchDuration();
            _lastLocomotionSwitchTime = Time.time;
            _lastTransitionLoggedTime = Time.time;
            EmitScenarioLog(decision.Reason, _lastFallbackReason);
            return;
        }

        if (decision.DecisionType == E_EnemyLocomotionDecisionType.SwitchToGrounded && _movementState == E_EnemyMovementState.Floating)
        {
            SetCombatMovementTarget(decision.LandingPoint);
            EnterMovementState(E_EnemyMovementState.SwitchingToGrounded, decision.Reason);
            _movementStateUntilTime = Time.time + GetMovementSwitchDuration();
            _lastLocomotionSwitchTime = Time.time;
            _lastTransitionLoggedTime = Time.time;
            EmitScenarioLog(decision.Reason, _lastFallbackReason);
            return;
        }

        if (decision.DecisionType == E_EnemyLocomotionDecisionType.KeepCurrent && !string.IsNullOrWhiteSpace(decision.Reason))
        {
            _lastFallbackReason = decision.Reason;
            _lastTransitionLoggedTime = Time.time;
            EmitScenarioLog(decision.Reason, _lastFallbackReason);
        }
    }

    /// <summary>
    /// 시나리오별 디버그 로그를 중복 없이 출력합니다.
    /// </summary>
    private void EmitScenarioLog(string triggerReason, string fallbackReason)
    {
        if (_lastTransitionLoggedTime <= 0f)
        {
            _lastTransitionLoggedTime = Time.time;
        }

        string logKey = $"{triggerReason}|{_lastLandingCandidateCount}|{_lastLandingSelectedIndex}|{fallbackReason}|{_state}|{_movementState}"; // 동일 로그 중복 출력을 막기 위한 비교 키입니다.
        if (_lastScenarioLogKey == logKey)
        {
            return;
        }

        _lastScenarioLogKey = logKey;
        Debug.Log($"[EnemyLocomotionScenarioLog] Trigger={triggerReason}, Time={_lastTransitionLoggedTime:0.000}, LandingCandidates={_lastLandingCandidateCount}, SelectedIndex={_lastLandingSelectedIndex}, Fallback={fallbackReason}, Combat={_state}, Move={_movementState}", this);
    }

    /// <summary>
    /// 이동 상태에 따른 최종 이동 속도 값을 반환합니다.
    /// </summary>
    private float GetMoveSpeedByMovementState()
    {
        float baseSpeed = _archetype != null ? _archetype.MoveSpeed : 2.5f; // 이동 상태 계산 기준이 되는 기본 이동 속도입니다.
        if (_movementState == E_EnemyMovementState.SwitchingToFloating || _movementState == E_EnemyMovementState.SwitchingToGrounded)
        {
            return baseSpeed * 0.8f;
        }

        return baseSpeed;
    }

    /// <summary>
    /// 전투 상태 머신이 계산한 이동 목표를 설정합니다.
    /// </summary>
    private void SetCombatMovementTarget(Vector2 targetPosition)
    {
        _combatTargetPosition = targetPosition;
        _combatWantsMove = true;
    }

    /// <summary>
    /// 전투 상태 머신이 계산한 이동 목표를 제거합니다.
    /// </summary>
    private void ClearCombatMovementTarget()
    {
        _combatWantsMove = false;
    }

    /// <summary>
    /// 프레임 시작 시 전투 상태 머신의 이동 명령 버퍼를 초기화합니다.
    /// </summary>
    private void ResetCombatMovementCommand()
    {
        _combatWantsMove = false;
        _combatTargetPosition = transform.position;
    }

    /// <summary>
    /// 디버그 표기용 상태 요약 문자열을 최신 값으로 갱신합니다.
    /// </summary>
    private void RefreshDebugSummary()
    {
        _debugStateSummary = $"Combat:{_state} | Move:{_movementState} | Reason:{_movementTransitionReason}";
    }

    /// <summary>
    /// 현재 프레임 센서/전투 상태를 기반으로 이동 정책 입력 컨텍스트를 구성합니다.
    /// </summary>
    private EnemyLocomotionContext BuildLocomotionContext(bool canSwitchMovement)
    {
        Vector2 selfPosition = transform.position; // 이동 컨텍스트 계산 기준이 되는 현재 자신의 위치입니다.
        float targetDistance = _target != null ? Vector2.Distance(selfPosition, _target.position) : float.PositiveInfinity; // 타겟까지의 현재 거리 값입니다.
        float targetYDelta = _target != null ? _target.position.y - selfPosition.y : 0f; // 타겟과 자신의 현재 Y축 고도 차이 값입니다.
        bool blockedAhead = IsBlockedAhead(); // 전방 장애물 감지 결과입니다.
        bool isGrounded = IsGrounded(); // 현재 프레임 접지 판정 결과입니다.
        bool hasValidGroundCandidate = TryResolveGroundCandidate(out Vector2 groundCandidatePoint); // 착지 후보 유효성 판정 결과입니다.
        bool canReturnToGroundByCombat = _state != E_EnemyState.Attack && _state != E_EnemyState.HitStun; // 전투 상태 기준 지상 복귀 허용 여부입니다.
        bool isFloating = _movementState == E_EnemyMovementState.Floating; // 정책 입력에 사용할 현재 부유 상태 여부입니다.
        float timeInCurrentMode = Time.time - _movementStateEnteredTime; // 현재 이동 상태 진입 후 경과 시간입니다.

        return new EnemyLocomotionContext(
            transform,
            _target,
            targetDistance,
            targetYDelta,
            blockedAhead,
            isGrounded,
            _lastLocomotionSwitchTime,
            Time.time,
            canSwitchMovement,
            GetSwitchCooldown(),
            isFloating,
            timeInCurrentMode,
            GetMinAirTime(),
            GetMaxAirTime(),
            hasValidGroundCandidate,
            groundCandidatePoint,
            canReturnToGroundByCombat);
    }

    /// <summary>
    /// 현재 진행 방향 전방에 장애물이 있는지 레이캐스트로 판정합니다.
    /// </summary>
    private bool IsBlockedAhead()
    {
        int facingDirection = _movementDriver != null ? _movementDriver.FacingDirection : 1; // 전방 장애물 레이캐스트에 사용할 좌우 방향 값입니다.
        Vector2 origin = transform.position + new Vector3(0f, 0.1f, 0f); // 전방 장애물 감지를 시작할 기준 좌표입니다.
        float checkDistance = Mathf.Max(0.1f, GetLocomotionObstacleCheckDistance()); // 전방 장애물 감지에 사용할 레이 길이 값입니다.
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * facingDirection, checkDistance); // 전방 장애물 감지 결과입니다.
        if (!hit.collider)
        {
            return false;
        }

        return hit.collider.transform != transform;
    }

    /// <summary>
    /// 현재 위치 기준 접지 상태를 레이캐스트로 판정합니다.
    /// </summary>
    private bool IsGrounded()
    {
        Vector2 origin = transform.position + new Vector3(0f, 0.05f, 0f); // 접지 판정 시작점 좌표입니다.
        float checkDistance = Mathf.Max(0.1f, GetLocomotionGroundCheckDistance()); // 접지 판정에 사용할 레이 길이 값입니다.
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, checkDistance); // 접지 판정 레이캐스트 결과입니다.
        if (!hit.collider)
        {
            return false;
        }

        return hit.collider.transform != transform;
    }

    /// <summary>
    /// SafeLandingResolver를 사용해 착지 후보를 탐색하고 실패 시 재탐색 쿨다운을 적용합니다.
    /// </summary>
    private bool TryResolveGroundCandidate(out Vector2 groundCandidatePoint)
    {
        if (_movementState == E_EnemyMovementState.Floating && Time.time < _nextLandingResolveTime)
        {
            groundCandidatePoint = _spawnPosition;
            _lastLandingCandidateCount = 0;
            _lastLandingSelectedIndex = -1;
            _lastFallbackReason = "ScenarioC_RequeryCooldownLoop";
            return false;
        }

        bool found = false; // 이번 프레임 착지 후보를 찾았는지 여부입니다.
        if (_safeLandingResolver != null)
        {
            Vector2 targetPosition = _target != null ? (Vector2)_target.position : _spawnPosition; // 착지 후보 정렬 점수 계산에 사용할 목표 좌표입니다.
            found = _safeLandingResolver.TryResolveLanding(transform.position, targetPosition, out groundCandidatePoint);
            _lastLandingCandidateCount = _safeLandingResolver.LastCandidateCount;
            _lastLandingSelectedIndex = _safeLandingResolver.LastSelectedCandidateIndex;
            _lastFallbackReason = _safeLandingResolver.LastFailureReason;
        }
        else
        {
            found = TryResolveGroundCandidateFallback(out groundCandidatePoint);
            _lastLandingCandidateCount = found ? 1 : 0;
            _lastLandingSelectedIndex = found ? 0 : -1;
            _lastFallbackReason = found ? string.Empty : "FallbackProbeFailed";
        }

        if (!found && _movementState == E_EnemyMovementState.Floating)
        {
            _nextLandingResolveTime = Time.time + GetLandingRequeryCooldown();
            _lastFallbackReason = string.IsNullOrWhiteSpace(_lastFallbackReason) ? "ScenarioC_RequeryCooldown" : _lastFallbackReason;
            return false;
        }

        _nextLandingResolveTime = 0f;
        return found;
    }

    /// <summary>
    /// SafeLandingResolver 미설정 시 단일 하향 프로브 기반 착지 후보를 반환합니다.
    /// </summary>
    private bool TryResolveGroundCandidateFallback(out Vector2 groundCandidatePoint)
    {
        Vector2 origin = transform.position + new Vector3(0f, 0.5f, 0f); // 착지 후보 탐색을 위한 레이캐스트 시작 좌표입니다.
        float checkDistance = Mathf.Max(0.5f, GetLocomotionGroundCheckDistance() + 2f); // 착지 후보 탐색에 사용할 레이 길이 값입니다.
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, checkDistance); // 착지 후보 탐색 결과입니다.
        if (!hit.collider || hit.collider.transform == transform)
        {
            groundCandidatePoint = _spawnPosition;
            return false;
        }

        groundCandidatePoint = hit.point;
        return true;
    }

    /// <summary>
    /// 이동 상태 진입 시각과 전환 사유를 함께 갱신합니다.
    /// </summary>
    private void EnterMovementState(E_EnemyMovementState movementState, string reason)
    {
        _movementState = movementState;
        _movementStateEnteredTime = Time.time;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            _movementTransitionReason = reason;
        }
    }

    /// <summary>
    /// 런타임 액션 상태를 읽어 새 Hit 액션 실행 진입 시 1회만 피격 경직 시간을 설정합니다.
    /// </summary>
    private void DetectHitStunFromActionRuntime()
    {
        if (_actionController == null)
        {
            return;
        }

        ActionRuntime runtime = _actionController.Runtime; // 피격 상태 해석에 사용할 현재 액션 런타임 스냅샷입니다.
        if (!runtime.IsRunning)
        {
            return;
        }

        if (runtime.ActionType != E_ActionType.Hit)
        {
            return;
        }

        if (runtime.ExecutionId == _lastObservedHitExecutionId)
        {
            return;
        }

        _lastObservedHitExecutionId = runtime.ExecutionId;

        float candidateUntilTime = Time.time + GetHitStunDuration(); // 이번 프레임 피격 액션 감지 기반으로 계산한 경직 종료 시각입니다.
        if (candidateUntilTime > _hitStunUntilTime)
        {
            _hitStunUntilTime = candidateUntilTime;
        }
    }

    /// <summary>
    /// 현재 피격 경직 윈도우 유지 중인지 판정합니다.
    /// </summary>
    private bool IsInHitStunWindow()
    {
        return Time.time < _hitStunUntilTime;
    }

    /// <summary>
    /// 현재 공격 후딜 윈도우 유지 중인지 판정합니다.
    /// </summary>
    private bool IsInRecoverWindow()
    {
        return Time.time < _recoverUntilTime;
    }

    /// <summary>
    /// 대기 중인 공격 액션이 완료되었는지 검사하고 완료 시 Recover 윈도우를 시작합니다.
    /// </summary>
    private void TryBeginRecoverFromCompletedAttack()
    {
        if (!_isWaitingRecoverFromAttack)
        {
            return;
        }

        if (_actionController == null)
        {
            return;
        }

        ActionRuntime runtime = _actionController.Runtime; // 공격 완료 여부 판정에 사용할 현재 액션 런타임 스냅샷입니다.

        if (runtime.IsRunning && runtime.ExecutionId == _pendingAttackExecutionId)
        {
            return;
        }

        _isWaitingRecoverFromAttack = false;
        _pendingAttackExecutionId = -1;
        _recoverUntilTime = Time.time + GetRecoverDuration();
    }

    /// <summary>
    /// 현재 런타임 액션 실행 식별자를 Recover 대기 상태로 기록합니다.
    /// </summary>
    private void StartRecoverPendingFromCurrentAction()
    {
        if (_actionController == null)
        {
            return;
        }

        ActionRuntime runtime = _actionController.Runtime; // Recover 대기 시작 시점의 액션 런타임 스냅샷입니다.
        if (!runtime.IsRunning)
        {
            _recoverUntilTime = Time.time + GetRecoverDuration();
            return;
        }

        _pendingAttackExecutionId = runtime.ExecutionId;
        _isWaitingRecoverFromAttack = true;
    }

    /// <summary>
    /// 공격 완료 대기 중인 Recover 상태를 즉시 해제합니다.
    /// </summary>
    private void ClearRecoverPendingFromAttack()
    {
        _isWaitingRecoverFromAttack = false;
        _pendingAttackExecutionId = -1;
        _recoverUntilTime = 0f;
    }

    /// <summary>
    /// 현재 타겟 거리가 공격 조건을 만족하는지 판정합니다.
    /// </summary>
    private bool CanAttack(float distanceToTarget)
    {
        return distanceToTarget <= GetAttackRange();
    }

    /// <summary>
    /// 현재 타겟 거리가 추적 조건을 만족하는지 판정합니다.
    /// </summary>
    private bool CanChase(float distanceToTarget)
    {
        if (_state == E_EnemyState.Chase || _state == E_EnemyState.Attack || _state == E_EnemyState.Recover)
        {
            return distanceToTarget <= GetLoseRange();
        }

        return distanceToTarget <= GetDetectRange();
    }

    /// <summary>
    /// 리쉬 규칙에 따라 스폰 지점 복귀가 필요한지 판정합니다.
    /// </summary>
    private bool ShouldReturnToSpawn()
    {
        if (_archetype == null || _archetype.UseLeash == false)
        {
            return false;
        }

        float distanceFromSpawn = Vector2.Distance(transform.position, _spawnPosition); // 현재 위치와 스폰 지점 사이 거리입니다.
        return distanceFromSpawn > _archetype.LeashDistance;
    }

    /// <summary>
    /// 순찰 기능을 사용해야 하는지 판정합니다.
    /// </summary>
    private bool UsePatrol()
    {
        return _archetype != null && _archetype.UsePatrol && _archetype.PatrolRadius > 0f;
    }

    /// <summary>
    /// 현재 구성된 확장 전술 정책을 평가해 행동 오버라이드 결과를 반환합니다.
    /// </summary>
    private EnemyDecisionResult EvaluateDecisionPolicy(in EnemyBrainContext context)
    {
        if (_decisionPolicy == null)
        {
            return EnemyDecisionResult.UseDefault();
        }

        return _decisionPolicy.Evaluate(context);
    }

    /// <summary>
    /// 정책이 반환한 오버라이드 결정을 적용하고 처리 여부를 반환합니다.
    /// </summary>
    private bool ApplyDecisionOverride(EnemyDecisionResult decision)
    {
        switch (decision.DecisionType)
        {
            case E_EnemyDecisionType.ForceAttack:
                EnterAttackState(BuildContext());
                return true;
            case E_EnemyDecisionType.ForceChase:
                EnterChaseState();
                return true;
            case E_EnemyDecisionType.ForceRetreat:
                EnterRetreatState(decision.RetreatTargetPosition);
                return true;
            case E_EnemyDecisionType.HoldPosition:
                EnterIdleState();
                return true;
            case E_EnemyDecisionType.UseDefault:
            default:
                return false;
        }
    }

    /// <summary>
    /// 패턴 러너가 기본 Brain 흐름을 오버라이드할지 평가합니다.
    /// </summary>
    private bool ShouldPatternOverride(in EnemyBrainContext context)
    {
        if (_patternRunner == null)
        {
            return false;
        }

        return _patternRunner.TickAndShouldOverride(context);
    }

    /// <summary>
    /// 등록된 고급 기믹 모듈을 순회하며 프레임 업데이트를 전달합니다.
    /// </summary>
    private void TickGimmickModules(in EnemyBrainContext context)
    {
        for (int i = 0; i < _gimmickModules.Count; i++)
        {
            IEnemyGimmickModule module = _gimmickModules[i]; // 이번 프레임 업데이트를 수행할 기믹 모듈 참조입니다.
            if (module == null)
            {
                continue;
            }

            module.OnBrainTick(context);
        }
    }

    /// <summary>
    /// 타겟 제공자 또는 태그 fallback 레지스트리로 추적 대상을 보정합니다.
    /// </summary>
    private void TryResolveTarget()
    {
        if (!IsTargetAvailable(_target))
        {
            _target = null;
        }

        if (_targetProvider != null)
        {
            _target = _targetProvider.ResolveTarget(transform);
            return;
        }

        if (string.IsNullOrWhiteSpace(_fallbackTargetTag))
        {
            _target = null;
            return;
        }

        IReadOnlyList<Transform> fallbackTargets = TargetRegistry.GetTargets(_fallbackTargetTag); // fallback 타겟 보정을 위해 조회한 레지스트리 타겟 목록입니다.
        _target = fallbackTargets.Count > 0 ? fallbackTargets[0] : null;
    }

    /// <summary>
    /// 타겟 Transform이 유효하고 활성 상태인지 검증합니다.
    /// </summary>
    private static bool IsTargetAvailable(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// 순찰 반경 내 랜덤 목적지를 계산해 반환합니다.
    /// </summary>
    private Vector2 PickPatrolDestination()
    {
        float radius = GetPatrolRadius(); // 랜덤 순찰 좌표 샘플링에 사용할 반경 값입니다.
        float randomX = Random.Range(-radius, radius); // 스폰 기준 X 오프셋 랜덤 값입니다.
        return _spawnPosition + new Vector2(randomX, 0f);
    }

    /// <summary>
    /// 복구 규칙 기반으로 Enemy 상태를 초기화합니다.
    /// </summary>
    public void ResetToSpawnState(EnemyResetRuleSet.RestorePositionMode restorePositionMode, Vector3 lastKnownPosition, bool respawnIfDead, float restoreHpPercent)
    {
        TryResolveReferences();

        Vector3 restoredPosition = ResolveRestorePosition(restorePositionMode, lastKnownPosition); // 복구 초기화에 사용할 위치 좌표입니다.
        transform.position = restoredPosition;

        ApplyHealthAfterRecoveryRule(respawnIfDead, restoreHpPercent);
        _target = null;
        _didRequestDeadAction = false;
        _state = E_EnemyState.Spawn;
        _movementTransitionReason = "ResetToSpawnState";
        _floatingAltitudeCommand = _spawnPosition.y + GetFloatingAltitude();
        _movementStateEnteredTime = Time.time;
        _movementStateUntilTime = 0f;
        _movementState = GetDefaultLocomotionType() == E_EnemyLocomotionType.Floating ? E_EnemyMovementState.Floating : E_EnemyMovementState.Grounded;
        RequestActionSafe(GetSpawnAction());
    }

    /// <summary>
    /// 저장된 Enemy 상태 스냅샷을 복원합니다.
    /// </summary>
    public void ApplyRecoveredState(Vector3 restoredPosition, bool isDead, float restoredHealthPercent, E_EnemyLocomotionType locomotionType, bool hasTarget)
    {
        TryResolveReferences();

        transform.position = restoredPosition;
        ApplyHealthSnapshot(isDead, restoredHealthPercent);
        _target = hasTarget ? _target : null;

        if (isDead)
        {
            _state = E_EnemyState.Dead;
            _didRequestDeadAction = RequestActionSafe(GetDeadAction());
        }
        else
        {
            _state = E_EnemyState.Idle;
            _didRequestDeadAction = false;
            RequestActionSafe(GetIdleAction());
        }

        _movementTransitionReason = "ApplyRecoveredState";
        _movementStateEnteredTime = Time.time;
        _movementStateUntilTime = 0f;
        ApplyRecoveredLocomotion(locomotionType);
    }

    /// <summary>
    /// 현재 프레임 문맥 스냅샷을 구성해 반환합니다.
    /// </summary>
    private EnemyBrainContext BuildContext()
    {
        float distance = _target != null ? Vector2.Distance(transform.position, _target.position) : float.PositiveInfinity; // 문맥 전달에 사용할 현재 타겟 거리 값입니다.
        return new EnemyBrainContext(transform, _target, distance, _spawnPosition, _archetype);
    }

    /// <summary>
    /// 복원 위치 모드와 저장 좌표를 기반으로 최종 복원 위치를 계산합니다.
    /// </summary>
    private Vector3 ResolveRestorePosition(EnemyResetRuleSet.RestorePositionMode restorePositionMode, Vector3 lastKnownPosition)
    {
        if (restorePositionMode == EnemyResetRuleSet.RestorePositionMode.LastKnown)
        {
            return lastKnownPosition;
        }

        if (restorePositionMode == EnemyResetRuleSet.RestorePositionMode.CheckpointArea && StageSession.Instance != null)
        {
            return StageSession.Instance.LastCheckpointWorldPosition;
        }

        return _spawnPosition;
    }

    /// <summary>
    /// 복구 규칙 기준 체력 정책을 적용합니다.
    /// </summary>
    private void ApplyHealthAfterRecoveryRule(bool respawnIfDead, float restoreHpPercent)
    {
        if (_healthComponent == null)
        {
            return;
        }

        float clampedPercent = Mathf.Clamp01(restoreHpPercent); // 복구 규칙에서 전달된 체력 비율의 안전 값입니다.
        float maxHealth = Mathf.Max(1f, _healthComponent.GetMaxHealth()); // 복구 시 체력 계산에 사용할 최대 체력 값입니다.
        float restoredHealth = Mathf.Max(1f, maxHealth * clampedPercent); // 복구 규칙 기반 최종 체력 값입니다.

        if (_healthComponent.IsDead)
        {
            if (respawnIfDead)
            {
                _healthComponent.Revive(restoredHealth);
            }
            else
            {
                _healthComponent.SetCurrentHealth(0f);
            }

            return;
        }

        _healthComponent.SetCurrentHealth(restoredHealth);
    }

    /// <summary>
    /// 저장된 체력 스냅샷을 기준으로 체력 상태를 복원합니다.
    /// </summary>
    private void ApplyHealthSnapshot(bool isDead, float restoredHealthPercent)
    {
        if (_healthComponent == null)
        {
            return;
        }

        float maxHealth = Mathf.Max(1f, _healthComponent.GetMaxHealth()); // 스냅샷 비율을 실제 체력 값으로 환산할 최대 체력 값입니다.
        float healthValue = Mathf.Clamp01(restoredHealthPercent) * maxHealth; // 스냅샷 비율을 환산한 체력 값입니다.

        if (isDead)
        {
            _healthComponent.SetCurrentHealth(0f);
            return;
        }

        if (_healthComponent.IsDead)
        {
            _healthComponent.Revive(Mathf.Max(1f, healthValue));
            return;
        }

        _healthComponent.SetCurrentHealth(Mathf.Max(1f, healthValue));
    }

    /// <summary>
    /// 저장된 로코모션 타입을 기준으로 이동 상태를 복원합니다.
    /// </summary>
    private void ApplyRecoveredLocomotion(E_EnemyLocomotionType locomotionType)
    {
        _movementState = locomotionType == E_EnemyLocomotionType.Floating
            ? E_EnemyMovementState.Floating
            : E_EnemyMovementState.Grounded;

        _floatingAltitudeCommand = transform.position.y + GetFloatingAltitude();
    }

    /// <summary>
    /// 액션 컨트롤러가 준비된 경우 지정 액션 요청을 시도합니다.
    /// </summary>
    private bool RequestActionSafe(E_ActionType actionType)
    {
        if (_actionController == null)
        {
            return false;
        }

        if (actionType == E_ActionType.None)
        {
            return false;
        }

        ActionRuntime runtime = _actionController.Runtime; // 중복 요청 방지에 사용할 현재 액션 런타임 스냅샷입니다.
        if (runtime.IsRunning && runtime.ActionType == actionType)
        {
            return true;
        }

        return _actionController.RequestAction(actionType);
    }

    /// <summary>
    /// 체력 컴포넌트 상태를 기반으로 사망 여부를 판정합니다.
    /// </summary>
    private bool IsDead()
    {
        return _healthComponent != null && _healthComponent.IsDead;
    }

    /// <summary>
    /// Enemy 런타임 식별자 누락 시 계층 경로 기반 기본값으로 보정합니다.
    /// </summary>
    private void EnsureEnemyRuntimeId()
    {
        if (!string.IsNullOrWhiteSpace(_enemyRuntimeId))
        {
            return;
        }

        _enemyRuntimeId = $"{gameObject.scene.name}:{GetHierarchyPath(transform)}";
    }

    /// <summary>
    /// Transform 계층 경로 문자열을 생성합니다.
    /// </summary>
    private static string GetHierarchyPath(Transform sourceTransform)
    {
        if (sourceTransform == null)
        {
            return string.Empty;
        }

        string path = sourceTransform.name; // 계층 경로 문자열 누적에 사용할 초기 노드 이름입니다.
        Transform current = sourceTransform.parent; // 부모 방향으로 경로를 확장할 현재 Transform 참조입니다.
        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }

    /// <summary>
    /// 타겟 제공자/정책/스킬/패턴/기믹 모듈 참조를 인터페이스로 해석해 캐시합니다.
    /// </summary>
    private void ResolveExtensionModules()
    {
        _targetProvider = _targetProviderBehaviour as IEnemyTargetProvider;
        _decisionPolicy = _decisionPolicyBehaviour as IEnemyDecisionPolicy;
        _locomotionDecisionPolicy = _locomotionDecisionPolicyBehaviour as IEnemyLocomotionDecisionPolicy;
        _skillExecutor = _skillExecutorBehaviour as IEnemySkillExecutor;
        _patternRunner = _patternRunnerBehaviour as IEnemyPatternRunner;

        _gimmickModules.Clear();
        if (_gimmickModuleBehaviours == null)
        {
            return;
        }

        for (int i = 0; i < _gimmickModuleBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = _gimmickModuleBehaviours[i]; // 기믹 모듈 인터페이스 캐시 시도 대상 컴포넌트입니다.
            if (behaviour is IEnemyGimmickModule gimmickModule)
            {
                _gimmickModules.Add(gimmickModule);
            }
        }
    }

    /// <summary>
    /// 참조가 비어 있을 때 동일 오브젝트 기준으로 자동 보정을 시도합니다.
    /// </summary>
    private bool TryResolveReferences()
    {
        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
        }

        if (_movementDriver == null)
        {
            _movementDriver = GetComponent<EnemyMovementDriver>();
        }

        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }

        if (_patrolRouteProvider == null)
        {
            _patrolRouteProvider = GetComponent<EnemyPatrolRouteProvider>();
        }

        if (_safeLandingResolver == null)
        {
            _safeLandingResolver = GetComponent<SafeLandingResolver>();
        }

        return _actionController != null && _movementDriver != null;
    }

    /// <summary>
    /// 스폰 액션 설정 값을 반환합니다.
    /// </summary>
    private E_ActionType GetSpawnAction()
    {
        return _archetype != null ? _archetype.SpawnAction : E_ActionType.Spawn;
    }

    /// <summary>
    /// 대기 액션 설정 값을 반환합니다.
    /// </summary>
    private E_ActionType GetIdleAction()
    {
        return _archetype != null ? _archetype.IdleAction : E_ActionType.Idle;
    }

    /// <summary>
    /// 순찰 액션 설정 값을 반환합니다.
    /// </summary>
    private E_ActionType GetPatrolAction()
    {
        return _archetype != null ? _archetype.PatrolAction : E_ActionType.Move;
    }

    /// <summary>
    /// 이동 액션 설정 값을 반환합니다.
    /// </summary>
    private E_ActionType GetMoveAction()
    {
        return _archetype != null ? _archetype.MoveAction : E_ActionType.Move;
    }

    /// <summary>
    /// 공격 액션 설정 값을 반환합니다.
    /// </summary>
    private E_ActionType GetAttackAction()
    {
        return _archetype != null ? _archetype.AttackAction : E_ActionType.Attack;
    }

    /// <summary>
    /// 공격 후딜 액션 설정 값을 반환합니다.
    /// </summary>
    private E_ActionType GetRecoverAction()
    {
        return _archetype != null ? _archetype.RecoverAction : E_ActionType.IdleBreakA;
    }

    /// <summary>
    /// 피격 경직 액션 설정 값을 반환합니다.
    /// </summary>
    private E_ActionType GetHitStunAction()
    {
        return _archetype != null ? _archetype.HitStunAction : E_ActionType.Hit;
    }

    /// <summary>
    /// 사망 액션 설정 값을 반환합니다.
    /// </summary>
    private E_ActionType GetDeadAction()
    {
        return _archetype != null ? _archetype.DeadAction : E_ActionType.Die;
    }

    /// <summary>
    /// 공격 상태 중 이동 모드 전환 허용 여부를 반환합니다.
    /// </summary>
    private bool GetAllowMovementTransitionDuringAttack()
    {
        return _archetype != null && _archetype.AllowMovementTransitionDuringAttack;
    }

    /// <summary>
    /// 아키타입 기본 로코모션 타입을 반환합니다.
    /// </summary>
    private E_EnemyLocomotionType GetDefaultLocomotionType()
    {
        return _archetype != null ? _archetype.DefaultLocomotionType : E_EnemyLocomotionType.Grounded;
    }

    /// <summary>
    /// 로코모션 전환 허용 여부를 반환합니다.
    /// </summary>
    private bool GetCanSwitchLocomotion()
    {
        return _archetype == null || _archetype.CanSwitchLocomotion;
    }

    /// <summary>
    /// 로코모션 전환 쿨다운 시간을 반환합니다.
    /// </summary>
    private float GetSwitchCooldown()
    {
        return _archetype != null ? _archetype.SwitchCooldown : 0.8f;
    }

    /// <summary>
    /// 공중 최소 체류 시간을 반환합니다.
    /// </summary>
    private float GetMinAirTime()
    {
        return _archetype != null ? _archetype.MinAirTime : 0.6f;
    }

    /// <summary>
    /// 공중 최대 체류 시간을 반환합니다.
    /// </summary>
    private float GetMaxAirTime()
    {
        return _archetype != null ? _archetype.MaxAirTime : 4f;
    }

    /// <summary>
    /// 부유 이동 모드 사용 여부를 반환합니다.
    /// </summary>
    private bool GetUseFloatingMovement()
    {
        return _archetype != null && _archetype.UseFloatingMovement;
    }

    /// <summary>
    /// 부유 이동 고도 오프셋 값을 반환합니다.
    /// </summary>
    private float GetFloatingAltitude()
    {
        return _archetype != null ? _archetype.FloatingAltitude : 1.5f;
    }

    /// <summary>
    /// 이동 모드 전환 시간을 반환합니다.
    /// </summary>
    private float GetMovementSwitchDuration()
    {
        return _archetype != null ? _archetype.MovementSwitchDuration : 0.2f;
    }

    /// <summary>
    /// 전방 장애물 감지 레이 길이를 반환합니다.
    /// </summary>
    private float GetLocomotionObstacleCheckDistance()
    {
        return _archetype != null ? _archetype.LocomotionObstacleCheckDistance : 0.75f;
    }

    /// <summary>
    /// 접지/착지 지면 감지 레이 길이를 반환합니다.
    /// </summary>
    private float GetLocomotionGroundCheckDistance()
    {
        return _archetype != null ? _archetype.LocomotionGroundCheckDistance : 1.25f;
    }

    /// <summary>
    /// 착지 후보 재탐색 쿨다운 시간을 반환합니다.
    /// </summary>
    private float GetLandingRequeryCooldown()
    {
        return _archetype != null ? _archetype.LandingRequeryCooldown : 0.3f;
    }

    /// <summary>
    /// 감지 반경 설정 값을 반환합니다.
    /// </summary>
    private float GetDetectRange()
    {
        return _archetype != null ? _archetype.DetectRange : 6f;
    }

    /// <summary>
    /// 타겟 상실 반경 설정 값을 반환합니다.
    /// </summary>
    private float GetLoseRange()
    {
        return _archetype != null ? _archetype.LoseRange : 8f;
    }

    /// <summary>
    /// 공격 반경 설정 값을 반환합니다.
    /// </summary>
    private float GetAttackRange()
    {
        return _archetype != null ? _archetype.AttackRange : 1.5f;
    }

    /// <summary>
    /// 공격 쿨다운 설정 값을 반환합니다.
    /// </summary>
    private float GetAttackCooldown()
    {
        return _archetype != null ? _archetype.AttackCooldown : 1.2f;
    }

    /// <summary>
    /// 공격 후딜 설정 값을 반환합니다.
    /// </summary>
    private float GetRecoverDuration()
    {
        return _archetype != null ? _archetype.RecoverDuration : 0.25f;
    }

    /// <summary>
    /// 피격 경직 설정 값을 반환합니다.
    /// </summary>
    private float GetHitStunDuration()
    {
        return _archetype != null ? _archetype.HitStunDuration : 0.15f;
    }

    /// <summary>
    /// 순찰 반경 설정 값을 반환합니다.
    /// </summary>
    private float GetPatrolRadius()
    {
        return _archetype != null ? _archetype.PatrolRadius : 2.5f;
    }

    /// <summary>
    /// 순찰 대기 시간 설정 값을 반환합니다.
    /// </summary>
    private float GetPatrolWaitTime()
    {
        return _archetype != null ? _archetype.PatrolWaitTime : 1f;
    }

    /// <summary>
    /// 이동 도착 판정 거리 값을 반환합니다.
    /// </summary>
    private float GetStoppingDistance()
    {
        return _archetype != null ? _archetype.StoppingDistance : 0.1f;
    }

    /// <summary>
    /// 씬 뷰에서 감지/상실/공격/순찰 반경을 시각화합니다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        float detectRange = GetDetectRange(); // 감지 반경 시각화에 사용할 반경 값입니다.
        float loseRange = GetLoseRange(); // 상실 반경 시각화에 사용할 반경 값입니다.
        float attackRange = GetAttackRange(); // 공격 반경 시각화에 사용할 반경 값입니다.
        float patrolRadius = GetPatrolRadius(); // 순찰 반경 시각화에 사용할 반경 값입니다.

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, loseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Vector3 patrolCenter = Application.isPlaying ? (Vector3)_spawnPosition : transform.position; // 순찰 반경 중심 시각화에 사용할 좌표입니다.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(patrolCenter, patrolRadius);
    }
}
