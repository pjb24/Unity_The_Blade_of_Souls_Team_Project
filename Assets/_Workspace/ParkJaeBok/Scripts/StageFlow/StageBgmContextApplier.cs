using UnityEngine;

/// <summary>
/// StageSession에 기록된 BGM 컨텍스트를 BgmDirector에 적용하는 브리지입니다.
/// </summary>
public class StageBgmContextApplier : MonoBehaviour
{
    [Tooltip("씬 활성화 시 자동으로 BGM 컨텍스트를 적용할지 여부입니다.")]
    [SerializeField] private bool _applyOnEnable = true; // 컴포넌트 활성화 시 컨텍스트를 자동 적용할지 여부입니다.

    [Tooltip("한 번 적용한 StageSession 컨텍스트를 소비(초기화)할지 여부입니다.")]
    [SerializeField] private bool _consumeContextAfterApply = true; // 적용 직후 StageSession의 요청 컨텍스트를 None으로 초기화할지 여부입니다.

    [Tooltip("비활성화/파괴 시 자신이 등록한 컨텍스트를 Pop할지 여부입니다.")]
    [SerializeField] private bool _popAppliedContextOnDisable = true; // 이 컴포넌트가 등록한 컨텍스트를 OnDisable에서 해제할지 여부입니다.

    [Tooltip("필요 시 수동 지정할 BgmDirector 참조입니다. 비어 있으면 씬에서 탐색합니다.")]
    [SerializeField] private BgmDirector _bgmDirector; // 컨텍스트 Push/Pop 요청을 전달할 대상 BgmDirector 참조입니다.

    private bool _hasAppliedContext; // 현재 컴포넌트가 컨텍스트를 등록한 상태인지 여부입니다.
    private E_BgmContextType _appliedContextType = E_BgmContextType.None; // 마지막으로 이 컴포넌트가 등록한 컨텍스트 타입입니다.

    /// <summary>
    /// 활성화 시 옵션에 따라 컨텍스트 적용을 시도합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_applyOnEnable)
        {
            ApplyFromStageSession();
        }
    }

    /// <summary>
    /// 비활성화 시 자신이 등록한 컨텍스트를 안전하게 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_popAppliedContextOnDisable)
        {
            PopAppliedContext();
        }
    }

    /// <summary>
    /// StageSession에 저장된 컨텍스트를 조회해 BGM 컨텍스트를 활성화합니다.
    /// </summary>
    public void ApplyFromStageSession()
    {
        if (TryResolveDirector() == false)
        {
            Debug.LogWarning("[StageBgmContextApplier] BgmDirector를 찾지 못해 컨텍스트 적용을 건너뜁니다.", this);
            return;
        }

        E_BgmContextType contextType = _consumeContextAfterApply
            ? StageSession.Instance.ConsumeRequestedBgmContextType()
            : StageSession.Instance.RequestedBgmContextType; // 이번 씬에서 적용할 요청 BGM 컨텍스트 타입입니다.

        if (contextType == E_BgmContextType.None)
        {
            return;
        }

        _bgmDirector.ClearRequester(this);
        _bgmDirector.PushContext(contextType, this);

        _appliedContextType = contextType;
        _hasAppliedContext = true;
    }

    /// <summary>
    /// 현재 컴포넌트가 등록한 컨텍스트를 Director에서 해제합니다.
    /// </summary>
    public void PopAppliedContext()
    {
        if (_hasAppliedContext == false)
        {
            return;
        }

        if (TryResolveDirector() == false)
        {
            _hasAppliedContext = false;
            _appliedContextType = E_BgmContextType.None;
            return;
        }

        _bgmDirector.PopContext(_appliedContextType, this);
        _bgmDirector.ClearRequester(this);

        _hasAppliedContext = false;
        _appliedContextType = E_BgmContextType.None;
    }

    /// <summary>
    /// BgmDirector 참조가 비어 있으면 씬 탐색으로 보정합니다.
    /// </summary>
    private bool TryResolveDirector()
    {
        if (_bgmDirector != null)
        {
            return true;
        }

        _bgmDirector = FindAnyObjectByType<BgmDirector>();
        return _bgmDirector != null;
    }
}
