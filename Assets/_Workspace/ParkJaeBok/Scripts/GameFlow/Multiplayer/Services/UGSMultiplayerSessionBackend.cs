using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// UGS(Authentication/Lobby/Relay)와 NGO(NetworkManager)를 연동해 Host/Client 세션을 관리하는 백엔드 구현입니다.
/// </summary>
public class UGSMultiplayerSessionBackend : MonoBehaviour, IMultiplayerSessionBackend
{
    private const string RelayJoinCodeKey = "relay_join_code";
    private const string StageInProgressKey = "stage_in_progress";
    private const string ReadyFlagKey = "ready";

    [Header("Dependencies")]
    [Tooltip("Host/Client 시작/종료를 수행할 NetworkManager 참조입니다. 비어 있으면 NetworkManager.Singleton을 사용합니다.")]
    [SerializeField] private NetworkManager _networkManager; // Host/Client 시작과 종료를 위임할 NetworkManager 참조입니다.

    [Tooltip("Relay 연결 정보를 적용할 UnityTransport 참조입니다. 비어 있으면 NetworkManager에서 자동 탐색합니다.")]
    [SerializeField] private UnityTransport _unityTransport; // Relay 서버 데이터 적용에 사용할 UnityTransport 참조입니다.

    [Header("Policy")]
    [Tooltip("Host가 Lobby를 유지하기 위해 Heartbeat를 보낼 간격(초)입니다.")]
    [SerializeField] private float _heartbeatIntervalSeconds = 15f; // Lobby Heartbeat 호출 간격을 제어하는 정책 값입니다.

    [Tooltip("디버그 로그 출력 여부입니다.")]
    [SerializeField] private bool _verboseLogging; // UGS 백엔드 상세 로그 출력 여부를 제어하는 플래그입니다.

    [Tooltip("UGS 초기화 시 사용할 Authentication 프로필 이름입니다. 비어 있지 않으면 멀티 인스턴스 테스트에서 프로필 분리가 가능합니다.")]
    [SerializeField] private string _authenticationProfile = "default"; // Unity Authentication 초기화에 사용할 프로필 이름입니다.

    [Tooltip("UGS 초기화 시 사용할 Environment 이름입니다. 비어 있으면 Unity Dashboard 기본 환경을 사용합니다.")]
    [SerializeField] private string _environmentName; // UnityServices.InitializeAsync 호출 시 적용할 환경 이름입니다.

    [Tooltip("UGS 준비 시 자동 익명 로그인을 수행할지 여부입니다.")]
    [SerializeField] private bool _autoSignInAnonymously = true; // 인증 상태가 없는 경우 SignInAnonymouslyAsync를 자동 호출할지 제어하는 플래그입니다.

    [Header("Player Spawn Fallback")]
    [Tooltip("세션 시작 후 플레이어 누락 보정에 사용할 Player Prefab입니다.")]
    [SerializeField] private GameObject _playerPrefab; // PlayerObject 누락 시 보정 스폰에 사용할 플레이어 프리팹 참조입니다.

    [Tooltip("Host/Server가 씬 로드 후 PlayerObject 누락 시 자동 재스폰을 시도할지 여부입니다.")]
    [SerializeField] private bool _ensureHostPlayerObjectAfterSceneLoad = true; // Host 씬 전환 이후 PlayerObject 누락 보정 활성화 여부입니다.

    [Tooltip("플레이어 스폰/재스폰 폴백을 허용할 GameFlow 상태 목록입니다. Title/Boot에서는 기본적으로 허용되지 않습니다.")]
    [SerializeField] private GameFlowState[] _playerSpawnAllowedStates =
    {
        GameFlowState.StageLoading,
        GameFlowState.StagePlaying,
        GameFlowState.ReturnToTown
    }; // 플레이어 스폰 폴백이 동작 가능한 GameFlow 상태 화이트리스트입니다.

