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

    [Header("Advanced Extensions")]
    [Tooltip("IEnemyDecisionPolicy 구현체를 담는 전술 정책 컴포넌트 참조입니다.")]
    [SerializeField] private MonoBehaviour _decisionPolicyBehaviour; // IEnemyDecisionPolicy 구현체를 담는 전술 정책 컴포넌트 참조입니다.
    [Tooltip("IEnemySkillExecutor 구현체를 담는 스킬 실행기 컴포넌트 참조입니다.")]
    [SerializeField] private MonoBehaviour _skillExecutorBehaviour; // IEnemySkillExecutor 구현체를 담는 스킬 실행기 컴포넌트 참조입니다.
    [Tooltip("IEnemyPatternRunner 구현체를 담는 보스 패턴 러너 컴포넌트 참조입니다.")]
    [SerializeField] private MonoBehaviour _patternRunnerBehaviour; // IEnemyPatternRunner 구현체를 담는 보스 패턴 러너 컴포넌트 참조입니다.
    [Tooltip("IEnemyGimmickModule 구현체를 담는 고급 기믹 모듈 배열입니다.")]
    [SerializeField] private MonoBehaviour[] _gimmickModuleBehaviours = new MonoBehaviour[0]; // IEnemyGimmickModule 구현체를 담는 고급 기믹 모듈 배열입니다.

    [Header("Debug")]
    [Tooltip("현재 Enemy 상태 디버그 표시 값입니다.")]
    [SerializeField] private E_EnemyState _state = E_EnemyState.Spawn; // 현재 Enemy 상태 디버그 표시 값입니다.

    private IEnemyTargetProvider _targetProvider; // 런타임 타겟 획득에 사용할 TargetProvider 인터페이스 참조입니다.
    private IEnemyDecisionPolicy _decisionPolicy; // 런타임 전술 결정 오버라이드에 사용할 정책 인터페이스 참조입니다.
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

    /// <summary>
    /// 초기 참조를 보정하고 스폰 기준 좌표를 기록합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveReferences();
        ResolveExtensionModules();

        _spawnPosition = transform.position;
        _patrolDestination = _spawnPosition;

        if (_movementDriver != null)
        {
            _movementDriver.ApplyArchetype(_archetype);
        }
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
        RequestActionSafe(GetSpawnAction());
        TryResolveTarget();
    }

    /// <summary>
    /// 매 프레임 상태 전환 조건을 평가하고 이동/액션을 갱신합니다.
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

        if (IsDead())
        {
            EnterDeadState();
            return;
        }

        if (ShouldPatternOverride(context))
        {
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

            float distanceToTarget = context.DistanceToTarget;
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
        _movementDriver.SetTargetPosition(_target.position);
    }

    /// <summary>
    /// 후퇴 상태로 전환하고 지정된 좌표를 향해 이동합니다.
    /// </summary>
    private void EnterRetreatState(Vector2 retreatTargetPosition)
    {
        _state = E_EnemyState.Chase;
        RequestActionSafe(GetMoveAction());
        _movementDriver.SetTargetPosition(retreatTargetPosition);
    }

    /// <summary>
    /// 공격 상태로 전환하고 조건을 만족하면 공격 액션 또는 확장 스킬 실행을 시도한 뒤 Recover 윈도우를 시작합니다.
    /// </summary>
    private void EnterAttackState(in EnemyBrainContext context)
    {
        _state = E_EnemyState.Attack;
        _movementDriver.ClearTargetPosition();

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
        _movementDriver.ClearTargetPosition();
        RequestActionSafe(GetRecoverAction());
    }

    /// <summary>
    /// 피격 경직 상태를 유지하며 이동을 정지합니다.
    /// </summary>
    private void EnterHitStunState()
    {
        _state = E_EnemyState.HitStun;
        ClearRecoverPendingFromAttack();
        _movementDriver.ClearTargetPosition();
        RequestActionSafe(GetHitStunAction());
    }

    /// <summary>
    /// 스폰 위치로 복귀 이동하는 상태를 처리합니다.
    /// </summary>
    private void EnterReturnToSpawnState()
    {
        _state = E_EnemyState.Chase;
        RequestActionSafe(GetMoveAction());
        _movementDriver.SetTargetPosition(_spawnPosition);
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
            _movementDriver.ClearTargetPosition();
            return;
        }

        float distanceToPatrolDestination = Vector2.Distance(transform.position, _patrolDestination); // 현재 위치와 순찰 목적지 사이 거리 값입니다.
        if (distanceToPatrolDestination <= GetStoppingDistance())
        {
            _patrolDestination = PickPatrolDestination();
            _nextPatrolPickTime = Time.time + GetPatrolWaitTime();
        }

        _movementDriver.SetTargetPosition(_patrolDestination);
    }

    /// <summary>
    /// 대기 상태로 전환하고 이동을 정지합니다.
    /// </summary>
    private void EnterIdleState()
    {
        _state = E_EnemyState.Idle;
        _movementDriver.ClearTargetPosition();
        RequestActionSafe(GetIdleAction());
    }

    /// <summary>
    /// 사망 상태로 전환하고 이동을 정지한 뒤 사망 액션을 1회 요청합니다.
    /// </summary>
    private void EnterDeadState()
    {
        _state = E_EnemyState.Dead;
        ClearRecoverPendingFromAttack();
        _movementDriver.ClearTargetPosition();

        if (_didRequestDeadAction)
        {
            return;
        }

        _didRequestDeadAction = RequestActionSafe(GetDeadAction());
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
    /// 타겟 제공자 또는 태그 fallback으로 추적 대상을 보정합니다.
    /// </summary>
    private void TryResolveTarget()
    {
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

        GameObject targetObject = GameObject.FindGameObjectWithTag(_fallbackTargetTag); // fallback 타겟 보정을 위해 탐색한 오브젝트 참조입니다.
        _target = targetObject != null ? targetObject.transform : null;
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
    /// 현재 프레임 문맥 스냅샷을 구성해 반환합니다.
    /// </summary>
    private EnemyBrainContext BuildContext()
    {
        float distance = _target != null ? Vector2.Distance(transform.position, _target.position) : float.PositiveInfinity; // 문맥 전달에 사용할 현재 타겟 거리 값입니다.
        return new EnemyBrainContext(transform, _target, distance, _spawnPosition, _archetype);
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
    /// 타겟 제공자/정책/스킬/패턴/기믹 모듈 참조를 인터페이스로 해석해 캐시합니다.
    /// </summary>
    private void ResolveExtensionModules()
    {
        _targetProvider = _targetProviderBehaviour as IEnemyTargetProvider;
        _decisionPolicy = _decisionPolicyBehaviour as IEnemyDecisionPolicy;
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
