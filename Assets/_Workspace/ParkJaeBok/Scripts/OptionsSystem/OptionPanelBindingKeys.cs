using System;

/// <summary>
/// Options 패널 Row가 어떤 OptionSaveData 필드를 다루는지 식별하는 키입니다.
/// </summary>
public enum E_OptionBindingKey
{
    None = 0,

    // Enum (Display)
    DisplayScreenMode = 100,
    DisplayVSync = 101,
    DisplayFrameLimit = 102,
    DisplayGraphicsPreset = 103,
    DisplayGraphicsDetailMode = 104,

    // Numeric (Display)
    DisplayGammaBrightness = 150,

    // Numeric (Audio)
    AudioMasterVolume = 200,
    AudioBgmVolume = 201,
    AudioSfxVolume = 202,

    // Enum (Input)
    InputHoldBehavior = 300,

    // Enum/Numeric (Accessibility)
    AccessibilitySubtitleEnabled = 400,
    AccessibilitySubtitleSize = 401,
    AccessibilitySubtitleSpeed = 402,
    AccessibilityUiScale = 403,
    AccessibilityCameraShake = 404,
    AccessibilityScreenShake = 405,
    AccessibilityShakeReduction = 406,
    AccessibilityFlashReduction = 407,
    AccessibilityColorBlindMode = 408,
    AccessibilityHighContrastMode = 409,

    // Enum (Gameplay)
    GameplayDifficulty = 500,
    GameplayAutoSaveNotification = 501
}

/// <summary>
/// Row가 사용하는 Unity 위젯 타입 식별자입니다.
/// </summary>
[Serializable]
public enum E_OptionBindingWidgetType
{
    Enum = 0,
    Numeric = 1,
    Action = 2
}
