using System;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 글로벌 옵션 저장과 Host 권한 슬롯 플레이 저장을 분리해서 관리하는 저장소입니다.
/// </summary>
public class SaveDataStore : MonoBehaviour
{
    public const int CurrentVersion = 1;
    public const int RequiredSlotCount = 3;

    [Header("File Rules")]
    [Tooltip("Unity의 Application.persistentDataPath 아래에 생성할 저장 폴더 이름입니다.")]
    [SerializeField] private string _saveFolderName = "SaveData"; // 모든 저장 파일을 모아둘 루트 폴더 이름입니다.

    [Tooltip("슬롯과 무관하게 하나만 존재하는 글로벌 옵션 저장 파일 이름입니다.")]
    [SerializeField] private string _globalOptionsFileName = "GlobalOptionsSave.json"; // 모든 슬롯이 공유하는 로컬 옵션 저장 파일 이름입니다.

    [Tooltip("슬롯별 플레이 저장 파일 이름 규칙입니다. {0}은 1, 2, 3 중 하나의 슬롯 번호로 치환됩니다.")]
    [SerializeField] private string _slotFileNameFormat = "Slot_{0}_Save.json"; // 슬롯 플레이 저장 파일 이름을 만드는 규칙입니다.

    [Header("Defaults")]
    [Tooltip("글로벌 옵션 저장 파일이 없거나 손상되었을 때 사용할 디자이너 설정 기본값 프로필입니다.")]
    [SerializeField] private OptionDefaultProfile _optionDefaultProfile; // 글로벌 옵션 기본값을 생성하는 프로필입니다.

    [Tooltip("슬롯 저장 파일이 없거나 손상되었을 때 사용할 기본 플레이 데이터입니다.")]
    [SerializeField] private SlotPlaySaveData _defaultPlayData = SaveRuntimeData.CreateDefaultSlotData(CurrentVersion, 1); // 새 슬롯 파일을 만들 때 기준으로 사용할 플레이 데이터 템플릿입니다.

    [Header("Runtime Bridges")]
    [Tooltip("로드한 글로벌 옵션을 적용할 OptionManager입니다. 비어 있으면 OptionManager.Instance를 사용합니다.")]
    [SerializeField] private OptionManager _optionManager; // 옵션 런타임 상태와 저장 데이터를 연결하는 브리지입니다.

    [Tooltip("로드한 슬롯 진행도를 적용할 StageProgressRuntime입니다. 비어 있으면 현재 씬의 기존 인스턴스를 찾습니다.")]
    [SerializeField] private StageProgressRuntime _stageProgressRuntime; // 스테이지 진행 런타임 상태와 슬롯 저장 데이터를 연결하는 브리지입니다.

    [Tooltip("로드한 슬롯 세션 데이터를 적용할 StageSession입니다. 비어 있으면 현재 씬의 기존 인스턴스를 찾습니다.")]
    [SerializeField] private StageSession _stageSession; // 스테이지 세션 런타임 상태와 슬롯 저장 데이터를 연결하는 브리지입니다.

    [Header("Slot Settings")]
    [Tooltip("저장 슬롯 개수입니다. 요구사항상 3개로 고정되며 다른 값이 들어오면 OnValidate에서 3으로 보정합니다.")]
    [SerializeField] private int _slotCount = RequiredSlotCount; // 인스펙터에 노출하지만 항상 3으로 유지해야 하는 슬롯 개수입니다.

    [Tooltip("현재 선택된 플레이 저장 슬롯입니다.")]
    [SerializeField] private E_SaveSlot _currentSlot = E_SaveSlot.Slot1; // 명시 슬롯이 없는 저장/로드 래퍼에서 사용할 활성 슬롯입니다.

