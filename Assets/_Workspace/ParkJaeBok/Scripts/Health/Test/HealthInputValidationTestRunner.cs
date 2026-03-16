using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Health 시스템의 입력 방어 및 경고 로그 동작을 검증하는 러너입니다.
/// </summary>
public class HealthInputValidationTestRunner : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private HealthComponent _target; // 입력 방어 테스트 대상 체력 컴포넌트

    [Header("Hotkeys")]
    [SerializeField] private bool _enableHotkeys = true; // 키 입력 기반 테스트 실행 활성화 여부

    private bool _isReady; // 대상 사용 가능 여부
    private Coroutine _readyCoroutine; // 대상 초기화 대기 루틴

    /// <summary>
    /// 활성화 시점에 대상 초기화 대기 루틴을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        StartReadyRoutine();
    }

    /// <summary>
    /// 비활성화 시점에 대상 초기화 대기 루틴을 중단합니다.
    /// </summary>
    private void OnDisable()
    {
        StopReadyRoutine();
        _isReady = false;
    }

    /// <summary>
    /// 키 입력으로 입력 방어 테스트 케이스를 실행합니다.
    /// </summary>
    private void Update()
    {
        if (!_enableHotkeys)
        {
            return;
        }

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            RunTC06_NegativeDamage();
        }

        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            RunTC07_NegativeHeal();
        }

        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            RunTC08_NoKillDamage();
        }

        if (Keyboard.current.f4Key.wasPressedThisFrame)
        {
            RunTC09_EmptySourceId();
        }

        if (Keyboard.current.f5Key.wasPressedThisFrame)
        {
            RunTC10_NullListenerAndModifier();
        }
    }

    /// <summary>
    /// 대상 준비 대기 루틴을 시작합니다.
    /// </summary>
    private void StartReadyRoutine()
    {
        StopReadyRoutine();
        _isReady = false;
        _readyCoroutine = StartCoroutine(Co_WaitUntilTargetReady());
    }

    /// <summary>
    /// 대상 준비 대기 루틴을 중단합니다.
    /// </summary>
    private void StopReadyRoutine()
    {
        if (_readyCoroutine == null)
        {
            return;
        }

        StopCoroutine(_readyCoroutine);
        _readyCoroutine = null;
    }

    /// <summary>
    /// 대상 초기화 완료 시점까지 기다린 뒤 사용 가능 상태로 전환합니다.
    /// </summary>
    private IEnumerator Co_WaitUntilTargetReady()
    {
        if (_target == null)
        {
            Debug.LogWarning($"[HealthInputValidationTestRunner] Missing target on {name}.");
            _readyCoroutine = null;
            yield break;
        }

        while (_target != null && _target.IsInitialized == false)
        {
            yield return null;
        }

        if (_target == null)
        {
            Debug.LogWarning($"[HealthInputValidationTestRunner] Target destroyed while waiting for initialization on {name}.");
            _readyCoroutine = null;
            yield break;
        }

        _isReady = true;
        _readyCoroutine = null;
        Debug.Log($"[HealthInputValidationTestRunner] Target is ready on {name}.");
    }

    /// <summary>
    /// 음수 데미지 입력 방어 케이스를 실행합니다.
    /// </summary>
    [ContextMenu("TC-06 Negative Damage")]
    public void RunTC06_NegativeDamage()
    {
        if (!TryValidateTarget("TC-06 Negative Damage"))
        {
            return;
        }

        DamageContext context = new DamageContext(
            amount: -10f,
            instigator: gameObject,
            sourceId: "TC-06_NegativeDamage",
            ignoreInvincibility: false,
            canKill: true,
            damageType: E_DamageType.Unknown);

        DamageResult result = _target.ApplyDamage(context);
        Debug.Log($"[HealthInputValidationTestRunner] TC-06 Invalid:{result.IsInvalid} Blocked:{result.IsBlocked}");
    }

    /// <summary>
    /// 음수 회복 입력 방어 케이스를 실행합니다.
    /// </summary>
    [ContextMenu("TC-07 Negative Heal")]
    public void RunTC07_NegativeHeal()
    {
        if (!TryValidateTarget("TC-07 Negative Heal"))
        {
            return;
        }

        HealContext context = new HealContext(
            amount: -5f,
            instigator: gameObject,
            sourceId: "TC-07_NegativeHeal",
            allowOverheal: false);

        HealResult result = _target.ApplyHeal(context);
        Debug.Log($"[HealthInputValidationTestRunner] TC-07 Invalid:{result.IsInvalid} Blocked:{result.IsBlocked}");
    }

    /// <summary>
    /// 치사 데미지를 금지하는 CanKill=false 케이스를 실행합니다.
    /// </summary>
    [ContextMenu("TC-08 CanKill False")]
    public void RunTC08_NoKillDamage()
    {
        if (!TryValidateTarget("TC-08 CanKill False"))
        {
            return;
        }

        _target.SetCurrentHealth(10f);

        DamageContext context = new DamageContext(
            amount: 999f,
            instigator: gameObject,
            sourceId: "TC-08_NoKillDamage",
            ignoreInvincibility: false,
            canKill: false,
            damageType: E_DamageType.True);

        DamageResult result = _target.ApplyDamage(context);
        Debug.Log($"[HealthInputValidationTestRunner] TC-08 Current:{result.CurrentHealth} Applied:{result.AppliedAmount}");
    }

    /// <summary>
    /// 빈 SourceId 폴백 경고 케이스를 실행합니다.
    /// </summary>
    [ContextMenu("TC-09 Empty SourceId")]
    public void RunTC09_EmptySourceId()
    {
        if (!TryValidateTarget("TC-09 Empty SourceId"))
        {
            return;
        }

        DamageContext context = new DamageContext(
            amount: 5f,
            instigator: gameObject,
            sourceId: " ",
            ignoreInvincibility: false,
            canKill: true,
            damageType: E_DamageType.Physical);

        DamageResult result = _target.ApplyDamage(context);
        Debug.Log($"[HealthInputValidationTestRunner] TC-09 Fallback:{result.HasWarningFallback}");
    }

    /// <summary>
    /// null 리스너/모디파이어 등록 및 해제 방어 케이스를 실행합니다.
    /// </summary>
    [ContextMenu("TC-10 Null Listener and Modifier")]
    public void RunTC10_NullListenerAndModifier()
    {
        if (!TryValidateTarget("TC-10 Null Listener and Modifier"))
        {
            return;
        }

        _target.AddListener(null);
        _target.RemoveListener(null);
        _target.AddDamageModifier(null);
        _target.RemoveDamageModifier(null);
        _target.AddHealModifier(null);
        _target.RemoveHealModifier(null);

        Debug.Log("[HealthInputValidationTestRunner] TC-10 executed. Check warning logs.");
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

        Debug.LogWarning($"[HealthInputValidationTestRunner] Missing target for {testCaseName} on {name}.");
        return false;
    }
}
