using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 고정형 원거리 Enemy의 Idle/Combat 상태 전이와 원거리 공격 흐름을 제어합니다.
/// </summary>
[DisallowMultipleComponent]
public class StationaryRangedEnemyController : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("EnemyAI 경로의 타겟 탐지기입니다.")]
    [SerializeField] private EnemyTargetDetector _targetDetector; // Player 탐지/타겟 캐시를 담당하는 탐지기입니다.
    [Tooltip("투사체 생성 요청 서비스입니다.")]
    [SerializeField] private MonoBehaviour _projectileSpawnServiceSource; // Inspector 주입용 서비스 소스입니다.
    [Tooltip("발사 기준 Transform입니다. 비어 있으면 Root를 사용합니다.")]
    [SerializeField] private Transform _firePoint; // 발사 위치/방향 기준 FirePoint입니다.
    [Tooltip("시각 좌우 반전에 사용할 Transform입니다.")]
    [SerializeField] private Transform _visualRoot; // 좌우 반전 대상 루트입니다.
    [Tooltip("공격 트리거를 전달할 Animator입니다.")]
    [SerializeField] private Animator _animator; // AttackTrigger를 호출할 Animator입니다.
    [Tooltip("피격/사망 이벤트를 수신할 Health 어댑터입니다.")]
    [SerializeField] private EnemyHealthAdapter _healthAdapter; // 피격/사망 이벤트 수신용 어댑터입니다.
    [Tooltip("사망 VFX/제거 시퀀스를 담당하는 사망 컨트롤러입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private EnemyAIDeathController _deathController; // 원거리 Enemy의 사망 제거 시퀀스를 담당하는 컨트롤러 참조입니다.
    [Tooltip("NGO 세션에서 서버 권한 여부를 판정할 NetworkObject 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private NetworkObject _networkObject; // 멀티플레이 서버 권한 판정에 사용할 NetworkObject 참조입니다.

    [Header("Ranges")]
    [Tooltip("Idle→Combat 감지 거리입니다.")]
    [SerializeField] private float _detectionRange = 8f; // Combat 진입/유지에 사용하는 감지 거리입니다.
    [Tooltip("공격 진입 거리입니다.")]
    [SerializeField] private float _attackEnterRange = 5f; // 공격 시작 임계 거리입니다.
    [Tooltip("공격 이탈 거리입니다. Enter보다 커야 합니다.")]
    [SerializeField] private float _attackExitRange = 6f; // 공격 중단 임계 거리입니다.

    [Header("Range Gizmos")]
    [Tooltip("씬 뷰에서 감지/공격 범위를 Gizmo로 시각화할지 여부입니다.")]
    [SerializeField] private bool _drawRangeGizmos = true; // 범위 시각화 활성화 여부입니다.
    [Tooltip("공격 범위를 FirePoint 기준으로 그릴지 여부입니다.")]
    [SerializeField] private bool _drawAttackRangeFromFirePoint = true; // 공격 범위 Gizmo 기준점 선택 플래그입니다.

    [Header("Attack")]
    [Tooltip("발사할 투사체 프리팹입니다.")]
    [SerializeField] private GameObject _projectilePrefab; // 발사할 투사체 프리팹입니다.
    [Tooltip("투사체 속도입니다.")]
    [SerializeField] private float _projectileSpeed = 12f; // 투사체 이동 속도입니다.
    [Tooltip("투사체 수명입니다.")]
    [SerializeField] private float _projectileLifetime = 3f; // 투사체 최대 생존 시간입니다.
    [Tooltip("투사체 발사 후 다음 발사까지 쿨다운(초)입니다.")]
    [SerializeField] private float _attackCooldown = 1.2f; // 실제 발사 시점부터 적용할 쿨다운 시간입니다.
    [Tooltip("애니메이션 이벤트 누락 시 공격 실패로 종료할 타임아웃(초)입니다.")]
    [SerializeField] private float _attackTimeout = 1f; // 이벤트 누락 감지용 최대 대기 시간입니다.
    [Tooltip("공격 시작 시 SetActive(false), 공격 종료 시 SetActive(true)로 토글할 첫 번째 오브젝트입니다.")]
    [SerializeField] private GameObject _toggleOffWhileAttackingObjectA; // 공격 중 숨길 첫 번째 오브젝트입니다.
    [Tooltip("공격 시작 시 SetActive(false), 공격 종료 시 SetActive(true)로 토글할 두 번째 오브젝트입니다.")]
    [SerializeField] private GameObject _toggleOffWhileAttackingObjectB; // 공격 중 숨길 두 번째 오브젝트입니다.

    [Header("Distance Optimization")]
    [Tooltip("거리 계산 주기입니다. 0이면 매 프레임 계산합니다.")]
    [SerializeField] private float _distanceUpdateInterval = 0f; // 거리 계산 갱신 주기입니다.
    [Tooltip("EnemyTargetDetector 타겟 탐색 주기(초)입니다.")]
    [SerializeField] private float _targetSearchInterval = 0.2f; // 타겟 재탐색 주기입니다.

    [Header("Hit / Die")]
    [Tooltip("피격 직후 공격 판단을 잠시 중단하는 시간(초)입니다.")]
    [SerializeField] private float _hitStaggerDuration = 0.15f; // 피격 경직(공격 중단) 시간입니다.

    [Header("Animator Parameters")]
    [Tooltip("전투 상태 여부를 전달할 Bool 파라미터 이름입니다.")]
    [SerializeField] private string _combatBoolParameter = "IsCombat"; // Combat 상태 동기화 Bool 파라미터 이름입니다.
    [Tooltip("피격 상태 진입을 전달할 Trigger 파라미터 이름입니다.")]
    [SerializeField] private string _hitTriggerParameter = "HitTrigger"; // 피격 연출 시작 Trigger 파라미터 이름입니다.
    [Tooltip("사망 상태 진입을 전달할 Trigger 파라미터 이름입니다.")]
    [SerializeField] private string _dieTriggerParameter = "DieTrigger"; // 사망 연출 시작 Trigger 파라미터 이름입니다.
    [Tooltip("공격 애니메이션 상태 식별에 사용할 Animator State Tag 이름입니다.")]
    [SerializeField] private string _attackStateTag = "Attack"; // 공격 애니메이션 상태 판정용 태그 이름입니다.
    [Tooltip("공격 애니메이션 상태를 조회할 Animator Layer 인덱스입니다.")]
    [SerializeField] private int _attackAnimationLayerIndex; // 공격 애니메이션 판정 대상 레이어 인덱스입니다.

    [Header("Debug")]
    [Tooltip("현재 상태입니다.")]
    [SerializeField] private E_StationaryRangedEnemyState _currentState = E_StationaryRangedEnemyState.Idle; // 상태 머신 현재 상태입니다.
    [Tooltip("네트워크 권한이 없는 관찰자 인스턴스 경고를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenAuthorityUnavailable = true; // 관찰자 인스턴스 권한 경고 출력 여부입니다.

    [Tooltip("현재 공격 진행 중 여부입니다.")]
    private bool _isAttacking; // 공격 진행 플래그입니다.
    [Tooltip("애니메이션 이벤트 수신 대기 중인 공격 사이클 여부입니다.")]
    private bool _isWaitingForFireEvent; // 발사 이벤트 대기 플래그입니다.
    [Tooltip("다음 공격 가능 시각입니다.")]
    private float _nextAttackAllowedAt; // 쿨다운 완료 절대 시각입니다.
    [Tooltip("공격 사이클 타임아웃 절대 시각입니다.")]
    private float _attackTimeoutAt; // 이벤트 누락 타임아웃 시각입니다.
    [Tooltip("다음 거리 계산 시각입니다.")]
    private float _nextDistanceUpdateAt; // 거리 계산 스케줄 시각입니다.
    [Tooltip("최근 계산한 타겟 거리입니다.")]
    private float _cachedDistance = float.MaxValue; // 최근 계산 거리 캐시입니다.
    [Tooltip("현재 사망 상태 여부입니다.")]
    private bool _isDead; // 사망 상태 플래그입니다.
    [Tooltip("피격 경직이 해제되는 시각입니다.")]
    private float _hitStaggerReleaseAt; // 피격 경직 종료 시각입니다.
    [Tooltip("현재 피격 상태 유지 여부입니다.")]
    private bool _isHitStateActive; // 피격 상태 유지 플래그입니다.
    [Tooltip("관찰자 인스턴스 권한 경고를 중복 출력하지 않기 위한 플래그입니다.")]
    private bool _hasLoggedAuthorityWarning; // 관찰자 인스턴스 권한 경고 중복 방지 플래그입니다.
    [Tooltip("공격 토글 오브젝트의 마지막 적용 활성 상태입니다.")]
    private bool _isAttackToggleObjectsActive = true; // SetActive 중복 호출 방지용 마지막 상태 캐시입니다.
    [Tooltip("현재 공격 사이클에서 발사 방향을 고정했는지 여부입니다.")]
    private bool _hasLockedAttackDirection; // 공격 시작 시 캡처한 발사 방향 유효성 플래그입니다.
    [Tooltip("공격 시작 시점 기준으로 고정한 발사 방향(Vector2 정규화)입니다.")]
    private Vector2 _lockedAttackDirection = Vector2.right; // 공격 시작 시점 타겟 기준 고정 발사 방향입니다.
    [Tooltip("공격 시작 시점에 저장한 타겟 월드 좌표입니다.")]
    private Vector2 _lockedTargetPosition; // 공격 시작 시점 타겟 좌표 스냅샷입니다.

    [Tooltip("런타임 ProjectileSpawn 서비스 인터페이스입니다.")]
    private IProjectileSpawnService _projectileSpawnService; // 투사체 생성 요청 인터페이스입니다.

    private static readonly int AttackTriggerHash = Animator.StringToHash("AttackTrigger"); // AttackTrigger Animator 해시입니다.
    [Tooltip("Combat Bool Animator 해시입니다.")]
    private int _combatBoolHash; // Combat Bool Animator 해시 캐시입니다.
    [Tooltip("Hit Trigger Animator 해시입니다.")]
    private int _hitTriggerHash; // Hit Trigger Animator 해시 캐시입니다.
    [Tooltip("Die Trigger Animator 해시입니다.")]
    private int _dieTriggerHash; // Die Trigger Animator 해시 캐시입니다.

    /// <summary>
    /// 초기 참조 구성/설정 검증을 수행합니다.
    /// </summary>
    [Tooltip("Projectile 생성/소멸 복제 시퀀스를 발급하는 값입니다.")]
    private int _projectileVisualSequence; // 복제 투사체 고유 ID 발급 시퀀스입니다.

    private readonly NetworkVariable<bool> _replicatedFacingRight = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 시각 방향 상태입니다.

    private bool _lastPublishedFacingRight = true; // 마지막으로 복제한 방향 상태입니다.

    private void Awake()
    {
        ResolveDependencies();
        ValidateAndFixSettings();
        CacheAnimatorHashes();
    }

    /// <summary>
    /// 활성화 시 Health 어댑터 이벤트를 구독합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        _replicatedFacingRight.OnValueChanged += HandleReplicatedFacingRightChanged;

        if (!CanExecuteAiAuthority())
        {
            ApplyReplicatedFacingDirection(_replicatedFacingRight.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        _replicatedFacingRight.OnValueChanged -= HandleReplicatedFacingRightChanged;
    }

    private void OnEnable()
    {
        SubscribeHealthSignals();
        _isDead = _healthAdapter != null && !_healthAdapter.IsAlive;
        _hasLoggedAuthorityWarning = false;
        _currentState = E_StationaryRangedEnemyState.Idle;
        _lastPublishedFacingRight = _replicatedFacingRight.Value;
        _deathController?.ResetRuntime();
        ForceAttackToggleObjectsActive(true);
    }

    /// <summary>
    /// 비활성화 시 Health 어댑터 이벤트를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeHealthSignals();
        ForceAttackToggleObjectsActive(true);
    }

    /// <summary>
    /// 에디터 값 변경 시 설정 검증/자동 보정을 수행합니다.
    /// </summary>
    private void OnValidate()
    {
        ValidateAndFixSettings();
        CacheAnimatorHashes();
    }

    /// <summary>
    /// 선택 상태에서 감지/공격 범위를 Gizmo로 시각화합니다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!_drawRangeGizmos)
        {
            return;
        }

        Vector3 detectionOrigin = transform.position;
        Vector3 attackOrigin = _drawAttackRangeFromFirePoint && _firePoint != null ? _firePoint.position : transform.position;

        DrawRangeGizmo(detectionOrigin, _detectionRange, new Color(0.1f, 0.75f, 1f, 0.45f));
        DrawRangeGizmo(attackOrigin, _attackEnterRange, new Color(0.2f, 1f, 0.2f, 0.45f));
        DrawRangeGizmo(attackOrigin, _attackExitRange, new Color(1f, 0.8f, 0.2f, 0.45f));
    }

    /// <summary>
    /// 프레임 루프에서 상태 머신/타겟 선택/공격 판단을 수행합니다.
    /// </summary>
    private void Update()
    {
        ApplyReplicatedFacingDirection(_replicatedFacingRight.Value);
        SyncAnimatorState();
        SyncAttackToggleObjectsByAnimation();

        if (!CanExecuteAiAuthority())
        {
            return;
        }

        if (_isDead)
        {
            return;
        }

        TickTargetDetection();

        if (_isHitStateActive)
        {
            if (Time.time < _hitStaggerReleaseAt)
            {
                return;
            }

            _isHitStateActive = false;
        }

        if (_isAttacking && Time.time >= _attackTimeoutAt)
        {
            Debug.LogWarning($"[StationaryRangedEnemyController] Attack timeout on {name}. AnimationEvent OnFireProjectile missing; cycle failed.");
            EndAttackCycle(applyCooldown: false);
        }

        switch (_currentState)
        {
            case E_StationaryRangedEnemyState.Idle:
                TickIdle();
                break;
            case E_StationaryRangedEnemyState.Combat:
                TickCombat();
                break;
        }
    }

    /// <summary>
    /// 현재 인스턴스가 원거리 Enemy AI 의사결정을 수행할 서버 권한을 가졌는지 판정합니다.
    /// </summary>
    private bool CanExecuteAiAuthority()
    {
        bool canExecute = EnemyNetworkAuthorityUtility.ShouldRunServerAuthoritativeLogic(_networkObject);
        if (!canExecute && _warnWhenAuthorityUnavailable && !_hasLoggedAuthorityWarning)
        {
            Debug.LogWarning($"[StationaryRangedEnemyController] AI authority unavailable on observer instance. object={name}", this);
            _hasLoggedAuthorityWarning = true;
        }

        if (canExecute)
        {
            _hasLoggedAuthorityWarning = false;
        }

        return canExecute;
    }

    /// <summary>
    /// 서버가 현재 시각 상태를 관찰자 인스턴스에 복제해야 하는지 판정합니다.
    /// </summary>
    private bool ShouldReplicateVisuals()
    {
        return EnemyNetworkAuthorityUtility.ShouldReplicateFromServer(_networkObject);
    }

    /// <summary>
    /// 상태 값을 갱신하고 Animator Bool 시각을 즉시 동기화합니다.
    /// </summary>
    private void SetCurrentState(E_StationaryRangedEnemyState nextState)
    {
        if (_currentState == nextState)
        {
            return;
        }

        _currentState = nextState;
        SyncAnimatorState();

        if (ShouldReplicateVisuals())
        {
            SetCurrentStateRpc((int)nextState);
        }
    }

    /// <summary>
    /// 서버가 확정한 전투 상태를 관찰자 인스턴스에 복제합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void SetCurrentStateRpc(int stateValue)
    {
        _currentState = (E_StationaryRangedEnemyState)stateValue;
        SyncAnimatorState();
    }

    /// <summary>
    /// 서버가 확정한 공격 시작 트리거를 관찰자 인스턴스 Animator에 전달합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void TriggerAttackVisualRpc()
    {
        _animator?.SetTrigger(AttackTriggerHash);
    }

    /// <summary>
    /// 서버가 확정한 피격 트리거를 관찰자 인스턴스 Animator에 전달합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void TriggerHitVisualRpc()
    {
        TriggerHitAnimation();
    }

    /// <summary>
    /// 서버가 확정한 사망 트리거를 관찰자 인스턴스 Animator에 전달합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void TriggerDieVisualRpc()
    {
        TriggerDieAnimation();
        _deathController?.TryEnterDeath();
    }

    /// <summary>
    /// 서버가 확정한 투사체 발사 결과를 관찰자 인스턴스에 전달해 동일한 시각 투사체를 생성합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void SpawnProjectileVisualRpc(int projectileVisualId, Vector2 firePosition, Vector2 direction, float projectileSpeed, float projectileLifetime)
    {
        if (_projectileSpawnService == null)
        {
            ResolveDependencies();
        }

        if (_projectileSpawnService == null)
        {
            Debug.LogWarning($"[StationaryRangedEnemyController] Missing IProjectileSpawnService on observer instance {name}.");
            return;
        }

        _projectileSpawnService.RequestSpawn(
            _projectilePrefab,
            firePosition,
            direction,
            gameObject,
            projectileSpeed,
            projectileLifetime,
            true,
            projectileVisualId);
    }

    /// <summary>
    /// 서버가 확정한 Projectile 종료 결과를 관찰자 인스턴스에 적용합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void DespawnProjectileVisualRpc(int projectileVisualId)
    {
        if (_projectileSpawnService == null)
        {
            ResolveDependencies();
        }

        _projectileSpawnService?.TryDespawnVisual(projectileVisualId);
    }

    /// <summary>
    /// Idle 상태 전이를 처리합니다.
    /// </summary>
    private void TickIdle()
    {
        Transform target = _targetDetector != null ? _targetDetector.CurrentTarget : null;
        if (!IsTargetValidForDetection(target))
        {
            return;
        }

        if (GetDistanceToTarget(target) <= _detectionRange)
        {
            SetCurrentState(E_StationaryRangedEnemyState.Combat);
        }
    }

    /// <summary>
    /// Combat 상태 전이 및 공격 흐름을 고정 순서로 처리합니다.
    /// </summary>
    private void TickCombat()
    {
        Transform target = _targetDetector != null ? _targetDetector.CurrentTarget : null;

        // 0) 이미 공격 애니메이션 이벤트 대기 중이면 타겟 이탈 여부와 무관하게 발사 이벤트를 기다립니다.
        if (_isAttacking)
        {
            if (target != null)
            {
                UpdateVisualFacing(target.position);
            }

            return;
        }

        // 1) 타겟 유효성 검사
        if (!IsTargetValidForDetection(target))
        {
            StopAttackAsFailedCycle();
            SetCurrentState(E_StationaryRangedEnemyState.Idle);
            return;
        }

        // 2) 거리 계산
        float distance = GetDistanceToTarget(target);

        if (distance > _detectionRange)
        {
            StopAttackAsFailedCycle();
            SetCurrentState(E_StationaryRangedEnemyState.Idle);
            return;
        }

        // 3) 공격 가능 상태 판정 (히스테리시스)
        bool isWithinEnterRange = distance <= _attackEnterRange;
        bool shouldExitAttack = distance >= _attackExitRange;

        if (shouldExitAttack)
        {
            StopAttackAsFailedCycle();
            return;
        }

        // 4) 쿨다운 검사
        bool isCooldownFinished = Time.time >= _nextAttackAllowedAt;
        if (!isCooldownFinished)
        {
            UpdateVisualFacing(target.position);
            return;
        }

        // 5) 공격 시작
        if (isWithinEnterRange)
        {
            StartAttackCycle();
            UpdateVisualFacing(target.position);
        }
    }

    /// <summary>
    /// 애니메이션 트리거를 발행하고 공격 사이클을 시작합니다.
    /// </summary>
    private void StartAttackCycle()
    {
        if (!TryLockAttackDirection())
        {
            Debug.LogWarning($"[StationaryRangedEnemyController] Failed to lock attack direction on {name}. Attack cycle failed.");
            EndAttackCycle(applyCooldown: false);
            return;
        }

        BeginAttackCycle();

        if (_animator != null)
        {
            _animator.SetTrigger(AttackTriggerHash);

            if (ShouldReplicateVisuals())
            {
                TriggerAttackVisualRpc();
            }
        }
        else
        {
            Debug.LogWarning($"[StationaryRangedEnemyController] Missing Animator on {name}. Attack cycle failed.");
            EndAttackCycle(applyCooldown: false);
        }
    }

    /// <summary>
    /// 애니메이션 이벤트에서 호출되어 투사체를 생성하고 공격 사이클을 종료합니다.
    /// </summary>
    public void OnFireProjectile()
    {
        if (!CanExecuteAiAuthority())
        {
            return;
        }

        if (!_isWaitingForFireEvent)
        {
            Debug.LogWarning($"[StationaryRangedEnemyController] OnFireProjectile called outside active cycle on {name}.");
            return;
        }

        Vector2 firePosition = (_firePoint != null ? (Vector2)_firePoint.position : (Vector2)transform.position);
        if (!TryResolveTargetPositionOnFire(out Vector2 targetPosition, out bool usedFallbackTargetPosition))
        {
            Debug.LogWarning($"[StationaryRangedEnemyController] OnFireProjectile failed: no live target and no locked fallback target on {name}.");
            StopAttackAsFailedCycle();
            return;
        }

        Vector2 direction = targetPosition - firePosition;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            direction = _hasLockedAttackDirection ? _lockedAttackDirection : Vector2.right;
        }

        direction = direction.normalized;

        if (_projectileSpawnService == null)
        {
            Debug.LogWarning($"[StationaryRangedEnemyController] Missing IProjectileSpawnService on {name}.");
            StopAttackAsFailedCycle();
            return;
        }

        int projectileVisualId = NextProjectileVisualId();
        PooledRangedProjectile spawnedProjectile = _projectileSpawnService.RequestSpawn(
            _projectilePrefab,
            firePosition,
            direction,
            gameObject,
            _projectileSpeed,
            _projectileLifetime,
            false,
            projectileVisualId);

        if (spawnedProjectile == null)
        {
            StopAttackAsFailedCycle();
            return;
        }

        spawnedProjectile.Despawned += HandleAuthoritativeProjectileDespawned;

        if (ShouldReplicateVisuals())
        {
            SpawnProjectileVisualRpc(projectileVisualId, firePosition, direction, _projectileSpeed, _projectileLifetime);
        }

        if (usedFallbackTargetPosition)
        {
            Debug.Log($"[StationaryRangedEnemyController] OnFireProjectile fallback used locked target position on {name}.");
        }

        EndAttackCycle(applyCooldown: true);
    }

    /// <summary>
    /// 실패한 공격 사이클을 종료하고 쿨다운을 적용하지 않습니다.
    /// </summary>
    private void StopAttackAsFailedCycle()
    {
        EndAttackCycle(applyCooldown: false);
    }

    /// <summary>
    /// 공격 사이클 시작 상태를 초기화하고 공격 중 토글 오브젝트를 비활성화합니다.
    /// </summary>
    private void BeginAttackCycle()
    {
        _isAttacking = true;
        _isWaitingForFireEvent = true;
        _attackTimeoutAt = Time.time + _attackTimeout;
    }

    /// <summary>
    /// 공격 사이클을 종료하고 공격 중 토글 오브젝트를 활성화합니다.
    /// </summary>
    private void EndAttackCycle(bool applyCooldown)
    {
        _isAttacking = false;
        _isWaitingForFireEvent = false;
        _hasLockedAttackDirection = false;
        _lockedAttackDirection = Vector2.right;
        _lockedTargetPosition = Vector2.zero;

        if (applyCooldown)
        {
            _nextAttackAllowedAt = Time.time + _attackCooldown;
        }
    }

    /// <summary>
    /// 공격 상태에 맞춰 지정된 토글 오브젝트 2개의 활성 상태를 일괄 전환합니다.
    /// </summary>
    private void SetAttackToggleObjectsActive(bool isActive)
    {
        if (_toggleOffWhileAttackingObjectA != null)
        {
            _toggleOffWhileAttackingObjectA.SetActive(isActive);
        }

        if (_toggleOffWhileAttackingObjectB != null)
        {
            _toggleOffWhileAttackingObjectB.SetActive(isActive);
        }
    }

    /// <summary>
    /// 공격 토글 오브젝트의 활성 상태를 강제로 적용하고 캐시 상태를 동기화합니다.
    /// </summary>
    private void ForceAttackToggleObjectsActive(bool isActive)
    {
        _isAttackToggleObjectsActive = isActive;
        SetAttackToggleObjectsActive(isActive);
    }

    /// <summary>
    /// 공격 애니메이션 재생 여부를 기준으로 토글 오브젝트 활성 상태를 동기화합니다.
    /// </summary>
    private void SyncAttackToggleObjectsByAnimation()
    {
        bool shouldBeActive = !IsAttackAnimationPlaying();
        if (_isAttackToggleObjectsActive == shouldBeActive)
        {
            return;
        }

        _isAttackToggleObjectsActive = shouldBeActive;
        SetAttackToggleObjectsActive(shouldBeActive);
    }

    /// <summary>
    /// 현재/다음 Animator 상태를 검사해 공격 애니메이션 진행 중인지 판정합니다.
    /// </summary>
    private bool IsAttackAnimationPlaying()
    {
        if (_animator == null)
        {
            return false;
        }

        int layerIndex = Mathf.Clamp(_attackAnimationLayerIndex, 0, _animator.layerCount - 1);
        AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (currentState.IsTag(_attackStateTag))
        {
            return true;
        }

        if (_animator.IsInTransition(layerIndex))
        {
            AnimatorStateInfo nextState = _animator.GetNextAnimatorStateInfo(layerIndex);
            return nextState.IsTag(_attackStateTag);
        }

        return false;
    }

    /// <summary>
    /// 공격 시작 시점의 타겟 좌표를 스냅샷으로 저장하고 발사 방향을 고정합니다.
    /// </summary>
    private bool TryLockAttackDirection()
    {
        Transform target = _targetDetector != null ? _targetDetector.CurrentTarget : null;
        if (target == null)
        {
            _hasLockedAttackDirection = false;
            return false;
        }

        Vector2 firePosition = (_firePoint != null ? (Vector2)_firePoint.position : (Vector2)transform.position);
        _lockedTargetPosition = target.position;
        Vector2 lockedDirection = _lockedTargetPosition - firePosition;
        if (lockedDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            lockedDirection = Vector2.right;
        }

        _lockedAttackDirection = lockedDirection.normalized;
        _hasLockedAttackDirection = true;
        return true;
    }

    /// <summary>
    /// 발사 이벤트 시점에 타겟을 재탐지하고, 실패하면 공격 시작 시점에 저장한 타겟 좌표를 대체 목표로 반환합니다.
    /// </summary>
    private bool TryResolveTargetPositionOnFire(out Vector2 targetPosition, out bool usedFallbackTargetPosition)
    {
        targetPosition = Vector2.zero;
        usedFallbackTargetPosition = false;

        TickTargetDetection();
        Transform liveTarget = _targetDetector != null ? _targetDetector.CurrentTarget : null;
        if (IsTargetValidForDetection(liveTarget))
        {
            targetPosition = liveTarget.position;
            return true;
        }

        if (_hasLockedAttackDirection)
        {
            targetPosition = _lockedTargetPosition;
            usedFallbackTargetPosition = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 타겟이 감지/전투 조건을 만족하는지 판정합니다.
    /// </summary>
    private bool IsTargetValidForDetection(Transform target)
    {
        return !_isDead && _targetDetector != null && _targetDetector.IsTargetValid(target, ResolveTargetHealthComponent(target));
    }

    /// <summary>
    /// 설정된 갱신 주기에 따라 타겟 거리를 계산/캐시합니다.
    /// </summary>
    private float GetDistanceToTarget(Transform target)
    {
        if (target == null)
        {
            _cachedDistance = float.MaxValue;
            return _cachedDistance;
        }

        if (_distanceUpdateInterval <= 0f || Time.time >= _nextDistanceUpdateAt)
        {
            _cachedDistance = Vector2.Distance(transform.position, target.position);
            _nextDistanceUpdateAt = Time.time + Mathf.Max(0f, _distanceUpdateInterval);
        }

        return _cachedDistance;
    }

    /// <summary>
    /// 타겟의 X 상대 위치를 기준으로 비주얼 좌우 반전을 처리합니다.
    /// </summary>
    private void UpdateVisualFacing(Vector2 targetPosition)
    {
        if (_visualRoot == null)
        {
            return;
        }

        float deltaX = targetPosition.x - transform.position.x;
        if (Mathf.Approximately(deltaX, 0f))
        {
            return;
        }

        bool isFacingRight = deltaX >= 0f;
        ApplyFacingDirection(isFacingRight);

        if (ShouldReplicateVisuals() && _lastPublishedFacingRight != isFacingRight)
        {
            _replicatedFacingRight.Value = isFacingRight;
            _lastPublishedFacingRight = isFacingRight;
        }
    }

    /// <summary>
    /// 필수 참조를 자동 연결하고 서비스 인터페이스를 해석합니다.
    /// </summary>
    /// <summary>
    /// 서버 Projectile 소멸 결과를 관찰자 시각 Projectile에도 복제합니다.
    /// </summary>
    /// <summary>
    /// 관찰자 인스턴스가 받은 방향 복제 값을 시각 루트에 반영합니다.
    /// </summary>
    private void HandleReplicatedFacingRightChanged(bool previousValue, bool currentValue)
    {
        ApplyReplicatedFacingDirection(currentValue);
    }

    /// <summary>
    /// 서버 확정 방향 값을 관찰자 시각 루트에 반영합니다.
    /// </summary>
    private void ApplyReplicatedFacingDirection(bool isFacingRight)
    {
        if (EnemyNetworkAuthorityUtility.ShouldRunServerAuthoritativeLogic(_networkObject))
        {
            return;
        }

        ApplyFacingDirection(isFacingRight);
    }

    /// <summary>
    /// 지정 방향 기준으로 시각 루트 좌우 반전을 적용합니다.
    /// </summary>
    private void ApplyFacingDirection(bool isFacingRight)
    {
        if (_visualRoot == null)
        {
            return;
        }

        Vector3 localScale = _visualRoot.localScale;
        localScale.x = Mathf.Abs(localScale.x) * (isFacingRight ? 1f : -1f);
        _visualRoot.localScale = localScale;
    }

    private void HandleAuthoritativeProjectileDespawned(PooledRangedProjectile projectile, E_ProjectileDespawnReason reason)
    {
        if (projectile == null)
        {
            return;
        }

        projectile.Despawned -= HandleAuthoritativeProjectileDespawned;

        if (!ShouldReplicateVisuals() || projectile.VisualInstanceId <= 0)
        {
            return;
        }

        DespawnProjectileVisualRpc(projectile.VisualInstanceId);
    }

    /// <summary>
    /// 관찰자용 Projectile 복제 ID를 발급합니다.
    /// </summary>
    private int NextProjectileVisualId()
    {
        _projectileVisualSequence++;
        if (_projectileVisualSequence <= 0)
        {
            _projectileVisualSequence = 1;
        }

        return _projectileVisualSequence;
    }

    private void ResolveDependencies()
    {
        if (_targetDetector == null)
        {
            _targetDetector = GetComponent<EnemyTargetDetector>();
        }

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        BindAnimationEventRelay();

        if (_healthAdapter == null)
        {
            _healthAdapter = GetComponent<EnemyHealthAdapter>();
        }

        if (_deathController == null)
        {
            _deathController = GetComponent<EnemyAIDeathController>();
        }

        if (_networkObject == null)
        {
            _networkObject = GetComponent<NetworkObject>();
        }

        if (_visualRoot == null)
        {
            _visualRoot = transform;
        }

        if (_projectileSpawnServiceSource != null)
        {
            _projectileSpawnService = _projectileSpawnServiceSource as IProjectileSpawnService;
        }

        if (_projectileSpawnService == null)
        {
            _projectileSpawnService = GetComponent<IProjectileSpawnService>();
        }

        if (_projectileSpawnService == null)
        {
            PooledProjectileSpawnService sceneSpawnService = FindFirstObjectByType<PooledProjectileSpawnService>();
            if (sceneSpawnService != null)
            {
                _projectileSpawnServiceSource = sceneSpawnService;
                _projectileSpawnService = sceneSpawnService;
            }
            else
            {
                Debug.LogWarning($"[StationaryRangedEnemyController] Could not find PooledProjectileSpawnService in scene on {name}. Please assign _projectileSpawnServiceSource.");
            }
        }
    }

    /// <summary>
    /// Animator가 다른 오브젝트에 있어도 애니메이션 이벤트가 본 컨트롤러로 전달되도록 Relay를 연결합니다.
    /// </summary>
    private void BindAnimationEventRelay()
    {
        if (_animator == null)
        {
            return;
        }

        StationaryRangedEnemyAnimationEventRelay relay = _animator.GetComponent<StationaryRangedEnemyAnimationEventRelay>();
        if (relay == null)
        {
            relay = _animator.gameObject.AddComponent<StationaryRangedEnemyAnimationEventRelay>();
        }

        relay.BindController(this);
    }

    /// <summary>
    /// 인스펙터 설정값의 제약을 검증하고 필요한 보정을 수행합니다.
    /// </summary>
    private void ValidateAndFixSettings()
    {
        if (_projectilePrefab == null)
        {
            Debug.LogWarning($"[StationaryRangedEnemyController] ProjectilePrefab is null on {name}.");
        }

        if (_attackEnterRange > _detectionRange)
        {
            Debug.LogWarning($"[StationaryRangedEnemyController] AttackEnterRange({_attackEnterRange}) > DetectionRange({_detectionRange}) on {name}. Clamped.");
            _attackEnterRange = _detectionRange;
        }

        if (_attackExitRange <= _attackEnterRange)
        {
            _attackExitRange = _attackEnterRange + 0.1f;
            Debug.LogWarning($"[StationaryRangedEnemyController] AttackExitRange must be greater than AttackEnterRange on {name}. Auto corrected.");
        }

        if (_attackCooldown <= 0f)
        {
            _attackCooldown = 0.1f;
            Debug.LogWarning($"[StationaryRangedEnemyController] Cooldown must be > 0 on {name}. Auto corrected to 0.1.");
        }

        if (_projectileSpeed <= 0f)
        {
            _projectileSpeed = 1f;
            Debug.LogWarning($"[StationaryRangedEnemyController] Projectile speed must be > 0 on {name}. Auto corrected to 1.");
        }

        if (_projectileLifetime <= 0f)
        {
            _projectileLifetime = 0.1f;
            Debug.LogWarning($"[StationaryRangedEnemyController] Projectile lifetime must be > 0 on {name}. Auto corrected to 0.1.");
        }

        if (_firePoint == null)
        {
            _firePoint = transform;
            Debug.LogWarning($"[StationaryRangedEnemyController] FirePoint is null on {name}. Fallback to root transform.");
        }

        if (_attackTimeout <= 0f)
        {
            _attackTimeout = 0.1f;
            Debug.LogWarning($"[StationaryRangedEnemyController] Attack timeout must be > 0 on {name}. Auto corrected to 0.1.");
        }

        if (_targetSearchInterval <= 0f)
        {
            _targetSearchInterval = 0.1f;
            Debug.LogWarning($"[StationaryRangedEnemyController] Target search interval must be > 0 on {name}. Auto corrected to 0.1.");
        }

        if (_hitStaggerDuration < 0f)
        {
            _hitStaggerDuration = 0f;
            Debug.LogWarning($"[StationaryRangedEnemyController] Hit stagger duration must be >= 0 on {name}. Auto corrected to 0.");
        }

        if (string.IsNullOrWhiteSpace(_combatBoolParameter))
        {
            _combatBoolParameter = "IsCombat";
            Debug.LogWarning($"[StationaryRangedEnemyController] Combat bool parameter is empty on {name}. Auto corrected to IsCombat.");
        }

        if (string.IsNullOrWhiteSpace(_hitTriggerParameter))
        {
            _hitTriggerParameter = "HitTrigger";
            Debug.LogWarning($"[StationaryRangedEnemyController] Hit trigger parameter is empty on {name}. Auto corrected to HitTrigger.");
        }

        if (string.IsNullOrWhiteSpace(_dieTriggerParameter))
        {
            _dieTriggerParameter = "DieTrigger";
            Debug.LogWarning($"[StationaryRangedEnemyController] Die trigger parameter is empty on {name}. Auto corrected to DieTrigger.");
        }

        if (string.IsNullOrWhiteSpace(_attackStateTag))
        {
            _attackStateTag = "Attack";
            Debug.LogWarning($"[StationaryRangedEnemyController] Attack state tag is empty on {name}. Auto corrected to Attack.");
        }

        if (_attackAnimationLayerIndex < 0)
        {
            _attackAnimationLayerIndex = 0;
            Debug.LogWarning($"[StationaryRangedEnemyController] Attack animation layer index must be >= 0 on {name}. Auto corrected to 0.");
        }
    }

    /// <summary>
    /// Health 어댑터 이벤트를 구독합니다.
    /// </summary>
    private void SubscribeHealthSignals()
    {
        if (_healthAdapter == null)
        {
            return;
        }

        _healthAdapter.Damaged += HandleDamaged;
        _healthAdapter.Died += HandleDied;
        _healthAdapter.Revived += HandleRevived;
    }

    /// <summary>
    /// Health 어댑터 이벤트 구독을 해제합니다.
    /// </summary>
    private void UnsubscribeHealthSignals()
    {
        if (_healthAdapter == null)
        {
            return;
        }

        _healthAdapter.Damaged -= HandleDamaged;
        _healthAdapter.Died -= HandleDied;
        _healthAdapter.Revived -= HandleRevived;
    }

    /// <summary>
    /// 피격 시 공격 사이클을 중단하고 짧은 경직을 적용합니다.
    /// </summary>
    private void HandleDamaged()
    {
        if (_isDead)
        {
            return;
        }

        StopAttackAsFailedCycle();
        _isHitStateActive = true;
        _hitStaggerReleaseAt = Time.time + _hitStaggerDuration;
        TriggerHitAnimation();

        if (ShouldReplicateVisuals())
        {
            TriggerHitVisualRpc();
        }
    }

    /// <summary>
    /// 사망 시 전투를 종료하고 업데이트를 정지합니다.
    /// </summary>
    private void HandleDied()
    {
        _isDead = true;
        _isHitStateActive = false;
        SetCurrentState(E_StationaryRangedEnemyState.Idle);
        StopAttackAsFailedCycle();
        TriggerDieAnimation();

        if (ShouldReplicateVisuals())
        {
            TriggerDieVisualRpc();
        }

        _healthAdapter?.SetCanBeHit(false);
        _targetDetector?.ClearTarget();
        _deathController?.TryEnterDeath();
    }

    /// <summary>
    /// 부활 시 전투 가능 상태로 복귀합니다.
    /// </summary>
    private void HandleRevived()
    {
        _isDead = false;
        _isHitStateActive = false;
        SetCurrentState(E_StationaryRangedEnemyState.Idle);
        _hitStaggerReleaseAt = 0f;
        _healthAdapter?.SetCanBeHit(true);
    }

    /// <summary>
    /// Animator 파라미터 해시를 캐시해 런타임 문자열 조회를 줄입니다.
    /// </summary>
    private void CacheAnimatorHashes()
    {
        _combatBoolHash = Animator.StringToHash(_combatBoolParameter);
        _hitTriggerHash = Animator.StringToHash(_hitTriggerParameter);
        _dieTriggerHash = Animator.StringToHash(_dieTriggerParameter);
    }

    /// <summary>
    /// 현재 로직 상태를 Animator 파라미터에 동기화합니다.
    /// </summary>
    private void SyncAnimatorState()
    {
        if (_animator == null)
        {
            return;
        }

        bool isCombatVisual = !_isDead && !_isHitStateActive && _currentState == E_StationaryRangedEnemyState.Combat;
        _animator.SetBool(_combatBoolHash, isCombatVisual);
    }

    /// <summary>
    /// 피격 애니메이션 Trigger를 호출해 공격 애니메이션을 중단하고 피격 상태를 재생합니다.
    /// </summary>
    private void TriggerHitAnimation()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.ResetTrigger(AttackTriggerHash);
        _animator.SetTrigger(_hitTriggerHash);
    }

    /// <summary>
    /// 사망 애니메이션 Trigger를 호출합니다.
    /// </summary>
    private void TriggerDieAnimation()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.ResetTrigger(AttackTriggerHash);
        _animator.SetTrigger(_dieTriggerHash);
    }

    /// <summary>
    /// 지정 원점/반경/색상으로 2D 범위 Gizmo를 그립니다.
    /// </summary>
    private void DrawRangeGizmo(Vector3 origin, float radius, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(origin, Mathf.Max(0f, radius));
    }

    /// <summary>
    /// EnemyTargetDetector를 사용해 Player 탐지를 갱신합니다.
    /// </summary>
    private void TickTargetDetection()
    {
        if (_targetDetector == null)
        {
            return;
        }

        _targetDetector.TickSearch(Time.time, transform.position, _detectionRange, _targetSearchInterval);
    }

    /// <summary>
    /// 타겟 Transform에서 HealthComponent를 찾아 유효성 판정 입력으로 전달합니다.
    /// </summary>
    private HealthComponent ResolveTargetHealthComponent(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        return target.GetComponent<HealthComponent>() ?? target.GetComponentInParent<HealthComponent>();
    }
}
