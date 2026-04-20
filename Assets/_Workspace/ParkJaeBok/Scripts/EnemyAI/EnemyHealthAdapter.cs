using System;
using UnityEngine;

/// <summary>
/// HealthComponent 이벤트를 Enemy AI 친화적인 시그널로 변환하는 어댑터입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealthAdapter : MonoBehaviour, IHealthListener
{
    [Tooltip("Enemy 생존 판정과 이벤트 구독에 사용할 HealthComponent 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // Enemy 체력 컴포넌트 참조입니다.

    /// <summary>
    /// 데미지 수신 이벤트입니다.
    /// </summary>
    public event Action Damaged;

    /// <summary>
    /// 사망 이벤트입니다.
    /// </summary>
    public event Action Died;

    /// <summary>
    /// 부활 이벤트입니다.
    /// </summary>
    public event Action Revived;

    /// <summary>
    /// 현재 생존 상태를 반환합니다.
    /// </summary>
    public bool IsAlive => _healthComponent != null && !_healthComponent.IsDead;

    /// <summary>
    /// HealthComponent를 자동 연결합니다.
    /// </summary>
    private void Awake()
    {
        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }

        if (_healthComponent == null)
        {
            Debug.LogWarning($"[EnemyHealthAdapter] Missing HealthComponent on {name}. Death/Hit state transitions will not work.");
        }
    }

    /// <summary>
    /// 활성화 시 HealthComponent 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_healthComponent == null)
        {
            return;
        }

        _healthComponent.AddListener(this);
    }

    /// <summary>
    /// 비활성화 시 HealthComponent 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_healthComponent == null)
        {
            return;
        }

        _healthComponent.RemoveListener(this);
    }

    /// <summary>
    /// 체력 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
    }

    /// <summary>
    /// 데미지 이벤트를 수신합니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
        Damaged?.Invoke();
    }

    /// <summary>
    /// 회복 이벤트를 수신합니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 사망 이벤트를 수신합니다.
    /// </summary>
    public void OnDied()
    {
        Died?.Invoke();
    }

    /// <summary>
    /// 부활 이벤트를 수신합니다.
    /// </summary>
    public void OnRevived()
    {
        Revived?.Invoke();
    }

    /// <summary>
    /// 최대 체력 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }
}
