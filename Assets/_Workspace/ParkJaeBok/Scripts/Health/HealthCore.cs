using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 체력 핵심 계산과 상태를 관리하는 순수 C# 코어 클래스입니다.
/// </summary>
public class HealthCore
{
    public delegate DamageContext DamageModifierDelegate(DamageContext context, HealthCore core);
    public delegate HealContext HealModifierDelegate(HealContext context, HealthCore core);

    private readonly List<IHealthListener> _listeners = new List<IHealthListener>(); // 체력 변경 알림 리스너 목록
    private readonly List<DamageModifierDelegate> _damageModifiers = new List<DamageModifierDelegate>(); // 데미지 전처리 모디파이어 목록
    private readonly List<HealModifierDelegate> _healModifiers = new List<HealModifierDelegate>(); // 회복 전처리 모디파이어 목록

    private float _currentHealth; // 현재 체력 값
    private float _maxHealth; // 최대 체력 값
    private bool _isDead; // 현재 사망 상태 플래그
    private bool _allowHealWhenDead; // 사망 상태 회복 허용 플래그

    /// <summary>
    /// 현재 사망 상태 여부를 반환합니다.
    /// </summary>
    public bool IsDead => _isDead;

    /// <summary>
    /// 초기 최대/현재 체력을 설정해 코어를 생성합니다.
    /// </summary>
    public HealthCore(float maxHealth, float currentHealth = -1f, bool allowHealWhenDead = false)
    {
        _allowHealWhenDead = allowHealWhenDead;

        if (maxHealth <= 0f)
        {
            Debug.LogWarning($"[HealthCore] Invalid maxHealth({maxHealth}). Fallback to 1.");
            maxHealth = 1f;
        }

        _maxHealth = maxHealth;
        _currentHealth = currentHealth < 0f ? _maxHealth : Mathf.Clamp(currentHealth, 0f, _maxHealth);
        _isDead = _currentHealth <= 0f;
    }

    /// <summary>
    /// 사망 상태에서의 회복 허용 여부를 런타임에 설정합니다.
    /// </summary>
    public void SetAllowHealWhenDead(bool allowHealWhenDead)
    {
        _allowHealWhenDead = allowHealWhenDead;
    }

    /// <summary>
    /// 데미지 모디파이어를 등록합니다.
    /// </summary>
    public void AddDamageModifier(DamageModifierDelegate modifier)
    {
        if (modifier == null)
        {
            Debug.LogWarning("[HealthCore] Cannot add null damage modifier.");
            return;
        }

        if (_damageModifiers.Contains(modifier))
        {
            Debug.LogWarning("[HealthCore] Duplicate damage modifier registration ignored.");
            return;
        }

        _damageModifiers.Add(modifier);
    }

    /// <summary>
    /// 데미지 모디파이어를 제거합니다.
    /// </summary>
    public void RemoveDamageModifier(DamageModifierDelegate modifier)
    {
        if (modifier == null)
        {
            Debug.LogWarning("[HealthCore] Cannot remove null damage modifier.");
            return;
        }

        if (!_damageModifiers.Remove(modifier))
        {
            Debug.LogWarning("[HealthCore] Tried to remove unknown damage modifier.");
        }
    }

    /// <summary>
    /// 회복 모디파이어를 등록합니다.
    /// </summary>
    public void AddHealModifier(HealModifierDelegate modifier)
    {
        if (modifier == null)
        {
            Debug.LogWarning("[HealthCore] Cannot add null heal modifier.");
            return;
        }

        if (_healModifiers.Contains(modifier))
        {
            Debug.LogWarning("[HealthCore] Duplicate heal modifier registration ignored.");
            return;
        }

        _healModifiers.Add(modifier);
    }

    /// <summary>
    /// 회복 모디파이어를 제거합니다.
    /// </summary>
    public void RemoveHealModifier(HealModifierDelegate modifier)
    {
        if (modifier == null)
        {
            Debug.LogWarning("[HealthCore] Cannot remove null heal modifier.");
            return;
        }

        if (!_healModifiers.Remove(modifier))
        {
            Debug.LogWarning("[HealthCore] Tried to remove unknown heal modifier.");
        }
    }

