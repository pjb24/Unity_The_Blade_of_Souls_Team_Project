using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enum 텍스트/버튼 기반 옵션 Row Binder입니다.
/// </summary>
public sealed class EnumOptionRowBinder : IOptionRowBinder
{
    private readonly OptionRowBindingEntry _entry; // Enum 바인딩에 사용할 Inspector 설정 항목입니다.
    private readonly int _maxEnumIndex; // Enum 인덱스 순환 시 사용할 최대 인덱스 값입니다.
    private int _currentIndex; // 현재 선택된 Enum 인덱스 런타임 값입니다.

    public E_OptionBindingKey BindingKey => _entry.BindingKey;

    /// <summary>
    /// Enum Row Binder를 생성하고 버튼 입력 이벤트를 초기화합니다.
    /// </summary>
    public EnumOptionRowBinder(OptionRowBindingEntry entry)
    {
        _entry = entry;
        _maxEnumIndex = Mathf.Max(0, ResolveMaxEnumIndex(entry.BindingKey));
        _currentIndex = 0;

        if (_entry.EnumPrevButton != null)
        {
            _entry.EnumPrevButton.onClick.AddListener(HandleClickPrev);
        }

        if (_entry.EnumNextButton != null)
        {
            _entry.EnumNextButton.onClick.AddListener(HandleClickNext);
        }
    }

    /// <summary>
    /// OptionSaveData의 enum/int 값을 텍스트 UI에 반영합니다.
    /// </summary>
    public void ApplyToWidget(OptionSaveData optionData)
    {
        if (OptionBindingDataAccessor.TryGetInt(optionData, _entry.BindingKey, out int value) == false)
        {
            return;
        }

        _currentIndex = Mathf.Clamp(value, 0, _maxEnumIndex);
        RefreshEnumVisuals();
    }

