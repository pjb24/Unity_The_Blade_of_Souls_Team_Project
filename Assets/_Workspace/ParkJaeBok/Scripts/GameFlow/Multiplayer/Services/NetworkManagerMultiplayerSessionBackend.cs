using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unity Netcode의 NetworkManager를 사용해 Host/Client 세션 생성과 참가를 처리하는 백엔드 구현입니다.
/// </summary>
public class NetworkManagerMultiplayerSessionBackend : MonoBehaviour, IMultiplayerSessionBackend
{
    [Header("Dependencies")]
    [Tooltip("멀티플레이 네트워크 수립을 수행할 NetworkManager 참조입니다. 비어 있으면 NetworkManager.Singleton을 사용합니다.")]
    [SerializeField] private NetworkManager _networkManager; // Host/Client 시작/종료를 위임할 NetworkManager 참조입니다.

    [Tooltip("Host/Client 자동 스폰에 사용할 공통 Player Prefab입니다. Phase 1 요구사항에 따라 단일 프리팹만 사용합니다.")]
    [SerializeField] private GameObject _playerPrefab; // NetworkManager.NetworkConfig.PlayerPrefab으로 연결할 단일 플레이어 프리팹 참조입니다.

    [Tooltip("NetworkManager에 기본 등록할 네트워크 프리팹 목록입니다. Player 포함 추가 네트워크 오브젝트를 등록할 때 사용합니다.")]
    [SerializeField] private List<GameObject> _defaultNetworkPrefabs = new(); // 세션 시작 전에 AddNetworkPrefab으로 등록할 기본 네트워크 프리팹 목록입니다.

    [Tooltip("런타임에서 Default Network Prefabs를 AddNetworkPrefab으로 추가 등록할지 여부입니다. 중복 등록 에러 방지를 위해 기본값은 false를 권장합니다.")]
    [SerializeField] private bool _registerDefaultNetworkPrefabsAtRuntime; // 기본 네트워크 프리팹 목록을 런타임에 추가 등록할지 제어하는 정책 값입니다.

    [Tooltip("Player Prefab 자동 스폰을 Server 권한으로 수행할지 여부입니다. Host 플레이어 누락을 방지하려면 false(기본값)를 권장합니다.")]
    [SerializeField] private bool _autoSpawnPlayerPrefabClientSide = false; // NetworkConfig.AutoSpawnPlayerPrefabClientSide에 반영할 정책 값입니다.

    [Tooltip("씬 전환 후 Host/Server 관점에서 로컬 PlayerObject가 사라졌을 때 자동 재스폰을 시도할지 여부입니다.")]
    [SerializeField] private bool _ensureHostPlayerObjectAfterSceneLoad = true; // 씬 전환으로 PlayerObject가 파괴된 경우 Host 로컬 플레이어 재스폰을 시도할지 제어하는 플래그입니다.

    [Tooltip("싱글플레이 씬 로드 후 Player 오브젝트가 없으면 _playerPrefab을 로컬 인스턴스로 보충 생성할지 여부입니다.")]
    [SerializeField] private bool _spawnSinglePlayerFallbackIfMissing = true; // 싱글플레이에서 플레이어 누락 시 로컬 플레이어 프리팹을 자동 생성할지 제어하는 플래그입니다.

    [Header("Policy")]
    [Tooltip("Host가 생성한 Join Code를 저장해 UI에 노출할 때 사용할 기본 문자열입니다.")]
    [SerializeField] private string _defaultJoinCode = "LOCAL"; // Host 시작 성공 시 Join Code로 반환할 기본 문자열입니다.

