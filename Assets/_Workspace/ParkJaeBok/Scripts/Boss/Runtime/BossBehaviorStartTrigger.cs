using System.Collections;
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
    [SerializeField] private BossController _targetBossController; // 이 컴포넌트가 StartBattle을 호출할 대상 보스 컨트롤러입니다.

    [Header("Network Timing")]
    [Tooltip("멀티플레이에서 보스 NetworkObject Spawn을 기다리는 최대 시간(초)입니다. Spawn 전에 시작하면 동기화가 누락될 수 있습니다.")]
    [Min(0.1f)]
    [SerializeField] private float _networkSpawnWaitTimeoutSeconds = 5f; // 멀티플레이 시작 시 보스 NetworkObject Spawn 완료를 기다리는 최대 시간입니다.

    private bool _hasRequestedStart; // 같은 Trigger 인스턴스에서 보스 행동 시작 요청이 중복 실행되지 않도록 막는 플래그입니다.
    private Coroutine _startRequestCoroutine; // 씬 로드 및 네트워크 Spawn 완료 이후 시작 요청을 수행하는 코루틴입니다.

    /// <summary>
    /// Inspector에서 비어 있는 참조를 같은 GameObject 기준으로 보정한다.
    /// </summary>
    private void Awake()
    {
        ResolveLocalBossController();
    }

    /// <summary>
    /// 씬 로드 완료 이후 보스 행동 시작 요청을 예약한다.
    /// </summary>
    private void Start()
    {
        RequestBossBehaviorStart();
    }

    /// <summary>
    /// 비활성화 시 진행 중인 시작 요청 코루틴을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (_startRequestCoroutine == null)
        {
            return;
        }

        StopCoroutine(_startRequestCoroutine);
        _startRequestCoroutine = null;
    }

    /// <summary>
    /// Inspector 값 변경 시 참조와 설정 값을 보정한다.
    /// </summary>
    private void OnValidate()
    {
        if (_networkSpawnWaitTimeoutSeconds < 0.1f)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] Network spawn wait timeout is too small. Fallback to 0.1. object={name}", this);
            _networkSpawnWaitTimeoutSeconds = 0.1f;
        }

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

        if (_startRequestCoroutine != null)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 보스 행동 시작 요청이 이미 대기 중이라 중복 요청을 중단합니다. object={name}", this);
            return;
        }

        _startRequestCoroutine = StartCoroutine(RequestBossBehaviorStartAfterReady());
    }

    /// <summary>
    /// 씬 초기화와 네트워크 Spawn 완료 이후 Host 권한으로 보스 행동 시작을 수행한다.
    /// </summary>
    private IEnumerator RequestBossBehaviorStartAfterReady()
    {
        yield return null;

        if (!TryResolveBossController(out BossController bossController))
        {
            _startRequestCoroutine = null;
            yield break;
        }

        if (!HasStartAuthority())
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 권한 없는 Client에서 보스 행동 시작을 시도해 중단합니다. object={name}", this);
            _startRequestCoroutine = null;
            yield break;
        }

        if (!WaitForNetworkSpawnIfNeeded(bossController, out IEnumerator waitRoutine))
        {
            _startRequestCoroutine = null;
            yield break;
        }

        if (waitRoutine != null)
        {
            yield return waitRoutine;
        }

        if (bossController == null)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 보스 Spawn 대기 후 BossController가 없어 행동 시작을 중단합니다. object={name}", this);
            _startRequestCoroutine = null;
            yield break;
        }

        if (IsNetworkSessionActive() && !bossController.IsSpawned)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 보스 NetworkObject가 Spawn되지 않아 행동 시작을 중단합니다. object={name}, boss={bossController.name}", this);
            _startRequestCoroutine = null;
            yield break;
        }

        if (!CanStartBossBehavior(bossController))
        {
            _startRequestCoroutine = null;
            yield break;
        }

        _hasRequestedStart = true;
        bossController.StartBattle();

        if (!bossController.IsBattleActive)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] StartBattle 호출 후 보스 전투가 활성화되지 않았습니다. object={name}, boss={bossController.name}", this);
        }

        _startRequestCoroutine = null;
    }

    /// <summary>
    /// 멀티플레이에서는 보스 NetworkObject Spawn 완료 후 시작할 수 있도록 대기 루틴을 준비한다.
    /// </summary>
    private bool WaitForNetworkSpawnIfNeeded(BossController bossController, out IEnumerator waitRoutine)
    {
        waitRoutine = null;
        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션 상태를 확인하기 위한 매니저 참조입니다.
        if (networkManager == null || !networkManager.IsListening)
        {
            return true;
        }

        NetworkObject bossNetworkObject = bossController.NetworkObject; // 보스 시작 상태를 Client에 동기화할 NetworkObject입니다.
        if (bossNetworkObject == null)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 멀티플레이 보스에 NetworkObject가 없어 행동 시작을 중단합니다. object={name}, boss={bossController.name}", this);
            return false;
        }

        if (bossController.IsSpawned)
        {
            return true;
        }

        waitRoutine = WaitForBossNetworkSpawn(bossController);
        return true;
    }

    /// <summary>
    /// 보스 NetworkObject가 Spawn될 때까지 제한 시간 동안 대기한다.
    /// </summary>
    private IEnumerator WaitForBossNetworkSpawn(BossController bossController)
    {
        float timeoutAt = Time.unscaledTime + _networkSpawnWaitTimeoutSeconds; // Spawn 대기 종료 시각입니다.
        while (bossController != null && !bossController.IsSpawned && Time.unscaledTime < timeoutAt)
        {
            yield return null;
        }

        if (bossController == null)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 보스 Spawn 대기 중 BossController가 사라져 행동 시작을 중단합니다. object={name}", this);
            yield break;
        }

        if (!bossController.IsSpawned)
        {
            Debug.LogWarning($"[BossBehaviorStartTrigger] 보스 NetworkObject Spawn 대기 시간이 초과되어 행동 시작을 중단합니다. object={name}, boss={bossController.name}", this);
        }
    }

    /// <summary>
    /// 싱글플레이 또는 Host/Server 인스턴스인지 확인한다.
    /// </summary>
    private bool HasStartAuthority()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션 상태를 확인하기 위한 매니저 참조입니다.
        if (!IsNetworkSessionActive())
        {
            return true;
        }

        return networkManager.IsServer;
    }

    /// <summary>
    /// NGO 네트워크 세션이 현재 활성 상태인지 확인한다.
    /// </summary>
    private bool IsNetworkSessionActive()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션 상태를 확인하기 위한 매니저 참조입니다.
        return networkManager != null && networkManager.IsListening;
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
            Debug.LogWarning($"[BossBehaviorStartTrigger] 보스 전투 컨트롤러에 HealthComponent가 없어 행동 시작 요청을 중단합니다. object={name}, boss={bossController.name}", this);
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
            Debug.LogWarning($"[BossBehaviorStartTrigger] 대상 보스에 대한 로직 권한이 없어 행동 시작을 중단합니다. object={name}, boss={bossController.name}", this);
            return false;
        }

        return true;
    }
}
