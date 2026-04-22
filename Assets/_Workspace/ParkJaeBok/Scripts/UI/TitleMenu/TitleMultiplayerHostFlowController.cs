using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 타이틀 메뉴에서 Multiplayer Host 시작/Client Join 요청을 단일 진입점으로 관리하고 상태 전이/중복 방지/실패 처리를 조율하는 흐름 제어기입니다.
/// </summary>
public class TitleMultiplayerHostFlowController : MonoBehaviour
{
    /// <summary>
    /// Multiplayer 요청 흐름의 현재 상태를 표현하는 열거형입니다.
    /// </summary>
    public enum E_MultiplayerRequestFlowState
    {
        Idle = 0,
        StartingHost = 1,
        JoiningClient = 2,
        StartingSinglePlayer = 3,
        Failed = 4,
        Succeeded = 5
    }

    [Serializable]
    public class MultiplayerRequestFailedEvent : UnityEvent<string> { }

    [Header("Dependencies")]
    [Tooltip("Host 세션 시작 실제 실행을 위임할 MultiplayerSessionOrchestrator 참조입니다. 비어 있으면 런타임에서 자동 탐색합니다.")]
    [SerializeField] private MultiplayerSessionOrchestrator _multiplayerSessionOrchestrator; // Host 세션 생성 및 게임 흐름 진입을 실행할 오케스트레이터 참조입니다.

    [Tooltip("Host 시작 중 입력 차단/상태/실패 UI를 렌더링할 뷰 참조입니다.")]
    [SerializeField] private TitleMultiplayerHostFlowView _flowView; // 흐름 상태에 맞춰 Busy/Failure UI를 표시할 뷰 참조입니다.

    [Header("Host Policy")]
    [Tooltip("Host 시작 요청 시 전달할 Host 식별자 문자열입니다.")]
    [SerializeField] private string _hostClientId = "Host_A"; // 오케스트레이터 Host 시작 API에 전달할 로컬 Host 식별자입니다.

    [Header("Client Policy")]
    [Tooltip("Client Join 진행 중 상태 텍스트로 표시할 문구입니다.")]
    [SerializeField] private string _joiningStatusMessage = "Joining session..."; // Client Join 요청 진행 중 사용자에게 표시할 상태 문구입니다.

    [Header("Single Player Policy")]
    [Tooltip("싱글플레이 시작 진행 중 상태 텍스트로 표시할 문구입니다.")]
    [SerializeField] private string _singlePlayerStartingStatusMessage = "Starting game..."; // 싱글플레이 시작 요청 진행 중 사용자에게 표시할 상태 문구입니다.

    [Header("Events")]
    [Tooltip("Host 시작 성공 시 호출할 이벤트입니다.")]
    [SerializeField] private UnityEvent _onHostStartSucceeded; // Host 시작 성공 후 추가 UI 후처리를 연결하기 위한 이벤트입니다.

    [Tooltip("Client Join 성공 시 호출할 이벤트입니다.")]
    [SerializeField] private UnityEvent _onClientJoinSucceeded; // Client Join 성공 후 추가 UI 후처리를 연결하기 위한 이벤트입니다.

    [Tooltip("싱글플레이 시작 성공 시 호출할 이벤트입니다.")]
    [SerializeField] private UnityEvent _onSinglePlayerStartSucceeded; // 싱글플레이 시작 성공 후 추가 UI 후처리를 연결하기 위한 이벤트입니다.

    [Tooltip("Host 시작/Client Join 실패 시 실패 사유 문자열과 함께 호출할 이벤트입니다.")]
    [SerializeField] private MultiplayerRequestFailedEvent _onMultiplayerRequestFailed; // 멀티 요청 실패 시 외부 UI/사운드 후처리를 연결하기 위한 이벤트입니다.

    [Header("Debug")]
    [Tooltip("디버그용: 현재 Multiplayer 요청 흐름 상태입니다.")]
    [SerializeField] private E_MultiplayerRequestFlowState _state = E_MultiplayerRequestFlowState.Idle; // 멀티 요청 흐름 상태 머신의 현재 상태를 추적하는 런타임 값입니다.

    [Tooltip("디버그용: 마지막 Host 시작/Client Join 실패 사유 문자열입니다.")]
    [SerializeField] private string _lastFailureReason; // 최근 멀티 요청 실패 원인을 Inspector에서 확인하기 위한 디버그 문자열입니다.

