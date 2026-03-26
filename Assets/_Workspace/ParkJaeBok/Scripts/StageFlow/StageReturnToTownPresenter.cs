using UnityEngine;

/// <summary>
/// 현재 스테이지에서 Town 스테이지로 복귀를 요청하는 프리젠터입니다.
/// </summary>
public class StageReturnToTownPresenter : MonoBehaviour
{
    [Tooltip("Town 스테이지를 조회할 StageCatalog 에셋입니다.")]
    [SerializeField] private StageCatalog _stageCatalog; // Town StageDefinition 조회에 사용할 카탈로그입니다.

    [Tooltip("현재 스테이지 ID를 강제로 지정할 값입니다. 비워두면 StageSession.SelectedStageId를 사용합니다.")]
    [SerializeField] private string _currentStageIdOverride; // 현재 스테이지를 수동 지정해야 할 때 사용하는 오버라이드 ID입니다.

    [Tooltip("현재 스테이지 정의의 TownReturnPointId를 Town 진입 엔트리 포인트로 사용할지 여부입니다.")]
    [SerializeField] private bool _useCurrentStageTownReturnPoint = true; // 현재 스테이지의 복귀 포인트를 Town 진입점으로 사용할지 여부입니다.

    [Tooltip("현재 스테이지에서 Town 복귀 포인트를 해석하지 못했을 때 사용할 기본 Town 엔트리 포인트 ID입니다.")]
    [SerializeField] private string _fallbackTownEntryPointId = "Town_Default"; // Town 복귀 엔트리 포인트를 찾지 못했을 때 적용할 폴백 ID입니다.

    [Tooltip("복귀 처리 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLog = true; // Town 복귀 요청 처리 로그 출력 여부입니다.

    /// <summary>
    /// Town 스테이지 복귀를 시도합니다.
    /// </summary>
    public void ReturnToTown()
    {
        if (_stageCatalog == null)
        {
            Debug.LogWarning("[StageReturnToTownPresenter] StageCatalog가 비어 있습니다.", this);
            return;
        }

        if (_stageCatalog.TryGetTownStage(out StageDefinition townStage) == false || townStage == null)
        {
            Debug.LogWarning("[StageReturnToTownPresenter] Town 스테이지를 찾지 못했습니다. StageCatalog 설정을 확인하세요.", this);
            return;
        }

        StageSession session = StageSession.Instance; // Town 복귀 문맥을 기록할 StageSession 인스턴스입니다.
        string entryPointId = ResolveTownEntryPointId(session); // Town 씬 진입 시 사용할 엔트리 포인트 ID입니다.
        session.SetNextStage(townStage, entryPointId);

        bool started = SceneTransitionService.Instance.TryLoadScene(townStage.SceneName);
        if (!started)
        {
            Debug.LogWarning("[StageReturnToTownPresenter] Town 씬 전환 시작에 실패했습니다.", this);
            return;
        }

        if (_verboseLog)
        {
            Debug.Log($"[StageReturnToTownPresenter] ReturnToTown started. townStageId={townStage.StageId}, scene={townStage.SceneName}, entry={entryPointId}", this);
        }
    }

    /// <summary>
    /// Town 씬 진입 시 사용할 엔트리 포인트 ID를 해석합니다.
    /// </summary>
    private string ResolveTownEntryPointId(StageSession session)
    {
        if (_useCurrentStageTownReturnPoint == false)
        {
            return _fallbackTownEntryPointId;
        }

        string currentStageId = ResolveCurrentStageId(session);
        if (string.IsNullOrWhiteSpace(currentStageId))
        {
            return _fallbackTownEntryPointId;
        }

        if (_stageCatalog.TryGetById(currentStageId, out StageDefinition currentStage) == false || currentStage == null)
        {
            return _fallbackTownEntryPointId;
        }

        if (string.IsNullOrWhiteSpace(currentStage.TownReturnPointId))
        {
            return _fallbackTownEntryPointId;
        }

        return currentStage.TownReturnPointId;
    }

    /// <summary>
    /// 현재 스테이지 ID를 오버라이드 값 또는 세션 값 기준으로 반환합니다.
    /// </summary>
    private string ResolveCurrentStageId(StageSession session)
    {
        if (string.IsNullOrWhiteSpace(_currentStageIdOverride) == false)
        {
            return _currentStageIdOverride;
        }

        if (session == null)
        {
            return string.Empty;
        }

        return session.SelectedStageId;
    }
}
