using System;
using UnityEngine;

/// <summary>
/// 화면 모드를 나타내는 옵션 열거형입니다.
/// </summary>
public enum E_OptionScreenMode
{
    Fullscreen = 0,
    Windowed = 1,
    Borderless = 2
}

/// <summary>
/// VSync 모드를 나타내는 옵션 열거형입니다.
/// </summary>
public enum E_OptionVSyncMode
{
    Off = 0,
    EveryVBlank = 1,
    EverySecondVBlank = 2
}

/// <summary>
/// 프레임 제한 모드를 나타내는 옵션 열거형입니다.
/// </summary>
public enum E_OptionFrameLimit
{
    Unlimited = 0,
    Fps30 = 30,
    Fps60 = 60,
    Fps120 = 120,
    Fps144 = 144,
    Fps240 = 240
}

/// <summary>
/// 그래픽 품질 프리셋 열거형입니다.
/// </summary>
public enum E_OptionGraphicsPreset
{
    Low = 0,
    Medium = 1,
    High = 2,
    Ultra = 3,
    Custom = 4
}

/// <summary>
/// 그래픽 품질 상세 모드 열거형입니다.
/// </summary>
public enum E_OptionGraphicsDetailMode
{
    UsePreset = 0,
    UseCustom = 1
}

/// <summary>
/// 입력 유지 방식 열거형입니다.
/// </summary>
public enum E_OptionInputHoldBehavior
{
    Hold = 0,
    Toggle = 1
}

/// <summary>
/// 자막 표시 여부 열거형입니다.
/// </summary>
public enum E_OptionSubtitleEnabled
{
    Off = 0,
    On = 1
}

/// <summary>
/// 자막 크기 열거형입니다.
/// </summary>
public enum E_OptionSubtitleSize
{
    Small = 0,
    Medium = 1,
    Large = 2
}

/// <summary>
/// 깜빡임 효과 감소 모드 열거형입니다.
/// </summary>
public enum E_OptionFlashReduction
{
    Off = 0,
    On = 1
}

/// <summary>
/// 색약 보정 모드 열거형입니다.
/// </summary>
public enum E_OptionColorBlindMode
{
    Off = 0,
    Protanopia = 1,
    Deuteranopia = 2,
    Tritanopia = 3
}

/// <summary>
/// 고대비 모드 열거형입니다.
/// </summary>
public enum E_OptionHighContrast
{
    Off = 0,
    On = 1
}

/// <summary>
/// 난이도 열거형입니다.
/// </summary>
public enum E_OptionDifficulty
{
    Story = 0,
    Normal = 1,
    Hard = 2
}

/// <summary>
/// 자동 저장 알림 표시 여부 열거형입니다.
/// </summary>
public enum E_OptionAutoSaveNotification
{
    Off = 0,
    On = 1
}

/// <summary>
/// 해상도 저장 구조체입니다.
/// </summary>
[Serializable]
public struct OptionResolution
{
    public int Width; // 해상도 가로 픽셀 값입니다.
    public int Height; // 해상도 세로 픽셀 값입니다.
    public int RefreshRate; // 해상도 주사율(Hz) 값입니다.
}

/// <summary>
/// Display 탭 저장 데이터 구조체입니다.
/// </summary>
[Serializable]
public struct DisplayOptionsData
{
    public E_OptionScreenMode ScreenMode; // 화면 모드 저장값입니다.
    public OptionResolution Resolution; // 해상도 저장값입니다.
    public E_OptionVSyncMode VSync; // VSync 저장값입니다.
    public E_OptionFrameLimit FrameLimit; // 프레임 제한 저장값입니다.
    public float GammaBrightness; // 감마/밝기 저장값입니다.
    public E_OptionGraphicsPreset GraphicsPreset; // 그래픽 프리셋 저장값입니다.
    public E_OptionGraphicsDetailMode GraphicsDetailMode; // 프리셋/커스텀 사용 여부 저장값입니다.
    public int TextureQualityLevel; // 커스텀 텍스처 품질 레벨 저장값입니다.
    public int ShadowQualityLevel; // 커스텀 그림자 품질 레벨 저장값입니다.
    public int PostProcessingQualityLevel; // 커스텀 후처리 품질 레벨 저장값입니다.
}

/// <summary>
/// Audio 탭 저장 데이터 구조체입니다.
/// </summary>
[Serializable]
public struct AudioOptionsData
{
    public float MasterVolume; // 마스터 볼륨 저장값입니다.
    public float BgmVolume; // BGM 볼륨 저장값입니다.
    public float SfxVolume; // SFX 볼륨 저장값입니다.
}

/// <summary>
/// 입력 바인딩 1건을 확장 가능하게 보관하는 구조체입니다.
/// </summary>
[Serializable]
public struct KeyBindingEntry
{
    public string ActionId; // 입력 액션 식별자입니다.
    public string BindingPath; // 액션 바인딩 경로 문자열입니다.
}

/// <summary>
/// Input 탭 저장 데이터 구조체입니다.
/// </summary>
[Serializable]
public struct InputOptionsData
{
    public E_OptionInputHoldBehavior InputHoldBehavior; // 입력 유지 방식 저장값입니다.
    public KeyBindingEntry[] KeyBindings; // 키 바인딩 상세 저장 목록입니다.
}

/// <summary>
/// Accessibility 탭 저장 데이터 구조체입니다.
/// </summary>
[Serializable]
public struct AccessibilityOptionsData
{
    public E_OptionSubtitleEnabled SubtitleEnabled; // 자막 on/off 저장값입니다.
    public E_OptionSubtitleSize SubtitleSize; // 자막 크기 저장값입니다.
    public float SubtitleSpeed; // 자막 속도 저장값입니다.
    public float UiScale; // UI 크기 저장값입니다.
    public float CameraShake; // 카메라 흔들림 강도 저장값입니다.
    public float ScreenShake; // 화면 흔들림 강도 저장값입니다.
    public float ShakeReduction; // 흔들림 감소 강도 저장값입니다.
    public E_OptionFlashReduction FlashReduction; // 깜빡임 효과 감소 저장값입니다.
    public E_OptionColorBlindMode ColorBlindMode; // 색약 보정 저장값입니다.
    public E_OptionHighContrast HighContrastMode; // 고대비 모드 저장값입니다.
}

/// <summary>
/// Gameplay 탭 저장 데이터 구조체입니다.
/// </summary>
[Serializable]
public struct GameplayOptionsData
{
    public E_OptionDifficulty Difficulty; // 난이도 저장값입니다.
    public E_OptionAutoSaveNotification AutoSaveNotification; // 자동 저장 알림 표시 저장값입니다.
}

/// <summary>
/// Numeric 옵션 메타데이터 구조체입니다.
/// </summary>
[Serializable]
public struct OptionNumericSetting
{
    public float DefaultValue; // 기본값입니다.
    public float MinValue; // 최소값입니다.
    public float MaxValue; // 최대값입니다.
    public float Step; // 증감 단위입니다.

    /// <summary>
    /// 전달 값이 범위 밖인 경우 경계로 보정합니다.
    /// </summary>
    public float Clamp(float value)
    {
        return Mathf.Clamp(value, MinValue, MaxValue);
    }
}