    /// <summary>
    /// 외부에서 현재 Busy 상태 여부를 조회하기 위한 프로퍼티입니다.
    /// </summary>
    public bool IsBusy => _state == E_MultiplayerRequestFlowState.StartingHost
        || _state == E_MultiplayerRequestFlowState.JoiningClient
        || _state == E_MultiplayerRequestFlowState.StartingSinglePlayer;

    /// <summary>
    /// 컴포넌트 활성 시 의존성을 자동 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_multiplayerSessionOrchestrator == null)
        {
            _multiplayerSessionOrchestrator = MultiplayerSessionOrchestrator.Instance != null
                ? MultiplayerSessionOrchestrator.Instance
                : FindAnyObjectByType<MultiplayerSessionOrchestrator>();
        }

        if (_flowView == null)
        {
            _flowView = GetComponent<TitleMultiplayerHostFlowView>();
        }

        _flowView?.ApplyIdle();
    }

    /// <summary>
    /// 타이틀 Host 시작 요청 공용 진입점입니다.
    /// 진행 중 재진입은 차단하고 Warning 로그를 남깁니다.
    /// </summary>
    public void RequestStartHost()
    {
        if (IsBusy)
        {
            Debug.LogWarning($"[TitleMultiplayerHostFlowController] 멀티 요청 진행 중 Host 시작 재요청을 차단했습니다. state={_state}", this);
            return;
        }

        _ = StartHostFlowAsync();
    }

    /// <summary>
    /// 타이틀 Client Join 요청 공용 진입점입니다.
    /// 진행 중 재진입은 차단하고 Warning 로그를 남깁니다.
    /// </summary>
    public async Task<bool> RequestJoinFromTitleAsync()
    {
        if (IsBusy)
        {
            Debug.LogWarning($"[TitleMultiplayerHostFlowController] 멀티 요청 진행 중 Client Join 재요청을 차단했습니다. state={_state}", this);
            return false;
        }

        return await StartClientJoinFlowAsync();
    }

    /// <summary>
    /// 타이틀 싱글플레이 시작 요청 공용 진입점입니다.
    /// 요청 실행 중에는 다른 입력을 차단하고 성공/실패에 따라 상태를 전이합니다.
    /// </summary>
    public bool RequestStartSinglePlayer(Func<bool> singlePlayerStartRequest, string requestName)
    {
        if (IsBusy)
        {
            Debug.LogWarning($"[TitleMultiplayerHostFlowController] 멀티/싱글 요청 진행 중 싱글 시작 재요청을 차단했습니다. state={_state}, request={requestName}", this);
            return false;
        }

        if (singlePlayerStartRequest == null)
        {
            HandleFailed("SinglePlayerStartRequestMissing", $"[TitleMultiplayerHostFlowController] 싱글 시작 요청 델리게이트가 null이라 시작을 중단합니다. request={requestName}");
            return false;
        }

        TransitionTo(E_MultiplayerRequestFlowState.StartingSinglePlayer);
        _flowView?.ShowStartingSinglePlayer(_singlePlayerStartingStatusMessage);

        bool started;
        try
        {
            started = singlePlayerStartRequest.Invoke();
        }
        catch (Exception exception)
        {
            HandleFailed($"SinglePlayerException:{exception.GetType().Name}", $"[TitleMultiplayerHostFlowController] 싱글 시작 중 예외가 발생했습니다. request={requestName}, message={exception.Message}");
            Debug.LogException(exception, this);
            return false;
        }

        if (started)
        {
            TransitionTo(E_MultiplayerRequestFlowState.Succeeded);
            _flowView?.ShowSucceeded();
            _onSinglePlayerStartSucceeded?.Invoke();
            return true;
        }

        string safeRequestName = string.IsNullOrWhiteSpace(requestName) ? "UnknownSingleStart" : requestName; // 실패 로그/코드에 사용할 안전한 요청 이름입니다.
        HandleFailed($"SinglePlayerStartFailed:{safeRequestName}", $"[TitleMultiplayerHostFlowController] 싱글 시작 실패. request={safeRequestName}");
        return false;
    }

    /// <summary>
    /// Host 시작 표준 흐름(상태 전이/입력 차단/로딩 표시/성공·실패 후처리)을 비동기로 실행합니다.
    /// </summary>
    private async Task StartHostFlowAsync()
    {
        TransitionTo(E_MultiplayerRequestFlowState.StartingHost);
        _flowView?.ShowStartingHost();

        if (_multiplayerSessionOrchestrator == null)
        {
            HandleFailed("OrchestratorMissing", "[TitleMultiplayerHostFlowController] MultiplayerSessionOrchestrator 참조가 비어 있어 Host 시작을 중단합니다.");
            return;
        }

        bool started;
        try
        {
            started = await _multiplayerSessionOrchestrator.StartHostSessionFromTitleAsync(_hostClientId);
        }
        catch (Exception exception)
        {
            HandleFailed($"Exception:{exception.GetType().Name}", $"[TitleMultiplayerHostFlowController] Host 시작 중 예외가 발생했습니다. message={exception.Message}");
            Debug.LogException(exception, this);
            return;
        }

        if (started)
        {
            TransitionTo(E_MultiplayerRequestFlowState.Succeeded);
            _flowView?.ShowSucceeded();
            _onHostStartSucceeded?.Invoke();
            return;
        }

        string failureReason = !string.IsNullOrWhiteSpace(_multiplayerSessionOrchestrator.LastFailureReason)
            ? _multiplayerSessionOrchestrator.LastFailureReason
            : "HostStartFailed"; // 오케스트레이터 실패 사유가 비어 있을 때 표시/로그에 사용할 기본 실패 코드입니다.

        HandleFailed(failureReason, $"[TitleMultiplayerHostFlowController] Host 시작 실패. reason={failureReason}");
    }

    /// <summary>
    /// Client Join 표준 흐름(상태 전이/입력 차단/로딩 표시/성공·실패 후처리)을 비동기로 실행합니다.
    /// </summary>
    private async Task<bool> StartClientJoinFlowAsync()
    {
        TransitionTo(E_MultiplayerRequestFlowState.JoiningClient);
        _flowView?.ShowJoiningClient(_joiningStatusMessage);

        if (_multiplayerSessionOrchestrator == null)
        {
            HandleFailed("OrchestratorMissing", "[TitleMultiplayerHostFlowController] MultiplayerSessionOrchestrator 참조가 비어 있어 Client Join을 중단합니다.");
            return false;
        }

        bool joined;
        try
        {
            joined = await _multiplayerSessionOrchestrator.OnClickJoinSessionFromTitleProxyAsync();
        }
        catch (Exception exception)
        {
            HandleFailed($"Exception:{exception.GetType().Name}", $"[TitleMultiplayerHostFlowController] Client Join 중 예외가 발생했습니다. message={exception.Message}");
            Debug.LogException(exception, this);
            return false;
        }

        if (joined)
        {
            TransitionTo(E_MultiplayerRequestFlowState.Succeeded);
            _flowView?.ShowSucceeded();
            _onClientJoinSucceeded?.Invoke();
            return true;
        }

        string failureReason = !string.IsNullOrWhiteSpace(_multiplayerSessionOrchestrator.LastFailureReason)
            ? _multiplayerSessionOrchestrator.LastFailureReason
            : "ClientJoinFailed"; // 오케스트레이터 실패 사유가 비어 있을 때 표시/로그에 사용할 기본 실패 코드입니다.

        HandleFailed(failureReason, $"[TitleMultiplayerHostFlowController] Client Join 실패. reason={failureReason}");
        return false;
    }

    /// <summary>
    /// 실패 상태 전이와 실패 UI 표시, 재시도 가능 상태 복귀를 처리합니다.
    /// </summary>
    private void HandleFailed(string failureReason, string logMessage)
    {
        _lastFailureReason = string.IsNullOrWhiteSpace(failureReason) ? "Unknown" : failureReason;
        Debug.LogWarning(logMessage, this);

        TransitionTo(E_MultiplayerRequestFlowState.Failed);
        _flowView?.ShowFailed(_lastFailureReason);
        _onMultiplayerRequestFailed?.Invoke(_lastFailureReason);

        TransitionTo(E_MultiplayerRequestFlowState.Idle);
    }

    /// <summary>
    /// 상태 전이 책임을 단일 메서드에서 관리해 추적 가능성을 높입니다.
    /// </summary>
    private void TransitionTo(E_MultiplayerRequestFlowState nextState)
    {
        _state = nextState;
    }
}
