using UnityEngine;

/// <summary>
/// 마을 UI에서 스테이지 선택 입력을 받아 상태 판정과 씬 전환을 처리하는 프리젠터입니다.
/// </summary>
public class TownStageSelectorPresenter : MonoBehaviour
{
    [Tooltip("마을 UI가 참조할 스테이지 카탈로그 에셋입니다.")]
    [SerializeField] private StageCatalog _stageCatalog; // UI에서 선택 가능한 스테이지 목록을 제공하는 카탈로그입니다.

    [Tooltip("스테이지 선택 전에 개방/입장 가능 상태를 검사할지 여부입니다.")]
    [SerializeField] private bool _useAvailabilityValidation = true; // 스테이지 선택 시 상태 판정을 강제할지 여부입니다.

    [Tooltip("선택 UI 클릭 시 재생할 효과음을 사용할지 여부입니다.")]
    [SerializeField] private bool _playUiClickSfx = true; // 스테이지 선택 직전 UI 클릭 효과음 재생 여부입니다.

    [Tooltip("전환 전에 선택 이벤트와 상태 정보를 로그로 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLog = true; // 디버깅을 위해 선택 정보를 로그에 기록할지 여부입니다.

    [Tooltip("스테이지 진입 명령을 전달할 GameFlowController 참조입니다. 비어 있으면 GameFlowController.Instance를 사용합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // 스테이지 진입을 GameFlow 명령으로 위임할 컨트롤러 참조입니다.

    /// <summary>
    /// 카탈로그 인덱스를 기반으로 스테이지 진입을 시도합니다.
    /// </summary>
    public void SelectStageByIndex(int stageIndex)
    {
        if (_stageCatalog == null)
        {
            Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyStageInvalidInput, "[TownStageSelectorPresenter] StageCatalog가 비어 있습니다."), this);
            return;
        }

        if (_stageCatalog.TryGetByIndex(stageIndex, out StageDefinition stageDefinition) == false)
        {
            Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyStageInvalidInput, $"[TownStageSelectorPresenter] 유효하지 않은 stageIndex입니다. index={stageIndex}"), this);
            return;
        }

        SelectStage(stageDefinition);
    }

    /// <summary>
    /// stageId를 기반으로 스테이지 진입을 시도합니다.
    /// </summary>
    public void SelectStageById(string stageId)
    {
        if (_stageCatalog == null)
        {
            Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyStageInvalidInput, "[TownStageSelectorPresenter] StageCatalog가 비어 있습니다."), this);
            return;
        }

        if (_stageCatalog.TryGetById(stageId, out StageDefinition stageDefinition) == false)
        {
            Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyStageInvalidInput, $"[TownStageSelectorPresenter] 유효하지 않은 stageId입니다. stageId={stageId}"), this);
            return;
        }

        SelectStage(stageDefinition);
    }

    /// <summary>
    /// 전달된 stageId의 현재 입장 가능 상태를 평가합니다.
    /// </summary>
    public StageAvailabilityResult EvaluateStageAvailabilityById(string stageId)
    {
        if (_stageCatalog == null)
        {
            return new StageAvailabilityResult(false, false, false, "StageCatalog is null");
        }

        if (_stageCatalog.TryGetById(stageId, out StageDefinition stageDefinition) == false)
        {
            return new StageAvailabilityResult(false, false, false, $"Unknown stage id: {stageId}");
        }

        StageProgressRuntime progressRuntime = StageProgressRuntime.Instance; // 스테이지 상태 판정에 사용할 진행도 런타임 인스턴스입니다.
        return StageAvailabilityService.Evaluate(stageDefinition, progressRuntime);
    }

    /// <summary>
    /// 전달된 StageDefinition 기준으로 상태 판정, 세션 저장, 씬 전환을 처리합니다.
    /// </summary>
    public void SelectStage(StageDefinition stageDefinition)
    {
        if (stageDefinition == null)
        {
            Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyStageInvalidInput, "[TownStageSelectorPresenter] stageDefinition이 null입니다."), this);
            return;
        }

        StageProgressRuntime progressRuntime = StageProgressRuntime.Instance; // 입장 가능 상태를 평가할 진행도 런타임 인스턴스입니다.
        StageAvailabilityResult availability = StageAvailabilityService.Evaluate(stageDefinition, progressRuntime);

        if (_useAvailabilityValidation && availability.IsEnterable == false)
        {
            Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyStageInvalidInput, $"[TownStageSelectorPresenter] 입장 불가 상태입니다. id={stageDefinition.StageId}, reason={availability.Reason}"), this);
            return;
        }

        if (_playUiClickSfx && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(E_SoundId.SFX_UI_Click, transform.position);
        }

        if (_verboseLog)
        {
            Debug.Log($"[TownStageSelectorPresenter] Stage Selected: id={stageDefinition.StageId}, scene={stageDefinition.SceneName}, multiplayer={availability.IsMultiplayerAvailable}", this);
        }

        GameFlowController gameFlowController = ResolveGameFlowController(); // 스테이지 진입 명령을 전달할 GameFlowController 참조입니다.
        if (gameFlowController != null)
        {
            bool startedFromFlow = gameFlowController.RequestEnterStage(stageDefinition);
            if (!startedFromFlow)
            {
                Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyStageFlowRequestFailed, "[TownStageSelectorPresenter] GameFlowController 기반 스테이지 진입 시작에 실패했습니다."), this);
            }

            return;
        }

        EmitGameFlowRequiredTelemetry("GameFlowControllerMissing", stageDefinition);
        Debug.LogError("[TownStageSelectorPresenter] GameFlowController가 필수라 스테이지 진입 요청을 중단합니다.", this);
    }

    /// <summary>
    /// 스테이지 클리어 결과를 진행도 런타임에 반영합니다.
    /// </summary>
    public void ReportStageClearedById(string stageId)
    {
        StageProgressRuntime.Instance.MarkStageCleared(stageId);

        if (_verboseLog)
        {
            Debug.Log($"[TownStageSelectorPresenter] Stage clear reported. id={stageId}", this);
        }
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
    private void EmitGameFlowRequiredTelemetry(string reason, StageDefinition stageDefinition)
    {
        string stageId = stageDefinition != null ? stageDefinition.StageId : string.Empty; // 텔레메트리 로그에 포함할 대상 스테이지 ID입니다.
        string sceneName = stageDefinition != null ? stageDefinition.SceneName : string.Empty; // 텔레메트리 로그에 포함할 대상 씬 이름입니다.
        Debug.LogWarning(GameFlowWarningCatalog.BuildKeyed(GameFlowWarningCatalog.KeyStageGameFlowRequired, $"[TownStageSelectorPresenter][GAMEFLOW_REQUIRED] reason={reason}, stageId={stageId}, scene={sceneName}"), this);
        Debug.Log($"[Telemetry][GameFlowRequired] feature=TownStageSelectorPresenter, reason={reason}, stageId={stageId}, scene={sceneName}", this);
    }
}
