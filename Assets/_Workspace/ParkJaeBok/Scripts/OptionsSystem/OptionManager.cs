using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 옵션 런타임 상태와 SaveSystem 연동을 담당하는 진입점 매니저입니다.
/// </summary>
public class OptionManager : MonoBehaviour, ISaveParticipant
{
    private const int PayloadSchemaVersion = 1;

    [Header("Dependencies")]
    [Tooltip("옵션 기본값과 Numeric 메타데이터를 제공하는 프로필 에셋입니다.")]
    [SerializeField] private OptionDefaultProfile _defaultProfile; // 옵션 기본값/메타데이터를 제공하는 프로필 참조입니다.

    [Tooltip("저장 호출을 위임할 SaveCoordinator입니다. 비어 있으면 Instance를 자동 탐색합니다.")]
    [SerializeField] private SaveCoordinator _saveCoordinator; // 옵션 저장/로드를 호출할 SaveCoordinator 참조입니다.

    [Header("Participant")]
    [Tooltip("SaveSnapshot 레코드에서 옵션 participant를 식별하는 고유 ID입니다.")]
    [SerializeField] private string _participantId = "core.options"; // 옵션 participant 고유 식별자 문자열입니다.

    [Tooltip("Recovery 채널 로드 시 옵션 복원을 허용할지 여부입니다.")]
    [SerializeField] private bool _restoreOnRecovery = false; // Recovery 채널에서 옵션 복원 허용 여부입니다.

    [Header("Runtime Debug")]
    [Tooltip("디버그용: 마지막 로드에서 저장 복원 성공 여부입니다.")]
    [SerializeField] private bool _isLoadedFromSave; // 옵션이 저장 데이터에서 복원되었는지 여부입니다.

    [Tooltip("디버그용: 마지막 Save 호출 성공 여부입니다.")]
    [SerializeField] private bool _lastSaveSucceeded; // 최근 SaveCurrentOptions 호출 성공 여부입니다.

    [Tooltip("디버그용: 마지막 Save 실패 사유입니다.")]
    [SerializeField] private string _lastSaveFailureReason; // 최근 SaveCurrentOptions 실패 사유 문자열입니다.

    private readonly OptionRuntimeState _runtimeState = new OptionRuntimeState(); // 런타임 옵션 스냅샷을 유지하는 메모리 컨테이너입니다.
    private Action<OptionSaveData> _optionChangedListeners; // 옵션 변경 알림 리스너 체인입니다.

    public static OptionManager Instance { get; private set; }

    public string ParticipantId => _participantId;
    public int PayloadVersion => PayloadSchemaVersion;

    /// <summary>
    /// 싱글톤과 기본 의존성을 초기화하고 기본값을 런타임에 적재합니다.
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