    [Header("Lifecycle")]
    [Tooltip("씬 전환 후에도 저장소 GameObject를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // 저장 서비스의 생명주기를 씬 전환과 분리할지 결정합니다.

    [Tooltip("Awake 생명주기에서 글로벌 옵션과 현재 선택 슬롯을 자동 로드할지 여부입니다.")]
    [SerializeField] private bool _autoLoadOnAwake = true; // 초기 로드 수행 여부입니다.

    [Tooltip("런타임 데이터가 변경되면 짧은 지연 후 자동 저장할지 여부입니다.")]
    [SerializeField] private bool _autoSaveDirtyData = true; // 자동 저장 사용 여부입니다.

    [Tooltip("반복되는 자동 저장 요청을 하나로 합치기 위해 대기할 시간입니다.")]
    [Min(0.1f)]
    [SerializeField] private float _autoSaveDelaySeconds = 1f; // 자동 저장 요청 병합에 사용하는 지연 시간입니다.

    [Header("Debug")]
    [Tooltip("시작 시점과 Inspector 요청 시 저장 파일 경로를 로그로 출력할지 여부입니다.")]
    [SerializeField] private bool _showSavePathDebug = false; // 저장 경로 디버그 출력을 켜고 끕니다.

    [Tooltip("가장 최근 저장/로드 작업의 성공 여부입니다.")]
    [SerializeField] private bool _lastOperationSucceeded; // 마지막 저장/로드 작업의 성공 여부입니다.

    [Tooltip("가장 최근 저장/로드 작업의 결과 메시지입니다.")]
    [SerializeField] private string _lastOperationMessage; // 마지막 저장/로드 작업의 진단 메시지입니다.

    private readonly SaveRuntimeData _runtimeData = new SaveRuntimeData(); // 옵션과 현재 슬롯 데이터를 보관하는 메모리 저장 상태입니다.
    private Action<SaveGameData> _changedListeners; // 리스너 등록과 해제 메서드로만 관리하는 변경 알림 리스너 체인입니다.
    private Coroutine _pendingAutoSaveCoroutine; // 지연 실행 중인 자동 저장 코루틴 핸들입니다.
    private bool _isApplyingLoadedData; // 로드 데이터 적용 중 변경 콜백이 되돌아오는 것을 막는 플래그입니다.

    public static SaveDataStore Instance { get; private set; }
    public string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, _saveFolderName);
    public string GlobalOptionsFilePath => Path.Combine(SaveDirectoryPath, _globalOptionsFileName);
    public string SaveFilePath => GetSlotFilePath(_currentSlot);
    public SaveRuntimeData RuntimeData => _runtimeData;

    /// <summary>
    /// 싱글톤을 구성하고 필요 시 저장된 데이터를 로드합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SaveDataStore] Duplicate instance was destroyed.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        ResolveRuntimeReferences();
        InitializeRuntimeDefaults();
        BindRuntimeListeners(true);

        if (_showSavePathDebug)
        {
            LogSaveFilePath();
        }

        if (_autoLoadOnAwake)
        {
            LoadGlobalOptions("Awake.AutoLoadGlobalOptions");
            LoadSlot(_currentSlot, "Awake.AutoLoadSlot");
        }
    }

    /// <summary>
    /// 애플리케이션 종료 전에 로컬 옵션과 Host 권한 플레이 데이터를 저장합니다.
    /// </summary>
    private void OnApplicationQuit()
    {
        SaveGlobalOptions("ApplicationQuit.GlobalOptions");
        SaveSlot(_currentSlot, "ApplicationQuit.PlaySlot");
    }

    /// <summary>
    /// 다른 부트스트랩 시스템의 Awake가 끝난 뒤 런타임 브리지를 다시 찾습니다.
    /// </summary>
    private void Start()
    {
        ResolveRuntimeReferences();
        BindRuntimeListeners(true);
        ApplyRuntimeOptionsToSystems("Start.ResolveReferences");
        ApplyRuntimePlayDataToSystems("Start.ResolveReferences");
    }

