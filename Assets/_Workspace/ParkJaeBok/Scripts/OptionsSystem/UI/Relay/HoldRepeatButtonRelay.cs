using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 버튼을 누르고 있는 동안 지정 간격으로 반복 콜백을 발생시키는 릴레이 컴포넌트입니다.
/// </summary>
public class HoldRepeatButtonRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("버튼을 누른 뒤 반복 시작까지 대기할 시간(초)입니다.")]
    [SerializeField] private float _initialDelay = 0.35f; // 반복 입력을 시작하기 전 대기 시간(초)입니다.

    [Tooltip("반복 입력 발생 간격(초)입니다.")]
    [SerializeField] private float _repeatInterval = 0.08f; // 반복 입력이 발생하는 주기(초)입니다.

    private Action _repeatListeners; // 반복 입력 발생 시 호출할 리스너 체인입니다.
    private bool _isPointerPressed; // 현재 포인터가 버튼을 누르고 있는 상태인지 여부입니다.
    private float _pressedDuration; // 포인터 누름 시작 후 누적 경과 시간(초)입니다.
    private float _nextRepeatAt; // 다음 반복 입력이 발생할 누적 시간 임계값(초)입니다.

    /// <summary>
    /// 반복 입력 리스너를 등록합니다.
    /// </summary>
    public void AddListener(Action listener)
    {
        _repeatListeners += listener;
    }

    /// <summary>
    /// 반복 입력 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(Action listener)
    {
        _repeatListeners -= listener;
    }

    /// <summary>
    /// 포인터 눌림 시작 시 반복 타이머를 초기화합니다.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        _isPointerPressed = true;
        _pressedDuration = 0f;
        _nextRepeatAt = Mathf.Max(0f, _initialDelay);
    }

    /// <summary>
    /// 포인터 눌림 해제 시 반복 상태를 종료합니다.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        _isPointerPressed = false;
    }

    /// <summary>
    /// 포인터가 버튼 영역을 벗어나면 반복 상태를 종료합니다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerPressed = false;
    }

    /// <summary>
    /// 포인터가 눌린 상태라면 대기/간격 조건에 맞춰 반복 콜백을 발생시킵니다.
    /// </summary>
    private void Update()
    {
        if (_isPointerPressed == false)
        {
            return;
        }

        _pressedDuration += Time.unscaledDeltaTime;
        float interval = Mathf.Max(0.01f, _repeatInterval); // 0 또는 음수 간격 입력으로 인한 무한 반복을 방지하는 안전 간격입니다.

        while (_pressedDuration >= _nextRepeatAt)
        {
            _repeatListeners?.Invoke();
            _nextRepeatAt += interval;
        }
    }
}
