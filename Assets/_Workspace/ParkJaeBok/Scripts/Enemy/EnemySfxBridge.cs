using System.Collections;
using UnityEngine;

/// <summary>
/// Action/Hit/Health 이벤트를 SfxOrchestrator 요청으로 변환하는 Enemy 전용 오디오 브리지입니다.
/// </summary>
public class EnemySfxBridge : MonoBehaviour, IActionListener, IHitListener, IHealthListener
{
    [Header("References")]
    [Tooltip("액션 이벤트를 구독할 ActionController 참조입니다.")]
    [SerializeField] private ActionController _actionController; // 액션 이벤트를 구독할 ActionController 참조입니다.
    [Tooltip("피격 결과를 구독할 HitReceiver 참조입니다.")]
    [SerializeField] private HitReceiver _hitReceiver; // 피격 결과를 구독할 HitReceiver 참조입니다.
    [Tooltip("사망 이벤트를 구독할 HealthComponent 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // 사망 이벤트를 구독할 HealthComponent 참조입니다.
    [Tooltip("실제 사운드 재생 요청을 전달할 오케스트레이터 참조입니다.")]
    [SerializeField] private SfxOrchestrator _sfxOrchestrator; // 실제 사운드 재생 요청을 전달할 오케스트레이터 참조입니다.

    [Header("Register Retry")]
    [Tooltip("참조 지연 준비 상황에서 등록/해제 재시도 간격(초)입니다.")]
    [SerializeField] private float _retryInterval = 0.1f; // 참조 지연 준비 상황에서 등록/해제 재시도 간격(초)입니다.
    [Tooltip("등록/해제 재시도 최대 횟수입니다.")]
    [SerializeField] private int _maxRetryCount = 30; // 등록/해제 재시도 최대 횟수입니다.

    [Header("Sfx Route")]
    [Tooltip("공격 시작 시 송신할 SFX 이벤트 타입입니다.")]
    [SerializeField] private E_SfxEventType _attackEventType = E_SfxEventType.AttackSwing; // 공격 시작 시 송신할 SFX 이벤트 타입입니다.
    [Tooltip("피격 수락 시 송신할 SFX 이벤트 타입입니다.")]
    [SerializeField] private E_SfxEventType _hitEventType = E_SfxEventType.HitConfirm; // 피격 수락 시 송신할 SFX 이벤트 타입입니다.
    [Tooltip("공격 시작 시 라우팅에 사용할 SFX 서브 타입 키입니다.")]
    [SerializeField] private string _attackSubType = "enemy_attack"; // 공격 시작 시 라우팅에 사용할 SFX 서브 타입 키입니다.
    [Tooltip("피격 수락 시 라우팅에 사용할 SFX 서브 타입 키입니다.")]
    [SerializeField] private string _hitSubType = "enemy_hit"; // 피격 수락 시 라우팅에 사용할 SFX 서브 타입 키입니다.
    [Tooltip("사망 시 라우팅에 사용할 SFX 서브 타입 키입니다.")]
    [SerializeField] private string _deathSubType = "enemy_die"; // 사망 시 라우팅에 사용할 SFX 서브 타입 키입니다.

    private Coroutine _registerCoroutine; // 지연 등록 처리 코루틴 핸들입니다.
    private Coroutine _unregisterCoroutine; // 지연 해제 처리 코루틴 핸들입니다.

    private bool _isActionSubscribed; // ActionController 리스너 등록 여부입니다.
    private bool _isHitSubscribed; // HitReceiver 리스너 등록 여부입니다.
    private bool _isHealthSubscribed; // HealthComponent 리스너 등록 여부입니다.

    /// <summary>
    /// 활성화 시 리스너 등록 코루틴을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        RestartRegisterCoroutine();
    }

    /// <summary>
    /// 비활성화 시 리스너 해제 코루틴을 시작합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        StopRunningCoroutine(ref _unregisterCoroutine);
        TryUnregisterListenersImmediately();
    }

    /// <summary>
    /// 파괴 시 코루틴을 정리하고 마지막으로 리스너 해제를 시도합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        StopRunningCoroutine(ref _unregisterCoroutine);

        if (_actionController != null)
        {
            _actionController.RemoveListener(this);
            _isActionSubscribed = false;
        }

        if (_hitReceiver != null)
        {
            _hitReceiver.RemoveListener(this);
            _isHitSubscribed = false;
        }

