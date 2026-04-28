using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로컬 플레이어 저장 데이터의 단일 진실 원천입니다.
/// </summary>
public class SaveDataStore : MonoBehaviour
{
    public const int CurrentVersion = 1;

    [Header("File")]
    [Tooltip("Application.persistentDataPath 아래에 생성할 저장 폴더 이름입니다.")]
    [SerializeField] private string _saveFolderName = "SaveData"; // 저장 파일을 그룹화할 하위 폴더 이름입니다.

    [Tooltip("로컬 플레이어 진행 데이터를 기록할 JSON 파일 이름입니다.")]
    [SerializeField] private string _saveFileName = "save_data.json"; // 실제 저장 파일 이름입니다.

    [Header("Defaults")]
    [Tooltip("저장 파일이 없거나 손상되었을 때 사용할 옵션 기본값 프로필입니다.")]
    [SerializeField] private OptionDefaultProfile _optionDefaultProfile; // 디자이너가 Inspector에서 조정하는 옵션 기본값 소스입니다.

    [Header("Runtime Bridges")]
    [Tooltip("옵션 런타임 상태를 제공하는 OptionManager입니다. 비어 있으면 Instance를 사용합니다.")]
    [SerializeField] private OptionManager _optionManager; // 저장/로드 시 옵션 런타임과 연결할 매니저입니다.

    [Tooltip("스테이지 진행 런타임 상태입니다. 비어 있으면 Instance를 사용합니다.")]
    [SerializeField] private StageProgressRuntime _stageProgressRuntime; // 저장/로드 시 진행 데이터와 연결할 런타임입니다.

    [Tooltip("이어하기 문맥을 제공하는 StageSession입니다. 비어 있으면 Instance를 사용합니다.")]
    [SerializeField] private StageSession _stageSession; // 저장/로드 시 스테이지 세션 문맥과 연결할 런타임입니다.

    [Header("Lifecycle")]
    [Tooltip("씬 전환 후에도 저장소 GameObject를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // 저장소 생명주기를 씬 전환과 분리할지 여부입니다.

    [Tooltip("Awake 시 저장 파일을 자동 로드할지 여부입니다.")]
    [SerializeField] private bool _autoLoadOnAwake = true; // 부트스트랩 시 저장 파일을 자동 반영할지 여부입니다.

    [Tooltip("중요 데이터 변경 시 짧은 지연 뒤 자동 저장할지 여부입니다.")]
    [SerializeField] private bool _autoSaveDirtyData = true; // 값 변경마다 즉시 파일을 쓰지 않기 위한 자동 저장 옵션입니다.

    [Tooltip("자동 저장 요청을 병합하기 위해 대기할 초 단위 시간입니다.")]
    [Min(0.1f)]
    [SerializeField] private float _autoSaveDelaySeconds = 1f; // 중복 저장을 줄이기 위한 자동 저장 지연 시간입니다.

    [Header("Debug")]
    [Tooltip("최근 저장/로드 성공 여부입니다.")]
    [SerializeField] private bool _lastOperationSucceeded; // Inspector 확인용 최근 작업 성공 플래그입니다.

    [Tooltip("최근 저장/로드 실패 또는 폴백 사유입니다.")]
    [SerializeField] private string _lastOperationMessage; // Inspector 확인용 최근 작업 메시지입니다.

    private readonly SaveRuntimeData _runtimeData = new SaveRuntimeData(); // 현재 세션에서 사용하는 저장 런타임 데이터입니다.
    private Action<SaveGameData> _changedListeners; // 저장 데이터 변경 알림 리스너 체인입니다.
    private Coroutine _pendingAutoSaveCoroutine; // 병합 대기 중인 자동 저장 코루틴입니다.
    private bool _isApplyingLoadedData; // 로드 반영 중 변경 콜백 재진입을 막는 플래그입니다.

    public static SaveDataStore Instance { get; private set; }
    public string SaveFilePath => Path.Combine(Application.persistentDataPath, _saveFolderName, _saveFileName);
    public SaveRuntimeData RuntimeData => _runtimeData;

    /// <summary>
    /// 싱글톤을 구성하고 필요 시 저장 파일을 로드합니다.
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

        if (_autoLoadOnAwake)
        {
            Load();
        }
    }

    /// <summary>
    /// 종료 시 저장 가능한 최신 런타임 데이터를 파일로 기록합니다.
    /// </summary>
    private void OnApplicationQuit()
    {
        CaptureFromRuntime("ApplicationQuit");
        Save("ApplicationQuit");
    }

    /// <summary>
    /// 다른 부트스트랩 매니저의 Awake 이후 런타임 참조와 리스너를 한 번 더 보정합니다.
    /// </summary>
    private void Start()
    {
        ResolveRuntimeReferences();
        BindRuntimeListeners(true);
        ApplyRuntimeDataToSystems("Start.ResolveReferences");
    }

    /// <summary>
    /// 등록한 런타임 리스너를 해제합니다.
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
    /// 저장 데이터 변경 알림 리스너를 등록합니다.
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
    /// 현재 저장 파일 경로를 로그로 출력합니다.
    /// </summary>
    public void LogSaveFilePath()
    {
        Debug.Log($"[SaveDataStore] Save file path: {SaveFilePath}", this);
    }

    /// <summary>
    /// 저장 파일이 존재하는지 반환합니다.
    /// </summary>
    public bool HasSaveFile()
    {
        return File.Exists(SaveFilePath);
    }

    /// <summary>
    /// 이어하기 가능한 진행 데이터가 있는지 반환합니다.
    /// </summary>
    public bool HasProgressData()
    {
        if (_runtimeData.HasProgress)
        {
            return true;
        }

        if (!File.Exists(SaveFilePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath); // 진행 데이터 존재 여부만 확인하기 위한 저장 파일 원문입니다.
            SaveGameData saveData = JsonUtility.FromJson<SaveGameData>(json);
            return saveData != null && saveData.HasProgress;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SaveDataStore] Failed to inspect save progress flag. Treating as no progress. path={SaveFilePath}, error={exception.Message}", this);
            return false;
        }
    }

