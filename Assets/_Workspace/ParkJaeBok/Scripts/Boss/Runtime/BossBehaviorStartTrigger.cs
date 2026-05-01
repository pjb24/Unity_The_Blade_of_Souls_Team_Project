using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 씬에 배치된 경우에만 기존 BossController 경로로 보스 행동 시작을 요청하는 전용 진입점입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossBehaviorStartTrigger : MonoBehaviour
{
    [Header("Start Target")]
    [Tooltip("이 Trigger가 행동 시작을 요청할 BossController입니다. 비어 있으면 동일 GameObject 또는 현재 씬에서 기존 검색 방식으로 보스를 찾습니다.")]
    [SerializeField] private BossController _targetBossController; // 이 컴포넌트가 StartBattle을 호출할 대상 보스 컨트롤러

    private bool _hasRequestedStart; // 같은 Trigger 인스턴스에서 보스 행동 시작 요청이 중복 실행되지 않도록 막는 플래그

    /// <summary>
    /// Inspector에서 비어 있는 참조를 같은 GameObject 기준으로 보정한다.
    /// </summary>
    private void Awake()
    {
        ResolveLocalBossController();
    }

    /// <summary>
    /// 씬 로드 완료 후 Unity Start 흐름에서 보스 행동 시작 요청을 1회 수행한다.
    /// </summary>
    private void Start()
    {
        RequestBossBehaviorStart();
    }

    /// <summary>
    /// Inspector 값 변경 시 같은 GameObject 기준으로 보스 참조를 보정한다.
    /// </summary>
    private void OnValidate()
    {
        ResolveLocalBossController();
    }

    /// <summary>
    /// 기존 BossController 시작 API로 보스 행동 시작을 요청한다.
    /// </summary>
    public void RequestBossBehaviorStart()
    {
        if (_hasRequestedStart)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 보스 행동 시작 요청이 이미 실행되어 중복 요청을 중단합니다. object={name}", this);
            return;
        }

        if (!HasStartAuthority())
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 권한 없는 Client에서 보스 행동 시작을 시도해 중단합니다. object={name}", this);
            return;
        }

        if (!TryResolveBossController(out BossController bossController))
        {
            return;
        }

        if (!CanStartBossBehavior(bossController))
        {
            return;
        }

        _hasRequestedStart = true;
        bossController.StartBattle();
    }

    /// <summary>
    /// 싱글플레이 또는 Host/Server 인스턴스인지 확인한다.
    /// </summary>
    private bool HasStartAuthority()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션 상태를 확인하기 위한 매니저 참조
        if (networkManager == null || !networkManager.IsListening)
        {
            return true;
        }

        return networkManager.IsServer;
    }

    /// <summary>
    /// Inspector 참조 또는 기존 검색 방식을 통해 시작 대상 BossController를 찾는다.
    /// </summary>
    private bool TryResolveBossController(out BossController bossController)
    {
        bossController = _targetBossController;
        if (bossController != null)
        {
            return true;
        }

        Debug.LogWarning($"[BossBehaviorStartTrigger] 시작 대상 보스 참조가 비어 있어 기존 검색 방식으로 보스를 찾습니다. object={name}", this);
        ResolveLocalBossController();
        bossController = _targetBossController;
        if (bossController != null)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 동일 GameObject에서 BossController를 fallback 검색으로 찾았습니다. object={name}, boss={bossController.name}", this);
            return true;
        }

        bossController = FindAnyObjectByType<BossController>();
        if (bossController != null)
        {
            _targetBossController = bossController;
            Debug.LogWarning($"[BossBehaviorStartTrigger] 현재 씬에서 BossController를 fallback 검색으로 찾았습니다. object={name}, boss={bossController.name}", this);
            return true;
        }

        Debug.LogWarning($"[BossBehaviorStartTrigger] 보스를 찾지 못해 행동 시작 요청을 중단합니다. object={name}", this);
        return false;
    }

    /// <summary>
    /// 같은 GameObject에 있는 BossController를 시작 대상으로 보정한다.
    /// </summary>
    private void ResolveLocalBossController()
    {
        if (_targetBossController == null)
        {
            _targetBossController = GetComponent<BossController>();
        }
    }

    /// <summary>
    /// 보스 행동 시작에 필요한 진입 조건, 실행 조건, 실패 조건을 검증한다.
    /// </summary>
    private bool CanStartBossBehavior(BossController bossController)
    {
        if (bossController == null)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] BossController가 없어 행동 시작 요청을 중단합니다. object={name}", this);
            return false;
        }

        if (bossController.HealthComponent == null)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 보스 전투 컨트롤러의 HealthComponent가 없어 행동 시작 요청을 중단합니다. object={name}, boss={bossController.name}", this);
            return false;
        }

        if (bossController.IsBattleActive)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 보스 행동이 이미 시작되어 중복 시작을 중단합니다. object={name}, boss={bossController.name}", this);
            return false;
        }

        if (bossController.IsDead())
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 보스가 이미 사망한 상태라 행동 시작을 중단합니다. object={name}, boss={bossController.name}", this);
            return false;
        }

        if (!bossController.IsBossLogicAuthority())
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 대상 보스에 대한 로컬 권한이 없어 행동 시작을 중단합니다. object={name}, boss={bossController.name}", this);
            return false;
        }

        return true;
    }
}
