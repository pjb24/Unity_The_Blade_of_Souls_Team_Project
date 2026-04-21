using System;
using UnityEngine;

/// <summary>
/// HealthComponent 이벤트를 Enemy AI 친화적인 시그널로 변환하고 피격 가능 여부를 일원화하는 어댑터입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealthAdapter : MonoBehaviour, IHealthListener
{
    [Tooltip("Enemy 생존 판정과 이벤트 구독에 사용할 HealthComponent 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // Enemy 체력 컴포넌트 참조입니다.
    [Tooltip("피격 수신 진입점 차단 제어에 사용할 HitReceiver 참조입니다.")]
    [SerializeField] private HitReceiver _hitReceiver; // HitReceiver 무적 상태 제어에 사용할 참조입니다.

    private bool _canBeHit = true; // 현재 로직 기준 피격 허용 여부입니다.

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
    /// 현재 피격 허용 여부를 반환합니다.
    /// </summary>
    public bool CanBeHit => _canBeHit && IsAlive;

    /// <summary>
    /// HealthComponent와 HitReceiver를 자동 연결합니다.
    /// </summary>
    private void Awake()
    {
        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }

        if (_hitReceiver == null)
        {
            _hitReceiver = GetComponent<HitReceiver>();
        }

        if (_healthComponent == null)
        {
            Debug.LogWarning($"[EnemyHealthAdapter] Missing HealthComponent on {name}. Death/Hit state transitions will not work.");
        }

        if (_hitReceiver == null)
        {
            Debug.LogWarning($"[EnemyHealthAdapter] Missing HitReceiver on {name}. CanBeHit gating cannot block damage entry.");
        }
    }

    /// <summary>
    /// 활성화 시 HealthComponent 이벤트를 구독하고 피격 상태를 기본값으로 복구합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_healthComponent != null)
        {
            _healthComponent.AddListener(this);
        }

        SetCanBeHit(true);
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
    /// 피격 가능 여부를 설정하고 HitReceiver 무적 상태를 동기화합니다.
    /// </summary>
    public void SetCanBeHit(bool canBeHit)
    {
        _canBeHit = canBeHit;

        if (_hitReceiver != null)
        {
            _hitReceiver.SetInvincible(!_canBeHit);
        }
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
    /// 사망 이벤트를 수신하고 피격 차단 상태로 전환합니다.
    /// </summary>
    public void OnDied()
    {
        SetCanBeHit(false);
        Died?.Invoke();
    }

    /// <summary>
    /// 부활 이벤트를 수신하고 피격 허용 상태를 복구합니다.
    /// </summary>
    public void OnRevived()
    {
        SetCanBeHit(true);
        Revived?.Invoke();
    }

    /// <summary>
    /// 최대 체력 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }
}