        ResolveSaveCoordinator();
        InitializeFromDefaults("Awake.DefaultInitialization", false);
    }

    /// <summary>
    /// 첫 프레임에서 저장 복원이 없었으면 기본값 폴백 상태를 명시적으로 로깅합니다.
    /// </summary>
    private IEnumerator Start()
    {
        yield return null;

        if (_isLoadedFromSave == false)
        {
            LoadCurrentOptions("OptionManager.StartAutoLoad");
        }

        if (_isLoadedFromSave == false)
        {
            Debug.LogWarning("[OptionManager] 저장 옵션을 찾지 못해 기본값을 런타임에 유지합니다.", this);
        }
    }

    /// <summary>
    /// UI/외부 시스템이 현재 옵션 전체 스냅샷을 읽을 때 호출합니다.
    /// </summary>
    public OptionSaveData GetCurrentOptions()
    {
        return _runtimeState.GetSnapshot();
    }


    /// <summary>
    /// UI/외부 시스템이 옵션 전체 스냅샷을 한 번에 교체할 때 호출합니다.
    /// </summary>
    public void SetAllOptions(OptionSaveData optionData)
    {
        OptionSaveData safeData = optionData ?? (_defaultProfile != null ? _defaultProfile.CreateDefaultSaveData() : new OptionSaveData()); // null 입력 방지를 위한 안전 옵션 스냅샷입니다.
        ApplyAndNotify(safeData, "SetAllOptions");
    }

    /// <summary>
    /// UI/외부 시스템이 Display 옵션을 교체할 때 호출합니다.
    /// </summary>
    public void SetDisplayOptions(DisplayOptionsData display)
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 변경 반영 전 현재 스냅샷입니다.
        snapshot.Display = display;
        ApplyAndNotify(snapshot, "SetDisplayOptions");
    }

    /// <summary>
    /// UI/외부 시스템이 Audio 옵션을 교체할 때 호출합니다.
    /// </summary>
    public void SetAudioOptions(AudioOptionsData audio)
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 변경 반영 전 현재 스냅샷입니다.
        snapshot.Audio = audio;
        ApplyAndNotify(snapshot, "SetAudioOptions");
    }

    /// <summary>
    /// UI/외부 시스템이 Input 옵션을 교체할 때 호출합니다.
    /// </summary>
    public void SetInputOptions(InputOptionsData input)
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 변경 반영 전 현재 스냅샷입니다.
        snapshot.Input = input;
        ApplyAndNotify(snapshot, "SetInputOptions");
    }

    /// <summary>
    /// UI/외부 시스템이 Accessibility 옵션을 교체할 때 호출합니다.
    /// </summary>
    public void SetAccessibilityOptions(AccessibilityOptionsData accessibility)
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 변경 반영 전 현재 스냅샷입니다.
        snapshot.Accessibility = accessibility;
        ApplyAndNotify(snapshot, "SetAccessibilityOptions");
    }

    /// <summary>
    /// UI/외부 시스템이 Gameplay 옵션을 교체할 때 호출합니다.
    /// </summary>
    public void SetGameplayOptions(GameplayOptionsData gameplay)
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 변경 반영 전 현재 스냅샷입니다.
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
        ResolveSaveCoordinator();
        if (_saveCoordinator == null)
        {
            _lastSaveSucceeded = false;
            _lastSaveFailureReason = "SaveCoordinator 없음";
            Debug.LogWarning("[OptionManager] SaveCoordinator가 없어 옵션 저장을 수행하지 못했습니다.", this);
            return false;
        }

        bool result = _saveCoordinator.SaveChannel(E_SaveChannelType.Persistent, E_SaveTriggerType.Manual, triggerContext); // Persistent 채널 저장 호출 결과입니다.
        _lastSaveSucceeded = result;
        _lastSaveFailureReason = result ? string.Empty : "SaveChannel 실패";

        if (result == false)
        {
            Debug.LogWarning($"[OptionManager] 옵션 저장 실패. context={triggerContext}", this);
        }

        return result;
    }

    /// <summary>
    /// SaveCoordinator를 통해 Persistent 옵션 로드를 수동 트리거합니다.
    /// </summary>
    public bool LoadCurrentOptions(string triggerContext = "OptionManager.ManualLoad")
    {
        ResolveSaveCoordinator();
        if (_saveCoordinator == null)
        {
            Debug.LogWarning("[OptionManager] SaveCoordinator가 없어 옵션 로드를 수행하지 못했습니다.", this);
            return false;
        }

        bool result = _saveCoordinator.LoadChannel(E_SaveChannelType.Persistent, E_SaveTriggerType.Manual, triggerContext); // Persistent 채널 로드 호출 결과입니다.
        if (result == false)
        {
            Debug.LogWarning($"[OptionManager] 옵션 로드 실패. context={triggerContext}", this);
        }

        return result;
    }

    /// <summary>
    /// Numeric 옵션 메타데이터를 프로필에서 조회합니다.
    /// </summary>
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
    /// SaveSystem 저장 문맥에서 옵션 participant 저장 가능 여부를 반환합니다.
    /// </summary>
    public bool CanSave(in SaveContext context)
    {
        return _defaultProfile != null;
    }

    /// <summary>
    /// 현재 런타임 옵션 상태를 저장 페이로드 JSON으로 직렬화합니다.
    /// </summary>
    public string CaptureAsJson(in SaveContext context)
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // JSON 직렬화 대상으로 사용할 런타임 옵션 스냅샷입니다.
        return JsonUtility.ToJson(snapshot);
    }

    /// <summary>
    /// 저장 페이로드 JSON을 런타임 옵션 상태로 복원합니다.
    /// </summary>
    public void RestoreFromJson(string payloadJson, in SaveContext context)
    {
        if (_defaultProfile == null)
        {
            Debug.LogWarning("[OptionManager] OptionDefaultProfile이 없어 저장 데이터 복원을 건너뜁니다.", this);
            return;
        }

        if (context.ChannelType == E_SaveChannelType.Recovery && _restoreOnRecovery == false)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            Debug.LogWarning("[OptionManager] payloadJson이 비어 있어 기본값으로 폴백합니다.", this);
            InitializeFromDefaults("RestoreFromJson.EmptyPayload", true);
            return;
        }

        OptionSaveData loadedData = null; // JSON 역직렬화 결과를 담을 임시 데이터입니다.

        try
        {
            loadedData = JsonUtility.FromJson<OptionSaveData>(payloadJson);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[OptionManager] payload 역직렬화 실패로 기본값으로 폴백합니다. message={exception.Message}", this);
            InitializeFromDefaults("RestoreFromJson.DeserializeFail", true);
            return;
        }

        OptionSaveData sanitizedData = OptionDataValidator.Sanitize(loadedData, _defaultProfile, payloadJson, this); // 검증 및 복구가 끝난 옵션 데이터입니다.
        _runtimeState.Replace(sanitizedData);
        _isLoadedFromSave = true;

        NotifyChanged();
    }

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
    /// 전달된 옵션 스냅샷을 검증 후 런타임에 반영하고 변경 알림을 발행합니다.
    /// </summary>
    private void ApplyAndNotify(OptionSaveData candidate, string reason)
    {
        string candidateJson = JsonUtility.ToJson(candidate); // 누락 필드 검사에 사용할 임시 JSON 문자열입니다.
        OptionSaveData sanitizedData = OptionDataValidator.Sanitize(candidate, _defaultProfile, candidateJson, this); // 변경 요청 데이터 검증/복구 결과입니다.
        _runtimeState.Replace(sanitizedData);

        Debug.Log($"[OptionManager] 옵션 변경이 반영되었습니다. reason={reason}", this);
        NotifyChanged();
    }

    /// <summary>
    /// 등록된 리스너에게 최신 옵션 스냅샷을 전달합니다.
    /// </summary>
    private void NotifyChanged()
    {
        OptionSaveData snapshot = _runtimeState.GetSnapshot(); // 리스너에게 전달할 최신 런타임 스냅샷입니다.
        _optionChangedListeners?.Invoke(snapshot);
    }

    /// <summary>
    /// SaveCoordinator 참조가 비어 있으면 전역 Instance를 자동 해석합니다.
    /// </summary>
    private void ResolveSaveCoordinator()
    {
        if (_saveCoordinator != null)
        {
            return;
        }

        _saveCoordinator = SaveCoordinator.Instance;
    }
}
