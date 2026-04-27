using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 스테이지 선택 버튼의 포인터 Hover 상태를 StageSelectionUIController로 전달합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageSelectionButtonPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private StageSelectionUIController _owner; // Hover 상태를 전달할 스테이지 선택 UI 컨트롤러입니다.
    private string _stageId; // Hover 상태가 발생한 버튼의 스테이지 ID입니다.

    /// <summary>
    /// Hover 상태를 전달할 컨트롤러와 스테이지 ID를 연결합니다.
    /// </summary>
    public void Bind(StageSelectionUIController owner, string stageId)
    {
        _owner = owner;
        _stageId = stageId;
    }

    /// <summary>
    /// 지정한 컨트롤러와 연결되어 있으면 연결을 해제합니다.
    /// </summary>
    public void Unbind(StageSelectionUIController owner)
    {
        if (_owner != owner)
        {
            return;
        }

        _owner = null;
        _stageId = string.Empty;
    }

    /// <summary>
    /// 포인터가 버튼에 들어오면 Hover 시작 상태를 컨트롤러에 전달합니다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        NotifyHoverChanged(true);
    }

    /// <summary>
    /// 포인터가 버튼에서 나가면 Hover 종료 상태를 컨트롤러에 전달합니다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        NotifyHoverChanged(false);
    }

    /// <summary>
    /// 현재 연결 상태를 검증한 뒤 Hover 변경을 컨트롤러에 전달합니다.
    /// </summary>
    private void NotifyHoverChanged(bool isHovered)
    {
        if (_owner == null)
        {
            Debug.LogWarning("[StageSelectionButtonPointerRelay] Owner controller is missing. Hover sync ignored.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(_stageId))
        {
            Debug.LogWarning("[StageSelectionButtonPointerRelay] Stage id is empty. Hover sync ignored.", this);
            return;
        }

        _owner.NotifyButtonHoverChangedFromPointer(_stageId, isHovered);
    }
}
