using UnityEngine;

/// <summary>
/// Quit 요청 시 확인 절차 후 애플리케이션 종료를 시도하는 액션입니다.
/// </summary>
public class RequestQuitAction : MonoBehaviour, ITitleMenuAction
{
    /// <summary>
    /// 현재 문맥에서 Quit 실행 가능 여부를 반환합니다.
    /// </summary>
    public bool CanExecute(TitleMenuActionContext context)
    {
        return context != null;
    }

    /// <summary>
    /// Quit 확인 후 플랫폼 정책에 맞게 종료를 수행합니다.
    /// </summary>
    public bool Execute(TitleMenuActionContext context)
    {
        if (context == null)
        {
            Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyActionDependencyMissing, "[RequestQuitAction] context가 null이라 Quit을 수행할 수 없습니다."), this);
            return false;
        }

        if (context.DialogService != null)
        {
            bool confirmed = context.DialogService.ConfirmQuit(); // Quit 요청 확인 결과입니다.
            if (confirmed == false)
            {
                return false;
            }
        }

        if (context.GameFlowController != null)
        {
            bool exited = context.GameFlowController.RequestExit(false);
            if (!exited)
            {
                Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyActionFlowRequestFailed, "[RequestQuitAction] GameFlowController 기반 Quit 요청에 실패했습니다."), this);
            }

            return exited;
        }

        EmitGameFlowRequiredTelemetry("GameFlowControllerMissing");
        Debug.LogError("[RequestQuitAction] GameFlowController가 필수라 Quit 요청을 중단합니다.", this);
        return false;
    }

    /// <summary>
    /// GameFlow 필수 경로 위반 시 강한 경고와 텔레메트리 로그를 출력합니다.
    /// </summary>
    private void EmitGameFlowRequiredTelemetry(string reason)
    {
        Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyActionGameFlowRequired, $"[RequestQuitAction][GAMEFLOW_REQUIRED] reason={reason}"), this);
        Debug.Log($"[Telemetry][GameFlowRequired] feature=RequestQuitAction, reason={reason}", this);
    }
}
