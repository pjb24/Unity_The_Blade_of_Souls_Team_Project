using System;
using UnityEngine;

/// <summary>
/// 옵션 저장 데이터를 검증하고 기본값 기반으로 복구하는 유틸리티입니다.
/// </summary>
public static class OptionDataValidator
{
    /// <summary>
    /// 로드된 옵션 데이터를 검증하여 복구 가능한 형태로 반환합니다.
    /// </summary>
    public static OptionSaveData Sanitize(OptionSaveData loadedData, OptionDefaultProfile profile, string rawJson, UnityEngine.Object logContext)
    {
        OptionSaveData defaultData = profile.CreateDefaultSaveData(); // 복구 불가능 항목의 기본값 기준 데이터입니다.
        OptionSaveData sanitized = loadedData ?? profile.CreateDefaultSaveData(); // null 입력을 방어하기 위한 정규화 대상 데이터입니다.

        if (loadedData == null)
        {
            Debug.LogWarning("[OptionDataValidator] 저장 데이터가 null이라 기본값으로 초기화합니다.", logContext);
            return defaultData;
        }

        if (sanitized.SchemaVersion != profile.SchemaVersion)
        {
            Debug.LogWarning($"[OptionDataValidator] 스키마 버전 불일치로 기본값 기반 복구를 수행합니다. loaded={sanitized.SchemaVersion}, expected={profile.SchemaVersion}", logContext);
            sanitized.SchemaVersion = profile.SchemaVersion;
        }

        ValidateDisplay(ref sanitized, in defaultData, profile, rawJson, logContext);
        ValidateAudio(ref sanitized, in defaultData, profile, rawJson, logContext);
        ValidateInput(ref sanitized, in defaultData, rawJson, logContext);
        ValidateAccessibility(ref sanitized, in defaultData, profile, rawJson, logContext);
        ValidateGameplay(ref sanitized, in defaultData, rawJson, logContext);

        return sanitized;
    }

