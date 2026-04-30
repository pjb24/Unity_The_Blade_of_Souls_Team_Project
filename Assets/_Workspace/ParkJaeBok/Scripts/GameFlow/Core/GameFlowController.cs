using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 전체 상위 루프를 상태 머신 기반으로 조율하는 오케스트레이터입니다.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("stageId/sceneName 해석 및 Town 스테이지 조회에 사용할 StageCatalog 참조입니다.")]
    [SerializeField] private StageCatalog _stageCatalog; // GameFlow가 스테이지 정의를 조회할 때 사용하는 카탈로그 참조입니다.

    [Tooltip("씬 전환 요청 실행에 사용할 SceneTransitionService 참조입니다. 비어 있으면 런타임 해석을 시도합니다.")]
    [SerializeField] private SceneTransitionService _sceneTransitionService; // 씬 전환 시작/상태 조회에 사용하는 서비스 참조입니다.

    [Tooltip("스테이지 세션 문맥을 유지할 StageSession 참조입니다. 비어 있으면 런타임 해석을 시도합니다.")]
    [SerializeField] private StageSession _stageSession; // 스테이지 진입/복귀 문맥을 기록하는 세션 참조입니다.

    [Tooltip("스테이지 진행도 조회/초기화에 사용할 StageProgressRuntime 참조입니다. 비어 있으면 런타임 해석을 시도합니다.")]
    [SerializeField] private StageProgressRuntime _stageProgressRuntime; // 진행도 런타임 상태를 읽고 쓰는 참조입니다.

    [Tooltip("실패 상황 처리 기준을 정의한 ErrorRecoveryPolicy 참조입니다. 비어 있으면 내장 기본 정책으로 동작합니다.")]
    [SerializeField] private ErrorRecoveryPolicy _errorRecoveryPolicy; // 로딩/복구/저장/종료 예외 처리 기준을 제공하는 정책 참조입니다.

    [Header("Scene Config")]
    [Tooltip("타이틀 씬으로 복귀할 때 기본으로 사용할 씬 이름입니다.")]
    [SerializeField] private string _titleSceneName = "Title"; // 타이틀 복귀 기본 씬 이름입니다.

    [Tooltip("새 게임 시작 시 기본으로 로드할 씬 이름입니다.")]
    [SerializeField] private string _defaultNewGameSceneName = "Town"; // 새 게임 시작 기본 진입 씬 이름입니다.

    [Tooltip("새 게임 시작 시 기본 씬 로드 후 도달할 논리 상태입니다.")]
    [SerializeField] private GameFlowState _defaultNewGameLoadedState = GameFlowState.Town; // 새 게임 로드 완료 이후 상태 전환 목표입니다.

    [Tooltip("타이틀에서 싱글플레이 시작 시 기본으로 로드할 씬 이름입니다.")]
    [SerializeField] private string _singlePlayerStartSceneName = "Town"; // 싱글플레이 시작 요청에서 사용할 기본 씬 이름입니다.

    [Tooltip("타이틀에서 멀티플레이 Host 시작 시 기본으로 로드할 로비/게임 씬 이름입니다.")]
    [SerializeField] private string _multiplayerHostStartSceneName = "Town"; // 멀티플레이 Host 시작 요청에서 사용할 기본 씬 이름입니다.

    [Tooltip("타이틀에서 멀티플레이 Client 참가 시 기본으로 로드할 로비/게임 씬 이름입니다.")]
    [SerializeField] private string _multiplayerClientStartSceneName = "Town"; // 멀티플레이 Client 참가 요청에서 사용할 기본 씬 이름입니다.

    [Tooltip("타이틀에서 멀티플레이 시작 시 Host/Client가 함께 진입할 공통 Town 씬 이름입니다. 비어 있으면 Host/Client 개별 씬 설정을 사용합니다.")]
    [SerializeField] private string _multiplayerSharedTownSceneName = "Town"; // 멀티플레이 시작 시 Host/Client 공통으로 사용할 Town 씬 이름입니다.

    [Header("Lifecycle")]
    [Tooltip("씬 전환 후에도 GameFlowController를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // DDOL 적용 여부를 제어하는 플래그입니다.

    [Tooltip("Awake 직후 Boot 상태로 상태 머신 초기화를 자동 수행할지 여부입니다.")]
    [SerializeField] private bool _autoInitializeOnAwake = true; // 자동 초기화 실행 여부를 제어하는 플래그입니다.

    [Header("Debug")]
    [Tooltip("요청/전환/폴백 로그를 상세 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLog = true; // 디버깅을 위한 상세 로그 출력 여부입니다.

    [Tooltip("디버그 확인용: 최근 저장 실패 발생 이후 더티 상태 플래그입니다.")]
    [SerializeField] private bool _hasSaveFailureDirty; // 저장 실패 이후 사용자 알림/재시도에 활용할 더티 상태입니다.

    [Tooltip("디버그 확인용: 최근 저장 실패 사유 문자열입니다.")]
    [SerializeField] private string _lastSaveFailureReason; // 마지막 저장 실패 원인을 기록하는 디버그 문자열입니다.

    [Tooltip("운영 진단: 최근 Warning 요약에 보관할 최대 개수입니다.")]
    [Min(1)]
    [SerializeField] private int _warningSummaryCapacity = 20; // 최근 Warning 요약 환형 버퍼의 최대 보관 개수입니다.

    [Tooltip("운영 진단: 최근 상태 전이 기록에 보관할 최대 개수입니다.")]
    [Min(1)]
    [SerializeField] private int _stateTransitionHistoryCapacity = 30; // 최근 상태 전이 환형 버퍼의 최대 보관 개수입니다.

    [Tooltip("디버그 확인용: 최근 씬 로딩 실패 누적 횟수입니다.")]
    [SerializeField] private int _sceneLoadFailureRetryCount; // 동일 씬 로딩 실패 누적 재시도 횟수입니다.

    [Tooltip("디버그 확인용: 현재 활성 전이 흐름 Epoch ID입니다.")]
    [SerializeField] private int _flowEpochId; // 전이 요청 단위 식별에 사용하는 흐름 Epoch ID입니다.

    [Tooltip("디버그 확인용: 현재 씬 로드 콜백 매칭에 사용하는 활성 Epoch ID입니다.")]
    [SerializeField] private int _activeSceneLoadEpochId; // 씬 로드 콜백의 stale 여부를 판정하는 활성 Epoch ID입니다.

    [Tooltip("디버그 확인용: 현재 활성 씬 로드 요청의 기대 씬 이름입니다.")]
    [SerializeField] private string _activeSceneLoadSceneName; // 씬 로드 콜백 매칭에 사용하는 기대 씬 이름입니다.

    private GameFlowContext _context; // GameFlow 상태 객체들이 공유할 서비스 컨텍스트입니다.
    private GameFlowStateMachine _stateMachine; // 현재 상위 루프 상태를 관리하는 상태 머신 인스턴스입니다.
    private GameFlowState _pendingLoadedState = GameFlowState.Title; // 현재 씬 로드 완료 후 전환할 목표 상태입니다.
    private bool _isExitRequested; // 중복 종료 요청을 차단하기 위한 런타임 플래그입니다.
    private ErrorRecoveryPolicy _runtimeDefaultPolicy; // 정책 에셋 미할당 시 기본값 제공에 사용할 런타임 정책 인스턴스입니다.
    private FlowRetryService _flowRetryService; // 씬 로딩 실패 재시도/추적/소진 처리를 위임할 서비스 참조입니다.
    private FlowFallbackService _flowFallbackService; // 정책 기반 폴백 분기 실행을 위임할 서비스 참조입니다.
    private readonly FlowExitGuard _flowExitGuard = new FlowExitGuard(); // 종료 요청 중복 진입을 원자적으로 차단할 가드 인스턴스입니다.
    private GameFlowRuntimeDiagnostics _runtimeDiagnostics; // 운영 가시성 지표를 누적/조회할 런타임 진단 저장소입니다.
    private DateTime _recoveryCircuitOpenUntilUtc; // Recovery 서킷브레이커 오픈 종료 예정 UTC 시각입니다.
    private E_GamePlayMode _currentPlayMode = E_GamePlayMode.SinglePlayer; // 현재 선택된 플레이 모드를 추적하는 런타임 상태입니다.
    [Tooltip("디버그용: 현재 멀티 세션에서 이 피어가 가진 권한 역할(Host/Client)입니다.")]
    [SerializeField] private E_MultiplayerSessionRole _currentMultiplayerSessionRole = E_MultiplayerSessionRole.None; // 멀티플레이 세션에서 현재 피어 권한(Host/Client)을 추적하는 런타임 상태입니다.

    /// <summary>
    /// 현재 GameFlow 상태 타입을 외부에서 조회합니다.
    /// </summary>
    public GameFlowState CurrentState => _stateMachine != null ? _stateMachine.CurrentStateType : GameFlowState.Boot;

    /// <summary>
    /// 저장 실패 더티 상태 플래그를 외부에서 조회합니다.
    /// </summary>
    public bool HasSaveFailureDirty => _hasSaveFailureDirty;

    /// <summary>
    /// 마지막 저장 실패 사유를 외부에서 조회합니다.
    /// </summary>
    public string LastSaveFailureReason => _lastSaveFailureReason;

    /// <summary>
    /// 현재 런타임 진단 지표 스냅샷을 외부(UI/툴)에서 조회합니다.
    /// </summary>
    public GameFlowDebugSnapshot DebugSnapshot => GetDebugSnapshot();

    /// <summary>
    /// 현재 선택된 플레이 모드를 외부(UI/로비)에서 조회합니다.
    /// </summary>
    public E_GamePlayMode CurrentPlayMode => _currentPlayMode;

    /// <summary>
    /// 현재 멀티 세션 권한 역할을 외부(UI/네트워크 어댑터)에서 조회합니다.
    /// </summary>
    public E_MultiplayerSessionRole CurrentMultiplayerSessionRole => _currentMultiplayerSessionRole;

    /// <summary>
    /// 지정 씬 이름이 플레이어 스폰을 허용하는 인게임 씬인지 판별합니다.
    /// </summary>
    public bool IsPlayerSpawnAllowedScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (MatchesSceneName(sceneName, _titleSceneName))
        {
            return false;
        }

        if (MatchesSceneName(sceneName, _defaultNewGameSceneName)
            || MatchesSceneName(sceneName, _singlePlayerStartSceneName)
            || MatchesSceneName(sceneName, ResolveMultiplayerStartSceneName(_multiplayerHostStartSceneName))
            || MatchesSceneName(sceneName, ResolveMultiplayerStartSceneName(_multiplayerClientStartSceneName)))
        {
            return true;
        }

        if (_stageCatalog == null || _stageCatalog.Stages == null)
        {
            return false;
        }

        IReadOnlyList<StageDefinition> stages = _stageCatalog.Stages; // 인게임 스테이지 씬 이름 매칭에 사용할 StageCatalog 목록입니다.
        for (int index = 0; index < stages.Count; index++)
        {
            StageDefinition stageDefinition = stages[index]; // 현재 인게임 씬 이름을 비교할 스테이지 정의입니다.
            if (stageDefinition == null)
            {
                continue;
            }

            if (MatchesSceneName(sceneName, stageDefinition.SceneName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 싱글톤 접근용 GameFlowController 인스턴스입니다.
    /// </summary>
    public static GameFlowController Instance { get; private set; }

    /// <summary>
    /// 두 씬 이름 문자열이 동일한지 대소문자 무시 비교로 판별합니다.
    /// </summary>
    private bool MatchesSceneName(string lhs, string rhs)
    {
        if (string.IsNullOrWhiteSpace(lhs) || string.IsNullOrWhiteSpace(rhs))
        {
            return false;
        }

        return string.Equals(lhs.Trim(), rhs.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 의존성을 보정하고 상태 머신을 구성합니다.
    /// </summary>
    private void Awake()
    {
        GameFlowLogger.SetVerbose(_verboseLog);
        _runtimeDiagnostics = new GameFlowRuntimeDiagnostics(_warningSummaryCapacity, _stateTransitionHistoryCapacity);
        GameFlowLogger.OnLogEmitted += HandleGameFlowLogEmitted;

        if (Instance != null && Instance != this)
        {
            LogWarning("중복 GameFlowController를 감지하여 새 인스턴스를 제거합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        ResolveDependencies();
        BuildFlowServices();
        BuildStateMachine();
        BindSceneTransitionEvents(true);

        if (_autoInitializeOnAwake)
        {
            Initialize();
        }
    }

    /// <summary>
    /// 프레임 단위로 상태 머신 Tick을 전달합니다.
    /// </summary>
    private void Update()
    {
        _stateMachine?.Tick(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// 구독한 씬 전환 이벤트와 재시도 코루틴을 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        GameFlowLogger.OnLogEmitted -= HandleGameFlowLogEmitted;
        BindSceneTransitionEvents(false);
        _flowRetryService?.StopRetry();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 상태 머신 초기화를 수행합니다.
    /// </summary>
    public bool Initialize()
    {
        if (_stateMachine == null)
        {
            LogWarning("상태 머신이 없어 Initialize를 수행할 수 없습니다.");
            return false;
        }

        bool initialized = _stateMachine.Initialize(GameFlowState.Boot, "GameFlow.Initialize");
        if (!initialized)
        {
            LogWarning("GameFlow 초기 상태 진입에 실패했습니다.");
        }

        return initialized;
    }

    /// <summary>
    /// 타이틀에서 싱글플레이 시작 요청을 처리합니다.
    /// </summary>
    private bool RequestStartSinglePlayer()
    {
        _currentPlayMode = E_GamePlayMode.SinglePlayer;
        _currentMultiplayerSessionRole = E_MultiplayerSessionRole.None;
        string sceneName = string.IsNullOrWhiteSpace(_singlePlayerStartSceneName) ? _defaultNewGameSceneName : _singlePlayerStartSceneName;
        return RequestStartNewGame(sceneName, _defaultNewGameLoadedState);
    }

    /// <summary>
    /// 지정 슬롯 기준으로 싱글 New Game 시작을 처리합니다.
    /// </summary>
    public bool RequestStartSinglePlayerNewGameInSlot(int slotIndex, bool clearSlotIfUsed)
    {
        E_SaveSlot requestedSlot = (E_SaveSlot)slotIndex; // 슬롯 기반 시작에 사용할 요청 슬롯입니다.
        SaveDataStore saveDataStore = ResolveSaveDataStore(); // 새 게임 슬롯 선택/초기화에 사용할 저장소입니다.
        if (saveDataStore == null || !saveDataStore.SetCurrentSlot(requestedSlot))
        {
            LogWarning($"유효하지 않은 저장 슬롯이라 새 게임 시작을 중단합니다. slot={slotIndex}");
            return false;
        }

        if (clearSlotIfUsed)
        {
            if (!saveDataStore.DeleteSlot(requestedSlot))
            {
                LogWarning($"슬롯 초기화에 실패하여 새 게임 시작을 중단합니다. slot={slotIndex}");
                return false;
            }
        }

        return RequestStartSinglePlayer();
    }

    /// <summary>
    /// 지정 슬롯 기준으로 Load Game(Continue)을 처리합니다.
    /// </summary>
    public bool RequestStartLoadGameInSlot(int slotIndex)
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore(); // Load Game에서 사용할 슬롯 저장소입니다.
        E_SaveSlot requestedSlot = (E_SaveSlot)slotIndex; // Load Game slot selected by the title slot list.
        if (saveDataStore == null)
        {
            LogWarning($"LoadGame failed because SaveDataStore was missing. slot={slotIndex}");
            return false;
        }

        if (!saveDataStore.HasSlotData(requestedSlot))
        {
            LogWarning($"LoadGame failed because selected slot data was missing or invalid. slot={slotIndex}");
            return false;
        }

        if (!saveDataStore.SetCurrentSlot(requestedSlot))
        {
            LogWarning($"유효하지 않은 저장 슬롯이라 Load Game을 중단합니다. slot={slotIndex}");
            return false;
        }

        return RequestContinue();
    }

    /// <summary>
    /// 마지막 사용 슬롯 기준으로 Continue 시작을 처리합니다.
    /// </summary>
    public bool RequestContinueFromLastUsedSlot()
    {
        return RequestContinue();
    }

    /// <summary>
    /// 타이틀에서 멀티플레이 Host 시작 요청을 처리합니다.
    /// </summary>
    public bool RequestStartMultiplayerHost()
    {
        _currentPlayMode = E_GamePlayMode.MultiplayerHost;
        _currentMultiplayerSessionRole = E_MultiplayerSessionRole.Host;
        string sceneName = ResolveMultiplayerStartSceneName(_multiplayerHostStartSceneName);
        return RequestStartNewGame(sceneName, _defaultNewGameLoadedState);
    }

    /// <summary>
    /// 타이틀에서 멀티플레이 Client 참가 요청을 처리합니다.
    /// </summary>
    public bool RequestStartMultiplayerClient()
    {
        _currentPlayMode = E_GamePlayMode.MultiplayerClient;
        _currentMultiplayerSessionRole = E_MultiplayerSessionRole.Client;
        string sceneName = ResolveMultiplayerStartSceneName(_multiplayerClientStartSceneName);
        return RequestStartNewGame(sceneName, _defaultNewGameLoadedState);
    }

    /// <summary>
    /// 멀티 세션 구성 완료 시 외부 네트워크 계층에서 피어 역할을 반영합니다.
    /// </summary>
    public void SetMultiplayerSessionRole(E_MultiplayerSessionRole role)
    {
        _currentMultiplayerSessionRole = role;
    }

    /// <summary>
    /// 현재 플레이 모드/역할 기준으로 스테이지 선택 권한이 있는지 판정합니다.
    /// </summary>
    public bool CanSelectStageInCurrentMode()
    {
        if (_currentPlayMode == E_GamePlayMode.SinglePlayer)
        {
            return true;
        }

        return _currentMultiplayerSessionRole == E_MultiplayerSessionRole.Host;
    }

    /// <summary>
    /// 지정 씬으로 새 게임 시작 요청을 처리합니다.
    /// </summary>
    public bool RequestStartNewGame(string sceneName, GameFlowState loadedState)
    {
        _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.StartNewGameRequested, "RequestStartNewGame", sceneName));

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            LogWarning("새 게임 씬 이름이 비어 있어 요청을 거부합니다.");
            return false;
        }

        ResetRuntimeProgressForNewGame();
        return TryStartSceneLoad(sceneName, loadedState, "RequestStartNewGame");
    }

    /// <summary>
    /// Continue 요청을 처리합니다.
    /// </summary>
    public bool RequestContinue()
    {
        _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.ContinueRequested, "RequestContinue"));

        SaveDataStore saveDataStore = ResolveSaveDataStore(); // Continue 로드 요청을 처리할 단일 저장소입니다.
        if (saveDataStore == null)
        {
            LogWarning("SaveDataStore를 찾을 수 없어 Continue를 수행할 수 없습니다.");
            return false;
        }

        if (!saveDataStore.TryLoadLastUsedSlotData(out SlotPlaySaveData loadedSlotData))
        {
            LogWarning($"Continue failed because last used slot data was missing or invalid. slot={(int)saveDataStore.GetCurrentSlot()}");
            LogWarning("저장 데이터 로드에 실패하여 Continue를 수행할 수 없습니다.");
            return false;
        }

        if (!TryResolveContinueSceneName(out string sceneName))
        {
            sceneName = loadedSlotData.LastPlayedSceneName;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            LogWarning("Continue에 사용할 씬 이름이 없어 로드를 중단합니다.");
            return false;
        }

        return TryStartSceneLoad(sceneName, _defaultNewGameLoadedState, "RequestContinue");
    }

    /// <summary>
    /// 스테이지 진입 요청을 처리합니다.
    /// </summary>
    public bool RequestEnterStage(StageDefinition stageDefinition)
    {
        _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.EnterStageRequested, "RequestEnterStage", "", stageDefinition));

        if (!CanSelectStageInCurrentMode())
        {
            LogWarning($"현재 플레이 모드/권한으로는 스테이지 선택이 불가합니다. mode={_currentPlayMode}, role={_currentMultiplayerSessionRole}");
            return false;
        }

        if (stageDefinition == null)
        {
            LogWarning("stageDefinition이 null이라 스테이지 진입 요청을 거부합니다.");
            return false;
        }

        if (_stageSession == null)
        {
            LogWarning("StageSession이 없어 스테이지 진입 요청을 처리할 수 없습니다.");
            return false;
        }

        _stageSession.SetNextStage(stageDefinition);
        return TryStartSceneLoad(stageDefinition.SceneName, GameFlowState.StagePlaying, "RequestEnterStage");
    }

    /// <summary>
    /// 멀티플레이 시작 시 Host/Client 공통 Town 씬 우선 규칙으로 시작 씬 이름을 해석합니다.
    /// </summary>
    private string ResolveMultiplayerStartSceneName(string fallbackSceneName)
    {
        if (!string.IsNullOrWhiteSpace(_multiplayerSharedTownSceneName))
        {
            return _multiplayerSharedTownSceneName;
        }

        if (!string.IsNullOrWhiteSpace(fallbackSceneName))
        {
            return fallbackSceneName;
        }

        return _defaultNewGameSceneName;
    }

    /// <summary>
    /// 마을 복귀 요청을 처리합니다.
    /// </summary>
    public bool RequestReturnToTown()
    {
        _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.ReturnToTownRequested, "RequestReturnToTown"));

        if (_stageCatalog == null)
        {
            LogWarning("StageCatalog가 없어 마을 복귀 요청을 처리할 수 없습니다.");
            return false;
        }

        if (!_stageCatalog.TryGetTownStage(out StageDefinition townStage) || townStage == null)
        {
            LogWarning("Town 스테이지를 찾지 못해 복귀 요청을 거부합니다.");
            return false;
        }

        if (_stageSession == null)
        {
            LogWarning("StageSession이 없어 복귀 엔트리 문맥을 기록할 수 없습니다.");
            return false;
        }

        _stageSession.SetNextStage(townStage);

        bool movedToReturnState = _stateMachine.TryTransitionTo(GameFlowState.ReturnToTown, "RequestReturnToTown.State");
        if (!movedToReturnState)
        {
            LogWarning("ReturnToTown 상태 진입에 실패했습니다.");
        }

        return TryStartSceneLoad(townStage.SceneName, GameFlowState.Town, "RequestReturnToTown.Load");
    }

    /// <summary>
    /// 게임 종료 또는 타이틀 복귀 요청을 처리합니다.
    /// </summary>
    public bool RequestExit(bool returnToTitleInsteadOfQuit)
    {
        _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.ExitRequested, "RequestExit"));

        bool entered = _flowExitGuard.TryEnter(); // 종료 요청 진입을 1회만 허용하기 위한 원자적 게이트 진입 결과입니다.
        if (!entered || _isExitRequested)
        {
            if (GetPolicy().IgnoreDuplicateExitRequest)
            {
                LogWarning(GameFlowWarningCatalog.DuplicateExitRequestIgnored);
                return false;
            }
        }

        _isExitRequested = true;

        bool movedToExit = _stateMachine.TryTransitionTo(GameFlowState.Exiting, "RequestExit.State");
        if (!movedToExit)
        {
            LogWarning("Exiting 상태 진입에 실패했습니다.");
        }

        TrySaveBeforeExit();

        if (returnToTitleInsteadOfQuit)
        {
            bool movedToTitle = TryStartSceneLoad(_titleSceneName, GameFlowState.Title, "RequestExit.ReturnToTitle");
            _isExitRequested = false;
            _flowExitGuard.Release();
            return movedToTitle;
        }

#if UNITY_EDITOR
        LogWarning("UNITY_EDITOR 환경에서는 Application.Quit이 동작하지 않습니다.");
        _isExitRequested = false;
        _flowExitGuard.Release();
        return true;
#else
        Application.Quit();
        return true;
#endif
    }

    /// <summary>
    /// 플레이어 사망 이벤트를 상위 루프에 전달합니다.
    /// </summary>
    public void NotifyPlayerDead(string reason = "")
    {
        _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.PlayerDead, reason));
    }

    /// <summary>
    /// 사망 복구 실패 이벤트를 상위 루프에 전달하고 정책 기반 폴백을 수행합니다.
    /// </summary>
    public void NotifyDeathRecoveryFailed(string reason)
    {
        _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.DeathRecoveryFailed, reason));
        LogWarning($"플레이어 사망 후 복귀 실패를 감지했습니다. reason={reason}");
        HandleDeathRecoveryFailure(reason);
    }

    /// <summary>
    /// 저장 실패 이벤트를 상위 루프에 전달하고 저장 실패 상태 플래그를 갱신합니다.
    /// </summary>
    public void NotifySaveFailed(string reason)
    {
        _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.SaveFailed, reason));

        if (GetPolicy().MarkSaveFailureDirty)
        {
            _hasSaveFailureDirty = true;
        }

        _lastSaveFailureReason = reason;
        _runtimeDiagnostics?.RecordSaveFailure(DateTime.UtcNow);
        LogWarning(GameFlowWarningCatalog.BuildSaveFailureReported(reason));
    }

    /// <summary>
    /// 현재 운영 진단 지표를 읽기 전용 DTO로 반환합니다.
    /// </summary>
    public GameFlowDebugSnapshot GetDebugSnapshot()
    {
        if (_runtimeDiagnostics == null)
        {
            return new GameFlowDebugSnapshot
            {
                SnapshotCreatedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }

        return _runtimeDiagnostics.CreateSnapshot(DateTime.UtcNow);
    }

    /// <summary>
    /// 타이틀 복귀 중 상태 불일치를 감지했을 때 강제 타이틀 리셋을 수행합니다.
    /// </summary>
    public void NotifyTitleReturnStateMismatch(string reason)
    {
        _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.TitleReturnStateMismatch, reason));

        if (!GetPolicy().ForceResetOnTitleMismatch)
        {
            LogWarning($"타이틀 복귀 상태 불일치를 감지했지만 정책에 따라 강제 리셋을 생략합니다. reason={reason}");
            return;
        }

        LogWarning($"타이틀 복귀 중 상태 꼬임을 감지해 강제 리셋을 수행합니다. reason={reason}");
        ForceResetToTitle("TitleReturnStateMismatch");
    }

    /// <summary>
    /// 저장 실패 더티 상태를 해제합니다.
    /// </summary>
    public void ClearSaveFailureDirty()
    {
        _hasSaveFailureDirty = false;
        _lastSaveFailureReason = string.Empty;
    }

    /// <summary>
    /// 직렬화 필드 및 런타임 싱글톤에서 의존성을 해석합니다.
    /// </summary>
    private void ResolveDependencies()
    {
        _context = new GameFlowContext(
            _sceneTransitionService,
            _stageSession,
            _stageProgressRuntime,
            _stageCatalog);

        _context.ResolveMissingReferencesIfNeeded();

        _sceneTransitionService = _context.SceneTransitionService;
        _stageSession = _context.StageSession;
        _stageProgressRuntime = _context.StageProgressRuntime;

        if (_sceneTransitionService == null)
        {
            LogWarning("SceneTransitionService를 해석하지 못했습니다.");
        }

        if (_stageSession == null)
        {
            LogWarning("StageSession을 해석하지 못했습니다.");
        }

        if (_stageProgressRuntime == null)
        {
            LogWarning("StageProgressRuntime을 해석하지 못했습니다.");
        }
    }

    /// <summary>
    /// 상태 머신 인스턴스를 생성하고 기본 상태들을 등록합니다.
    /// </summary>
    private void BuildStateMachine()
    {
        _stateMachine = new GameFlowStateMachine();

        List<IGameFlowState> states = new List<IGameFlowState>
        {
            new GameFlowBootState(_context, _stateMachine),
            new GameFlowTitleState(_context, _stateMachine),
            new GameFlowTownState(_context, _stateMachine),
            new GameFlowStageLoadingState(_context, _stateMachine),
            new GameFlowStagePlayingState(_context, _stateMachine),
            new GameFlowReturnToTownState(_context, _stateMachine),
            new GameFlowExitingState(_context, _stateMachine),
            new GameFlowErrorRecoveryState(_context, _stateMachine)
        };

        for (int i = 0; i < states.Count; i++)
        {
            _stateMachine.RegisterState(states[i]);
        }
    }

    /// <summary>
    /// 흐름 재시도/폴백/저장동기화 서비스들을 구성합니다.
    /// </summary>
    private void BuildFlowServices()
    {
        _flowRetryService = new FlowRetryService(
            this,
            sceneName => _sceneTransitionService != null && _sceneTransitionService.TryLoadScene(sceneName),
            () =>
            {
                if (_verboseLog)
                {
                    GameFlowLogger.Recovery("Scene load retry started successfully.", this);
                }
            },
            HandleRetryExhausted);

        _flowFallbackService = new FlowFallbackService(
            RequestReturnToTown,
            ForceResetToTitle,
            (state, reason) => _stateMachine != null && _stateMachine.TryTransitionTo(state, $"Fallback.{reason}"));
    }

    /// <summary>
    /// SceneTransitionService 이벤트 구독/해제를 처리합니다.
    /// </summary>
    private void BindSceneTransitionEvents(bool shouldBind)
    {
        if (_sceneTransitionService == null)
        {
            return;
        }

        if (shouldBind)
        {
            _sceneTransitionService.OnBeforeSceneLoad += HandleBeforeSceneLoad;
            _sceneTransitionService.OnAfterSceneLoad += HandleAfterSceneLoad;
        }
        else
        {
            _sceneTransitionService.OnBeforeSceneLoad -= HandleBeforeSceneLoad;
            _sceneTransitionService.OnAfterSceneLoad -= HandleAfterSceneLoad;
        }
    }

    /// <summary>
    /// 씬 로드 시작 이벤트를 상태 머신에 전달합니다.
    /// </summary>
    private void HandleBeforeSceneLoad(string sceneName)
    {
        _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.SceneLoadStarted, "SceneTransitionService.OnBeforeSceneLoad", sceneName));

        bool moved = _stateMachine != null && _stateMachine.TryTransitionTo(GameFlowState.StageLoading, "SceneLoadStarted");
        if (!moved)
        {
            LogWarning($"StageLoading 상태 진입에 실패했습니다. scene={sceneName}");
        }
    }

    /// <summary>
    /// 씬 로드 완료 이벤트를 상태 머신에 전달하고 목표 상태로 전환합니다.
    /// </summary>
    private void HandleAfterSceneLoad(string sceneName)
    {
        if (IsStaleSceneLoadCallback(sceneName))
        {
            LogWarning($"stale 씬 로드 콜백을 무시합니다. scene={sceneName}, expectedScene={_activeSceneLoadSceneName}, epoch={_activeSceneLoadEpochId}");
            return;
        }

        _flowRetryService?.StopRetry();
        _flowRetryService?.ResetTracking();
        if (_sceneLoadFailureRetryCount > 0)
        {
            _runtimeDiagnostics?.SetRecoveryResult(true, DateTime.UtcNow);
        }
        _sceneLoadFailureRetryCount = 0;

        _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.SceneLoaded, "SceneTransitionService.OnAfterSceneLoad", sceneName));

        bool moved = _stateMachine != null && _stateMachine.TryTransitionTo(_pendingLoadedState, $"SceneLoaded.{sceneName}");
        if (!moved)
        {
            LogWarning($"로드 완료 후 목표 상태 전환에 실패했습니다. scene={sceneName}, target={_pendingLoadedState}");
        }

        if (_pendingLoadedState == GameFlowState.Title)
        {
            string activeSceneName = SceneManager.GetActiveScene().name; // 타이틀 복귀 완료 시 씬 일치 여부 판정에 사용할 활성 씬 이름입니다.
            if (!string.IsNullOrWhiteSpace(_titleSceneName) && activeSceneName != _titleSceneName)
            {
                NotifyTitleReturnStateMismatch($"Expected={_titleSceneName}, Actual={activeSceneName}");
            }
        }

        _activeSceneLoadEpochId = 0;
        _activeSceneLoadSceneName = string.Empty;
    }

    /// <summary>
    /// 씬 전환 요청을 시작하고 로드 완료 목표 상태를 설정합니다.
    /// </summary>
    private bool TryStartSceneLoad(string sceneName, GameFlowState loadedState, string requestName)
    {
        if (TryRejectByRecoveryCircuitBreaker(requestName))
        {
            return false;
        }

        if (_sceneTransitionService == null)
        {
            LogWarning($"SceneTransitionService가 없어 씬 로드를 시작할 수 없습니다. request={requestName}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            LogWarning($"씬 이름이 비어 있어 씬 로드를 시작할 수 없습니다. request={requestName}");
            return false;
        }

        _pendingLoadedState = loadedState;
        BeginFlowEpoch(sceneName, requestName);

        bool started = _sceneTransitionService.TryLoadScene(sceneName);
        if (!started)
        {
            _stateMachine?.DispatchEvent(new GameFlowEvent(GameFlowEventType.SceneLoadFailed, requestName, sceneName));
            HandleSceneLoadFailure(sceneName, loadedState, requestName);
            return false;
        }

        if (_verboseLog)
        {
            GameFlowLogger.Info($"Scene load requested. request={requestName}, scene={sceneName}, targetState={loadedState}", this);
        }

        return true;
    }

    /// <summary>
    /// 씬 로딩 실패 시 재시도 또는 폴백을 정책 기준으로 처리합니다.
    /// </summary>
    private void HandleSceneLoadFailure(string sceneName, GameFlowState loadedState, string requestName)
    {
        ErrorRecoveryPolicy policy = GetPolicy(); // 씬 로딩 실패 처리에 사용할 복구 정책 참조입니다.

        bool scheduledRetry = _flowRetryService != null && _flowRetryService.HandleFailure(sceneName, loadedState, requestName, policy);
        _sceneLoadFailureRetryCount = _flowRetryService != null ? _flowRetryService.GetRetryCount() : 0;
        if (scheduledRetry)
        {
            _runtimeDiagnostics?.IncrementRecoveryRetry(DateTime.UtcNow);
        }

        LogWarning(GameFlowWarningCatalog.BuildSceneLoadStartFailed(requestName, sceneName, _sceneLoadFailureRetryCount, policy.MaxSceneLoadRetryCount));
        GameFlowLogger.Recovery($"Scene load retry scheduled. request={requestName}, scene={sceneName}, retry={_sceneLoadFailureRetryCount}/{policy.MaxSceneLoadRetryCount}", this);

        if (scheduledRetry)
        {
            return;
        }

        _stateMachine?.TryTransitionTo(GameFlowState.ErrorRecovery, $"SceneLoadFailed.{sceneName}");
        ExecuteFallback(policy.SceneLoadFailureFallbackState, $"SceneLoadFailureFallback.{sceneName}");
    }

    /// <summary>
    /// 사망 복구 실패 시 정책 기준 폴백을 수행합니다.
    /// </summary>
    private void HandleDeathRecoveryFailure(string reason)
    {
        ErrorRecoveryPolicy policy = GetPolicy(); // 사망 복구 실패 처리에 사용할 정책 참조입니다.
        ExecuteFallback(policy.DeathRecoveryFailureFallbackState, $"DeathRecoveryFailure.{reason}");
    }

    /// <summary>
    /// 정책에서 지정한 폴백 상태로 이동을 시도합니다.
    /// </summary>
    private void ExecuteFallback(GameFlowState fallbackState, string reason)
    {
        bool moved = _flowFallbackService != null && _flowFallbackService.ExecuteFallback(fallbackState, reason);
        if (!moved)
        {
            LogWarning($"폴백 상태 전환에 실패했습니다. fallbackState={fallbackState}, reason={reason}");
        }
        else
        {
            GameFlowLogger.Recovery($"Fallback state transition completed. target={fallbackState}, reason={reason}", this);
        }
    }

    /// <summary>
    /// 타이틀 씬 강제 재진입으로 상태를 초기화합니다.
    /// </summary>
    private bool ForceResetToTitle(string reason)
    {
        _flowRetryService?.StopRetry();
        _flowRetryService?.ResetTracking();
        _sceneLoadFailureRetryCount = 0;

        if (string.IsNullOrWhiteSpace(_titleSceneName))
        {
            LogWarning($"titleSceneName이 비어 있어 강제 타이틀 리셋에 실패했습니다. reason={reason}");
            return false;
        }

        bool movedToLoading = _stateMachine != null && _stateMachine.TryTransitionTo(GameFlowState.StageLoading, $"ForceResetToTitle.{reason}");
        if (!movedToLoading)
        {
            LogWarning($"ForceResetToTitle에서 StageLoading 전환에 실패했습니다. reason={reason}");
        }

        bool started = _sceneTransitionService != null && _sceneTransitionService.TryLoadScene(_titleSceneName);
        if (!started)
        {
            LogWarning($"ForceResetToTitle 씬 전환 시작에 실패했습니다. scene={_titleSceneName}, reason={reason}");
            return false;
        }

        BeginFlowEpoch(_titleSceneName, $"ForceResetToTitle.{reason}");
        _pendingLoadedState = GameFlowState.Title;
        return true;
    }

    /// <summary>
    /// 저장/로드 요청을 처리할 SaveDataStore 인스턴스를 해석합니다.
    /// </summary>
    private SaveDataStore ResolveSaveDataStore()
    {
        SaveDataStore saveDataStore = SaveDataStore.Instance; // 현재 부트스트랩에 배치된 단일 저장소입니다.
        if (saveDataStore == null)
        {
            LogWarning("SaveDataStore Instance가 없어 저장/로드 요청을 처리할 수 없습니다.");
        }

        return saveDataStore;
    }

    /// <summary>
    /// Continue 요청에서 마지막 진입 씬 이름을 해석합니다.
    /// </summary>
    private bool TryResolveContinueSceneName(out string sceneName)
    {
        sceneName = string.Empty;

        if (_stageSession == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_stageSession.SelectedStageId))
        {
            return false;
        }

        if (_stageCatalog == null)
        {
            LogWarning("StageCatalog가 없어 Continue 씬 해석에 실패했습니다.");
            return false;
        }

        if (!_stageCatalog.TryGetById(_stageSession.SelectedStageId, out StageDefinition stageDefinition) || stageDefinition == null)
        {
            LogWarning($"Continue 대상 stageId를 찾지 못했습니다. stageId={_stageSession.SelectedStageId}");
            return false;
        }

        sceneName = stageDefinition.SceneName;
        return string.IsNullOrWhiteSpace(sceneName) == false;
    }

    /// <summary>
    /// 새 게임 시작 전 런타임 진행 데이터를 초기화합니다.
    /// </summary>
    private void ResetRuntimeProgressForNewGame()
    {
        if (_stageSession != null)
        {
            _stageSession.ApplySnapshot(new StageSession.SnapshotData());
        }
        else
        {
            LogWarning("StageSession이 없어 새 게임 세션 초기화를 건너뜁니다.");
        }

        if (_stageProgressRuntime != null)
        {
            _stageProgressRuntime.ApplySnapshot(new StageProgressRuntime.SnapshotData
            {
                Records = new List<StageProgressRecord>()
            });
        }
        else
        {
            LogWarning("StageProgressRuntime이 없어 새 게임 진행도 초기화를 건너뜁니다.");
        }

        ClearSaveFailureDirty();
    }

    /// <summary>
    /// 종료 요청 시 저장 정책에 따라 Persistent/Session 채널 저장을 시도합니다.
    /// </summary>
    private void TrySaveBeforeExit()
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore(); // 종료 또는 타이틀 복귀 직전 저장을 처리할 단일 저장소입니다.
        if (saveDataStore == null)
        {
            LogWarning("SaveDataStore를 찾을 수 없어 종료 전 저장을 건너뜁니다.");
            return;
        }

        if (!saveDataStore.SaveSlot(saveDataStore.GetCurrentSlot(), "GameFlow.Exit"))
        {
            NotifySaveFailed("GameFlow.Exit");
        }
    }

    /// <summary>
    /// 재시도 횟수 소진 시 에러 복구 상태 전환과 폴백 실행을 처리합니다.
    /// </summary>
    private void HandleRetryExhausted(string sceneName, GameFlowState loadedState, string requestName)
    {
        _sceneLoadFailureRetryCount = _flowRetryService != null ? _flowRetryService.GetRetryCount() : _sceneLoadFailureRetryCount;
        _runtimeDiagnostics?.SetRecoveryResult(false, DateTime.UtcNow);
        OpenRecoveryCircuitBreakerIfNeeded(requestName, sceneName);
        _stateMachine?.TryTransitionTo(GameFlowState.ErrorRecovery, $"SceneLoadFailed.{sceneName}");
        ExecuteFallback(GetPolicy().SceneLoadFailureFallbackState, $"SceneLoadFailureFallback.{sceneName}");
    }

    /// <summary>
    /// Recovery 서킷브레이커 오픈 여부를 확인하고 오픈 중이면 요청을 차단합니다.
    /// </summary>
    private bool TryRejectByRecoveryCircuitBreaker(string requestName)
    {
        if (_recoveryCircuitOpenUntilUtc <= DateTime.UtcNow)
        {
            if (_recoveryCircuitOpenUntilUtc != default)
            {
                _runtimeDiagnostics?.CloseRecoveryCircuit(DateTime.UtcNow);
            }

            return false;
        }

        TimeSpan remaining = _recoveryCircuitOpenUntilUtc - DateTime.UtcNow; // 서킷브레이커 오픈 해제까지 남은 시간입니다.
        _runtimeDiagnostics?.RecordRecoveryCircuitRejected();
        LogWarning(GameFlowWarningCatalog.BuildRecoveryCircuitOpenRejected(requestName, remaining.TotalSeconds));
        return true;
    }

    /// <summary>
    /// 재시도 소진 시 정책에 따라 Recovery 서킷브레이커를 오픈합니다.
    /// </summary>
    private void OpenRecoveryCircuitBreakerIfNeeded(string requestName, string sceneName)
    {
        ErrorRecoveryPolicy policy = GetPolicy(); // 서킷브레이커 오픈 여부와 시간을 결정할 정책 참조입니다.
        if (!policy.EnableRecoveryCircuitBreaker)
        {
            return;
        }

        double openSeconds = policy.RecoveryCircuitOpenSeconds; // 서킷브레이커 오픈 유지 시간(초)입니다.
        if (openSeconds <= 0d)
        {
            return;
        }

        DateTime openedAtUtc = DateTime.UtcNow; // 서킷브레이커 오픈 시작 시각입니다.
        _recoveryCircuitOpenUntilUtc = openedAtUtc.AddSeconds(openSeconds);
        string userGuidance = $"요청이 일시적으로 제한되었습니다. {openSeconds:F0}초 후 다시 시도하세요."; // 사용자 안내 메시지 문자열입니다.
        _runtimeDiagnostics?.OpenRecoveryCircuit(openedAtUtc, _recoveryCircuitOpenUntilUtc, userGuidance);
        GameFlowLogger.Recovery($"Recovery circuit opened. request={requestName}, scene={sceneName}, openSeconds={openSeconds:F1}", this);
    }

    /// <summary>
    /// GameFlowLogger 이벤트를 수신해 Warning/State 지표를 런타임 진단 저장소에 반영합니다.
    /// </summary>
    private void HandleGameFlowLogEmitted(GameFlowLogger.GameFlowLogEntry entry)
    {
        if (_runtimeDiagnostics == null)
        {
            return;
        }

        if (entry.Level == "Warning")
        {
            _runtimeDiagnostics.RecordWarning(entry.Message, entry.TimestampUtc);
            return;
        }

        if (entry.Level == "State")
        {
            ParseAndRecordStateTransition(entry.Message, entry.TimestampUtc);
        }
    }

    /// <summary>
    /// 표준 상태 전이 로그 문자열에서 from/to/reason을 추출해 진단 저장소에 기록합니다.
    /// </summary>
    private void ParseAndRecordStateTransition(string stateMessage, DateTime timestampUtc)
    {
        const string fromToken = "from=";
        const string toToken = ", to=";
        const string reasonToken = ", reason=";

        int fromIndex = stateMessage.IndexOf(fromToken, StringComparison.Ordinal); // 상태 전이 from 토큰 시작 인덱스입니다.
        int toIndex = stateMessage.IndexOf(toToken, StringComparison.Ordinal); // 상태 전이 to 토큰 시작 인덱스입니다.
        int reasonIndex = stateMessage.IndexOf(reasonToken, StringComparison.Ordinal); // 상태 전이 reason 토큰 시작 인덱스입니다.
        if (fromIndex < 0 || toIndex < 0 || reasonIndex < 0)
        {
            return;
        }

        int fromValueStart = fromIndex + fromToken.Length; // from 값 시작 인덱스입니다.
        int toValueStart = toIndex + toToken.Length; // to 값 시작 인덱스입니다.
        int reasonValueStart = reasonIndex + reasonToken.Length; // reason 값 시작 인덱스입니다.

        string fromState = stateMessage.Substring(fromValueStart, toIndex - fromValueStart).Trim(); // 추출된 이전 상태 문자열입니다.
        string toState = stateMessage.Substring(toValueStart, reasonIndex - toValueStart).Trim(); // 추출된 다음 상태 문자열입니다.
        string reason = reasonValueStart <= stateMessage.Length ? stateMessage.Substring(reasonValueStart).Trim() : string.Empty; // 추출된 전이 원인 문자열입니다.

        _runtimeDiagnostics.RecordStateTransition(fromState, toState, reason, timestampUtc);
    }

    /// <summary>
    /// 새로운 전이 요청 Epoch를 발급하고 씬 콜백 매칭 기준을 갱신합니다.
    /// </summary>
    private void BeginFlowEpoch(string sceneName, string reason)
    {
        _flowEpochId++;
        _activeSceneLoadEpochId = _flowEpochId;
        _activeSceneLoadSceneName = sceneName;

        if (_verboseLog)
        {
            GameFlowLogger.Info($"FlowEpoch advanced. epoch={_flowEpochId}, scene={sceneName}, reason={reason}", this);
        }
    }

    /// <summary>
    /// 현재 수신한 씬 로드 콜백이 stale(이전 전이)인지 판정합니다.
    /// </summary>
    private bool IsStaleSceneLoadCallback(string sceneName)
    {
        if (_activeSceneLoadEpochId <= 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_activeSceneLoadSceneName))
        {
            return true;
        }

        return _activeSceneLoadSceneName != sceneName;
    }

    /// <summary>
    /// 현재 설정된 복구 정책을 반환하고 null인 경우 기본 정책 값을 사용합니다.
    /// </summary>
    private ErrorRecoveryPolicy GetPolicy()
    {
        if (_errorRecoveryPolicy != null)
        {
            return _errorRecoveryPolicy;
        }

        if (_runtimeDefaultPolicy == null)
        {
            _runtimeDefaultPolicy = ScriptableObject.CreateInstance<ErrorRecoveryPolicy>();
        }

        return _runtimeDefaultPolicy;
    }

    /// <summary>
    /// 정책/옵션 기준으로 표준 Warning 로그를 출력합니다.
    /// </summary>
    private void LogWarning(string message)
    {
        if (_errorRecoveryPolicy != null && !_errorRecoveryPolicy.EnableWarningLogs)
        {
            return;
        }

        GameFlowLogger.Warning(message, this);
    }
}
