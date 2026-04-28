/// <summary>
/// GameFlow 계층에서 공유하는 런타임 서비스 참조 컨테이너입니다.
/// </summary>
public sealed class GameFlowContext
{
    public SceneTransitionService SceneTransitionService { get; private set; } // 씬 전환 요청과 상태 조회에 사용하는 서비스 참조입니다.
    public StageSession StageSession { get; private set; } // 스테이지 진입과 복귀 문맥을 관리하는 런타임 참조입니다.
    public StageProgressRuntime StageProgressRuntime { get; private set; } // 스테이지 진행도 런타임 참조입니다.
    public StageCatalog StageCatalog { get; private set; } // stageId와 sceneName 해석에 사용하는 카탈로그 참조입니다.

    /// <summary>
    /// 전달된 런타임 참조로 GameFlow 컨텍스트를 생성합니다.
    /// </summary>
    public GameFlowContext(
        SceneTransitionService sceneTransitionService,
        StageSession stageSession,
        StageProgressRuntime stageProgressRuntime,
        StageCatalog stageCatalog)
    {
        SceneTransitionService = sceneTransitionService;
        StageSession = stageSession;
        StageProgressRuntime = stageProgressRuntime;
        StageCatalog = stageCatalog;
    }

    /// <summary>
    /// 비어 있는 런타임 참조를 현재 씬의 싱글톤 인스턴스로 보정합니다.
    /// </summary>
    public void ResolveMissingReferencesIfNeeded()
    {
        if (SceneTransitionService == null)
        {
            SceneTransitionService = SceneTransitionService.Instance;
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
