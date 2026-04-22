using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// 타이틀 Host 시작 흐름의 Busy 입력 차단, 진행 상태, 실패 메시지 표시를 담당하는 View 컴포넌트입니다.
/// </summary>
public class TitleMultiplayerHostFlowView : MonoBehaviour
{
    [Header("Input Block")]
    [Tooltip("Busy 상태에서 전체 메뉴 상호작용을 비활성화할 CanvasGroup입니다.")]
    [SerializeField] private CanvasGroup _titleMenuCanvasGroup; // 타이틀 메뉴 루트 상호작용을 일괄 차단하기 위한 CanvasGroup 참조입니다.

    [Tooltip("Busy 상태에서 입력 차단용 전체화면 오버레이로 사용할 오브젝트입니다.")]
    [SerializeField] private GameObject _inputBlockOverlay; // 마우스 포인터 입력을 가로채고 Busy 시각 피드백을 제공할 오버레이 루트입니다.

    [Tooltip("Busy 상태에서 비활성화할 EventSystem 참조입니다. 비어 있으면 EventSystem.current를 사용합니다.")]
    [SerializeField] private EventSystem _eventSystem; // 키보드/게임패드 내비게이션과 Submit/Cancel을 차단할 EventSystem 참조입니다.

    [Tooltip("Busy 상태에서 비활성화할 UI Input Module 컴포넌트입니다(Input System UI Input Module/Standalone Input Module).")]
    [SerializeField] private InputSystemUIInputModule _uiInputModuleBehaviour; // EventSystem에 연결된 UI 입력 모듈 비활성화를 위한 공용 Behaviour 참조입니다.

    [Header("Status UI")]
    [Tooltip("진행 상태 패널 루트 오브젝트입니다.")]
    [SerializeField] private GameObject _statusRoot; // Host 시작 진행 상태 텍스트/스피너를 묶는 루트 오브젝트입니다.

    [Tooltip("진행 상태 문구를 표시할 TMP_Text입니다.")]
    [SerializeField] private TMP_Text _statusText; // 현재 흐름 상태 문자열을 사용자에게 표시할 텍스트 컴포넌트입니다.

    [Tooltip("Host 시작 진행 중 표시할 기본 상태 문구입니다.")]
    [SerializeField] private string _startingStatusMessage = "Starting host..."; // StartingHost 상태에서 표시할 기본 안내 문구입니다.

    [Header("Failure UI")]
    [Tooltip("Host 시작 실패 메시지 패널 루트 오브젝트입니다.")]
    [SerializeField] private GameObject _failureRoot; // 실패 사실을 사용자에게 알리는 패널 루트 오브젝트입니다.

    [Tooltip("Host 시작 실패 문구를 표시할 TMP_Text입니다.")]
    [SerializeField] private TMP_Text _failureText; // 실패 사유 또는 일반 실패 메시지를 출력할 텍스트 컴포넌트입니다.

    [Tooltip("실패 사유가 비어 있을 때 출력할 기본 실패 문구입니다.")]
    [SerializeField] private string _defaultFailureMessage = "Failed to start host. Please retry."; // 실패 사유가 비어 있거나 내부 코드일 때 사용자에게 안내할 기본 실패 메시지입니다.

    [Header("Policy")]
    [Tooltip("진행 상태 패널 표시를 사용할지 여부입니다.")]
    [SerializeField] private bool _showStatusWhileBusy = true; // Busy 상태에서 상태 표시 UI를 노출할지 제어하는 정책 플래그입니다.

    [Tooltip("실패 사유 코드(reason)를 실패 문구 뒤에 함께 표시할지 여부입니다.")]
    [SerializeField] private bool _appendFailureReason = true; // 실패 패널에서 내부 실패 코드를 함께 출력할지 제어하는 정책 플래그입니다.

    private bool _cachedEventSystemEnabled = true; // Busy 진입 전 EventSystem 활성 상태를 복원하기 위해 캐시한 값입니다.
    private bool _cachedUiInputModuleEnabled = true; // Busy 진입 전 UI Input Module 활성 상태를 복원하기 위해 캐시한 값입니다.

    /// <summary>
    /// 초기 상태(Idle) UI를 적용합니다.
    /// </summary>
    private void Awake()
    {
        ApplyIdle();
    }

    /// <summary>
    /// Idle 상태 UI를 적용하고 입력 차단을 해제합니다.
    /// </summary>
    public void ApplyIdle()
    {
        SetInputBlocked(false);
        SetStatusVisible(false, string.Empty);
        SetFailureVisible(false, string.Empty);
    }

    /// <summary>
    /// StartingHost 상태 UI를 적용하고 전체 입력을 차단합니다.
    /// </summary>
    public void ShowStartingHost()
    {
        ShowBusyStatus(_startingStatusMessage);
    }