    /// <summary>
    /// 리스너를 등록합니다.
    /// </summary>
    public void AddListener(IHealthListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[HealthCore] Cannot add null listener.");
            return;
        }

        if (_listeners.Contains(listener))
        {
            Debug.LogWarning("[HealthCore] Duplicate listener registration ignored.");
            return;
        }

        _listeners.Add(listener);
    }

    /// <summary>
    /// 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(IHealthListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[HealthCore] Cannot remove null listener.");
            return;
        }

        if (!_listeners.Remove(listener))
        {
            Debug.LogWarning("[HealthCore] Tried to remove unknown listener.");
        }
    }

    /// <summary>
    /// 최대 체력을 변경하고 필요 시 현재 체력도 보정합니다.
    /// </summary>
    public void SetMaxHealth(float maxHealth, bool adjustCurrentHealth)
    {
        if (maxHealth <= 0f)
        {
            Debug.LogWarning($"[HealthCore] Invalid maxHealth({maxHealth}) in SetMaxHealth.");
            return;
        }

        float previousMaxHealth = _maxHealth;
        float previousHealth = _currentHealth;

        _maxHealth = maxHealth;

        if (adjustCurrentHealth)
        {
            _currentHealth = _maxHealth;
        }
        else if (_currentHealth > _maxHealth)
        {
            _currentHealth = _maxHealth;
            Debug.LogWarning("[HealthCore] Current health was clamped after max health reduction.");
        }

        NotifyMaxHealthChanged(previousMaxHealth, _maxHealth);

        if (!Mathf.Approximately(previousHealth, _currentHealth))
        {
            NotifyHealthChanged(previousHealth, _currentHealth);
            EvaluateDeathTransition(previousHealth);
        }
    }

    /// <summary>
    /// 현재 체력을 직접 설정합니다.
    /// </summary>
    public void SetCurrentHealth(float currentHealth)
    {
        if (currentHealth < 0f)
        {
            Debug.LogWarning($"[HealthCore] Negative current health({currentHealth}) is invalid.");
            return;
        }

        float previousHealth = _currentHealth;
        _currentHealth = Mathf.Clamp(currentHealth, 0f, _maxHealth);

        if (!Mathf.Approximately(previousHealth, _currentHealth))
        {
            NotifyHealthChanged(previousHealth, _currentHealth);
            EvaluateDeathTransition(previousHealth);
        }
    }

    /// <summary>
    /// 사망 상태를 해제하며 지정된 체력으로 부활시킵니다.
    /// </summary>
    public void Revive(float health)
    {
        if (health <= 0f)
        {
            Debug.LogWarning($"[HealthCore] Invalid revive health({health}).");
            return;
        }

        float previousHealth = _currentHealth;
        _currentHealth = Mathf.Clamp(health, 0f, _maxHealth);

        bool wasDead = _isDead;
        _isDead = _currentHealth <= 0f;

        NotifyHealthChanged(previousHealth, _currentHealth);

        if (wasDead && !_isDead)
        {
            NotifyRevived();
        }
    }

    /// <summary>
    /// 데미지를 적용하고 결과를 반환합니다.
    /// </summary>
    public DamageResult ApplyDamage(DamageContext context)
    {
        float previousHealth = _currentHealth;
        bool hasWarningFallback = false;

        if (context.Amount < 0f)
        {
            Debug.LogWarning($"[HealthCore] Negative damage({context.Amount}) is invalid.");
            return new DamageResult(previousHealth, _currentHealth, 0f, true, true, false, true);
        }

        context = RunDamageModifiers(context, ref hasWarningFallback);

        if (context.Amount <= 0f)
        {
            if (context.Amount < 0f)
            {
                Debug.LogWarning("[HealthCore] Damage modifier produced negative amount. Clamped to 0.");
                hasWarningFallback = true;
            }

            DamageResult blockedResult = new DamageResult(previousHealth, _currentHealth, 0f, false, true, false, hasWarningFallback);
            NotifyDamaged(blockedResult);
            return blockedResult;
        }

        if (_isDead)
        {
            DamageResult blockedDeadResult = new DamageResult(previousHealth, _currentHealth, 0f, false, true, false, hasWarningFallback);
            NotifyDamaged(blockedDeadResult);
            return blockedDeadResult;
        }

        float minHealth = context.CanKill ? 0f : 1f;
        if (!context.CanKill && _currentHealth <= 1f)
        {
            DamageResult blockedNoKillResult = new DamageResult(previousHealth, _currentHealth, 0f, false, true, false, hasWarningFallback);
            NotifyDamaged(blockedNoKillResult);
            return blockedNoKillResult;
        }

        float targetHealth = Mathf.Max(minHealth, _currentHealth - context.Amount);
        _currentHealth = Mathf.Clamp(targetHealth, 0f, _maxHealth);

        float appliedAmount = Mathf.Max(0f, previousHealth - _currentHealth);
        bool didCauseDeath = previousHealth > 0f && _currentHealth <= 0f;

        DamageResult result = new DamageResult(previousHealth, _currentHealth, appliedAmount, false, false, didCauseDeath, hasWarningFallback);

        if (!Mathf.Approximately(previousHealth, _currentHealth))
        {
            NotifyHealthChanged(previousHealth, _currentHealth);
        }

        NotifyDamaged(result);
        EvaluateDeathTransition(previousHealth);
        return result;
    }

    /// <summary>
    /// 회복을 적용하고 결과를 반환합니다.
    /// </summary>
    public HealResult ApplyHeal(HealContext context)
    {
        float previousHealth = _currentHealth;
        bool hasWarningFallback = false;

        if (context.Amount < 0f)
        {
            Debug.LogWarning($"[HealthCore] Negative heal({context.Amount}) is invalid.");
            return new HealResult(previousHealth, _currentHealth, 0f, true, true, false, true);
        }

        context = RunHealModifiers(context, ref hasWarningFallback);

        if (context.Amount <= 0f)
        {
            if (context.Amount < 0f)
            {
                Debug.LogWarning("[HealthCore] Heal modifier produced negative amount. Clamped to 0.");
                hasWarningFallback = true;
            }

            HealResult blockedZeroResult = new HealResult(previousHealth, _currentHealth, 0f, false, true, false, hasWarningFallback);
            NotifyHealed(blockedZeroResult);
            return blockedZeroResult;
        }

        if (_isDead && !_allowHealWhenDead)
        {
            Debug.LogWarning("[HealthCore] Heal blocked because target is dead and allowHealWhenDead is false.");
            HealResult blockedDeadResult = new HealResult(previousHealth, _currentHealth, 0f, false, true, false, true);
            NotifyHealed(blockedDeadResult);
            return blockedDeadResult;
        }

        float maxLimit = context.AllowOverheal ? float.MaxValue : _maxHealth;
        float nextHealth = _currentHealth + context.Amount;
        _currentHealth = Mathf.Clamp(nextHealth, 0f, maxLimit);

        float appliedAmount = Mathf.Max(0f, _currentHealth - previousHealth);
        bool didRevive = previousHealth <= 0f && _currentHealth > 0f;

        HealResult result = new HealResult(previousHealth, _currentHealth, appliedAmount, false, false, didRevive, hasWarningFallback);

        if (!Mathf.Approximately(previousHealth, _currentHealth))
        {
            NotifyHealthChanged(previousHealth, _currentHealth);
        }

        NotifyHealed(result);
        EvaluateDeathTransition(previousHealth);
        return result;
    }

    /// <summary>
    /// 현재 체력을 반환합니다.
    /// </summary>
    public float GetCurrentHealth()
    {
        return _currentHealth;
    }

    /// <summary>
    /// 최대 체력을 반환합니다.
    /// </summary>
    public float GetMaxHealth()
    {
        return _maxHealth;
    }

    /// <summary>
    /// 현재 체력의 정규화 값을 반환합니다.
    /// </summary>
    public float GetHealthNormalized()
    {
        if (_maxHealth <= 0f)
        {
            Debug.LogWarning("[HealthCore] Max health is zero or below while normalizing.");
            return 0f;
        }

        return Mathf.Clamp01(_currentHealth / _maxHealth);
    }

    /// <summary>
    /// 등록된 모디파이어를 순서대로 실행합니다.
    /// </summary>
    private DamageContext RunDamageModifiers(DamageContext context, ref bool hasWarningFallback)
    {
        for (int i = 0; i < _damageModifiers.Count; i++)
        {
            context = _damageModifiers[i](context, this);
        }

        if (context.Amount < 0f)
        {
            context.Amount = 0f;
            hasWarningFallback = true;
        }

        if (string.IsNullOrWhiteSpace(context.SourceId))
        {
            Debug.LogWarning("[HealthCore] DamageContext.SourceId was empty. Fallback to Unknown.");
            context.SourceId = "Unknown";
            hasWarningFallback = true;
        }

        return context;
    }

    /// <summary>
    /// 등록된 모디파이어를 순서대로 실행합니다.
    /// </summary>
    private HealContext RunHealModifiers(HealContext context, ref bool hasWarningFallback)
    {
        for (int i = 0; i < _healModifiers.Count; i++)
        {
            context = _healModifiers[i](context, this);
        }

        if (context.Amount < 0f)
        {
            context.Amount = 0f;
            hasWarningFallback = true;
        }

        if (string.IsNullOrWhiteSpace(context.SourceId))
        {
            Debug.LogWarning("[HealthCore] HealContext.SourceId was empty. Fallback to Unknown.");
            context.SourceId = "Unknown";
            hasWarningFallback = true;
        }

        return context;
    }

    /// <summary>
    /// 현재 상태를 기준으로 사망/부활 전환 알림을 처리합니다.
    /// </summary>
    private void EvaluateDeathTransition(float previousHealth)
    {
        bool wasDead = _isDead;
        _isDead = _currentHealth <= 0f;

        if (!wasDead && _isDead)
        {
            NotifyDied();
        }
        else if (wasDead && !_isDead && previousHealth <= 0f)
        {
            NotifyRevived();
        }
    }

    /// <summary>
    /// 모든 리스너에 체력 변경 알림을 전송합니다.
    /// </summary>
    private void NotifyHealthChanged(float previousHealth, float currentHealth)
    {
        HealthChangeData data = new HealthChangeData(previousHealth, currentHealth, _maxHealth);
        for (int i = 0; i < _listeners.Count; i++)
        {
            _listeners[i].OnHealthChanged(data);
        }
    }

    /// <summary>
    /// 모든 리스너에 데미지 적용 알림을 전송합니다.
    /// </summary>
    private void NotifyDamaged(DamageResult result)
    {
        for (int i = 0; i < _listeners.Count; i++)
        {
            _listeners[i].OnDamaged(result);
        }
    }

    /// <summary>
    /// 모든 리스너에 회복 적용 알림을 전송합니다.
    /// </summary>
    private void NotifyHealed(HealResult result)
    {
        for (int i = 0; i < _listeners.Count; i++)
        {
            _listeners[i].OnHealed(result);
        }
    }

    /// <summary>
    /// 모든 리스너에 사망 알림을 전송합니다.
    /// </summary>
    private void NotifyDied()
    {
        for (int i = 0; i < _listeners.Count; i++)
        {
            _listeners[i].OnDied();
        }
    }

    /// <summary>
    /// 모든 리스너에 부활 알림을 전송합니다.
    /// </summary>
    private void NotifyRevived()
    {
        for (int i = 0; i < _listeners.Count; i++)
        {
            _listeners[i].OnRevived();
        }
    }

    /// <summary>
    /// 모든 리스너에 최대 체력 변경 알림을 전송합니다.
    /// </summary>
    private void NotifyMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
        for (int i = 0; i < _listeners.Count; i++)
        {
            _listeners[i].OnMaxHealthChanged(previousMaxHealth, currentMaxHealth);
        }
    }
}
