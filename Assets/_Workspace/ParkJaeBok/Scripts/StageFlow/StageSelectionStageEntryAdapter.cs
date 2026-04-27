using UnityEngine;

/// <summary>
/// 스테이지 선택 UI 버튼에서 기존 TownStageSelectorPresenter 진입 기능을 호출하는 어댑터입니다.
/// </summary>
[DisallowMultipleComponent]
public class StageSelectionStageEntryAdapter : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("기존 스테이지 진입 흐름을 수행하는 TownStageSelectorPresenter 참조입니다. 비어 있으면 상위 오브젝트에서 자동 검색합니다.")]
    [SerializeField] private TownStageSelectorPresenter _stageSelectorPresenter; // 기존 스테이지 진입 Presenter를 재사용하기 위한 참조입니다.

    [Header("Default Request")]
    [Tooltip("버튼에서 별도 ID를 넘기지 않을 때 사용할 기본 스테이지 식별자입니다.")]
    [SerializeField] private string _defaultStageId; // Inspector에서 버튼별 기본 스테이지 ID를 연결하기 위한 값입니다.

    /// <summary>
    /// 전달받은 스테이지 ID를 우선 사용하고, 없으면 기본 스테이지 ID로 기존 진입 기능을 호출합니다.
    /// </summary>
    public bool RequestEnterStage(string stageId)
    {
        if (_stageSelectorPresenter == null)
        {
            ResolvePresenter();
        }

        if (_stageSelectorPresenter == null)
        {
            Debug.LogWarning("[StageSelectionStageEntryAdapter] TownStageSelectorPresenter is missing. Stage entry request ignored.", this);
            return false;
        }

        string resolvedStageId = string.IsNullOrWhiteSpace(stageId) ? _defaultStageId : stageId; // 실제 진입 요청에 사용할 스테이지 ID입니다.
        if (string.IsNullOrWhiteSpace(resolvedStageId))
        {
            Debug.LogWarning("[StageSelectionStageEntryAdapter] Stage id is empty. Stage entry request ignored.", this);
            return false;
        }

        _stageSelectorPresenter.SelectStageById(resolvedStageId);
        return true;
    }

    /// <summary>
    /// 같은 오브젝트 또는 상위 오브젝트에서 기존 Presenter 참조를 자동 보정합니다.
    /// </summary>
    private void ResolvePresenter()
    {
        if (_stageSelectorPresenter != null)
        {
            return;
        }

        _stageSelectorPresenter = GetComponent<TownStageSelectorPresenter>();
        if (_stageSelectorPresenter == null)
        {
            _stageSelectorPresenter = GetComponentInParent<TownStageSelectorPresenter>();
        }
    }

    /// <summary>
    /// Inspector 편집 시 필수 참조를 가능한 범위에서 자동 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        ResolvePresenter();
    }
}
