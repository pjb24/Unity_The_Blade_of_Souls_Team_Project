using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 저장 participant 수집/저장/복원을 단계별 파이프라인으로 조율하는 서비스입니다.
/// </summary>
public class SaveCoordinator : MonoBehaviour
{
    private const string SchemaVersion = "1.0.0";

    [Header("Dependencies")]
    [Tooltip("실제 저장/로드 I/O를 수행할 백엔드 컴포넌트입니다.")]
    [SerializeField] private MonoBehaviour _backendComponent; // ISaveBackend를 구현한 백엔드 컴포넌트 참조입니다.

    [Tooltip("채널별 파일/백업 정책 목록입니다.")]
    [SerializeField] private List<SaveChannelPolicy> _channelPolicies = new List<SaveChannelPolicy>(); // 채널 유형별 저장 정책 목록입니다.

    [Tooltip("강제 종료 및 수명주기 이벤트 저장 기준 정책입니다.")]
    [SerializeField] private ForceClosePolicy _forceClosePolicy; // 씬전환/주기/종료 저장 트리거 기준 정책입니다.

    [Tooltip("사망 후 복구 저장/복원 선택 정책입니다.")]
    [SerializeField] private RecoveryPolicy _recoveryPolicy; // 복구 채널 저장 및 복원 허용 항목 정책입니다.

    [Header("Runtime")]
    [Tooltip("Awake 시 씬 전체에서 ISaveParticipant를 자동 수집할지 여부입니다.")]
    [SerializeField] private bool _autoRegisterParticipants = true; // 시작 시 participant 자동 수집 활성화 여부입니다.

    [Tooltip("씬 전환 후 participant를 재수집할지 여부입니다.")]
    [SerializeField] private bool _refreshParticipantsAfterSceneLoad = true; // 씬 로드 완료 후 participant 재수집 여부입니다.

    [Tooltip("씬 전환 후 초기 복원을 자동 시도할지 여부입니다.")]
    [SerializeField] private bool _autoLoadOnStart = true; // 시작 시 자동 로드 실행 여부입니다.

    [Tooltip("Save/Load 파이프라인 단계 로그를 상세 출력할지 여부입니다.")]
    [SerializeField] private bool _enablePipelineVerboseLog = true; // 저장 파이프라인 단계 추적용 상세 로그 출력 여부입니다.

    [Header("Save Slots")]
    [Tooltip("저장 슬롯 기능 사용 여부입니다. 활성화 시 채널 파일명이 슬롯별로 분기됩니다.")]
    [SerializeField] private bool _useSaveSlots = true; // 슬롯 저장 파일 분기 기능 사용 여부입니다.

    [Tooltip("최초 실행 또는 슬롯 메타 부재 시 사용할 기본 슬롯 번호입니다.")]
    [Min(1)]
    [SerializeField] private int _defaultSaveSlotIndex = 1; // 초기 활성 슬롯으로 사용할 기본 슬롯 번호입니다.

    [Tooltip("마지막 사용 슬롯 정보를 저장할 메타 파일명입니다.")]
    [SerializeField] private string _slotProfileFileName = "save_slot_profile.json"; // 마지막 사용 슬롯 메타를 기록할 파일 이름입니다.

    [Header("Runtime Save Status")]
    [Tooltip("디버그용: 마지막 SaveChannel 호출 성공 여부입니다.")]
    [SerializeField] private bool _lastSaveSucceeded; // 마지막 저장 시도의 성공 여부입니다.

    [Tooltip("디버그용: 마지막 SaveChannel 호출 채널 타입입니다.")]
    [SerializeField] private E_SaveChannelType _lastSaveChannelType; // 마지막 저장 시도에 사용된 채널 타입입니다.

    [Tooltip("디버그용: 마지막 SaveChannel 호출 트리거 타입입니다.")]
    [SerializeField] private E_SaveTriggerType _lastSaveTriggerType; // 마지막 저장 시도에 사용된 트리거 타입입니다.

    [Tooltip("디버그용: 마지막 SaveChannel 호출 트리거 컨텍스트입니다.")]
    [SerializeField] private string _lastSaveTriggerContext; // 마지막 저장 시도에 사용된 트리거 컨텍스트 문자열입니다.

    [Tooltip("디버그용: 마지막 저장 실패 사유 문자열입니다.")]
    [SerializeField] private string _lastSaveFailureReason; // 마지막 저장 실패 원인 문자열입니다.

    [Header("Runtime Load Status")]
    [Tooltip("디버그용: 마지막 LoadChannel 호출 성공 여부입니다.")]
    [SerializeField] private bool _lastLoadSucceeded; // 마지막 로드 시도의 성공 여부입니다.

    [Tooltip("디버그용: 마지막 LoadChannel 호출 채널 타입입니다.")]
    [SerializeField] private E_SaveChannelType _lastLoadChannelType; // 마지막 로드 시도에 사용된 채널 타입입니다.

    [Tooltip("디버그용: 마지막 LoadChannel 호출 트리거 타입입니다.")]
    [SerializeField] private E_SaveTriggerType _lastLoadTriggerType; // 마지막 로드 시도에 사용된 트리거 타입입니다.

    [Tooltip("디버그용: 마지막 LoadChannel 호출 트리거 컨텍스트입니다.")]
    [SerializeField] private string _lastLoadTriggerContext; // 마지막 로드 시도에 사용된 트리거 컨텍스트 문자열입니다.

    [Tooltip("디버그용: 마지막 로드 실패 사유 문자열입니다.")]
    [SerializeField] private string _lastLoadFailureReason; // 마지막 로드 실패 원인 문자열입니다.

    private readonly Dictionary<E_SaveChannelType, SaveChannelPolicy> _policyByChannel = new Dictionary<E_SaveChannelType, SaveChannelPolicy>(); // 채널 타입별 정책 빠른 조회 맵입니다.
    private readonly List<ISaveParticipant> _participants = new List<ISaveParticipant>(); // 현재 등록된 participant 목록입니다.