    /// <summary>
    /// 등록된 리스너를 해제하고 싱글톤 참조를 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        BindRuntimeListeners(false);

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 저장 요구사항상 고정되어야 하는 Inspector 값을 검증하고 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_slotCount != RequiredSlotCount)
        {
            Debug.LogWarning($"[SaveDataStore] Slot count must be {RequiredSlotCount}. Inspector value was restored from {_slotCount}.", this);
            _slotCount = RequiredSlotCount;
        }

        if (string.IsNullOrWhiteSpace(_globalOptionsFileName))
        {
            Debug.LogWarning("[SaveDataStore] Global options file name was empty. Fallback file name was restored.", this);
            _globalOptionsFileName = "GlobalOptionsSave.json";
        }

        if (string.IsNullOrWhiteSpace(_slotFileNameFormat) || !_slotFileNameFormat.Contains("{0}"))
        {
            Debug.LogWarning("[SaveDataStore] Slot file name format was invalid. Fallback format was restored.", this);
            _slotFileNameFormat = "Slot_{0}_Save.json";
        }

        if (_defaultPlayData == null)
        {
            _defaultPlayData = SaveRuntimeData.CreateDefaultSlotData(CurrentVersion, (int)_currentSlot);
        }

        _defaultPlayData.Version = CurrentVersion;
        _defaultPlayData.SlotIndex = (int)_currentSlot;
        if (_defaultPlayData.OwnedItemIds == null)
        {
            _defaultPlayData.OwnedItemIds = new System.Collections.Generic.List<string>();
        }

        if (_defaultPlayData.UnlockedIds == null)
        {
            _defaultPlayData.UnlockedIds = new System.Collections.Generic.List<string>();
        }
    }

    /// <summary>
    /// 저장 데이터 변경 알림을 받을 리스너를 등록합니다.
    /// </summary>
    public void AddListener(Action<SaveGameData> listener)
    {
        _changedListeners += listener;
    }

    /// <summary>
    /// 저장 데이터 변경 알림 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(Action<SaveGameData> listener)
    {
        _changedListeners -= listener;
    }

    /// <summary>
    /// 현재 사용 중인 글로벌 옵션 경로와 선택 슬롯 저장 경로를 로그로 출력합니다.
    /// </summary>
    public void LogSaveFilePath()
    {
        Debug.Log($"[SaveDataStore] Global options path: {GlobalOptionsFilePath}", this);
        Debug.Log($"[SaveDataStore] Current slot path: {SaveFilePath}", this);
    }

    /// <summary>
    /// 현재 선택된 슬롯의 플레이 저장 파일이 존재하는지 반환합니다.
    /// </summary>
    public bool HasSaveFile()
    {
        return HasSlotData(_currentSlot);
    }

    /// <summary>
    /// 현재 선택된 슬롯에 이어하기 가능한 진행 데이터가 있는지 반환합니다.
    /// </summary>
    public bool HasProgressData()
    {
        return HasSlotData(_currentSlot);
    }

    /// <summary>
    /// Returns whether at least one save slot contains valid playable progress data.
    /// </summary>
    public bool HasAnySlotData()
    {
        for (int slotIndex = 1; slotIndex <= RequiredSlotCount; slotIndex++)
        {
            if (HasSlotData((E_SaveSlot)slotIndex))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether the last selected slot points to valid playable progress data.
    /// </summary>
    public bool HasLastUsedSlotData()
    {
        return HasSlotData(_currentSlot);
    }

    /// <summary>
    /// 기본 호출 문맥으로 글로벌 옵션 데이터를 로드합니다.
    /// </summary>
    public bool LoadGlobalOptions()
    {
        return LoadGlobalOptions("Manual.LoadGlobalOptions");
    }

    /// <summary>
    /// 기본 호출 문맥으로 글로벌 옵션 데이터를 저장합니다.
    /// </summary>
    public bool SaveGlobalOptions()
    {
        return SaveGlobalOptions("Manual.SaveGlobalOptions");
    }

    /// <summary>
    /// 기본 호출 문맥으로 지정 슬롯의 플레이 데이터를 로드합니다.
    /// </summary>
    public bool LoadSlot(E_SaveSlot slot)
    {
        return LoadSlot(slot, "Manual.LoadSlot");
    }

    /// <summary>
    /// 기본 호출 문맥으로 지정 슬롯의 플레이 데이터를 저장합니다.
    /// </summary>
    public bool SaveSlot(E_SaveSlot slot)
    {
        return SaveSlot(slot, "Manual.SaveSlot");
    }

    /// <summary>
    /// 지정 슬롯 파일이 존재하고 이어하기 가능한 진행 데이터를 포함하는지 반환합니다.
    /// </summary>
    public bool HasSlotData(E_SaveSlot slot)
    {
        return TryReadValidSlotData(slot, out _);
    }

    /// <summary>
    /// Loads a valid slot into runtime data without creating fallback data for missing or invalid slots.
    /// </summary>
    public bool TryLoadSlotData(E_SaveSlot slot, out SlotPlaySaveData slotData)
    {
        slotData = null;
        if (!TryReadValidSlotData(slot, out SlotPlaySaveData loadedSlotData))
        {
            return false;
        }

        if (!TryValidateSlot(slot, out int slotIndex))
        {
            return false;
        }

        if (!CanUsePlayData($"TryLoadSlotData.{slotIndex}"))
        {
            return false;
        }

        _currentSlot = slot;
        _runtimeData.ApplyPlayData(loadedSlotData);
        ApplyRuntimePlayDataToSystems($"TryLoadSlotData.{slotIndex}");
        SetLastResult(true, $"Slot loaded. slot={slotIndex}, context=TryLoadSlotData");
        Debug.Log($"[SaveDataStore] Slot load completed. slot={slotIndex}, context=TryLoadSlotData", this);
        NotifyChanged();

        slotData = loadedSlotData;
        return true;
    }

    /// <summary>
    /// Loads the last selected valid slot into runtime data.
    /// </summary>
    public bool TryLoadLastUsedSlotData(out SlotPlaySaveData slotData)
    {
        return TryLoadSlotData(_currentSlot, out slotData);
    }

    /// <summary>
    /// 지정 슬롯의 플레이 저장 파일을 삭제하고, 현재 슬롯이면 런타임 플레이 데이터도 초기화합니다.
    /// </summary>
    public bool DeleteSlot(E_SaveSlot slot)
    {
        if (!TryValidateSlot(slot, out int slotIndex))
        {
            return false;
        }

        if (!CanWritePlayData($"DeleteSlot.{slotIndex}"))
        {
            return false;
        }

        string path = GetSlotFilePath(slot);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.LogWarning($"[SaveDataStore] Slot save file was deleted. slot={slotIndex}, path={path}", this);
            }

            if (slot == _currentSlot)
            {
                CreateDefaultRuntimePlayData(slot, "DeleteSlot.CurrentSlot");
                ApplyRuntimePlayDataToSystems("DeleteSlot.CurrentSlot");
            }

            NotifyChanged();
            return true;
        }
        catch (Exception exception)
        {
            SetLastResult(false, exception.Message);
            Debug.LogError($"[SaveDataStore] Failed to delete slot save file. slot={slotIndex}, path={path}, error={exception}", this);
            return false;
        }
    }

    /// <summary>
    /// 현재 선택된 플레이 저장 슬롯을 반환합니다.
    /// </summary>
    public E_SaveSlot GetCurrentSlot()
    {
        return _currentSlot;
    }

    /// <summary>
    /// 유효성 검사를 통과한 슬롯을 현재 플레이 저장 슬롯으로 설정합니다.
    /// </summary>
    public bool SetCurrentSlot(E_SaveSlot slot)
    {
        if (!TryValidateSlot(slot, out int slotIndex))
        {
            return false;
        }

        _currentSlot = slot;
        Debug.Log($"[SaveDataStore] Current slot changed. slot={slotIndex}", this);
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// 파일에 쓰지 않고 현재 런타임 시스템 상태를 메모리 저장 상태로 수집합니다.
    /// </summary>
    public void CaptureFromRuntime(string reason)
    {
        CaptureGlobalOptionsFromRuntime();
        CapturePlayDataFromRuntime();
        NotifyChanged();
    }

    /// <summary>
    /// 기존 호출 호환을 위해 현재 선택 슬롯의 플레이 데이터를 저장합니다.
    /// </summary>
    public bool Save(string triggerContext = "Manual")
    {
        return SaveSlot(_currentSlot, triggerContext);
    }

    /// <summary>
    /// 멀티플레이 Client의 개인 체크포인트 진행도가 Host보다 낮을 때 로컬 슬롯 파일을 갱신합니다.
    /// </summary>
    public bool SaveClientPersonalPlayData(string triggerContext = "ClientPersonalCheckpoint")
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 현재 네트워크 세션 상태를 확인할 NetworkManager입니다.
        if (networkManager == null || !networkManager.IsListening || networkManager.IsServer)
        {
            return SaveSlot(_currentSlot, triggerContext);
        }

        CapturePlayDataFromRuntime();

        try
        {
            string path = GetSlotFilePath(_currentSlot);
            SlotPlaySaveData saveData = _runtimeData.ToSlotPlayData(CurrentVersion, _currentSlot);
            WriteJson(path, saveData);
            SetLastResult(true, $"Client personal slot saved. slot={(int)_currentSlot}, context={triggerContext}");
            Debug.Log($"[SaveDataStore] Client personal slot save completed. slot={(int)_currentSlot}, path={path}, context={triggerContext}", this);
            NotifyChanged();
            return true;
        }
        catch (Exception exception)
        {
            SetLastResult(false, exception.Message);
            Debug.LogError($"[SaveDataStore] Client personal slot save failed. slot={(int)_currentSlot}, path={GetSlotFilePath(_currentSlot)}, context={triggerContext}, error={exception}", this);
            return false;
        }
    }

    /// <summary>
    /// 로컬 글로벌 옵션을 로드하고, 파일이 없으면 기본 옵션 파일을 생성합니다.
    /// </summary>
    public bool LoadGlobalOptions(string triggerContext)
    {
        string path = GlobalOptionsFilePath;
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveDataStore] Global options file was missing. Default options file will be created. path={path}, context={triggerContext}", this);
            GlobalOptionsSaveData defaults = CreateDefaultGlobalOptionsData();
            _runtimeData.ApplyGlobalOptions(defaults);
            ApplyRuntimeOptionsToSystems("GlobalOptionsMissingFile");
            SaveGlobalOptions("GlobalOptionsMissingFile.CreateDefaultFile");
            SetLastResult(false, "Global options missing. Defaults created.");
            NotifyChanged();
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            GlobalOptionsSaveData loadedData = JsonUtility.FromJson<GlobalOptionsSaveData>(json);
            bool usedFallback = loadedData == null || loadedData.Version != CurrentVersion || loadedData.Options == null; // 반환값 결정을 위해 기본값 폴백 여부를 추적합니다.
            GlobalOptionsSaveData sanitizedData = ValidateGlobalOptionsData(loadedData, json, path);
            _runtimeData.ApplyGlobalOptions(sanitizedData);
            ApplyRuntimeOptionsToSystems(triggerContext);
            if (usedFallback)
            {
                SaveGlobalOptions("GlobalOptionsInvalid.CreateDefaultFile");
                SetLastResult(false, "Global options invalid. Defaults created.");
                NotifyChanged();
                return false;
            }

            SetLastResult(true, $"Global options loaded. context={triggerContext}");
            NotifyChanged();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SaveDataStore] Global options load failed. Defaults will be used and the damaged file will be backed up. path={path}, error={exception.Message}", this);
            BackupDamagedFile(path, "GlobalOptionsLoadException");
            GlobalOptionsSaveData defaults = CreateDefaultGlobalOptionsData();
            _runtimeData.ApplyGlobalOptions(defaults);
            ApplyRuntimeOptionsToSystems("GlobalOptionsLoadException");
            SaveGlobalOptions("GlobalOptionsLoadException.CreateDefaultFile");
            SetLastResult(false, exception.Message);
            NotifyChanged();
            return false;
        }
    }

    /// <summary>
    /// 현재 플레이 슬롯과 무관하게 로컬 글로벌 옵션만 저장합니다.
    /// </summary>
    public bool SaveGlobalOptions(string triggerContext)
    {
        CaptureGlobalOptionsFromRuntime();

        try
        {
            GlobalOptionsSaveData saveData = _runtimeData.ToGlobalOptionsData(CurrentVersion);
            WriteJson(GlobalOptionsFilePath, saveData);
            SetLastResult(true, $"Global options saved. context={triggerContext}");
            Debug.Log($"[SaveDataStore] Global options save completed. path={GlobalOptionsFilePath}, context={triggerContext}", this);
            NotifyChanged();
            return true;
        }
        catch (Exception exception)
        {
            SetLastResult(false, exception.Message);
            Debug.LogError($"[SaveDataStore] Global options save failed. path={GlobalOptionsFilePath}, context={triggerContext}, error={exception}", this);
            return false;
        }
    }

    /// <summary>
    /// 유효한 슬롯의 Host 권한 플레이 데이터를 로드합니다.
    /// </summary>
    public bool LoadSlot(E_SaveSlot slot, string triggerContext)
    {
        if (!TryValidateSlot(slot, out int slotIndex))
        {
            return false;
        }

        if (!CanUsePlayData($"LoadSlot.{slotIndex}"))
        {
            return false;
        }

        _currentSlot = slot;
        string path = GetSlotFilePath(slot);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveDataStore] Slot save file was missing. Default slot data file will be created. slot={slotIndex}, path={path}, context={triggerContext}", this);
            CreateDefaultRuntimePlayData(slot, "SlotMissingFile");
            ApplyRuntimePlayDataToSystems("SlotMissingFile");
            SaveSlot(slot, "SlotMissingFile.CreateDefaultFile");
            SetLastResult(false, "Slot missing. Defaults created.");
            NotifyChanged();
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            SlotPlaySaveData loadedData = JsonUtility.FromJson<SlotPlaySaveData>(json);
            bool usedFallback = loadedData == null
                || loadedData.Version != CurrentVersion
                || loadedData.SlotIndex != slotIndex
                || loadedData.StageProgress.Records == null; // 반환값 결정을 위해 기본값 폴백 여부를 추적합니다.
            SlotPlaySaveData sanitizedData = ValidateSlotPlayData(loadedData, json, path, slot);
            _runtimeData.ApplyPlayData(sanitizedData);
            ApplyRuntimePlayDataToSystems(triggerContext);
            if (usedFallback)
            {
                SaveSlot(slot, $"Slot{slotIndex}Invalid.CreateDefaultFile");
                SetLastResult(false, "Slot invalid. Defaults created.");
                NotifyChanged();
                return false;
            }

            SetLastResult(true, $"Slot loaded. slot={slotIndex}, context={triggerContext}");
            Debug.Log($"[SaveDataStore] Slot load completed. slot={slotIndex}, path={path}, context={triggerContext}", this);
            NotifyChanged();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SaveDataStore] Slot load failed. Defaults will be used and the damaged file will be backed up. slot={slotIndex}, path={path}, error={exception.Message}", this);
            BackupDamagedFile(path, $"Slot{slotIndex}LoadException");
            CreateDefaultRuntimePlayData(slot, "SlotLoadException");
            ApplyRuntimePlayDataToSystems("SlotLoadException");
            SaveSlot(slot, "SlotLoadException.CreateDefaultFile");
            SetLastResult(false, exception.Message);
            NotifyChanged();
            return false;
        }
    }

    /// <summary>
    /// 유효한 슬롯의 Host 권한 플레이 데이터를 저장합니다.
    /// </summary>
    public bool SaveSlot(E_SaveSlot slot, string triggerContext)
    {
        if (!TryValidateSlot(slot, out int slotIndex))
        {
            return false;
        }

        if (!CanWritePlayData($"SaveSlot.{slotIndex}"))
        {
            return false;
        }

        _currentSlot = slot;
        CapturePlayDataFromRuntime();

        try
        {
            string path = GetSlotFilePath(slot);
            SlotPlaySaveData saveData = _runtimeData.ToSlotPlayData(CurrentVersion, slot);
            WriteJson(path, saveData);
            SetLastResult(true, $"Slot saved. slot={slotIndex}, context={triggerContext}");
            Debug.Log($"[SaveDataStore] Slot save completed. slot={slotIndex}, path={path}, context={triggerContext}", this);
            NotifyChanged();
            return true;
        }
        catch (Exception exception)
        {
            SetLastResult(false, exception.Message);
            Debug.LogError($"[SaveDataStore] Slot save failed. slot={slotIndex}, path={GetSlotFilePath(slot)}, context={triggerContext}, error={exception}", this);
            return false;
        }
    }

    /// <summary>
    /// 저장소가 사용할 런타임 브리지 오브젝트를 찾습니다.
    /// </summary>
    private void ResolveRuntimeReferences()
    {
        if (_optionManager == null)
        {
            _optionManager = OptionManager.Instance;
        }

        if (_stageProgressRuntime == null && StageProgressRuntime.TryGetExistingInstance(out StageProgressRuntime progressRuntime))
        {
            _stageProgressRuntime = progressRuntime;
        }

        if (_stageSession == null && StageSession.TryGetExistingInstance(out StageSession session))
        {
            _stageSession = session;
        }
    }

    /// <summary>
    /// 파일 로드 전에 메모리 저장 상태를 기본값으로 초기화합니다.
    /// </summary>
    private void InitializeRuntimeDefaults()
    {
        _runtimeData.InitializeFromDefaults(CreateDefaultOptions());
        CreateDefaultRuntimePlayData(_currentSlot, "InitializeRuntimeDefaults");
    }

    /// <summary>
    /// 런타임 시스템의 변경 알림 리스너를 등록하거나 해제합니다.
    /// </summary>
    private void BindRuntimeListeners(bool shouldBind)
    {
        if (_optionManager != null)
        {
            if (shouldBind)
            {
                _optionManager.RemoveListener(HandleOptionsChanged);
                _optionManager.AddListener(HandleOptionsChanged);
            }
            else
            {
                _optionManager.RemoveListener(HandleOptionsChanged);
            }
        }

        if (_stageProgressRuntime != null)
        {
            if (shouldBind)
            {
                _stageProgressRuntime.RemoveListener(HandleStageProgressChanged);
                _stageProgressRuntime.AddListener(HandleStageProgressChanged);
            }
            else
            {
                _stageProgressRuntime.RemoveListener(HandleStageProgressChanged);
            }
        }
    }

    /// <summary>
    /// 로드한 글로벌 옵션을 런타임 시스템에 적용합니다.
    /// </summary>
    private void ApplyRuntimeOptionsToSystems(string reason)
    {
        _isApplyingLoadedData = true;
        ResolveRuntimeReferences();

        if (_optionManager != null)
        {
            _optionManager.SetAllOptions(_runtimeData.Options);
        }
        else
        {
            Debug.LogWarning($"[SaveDataStore] OptionManager was not found. Loaded options stayed only in SaveRuntimeData. reason={reason}", this);
        }

        _isApplyingLoadedData = false;
    }

    /// <summary>
    /// 로드한 슬롯 플레이 데이터를 런타임 시스템에 적용합니다.
    /// </summary>
    private void ApplyRuntimePlayDataToSystems(string reason)
    {
        _isApplyingLoadedData = true;
        ResolveRuntimeReferences();

        if (_stageProgressRuntime != null)
        {
            _stageProgressRuntime.ApplySnapshot(_runtimeData.StageProgress);
        }
        else
        {
            Debug.LogWarning($"[SaveDataStore] StageProgressRuntime was not found. Loaded progress data stayed only in SaveRuntimeData. reason={reason}", this);
        }

        if (_stageSession != null)
        {
            _stageSession.ApplySnapshot(_runtimeData.StageSession);
        }
        else
        {
            Debug.LogWarning($"[SaveDataStore] StageSession was not found. Loaded session data stayed only in SaveRuntimeData. reason={reason}", this);
        }

        _isApplyingLoadedData = false;
    }

    /// <summary>
    /// 로컬 옵션 런타임 상태를 메모리 저장 상태로 수집합니다.
    /// </summary>
    private void CaptureGlobalOptionsFromRuntime()
    {
        ResolveRuntimeReferences();

        if (_optionManager != null)
        {
            _runtimeData.Options = _optionManager.GetCurrentOptions();
        }
    }

    /// <summary>
    /// 현재 슬롯의 플레이 런타임 상태를 메모리 저장 상태로 수집합니다.
    /// </summary>
    private void CapturePlayDataFromRuntime()
    {
        ResolveRuntimeReferences();

        if (_stageProgressRuntime != null)
        {
            _runtimeData.StageProgress = _stageProgressRuntime.CreateSnapshot();
        }

        if (_stageSession != null)
        {
            _runtimeData.StageSession = _stageSession.CreateSnapshot();
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrWhiteSpace(activeSceneName))
        {
            _runtimeData.LastPlayedSceneName = activeSceneName;
        }

        _runtimeData.HasProgress = HasMeaningfulProgress();
    }

    /// <summary>
    /// 글로벌 옵션 저장 데이터를 검증하고 필요하면 기본값으로 폴백합니다.
    /// </summary>
    private GlobalOptionsSaveData ValidateGlobalOptionsData(GlobalOptionsSaveData loadedData, string rawJson, string path)
    {
        if (loadedData == null || loadedData.Version != CurrentVersion || loadedData.Options == null)
        {
            Debug.LogWarning($"[SaveDataStore] Global options file was invalid. Defaults will be used. path={path}, jsonLength={rawJson?.Length ?? 0}", this);
            BackupDamagedFile(path, "GlobalOptionsInvalid");
            return CreateDefaultGlobalOptionsData();
        }

        return loadedData;
    }

    /// <summary>
    /// Reads and validates a slot save file without mutating runtime data or creating fallback files.
    /// </summary>
    private bool TryReadValidSlotData(E_SaveSlot slot, out SlotPlaySaveData slotData)
    {
        slotData = null;
        if (!TryValidateSlot(slot, out int slotIndex))
        {
            return false;
        }

        string path = GetSlotFilePath(slot);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            SlotPlaySaveData loadedData = JsonUtility.FromJson<SlotPlaySaveData>(json);
            bool isValid = loadedData != null
                && loadedData.Version == CurrentVersion
                && loadedData.SlotIndex == slotIndex
                && loadedData.HasProgress
                && loadedData.StageProgress.Records != null;

            if (!isValid)
            {
                return false;
            }

            if (loadedData.LastPlayedSceneName == null)
            {
                loadedData.LastPlayedSceneName = string.Empty;
            }

            if (loadedData.OwnedItemIds == null)
            {
                loadedData.OwnedItemIds = new System.Collections.Generic.List<string>();
            }

            if (loadedData.UnlockedIds == null)
            {
                loadedData.UnlockedIds = new System.Collections.Generic.List<string>();
            }

            slotData = loadedData;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SaveDataStore] Failed to inspect slot data. Treating as no progress. slot={slotIndex}, path={path}, error={exception.Message}", this);
            return false;
        }
    }

    /// <summary>
    /// 슬롯 플레이 저장 데이터를 검증하고 필요하면 기본값으로 폴백합니다.
    /// </summary>
    private SlotPlaySaveData ValidateSlotPlayData(SlotPlaySaveData loadedData, string rawJson, string path, E_SaveSlot slot)
    {
        int slotIndex = (int)slot;
        bool invalid = loadedData == null
            || loadedData.Version != CurrentVersion
            || loadedData.SlotIndex != slotIndex
            || loadedData.StageProgress.Records == null;

        if (invalid)
        {
            Debug.LogWarning($"[SaveDataStore] Slot save file was invalid. Defaults will be used. slot={slotIndex}, path={path}, jsonLength={rawJson?.Length ?? 0}", this);
            BackupDamagedFile(path, $"Slot{slotIndex}Invalid");
            return CreateDefaultSlotPlayData(slot);
        }

        if (loadedData.LastPlayedSceneName == null)
        {
            loadedData.LastPlayedSceneName = string.Empty;
        }

        if (loadedData.OwnedItemIds == null)
        {
            loadedData.OwnedItemIds = new System.Collections.Generic.List<string>();
        }

        if (loadedData.UnlockedIds == null)
        {
            loadedData.UnlockedIds = new System.Collections.Generic.List<string>();
        }
        return loadedData;
    }

    /// <summary>
    /// 기본 글로벌 옵션 저장 데이터를 생성합니다.
    /// </summary>
    private GlobalOptionsSaveData CreateDefaultGlobalOptionsData()
    {
        return new GlobalOptionsSaveData
        {
            Version = CurrentVersion,
            Options = CreateDefaultOptions(),
            LastSavedUnixTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    /// <summary>
    /// 디자이너 기본값 프로필에서 옵션 기본값을 생성합니다.
    /// </summary>
    private OptionSaveData CreateDefaultOptions()
    {
        if (_optionDefaultProfile != null)
        {
            return _optionDefaultProfile.CreateDefaultSaveData();
        }

        Debug.LogWarning("[SaveDataStore] OptionDefaultProfile is missing. Empty option defaults were used.", this);
        return new OptionSaveData();
    }

    /// <summary>
    /// 지정 슬롯에 사용할 기본 플레이 데이터를 생성합니다.
    /// </summary>
    private SlotPlaySaveData CreateDefaultSlotPlayData(E_SaveSlot slot)
    {
        SlotPlaySaveData source = _defaultPlayData ?? SaveRuntimeData.CreateDefaultSlotData(CurrentVersion, (int)slot); // 새 슬롯 파일 생성에 사용할 디자이너 설정 템플릿입니다.
        return new SlotPlaySaveData
        {
            Version = CurrentVersion,
            SlotIndex = (int)slot,
            StageProgress = new StageProgressRuntime.SnapshotData
            {
                Records = source.StageProgress.Records != null
                    ? new System.Collections.Generic.List<StageProgressRecord>(source.StageProgress.Records)
                    : new System.Collections.Generic.List<StageProgressRecord>()
            },
            StageSession = source.StageSession,
            LastPlayedSceneName = source.LastPlayedSceneName ?? string.Empty,
            LastSavedUnixTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            HasProgress = source.HasProgress,
            Currency = Mathf.Max(0, source.Currency),
            OwnedItemIds = source.OwnedItemIds != null
                ? new System.Collections.Generic.List<string>(source.OwnedItemIds)
                : new System.Collections.Generic.List<string>(),
            UnlockedIds = source.UnlockedIds != null
                ? new System.Collections.Generic.List<string>(source.UnlockedIds)
                : new System.Collections.Generic.List<string>(),
            PlayTimeSeconds = Mathf.Max(0f, source.PlayTimeSeconds)
        };
    }

    /// <summary>
    /// 런타임 플레이 데이터를 지정 슬롯의 기본값으로 초기화합니다.
    /// </summary>
    private void CreateDefaultRuntimePlayData(E_SaveSlot slot, string reason)
    {
        Debug.LogWarning($"[SaveDataStore] Runtime slot play data was reset to defaults. slot={(int)slot}, reason={reason}", this);
        _runtimeData.ApplyPlayData(CreateDefaultSlotPlayData(slot));
        NotifyChanged();
    }

    /// <summary>
    /// 현재 런타임 상태에 저장할 의미 있는 슬롯 진행 데이터가 있는지 반환합니다.
    /// </summary>
    private bool HasMeaningfulProgress()
    {
        if (_runtimeData.StageProgress.Records != null && _runtimeData.StageProgress.Records.Count > 0)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_runtimeData.StageSession.SelectedStageId))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 옵션 변경을 감지해 글로벌 옵션 파일만 자동 저장 대상으로 예약합니다.
    /// </summary>
    private void HandleOptionsChanged(OptionSaveData optionData)
    {
        if (_isApplyingLoadedData)
        {
            return;
        }

        _runtimeData.Options = optionData;
        QueueAutoSave("OptionChanged", true);
    }

    /// <summary>
    /// 스테이지 진행도 변경을 감지해 현재 슬롯 파일만 자동 저장 대상으로 예약합니다.
    /// </summary>
    private void HandleStageProgressChanged(string stageId)
    {
        if (_isApplyingLoadedData)
        {
            return;
        }

        CapturePlayDataFromRuntime();
        QueueAutoSave($"StageProgressChanged.{stageId}", false);
    }

    /// <summary>
    /// 글로벌 옵션 또는 슬롯 플레이 데이터 저장을 지연 예약합니다.
    /// </summary>
    private void QueueAutoSave(string reason, bool isGlobalOptions)
    {
        if (!_autoSaveDirtyData)
        {
            NotifyChanged();
            return;
        }

        if (_pendingAutoSaveCoroutine != null)
        {
            StopCoroutine(_pendingAutoSaveCoroutine);
        }

        _pendingAutoSaveCoroutine = StartCoroutine(DelayedAutoSave(reason, isGlobalOptions));
    }

    /// <summary>
    /// 지연된 자동 저장 요청을 실행합니다.
    /// </summary>
    private System.Collections.IEnumerator DelayedAutoSave(string reason, bool isGlobalOptions)
    {
        yield return new WaitForSecondsRealtime(_autoSaveDelaySeconds);
        _pendingAutoSaveCoroutine = null;

        if (isGlobalOptions)
        {
            SaveGlobalOptions($"Auto.{reason}");
        }
        else
        {
            SaveSlot(_currentSlot, $"Auto.{reason}");
        }
    }

    /// <summary>
    /// 외부에서 전달된 슬롯 enum 값이 1~3 범위인지 검증합니다.
    /// </summary>
    private bool TryValidateSlot(E_SaveSlot slot, out int slotIndex)
    {
        slotIndex = (int)slot;
        if (slotIndex < 1 || slotIndex > RequiredSlotCount)
        {
            Debug.LogWarning($"[SaveDataStore] Invalid save slot. Save/load was aborted. slot={slotIndex}, validRange=1-{RequiredSlotCount}", this);
            SetLastResult(false, $"Invalid slot {slotIndex}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 현재 네트워크 역할에서 플레이 데이터를 읽을 수 있는지 반환합니다.
    /// </summary>
    private bool CanUsePlayData(string context)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || networkManager.IsServer)
        {
            return true;
        }

        Debug.LogWarning($"[SaveDataStore] Multiplayer Client cannot directly load host-authoritative play slot data. context={context}", this);
        SetLastResult(false, "Client play data load blocked");
        return false;
    }

    /// <summary>
    /// 현재 네트워크 역할에서 플레이 데이터를 쓸 수 있는지 반환합니다.
    /// </summary>
    private bool CanWritePlayData(string context)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || networkManager.IsServer)
        {
            return true;
        }

        Debug.LogWarning($"[SaveDataStore] Multiplayer Client cannot directly write host-authoritative play slot data. context={context}", this);
        SetLastResult(false, "Client play data write blocked");
        return false;
    }

    /// <summary>
    /// 지정 슬롯의 저장 파일 경로를 생성합니다.
    /// </summary>
    private string GetSlotFilePath(E_SaveSlot slot)
    {
        return Path.Combine(SaveDirectoryPath, string.Format(_slotFileNameFormat, (int)slot));
    }

    /// <summary>
    /// 대상 디렉터리를 먼저 만든 뒤 읽기 쉬운 JSON 형식으로 파일을 씁니다.
    /// </summary>
    private void WriteJson<T>(string path, T data)
    {
        string directoryPath = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("Save directory path is empty.");
        }

        Directory.CreateDirectory(directoryPath);
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// 폴백 데이터를 쓰기 전에 손상된 파일을 백업 파일로 분리합니다.
    /// </summary>
    private void BackupDamagedFile(string path, string reason)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string backupPath = $"{path}.bak_{reason}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Copy(path, backupPath, true);
            Debug.LogWarning($"[SaveDataStore] Damaged save file was backed up. source={path}, backup={backupPath}, reason={reason}", this);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SaveDataStore] Failed to backup damaged save file. path={path}, reason={reason}, error={exception.Message}", this);
        }
    }

    /// <summary>
    /// 인스펙터 디버그 필드에 최근 작업 결과를 기록합니다.
    /// </summary>
    private void SetLastResult(bool succeeded, string message)
    {
        _lastOperationSucceeded = succeeded;
        _lastOperationMessage = message;
    }

    /// <summary>
    /// 등록된 리스너에게 현재 저장 상태 스냅샷을 전달합니다.
    /// </summary>
    private void NotifyChanged()
    {
        _changedListeners?.Invoke(_runtimeData.ToSaveData(CurrentVersion, _currentSlot));
    }
}
