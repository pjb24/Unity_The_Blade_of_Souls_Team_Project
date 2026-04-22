using System.Collections.Generic;
using System.Threading.Tasks;
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

    [Tooltip("Host/Client 자동 스폰에 사용할 공통 Player Prefab입니다.")]
    [SerializeField] private GameObject _playerPrefab; // NetworkManager.NetworkConfig.PlayerPrefab으로 연결할 단일 플레이어 프리팹 참조입니다.

    [Tooltip("씬 배치형 플레이어 스폰 좌표를 해석/적용할 코디네이터입니다. 비어 있으면 활성 씬에서 자동 탐색합니다.")]
    [SerializeField] private PlayerSpawnCoordinator _playerSpawnCoordinator; // 슬롯 기반 스폰 위치를 조회하고 적용할 코디네이터 참조입니다.

    [Tooltip("씬 전환 후 Host/Server 관점에서 로컬 PlayerObject가 사라졌을 때 자동 재스폰을 시도할지 여부입니다.")]
    [SerializeField] private bool _ensureHostPlayerObjectAfterSceneLoad = true; // 씬 전환으로 PlayerObject가 파괴된 경우 Host 로컬 플레이어 재스폰을 시도할지 제어하는 플래그입니다.

    [Tooltip("클라이언트 접속 직후 PlayerObject 준비 지연을 보정하기 위한 서버측 정렬 재시도 횟수입니다.")]
    [SerializeField] private int _spawnAlignRetryCount = 20; // 접속 직후 PlayerObject 준비 타이밍 레이스를 보정할 스폰 정렬 재시도 횟수입니다.

    [Tooltip("클라이언트 접속 직후 PlayerObject 정렬 재시도 간격(초)입니다.")]
    [SerializeField] private float _spawnAlignRetryInterval = 0.1f; // 접속 직후 PlayerObject 정렬 재시도 간 대기 시간입니다.

    [Tooltip("플레이어 스폰/재스폰 폴백을 허용할 GameFlow 상태 목록입니다. Title/Boot에서는 기본적으로 허용되지 않습니다.")]
    [SerializeField]
    private GameFlowState[] _playerSpawnAllowedStates =
     {
         GameFlowState.StageLoading,
         GameFlowState.StagePlaying,
         GameFlowState.ReturnToTown
     }; // 플레이어 스폰 폴백이 동작 가능한 GameFlow 상태 화이트리스트입니다.

    [Header("Policy")]
    [Tooltip("Host가 생성한 Join Code를 저장해 UI에 노출할 때 사용할 기본 문자열입니다.")]
    [SerializeField] private string _defaultJoinCode = "LOCAL"; // Host 시작 성공 시 Join Code로 반환할 기본 문자열입니다.

    [Tooltip("디버그 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLogging; // 백엔드 동작 로그 출력 여부입니다.

    private string _activeJoinCode; // 현재 런타임 세션에서 사용 중인 Join Code 캐시입니다.
    private int _cachedMaxPlayerCount = 2; // 세션 정원 판정에 사용할 최대 인원 수 캐시입니다.
    private bool _isStageInProgress; // 중도 Join 금지 정책을 반영하기 위한 Stage 진행 상태 캐시입니다.
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
    public Task<SessionCreateResult> CreateSessionAsync(string hostClientId, int maxPlayerCount)
    {
        string joinCode = string.Empty;

        if (!TryResolveNetworkManager(out NetworkManager networkManager, out string reason))
        {
            return Task.FromResult(new SessionCreateResult(false, string.Empty, reason));
        }

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        RegisterNetworkCallbacks(networkManager);

        bool started = networkManager.StartHost();
        if (!started)
        {
            return Task.FromResult(new SessionCreateResult(false, string.Empty, "StartHostFailed"));
        }

        _cachedMaxPlayerCount = Mathf.Max(1, maxPlayerCount);
        _isStageInProgress = false;
        _activeJoinCode = string.IsNullOrWhiteSpace(_defaultJoinCode) ? "LOCAL" : _defaultJoinCode.Trim().ToUpperInvariant();
        joinCode = _activeJoinCode;

        if (_verboseLogging)
        {
            Debug.Log($"[NetworkManagerMultiplayerSessionBackend] Host started. hostClientId={hostClientId}, joinCode={joinCode}", this);
        }

        return Task.FromResult(new SessionCreateResult(true, joinCode, string.Empty));
    }

    /// <summary>
    /// Join Code 기반 참가를 시도합니다.
    /// </summary>
    public Task<SessionOperationResult> JoinSessionAsync(string joinCode, string clientId)
    {
        if (_isStageInProgress)
        {
            return Task.FromResult(new SessionOperationResult(false, "StageInProgress"));
        }

        string reason = string.Empty;
        if (!TryResolveNetworkManager(out NetworkManager networkManager, out reason))
        {
            return Task.FromResult(new SessionOperationResult(false, reason));
        }

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        RegisterNetworkCallbacks(networkManager);

        bool started = networkManager.StartClient();
        if (!started)
        {
            return Task.FromResult(new SessionOperationResult(false, "StartClientFailed"));
        }

        _activeJoinCode = string.IsNullOrWhiteSpace(joinCode)
            ? (string.IsNullOrWhiteSpace(_defaultJoinCode) ? "LOCAL" : _defaultJoinCode.Trim().ToUpperInvariant())
            : joinCode.Trim().ToUpperInvariant();

        if (_verboseLogging)
        {
            Debug.Log($"[NetworkManagerMultiplayerSessionBackend] Client started. clientId={clientId}, joinCode={_activeJoinCode}", this);
        }

        return Task.FromResult(new SessionOperationResult(true, string.Empty));
    }

    /// <summary>
    /// 세션의 Stage 진행 상태를 갱신합니다.
    /// </summary>
    public Task<SessionOperationResult> SetStageInProgressAsync(string joinCode, bool isInProgress)
    {
        _isStageInProgress = isInProgress;
        return Task.FromResult(new SessionOperationResult(true, string.Empty));
    }

    /// <summary>
    /// 세션의 현재 참가 인원 수를 조회합니다.
    /// </summary>
    public Task<PlayerCountResult> GetPlayerCountAsync(string joinCode)
    {
        if (!TryResolveNetworkManager(out NetworkManager networkManager, out _))
        {
            return Task.FromResult(new PlayerCountResult(false, 0, "NetworkManagerMissing"));
        }

        if (!networkManager.IsListening)
        {
            return Task.FromResult(new PlayerCountResult(false, 0, "NetworkManagerNotListening"));
        }

        int playerCount = networkManager.ConnectedClientsList != null ? networkManager.ConnectedClientsList.Count : 0;
        return Task.FromResult(new PlayerCountResult(true, playerCount, string.Empty));
    }

    /// <summary>
    /// Client가 로드 완료/준비 완료 상태임을 세션에 기록합니다.
    /// </summary>
    public Task<SessionOperationResult> MarkClientReadyAsync(string joinCode, string clientId)
    {
        return Task.FromResult(new SessionOperationResult(true, string.Empty));
    }

    /// <summary>
    /// Host가 Stage 진입을 시작해도 되는 세션 준비 완료 상태인지 조회합니다.
    /// </summary>
    public async Task<StageEntryReadyResult> GetStageEntryReadyAsync(string joinCode)
    {
        PlayerCountResult playerCountResult = await GetPlayerCountAsync(joinCode);
        if (!playerCountResult.IsSuccess)
        {
            return new StageEntryReadyResult(false, false, "PlayerCountUnavailable");
        }

        bool isReady = playerCountResult.PlayerCount >= Mathf.Max(1, _cachedMaxPlayerCount);
        if (!isReady)
        {
            return new StageEntryReadyResult(true, false, "PlayersNotFull");
        }

        return new StageEntryReadyResult(true, true, string.Empty);
    }

    /// <summary>
    /// 세션을 종료합니다.
    /// </summary>
    public Task<SessionOperationResult> CloseSessionAsync(string joinCode)
    {
        if (!TryResolveNetworkManager(out NetworkManager networkManager, out _))
        {
            return Task.FromResult(new SessionOperationResult(false, "NetworkManagerMissing"));
        }

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        UnregisterNetworkCallbacks(networkManager);
        _activeJoinCode = string.Empty;
        _isStageInProgress = false;
        return Task.FromResult(new SessionOperationResult(true, string.Empty));
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
    /// 씬 로드 이후 Host 로컬 PlayerObject가 누락되었는지 점검하고 필요 시 재스폰을 시도합니다.
    /// </summary>
    private void HandleSceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
    {
        if (!IsPlayerSpawnAllowedByGameFlowState(loadedScene))
        {
            return;
        }

        TrySpawnSinglePlayerLocalPlayer();

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
            TryApplySpawnToExistingPlayerObject(networkManager, connectedClientIds[index]);
        }
    }

    /// <summary>
    /// 지정된 클라이언트의 PlayerObject가 누락된 경우 Player Prefab을 생성해 PlayerObject로 스폰합니다.
    /// </summary>
    private void TrySpawnMissingPlayerObject(NetworkManager networkManager, ulong clientId)
    {
        if (networkManager == null || _playerPrefab == null)
        {
            if (_playerPrefab == null)
            {
                Debug.LogWarning("[NetworkManagerMultiplayerSessionBackend] PlayerObject 보정 스폰을 건너뜁니다. Player Prefab이 비어 있습니다.", this);
            }
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

        if (clientId == NetworkManager.ServerClientId && TryResolveSpawnCoordinator(out PlayerSpawnCoordinator spawnCoordinator))
        {
            spawnCoordinator.TrySpawnPlayerObject(networkManager, _playerPrefab, clientId);
            return;
        }

        GameObject spawnedPlayer = Instantiate(_playerPrefab); // 스폰 코디네이터가 없을 때 기본 좌표에서 생성하는 플레이어 프리팹 인스턴스입니다.
        if (!spawnedPlayer.TryGetComponent(out NetworkObject networkObject))
        {
            Debug.LogWarning("[NetworkManagerMultiplayerSessionBackend] PlayerPrefab에 NetworkObject가 없어 Host 재스폰을 중단합니다.", this);
            Destroy(spawnedPlayer);
            return;
        }

        Debug.LogWarning($"[NetworkManagerMultiplayerSessionBackend] 기본 좌표로 재스폰합니다. clientId={clientId}", this);
        networkObject.SpawnAsPlayerObject(clientId, true);
    }

    /// <summary>
    /// 싱글플레이 모드에서 씬 로드 후 플레이어가 누락된 경우 로컬 플레이어 프리팹을 생성합니다.
    /// </summary>
    private void TrySpawnSinglePlayerLocalPlayer()
    {
        if (_playerPrefab == null)
        {
            Debug.LogWarning("[NetworkManagerMultiplayerSessionBackend] 싱글플레이 폴백 스폰을 건너뜁니다. Player Prefab이 비어 있습니다.", this);

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

        if (TryResolveSpawnCoordinator(out PlayerSpawnCoordinator spawnCoordinator))
        {
            if (!spawnCoordinator.TrySpawnSinglePlayer(_playerPrefab))
            {
                Debug.LogWarning("[NetworkManagerMultiplayerSessionBackend] 싱글플레이 스폰 코디네이터 적용에 실패했습니다.", this);
            }

            return;
        }

        Debug.LogWarning("[NetworkManagerMultiplayerSessionBackend] PlayerSpawnCoordinator를 찾지 못해 싱글플레이 플레이어를 기본 좌표로 생성합니다.", this);
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
    /// 현재 GameFlow 상태가 플레이어 스폰 폴백 허용 대상인지 판별합니다.
    /// </summary>
    private bool IsPlayerSpawnAllowedByGameFlowState(Scene loadedScene)
    {
        if (!TryResolveGameFlowController(out GameFlowController gameFlowController))
        {
            Debug.LogWarning("[NetworkManagerMultiplayerSessionBackend] Player spawn fallback blocked: GameFlowController is missing.", this);
            return false;
        }

        if (!gameFlowController.IsPlayerSpawnAllowedScene(loadedScene.name))
        {
            if (_verboseLogging)
            {
                Debug.LogWarning($"[NetworkManagerMultiplayerSessionBackend] Player spawn fallback blocked by scene policy. scene={loadedScene.name}", this);
            }

            return false;
        }

        GameFlowState currentState = gameFlowController.CurrentState; // 플레이어 스폰 폴백 허용 여부를 판정할 현재 GameFlow 상태입니다.
        if (_playerSpawnAllowedStates == null || _playerSpawnAllowedStates.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < _playerSpawnAllowedStates.Length; index++)
        {
            if (_playerSpawnAllowedStates[index] == currentState)
            {
                return true;
            }
        }

        if (_verboseLogging)
        {
            Debug.LogWarning($"[NetworkManagerMultiplayerSessionBackend] Player spawn fallback blocked by GameFlowState={currentState}", this);
        }

        return false;
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
        TryApplySpawnToExistingPlayerObject(networkManager, clientId);
        StartCoroutine(AlignClientSpawnWhenReadyRoutine(clientId));
    }

    /// <summary>
    /// 클라이언트 이탈 시점 후속 동작 확장 포인트입니다.
    /// </summary>
    private void HandleClientDisconnected(ulong _)
    {

    }

    /// <summary>
    /// 활성 씬의 플레이어 스폰 코디네이터를 해석합니다.
    /// </summary>
    private bool TryResolveSpawnCoordinator(out PlayerSpawnCoordinator spawnCoordinator)
    {
        if (_playerSpawnCoordinator != null && _playerSpawnCoordinator.gameObject.scene == SceneManager.GetActiveScene())
        {
            spawnCoordinator = _playerSpawnCoordinator;
            return true;
        }

        if (PlayerSpawnCoordinator.TryFindForActiveScene(out spawnCoordinator))
        {
            _playerSpawnCoordinator = spawnCoordinator;
            return true;
        }

        spawnCoordinator = null;
        return false;
    }

    /// <summary>
    /// 이미 존재하는 PlayerObject를 슬롯 규칙 위치로 서버 권한에서 정렬합니다.
    /// </summary>
    private void TryApplySpawnToExistingPlayerObject(NetworkManager networkManager, ulong clientId)
    {
        if (networkManager != null && clientId != NetworkManager.ServerClientId)
        {
            return;
        }

        if (!TryResolveSpawnCoordinator(out PlayerSpawnCoordinator spawnCoordinator))
        {
            Debug.LogWarning($"[NetworkManagerMultiplayerSessionBackend] PlayerSpawnCoordinator 누락으로 위치 정렬을 건너뜁니다. clientId={clientId}", this);
            return;
        }

        if (!spawnCoordinator.TryApplySpawnToExistingPlayerObject(networkManager, clientId))
        {
            Debug.LogWarning($"[NetworkManagerMultiplayerSessionBackend] 서버 권한 위치 정렬 실패. clientId={clientId}", this);
        }
    }

    /// <summary>
    /// 접속 직후 PlayerObject 준비가 지연되는 경우를 대비해 서버 권한 정렬을 재시도합니다.
    /// </summary>
    private System.Collections.IEnumerator AlignClientSpawnWhenReadyRoutine(ulong clientId)
    {
        int safeRetryCount = Mathf.Max(1, _spawnAlignRetryCount); // 정렬 재시도 횟수의 하한을 보정한 안전 값입니다.
        float safeRetryInterval = Mathf.Max(0.01f, _spawnAlignRetryInterval); // 정렬 재시도 간격의 하한을 보정한 안전 값입니다.

        for (int retryIndex = 0; retryIndex < safeRetryCount; retryIndex++)
        {
            if (!TryResolveNetworkManager(out NetworkManager networkManager, out _) || !networkManager.IsServer)
            {
                yield break;
            }

            if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient connectedClient))
            {
                yield break;
            }

            if (connectedClient.PlayerObject == null || !connectedClient.PlayerObject.IsSpawned)
            {
                TrySpawnMissingPlayerObject(networkManager, clientId);
                yield return new WaitForSecondsRealtime(safeRetryInterval);
                continue;
            }

            TryApplySpawnToExistingPlayerObject(networkManager, clientId);
            yield break;
        }

        Debug.LogWarning($"[NetworkManagerMultiplayerSessionBackend] 재시도 내에 Client 스폰 정렬을 완료하지 못했습니다. clientId={clientId}", this);
    }
}