    private ISaveBackend _backend; // 저장/로드를 담당하는 백엔드 인터페이스 참조입니다.
    private Coroutine _periodicSaveCoroutine; // 주기 저장 루프 코루틴 핸들입니다.
    private SceneTransitionService _sceneTransitionService; // 씬 전환 이벤트 구독/해제를 위한 서비스 참조입니다.
    private int _activeSaveSlotIndex = 1; // 현재 저장/로드에 사용할 활성 슬롯 번호입니다.

    public static SaveCoordinator Instance { get; private set; }

    public RecoveryPolicy RecoveryPolicy => _recoveryPolicy;
    public int ActiveSaveSlotIndex => _activeSaveSlotIndex;

    [Serializable]
    private class SaveSlotProfileData
    {
        public int LastUsedSlotIndex = 1; // Continue 기본 대상으로 사용할 마지막 사용 슬롯 번호입니다.
    }

    private readonly struct SavePipelineRequest
    {
        public readonly E_SaveChannelType ChannelType; // 저장 채널 유형입니다.
        public readonly E_SaveTriggerType TriggerType; // 저장 트리거 유형입니다.
        public readonly string TriggerContext; // 저장 트리거 상세 문자열입니다.

        /// <summary>
        /// 저장 요청 컨텍스트를 생성합니다.
        /// </summary>
        public SavePipelineRequest(E_SaveChannelType channelType, E_SaveTriggerType triggerType, string triggerContext)
        {
            ChannelType = channelType;
            TriggerType = triggerType;
            TriggerContext = triggerContext;
        }
    }

    private readonly struct LoadPipelineRequest
    {
        public readonly E_SaveChannelType ChannelType; // 로드 채널 유형입니다.
        public readonly E_SaveTriggerType TriggerType; // 로드 트리거 유형입니다.
        public readonly string TriggerContext; // 로드 트리거 상세 문자열입니다.

        /// <summary>
        /// 로드 요청 컨텍스트를 생성합니다.
        /// </summary>
        public LoadPipelineRequest(E_SaveChannelType channelType, E_SaveTriggerType triggerType, string triggerContext)
        {
            ChannelType = channelType;
            TriggerType = triggerType;
            TriggerContext = triggerContext;
        }
    }

    /// <summary>
    /// 저장 시도 결과를 외부 런타임에 전달하기 위한 상태 데이터입니다.
    /// </summary>
    public readonly struct SaveOperationStatus
    {
        public readonly bool Succeeded; // 저장 호출 성공 여부입니다.
        public readonly E_SaveChannelType ChannelType; // 저장에 사용된 채널 타입입니다.
        public readonly E_SaveTriggerType TriggerType; // 저장에 사용된 트리거 타입입니다.
        public readonly string TriggerContext; // 저장에 사용된 트리거 컨텍스트 문자열입니다.
        public readonly string FailureReason; // 실패 시 원인 문자열입니다.

        /// <summary>
        /// 전달된 값으로 SaveOperationStatus를 생성합니다.
        /// </summary>
        public SaveOperationStatus(bool succeeded, E_SaveChannelType channelType, E_SaveTriggerType triggerType, string triggerContext, string failureReason)
        {
            Succeeded = succeeded;
            ChannelType = channelType;
            TriggerType = triggerType;
            TriggerContext = triggerContext;
            FailureReason = failureReason;
        }
    }

    /// <summary>
    /// 로드 시도 결과를 외부 런타임에 전달하기 위한 상태 데이터입니다.
    /// </summary>
    public readonly struct LoadOperationStatus
    {
        public readonly bool Succeeded; // 로드 호출 성공 여부입니다.
        public readonly E_SaveChannelType ChannelType; // 로드에 사용된 채널 타입입니다.
        public readonly E_SaveTriggerType TriggerType; // 로드에 사용된 트리거 타입입니다.
        public readonly string TriggerContext; // 로드에 사용된 트리거 컨텍스트 문자열입니다.
        public readonly string FailureReason; // 실패 시 원인 문자열입니다.

        /// <summary>
        /// 전달된 값으로 LoadOperationStatus를 생성합니다.
        /// </summary>
        public LoadOperationStatus(bool succeeded, E_SaveChannelType channelType, E_SaveTriggerType triggerType, string triggerContext, string failureReason)
        {
            Succeeded = succeeded;
            ChannelType = channelType;
            TriggerType = triggerType;
            TriggerContext = triggerContext;
            FailureReason = failureReason;
        }
    }

    /// <summary>
    /// 저장 시도 완료 시 결과를 전달하는 이벤트입니다.
    /// </summary>
    public event Action<SaveOperationStatus> OnSaveOperationCompleted;

    /// <summary>
    /// 로드 시도 완료 시 결과를 전달하는 이벤트입니다.
    /// </summary>
    public event Action<LoadOperationStatus> OnLoadOperationCompleted;

    /// <summary>
    /// 의존성을 검증하고 서비스 싱글톤을 초기화합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SaveCoordinator] 중복 인스턴스가 감지되어 새 인스턴스를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _backend = _backendComponent as ISaveBackend;
        if (_backend == null)
        {
            Debug.LogWarning("[SaveCoordinator] Backend가 ISaveBackend를 구현하지 않았습니다.", this);
        }

        BuildPolicyMap();
        RestoreLastUsedSlotOrDefault();

