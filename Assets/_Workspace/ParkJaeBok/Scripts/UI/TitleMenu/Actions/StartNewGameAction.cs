using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// New Game 요청 시 세션 초기화와 시작 씬 전환을 처리하는 액션입니다.
/// </summary>
public class StartNewGameAction : MonoBehaviour, ITitleMenuAction
{
    /// <summary>
    /// 현재 문맥에서 New Game 실행 가능 여부를 반환합니다.
    /// </summary>
    public bool CanExecute(TitleMenuActionContext context)
    {
        return context != null && context.SceneTransitionService != null && context.HasValidNewGameScene();
    }

    /// <summary>
    /// 기존 진행 데이터 확인 후 새 게임 시작을 실행합니다.
    /// </summary>
    public bool Execute(TitleMenuActionContext context)
    {
        if (CanExecute(context) == false)
        {
            Debug.LogWarning("[StartNewGameAction] 필수 의존성이 없어 New Game을 실행할 수 없습니다.", this);
            return false;
        }

        bool hasExistingProgress = context.SaveQueryService != null && context.SaveQueryService.HasExistingProgress(); // 덮어쓰기 확인이 필요한 기존 진행 데이터 존재 여부입니다.
        if (hasExistingProgress)
        {
            if (context.DialogService == null)
            {
                Debug.LogWarning("[StartNewGameAction] 덮어쓰기 확인 서비스가 없어 New Game을 취소합니다.", this);
                return false;
            }

            bool confirmed = context.DialogService.ConfirmStartNewGameWithOverwrite(); // 기존 진행 덮어쓰기 확인 결과입니다.
            if (confirmed == false)
            {
                return false;
            }
        }

        ResetRuntimeProgress();

        bool started = context.SceneTransitionService.TryLoadScene(context.NewGameSceneName);
        if (started == false)
        {
            Debug.LogWarning($"[StartNewGameAction] New Game 씬 전환 시작 실패 scene={context.NewGameSceneName}", this);
        }

        return started;
    }

    /// <summary>
    /// 새 게임 시작 전 런타임 진행 데이터를 초기화합니다.
    /// </summary>
    private void ResetRuntimeProgress()
    {
        StageSession.Instance.ApplySnapshot(new StageSession.SnapshotData());
        StageProgressRuntime.Instance.ApplySnapshot(new StageProgressRuntime.SnapshotData
        {
            Records = new List<StageProgressRecord>()
        });
    }
}
