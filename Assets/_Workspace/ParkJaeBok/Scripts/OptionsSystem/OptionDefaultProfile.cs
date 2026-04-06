using UnityEngine;

/// <summary>
/// 옵션 기본값과 Numeric 메타데이터를 관리하는 프로필 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "OptionDefaultProfile", menuName = "Game/Options/Default Profile")]
public class OptionDefaultProfile : ScriptableObject
{
    [Header("Version")]
    [Tooltip("신규 저장 스냅샷에 기록할 옵션 스키마 버전입니다.")]
    [SerializeField] private int _schemaVersion = 1; // 옵션 저장 스키마 버전 값입니다.

    [Header("Default Option Values")]
    [Tooltip("Display 탭 기본 옵션값입니다.")]
    [SerializeField] private DisplayOptionsData _defaultDisplay; // Display 탭 기본 옵션 데이터입니다.

    [Tooltip("Audio 탭 기본 옵션값입니다.")]
    [SerializeField] private AudioOptionsData _defaultAudio; // Audio 탭 기본 옵션 데이터입니다.

    [Tooltip("Input 탭 기본 옵션값입니다.")]
    [SerializeField] private InputOptionsData _defaultInput; // Input 탭 기본 옵션 데이터입니다.

    [Tooltip("Accessibility 탭 기본 옵션값입니다.")]
    [SerializeField] private AccessibilityOptionsData _defaultAccessibility; // Accessibility 탭 기본 옵션 데이터입니다.

    [Tooltip("Gameplay 탭 기본 옵션값입니다.")]
    [SerializeField] private GameplayOptionsData _defaultGameplay; // Gameplay 탭 기본 옵션 데이터입니다.

    [Header("Numeric Metadata")]
    [Tooltip("Gamma/Brightness 옵션 범위 메타데이터입니다.")]
    [SerializeField] private OptionNumericSetting _gammaBrightnessSetting; // 감마/밝기 범위 메타데이터입니다.

    [Tooltip("Master 볼륨 옵션 범위 메타데이터입니다.")]
    [SerializeField] private OptionNumericSetting _masterVolumeSetting; // 마스터 볼륨 범위 메타데이터입니다.

    [Tooltip("BGM 볼륨 옵션 범위 메타데이터입니다.")]
    [SerializeField] private OptionNumericSetting _bgmVolumeSetting; // BGM 볼륨 범위 메타데이터입니다.

    [Tooltip("SFX 볼륨 옵션 범위 메타데이터입니다.")]
    [SerializeField] private OptionNumericSetting _sfxVolumeSetting; // SFX 볼륨 범위 메타데이터입니다.

    [Tooltip("자막 속도 옵션 범위 메타데이터입니다.")]
    [SerializeField] private OptionNumericSetting _subtitleSpeedSetting; // 자막 속도 범위 메타데이터입니다.

    [Tooltip("UI 크기 옵션 범위 메타데이터입니다.")]
    [SerializeField] private OptionNumericSetting _uiScaleSetting; // UI 크기 범위 메타데이터입니다.

    [Tooltip("카메라 흔들림 옵션 범위 메타데이터입니다.")]
    [SerializeField] private OptionNumericSetting _cameraShakeSetting; // 카메라 흔들림 범위 메타데이터입니다.

    [Tooltip("화면 흔들림 옵션 범위 메타데이터입니다.")]
    [SerializeField] private OptionNumericSetting _screenShakeSetting; // 화면 흔들림 범위 메타데이터입니다.

    [Tooltip("흔들림 감소 옵션 범위 메타데이터입니다.")]
    [SerializeField] private OptionNumericSetting _shakeReductionSetting; // 흔들림 감소 범위 메타데이터입니다.

    public int SchemaVersion => _schemaVersion;

    /// <summary>
    /// 프로필에 설정된 기본값으로 OptionSaveData 인스턴스를 생성합니다.
    /// </summary>
    public OptionSaveData CreateDefaultSaveData()
    {
        return new OptionSaveData
        {
            SchemaVersion = _schemaVersion,
            Display = _defaultDisplay,
            Audio = _defaultAudio,
            Input = _defaultInput,
            Accessibility = _defaultAccessibility,
            Gameplay = _defaultGameplay
        };
    }

    public OptionNumericSetting GammaBrightnessSetting => _gammaBrightnessSetting;
    public OptionNumericSetting MasterVolumeSetting => _masterVolumeSetting;
    public OptionNumericSetting BgmVolumeSetting => _bgmVolumeSetting;
    public OptionNumericSetting SfxVolumeSetting => _sfxVolumeSetting;
    public OptionNumericSetting SubtitleSpeedSetting => _subtitleSpeedSetting;
    public OptionNumericSetting UiScaleSetting => _uiScaleSetting;
    public OptionNumericSetting CameraShakeSetting => _cameraShakeSetting;
    public OptionNumericSetting ScreenShakeSetting => _screenShakeSetting;
    public OptionNumericSetting ShakeReductionSetting => _shakeReductionSetting;
}
