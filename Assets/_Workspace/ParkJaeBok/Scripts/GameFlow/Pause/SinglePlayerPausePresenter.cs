using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 싱글/멀티플레이 Town/Stage 공통 Pause 입력, UI 표시, Settings 연동, Main Menu 복귀를 조율하는 Presenter입니다.
/// </summary>
public class SinglePlayerPausePresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Pause 상태를 제어할 SinglePlayerPauseService입니다. 비어 있으면 런타임에서 해석합니다.")]
    [SerializeField] private SinglePlayerPauseService _pauseService; // Pause 상태 진입/해제를 수행할 서비스 참조입니다.

    [Tooltip("Pause 패널 View 컴포넌트입니다.")]
    [SerializeField] private PausePanelView _pausePanelView; // Pause 버튼 UI 표시/리스너 연결을 담당하는 View 참조입니다.

    [Tooltip("타이틀과 동일한 Settings UI 루트(GameObject)입니다.")]
    [SerializeField] private GameObject _settingsRoot; // Pause에서 재사용할 SettingsRoot 오브젝트 참조입니다.

    [Tooltip("SettingsRoot 오픈 전 동기화를 수행할 TitleMenuOptionsPanelBridge 컴포넌트입니다.")]
    [SerializeField] private MonoBehaviour _settingsBridgeComponent; // Settings 오픈 시점 Option 동기화를 담당하는 브리지 컴포넌트 참조입니다.

    [Tooltip("씬 전환 중 입력 차단 판정에 사용할 SceneTransitionService입니다. 비어 있으면 런타임에서 해석합니다.")]
    [SerializeField] private SceneTransitionService _sceneTransitionService; // 씬 전환 중 Pause 입력 차단 여부를 판정할 서비스 참조입니다.

    [Tooltip("Main Menu 복귀 요청을 전달할 GameFlowController입니다. 비어 있으면 런타임에서 해석합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // Main Menu 복귀 요청을 위임할 게임 흐름 컨트롤러 참조입니다.

    [Tooltip("멀티 세션 Host 종료/Client 단독 이탈 흐름을 위임할 MultiplayerSessionOrchestrator입니다. 비어 있으면 런타임에서 해석합니다.")]
    [SerializeField] private MultiplayerSessionOrchestrator _multiplayerSessionOrchestrator; // 멀티플레이 Main Menu 경로를 위임할 세션 오케스트레이터 참조입니다.

    [Header("Input")]
    [Tooltip("Pause 토글 입력 액션 참조입니다. 비어 있으면 Escape 키 폴백을 사용합니다.")]
    [SerializeField] private InputActionReference _pauseToggleAction; // Pause 토글 입력 액션 참조입니다.

    [Tooltip("Pause 토글 액션이 없을 때 Escape 키 폴백 입력을 사용할지 여부입니다.")]
    [SerializeField] private bool _useEscapeFallback = true; // Pause 액션 미구성 시 Escape 입력 폴백 활성 여부입니다.

    [Header("Behavior")]
    [Tooltip("Pause 해제 시 SettingsRoot가 열려 있으면 함께 닫을지 여부입니다.")]
    [SerializeField] private bool _closeSettingsOnResume = true; // Resume 동작에서 SettingsRoot를 자동으로 닫을지 여부입니다.

    [Tooltip("Pause 입력/상태 전환 로그를 상세 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLog = true; // Pause Presenter 상세 로그 출력 여부입니다.

    private ITitleMenuOptionsPanelBridge _settingsBridge; // SettingsRoot 오픈 전 동기화를 호출할 브리지 인터페이스 참조입니다.
    private PauseFlowCoordinator _pauseFlowCoordinator; // 플레이 모드별 Pause 흐름 제어를 담당하는 Coordinator 인스턴스입니다.
    private bool _isSettingsOpenedFromPause; // Pause에서 Settings를 열어둔 상태인지 추적하는 런타임 플래그입니다.

    /// <summary>
    /// 런타임 의존성 해석 및 브리지 유효성 검증을 수행합니다.
    /// </summary>
    private void Awake()
    {
        ResolveDependencies();

        _settingsBridge = _settingsBridgeComponent as ITitleMenuOptionsPanelBridge;
        if (_settingsBridgeComponent != null && _settingsBridge == null)
        {
            Debug.LogWarning("[SinglePlayerPausePresenter] settingsBridgeComponent가 ITitleMenuOptionsPanelBridge를 구현하지 않았습니다.", this);
        }

        if (_pausePanelView != null)
        {
            _pausePanelView.SetVisible(false);
            _pausePanelView.SetInteractable(true);
        }

        _pauseFlowCoordinator = new PauseFlowCoordinator(_gameFlowController, _pauseService, _multiplayerSessionOrchestrator);
    }

    /// <summary>
    /// Pause 버튼 리스너와 Pause 상태 변경 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        BindPauseButtons(true);

        if (_pauseService != null)
        {
            _pauseService.PauseStateChanged += HandlePauseStateChanged;
        }
    }

    /// <summary>
    /// Pause 버튼 리스너와 Pause 상태 변경 이벤트를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        BindPauseButtons(false);

        if (_pauseService != null)
        {
            _pauseService.PauseStateChanged -= HandlePauseStateChanged;
        }
    }

    /// <summary>
    /// 프레임마다 Pause 토글 입력과 Settings 닫힘 복귀를 처리합니다.
    /// </summary>
    private void Update()
    {
        if (ShouldIgnorePauseInput())
        {
            return;
        }

        if (WasPauseTogglePressedThisFrame() == false)
        {
            return;
        }

        if (StageSelectionUIController.TryCloseAnyOpenFromPauseInput())
        {
            return;
        }

        if (_pauseFlowCoordinator == null)
        {
            _pauseFlowCoordinator = new PauseFlowCoordinator(_gameFlowController, _pauseService, _multiplayerSessionOrchestrator);
        }

        if (_pauseFlowCoordinator != null && _pauseFlowCoordinator.IsPaused())
        {
            ResumeGame();
            return;
        }

        OpenPause();
    }

    /// <summary>
    /// Pause 상태 진입을 시도하고 Pause 패널을 표시합니다.
    /// </summary>
    public void OpenPause()
    {
        if (_pauseFlowCoordinator == null)
        {
            _pauseFlowCoordinator = new PauseFlowCoordinator(_gameFlowController, _pauseService, _multiplayerSessionOrchestrator);
        }

        if (_pauseFlowCoordinator == null)
        {
            Debug.LogWarning("[SinglePlayerPausePresenter] PauseFlowCoordinator가 없어 OpenPause를 중단합니다.", this);
            return;
        }

        bool paused = _pauseFlowCoordinator.TryOpenPause("Pause.ToggleInput"); // Pause 입력 토글에서 상태 진입 시도 결과입니다.
        if (paused == false)
        {
            return;
        }

        ShowPausePanel();
        if (_verboseLog)
        {
            Debug.Log("[SinglePlayerPausePresenter] Pause opened.", this);
        }
    }

    /// <summary>
    /// Pause 상태를 해제하고 Pause 패널을 숨깁니다.
    /// </summary>
    public void ResumeGame()
    {
        if (_pauseFlowCoordinator == null)
        {
            _pauseFlowCoordinator = new PauseFlowCoordinator(_gameFlowController, _pauseService, _multiplayerSessionOrchestrator);
        }

        if (_pauseFlowCoordinator == null)
        {
            Debug.LogWarning("[SinglePlayerPausePresenter] PauseFlowCoordinator가 없어 ResumeGame을 중단합니다.", this);
            return;
        }

        bool resumed = _pauseFlowCoordinator.TryResume("Pause.ResumeButton"); // Resume 처리 결과입니다.
        if (resumed == false)
        {
            Debug.LogWarning("[SinglePlayerPausePresenter] Pause 해제에 실패했습니다.", this);
            return;
        }

        if (_closeSettingsOnResume)
        {
            SetSettingsVisible(false);
        }

        _isSettingsOpenedFromPause = false;
        HidePausePanel();
        if (_verboseLog)
        {
            Debug.Log("[SinglePlayerPausePresenter] Pause resumed.", this);
        }
    }

    /// <summary>
    /// Pause 상태에서 SettingsRoot를 열고 Option 브리지 동기화를 수행합니다.
    /// </summary>
    public void OpenSettings()
    {
        if (_pauseFlowCoordinator == null)
        {
            _pauseFlowCoordinator = new PauseFlowCoordinator(_gameFlowController, _pauseService, _multiplayerSessionOrchestrator);
        }

        if (_pauseFlowCoordinator == null || _pauseFlowCoordinator.IsPaused() == false)
        {
            Debug.LogWarning("[SinglePlayerPausePresenter] Pause 상태가 아니라 Settings 오픈을 차단합니다.", this);
            return;
        }

        if (_settingsRoot == null)
        {
            Debug.LogWarning("[SinglePlayerPausePresenter] SettingsRoot가 비어 있어 Settings 오픈을 건너뜁니다.", this);
            return;
        }

        _settingsBridge?.HandleBeforeOpen();

        SetSettingsVisible(true);
        _isSettingsOpenedFromPause = true;
        HidePausePanel();

        _settingsBridge?.HandleAfterOpen();
        if (_verboseLog)
        {
            Debug.Log("[SinglePlayerPausePresenter] Settings opened from pause.", this);
        }
    }

    /// <summary>
    /// Pause 상태에서 Main Menu 복귀를 요청합니다.
    /// </summary>
    public async void ReturnToMainMenu()
    {
        ResolveDependencies();

        if (_pauseFlowCoordinator == null)
        {
            _pauseFlowCoordinator = new PauseFlowCoordinator(_gameFlowController, _pauseService, _multiplayerSessionOrchestrator);
        }

        if (_pauseFlowCoordinator == null)
        {
            Debug.LogWarning("[SinglePlayerPausePresenter] PauseFlowCoordinator가 없어 Main Menu 복귀를 중단합니다.", this);
            return;
        }
        _pauseFlowCoordinator.ForceClearPauseState("Pause.ReturnToMainMenu");

        SetSettingsVisible(false);
        HidePausePanel();
        _isSettingsOpenedFromPause = false;

        bool requested = await _pauseFlowCoordinator.ReturnToMainMenuAsync(); // 모드별 Main Menu 복귀 플로우 요청 결과입니다.
        if (requested == false)
        {
            Debug.LogWarning("[SinglePlayerPausePresenter] Main Menu 복귀 요청에 실패했습니다.", this);
            return;
        }

        if (_verboseLog)
        {
            Debug.Log("[SinglePlayerPausePresenter] Requested return to Main Menu from pause.", this);
        }
    }

    /// <summary>
    /// Pause 상태 변경 이벤트를 수신해 UI 표시 상태를 동기화합니다.
    /// </summary>
    private void HandlePauseStateChanged(bool isPaused)
    {
        if (isPaused == false)
        {
            SetSettingsVisible(false);
            HidePausePanel();
            _isSettingsOpenedFromPause = false;
            return;
        }

        if (_isSettingsOpenedFromPause == false)
        {
            ShowPausePanel();
        }
    }

    /// <summary>
    /// Pause 패널 버튼 리스너 구독/해제를 처리합니다.
    /// </summary>
    private void BindPauseButtons(bool bind)
    {
        if (_pausePanelView == null)
        {
            return;
        }

        if (bind)
        {
            _pausePanelView.AddResumeListener(ResumeGame);
            _pausePanelView.AddSettingsListener(OpenSettings);
            _pausePanelView.AddMainMenuListener(ReturnToMainMenu);
            return;
        }

        _pausePanelView.RemoveResumeListener(ResumeGame);
        _pausePanelView.RemoveSettingsListener(OpenSettings);
        _pausePanelView.RemoveMainMenuListener(ReturnToMainMenu);
    }

    /// <summary>
    /// SettingsRoot 활성 상태를 안전하게 설정합니다.
    /// </summary>
    private void SetSettingsVisible(bool isVisible)
    {
        if (_settingsRoot != null)
        {
            _settingsRoot.SetActive(isVisible);
        }
    }

    /// <summary>
    /// Pause 패널 표시를 수행합니다.
    /// </summary>
    private void ShowPausePanel()
    {
        if (_pausePanelView == null)
        {
            Debug.LogWarning("[SinglePlayerPausePresenter] PausePanelView가 비어 있어 패널 표시를 건너뜁니다.", this);
            return;
        }

        _pausePanelView.SetVisible(true);
        _pausePanelView.SetInteractable(true);
    }

    /// <summary>
    /// Pause 패널 숨김을 수행합니다.
    /// </summary>
    private void HidePausePanel()
    {
        if (_pausePanelView == null)
        {
            return;
        }

        _pausePanelView.SetVisible(false);
    }

    /// <summary>
    /// Pause 토글 입력 처리 전 차단 조건을 판정합니다.
    /// </summary>
    private bool ShouldIgnorePauseInput()
    {
        ResolveDependencies();

        if (_sceneTransitionService != null && _sceneTransitionService.IsTransitioning())
        {
            return true;
        }

        if (_pauseFlowCoordinator == null)
        {
            _pauseFlowCoordinator = new PauseFlowCoordinator(_gameFlowController, _pauseService, _multiplayerSessionOrchestrator);
        }

        if (_pauseFlowCoordinator != null && !_pauseFlowCoordinator.CanOpenPausePanel())
        {
            return true;
        }

        if (_isSettingsOpenedFromPause && _settingsRoot != null)
        {
            if (_settingsRoot.activeSelf == false)
            {
                _isSettingsOpenedFromPause = false;
                ShowPausePanel();
            }

            return _settingsRoot.activeSelf;
        }

        return false;
    }

    /// <summary>
    /// 이번 프레임 Pause 토글 입력 발생 여부를 판정합니다.
    /// </summary>
    private bool WasPauseTogglePressedThisFrame()
    {
        if (_pauseToggleAction != null && _pauseToggleAction.action != null)
        {
            return _pauseToggleAction.action.WasPerformedThisFrame();
        }

        if (_useEscapeFallback)
        {
            if (Keyboard.current == null)
            {
                Debug.LogWarning("[SinglePlayerPausePresenter] Keyboard.current가 null이라 Escape 폴백 입력을 사용할 수 없습니다.", this);
                return false;
            }

            return Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        return false;
    }

    /// <summary>
    /// 직렬화 참조 또는 싱글톤에서 런타임 의존성을 해석합니다.
    /// </summary>
    private void ResolveDependencies()
    {
        if (_pauseService == null)
        {
            _pauseService = SinglePlayerPauseService.Instance != null
                ? SinglePlayerPauseService.Instance
                : FindAnyObjectByType<SinglePlayerPauseService>();
        }

        if (_sceneTransitionService == null)
        {
            _sceneTransitionService = SceneTransitionService.Instance;
        }

        if (_gameFlowController == null)
        {
            _gameFlowController = GameFlowController.Instance;
        }

        if (_multiplayerSessionOrchestrator == null)
        {
            _multiplayerSessionOrchestrator = MultiplayerSessionOrchestrator.Instance;
        }
    }
}
