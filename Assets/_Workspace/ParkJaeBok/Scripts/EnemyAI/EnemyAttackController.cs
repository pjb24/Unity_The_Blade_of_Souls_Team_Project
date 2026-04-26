using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 공격 가능 여부, 애니메이션 이벤트 기반 공격 판정, 공격 종료 신호를 관리하는 공격 컨트롤러입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAttackController : NetworkBehaviour
{
    [Header("Attack Runtime")]
    [Tooltip("공격 쿨다운 시간(초)입니다.")]
    [SerializeField] private float _attackCooldown = 1.2f; // 공격 쿨다운 시간 설정 값입니다.
    [Tooltip("AttackEnd 이벤트가 누락되었을 때 사용할 공격 최대 지속 시간(초)입니다.")]
    [SerializeField] private float _attackFallbackDuration = 0.9f; // 공격 종료 이벤트 누락 시 폴백 종료 시간입니다.

    [Header("Damage / Area")]
    [Tooltip("AnimationEvent_AttackActive에서 적용할 기본 공격 데미지입니다.")]
    [SerializeField] private float _attackDamage = 10f; // 공격 활성화 시 피격 대상에 전달할 데미지 값입니다.
    [Tooltip("공격 판정 원형 반경입니다.")]
    [SerializeField] private float _attackRadius = 1.2f; // 공격 판정에 사용할 원형 반경 값입니다.
    [Tooltip("공격 판정 중심점 로컬 오프셋입니다.")]
    [SerializeField] private Vector2 _attackOffset = new Vector2(0.8f, 0f); // 공격 판정 중심 오프셋 값입니다.
    [Tooltip("공격 대상 탐지에 사용할 레이어 마스크입니다.")]
    [SerializeField] private LayerMask _targetLayerMask = ~0; // 공격 대상 탐지 레이어 마스크입니다.
    [Tooltip("비어 있지 않으면 지정 태그와 일치하는 대상만 타격합니다.")]
    [SerializeField] private string _targetTag = "Player"; // 공격 타겟 태그 필터 문자열입니다.
    [Tooltip("HitRequest.StatusTag로 사용할 문자열입니다.")]
    [SerializeField] private string _statusTag = "BloodLight_Suicide"; // HitRequest에 기록할 상태 태그 문자열입니다.
    [Tooltip("중복 이벤트 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseEventLog; // 이벤트 중복/비정상 순서 방어 로그 출력 여부입니다.
    [Tooltip("1회 AttackActive에서 타격 가능한 최대 플레이어 수입니다. 최대 2인 멀티 기준으로 2를 권장합니다.")]
    [SerializeField] private int _maxTargetsPerAttack = 2; // 단일 공격 이벤트에서 타격 처리할 최대 플레이어 수입니다.

    [Header("References")]
    [Tooltip("공격 의도/애니메이션 이벤트 전달 브리지 참조입니다.")]
    [SerializeField] private EnemyAnimationBridge _animationBridge; // 공격 의도/애니메이션 이벤트 전달용 브리지 참조입니다.
    [Tooltip("네트워크 스폰 상태에서 서버 권한 판정을 위한 NetworkObject 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private NetworkObject _networkObject; // 네트워크 권한 판정에 사용할 NetworkObject 참조입니다.

    [Header("Multiplayer")]
    [Tooltip("네트워크 스폰 상태에서는 서버 인스턴스에서만 공격 판정을 수행할지 여부입니다.")]
    [SerializeField] private bool _executeOnlyOnServerWhenSpawned = true; // 네트워크 스폰 상태에서 서버 전용 공격 판정 실행 여부입니다.
    [Tooltip("네트워크 권한 판정을 확정할 수 없는 경우 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenNetworkAuthorityUnavailable = true; // 네트워크 권한 판정 실패 시 경고 출력 여부입니다.

    private readonly Collider2D[] _overlapBuffer = new Collider2D[16]; // OverlapCircle 탐지 결과를 재사용 버퍼에 보관하는 임시 배열입니다.
    private readonly HashSet<int> _damagedTargetIds = new HashSet<int>(); // 현재 공격 시퀀스에서 이미 타격한 타겟 InstanceId 집합입니다.

    private float _nextAttackAllowedAt; // 다음 공격 가능 시각입니다.
    private float _attackStartedAt = -1f; // 현재 공격 시작 시각입니다.
    private int _attackSequenceId; // 공격 시퀀스 고유 식별자입니다.

    private bool _isAttacking; // 현재 공격 시퀀스 진행 여부입니다.
    private bool _isAttackAreaActive; // 공격 범위 활성 상태 여부입니다.
    private bool _hasResolvedDamageThisSequence; // 현재 시퀀스에서 데미지 판정을 이미 수행했는지 여부입니다.
    private bool _isTerminalState; // 사망/제거 등 터미널 상태로 인해 공격 이벤트를 차단해야 하는지 여부입니다.
    private bool _hasWarnedFallback; // 폴백 종료 경고 출력 여부입니다.

    /// <summary>
    /// 공격 애니메이션 종료 이벤트(AttackEnd/폴백)입니다.
    /// </summary>
    public event Action AttackSequenceCompleted;

    /// <summary>
    /// 현재 공격 진행 여부를 반환합니다.
    /// </summary>
    public bool IsAttacking => _isAttacking;

    /// <summary>
    /// 현재 공격 범위 활성화 상태 여부를 반환합니다.
    /// </summary>
    public bool IsAttackAreaActive => _isAttackAreaActive;

    /// <summary>
    /// 현재 공격 쿨다운 상태 여부를 반환합니다.
    /// </summary>
    public bool IsCooldownActive => Time.time < _nextAttackAllowedAt;

    /// <summary>
    /// 초기 의존성 구성을 수행합니다.
    /// </summary>
    private void Awake()
    {
        if (_animationBridge == null)
        {
            _animationBridge = GetComponent<EnemyAnimationBridge>();
        }

        if (_networkObject == null)
        {
            _networkObject = GetComponent<NetworkObject>();
        }

        ValidateAndClampSettings();
    }

    /// <summary>
    /// 에디터 값 변경 시 공격 설정 제약을 검증합니다.
    /// </summary>
    private void OnValidate()
    {
        ValidateAndClampSettings();

        if (_animationBridge == null)
        {
            _animationBridge = GetComponent<EnemyAnimationBridge>();
        }

        if (_networkObject == null)
        {
            _networkObject = GetComponent<NetworkObject>();
        }
    }

    /// <summary>
    /// 활성화 시 애니메이션 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_animationBridge == null)
        {
            return;
        }

        _animationBridge.AttackActiveEvent += HandleAttackActiveEvent;
        _animationBridge.AttackEndEvent += HandleAttackEndEvent;
        _animationBridge.AttackAnimationFinished += HandleAttackEndEvent;
    }

    /// <summary>
    /// 비활성화 시 애니메이션 이벤트를 해제하고 런타임 상태를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_animationBridge != null)
        {
            _animationBridge.AttackActiveEvent -= HandleAttackActiveEvent;
            _animationBridge.AttackEndEvent -= HandleAttackEndEvent;
            _animationBridge.AttackAnimationFinished -= HandleAttackEndEvent;
        }

        ResetRuntime();
    }

    /// <summary>
    /// 매 프레임 공격 폴백 종료 조건을 점검합니다.
    /// </summary>
    public void Tick(float nowTime)
    {
        if (!_isAttacking || _isTerminalState)
        {
            return;
        }

        if (_attackStartedAt >= 0f && nowTime - _attackStartedAt >= _attackFallbackDuration)
        {
            if (!_hasWarnedFallback)
            {
                Debug.LogWarning($"[EnemyAttackController] Attack ended by fallback timeout on {name}. Add AnimationEvent_AttackEnd for precise sync.");
                _hasWarnedFallback = true;
            }

            HandleAttackEndEvent();
        }
    }

    /// <summary>
    /// 현재 시점에 공격 진입 가능 여부를 반환합니다.
    /// </summary>
    public bool CanStartAttack(float nowTime)
    {
        return !_isTerminalState && !_isAttacking && nowTime >= _nextAttackAllowedAt;
    }

    /// <summary>
    /// 외부 설정값으로 공격 쿨다운을 갱신합니다.
    /// </summary>
    public void SetAttackCooldown(float cooldown)
    {
        if (cooldown < 0f)
        {
            Debug.LogWarning($"[EnemyAttackController] Invalid cooldown({cooldown}) on {name}. Fallback to 0.");
            cooldown = 0f;
        }

        _attackCooldown = cooldown;
    }

    /// <summary>
    /// 사망/제거 등 터미널 상태 진입 여부를 설정합니다.
    /// </summary>
    public void SetTerminalState(bool isTerminal)
    {
        _isTerminalState = isTerminal;

        if (isTerminal)
        {
            ForceStopAttack();
        }
    }

    /// <summary>
    /// 공격 시작을 요청하고 쿨다운 타이머를 설정합니다.
    /// </summary>
    public bool TryStartAttack(float nowTime)
    {
        if (!CanStartAttack(nowTime))
        {
            return false;
        }

        _isAttacking = true;
        _isAttackAreaActive = false;
        _hasResolvedDamageThisSequence = false;
        _damagedTargetIds.Clear();
        _attackSequenceId++;

        _attackStartedAt = nowTime;
        _nextAttackAllowedAt = nowTime + _attackCooldown;
        _hasWarnedFallback = false;

        TriggerAttackIntentVisual();

        return true;
    }

    /// <summary>
    /// 공격 진행 상태를 강제로 종료합니다.
    /// </summary>
    public void ForceStopAttack()
    {
        _isAttacking = false;
        _isAttackAreaActive = false;
        _attackStartedAt = -1f;
        _hasResolvedDamageThisSequence = false;
        _damagedTargetIds.Clear();
    }

    /// <summary>
    /// 런타임 공격 상태를 초기화합니다.
    /// </summary>
    public void ResetRuntime()
    {
        _nextAttackAllowedAt = 0f;
        _attackStartedAt = -1f;
        _attackSequenceId = 0;
        _isAttacking = false;
        _isAttackAreaActive = false;
        _hasResolvedDamageThisSequence = false;
        _isTerminalState = false;
        _hasWarnedFallback = false;
        _damagedTargetIds.Clear();
    }

    /// <summary>
    /// 공격 시작 시각 의도를 로컬 Animator에 반영하고 필요 시 관찰자에게 복제합니다.
    /// </summary>
    private void TriggerAttackIntentVisual()
    {
        if (_animationBridge != null)
        {
            _animationBridge.TriggerAttackIntent();
        }
        else
        {
            Debug.LogWarning($"[EnemyAttackController] Missing EnemyAnimationBridge on {name}. Attack intent is not forwarded.");
        }

        if (EnemyNetworkAuthorityUtility.ShouldReplicateFromServer(_networkObject))
        {
            TriggerAttackIntentVisualRpc();
        }
    }

    /// <summary>
    /// 서버가 확정한 공격 트리거를 관찰자 인스턴스에 전달합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void TriggerAttackIntentVisualRpc()
    {
        _animationBridge?.TriggerAttackIntent();
    }

    /// <summary>
    /// 애니메이션 AttackActive 이벤트를 수신해 공격 범위를 활성화하고 판정을 1회 수행합니다.
    /// </summary>
    private void HandleAttackActiveEvent()
    {
        if (_isTerminalState)
        {
            if (_verboseEventLog)
            {
                Debug.LogWarning($"[EnemyAttackController] AttackActive ignored in terminal state on {name}.");
            }

            return;
        }

        if (!_isAttacking)
        {
            Debug.LogWarning($"[EnemyAttackController] AttackActive ignored because attack is not running on {name}.");
            return;
        }

        if (_isAttackAreaActive)
        {
            if (_verboseEventLog)
            {
                Debug.LogWarning($"[EnemyAttackController] Duplicate AttackActive event ignored on {name}.");
            }
        }

        _isAttackAreaActive = true;

        if (_hasResolvedDamageThisSequence)
        {
            return;
        }

        _hasResolvedDamageThisSequence = true;
        ResolveDamageByOverlap();
    }

    /// <summary>
    /// 애니메이션 AttackEnd 이벤트를 수신해 공격 범위를 비활성화하고 시퀀스를 종료합니다.
    /// </summary>
    private void HandleAttackEndEvent()
    {
        if (_isTerminalState)
        {
            return;
        }

        if (!_isAttacking)
        {
            if (_verboseEventLog)
            {
                Debug.LogWarning($"[EnemyAttackController] AttackEnd ignored because attack is not running on {name}.");
            }

            return;
        }

        _isAttackAreaActive = false;
        _isAttacking = false;
        _attackStartedAt = -1f;
        _damagedTargetIds.Clear();
        AttackSequenceCompleted?.Invoke();
    }

    /// <summary>
    /// ContactFilter2D + OverlapCircle 기반 즉시 판정으로 대상에게 HitRequest를 전달합니다.
    /// </summary>
    private void ResolveDamageByOverlap()
    {
        if (!CanExecuteAttackSimulation())
        {
            return;
        }

        Vector2 center = (Vector2)transform.position + _attackOffset;
        ContactFilter2D targetFilter = default; // 공격 대상 레이어/트리거 포함 규칙을 정의하는 물리 필터입니다.
        targetFilter.useLayerMask = true;
        targetFilter.layerMask = _targetLayerMask;
        targetFilter.useTriggers = true;

        int hitCount = Physics2D.OverlapCircle(center, _attackRadius, targetFilter, _overlapBuffer);
        int processedTargetCount = 0; // 현재 AttackActive 이벤트에서 실제 처리한 타겟 수입니다.

        for (int index = 0; index < hitCount; index++)
        {
            if (processedTargetCount >= _maxTargetsPerAttack)
            {
                break;
            }

            Collider2D targetCollider = _overlapBuffer[index];
            _overlapBuffer[index] = null;

            if (targetCollider == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(_targetTag) && !targetCollider.CompareTag(_targetTag))
            {
                continue;
            }

            HitReceiver receiver = targetCollider.GetComponent<HitReceiver>()
                                  ?? targetCollider.GetComponentInParent<HitReceiver>()
                                  ?? targetCollider.GetComponentInChildren<HitReceiver>();
            if (receiver == null)
            {
                continue;
            }

            int targetId = receiver.gameObject.GetInstanceID();
            if (_damagedTargetIds.Contains(targetId))
            {
                continue;
            }

            Vector3 hitDirection = (targetCollider.transform.position - transform.position).normalized;
            if (hitDirection.sqrMagnitude < 0.0001f)
            {
                hitDirection = Vector3.right;
            }

            string hitId = $"{name}_suicide_{_attackSequenceId}_{targetId}";
            HitRequest request = new HitRequest(
                hitId,
                _attackDamage,
                gameObject,
                targetCollider.transform.position,
                hitDirection,
                _statusTag,
                Time.time);

            HitResult result = receiver.ReceiveHit(request);
            if (result.IsAccepted)
            {
                _damagedTargetIds.Add(targetId);
                processedTargetCount++;
            }
        }
    }

    /// <summary>
    /// 현재 런타임 권한 정책 기준으로 공격 판정 실행 가능 여부를 판단합니다.
    /// </summary>
    private bool CanExecuteAttackSimulation()
    {
        if (!_executeOnlyOnServerWhenSpawned)
        {
            return true;
        }

        if (_networkObject == null)
        {
            _networkObject = GetComponent<NetworkObject>();
        }

        bool canExecute = EnemyNetworkAuthorityUtility.ShouldRunServerAuthoritativeLogic(_networkObject);
        if (!canExecute && _warnWhenNetworkAuthorityUnavailable)
        {
            Debug.LogWarning($"[EnemyAttackController] Observer instance skipped attack simulation. object={name}", this);
        }

        return canExecute;
    }

    /// <summary>
    /// 공격 관련 설정값을 보정하고 누락 참조를 경고합니다.
    /// </summary>
    private void ValidateAndClampSettings()
    {
        if (_attackCooldown < 0f)
        {
            Debug.LogWarning($"[EnemyAttackController] Invalid _attackCooldown({_attackCooldown}) on {name}. Fallback to 0.");
            _attackCooldown = 0f;
        }

        if (_attackFallbackDuration <= 0f)
        {
            Debug.LogWarning($"[EnemyAttackController] Invalid _attackFallbackDuration({_attackFallbackDuration}) on {name}. Fallback to 0.3.");
            _attackFallbackDuration = 0.3f;
        }

        if (_attackDamage <= 0f)
        {
            Debug.LogWarning($"[EnemyAttackController] Invalid _attackDamage({_attackDamage}) on {name}. Fallback to 1.");
            _attackDamage = 1f;
        }

        if (_attackRadius <= 0f)
        {
            Debug.LogWarning($"[EnemyAttackController] Invalid _attackRadius({_attackRadius}) on {name}. Fallback to 0.1.");
            _attackRadius = 0.1f;
        }

        if (_maxTargetsPerAttack <= 0)
        {
            Debug.LogWarning($"[EnemyAttackController] Invalid _maxTargetsPerAttack({_maxTargetsPerAttack}) on {name}. Fallback to 1.");
            _maxTargetsPerAttack = 1;
        }

        if (_maxTargetsPerAttack > 2)
        {
            Debug.LogWarning($"[EnemyAttackController] _maxTargetsPerAttack({_maxTargetsPerAttack}) exceeds recommended max(2) for this project.");
        }

        if (_animationBridge == null)
        {
            Debug.LogWarning($"[EnemyAttackController] EnemyAnimationBridge is not assigned on {name}. Animation events will not be received.");
        }
    }
}