    /// <summary>
    /// Client Join 진행 상태 UI를 적용하고 전체 입력을 차단합니다.
    /// </summary>
    public void ShowJoiningClient(string joiningStatusMessage)
    {
        string safeStatusMessage = string.IsNullOrWhiteSpace(joiningStatusMessage)
            ? "Joining session..."
            : joiningStatusMessage; // Join 진행 상태 UI에 표시할 최종 문구 문자열입니다.

        ShowBusyStatus(safeStatusMessage);
    }

    /// <summary>
    /// 싱글플레이 시작 진행 상태 UI를 적용하고 전체 입력을 차단합니다.
    /// </summary>
    public void ShowStartingSinglePlayer(string startingStatusMessage)
    {
        string safeStatusMessage = string.IsNullOrWhiteSpace(startingStatusMessage)
            ? "Starting game..."
            : startingStatusMessage; // 싱글플레이 시작 진행 상태 UI에 표시할 최종 문구 문자열입니다.

        ShowBusyStatus(safeStatusMessage);
    }

    /// <summary>
    /// 성공 상태 UI를 적용합니다.
    /// 성공 시에는 일반적으로 즉시 씬 전환되므로 입력 차단만 유지하고 상태 패널을 숨깁니다.
    /// </summary>
    public void ShowSucceeded()
    {
        SetStatusVisible(false, string.Empty);
        SetFailureVisible(false, string.Empty);
    }

    /// <summary>
    /// 실패 상태 UI를 적용하고 입력 차단을 해제해 재시도를 허용합니다.
    /// </summary>
    public void ShowFailed(string failureReason)
    {
        SetInputBlocked(false);
        SetStatusVisible(false, string.Empty);

        string failureMessage = _defaultFailureMessage; // 실패 패널에 출력할 최종 문구 문자열입니다.
        if (_appendFailureReason && !string.IsNullOrWhiteSpace(failureReason))
        {
            failureMessage = $"{_defaultFailureMessage}\nReason: {failureReason}";
        }

        SetFailureVisible(true, failureMessage);
    }

    /// <summary>
    /// 공통 Busy 상태 UI를 적용해 입력을 차단하고 진행 문구를 표시합니다.
    /// </summary>
    private void ShowBusyStatus(string statusMessage)
    {
        SetFailureVisible(false, string.Empty);
        SetInputBlocked(true);
        SetStatusVisible(_showStatusWhileBusy, statusMessage);
    }

    /// <summary>
    /// Busy 정책에 따라 입력 차단 관련 컴포넌트 활성 상태를 제어합니다.
    /// </summary>
    private void SetInputBlocked(bool blocked)
    {
        if (_titleMenuCanvasGroup != null)
        {
            _titleMenuCanvasGroup.interactable = !blocked;
            _titleMenuCanvasGroup.blocksRaycasts = !blocked;
        }

        if (_inputBlockOverlay != null)
        {
            _inputBlockOverlay.SetActive(blocked);
        }

        EventSystem resolvedEventSystem = ResolveEventSystem(); // Busy 입력 차단 시 토글할 EventSystem 참조입니다.
        if (resolvedEventSystem != null)
        {
            if (blocked)
            {
                _cachedEventSystemEnabled = resolvedEventSystem.enabled;
                resolvedEventSystem.SetSelectedGameObject(null);
                resolvedEventSystem.enabled = false;
            }
            else
            {
                resolvedEventSystem.enabled = _cachedEventSystemEnabled;
            }
        }

        if (_uiInputModuleBehaviour != null)
        {
            if (blocked)
            {
                _cachedUiInputModuleEnabled = _uiInputModuleBehaviour.enabled;
                _uiInputModuleBehaviour.enabled = false;
            }
            else
            {
                _uiInputModuleBehaviour.enabled = _cachedUiInputModuleEnabled;
            }
        }
    }

    /// <summary>
    /// 진행 상태 패널 표시 여부와 문구를 갱신합니다.
    /// </summary>
    private void SetStatusVisible(bool isVisible, string statusMessage)
    {
        if (_statusRoot != null)
        {
            _statusRoot.SetActive(isVisible);
        }

        if (_statusText != null)
        {
            _statusText.text = statusMessage;
        }
    }

    /// <summary>
    /// 실패 패널 표시 여부와 문구를 갱신합니다.
    /// </summary>
    private void SetFailureVisible(bool isVisible, string failureMessage)
    {
        if (_failureRoot != null)
        {
            _failureRoot.SetActive(isVisible);
        }

        if (_failureText != null)
        {
            _failureText.text = failureMessage;
        }
    }

    /// <summary>
    /// 직렬화 참조 또는 EventSystem.current를 통해 유효한 EventSystem을 해석합니다.
    /// </summary>
    private EventSystem ResolveEventSystem()
    {
        if (_eventSystem != null)
        {
            return _eventSystem;
        }

        return EventSystem.current;
    }
}
