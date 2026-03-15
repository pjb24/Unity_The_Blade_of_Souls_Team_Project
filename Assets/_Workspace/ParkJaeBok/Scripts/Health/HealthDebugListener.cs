using UnityEngine;

/// <summary>
/// 체력 이벤트를 로그로 출력하는 디버그용 리스너입니다.
/// </summary>
public class HealthDebugListener : MonoBehaviour, IHealthListener
{
    [SerializeField] private HealthComponent _targetHealth; // 구독할 HealthComponent 참조

    /// <summary>
    /// 대상 HealthComponent를 찾아 등록합니다.
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

        _targetHealth.AddListener(this);
    }

    /// <summary>
    /// 대상 HealthComponent에서 등록을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_targetHealth == null)
        {
            return;
        }

        _targetHealth.RemoveListener(this);
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
