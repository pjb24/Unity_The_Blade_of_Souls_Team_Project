/// <summary>
/// 타이틀 메뉴 액션 실행 시 공유할 의존성과 상태를 담는 문맥 객체입니다.
/// </summary>
public class TitleMenuActionContext
{
    public SceneTransitionService SceneTransitionService { get; }
    public SaveCoordinator SaveCoordinator { get; }
    public ITitleSaveQueryService SaveQueryService { get; }
    public ITitleDialogService DialogService { get; }
    public StageCatalog StageCatalog { get; }
    public string NewGameSceneName { get; }

    /// <summary>
    /// 전달받은 서비스/설정을 기반으로 액션 문맥을 초기화합니다.
    /// </summary>
    public TitleMenuActionContext(
        SceneTransitionService sceneTransitionService,
        SaveCoordinator saveCoordinator,
        ITitleSaveQueryService saveQueryService,
        ITitleDialogService dialogService,
        StageCatalog stageCatalog,
        string newGameSceneName)
    {
        SceneTransitionService = sceneTransitionService;
        SaveCoordinator = saveCoordinator;
        SaveQueryService = saveQueryService;
        DialogService = dialogService;
        StageCatalog = stageCatalog;
        NewGameSceneName = newGameSceneName;
    }

    /// <summary>
    /// 새 게임 시작 씬 이름이 유효한지 검사합니다.
    /// </summary>
    public bool HasValidNewGameScene()
    {
        return string.IsNullOrWhiteSpace(NewGameSceneName) == false;
    }
}