        if (_autoRegisterParticipants)
        {
            RefreshParticipants();
        }
    }

    /// <summary>
    /// 이벤트 구독과 자동 로드/주기 저장을 시작합니다.
    /// </summary>
    private void Start()
    {
        if (SceneTransitionService.TryGetExistingInstance(out _sceneTransitionService))
        {
            _sceneTransitionService.OnBeforeSceneLoad += HandleBeforeSceneLoad;
            _sceneTransitionService.OnAfterSceneLoad += HandleAfterSceneLoad;
        }

        if (_autoLoadOnStart)
        {
            LoadChannel(E_SaveChannelType.Persistent, E_SaveTriggerType.Manual, "AutoLoad.Persistent");
            LoadChannel(E_SaveChannelType.Session, E_SaveTriggerType.Manual, "AutoLoad.Session");
        }

        TryStartPeriodicSaveLoop();
    }

    /// <summary>
    /// 이벤트 구독을 해제하고 주기 저장을 중지합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (_sceneTransitionService != null)
        {
            _sceneTransitionService.OnBeforeSceneLoad -= HandleBeforeSceneLoad;
            _sceneTransitionService.OnAfterSceneLoad -= HandleAfterSceneLoad;
        }

        StopPeriodicSaveLoop();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 현재 씬에서 participant 목록을 다시 수집합니다.
    /// </summary>
    public void RefreshParticipants()
    {
        _participants.Clear();

        MonoBehaviour[] monoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < monoBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = monoBehaviours[i];
            if (behaviour is ISaveParticipant participant)
            {
                _participants.Add(participant);
            }
        }
    }

    /// <summary>
    /// 지정 채널을 저장합니다.
    /// </summary>
    public bool SaveChannel(E_SaveChannelType channelType, E_SaveTriggerType triggerType, string triggerContext)
    {
        SavePipelineRequest request = new SavePipelineRequest(channelType, triggerType, triggerContext); // 저장 파이프라인에서 사용할 요청 컨텍스트입니다.
        return ExecuteSavePipeline(in request);
    }

    /// <summary>
    /// 지정 채널을 로드하고 participant에 복원합니다.
    /// </summary>
    public bool LoadChannel(E_SaveChannelType channelType, E_SaveTriggerType triggerType, string triggerContext)
    {
        LoadPipelineRequest request = new LoadPipelineRequest(channelType, triggerType, triggerContext); // 로드 파이프라인에서 사용할 요청 컨텍스트입니다.
        return ExecuteLoadPipeline(in request);
    }

    /// <summary>
    /// Save 파이프라인의 전체 단계를 순서대로 실행합니다.
    /// </summary>
    private bool ExecuteSavePipeline(in SavePipelineRequest request)
    {
        if (TryResolveSavePreparation(in request, out SaveChannelPolicy policy, out SaveContext context) == false)
        {
            return false;
        }

        SaveSnapshot snapshot = BuildSaveSnapshot(in request); // 직렬화 전 DTO 루트 모델입니다.
        CollectParticipantRecordsForSave(snapshot, in context);

        string snapshotJson = JsonUtility.ToJson(snapshot, true); // 파일 기록 직전 JSON 직렬화 결과입니다.
        string resolvedFileName = ResolveChannelFileName(policy.FileName, _activeSaveSlotIndex); // 슬롯 규칙이 반영된 최종 저장 파일명입니다.
        bool writeSucceeded = _backend.TryWrite(resolvedFileName, snapshotJson, policy.UseAtomicReplace, policy.BackupCount); // 백엔드 파일 기록 성공 여부입니다.

        if (!writeSucceeded)
        {
            string reason = "Backend write failed";
            Debug.LogWarning($"[SavePipeline] 파일 기록 실패 channel={request.ChannelType}, file={resolvedFileName}", this);
            PublishSaveStatus(false, request.ChannelType, request.TriggerType, request.TriggerContext, reason);
            return false;
        }

        PostProcessAfterSaveSuccess(in request, snapshot, resolvedFileName);
        PublishSaveStatus(true, request.ChannelType, request.TriggerType, request.TriggerContext, string.Empty);
        return true;
    }

    /// <summary>
    /// Save 파이프라인 시작 전 의존성과 채널 정책을 검증합니다.
    /// </summary>
    private bool TryResolveSavePreparation(in SavePipelineRequest request, out SaveChannelPolicy policy, out SaveContext context)
    {
        context = new SaveContext(request.ChannelType, request.TriggerType, request.TriggerContext);
        policy = null;

        if (_backend == null)
        {
            string reason = "Backend is null";
            Debug.LogWarning($"[SavePipeline] Backend가 없어 저장을 건너뜁니다. channel={request.ChannelType}", this);
            PublishSaveStatus(false, request.ChannelType, request.TriggerType, request.TriggerContext, reason);
            return false;
        }

        if (TryGetPolicy(request.ChannelType, out policy) == false || policy == null)
        {
            string reason = "Policy not found";
            Debug.LogWarning($"[SavePipeline] 채널 정책이 없어 저장을 건너뜁니다. channel={request.ChannelType}", this);
            PublishSaveStatus(false, request.ChannelType, request.TriggerType, request.TriggerContext, reason);
            return false;
        }

        LogPipelineStep("Save", request.ChannelType, "SaveRequestResolved", $"trigger={request.TriggerType}, context={request.TriggerContext}");
        return true;
    }

    /// <summary>
    /// 저장 시각/스키마 정보가 포함된 SaveSnapshot 루트 DTO를 생성합니다.
    /// </summary>
    private SaveSnapshot BuildSaveSnapshot(in SavePipelineRequest request)
    {
        SaveSnapshot snapshot = new SaveSnapshot
        {
            SchemaVersion = SchemaVersion,
            SavedUnixTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            SavedIsoUtc = DateTime.UtcNow.ToString("O"),
            TriggerType = request.TriggerType,
            TriggerContext = request.TriggerContext
        };

        LogPipelineStep("Save", request.ChannelType, "SaveSnapshotBuilt", $"schema={snapshot.SchemaVersion}, timestamp={snapshot.SavedIsoUtc}");
        return snapshot;
    }

    /// <summary>
    /// 등록된 participant를 순회하며 저장 가능한 레코드를 SaveSnapshot에 수집합니다.
    /// </summary>
    private void CollectParticipantRecordsForSave(SaveSnapshot snapshot, in SaveContext context)
    {
        int collectedRecordCount = 0; // 저장 스냅샷에 실제로 추가된 participant 레코드 수입니다.

        for (int i = 0; i < _participants.Count; i++)
        {
            ISaveParticipant participant = _participants[i]; // 현재 저장 수집 중인 participant 참조입니다.
            if (participant == null)
            {
                Debug.LogWarning($"[SavePipeline] participant 인스턴스가 null이라 수집을 건너뜁니다. index={i}", this);
                continue;
            }

            if (!participant.CanSave(context))
            {
                continue;
            }

            string payloadJson = participant.CaptureAsJson(context); // participant가 직렬화한 payload JSON 문자열입니다.
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                Debug.LogWarning($"[SavePipeline] payload가 비어 participant 수집을 건너뜁니다. participant={participant.ParticipantId}", this);
                continue;
            }

            snapshot.Records.Add(new SaveParticipantRecord
            {
                ParticipantId = participant.ParticipantId,
                PayloadVersion = participant.PayloadVersion,
                PayloadJson = payloadJson
            });
            collectedRecordCount++;
        }

        LogPipelineStep("Save", context.ChannelType, "SaveRecordsCollected", $"records={collectedRecordCount}");
    }

    /// <summary>
    /// 저장 성공 후 슬롯 프로필 기록 및 디버그 로그 후처리를 수행합니다.
    /// </summary>
    private void PostProcessAfterSaveSuccess(in SavePipelineRequest request, SaveSnapshot snapshot, string resolvedFileName)
    {
        SaveLastUsedSlotProfile();

        LogPipelineStep(
            "Save",
            request.ChannelType,
            "SaveCompleted",
            $"file={resolvedFileName}, records={snapshot.Records.Count}, slot={_activeSaveSlotIndex}");
    }

    /// <summary>
    /// Load 파이프라인의 전체 단계를 순서대로 실행합니다.
    /// </summary>
    private bool ExecuteLoadPipeline(in LoadPipelineRequest request)
    {
        if (TryResolveLoadPreparation(in request, out SaveChannelPolicy policy, out SaveContext context) == false)
        {
            return false;
        }

        if (TryReadSnapshotJson(in request, policy, out string snapshotJson, out string resolvedFileName) == false)
        {
            return false;
        }

        SaveSnapshot snapshot = JsonUtility.FromJson<SaveSnapshot>(snapshotJson); // 역직렬화된 로드 스냅샷 DTO입니다.
        if (TryValidateSnapshotForLoad(snapshot, in request, resolvedFileName) == false)
        {
            return false;
        }

        ApplySnapshotRecordsToParticipants(snapshot, in context, in request);
        PostProcessAfterLoadSuccess(in request, snapshot, resolvedFileName);
        PublishLoadStatus(true, request.ChannelType, request.TriggerType, request.TriggerContext, string.Empty);
        return true;
    }

    /// <summary>
    /// Load 파이프라인 시작 전 의존성과 채널 정책을 검증합니다.
    /// </summary>
    private bool TryResolveLoadPreparation(in LoadPipelineRequest request, out SaveChannelPolicy policy, out SaveContext context)
    {
        context = new SaveContext(request.ChannelType, request.TriggerType, request.TriggerContext);
        policy = null;

        if (_backend == null)
        {
            string reason = "Backend is null";
            Debug.LogWarning($"[LoadPipeline] Backend가 없어 로드를 건너뜁니다. channel={request.ChannelType}", this);
            PublishLoadStatus(false, request.ChannelType, request.TriggerType, request.TriggerContext, reason);
            return false;
        }

        if (TryGetPolicy(request.ChannelType, out policy) == false || policy == null)
        {
            string reason = "Policy not found";
            Debug.LogWarning($"[LoadPipeline] 채널 정책이 없어 로드를 건너뜁니다. channel={request.ChannelType}", this);
            PublishLoadStatus(false, request.ChannelType, request.TriggerType, request.TriggerContext, reason);
            return false;
        }

        LogPipelineStep("Load", request.ChannelType, "LoadRequestResolved", $"trigger={request.TriggerType}, context={request.TriggerContext}");
        return true;
    }

    /// <summary>
    /// 슬롯 규칙을 반영해 스냅샷 JSON 파일을 읽고 필요 시 기본 파일명 폴백을 시도합니다.
    /// </summary>
    private bool TryReadSnapshotJson(in LoadPipelineRequest request, SaveChannelPolicy policy, out string snapshotJson, out string resolvedFileName)
    {
        resolvedFileName = ResolveChannelFileName(policy.FileName, _activeSaveSlotIndex);
        bool readSucceeded = _backend.TryRead(resolvedFileName, out snapshotJson); // 슬롯 기반 파일 읽기 성공 여부입니다.

        if (!readSucceeded)
        {
            bool canFallbackToBaseFile = _useSaveSlots && _activeSaveSlotIndex == 1; // 기본 슬롯에서 구버전 파일명 폴백 허용 여부입니다.
            if (!canFallbackToBaseFile)
            {
                string reason = "Snapshot file not found";
                Debug.LogWarning($"[LoadPipeline] 스냅샷 파일이 없어 로드를 실패합니다. file={resolvedFileName}", this);
                PublishLoadStatus(false, request.ChannelType, request.TriggerType, request.TriggerContext, reason);
                return false;
            }

            if (_backend.TryRead(policy.FileName, out snapshotJson) == false)
            {
                string reason = "Snapshot file not found (fallback failed)";
                Debug.LogWarning($"[LoadPipeline] 슬롯 파일과 기본 파일 모두 읽기에 실패했습니다. slotFile={resolvedFileName}, fallbackFile={policy.FileName}", this);
                PublishLoadStatus(false, request.ChannelType, request.TriggerType, request.TriggerContext, reason);
                return false;
            }

            resolvedFileName = policy.FileName;
            Debug.LogWarning($"[LoadPipeline] 슬롯 파일이 없어 기본 파일명으로 폴백했습니다. channel={request.ChannelType}, fallbackFile={resolvedFileName}", this);
        }

        LogPipelineStep("Load", request.ChannelType, "SnapshotJsonRead", $"file={resolvedFileName}");
        return true;
    }

    /// <summary>
    /// 로드 직후 스냅샷 유효성을 검증하고 버전 불일치 경고를 출력합니다.
    /// </summary>
    private bool TryValidateSnapshotForLoad(SaveSnapshot snapshot, in LoadPipelineRequest request, string resolvedFileName)
    {
        if (snapshot == null)
        {
            string reason = "Snapshot deserialize failed";
            Debug.LogWarning($"[LoadPipeline] 스냅샷 역직렬화에 실패했습니다. file={resolvedFileName}", this);
            PublishLoadStatus(false, request.ChannelType, request.TriggerType, request.TriggerContext, reason);
            return false;
        }

        if (snapshot.Records == null)
        {
            string reason = "Snapshot records null";
            Debug.LogWarning($"[LoadPipeline] 스냅샷 레코드가 null입니다. file={resolvedFileName}", this);
            PublishLoadStatus(false, request.ChannelType, request.TriggerType, request.TriggerContext, reason);
            return false;
        }

        if (string.Equals(snapshot.SchemaVersion, SchemaVersion, StringComparison.Ordinal) == false)
        {
            Debug.LogWarning($"[LoadPipeline] 스키마 버전 불일치가 감지되었습니다. file={resolvedFileName}, current={SchemaVersion}, loaded={snapshot.SchemaVersion}", this);
        }

        LogPipelineStep("Load", request.ChannelType, "SnapshotValidated", $"records={snapshot.Records.Count}, schema={snapshot.SchemaVersion}");
        return true;
    }

    /// <summary>
    /// 스냅샷 레코드를 participant에 순차 적용하고 누락 participant를 경고 로그로 남깁니다.
    /// </summary>
    private void ApplySnapshotRecordsToParticipants(SaveSnapshot snapshot, in SaveContext context, in LoadPipelineRequest request)
    {
        int appliedRecordCount = 0; // 로드에서 실제 participant에 적용된 레코드 수입니다.

        for (int i = 0; i < snapshot.Records.Count; i++)
        {
            SaveParticipantRecord record = snapshot.Records[i]; // 현재 로드 적용 중인 participant 레코드입니다.
            if (record == null)
            {
                Debug.LogWarning($"[LoadPipeline] null 레코드를 건너뜁니다. index={i}, channel={request.ChannelType}", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.ParticipantId))
            {
                Debug.LogWarning($"[LoadPipeline] ParticipantId가 비어있는 레코드를 건너뜁니다. index={i}, channel={request.ChannelType}", this);
                continue;
            }

            ISaveParticipant participant = FindParticipantById(record.ParticipantId); // 레코드 적용 대상 participant 참조입니다.
            if (participant == null)
            {
                Debug.LogWarning($"[LoadPipeline] participant를 찾지 못해 레코드 적용을 건너뜁니다. participant={record.ParticipantId}, channel={request.ChannelType}", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.PayloadJson))
            {
                Debug.LogWarning($"[LoadPipeline] payload가 비어 participant 적용을 건너뜁니다. participant={record.ParticipantId}, channel={request.ChannelType}", this);
                continue;
            }

            participant.RestoreFromJson(record.PayloadJson, context);
            appliedRecordCount++;
        }

        LogPipelineStep("Load", request.ChannelType, "SnapshotApplied", $"appliedRecords={appliedRecordCount}, totalRecords={snapshot.Records.Count}");
    }

    /// <summary>
    /// 로드 성공 후 슬롯 프로필 기록 및 씬 후처리 연계를 수행합니다.
    /// </summary>
    private void PostProcessAfterLoadSuccess(in LoadPipelineRequest request, SaveSnapshot snapshot, string resolvedFileName)
    {
        SaveLastUsedSlotProfile();

        LogPipelineStep(
            "Load",
            request.ChannelType,
            "LoadCompleted",
            $"file={resolvedFileName}, records={snapshot.Records.Count}, slot={_activeSaveSlotIndex}");
    }

    /// <summary>
    /// 마지막 저장 시도 상태를 조회합니다.
    /// </summary>
    public SaveOperationStatus GetLastSaveOperationStatus()
    {
        return new SaveOperationStatus(
            _lastSaveSucceeded,
            _lastSaveChannelType,
            _lastSaveTriggerType,
            _lastSaveTriggerContext,
            _lastSaveFailureReason);
    }

    /// <summary>
    /// 마지막 로드 시도 상태를 조회합니다.
    /// </summary>
    public LoadOperationStatus GetLastLoadOperationStatus()
    {
        return new LoadOperationStatus(
            _lastLoadSucceeded,
            _lastLoadChannelType,
            _lastLoadTriggerType,
            _lastLoadTriggerContext,
            _lastLoadFailureReason);
    }

    /// <summary>
    /// 지정 채널의 스냅샷 파일 존재 여부를 조회합니다.
    /// </summary>
    public bool HasChannelSnapshot(E_SaveChannelType channelType)
    {
        if (_backend == null)
        {
            Debug.LogWarning($"[SaveCoordinator] Backend가 없어 스냅샷 조회를 건너뜁니다. channel={channelType}", this);
            return false;
        }

        if (TryGetPolicy(channelType, out SaveChannelPolicy policy) == false || policy == null)
        {
            Debug.LogWarning($"[SaveCoordinator] 채널 정책이 없어 스냅샷 조회를 건너뜁니다. channel={channelType}", this);
            return false;
        }

        string resolvedFileName = ResolveChannelFileName(policy.FileName, _activeSaveSlotIndex); // 현재 활성 슬롯 규칙이 반영된 스냅샷 조회 파일명입니다.
        bool hasSnapshot = _backend.TryRead(resolvedFileName, out string snapshotJson);
        if (hasSnapshot == false)
        {
            if (_useSaveSlots == false || _activeSaveSlotIndex != 1 || _backend.TryRead(policy.FileName, out snapshotJson) == false)
            {
                return false;
            }

            Debug.LogWarning($"[SaveCoordinator] 슬롯 파일이 없어 기본 파일명으로 스냅샷 조회를 폴백했습니다. channel={channelType}, fallbackFile={policy.FileName}", this);
        }

        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return false;
        }

        SaveSnapshot snapshot = JsonUtility.FromJson<SaveSnapshot>(snapshotJson); // 유효한 세이브 레코드 존재 여부를 검증할 스냅샷 객체입니다.
        if (snapshot == null || snapshot.Records == null || snapshot.Records.Count <= 0)
        {
            return false;
        }

        for (int i = 0; i < snapshot.Records.Count; i++)
        {
            SaveParticipantRecord record = snapshot.Records[i]; // 저장 데이터 유효성 판정에 사용할 participant 레코드입니다.
            if (record == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.ParticipantId) == false && string.IsNullOrWhiteSpace(record.PayloadJson) == false)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 현재 저장/로드에 사용할 활성 슬롯을 지정하고 필요 시 마지막 사용 슬롯 메타를 갱신합니다.
    /// </summary>
    public void SetActiveSaveSlot(int slotIndex, bool persistAsLastUsed = true)
    {
        _activeSaveSlotIndex = Mathf.Max(1, slotIndex);

        if (persistAsLastUsed)
        {
            SaveLastUsedSlotProfile();
        }
    }

    /// <summary>
    /// 마지막 사용 슬롯 번호를 조회합니다.
    /// </summary>
    public bool TryGetLastUsedSlotIndex(out int slotIndex)
    {
        slotIndex = Mathf.Max(1, _activeSaveSlotIndex);
        if (_useSaveSlots == false)
        {
            return false;
        }

        if (_backend == null || string.IsNullOrWhiteSpace(_slotProfileFileName))
        {
            return false;
        }

        if (_backend.TryRead(_slotProfileFileName, out string profileJson) == false || string.IsNullOrWhiteSpace(profileJson))
        {
            return false;
        }

        SaveSlotProfileData profile = JsonUtility.FromJson<SaveSlotProfileData>(profileJson); // 마지막 사용 슬롯 메타를 담는 역직렬화 결과입니다.
        if (profile == null || profile.LastUsedSlotIndex <= 0)
        {
            return false;
        }

        slotIndex = Mathf.Max(1, profile.LastUsedSlotIndex);
        return true;
    }

    /// <summary>
    /// 지정 슬롯에 실제 진행 데이터가 있는지 확인합니다.
    /// </summary>
    public bool HasUsedProgressInSlot(int slotIndex)
    {
        return TryGetSlotProgressSummary(slotIndex, out SaveSlotProgressSummary summary) && summary.HasUsedData;
    }

    /// <summary>
    /// 지정 슬롯의 진행 요약 정보를 반환합니다.
    /// </summary>
    public bool TryGetSlotProgressSummary(int slotIndex, out SaveSlotProgressSummary summary)
    {
        summary = new SaveSlotProgressSummary
        {
            SlotIndex = Mathf.Max(1, slotIndex),
            HasUsedData = false,
            SelectedStageId = string.Empty,
            TotalClearCount = 0,
            LastSavedUnixTimeUtc = 0
        };

        if (_backend == null)
        {
            return false;
        }

        if (TryGetPolicy(E_SaveChannelType.Persistent, out SaveChannelPolicy persistentPolicy) == false || persistentPolicy == null)
        {
            return false;
        }

        string persistentFileName = ResolveChannelFileName(persistentPolicy.FileName, summary.SlotIndex); // 슬롯 분기 규칙이 반영된 Persistent 파일명입니다.
        if (_backend.TryRead(persistentFileName, out string persistentJson) == false || string.IsNullOrWhiteSpace(persistentJson))
        {
            if (_useSaveSlots == false || summary.SlotIndex != 1 || _backend.TryRead(persistentPolicy.FileName, out persistentJson) == false || string.IsNullOrWhiteSpace(persistentJson))
            {
                return false;
            }

            Debug.LogWarning($"[SaveCoordinator] 슬롯 Persistent 파일이 없어 기본 파일명으로 요약 조회를 폴백했습니다. slot={summary.SlotIndex}, fallbackFile={persistentPolicy.FileName}", this);
        }

        SaveSnapshot snapshot = JsonUtility.FromJson<SaveSnapshot>(persistentJson); // 슬롯 요약 산출에 사용할 Persistent 스냅샷입니다.
        if (snapshot == null || snapshot.Records == null || snapshot.Records.Count <= 0)
        {
            return false;
        }

        summary.LastSavedUnixTimeUtc = snapshot.SavedUnixTimeUtc;
        summary.SelectedStageId = ExtractSelectedStageId(snapshot);
        summary.TotalClearCount = ExtractTotalClearCount(snapshot);
        summary.HasUsedData = string.IsNullOrWhiteSpace(summary.SelectedStageId) == false || summary.TotalClearCount > 0;
        return true;
    }

    /// <summary>
    /// 슬롯 메타 또는 기본 설정값으로 활성 슬롯을 초기화합니다.
    /// </summary>
    private void RestoreLastUsedSlotOrDefault()
    {
        _activeSaveSlotIndex = Mathf.Max(1, _defaultSaveSlotIndex);
        if (_useSaveSlots == false)
        {
            return;
        }

        if (TryGetLastUsedSlotIndex(out int lastUsedSlotIndex))
        {
            _activeSaveSlotIndex = Mathf.Max(1, lastUsedSlotIndex);

            return;
        }

        Debug.LogWarning($"[SaveCoordinator] 마지막 사용 슬롯 메타를 찾지 못해 기본 슬롯으로 폴백합니다. defaultSlot={_activeSaveSlotIndex}", this);
    }

    /// <summary>
    /// 현재 활성 슬롯을 마지막 사용 슬롯 메타 파일에 기록합니다.
    /// </summary>
    private void SaveLastUsedSlotProfile()
    {
        if (_useSaveSlots == false || _backend == null || string.IsNullOrWhiteSpace(_slotProfileFileName))
        {
            return;
        }

        SaveSlotProfileData profile = new SaveSlotProfileData
        {
            LastUsedSlotIndex = Mathf.Max(1, _activeSaveSlotIndex)
        };

        string profileJson = JsonUtility.ToJson(profile, true); // 마지막 사용 슬롯 메타를 파일에 기록할 JSON 문자열입니다.
        bool writeSucceeded = _backend.TryWrite(_slotProfileFileName, profileJson, true, 1); // 슬롯 메타 파일 기록 성공 여부입니다.
        if (!writeSucceeded)
        {
            Debug.LogWarning($"[SaveCoordinator] 마지막 사용 슬롯 메타 기록에 실패했습니다. file={_slotProfileFileName}", this);
        }
    }

    /// <summary>
    /// 채널 기본 파일명에 슬롯 번호 접미사를 반영한 최종 파일명을 계산합니다.
    /// </summary>
    private string ResolveChannelFileName(string baseFileName, int slotIndex)
    {
        if (_useSaveSlots == false)
        {
            return baseFileName;
        }

        int safeSlotIndex = Mathf.Max(1, slotIndex); // 파일명 접미사 생성에 사용할 보정된 슬롯 번호입니다.
        string extension = Path.GetExtension(baseFileName);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(baseFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return $"{fileNameWithoutExtension}_slot{safeSlotIndex}";
        }

        return $"{fileNameWithoutExtension}_slot{safeSlotIndex}{extension}";
    }

    /// <summary>
    /// 스냅샷에서 마지막 진행 스테이지 ID를 추출합니다.
    /// </summary>
    private string ExtractSelectedStageId(SaveSnapshot snapshot)
    {
        const string stageSessionParticipantId = "core.stage_session";
        for (int i = 0; i < snapshot.Records.Count; i++)
        {
            SaveParticipantRecord record = snapshot.Records[i]; // stage_session participant 탐색에 사용할 레코드입니다.
            if (record == null || string.Equals(record.ParticipantId, stageSessionParticipantId, StringComparison.Ordinal) == false)
            {
                continue;
            }

            StageSessionPayloadProxy payload = JsonUtility.FromJson<StageSessionPayloadProxy>(record.PayloadJson); // SelectedStageId 추출용 임시 payload 객체입니다.
            if (payload == null)
            {
                continue;
            }

            return payload.SelectedStageId ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// 스냅샷에서 누적 스테이지 클리어 횟수를 추출합니다.
    /// </summary>
    private int ExtractTotalClearCount(SaveSnapshot snapshot)
    {
        const string stageProgressParticipantId = "core.stage_progress";
        for (int i = 0; i < snapshot.Records.Count; i++)
        {
            SaveParticipantRecord record = snapshot.Records[i]; // stage_progress participant 탐색에 사용할 레코드입니다.
            if (record == null || string.Equals(record.ParticipantId, stageProgressParticipantId, StringComparison.Ordinal) == false)
            {
                continue;
            }

            StageProgressPayloadProxy payload = JsonUtility.FromJson<StageProgressPayloadProxy>(record.PayloadJson); // 누적 클리어 횟수 계산에 사용할 임시 payload 객체입니다.
            if (payload == null || payload.Records == null)
            {
                continue;
            }

            int totalClearCount = 0;
            for (int recordIndex = 0; recordIndex < payload.Records.Count; recordIndex++)
            {
                StageProgressRecord progressRecord = payload.Records[recordIndex]; // 합산 중인 stage_progress 레코드입니다.
                if (progressRecord == null)
                {
                    continue;
                }

                totalClearCount += Mathf.Max(0, progressRecord.ClearCount);
            }

            return totalClearCount;
        }

        return 0;
    }

    [Serializable]
    private class StageSessionPayloadProxy
    {
        public string SelectedStageId; // stage_session participant payload에서 추출할 스테이지 ID입니다.
    }

    [Serializable]
    private class StageProgressPayloadProxy
    {
        public List<StageProgressRecord> Records = new List<StageProgressRecord>(); // stage_progress participant payload에서 추출할 진행 레코드 목록입니다.
    }

    /// <summary>
    /// 복구 정책을 반영해 Recovery 채널을 복원합니다.
    /// </summary>
    public bool TryRecoverAfterDeath()
    {
        return LoadChannel(E_SaveChannelType.Recovery, E_SaveTriggerType.Death, "Recovery.Load");
    }

    /// <summary>
    /// 씬 전환 직전 정책에 따라 채널 저장을 수행합니다.
    /// </summary>
    private void HandleBeforeSceneLoad(string sceneName)
    {
        if (_forceClosePolicy == null)
        {
            return;
        }

        if (_forceClosePolicy.SavePersistentOnSceneTransition)
        {
            SaveChannel(E_SaveChannelType.Persistent, E_SaveTriggerType.SceneTransition, sceneName);
        }

        if (_forceClosePolicy.SaveSessionOnSceneTransition)
        {
            SaveChannel(E_SaveChannelType.Session, E_SaveTriggerType.SceneTransition, sceneName);
        }
    }

    /// <summary>
    /// 씬 로드 직후 participant 재수집을 수행합니다.
    /// </summary>
    private void HandleAfterSceneLoad(string _)
    {
        if (_refreshParticipantsAfterSceneLoad)
        {
            RefreshParticipants();
        }

        GimmickStateSaveParticipant.ApplyDeferredRestoresInScene(GimmickRestoreRuleSet.RestoreTiming.AfterSceneLoad);
    }

    /// <summary>
    /// 앱 일시정지 시 정책에 따라 저장을 시도합니다.
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus || _forceClosePolicy == null)
        {
            return;
        }

        if (_forceClosePolicy.SavePersistentOnPause)
        {
            SaveChannel(E_SaveChannelType.Persistent, E_SaveTriggerType.Pause, "OnApplicationPause");
        }
    }

    /// <summary>
    /// 앱 포커스 손실 시 정책에 따라 저장을 시도합니다.
    /// </summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus || _forceClosePolicy == null)
        {
            return;
        }

        if (_forceClosePolicy.SaveSessionOnFocusLost)
        {
            SaveChannel(E_SaveChannelType.Session, E_SaveTriggerType.FocusLost, "OnApplicationFocus");
        }
    }

    /// <summary>
    /// 앱 종료 시 정책에 따라 저장을 시도합니다.
    /// </summary>
    private void OnApplicationQuit()
    {
        if (_forceClosePolicy == null)
        {
            return;
        }

        if (_forceClosePolicy.SavePersistentOnQuit)
        {
            SaveChannel(E_SaveChannelType.Persistent, E_SaveTriggerType.ApplicationQuit, "OnApplicationQuit");
        }
    }

    /// <summary>
    /// 주기 저장 루프를 시작합니다.
    /// </summary>
    private void TryStartPeriodicSaveLoop()
    {
        if (_forceClosePolicy == null || !_forceClosePolicy.UsePeriodicSave)
        {
            return;
        }

        StopPeriodicSaveLoop();
        _periodicSaveCoroutine = StartCoroutine(CoPeriodicSave());
    }

    /// <summary>
    /// 주기 저장 루프를 정지합니다.
    /// </summary>
    private void StopPeriodicSaveLoop()
    {
        if (_periodicSaveCoroutine == null)
        {
            return;
        }

        StopCoroutine(_periodicSaveCoroutine);
        _periodicSaveCoroutine = null;
    }

    /// <summary>
    /// 저장 결과를 디버그 필드와 이벤트에 반영합니다.
    /// </summary>
    private void PublishSaveStatus(bool succeeded, E_SaveChannelType channelType, E_SaveTriggerType triggerType, string triggerContext, string failureReason)
    {
        _lastSaveSucceeded = succeeded;
        _lastSaveChannelType = channelType;
        _lastSaveTriggerType = triggerType;
        _lastSaveTriggerContext = triggerContext;
        _lastSaveFailureReason = failureReason;

        OnSaveOperationCompleted?.Invoke(new SaveOperationStatus(
            succeeded,
            channelType,
            triggerType,
            triggerContext,
            failureReason));
    }

    /// <summary>
    /// 로드 결과를 디버그 필드와 이벤트에 반영합니다.
    /// </summary>
    private void PublishLoadStatus(bool succeeded, E_SaveChannelType channelType, E_SaveTriggerType triggerType, string triggerContext, string failureReason)
    {
        _lastLoadSucceeded = succeeded;
        _lastLoadChannelType = channelType;
        _lastLoadTriggerType = triggerType;
        _lastLoadTriggerContext = triggerContext;
        _lastLoadFailureReason = failureReason;

        OnLoadOperationCompleted?.Invoke(new LoadOperationStatus(
            succeeded,
            channelType,
            triggerType,
            triggerContext,
            failureReason));
    }

    /// <summary>
    /// Save/Load 단계 로그를 설정에 따라 출력합니다.
    /// </summary>
    private void LogPipelineStep(string pipeline, E_SaveChannelType channelType, string stepName, string detail)
    {
        if (!_enablePipelineVerboseLog)
        {
            return;
        }

        Debug.Log($"[{pipeline}Pipeline] step={stepName}, channel={channelType}, detail={detail}", this);
    }

    /// <summary>
    /// 주기적으로 Persistent 채널 저장을 실행합니다.
    /// </summary>
    private IEnumerator CoPeriodicSave()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(_forceClosePolicy.PeriodicSaveIntervalSeconds);
        while (true)
        {
            yield return wait;
            SaveChannel(E_SaveChannelType.Persistent, E_SaveTriggerType.Interval, "PeriodicSave");
        }
    }

    /// <summary>
    /// inspector 정책 목록을 채널 타입 맵으로 구성합니다.
    /// </summary>
    private void BuildPolicyMap()
    {
        _policyByChannel.Clear();

        for (int i = 0; i < _channelPolicies.Count; i++)
        {
            SaveChannelPolicy policy = _channelPolicies[i];
            if (policy == null)
            {
                continue;
            }

            _policyByChannel[policy.ChannelType] = policy;
        }
    }

    /// <summary>
    /// 채널 타입에 해당하는 정책을 조회합니다.
    /// </summary>
    private bool TryGetPolicy(E_SaveChannelType channelType, out SaveChannelPolicy policy)
    {
        return _policyByChannel.TryGetValue(channelType, out policy);
    }

    /// <summary>
    /// participant ID로 등록된 participant를 검색합니다.
    /// </summary>
    private ISaveParticipant FindParticipantById(string participantId)
    {
        if (string.IsNullOrWhiteSpace(participantId))
        {
            return null;
        }

        for (int i = 0; i < _participants.Count; i++)
        {
            ISaveParticipant participant = _participants[i];
            if (participant == null)
            {
                continue;
            }

            if (participant.ParticipantId == participantId)
            {
                return participant;
            }
        }

        return null;
    }
}
