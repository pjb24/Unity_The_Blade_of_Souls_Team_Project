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
        return context != null;
    }

    /// <summary>
    /// 저장 채널 복원 후 마지막 씬 진입을 시도합니다.
    /// </summary>
    public bool Execute(TitleMenuActionContext context)
    {
        if (CanExecute(context) == false)
        {
            Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyActionDependencyMissing, "[ContinueGameAction] 필수 의존성이 없어 Continue를 실행할 수 없습니다."), this);
            return false;
        }

        SaveCoordinator saveCoordinator = context.SaveCoordinator; // Continue 전에 마지막 사용 슬롯 적용에 사용할 SaveCoordinator 참조입니다.
        ITitleSaveQueryService saveQueryService = context.SaveQueryService; // 마지막 사용 슬롯 조회에 사용할 타이틀 세이브 쿼리 서비스 참조입니다.
        if (saveCoordinator != null && saveQueryService != null && saveQueryService.TryGetLastUsedSlotIndex(out int lastUsedSlotIndex))
        {
            saveCoordinator.SetActiveSaveSlot(lastUsedSlotIndex, false);
        }

        GameFlowController gameFlowController = context.GameFlowController; // Continue 명령 라우팅에 사용할 GameFlowController 참조입니다.
        if (gameFlowController != null)
        {
            bool continued = gameFlowController.RequestContinue();
            if (!continued)
            {
                Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyActionFlowRequestFailed, "[ContinueGameAction] GameFlowController 기반 Continue 실행에 실패했습니다."), this);
            }

            return continued;
        }

        EmitGameFlowRequiredTelemetry("GameFlowControllerMissing");
        Debug.LogError("[ContinueGameAction] GameFlowController가 필수라 Continue 요청을 중단합니다.", this);
        return false;
    }

    /// <summary>
    /// GameFlow 필수 경로 위반 시 강한 경고와 텔레메트리 로그를 출력합니다.
    /// </summary>
    private void EmitGameFlowRequiredTelemetry(string reason)
    {
        Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyActionGameFlowRequired, $"[ContinueGameAction][GAMEFLOW_REQUIRED] reason={reason}"), this);
        Debug.Log($"[Telemetry][GameFlowRequired] feature=ContinueGameAction, reason={reason}", this);
    }
}
