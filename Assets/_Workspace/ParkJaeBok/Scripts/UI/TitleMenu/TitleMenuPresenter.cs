using UnityEngine;

/// <summary>
/// 타이틀 메뉴 View 이벤트를 받아 상태 갱신과 액션 실행을 조율하는 Presenter입니다.
/// </summary>
public class TitleMenuPresenter : MonoBehaviour
{
    [Header("View")]
    [Tooltip("타이틀 메뉴 UI View 컴포넌트입니다.")]
    [SerializeField] private TitleMenuView _view; // 버튼 이벤트 수신과 상태 표시를 담당하는 View 참조입니다.

    [Header("Dependencies")]
    [Tooltip("씬 전환 상태 조회에 사용할 SceneTransitionService 참조입니다. 비어 있으면 SceneTransitionService.Instance를 사용합니다.")]
    [SerializeField] private SceneTransitionService _sceneTransitionService; // 씬 전환 중복 입력 가드에 사용할 서비스 참조입니다.

    [Tooltip("상위 게임 흐름 명령을 전달할 GameFlowController 참조입니다. 비어 있으면 GameFlowController.Instance를 사용합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // 타이틀 액션을 GameFlow 명령 API로 라우팅할 컨트롤러 참조입니다.

    [Tooltip("타이틀 메뉴 세이브 조회 서비스입니다.")]
    [SerializeField] private MonoBehaviour _saveQueryComponent; // ITitleSaveQueryService를 구현한 컴포넌트 참조입니다.

    [Tooltip("타이틀 메뉴 확인 대화상자 서비스입니다.")]
    [SerializeField] private MonoBehaviour _dialogComponent; // ITitleDialogService를 구현한 컴포넌트 참조입니다.

    [Tooltip("Continue 시 stageId를 sceneName으로 해석할 StageCatalog 에셋입니다.")]
    [SerializeField] private StageCatalog _stageCatalog; // Continue 복원 시 씬 해석에 사용할 카탈로그 참조입니다.

    [Header("New Game")]
    [Tooltip("New Game 시작 시 진입할 씬 이름입니다.")]
    [SerializeField] private string _newGameSceneName; // New Game 액션에서 전달할 시작 씬 이름입니다.

    [Tooltip("New Game 씬 로드 완료 후 도달할 GameFlow 상태입니다.")]
    [SerializeField] private GameFlowState _newGameLoadedState = GameFlowState.Town; // New Game 로드 완료 후 상태 머신 목표 상태입니다.

    [Header("Actions")]
    [Tooltip("New Game 버튼에 연결할 액션 컴포넌트입니다.")]
    [SerializeField] private MonoBehaviour _newGameActionComponent; // ITitleMenuAction(New Game) 구현 컴포넌트 참조입니다.

    [Tooltip("Continue 버튼에 연결할 액션 컴포넌트입니다.")]
    [SerializeField] private MonoBehaviour _continueActionComponent; // ITitleMenuAction(Continue) 구현 컴포넌트 참조입니다.

    [Tooltip("Load Game 버튼에 연결할 액션 컴포넌트입니다.")]
    [SerializeField] private MonoBehaviour _loadGameActionComponent; // ITitleMenuAction(Load Game) 구현 컴포넌트 참조입니다.

    [Tooltip("Option 버튼에 연결할 액션 컴포넌트입니다.")]
    [SerializeField] private MonoBehaviour _optionActionComponent; // ITitleMenuAction(Option) 구현 컴포넌트 참조입니다.

    [Tooltip("Quit 버튼에 연결할 액션 컴포넌트입니다.")]
    [SerializeField] private MonoBehaviour _quitActionComponent; // ITitleMenuAction(Quit) 구현 컴포넌트 참조입니다.

    private ITitleSaveQueryService _saveQueryService; // 세이브 존재 여부 조회에 사용할 런타임 서비스 참조입니다.
    private ITitleDialogService _dialogService; // 확인 대화상자 요청에 사용할 런타임 서비스 참조입니다.

    private ITitleMenuAction _newGameAction; // New Game 버튼 실행용 액션 참조입니다.
    private ITitleMenuAction _continueAction; // Continue 버튼 실행용 액션 참조입니다.
    private ITitleMenuAction _loadGameAction; // Load Game 버튼 실행용 액션 참조입니다.
    private ITitleMenuAction _optionAction; // Option 버튼 실행용 액션 참조입니다.
    private ITitleMenuAction _quitAction; // Quit 버튼 실행용 액션 참조입니다.

    private bool _isBusy; // 중복 입력 방지를 위한 Presenter 실행 잠금 상태입니다.

    /// <summary>
    /// 컴포넌트 참조를 인터페이스로 해석하고 의존성 유효성을 검증합니다.
    /// </summary>
    private void Awake()
    {
        _saveQueryService = _saveQueryComponent as ITitleSaveQueryService;
        _dialogService = _dialogComponent as ITitleDialogService;

        _newGameAction = _newGameActionComponent as ITitleMenuAction;
        _continueAction = _continueActionComponent as ITitleMenuAction;
        _loadGameAction = _loadGameActionComponent as ITitleMenuAction;
        _optionAction = _optionActionComponent as ITitleMenuAction;
        _quitAction = _quitActionComponent as ITitleMenuAction;

        if (_view == null)
        {
            Debug.LogWarning("[TitleMenuPresenter] TitleMenuView가 비어 있습니다.", this);
        }

        if (_saveQueryComponent != null && _saveQueryService == null)
        {
            Debug.LogWarning("[TitleMenuPresenter] saveQueryComponent가 ITitleSaveQueryService를 구현하지 않았습니다.", this);
        }

        if (_dialogComponent != null && _dialogService == null)
        {
            Debug.LogWarning("[TitleMenuPresenter] dialogComponent가 ITitleDialogService를 구현하지 않았습니다.", this);
        }
    }

    /// <summary>
    /// View 이벤트를 구독하고 메뉴 상태를 초기화합니다.
    /// </summary>
    private void OnEnable()
    {
        BindViewEvents(true);
        RefreshMenuState();
        _view?.SetDefaultSelection();
    }

    /// <summary>
    /// View 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        BindViewEvents(false);
    }

    /// <summary>
    /// 외부 UI(옵션/로드 패널) 종료 후 메뉴 상태를 재동기화합니다.
    /// </summary>
    public void NotifyModalClosed()
    {
        RefreshMenuState();
        _view?.SetDefaultSelection();
    }

    /// <summary>
    /// Continue/Load 활성 상태를 세이브 존재 여부 기반으로 갱신합니다.
    /// </summary>
    public void RefreshMenuState()
    {
        bool canContinue = _saveQueryService != null && _saveQueryService.HasContinueData(); // Continue 버튼 활성화 여부입니다.
        bool canLoad = _saveQueryService != null && _saveQueryService.HasLoadableData(); // Load Game 버튼 활성화 여부입니다.

        _view?.SetAvailability(canContinue, canLoad);
    }

    /// <summary>
    /// New Game 버튼 요청을 처리합니다.
    /// </summary>
    private void HandleNewGameRequested()
    {
        ExecuteAction("NewGame", _newGameAction);
    }

    /// <summary>
    /// Continue 버튼 요청을 처리합니다.
    /// </summary>
    private void HandleContinueRequested()
    {
        ExecuteAction("Continue", _continueAction);
    }

    /// <summary>
    /// Load Game 버튼 요청을 처리합니다.
    /// </summary>
    private void HandleLoadGameRequested()
    {
        ExecuteAction("LoadGame", _loadGameAction);
    }

    /// <summary>
    /// Option 버튼 요청을 처리합니다.
    /// </summary>
    private void HandleOptionRequested()
    {
        ExecuteAction("Option", _optionAction);
    }

    /// <summary>
    /// Quit 버튼 요청을 처리합니다.
    /// </summary>
    private void HandleQuitRequested()
    {
        ExecuteAction("Quit", _quitAction);
    }

    /// <summary>
    /// 공통 입력 가드와 문맥 생성 후 액션 실행을 조율합니다.
    /// </summary>
    private void ExecuteAction(string actionName, ITitleMenuAction action)
    {
        if (_isBusy)
        {
            Debug.LogWarning($"[TitleMenuPresenter] Busy 상태라 입력을 무시합니다. action={actionName}", this);
            return;
        }

        SceneTransitionService transitionService = ResolveSceneTransitionService(); // 현재 액션 실행에 사용할 씬 전환 서비스 인스턴스입니다.
        if (transitionService != null && transitionService.IsTransitioning())
        {
            Debug.LogWarning($"[TitleMenuPresenter] 씬 전환 중이라 입력을 무시합니다. action={actionName}", this);
            return;
        }

        if (action == null)
        {
            Debug.LogWarning($"[TitleMenuPresenter] 액션이 비어 있습니다. action={actionName}", this);
            return;
        }

        _isBusy = true;
        _view?.SetLoadingBlockerVisible(true);

        TitleMenuActionContext context = BuildContext(transitionService); // 액션 실행에 전달할 현재 문맥 객체입니다.
        bool executed = false;
        if (action.CanExecute(context))
        {
            executed = action.Execute(context);
        }
        else
        {
            Debug.LogWarning($"[TitleMenuPresenter] 액션 실행 조건을 만족하지 못했습니다. action={actionName}", this);
        }

        bool isTransitionRunning = transitionService != null && transitionService.IsTransitioning(); // 액션 실행 직후 씬 전환이 진행 중인지 여부입니다.
        if (executed == false || isTransitionRunning == false)
        {
            _view?.SetLoadingBlockerVisible(false);
        }

        _isBusy = false;
    }

    /// <summary>
    /// 직렬화 참조 또는 싱글톤에서 SceneTransitionService를 해석합니다.
    /// </summary>
    private SceneTransitionService ResolveSceneTransitionService()
    {
        if (_sceneTransitionService != null)
        {
            return _sceneTransitionService;
        }

        return SceneTransitionService.Instance;
    }

    /// <summary>
    /// 직렬화 참조 또는 싱글톤에서 GameFlowController를 해석합니다.
    /// </summary>
    private GameFlowController ResolveGameFlowController()
    {
        if (_gameFlowController != null)
        {
            return _gameFlowController;
        }

        return GameFlowController.Instance;
    }

    /// <summary>
    /// 현재 Presenter 상태를 기반으로 액션 문맥 객체를 생성합니다.
    /// </summary>
    private TitleMenuActionContext BuildContext(SceneTransitionService transitionService)
    {
        return new TitleMenuActionContext(
            transitionService,
            _saveQueryService,
            _dialogService,
            _stageCatalog,
            ResolveGameFlowController(),
            _newGameSceneName,
            _newGameLoadedState);
    }

    /// <summary>
    /// View 이벤트 구독/해제를 일괄 처리합니다.
    /// </summary>
    private void BindViewEvents(bool shouldBind)
    {
        if (_view == null)
        {
            return;
        }

        if (shouldBind)
        {
            _view.OnNewGameRequested += HandleNewGameRequested;
            _view.OnContinueRequested += HandleContinueRequested;
            _view.OnLoadGameRequested += HandleLoadGameRequested;
            _view.OnOptionRequested += HandleOptionRequested;
            _view.OnQuitRequested += HandleQuitRequested;
            return;
        }

        _view.OnNewGameRequested -= HandleNewGameRequested;
        _view.OnContinueRequested -= HandleContinueRequested;
        _view.OnLoadGameRequested -= HandleLoadGameRequested;
        _view.OnOptionRequested -= HandleOptionRequested;
        _view.OnQuitRequested -= HandleQuitRequested;
    }
}