    private string _activeLobbyId; // 현재 로컬 피어가 연결된 Lobby ID 캐시입니다.
    private string _activeJoinCode; // 현재 세션에서 사용하는 Lobby Join Code 캐시입니다.
    private string _localPlayerId; // UGS Authentication에서 발급된 로컬 플레이어 ID 캐시입니다.
    private int _cachedMaxPlayerCount = 2; // Stage 준비 판정에 사용할 세션 최대 인원 수 캐시입니다.
    private bool _isStageInProgress; // 중도 Join 제한 정책에 사용할 Stage 진행 상태 캐시입니다.
    private bool _isUgsInitialized; // 현재 런타임에서 UGS Core/Auth 준비가 완료되었는지 추적하는 플래그입니다.
    private Task _ugsInitializationTask; // UGS Core/Auth 비동기 초기화 진행 상태를 추적하는 Task 캐시입니다.
    private string _ugsInitializationFailureReason; // UGS 초기화 Task 실패 시 마지막 실패 사유를 저장하는 캐시입니다.

    private CancellationTokenSource _heartbeatCancellationTokenSource; // Host Heartbeat 루프 취소를 제어하는 CancellationTokenSource입니다.
    private Task _heartbeatLoopTask; // Host Heartbeat 비동기 루프 Task를 추적하는 캐시입니다.
    private bool _networkCallbacksRegistered; // NetworkManager 콜백 등록 상태를 추적하는 런타임 플래그입니다.
    private GameFlowController _cachedGameFlowController; // 플레이 모드 조회에 사용할 GameFlowController 캐시 참조입니다.

