using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 옵션 Row의 포인터 진입/이탈 이벤트를 외부 콜백으로 전달하는 릴레이 컴포넌트입니다.
/// </summary>
public sealed class OptionRowHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Action _onPointerEnter; // Row 포인터 진입 시 실행할 콜백입니다.
    private Action _onPointerExit; // Row 포인터 이탈 시 실행할 콜백입니다.

    /// <summary>
    /// 포인터 진입 콜백을 등록합니다.
    /// </summary>
    public void SetEnterListener(Action listener)
    {
        _onPointerEnter = listener;
    }

    /// <summary>
    /// 포인터 이탈 콜백을 등록합니다.
    /// </summary>
    public void SetExitListener(Action listener)
    {
        _onPointerExit = listener;
    }

    /// <summary>
    /// 포인터가 Row 영역에 진입했을 때 등록된 콜백을 호출합니다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _onPointerEnter?.Invoke();
    }

    /// <summary>
    /// 포인터가 Row 영역에서 이탈했을 때 등록된 콜백을 호출합니다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        _onPointerExit?.Invoke();
    }
}
