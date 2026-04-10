using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면에서 싱글/멀티 플레이 모드 선택을 GameFlowController에 전달하는 프레젠터입니다.
/// </summary>
public class TitlePlayModePresenter : MonoBehaviour
{
    private enum E_TitleSlotFlowMode
    {
        None = 0,
        SingleNewGame = 1,
        SingleLoadGame = 2,
        Multiplay = 3
    }

    [Header("Dependencies")]
    [Tooltip("모드 선택 요청을 전달할 GameFlowController 참조입니다. 비어 있으면 런타임에서 자동 탐색합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // 모드 선택 요청을 전달할 게임 흐름 컨트롤러 참조입니다.

    [Tooltip("플레이 시작 전에 활성 슬롯을 지정할 SaveCoordinator 참조입니다. 비어 있으면 SaveCoordinator.Instance를 사용합니다.")]
    [SerializeField] private SaveCoordinator _saveCoordinator; // 플레이 진입 전에 활성 저장 슬롯을 반영할 SaveCoordinator 참조입니다.

    [Tooltip("멀티 세션 생성/참가 흐름을 위임할 MultiplayerSessionOrchestrator 참조입니다. 비어 있으면 런타임에서 자동 탐색합니다.")]
    [SerializeField] private MultiplayerSessionOrchestrator _multiplayerSessionOrchestrator; // 멀티 Host/Client 세션 흐름을 처리할 오케스트레이터 참조입니다.

    [Tooltip("멀티 관련 UI 액션 시 오케스트레이터 참조가 비어 있으면 DDOL 영역까지 자동 재탐색할지 여부입니다.")]
    [SerializeField] private bool _autoResolveMultiplayerSessionOrchestratorOnUse = true; // 멀티 버튼 클릭 시 오케스트레이터 자동 재탐색 활성화 여부를 제어하는 플래그입니다.

    [Header("Slot Policy")]
    [Tooltip("Play 관련 버튼 클릭 시 공통으로 적용할 기본 슬롯 번호입니다.")]
    [Min(1)]
    [SerializeField] private int _selectedSlotIndex = 1; // Play 요청 전에 SaveCoordinator에 적용할 현재 선택 슬롯 번호입니다.

    [Tooltip("Continue 클릭 시 마지막 사용 슬롯을 자동으로 선택할지 여부입니다.")]
    [SerializeField] private bool _autoSelectLastUsedSlotForContinue = true; // Continue 요청 전에 마지막 사용 슬롯 자동 선택 정책 플래그입니다.

    [Tooltip("New Game 시작 전에 선택 슬롯 데이터를 삭제할지 여부입니다.")]
    [SerializeField] private bool _clearSlotBeforeNewGame = true; // New Game 시작 전 슬롯 초기화 수행 여부를 제어하는 정책 플래그입니다.

    [Header("Panels")]
    [Tooltip("타이틀 초기 상단 메뉴 패널입니다.")]
    [SerializeField] private GameObject _panelTopMenu; // 타이틀 진입 직후 노출할 상단 메뉴 패널 참조입니다.

    [Tooltip("싱글 메뉴 패널입니다.")]
    [SerializeField] private GameObject _panelSingleMenu; // 싱글 메뉴 흐름에서 활성화할 패널 참조입니다.

    [Tooltip("슬롯 선택 패널입니다.")]
    [SerializeField] private GameObject _panelSlotSelect; // 싱글/멀티 공용 슬롯 선택 패널 참조입니다.

    [Tooltip("멀티 Host/Client 모드 선택 패널입니다.")]
    [SerializeField] private GameObject _panelMultiplayMode; // 멀티 슬롯 선택 후 Host/Client 선택에 사용할 패널 참조입니다.

    [Tooltip("Client Join 팝업 루트 오브젝트입니다.")]
    [SerializeField] private GameObject _multiplayerPanelRoot; // Client Join Code 입력 팝업을 표시할 루트 패널 참조입니다.

    [Tooltip("기존 Load Game 패널 루트(레거시 패널 유지 시 사용)입니다.")]
    [SerializeField] private GameObject _legacyLoadGamePanelRoot; // 레거시 Load Game 모달을 열고 닫을 패널 루트 참조입니다.

    [Tooltip("기존 Options 패널 루트(레거시 패널 유지 시 사용)입니다.")]
    [SerializeField] private GameObject _legacyOptionsPanelRoot; // 레거시 Options 모달을 열고 닫을 패널 루트 참조입니다.

    [Tooltip("모달 패널 표시 시 함께 켤 공통 백드롭입니다.")]
    [SerializeField] private GameObject _modalBackdrop; // 레거시 Load/Options 패널 표시 중 입력 차단을 담당하는 백드롭 참조입니다.

    [Header("Single Menu Buttons")]
    [Tooltip("Continue 버튼 참조입니다. 데이터가 없으면 비활성화됩니다.")]
    [SerializeField] private Button _singleContinueButton; // Continue 가능 여부를 반영할 싱글 메뉴 Continue 버튼 참조입니다.

    [Tooltip("Load Game 버튼 참조입니다. 데이터가 없으면 비활성화됩니다.")]
    [SerializeField] private Button _singleLoadGameButton; // Load Game 가능 여부를 반영할 싱글 메뉴 Load 버튼 참조입니다.

    [Header("Multiplayer")]
    [Tooltip("Host 세션 생성 시 사용할 Host 식별자 문자열입니다.")]
    [SerializeField] private string _hostClientId = "Host_A"; // Host 세션 생성 요청에서 사용할 로컬 Host 식별자입니다.

    [Tooltip("Client 모드 선택 시 Join 팝업을 자동으로 열지 여부입니다.")]
    [SerializeField] private bool _autoOpenJoinPopupOnClientSelected = true; // Client 선택 직후 Join 팝업 자동 표시 여부를 제어하는 플래그입니다.

    [Tooltip("Join 요청 성공 시 Join 팝업을 자동으로 닫을지 여부입니다.")]
    [SerializeField] private bool _autoCloseJoinPopupOnJoinSucceeded = true; // Join 성공 후 Join 팝업 자동 닫기 수행 여부를 제어하는 플래그입니다.

    [Header("Events")]
    [Tooltip("싱글플레이 시작 요청 성공 시 호출할 UnityEvent입니다.")]
    [SerializeField] private UnityEvent _onSingleStartSucceeded; // 싱글플레이 시작 성공 시 UI 후처리에 사용하는 이벤트입니다.

    [Tooltip("멀티 Host 시작 요청 성공 시 호출할 UnityEvent입니다.")]
    [SerializeField] private UnityEvent _onMultiplayerHostStartSucceeded; // 멀티 Host 시작 성공 시 UI 후처리에 사용하는 이벤트입니다.

    [Tooltip("멀티 Client 참가 요청 성공 시 호출할 UnityEvent입니다.")]
    [SerializeField] private UnityEvent _onMultiplayerClientStartSucceeded; // 멀티 Client 시작 성공 시 UI 후처리에 사용하는 이벤트입니다.

    [Tooltip("요청 실패 시 호출할 UnityEvent입니다.")]
    [SerializeField] private UnityEvent _onStartFailed; // 모드 시작 요청 실패 시 경고 UI 표시에 사용하는 이벤트입니다.

    [Header("Debug")]
    [Tooltip("디버그용: 마지막으로 처리한 플레이 모드 선택값입니다.")]
    [SerializeField] private E_GamePlayMode _lastSelectedMode = E_GamePlayMode.SinglePlayer; // 마지막 선택 모드를 Inspector에서 확인하기 위한 디버그 값입니다.

    [Tooltip("디버그용: 마지막 요청 처리 성공 여부입니다.")]
    [SerializeField] private bool _lastRequestSucceeded; // 마지막 요청의 성공/실패 여부를 Inspector에서 확인하기 위한 디버그 값입니다.

    [Tooltip("디버그용: 마지막으로 SaveCoordinator에 반영한 슬롯 번호입니다.")]
    [SerializeField] private int _lastAppliedSlotIndex = 1; // 최근 Play 요청 직전에 활성화한 저장 슬롯 번호입니다.

    [Tooltip("디버그용: 슬롯 선택 패널의 현재 처리 모드입니다.")]
    [SerializeField] private E_TitleSlotFlowMode _currentSlotFlowMode = E_TitleSlotFlowMode.None; // 슬롯 선택 패널에서 어떤 흐름을 처리 중인지 추적하는 런타임 상태입니다.

    /// <summary>
    /// Inspector 미할당 시 GameFlowController를 자동으로 해석합니다.
    /// </summary>
    private void Awake()
    {
        if (_gameFlowController == null)
        {
            _gameFlowController = GameFlowController.Instance != null
                ? GameFlowController.Instance
                : FindAnyObjectByType<GameFlowController>();
        }

        if (_saveCoordinator == null)
        {
            _saveCoordinator = SaveCoordinator.Instance;
        }

        if (_multiplayerSessionOrchestrator == null)
        {
            ResolveMultiplayerSessionOrchestrator();
        }
    }

    /// <summary>
    /// 타이틀 오브젝트 활성화 시 기본 메뉴 상태를 초기화합니다.
    /// </summary>
    private void OnEnable()
    {
        OpenTopMenu();
    }

    /// <summary>
    /// 타이틀 상단 메뉴 패널을 열고 하위 패널을 닫습니다.
    /// </summary>
    public void OpenTopMenu()
    {
        SetPanelVisible(_panelTopMenu, true);
        SetPanelVisible(_panelSingleMenu, false);
        SetPanelVisible(_panelSlotSelect, false);
        SetPanelVisible(_panelMultiplayMode, false);
        SetPanelVisible(_multiplayerPanelRoot, false);
        _currentSlotFlowMode = E_TitleSlotFlowMode.None;
    }

    /// <summary>
    /// 싱글 메뉴 패널을 엽니다.
    /// </summary>
    public void OpenSingleMenu()
    {
        SetPanelVisible(_panelTopMenu, false);
        SetPanelVisible(_panelSingleMenu, true);
        SetPanelVisible(_panelSlotSelect, false);
        SetPanelVisible(_panelMultiplayMode, false);
        SetPanelVisible(_multiplayerPanelRoot, false);
        _currentSlotFlowMode = E_TitleSlotFlowMode.None;
        RefreshSingleMenuInteractivity();
    }

    /// <summary>
    /// Top 메뉴의 SinglePlay 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickTopSinglePlay()
    {
        OpenSingleMenu();
    }

    /// <summary>
    /// 싱글 NewGame 슬롯 선택 패널을 엽니다.
    /// </summary>
    public void OpenSlotSelectForSingleNewGame()
    {
        OpenSlotSelect(E_TitleSlotFlowMode.SingleNewGame);
    }

    /// <summary>
    /// 싱글 LoadGame 슬롯 선택 패널을 엽니다.
    /// </summary>
    public void OpenSlotSelectForSingleLoadGame()
    {
        OpenSlotSelect(E_TitleSlotFlowMode.SingleLoadGame);
    }

    /// <summary>
    /// 멀티플레이 슬롯 선택 패널을 엽니다.
    /// </summary>
    public void OpenSlotSelectForMultiplay()
    {
        OpenSlotSelect(E_TitleSlotFlowMode.Multiplay);
    }

    /// <summary>
    /// Top 메뉴의 Multiplay 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickTopMultiplay()
    {
        OpenSlotSelectForMultiplay();
    }

    /// <summary>
    /// 슬롯 선택 패널에서 슬롯을 선택한 뒤 현재 모드에 맞는 다음 흐름으로 진행합니다.
    /// </summary>
    public void SelectSlotAndProceed(int slotIndex)
    {
        SelectPlaySlot(slotIndex);

        if (_currentSlotFlowMode == E_TitleSlotFlowMode.SingleNewGame)
        {
            OnClickSingleNewGame();
            return;
        }

        if (_currentSlotFlowMode == E_TitleSlotFlowMode.SingleLoadGame)
        {
            OnClickSingleLoadGame();
            return;
        }

        if (_currentSlotFlowMode == E_TitleSlotFlowMode.Multiplay)
        {
            OpenMultiplayModePanel();
        }
    }

    /// <summary>
    /// 슬롯 1번 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickSlot01()
    {
        SelectSlotAndProceed(1);
    }

    /// <summary>
    /// 슬롯 2번 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickSlot02()
    {
        SelectSlotAndProceed(2);
    }

    /// <summary>
    /// 슬롯 3번 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickSlot03()
    {
        SelectSlotAndProceed(3);
    }

    /// <summary>
    /// 멀티 Host/Client 선택 패널을 엽니다.
    /// </summary>
    public void OpenMultiplayModePanel()
    {
        SetPanelVisible(_panelTopMenu, false);
        SetPanelVisible(_panelSingleMenu, false);
        SetPanelVisible(_panelSlotSelect, false);
        SetPanelVisible(_panelMultiplayMode, true);
        SetPanelVisible(_multiplayerPanelRoot, false);
    }

    /// <summary>
    /// Client Join 팝업을 엽니다.
    /// </summary>
    public void OpenJoinPopup()
    {
        SetPanelVisible(_multiplayerPanelRoot, true);
    }

    /// <summary>
    /// Client Join 팝업을 닫습니다.
    /// </summary>
    public void CloseJoinPopup()
    {
        SetPanelVisible(_multiplayerPanelRoot, false);
    }

    /// <summary>
    /// 옵션 버튼 클릭 시 레거시 Options 패널을 엽니다.
    /// </summary>
    public void OnClickOption()
    {
        OpenLegacyPanel(_legacyOptionsPanelRoot, _legacyLoadGamePanelRoot);
    }

    /// <summary>
    /// Top 메뉴의 Option 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickTopOption()
    {
        OnClickOption();
    }

    /// <summary>
    /// 종료 버튼 클릭 시 GameFlow 종료 요청을 수행합니다.
    /// </summary>
    public void OnClickQuit()
    {
        _lastSelectedMode = E_GamePlayMode.SinglePlayer;
        bool started = _gameFlowController != null && _gameFlowController.RequestExit(false);
        HandleStartResult(started, null);
    }

    /// <summary>
    /// Top 메뉴의 Quit 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickTopQuit()
    {
        OnClickQuit();
    }

    /// <summary>
    /// Single 메뉴의 Back 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickBackFromSingle()
    {
        OpenTopMenu();
    }

    /// <summary>
    /// SlotSelect 메뉴의 Back 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickBackFromSlot()
    {
        if (_currentSlotFlowMode == E_TitleSlotFlowMode.Multiplay)
        {
            OpenTopMenu();
            return;
        }

        OpenSingleMenu();
    }

    /// <summary>
    /// MultiplayMode 메뉴의 Back 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickBackFromMultiplayMode()
    {
        OpenSlotSelectForMultiplay();
    }

    /// <summary>
    /// 레거시 Load Game 패널 열기 버튼에서 호출할 API입니다.
    /// </summary>
    public void OpenLegacyLoadGamePanel()
    {
        OpenLegacyPanel(_legacyLoadGamePanelRoot, _legacyOptionsPanelRoot);
    }

    /// <summary>
    /// 레거시 Options 패널 열기 버튼에서 호출할 API입니다.
    /// </summary>
    public void OpenLegacyOptionsPanel()
    {
        OpenLegacyPanel(_legacyOptionsPanelRoot, _legacyLoadGamePanelRoot);
    }

    /// <summary>
    /// 슬롯 선택 UI에서 전달받은 슬롯 번호를 현재 플레이 대상 슬롯으로 설정합니다.
    /// </summary>
    public void SelectPlaySlot(int slotIndex)
    {
        _selectedSlotIndex = Mathf.Max(1, slotIndex);
    }

    /// <summary>
    /// Continue 버튼 클릭 시 마지막 사용 슬롯을 우선 적용해 Continue 요청을 처리합니다.
    /// </summary>
    public void OnClickSingleContinue()
    {
        bool started = TryStartContinueUsingLastUsedSlot();
        HandleStartResult(started, _onSingleStartSucceeded);
    }

    /// <summary>
    /// New Game 버튼 클릭 시 현재 선택 슬롯을 적용하고 싱글 시작 요청을 처리합니다.
    /// </summary>
    public void OnClickSingleNewGame()
    {
        bool started = TryStartNewGameUsingSelectedSlot();
        HandleStartResult(started, _onSingleStartSucceeded);
    }

    /// <summary>
    /// Load Game 버튼 클릭 시 현재 선택 슬롯을 적용하고 Continue 요청을 처리합니다.
    /// </summary>
    public void OnClickSingleLoadGame()
    {
        bool started = TryStartLoadGameUsingSelectedSlot();
        HandleStartResult(started, _onSingleStartSucceeded);
    }

    /// <summary>
    /// 선택 슬롯 기준 New Game 시작을 시도합니다.
    /// 규칙: 데이터가 있으면 삭제 후 시작, 없으면 그대로 시작합니다.
    /// </summary>
    public bool TryStartNewGameUsingSelectedSlot()
    {
        int safeSlotIndex = Mathf.Max(1, _selectedSlotIndex); // New Game 시작 전 검증/적용할 슬롯 번호입니다.
        if (_gameFlowController == null)
        {
            return false;
        }

        _lastSelectedMode = E_GamePlayMode.SinglePlayer;
        return _gameFlowController.RequestStartSinglePlayerNewGameInSlot(safeSlotIndex, _clearSlotBeforeNewGame);
    }

    /// <summary>
    /// 선택 슬롯 기준 Load Game 시작을 시도합니다.
    /// 규칙: 데이터가 있는 슬롯만 로드 가능하며, 비어 있으면 시작하지 않습니다.
    /// </summary>
    public bool TryStartLoadGameUsingSelectedSlot()
    {
        int safeSlotIndex = Mathf.Max(1, _selectedSlotIndex); // Load Game 시작 전 검증/적용할 슬롯 번호입니다.
        if (_gameFlowController == null)
        {
            return false;
        }

        _lastSelectedMode = E_GamePlayMode.SinglePlayer;
        return _gameFlowController.RequestStartLoadGameInSlot(safeSlotIndex);
    }

    /// <summary>
    /// 마지막 사용 슬롯 기준 Continue 시작을 시도합니다.
    /// 규칙: 마지막 슬롯이 존재하고 데이터가 있을 때만 로드 가능합니다.
    /// </summary>
    public bool TryStartContinueUsingLastUsedSlot()
    {
        if (!_autoSelectLastUsedSlotForContinue)
        {
            return TryStartLoadGameUsingSelectedSlot();
        }

        if (_gameFlowController == null)
        {
            return false;
        }

        _lastSelectedMode = E_GamePlayMode.SinglePlayer;
        return _gameFlowController.RequestContinueFromLastUsedSlot();
    }

    /// <summary>
    /// 멀티플레이 Host 버튼 클릭 시 Host 시작 요청을 처리합니다.
    /// </summary>
    public void OnClickMultiplayerHost()
    {
        ResolveMultiplayerSessionOrchestratorIfNeeded();
        ApplySelectedSlotBeforePlay();
        _lastSelectedMode = E_GamePlayMode.MultiplayerHost;
        bool started = _multiplayerSessionOrchestrator != null
            ? _multiplayerSessionOrchestrator.StartHostSessionFromTitle(_hostClientId)
            : _gameFlowController != null && _gameFlowController.RequestStartMultiplayerHost();
        HandleStartResult(started, _onMultiplayerHostStartSucceeded);
    }

    /// <summary>
    /// 멀티플레이 Client 버튼 클릭 시 Client 참가 요청을 처리합니다.
    /// </summary>
    public void OnClickMultiplayerClient()
    {
        ResolveMultiplayerSessionOrchestratorIfNeeded();
        ApplySelectedSlotBeforePlay();
        _lastSelectedMode = E_GamePlayMode.MultiplayerClient;
        bool started = _multiplayerSessionOrchestrator != null || _gameFlowController != null;
        HandleStartResult(started, _onMultiplayerClientStartSucceeded);

        if (started && _autoOpenJoinPopupOnClientSelected)
        {
            OpenJoinPopup();
        }
    }

    /// <summary>
    /// MultiplayMode의 Host 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickMultiplayModeHost()
    {
        OnClickMultiplayerHost();
    }

    /// <summary>
    /// MultiplayMode의 Client 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickMultiplayModeClient()
    {
        OnClickMultiplayerClient();
    }

    /// <summary>
    /// Join 팝업의 Close 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// </summary>
    public void OnClickJoinPopupClose()
    {
        CloseJoinPopup();
        OpenMultiplayModePanel();
    }

    /// <summary>
    /// Join 팝업의 Join 버튼 OnClick에서 호출하는 래퍼 메서드입니다.
    /// Bootstrap/DDOL 오케스트레이터의 Join 진입점을 실행합니다.
    /// </summary>
    public void OnClickJoinSessionFromTitle()
    {
        ResolveMultiplayerSessionOrchestratorIfNeeded();
        _lastSelectedMode = E_GamePlayMode.MultiplayerClient;

        bool started = _multiplayerSessionOrchestrator != null && _multiplayerSessionOrchestrator.OnClickJoinSessionFromTitleProxy();
        HandleStartResult(started, _onMultiplayerClientStartSucceeded);

        if (started && _autoCloseJoinPopupOnJoinSucceeded)
        {
            CloseJoinPopup();
        }
    }

    /// <summary>
    /// 현재 선택 슬롯을 SaveCoordinator의 활성 슬롯으로 반영합니다.
    /// </summary>
    private void ApplySelectedSlotBeforePlay()
    {
        ApplySlotBeforePlay(Mathf.Max(1, _selectedSlotIndex));
    }

    /// <summary>
    /// 슬롯 선택 패널을 지정한 흐름 모드로 엽니다.
    /// </summary>
    private void OpenSlotSelect(E_TitleSlotFlowMode flowMode)
    {
        _currentSlotFlowMode = flowMode;
        SetPanelVisible(_panelTopMenu, false);
        SetPanelVisible(_panelSingleMenu, false);
        SetPanelVisible(_panelSlotSelect, true);
        SetPanelVisible(_panelMultiplayMode, false);
        SetPanelVisible(_multiplayerPanelRoot, false);
    }

    /// <summary>
    /// 레거시 모달 패널을 열고 다른 패널을 닫은 뒤 백드롭을 노출합니다.
    /// </summary>
    private void OpenLegacyPanel(GameObject panelToOpen, GameObject panelToClose)
    {
        if (panelToOpen == null)
        {
            return;
        }

        SetPanelVisible(panelToClose, false);
        SetPanelVisible(panelToOpen, true);
        SetPanelVisible(_modalBackdrop, true);
    }

    /// <summary>
    /// 대상 패널 오브젝트의 활성 상태를 안전하게 설정합니다.
    /// </summary>
    private void SetPanelVisible(GameObject panel, bool isVisible)
    {
        if (panel != null)
        {
            panel.SetActive(isVisible);
        }
    }

    /// <summary>
    /// 지정 슬롯을 SaveCoordinator 활성 슬롯으로 반영합니다.
    /// </summary>
    private void ApplySlotBeforePlay(int slotIndex)
    {
        if (_saveCoordinator == null)
        {
            return;
        }

        int safeSlotIndex = Mathf.Max(1, slotIndex); // SaveCoordinator에 반영할 보정 슬롯 번호입니다.
        _saveCoordinator.SetActiveSaveSlot(safeSlotIndex, true);
        _lastAppliedSlotIndex = safeSlotIndex;
    }

    /// <summary>
    /// 멀티 UI 액션 시 오케스트레이터 참조가 비어 있으면 자동 재탐색을 수행합니다.
    /// </summary>
    private void ResolveMultiplayerSessionOrchestratorIfNeeded()
    {
        if (_multiplayerSessionOrchestrator != null || !_autoResolveMultiplayerSessionOrchestratorOnUse)
        {
            return;
        }

        ResolveMultiplayerSessionOrchestrator();
    }

    /// <summary>
    /// MultiplayerSessionOrchestrator를 Instance, 활성 씬, DDOL 포함 전체 로드 오브젝트 순서로 탐색합니다.
    /// </summary>
    private void ResolveMultiplayerSessionOrchestrator()
    {
        if (_multiplayerSessionOrchestrator != null)
        {
            return;
        }

        if (MultiplayerSessionOrchestrator.Instance != null)
        {
            _multiplayerSessionOrchestrator = MultiplayerSessionOrchestrator.Instance;
            return;
        }

        MultiplayerSessionOrchestrator[] activeCandidates = FindObjectsByType<MultiplayerSessionOrchestrator>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 활성 씬에서 우선 탐색한 오케스트레이터 후보 목록입니다.
        if (activeCandidates != null && activeCandidates.Length > 0)
        {
            _multiplayerSessionOrchestrator = activeCandidates[0];
            return;
        }

        MultiplayerSessionOrchestrator[] allLoadedCandidates = Resources.FindObjectsOfTypeAll<MultiplayerSessionOrchestrator>(); // DDOL 씬을 포함해 전체 로드 오브젝트에서 탐색한 오케스트레이터 후보 목록입니다.
        if (allLoadedCandidates == null || allLoadedCandidates.Length == 0)
        {
            return;
        }

        for (int i = 0; i < allLoadedCandidates.Length; i++)
        {
            MultiplayerSessionOrchestrator candidate = allLoadedCandidates[i]; // hideFlags를 검사해 유효한 오케스트레이터를 선택할 후보입니다.
            if (candidate == null)
            {
                continue;
            }

            if ((candidate.hideFlags & HideFlags.HideAndDontSave) != 0)
            {
                continue;
            }

            _multiplayerSessionOrchestrator = candidate;
            return;
        }
    }

    /// <summary>
    /// 시작 요청 결과를 공통 후처리하고 성공/실패 이벤트를 발행합니다.
    /// </summary>
    private void HandleStartResult(bool started, UnityEvent successEvent)
    {
        _lastRequestSucceeded = started;

        if (started)
        {
            successEvent?.Invoke();
            return;
        }

        _onStartFailed?.Invoke();
    }

    /// <summary>
    /// Continue/Load 버튼 활성 상태를 저장 데이터 존재 여부에 맞게 갱신합니다.
    /// </summary>
    private void RefreshSingleMenuInteractivity()
    {
        bool hasAnyProgress = HasAnyProgressData(); // Continue/Load 활성화에 사용할 데이터 존재 여부입니다.

        if (_singleContinueButton != null)
        {
            _singleContinueButton.interactable = hasAnyProgress;
        }

        if (_singleLoadGameButton != null)
        {
            _singleLoadGameButton.interactable = hasAnyProgress;
        }
    }

    /// <summary>
    /// 저장 슬롯 전체를 순회해 진행 데이터 존재 여부를 확인합니다.
    /// </summary>
    private bool HasAnyProgressData()
    {
        if (_saveCoordinator == null)
        {
            return false;
        }

        for (int slotIndex = 1; slotIndex <= 3; slotIndex++)
        {
            if (_saveCoordinator.TryGetSlotProgressSummary(slotIndex, out SaveSlotProgressSummary summary) && summary.HasUsedData)
            {
                return true;
            }
        }

        return false;
    }
}