    [Tooltip("디버그 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLogging; // 백엔드 동작 로그 출력 여부입니다.

    private string _activeJoinCode; // 현재 런타임 세션에서 사용 중인 Join Code 캐시입니다.
    private int _cachedMaxPlayerCount = 2; // 세션 정원 판정에 사용할 최대 인원 수 캐시입니다.
    private bool _isStageInProgress; // 중도 Join 금지 정책을 반영하기 위한 Stage 진행 상태 캐시입니다.
    private readonly HashSet<int> _registeredNetworkPrefabIds = new(); // AddNetworkPrefab 중복 등록을 방지하기 위해 기록하는 프리팹 InstanceID 집합입니다.
    private bool _networkCallbacksRegistered; // 현재 NetworkManager 연결/해제 콜백이 등록된 상태인지 추적하는 플래그입니다.
    private GameFlowController _cachedGameFlowController; // 플레이 모드 판별에 사용할 GameFlowController 캐시 참조입니다.

    /// <summary>
    /// 씬 로드 콜백을 구독해 씬 전환 이후 Host PlayerObject 유지 보정 기능을 활성화합니다.
    /// </summary>
    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>
    /// 오브젝트 비활성화 시 씬 로드 콜백 구독을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (TryResolveNetworkManager(out NetworkManager networkManager, out _))
        {
            UnregisterNetworkCallbacks(networkManager);
        }
    }

    /// <summary>
    /// Host 세션을 생성하고 Join Code를 발급합니다.
    /// </summary>
    public bool TryCreateSession(string hostClientId, int maxPlayerCount, out string joinCode, out string reason)
    {
        joinCode = string.Empty;
        reason = string.Empty;

        if (!TryResolveNetworkManager(out NetworkManager networkManager, out reason))
        {
            return false;
        }

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        ConfigureNetworkPrefabs(networkManager);
        RegisterNetworkCallbacks(networkManager);

        bool started = networkManager.StartHost();
        if (!started)
        {
            reason = "StartHostFailed";
            return false;
        }

        _cachedMaxPlayerCount = Mathf.Max(1, maxPlayerCount);
        _isStageInProgress = false;
        _activeJoinCode = string.IsNullOrWhiteSpace(_defaultJoinCode) ? "LOCAL" : _defaultJoinCode.Trim().ToUpperInvariant();
        joinCode = _activeJoinCode;

        if (_verboseLogging)
        {
            Debug.Log($"[NetworkManagerMultiplayerSessionBackend] Host started. hostClientId={hostClientId}, joinCode={joinCode}", this);
        }

        return true;
    }

    /// <summary>
    /// Join Code 기반 참가를 시도합니다.
    /// </summary>
    public bool TryJoinSession(string joinCode, string clientId, out string reason)
    {
        reason = string.Empty;
        if (_isStageInProgress)
        {
            reason = "StageInProgress";
            return false;
        }

        if (!TryResolveNetworkManager(out NetworkManager networkManager, out reason))
        {
            return false;
        }

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        ConfigureNetworkPrefabs(networkManager);
        RegisterNetworkCallbacks(networkManager);

        bool started = networkManager.StartClient();
        if (!started)
        {
            reason = "StartClientFailed";
            return false;
        }

        _activeJoinCode = string.IsNullOrWhiteSpace(joinCode)
            ? (string.IsNullOrWhiteSpace(_defaultJoinCode) ? "LOCAL" : _defaultJoinCode.Trim().ToUpperInvariant())
            : joinCode.Trim().ToUpperInvariant();

        if (_verboseLogging)
        {
            Debug.Log($"[NetworkManagerMultiplayerSessionBackend] Client started. clientId={clientId}, joinCode={_activeJoinCode}", this);
        }

        return true;
    }

    /// <summary>
    /// 세션의 Stage 진행 상태를 갱신합니다.
    /// </summary>
    public void SetStageInProgress(string joinCode, bool isInProgress)
    {
        _isStageInProgress = isInProgress;
    }

    /// <summary>
    /// 세션의 현재 참가 인원 수를 조회합니다.
    /// </summary>
    public bool TryGetPlayerCount(string joinCode, out int playerCount)
    {
        playerCount = 0;
        if (!TryResolveNetworkManager(out NetworkManager networkManager, out _))
        {
            return false;
        }

        if (!networkManager.IsListening)
        {
            return false;
        }

        playerCount = networkManager.ConnectedClientsList != null ? networkManager.ConnectedClientsList.Count : 0;
        return true;
    }

    /// <summary>
    /// Client가 로드 완료/준비 완료 상태임을 세션에 기록합니다.
    /// </summary>
    public bool TryMarkClientReady(string joinCode, string clientId, out string reason)
    {
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Host가 Stage 진입을 시작해도 되는 세션 준비 완료 상태인지 조회합니다.
    /// </summary>
    public bool TryGetStageEntryReady(string joinCode, out bool isReady, out string reason)
    {
        isReady = false;
        reason = string.Empty;

        if (!TryGetPlayerCount(joinCode, out int playerCount))
        {
            reason = "PlayerCountUnavailable";
            return false;
        }

        isReady = playerCount >= Mathf.Max(1, _cachedMaxPlayerCount);
        if (!isReady)
        {
            reason = "PlayersNotFull";
        }

        return true;
    }

    /// <summary>
    /// 세션을 종료합니다.
    /// </summary>
    public void CloseSession(string joinCode)
    {
        if (!TryResolveNetworkManager(out NetworkManager networkManager, out _))
        {
            return;
        }

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        UnregisterNetworkCallbacks(networkManager);
        _activeJoinCode = string.Empty;
        _isStageInProgress = false;
    }

    /// <summary>
    /// NetworkManager 참조를 확인하고 누락 시 Singleton으로 자동 해석합니다.
    /// </summary>
    private bool TryResolveNetworkManager(out NetworkManager networkManager, out string reason)
    {
        networkManager = _networkManager != null ? _networkManager : NetworkManager.Singleton;
        _networkManager = networkManager;
        reason = string.Empty;

        if (networkManager != null)
        {
            return true;
        }

        reason = "NetworkManagerMissing";
        return false;
    }

    /// <summary>
    /// 세션 시작 전에 PlayerPrefab/DefaultNetworkPrefabs를 NetworkManager에 반영합니다.
    /// </summary>
    private void ConfigureNetworkPrefabs(NetworkManager networkManager)
    {
        if (networkManager == null)
        {
            return;
        }

        if (_playerPrefab != null)
        {
            networkManager.NetworkConfig.PlayerPrefab = _playerPrefab;
            networkManager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = _autoSpawnPlayerPrefabClientSide;
        }

        if (!_registerDefaultNetworkPrefabsAtRuntime)
        {
            return;
        }

        if (_defaultNetworkPrefabs == null || _defaultNetworkPrefabs.Count == 0)
        {
            return;
        }

        for (int index = 0; index < _defaultNetworkPrefabs.Count; index++)
        {
            GameObject prefab = _defaultNetworkPrefabs[index];
            if (prefab == _playerPrefab)
            {
                continue;
            }

            RegisterNetworkPrefabIfNeeded(networkManager, prefab);
        }
    }

    /// <summary>
    /// 동일 프리팹 중복 등록 없이 NetworkManager.AddNetworkPrefab을 수행합니다.
    /// </summary>
    private void RegisterNetworkPrefabIfNeeded(NetworkManager networkManager, GameObject prefab)
    {
        if (networkManager == null || prefab == null)
        {
            return;
        }

        int prefabId = prefab.GetInstanceID();
        if (_registeredNetworkPrefabIds.Contains(prefabId))
        {
            return;
        }

        networkManager.AddNetworkPrefab(prefab);
        _registeredNetworkPrefabIds.Add(prefabId);
    }

    /// <summary>
    /// 씬 로드 이후 Host 로컬 PlayerObject가 누락되었는지 점검하고 필요 시 재스폰을 시도합니다.
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySpawnMissingSinglePlayerLocalPlayer();

        if (!_ensureHostPlayerObjectAfterSceneLoad)
        {
            return;
        }

        if (!TryResolveNetworkManager(out NetworkManager networkManager, out _))
        {
            return;
        }

        if (!networkManager.IsListening || !networkManager.IsServer)
        {
            return;
        }

        ulong[] connectedClientIds = new ulong[networkManager.ConnectedClientsIds.Count]; // 씬 로드 시점에 재스폰 점검할 현재 연결 클라이언트 ID 스냅샷입니다.
        for (int index = 0; index < networkManager.ConnectedClientsIds.Count; index++)
        {
            connectedClientIds[index] = networkManager.ConnectedClientsIds[index];
        }

        for (int index = 0; index < connectedClientIds.Length; index++)
        {
            TrySpawnMissingPlayerObject(networkManager, connectedClientIds[index]);
        }
    }

    /// <summary>
    /// 지정된 클라이언트의 PlayerObject가 누락된 경우 Player Prefab을 생성해 PlayerObject로 스폰합니다.
    /// </summary>
    private void TrySpawnMissingPlayerObject(NetworkManager networkManager, ulong clientId)
    {
        if (networkManager == null || _playerPrefab == null)
        {
            return;
        }

        if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient networkClient))
        {
            return;
        }