    /// <summary>
    /// 현재 런타임 Enum 인덱스를 OptionSaveData에 기록합니다.
    /// </summary>
    public bool TryWriteToData(ref OptionSaveData optionData, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (OptionBindingDataAccessor.TrySetInt(ref optionData, _entry.BindingKey, _currentIndex) == false)
        {
            errorMessage = $"[EnumOptionRowBinder] int 매핑이 정의되지 않았습니다. key={_entry.BindingKey}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Enum 이전 값 버튼 클릭 시 인덱스를 감소시키고 UI를 갱신합니다.
    /// </summary>
    private void HandleClickPrev()
    {
        _currentIndex = _currentIndex <= 0 ? _maxEnumIndex : _currentIndex - 1;
        RefreshEnumVisuals();
    }

    /// <summary>
    /// Enum 다음 값 버튼 클릭 시 인덱스를 증가시키고 UI를 갱신합니다.
    /// </summary>
    private void HandleClickNext()
    {
        _currentIndex = _currentIndex >= _maxEnumIndex ? 0 : _currentIndex + 1;
        RefreshEnumVisuals();
    }

    /// <summary>
    /// 현재 Enum 인덱스를 텍스트에 반영하고 버튼 활성 상태를 갱신합니다.
    /// </summary>
    private void RefreshEnumVisuals()
    {
        TMP_Text label = _entry.EnumValueText; // Enum 현재 선택값을 표시할 텍스트 참조입니다.
        if (label != null)
        {
            string[] labels = _entry.EnumDisplayLabels; // Enum 인덱스별 표시 라벨 배열입니다.
            label.text = labels != null && _currentIndex >= 0 && _currentIndex < labels.Length
                ? labels[_currentIndex]
                : _currentIndex.ToString();
        }

        if (_entry.EnumPrevButton != null)
        {
            _entry.EnumPrevButton.interactable = _maxEnumIndex > 0;
        }

        if (_entry.EnumNextButton != null)
        {
            _entry.EnumNextButton.interactable = _maxEnumIndex > 0;
        }
    }

    /// <summary>
    /// BindingKey에 대응하는 enum 최대 인덱스를 계산합니다.
    /// </summary>
    private int ResolveMaxEnumIndex(E_OptionBindingKey bindingKey)
    {
        switch (bindingKey)
        {
            case E_OptionBindingKey.DisplayScreenMode: return (int)E_OptionScreenMode.Borderless;
            case E_OptionBindingKey.DisplayVSync: return (int)E_OptionVSyncMode.EverySecondVBlank;
            case E_OptionBindingKey.DisplayFrameLimit: return 5;
            case E_OptionBindingKey.DisplayGraphicsPreset: return (int)E_OptionGraphicsPreset.Custom;
            case E_OptionBindingKey.DisplayGraphicsDetailMode: return (int)E_OptionGraphicsDetailMode.UseCustom;
            case E_OptionBindingKey.InputHoldBehavior: return (int)E_OptionInputHoldBehavior.Toggle;
            case E_OptionBindingKey.AccessibilitySubtitleEnabled: return (int)E_OptionSubtitleEnabled.On;
            case E_OptionBindingKey.AccessibilitySubtitleSize: return (int)E_OptionSubtitleSize.Large;
            case E_OptionBindingKey.AccessibilityFlashReduction: return (int)E_OptionFlashReduction.On;
            case E_OptionBindingKey.AccessibilityColorBlindMode: return (int)E_OptionColorBlindMode.Tritanopia;
            case E_OptionBindingKey.AccessibilityHighContrastMode: return (int)E_OptionHighContrast.On;
            case E_OptionBindingKey.GameplayDifficulty: return (int)E_OptionDifficulty.Hard;
            case E_OptionBindingKey.GameplayAutoSaveNotification: return (int)E_OptionAutoSaveNotification.On;
            default: return 0;
        }
    }
}

/// <summary>
/// Numeric 이미지 게이지/텍스트/버튼 기반 옵션 Row Binder입니다.
/// </summary>
public sealed class NumericOptionRowBinder : IOptionRowBinder
{
    private readonly OptionRowBindingEntry _entry; // Numeric 바인딩에 사용할 Inspector 설정 항목입니다.
    private readonly float _minValue; // Numeric 런타임 최소값입니다.
    private readonly float _maxValue; // Numeric 런타임 최대값입니다.
    private readonly float _stepValue; // Numeric 버튼 입력 시 증감 단위입니다.
    private float _currentValue; // 현재 Numeric 런타임 값입니다.
    private bool _hasInitializedCurrentValue; // 현재 Numeric 값이 저장 스냅샷 기준으로 초기화되었는지 여부입니다.

    public E_OptionBindingKey BindingKey => _entry.BindingKey;

    /// <summary>
    /// Numeric Row Binder를 생성하고 버튼/입력 이벤트를 초기화합니다.
    /// </summary>
    public NumericOptionRowBinder(OptionRowBindingEntry entry)
    {
        _entry = entry;
        _minValue = Mathf.Min(entry.NumericMinValue, entry.NumericMaxValue);
        _maxValue = Mathf.Max(entry.NumericMinValue, entry.NumericMaxValue);
        _stepValue = Mathf.Max(0.0001f, entry.NumericStep);
        _currentValue = _minValue;
        _hasInitializedCurrentValue = false;

        if (_entry.NumericDecreaseButton != null)
        {
            _entry.NumericDecreaseButton.onClick.AddListener(HandleClickDecrease);
            AttachHoldRepeatRelay(_entry.NumericDecreaseButton, HandleClickDecrease);
        }

        if (_entry.NumericIncreaseButton != null)
        {
            _entry.NumericIncreaseButton.onClick.AddListener(HandleClickIncrease);
            AttachHoldRepeatRelay(_entry.NumericIncreaseButton, HandleClickIncrease);
        }

        AttachPointerInputRelay();
    }

    /// <summary>
    /// Numeric 버튼에 홀드 반복 입력 릴레이를 부착하고 반복 콜백을 등록합니다.
    /// </summary>
    private void AttachHoldRepeatRelay(Button button, System.Action callback)
    {
        HoldRepeatButtonRelay relay = button.gameObject.GetComponent<HoldRepeatButtonRelay>(); // 버튼 오브젝트에서 재사용하거나 신규로 생성할 홀드 반복 릴레이 참조입니다.
        if (relay == null)
        {
            relay = button.gameObject.AddComponent<HoldRepeatButtonRelay>();
        }

        relay.RemoveListener(callback);
        relay.AddListener(callback);
    }

    /// <summary>
    /// Numeric 이미지 입력 영역에 포인터 릴레이를 부착하고 정규화 좌표 콜백을 등록합니다.
    /// </summary>
    private void AttachPointerInputRelay()
    {
        RectTransform inputArea = ResolveNumericPointerInputArea(); // Numeric 클릭/드래그 입력을 수신할 RectTransform 영역입니다.
        if (inputArea == null)
        {
            return;
        }

        NumericPointerInputRelay relay = inputArea.GetComponent<NumericPointerInputRelay>(); // 입력 영역에서 재사용하거나 신규로 생성할 포인터 입력 릴레이 참조입니다.
        if (relay == null)
        {
            relay = inputArea.gameObject.AddComponent<NumericPointerInputRelay>();
        }

        relay.RemoveListener(HandlePointerInputNormalized);
        relay.AddListener(HandlePointerInputNormalized);
    }

    /// <summary>
    /// OptionSaveData의 float 값을 Numeric UI 상태에 반영합니다.
    /// </summary>
    public void ApplyToWidget(OptionSaveData optionData)
    {
        if (OptionBindingDataAccessor.TryGetFloat(optionData, _entry.BindingKey, out float value) == false)
        {
            return;
        }

        _currentValue = Mathf.Clamp(value, _minValue, _maxValue);
        _hasInitializedCurrentValue = true;
        RefreshNumericVisuals();
    }

    /// <summary>
    /// 현재 Numeric 런타임 값을 OptionSaveData에 기록합니다.
    /// </summary>
    public bool TryWriteToData(ref OptionSaveData optionData, out string errorMessage)
    {
        errorMessage = string.Empty;
        EnsureInitializedFromData(optionData);

        if (OptionBindingDataAccessor.TrySetFloat(ref optionData, _entry.BindingKey, _currentValue) == false)
        {
            errorMessage = $"[NumericOptionRowBinder] float 매핑이 정의되지 않았습니다. key={_entry.BindingKey}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 감소 버튼 클릭 시 값을 Step만큼 낮추고 UI를 갱신합니다.
    /// </summary>
    private void HandleClickDecrease()
    {
        EnsureInitializedWithFallbackValue();
        _currentValue = Mathf.Clamp(_currentValue - _stepValue, _minValue, _maxValue);
        RefreshNumericVisuals();
    }

    /// <summary>
    /// 증가 버튼 클릭 시 값을 Step만큼 높이고 UI를 갱신합니다.
    /// </summary>
    private void HandleClickIncrease()
    {
        EnsureInitializedWithFallbackValue();
        _currentValue = Mathf.Clamp(_currentValue + _stepValue, _minValue, _maxValue);
        RefreshNumericVisuals();
    }

    /// <summary>
    /// 포인터 입력의 정규화 좌표를 Numeric 값으로 변환해 반영합니다.
    /// </summary>
    private void HandlePointerInputNormalized(float normalized)
    {
        EnsureInitializedWithFallbackValue();
        float unclampedValue = Mathf.Lerp(_minValue, _maxValue, Mathf.Clamp01(normalized)); // 정규화 좌표를 실제 Numeric 범위로 변환한 값입니다.
        _currentValue = ClampToStep(unclampedValue);
        RefreshNumericVisuals();
    }

    /// <summary>
    /// 저장 스냅샷에서 현재 키 값을 읽어 내부 Numeric 값을 초기화합니다.
    /// </summary>
    private void EnsureInitializedFromData(OptionSaveData optionData)
    {
        if (_hasInitializedCurrentValue)
        {
            return;
        }

        if (OptionBindingDataAccessor.TryGetFloat(optionData, _entry.BindingKey, out float value))
        {
            _currentValue = Mathf.Clamp(value, _minValue, _maxValue);
            _hasInitializedCurrentValue = true;
            return;
        }

        EnsureInitializedWithFallbackValue();
    }

    /// <summary>
    /// 저장 스냅샷을 받을 수 없는 경로에서 현재 값을 안전 범위로 초기화합니다.
    /// </summary>
    private void EnsureInitializedWithFallbackValue()
    {
        if (_hasInitializedCurrentValue)
        {
            return;
        }

        _currentValue = Mathf.Clamp(_currentValue, _minValue, _maxValue);
        _hasInitializedCurrentValue = true;
    }

    /// <summary>
    /// 엔트리 설정/Fill 이미지 기준으로 Numeric 포인터 입력 영역을 해석합니다.
    /// </summary>
    private RectTransform ResolveNumericPointerInputArea()
    {
        if (_entry.NumericPointerInputArea != null)
        {
            return _entry.NumericPointerInputArea;
        }

        if (_entry.NumericFillImage != null)
        {
            return _entry.NumericFillImage.rectTransform;
        }

        return null;
    }

    /// <summary>
    /// 입력 값을 Step 단위로 반올림한 뒤 최소/최대 범위로 제한합니다.
    /// </summary>
    private float ClampToStep(float rawValue)
    {
        float stepCount = Mathf.Round((rawValue - _minValue) / _stepValue); // 최소값 기준 Step 오프셋 반올림 횟수입니다.
        float steppedValue = _minValue + (stepCount * _stepValue); // Step 경계에 정렬된 값입니다.
        return Mathf.Clamp(steppedValue, _minValue, _maxValue);
    }

    /// <summary>
    /// 현재 Numeric 값을 텍스트/입력/게이지 시각 요소에 반영합니다.
    /// </summary>
    private void RefreshNumericVisuals()
    {
        TMP_Text valueText = _entry.NumericValueText; // Numeric 현재 값을 표시할 텍스트 참조입니다.
        if (valueText != null)
        {
            valueText.text = _currentValue.ToString("0.##");
        }

        Image fillImage = _entry.NumericFillImage; // Numeric 값을 시각적으로 보여줄 Fill 이미지 참조입니다.
        if (fillImage != null)
        {
            float normalized = Mathf.Approximately(_maxValue, _minValue) ? 0f : Mathf.InverseLerp(_minValue, _maxValue, _currentValue); // 최소-최대 범위를 0~1 구간으로 정규화한 값입니다.
            if (fillImage.type == Image.Type.Filled)
            {
                fillImage.fillAmount = normalized;
            }
            else
            {
                RectTransform fillRect = fillImage.rectTransform; // Fill 이미지 폭 갱신을 위한 RectTransform 참조입니다.
                float width = Mathf.Max(0f, _entry.NumericFillMaxWidth) * normalized;
                fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            }
        }
    }
}

/// <summary>
/// Action 위젯 기반 옵션 Row Binder입니다.
/// </summary>
public sealed class ActionOptionRowBinder : IOptionRowBinder
{
    private readonly OptionRowBindingEntry _entry; // Action 바인딩에 사용할 Inspector 설정 항목입니다.

    public E_OptionBindingKey BindingKey => _entry.BindingKey;

    /// <summary>
    /// Action Row Binder를 생성합니다.
    /// </summary>
    public ActionOptionRowBinder(OptionRowBindingEntry entry)
    {
        _entry = entry;
    }

    /// <summary>
    /// Action Row는 저장값 반영 대상이 아니므로 UI 반영을 수행하지 않습니다.
    /// </summary>
    public void ApplyToWidget(OptionSaveData optionData)
    {
    }

    /// <summary>
    /// Action Row는 저장값 생성 대상이 아니므로 버튼 참조만 검사합니다.
    /// </summary>
    public bool TryWriteToData(ref OptionSaveData optionData, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (_entry.ActionWidget == null)
        {
            errorMessage = $"[ActionOptionRowBinder] Action 위젯이 비어 있습니다. key={_entry.BindingKey}";
            return false;
        }

        return true;
    }
}
