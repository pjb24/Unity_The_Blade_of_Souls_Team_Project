using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy AI 상태 관리/우선순위 전이/홈 복귀 정책을 제어하는 중앙 브레인입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAIController : NetworkBehaviour
{
    [Header("Actor Root")]
    [Tooltip("AI가 실제 위치 기준으로 사용할 루트 Transform입니다. Child 분리 프리팹에서는 Parent 루트를 연결합니다.")]
    [SerializeField] private Transform _actorRoot; // AI 거리 판정/홈 포지션/Gizmo 기준으로 사용할 루트 Transform입니다.

    [Header("AI Components")]
    [Tooltip("NavMesh 경로 계산 + Rigidbody2D 이동 적용을 담당하는 이동 컨트롤러입니다.")]
    [SerializeField] private EnemyMovementController _movementController; // 이동 담당 컨트롤러 참조입니다.
    [Tooltip("LayerMask 기반 타겟 탐색/캐싱을 담당하는 타겟 탐지 컨트롤러입니다.")]
    [SerializeField] private EnemyTargetDetector _targetDetector; // 타겟 탐색 담당 컨트롤러 참조입니다.
    [Tooltip("공격 쿨다운/요청/종료를 담당하는 공격 컨트롤러입니다.")]
    [SerializeField] private EnemyAttackController _attackController; // 공격 담당 컨트롤러 참조입니다.
    [Tooltip("피격 상태 지속/종료 정책을 담당하는 피격 컨트롤러입니다.")]
    [SerializeField] private EnemyHitReactionController _hitReactionController; // 피격 상태 정책 컨트롤러 참조입니다.
    [Tooltip("AI 상태 신호를 애니메이션 계층에 전달하는 브리지입니다.")]
    [SerializeField] private EnemyAnimationBridge _animationBridge; // 애니메이션 브리지 참조입니다.
    [Tooltip("HealthComponent 이벤트를 AI 사망/피격 신호로 변환하는 어댑터입니다.")]
    [SerializeField] private EnemyHealthAdapter _healthAdapter; // 체력 어댑터 참조입니다.
    [Tooltip("자폭 종료 시 자기 피해를 적용할 HealthComponent 참조입니다. 비어 있으면 자식/부모 계층에서 자동 탐색합니다.")]
    [SerializeField] private HealthComponent _selfHealthComponent; // 자폭 종료 시 자기 최대 체력 피해를 적용할 HealthComponent 참조입니다.
    [Tooltip("사망 1회 진입/VFX/제거 시퀀스를 담당하는 사망 컨트롤러입니다.")]
    [SerializeField] private EnemyAIDeathController _deathController; // 사망 처리 컨트롤러 참조입니다.
    [Tooltip("NGO 세션에서 Enemy AI 서버 권한을 판정할 NetworkObject 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private NetworkObject _networkObject; // 멀티플레이 서버 권한 판정에 사용할 NetworkObject 참조입니다.

    [Header("Distance Settings")]
    [Tooltip("플레이어를 최초 인식하는 거리입니다.")]
    [SerializeField] private float _detectionRange = 8f; // 타겟 인식 거리입니다.
    [Tooltip("추적을 지속할 최대 거리입니다.")]
    [SerializeField] private float _chaseRange = 12f; // 추적 가능 거리입니다.
    [Tooltip("공격 상태 진입 거리(히스테리시스 진입)입니다.")]
    [SerializeField] private float _attackEnterRange = 1.2f; // 공격 진입 거리입니다.
    [Tooltip("공격 상태 이탈 거리(히스테리시스 이탈)입니다.")]
    [SerializeField] private float _attackExitRange = 1.8f; // 공격 이탈 거리입니다.
    [Tooltip("Return 상태에서 Home 도착 완료로 간주하는 거리입니다.")]
    [SerializeField] private float _returnStopDistance = 0.2f; // 복귀 완료 거리 임계값입니다.
    [Tooltip("복귀 도착 속도 임계값입니다.")]
    [SerializeField] private float _arrivalVelocityEpsilon = 0.05f; // 복귀 도착 속도 임계값입니다.

    [Header("Intervals / Timers")]
    [Tooltip("추적/복귀 상태에서 목적지 재계산 간격(초)입니다.")]
    [SerializeField] private float _repathInterval = 0.25f; // 경로 재계산 주기입니다.
    [Tooltip("타겟 탐색 주기(초)입니다.")]
    [SerializeField] private float _targetSearchInterval = 0.3f; // 타겟 탐색 주기입니다.
    [Tooltip("공격 쿨다운(초)입니다. EnemyAttackController와 동기화 검사용입니다.")]
    [SerializeField] private float _attackCooldown = 1.2f; // 디자이너 확인용 공격 쿨다운 값입니다.
    [Tooltip("상태 전이 최소 유지 시간(초)입니다.")]
    [SerializeField] private float _stateLockTime = 0.2f; // 상태 전이 락 시간입니다.
    [Header("Home Position")]
    [Tooltip("오브젝트 풀 재스폰 시 Home Position을 다시 저장할지 여부입니다.")]
    [SerializeField] private bool _resetHomePositionOnRespawn = true; // 재활성화 시 Home 재설정 여부입니다.
    [Tooltip("Enable 직후 Home Position 저장 전 NavMesh 준비를 기다리는 최대 시간(초)입니다.")]
    [SerializeField] private float _homeCaptureTimeout = 1.5f; // Home 저장 준비 대기 시간입니다.

    [Header("Movement Sync")]
    [Tooltip("Enemy 루트 위치를 NGO NetworkTransform으로 동기화할 대상 참조입니다. 비어 있으면 자동 탐색하거나 런타임에 추가합니다.")]
    [SerializeField] private NetworkTransform _networkTransform; // Enemy 이동 동기화를 담당하는 NetworkTransform 참조입니다.
    [Tooltip("NetworkTransform이 비어 있을 때 런타임에 자동 추가할지 여부입니다.")]
    [SerializeField] private bool _autoAddNetworkTransformWhenMissing = true; // NetworkTransform 자동 보정 여부입니다.
    [Tooltip("NetworkTransform 기반 이동 동기화를 사용할지 여부입니다. 끄면 기존 로컬 이동만 수행합니다.")]
    [SerializeField] private bool _useNetworkTransformMovementSync = true; // Enemy 이동을 NetworkTransform으로 동기화할지 여부입니다.

    [Header("Debug")]
    [Tooltip("상태 전이/경고를 상세 로그로 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseStateLog = true; // 상세 상태 로그 출력 여부입니다.
    [Tooltip("네트워크 권한이 없는 인스턴스가 AI 판단을 건너뛸 때 경고를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenAuthorityUnavailable = true; // 네트워크 권한 누락 경고 출력 여부입니다.

    private readonly Dictionary<EnemyAIStateId, IEnemyAIState> _states = new Dictionary<EnemyAIStateId, IEnemyAIState>(); // 상태 객체 맵입니다.

    private EnemyAIStateId _currentStateId = EnemyAIStateId.Idle; // 현재 상태 식별자입니다.
    private IEnemyAIState _currentState; // 현재 상태 객체입니다.
    private float _stateEnteredAt; // 현재 상태 진입 시각입니다.
    private float _nextRepathAt; // 다음 경로 갱신 시각입니다.

    private Vector2 _homePosition; // 초기 Home Position입니다.
    private bool _homeCaptured; // Home Position 저장 성공 여부입니다.
    private Coroutine _homeCaptureRoutine; // Home Position 저장 루틴 핸들입니다.

    private bool _isDead; // 사망 상태 여부입니다.
    private bool _isRemovingOrRemoved; // 제거 진행/완료 상태 여부입니다.
    private bool _hasLoggedAuthorityWarning; // 관찰자 인스턴스 권한 경고의 중복 출력을 막는 플래그입니다.

    /// <summary>
    /// 현재 상태 식별자를 반환합니다.
    /// </summary>
    public EnemyAIStateId CurrentStateId => _currentStateId;

    /// <summary>
    /// 현재 타겟을 반환합니다.
    /// </summary>
    public Transform CurrentTarget => _targetDetector != null ? _targetDetector.CurrentTarget : null;

    /// <summary>
    /// Home Position을 반환합니다.
    /// </summary>
    public Vector2 HomePosition => _homePosition;

    /// <summary>
    /// Home Position 저장 여부를 반환합니다.
    /// </summary>
    public bool HasHomePosition => _homeCaptured;

    /// <summary>
    /// AI가 거리 판정에 사용할 현재 루트 위치를 반환합니다.
    /// </summary>
    public Vector2 ActorWorldPosition => _actorRoot != null ? (Vector2)_actorRoot.position : (Vector2)transform.position;

    private void Awake()
    {
        ResolveActorRoot();
        ResolveDependencies();
        ValidateRanges();
        _attackController?.SetAttackCooldown(_attackCooldown);
        BuildStates();
    }

    /// <summary>
    /// 에디터 값 변경 시 설정 제약을 즉시 검증합니다.
    /// </summary>
    private void OnValidate()
    {
        ResolveActorRoot();
        ValidateRanges();
        _attackController?.SetAttackCooldown(_attackCooldown);
    }

    /// <summary>
    /// 활성화 시 이벤트 구독과 런타임 초기화를 수행합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        EnsureNetworkTransformReady();
    }

    public override void OnNetworkDespawn()
    {
    }

    private void OnEnable()
    {
        SubscribeHealthSignals();
        SubscribeAttackSignals();
        ResetRuntimeStateOnEnable();
    }

    /// <summary>
    /// 비활성화 시 이벤트 해제와 런타임 정리를 수행합니다.
    /// </summary>
    private void OnDisable()
    {
        if (!_isDead)
        {
            Debug.LogWarning($"[EnemyAIController] {name} disabled while alive. Runtime state will be reset.");
        }

        UnsubscribeHealthSignals();
        UnsubscribeAttackSignals();
        TearDownRuntime();
    }

    /// <summary>
    /// 매 프레임 상태 업데이트와 전이 평가를 수행합니다.
    /// </summary>
    private void Update()
    {
        if (!CanExecuteAiAuthority())
        {
            return;
        }

        float now = Time.time;

        if (_targetDetector != null)
        {
            _targetDetector.TickSearch(now, ActorWorldPosition, _detectionRange, _targetSearchInterval);
        }

        _attackController?.Tick(now);
        _hitReactionController?.Tick(now);

        _currentState?.Tick(Time.deltaTime);

        EnemyAIStateId nextState = EvaluateNextState();
        TryChangeState(nextState, false);
    }

    /// <summary>
    /// NetworkTransform 사용 조건을 검사하고 필요 시 런타임에 보정합니다.
    /// </summary>
    private void EnsureNetworkTransformReady()
    {
        if (!_useNetworkTransformMovementSync)
        {
            return;
        }

        if (_networkTransform != null)
        {
            return;
        }

        _networkTransform = GetComponent<NetworkTransform>();
        if (_networkTransform != null)
        {
            return;
        }

        if (!_autoAddNetworkTransformWhenMissing)
        {
            Debug.LogWarning($"[EnemyAIController] NetworkTransform is missing on {name}. Client movement will not stay in sync until one is assigned.", this);
            return;
        }

        _networkTransform = gameObject.AddComponent<NetworkTransform>();
    }

    private void ResolveDependencies()
    {
        if (_movementController == null)
        {
            _movementController = GetComponent<EnemyMovementController>();
        }

        if (_targetDetector == null)
        {
            _targetDetector = GetComponent<EnemyTargetDetector>();
        }

        if (_attackController == null)
        {
            _attackController = GetComponent<EnemyAttackController>();
        }

        if (_hitReactionController == null)
        {
            _hitReactionController = GetComponent<EnemyHitReactionController>();
        }

        if (_animationBridge == null)
        {
            _animationBridge = GetComponent<EnemyAnimationBridge>();
        }

        if (_healthAdapter == null)
        {
            _healthAdapter = GetComponent<EnemyHealthAdapter>();
        }

        if (_selfHealthComponent == null)
        {
            _selfHealthComponent = GetComponent<HealthComponent>();
        }

        if (_selfHealthComponent == null)
        {
            _selfHealthComponent = GetComponentInChildren<HealthComponent>(true);
        }

        if (_selfHealthComponent == null)
        {
            _selfHealthComponent = GetComponentInParent<HealthComponent>();
        }

        if (_deathController == null)
        {
            _deathController = GetComponent<EnemyAIDeathController>();
        }

        if (_networkObject == null)
        {
            _networkObject = GetComponent<NetworkObject>();
        }

        if (_networkTransform == null)
        {
            _networkTransform = GetComponent<NetworkTransform>();
        }

        if (_movementController == null)
        {
            Debug.LogWarning($"[EnemyAIController] Missing EnemyMovementController on {name}.");
        }

        if (_targetDetector == null)
        {
            Debug.LogWarning($"[EnemyAIController] Missing EnemyTargetDetector on {name}.");
        }

        if (_attackController == null)
        {
            Debug.LogWarning($"[EnemyAIController] Missing EnemyAttackController on {name}.");
        }

        if (_selfHealthComponent == null)
        {
            Debug.LogWarning($"[EnemyAIController] Missing self HealthComponent on {name}. Self-destruct damage cannot route through the shared health pipeline.");
        }

        if (_hitReactionController == null)
        {
            Debug.LogWarning($"[EnemyAIController] Missing EnemyHitReactionController on {name}. HitReaction state will not react to damage.");
        }

        EnsureNetworkTransformReady();
    }

    /// <summary>
    /// 현재 인스턴스가 Enemy AI 의사결정을 수행할 서버 권한을 가졌는지 판정합니다.
    /// </summary>
    private bool CanExecuteAiAuthority()
    {
        bool canExecute = EnemyNetworkAuthorityUtility.ShouldRunServerAuthoritativeLogic(_networkObject);
        if (!canExecute && _warnWhenAuthorityUnavailable && _verboseStateLog && !_hasLoggedAuthorityWarning)
        {
            Debug.LogWarning($"[EnemyAIController] AI authority unavailable on observer instance. object={name}", this);
            _hasLoggedAuthorityWarning = true;
        }

        if (canExecute)
        {
            _hasLoggedAuthorityWarning = false;
        }

        return canExecute;
    }

    /// <summary>
    /// 현재 인스턴스가 서버 확정 시각 상태를 NGO RPC로 복제해야 하는지 판정합니다.
    /// </summary>
    private bool ShouldReplicateVisuals()
    {
        return EnemyNetworkAuthorityUtility.ShouldReplicateFromServer(_networkObject);
    }

    /// <summary>
    /// Actor Root 참조를 보정합니다.
    /// </summary>
    private void ResolveActorRoot()
    {
        if (_actorRoot == null)
        {
            _actorRoot = transform;
        }
    }

    /// <summary>
    /// 상태 객체를 생성하고 상태 맵에 등록합니다.
    /// </summary>
    private void BuildStates()
    {
        _states.Clear();
        _states[EnemyAIStateId.Idle] = new IdleState(this);
        _states[EnemyAIStateId.Chase] = new ChaseState(this);
        _states[EnemyAIStateId.Attack] = new AttackState(this);
        _states[EnemyAIStateId.Return] = new ReturnState(this);
        _states[EnemyAIStateId.Death] = new DeathState(this);
        _states[EnemyAIStateId.HitReaction] = new HitReactionState(this);
        _states[EnemyAIStateId.Patrol] = new PatrolState(this);
    }

    /// <summary>
    /// 거리/타이머 설정값 제약을 검사합니다.
    /// </summary>
    private void ValidateRanges()
    {
        if (_attackEnterRange > _attackExitRange)
        {
            Debug.LogWarning($"[EnemyAIController] Invalid range: _attackEnterRange({_attackEnterRange}) > _attackExitRange({_attackExitRange}) on {name}.");
        }

        if (_attackExitRange > _detectionRange)
        {
            Debug.LogWarning($"[EnemyAIController] Invalid range: _attackExitRange({_attackExitRange}) > _detectionRange({_detectionRange}) on {name}.");
        }

        if (_detectionRange > _chaseRange)
        {
            Debug.LogWarning($"[EnemyAIController] Invalid range: _detectionRange({_detectionRange}) > _chaseRange({_chaseRange}) on {name}.");
        }

        if (_targetSearchInterval < 0.05f)
        {
            Debug.LogWarning($"[EnemyAIController] _targetSearchInterval({_targetSearchInterval}) is too small on {name}. Recommended: 0.2~0.5.");
        }

        if (_repathInterval < 0.05f)
        {
            Debug.LogWarning($"[EnemyAIController] _repathInterval({_repathInterval}) is too small on {name}. Recommended: 0.2~0.3.");
        }
    }

    /// <summary>
    /// 체력 어댑터 이벤트를 구독합니다.
    /// </summary>
    private void SubscribeHealthSignals()
    {
        if (_healthAdapter == null)
        {
            return;
        }

        _healthAdapter.Died += HandleDied;
        _healthAdapter.Damaged += HandleDamaged;
        _healthAdapter.Revived += HandleRevived;
    }

    /// <summary>
    /// 체력 어댑터 이벤트를 해제합니다.
    /// </summary>
    private void UnsubscribeHealthSignals()
    {
        if (_healthAdapter == null)
        {
            return;
        }

        _healthAdapter.Died -= HandleDied;
        _healthAdapter.Damaged -= HandleDamaged;
        _healthAdapter.Revived -= HandleRevived;
    }

    /// <summary>
    /// 공격 컨트롤러 이벤트를 구독합니다.
    /// </summary>
    private void SubscribeAttackSignals()
    {
        if (_attackController == null)
        {
            return;
        }

        _attackController.AttackSequenceCompleted += HandleAttackSequenceCompleted;
    }

    /// <summary>
    /// 공격 컨트롤러 이벤트를 해제합니다.
    /// </summary>
    private void UnsubscribeAttackSignals()
    {
        if (_attackController == null)
        {
            return;
        }

        _attackController.AttackSequenceCompleted -= HandleAttackSequenceCompleted;
    }

    /// <summary>
    /// 활성화 시 런타임 상태를 초기화하고 HomePosition 저장을 준비합니다.
    /// </summary>
    private void ResetRuntimeStateOnEnable()
    {
        if (_resetHomePositionOnRespawn)
        {
            _homeCaptured = false;
        }

        _isDead = _healthAdapter != null && !_healthAdapter.IsAlive;
        _isRemovingOrRemoved = false;
        _hasLoggedAuthorityWarning = false;
        _nextRepathAt = 0f;

        _targetDetector?.ClearTarget();
        _attackController?.ResetRuntime();
        _hitReactionController?.ResetRuntime();
        _deathController?.ResetRuntime();
        _healthAdapter?.SetCanBeHit(true);
        _movementController?.ForceSyncNow();

        if (_homeCaptureRoutine != null)
        {
            StopCoroutine(_homeCaptureRoutine);
        }

        _homeCaptureRoutine = StartCoroutine(CaptureHomePositionRoutine());

        if (_isDead)
        {
            TryChangeState(EnemyAIStateId.Death, true);
        }
        else
        {
            TryChangeState(EnemyAIStateId.Idle, true);
        }
    }

    /// <summary>
    /// 비활성화 시 런타임 상태를 정리합니다.
    /// </summary>
    private void TearDownRuntime()
    {
        if (_homeCaptureRoutine != null)
        {
            StopCoroutine(_homeCaptureRoutine);
            _homeCaptureRoutine = null;
        }

        _attackController?.ForceStopAttack();
        _attackController?.SetTerminalState(true);
        _hitReactionController?.ForceComplete();
        _movementController?.StopMovement();
        _targetDetector?.ClearTarget();
    }

    /// <summary>
    /// Home Position 저장 시점을 보장하기 위해 NavMesh 준비를 대기합니다.
    /// </summary>
    private IEnumerator CaptureHomePositionRoutine()
    {
        float startedAt = Time.time;
        yield return null;

        bool captured = false;
        while (Time.time - startedAt <= _homeCaptureTimeout)
        {
            if (!isActiveAndEnabled)
            {
                yield break;
            }

            if (NavMesh.SamplePosition(ActorWorldPosition, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
            {
                _homePosition = hit.position;
                _homeCaptured = true;
                captured = true;

                if (_verboseStateLog)
                {
                    Debug.Log($"[EnemyAIController] Home position captured for {name}: {_homePosition}");
                }

                break;
            }

            yield return null;
        }

        if (!captured)
        {
            Debug.LogWarning($"[EnemyAIController] Failed to capture Home Position on {name}. NavMesh may be missing or not baked.");
        }
    }

    /// <summary>
    /// 우선순위 규칙에 따라 다음 상태를 평가합니다.
    /// </summary>
    private EnemyAIStateId EvaluateNextState()
    {
        if (_isDead || _isRemovingOrRemoved)
        {
            return EnemyAIStateId.Death;
        }

        if (_hitReactionController != null && _hitReactionController.IsInHitReaction)
        {
            return EnemyAIStateId.HitReaction;
        }

        Transform target = CurrentTarget;
        bool hasValidTarget = _targetDetector != null && _targetDetector.HasValidTarget();

        // 1) 타겟 무효/사망
        if (!hasValidTarget)
        {
            if (NeedsReturnHome())
            {
                return EnemyAIStateId.Return;
            }

            return EnemyAIStateId.Idle;
        }

        float distanceToTarget = Vector2.Distance(ActorWorldPosition, target.position);

        // 2) 공격 가능 여부 + 공격 거리 판정
        bool isWithinAttackEnter = distanceToTarget <= _attackEnterRange;
        bool isWithinAttackExit = distanceToTarget <= _attackExitRange;
        bool canAttackNow = _attackController != null && _attackController.CanStartAttack(Time.time);

        if (_currentStateId == EnemyAIStateId.Attack)
        {
            if (_attackController != null && _attackController.IsAttacking)
            {
                return EnemyAIStateId.Attack;
            }

            if (isWithinAttackExit && canAttackNow)
            {
                return EnemyAIStateId.Attack;
            }
        }
        else if (isWithinAttackEnter && canAttackNow)
        {
            return EnemyAIStateId.Attack;
        }

        // 3) 추적 가능 여부 + 추적 범위 판정
        bool canChase = distanceToTarget <= _chaseRange;
        if (canChase)
        {
            return EnemyAIStateId.Chase;
        }

        // 4) 복귀 필요 여부
        if (NeedsReturnHome())
        {
            return EnemyAIStateId.Return;
        }

        // 5) Idle 유지
        return EnemyAIStateId.Idle;
    }

    /// <summary>
    /// 상태 전이 락 조건을 검사합니다.
    /// </summary>
    private bool IsStateLockActive()
    {
        return Time.time - _stateEnteredAt < _stateLockTime;
    }

    /// <summary>
    /// 지정 상태로 전이를 시도합니다.
    /// </summary>
    private bool TryChangeState(EnemyAIStateId nextStateId, bool force)
    {
        if (!force && nextStateId == _currentStateId)
        {
            return false;
        }

        if (!force && IsStateLockActive())
        {
            return false;
        }

        if (!_states.TryGetValue(nextStateId, out IEnemyAIState nextState))
        {
            Debug.LogWarning($"[EnemyAIController] Missing state implementation for {nextStateId} on {name}.");
            return false;
        }

        if (!force && !nextState.CanEnter())
        {
            return false;
        }

        _currentState?.Exit();
        EnemyAIStateId previous = _currentStateId;

        _currentState = nextState;
        _currentStateId = nextStateId;
        _stateEnteredAt = Time.time;
        _currentState.Enter();

        if (_verboseStateLog)
        {
            Debug.Log($"[EnemyAIController] State changed: {previous} -> {nextStateId} ({name})");
        }

        return true;
    }

    /// <summary>
    /// 공격 상태 진입 시 공격 시작을 요청합니다.
    /// </summary>
    public void RequestAttackStart()
    {
        if (_attackController == null)
        {
            Debug.LogWarning($"[EnemyAIController] Cannot request attack because EnemyAttackController is missing on {name}.");
            return;
        }

        if (_isDead || _isRemovingOrRemoved || (_healthAdapter != null && !_healthAdapter.IsAlive))
        {
            TryEnterDeathState();
            return;
        }

        bool started = _attackController.TryStartAttack(Time.time);
        if (!started)
        {
            Debug.LogWarning($"[EnemyAIController] Attack state entered but attack start failed on {name}. Cooldown or runtime state check failed.");
            return;
        }

        _healthAdapter?.SetCanBeHit(false);
    }

    /// <summary>
    /// 필요 시 주기적으로 타겟 위치를 목적지로 갱신합니다.
    /// </summary>
    public void RepathToCurrentTargetIfNeeded()
    {
        if (_movementController == null)
        {
            return;
        }

        Transform target = CurrentTarget;
        if (target == null)
        {
            Debug.LogWarning($"[EnemyAIController] RepathToCurrentTargetIfNeeded called without target on {name}.");
            return;
        }

        if (Time.time < _nextRepathAt)
        {
            return;
        }

        _nextRepathAt = Time.time + _repathInterval;
        _movementController.TrySetDestination(target.position);
    }

    /// <summary>
    /// 필요 시 Home Position으로 목적지를 갱신합니다.
    /// </summary>
    public void RepathToHomeIfNeeded()
    {
        if (_movementController == null)
        {
            return;
        }

        if (!_homeCaptured)
        {
            Debug.LogWarning($"[EnemyAIController] Return requested before Home Position capture on {name}.");
            return;
        }

        if (Time.time < _nextRepathAt)
        {
            return;
        }

        _nextRepathAt = Time.time + _repathInterval;
        _movementController.TrySetDestination(_homePosition);
    }

    /// <summary>
    /// 복귀 필요 여부를 평가합니다.
    /// </summary>
    public bool NeedsReturnHome()
    {
        if (!_homeCaptured)
        {
            return false;
        }

        float distance = Vector2.Distance(ActorWorldPosition, _homePosition);
        return distance > _returnStopDistance;
    }

    /// <summary>
    /// 복귀 완료 조건을 평가합니다.
    /// </summary>
    public bool IsReturnComplete()
    {
        if (_movementController == null)
        {
            return false;
        }

        return _movementController.IsArrived(_returnStopDistance, _arrivalVelocityEpsilon);
    }

    /// <summary>
    /// 이동 활성 상태를 변경합니다.
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        _movementController?.SetMovementEnabled(enabled);
    }

    /// <summary>
    /// 이동을 즉시 중지합니다.
    /// </summary>
    public void StopMovementNow()
    {
        _movementController?.StopMovement();
    }

    /// <summary>
    /// 애니메이션 브리지에 상태 의도를 전달합니다.
    /// </summary>
    public void PushAnimationIntent(EnemyAIStateId stateId, bool isMoving)
    {
        _animationBridge?.ApplyStateIntent(stateId, isMoving);

        if (ShouldReplicateVisuals())
        {
            ApplyStateIntentRpc((int)stateId, isMoving);
        }
    }

    /// <summary>
    /// 공격 시작 시각 의도를 로컬에 적용하고 필요 시 관찰자에게 복제합니다.
    /// </summary>
    public void TriggerAttackVisual()
    {
        _animationBridge?.TriggerAttackIntent();

        if (ShouldReplicateVisuals())
        {
            TriggerAttackVisualRpc();
        }
    }

    /// <summary>
    /// 피격 시각 의도를 로컬에 적용하고 필요 시 관찰자에게 복제합니다.
    /// </summary>
    public void TriggerHitVisual()
    {
        _animationBridge?.TriggerHitIntent();

        if (ShouldReplicateVisuals())
        {
            TriggerHitVisualRpc();
        }
    }

    /// <summary>
    /// 사망 시각 의도를 로컬에 적용하고 필요 시 관찰자에게 복제합니다.
    /// </summary>
    public void TriggerDeathVisual()
    {
        _animationBridge?.TriggerDeathIntent();

        if (ShouldReplicateVisuals())
        {
            TriggerDeathVisualRpc();
        }

    }

    /// <summary>
    /// 서버가 확정한 상태 시각값을 관찰자 인스턴스 Animator에 반영합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void ApplyStateIntentRpc(int stateIdValue, bool isMoving)
    {
        _animationBridge?.ApplyStateIntent((EnemyAIStateId)stateIdValue, isMoving);
    }

    /// <summary>
    /// 서버가 확정한 공격 시작 트리거를 관찰자 인스턴스에 전달합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void TriggerAttackVisualRpc()
    {
        _animationBridge?.TriggerAttackIntent();
    }

    /// <summary>
    /// 서버가 확정한 피격 트리거를 관찰자 인스턴스에 전달합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void TriggerHitVisualRpc()
    {
        _animationBridge?.TriggerHitIntent();
    }

    /// <summary>
    /// 서버가 확정한 사망 트리거를 관찰자 인스턴스에 전달합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void TriggerDeathVisualRpc()
    {
        _animationBridge?.TriggerDeathIntent();
        _deathController?.TryEnterDeath();
    }

    /// <summary>
    /// 서버가 확정한 피격 사망 숨김 결과를 관찰자 인스턴스에 반영합니다.
    /// </summary>
    /// <summary>
    /// Death 상태 진입 처리입니다.
    /// </summary>
    private void HandleDied()
    {
        _isDead = true;
        _healthAdapter?.SetCanBeHit(false);
        _attackController?.SetTerminalState(true);
        _hitReactionController?.ForceComplete();
        TryEnterDeathState();
    }

    /// <summary>
    /// Damaged 이벤트 수신 처리입니다.
    /// </summary>
    private void HandleDamaged()
    {
        if (_isDead || _isRemovingOrRemoved || (_healthAdapter != null && !_healthAdapter.CanBeHit))
        {
            return;
        }

        _hitReactionController?.RequestHitReaction(Time.time);
        TryChangeState(EnemyAIStateId.HitReaction, true);
    }

    /// <summary>
    /// Revived 이벤트 수신 처리입니다.
    /// </summary>
    private void HandleRevived()
    {
        _isDead = false;
        _isRemovingOrRemoved = false;
        _attackController?.SetTerminalState(false);
        _healthAdapter?.SetCanBeHit(true);
        TryChangeState(EnemyAIStateId.Idle, true);
    }

    /// <summary>
    /// 공격 종료 시 자폭형 몬스터의 사망 진입을 트리거합니다.
    /// </summary>
    private void HandleAttackSequenceCompleted()
    {
        if (_isDead || _isRemovingOrRemoved)
        {
            return;
        }

        ApplySelfDestructDamage();
    }

    /// <summary>
    /// Death 상태 진입과 사망 시퀀스 시작을 단일 진입점으로 수행합니다.
    /// </summary>
    /// <summary>
    /// 자폭 공격 종료 시 자기 최대 체력만큼 피해를 적용해 일반 사망 경로로 진입시킵니다.
    /// </summary>
    private void ApplySelfDestructDamage()
    {
        if (_selfHealthComponent == null)
        {
            Debug.LogWarning($"[EnemyAIController] Missing self HealthComponent for self-destruct damage on {name}. Self-destruct cannot be resolved through HealthComponent.", this);
            return;
        }

        float selfDestructDamage = Mathf.Max(1f, _selfHealthComponent.GetMaxHealth());
        DamageContext selfDamageContext = new DamageContext(
            selfDestructDamage,
            gameObject,
            $"{name}_self_destruct",
            false,
            true,
            E_DamageType.True);

        DamageResult selfDamageResult = _selfHealthComponent.ApplyDamage(selfDamageContext);
        if (!selfDamageResult.DidCauseDeath && !_selfHealthComponent.IsDead)
        {
            Debug.LogWarning($"[EnemyAIController] Self-destruct damage did not kill {name}. Death state will remain driven by HealthComponent.", this);
        }
    }

    private void TryEnterDeathState()
    {
        if (_isRemovingOrRemoved)
        {
            return;
        }

        _isDead = true;
        _healthAdapter?.SetCanBeHit(false);
        _attackController?.SetTerminalState(true);

        if (_deathController != null)
        {
            _deathController.TryEnterDeath();
            _isRemovingOrRemoved = _deathController.IsRemovalInProgress || _deathController.IsRemoved;
        }

        TryChangeState(EnemyAIStateId.Death, true);
    }

    /// <summary>
    /// 피격 사망이 확정되면 서버와 관찰자 화면 모두에서 Enemy 루트를 즉시 비활성화합니다.
    /// </summary>
    /// <summary>
    /// 선택 시 AI 디버그 Gizmos를 그립니다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(ActorWorldPosition, _detectionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ActorWorldPosition, _chaseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(ActorWorldPosition, _attackEnterRange);
        Gizmos.DrawWireSphere(ActorWorldPosition, _attackExitRange);

        if (_homeCaptured)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_homePosition, 0.2f);
            Gizmos.DrawLine(ActorWorldPosition, _homePosition);
        }
    }

    /// <summary>
    /// Idle 상태 구현입니다.
    /// </summary>
    private sealed class IdleState : EnemyAIStateBase
    {
        /// <summary>
        /// Idle 상태 생성자입니다.
        /// </summary>
        public IdleState(EnemyAIController controller) : base(controller)
        {
        }

        /// <summary>
        /// 상태 식별자를 반환합니다.
        /// </summary>
        public override EnemyAIStateId StateId => EnemyAIStateId.Idle;

        /// <summary>
        /// Idle 진입 시 이동을 정지합니다.
        /// </summary>
        public override void Enter()
        {
            Controller.SetMovementEnabled(false);
            Controller.StopMovementNow();
            Controller.PushAnimationIntent(StateId, false);
        }
    }

    /// <summary>
    /// Chase 상태 구현입니다.
    /// </summary>
    private sealed class ChaseState : EnemyAIStateBase
    {
        /// <summary>
        /// Chase 상태 생성자입니다.
        /// </summary>
        public ChaseState(EnemyAIController controller) : base(controller)
        {
        }

        /// <summary>
        /// 상태 식별자를 반환합니다.
        /// </summary>
        public override EnemyAIStateId StateId => EnemyAIStateId.Chase;

        /// <summary>
        /// Chase 진입 시 이동을 활성화합니다.
        /// </summary>
        public override void Enter()
        {
            Controller.SetMovementEnabled(true);
            Controller.PushAnimationIntent(StateId, true);
            Controller.RepathToCurrentTargetIfNeeded();
        }

        /// <summary>
        /// Chase 유지 중 주기적으로 타겟 목적지를 갱신합니다.
        /// </summary>
        public override void Tick(float deltaTime)
        {
            Controller.RepathToCurrentTargetIfNeeded();
        }
    }

    /// <summary>
    /// Attack 상태 구현입니다.
    /// </summary>
    private sealed class AttackState : EnemyAIStateBase
    {
        /// <summary>
        /// Attack 상태 생성자입니다.
        /// </summary>
        public AttackState(EnemyAIController controller) : base(controller)
        {
        }

        /// <summary>
        /// 상태 식별자를 반환합니다.
        /// </summary>
        public override EnemyAIStateId StateId => EnemyAIStateId.Attack;

        /// <summary>
        /// Attack 진입 가능 여부를 검사합니다.
        /// </summary>
        public override bool CanEnter()
        {
            return Controller.CurrentTarget != null
                && !Controller._isDead
                && !Controller._isRemovingOrRemoved
                && (Controller._healthAdapter == null || Controller._healthAdapter.IsAlive);
        }

        /// <summary>
        /// Attack 진입 시 이동을 멈추고 공격 시작을 요청합니다.
        /// </summary>
        public override void Enter()
        {
            Controller.SetMovementEnabled(false);
            Controller.StopMovementNow();
            Controller.PushAnimationIntent(StateId, false);
            Controller.RequestAttackStart();
        }

        /// <summary>
        /// Attack 유지 중에는 별도 이동 없이 공격 종료를 대기합니다.
        /// </summary>
        public override void Tick(float deltaTime)
        {
        }
    }

    /// <summary>
    /// Return 상태 구현입니다.
    /// </summary>
    private sealed class ReturnState : EnemyAIStateBase
    {
        /// <summary>
        /// Return 상태 생성자입니다.
        /// </summary>
        public ReturnState(EnemyAIController controller) : base(controller)
        {
        }

        /// <summary>
        /// 상태 식별자를 반환합니다.
        /// </summary>
        public override EnemyAIStateId StateId => EnemyAIStateId.Return;

        /// <summary>
        /// Return 진입 시 이동을 활성화하고 Home으로 복귀를 시작합니다.
        /// </summary>
        public override void Enter()
        {
            Controller.SetMovementEnabled(true);
            Controller.PushAnimationIntent(StateId, true);
            Controller.RepathToHomeIfNeeded();
        }

        /// <summary>
        /// Return 유지 중 주기적으로 Home 목적지를 갱신합니다.
        /// </summary>
        public override void Tick(float deltaTime)
        {
            Controller.RepathToHomeIfNeeded();

            if (Controller.IsReturnComplete())
            {
                Controller.TryChangeState(EnemyAIStateId.Idle, false);
            }
        }
    }

    /// <summary>
    /// Death 상태 구현입니다.
    /// </summary>
    private sealed class DeathState : EnemyAIStateBase
    {
        /// <summary>
        /// Death 상태 생성자입니다.
        /// </summary>
        public DeathState(EnemyAIController controller) : base(controller)
        {
        }

        /// <summary>
        /// 상태 식별자를 반환합니다.
        /// </summary>
        public override EnemyAIStateId StateId => EnemyAIStateId.Death;

        /// <summary>
        /// Death 진입 시 모든 AI 런타임을 즉시 정지합니다.
        /// </summary>
        public override void Enter()
        {
            Controller.SetMovementEnabled(false);
            Controller.StopMovementNow();
            Controller._targetDetector?.ClearTarget();
            Controller._attackController?.SetTerminalState(true);
            Controller._attackController?.ForceStopAttack();
            Controller._healthAdapter?.SetCanBeHit(false);
            Controller.PushAnimationIntent(StateId, false);
            Controller.TriggerDeathVisual();
        }
    }

    /// <summary>
    /// HitReaction 상태 구현입니다.
    /// </summary>
    private sealed class HitReactionState : EnemyAIStateBase
    {
        /// <summary>
        /// HitReaction 상태 생성자입니다.
        /// </summary>
        public HitReactionState(EnemyAIController controller) : base(controller)
        {
        }

        /// <summary>
        /// 상태 식별자를 반환합니다.
        /// </summary>
        public override EnemyAIStateId StateId => EnemyAIStateId.HitReaction;

        /// <summary>
        /// HitReaction 진입 시 이동을 정지하고 피격 의도를 전달합니다.
        /// </summary>
        public override void Enter()
        {
            Controller.SetMovementEnabled(false);
            Controller.StopMovementNow();
            Controller.PushAnimationIntent(StateId, false);
            Controller.TriggerHitVisual();
        }
    }

    /// <summary>
    /// Patrol 확장을 위한 기본 자리 상태 구현입니다.
    /// </summary>
    private sealed class PatrolState : EnemyAIStateBase
    {
        /// <summary>
        /// Patrol 상태 생성자입니다.
        /// </summary>
        public PatrolState(EnemyAIController controller) : base(controller)
        {
        }

        /// <summary>
        /// 상태 식별자를 반환합니다.
        /// </summary>
        public override EnemyAIStateId StateId => EnemyAIStateId.Patrol;

        /// <summary>
        /// Patrol 상태는 현재 미구현이므로 진입 불가를 반환합니다.
        /// </summary>
        public override bool CanEnter()
        {
            return false;
        }
    }
}