        if (networkClient.PlayerObject != null && networkClient.PlayerObject.IsSpawned)
        {
            return;
        }

        GameObject spawnedPlayer = Instantiate(_playerPrefab); // 누락된 PlayerObject를 보충하기 위해 생성하는 플레이어 프리팹 인스턴스입니다.
        if (!spawnedPlayer.TryGetComponent(out NetworkObject networkObject))
        {
            Debug.LogWarning("[NetworkManagerMultiplayerSessionBackend] PlayerPrefab에 NetworkObject가 없어 Host 재스폰을 중단합니다.", this);
            Destroy(spawnedPlayer);
            return;
        }

        networkObject.SpawnAsPlayerObject(clientId, true);
    }

    /// <summary>
    /// 싱글플레이 모드에서 씬 로드 후 플레이어가 누락된 경우 로컬 플레이어 프리팹을 생성합니다.
    /// </summary>
    private void TrySpawnMissingSinglePlayerLocalPlayer()
    {
        if (!_spawnSinglePlayerFallbackIfMissing || _playerPrefab == null)
        {
            return;
        }

        if (!TryResolveNetworkManager(out NetworkManager networkManager, out _) || networkManager.IsListening)
        {
            return;
        }

        if (!TryResolveGameFlowController(out GameFlowController gameFlowController))
        {
            return;
        }

        if (gameFlowController.CurrentPlayMode != E_GamePlayMode.SinglePlayer)
        {
            return;
        }

        PlayerMovement existingPlayer = FindAnyObjectByType<PlayerMovement>(); // 현재 활성 씬에 이미 존재하는 플레이어 이동 컴포넌트 참조입니다.
        if (existingPlayer != null)
        {
            return;
        }

        Instantiate(_playerPrefab);
    }

    /// <summary>
    /// 플레이 모드 판별에 사용할 GameFlowController 참조를 해석합니다.
    /// </summary>
    private bool TryResolveGameFlowController(out GameFlowController gameFlowController)
    {
        if (_cachedGameFlowController == null)
        {
            _cachedGameFlowController = GameFlowController.Instance != null
                ? GameFlowController.Instance
                : FindAnyObjectByType<GameFlowController>();
        }

        gameFlowController = _cachedGameFlowController;
        return gameFlowController != null;
    }

    /// <summary>
    /// NetworkManager 연결/해제 콜백을 등록해 접속한 클라이언트의 PlayerObject 누락을 자동 보정합니다.
    /// </summary>
    private void RegisterNetworkCallbacks(NetworkManager networkManager)
    {
        if (networkManager == null || _networkCallbacksRegistered)
        {
            return;
        }

        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        _networkCallbacksRegistered = true;
    }

    /// <summary>
    /// NetworkManager 콜백 등록을 해제해 중복 콜백/누수 문제를 방지합니다.
    /// </summary>
    private void UnregisterNetworkCallbacks(NetworkManager networkManager)
    {
        if (networkManager == null || !_networkCallbacksRegistered)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        _networkCallbacksRegistered = false;
    }

    /// <summary>
    /// 클라이언트 접속 완료 시 서버 관점에서 PlayerObject 누락 여부를 확인하고 필요 시 즉시 스폰합니다.
    /// </summary>
    private void HandleClientConnected(ulong clientId)
    {
        if (!TryResolveNetworkManager(out NetworkManager networkManager, out _))
        {
            return;
        }

        if (!networkManager.IsServer)
        {
            return;
        }

        TrySpawnMissingPlayerObject(networkManager, clientId);
    }

    /// <summary>
    /// 클라이언트 이탈 시점 후속 동작 확장 포인트입니다.
    /// </summary>
    private void HandleClientDisconnected(ulong _)
    {

    }
}
