/// <summary>
/// GameFlow 계층이 공유해서 사용하는 런타임 서비스 참조 컨테이너입니다.
/// </summary>
public sealed class GameFlowContext
{
    public SceneTransitionService SceneTransitionService { get; private set; } // 씬 전환 요청/상태 조회에 사용하는 서비스 참조입니다.
    public SaveCoordinator SaveCoordinator { get; private set; } // 세이브 채널 저장/복원에 사용하는 코디네이터 참조입니다.
    public StageSession StageSession { get; private set; } // 씬 간 스테이지 세션 문맥을 관리하는 참조입니다.
    public StageProgressRuntime StageProgressRuntime { get; private set; } // 스테이지 진행도 런타임 참조입니다.
    public StageCatalog StageCatalog { get; private set; } // stageId/sceneName 해석에 사용하는 카탈로그 참조입니다.

    /// <summary>
    /// 전달된 참조로 GameFlow 컨텍스트를 생성합니다.
    /// </summary>
    public GameFlowContext(
        SceneTransitionService sceneTransitionService,
        SaveCoordinator saveCoordinator,
        StageSession stageSession,
        StageProgressRuntime stageProgressRuntime,
        StageCatalog stageCatalog)
    {
        SceneTransitionService = sceneTransitionService;
        SaveCoordinator = saveCoordinator;
        StageSession = stageSession;
        StageProgressRuntime = stageProgressRuntime;
        StageCatalog = stageCatalog;
    }

    /// <summary>
    /// 일부 참조가 비어 있을 때 런타임 해석된 인스턴스로 보정합니다.
    /// </summary>
    public void ResolveMissingReferencesIfNeeded()
    {
        if (SceneTransitionService == null)
        {
            SceneTransitionService = SceneTransitionService.Instance;
        }

        if (SaveCoordinator == null)
        {
            SaveCoordinator = SaveCoordinator.Instance;
        }

        if (StageSession == null)
        {
            StageSession = StageSession.Instance;
        }

        if (StageProgressRuntime == null)
        {
            StageProgressRuntime = StageProgressRuntime.Instance;
        }
    }
}
