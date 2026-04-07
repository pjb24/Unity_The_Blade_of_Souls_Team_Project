/// <summary>
/// E_OptionBindingKey와 OptionSaveData 필드 간 값을 읽고 쓰는 접근 유틸리티입니다.
/// </summary>
public static class OptionBindingDataAccessor
{
    /// <summary>
    /// 지정 키를 float 값으로 읽습니다.
    /// </summary>
    public static bool TryGetFloat(in OptionSaveData optionData, E_OptionBindingKey key, out float value)
    {
        value = 0f;
        switch (key)
        {
            case E_OptionBindingKey.DisplayGammaBrightness:
                value = optionData.Display.GammaBrightness;
                return true;
            case E_OptionBindingKey.AudioMasterVolume:
                value = optionData.Audio.MasterVolume;
                return true;
            case E_OptionBindingKey.AudioBgmVolume:
                value = optionData.Audio.BgmVolume;
                return true;
            case E_OptionBindingKey.AudioSfxVolume:
                value = optionData.Audio.SfxVolume;
                return true;
            case E_OptionBindingKey.AccessibilitySubtitleSpeed:
                value = optionData.Accessibility.SubtitleSpeed;
                return true;
            case E_OptionBindingKey.AccessibilityUiScale:
                value = optionData.Accessibility.UiScale;
                return true;
            case E_OptionBindingKey.AccessibilityCameraShake:
                value = optionData.Accessibility.CameraShake;
                return true;
            case E_OptionBindingKey.AccessibilityScreenShake:
                value = optionData.Accessibility.ScreenShake;
                return true;
            case E_OptionBindingKey.AccessibilityShakeReduction:
                value = optionData.Accessibility.ShakeReduction;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 지정 키에 float 값을 기록합니다.
    /// </summary>
    public static bool TrySetFloat(ref OptionSaveData optionData, E_OptionBindingKey key, float value)
    {
        switch (key)
        {
            case E_OptionBindingKey.DisplayGammaBrightness:
                optionData.Display.GammaBrightness = value;
                return true;
            case E_OptionBindingKey.AudioMasterVolume:
                optionData.Audio.MasterVolume = value;
                return true;
            case E_OptionBindingKey.AudioBgmVolume:
                optionData.Audio.BgmVolume = value;
                return true;
            case E_OptionBindingKey.AudioSfxVolume:
                optionData.Audio.SfxVolume = value;
                return true;
            case E_OptionBindingKey.AccessibilitySubtitleSpeed:
                optionData.Accessibility.SubtitleSpeed = value;
                return true;
            case E_OptionBindingKey.AccessibilityUiScale:
                optionData.Accessibility.UiScale = value;
                return true;
            case E_OptionBindingKey.AccessibilityCameraShake:
                optionData.Accessibility.CameraShake = value;
                return true;
            case E_OptionBindingKey.AccessibilityScreenShake:
                optionData.Accessibility.ScreenShake = value;
                return true;
            case E_OptionBindingKey.AccessibilityShakeReduction:
                optionData.Accessibility.ShakeReduction = value;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 지정 키를 Dropdown 인덱스(int) 값으로 읽습니다.
    /// </summary>
    public static bool TryGetInt(in OptionSaveData optionData, E_OptionBindingKey key, out int value)
    {
        value = 0;
        switch (key)
        {
            case E_OptionBindingKey.DisplayScreenMode:
                value = (int)optionData.Display.ScreenMode;
                return true;
            case E_OptionBindingKey.DisplayVSync:
                value = (int)optionData.Display.VSync;
                return true;
            case E_OptionBindingKey.DisplayFrameLimit:
                value = ToFrameLimitDropdownIndex(optionData.Display.FrameLimit);
                return true;
            case E_OptionBindingKey.DisplayGraphicsPreset:
                value = (int)optionData.Display.GraphicsPreset;
                return true;
            case E_OptionBindingKey.DisplayGraphicsDetailMode:
                value = (int)optionData.Display.GraphicsDetailMode;
                return true;
            case E_OptionBindingKey.InputHoldBehavior:
                value = (int)optionData.Input.InputHoldBehavior;
                return true;
            case E_OptionBindingKey.AccessibilitySubtitleSize:
                value = (int)optionData.Accessibility.SubtitleSize;
                return true;
            case E_OptionBindingKey.AccessibilitySubtitleEnabled:
                value = (int)optionData.Accessibility.SubtitleEnabled;
                return true;
            case E_OptionBindingKey.AccessibilityFlashReduction:
                value = (int)optionData.Accessibility.FlashReduction;
                return true;
            case E_OptionBindingKey.AccessibilityColorBlindMode:
                value = (int)optionData.Accessibility.ColorBlindMode;
                return true;
            case E_OptionBindingKey.AccessibilityHighContrastMode:
                value = (int)optionData.Accessibility.HighContrastMode;
                return true;
            case E_OptionBindingKey.GameplayDifficulty:
                value = (int)optionData.Gameplay.Difficulty;
                return true;
            case E_OptionBindingKey.GameplayAutoSaveNotification:
                value = (int)optionData.Gameplay.AutoSaveNotification;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 지정 키에 Dropdown 인덱스(int) 값을 기록합니다.
    /// </summary>
    public static bool TrySetInt(ref OptionSaveData optionData, E_OptionBindingKey key, int value)
    {
        switch (key)
        {
            case E_OptionBindingKey.DisplayScreenMode:
                optionData.Display.ScreenMode = (E_OptionScreenMode)value;
                return true;
            case E_OptionBindingKey.DisplayVSync:
                optionData.Display.VSync = (E_OptionVSyncMode)value;
                return true;
            case E_OptionBindingKey.DisplayFrameLimit:
                optionData.Display.FrameLimit = ToFrameLimitEnum(value);
                return true;
            case E_OptionBindingKey.DisplayGraphicsPreset:
                optionData.Display.GraphicsPreset = (E_OptionGraphicsPreset)value;
                return true;
            case E_OptionBindingKey.DisplayGraphicsDetailMode:
                optionData.Display.GraphicsDetailMode = (E_OptionGraphicsDetailMode)value;
                return true;
            case E_OptionBindingKey.InputHoldBehavior:
                optionData.Input.InputHoldBehavior = (E_OptionInputHoldBehavior)value;
                return true;
            case E_OptionBindingKey.AccessibilitySubtitleSize:
                optionData.Accessibility.SubtitleSize = (E_OptionSubtitleSize)value;
                return true;
            case E_OptionBindingKey.AccessibilitySubtitleEnabled:
                optionData.Accessibility.SubtitleEnabled = (E_OptionSubtitleEnabled)value;
                return true;
            case E_OptionBindingKey.AccessibilityFlashReduction:
                optionData.Accessibility.FlashReduction = (E_OptionFlashReduction)value;
                return true;
            case E_OptionBindingKey.AccessibilityColorBlindMode:
                optionData.Accessibility.ColorBlindMode = (E_OptionColorBlindMode)value;
                return true;
            case E_OptionBindingKey.AccessibilityHighContrastMode:
                optionData.Accessibility.HighContrastMode = (E_OptionHighContrast)value;
                return true;
            case E_OptionBindingKey.GameplayDifficulty:
                optionData.Gameplay.Difficulty = (E_OptionDifficulty)value;
                return true;
            case E_OptionBindingKey.GameplayAutoSaveNotification:
                optionData.Gameplay.AutoSaveNotification = (E_OptionAutoSaveNotification)value;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// E_OptionFrameLimit 값을 Dropdown 인덱스로 변환합니다.
    /// </summary>
    private static int ToFrameLimitDropdownIndex(E_OptionFrameLimit frameLimit)
    {
        switch (frameLimit)
        {
            case E_OptionFrameLimit.Unlimited: return 0;
            case E_OptionFrameLimit.Fps30: return 1;
            case E_OptionFrameLimit.Fps60: return 2;
            case E_OptionFrameLimit.Fps120: return 3;
            case E_OptionFrameLimit.Fps144: return 4;
            case E_OptionFrameLimit.Fps240: return 5;
            default: return 0;
        }
    }

    /// <summary>
    /// Dropdown 인덱스를 E_OptionFrameLimit 값으로 변환합니다.
    /// </summary>
    private static E_OptionFrameLimit ToFrameLimitEnum(int dropdownIndex)
    {
        switch (dropdownIndex)
        {
            case 0: return E_OptionFrameLimit.Unlimited;
            case 1: return E_OptionFrameLimit.Fps30;
            case 2: return E_OptionFrameLimit.Fps60;
            case 3: return E_OptionFrameLimit.Fps120;
            case 4: return E_OptionFrameLimit.Fps144;
            case 5: return E_OptionFrameLimit.Fps240;
            default: return E_OptionFrameLimit.Unlimited;
        }
    }
}
