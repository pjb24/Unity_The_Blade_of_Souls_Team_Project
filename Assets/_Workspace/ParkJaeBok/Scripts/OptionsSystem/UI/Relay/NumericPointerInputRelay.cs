using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Numeric 클릭/드래그 입력을 0~1 정규화 값으로 변환해 전달하는 포인터 릴레이 컴포넌트입니다.
/// </summary>
public sealed class NumericPointerInputRelay : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Tooltip("정규화 좌표를 계산할 기준 RectTransform입니다. 비워두면 현재 오브젝트 RectTransform을 사용합니다.")]
    [SerializeField] private RectTransform _referenceRect; // 포인터 좌표를 정규화할 기준 사각형 영역입니다.

    private Action<float> _onNormalizedPointerInput; // 정규화 좌표 입력을 수신할 외부 콜백 목록입니다.

    /// <summary>
    /// 정규화 입력 콜백을 등록합니다.
    /// </summary>
    public void AddListener(Action<float> listener)
    {
        _onNormalizedPointerInput += listener;
    }

    /// <summary>
    /// 등록된 정규화 입력 콜백을 해제합니다.
    /// </summary>
    public void RemoveListener(Action<float> listener)
    {
        _onNormalizedPointerInput -= listener;
    }

    /// <summary>
    /// 포인터 다운 이벤트를 수신해 정규화 좌표를 전달합니다.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        EmitNormalizedPointerInput(eventData);
    }

    /// <summary>
    /// 드래그 이벤트를 수신해 정규화 좌표를 지속 전달합니다.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        EmitNormalizedPointerInput(eventData);
    }

    /// <summary>
    /// 이벤트 좌표를 기준 RectTransform 내부의 0~1 값으로 변환해 브로드캐스트합니다.
    /// </summary>
    private void EmitNormalizedPointerInput(PointerEventData eventData)
    {
        RectTransform targetRect = ResolveTargetRect(); // 좌표 정규화 계산에 사용할 기준 RectTransform입니다.
        if (targetRect == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint) == false)
        {
            return;
        }

        Rect rect = targetRect.rect; // 로컬 좌표를 0~1로 정규화하기 위한 기준 사각형입니다.
        float normalized = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        _onNormalizedPointerInput?.Invoke(Mathf.Clamp01(normalized));
    }

    /// <summary>
    /// 명시 설정 또는 현재 오브젝트에서 정규화 기준 RectTransform을 해석합니다.
    /// </summary>
    private RectTransform ResolveTargetRect()
    {
        if (_referenceRect != null)
        {
            return _referenceRect;
        }

        return transform as RectTransform;
    }
}
