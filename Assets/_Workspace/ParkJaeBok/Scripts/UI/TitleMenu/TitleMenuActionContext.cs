/// <summary>
/// 타이틀 메뉴 액션 실행에 필요한 런타임 참조와 상태를 담는 문맥 객체입니다.
/// </summary>
public class TitleMenuActionContext
{
    public SceneTransitionService SceneTransitionService { get; } // 타이틀 액션에서 씬 전환 상태를 조회할 때 사용하는 서비스 참조입니다.
    public ITitleSaveQueryService SaveQueryService { get; } // 저장 기능 제거 후에도 버튼 상태를 일관되게 조회하기 위한 서비스 참조입니다.
    public ITitleDialogService DialogService { get; } // 확인 팝업을 표시하는 서비스 참조입니다.
    public StageCatalog StageCatalog { get; } // stageId 해석에 사용하는 스테이지 카탈로그 참조입니다.
    public GameFlowController GameFlowController { get; } // 상위 게임 흐름 명령을 전달할 컨트롤러 참조입니다.
    public string NewGameSceneName { get; } // New Game 시작 시 로드할 씬 이름입니다.
    public GameFlowState NewGameLoadedState { get; } // New Game 씬 로드 완료 후 전환할 상태입니다.

    /// <summary>
    /// 전달받은 서비스와 설정값으로 액션 문맥을 초기화합니다.
    /// </summary>
    public TitleMenuActionContext(
        SceneTransitionService sceneTransitionService,
        ITitleSaveQueryService saveQueryService,
        ITitleDialogService dialogService,
        StageCatalog stageCatalog,
        GameFlowController gameFlowController,
        string newGameSceneName,
        GameFlowState newGameLoadedState)
    {
        SceneTransitionService = sceneTransitionService;
        SaveQueryService = saveQueryService;
        DialogService = dialogService;
        StageCatalog = stageCatalog;
        GameFlowController = gameFlowController;
        NewGameSceneName = newGameSceneName;
        NewGameLoadedState = newGameLoadedState;
    }

    /// <summary>
    /// 새 게임 시작 씬 이름이 유효한지 검사합니다.
    /// </summary>
    public bool HasValidNewGameScene()
    {
        return string.IsNullOrWhiteSpace(NewGameSceneName) == false;
    }
}
