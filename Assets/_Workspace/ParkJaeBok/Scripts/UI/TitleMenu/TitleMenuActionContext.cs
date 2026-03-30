/// <summary>
/// 타이틀 메뉴 액션 실행 시 공유할 의존성과 상태를 담는 문맥 객체입니다.
/// </summary>
public class TitleMenuActionContext
{
    public SceneTransitionService SceneTransitionService { get; } // 타이틀 액션이 씬 전환 상태를 조회할 때 사용하는 서비스 참조입니다.
    public SaveCoordinator SaveCoordinator { get; } // Continue 복원에 사용하는 저장 코디네이터 참조입니다.
    public ITitleSaveQueryService SaveQueryService { get; } // 타이틀 세이브 존재 여부 조회 서비스 참조입니다.
    public ITitleDialogService DialogService { get; } // 확인 팝업(덮어쓰기/종료) 서비스 참조입니다.
    public StageCatalog StageCatalog { get; } // stageId 해석에 사용하는 스테이지 카탈로그 참조입니다.
    public GameFlowController GameFlowController { get; } // 상위 루프 명령을 전달할 GameFlowController 참조입니다.
    public string NewGameSceneName { get; } // New Game 시작 시 전달할 씬 이름입니다.
    public GameFlowState NewGameLoadedState { get; } // New Game 씬 로드 완료 후 도달할 목표 GameFlow 상태입니다.

    /// <summary>
    /// 전달받은 서비스/설정을 기반으로 액션 문맥을 초기화합니다.
    /// </summary>
    public TitleMenuActionContext(
        SceneTransitionService sceneTransitionService,
        SaveCoordinator saveCoordinator,
        ITitleSaveQueryService saveQueryService,
        ITitleDialogService dialogService,
        StageCatalog stageCatalog,
        GameFlowController gameFlowController,
        string newGameSceneName,
        GameFlowState newGameLoadedState)
    {
        SceneTransitionService = sceneTransitionService;
        SaveCoordinator = saveCoordinator;
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
