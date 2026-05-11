using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 보스 처치, 보스룸 플레이어 사망, 멀티플레이 전원 사망 결과를 UI와 스테이지 흐름으로 연결합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossRoomOutcomeController : NetworkBehaviour, IHealthListener
{
    [Header("Boss")]
    [Tooltip("처치 이벤트를 구독할 보스 HealthComponent입니다. 비어 있으면 같은 오브젝트의 BossController 또는 HealthComponent에서 자동 탐색합니다.")]
    [SerializeField] private HealthComponent _bossHealthComponent; // 보스 처치 결과를 감지할 체력 컴포넌트입니다.

    [Header("UI")]
    [Tooltip("보스 처치 또는 싱글플레이 보스룸 사망 시 표시할 엔딩 UI입니다. 비어 있으면 현재 씬에서 자동 탐색합니다.")]
    [SerializeField] private EndingPanelView _endingPanelView; // 엔딩 결과 표시와 버튼 이벤트를 제공하는 View입니다.

    [Tooltip("멀티플레이 전원 사망 시 표시할 사망 UI입니다. 비어 있으면 현재 씬에서 자동 탐색합니다.")]
    [SerializeField] private DeathPanelView _deathPanelView; // 전원 사망 결과 표시와 버튼 이벤트를 제공하는 View입니다.

    [Tooltip("스테이지 클리어와 결과 버튼 흐름을 담당할 컨트롤러입니다. 비어 있으면 현재 씬에서 자동 탐색합니다.")]
    [SerializeField] private StageOutcomeFlowController _stageOutcomeFlowController; // Stage 결과 진행도와 버튼 흐름을 처리할 컨트롤러입니다.

    [Header("Timing")]
    [Tooltip("보스 처치 후 엔딩 UI를 표시하기 전 대기 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _bossDefeatedEndingDelaySeconds = 3f; // 보스 처치 연출을 보여줄 엔딩 UI 지연 시간입니다.

    [Tooltip("보스룸 플레이어 사망 후 결과 UI를 표시하기 전 대기 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _playerDeathPanelDelaySeconds; // 플레이어 사망 결과 UI 지연 시간입니다.

    [Tooltip("결과 UI 대기 시간을 Time.timeScale 영향을 받지 않는 시간으로 계산할지 여부입니다.")]
    [SerializeField] private bool _useUnscaledDelayTime = true; // Pause 상태와 무관하게 UI 지연 시간을 계산할지 결정합니다.

    [Header("Camera Effect")]
    [Tooltip("Death 또는 Ending UI를 표시하기 직전에 재생할 FadeOut CameraEffectPreset입니다.")]
    [SerializeField] private CameraEffectPresetBase _outcomeFadeOutPreset; // 결과 UI 표시 직전에 화면을 자연스럽게 가리는 카메라 이펙트 프리셋입니다.

    [Tooltip("Death 또는 Ending UI가 준비된 직후 재생할 FadeIn CameraEffectPreset입니다.")]
    [SerializeField] private CameraEffectPresetBase _outcomeFadeInPreset; // 결과 UI 표시 직후 화면을 다시 보여주는 카메라 이펙트 프리셋입니다.

    [Header("Boss Room Death Rule")]
    [Tooltip("싱글플레이에서 보스룸 플레이어가 죽으면 사망 UI를 표시할지 여부입니다. 보스 처치 엔딩과 플레이어 사망 결과를 분리합니다.")]
    [SerializeField] private bool _showDeathPanelOnSinglePlayerDeath = true; // 싱글플레이 보스룸 사망 시 사망 UI를 사용할지 결정합니다.

    [Header("Input Restriction")]
    [Tooltip("결과 UI 표시 중 Gameplay 입력을 차단할지 여부입니다.")]
    [SerializeField] private bool _blockGameplayInput = true; // 결과 UI가 열린 동안 로컬 Gameplay 입력을 차단할지 여부입니다.

    [Tooltip("결과 UI 표시 중 로컬 플레이어 이동을 잠글지 여부입니다.")]
    [SerializeField] private bool _lockLocalPlayerMovement = true; // 결과 UI가 열린 동안 로컬 플레이어 이동을 잠글지 여부입니다.

    [Tooltip("이동 잠금 적용 시 수평 속도를 즉시 제거할지 여부입니다.")]
    [SerializeField] private bool _clearHorizontalVelocityOnLock = true; // 결과 UI 진입 직후 잔여 이동을 멈출지 결정합니다.

    [Header("Debug")]
    [Tooltip("필수 참조 누락이나 네트워크 Spawn 누락 경고를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingReferences = true; // 결과 흐름 설정 누락을 빠르게 발견하기 위한 경고 출력 여부입니다.

    private readonly object _gameplayInputBlocker = new object(); // InputManager 전역 차단에 사용할 고유 토큰입니다.
    private readonly System.Collections.Generic.List<PlayerMovement> _lockedLocalPlayerMovements = new System.Collections.Generic.List<PlayerMovement>(); // 결과 UI 동안 이동 잠금을 적용한 로컬 플레이어 목록입니다.
    private Coroutine _showOutcomeCoroutine; // 지연 표시 중인 결과 UI 코루틴입니다.
    private bool _isBossHealthListenerRegistered; // 보스 HealthComponent 리스너 등록 여부입니다.
    private bool _isOutcomeResolved; // 보스룸 결과가 이미 확정되었는지 여부입니다.
    private bool _isGameplayInputBlocked; // InputManager 차단 토큰 등록 여부입니다.
    private bool _hasLoggedNetworkSpawnWarning; // NetworkObject Spawn 누락 경고 중복 방지 플래그입니다.

    /// <summary>
    /// 참조를 보정하고 보스 체력 이벤트를 구독합니다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        RegisterBossHealthListener();
    }

    /// <summary>
    /// 활성화 시 보스 체력과 플레이어 사망 정책 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        RegisterBossHealthListener();
        CheckpointPlayerRespawnHandler.DeathStateChanged += HandlePlayerDeathStateChanged;
        CheckpointPlayerRespawnHandler.AutomaticRespawnSuppressionRequested += ShouldSuppressAutomaticRespawn;
    }

    /// <summary>
    /// 비활성화 시 이벤트 구독과 로컬 입력 제한을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterBossHealthListener();
        CheckpointPlayerRespawnHandler.DeathStateChanged -= HandlePlayerDeathStateChanged;
        CheckpointPlayerRespawnHandler.AutomaticRespawnSuppressionRequested -= ShouldSuppressAutomaticRespawn;
        StopOutcomeCoroutine();
        ReleaseLocalInputRestriction();
    }

    /// <summary>
    /// Inspector 값이 바뀔 때 누락된 참조를 자동 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 체력 변경 이벤트는 결과 판정에 사용하지 않습니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
    }

    /// <summary>
    /// 데미지 이벤트는 결과 판정에 사용하지 않습니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// 회복 이벤트는 결과 판정에 사용하지 않습니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 보스 처치 결과를 권한 인스턴스에서 확정합니다.
    /// </summary>
    public void OnDied()
    {
        if (!HasOutcomeAuthority())
        {
            return;
        }

        ResolveBossDefeatedOutcome();
    }

    /// <summary>
    /// 보스 부활 시 결과 UI 상태를 초기화합니다.
    /// </summary>
    public void OnRevived()
    {
        if (!HasOutcomeAuthority())
        {
            return;
        }

        ResetOutcomeLocally();
    }

    /// <summary>
    /// 최대 체력 변경 이벤트는 결과 판정에 사용하지 않습니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }

    /// <summary>
    /// 플레이어 사망 상태 변화에 따라 싱글 사망 또는 멀티 전원 사망 결과를 확정합니다.
    /// </summary>
    private void HandlePlayerDeathStateChanged(CheckpointPlayerRespawnHandler handler, bool isDead)
    {
        if (!isDead || !HasOutcomeAuthority() || _isOutcomeResolved)
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || networkManager.ConnectedClients.Count <= 1)
        {
            if (_showDeathPanelOnSinglePlayerDeath)
            {
                ResolveSinglePlayerDeathOutcome();
            }

            return;
        }

        if (!CheckpointPlayerRespawnHandler.AreAllConnectedPlayersDead())
        {
            return;
        }

        CheckpointPlayerRespawnHandler.CancelAllScheduledRespawns();
        ResolveAllPlayersDeadOutcome();
    }

    /// <summary>
    /// 보스룸 특수 규칙이 현재 자동 리스폰을 억제해야 하는지 반환합니다.
    /// </summary>
    private bool ShouldSuppressAutomaticRespawn(CheckpointPlayerRespawnHandler handler)
    {
        if (_isOutcomeResolved)
        {
            return true;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || networkManager.ConnectedClients.Count <= 1)
        {
            return _showDeathPanelOnSinglePlayerDeath;
        }

        return CheckpointPlayerRespawnHandler.AreAllConnectedPlayersDead();
    }

    /// <summary>
    /// 보스 처치 결과를 클리어로 기록하고 모든 피어에 엔딩 UI 표시를 요청합니다.
    /// </summary>
    private void ResolveBossDefeatedOutcome()
    {
        if (_isOutcomeResolved)
        {
            return;
        }

        _isOutcomeResolved = true;
        _stageOutcomeFlowController?.MarkCurrentStageCleared();
        ShowOutcomeAuthoritatively(E_BossRoomOutcomeKind.Ending, _bossDefeatedEndingDelaySeconds);
    }

    /// <summary>
    /// 싱글플레이 보스룸 사망 결과를 사망 UI로 표시합니다.
    /// </summary>
    private void ResolveSinglePlayerDeathOutcome()
    {
        if (_isOutcomeResolved)
        {
            return;
        }

        _isOutcomeResolved = true;
        ShowOutcomeAuthoritatively(E_BossRoomOutcomeKind.Death, _playerDeathPanelDelaySeconds);
    }

    /// <summary>
    /// 멀티플레이 전원 사망 결과를 사망 UI로 표시합니다.
    /// </summary>
    private void ResolveAllPlayersDeadOutcome()
    {
        if (_isOutcomeResolved)
        {
            return;
        }

        _isOutcomeResolved = true;
        ShowOutcomeAuthoritatively(E_BossRoomOutcomeKind.Death, _playerDeathPanelDelaySeconds);
    }

    /// <summary>
    /// 권한 인스턴스에서 로컬 또는 네트워크 RPC로 결과 UI 표시를 전파합니다.
    /// </summary>
    private void ShowOutcomeAuthoritatively(E_BossRoomOutcomeKind outcomeKind, float delaySeconds)
    {
        DeactivateAllPlayerBuffsForOutcome(outcomeKind);

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            BeginOutcomeLocally(outcomeKind, delaySeconds);
            return;
        }

        if (!IsSpawned)
        {
            if (_warnMissingReferences && !_hasLoggedNetworkSpawnWarning)
            {
                Debug.LogWarning($"[BossRoomOutcomeController] NetworkObject가 Spawn되지 않아 결과 UI를 Host 로컬에서만 표시합니다. object={name}", this);
                _hasLoggedNetworkSpawnWarning = true;
            }

            BeginOutcomeLocally(outcomeKind, delaySeconds);
            return;
        }

        BeginOutcomeRpc(outcomeKind, delaySeconds);
    }

    /// <summary>
    /// 보스룸 결과가 확정되면 서버 또는 오프라인 권한에서 모든 플레이어 Buff를 종료합니다.
    /// </summary>
    private void DeactivateAllPlayerBuffsForOutcome(E_BossRoomOutcomeKind outcomeKind)
    {
        PlayerBuffController[] buffControllers = FindObjectsByType<PlayerBuffController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 결과 진입 시 정리할 플레이어 Buff 컨트롤러 후보 목록입니다.
        for (int i = 0; i < buffControllers.Length; i++)
        {
            PlayerBuffController buffController = buffControllers[i]; // 서버 권한에서 종료를 요청할 Buff 컨트롤러 후보입니다.
            if (buffController == null)
            {
                continue;
            }

            buffController.RequestStopBuffFromGameplaySystem($"BossRoomOutcome:{outcomeKind}");
        }
    }

    /// <summary>
    /// 결과 UI가 로컬에 열리기 직전에 로컬 오너 플레이어 Buff 종료를 요청합니다.
    /// </summary>
    private void DeactivateLocalPlayerBuffForOutcome(E_BossRoomOutcomeKind outcomeKind)
    {
        PlayerBuffController[] buffControllers = FindObjectsByType<PlayerBuffController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 로컬 오너 판정에 사용할 Buff 컨트롤러 후보 목록입니다.
        for (int i = 0; i < buffControllers.Length; i++)
        {
            PlayerBuffController buffController = buffControllers[i]; // 현재 로컬 인스턴스가 제어할 수 있는지 확인할 Buff 컨트롤러 후보입니다.
            if (buffController == null || !CanLocalInstanceRequestBuffStop(buffController))
            {
                continue;
            }

            buffController.RequestStopBuffFromGameplaySystem($"BossRoomOutcomeLocal:{outcomeKind}");
        }
    }

    /// <summary>
    /// 현재 로컬 인스턴스가 지정한 Buff 컨트롤러의 종료 요청을 보낼 수 있는지 판정합니다.
    /// </summary>
    private bool CanLocalInstanceRequestBuffStop(PlayerBuffController buffController)
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션과 로컬 오너 판정에 사용할 NetworkManager 참조입니다.
        if (networkManager == null || !networkManager.IsListening)
        {
            return true;
        }

        NetworkObject networkObject = buffController.GetComponentInParent<NetworkObject>(); // Buff 컨트롤러가 속한 플레이어 NetworkObject 참조입니다.
        return networkObject == null || networkObject.IsOwner || networkManager.IsServer;
    }

    /// <summary>
    /// Host가 확정한 보스룸 결과 UI 표시를 모든 Client와 Host에 전달합니다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void BeginOutcomeRpc(E_BossRoomOutcomeKind outcomeKind, float delaySeconds)
    {
        BeginOutcomeLocally(outcomeKind, delaySeconds);
    }

    /// <summary>
    /// 로컬 입력을 제한하고 지연 시간 뒤 결과 UI를 표시합니다.
    /// </summary>
    private void BeginOutcomeLocally(E_BossRoomOutcomeKind outcomeKind, float delaySeconds)
    {
        _isOutcomeResolved = true;
        DeactivateLocalPlayerBuffForOutcome(outcomeKind);
        ApplyLocalInputRestriction();
        StopOutcomeCoroutine();
        _showOutcomeCoroutine = StartCoroutine(ShowOutcomeAfterDelay(outcomeKind, Mathf.Max(0f, delaySeconds)));
    }

    /// <summary>
    /// 지정된 지연 시간 뒤 결과 UI를 실제로 표시합니다.
    /// </summary>
    private IEnumerator ShowOutcomeAfterDelay(E_BossRoomOutcomeKind outcomeKind, float delaySeconds)
    {
        if (delaySeconds > 0f)
        {
            if (_useUnscaledDelayTime)
            {
                yield return new WaitForSecondsRealtime(delaySeconds);
            }
            else
            {
                yield return new WaitForSeconds(delaySeconds);
            }
        }

        yield return CameraEffectPlaybackUtility.PlayAndWait(_outcomeFadeOutPreset, gameObject, 0f);
        ShowOutcomePanel(outcomeKind);
        yield return null;
        yield return CameraEffectPlaybackUtility.PlayAndWait(_outcomeFadeInPreset, gameObject, 0f);
        _showOutcomeCoroutine = null;
    }

    /// <summary>
    /// 결과 종류에 맞는 UI View를 표시합니다.
    /// </summary>
    private void ShowOutcomePanel(E_BossRoomOutcomeKind outcomeKind)
    {
        ResolveReferences();
        if (_endingPanelView != null)
        {
            _endingPanelView.SetVisible(false);
        }

        if (_deathPanelView != null)
        {
            _deathPanelView.SetVisible(false);
        }

        if (outcomeKind == E_BossRoomOutcomeKind.Ending && _endingPanelView != null)
        {
            _endingPanelView.SetVisible(true);
        }

        if (outcomeKind == E_BossRoomOutcomeKind.Death && _deathPanelView != null)
        {
            _deathPanelView.SetVisible(true);
        }

        if (outcomeKind == E_BossRoomOutcomeKind.Ending && _endingPanelView == null && _warnMissingReferences)
        {
            Debug.LogWarning("[BossRoomOutcomeController] EndingPanelView가 없어 엔딩 UI를 표시하지 못했습니다.", this);
        }

        if (outcomeKind == E_BossRoomOutcomeKind.Death && _deathPanelView == null && _warnMissingReferences)
        {
            Debug.LogWarning("[BossRoomOutcomeController] DeathPanelView가 없어 사망 UI를 표시하지 못했습니다.", this);
        }
    }

    /// <summary>
    /// 로컬 Gameplay 입력과 로컬 플레이어 이동을 제한합니다.
    /// </summary>
    private void ApplyLocalInputRestriction()
    {
        if (_blockGameplayInput && !_isGameplayInputBlocked)
        {
            InputManager.AddGameplayInputBlocker(_gameplayInputBlocker);
            _isGameplayInputBlocked = true;
        }

        if (!_lockLocalPlayerMovement)
        {
            return;
        }

        PlayerMovement[] playerMovements = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < playerMovements.Length; i++)
        {
            PlayerMovement playerMovement = playerMovements[i]; // 로컬 제어 대상 여부를 확인할 플레이어 이동 컴포넌트입니다.
            if (playerMovement == null || !IsLocalControllablePlayer(playerMovement) || _lockedLocalPlayerMovements.Contains(playerMovement))
            {
                continue;
            }

            playerMovement.AddMovementLock(E_MovementLockReason.Ending, _clearHorizontalVelocityOnLock);
            _lockedLocalPlayerMovements.Add(playerMovement);
        }
    }

    /// <summary>
    /// 결과 UI에서 적용한 로컬 입력 제한을 해제합니다.
    /// </summary>
    private void ReleaseLocalInputRestriction()
    {
        if (_isGameplayInputBlocked)
        {
            InputManager.RemoveGameplayInputBlocker(_gameplayInputBlocker);
            _isGameplayInputBlocked = false;
        }

        for (int i = _lockedLocalPlayerMovements.Count - 1; i >= 0; i--)
        {
            PlayerMovement playerMovement = _lockedLocalPlayerMovements[i];
            if (playerMovement != null)
            {
                playerMovement.RemoveMovementLock(E_MovementLockReason.Ending);
            }
        }

        _lockedLocalPlayerMovements.Clear();
    }

    /// <summary>
    /// 현재 PlayerMovement가 로컬에서 제어 가능한 플레이어인지 판정합니다.
    /// </summary>
    private bool IsLocalControllablePlayer(PlayerMovement playerMovement)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            return true;
        }

        NetworkObject networkObject = playerMovement.GetComponentInParent<NetworkObject>();
        return networkObject == null || networkObject.IsOwner;
    }

    /// <summary>
    /// 보스룸 결과를 확정할 권한이 있는지 판정합니다.
    /// </summary>
    private bool HasOutcomeAuthority()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager == null || !networkManager.IsListening || networkManager.IsServer;
    }

    /// <summary>
    /// 보스 HealthComponent 리스너를 등록합니다.
    /// </summary>
    private void RegisterBossHealthListener()
    {
        if (_isBossHealthListenerRegistered || _bossHealthComponent == null)
        {
            return;
        }

        _bossHealthComponent.AddListener(this);
        _isBossHealthListenerRegistered = true;
    }

    /// <summary>
    /// 보스 HealthComponent 리스너를 해제합니다.
    /// </summary>
    private void UnregisterBossHealthListener()
    {
        if (!_isBossHealthListenerRegistered || _bossHealthComponent == null)
        {
            return;
        }

        _bossHealthComponent.RemoveListener(this);
        _isBossHealthListenerRegistered = false;
    }

    /// <summary>
    /// 진행 중인 결과 UI 지연 코루틴을 중지합니다.
    /// </summary>
    private void StopOutcomeCoroutine()
    {
        if (_showOutcomeCoroutine == null)
        {
            return;
        }

        StopCoroutine(_showOutcomeCoroutine);
        _showOutcomeCoroutine = null;
    }

    /// <summary>
    /// 보스룸 결과 UI와 입력 제한 상태를 초기화합니다.
    /// </summary>
    private void ResetOutcomeLocally()
    {
        _isOutcomeResolved = false;
        StopOutcomeCoroutine();
        ReleaseLocalInputRestriction();

        if (_endingPanelView != null)
        {
            _endingPanelView.SetVisible(false);
        }

        if (_deathPanelView != null)
        {
            _deathPanelView.SetVisible(false);
        }
    }

    /// <summary>
    /// 선택 참조를 같은 오브젝트 또는 현재 씬에서 자동 탐색합니다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_bossHealthComponent == null)
        {
            BossController bossController = GetComponent<BossController>();
            if (bossController != null)
            {
                _bossHealthComponent = bossController.HealthComponent;
            }
        }

        if (_bossHealthComponent == null)
        {
            _bossHealthComponent = GetComponent<HealthComponent>();
        }

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

        if (_stageOutcomeFlowController == null)
        {
            StageOutcomeFlowController[] flowControllers = FindObjectsByType<StageOutcomeFlowController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 비활성 오브젝트까지 포함해 찾은 결과 흐름 컨트롤러 후보입니다.
            if (flowControllers.Length > 0)
            {
                _stageOutcomeFlowController = flowControllers[0];
            }
        }
    }
}

/// <summary>
/// 보스룸 결과 UI 종류입니다.
/// </summary>
public enum E_BossRoomOutcomeKind
{
    Ending = 0,
    Death = 1
}
