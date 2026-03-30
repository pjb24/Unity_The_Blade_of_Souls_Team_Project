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
        return context != null && context.HasValidNewGameScene();
    }

    /// <summary>
    /// 기존 진행 데이터 확인 후 새 게임 시작을 실행합니다.
    /// </summary>
    public bool Execute(TitleMenuActionContext context)
    {
        if (CanExecute(context) == false)
        {
            Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyActionDependencyMissing, "[StartNewGameAction] 필수 의존성이 없어 New Game을 실행할 수 없습니다."), this);
            return false;
        }

        bool hasExistingProgress = context.SaveQueryService != null && context.SaveQueryService.HasExistingProgress(); // 덮어쓰기 확인이 필요한 기존 진행 데이터 존재 여부입니다.
        if (hasExistingProgress)
        {
            if (context.DialogService == null)
            {
                Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyActionDependencyMissing, "[StartNewGameAction] 덮어쓰기 확인 서비스가 없어 New Game을 취소합니다."), this);
                return false;
            }

            bool confirmed = context.DialogService.ConfirmStartNewGameWithOverwrite(); // 기존 진행 덮어쓰기 확인 결과입니다.
            if (confirmed == false)
            {
                return false;
            }
        }

        GameFlowController gameFlowController = context.GameFlowController; // New Game 명령 라우팅에 사용할 GameFlowController 참조입니다.
        if (gameFlowController != null)
        {
            bool startedFromFlow = gameFlowController.RequestStartNewGame(context.NewGameSceneName, context.NewGameLoadedState);
            if (!startedFromFlow)
            {
                Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyActionFlowRequestFailed, $"[StartNewGameAction] GameFlowController 기반 New Game 시작에 실패했습니다. scene={context.NewGameSceneName}"), this);
            }

            return startedFromFlow;
        }

        EmitGameFlowRequiredTelemetry("GameFlowControllerMissing", context.NewGameSceneName);
        Debug.LogError("[StartNewGameAction] GameFlowController가 필수라 New Game 요청을 중단합니다.", this);
        return false;
    }

    /// <summary>
    /// GameFlow 필수 경로 위반 시 강한 경고와 텔레메트리 로그를 출력합니다.
    /// </summary>
    private void EmitGameFlowRequiredTelemetry(string reason, string sceneName)
    {
        Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyActionGameFlowRequired, $"[StartNewGameAction][GAMEFLOW_REQUIRED] reason={reason}, scene={sceneName}"), this);
        Debug.Log($"[Telemetry][GameFlowRequired] feature=StartNewGameAction, reason={reason}, scene={sceneName}", this);
    }
}
