using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 보스 사망 후 로컬 플레이어 입력을 제한하고 지연 시간 뒤 엔딩 UI를 표시합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossEndingSequenceController : NetworkBehaviour, IHealthListener
{
    [Header("Boss")]
    [Tooltip("사망 이벤트를 구독할 보스 HealthComponent입니다. 비어 있으면 같은 오브젝트의 BossController 또는 HealthComponent에서 자동 탐색합니다.")]
    [SerializeField] private HealthComponent _bossHealthComponent; // 엔딩 시퀀스 시작 기준으로 사용할 보스 체력 컴포넌트입니다.

    [Header("Ending UI")]
    [Tooltip("엔딩 UI 표시를 담당할 View입니다. 비어 있으면 활성/비활성 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private EndingPanelView _endingPanelView; // 엔딩 UI 표시 상태를 적용할 View 참조입니다.

    [Tooltip("보스 사망 후 엔딩 UI를 표시하기까지 기다릴 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _endingUiDelaySeconds = 3f; // 보스 사망 연출을 보여 준 뒤 엔딩 UI를 띄우기 위한 대기 시간입니다.

    [Tooltip("엔딩 UI 대기 시간을 Time.timeScale 영향을 받지 않는 시간으로 계산할지 여부입니다.")]
    [SerializeField] private bool _useUnscaledDelayTime = true; // Pause나 타임스케일 변화와 무관하게 엔딩 대기 시간을 흐르게 할지 결정합니다.

    [Header("Input Restriction")]
    [Tooltip("엔딩 시퀀스 중 InputManager의 Gameplay 입력 값을 차단할지 여부입니다.")]
    [SerializeField] private bool _blockGameplayInput = true; // 로컬 Gameplay 입력 버퍼를 0으로 유지할지 결정합니다.

    [Tooltip("엔딩 시퀀스 중 로컬 플레이어 캐릭터의 PlayerMovement 이동 잠금을 적용할지 여부입니다.")]
    [SerializeField] private bool _lockLocalPlayerMovement = true; // 로컬 플레이어의 이동 시스템에도 잠금을 적용할지 결정합니다.

    [Tooltip("이동 잠금 적용 시 플레이어의 수평 속도를 즉시 제거할지 여부입니다.")]
    [SerializeField] private bool _clearHorizontalVelocityOnLock = true; // 엔딩 진입 직후 미끄러지는 움직임을 멈출지 결정합니다.

    [Header("Debug")]
    [Tooltip("필수 참조가 없거나 네트워크 동기화를 수행할 수 없을 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingReferences = true; // 디자이너 설정 누락을 빠르게 확인하기 위한 경고 출력 여부입니다.

    private readonly object _gameplayInputBlocker = new object(); // InputManager에 등록할 엔딩 전용 입력 차단 토큰입니다.
    private readonly List<PlayerMovement> _lockedLocalPlayerMovements = new List<PlayerMovement>(); // 이 컨트롤러가 이동 잠금을 적용한 로컬 플레이어 목록입니다.
    private Coroutine _endingSequenceCoroutine; // 로컬 엔딩 UI 지연 표시 코루틴입니다.
    private bool _isHealthListenerRegistered; // 보스 HealthComponent 리스너 등록 여부입니다.
    private bool _isEndingSequenceStarted; // 엔딩 시퀀스 중복 시작을 방지하는 플래그입니다.
    private bool _isGameplayInputBlocked; // InputManager 입력 차단 토큰 등록 여부입니다.
    private bool _hasLoggedNetworkSpawnWarning; // NetworkObject Spawn 누락 경고 중복 출력을 막는 플래그입니다.

    /// <summary>
    /// 컴포넌트 초기화 시 선택 참조를 자동 보정하고 Health 이벤트를 구독합니다.
    /// </summary>
    private void Awake()
    {
        ResolveOptionalReferences();
        RegisterHealthListener();
    }

    /// <summary>
    /// 컴포넌트 활성화 시 Health 이벤트 구독 상태를 보장합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveOptionalReferences();
        RegisterHealthListener();
    }

    /// <summary>
    /// 컴포넌트 비활성화 시 이벤트 구독과 로컬 입력 제한을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterHealthListener();
        StopLocalEndingSequenceCoroutine();
        ReleaseLocalInputRestriction();
    }

    /// <summary>
    /// Inspector 값 변경 시 참조와 설정 값을 검증합니다.
    /// </summary>
    private void OnValidate()
    {
        ResolveOptionalReferences();
        ValidateSettings();
    }

    /// <summary>
    /// 체력 변경 이벤트를 수신하지만 엔딩 시퀀스에는 사용하지 않습니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
    }

    /// <summary>
    /// 피해 이벤트를 수신하지만 엔딩 시퀀스는 사망 이벤트에서만 시작합니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// 회복 이벤트를 수신하지만 엔딩 시퀀스에는 사용하지 않습니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 보스 사망 이벤트를 권한 인스턴스에서 확정하고 모든 로컬 환경에 엔딩 시퀀스를 시작시킵니다.
    /// </summary>
    public void OnDied()
    {
        if (!HasEndingSequenceAuthority())
        {
            return;
        }

        StartEndingSequenceAuthoritatively();
    }

    /// <summary>
    /// 보스가 부활하면 로컬 엔딩 시퀀스 상태를 초기화합니다.
    /// </summary>
    public void OnRevived()
    {
        if (!HasEndingSequenceAuthority())
        {
            return;
        }

        ResetEndingSequenceLocally();
    }

    /// <summary>
    /// 최대 체력 변경 이벤트를 수신하지만 엔딩 시퀀스에는 사용하지 않습니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }

    /// <summary>
    /// 권한 인스턴스에서 엔딩 시퀀스 시작 명령을 로컬 또는 네트워크로 전달합니다.
    /// </summary>
    private void StartEndingSequenceAuthoritatively()
    {
        if (_isEndingSequenceStarted)
        {
            return;
        }

        _isEndingSequenceStarted = true;

        NetworkManager networkManager = NetworkManager.Singleton; // 현재 멀티플레이 세션 활성 여부를 확인할 NGO 관리자입니다.
        if (networkManager == null || !networkManager.IsListening)
        {
            BeginEndingSequenceLocally(_endingUiDelaySeconds);
            return;
        }

        if (!IsSpawned)
        {
            if (_warnMissingReferences && !_hasLoggedNetworkSpawnWarning)
            {
                Debug.LogWarning($"[BossEndingSequenceController] NetworkObject가 Spawn되지 않아 엔딩 시퀀스를 로컬에서만 시작합니다. object={name}", this);
                _hasLoggedNetworkSpawnWarning = true;
            }

            BeginEndingSequenceLocally(_endingUiDelaySeconds);
            return;
        }

        BeginEndingSequenceRpc(_endingUiDelaySeconds);
    }

    /// <summary>
    /// 서버가 확정한 엔딩 시퀀스 시작을 모든 클라이언트와 Host에 전달합니다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void BeginEndingSequenceRpc(float delaySeconds)
    {
        BeginEndingSequenceLocally(delaySeconds);
    }

    /// <summary>
    /// 현재 로컬 환경에서 입력을 즉시 제한하고 지연 시간 뒤 엔딩 UI를 표시합니다.
    /// </summary>
    private void BeginEndingSequenceLocally(float delaySeconds)
    {
        _isEndingSequenceStarted = true;
        ApplyLocalInputRestriction();
        StopLocalEndingSequenceCoroutine();
        _endingSequenceCoroutine = StartCoroutine(ShowEndingUiAfterDelay(Mathf.Max(0f, delaySeconds)));
    }

    /// <summary>
    /// 지정한 지연 시간 뒤 엔딩 UI를 표시합니다.
    /// </summary>
    private IEnumerator ShowEndingUiAfterDelay(float delaySeconds)
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

        ShowEndingUi();
        _endingSequenceCoroutine = null;
    }

    /// <summary>
    /// 엔딩 UI View를 찾아 표시 상태로 전환합니다.
    /// </summary>
    private void ShowEndingUi()
    {
        ResolveEndingPanelViewIfNeeded();
        if (_endingPanelView == null)
        {
            if (_warnMissingReferences)
            {
                Debug.LogWarning($"[BossEndingSequenceController] EndingPanelView가 없어 엔딩 UI 표시를 건너뜁니다. object={name}", this);
            }

            return;
        }

        _endingPanelView.SetVisible(true);
        _endingPanelView.SetInteractable(true);
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

        PlayerMovement[] playerMovements = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 현재 씬에서 제어 가능한 플레이어 이동 컴포넌트 후보입니다.
        for (int index = 0; index < playerMovements.Length; index++)
        {
            PlayerMovement playerMovement = playerMovements[index]; // 로컬 소유 여부를 검사할 플레이어 이동 컴포넌트입니다.
            if (playerMovement == null || !IsLocalControllablePlayer(playerMovement) || _lockedLocalPlayerMovements.Contains(playerMovement))
            {
                continue;
            }

            playerMovement.AddMovementLock(E_MovementLockReason.Ending, _clearHorizontalVelocityOnLock);
            _lockedLocalPlayerMovements.Add(playerMovement);
        }
    }

    /// <summary>
    /// 로컬 입력 제한을 해제합니다.
    /// </summary>
    private void ReleaseLocalInputRestriction()
    {
        if (_isGameplayInputBlocked)
        {
            InputManager.RemoveGameplayInputBlocker(_gameplayInputBlocker);
            _isGameplayInputBlocked = false;
        }

        for (int index = _lockedLocalPlayerMovements.Count - 1; index >= 0; index--)
        {
            PlayerMovement playerMovement = _lockedLocalPlayerMovements[index]; // 이전에 이 컨트롤러가 잠근 플레이어 이동 컴포넌트입니다.
            if (playerMovement != null)
            {
                playerMovement.RemoveMovementLock(E_MovementLockReason.Ending);
            }
        }

        _lockedLocalPlayerMovements.Clear();
    }

    /// <summary>
    /// 현재 PlayerMovement가 이 로컬 환경에서 제어해야 할 플레이어인지 판단합니다.
    /// </summary>
    private bool IsLocalControllablePlayer(PlayerMovement playerMovement)
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 싱글플레이와 멀티플레이 분기를 위한 NGO 관리자입니다.
        if (networkManager == null || !networkManager.IsListening)
        {
            return true;
        }

        NetworkObject networkObject = playerMovement.GetComponentInParent<NetworkObject>(); // 플레이어 소유권 판정을 위한 NetworkObject입니다.
        return networkObject == null || networkObject.IsOwner;
    }

    /// <summary>
    /// 엔딩 시퀀스를 확정할 권한이 현재 인스턴스에 있는지 반환합니다.
    /// </summary>
    private bool HasEndingSequenceAuthority()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 네트워크 권한 판정을 위한 NGO 관리자입니다.
        if (networkManager == null || !networkManager.IsListening)
        {
            return true;
        }

        return IsServer;
    }

    /// <summary>
    /// 보스 HealthComponent 이벤트 구독을 등록합니다.
    /// </summary>
    private void RegisterHealthListener()
    {
        if (_isHealthListenerRegistered || _bossHealthComponent == null)
        {
            return;
        }

        _bossHealthComponent.AddListener(this);
        _isHealthListenerRegistered = true;
    }

    /// <summary>
    /// 보스 HealthComponent 이벤트 구독을 해제합니다.
    /// </summary>
    private void UnregisterHealthListener()
    {
        if (!_isHealthListenerRegistered || _bossHealthComponent == null)
        {
            return;
        }

        _bossHealthComponent.RemoveListener(this);
        _isHealthListenerRegistered = false;
    }

    /// <summary>
    /// 실행 중인 로컬 엔딩 UI 지연 코루틴을 중지합니다.
    /// </summary>
    private void StopLocalEndingSequenceCoroutine()
    {
        if (_endingSequenceCoroutine == null)
        {
            return;
        }

        StopCoroutine(_endingSequenceCoroutine);
        _endingSequenceCoroutine = null;
    }

    /// <summary>
    /// 부활 또는 재사용 상황에서 로컬 엔딩 시퀀스 상태를 초기화합니다.
    /// </summary>
    private void ResetEndingSequenceLocally()
    {
        _isEndingSequenceStarted = false;
        StopLocalEndingSequenceCoroutine();
        ReleaseLocalInputRestriction();

        if (_endingPanelView != null)
        {
            _endingPanelView.SetVisible(false);
        }
    }

    /// <summary>
    /// 선택 참조가 비어 있으면 현재 오브젝트 기준으로 자동 탐색합니다.
    /// </summary>
    private void ResolveOptionalReferences()
    {
        if (_bossHealthComponent == null)
        {
            BossController bossController = GetComponent<BossController>(); // 같은 보스 루트에 있는 기존 BossController 참조입니다.
            if (bossController != null)
            {
                _bossHealthComponent = bossController.HealthComponent;
            }
        }

        if (_bossHealthComponent == null)
        {
            _bossHealthComponent = GetComponent<HealthComponent>();
        }

        ResolveEndingPanelViewIfNeeded();
    }

    /// <summary>
    /// 엔딩 UI View 참조가 비어 있으면 씬에서 자동 탐색합니다.
    /// </summary>
    private void ResolveEndingPanelViewIfNeeded()
    {
        if (_endingPanelView != null)
        {
            return;
        }

        EndingPanelView[] candidates = FindObjectsByType<EndingPanelView>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 비활성 UI까지 포함해 찾은 엔딩 View 후보입니다.
        if (candidates.Length > 0)
        {
            _endingPanelView = candidates[0];
        }
    }

    /// <summary>
    /// Inspector 설정 값의 유효 범위를 보정합니다.
    /// </summary>
    private void ValidateSettings()
    {
        if (_endingUiDelaySeconds < 0f)
        {
            _endingUiDelaySeconds = 0f;
        }
    }
}
