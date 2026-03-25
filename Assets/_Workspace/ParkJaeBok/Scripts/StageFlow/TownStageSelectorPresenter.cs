using UnityEngine;

/// <summary>
/// 마을 UI에서 스테이지 선택 입력을 받아 씬 전환을 요청하는 프리젠터입니다.
/// </summary>
public class TownStageSelectorPresenter : MonoBehaviour
{
    [Tooltip("마을 UI가 참조할 스테이지 카탈로그 에셋입니다.")]
    [SerializeField] private StageCatalog _stageCatalog; // UI에서 선택 가능한 스테이지 목록을 제공하는 카탈로그입니다.

    [Tooltip("선택 UI 클릭 시 재생할 효과음을 사용할지 여부입니다.")]
    [SerializeField] private bool _playUiClickSfx = true; // 스테이지 선택 직전 UI 클릭 효과음 재생 여부입니다.

    [Tooltip("전환 전에 선택 이벤트를 로그로 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLog = true; // 디버깅을 위해 선택 정보를 로그에 기록할지 여부입니다.

    /// <summary>
    /// 카탈로그 인덱스를 기반으로 스테이지 진입을 시도합니다.
    /// </summary>
    public void SelectStageByIndex(int stageIndex)
    {
        if (_stageCatalog == null)
        {
            Debug.LogWarning("[TownStageSelectorPresenter] StageCatalog가 비어 있습니다.", this);
            return;
        }

        if (_stageCatalog.TryGetByIndex(stageIndex, out StageDefinition stageDefinition) == false)
        {
            Debug.LogWarning($"[TownStageSelectorPresenter] 유효하지 않은 stageIndex입니다. index={stageIndex}", this);
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
            Debug.LogWarning("[TownStageSelectorPresenter] StageCatalog가 비어 있습니다.", this);
            return;
        }

        if (_stageCatalog.TryGetById(stageId, out StageDefinition stageDefinition) == false)
        {
            Debug.LogWarning($"[TownStageSelectorPresenter] 유효하지 않은 stageId입니다. stageId={stageId}", this);
            return;
        }

        SelectStage(stageDefinition);
    }

    /// <summary>
    /// 전달된 StageDefinition 기준으로 세션 저장과 씬 전환을 처리합니다.
    /// </summary>
    public void SelectStage(StageDefinition stageDefinition)
    {
        if (stageDefinition == null)
        {
            Debug.LogWarning("[TownStageSelectorPresenter] stageDefinition이 null입니다.", this);
            return;
        }

        if (_playUiClickSfx && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(E_SoundId.SFX_UI_Click, transform.position);
        }

        if (_verboseLog)
        {
            Debug.Log($"[TownStageSelectorPresenter] Stage Selected: id={stageDefinition.StageId}, scene={stageDefinition.SceneName}", this);
        }

        bool started = SceneTransitionService.Instance.TryLoadStage(stageDefinition);
        if (started == false)
        {
            Debug.LogWarning("[TownStageSelectorPresenter] 씬 전환 시작에 실패했습니다.", this);
        }
    }
}
