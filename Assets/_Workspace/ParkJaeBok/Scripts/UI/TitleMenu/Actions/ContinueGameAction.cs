using UnityEngine;

/// <summary>
/// Continue 요청 시 저장 데이터 복원 후 마지막 진행 지점으로 진입하는 액션입니다.
/// </summary>
public class ContinueGameAction : MonoBehaviour, ITitleMenuAction
{
    /// <summary>
    /// 현재 문맥에서 Continue 실행 가능 여부를 반환합니다.
    /// </summary>
    public bool CanExecute(TitleMenuActionContext context)
    {
        return context != null && context.SceneTransitionService != null && context.SaveCoordinator != null;
    }

    /// <summary>
    /// 저장 채널 복원 후 마지막 씬 진입을 시도합니다.
    /// </summary>
    public bool Execute(TitleMenuActionContext context)
    {
        if (CanExecute(context) == false)
        {
            Debug.LogWarning("[ContinueGameAction] 필수 의존성이 없어 Continue를 실행할 수 없습니다.", this);
            return false;
        }

        bool loadedPersistent = context.SaveCoordinator.LoadChannel(E_SaveChannelType.Persistent, E_SaveTriggerType.Manual, "Title.Continue.Persistent");
        bool loadedSession = context.SaveCoordinator.LoadChannel(E_SaveChannelType.Session, E_SaveTriggerType.Manual, "Title.Continue.Session");
        if (loadedPersistent == false && loadedSession == false)
        {
            Debug.LogWarning("[ContinueGameAction] 복원 가능한 저장 데이터가 없어 Continue를 취소합니다.", this);
            return false;
        }

        if (TryResolveContinueSceneName(context, out string sceneName) == false)
        {
            Debug.LogWarning("[ContinueGameAction] 마지막 진입 씬을 해석하지 못했습니다. Load Game으로 폴백하세요.", this);
            return false;
        }

        bool started = context.SceneTransitionService.TryLoadScene(sceneName);
        if (started == false)
        {
            Debug.LogWarning($"[ContinueGameAction] Continue 씬 전환 시작 실패 scene={sceneName}", this);
        }

        return started;
    }

    /// <summary>
    /// StageSession/StageCatalog를 이용해 Continue 대상 씬 이름을 계산합니다.
    /// </summary>
    private bool TryResolveContinueSceneName(TitleMenuActionContext context, out string sceneName)
    {
        sceneName = string.Empty;

        StageSession session = StageSession.Instance; // Continue 대상 씬 계산에 사용할 세션 인스턴스입니다.
        if (session.UseCheckpointForNextSpawn && string.IsNullOrWhiteSpace(session.LastCheckpointSceneName) == false)
        {
            sceneName = session.LastCheckpointSceneName;
            return true;
        }

        if (string.IsNullOrWhiteSpace(session.SelectedStageId))
        {
            return false;
        }

        if (context.StageCatalog == null)
        {
            Debug.LogWarning("[ContinueGameAction] StageCatalog가 없어 stageId->sceneName 해석에 실패했습니다.", this);
            return false;
        }

        if (context.StageCatalog.TryGetById(session.SelectedStageId, out StageDefinition stageDefinition) == false || stageDefinition == null)
        {
            Debug.LogWarning($"[ContinueGameAction] 카탈로그에서 stageId를 찾지 못했습니다. stageId={session.SelectedStageId}", this);
            return false;
        }

        sceneName = stageDefinition.SceneName;
        return string.IsNullOrWhiteSpace(sceneName) == false;
    }
}
