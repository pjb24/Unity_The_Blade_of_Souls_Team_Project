using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 지정된 연속 보스 씬 전환 사이에서 플레이어 체력 스냅샷을 보존하고 새 씬의 플레이어에게 다시 적용합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHealthScenePersistenceService : MonoBehaviour
{
    [Serializable]
    private struct SceneHealthCarryRule
    {
        [Tooltip("체력을 저장해야 하는 출발 씬 이름입니다.")]
        [SerializeField] private string _fromSceneName; // 체력 보존 규칙이 시작되는 출발 씬 이름입니다.

        [Tooltip("저장된 체력을 적용해야 하는 도착 씬 이름입니다.")]
        [SerializeField] private string _toSceneName; // 체력 보존 규칙이 끝나는 도착 씬 이름입니다.

        /// <summary>
        /// 출발 씬 이름을 반환합니다.
        /// </summary>
        public string FromSceneName => _fromSceneName;

        /// <summary>
        /// 도착 씬 이름을 반환합니다.
        /// </summary>
        public string ToSceneName => _toSceneName;

        /// <summary>
        /// 씬 이름 쌍을 보존 규칙으로 생성합니다.
        /// </summary>
        public SceneHealthCarryRule(string fromSceneName, string toSceneName)
        {
            _fromSceneName = fromSceneName;
            _toSceneName = toSceneName;
        }
    }

    private struct HealthSnapshot
    {
        public float CurrentHealth; // 전환 직전 플레이어의 현재 체력입니다.
        public float MaxHealth; // 전환 직전 플레이어의 최대 체력입니다.
        public bool IsDead; // 전환 직전 플레이어의 사망 상태입니다.
    }

    private const string SinglePlayerSnapshotKey = "SinglePlayer";
    private const string UnknownPlayerSnapshotKey = "Unknown";

    private static PlayerHealthScenePersistenceService _instance; // 중복 방지를 위한 전역 서비스 참조입니다.

    [Header("Lifecycle")]
    [Tooltip("씬 전환 이후에도 체력 보존 서비스를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // 연속 씬 전환 사이에 스냅샷을 유지하기 위한 생명주기 옵션입니다.

    [Header("Carry Rules")]
    [Tooltip("체력을 이어받아야 하는 씬 전환 규칙 목록입니다.")]
    [SerializeField]
    private SceneHealthCarryRule[] _carryRules =
    {
        new SceneHealthCarryRule("1 Chapter", "BossMap_Hallway"),
        new SceneHealthCarryRule("BossMap_Hallway", "BossMap_Main"),
    }; // 디자이너가 씬 이름 변경에 맞춰 수정할 수 있는 체력 보존 전환 규칙입니다.

    [Header("Apply Policy")]
    [Tooltip("새 씬 로드 직후 플레이어가 아직 준비되지 않았을 때 체력 적용을 재시도할 횟수입니다.")]
    [Min(1)]
    [SerializeField] private int _applyRetryCount = 30; // PlayerObject 생성 타이밍 차이를 흡수하기 위한 재시도 횟수입니다.

    [Tooltip("체력 적용 재시도 사이의 대기 시간(초)입니다.")]
    [Min(0.01f)]
    [SerializeField] private float _applyRetryIntervalSeconds = 0.1f; // PlayerObject 생성 타이밍 차이를 흡수하기 위한 재시도 간격입니다.

    [Tooltip("도착 씬에 스냅샷을 한 번 이상 적용한 뒤 스냅샷을 비울지 여부입니다.")]
    [SerializeField] private bool _clearSnapshotAfterSuccessfulApply = true; // 같은 전환 스냅샷이 이후 무관한 씬에 재사용되는 것을 막는 정책입니다.

    [Header("Debug")]
    [Tooltip("체력 저장과 적용 과정을 Console에 자세히 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLogging; // 체력 보존 흐름을 추적하기 위한 상세 로그 옵션입니다.

    [Tooltip("디버그용: 가장 최근 체력 스냅샷을 저장한 출발 씬 이름입니다.")]
    [SerializeField] private string _lastSnapshotSourceSceneName; // Inspector에서 최근 저장 출발 씬을 확인하기 위한 디버그 값입니다.

    [Tooltip("디버그용: 가장 최근 체력 스냅샷을 적용할 도착 씬 이름입니다.")]
    [SerializeField] private string _pendingSnapshotTargetSceneName; // Inspector에서 현재 적용 대기 도착 씬을 확인하기 위한 디버그 값입니다.

    [Tooltip("디버그용: 현재 보존 중인 플레이어 체력 스냅샷 수입니다.")]
    [SerializeField] private int _snapshotCount; // Inspector에서 현재 보존 중인 플레이어 수를 확인하기 위한 디버그 값입니다.

    private readonly Dictionary<string, HealthSnapshot> _snapshotsByPlayerKey = new Dictionary<string, HealthSnapshot>(); // 플레이어 식별자별 체력 스냅샷 저장소입니다.
    private readonly HashSet<string> _appliedSnapshotKeys = new HashSet<string>(); // 현재 도착 씬에서 이미 적용한 플레이어 스냅샷 식별자 목록입니다.
    private SceneTransitionService _sceneTransitionService; // 씬 전환 전후 이벤트를 구독하기 위한 전환 서비스 참조입니다.
    private Coroutine _applyRoutine; // 씬 로드 후 지연 생성되는 플레이어에게 체력을 적용하는 재시도 루틴입니다.
    private bool _isSubscribedToTransitionService; // SceneTransitionService 이벤트 구독 상태를 추적하는 플래그입니다.

    /// <summary>
    /// 중복 서비스를 정리하고 전환 서비스 구독을 준비합니다.
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    /// <summary>
    /// 서비스 활성화 시 씬 전환 이벤트와 일반 씬 로드 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        TrySubscribeTransitionService();
        SceneManager.sceneLoaded += HandleSceneLoadedFallback;
    }

    /// <summary>
    /// 서비스 비활성화 시 등록한 이벤트를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeTransitionService();
        SceneManager.sceneLoaded -= HandleSceneLoadedFallback;
    }

    /// <summary>
    /// 매 프레임 SceneTransitionService 준비 지연을 보정합니다.
    /// </summary>
    private void Update()
    {
        if (!_isSubscribedToTransitionService)
        {
            TrySubscribeTransitionService();
        }
    }

    /// <summary>
    /// SceneTransitionService 이벤트를 구독합니다.
    /// </summary>
    private void TrySubscribeTransitionService()
    {
        if (_isSubscribedToTransitionService)
        {
            return;
        }

        if (!SceneTransitionService.TryGetExistingInstance(out _sceneTransitionService) || _sceneTransitionService == null)
        {
            return;
        }

        _sceneTransitionService.OnBeforeSceneLoad += HandleBeforeSceneLoad;
        _sceneTransitionService.OnAfterSceneLoad += HandleAfterSceneLoad;
        _isSubscribedToTransitionService = true;
    }

    /// <summary>
    /// SceneTransitionService 이벤트 구독을 해제합니다.
    /// </summary>
    private void UnsubscribeTransitionService()
    {
        if (!_isSubscribedToTransitionService || _sceneTransitionService == null)
        {
            return;
        }

        _sceneTransitionService.OnBeforeSceneLoad -= HandleBeforeSceneLoad;
        _sceneTransitionService.OnAfterSceneLoad -= HandleAfterSceneLoad;
        _sceneTransitionService = null;
        _isSubscribedToTransitionService = false;
    }

    /// <summary>
    /// 보존 대상 씬 전환이 시작되면 현재 플레이어 체력 스냅샷을 저장합니다.
    /// </summary>
    private void HandleBeforeSceneLoad(string targetSceneName)
    {
        string activeSceneName = SceneManager.GetActiveScene().name; // 규칙 판정에 사용할 현재 출발 씬 이름입니다.
        if (!TryFindCarryRule(activeSceneName, targetSceneName, out SceneHealthCarryRule carryRule))
        {
            return;
        }

        CapturePlayerHealthSnapshots();
        _lastSnapshotSourceSceneName = carryRule.FromSceneName;
        _pendingSnapshotTargetSceneName = carryRule.ToSceneName;
        _snapshotCount = _snapshotsByPlayerKey.Count;

        if (_verboseLogging)
        {
            Debug.Log($"[PlayerHealthScenePersistenceService] 체력 스냅샷 저장 완료. from={activeSceneName}, to={targetSceneName}, count={_snapshotCount}", this);
        }
    }

    /// <summary>
    /// 보존 대상 도착 씬이 로드되면 저장된 체력 스냅샷 적용을 시작합니다.
    /// </summary>
    private void HandleAfterSceneLoad(string loadedSceneName)
    {
        if (!HasPendingSnapshotForScene(loadedSceneName))
        {
            return;
        }

        StartApplyRoutine(loadedSceneName);
    }

    /// <summary>
    /// SceneTransitionService가 없는 직접 로드 경로에서도 도착 씬 적용을 보정합니다.
    /// </summary>
    private void HandleSceneLoadedFallback(Scene loadedScene, LoadSceneMode loadSceneMode)
    {
        if (!HasPendingSnapshotForScene(loadedScene.name))
        {
            return;
        }

        StartApplyRoutine(loadedScene.name);
    }

    /// <summary>
    /// 저장된 스냅샷을 새 씬 플레이어에게 적용하는 재시도 루틴을 시작합니다.
    /// </summary>
    private void StartApplyRoutine(string loadedSceneName)
    {
        if (_applyRoutine != null)
        {
            StopCoroutine(_applyRoutine);
        }

        _applyRoutine = StartCoroutine(ApplySnapshotsWhenPlayersReadyRoutine(loadedSceneName));
    }

    /// <summary>
    /// 플레이어 오브젝트가 준비될 때까지 재시도하면서 체력 스냅샷을 적용합니다.
    /// </summary>
    private IEnumerator ApplySnapshotsWhenPlayersReadyRoutine(string loadedSceneName)
    {
        int safeRetryCount = Mathf.Max(1, _applyRetryCount); // 재시도 횟수 하한을 보장한 값입니다.
        float safeRetryInterval = Mathf.Max(0.01f, _applyRetryIntervalSeconds); // 재시도 간격 하한을 보장한 값입니다.
        bool appliedAnySnapshot = false; // 이번 도착 씬에서 하나 이상의 스냅샷 적용에 성공했는지 추적합니다.

        for (int retryIndex = 0; retryIndex < safeRetryCount; retryIndex++)
        {
            appliedAnySnapshot |= TryApplySnapshotsToCurrentPlayers();
            if (HasAppliedAllSnapshots())
            {
                break;
            }

            yield return new WaitForSecondsRealtime(safeRetryInterval);
        }

        if (appliedAnySnapshot && _clearSnapshotAfterSuccessfulApply && HasAppliedAllSnapshots())
        {
            ClearSnapshots();
        }
        else if (appliedAnySnapshot)
        {
            Debug.LogWarning($"[PlayerHealthScenePersistenceService] 일부 플레이어 체력 스냅샷만 적용했습니다. scene={loadedSceneName}, applied={_appliedSnapshotKeys.Count}, total={_snapshotsByPlayerKey.Count}", this);
        }
        else if (!appliedAnySnapshot)
        {
            Debug.LogWarning($"[PlayerHealthScenePersistenceService] 도착 씬에서 체력 스냅샷을 적용할 플레이어를 찾지 못했습니다. scene={loadedSceneName}, count={_snapshotsByPlayerKey.Count}", this);
        }

        _applyRoutine = null;
    }

    /// <summary>
    /// 현재 씬에 존재하는 플레이어 HealthComponent 목록에 저장된 체력을 적용합니다.
    /// </summary>
    private bool TryApplySnapshotsToCurrentPlayers()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 싱글/Host/Client 분기를 판정하기 위한 NGO 세션 참조입니다.
        if (networkManager != null && networkManager.IsListening)
        {
            return TryApplySnapshotsToNetworkPlayers(networkManager);
        }

        return TryApplySnapshotToSinglePlayer();
    }

    /// <summary>
    /// 네트워크 세션의 플레이어 오브젝트에 OwnerClientId 기준으로 체력 스냅샷을 적용합니다.
    /// </summary>
    private bool TryApplySnapshotsToNetworkPlayers(NetworkManager networkManager)
    {
        bool appliedAnySnapshot = false; // 네트워크 플레이어 중 하나 이상에게 적용했는지 추적합니다.

        foreach (KeyValuePair<ulong, NetworkClient> clientPair in networkManager.ConnectedClients)
        {
            NetworkClient client = clientPair.Value; // 현재 확인 중인 접속 클라이언트 정보입니다.
            if (client == null || client.PlayerObject == null)
            {
                continue;
            }

            string playerKey = BuildNetworkPlayerKey(clientPair.Key);
            if (_appliedSnapshotKeys.Contains(playerKey))
            {
                continue;
            }

            if (!_snapshotsByPlayerKey.TryGetValue(playerKey, out HealthSnapshot snapshot))
            {
                continue;
            }

            if (!TryResolveHealthFromNetworkObject(client.PlayerObject, out HealthComponent healthComponent))
            {
                continue;
            }

            ApplySnapshotToHealth(healthComponent, snapshot);
            _appliedSnapshotKeys.Add(playerKey);
            appliedAnySnapshot = true;
        }

        if (!appliedAnySnapshot && networkManager.LocalClient != null && networkManager.LocalClient.PlayerObject != null)
        {
            appliedAnySnapshot = TryApplyLocalClientFallbackSnapshot(networkManager);
        }

        return appliedAnySnapshot;
    }

    /// <summary>
    /// Client 화면에서 서버 복제 도착 전까지 로컬 스냅샷을 임시로 적용합니다.
    /// </summary>
    private bool TryApplyLocalClientFallbackSnapshot(NetworkManager networkManager)
    {
        if (!_snapshotsByPlayerKey.TryGetValue(SinglePlayerSnapshotKey, out HealthSnapshot snapshot))
        {
            return false;
        }

        if (_appliedSnapshotKeys.Contains(SinglePlayerSnapshotKey))
        {
            return false;
        }

        if (!TryResolveHealthFromNetworkObject(networkManager.LocalClient.PlayerObject, out HealthComponent healthComponent))
        {
            return false;
        }

        ApplySnapshotToHealth(healthComponent, snapshot);
        _appliedSnapshotKeys.Add(SinglePlayerSnapshotKey);
        return true;
    }

    /// <summary>
    /// 싱글플레이 로컬 플레이어에게 저장된 체력 스냅샷을 적용합니다.
    /// </summary>
    private bool TryApplySnapshotToSinglePlayer()
    {
        if (!_snapshotsByPlayerKey.TryGetValue(SinglePlayerSnapshotKey, out HealthSnapshot snapshot)
            && !_snapshotsByPlayerKey.TryGetValue(UnknownPlayerSnapshotKey, out snapshot))
        {
            return false;
        }

        string snapshotKey = _snapshotsByPlayerKey.ContainsKey(SinglePlayerSnapshotKey) ? SinglePlayerSnapshotKey : UnknownPlayerSnapshotKey; // 싱글플레이 적용 완료 여부를 기록할 스냅샷 식별자입니다.
        if (_appliedSnapshotKeys.Contains(snapshotKey))
        {
            return false;
        }

        if (!TryResolveSinglePlayerHealth(out HealthComponent healthComponent))
        {
            return false;
        }

        ApplySnapshotToHealth(healthComponent, snapshot);
        _appliedSnapshotKeys.Add(snapshotKey);
        return true;
    }

    /// <summary>
    /// 현재 존재하는 플레이어들의 체력 스냅샷을 저장합니다.
    /// </summary>
    private void CapturePlayerHealthSnapshots()
    {
        _snapshotsByPlayerKey.Clear();
        _appliedSnapshotKeys.Clear();

        NetworkManager networkManager = NetworkManager.Singleton; // 멀티플레이 세션 여부와 PlayerObject 목록을 확인하는 NGO 참조입니다.
        if (networkManager != null && networkManager.IsListening)
        {
            CaptureNetworkPlayerHealthSnapshots(networkManager);
        }
        else
        {
            CaptureSinglePlayerHealthSnapshot();
        }

        _snapshotCount = _snapshotsByPlayerKey.Count;
    }

    /// <summary>
    /// 네트워크 플레이어들의 체력 스냅샷을 OwnerClientId 기준으로 저장합니다.
    /// </summary>
    private void CaptureNetworkPlayerHealthSnapshots(NetworkManager networkManager)
    {
        foreach (KeyValuePair<ulong, NetworkClient> clientPair in networkManager.ConnectedClients)
        {
            NetworkClient client = clientPair.Value; // 체력을 저장할 대상 클라이언트 정보입니다.
            if (client == null || client.PlayerObject == null)
            {
                continue;
            }

            if (!TryResolveHealthFromNetworkObject(client.PlayerObject, out HealthComponent healthComponent))
            {
                continue;
            }

            _snapshotsByPlayerKey[BuildNetworkPlayerKey(clientPair.Key)] = CreateSnapshot(healthComponent);
        }

        if (_snapshotsByPlayerKey.Count > 0)
        {
            return;
        }

        CaptureSinglePlayerHealthSnapshot();
    }

    /// <summary>
    /// 싱글플레이 로컬 플레이어 체력 스냅샷을 저장합니다.
    /// </summary>
    private void CaptureSinglePlayerHealthSnapshot()
    {
        if (!TryResolveSinglePlayerHealth(out HealthComponent healthComponent))
        {
            return;
        }

        _snapshotsByPlayerKey[SinglePlayerSnapshotKey] = CreateSnapshot(healthComponent);
    }

    /// <summary>
    /// HealthComponent에서 현재 체력 상태 스냅샷을 생성합니다.
    /// </summary>
    private HealthSnapshot CreateSnapshot(HealthComponent healthComponent)
    {
        return new HealthSnapshot
        {
            CurrentHealth = healthComponent.GetCurrentHealth(),
            MaxHealth = healthComponent.GetMaxHealth(),
            IsDead = healthComponent.IsDead
        };
    }

    /// <summary>
    /// 저장된 체력 스냅샷을 HealthComponent에 적용합니다.
    /// </summary>
    private void ApplySnapshotToHealth(HealthComponent healthComponent, HealthSnapshot snapshot)
    {
        if (healthComponent == null)
        {
            return;
        }

        float safeMaxHealth = Mathf.Max(1f, snapshot.MaxHealth); // HealthCore가 허용하는 안전한 최대 체력 값입니다.
        float safeCurrentHealth = Mathf.Clamp(snapshot.CurrentHealth, 0f, safeMaxHealth); // 최대 체력 범위 안으로 보정한 현재 체력 값입니다.

        healthComponent.SetMaxHealth(safeMaxHealth, false);
        if (snapshot.IsDead || safeCurrentHealth <= 0f)
        {
            healthComponent.SetCurrentHealth(0f);
            return;
        }

        if (healthComponent.IsDead)
        {
            healthComponent.Revive(Mathf.Max(0.01f, safeCurrentHealth));
            return;
        }

        healthComponent.SetCurrentHealth(safeCurrentHealth);
    }

    /// <summary>
    /// NetworkObject 계층에서 HealthComponent를 찾습니다.
    /// </summary>
    private bool TryResolveHealthFromNetworkObject(NetworkObject networkObject, out HealthComponent healthComponent)
    {
        healthComponent = null;
        if (networkObject == null)
        {
            return false;
        }

        if (networkObject.TryGetComponent(out healthComponent))
        {
            return true;
        }

        healthComponent = networkObject.GetComponentInChildren<HealthComponent>(true);
        if (healthComponent != null)
        {
            return true;
        }

        healthComponent = networkObject.GetComponentInParent<HealthComponent>();
        return healthComponent != null;
    }

    /// <summary>
    /// 싱글플레이에서 로컬 플레이어 HealthComponent를 찾습니다.
    /// </summary>
    private bool TryResolveSinglePlayerHealth(out HealthComponent healthComponent)
    {
        healthComponent = null;

        PlayerInputDriver[] inputDrivers = FindObjectsByType<PlayerInputDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 입력 소유 플레이어 후보 목록입니다.
        for (int index = 0; index < inputDrivers.Length; index++)
        {
            PlayerInputDriver inputDriver = inputDrivers[index]; // 현재 검사 중인 입력 드라이버입니다.
            if (inputDriver == null)
            {
                continue;
            }

            if (TryResolveHealthFromComponent(inputDriver, out healthComponent))
            {
                return true;
            }
        }

        GameObject taggedPlayer = GameObject.FindWithTag("Player"); // 태그 기반 fallback으로 찾은 플레이어 오브젝트입니다.
        if (taggedPlayer != null && TryResolveHealthFromComponent(taggedPlayer.transform, out healthComponent))
        {
            return true;
        }

        PlayerMovement[] playerMovements = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 이동 컴포넌트 기반 fallback 후보 목록입니다.
        for (int index = 0; index < playerMovements.Length; index++)
        {
            PlayerMovement movement = playerMovements[index]; // 현재 검사 중인 이동 컴포넌트입니다.
            if (movement == null)
            {
                continue;
            }

            if (TryResolveHealthFromComponent(movement, out healthComponent))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 컴포넌트 계층에서 HealthComponent를 찾습니다.
    /// </summary>
    private bool TryResolveHealthFromComponent(Component component, out HealthComponent healthComponent)
    {
        healthComponent = null;
        if (component == null)
        {
            return false;
        }

        healthComponent = component.GetComponent<HealthComponent>();
        if (healthComponent != null)
        {
            return true;
        }

        healthComponent = component.GetComponentInChildren<HealthComponent>(true);
        if (healthComponent != null)
        {
            return true;
        }

        healthComponent = component.GetComponentInParent<HealthComponent>();
        return healthComponent != null;
    }

    /// <summary>
    /// 현재 대기 중인 스냅샷이 지정 씬에 적용되어야 하는지 확인합니다.
    /// </summary>
    private bool HasPendingSnapshotForScene(string sceneName)
    {
        if (_snapshotsByPlayerKey.Count == 0 || string.IsNullOrWhiteSpace(_pendingSnapshotTargetSceneName))
        {
            return false;
        }

        return IsSameSceneName(_pendingSnapshotTargetSceneName, sceneName);
    }

    /// <summary>
    /// 현재 보존 중인 모든 플레이어 스냅샷을 도착 씬에 적용했는지 확인합니다.
    /// </summary>
    private bool HasAppliedAllSnapshots()
    {
        return _snapshotsByPlayerKey.Count > 0 && _appliedSnapshotKeys.Count >= _snapshotsByPlayerKey.Count;
    }

    /// <summary>
    /// 출발/도착 씬 이름 쌍에 맞는 체력 보존 규칙을 찾습니다.
    /// </summary>
    private bool TryFindCarryRule(string fromSceneName, string toSceneName, out SceneHealthCarryRule carryRule)
    {
        carryRule = default;
        if (_carryRules == null)
        {
            return false;
        }

        for (int index = 0; index < _carryRules.Length; index++)
        {
            SceneHealthCarryRule candidate = _carryRules[index]; // 현재 검사 중인 씬 체력 보존 규칙입니다.
            if (IsSameSceneName(candidate.FromSceneName, fromSceneName) && IsSameSceneName(candidate.ToSceneName, toSceneName))
            {
                carryRule = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 대소문자와 양끝 공백을 무시하고 씬 이름이 같은지 비교합니다.
    /// </summary>
    private bool IsSameSceneName(string lhs, string rhs)
    {
        if (string.IsNullOrWhiteSpace(lhs) || string.IsNullOrWhiteSpace(rhs))
        {
            return false;
        }

        return string.Equals(lhs.Trim(), rhs.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 네트워크 플레이어 스냅샷 식별자를 생성합니다.
    /// </summary>
    private string BuildNetworkPlayerKey(ulong clientId)
    {
        return $"Client:{clientId}";
    }

    /// <summary>
    /// 저장된 체력 스냅샷과 디버그 상태를 초기화합니다.
    /// </summary>
    private void ClearSnapshots()
    {
        _snapshotsByPlayerKey.Clear();
        _appliedSnapshotKeys.Clear();
        _lastSnapshotSourceSceneName = string.Empty;
        _pendingSnapshotTargetSceneName = string.Empty;
        _snapshotCount = 0;
    }
}