    /// <summary>
    /// 씬 로드 이후 플레이어 누락 보정 처리를 위해 sceneLoaded 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>
    /// Host 세션을 생성하고 Lobby Join Code를 발급합니다.
    /// </summary>
    public async Task<SessionCreateResult> CreateSessionAsync(string hostClientId, int maxPlayerCount)
    {
        SessionOperationResult ensureResult = await EnsureUgsReadyAsync();
        if (!ensureResult.IsSuccess)
        {
            return new SessionCreateResult(false, string.Empty, ensureResult.Reason);
        }

        if (!TryResolveNetworkManager(out NetworkManager networkManager, out string resolveReason))
        {
            return new SessionCreateResult(false, string.Empty, resolveReason);
        }

        if (!TryResolveTransport(networkManager, out UnityTransport transport, out resolveReason))
        {
            return new SessionCreateResult(false, string.Empty, resolveReason);
        }

        try
        {
            RegisterNetworkCallbacks(networkManager);

            int allocationClientCount = Mathf.Max(1, maxPlayerCount) - 1; // Host를 제외한 Relay Allocation 참가 슬롯 수입니다.
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(allocationClientCount);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            transport.SetHostRelayData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData);

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode) },
                    { StageInProgressKey, new DataObject(DataObject.VisibilityOptions.Public, "0") }
                }
            };

            _cachedMaxPlayerCount = Mathf.Max(1, maxPlayerCount);
            _isStageInProgress = false;
            string lobbyName = $"BoS_{hostClientId}_{DateTime.UtcNow:HHmmss}"; // Lobby 생성 시 디버그 식별을 돕기 위한 표시 이름입니다.
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, _cachedMaxPlayerCount, options);

            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            if (!networkManager.StartHost())
            {
                return new SessionCreateResult(false, string.Empty, "StartHostFailed");
            }

            _activeLobbyId = lobby.Id;
            _activeJoinCode = lobby.LobbyCode;
            _localPlayerId = AuthenticationService.Instance.PlayerId;
            StartHeartbeatLoop();

            return new SessionCreateResult(true, _activeJoinCode, string.Empty);
        }
        catch (Exception exception)
        {
            string reason = exception.GetType().Name;
            if (_verboseLogging)
            {
                Debug.LogError($"[UGSMultiplayerSessionBackend] TryCreateSession failed. reason={exception}", this);
            }

            return new SessionCreateResult(false, string.Empty, reason);
        }
    }

    /// <summary>
    /// Join Code(Lobby Code) 기반 참가를 시도합니다.
    /// </summary>
    public async Task<SessionOperationResult> JoinSessionAsync(string joinCode, string clientId)
    {
        if (_isStageInProgress)
        {
            return new SessionOperationResult(false, "StageInProgress");
        }

        SessionOperationResult ensureResult = await EnsureUgsReadyAsync();
        if (!ensureResult.IsSuccess)
        {
            return ensureResult;
        }

        if (!TryResolveNetworkManager(out NetworkManager networkManager, out string resolveReason))
        {
            return new SessionOperationResult(false, resolveReason);
        }

        if (!TryResolveTransport(networkManager, out UnityTransport transport, out resolveReason))
        {
            return new SessionOperationResult(false, resolveReason);
        }

        try
        {
            RegisterNetworkCallbacks(networkManager);

            Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(joinCode.Trim());
            if (!lobby.Data.TryGetValue(RelayJoinCodeKey, out DataObject relayCodeData))
            {
                return new SessionOperationResult(false, "RelayJoinCodeMissing");
            }

            string relayJoinCode = relayCodeData.Value;
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            transport.SetClientRelayData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData, allocation.HostConnectionData);

            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            if (!networkManager.StartClient())
            {
                return new SessionOperationResult(false, "StartClientFailed");
            }

            _activeLobbyId = lobby.Id;
            _activeJoinCode = lobby.LobbyCode;
            _localPlayerId = AuthenticationService.Instance.PlayerId;
            return new SessionOperationResult(true, string.Empty);
        }
        catch (Exception exception)
        {
            string reason = exception.GetType().Name;
            if (_verboseLogging)
            {
                Debug.LogError($"[UGSMultiplayerSessionBackend] TryJoinSession failed. reason={exception}", this);
            }

            return new SessionOperationResult(false, reason);
        }
    }

    /// <summary>
    /// 세션의 Stage 진행 상태를 Lobby 데이터에 반영합니다.
    /// </summary>
    public async Task<SessionOperationResult> SetStageInProgressAsync(string joinCode, bool isInProgress)
    {
        _isStageInProgress = isInProgress;

        if (string.IsNullOrWhiteSpace(_activeLobbyId))
        {
            Debug.LogWarning("[UGSMultiplayerSessionBackend] SetStageInProgressAsync fallback: active lobby id is empty.", this);
            return new SessionOperationResult(false, "LobbyNotJoined");
        }

        try
        {
            UpdateLobbyOptions options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { StageInProgressKey, new DataObject(DataObject.VisibilityOptions.Public, isInProgress ? "1" : "0") }
                }
            };

            await LobbyService.Instance.UpdateLobbyAsync(_activeLobbyId, options);
            return new SessionOperationResult(true, string.Empty);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[UGSMultiplayerSessionBackend] SetStageInProgressAsync failed. error={exception.Message}", this);
            return new SessionOperationResult(false, exception.GetType().Name);
        }
    }

    /// <summary>
    /// 세션의 현재 참가 인원 수를 조회합니다.
    /// </summary>
    public async Task<PlayerCountResult> GetPlayerCountAsync(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(_activeLobbyId))
        {
            Debug.LogWarning("[UGSMultiplayerSessionBackend] GetPlayerCountAsync fallback: active lobby id is empty.", this);
            return new PlayerCountResult(false, 0, "LobbyNotJoined");
        }

        try
        {
            Lobby lobby = await LobbyService.Instance.GetLobbyAsync(_activeLobbyId);
            int playerCount = lobby.Players != null ? lobby.Players.Count : 0;
            return new PlayerCountResult(true, playerCount, string.Empty);
        }
        catch (Exception exception)
        {
            return new PlayerCountResult(false, 0, exception.GetType().Name);
        }
    }

    /// <summary>
    /// 로컬 플레이어의 준비 완료 상태를 Lobby PlayerData에 기록합니다.
    /// </summary>
    public async Task<SessionOperationResult> MarkClientReadyAsync(string joinCode, string clientId)
    {
        if (string.IsNullOrWhiteSpace(_activeLobbyId) || string.IsNullOrWhiteSpace(_localPlayerId))
        {
            Debug.LogWarning("[UGSMultiplayerSessionBackend] MarkClientReadyAsync fallback: lobby/player id is missing.", this);
            return new SessionOperationResult(false, "LobbyNotJoined");
        }

        try
        {
            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { ReadyFlagKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "1") }
                }
            };

            await LobbyService.Instance.UpdatePlayerAsync(_activeLobbyId, _localPlayerId, options);
            return new SessionOperationResult(true, string.Empty);
        }
        catch (Exception exception)
        {
            return new SessionOperationResult(false, exception.GetType().Name);
        }
    }

    /// <summary>
    /// Host가 Stage 진입을 시작해도 되는 세션 준비 완료 상태인지 조회합니다.
    /// </summary>
    public async Task<StageEntryReadyResult> GetStageEntryReadyAsync(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(_activeLobbyId))
        {
            Debug.LogWarning("[UGSMultiplayerSessionBackend] GetStageEntryReadyAsync fallback: active lobby id is empty.", this);
            return new StageEntryReadyResult(false, false, "LobbyNotJoined");
        }

        try
        {
            Lobby lobby = await LobbyService.Instance.GetLobbyAsync(_activeLobbyId);
            int playerCount = lobby.Players != null ? lobby.Players.Count : 0;
            if (playerCount < Mathf.Max(1, _cachedMaxPlayerCount))
            {
                return new StageEntryReadyResult(true, false, "PlayersNotFull");
            }

            for (int index = 0; index < lobby.Players.Count; index++)
            {
                Player player = lobby.Players[index];
                bool isReadyPlayer = player.Data != null
                    && player.Data.TryGetValue(ReadyFlagKey, out PlayerDataObject readyFlag)
                    && readyFlag.Value == "1";
                if (!isReadyPlayer)
                {
                    return new StageEntryReadyResult(true, false, "PlayersNotReady");
                }
            }

            return new StageEntryReadyResult(true, true, string.Empty);
        }
        catch (Exception exception)
        {
            return new StageEntryReadyResult(false, false, exception.GetType().Name);
        }
    }

    /// <summary>
    /// 세션을 종료하고 Network/Lobby 리소스를 정리합니다.
    /// </summary>
    public async Task<SessionOperationResult> CloseSessionAsync(string joinCode)
    {
        StopHeartbeatLoop();

        if (TryResolveNetworkManager(out NetworkManager networkManager, out _))
        {
            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        if (!string.IsNullOrWhiteSpace(_activeLobbyId))
        {
            try
            {
                if (TryResolveNetworkManager(out NetworkManager resolvedManager, out _) && resolvedManager.IsServer)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(_activeLobbyId);
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(_activeLobbyId, _localPlayerId);
                }
            }
            catch (Exception exception)
            {
                if (_verboseLogging)
                {
                    Debug.LogWarning($"[UGSMultiplayerSessionBackend] CloseSession cleanup warning. error={exception.Message}", this);
                }
            }
        }

        _activeLobbyId = string.Empty;
        _activeJoinCode = string.Empty;
        _isStageInProgress = false;
        _localPlayerId = string.Empty;
        if (TryResolveNetworkManager(out NetworkManager resolvedManagerForCallbacks, out _))
        {
            UnregisterNetworkCallbacks(resolvedManagerForCallbacks);
        }

        return new SessionOperationResult(true, string.Empty);
    }

    /// <summary>
    /// UGS Core 초기화 및 익명 인증 로그인을 보장합니다.
    /// </summary>
    private async Task<SessionOperationResult> EnsureUgsReadyAsync()
    {
        if (_isUgsInitialized)
        {
            return new SessionOperationResult(true, string.Empty);
        }

        if (_ugsInitializationTask == null)
        {
            _ugsInitializationFailureReason = string.Empty;
            _ugsInitializationTask = InitializeUgsAsync(); // 중복 초기화 요청이 와도 동일 Task를 재사용합니다.
        }

        try
        {
            await _ugsInitializationTask; // 호출자는 비동기 대기하며, 메인 스레드를 블로킹하지 않습니다.
        }
        catch (Exception exception)
        {
            _ugsInitializationFailureReason = exception.GetType().Name;
        }

        if (_isUgsInitialized)
        {
            return new SessionOperationResult(true, string.Empty);
        }

        string reason = string.IsNullOrWhiteSpace(_ugsInitializationFailureReason)
            ? "UgsInitializationFailed"
            : _ugsInitializationFailureReason;
        _ugsInitializationTask = null; // 실패 시 다음 호출에서 초기화를 재시도할 수 있도록 리셋합니다.
        return new SessionOperationResult(false, reason);
    }

    /// <summary>
    /// UGS Core/Auth 비동기 초기화를 수행하고 성공 시 런타임 캐시 상태를 갱신합니다.
    /// </summary>
    private async Task InitializeUgsAsync()
    {
        try
        {
            InitializationOptions options = new InitializationOptions(); // UGS Core 초기화 시 프로필/환경 옵션을 전달하기 위한 옵션 객체입니다.
            if (!string.IsNullOrWhiteSpace(_authenticationProfile))
            {
                options.SetProfile(_authenticationProfile.Trim());
            }

            if (!string.IsNullOrWhiteSpace(_environmentName))
            {
                options.SetEnvironmentName(_environmentName.Trim());
            }

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn && _autoSignInAnonymously)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                _ugsInitializationFailureReason = "AuthenticationNotSignedIn";
                return;
            }

            _localPlayerId = AuthenticationService.Instance.PlayerId;
            _isUgsInitialized = true;
        }
        catch (Exception exception)
        {
            _ugsInitializationFailureReason = exception.GetType().Name;
            if (_verboseLogging)
            {
                Debug.LogError($"[UGSMultiplayerSessionBackend] UGS initialization failed. reason={exception}", this);
            }
        }
    }

    /// <summary>
    /// Host Lobby Heartbeat를 주기적으로 전송하는 비동기 루프를 시작합니다.
    /// </summary>
    private void StartHeartbeatLoop()
    {
        StopHeartbeatLoop();
        _heartbeatCancellationTokenSource = new CancellationTokenSource();
        _heartbeatLoopTask = RunHeartbeatLoopAsync(_heartbeatCancellationTokenSource.Token);
    }

    /// <summary>
    /// 실행 중인 Host Lobby Heartbeat 루프를 취소하고 정리합니다.
    /// </summary>
    private void StopHeartbeatLoop()
    {
        if (_heartbeatCancellationTokenSource == null)
        {
            return;
        }

        _heartbeatCancellationTokenSource.Cancel();
        _heartbeatCancellationTokenSource.Dispose();
        _heartbeatCancellationTokenSource = null;
        _heartbeatLoopTask = null;
    }

    /// <summary>
    /// Heartbeat 간격 정책에 맞춰 Lobby heartbeat를 비동기로 전송합니다.
    /// </summary>
    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_activeLobbyId))
                {
                    Debug.LogWarning("[UGSMultiplayerSessionBackend] Heartbeat loop fallback: active lobby id is empty.", this);
                    return;
                }

                if (!TryResolveNetworkManager(out NetworkManager networkManager, out _))
                {
                    Debug.LogWarning("[UGSMultiplayerSessionBackend] Heartbeat loop fallback: NetworkManager is missing.", this);
                    return;
                }

                if (!networkManager.IsServer)
                {
                    Debug.LogWarning("[UGSMultiplayerSessionBackend] Heartbeat loop fallback: local peer is not host.", this);
                    return;
                }

                await LobbyService.Instance.SendHeartbeatPingAsync(_activeLobbyId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[UGSMultiplayerSessionBackend] Heartbeat failed. error={exception.Message}", this);
            }

            float delaySeconds = Mathf.Max(5f, _heartbeatIntervalSeconds);
            int delayMilliseconds = Mathf.CeilToInt(delaySeconds * 1000f);
            await Task.Delay(delayMilliseconds, cancellationToken);
        }
    }

    /// <summary>
    /// 오브젝트 비활성화 시 heartbeat 루프를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        StopHeartbeatLoop();
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (TryResolveNetworkManager(out NetworkManager networkManager, out _))
        {
            UnregisterNetworkCallbacks(networkManager);
        }
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
    /// Relay 연결에 사용할 UnityTransport 참조를 확인합니다.
    /// </summary>
    private bool TryResolveTransport(NetworkManager networkManager, out UnityTransport transport, out string reason)
    {
        transport = _unityTransport != null ? _unityTransport : (networkManager != null ? networkManager.GetComponent<UnityTransport>() : null);
        _unityTransport = transport;
        reason = string.Empty;

        if (transport != null)
        {
            return true;
        }

        reason = "UnityTransportMissing";
        return false;
    }

    /// <summary>
    /// 씬 로드 후 플레이어 누락 폴백을 점검하고 필요 시 보정 스폰을 수행합니다.
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

        if (!TryResolveNetworkManager(out NetworkManager networkManager, out _) || !networkManager.IsListening || !networkManager.IsServer)
        {
            return;
        }

        ulong[] connectedClientIds = new ulong[networkManager.ConnectedClientsIds.Count]; // 씬 로드 시점 연결된 클라이언트 ID 스냅샷입니다.
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
    /// 지정 클라이언트의 PlayerObject가 누락된 경우 Player Prefab을 재스폰합니다.
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

        GameObject spawnedPlayer = Instantiate(_playerPrefab); // 누락된 PlayerObject를 보충하기 위한 플레이어 프리팹 인스턴스입니다.
        if (!spawnedPlayer.TryGetComponent(out NetworkObject networkObject))
        {
            Debug.LogWarning("[UGSMultiplayerSessionBackend] PlayerPrefab에 NetworkObject가 없어 재스폰을 중단합니다.", this);
            Destroy(spawnedPlayer);
            return;
        }

        networkObject.SpawnAsPlayerObject(clientId, true);
    }

    /// <summary>
    /// 싱글플레이 Town 진입 시 플레이어가 없으면 로컬 Player Prefab을 생성합니다.
    /// </summary>
    private void TrySpawnSinglePlayerLocalPlayer()
    {
        if (_playerPrefab == null)
        {
            return;
        }

        if (!TryResolveNetworkManager(out NetworkManager networkManager, out _) || networkManager.IsListening)
        {
            return;
        }

        if (!TryResolveGameFlowController(out GameFlowController gameFlowController))
        {
            Debug.LogWarning("[UGSMultiplayerSessionBackend] SinglePlayer fallback skipped: GameFlowController missing.", this);
            return;
        }

        if (gameFlowController.CurrentPlayMode != E_GamePlayMode.SinglePlayer)
        {
            return;
        }

        Instantiate(_playerPrefab);
    }

    /// <summary>
    /// 플레이 모드 판별에 사용할 GameFlowController를 캐시/해석합니다.
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
            Debug.LogWarning("[UGSMultiplayerSessionBackend] Player spawn fallback blocked: GameFlowController is missing.", this);
            return false;
        }

        if (!gameFlowController.IsPlayerSpawnAllowedScene(loadedScene.name))
        {
            if (_verboseLogging)
            {
                Debug.LogWarning($"[UGSMultiplayerSessionBackend] Player spawn fallback blocked by scene policy. scene={loadedScene.name}", this);
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
            Debug.LogWarning($"[UGSMultiplayerSessionBackend] Player spawn fallback blocked by GameFlowState={currentState}", this);
        }

        return false;
    }

    /// <summary>
    /// NetworkManager 연결/해제 콜백을 등록해 접속 직후 PlayerObject 누락을 자동 보정합니다.
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
    /// NetworkManager 연결/해제 콜백 등록을 해제해 중복 콜백과 누수를 방지합니다.
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
    /// 클라이언트 접속 시 서버 관점에서 PlayerObject 누락 여부를 확인하고 필요 시 스폰합니다.
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
    /// 클라이언트 연결 해제 시점 확장 포인트입니다.
    /// </summary>
    private void HandleClientDisconnected(ulong _)
    {
    }
}
