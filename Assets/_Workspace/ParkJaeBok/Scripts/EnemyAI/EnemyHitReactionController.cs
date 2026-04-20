using System;
using UnityEngine;

/// <summary>
/// 피격 반응 상태의 지속 시간/애니메이션 종료/연속 피격 갱신 정책을 관리하는 컨트롤러입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHitReactionController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("피격 애니메이션 종료 이벤트를 수신할 브리지 참조입니다.")]
    [SerializeField] private EnemyAnimationBridge _animationBridge; // 피격 애니메이션 종료 이벤트 수신용 브리지 참조입니다.
    [Tooltip("피격 시 공격 중단 제어를 위해 사용할 공격 컨트롤러 참조입니다.")]
    [SerializeField] private EnemyAttackController _attackController; // 피격 시 공격 중단을 담당하는 공격 컨트롤러 참조입니다.

    [Header("Hit Reaction Policy")]
    [Tooltip("피격 상태 최소 유지 시간(초)입니다.")]
    [SerializeField] private float _minimumHitLockDuration = 0.15f; // 피격 상태 최소 유지 시간입니다.
    [Tooltip("피격 종료를 애니메이션 이벤트로 우선 판단할지 여부입니다.")]
    [SerializeField] private bool _useAnimationEventAsPrimaryExit = true; // 피격 종료에 애니메이션 이벤트를 우선 사용할지 여부입니다.
    [Tooltip("애니메이션 이벤트가 오지 않을 때 강제 종료할 최대 시간(초)입니다.")]
    [SerializeField] private float _maxHitReactionDuration = 0.7f; // 피격 최대 지속 시간(타임아웃)입니다.
    [Tooltip("피격 중 추가 피격이 들어오면 종료 시점을 갱신할지 여부입니다.")]
    [SerializeField] private bool _allowHitChainRefresh = true; // 연속 피격 시 종료 시점 갱신 허용 여부입니다.
    [Tooltip("피격 진입 시 진행 중인 공격을 강제 종료할지 여부입니다.")]
    [SerializeField] private bool _interruptAttackOnHit = true; // 피격 시 공격 중단 여부입니다.
    [Tooltip("애니메이션 이벤트 종료를 사용하지만 브리지가 없을 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenAnimationBridgeMissing = true; // 브리지 누락 경고 출력 여부입니다.

    private bool _isInHitReaction; // 현재 피격 상태 진행 여부입니다.
    private float _hitStartedAt = -1f; // 피격 상태 시작 시각입니다.
    private float _minimumUnlockAt = -1f; // 최소 유지 시간 종료 시각입니다.
    private float _forceExitAt = -1f; // 강제 종료 시각입니다.
    private bool _receivedAnimationFinishEvent; // 현재 피격 사이클에서 종료 이벤트 수신 여부입니다.

    /// <summary>
    /// 피격 상태 진입 이벤트입니다.
    /// </summary>
    public event Action HitReactionStarted;

    /// <summary>
    /// 피격 상태 종료 이벤트입니다.
    /// </summary>
    public event Action HitReactionCompleted;

    /// <summary>
    /// 현재 피격 상태 여부를 반환합니다.
    /// </summary>
    public bool IsInHitReaction => _isInHitReaction;

    /// <summary>
    /// 초기 의존성 연결과 설정 검증을 수행합니다.
    /// </summary>
    private void Awake()
    {
        if (_animationBridge == null)
        {
            _animationBridge = GetComponent<EnemyAnimationBridge>();
        }

        if (_attackController == null)
        {
            _attackController = GetComponent<EnemyAttackController>();
        }

        ValidatePolicyValues();
    }

    /// <summary>
    /// 활성화 시 애니메이션 종료 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_animationBridge != null)
        {
            _animationBridge.HitReactionFinished += HandleAnimationHitReactionFinished;
        }
        else if (_useAnimationEventAsPrimaryExit && _warnWhenAnimationBridgeMissing)
        {
            Debug.LogWarning($"[EnemyHitReactionController] AnimationBridge is missing on {name}. HitReaction will fallback to timeout.");
        }

        ResetRuntime();
    }

    /// <summary>
    /// 비활성화 시 이벤트 구독을 해제하고 런타임 상태를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_animationBridge != null)
        {
            _animationBridge.HitReactionFinished -= HandleAnimationHitReactionFinished;
        }

        ResetRuntime();
    }

    /// <summary>
    /// 에디터 값 변경 시 정책 유효성을 검사합니다.
    /// </summary>
    private void OnValidate()
    {
        ValidatePolicyValues();
    }

    /// <summary>
    /// 피격 상태 진입을 요청합니다.
    /// </summary>
    public void RequestHitReaction(float nowTime)
    {
        if (_isInHitReaction && !_allowHitChainRefresh)
        {
            return;
        }

        if (_interruptAttackOnHit && _attackController != null)
        {
            _attackController.ForceStopAttack();
        }

        if (_interruptAttackOnHit && _attackController == null)
        {
            Debug.LogWarning($"[EnemyHitReactionController] _interruptAttackOnHit is enabled but EnemyAttackController is missing on {name}.");
        }

        _isInHitReaction = true;
        _hitStartedAt = nowTime;
        _minimumUnlockAt = nowTime + _minimumHitLockDuration;
        _forceExitAt = nowTime + _maxHitReactionDuration;
        _receivedAnimationFinishEvent = false;

        HitReactionStarted?.Invoke();
    }

    /// <summary>
    /// 피격 상태 종료 조건을 매 프레임 평가합니다.
    /// </summary>
    public void Tick(float nowTime)
    {
        if (!_isInHitReaction)
        {
            return;
        }

        bool minimumLockExpired = nowTime >= _minimumUnlockAt;
        bool timeoutExpired = nowTime >= _forceExitAt;
        bool animationFinished = !_useAnimationEventAsPrimaryExit || _receivedAnimationFinishEvent;

        if (!minimumLockExpired)
        {
            return;
        }

        if (animationFinished || timeoutExpired)
        {
            if (timeoutExpired && _useAnimationEventAsPrimaryExit)
            {
                Debug.LogWarning($"[EnemyHitReactionController] HitReaction ended by timeout on {name}. Add AnimationEvent_HitReactionFinished for precise sync.");
            }

            CompleteHitReaction();
        }
    }

    /// <summary>
    /// 런타임 상태를 초기화합니다.
    /// </summary>
    public void ResetRuntime()
    {
        _isInHitReaction = false;
        _hitStartedAt = -1f;
        _minimumUnlockAt = -1f;
        _forceExitAt = -1f;
        _receivedAnimationFinishEvent = false;
    }

    /// <summary>
    /// 피격 상태를 강제로 종료합니다.
    /// </summary>
    public void ForceComplete()
    {
        if (!_isInHitReaction)
        {
            return;
        }

        CompleteHitReaction();
    }

    /// <summary>
    /// 애니메이션 피격 종료 이벤트를 수신합니다.
    /// </summary>
    private void HandleAnimationHitReactionFinished()
    {
        _receivedAnimationFinishEvent = true;
    }

    /// <summary>
    /// 피격 상태를 종료 처리하고 이벤트를 알립니다.
    /// </summary>
    private void CompleteHitReaction()
    {
        _isInHitReaction = false;
        HitReactionCompleted?.Invoke();
    }

    /// <summary>
    /// 정책 설정값의 유효성을 검사합니다.
    /// </summary>
    private void ValidatePolicyValues()
    {
        if (_minimumHitLockDuration < 0f)
        {
            Debug.LogWarning($"[EnemyHitReactionController] Invalid _minimumHitLockDuration({_minimumHitLockDuration}) on {name}. Fallback to 0.");
            _minimumHitLockDuration = 0f;
        }

        if (_maxHitReactionDuration <= 0f)
        {
            Debug.LogWarning($"[EnemyHitReactionController] Invalid _maxHitReactionDuration({_maxHitReactionDuration}) on {name}. Fallback to 0.1.");
            _maxHitReactionDuration = 0.1f;
        }

        if (_maxHitReactionDuration < _minimumHitLockDuration)
        {
            Debug.LogWarning($"[EnemyHitReactionController] _maxHitReactionDuration({_maxHitReactionDuration}) is smaller than _minimumHitLockDuration({_minimumHitLockDuration}) on {name}.");
        }
    }
}
