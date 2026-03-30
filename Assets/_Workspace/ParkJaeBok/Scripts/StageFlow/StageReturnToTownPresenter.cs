using UnityEngine;

/// <summary>
/// 현재 스테이지에서 Town 스테이지로 복귀를 요청하는 프리젠터입니다.
/// </summary>
public class StageReturnToTownPresenter : MonoBehaviour
{
    [Tooltip("복귀 처리 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLog = true; // Town 복귀 요청 처리 로그 출력 여부입니다.

    [Tooltip("Town 복귀 명령을 전달할 GameFlowController 참조입니다. 비어 있으면 GameFlowController.Instance를 사용합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // Town 복귀 요청을 GameFlow 명령으로 위임할 컨트롤러 참조입니다.

    /// <summary>
    /// Town 스테이지 복귀를 시도합니다.
    /// </summary>
    public void ReturnToTown()
    {
        GameFlowController gameFlowController = ResolveGameFlowController(); // Town 복귀 명령을 전달할 GameFlowController 참조입니다.
        if (gameFlowController != null)
        {
            bool startedFromFlow = gameFlowController.RequestReturnToTown();
            if (!startedFromFlow)
            {
                Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyStageFlowRequestFailed, "[StageReturnToTownPresenter] GameFlowController 기반 Town 복귀 시작에 실패했습니다."), this);
            }
            else if (_verboseLog)
            {
                Debug.Log("[StageReturnToTownPresenter] ReturnToTown requested via GameFlowController.", this);
            }

            return;
        }

        EmitGameFlowRequiredTelemetry("GameFlowControllerMissing");
        Debug.LogError("[StageReturnToTownPresenter] GameFlowController가 필수라 Town 복귀 요청을 중단합니다.", this);
    }

    /// <summary>
    /// 직렬화 참조 또는 싱글톤에서 GameFlowController를 해석합니다.
    /// </summary>
    private GameFlowController ResolveGameFlowController()
    {
        if (_gameFlowController != null)
        {
            return _gameFlowController;
        }

        return GameFlowController.Instance;
    }

    /// <summary>
    /// GameFlow 필수 경로 위반 시 강한 경고와 텔레메트리 로그를 출력합니다.
    /// </summary>
    private void EmitGameFlowRequiredTelemetry(string reason)
    {
        Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyStageGameFlowRequired, $"[StageReturnToTownPresenter][GAMEFLOW_REQUIRED] reason={reason}"), this);
        Debug.Log($"[Telemetry][GameFlowRequired] feature=StageReturnToTownPresenter, reason={reason}", this);
    }
}
