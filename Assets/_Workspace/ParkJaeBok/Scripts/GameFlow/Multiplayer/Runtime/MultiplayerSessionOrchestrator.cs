using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// 멀티 세션 생성/참가/재접속/종료 정책을 상위 흐름(GameFlow)과 연결하는 오케스트레이터입니다.
/// </summary>
public class MultiplayerSessionOrchestrator : MonoBehaviour
{
    [Serializable]
    public class StringEvent : UnityEvent<string> { }

    [Serializable]
    public class IntEvent : UnityEvent<int> { }

    [Header("Dependencies")]
    [Tooltip("플레이 모드 전환과 씬 전환 요청을 전달할 GameFlowController 참조입니다. 비어 있으면 런타임에서 자동 탐색합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // 모드 시작/종료를 처리할 게임 흐름 컨트롤러 참조입니다.

    [Tooltip("세션 생성/참가를 처리할 백엔드 어댑터입니다. 비어 있으면 런타임에서 IMultiplayerSessionBackend를 자동 탐색합니다.")]
    [SerializeField] private MonoBehaviour _sessionBackendBehaviour; // Inspector에서 IMultiplayerSessionBackend 구현체를 연결하기 위한 참조입니다.

    [Header("Policy")]
    [Tooltip("멀티 세션 최대 인원 수(Host 포함)입니다. 요구사항 기본값은 2입니다.")]
    [SerializeField] private int _maxPlayerCount = 2; // 세션 정원 제한 정책 값입니다.

    [Tooltip("클라이언트 재접속 허용 시간(초)입니다. 요구사항 범위는 30~60초입니다.")]
    [SerializeField] private float _reconnectWindowSeconds = 45f; // 재접속 윈도우 정책 값입니다.

    [Header("Lifecycle")]
    [Tooltip("씬 전환 후에도 오케스트레이터를 유지할지 여부입니다. Host가 Town에서 Join Code를 확인하려면 true를 권장합니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // 씬 전환 시 세션 런타임 상태를 유지할지 제어하는 플래그입니다.

    [Tooltip("런타임 호출 시 GameFlowController/Backend 참조가 비어 있으면 자동으로 재해석할지 여부입니다.")]
    [SerializeField] private bool _autoResolveDependenciesOnUse = true; // 씬 전환 이후 누락된 의존성 참조를 자동 재해석할지 제어하는 플래그입니다.

    [Header("Runtime (Debug)")]
    [Tooltip("디버그용: 현재 활성 Join Code입니다.")]
    [SerializeField] private string _activeJoinCode; // 현재 세션에서 사용하는 Join Code입니다.

    [Tooltip("디버그용: 현재 멀티 연결 상태입니다.")]
    [SerializeField] private E_MultiplayerConnectionState _connectionState = E_MultiplayerConnectionState.None; // 멀티 연결 상태를 확인하기 위한 디버그 값입니다.

    [Tooltip("디버그용: 마지막 정책 거부 사유입니다.")]
    [SerializeField] private string _lastFailureReason; // 최근 실패 사유를 추적하는 디버그 문자열입니다.

    [Tooltip("UI 버튼 OnClick에서 사용할 Join Code 입력 캐시 값입니다. 제출 직전에 입력 필드 값으로 갱신됩니다.")]
    [SerializeField] private string _uiJoinCode; // UI에서 입력한 Join Code를 임시 보관하는 문자열입니다.

    [Tooltip("Join 제출 직전에 최신 Join Code를 읽어올 TMP_InputField입니다.")]
    [SerializeField] private TMP_InputField _uiJoinCodeInputField; // Join 버튼 클릭 시점에 최신 코드를 읽어올 입력 필드 참조입니다.

    [Tooltip("Join 요청 시 InputField 참조가 비어 있으면 활성 씬에서 자동 탐색을 시도할지 여부입니다.")]
    [SerializeField] private bool _autoResolveJoinCodeInputFieldOnUse = true; // Join 요청 직전 InputField 자동 탐색 활성화 여부를 제어하는 플래그입니다.

    [Tooltip("자동 탐색 시 우선적으로 매칭할 Join Code InputField 오브젝트 이름입니다. 비어 있으면 첫 번째 TMP_InputField를 사용합니다.")]
    [SerializeField] private string _joinCodeInputFieldObjectName = "IF_JoinCode"; // 씬 자동 탐색에서 Join Code 입력 필드를 특정하기 위한 이름 힌트입니다.

    [Tooltip("UI 버튼 OnClick에서 사용할 Client 식별자입니다.")]
    [SerializeField] private string _uiClientId = "Client_A"; // Join 요청 시 사용할 로컬 Client 식별자 문자열입니다.

    [Tooltip("디버그용: 현재 로컬 피어가 세션에 보고한 Client 식별자입니다.")]
    [SerializeField] private string _localClientId; // 로컬 피어의 준비 완료 보고에 사용할 Client 식별자 캐시입니다.

    [Tooltip("디버그용: 로컬 Client 준비 완료 자동 선언 성공 여부입니다.")]
    [SerializeField] private bool _isLocalClientReadyDeclared; // 씬 로드 완료 후 Client 준비 완료를 이미 보고했는지 추적하는 런타임 플래그입니다.

    [Header("Events")]
    [Tooltip("Host 세션 생성 시 발급된 Join Code를 UI에 전달할 이벤트입니다.")]
    [SerializeField] private StringEvent _onJoinCodeGenerated; // Join Code 노출 UI를 갱신하기 위한 이벤트입니다.

    [Tooltip("정책 또는 세션 처리 실패 사유를 UI에 전달할 이벤트입니다.")]
    [SerializeField] private StringEvent _onSessionFailed; // 세션 처리 실패 사유 표시용 이벤트입니다.

    [Tooltip("Host 종료 또는 재접속 실패 등으로 타이틀 복귀가 필요할 때 호출할 이벤트입니다.")]
    [SerializeField] private UnityEvent _onReturnToTitle; // 타이틀 복귀 안내 연출을 트리거하기 위한 이벤트입니다.

    [Tooltip("세션 인원 수가 갱신될 때 UI에 전달할 이벤트입니다.")]
    [SerializeField] private IntEvent _onPlayerCountUpdated; // 세션 인원 수 변경을 표시하기 위한 이벤트입니다.

    private IMultiplayerSessionBackend _sessionBackend; // 세션 생성/참가/종료를 실제로 처리하는 백엔드 인터페이스 참조입니다.
    private SessionAdmissionGuardService _admissionGuardService; // 정원 제한과 중도 Join 금지를 판정하는 서비스 인스턴스입니다.
    private ReconnectPolicyService _reconnectPolicyService; // 재접속 윈도우/1회 제한을 판정하는 서비스 인스턴스입니다.
    private int _currentPlayerCount = 1; // 현재 세션 인원 수(Host 포함)를 추적하는 런타임 값입니다.
    private bool _isStageInProgress; // 현재 Stage 진행 여부를 추적하는 런타임 플래그입니다.

    /// <summary>
    /// 현재 활성 세션 Join Code를 조회합니다.
    /// </summary>
    public string ActiveJoinCode => _activeJoinCode;

    /// <summary>
    /// 싱글톤 접근용 MultiplayerSessionOrchestrator 인스턴스입니다.
    /// </summary>
    public static MultiplayerSessionOrchestrator Instance { get; private set; }

    /// <summary>
    /// 현재 오케스트레이터가 Host 상태인지 조회합니다.
    /// </summary>
    public bool IsHosting => _connectionState == E_MultiplayerConnectionState.Hosting;

    /// <summary>
    /// Host가 Stage 진입을 시작해도 되는 최소 세션 인원 충족 여부를 조회합니다.
    /// </summary>
    public bool IsHostReadyForStageEntry
    {
        get
        {
            if (!IsHosting)
            {
                return true;
            }

            if (_currentPlayerCount < _maxPlayerCount)
            {
                return false;
            }

            if (_sessionBackend == null || string.IsNullOrWhiteSpace(_activeJoinCode))
            {
                return false;
            }

            return _currentPlayerCount >= _maxPlayerCount;
        }
    }

    /// <summary>
    /// 현재 세션 인원 수를 조회합니다.
    /// </summary>
    public int CurrentPlayerCount => _currentPlayerCount;

    /// <summary>
    /// Host 관점에서 원격 Client가 최소 1명 이상 참가했는지 여부를 조회합니다.
    /// </summary>
    public bool HasRemoteClientJoined => _currentPlayerCount > 1;

    /// <summary>
    /// 의존성과 정책 서비스를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (_gameFlowController == null)
        {
            _gameFlowController = GameFlowController.Instance != null
                ? GameFlowController.Instance
                : FindAnyObjectByType<GameFlowController>();
        }

        _sessionBackend = _sessionBackendBehaviour as IMultiplayerSessionBackend;
        if (_sessionBackend == null)
        {
            _sessionBackend = FindSessionBackendInScene();
        }
        _admissionGuardService = new SessionAdmissionGuardService(_maxPlayerCount);
        _reconnectPolicyService = new ReconnectPolicyService(_reconnectWindowSeconds);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>
    /// 오브젝트가 파괴될 때 싱글톤 참조를 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 타이틀에서 Host 멀티 세션 생성을 요청합니다.
    /// </summary>
    public async Task<bool> StartHostSessionFromTitleAsync(string hostClientId)
    {
        ResolveDependenciesIfNeeded();

        if (_sessionBackend == null)
        {
            return Fail("SessionBackendMissing");
        }

        if (_gameFlowController == null)
        {
            return Fail("GameFlowControllerMissing");
        }

        SessionCreateResult createdResult = await _sessionBackend.CreateSessionAsync(hostClientId, _maxPlayerCount);
        if (!createdResult.IsSuccess)
        {
            return Fail($"HostCreateFailed:{createdResult.Reason}");
        }
        _activeJoinCode = createdResult.JoinCode;

        _currentPlayerCount = 1;
        _isStageInProgress = false;
        _connectionState = E_MultiplayerConnectionState.Hosting;
        _reconnectPolicyService.Reset();

        if (_gameFlowController != null)
        {
            _gameFlowController.SetMultiplayerSessionRole(E_MultiplayerSessionRole.Host);
            bool movedToTown = _gameFlowController.RequestStartMultiplayerHost();
            if (!movedToTown)
            {
                return Fail("HostStartSceneLoadFailed");
            }
        }

        _onJoinCodeGenerated?.Invoke(_activeJoinCode);
        _onPlayerCountUpdated?.Invoke(_currentPlayerCount);
        return true;
    }

    /// <summary>
    /// 타이틀에서 Join Code 기반 Client 참가를 요청합니다.
    /// </summary>
    public async Task<bool> JoinSessionFromTitleAsync(string joinCode, string clientId)
    {
        ResolveDependenciesIfNeeded();

        string normalizedJoinCode = NormalizeJoinCode(joinCode); // 백엔드 조회에 사용할 공백 제거/대문자 보정 Join Code입니다.
        if (string.IsNullOrWhiteSpace(normalizedJoinCode))
        {
            return Fail("JoinCodeEmpty");
        }

        if (_sessionBackend == null)
        {
            return Fail("SessionBackendMissing");
        }

        if (_gameFlowController == null)
        {
            return Fail("GameFlowControllerMissing");
        }

        if (!_admissionGuardService.CanJoin(_isStageInProgress, _currentPlayerCount, out string policyReason))
        {
            return Fail($"AdmissionDenied:{policyReason}");
        }

        SessionOperationResult joinedResult = await _sessionBackend.JoinSessionAsync(normalizedJoinCode, clientId);
        if (!joinedResult.IsSuccess)
        {
            return Fail($"JoinFailed:{joinedResult.Reason}");
        }

        _activeJoinCode = normalizedJoinCode;
        _localClientId = clientId;
        _isLocalClientReadyDeclared = false;
        _currentPlayerCount = Math.Min(_maxPlayerCount, _currentPlayerCount + 1);
        _connectionState = E_MultiplayerConnectionState.JoinedAsClient;
        _onPlayerCountUpdated?.Invoke(_currentPlayerCount);

        if (_gameFlowController != null)
        {
            _gameFlowController.SetMultiplayerSessionRole(E_MultiplayerSessionRole.Client);
            _gameFlowController.RequestStartMultiplayerClient();
        }

        return true;
    }

    /// <summary>
    /// 씬 로드 완료 시점에 Client 준비 완료 상태를 자동 선언합니다.
    /// </summary>
    private void HandleSceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
    {
        if (_connectionState != E_MultiplayerConnectionState.JoinedAsClient)
        {
            return;
        }

        if (_isLocalClientReadyDeclared)
        {
            return;
        }

        _ = TryDeclareLocalClientReadyAsync();
    }

    /// <summary>
    /// UI 버튼 OnClick에서 인자 없이 호출할 수 있는 Join 요청 엔트리 포인트입니다.
    /// 제출 직전에 입력 필드 값을 다시 읽어 최신 Join Code로 참가를 시도합니다.
    /// </summary>
    public async void OnClickJoinSessionFromTitle()
    {
        RefreshUiJoinCodeFromInputField();
        await JoinSessionFromTitleAsync(_uiJoinCode, _uiClientId);
    }

    /// <summary>
    /// UI Presenter에서 성공/실패를 판정할 수 있도록 Join 요청 결과를 bool로 반환합니다.
    /// </summary>
    public async Task<bool> OnClickJoinSessionFromTitleProxyAsync()
    {
        RefreshUiJoinCodeFromInputField();
        return await JoinSessionFromTitleAsync(_uiJoinCode, _uiClientId);
    }

    /// <summary>
    /// 외부 UI(Binder/Presenter)에서 Join Code 입력 필드를 런타임에 주입합니다.
    /// </summary>
    public void SetUiJoinCodeInputField(TMP_InputField inputField)
    {
        _uiJoinCodeInputField = inputField;
    }

    /// <summary>
    /// 외부 UI가 비활성화될 때 현재 등록된 입력 필드를 안전하게 해제합니다.
    /// </summary>
    public void ReleaseUiJoinCodeInputField(TMP_InputField inputField)
    {
        if (_uiJoinCodeInputField == inputField)
        {
            _uiJoinCodeInputField = null;
        }
    }

    /// <summary>
    /// Host가 Stage 시작을 확정할 때 중도 Join 금지 상태를 활성화합니다.
    /// </summary>
    public async void NotifyHostStageStarted()
    {
        _isStageInProgress = true;
        if (!string.IsNullOrWhiteSpace(_activeJoinCode) && _sessionBackend != null)
        {
            SessionOperationResult result = await _sessionBackend.SetStageInProgressAsync(_activeJoinCode, true);
            if (!result.IsSuccess)
            {
                Debug.LogWarning($"[MultiplayerSessionOrchestrator] SetStageInProgressAsync failed. reason={result.Reason}", this);
            }
        }
    }

    /// <summary>
    /// Host가 Stage 종료를 확정할 때 중도 Join 금지 상태를 해제합니다.
    /// </summary>
    public async void NotifyHostStageEnded()
    {
        _isStageInProgress = false;
        if (!string.IsNullOrWhiteSpace(_activeJoinCode) && _sessionBackend != null)
        {
            SessionOperationResult result = await _sessionBackend.SetStageInProgressAsync(_activeJoinCode, false);
            if (!result.IsSuccess)
            {
                Debug.LogWarning($"[MultiplayerSessionOrchestrator] SetStageInProgressAsync failed. reason={result.Reason}", this);
            }
        }
    }

    /// <summary>
    /// Client 이탈을 기록하여 재접속 정책 검증에 사용합니다.
    /// </summary>
    public void NotifyClientDisconnected()
    {
        _reconnectPolicyService.MarkDisconnected(DateTime.UtcNow);
    }

    /// <summary>
    /// Client 재접속을 시도하고 정책 실패 시 타이틀 복귀를 수행합니다.
    /// </summary>
    public async Task<bool> TryReconnectClientAsync(string joinCode, string clientId)
    {
        if (!_reconnectPolicyService.TryConsumeReconnectToken(DateTime.UtcNow, out string reason))
        {
            Fail($"ReconnectDenied:{reason}");
            ReturnToTitle();
            return false;
        }

        if (_sessionBackend == null)
        {
            Fail("ReconnectJoinFailed:SessionBackendMissing");
            ReturnToTitle();
            return false;
        }

        SessionOperationResult joinResult = await _sessionBackend.JoinSessionAsync(joinCode, clientId);
        if (!joinResult.IsSuccess)
        {
            Fail($"ReconnectJoinFailed:{joinResult.Reason}");
            ReturnToTitle();
            return false;
        }

        _reconnectPolicyService.MarkReconnectSucceeded();
        _connectionState = E_MultiplayerConnectionState.JoinedAsClient;
        return true;
    }

    /// <summary>
    /// Host 종료 시 세션을 닫고 타이틀 복귀를 수행합니다.
    /// </summary>
    public async void ShutdownSessionByHost()
    {
        if (_sessionBackend != null && !string.IsNullOrWhiteSpace(_activeJoinCode))
        {
            SessionOperationResult result = await _sessionBackend.CloseSessionAsync(_activeJoinCode);
            if (!result.IsSuccess)
            {
                Debug.LogWarning($"[MultiplayerSessionOrchestrator] CloseSessionAsync warning. reason={result.Reason}", this);
            }
        }

        _connectionState = E_MultiplayerConnectionState.Closed;
        _currentPlayerCount = 0;
        _onPlayerCountUpdated?.Invoke(_currentPlayerCount);
        ReturnToTitle();
    }

    /// <summary>
    /// 현재 활성 세션 Join Code를 기준으로 백엔드 인원 수를 재조회하고 캐시를 갱신합니다.
    /// </summary>
    public async Task<bool> RefreshPlayerCountFromActiveSessionAsync()
    {
        ResolveDependenciesIfNeeded();

        if (_sessionBackend == null || string.IsNullOrWhiteSpace(_activeJoinCode))
        {
            return false;
        }

        PlayerCountResult playerCountResult = await _sessionBackend.GetPlayerCountAsync(_activeJoinCode);
        if (!playerCountResult.IsSuccess)
        {
            return false;
        }

        _currentPlayerCount = Mathf.Max(0, playerCountResult.PlayerCount);
        _onPlayerCountUpdated?.Invoke(_currentPlayerCount);
        return true;
    }

    /// <summary>
    /// 실패 사유를 기록하고 UI 이벤트를 발행합니다.
    /// </summary>
    private bool Fail(string reason)
    {
        _lastFailureReason = reason;
        _onSessionFailed?.Invoke(reason);
        return false;
    }

    /// <summary>
    /// 누락된 의존성 참조를 런타임에서 자동 재해석합니다.
    /// </summary>
    private void ResolveDependenciesIfNeeded()
    {
        if (!_autoResolveDependenciesOnUse)
        {
            return;
        }

        if (_gameFlowController == null)
        {
            _gameFlowController = GameFlowController.Instance != null
                ? GameFlowController.Instance
                : FindAnyObjectByType<GameFlowController>();
        }

        if (_sessionBackend == null)
        {
            _sessionBackend = FindSessionBackendInScene();
        }
    }

    /// <summary>
    /// Inspector 연결 우선 규칙을 유지하면서 활성 씬/런타임에서 IMultiplayerSessionBackend 구현체를 일반화 탐색합니다.
    /// </summary>
    private IMultiplayerSessionBackend FindSessionBackendInScene()
    {
        IMultiplayerSessionBackend backendFromInspector = _sessionBackendBehaviour as IMultiplayerSessionBackend; // Inspector 슬롯에 직접 연결된 백엔드 구현체입니다.
        if (backendFromInspector != null)
        {
            return backendFromInspector;
        }

        MonoBehaviour[] behaviourCandidates = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None); // IMultiplayerSessionBackend 구현 여부를 검사할 MonoBehaviour 후보 목록입니다.
        for (int index = 0; index < behaviourCandidates.Length; index++)
        {
            MonoBehaviour candidate = behaviourCandidates[index]; // 현재 검사 중인 백엔드 후보 컴포넌트입니다.
            if (candidate is not IMultiplayerSessionBackend backend)
            {
                continue;
            }

            _sessionBackendBehaviour = candidate;
            return backend;
        }

        return null;
    }

    /// <summary>
    /// 로컬 Client의 시스템 로드 완료 상태를 세션 백엔드에 자동 보고합니다.
    /// </summary>
    private async Task TryDeclareLocalClientReadyAsync()
    {
        ResolveDependenciesIfNeeded();

        if (_sessionBackend == null || string.IsNullOrWhiteSpace(_activeJoinCode))
        {
            return;
        }

        string localClientId = !string.IsNullOrWhiteSpace(_localClientId) ? _localClientId : _uiClientId; // 준비 완료 보고에 사용할 로컬 Client 식별자입니다.
        if (string.IsNullOrWhiteSpace(localClientId))
        {
            return;
        }

        SessionOperationResult declaredResult = await _sessionBackend.MarkClientReadyAsync(_activeJoinCode, localClientId);
        if (!declaredResult.IsSuccess)
        {
            _lastFailureReason = $"ClientReadyDeclareFailed:{declaredResult.Reason}";
            return;
        }

        _isLocalClientReadyDeclared = true;
    }

    /// <summary>
    /// GameFlow 종료 경로를 통해 타이틀로 복귀합니다.
    /// </summary>
    private void ReturnToTitle()
    {
        _onReturnToTitle?.Invoke();
        _gameFlowController?.RequestExit(true);
    }

    /// <summary>
    /// Join 버튼 클릭 직전에 입력 필드에서 최신 Join Code를 읽어 캐시를 동기화합니다.
    /// </summary>
    private void RefreshUiJoinCodeFromInputField()
    {
        ResolveUiJoinCodeInputFieldIfNeeded();
        if (_uiJoinCodeInputField == null)
        {
            return;
        }

        _uiJoinCode = NormalizeJoinCode(_uiJoinCodeInputField.text);
    }

    /// <summary>
    /// Join Code 입력 필드가 누락된 경우 활성 씬에서 자동 탐색해 참조를 보정합니다.
    /// </summary>
    private void ResolveUiJoinCodeInputFieldIfNeeded()
    {
        if (_uiJoinCodeInputField != null || !_autoResolveJoinCodeInputFieldOnUse)
        {
            return;
        }

        TMP_InputField[] inputFields = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 씬에서 탐색한 TMP_InputField 후보 목록입니다.
        if (inputFields == null || inputFields.Length == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_joinCodeInputFieldObjectName))
        {
            for (int i = 0; i < inputFields.Length; i++)
            {
                TMP_InputField candidate = inputFields[i]; // 이름 힌트 일치 여부를 검사할 입력 필드 후보입니다.
                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(candidate.gameObject.name, _joinCodeInputFieldObjectName, StringComparison.Ordinal))
                {
                    _uiJoinCodeInputField = candidate;
                    return;
                }
            }
        }

        _uiJoinCodeInputField = inputFields[0];
    }

    /// <summary>
    /// Join Code 입력 문자열을 백엔드 조회에 맞게 공백 제거/대문자 보정합니다.
    /// </summary>
    private static string NormalizeJoinCode(string joinCode)
    {
        if (joinCode == null)
        {
            return string.Empty;
        }

        return joinCode.Trim().ToUpperInvariant();
    }
}
