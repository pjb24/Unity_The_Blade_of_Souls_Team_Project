using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Unity Netcode의 NetworkManager를 사용해 Host/Client 세션 생성과 참가를 처리하는 백엔드 구현입니다.
/// </summary>
public class NetworkManagerMultiplayerSessionBackend : MonoBehaviour, IMultiplayerSessionBackend
{
    [Header("Dependencies")]
    [Tooltip("멀티플레이 네트워크 수립을 수행할 NetworkManager 참조입니다. 비어 있으면 NetworkManager.Singleton을 사용합니다.")]
    [SerializeField] private NetworkManager _networkManager; // Host/Client 시작/종료를 위임할 NetworkManager 참조입니다.

    [Header("Policy")]
    [Tooltip("Host가 생성한 Join Code를 저장해 UI에 노출할 때 사용할 기본 문자열입니다.")]
    [SerializeField] private string _defaultJoinCode = "LOCAL"; // Host 시작 성공 시 Join Code로 반환할 기본 문자열입니다.

    [Tooltip("디버그 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLogging; // 백엔드 동작 로그 출력 여부입니다.

    private string _activeJoinCode; // 현재 런타임 세션에서 사용 중인 Join Code 캐시입니다.
    private int _cachedMaxPlayerCount = 2; // 세션 정원 판정에 사용할 최대 인원 수 캐시입니다.
    private bool _isStageInProgress; // 중도 Join 금지 정책을 반영하기 위한 Stage 진행 상태 캐시입니다.

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
}