    /// <summary>
    /// Display 탭 데이터를 검증하고 복구합니다.
    /// </summary>
    private static void ValidateDisplay(ref OptionSaveData target, in OptionSaveData defaults, OptionDefaultProfile profile, string rawJson, UnityEngine.Object logContext)
    {
        if (!Enum.IsDefined(typeof(E_OptionScreenMode), target.Display.ScreenMode))
        {
            target.Display.ScreenMode = defaults.Display.ScreenMode;
            Debug.LogWarning("[OptionDataValidator] Display.ScreenMode 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!HasProperty(rawJson, "Width") || target.Display.Resolution.Width <= 0)
        {
            target.Display.Resolution.Width = defaults.Display.Resolution.Width;
            Debug.LogWarning("[OptionDataValidator] Display.Resolution.Width 값이 없거나 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!HasProperty(rawJson, "Height") || target.Display.Resolution.Height <= 0)
        {
            target.Display.Resolution.Height = defaults.Display.Resolution.Height;
            Debug.LogWarning("[OptionDataValidator] Display.Resolution.Height 값이 없거나 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!HasProperty(rawJson, "RefreshRate") || target.Display.Resolution.RefreshRate <= 0)
        {
            target.Display.Resolution.RefreshRate = defaults.Display.Resolution.RefreshRate;
            Debug.LogWarning("[OptionDataValidator] Display.Resolution.RefreshRate 값이 없거나 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!Enum.IsDefined(typeof(E_OptionVSyncMode), target.Display.VSync))
        {
            target.Display.VSync = defaults.Display.VSync;
            Debug.LogWarning("[OptionDataValidator] Display.VSync 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!Enum.IsDefined(typeof(E_OptionFrameLimit), target.Display.FrameLimit))
        {
            target.Display.FrameLimit = defaults.Display.FrameLimit;
            Debug.LogWarning("[OptionDataValidator] Display.FrameLimit 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!HasProperty(rawJson, "GammaBrightness"))
        {
            target.Display.GammaBrightness = defaults.Display.GammaBrightness;
            Debug.LogWarning("[OptionDataValidator] Display.GammaBrightness 값이 누락되어 기본값으로 복구했습니다.", logContext);
        }
        else
        {
            float clampedGamma = profile.GammaBrightnessSetting.Clamp(target.Display.GammaBrightness); // 범위 메타데이터 기반 감마 보정값입니다.
            if (!Mathf.Approximately(target.Display.GammaBrightness, clampedGamma))
            {
                target.Display.GammaBrightness = clampedGamma;
                Debug.LogWarning("[OptionDataValidator] Display.GammaBrightness 값이 범위를 벗어나 보정했습니다.", logContext);
            }
        }

        if (!Enum.IsDefined(typeof(E_OptionGraphicsPreset), target.Display.GraphicsPreset))
        {
            target.Display.GraphicsPreset = defaults.Display.GraphicsPreset;
            Debug.LogWarning("[OptionDataValidator] Display.GraphicsPreset 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!Enum.IsDefined(typeof(E_OptionGraphicsDetailMode), target.Display.GraphicsDetailMode))
        {
            target.Display.GraphicsDetailMode = defaults.Display.GraphicsDetailMode;
            Debug.LogWarning("[OptionDataValidator] Display.GraphicsDetailMode 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        target.Display.TextureQualityLevel = Mathf.Max(0, target.Display.TextureQualityLevel);
        target.Display.ShadowQualityLevel = Mathf.Max(0, target.Display.ShadowQualityLevel);
        target.Display.PostProcessingQualityLevel = Mathf.Max(0, target.Display.PostProcessingQualityLevel);
    }

    /// <summary>
    /// Audio 탭 데이터를 검증하고 복구합니다.
    /// </summary>
    private static void ValidateAudio(ref OptionSaveData target, in OptionSaveData defaults, OptionDefaultProfile profile, string rawJson, UnityEngine.Object logContext)
    {
        ValidateNumericField(rawJson, "MasterVolume", ref target.Audio.MasterVolume, defaults.Audio.MasterVolume, profile.MasterVolumeSetting, "Audio.MasterVolume", logContext);
        ValidateNumericField(rawJson, "BgmVolume", ref target.Audio.BgmVolume, defaults.Audio.BgmVolume, profile.BgmVolumeSetting, "Audio.BgmVolume", logContext);
        ValidateNumericField(rawJson, "SfxVolume", ref target.Audio.SfxVolume, defaults.Audio.SfxVolume, profile.SfxVolumeSetting, "Audio.SfxVolume", logContext);
    }

    /// <summary>
    /// Input 탭 데이터를 검증하고 복구합니다.
    /// </summary>
    private static void ValidateInput(ref OptionSaveData target, in OptionSaveData defaults, string rawJson, UnityEngine.Object logContext)
    {
        if (!Enum.IsDefined(typeof(E_OptionInputHoldBehavior), target.Input.InputHoldBehavior))
        {
            target.Input.InputHoldBehavior = defaults.Input.InputHoldBehavior;
            Debug.LogWarning("[OptionDataValidator] Input.InputHoldBehavior 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!HasProperty(rawJson, "KeyBindings") || target.Input.KeyBindings == null)
        {
            target.Input.KeyBindings = defaults.Input.KeyBindings ?? Array.Empty<KeyBindingEntry>();
            Debug.LogWarning("[OptionDataValidator] Input.KeyBindings 값이 누락되어 기본값으로 복구했습니다.", logContext);
        }
    }

    /// <summary>
    /// Accessibility 탭 데이터를 검증하고 복구합니다.
    /// </summary>
    private static void ValidateAccessibility(ref OptionSaveData target, in OptionSaveData defaults, OptionDefaultProfile profile, string rawJson, UnityEngine.Object logContext)
    {
        if (!Enum.IsDefined(typeof(E_OptionSubtitleEnabled), target.Accessibility.SubtitleEnabled))
        {
            target.Accessibility.SubtitleEnabled = defaults.Accessibility.SubtitleEnabled;
            Debug.LogWarning("[OptionDataValidator] Accessibility.SubtitleEnabled 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!Enum.IsDefined(typeof(E_OptionSubtitleSize), target.Accessibility.SubtitleSize))
        {
            target.Accessibility.SubtitleSize = defaults.Accessibility.SubtitleSize;
            Debug.LogWarning("[OptionDataValidator] Accessibility.SubtitleSize 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        ValidateNumericField(rawJson, "SubtitleSpeed", ref target.Accessibility.SubtitleSpeed, defaults.Accessibility.SubtitleSpeed, profile.SubtitleSpeedSetting, "Accessibility.SubtitleSpeed", logContext);
        ValidateNumericField(rawJson, "UiScale", ref target.Accessibility.UiScale, defaults.Accessibility.UiScale, profile.UiScaleSetting, "Accessibility.UiScale", logContext);
        ValidateNumericField(rawJson, "CameraShake", ref target.Accessibility.CameraShake, defaults.Accessibility.CameraShake, profile.CameraShakeSetting, "Accessibility.CameraShake", logContext);
        ValidateNumericField(rawJson, "ScreenShake", ref target.Accessibility.ScreenShake, defaults.Accessibility.ScreenShake, profile.ScreenShakeSetting, "Accessibility.ScreenShake", logContext);
        ValidateNumericField(rawJson, "ShakeReduction", ref target.Accessibility.ShakeReduction, defaults.Accessibility.ShakeReduction, profile.ShakeReductionSetting, "Accessibility.ShakeReduction", logContext);

        if (!Enum.IsDefined(typeof(E_OptionFlashReduction), target.Accessibility.FlashReduction))
        {
            target.Accessibility.FlashReduction = defaults.Accessibility.FlashReduction;
            Debug.LogWarning("[OptionDataValidator] Accessibility.FlashReduction 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!Enum.IsDefined(typeof(E_OptionColorBlindMode), target.Accessibility.ColorBlindMode))
        {
            target.Accessibility.ColorBlindMode = defaults.Accessibility.ColorBlindMode;
            Debug.LogWarning("[OptionDataValidator] Accessibility.ColorBlindMode 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!Enum.IsDefined(typeof(E_OptionHighContrast), target.Accessibility.HighContrastMode))
        {
            target.Accessibility.HighContrastMode = defaults.Accessibility.HighContrastMode;
            Debug.LogWarning("[OptionDataValidator] Accessibility.HighContrastMode 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }
    }

    /// <summary>
    /// Gameplay 탭 데이터를 검증하고 복구합니다.
    /// </summary>
    private static void ValidateGameplay(ref OptionSaveData target, in OptionSaveData defaults, string rawJson, UnityEngine.Object logContext)
    {
        if (!Enum.IsDefined(typeof(E_OptionDifficulty), target.Gameplay.Difficulty))
        {
            target.Gameplay.Difficulty = defaults.Gameplay.Difficulty;
            Debug.LogWarning("[OptionDataValidator] Gameplay.Difficulty 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!Enum.IsDefined(typeof(E_OptionAutoSaveNotification), target.Gameplay.AutoSaveNotification))
        {
            target.Gameplay.AutoSaveNotification = defaults.Gameplay.AutoSaveNotification;
            Debug.LogWarning("[OptionDataValidator] Gameplay.AutoSaveNotification 값이 잘못되어 기본값으로 복구했습니다.", logContext);
        }

        if (!HasProperty(rawJson, "Gameplay"))
        {
            target.Gameplay = defaults.Gameplay;
            Debug.LogWarning("[OptionDataValidator] Gameplay 섹션이 누락되어 기본값으로 복구했습니다.", logContext);
        }
    }

    /// <summary>
    /// Numeric 필드 누락/범위를 검증하고 필요 시 기본값 또는 보정값을 적용합니다.
    /// </summary>
    private static void ValidateNumericField(string rawJson, string propertyName, ref float targetValue, float defaultValue, OptionNumericSetting setting, string logPath, UnityEngine.Object logContext)
    {
        if (!HasProperty(rawJson, propertyName))
        {
            targetValue = defaultValue;
            Debug.LogWarning($"[OptionDataValidator] {logPath} 값이 누락되어 기본값으로 복구했습니다.", logContext);
            return;
        }

        float clampedValue = setting.Clamp(targetValue); // 메타데이터 범위를 기준으로 보정한 값입니다.
        if (!Mathf.Approximately(clampedValue, targetValue))
        {
            targetValue = clampedValue;
            Debug.LogWarning($"[OptionDataValidator] {logPath} 값이 범위를 벗어나 보정했습니다.", logContext);
        }
    }

    /// <summary>
    /// 원본 JSON 안에 특정 프로퍼티 문자열이 있는지 확인합니다.
    /// </summary>
    private static bool HasProperty(string rawJson, string propertyName)
    {
        return !string.IsNullOrWhiteSpace(rawJson) && rawJson.Contains($"\"{propertyName}\"");
    }
}
