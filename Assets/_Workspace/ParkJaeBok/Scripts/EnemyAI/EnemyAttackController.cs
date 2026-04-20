using UnityEngine;

/// <summary>
/// 공격 가능 여부, 쿨다운, 공격 요청, 공격 종료 신호를 관리하는 공격 컨트롤러입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAttackController : MonoBehaviour
{
    [Header("Attack Runtime")]
    [Tooltip("공격 쿨다운 시간(초)입니다.")]
    [SerializeField] private float _attackCooldown = 1.2f; // 공격 쿨다운 시간 설정 값입니다.
    [Tooltip("애니메이션 종료 이벤트가 없을 때 사용할 공격 최대 지속 시간(초)입니다.")]
    [SerializeField] private float _attackFallbackDuration = 0.6f; // 공격 종료 이벤트 누락 시 폴백 종료 시간입니다.

    [Header("References")]
    [Tooltip("공격 의도/애니메이션 전달 브리지 참조입니다.")]
    [SerializeField] private EnemyAnimationBridge _animationBridge; // 공격 의도 전달용 애니메이션 브리지 참조입니다.

    private float _nextAttackAllowedAt; // 다음 공격 가능 시각입니다.
    private float _attackStartedAt = -1f; // 현재 공격 시작 시각입니다.
    private bool _isAttacking; // 현재 공격 진행 여부입니다.
    private bool _hasWarnedFallback; // 공격 종료 이벤트 폴백 경고 출력 여부입니다.

    /// <summary>
    /// 현재 공격 진행 여부를 반환합니다.
    /// </summary>
    public bool IsAttacking => _isAttacking;

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
    }

    /// <summary>
    /// 활성화 시 애니메이션 종료 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_animationBridge != null)
        {
            _animationBridge.AttackAnimationFinished += HandleAttackFinished;
        }
    }

    /// <summary>
    /// 비활성화 시 애니메이션 종료 이벤트를 해제하고 런타임 상태를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_animationBridge != null)
        {
            _animationBridge.AttackAnimationFinished -= HandleAttackFinished;
        }

        ResetRuntime();
    }

    /// <summary>
    /// 매 프레임 공격 폴백 종료 조건을 점검합니다.
    /// </summary>
    public void Tick(float nowTime)
    {
        if (!_isAttacking)
        {
            return;
        }

        if (_attackStartedAt >= 0f && nowTime - _attackStartedAt >= _attackFallbackDuration)
        {
            if (!_hasWarnedFallback)
            {
                Debug.LogWarning($"[EnemyAttackController] Attack finished by fallback timeout on {name}. Add AnimationEvent_AttackFinished for precise sync.");
                _hasWarnedFallback = true;
            }

            HandleAttackFinished();
        }
    }

    /// <summary>
    /// 현재 시점에 공격 진입 가능 여부를 반환합니다.
    /// </summary>
    public bool CanStartAttack(float nowTime)
    {
        return !_isAttacking && nowTime >= _nextAttackAllowedAt;
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
    /// 공격 시작을 요청하고 쿨다운 타이머를 설정합니다.
    /// </summary>
    public bool TryStartAttack(float nowTime)
    {
        if (!CanStartAttack(nowTime))
        {
            return false;
        }

        _isAttacking = true;
        _attackStartedAt = nowTime;
        _nextAttackAllowedAt = nowTime + _attackCooldown;

        if (_animationBridge != null)
        {
            _animationBridge.TriggerAttackIntent();
        }
        else
        {
            Debug.LogWarning($"[EnemyAttackController] Missing EnemyAnimationBridge on {name}. Attack intent is not forwarded.");
        }

        return true;
    }

    /// <summary>
    /// 공격 진행 상태를 강제로 종료합니다.
    /// </summary>
    public void ForceStopAttack()
    {
        _isAttacking = false;
        _attackStartedAt = -1f;
    }

    /// <summary>
    /// 런타임 공격 상태를 초기화합니다.
    /// </summary>
    public void ResetRuntime()
    {
        _nextAttackAllowedAt = 0f;
        _attackStartedAt = -1f;
        _isAttacking = false;
    }

    /// <summary>
    /// 애니메이션 종료 신호를 받아 공격 상태를 종료합니다.
    /// </summary>
    private void HandleAttackFinished()
    {
        _isAttacking = false;
        _attackStartedAt = -1f;
    }
}
