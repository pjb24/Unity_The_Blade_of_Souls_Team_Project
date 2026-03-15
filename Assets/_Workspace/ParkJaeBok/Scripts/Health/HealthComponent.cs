using UnityEngine;

/// <summary>
/// 씬에서 HealthCore를 사용할 수 있게 감싸는 MonoBehaviour 래퍼입니다.
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("Initial Health")]
    [SerializeField] private float _initialMaxHealth = 100f; // 시작 최대 체력 값
    [SerializeField] private float _initialCurrentHealth = 100f; // 시작 현재 체력 값
    [SerializeField] private bool _allowHealWhenDead = false; // 사망 중 회복 허용 여부

    private HealthCore _healthCore; // 내부 체력 코어 인스턴스

    /// <summary>
    /// 코어 생성 여부를 반환합니다.
    /// </summary>
    public bool IsInitialized => _healthCore != null;

    /// <summary>
    /// 현재 사망 상태 여부를 반환합니다.
    /// </summary>
    public bool IsDead => _healthCore != null && _healthCore.IsDead;

    /// <summary>
    /// 초기 설정을 검증하고 HealthCore를 생성합니다.
    /// </summary>
    private void Awake()
    {
        float safeMaxHealth = _initialMaxHealth;
        if (safeMaxHealth <= 0f)
        {
            Debug.LogWarning($"[HealthComponent] Invalid initial max health({safeMaxHealth}) on {name}. Fallback to 1.");
            safeMaxHealth = 1f;
        }

        float safeCurrentHealth = Mathf.Clamp(_initialCurrentHealth, 0f, safeMaxHealth);
        if (!Mathf.Approximately(safeCurrentHealth, _initialCurrentHealth))
        {
            Debug.LogWarning($"[HealthComponent] Initial current health({_initialCurrentHealth}) was clamped on {name}.");
        }

        _healthCore = new HealthCore(safeMaxHealth, safeCurrentHealth, _allowHealWhenDead);
    }

    /// <summary>
    /// 외부에서 리스너를 등록합니다.
    /// </summary>
    public void AddListener(IHealthListener listener)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] AddListener called before initialization on {name}.");
            return;
        }

        _healthCore.AddListener(listener);
    }

    /// <summary>
    /// 외부에서 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(IHealthListener listener)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] RemoveListener called before initialization on {name}.");
            return;
        }

        _healthCore.RemoveListener(listener);
    }

    /// <summary>
    /// 데미지를 코어에 전달합니다.
    /// </summary>
    public DamageResult ApplyDamage(DamageContext context)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] ApplyDamage called before initialization on {name}.");
            return new DamageResult(0f, 0f, 0f, true, true, false, true);
        }

        return _healthCore.ApplyDamage(context);
    }

    /// <summary>
    /// 회복을 코어에 전달합니다.
    /// </summary>
    public HealResult ApplyHeal(HealContext context)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] ApplyHeal called before initialization on {name}.");
            return new HealResult(0f, 0f, 0f, true, true, false, true);
        }

        return _healthCore.ApplyHeal(context);
    }

    /// <summary>
    /// 최대 체력을 설정합니다.
    /// </summary>
    public void SetMaxHealth(float maxHealth, bool adjustCurrentHealth)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] SetMaxHealth called before initialization on {name}.");
            return;
        }

        _healthCore.SetMaxHealth(maxHealth, adjustCurrentHealth);
    }

    /// <summary>
    /// 현재 체력을 설정합니다.
    /// </summary>
    public void SetCurrentHealth(float currentHealth)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] SetCurrentHealth called before initialization on {name}.");
            return;
        }

        _healthCore.SetCurrentHealth(currentHealth);
    }

    /// <summary>
    /// 지정 체력으로 부활시킵니다.
    /// </summary>
    public void Revive(float health)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] Revive called before initialization on {name}.");
            return;
        }

        _healthCore.Revive(health);
    }

    /// <summary>
    /// 런타임에 사망 중 회복 허용 여부를 변경합니다.
    /// </summary>
    public void SetAllowHealWhenDead(bool allowHealWhenDead)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] SetAllowHealWhenDead called before initialization on {name}.");
            return;
        }

        _healthCore.SetAllowHealWhenDead(allowHealWhenDead);
    }

    /// <summary>
    /// 현재 체력을 조회합니다.
    /// </summary>
    public float GetCurrentHealth()
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] GetCurrentHealth called before initialization on {name}.");
            return 0f;
        }

        return _healthCore.GetCurrentHealth();
    }

    /// <summary>
    /// 최대 체력을 조회합니다.
    /// </summary>
    public float GetMaxHealth()
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] GetMaxHealth called before initialization on {name}.");
            return 1f;
        }

        return _healthCore.GetMaxHealth();
    }

    /// <summary>
    /// 정규화 체력을 조회합니다.
    /// </summary>
    public float GetHealthNormalized()
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] GetHealthNormalized called before initialization on {name}.");
            return 0f;
        }

        return _healthCore.GetHealthNormalized();
    }

    /// <summary>
    /// 데미지 모디파이어를 코어에 등록합니다.
    /// </summary>
    public void AddDamageModifier(HealthCore.DamageModifierDelegate modifier)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] AddDamageModifier called before initialization on {name}.");
            return;
        }

        _healthCore.AddDamageModifier(modifier);
    }

    /// <summary>
    /// 데미지 모디파이어를 코어에서 제거합니다.
    /// </summary>
    public void RemoveDamageModifier(HealthCore.DamageModifierDelegate modifier)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] RemoveDamageModifier called before initialization on {name}.");
            return;
        }

        _healthCore.RemoveDamageModifier(modifier);
    }

    /// <summary>
    /// 회복 모디파이어를 코어에 등록합니다.
    /// </summary>
    public void AddHealModifier(HealthCore.HealModifierDelegate modifier)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] AddHealModifier called before initialization on {name}.");
            return;
        }

        _healthCore.AddHealModifier(modifier);
    }

    /// <summary>
    /// 회복 모디파이어를 코어에서 제거합니다.
    /// </summary>
    public void RemoveHealModifier(HealthCore.HealModifierDelegate modifier)
    {
        if (_healthCore == null)
        {
            Debug.LogWarning($"[HealthComponent] RemoveHealModifier called before initialization on {name}.");
            return;
        }

        _healthCore.RemoveHealModifier(modifier);
    }
}
