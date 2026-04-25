using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버프 게이지 값을 보관하고 증가/감소/클램프 및 리스너 통지를 담당하는 컴포넌트입니다.
/// </summary>
public class PlayerBuffGauge : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("현재 버프 게이지 값입니다.")]
    [SerializeField] private float _currentGauge; // 현재 버프 게이지 값입니다.

    [Tooltip("최대 버프 게이지 값입니다.")]
    [SerializeField] private float _maxGauge; // 최대 버프 게이지 값입니다.

    [Tooltip("현재 게이지 정규화 값(0~1)입니다.")]
    [SerializeField] private float _normalizedGauge; // 현재 게이지 정규화 값입니다.

    private readonly List<IPlayerBuffGaugeListener> _listeners = new List<IPlayerBuffGaugeListener>(); // 게이지 변경 리스너 목록입니다.

    /// <summary>
    /// 현재 버프 게이지 값을 반환합니다.
    /// </summary>
    public float CurrentGauge => _currentGauge;

    /// <summary>
    /// 최대 버프 게이지 값을 반환합니다.
    /// </summary>
    public float MaxGauge => _maxGauge;

    /// <summary>
    /// 현재 게이지 정규화 값을 반환합니다.
    /// </summary>
    public float NormalizedGauge => _normalizedGauge;

    /// <summary>
    /// 최대 게이지와 현재 게이지를 초기화합니다.
    /// </summary>
    public void Initialize(float maxGauge, float initialGauge)
    {
        _maxGauge = Mathf.Max(0f, maxGauge);
        SetCurrentGauge(initialGauge);
    }

    /// <summary>
    /// 게이지 변경 리스너를 등록합니다.
    /// </summary>
    public void AddListener(IPlayerBuffGaugeListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[PlayerBuffGauge] Cannot add null listener.");
            return;
        }

        if (_listeners.Contains(listener))
        {
            Debug.LogWarning("[PlayerBuffGauge] Duplicate listener registration ignored.");
            return;
        }

        _listeners.Add(listener);
    }

    /// <summary>
    /// 게이지 변경 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(IPlayerBuffGaugeListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[PlayerBuffGauge] Cannot remove null listener.");
            return;
        }

        if (_listeners.Remove(listener) == false)
        {
            Debug.LogWarning("[PlayerBuffGauge] Tried to remove unknown listener.");
        }
    }

    /// <summary>
    /// 게이지를 증가시킵니다.
    /// </summary>
    public void AddGauge(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetCurrentGauge(_currentGauge + amount);
    }

    /// <summary>
    /// 게이지를 감소시킵니다.
    /// </summary>
    public void ConsumeGauge(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetCurrentGauge(_currentGauge - amount);
    }

    /// <summary>
    /// 현재 게이지 값을 설정하고 정규화 값을 갱신합니다.
    /// </summary>
    public void SetCurrentGauge(float gauge)
    {
        float previousGauge = _currentGauge; // 변경 전 게이지 값입니다.

        if (_maxGauge <= 0f)
        {
            _currentGauge = 0f;
            _normalizedGauge = 0f;
        }
        else
        {
            _currentGauge = Mathf.Clamp(gauge, 0f, _maxGauge);
            _normalizedGauge = Mathf.Clamp01(_currentGauge / _maxGauge);
        }

        if (Mathf.Approximately(previousGauge, _currentGauge))
        {
            return;
        }

        NotifyGaugeChanged();
    }

    /// <summary>
    /// 현재 게이지가 비어 있는지 여부를 반환합니다.
    /// </summary>
    public bool IsGaugeEmpty()
    {
        return _currentGauge <= 0f;
    }

    /// <summary>
    /// 리스너에게 게이지 변경 값을 전달합니다.
    /// </summary>
    public void NotifyGaugeChanged()
    {
        for (int index = 0; index < _listeners.Count; index++)
        {
            _listeners[index].OnBuffGaugeChanged(_currentGauge, _maxGauge, _normalizedGauge);
        }
    }
}
