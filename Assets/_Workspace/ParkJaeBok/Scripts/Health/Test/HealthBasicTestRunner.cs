using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Health 시스템의 기본 동작(데미지, 회복, 사망, 부활, 최대 체력 변경)을 검증하는 러너입니다.
/// </summary>
public class HealthBasicTestRunner : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private HealthComponent _playerTarget; // 플레이어 테스트 대상 체력 컴포넌트
    [SerializeField] private HealthComponent _enemyTarget; // 적 테스트 대상 체력 컴포넌트
    [SerializeField] private HealthComponent _bossTarget; // 보스 테스트 대상 체력 컴포넌트
    [SerializeField] private HealthComponent _destructibleTarget; // 파괴 오브젝트 테스트 대상 체력 컴포넌트

    [Header("Hotkeys")]
    [SerializeField] private bool _enableHotkeys = true; // 키 입력 기반 테스트 실행 활성화 여부

    private bool _isReady; // 전체 테스트 실행 가능 여부
    private Coroutine _readyCoroutine; // 타겟 초기화 대기 루틴

    /// <summary>
    /// 활성화 시점에 타겟 초기화 대기 루틴을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        StartReadyRoutine();
    }

    /// <summary>
    /// 비활성화 시점에 타겟 초기화 대기 루틴을 중단합니다.
    /// </summary>
    private void OnDisable()
    {
        StopReadyRoutine();
        _isReady = false;
    }

    /// <summary>
    /// 키 입력으로 기본 테스트 케이스를 실행합니다.
    /// </summary>
    private void Update()
    {
        if (!_enableHotkeys)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            RunTC01_PlayerDamage();
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            RunTC02_PlayerHeal();
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            RunTC03_EnemyKill();
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            RunTC04_EnemyRevive();
        }

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            RunTC05_BossSetMaxHealthClamp();
        }

        if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            RunTC06_DestructibleDestroy();
        }
    }

    /// <summary>
    /// 타겟 준비 대기 루틴을 시작합니다.
    /// </summary>
    private void StartReadyRoutine()
    {
        StopReadyRoutine();
        _isReady = false;
        _readyCoroutine = StartCoroutine(Co_WaitUntilTargetsReady());
    }

    /// <summary>
    /// 타겟 준비 대기 루틴을 중단합니다.
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
    /// 할당된 모든 타겟의 초기화 완료 시점까지 기다린 뒤 사용 가능 상태로 전환합니다.
    /// </summary>
    private IEnumerator Co_WaitUntilTargetsReady()
    {
        if (!HasAnyAssignedTarget())
        {
            Debug.LogWarning($"[HealthBasicTestRunner] No assigned targets on {name}.");
            _readyCoroutine = null;
            yield break;
        }

        while (!AreAssignedTargetsReady())
        {
            yield return null;
        }

        _isReady = true;
        _readyCoroutine = null;
        Debug.Log($"[HealthBasicTestRunner] All assigned targets are ready on {name}.");
    }

    /// <summary>
    /// 하나 이상의 타겟이 할당되어 있는지 검사합니다.
    /// </summary>
    private bool HasAnyAssignedTarget()
    {
        return _playerTarget != null
            || _enemyTarget != null
            || _bossTarget != null
            || _destructibleTarget != null;
    }

    /// <summary>
    /// 할당된 모든 타겟이 초기화 완료 상태인지 검사합니다.
    /// null 타겟은 미할당으로 간주하고 검사 대상에서 제외합니다.
    /// </summary>
    private bool AreAssignedTargetsReady()
    {
        if (!IsTargetReadyOrUnassigned(_playerTarget))
        {
            return false;
        }

        if (!IsTargetReadyOrUnassigned(_enemyTarget))
        {
            return false;
        }

        if (!IsTargetReadyOrUnassigned(_bossTarget))
        {
            return false;
        }

        if (!IsTargetReadyOrUnassigned(_destructibleTarget))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 타겟이 미할당 상태이거나 초기화 완료 상태인지 검사합니다.
    /// </summary>
    private bool IsTargetReadyOrUnassigned(HealthComponent target)
    {
        if (target == null)
        {
            return true;
        }

        return target.IsInitialized;
    }

    /// <summary>
    /// 플레이어 대상에 기본 데미지 케이스를 실행합니다.
    /// </summary>
    [ContextMenu("TC-01 Player Damage")]
    public void RunTC01_PlayerDamage()
    {
        if (!TryValidateTarget(_playerTarget, "TC-01 Player Damage"))
        {
            return;
        }

        DamageContext context = new DamageContext(
            amount: 25f,
            instigator: gameObject,
            sourceId: "TC-01_PlayerDamage",
            ignoreInvincibility: false,
            canKill: true,
            damageType: E_DamageType.Physical);

        DamageResult result = _playerTarget.ApplyDamage(context);
        Debug.Log($"[HealthBasicTestRunner] TC-01 Applied:{result.AppliedAmount} Current:{result.CurrentHealth}");
    }

    /// <summary>
    /// 플레이어 대상에 기본 회복 케이스를 실행합니다.
    /// </summary>
    [ContextMenu("TC-02 Player Heal")]
    public void RunTC02_PlayerHeal()
    {
        if (!TryValidateTarget(_playerTarget, "TC-02 Player Heal"))
        {
            return;
        }

        HealContext context = new HealContext(
            amount: 10f,
            instigator: gameObject,
            sourceId: "TC-02_PlayerHeal",
            allowOverheal: false);

        HealResult result = _playerTarget.ApplyHeal(context);
        Debug.Log($"[HealthBasicTestRunner] TC-02 Applied:{result.AppliedAmount} Current:{result.CurrentHealth}");
    }

    /// <summary>
    /// 적 대상에 치사 데미지 케이스를 실행합니다.
    /// </summary>
    [ContextMenu("TC-03 Enemy Kill")]
    public void RunTC03_EnemyKill()
    {
        if (!TryValidateTarget(_enemyTarget, "TC-03 Enemy Kill"))
        {
            return;
        }

        DamageContext context = new DamageContext(
            amount: 999f,
            instigator: gameObject,
            sourceId: "TC-03_EnemyKill",
            ignoreInvincibility: false,
            canKill: true,
            damageType: E_DamageType.True);

        DamageResult result = _enemyTarget.ApplyDamage(context);
        Debug.Log($"[HealthBasicTestRunner] TC-03 Died:{result.DidCauseDeath} Current:{result.CurrentHealth}");
    }

    /// <summary>
    /// 적 대상을 지정 체력으로 부활시키는 케이스를 실행합니다.
    /// </summary>
    [ContextMenu("TC-04 Enemy Revive")]
    public void RunTC04_EnemyRevive()
    {
        if (!TryValidateTarget(_enemyTarget, "TC-04 Enemy Revive"))
        {
            return;
        }

        _enemyTarget.Revive(30f);
        Debug.Log($"[HealthBasicTestRunner] TC-04 Current:{_enemyTarget.GetCurrentHealth()}");
    }

    /// <summary>
    /// 보스 대상의 최대 체력을 줄여 Clamp 동작을 검증합니다.
    /// </summary>
    [ContextMenu("TC-05 Boss SetMaxHealth Clamp")]
    public void RunTC05_BossSetMaxHealthClamp()
    {
        if (!TryValidateTarget(_bossTarget, "TC-05 Boss SetMaxHealth Clamp"))
        {
            return;
        }

        _bossTarget.SetMaxHealth(50f, false);
        Debug.Log($"[HealthBasicTestRunner] TC-05 Current:{_bossTarget.GetCurrentHealth()} Max:{_bossTarget.GetMaxHealth()}");
    }

    /// <summary>
    /// 파괴 오브젝트 대상에 즉시 파괴 수준의 데미지를 적용합니다.
    /// </summary>
    [ContextMenu("TC-06 Destructible Destroy")]
    public void RunTC06_DestructibleDestroy()
    {
        if (!TryValidateTarget(_destructibleTarget, "TC-06 Destructible Destroy"))
        {
            return;
        }

        DamageContext context = new DamageContext(
            amount: 999f,
            instigator: gameObject,
            sourceId: "TC-06_DestructibleDestroy",
            ignoreInvincibility: false,
            canKill: true,
            damageType: E_DamageType.Environmental);

        DamageResult result = _destructibleTarget.ApplyDamage(context);
        Debug.Log($"[HealthBasicTestRunner] TC-06 Died:{result.DidCauseDeath} Current:{result.CurrentHealth}");
    }

    /// <summary>
    /// 테스트 대상이 유효한지 검사합니다.
    /// </summary>
    private bool TryValidateTarget(HealthComponent target, string testCaseName)
    {
        if (target == null)
        {
            Debug.LogWarning($"[HealthBasicTestRunner] Missing target for {testCaseName} on {name}.");
            return false;
        }

        if (_isReady == false)
        {
            Debug.LogWarning($"[HealthBasicTestRunner] Targets are not ready for {testCaseName} on {name}.");
            return false;
        }

        if (target.IsInitialized == false)
        {
            Debug.LogWarning($"[HealthBasicTestRunner] Target is not initialized for {testCaseName} on {name}.");
            return false;
        }

        return true;
    }
}
