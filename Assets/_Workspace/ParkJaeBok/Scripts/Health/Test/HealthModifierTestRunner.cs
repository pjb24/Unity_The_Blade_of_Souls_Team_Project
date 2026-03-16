using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Health 시스템의 모디파이어 파이프라인(무적/배율/실드/회복 차단)을 검증하는 러너입니다.
/// </summary>
public class HealthModifierTestRunner : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private HealthComponent _target; // 모디파이어 테스트 대상 체력 컴포넌트

    [Header("Runtime Rule State")]
    [SerializeField] private bool _isInvincible; // 무적 상태 여부
    [SerializeField] private bool _isHealBlocked; // 회복 차단 상태 여부
    [SerializeField] private float _damageScale = 1f; // 데미지 배율 값
    [SerializeField] private float _healScale = 1f; // 회복 배율 값
    [SerializeField] private float _shieldAmount = 30f; // 남아 있는 실드 값

    [Header("Hotkeys")]
    [SerializeField] private bool _enableHotkeys = true; // 키 입력 기반 테스트 실행 활성화 여부

    private bool _isRegistered; // 모디파이어 등록 완료 여부
    private Coroutine _registerCoroutine; // 초기화 대기 후 등록 루틴

    /// <summary>
    /// 활성화 시점에 모디파이어 등록 루틴을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_registerCoroutine != null)
        {
            StopCoroutine(_registerCoroutine);
            _registerCoroutine = null;
        }

        _registerCoroutine = StartCoroutine(Co_RegisterModifiersWhenReady());
    }

    /// <summary>
    /// 비활성화 시점에 모디파이어를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_registerCoroutine != null)
        {
            StopCoroutine(_registerCoroutine);
            _registerCoroutine = null;
        }

        UnregisterModifiers();
    }

    /// <summary>
    /// 키 입력으로 모디파이어 테스트 케이스를 실행합니다.
    /// </summary>
    private void Update()
    {
        if (!_enableHotkeys)
        {
            return;
        }

        if (Keyboard.current.f6Key.wasPressedThisFrame)
        {
            ToggleInvincible();
        }

        if (Keyboard.current.f7Key.wasPressedThisFrame)
        {
            RunTC09_ModifierDamage();
        }

        if (Keyboard.current.f8Key.wasPressedThisFrame)
        {
            RunTC10_ModifierHeal();
        }

        if (Keyboard.current.f9Key.wasPressedThisFrame)
        {
            ResetShield(30f);
        }

        if (Keyboard.current.f10Key.wasPressedThisFrame)
        {
            ToggleHealBlocked();
        }
    }

    /// <summary>
    /// 대상 초기화 완료 시점까지 기다린 뒤 모디파이어를 등록합니다.
    /// </summary>
    private IEnumerator Co_RegisterModifiersWhenReady()
    {
        if (_isRegistered)
        {
            yield break;
        }

        if (_target == null)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Missing target on {name}.");
            _registerCoroutine = null;
            yield break;
        }

        while (_target != null && _target.IsInitialized == false)
        {
            yield return null;
        }

        if (_target == null)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Target destroyed while waiting for initialization on {name}.");
            _registerCoroutine = null;
            yield break;
        }

        RegisterModifiersInternal();
        _registerCoroutine = null;
    }

    /// <summary>
    /// 테스트 대상에 모디파이어를 등록합니다.
    /// </summary>
    [ContextMenu("Register Modifiers")]
    public void RegisterModifiers()
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] RegisterModifiers called while disabled on {name}.");
            return;
        }

        if (_registerCoroutine != null)
        {
            StopCoroutine(_registerCoroutine);
            _registerCoroutine = null;
        }

        _registerCoroutine = StartCoroutine(Co_RegisterModifiersWhenReady());
    }

    /// <summary>
    /// 실제 모디파이어 등록을 수행합니다.
    /// </summary>
    private void RegisterModifiersInternal()
    {
        if (_isRegistered)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Modifiers already registered on {name}.");
            return;
        }

        if (_target == null)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Missing target while registering on {name}.");
            return;
        }

        if (_target.IsInitialized == false)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Target is not initialized while registering on {name}.");
            return;
        }

        _target.AddDamageModifier(ApplyDamageModifierPipeline);
        _target.AddHealModifier(ApplyHealModifierPipeline);
        _isRegistered = true;
        Debug.Log($"[HealthModifierTestRunner] Modifiers registered on {name}.");
    }

    /// <summary>
    /// 테스트 대상에서 모디파이어를 해제합니다.
    /// </summary>
    [ContextMenu("Unregister Modifiers")]
    public void UnregisterModifiers()
    {
        if (!_isRegistered)
        {
            return;
        }

        if (_target == null)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Target missing while unregistering on {name}.");
            _isRegistered = false;
            return;
        }

        if (_target.IsInitialized == false)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Target not initialized while unregistering on {name}. Registered state will be cleared.");
            _isRegistered = false;
            return;
        }

        _target.RemoveDamageModifier(ApplyDamageModifierPipeline);
        _target.RemoveHealModifier(ApplyHealModifierPipeline);
        _isRegistered = false;
        Debug.Log($"[HealthModifierTestRunner] Modifiers unregistered on {name}.");
    }

    /// <summary>
    /// 무적 상태를 토글합니다.
    /// </summary>
    [ContextMenu("Toggle Invincible")]
    public void ToggleInvincible()
    {
        _isInvincible = !_isInvincible;
        Debug.Log($"[HealthModifierTestRunner] Invincible:{_isInvincible}");
    }

    /// <summary>
    /// 회복 차단 상태를 토글합니다.
    /// </summary>
    [ContextMenu("Toggle Heal Blocked")]
    public void ToggleHealBlocked()
    {
        _isHealBlocked = !_isHealBlocked;
        Debug.Log($"[HealthModifierTestRunner] HealBlocked:{_isHealBlocked}");
    }

    /// <summary>
    /// 실드 값을 초기화합니다.
    /// </summary>
    public void ResetShield(float newShieldAmount)
    {
        if (newShieldAmount < 0f)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Invalid shield value({newShieldAmount}) on {name}. Fallback to 0.");
            newShieldAmount = 0f;
        }

        _shieldAmount = newShieldAmount;
        Debug.Log($"[HealthModifierTestRunner] Shield reset to {_shieldAmount}");
    }

    /// <summary>
    /// 데미지 모디파이어 테스트 케이스를 실행합니다.
    /// </summary>
    [ContextMenu("TC-09 Modifier Damage")]
    public void RunTC09_ModifierDamage()
    {
        if (!TryValidateTarget("TC-09 Modifier Damage"))
        {
            return;
        }

        DamageContext context = new DamageContext(
            amount: 20f,
            instigator: gameObject,
            sourceId: "TC-09_ModifierDamage",
            ignoreInvincibility: false,
            canKill: true,
            damageType: E_DamageType.Physical);

        DamageResult result = _target.ApplyDamage(context);
        Debug.Log($"[HealthModifierTestRunner] TC-09 Applied:{result.AppliedAmount} Current:{result.CurrentHealth} Shield:{_shieldAmount}");
    }

    /// <summary>
    /// 회복 모디파이어 테스트 케이스를 실행합니다.
    /// </summary>
    [ContextMenu("TC-10 Modifier Heal")]
    public void RunTC10_ModifierHeal()
    {
        if (!TryValidateTarget("TC-10 Modifier Heal"))
        {
            return;
        }

        HealContext context = new HealContext(
            amount: 20f,
            instigator: gameObject,
            sourceId: "TC-10_ModifierHeal",
            allowOverheal: false);

        HealResult result = _target.ApplyHeal(context);
        Debug.Log($"[HealthModifierTestRunner] TC-10 Applied:{result.AppliedAmount} Current:{result.CurrentHealth} Blocked:{result.IsBlocked}");
    }

    /// <summary>
    /// 데미지 컨텍스트에 모디파이어 파이프라인을 적용합니다.
    /// </summary>
    private DamageContext ApplyDamageModifierPipeline(DamageContext context, HealthCore core)
    {
        if (_isInvincible && !context.IgnoreInvincibility)
        {
            context.Amount = 0f;
            return context;
        }

        if (_damageScale < 0f)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Invalid damage scale({_damageScale}) on {name}. Fallback to 0.");
            _damageScale = 0f;
        }

        context.Amount *= _damageScale;

        if (_shieldAmount > 0f && context.Amount > 0f)
        {
            float absorbedAmount = Mathf.Min(_shieldAmount, context.Amount);
            _shieldAmount -= absorbedAmount;
            context.Amount -= absorbedAmount;
        }

        if (context.Amount < 0f)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Negative damage after pipeline on {name}. Fallback to 0.");
            context.Amount = 0f;
        }

        return context;
    }

    /// <summary>
    /// 회복 컨텍스트에 모디파이어 파이프라인을 적용합니다.
    /// </summary>
    private HealContext ApplyHealModifierPipeline(HealContext context, HealthCore core)
    {
        if (_isHealBlocked)
        {
            context.Amount = 0f;
            return context;
        }

        if (_healScale < 0f)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Invalid heal scale({_healScale}) on {name}. Fallback to 0.");
            _healScale = 0f;
        }

        context.Amount *= _healScale;

        if (context.Amount < 0f)
        {
            Debug.LogWarning($"[HealthModifierTestRunner] Negative heal after pipeline on {name}. Fallback to 0.");
            context.Amount = 0f;
        }

        return context;
    }

    /// <summary>
    /// 테스트 대상이 유효한지 검사합니다.
    /// </summary>
    private bool TryValidateTarget(string testCaseName)
    {
        if (_target != null)
        {
            return true;
        }

        Debug.LogWarning($"[HealthModifierTestRunner] Missing target for {testCaseName} on {name}.");
        return false;
    }
}
