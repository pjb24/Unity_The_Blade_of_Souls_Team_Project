using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 결과 UI 버튼 요청을 GameFlow, StageSession, Checkpoint 흐름으로 연결합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageOutcomeFlowController : NetworkBehaviour
{
    [Header("Dependencies")]
    [Tooltip("엔딩 UI View입니다. 비어 있으면 현재 씬에서 자동 탐색합니다.")]
    [SerializeField] private EndingPanelView _endingPanelView; // 엔딩 UI 버튼 이벤트를 구독할 View입니다.

    [Tooltip("사망 UI View입니다. 비어 있으면 현재 씬에서 자동 탐색합니다.")]
    [SerializeField] private DeathPanelView _deathPanelView; // 사망 UI 버튼 이벤트를 구독할 View입니다.

    [Tooltip("다음 스테이지와 Town 스테이지를 조회할 StageCatalog입니다.")]
    [SerializeField] private StageCatalog _stageCatalog; // 현재/다음/Town StageDefinition 조회에 사용할 카탈로그입니다.

    [Tooltip("씬 전환을 수행할 SceneTransitionService입니다. 비어 있으면 싱글톤으로 해석합니다.")]
    [SerializeField] private SceneTransitionService _sceneTransitionService; // GameFlow 폴백 시 직접 씬 전환을 수행할 서비스입니다.

    [Tooltip("상위 GameFlow 명령을 수행할 GameFlowController입니다. 비어 있으면 싱글톤으로 해석합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // Town 복귀와 스테이지 진입을 위임할 상위 흐름 컨트롤러입니다.

    [Header("Restart")]
    [Tooltip("Ending UI의 Restart Stage Entry Button을 눌렀을 때 이동할 스테이지입니다. 비어 있으면 현재 StageSession 스테이지를 사용합니다.")]
    [SerializeField] private StageDefinition _restartStageEntryTargetStage; // 엔딩 UI 시작 체크포인트 재시작 버튼이 로드할 스테이지 정의입니다.

    [Tooltip("Death UI의 Restart Last Checkpoint Button을 눌렀을 때 이동할 스테이지입니다. 비어 있으면 현재 StageSession 스테이지를 사용합니다.")]
    [SerializeField] private StageDefinition _restartLastCheckpointTargetStage; // 사망 UI 마지막 체크포인트 재시작 버튼이 로드할 스테이지 정의입니다.

    [Tooltip("StageCatalog에서 현재 스테이지 뒤에 다음 스테이지가 없으면 Town으로 이동할지 여부입니다.")]
    [SerializeField] private bool _returnToTownWhenNextStageMissing = true; // 다음 스테이지 누락 시 Town 복귀 폴백 여부입니다.

    [Header("Scene Change Revival")]
    [Tooltip("Ending/Death UI 버튼으로 씬을 변경하기 직전에 Die 상태 플레이어를 부활시킬지 여부입니다.")]
    [SerializeField] private bool _reviveDeadPlayersBeforeSceneChange = true; // 결과 UI 씬 전환 전 사망 플레이어 부활 적용 여부입니다.

    [Tooltip("씬 변경 직전 사망 플레이어를 부활시킬 때 최대 체력 대비 회복 비율입니다.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float _sceneChangeReviveHealthRatio = 1f; // 씬 전환 전 부활 체력 비율입니다.

    [Header("Debug")]
    [Tooltip("버튼 요청 처리 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLog; // 결과 버튼 처리 상세 로그 출력 여부입니다.

    /// <summary>
    /// 참조를 보정하고 UI 버튼 이벤트를 구독합니다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        BindViews(true);
        RefreshOutcomeButtonAccess();
    }

    /// <summary>
    /// 활성화 시 누락된 참조를 다시 보정하고 버튼 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        BindViews(true);
        RefreshOutcomeButtonAccess();
    }

    /// <summary>
    /// 네트워크 스폰 이후 확정된 Host/Client 권한을 결과 UI 버튼 상태에 반영합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        RefreshOutcomeButtonAccess();
    }

    /// <summary>
    /// 런타임 중 Host/Client 세션 상태가 바뀌어도 결과 UI 버튼 권한이 따라가도록 갱신합니다.
    /// </summary>
    private void Update()
    {
        RefreshOutcomeButtonAccess();
    }

    /// <summary>
    /// 비활성화 시 UI 버튼 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        BindViews(false);
    }

    /// <summary>
    /// 현재 StageSession의 스테이지를 클리어 처리합니다.
    /// </summary>
    public void MarkCurrentStageCleared()
    {
        if (!HasFlowAuthority())
        {
            return;
        }

        string stageId = ResolveCurrentStageId(); // 클리어 처리할 현재 Stage ID입니다.
        if (string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogWarning("[StageOutcomeFlowController] 현재 Stage ID를 찾지 못해 클리어 처리를 건너뜁니다.", this);
            return;
        }

        StageProgressRuntime.Instance.MarkStageCleared(stageId);

        if (_verboseLog)
        {
            Debug.Log($"[StageOutcomeFlowController] Stage clear marked. stage={stageId}", this);
        }
    }

    /// <summary>
    /// 다음 스테이지 이동 버튼 요청을 처리합니다.
    /// </summary>
    public void RequestNextStage()
    {
        if (TryForwardRequestToServer(E_StageOutcomeRequest.NextStage))
        {
            return;
        }

        ExecuteNextStage();
    }

    /// <summary>
    /// 마을 이동 버튼 요청을 처리합니다.
    /// </summary>
    public void RequestReturnToTown()
    {
        if (TryForwardRequestToServer(E_StageOutcomeRequest.ReturnToTown))
        {
            return;
        }

        ExecuteReturnToTown();
    }

    /// <summary>
    /// 스테이지 진입 체크포인트 재시작 버튼 요청을 처리합니다.
    /// </summary>
    public void RequestRestartStageEntry()
    {
        if (TryForwardRequestToServer(E_StageOutcomeRequest.RestartStageEntry))
        {
            return;
        }

        ExecuteRestartStageEntry();
    }

    /// <summary>
    /// 마지막 체크포인트 재시작 버튼 요청을 처리합니다.
    /// </summary>
    public void RequestRestartLastCheckpoint()
    {
        if (TryForwardRequestToServer(E_StageOutcomeRequest.RestartLastCheckpoint))
        {
            return;
        }

        ExecuteRestartCurrentStage();
    }

    /// <summary>
    /// 게임 종료 버튼 요청을 처리합니다.
    /// </summary>
    public void RequestQuitGame()
    {
        ExecuteQuitGame();
    }

    /// <summary>
    /// 현재 스테이지 다음에 배치된 스테이지로 이동합니다.
    /// </summary>
    private void ExecuteNextStage()
    {
        if (!HasFlowAuthority())
        {
            return;
        }

        if (!TryResolveNextStage(out StageDefinition nextStage))
        {
            if (_returnToTownWhenNextStageMissing)
            {
                ExecuteReturnToTown();
            }

            return;
        }

        StartStageLoad(nextStage);
    }

    /// <summary>
    /// GameFlowController를 통해 Town으로 복귀합니다.
    /// </summary>
    private void ExecuteReturnToTown()
    {
        if (!HasFlowAuthority())
        {
            return;
        }

        ReviveDeadPlayersBeforeSceneChange();

        GameFlowController gameFlowController = ResolveGameFlowController();
        if (gameFlowController != null && gameFlowController.RequestReturnToTown())
        {
            return;
        }

        if (_stageCatalog != null && _stageCatalog.TryGetTownStage(out StageDefinition townStage) && townStage != null)
        {
            StartStageLoad(townStage);
        }
    }

    /// <summary>
    /// 현재 스테이지의 체크포인트 기준을 시작 체크포인트로 되돌린 뒤 씬을 다시 로드합니다.
    /// </summary>
    private void ExecuteRestartStageEntry()
    {
        if (!HasFlowAuthority())
        {
            return;
        }

        StageDefinition restartStage = ResolveRestartStageEntryTargetStage(); // 엔딩 UI 재시작 버튼으로 이동할 대상 스테이지입니다.
        if (restartStage != null)
        {
            StageSession.Instance.RequestStageEntryCheckpointOnNextLoad(restartStage);
        }

        if (ShouldResetActiveSceneEntryCheckpoint(restartStage))
        {
            CheckpointStageController checkpointStageController = FindAnyObjectByType<CheckpointStageController>();
            if (checkpointStageController != null)
            {
                checkpointStageController.ResetCurrentCheckpointToStageEntry();
            }
            else
            {
                Debug.LogWarning("[StageOutcomeFlowController] CheckpointStageController를 찾지 못해 시작 체크포인트 리셋 없이 대상 스테이지를 로드합니다.", this);
            }
        }

        StartRestartStageLoad(restartStage);
    }

    /// <summary>
    /// 현재 StageSession의 스테이지를 현재 체크포인트 기준으로 다시 로드합니다.
    /// </summary>
    private void ExecuteRestartCurrentStage()
    {
        if (!HasFlowAuthority())
        {
            return;
        }

        StageDefinition restartStage = ResolveRestartLastCheckpointTargetStage(); // 사망 UI 재시작 버튼으로 이동할 대상 스테이지입니다.
        StartRestartStageLoad(restartStage);
    }

    /// <summary>
    /// 재시작 대상 스테이지를 로드하고, 대상을 해석하지 못하면 활성 씬 재로드로 폴백합니다.
    /// </summary>
    private void StartRestartStageLoad(StageDefinition restartStage)
    {
        if (restartStage == null)
        {
            Debug.LogWarning("[StageOutcomeFlowController] 재시작 대상 StageDefinition을 찾지 못해 활성 씬 이름으로 재로드를 시도합니다.", this);
            ReviveDeadPlayersBeforeSceneChange();
            ResolveSceneTransitionService()?.TryLoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        StartStageLoad(restartStage);
    }

    /// <summary>
    /// GameFlowController를 통해 게임 종료를 요청합니다.
    /// </summary>
    private void ExecuteQuitGame()
    {
        GameFlowController gameFlowController = ResolveGameFlowController();
        if (gameFlowController != null)
        {
            gameFlowController.RequestExit(false);
            return;
        }

        Application.Quit();
    }

    /// <summary>
    /// StageDefinition을 GameFlow 또는 SceneTransitionService로 로드합니다.
    /// </summary>
    private void StartStageLoad(StageDefinition stageDefinition)
    {
        if (stageDefinition == null)
        {
            return;
        }

        ReviveDeadPlayersBeforeSceneChange();

        GameFlowController gameFlowController = ResolveGameFlowController();
        if (gameFlowController != null && !stageDefinition.IsTownStage && gameFlowController.RequestEnterStage(stageDefinition))
        {
            return;
        }

        SceneTransitionService sceneTransitionService = ResolveSceneTransitionService();
        if (sceneTransitionService != null)
        {
            sceneTransitionService.TryLoadStage(stageDefinition);
        }
    }

    /// <summary>
    /// Client에서 발생한 씬 전환 UI 요청을 차단하고 로컬 처리를 종료할지 반환합니다.
    /// </summary>
    private bool TryForwardRequestToServer(E_StageOutcomeRequest request)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || networkManager.IsServer)
        {
            return false;
        }

        if (request == E_StageOutcomeRequest.QuitGame)
        {
            return false;
        }

        Debug.LogWarning($"[StageOutcomeFlowController] Client는 결과 UI의 씬 전환 버튼을 선택할 수 없습니다. request={request}", this);
        return true;
    }

    /// <summary>
    /// 현재 로컬 인스턴스가 결과 UI의 씬 전환 버튼을 선택할 수 있는지 View에 반영합니다.
    /// </summary>
    private void RefreshOutcomeButtonAccess()
    {
        bool canSelectFlowButtons = HasFlowAuthority(); // 싱글플레이 또는 Host/Server만 씬 전환 결정을 내릴 수 있습니다.
        _endingPanelView?.SetFlowButtonsSelectable(canSelectFlowButtons);
        _deathPanelView?.SetFlowButtonsSelectable(canSelectFlowButtons);
    }

    /// <summary>
    /// 씬 변경 직전에 현재 씬의 사망 플레이어를 서버 권한 또는 싱글플레이 경로로 부활시킵니다.
    /// </summary>
    private void ReviveDeadPlayersBeforeSceneChange()
    {
        if (!_reviveDeadPlayersBeforeSceneChange || !HasFlowAuthority())
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션의 PlayerObject 목록을 확인할 관리자입니다.
        if (networkManager != null && networkManager.IsListening)
        {
            ReviveDeadNetworkPlayers(networkManager);
            return;
        }

        ReviveDeadLocalPlayers();
    }

    /// <summary>
    /// NGO에 연결된 모든 플레이어 오브젝트 중 Die 상태인 캐릭터를 부활시킵니다.
    /// </summary>
    private void ReviveDeadNetworkPlayers(NetworkManager networkManager)
    {
        foreach (NetworkClient client in networkManager.ConnectedClientsList)
        {
            if (client == null || client.PlayerObject == null)
            {
                continue;
            }

            TryRevivePlayerObject(client.PlayerObject.gameObject, $"SceneChange.Client:{client.ClientId}");
        }
    }

    /// <summary>
    /// 싱글플레이 씬에 배치된 플레이어 HealthComponent 중 Die 상태인 캐릭터를 부활시킵니다.
    /// </summary>
    private void ReviveDeadLocalPlayers()
    {
        PlayerInputDriver[] inputDrivers = FindObjectsByType<PlayerInputDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 싱글플레이 플레이어 후보입니다.
        for (int index = 0; index < inputDrivers.Length; index++)
        {
            PlayerInputDriver inputDriver = inputDrivers[index];
            if (inputDriver == null)
            {
                continue;
            }

            TryRevivePlayerObject(inputDriver.gameObject, "SceneChange.SinglePlayerInput");
        }

        if (inputDrivers.Length > 0)
        {
            return;
        }

        GameObject taggedPlayer = GameObject.FindWithTag("Player"); // PlayerInputDriver가 없는 테스트 씬을 위한 기본 Player 태그 후보입니다.
        if (taggedPlayer != null)
        {
            TryRevivePlayerObject(taggedPlayer, "SceneChange.TaggedPlayer");
        }
    }

    /// <summary>
    /// 지정한 플레이어 계층에서 HealthComponent를 찾아 사망 상태이면 부활시키고 Die 액션 고정을 해제합니다.
    /// </summary>
    private void TryRevivePlayerObject(GameObject playerObject, string reason)
    {
        if (playerObject == null)
        {
            return;
        }

        HealthComponent healthComponent = playerObject.GetComponentInChildren<HealthComponent>(true);
        if (healthComponent == null)
        {
            healthComponent = playerObject.GetComponentInParent<HealthComponent>();
        }

        if (healthComponent == null || !healthComponent.IsDead)
        {
            return;
        }

        float reviveHealth = Mathf.Max(0.01f, healthComponent.GetMaxHealth() * Mathf.Clamp01(_sceneChangeReviveHealthRatio)); // 부활 후 적용할 체력입니다.
        healthComponent.Revive(reviveHealth);
        healthComponent.NotifyCurrentHealthState();
        CompleteDieAction(playerObject, reason);

        if (_verboseLog)
        {
            Debug.Log($"[StageOutcomeFlowController] Dead player revived before scene change. player={playerObject.name}, reason={reason}, health={reviveHealth}", this);
        }
    }

    /// <summary>
    /// 부활한 플레이어가 Die 액션과 이동 잠금에 남지 않도록 현재 Die 액션을 완료 처리합니다.
    /// </summary>
    private void CompleteDieAction(GameObject playerObject, string reason)
    {
        ActionController actionController = playerObject.GetComponentInChildren<ActionController>(true);
        if (actionController == null)
        {
            actionController = playerObject.GetComponentInParent<ActionController>();
        }

        if (actionController == null || !actionController.Runtime.IsRunning || actionController.Runtime.ActionType != E_ActionType.Die)
        {
            return;
        }

        actionController.CompleteCurrentAction();

        if (_verboseLog)
        {
            Debug.Log($"[StageOutcomeFlowController] Die action completed before scene change. player={playerObject.name}, reason={reason}", this);
        }
    }

    /// <summary>
    /// 현재 인스턴스가 Stage 흐름을 변경할 권한이 있는지 판정합니다.
    /// </summary>
    private bool HasFlowAuthority()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager == null || !networkManager.IsListening || networkManager.IsServer;
    }

    /// <summary>
    /// 현재 Stage ID를 StageSession 또는 활성 씬 이름으로 해석합니다.
    /// </summary>
    private string ResolveCurrentStageId()
    {
        if (StageSession.TryGetExistingInstance(out StageSession session) && session != null && !string.IsNullOrWhiteSpace(session.SelectedStageId))
        {
            return session.SelectedStageId;
        }

        return SceneManager.GetActiveScene().name;
    }

    /// <summary>
    /// StageCatalog에서 현재 StageDefinition을 조회합니다.
    /// </summary>
    private bool TryResolveCurrentStageDefinition(out StageDefinition currentStage)
    {
        currentStage = null;
        string stageId = ResolveCurrentStageId(); // 현재 런타임 문맥의 Stage ID입니다.
        if (_stageCatalog != null && _stageCatalog.TryGetById(stageId, out currentStage))
        {
            return true;
        }

        if (_stageCatalog == null)
        {
            return false;
        }

        string activeSceneName = SceneManager.GetActiveScene().name; // Stage ID와 씬 이름이 다른 경우를 위한 폴백입니다.
        for (int i = 0; i < _stageCatalog.Stages.Count; i++)
        {
            StageDefinition candidate = _stageCatalog.Stages[i];
            if (candidate != null && candidate.SceneName == activeSceneName)
            {
                currentStage = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Ending UI 시작 체크포인트 재시작 버튼의 대상 스테이지를 해석합니다.
    /// </summary>
    private StageDefinition ResolveRestartStageEntryTargetStage()
    {
        if (_restartStageEntryTargetStage != null)
        {
            return _restartStageEntryTargetStage;
        }

        return TryResolveCurrentStageDefinition(out StageDefinition currentStage) ? currentStage : null;
    }

    /// <summary>
    /// Death UI 마지막 체크포인트 재시작 버튼의 대상 스테이지를 해석합니다.
    /// </summary>
    private StageDefinition ResolveRestartLastCheckpointTargetStage()
    {
        if (_restartLastCheckpointTargetStage != null)
        {
            return _restartLastCheckpointTargetStage;
        }

        return TryResolveCurrentStageDefinition(out StageDefinition currentStage) ? currentStage : null;
    }

    /// <summary>
    /// 시작 체크포인트 리셋을 현재 활성 씬의 CheckpointStageController에 적용해도 되는지 판정합니다.
    /// </summary>
    private bool ShouldResetActiveSceneEntryCheckpoint(StageDefinition restartStage)
    {
        if (restartStage == null)
        {
            return true;
        }

        string activeSceneName = SceneManager.GetActiveScene().name; // 현재 CheckpointStageController가 관리하는 활성 씬 이름입니다.
        return string.Equals(restartStage.SceneName, activeSceneName, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// StageCatalog 순서에서 현재 스테이지 다음 플레이 가능 스테이지를 조회합니다.
    /// </summary>
    private bool TryResolveNextStage(out StageDefinition nextStage)
    {
        nextStage = null;
        if (_stageCatalog == null || !TryResolveCurrentStageDefinition(out StageDefinition currentStage))
        {
            return false;
        }

        int currentIndex = -1; // StageCatalog 안에서 현재 스테이지가 배치된 인덱스입니다.
        for (int i = 0; i < _stageCatalog.Stages.Count; i++)
        {
            if (_stageCatalog.Stages[i] == currentStage)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            return false;
        }

        for (int i = currentIndex + 1; i < _stageCatalog.Stages.Count; i++)
        {
            StageDefinition candidate = _stageCatalog.Stages[i]; // 다음 스테이지 후보입니다.
            if (candidate == null || candidate.IsTownStage || !candidate.IsEntryEnabled)
            {
                continue;
            }

            nextStage = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 직렬화 참조 또는 싱글톤에서 GameFlowController를 해석합니다.
    /// </summary>
    private GameFlowController ResolveGameFlowController()
    {
        return _gameFlowController != null ? _gameFlowController : GameFlowController.Instance;
    }

    /// <summary>
    /// 직렬화 참조 또는 싱글톤에서 SceneTransitionService를 해석합니다.
    /// </summary>
    private SceneTransitionService ResolveSceneTransitionService()
    {
        return _sceneTransitionService != null ? _sceneTransitionService : SceneTransitionService.Instance;
    }

    /// <summary>
    /// 선택 참조를 현재 씬에서 자동 탐색합니다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_endingPanelView == null)
        {
            EndingPanelView[] endingViews = FindObjectsByType<EndingPanelView>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 비활성 UI까지 포함해 찾은 엔딩 View 후보입니다.
            if (endingViews.Length > 0)
            {
                _endingPanelView = endingViews[0];
            }
        }

        if (_deathPanelView == null)
        {
            DeathPanelView[] deathViews = FindObjectsByType<DeathPanelView>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 비활성 UI까지 포함해 찾은 사망 View 후보입니다.
            if (deathViews.Length > 0)
            {
                _deathPanelView = deathViews[0];
            }
        }

        if (_sceneTransitionService == null)
        {
            SceneTransitionService.TryGetExistingInstance(out _sceneTransitionService);
        }

        if (_gameFlowController == null)
        {
            _gameFlowController = GameFlowController.Instance;
        }
    }

    /// <summary>
    /// UI View 버튼 이벤트를 구독하거나 해제합니다.
    /// </summary>
    private void BindViews(bool shouldBind)
    {
        if (_endingPanelView != null)
        {
            _endingPanelView.NextStageRequested -= RequestNextStage;
            _endingPanelView.ReturnToTownRequested -= RequestReturnToTown;
            _endingPanelView.RestartStageEntryRequested -= RequestRestartStageEntry;

            if (shouldBind)
            {
                _endingPanelView.NextStageRequested += RequestNextStage;
                _endingPanelView.ReturnToTownRequested += RequestReturnToTown;
                _endingPanelView.RestartStageEntryRequested += RequestRestartStageEntry;
            }
        }

        if (_deathPanelView != null)
        {
            _deathPanelView.RestartLastCheckpointRequested -= RequestRestartLastCheckpoint;
            _deathPanelView.ReturnToTownRequested -= RequestReturnToTown;
            _deathPanelView.QuitGameRequested -= RequestQuitGame;

            if (shouldBind)
            {
                _deathPanelView.RestartLastCheckpointRequested += RequestRestartLastCheckpoint;
                _deathPanelView.ReturnToTownRequested += RequestReturnToTown;
                _deathPanelView.QuitGameRequested += RequestQuitGame;
            }
        }
    }
}

/// <summary>
/// 스테이지 결과 UI에서 Host로 전달할 요청 종류입니다.
/// </summary>
public enum E_StageOutcomeRequest
{
    NextStage = 0,
    ReturnToTown = 1,
    RestartStageEntry = 2,
    RestartLastCheckpoint = 3,
    QuitGame = 4
}
