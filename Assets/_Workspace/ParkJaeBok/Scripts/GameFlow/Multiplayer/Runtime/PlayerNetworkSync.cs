using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 플레이어 이동 상태(액션/방향)를 네트워크로 동기화하기 위한 NetworkTransform 보유/검증 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class PlayerNetworkSync : NetworkBehaviour, IHealthListener
{
    [Header("Dependencies")]
    [Tooltip("플레이어 Transform 동기화를 담당하는 NetworkTransform 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private NetworkTransform _networkTransform; // 원격 플레이어 위치 동기화를 처리하는 NetworkTransform 참조입니다.
    [Tooltip("네트워크로 확정된 액션 상태를 조회/적용할 ActionController 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private ActionController _actionController; // 네트워크 액션 상태를 생산/소비할 ActionController 참조입니다.
    [Tooltip("네트워크로 확정된 좌우 방향 상태를 읽고 반영할 PlayerMovement 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 바라보는 방향 동기화 원본/적용을 담당할 PlayerMovement 참조입니다.

    [Tooltip("서버 확정 체력 상태를 복제하고 로컬 HealthComponent에 반영할 대상 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // 서버 확정 체력 스냅샷을 기록할 HealthComponent 참조입니다.

    [Header("Action State Sync")]
    [Tooltip("Owner가 확정한 액션 상태를 서버를 통해 전파할지 여부입니다.")]
    [SerializeField] private bool _enableActionStateSync = true; // 액션 상태 네트워크 동기화 활성화 여부입니다.
    [Tooltip("액션 상태 전송 간 최소 간격(초)입니다.")]
    [SerializeField] private float _actionStateSendInterval = 0.033f; // 액션 상태 전송 빈도를 제한하기 위한 최소 간격입니다.
    [Tooltip("Running 상태에서 필터 없이 모든 액션 타입을 복제할지 여부입니다.")]
    [SerializeField] private bool _replicateAllActionTypes = true; // Running 상태에서 액션 타입 필터를 비활성화하고 전체 액션을 복제할지 여부입니다.
    [Tooltip("모든 액션 복제를 사용하지 않을 때 추가로 복제 허용할 액션 타입 목록입니다.")]
    [SerializeField]
    private E_ActionType[] _additionalReplicatedActions = new E_ActionType[]
    {
        E_ActionType.Attack,
        E_ActionType.AttackCombo1,
        E_ActionType.AttackCombo2,
        E_ActionType.AttackCombo3,
        E_ActionType.AttackAir,
        E_ActionType.AttackDash,
        E_ActionType.AttackWall,
    }; // 기본 이동 액션 외에도 복제를 허용할 추가 액션 타입 목록입니다.
    [Tooltip("ActionController 참조 누락 시 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingActionController = true; // ActionController 참조 누락 경고 출력 여부입니다.
    [Tooltip("서버가 강제로 확정해야 하는 피격 계열 액션 목록입니다. Owner 입력보다 서버 확정값을 우선 적용합니다.")]
    [SerializeField]
    private E_ActionType[] _serverAuthoritativeActions = new E_ActionType[]
    {
        E_ActionType.Hit,
        E_ActionType.Break,
        E_ActionType.Die,
    }; // 서버 확정값으로 우선 처리할 액션 타입 목록입니다.
    [Tooltip("Owner 클라이언트에도 서버 확정 피격 계열 액션을 재적용할지 여부입니다.")]
    [SerializeField] private bool _applyServerAuthoritativeActionsToOwner = true; // Owner 화면에 서버 확정 피격 계열 액션을 재적용할지 여부입니다.

    [Header("Facing Direction Sync")]
    [Tooltip("Owner가 바라보는 방향 상태를 서버로 전송할지 여부입니다.")]
    [SerializeField] private bool _enableFacingDirectionSync = true; // 좌우 방향 네트워크 동기화 활성화 여부입니다.
    [Tooltip("방향 상태 전송 간 최소 간격(초)입니다.")]
    [SerializeField] private float _facingDirectionSendInterval = 0.05f; // 방향 상태 전송 빈도를 제한하기 위한 최소 간격입니다.
    [Tooltip("PlayerMovement 참조 누락 시 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingPlayerMovement = true; // PlayerMovement 참조 누락 경고 출력 여부입니다.
    [Tooltip("Owner 클라이언트에도 서버 복제 방향값을 재적용할지 여부입니다. 기본값(false)은 Owner 입력 응답성을 우선합니다.")]
    [SerializeField] private bool _applyReplicatedFacingToOwner = false; // Owner 로컬 인스턴스에 복제 방향값 재적용 여부를 제어하는 플래그입니다.

    [Header("Health Sync")]
    [Tooltip("서버 확정 체력 상태를 네트워크로 복제할지 여부입니다.")]
    [SerializeField] private bool _enableHealthStateSync = true; // 체력 상태 복제 활성화 여부입니다.
    [Tooltip("Owner 클라이언트에도 서버 확정 체력 값을 적용할지 여부입니다.")]
    [SerializeField] private bool _applyReplicatedHealthToOwner = true; // Owner 화면에 서버 체력 상태를 반영할지 여부입니다.
    [Tooltip("HealthComponent 참조가 없을 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingHealthComponent = true; // HealthComponent 누락 경고 출력 여부입니다.

    [Header("Facing Debug")]
    [Tooltip("디버그용: 마지막으로 Owner가 전송한 방향 상태입니다.")]
    [SerializeField] private bool _lastSentFacingRight = true; // 마지막 전송 방향 상태를 인스펙터에서 확인하기 위한 디버그 값입니다.
    [Tooltip("디버그용: 마지막으로 복제된 방향 상태입니다.")]
    [SerializeField] private bool _lastReplicatedFacingRight = true; // 마지막 복제 방향 상태를 인스펙터에서 확인하기 위한 디버그 값입니다.

    private readonly NetworkVariable<int> _replicatedActionType = new NetworkVariable<int>(
        (int)E_ActionType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 액션 타입을 모든 관찰자에게 복제하는 네트워크 변수입니다.
    private readonly NetworkVariable<bool> _replicatedActionRunning = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 액션 실행 여부를 모든 관찰자에게 복제하는 네트워크 변수입니다.
    private readonly NetworkVariable<int> _replicatedActionStartType = new NetworkVariable<int>(
        (int)E_ActionType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버가 확정한 최신 액션 시작 타입입니다.
    private readonly NetworkVariable<int> _replicatedActionStartRevision = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버가 확정한 액션 시작 이벤트 revision 값입니다.
    private readonly NetworkVariable<bool> _replicatedFacingRight = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 좌우 방향 상태를 모든 관찰자에게 복제하는 네트워크 변수입니다.

    private E_ActionType _lastSentActionType = E_ActionType.None; // Owner가 서버에 마지막으로 전송한 액션 타입 캐시입니다.
    private bool _lastSentIsRunning; // Owner가 서버에 마지막으로 전송한 액션 실행 여부 캐시입니다.
    private float _nextActionStateSendTime; // 다음 액션 상태 전송 가능 시각(초)입니다.
    private float _nextFacingDirectionSendTime; // 다음 방향 상태 전송 가능 시각(초)입니다.

    /// <summary>
    /// 초기화 시 NetworkTransform/의존성 참조를 캐시합니다.
    /// </summary>
    private readonly NetworkVariable<float> _replicatedCurrentHealth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 현재 체력 값입니다.
    private readonly NetworkVariable<float> _replicatedMaxHealth = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 최대 체력 값입니다.
    private readonly NetworkVariable<bool> _replicatedIsDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 사망 상태 값입니다.
    private readonly NetworkVariable<int> _replicatedHealthRevision = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 체력 스냅샷 갱신 순번입니다.

    private bool _isHealthListenerRegistered; // 서버 HealthComponent 리스너 등록 여부를 추적하는 플래그입니다.
    private int _lastAppliedHealthRevision = -1; // 로컬 HealthComponent에 마지막으로 반영한 체력 스냅샷 순번입니다.
    private int _lastAppliedActionStartRevision = -1; // 로컬 관찰자 인스턴스가 마지막으로 처리한 액션 시작 revision 값입니다.
    private int _lastPublishedServerAuthoritativeExecutionId = -1; // 서버 권한 액션 시작 이벤트의 중복 발행을 막기 위한 마지막 실행 ID입니다.

    private void Awake()
    {
        if (_networkTransform == null)
        {
            _networkTransform = GetComponent<NetworkTransform>();
        }

        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
        }

        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }

        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }
    }

    /// <summary>
    /// 네트워크 스폰 시 액션/방향 복제 콜백을 바인딩하고 초기 상태를 적용합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        _replicatedActionType.OnValueChanged += HandleReplicatedActionTypeChanged;
        _replicatedActionRunning.OnValueChanged += HandleReplicatedActionRunningChanged;
        _replicatedActionStartRevision.OnValueChanged += HandleReplicatedActionStartRevisionChanged;
        _replicatedFacingRight.OnValueChanged += HandleReplicatedFacingDirectionChanged;
        _replicatedHealthRevision.OnValueChanged += HandleReplicatedHealthRevisionChanged;

        if (_enableFacingDirectionSync && TryResolvePlayerMovement())
        {
            _playerMovement.FacingDirectionChanged += HandleLocalFacingDirectionChanged;

            if (IsServer)
            {
                _replicatedFacingRight.Value = _playerMovement.IsFacingRight;
            }

            _lastSentFacingRight = _playerMovement.IsFacingRight;
            _lastReplicatedFacingRight = _replicatedFacingRight.Value;
            ApplyReplicatedFacingDirection(_replicatedFacingRight.Value);
        }

        if (_enableActionStateSync)
        {
            TryApplyReplicatedActionStartEvent();
            TryApplyReplicatedActionState();
        }

        if (_enableHealthStateSync)
        {
            RegisterHealthListener();

            if (IsServer)
            {
                PublishHealthSnapshot();
            }
            else
            {
                ApplyReplicatedHealthState();
            }
        }
    }

    /// <summary>
    /// 네트워크 디스폰 시 액션/방향 복제 콜백 바인딩을 해제합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        _replicatedActionType.OnValueChanged -= HandleReplicatedActionTypeChanged;
        _replicatedActionRunning.OnValueChanged -= HandleReplicatedActionRunningChanged;
        _replicatedActionStartRevision.OnValueChanged -= HandleReplicatedActionStartRevisionChanged;
        _replicatedFacingRight.OnValueChanged -= HandleReplicatedFacingDirectionChanged;
        _replicatedHealthRevision.OnValueChanged -= HandleReplicatedHealthRevisionChanged;

        if (_playerMovement != null)
        {
            _playerMovement.FacingDirectionChanged -= HandleLocalFacingDirectionChanged;
        }

        UnregisterHealthListener();
    }

    /// <summary>
    /// Owner 인스턴스에서 현재 액션/방향 상태를 샘플링하고 서버 확정 동기화를 수행합니다.
    /// </summary>
    private void Update()
    {
        if (_enableHealthStateSync)
        {
            MaintainHealthSyncBinding();
        }

        if (_enableActionStateSync)
        {
            MaintainServerAuthoritativeActionState();
        }

        if (!IsSpawned || !IsOwner)
        {
            return;
        }

        if (_enableActionStateSync)
        {
            TrySendActionState();
        }

        if (_enableFacingDirectionSync)
        {
            TrySendFacingDirection();
        }
    }

    /// <summary>
    /// 서버/클라이언트 역할에 맞춰 체력 동기화 바인딩과 스냅샷 반영을 유지합니다.
    /// </summary>
    private void MaintainHealthSyncBinding()
    {
        if (!IsSpawned)
        {
            return;
        }

        if (IsServer)
        {
            if (!_isHealthListenerRegistered)
            {
                RegisterHealthListener();

                if (_isHealthListenerRegistered)
                {
                    PublishHealthSnapshot();
                }
            }

            return;
        }

        if (_lastAppliedHealthRevision == _replicatedHealthRevision.Value)
        {
            return;
        }

        ApplyReplicatedHealthState();
    }

    /// <summary>
    /// Owner 인스턴스에서 액션 상태 전송 조건을 검증한 뒤 변경분만 서버에 전송합니다.
    /// </summary>
    private void TrySendActionState()
    {
        if (!TryResolveActionController())
        {
            return;
        }

        if (Time.time < _nextActionStateSendTime)
        {
            return;
        }

        ActionRuntime runtime = _actionController.Runtime; // 현재 프레임 Owner가 확정한 액션 런타임 스냅샷입니다.
        E_ActionType currentActionType = runtime.IsRunning ? runtime.ActionType : E_ActionType.None; // 네트워크 전송용 액션 타입 스냅샷입니다.
        bool currentIsRunning = runtime.IsRunning; // 네트워크 전송용 액션 실행 여부 스냅샷입니다.

        if (currentIsRunning && !IsReplicatedAction(currentActionType))
        {
            return;
        }

        if (_lastSentActionType == currentActionType && _lastSentIsRunning == currentIsRunning)
        {
            return;
        }

        SubmitActionStateRpc((int)currentActionType, currentIsRunning);
        _lastSentActionType = currentActionType;
        _lastSentIsRunning = currentIsRunning;
        _nextActionStateSendTime = Time.time + Mathf.Max(0.01f, _actionStateSendInterval);
    }

    /// <summary>
    /// Owner 인스턴스에서 방향 상태 전송 조건을 검증한 뒤 변경분만 서버에 전송합니다.
    /// </summary>
    private void TrySendFacingDirection()
    {
        if (!TryResolvePlayerMovement())
        {
            return;
        }

        if (Time.time < _nextFacingDirectionSendTime)
        {
            return;
        }

        SubmitFacingDirectionIfChanged(_playerMovement.IsFacingRight);
    }

    /// <summary>
    /// 로컬 PlayerMovement 방향 이벤트를 수신해 방향 상태 동기화를 즉시 시도합니다.
    /// </summary>
    private void HandleLocalFacingDirectionChanged(bool isFacingRight)
    {
        if (!_enableFacingDirectionSync || !IsSpawned || !IsOwner)
        {
            return;
        }

        SubmitFacingDirectionIfChanged(isFacingRight);
    }

    /// <summary>
    /// Owner가 보고한 액션 상태를 서버에서 소유권 검증 후 확정해 복제 변수에 기록합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SubmitActionStateRpc(int actionTypeValue, bool isRunning, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning($"[PlayerNetworkSync] Unauthorized action sync sender. object={name}, sender={rpcParams.Receive.SenderClientId}, owner={OwnerClientId}", this);
            return;
        }

        if (TryResolveActionController())
        {
            ActionRuntime runtime = _actionController.Runtime; // 서버 인스턴스에서 현재 유지 중인 액션 상태 스냅샷입니다.
            if (runtime.IsRunning && IsServerAuthoritativeAction(runtime.ActionType))
            {
                return;
            }
        }

        PublishReplicatedActionState(actionTypeValue, isRunning, shouldPublishStartEvent: isRunning);
    }

    /// <summary>
    /// Owner가 보고한 방향 상태를 서버에서 소유권 검증 후 확정해 복제 변수에 기록합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SubmitFacingDirectionRpc(bool isFacingRight, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning($"[PlayerNetworkSync] Unauthorized facing sync sender. object={name}, sender={rpcParams.Receive.SenderClientId}, owner={OwnerClientId}", this);
            return;
        }

        _replicatedFacingRight.Value = isFacingRight;
    }

    /// <summary>
    /// 로컬 상태와 마지막 전송 상태를 비교해 변경된 경우에만 방향 상태를 서버 전송합니다.
    /// </summary>
    private void SubmitFacingDirectionIfChanged(bool isFacingRight)
    {
        if (_lastSentFacingRight == isFacingRight)
        {
            return;
        }

        SubmitFacingDirectionRpc(isFacingRight);
        _lastSentFacingRight = isFacingRight;
        _nextFacingDirectionSendTime = Time.time + Mathf.Max(0.01f, _facingDirectionSendInterval);
    }

    /// <summary>
    /// 액션 타입 복제값이 변경되면 최신 확정 상태 적용을 시도합니다.
    /// </summary>
    private void HandleReplicatedActionTypeChanged(int previousValue, int currentValue)
    {
        TryApplyReplicatedActionState();
    }

    /// <summary>
    /// 액션 실행 여부 복제값이 변경되면 최신 확정 상태 적용을 시도합니다.
    /// </summary>
    private void HandleReplicatedActionRunningChanged(bool previousValue, bool currentValue)
    {
        TryApplyReplicatedActionState();
    }

    /// <summary>
    /// 복제된 액션 시작 revision 값이 변경되면 최신 시작 이벤트를 즉시 적용합니다.
    /// </summary>
    private void HandleReplicatedActionStartRevisionChanged(int previousValue, int currentValue)
    {
        if (currentValue == previousValue)
        {
            return;
        }

        TryApplyReplicatedActionStartEvent();
    }

    /// <summary>
    /// 방향 복제값이 변경되면 최신 확정 방향을 비주얼에 적용합니다.
    /// </summary>
    private void HandleReplicatedFacingDirectionChanged(bool previousValue, bool currentValue)
    {
        _lastReplicatedFacingRight = currentValue;
        ApplyReplicatedFacingDirection(currentValue);
    }

    /// <summary>
    /// 네트워크로 확정된 액션 상태를 비소유 인스턴스 ActionController에 적용합니다.
    /// </summary>
    private void TryApplyReplicatedActionState()
    {
        if (!_enableActionStateSync)
        {
            return;
        }

        if (!TryResolveActionController())
        {
            return;
        }

        E_ActionType resolvedActionType = _replicatedActionRunning.Value
            ? (E_ActionType)_replicatedActionType.Value
            : E_ActionType.Idle; // 실행 중이 아닌 경우 원격 화면 표현을 Idle로 정규화합니다.

        bool isServerAuthoritativeAction = IsServerAuthoritativeAction(resolvedActionType); // Owner 재적용 허용 여부를 판정할 서버 확정 액션 플래그입니다.
        if (IsOwner && (!isServerAuthoritativeAction || !_applyServerAuthoritativeActionsToOwner))
        {
            return;
        }

        if (!IsReplicatedAction(resolvedActionType))
        {
            return;
        }

        ActionRuntime runtime = _actionController.Runtime; // 현재 원격 인스턴스에서 실행 중인 액션 런타임 스냅샷입니다.
        if (_replicatedActionRunning.Value)
        {
            if (runtime.IsRunning && runtime.ActionType == resolvedActionType)
            {
                return;
            }

            _actionController.ApplyReplicatedActionStart(resolvedActionType, "PlayerNetworkSync-State");
            return;
        }

        _actionController.ApplyReplicatedActionStop(E_ActionType.Idle, "PlayerNetworkSync-State");
    }

    /// <summary>
    /// 서버 확정 방향 값을 PlayerMovement에 반영해 모든 참여자의 좌우 반전 상태를 일치시킵니다.
    /// </summary>
    /// <summary>
    /// 서버가 피격 계열 강제 액션을 감지하면 Owner 입력보다 우선하는 확정 상태로 복제합니다.
    /// </summary>
    private void MaintainServerAuthoritativeActionState()
    {
        if (!IsServer || !TryResolveActionController())
        {
            return;
        }

        ActionRuntime runtime = _actionController.Runtime; // 서버 인스턴스에서 현재 실행 중인 액션 스냅샷입니다.
        if (!runtime.IsRunning || !IsServerAuthoritativeAction(runtime.ActionType))
        {
            return;
        }

        if (_lastPublishedServerAuthoritativeExecutionId == runtime.ExecutionId
            && _replicatedActionRunning.Value
            && _replicatedActionType.Value == (int)runtime.ActionType)
        {
            return;
        }

        _lastPublishedServerAuthoritativeExecutionId = runtime.ExecutionId;
        PublishReplicatedActionState((int)runtime.ActionType, true, shouldPublishStartEvent: true);
    }

    /// <summary>
    /// 서버 확정 방향 값을 PlayerMovement에 반영합니다.
    /// </summary>
    private void ApplyReplicatedFacingDirection(bool isFacingRight)
    {
        if (!_enableFacingDirectionSync)
        {
            return;
        }

        if (IsOwner && !IsServer && !_applyReplicatedFacingToOwner)
        {
            return;
        }

        if (!TryResolvePlayerMovement())
        {
            return;
        }

        _playerMovement.SetFacingDirection(isFacingRight);
    }

    /// <summary>
    /// 원격 표현 동기화 대상으로 허용된 액션인지 판정합니다.
    /// </summary>
    /// <summary>
    /// 서버 체력 스냅샷 순번이 변경되면 최신 체력 상태를 로컬 HealthComponent에 반영합니다.
    /// </summary>
    private void HandleReplicatedHealthRevisionChanged(int previousValue, int currentValue)
    {
        if (currentValue == previousValue)
        {
            return;
        }

        ApplyReplicatedHealthState();
    }

    /// <summary>
    /// HealthComponent 리스너를 등록해 서버 확정 체력 상태를 복제합니다.
    /// </summary>
    private void RegisterHealthListener()
    {
        if (!_enableHealthStateSync || !IsServer || _isHealthListenerRegistered)
        {
            return;
        }

        if (!TryResolveHealthComponent())
        {
            return;
        }

        _healthComponent.AddListener(this);
        _isHealthListenerRegistered = true;
    }

    /// <summary>
    /// HealthComponent 리스너 등록을 해제합니다.
    /// </summary>
    private void UnregisterHealthListener()
    {
        if (!IsServer || !_isHealthListenerRegistered || _healthComponent == null || !_healthComponent.IsInitialized)
        {
            return;
        }

        _healthComponent.RemoveListener(this);
        _isHealthListenerRegistered = false;
    }

    /// <summary>
    /// 서버가 현재 HealthComponent 상태를 네트워크 변수 스냅샷으로 발행합니다.
    /// </summary>
    private void PublishHealthSnapshot()
    {
        if (!_enableHealthStateSync || !IsServer || !TryResolveHealthComponent())
        {
            return;
        }

        _replicatedCurrentHealth.Value = _healthComponent.GetCurrentHealth();
        _replicatedMaxHealth.Value = _healthComponent.GetMaxHealth();
        _replicatedIsDead.Value = _healthComponent.IsDead;
        _replicatedHealthRevision.Value++;
    }

    /// <summary>
    /// 서버 확정 체력 스냅샷을 로컬 HealthComponent에 적용합니다.
    /// </summary>
    private void ApplyReplicatedHealthState()
    {
        if (!_enableHealthStateSync || IsServer)
        {
            return;
        }

        if (IsOwner && !_applyReplicatedHealthToOwner)
        {
            return;
        }

        if (!TryResolveHealthComponent())
        {
            return;
        }

        float safeMaxHealth = Mathf.Max(1f, _replicatedMaxHealth.Value);
        float safeCurrentHealth = Mathf.Clamp(_replicatedCurrentHealth.Value, 0f, safeMaxHealth);

        _healthComponent.SetMaxHealth(safeMaxHealth, false);

        if (_replicatedIsDead.Value)
        {
            _healthComponent.SetCurrentHealth(0f);
            _lastAppliedHealthRevision = _replicatedHealthRevision.Value;
            return;
        }

        if (_healthComponent.IsDead)
        {
            _healthComponent.Revive(Mathf.Max(0.01f, safeCurrentHealth));
        }
        else
        {
            _healthComponent.SetCurrentHealth(safeCurrentHealth);
        }

        _lastAppliedHealthRevision = _replicatedHealthRevision.Value;
    }

    /// <summary>
    /// HealthComponent 참조를 보정하고 필요 시 경고를 출력합니다.
    /// </summary>
    private bool TryResolveHealthComponent()
    {
        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }

        if (_healthComponent == null)
        {
            _healthComponent = GetComponentInChildren<HealthComponent>(true);
        }

        if (_healthComponent == null)
        {
            _healthComponent = GetComponentInParent<HealthComponent>();
        }

        if (_healthComponent != null)
        {
            return true;
        }

        if (_warnMissingHealthComponent)
        {
            Debug.LogWarning($"[PlayerNetworkSync] HealthComponent가 없어 체력 상태 동기화를 수행할 수 없습니다. object={name}", this);
        }

        return false;
    }

    /// <summary>
    /// 체력 변경 이벤트를 받아 서버 체력 스냅샷을 갱신합니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 피해 적용 이벤트를 받아 서버 체력 스냅샷을 갱신합니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 회복 이벤트를 받아 서버 체력 스냅샷을 갱신합니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 사망 이벤트를 받아 서버 체력 스냅샷을 갱신합니다.
    /// </summary>
    public void OnDied()
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 부활 이벤트를 받아 서버 체력 스냅샷을 갱신합니다.
    /// </summary>
    public void OnRevived()
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 최대 체력 변경 이벤트를 받아 서버 체력 스냅샷을 갱신합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 서버 확정 액션 상태를 네트워크 변수에 반영하고 필요 시 시작 이벤트도 함께 발행합니다.
    /// </summary>
    private void PublishReplicatedActionState(int actionTypeValue, bool isRunning, bool shouldPublishStartEvent)
    {
        _replicatedActionType.Value = actionTypeValue;
        _replicatedActionRunning.Value = isRunning;

        if (!shouldPublishStartEvent)
        {
            return;
        }

        E_ActionType startedActionType = (E_ActionType)actionTypeValue; // 시작 이벤트로 전파할 확정 액션 타입입니다.
        if (!IsReplicatedAction(startedActionType))
        {
            return;
        }

        _replicatedActionStartType.Value = actionTypeValue;
        _replicatedActionStartRevision.Value++;
    }

    /// <summary>
    /// 서버가 확정한 액션 시작 이벤트를 원격 관찰자 ActionController에 적용합니다.
    /// </summary>
    private void TryApplyReplicatedActionStartEvent()
    {
        if (!_enableActionStateSync)
        {
            return;
        }

        int currentRevision = _replicatedActionStartRevision.Value; // 원격 관찰자가 처리해야 할 최신 시작 revision 값입니다.
        if (currentRevision <= 0 || _lastAppliedActionStartRevision == currentRevision)
        {
            return;
        }

        _lastAppliedActionStartRevision = currentRevision;

        if (IsOwner)
        {
            return;
        }

        if (!TryResolveActionController())
        {
            return;
        }

        E_ActionType startedActionType = (E_ActionType)_replicatedActionStartType.Value; // 원격에 재생해야 할 시작 액션 타입입니다.
        if (!IsReplicatedAction(startedActionType))
        {
            return;
        }

        _actionController.ApplyReplicatedActionStart(startedActionType, "PlayerNetworkSync-StartEvent");
    }

    private bool IsReplicatedAction(E_ActionType actionType)
    {
        if (_replicateAllActionTypes)
        {
            return true;
        }

        switch (actionType)
        {
            case E_ActionType.None:
            case E_ActionType.Idle:
            case E_ActionType.Move:
            case E_ActionType.Jump:
            case E_ActionType.Land:
            case E_ActionType.Dash:
            case E_ActionType.Falling:
            case E_ActionType.WallSlide:
            case E_ActionType.WallJump:
            case E_ActionType.Slide:
                return true;
            default:
                break;
        }

        for (int i = 0; i < _additionalReplicatedActions.Length; i++)
        {
            if (_additionalReplicatedActions[i] == actionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ActionController 참조를 보정하고 누락 시 경고 로그를 출력합니다.
    /// </summary>
    /// <summary>
    /// 서버 확정 액션 목록에 포함된 액션인지 판정합니다.
    /// </summary>
    private bool IsServerAuthoritativeAction(E_ActionType actionType)
    {
        if (_serverAuthoritativeActions == null || _serverAuthoritativeActions.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < _serverAuthoritativeActions.Length; index++)
        {
            if (_serverAuthoritativeActions[index] == actionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ActionController 참조를 보정하고 필요 시 경고를 출력합니다.
    /// </summary>
    private bool TryResolveActionController()
    {
        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
        }

        if (_actionController != null)
        {
            return true;
        }

        if (_warnMissingActionController)
        {
            Debug.LogWarning($"[PlayerNetworkSync] ActionController가 없어 액션 상태 동기화를 수행할 수 없습니다. object={name}", this);
        }

        return false;
    }

    /// <summary>
    /// PlayerMovement 참조를 보정하고 누락 시 경고 로그를 출력합니다.
    /// </summary>
    private bool TryResolvePlayerMovement()
    {
        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }

        if (_playerMovement != null)
        {
            return true;
        }

        if (_warnMissingPlayerMovement)
        {
            Debug.LogWarning($"[PlayerNetworkSync] PlayerMovement가 없어 방향 상태 동기화를 수행할 수 없습니다. object={name}", this);
        }

        return false;
    }
}
