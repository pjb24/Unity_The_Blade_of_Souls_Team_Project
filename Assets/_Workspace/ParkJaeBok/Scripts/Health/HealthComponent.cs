using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 오브젝트에서 <see cref="HealthCore"/>를 사용할 수 있게 감싸는 MonoBehaviour 래퍼입니다.
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("Initial Health")]
    [SerializeField] private float _initialMaxHealth = 100f; // 시작 최대 체력 값입니다.
    [SerializeField] private float _initialCurrentHealth = 100f; // 시작 현재 체력 값입니다.
    [SerializeField] private bool _allowHealWhenDead = false; // 사망 중 회복 허용 여부입니다.

    [Header("Debug")]
    [SerializeField] private float _debugCurrentHealth = 0f; // 디버그 표시용 현재 체력 값입니다.

    private HealthCore _healthCore; // 실제 체력 계산과 상태 전이를 담당하는 HealthCore 인스턴스입니다.
    private readonly List<IHealthListener> _pendingListeners = new List<IHealthListener>(); // HealthCore 초기화 전에 등록 요청된 리스너를 임시 보관하는 목록입니다.

    /// <summary>
    /// 코어 생성 여부를 반환합니다.
    /// </summary>
    public bool IsInitialized => _healthCore != null;

    /// <summary>
    /// 현재 사망 상태 여부를 반환합니다.
    /// </summary>
    public bool IsDead => _healthCore != null && _healthCore.IsDead;

    /// <summary>
    /// 초기 설정을 검증하고 HealthCore를 생성한 뒤 대기 중인 리스너를 반영합니다.
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
        FlushPendingListeners();
        _debugCurrentHealth = _healthCore.GetCurrentHealth();
    }

    /// <summary>
    /// 외부에서 리스너를 등록합니다.
    /// </summary>
    public void AddListener(IHealthListener listener)
    {
        TryAddListener(listener);
    }

    /// <summary>
    /// 외부에서 리스너 등록을 시도하고 성공 여부를 반환합니다.
    /// </summary>
    public bool TryAddListener(IHealthListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[HealthComponent] AddListener called with null listener on {name}.");
            return false;
        }

        if (_healthCore == null)
        {
            if (_pendingListeners.Contains(listener))
            {
                return true;
            }

            _pendingListeners.Add(listener);
            return true;
        }

        _healthCore.AddListener(listener);
        return true;
    }

    /// <summary>
    /// 외부에서 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(IHealthListener listener)
    {
        TryRemoveListener(listener);
    }

    /// <summary>
    /// 외부에서 리스너 해제를 시도하고 성공 여부를 반환합니다.
    /// </summary>
    public bool TryRemoveListener(IHealthListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[HealthComponent] RemoveListener called with null listener on {name}.");
            return false;
        }

        if (_healthCore == null)
        {
            return _pendingListeners.Remove(listener);
        }

        _healthCore.RemoveListener(listener);
        return true;
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

        DamageResult result = _healthCore.ApplyDamage(context);
        _debugCurrentHealth = _healthCore.GetCurrentHealth();
        return result;
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

        HealResult result = _healthCore.ApplyHeal(context);
        _debugCurrentHealth = _healthCore.GetCurrentHealth();
        return result;
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
    /// 정규화된 체력 값을 조회합니다.
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

    /// <summary>
    /// HealthCore 초기화 전에 들어온 리스너 등록 요청을 실제 코어에 반영합니다.
    /// </summary>
    private void FlushPendingListeners()
    {
        if (_healthCore == null || _pendingListeners.Count == 0)
        {
            return;
        }

        for (int index = 0; index < _pendingListeners.Count; index++)
        {
            IHealthListener pendingListener = _pendingListeners[index]; // 초기화 전에 등록 요청된 리스너 참조입니다.
            if (pendingListener == null)
            {
                continue;
            }

            _healthCore.AddListener(pendingListener);
        }

        _pendingListeners.Clear();
    }
}