        if (_healthComponent != null)
        {
            _healthComponent.RemoveListener(this);
            _isHealthSubscribed = false;
        }
    }

    /// <summary>
    /// 액션 시작 이벤트를 받아 공격 계열 액션이면 공격 SFX를 송신합니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
        if (!IsAttackAction(runtime.ActionType))
        {
            return;
        }

        RequestSfx(_attackEventType, _attackSubType);
    }

    /// <summary>
    /// 액션 단계 변경 이벤트는 현재 구현에서 사용하지 않습니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
    }

    /// <summary>
    /// 액션 완료 이벤트는 현재 구현에서 사용하지 않습니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
    }

    /// <summary>
    /// 액션 취소 이벤트는 현재 구현에서 사용하지 않습니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
    }

    /// <summary>
    /// 피격 처리 결과를 받아 수락된 경우 피격 SFX를 송신합니다.
    /// </summary>
    public void OnHitResolved(HitRequest request, HitResult result)
    {
        if (!result.IsAccepted)
        {
            return;
        }

        RequestSfx(_hitEventType, _hitSubType);

        if (result.IsDeadAfter)
        {
            RequestSfx(_hitEventType, _deathSubType);
        }
    }

    /// <summary>
    /// 체력 변경 이벤트는 현재 구현에서 사용하지 않습니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
    }

    /// <summary>
    /// 데미지 적용 이벤트는 현재 구현에서 사용하지 않습니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// 회복 적용 이벤트는 현재 구현에서 사용하지 않습니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 사망 이벤트를 받아 사망 SFX를 송신합니다.
    /// </summary>
    public void OnDied()
    {
        RequestSfx(_hitEventType, _deathSubType);
    }

    /// <summary>
    /// 부활 이벤트는 현재 구현에서 사용하지 않습니다.
    /// </summary>
    public void OnRevived()
    {
    }

    /// <summary>
    /// 최대 체력 변경 이벤트는 현재 구현에서 사용하지 않습니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }

    /// <summary>
    /// 등록 코루틴을 재시작합니다.
    /// </summary>
    private void RestartRegisterCoroutine()
    {
        StopRunningCoroutine(ref _unregisterCoroutine);
        StopRunningCoroutine(ref _registerCoroutine);
        _registerCoroutine = StartCoroutine(RegisterListenerWithRetryCoroutine());
    }

    /// <summary>
    /// 해제 코루틴을 재시작합니다.
    /// </summary>
    private void RestartUnregisterCoroutine()
    {
        StopRunningCoroutine(ref _unregisterCoroutine);
        if (!isActiveAndEnabled)
        {
            TryUnregisterListenersImmediately();
            return;
        }

        _unregisterCoroutine = StartCoroutine(UnregisterListenerWithRetryCoroutine());
    }

    /// <summary>
    /// 대상 컴포넌트가 준비될 때까지 재시도한 뒤 리스너 등록을 수행합니다.
    /// </summary>
    private IEnumerator RegisterListenerWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값입니다.
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값입니다.

        if (_maxRetryCount < 1 || _retryInterval <= 0f)
        {
            Debug.LogWarning($"[EnemySfxBridge] Invalid retry settings on {name}. Fallback maxRetry={safeMaxRetry}, interval={safeInterval}.", this);
        }

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            bool didRegisterAny = false; // 이번 재시도 프레임에서 하나 이상 등록 성공했는지 여부입니다.
            bool allResolved = true; // 활성 구독 대상 참조가 모두 해석되었는지 여부입니다.

            if (!_isActionSubscribed)
            {
                if (TryResolveActionControllerReference())
                {
                    _actionController.AddListener(this);
                    _isActionSubscribed = true;
                    didRegisterAny = true;
                }
                else
                {
                    allResolved = false;
                }
            }

            if (!_isHitSubscribed)
            {
                if (TryResolveHitReceiverReference())
                {
                    _hitReceiver.AddListener(this);
                    _isHitSubscribed = true;
                    didRegisterAny = true;
                }
                else
                {
                    allResolved = false;
                }
            }

            if (!_isHealthSubscribed)
            {
                if (TryResolveHealthComponentReference())
                {
                    _healthComponent.AddListener(this);
                    _isHealthSubscribed = true;
                    didRegisterAny = true;
                }
                else
                {
                    allResolved = false;
                }
            }

            TryResolveSfxOrchestratorReference();

            if (_isActionSubscribed && _isHitSubscribed && _isHealthSubscribed)
            {
                _registerCoroutine = null;
                yield break;
            }

            if (allResolved && didRegisterAny)
            {
                _registerCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[EnemySfxBridge] Some listener targets are null on {name}. Delaying AddListener registration.", this);
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[EnemySfxBridge] AddListener registration finished with unresolved references on {name}.", this);
        _registerCoroutine = null;
    }

    /// <summary>
    /// 대상 컴포넌트가 준비될 때까지 재시도한 뒤 리스너 해제를 수행합니다.
    /// </summary>
    private IEnumerator UnregisterListenerWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값입니다.
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값입니다.

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            bool hasPendingUnregister = false; // 아직 해제가 필요한 리스너가 남아 있는지 여부입니다.

            if (_isActionSubscribed)
            {
                hasPendingUnregister = true;
                if (TryResolveActionControllerReference())
                {
                    _actionController.RemoveListener(this);
                    _isActionSubscribed = false;
                }
            }

            if (_isHitSubscribed)
            {
                hasPendingUnregister = true;
                if (TryResolveHitReceiverReference())
                {
                    _hitReceiver.RemoveListener(this);
                    _isHitSubscribed = false;
                }
            }

            if (_isHealthSubscribed)
            {
                hasPendingUnregister = true;
                if (TryResolveHealthComponentReference())
                {
                    _healthComponent.RemoveListener(this);
                    _isHealthSubscribed = false;
                }
            }

            if (!hasPendingUnregister || (!_isActionSubscribed && !_isHitSubscribed && !_isHealthSubscribed))
            {
                _unregisterCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[EnemySfxBridge] Some listener targets are null on {name}. Delaying RemoveListener unregistration.", this);
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[EnemySfxBridge] RemoveListener unregistration finished with unresolved references on {name}.", this);
        _unregisterCoroutine = null;
    }

    /// <summary>
    /// 비활성 상태에서도 코루틴 없이 즉시 리스너 해제를 시도하고 구독 플래그를 정리합니다.
    /// </summary>
    private void TryUnregisterListenersImmediately()
    {
        if (_isActionSubscribed)
        {
            if (_actionController != null || TryResolveActionControllerReference())
            {
                _actionController.RemoveListener(this);
            }

            _isActionSubscribed = false;
        }

        if (_isHitSubscribed)
        {
            if (_hitReceiver != null || TryResolveHitReceiverReference())
            {
                _hitReceiver.RemoveListener(this);
            }

            _isHitSubscribed = false;
        }

        if (_isHealthSubscribed)
        {
            if (_healthComponent != null || TryResolveHealthComponentReference())
            {
                _healthComponent.RemoveListener(this);
            }

            _isHealthSubscribed = false;
        }
    }

    /// <summary>
    /// 액션 타입이 공격 계열인지 판정합니다.
    /// </summary>
    private bool IsAttackAction(E_ActionType actionType)
    {
        return actionType == E_ActionType.Attack ||
               actionType == E_ActionType.AttackCombo1 ||
               actionType == E_ActionType.AttackCombo2 ||
               actionType == E_ActionType.AttackCombo3 ||
               actionType == E_ActionType.AttackAir ||
               actionType == E_ActionType.AttackDash ||
               actionType == E_ActionType.AttackWall;
    }

    /// <summary>
    /// 오케스트레이터가 준비된 경우 지정 이벤트로 SFX 재생을 요청합니다.
    /// </summary>
    private void RequestSfx(E_SfxEventType eventType, string subType)
    {
        if (_sfxOrchestrator == null)
        {
            TryResolveSfxOrchestratorReference();
        }

        if (_sfxOrchestrator == null)
        {
            return;
        }

        _sfxOrchestrator.Request(eventType, subType, transform);
    }

    /// <summary>
    /// ActionController 참조가 비어 있을 때 동일 오브젝트 fallback으로 보정합니다.
    /// </summary>
    private bool TryResolveActionControllerReference()
    {
        if (_actionController != null)
        {
            return true;
        }

        _actionController = GetComponent<ActionController>();
        if (_actionController != null)
        {
            Debug.LogWarning($"[EnemySfxBridge] _actionController was null on {name}. Fallback to same GameObject ActionController.", this);
            return true;
        }

        return false;
    }

    /// <summary>
    /// HitReceiver 참조가 비어 있을 때 동일 오브젝트 fallback으로 보정합니다.
    /// </summary>
    private bool TryResolveHitReceiverReference()
    {
        if (_hitReceiver != null)
        {
            return true;
        }

        _hitReceiver = GetComponent<HitReceiver>();
        if (_hitReceiver != null)
        {
            Debug.LogWarning($"[EnemySfxBridge] _hitReceiver was null on {name}. Fallback to same GameObject HitReceiver.", this);
            return true;
        }

        return false;
    }

    /// <summary>
    /// HealthComponent 참조가 비어 있을 때 동일 오브젝트 fallback으로 보정합니다.
    /// </summary>
    private bool TryResolveHealthComponentReference()
    {
        if (_healthComponent != null)
        {
            return true;
        }

        _healthComponent = GetComponent<HealthComponent>();
        if (_healthComponent != null)
        {
            Debug.LogWarning($"[EnemySfxBridge] _healthComponent was null on {name}. Fallback to same GameObject HealthComponent.", this);
            return true;
        }

        return false;
    }

    /// <summary>
    /// SfxOrchestrator 참조가 비어 있을 때 씬에서 fallback 탐색을 시도합니다.
    /// </summary>
    private bool TryResolveSfxOrchestratorReference()
    {
        if (_sfxOrchestrator != null)
        {
            return true;
        }

        _sfxOrchestrator = FindAnyObjectByType<SfxOrchestrator>();
        if (_sfxOrchestrator != null)
        {
            Debug.LogWarning($"[EnemySfxBridge] _sfxOrchestrator was null on {name}. Fallback to scene SfxOrchestrator.", this);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 실행 중인 코루틴을 안전하게 중지하고 핸들을 정리합니다.
    /// </summary>
    private void StopRunningCoroutine(ref Coroutine coroutineHandle)
    {
        if (coroutineHandle == null)
        {
            return;
        }

        StopCoroutine(coroutineHandle);
        coroutineHandle = null;
    }
}