    /// <summary>
    /// 현재 런타임 시스템에서 저장할 값을 수집합니다.
    /// </summary>
    public void CaptureFromRuntime(string reason)
    {
        ResolveRuntimeReferences();

        if (_optionManager != null)
        {
            _runtimeData.Options = _optionManager.GetCurrentOptions();
        }

        if (_stageProgressRuntime != null)
        {
            _runtimeData.StageProgress = _stageProgressRuntime.CreateSnapshot();
        }

        if (_stageSession != null)
        {
            _runtimeData.StageSession = _stageSession.CreateSnapshot();
        }

        string activeSceneName = SceneManager.GetActiveScene().name; // 마지막 로컬 진행 위치 추적용 씬 이름입니다.
        if (!string.IsNullOrWhiteSpace(activeSceneName))
        {
            _runtimeData.LastPlayedSceneName = activeSceneName;
        }

        _runtimeData.HasProgress = HasMeaningfulProgress();
        NotifyChanged();
    }

    /// <summary>
    /// 현재 런타임 데이터를 JSON 파일로 저장합니다.
    /// </summary>
    public bool Save(string triggerContext = "Manual")
    {
        CaptureFromRuntime(triggerContext);

        try
        {
            string directoryPath = Path.GetDirectoryName(SaveFilePath); // 저장 파일이 위치할 디렉터리입니다.
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                Debug.LogWarning("[SaveDataStore] Save directory path is empty. Save was aborted.", this);
                _lastOperationSucceeded = false;
                _lastOperationMessage = "Empty save directory path";
                return false;
            }

            Directory.CreateDirectory(directoryPath);
            SaveGameData saveData = _runtimeData.ToSaveData(CurrentVersion); // 파일에 기록할 직렬화 대상 데이터입니다.
            string json = JsonUtility.ToJson(saveData, true); // 사람이 읽기 쉬운 JSON 저장 문자열입니다.
            File.WriteAllText(SaveFilePath, json);

            _lastOperationSucceeded = true;
            _lastOperationMessage = $"Saved. context={triggerContext}";
            Debug.Log($"[SaveDataStore] Save completed. path={SaveFilePath}, context={triggerContext}", this);
            return true;
        }
        catch (Exception exception)
        {
            _lastOperationSucceeded = false;
            _lastOperationMessage = exception.Message;
            Debug.LogError($"[SaveDataStore] Save failed. path={SaveFilePath}, context={triggerContext}, error={exception}", this);
            return false;
        }
    }

    /// <summary>
    /// JSON 파일을 읽고 검증한 뒤 필요한 값만 런타임 데이터와 시스템에 반영합니다.
    /// </summary>
    public bool Load(string triggerContext = "Manual")
    {
        if (!File.Exists(SaveFilePath))
        {
            Debug.LogWarning($"[SaveDataStore] Save file was not found. Default save data was created in memory. path={SaveFilePath}, context={triggerContext}", this);
            CreateDefaultRuntimeData("MissingFile");
            ApplyRuntimeDataToSystems("MissingFile");
            _lastOperationSucceeded = false;
            _lastOperationMessage = "Save file missing. Defaults created in memory.";
            return false;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath); // 역직렬화할 저장 파일 원문입니다.
            SaveGameData loadedData = JsonUtility.FromJson<SaveGameData>(json);
            SaveGameData sanitizedData = ValidateAndRepairLoadedData(loadedData, json);

            _runtimeData.ApplyLoadedData(sanitizedData);
            ApplyRuntimeDataToSystems(triggerContext);

            _lastOperationSucceeded = true;
            _lastOperationMessage = $"Loaded. context={triggerContext}";
            Debug.Log($"[SaveDataStore] Load completed. path={SaveFilePath}, context={triggerContext}", this);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SaveDataStore] Load failed. Default save data was created in memory. path={SaveFilePath}, context={triggerContext}, error={exception}", this);
            CreateDefaultRuntimeData("LoadException");
            ApplyRuntimeDataToSystems("LoadException");
            _lastOperationSucceeded = false;
            _lastOperationMessage = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// 저장 파일과 런타임 저장 데이터를 기본값으로 초기화합니다.
    /// </summary>
    public bool ResetSaveData(bool deleteFile, string triggerContext = "ManualReset")
    {
        CreateDefaultRuntimeData(triggerContext);
        ApplyRuntimeDataToSystems(triggerContext);

        if (deleteFile && File.Exists(SaveFilePath))
        {
            try
            {
                File.Delete(SaveFilePath);
                Debug.LogWarning($"[SaveDataStore] Save file was deleted by reset. path={SaveFilePath}, context={triggerContext}", this);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveDataStore] Failed to delete save file during reset. path={SaveFilePath}, error={exception}", this);
                return false;
            }
        }

        if (deleteFile)
        {
            return true;
        }

        return Save(triggerContext);
    }

    /// <summary>
    /// Inspector/Button에서 강제 저장을 실행합니다.
    /// </summary>
    public void ForceSaveFromInspector()
    {
        Save("Inspector.ForceSave");
    }

    /// <summary>
    /// Inspector/Button에서 강제 로드를 실행합니다.
    /// </summary>
    public void ForceLoadFromInspector()
    {
        Load("Inspector.ForceLoad");
    }

    /// <summary>
    /// Inspector/Button에서 저장 데이터를 초기화합니다.
    /// </summary>
    public void ResetSaveDataFromInspector()
    {
        ResetSaveData(true, "Inspector.Reset");
    }

    /// <summary>
    /// 런타임 참조를 자동 해석합니다.
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
    /// 기본 옵션 프로필을 기준으로 런타임 저장 데이터를 초기화합니다.
    /// </summary>
    private void InitializeRuntimeDefaults()
    {
        OptionSaveData defaultOptions = _optionDefaultProfile != null
            ? _optionDefaultProfile.CreateDefaultSaveData()
            : new OptionSaveData(); // 기본 프로필 누락 시 사용할 최소 옵션 데이터입니다.

        if (_optionDefaultProfile == null)
        {
            Debug.LogWarning("[SaveDataStore] OptionDefaultProfile is missing. Option defaults fell back to an empty OptionSaveData.", this);
        }

        _runtimeData.InitializeFromDefaults(defaultOptions);
    }

    /// <summary>
    /// 런타임 변경 이벤트 구독 상태를 변경합니다.
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
    /// 로드된 런타임 저장 데이터를 실제 게임 시스템에 반영합니다.
    /// </summary>
    private void ApplyRuntimeDataToSystems(string reason)
    {
        _isApplyingLoadedData = true;

        ResolveRuntimeReferences();

        if (_optionManager != null)
        {
            _optionManager.SetAllOptions(_runtimeData.Options);
        }
        else
        {
            Debug.LogWarning($"[SaveDataStore] OptionManager was not found. Loaded option data stayed only in SaveRuntimeData. reason={reason}", this);
        }

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
        NotifyChanged();
    }

    /// <summary>
    /// 저장 파일에서 읽은 데이터를 현재 버전에 맞게 검증하고 보정합니다.
    /// </summary>
    private SaveGameData ValidateAndRepairLoadedData(SaveGameData loadedData, string rawJson)
    {
        if (loadedData == null)
        {
            Debug.LogWarning("[SaveDataStore] Save JSON was deserialized to null. Default data will be used.", this);
            return CreateDefaultSaveData();
        }

        if (loadedData.Version != CurrentVersion)
        {
            Debug.LogWarning($"[SaveDataStore] Save version mismatch. loaded={loadedData.Version}, current={CurrentVersion}. Data will be repaired to the current version.", this);
            loadedData.Version = CurrentVersion;
        }

        if (loadedData.Options == null)
        {
            Debug.LogWarning($"[SaveDataStore] Option data was missing in save JSON. Default options will be used. jsonLength={rawJson?.Length ?? 0}", this);
            loadedData.Options = CreateDefaultOptions();
        }

        if (loadedData.StageProgress.Records == null)
        {
            Debug.LogWarning("[SaveDataStore] Stage progress records were missing. Empty progress records will be used.", this);
            loadedData.StageProgress = new StageProgressRuntime.SnapshotData();
        }

        loadedData.LastPlayedSceneName = loadedData.LastPlayedSceneName ?? string.Empty;
        return loadedData;
    }

    /// <summary>
    /// 기본 저장 데이터 객체를 생성합니다.
    /// </summary>
    private SaveGameData CreateDefaultSaveData()
    {
        SaveRuntimeData defaults = new SaveRuntimeData(); // 기본 저장 데이터 구성을 위한 임시 런타임 컨테이너입니다.
        defaults.InitializeFromDefaults(CreateDefaultOptions());
        return defaults.ToSaveData(CurrentVersion);
    }

    /// <summary>
    /// 기본 옵션 저장 데이터를 생성합니다.
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
    /// 기본 런타임 저장 데이터를 만들고 변경 알림을 발생시킵니다.
    /// </summary>
    private void CreateDefaultRuntimeData(string reason)
    {
        Debug.LogWarning($"[SaveDataStore] Runtime save data was reset to defaults. reason={reason}", this);
        _runtimeData.InitializeFromDefaults(CreateDefaultOptions());
        NotifyChanged();
    }

    /// <summary>
    /// 현재 런타임에 저장할 의미 있는 진행이 있는지 판정합니다.
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

        if (!string.IsNullOrWhiteSpace(_runtimeData.StageSession.LastCheckpointId))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 옵션 변경을 저장 런타임에 반영하고 자동 저장을 예약합니다.
    /// </summary>
    private void HandleOptionsChanged(OptionSaveData optionData)
    {
        if (_isApplyingLoadedData)
        {
            return;
        }

        _runtimeData.Options = optionData;
        QueueAutoSave("OptionChanged");
    }

    /// <summary>
    /// 스테이지 진행 변경을 저장 런타임에 반영하고 자동 저장을 예약합니다.
    /// </summary>
    private void HandleStageProgressChanged(string stageId)
    {
        if (_isApplyingLoadedData)
        {
            return;
        }

        CaptureFromRuntime($"StageProgressChanged.{stageId}");
        QueueAutoSave($"StageProgressChanged.{stageId}");
    }

    /// <summary>
    /// 자동 저장 요청을 병합해 지연 실행합니다.
    /// </summary>
    private void QueueAutoSave(string reason)
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

        _pendingAutoSaveCoroutine = StartCoroutine(DelayedAutoSave(reason));
    }

    /// <summary>
    /// 지연 시간 이후 저장 파일을 갱신합니다.
    /// </summary>
    private System.Collections.IEnumerator DelayedAutoSave(string reason)
    {
        yield return new WaitForSecondsRealtime(_autoSaveDelaySeconds);
        _pendingAutoSaveCoroutine = null;
        Save($"Auto.{reason}");
    }

    /// <summary>
    /// 리스너에게 최신 저장 데이터 스냅샷을 전달합니다.
    /// </summary>
    private void NotifyChanged()
    {
        _changedListeners?.Invoke(_runtimeData.ToSaveData(CurrentVersion));
    }
}
