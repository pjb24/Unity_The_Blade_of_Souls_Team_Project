using System.Collections;
using UnityEngine;

/// <summary>
/// 체력 이벤트를 로그로 출력하는 디버그용 리스너입니다.
/// </summary>
public class HealthDebugListener : MonoBehaviour, IHealthListener
{
    [SerializeField] private HealthComponent _targetHealth; // 구독할 HealthComponent 참조

    private bool _isRegistered;
    private Coroutine _registerCoroutine;

    /// <summary>
    /// 대상 HealthComponent를 찾아 초기화 완료까지 기다린 뒤 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_targetHealth == null)
        {
            _targetHealth = GetComponent<HealthComponent>();
        }

        if (_targetHealth == null)
        {
            Debug.LogWarning($"[HealthDebugListener] Target HealthComponent not found on {name}.");
            return;
        }

        StartRegisterRoutine();
    }

    /// <summary>
    /// 대상 HealthComponent에서 등록을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRegisterRoutine();
        UnregisterListener();
    }

    /// <summary>
    /// 등록 대기 루틴을 시작합니다.
    /// </summary>
    private void StartRegisterRoutine()
    {
        StopRegisterRoutine();
        _registerCoroutine = StartCoroutine(Co_RegisterWhenReady());
    }

    /// <summary>
    /// 등록 대기 루틴을 중단합니다.
    /// </summary>
    private void StopRegisterRoutine()
    {
        if (_registerCoroutine == null)
        {
            return;
        }

        StopCoroutine(_registerCoroutine);
        _registerCoroutine = null;
    }

    /// <summary>
    /// 대상 초기화 완료까지 기다린 뒤 리스너를 등록합니다.
    /// </summary>
    private IEnumerator Co_RegisterWhenReady()
    {
        if (_targetHealth == null)
        {
            Debug.LogWarning($"[HealthDebugListener] Target HealthComponent missing on {name}.");
            _registerCoroutine = null;
            yield break;
        }

        while (_targetHealth != null && _targetHealth.IsInitialized == false)
        {
            yield return null;
        }

        if (_targetHealth == null)
        {
            Debug.LogWarning($"[HealthDebugListener] Target destroyed while waiting initialization on {name}.");
            _registerCoroutine = null;
            yield break;
        }

        RegisterListenerInternal();
        _registerCoroutine = null;
    }

    /// <summary>
    /// 실제 리스너 등록을 수행합니다.
    /// </summary>
    private void RegisterListenerInternal()
    {
        if (_isRegistered)
        {
            Debug.LogWarning($"[HealthDebugListener] Listener already registered on {name}.");
            return;
        }

        if (_targetHealth == null)
        {
            Debug.LogWarning($"[HealthDebugListener] Target missing while registering on {name}.");
            return;
        }

        if (_targetHealth.IsInitialized == false)
        {
            Debug.LogWarning($"[HealthDebugListener] Target not initialized while registering on {name}.");
            return;
        }

        _targetHealth.AddListener(this);
        _isRegistered = true;
    }

    /// <summary>
    /// 리스너를 해제합니다.
    /// </summary>
    private void UnregisterListener()
    {
        if (_isRegistered == false)
        {
            return;
        }

        if (_targetHealth == null)
        {
            _isRegistered = false;
            return;
        }

        if (_targetHealth.IsInitialized == false)
        {
            _isRegistered = false;
            return;
        }

        _targetHealth.RemoveListener(this);
        _isRegistered = false;
    }

    /// <summary>
    /// 체력 수치 변경 로그를 출력합니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
        Debug.Log($"[HealthDebugListener] {name} HealthChanged prev:{data.PreviousHealth} current:{data.CurrentHealth} max:{data.MaxHealth} normalized:{data.NormalizedHealth:0.00} delta:{data.Delta}");
    }

    /// <summary>
    /// 데미지 적용 결과 로그를 출력합니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
        Debug.Log($"[HealthDebugListener] {name} Damaged applied:{result.AppliedAmount} prev:{result.PreviousHealth} current:{result.CurrentHealth} blocked:{result.IsBlocked} invalid:{result.IsInvalid} died:{result.DidCauseDeath} fallback:{result.HasWarningFallback}");
    }

    /// <summary>
    /// 회복 적용 결과 로그를 출력합니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
        Debug.Log($"[HealthDebugListener] {name} Healed applied:{result.AppliedAmount} prev:{result.PreviousHealth} current:{result.CurrentHealth} blocked:{result.IsBlocked} invalid:{result.IsInvalid} revived:{result.DidRevive} fallback:{result.HasWarningFallback}");
    }

    /// <summary>
    /// 사망 로그를 출력합니다.
    /// </summary>
    public void OnDied()
    {
        Debug.Log($"[HealthDebugListener] {name} Died");
    }

    /// <summary>
    /// 부활 로그를 출력합니다.
    /// </summary>
    public void OnRevived()
    {
        Debug.Log($"[HealthDebugListener] {name} Revived");
    }

    /// <summary>
    /// 최대 체력 변경 로그를 출력합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
        Debug.Log($"[HealthDebugListener] {name} MaxHealthChanged prev:{previousMaxHealth} current:{currentMaxHealth}");
    }
}
