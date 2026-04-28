using System;
using UnityEngine;

/// <summary>
/// 옵션 런타임 상태를 관리하는 진입점 매니저입니다.
/// </summary>
public class OptionManager : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("옵션 기본값과 Numeric 메타데이터를 제공하는 프로필 에셋입니다.")]
    [SerializeField] private OptionDefaultProfile _defaultProfile; // 옵션 기본값과 UI 메타데이터를 제공하는 프로필 참조입니다.

    [Header("Runtime Debug")]
    [Tooltip("디버그용: 기본값 또는 외부 입력으로 런타임 옵션이 초기화되었는지 여부입니다.")]
    [SerializeField] private bool _isLoadedFromSave; // 세이브 제거 후에는 SetAllOptions 적용 여부를 확인하는 런타임 표시값입니다.

    [Tooltip("디버그용: 마지막 Save 호출 성공 여부입니다.")]
    [SerializeField] private bool _lastSaveSucceeded; // 저장 기능 제거 후에는 항상 false입니다.

    [Tooltip("디버그용: 마지막 Save 실패 사유입니다.")]
    [SerializeField] private string _lastSaveFailureReason; // 저장 기능 제거 사유를 표시하는 문자열입니다.

    private readonly OptionRuntimeState _runtimeState = new OptionRuntimeState(); // 런타임 옵션 스냅샷을 유지하는 메모리 컨테이너입니다.
    private Action<OptionSaveData> _optionChangedListeners; // 옵션 변경 알림 리스너 체인입니다.

    public static OptionManager Instance { get; private set; }

    /// <summary>
    /// 싱글톤과 기본 옵션 상태를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[OptionManager] 중복 인스턴스를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_defaultProfile == null)
        {
            Debug.LogWarning("[OptionManager] OptionDefaultProfile이 없어 옵션 초기화에 실패했습니다.", this);
            return;
        }

        InitializeFromDefaults("Awake.DefaultInitialization", false);
    }

    /// <summary>
    /// UI나 다른 시스템이 현재 옵션 전체 스냅샷을 읽을 때 호출합니다.
    /// </summary>
    public OptionSaveData GetCurrentOptions()
    {
        return _runtimeState.GetSnapshot();
    }

    /// <summary>
    /// 기본 프로필 기준의 옵션 스냅샷을 생성해 반환합니다.
    /// </summary>
    public bool TryGetDefaultOptions(out OptionSaveData optionData)
    {
        optionData = new OptionSaveData();
        if (_defaultProfile == null)
        {
            Debug.LogWarning("[OptionManager] OptionDefaultProfile이 없어 기본 옵션 스냅샷을 생성할 수 없습니다.", this);
            return false;
        }

        optionData = _defaultProfile.CreateDefaultSaveData();
        return true;
    }

    /// <summary>
    /// UI나 외부 시스템이 옵션 전체 스냅샷을 한 번에 교체할 때 호출합니다.
    /// </summary>
    public void SetAllOptions(OptionSaveData optionData)
    {
        OptionSaveData safeData = optionData ?? (_defaultProfile != null ? _defaultProfile.CreateDefaultSaveData() : new OptionSaveData()); // null 입력 방지용 안전 옵션 데이터입니다.
        ApplyAndNotify(safeData, "SetAllOptions");
    }

    /// <summary>
    /// Display 옵션을 교체합니다.
    /// </summary>
    public void SetDisplayOptions(DisplayOptionsData display)
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 변경 반영 전 현재 옵션 스냅샷입니다.
        snapshot.Display = display;
        ApplyAndNotify(snapshot, "SetDisplayOptions");
    }

    /// <summary>
    /// Audio 옵션을 교체합니다.
    /// </summary>
    public void SetAudioOptions(AudioOptionsData audio)
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 변경 반영 전 현재 옵션 스냅샷입니다.
        snapshot.Audio = audio;
        ApplyAndNotify(snapshot, "SetAudioOptions");
    }

    /// <summary>
    /// Input 옵션을 교체합니다.
    /// </summary>
    public void SetInputOptions(InputOptionsData input)
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 변경 반영 전 현재 옵션 스냅샷입니다.
        snapshot.Input = input;
        ApplyAndNotify(snapshot, "SetInputOptions");
    }

    /// <summary>
    /// Accessibility 옵션을 교체합니다.
    /// </summary>
    public void SetAccessibilityOptions(AccessibilityOptionsData accessibility)
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 변경 반영 전 현재 옵션 스냅샷입니다.
        snapshot.Accessibility = accessibility;
        ApplyAndNotify(snapshot, "SetAccessibilityOptions");
    }

    /// <summary>
    /// Gameplay 옵션을 교체합니다.
    /// </summary>
    public void SetGameplayOptions(GameplayOptionsData gameplay)
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 변경 반영 전 현재 옵션 스냅샷입니다.
        snapshot.Gameplay = gameplay;
        ApplyAndNotify(snapshot, "SetGameplayOptions");
    }

    /// <summary>
    /// 옵션 변경 알림 리스너를 등록합니다.
    /// </summary>
    public void AddListener(Action<OptionSaveData> listener)
    {
        _optionChangedListeners += listener;
    }

    /// <summary>
    /// 옵션 변경 알림 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(Action<OptionSaveData> listener)
    {
        _optionChangedListeners -= listener;
    }

    /// <summary>
    /// 런타임 옵션을 저장 시스템으로 저장합니다.
    /// </summary>
    public bool SaveCurrentOptions(string triggerContext = "OptionManager.ManualSave")
    {
        SaveDataStore saveDataStore = SaveDataStore.Instance; // 옵션 저장 요청을 위임할 단일 저장소입니다.
        if (saveDataStore == null)
        {
            _lastSaveSucceeded = false;
            _lastSaveFailureReason = "SaveDataStore missing";
            Debug.LogWarning($"[OptionManager] SaveDataStore를 찾을 수 없어 옵션 저장을 수행하지 못했습니다. context={triggerContext}", this);
            return false;
        }

        saveDataStore.CaptureFromRuntime(triggerContext);
        _lastSaveSucceeded = saveDataStore.SaveGlobalOptions(triggerContext);
        _lastSaveFailureReason = _lastSaveSucceeded ? string.Empty : "SaveDataStore.SaveGlobalOptions failed";
        return _lastSaveSucceeded;
    }

    /// <summary>
    /// 옵션 로드를 트리거합니다.
    /// </summary>
    public bool LoadCurrentOptions(string triggerContext = "OptionManager.ManualLoad")
    {
        SaveDataStore saveDataStore = SaveDataStore.Instance; // 옵션 로드 요청을 위임할 단일 저장소입니다.
        if (saveDataStore == null)
        {
            Debug.LogWarning($"[OptionManager] SaveDataStore를 찾을 수 없어 옵션 로드를 수행하지 못했습니다. context={triggerContext}", this);
            return false;
        }

        return saveDataStore.LoadGlobalOptions(triggerContext);
    }

    public OptionNumericSetting GetGammaBrightnessMetadata() => _defaultProfile.GammaBrightnessSetting;
    public OptionNumericSetting GetMasterVolumeMetadata() => _defaultProfile.MasterVolumeSetting;
    public OptionNumericSetting GetBgmVolumeMetadata() => _defaultProfile.BgmVolumeSetting;
    public OptionNumericSetting GetSfxVolumeMetadata() => _defaultProfile.SfxVolumeSetting;
    public OptionNumericSetting GetSubtitleSpeedMetadata() => _defaultProfile.SubtitleSpeedSetting;
    public OptionNumericSetting GetUiScaleMetadata() => _defaultProfile.UiScaleSetting;
    public OptionNumericSetting GetCameraShakeMetadata() => _defaultProfile.CameraShakeSetting;
    public OptionNumericSetting GetScreenShakeMetadata() => _defaultProfile.ScreenShakeSetting;
    public OptionNumericSetting GetShakeReductionMetadata() => _defaultProfile.ShakeReductionSetting;

    /// <summary>
    /// 기본 프로필 값을 런타임 상태로 초기화합니다.
    /// </summary>
    private void InitializeFromDefaults(string reason, bool shouldWarn)
    {
        if (_defaultProfile == null)
        {
            return;
        }

        OptionSaveData defaultData = _defaultProfile.CreateDefaultSaveData(); // 런타임 초기화에 사용할 기본 옵션 데이터입니다.
        _runtimeState.Initialize(defaultData);
        _isLoadedFromSave = false;

        if (shouldWarn)
        {
            Debug.LogWarning($"[OptionManager] 옵션 데이터를 기본값으로 초기화했습니다. reason={reason}", this);
        }

        NotifyChanged();
    }

    /// <summary>
    /// 전달된 옵션 스냅샷을 검증해 런타임에 반영하고 변경 알림을 발행합니다.
    /// </summary>
    private void ApplyAndNotify(OptionSaveData candidate, string reason)
    {
        string candidateJson = JsonUtility.ToJson(candidate); // 누락 필드 검사용 임시 JSON 문자열입니다.
        OptionSaveData sanitizedData = OptionDataValidator.Sanitize(candidate, _defaultProfile, candidateJson, this); // 검증과 보정이 끝난 옵션 데이터입니다.
        _runtimeState.Replace(sanitizedData);
        _isLoadedFromSave = true;

        Debug.Log($"[OptionManager] 옵션 변경이 반영되었습니다. reason={reason}", this);
        NotifyChanged();
    }

    /// <summary>
    /// 등록된 리스너에게 최신 옵션 스냅샷을 전달합니다.
    /// </summary>
    private void NotifyChanged()
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 리스너에게 전달할 최신 런타임 옵션 스냅샷입니다.
        _optionChangedListeners?.Invoke(snapshot);
    }
}
