using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 플레이어 이동 상태(액션/방향)를 네트워크로 동기화하기 위한 NetworkTransform 보유/검증 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class PlayerNetworkSync : NetworkBehaviour, IHealthListener, IActionListener
{
    private struct ReplicatedHealthState : INetworkSerializable, System.IEquatable<ReplicatedHealthState>
    {
        public float CurrentHealth; // 서버가 확정한 현재 체력 값입니다.
        public float MaxHealth; // 서버가 확정한 최대 체력 값입니다.
        public bool IsDead; // 서버가 확정한 사망 상태입니다.
        public int Revision; // 체력 스냅샷의 적용 순서를 판정하는 증가 번호입니다.

        /// <summary>
        /// 체력 스냅샷을 하나의 네트워크 페이로드로 생성합니다.
        /// </summary>
        public ReplicatedHealthState(float currentHealth, float maxHealth, bool isDead, int revision)
        {
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            IsDead = isDead;
            Revision = revision;
        }

        /// <summary>
        /// NGO NetworkVariable이 체력 스냅샷 전체를 원자적으로 복제할 수 있도록 직렬화합니다.
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CurrentHealth);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref IsDead);
            serializer.SerializeValue(ref Revision);
        }

        /// <summary>
        /// NGO NetworkVariable이 스냅샷 변경 여부를 정확하게 비교할 수 있도록 값 동등성을 판정합니다.
        /// </summary>
        public bool Equals(ReplicatedHealthState other)
        {
            return Mathf.Approximately(CurrentHealth, other.CurrentHealth)
                && Mathf.Approximately(MaxHealth, other.MaxHealth)
                && IsDead == other.IsDead
                && Revision == other.Revision;
        }
    }

    [Header("Dependencies")]
    [Tooltip("플레이어 Transform 동기화를 담당하는 NetworkTransform 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private NetworkTransform _networkTransform; // 원격 플레이어 위치 동기화를 처리하는 NetworkTransform 참조입니다.
    [Tooltip("네트워크로 확정된 액션 상태를 조회/적용할 ActionController 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private ActionController _actionController; // 네트워크 액션 상태를 생산/소비할 ActionController 참조입니다.
    [Tooltip("네트워크로 확정된 좌우 방향 상태를 읽고 반영할 PlayerMovement 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 바라보는 방향 동기화 원본/적용을 담당할 PlayerMovement 참조입니다.

    [Tooltip("서버 확정 체력 상태를 복제하고 로컬 HealthComponent에 반영할 대상 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // 서버 확정 체력 스냅샷을 기록할 HealthComponent 참조입니다.

    [Tooltip("Owner Client의 공격 HitWindow 요청을 서버 권한 공격 판정으로 실행할 AttackExecutor 참조입니다. 비어 있으면 자식 계층에서 자동 탐색합니다.")]
    [SerializeField] private AttackExecutor _attackExecutor; // 멀티플레이에서 클라이언트 플레이어 공격을 서버 판정으로 실행하기 위한 AttackExecutor 참조입니다.

    [Header("Action State Sync")]
    [Tooltip("Owner가 확정한 액션 상태를 서버를 통해 전파할지 여부입니다.")]
    [SerializeField] private bool _enableActionStateSync = true; // 액션 상태 네트워크 동기화 활성화 여부입니다.
    [Tooltip("액션 상태 전송 간 최소 간격(초)입니다.")]
    [SerializeField] private float _actionStateSendInterval = 0.033f; // 액션 상태 전송 빈도를 제한하기 위한 최소 간격입니다.
    [Tooltip("서버 권한 액션 때문에 Owner 액션 상태 RPC가 임시 거부됐을 때 미확정 상태를 재전송할 간격(초)입니다.")]
    [SerializeField] private float _unconfirmedActionStateResendInterval = 0.1f; // 서버 복제 상태가 마지막 Owner 전송 상태와 일치하지 않을 때 재전송을 제한하는 간격입니다.
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
    [Tooltip("서버 권한 액션이 완료 이벤트를 내지 못했을 때 서버 ActionController를 자동 완료 처리할 최대 유지 시간(초)입니다. 0 이하면 비활성화됩니다.")]
    [SerializeField] private float _serverAuthoritativeActionMaxDuration = 0.6f; // Hit 같은 서버 권한 액션이 완료 신호를 놓쳐 입력 복제가 영구 차단되지 않도록 제한하는 시간입니다.
    [Tooltip("자동 완료 보호에서 제외할 서버 권한 액션 목록입니다. Die처럼 외부 부활/장면 전환으로 끝나는 액션을 넣습니다.")]
    [SerializeField] private E_ActionType[] _serverAuthoritativeAutoCompleteExclusions = new E_ActionType[] { E_ActionType.Die }; // 자동 완료 보호를 적용하지 않을 서버 권한 액션 목록입니다.

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
    private float _nextUnconfirmedActionStateResendTime; // 서버 복제 변수에 반영되지 않은 Owner 액션 상태를 다음에 재전송할 수 있는 시각입니다.
    private float _nextFacingDirectionSendTime; // 다음 방향 상태 전송 가능 시각(초)입니다.

    /// <summary>
    /// 초기화 시 NetworkTransform/의존성 참조를 캐시합니다.
    /// </summary>
    private readonly NetworkVariable<ReplicatedHealthState> _replicatedHealthState = new NetworkVariable<ReplicatedHealthState>(
        new ReplicatedHealthState(0f, 1f, false, 0),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 체력 스냅샷을 하나의 순서 보장 단위로 복제하는 네트워크 변수입니다.

    private bool _isHealthListenerRegistered; // 서버 HealthComponent 리스너 등록 여부를 추적하는 플래그입니다.
    private int _lastAppliedHealthRevision = -1; // 로컬 HealthComponent에 마지막으로 반영한 체력 스냅샷 순번입니다.
    private int _lastAppliedActionStartRevision = -1; // 로컬 관찰자 인스턴스가 마지막으로 처리한 액션 시작 revision 값입니다.
    private int _lastPublishedServerAuthoritativeExecutionId = -1; // 서버 권한 액션 시작 이벤트의 중복 발행을 막기 위한 마지막 실행 ID입니다.
    private bool _isActionListenerRegistered; // 서버 권한 액션 완료/취소를 복제 변수에 반영하기 위한 ActionController 리스너 등록 여부입니다.
    private E_ActionType _serverAuthoritativeRuntimeAction = E_ActionType.None; // 현재 서버가 권한을 가진 상태로 추적 중인 액션 타입입니다.
    private int _serverAuthoritativeRuntimeExecutionId = -1; // 현재 서버 권한 액션의 실행 ID입니다.
    private float _serverAuthoritativeRuntimeReleaseAt = -1f; // 완료 이벤트 누락 시 서버 권한 액션을 자동 완료할 시각입니다.

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

        if (_attackExecutor == null)
        {
            _attackExecutor = GetComponentInChildren<AttackExecutor>(true);
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
        _replicatedHealthState.OnValueChanged += HandleReplicatedHealthStateChanged;

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
            RegisterActionListener();
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
        _replicatedHealthState.OnValueChanged -= HandleReplicatedHealthStateChanged;

        if (_playerMovement != null)
        {
            _playerMovement.FacingDirectionChanged -= HandleLocalFacingDirectionChanged;
        }

        UnregisterActionListener();
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

        if (_lastAppliedHealthRevision == _replicatedHealthState.Value.Revision)
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

        if (currentIsRunning && IsServerAuthoritativeAction(currentActionType))
        {
            return;
        }

        if (currentIsRunning && !IsReplicatedAction(currentActionType))
        {
            return;
        }

        bool matchesLastSubmittedState = _lastSentActionType == currentActionType && _lastSentIsRunning == currentIsRunning; // 서버 확인 이후 중복 RPC를 막기 위해 마지막 전송 상태와 현재 로컬 상태가 같은지 비교합니다.
        if (matchesLastSubmittedState && IsActionStateConfirmedByServer(currentActionType, currentIsRunning))
        {
            return;
        }

        if (matchesLastSubmittedState && Time.time < _nextUnconfirmedActionStateResendTime)
        {
            return;
        }

        SubmitActionStateRpc((int)currentActionType, currentIsRunning);
        _lastSentActionType = currentActionType;
        _lastSentIsRunning = currentIsRunning;
        _nextActionStateSendTime = Time.time + Mathf.Max(0.01f, _actionStateSendInterval);
        _nextUnconfirmedActionStateResendTime = Time.time + Mathf.Max(0.02f, _unconfirmedActionStateResendInterval);
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
    /// Owner Client에서 열린 공격 HitWindow를 서버 권한 공격 판정 RPC로 요청합니다.
    /// </summary>
    public bool TryRequestAttackExecutionOnServer(E_ActionType actionType, int executionId)
    {
        if (!IsSpawned)
        {
            Debug.LogWarning($"[PlayerNetworkSync] Cannot request attack execution before network spawn. object={name}", this);
            return false;
        }

        if (!IsOwner)
        {
            Debug.LogWarning($"[PlayerNetworkSync] Non-owner Client cannot request attack execution. object={name}", this);
            return false;
        }

        if (IsServer)
        {
            return TryResolveAttackExecutor() && _attackExecutor.TryExecuteNetworkAuthorityAttack(actionType, executionId);
        }

        SubmitAttackExecutionRpc((int)actionType, executionId);
        return true;
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
                TryCompleteExpiredServerAuthoritativeAction(runtime);

                runtime = _actionController.Runtime;
                if (runtime.IsRunning && IsServerAuthoritativeAction(runtime.ActionType))
                {
                    return;
                }
            }
        }

        PublishReplicatedActionState(actionTypeValue, isRunning, shouldPublishStartEvent: isRunning);
    }

    /// <summary>
    /// Owner Client가 요청한 공격 판정을 서버에서 소유권 검증 후 실행합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SubmitAttackExecutionRpc(int actionTypeValue, int executionId, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning($"[PlayerNetworkSync] Unauthorized attack execution sender. object={name}, sender={rpcParams.Receive.SenderClientId}, owner={OwnerClientId}", this);
            return;
        }

        if (!System.Enum.IsDefined(typeof(E_ActionType), actionTypeValue))
        {
            Debug.LogWarning($"[PlayerNetworkSync] Invalid attack action value. object={name}, actionValue={actionTypeValue}", this);
            return;
        }

        if (executionId < 0)
        {
            Debug.LogWarning($"[PlayerNetworkSync] Invalid attack execution id. object={name}, executionId={executionId}", this);
            return;
        }

        if (!TryResolveAttackExecutor())
        {
            return;
        }

        E_ActionType actionType = (E_ActionType)actionTypeValue; // 서버 권한 판정으로 실행할 공격 액션 타입입니다.
        _attackExecutor.TryExecuteNetworkAuthorityAttack(actionType, executionId);
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
    /// 서버 확정 체력 스냅샷이 변경되면 묶여서 도착한 최신 상태만 로컬 HealthComponent와 UI 리스너에 반영합니다.
    /// </summary>
    private void HandleReplicatedHealthStateChanged(ReplicatedHealthState previousValue, ReplicatedHealthState currentValue)
    {
        if (currentValue.Revision == previousValue.Revision)
        {
            return;
        }

        ApplyReplicatedHealthState();
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

        bool isReplicatedActionRunning = _replicatedActionRunning.Value; // 서버가 복제한 액션 실행 여부입니다.
        E_ActionType resolvedActionType = isReplicatedActionRunning
            ? (E_ActionType)_replicatedActionType.Value
            : E_ActionType.Idle; // 실행 중이 아닌 경우 원격 화면 표현을 Idle로 정규화합니다.

        bool isServerAuthoritativeAction = IsServerAuthoritativeAction(resolvedActionType); // Owner 재적용 허용 여부를 판정할 서버 확정 액션 플래그입니다.
        if (IsOwner && !ShouldApplyReplicatedActionStateToOwner(isReplicatedActionRunning, isServerAuthoritativeAction))
        {
            return;
        }

        if (isReplicatedActionRunning && !IsReplicatedAction(resolvedActionType))
        {
            return;
        }

        ActionRuntime runtime = _actionController.Runtime; // 현재 원격 인스턴스에서 실행 중인 액션 런타임 스냅샷입니다.
        if (isReplicatedActionRunning)
        {
            if (!IsOwner && isServerAuthoritativeAction)
            {
                return;
            }

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
    /// Owner 클라이언트가 서버 복제 액션 상태를 로컬 ActionController에 적용해야 하는지 판정합니다.
    /// </summary>
    private bool ShouldApplyReplicatedActionStateToOwner(bool isReplicatedActionRunning, bool isReplicatedServerAuthoritativeAction)
    {
        if (!_applyServerAuthoritativeActionsToOwner)
        {
            return false;
        }

        if (isReplicatedActionRunning)
        {
            return isReplicatedServerAuthoritativeAction;
        }

        if (!TryResolveActionController())
        {
            return false;
        }

        ActionRuntime runtime = _actionController.Runtime; // Owner 로컬에서 현재 실행 중인 액션을 확인하기 위한 런타임 참조입니다.
        return runtime.IsRunning && IsServerAuthoritativeAction(runtime.ActionType);
    }

    /// <summary>
    /// 서버가 피격 계열 강제 액션을 감지하면 Owner 입력보다 우선하는 확정 상태로 복제합니다.
    /// </summary>
    private void MaintainServerAuthoritativeActionState()
    {
        if (!IsServer || !TryResolveActionController())
        {
            return;
        }

        RegisterActionListener();

        ActionRuntime runtime = _actionController.Runtime; // 서버 인스턴스에서 현재 실행 중인 액션 스냅샷입니다.
        if (!runtime.IsRunning || !IsServerAuthoritativeAction(runtime.ActionType))
        {
            ClearServerAuthoritativeRuntimeTracking();
            return;
        }

        if (_lastPublishedServerAuthoritativeExecutionId == runtime.ExecutionId
            && _replicatedActionRunning.Value
            && _replicatedActionType.Value == (int)runtime.ActionType)
        {
            TryCompleteExpiredServerAuthoritativeAction(runtime);
            return;
        }

        PublishServerAuthoritativeActionStart(runtime);
        TryCompleteExpiredServerAuthoritativeAction(runtime);
    }

    /// <summary>
    /// 서버 ActionController의 액션 생명주기 이벤트를 구독해 서버 권한 액션 종료를 복제 상태에 반영합니다.
    /// </summary>
    private void RegisterActionListener()
    {
        if (!_enableActionStateSync || !IsServer || _isActionListenerRegistered)
        {
            return;
        }

        if (!TryResolveActionController())
        {
            return;
        }

        _actionController.AddListener(this);
        _isActionListenerRegistered = true;
    }

    /// <summary>
    /// 서버 ActionController 액션 생명주기 이벤트 구독을 해제합니다.
    /// </summary>
    private void UnregisterActionListener()
    {
        if (!_isActionListenerRegistered || _actionController == null)
        {
            _isActionListenerRegistered = false;
            return;
        }

        _actionController.RemoveListener(this);
        _isActionListenerRegistered = false;
    }

    /// <summary>
    /// 서버 권한 액션 시작을 네트워크 복제 상태와 추적 캐시에 기록합니다.
    /// </summary>
    private void PublishServerAuthoritativeActionStart(ActionRuntime runtime)
    {
        if (!IsServer || runtime == null || !runtime.IsRunning || !IsServerAuthoritativeAction(runtime.ActionType))
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
        _serverAuthoritativeRuntimeAction = runtime.ActionType;
        _serverAuthoritativeRuntimeExecutionId = runtime.ExecutionId;
        _serverAuthoritativeRuntimeReleaseAt = ResolveServerAuthoritativeReleaseTime(runtime.ActionType);
        PublishReplicatedActionState((int)runtime.ActionType, true, shouldPublishStartEvent: true);
    }

    /// <summary>
    /// 서버 권한 액션 종료를 네트워크 복제 상태에 반영해 원격 관찰자가 Hit 상태에 고정되지 않도록 합니다.
    /// </summary>
    private void PublishServerAuthoritativeActionStop(ActionRuntime runtime)
    {
        if (!IsServer || runtime == null || !IsServerAuthoritativeAction(runtime.ActionType))
        {
            return;
        }

        if (_serverAuthoritativeRuntimeExecutionId >= 0 && _serverAuthoritativeRuntimeExecutionId != runtime.ExecutionId)
        {
            return;
        }

        PublishReplicatedActionState((int)E_ActionType.None, false, shouldPublishStartEvent: false);
        ClearServerAuthoritativeRuntimeTracking();
    }

    /// <summary>
    /// 서버 권한 액션의 자동 완료 보호 시각을 계산합니다.
    /// </summary>
    private float ResolveServerAuthoritativeReleaseTime(E_ActionType actionType)
    {
        if (_serverAuthoritativeActionMaxDuration <= 0f || IsServerAuthoritativeAutoCompleteExcluded(actionType))
        {
            return -1f;
        }

        return Time.time + Mathf.Max(0.05f, _serverAuthoritativeActionMaxDuration);
    }

    /// <summary>
    /// 완료 이벤트가 누락된 서버 권한 액션을 제한 시간 이후 안전하게 완료 처리합니다.
    /// </summary>
    private void TryCompleteExpiredServerAuthoritativeAction(ActionRuntime runtime)
    {
        if (!IsServer || runtime == null || !runtime.IsRunning || !IsServerAuthoritativeAction(runtime.ActionType))
        {
            return;
        }

        if (_serverAuthoritativeRuntimeExecutionId != runtime.ExecutionId)
        {
            _serverAuthoritativeRuntimeAction = runtime.ActionType;
            _serverAuthoritativeRuntimeExecutionId = runtime.ExecutionId;
            _serverAuthoritativeRuntimeReleaseAt = ResolveServerAuthoritativeReleaseTime(runtime.ActionType);
        }

        if (_serverAuthoritativeRuntimeReleaseAt < 0f || Time.time < _serverAuthoritativeRuntimeReleaseAt)
        {
            return;
        }

        _actionController.CompleteCurrentAction();
    }

    /// <summary>
    /// 서버 권한 액션 자동 완료 보호에서 제외된 액션인지 확인합니다.
    /// </summary>
    private bool IsServerAuthoritativeAutoCompleteExcluded(E_ActionType actionType)
    {
        if (_serverAuthoritativeAutoCompleteExclusions == null)
        {
            return false;
        }

        for (int index = 0; index < _serverAuthoritativeAutoCompleteExclusions.Length; index++)
        {
            if (_serverAuthoritativeAutoCompleteExclusions[index] == actionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 서버 권한 액션 추적 캐시를 초기화합니다.
    /// </summary>
    private void ClearServerAuthoritativeRuntimeTracking()
    {
        _lastPublishedServerAuthoritativeExecutionId = -1;
        _serverAuthoritativeRuntimeAction = E_ActionType.None;
        _serverAuthoritativeRuntimeExecutionId = -1;
        _serverAuthoritativeRuntimeReleaseAt = -1f;
    }

    /// <summary>
    /// 액션 시작 이벤트에서 서버 권한 액션을 즉시 복제합니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
        if (!IsServer || runtime == null || !IsServerAuthoritativeAction(runtime.ActionType))
        {
            return;
        }

        PublishServerAuthoritativeActionStart(runtime);
    }

    /// <summary>
    /// 액션 단계 변경 이벤트는 현재 네트워크 복제에 추가 처리가 필요하지 않습니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
    }

    /// <summary>
    /// 서버 권한 액션 완료 시 원격 관찰자에게 액션 종료 상태를 복제합니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
        PublishServerAuthoritativeActionStop(runtime);
    }

    /// <summary>
    /// 서버 권한 액션 취소 시 원격 관찰자에게 액션 종료 상태를 복제합니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
        PublishServerAuthoritativeActionStop(runtime);
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

        ReplicatedHealthState previousState = _replicatedHealthState.Value; // 새 스냅샷의 순번을 계산하기 위한 직전 서버 상태입니다.
        _replicatedHealthState.Value = new ReplicatedHealthState(
            _healthComponent.GetCurrentHealth(),
            _healthComponent.GetMaxHealth(),
            _healthComponent.IsDead,
            previousState.Revision + 1);
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

        ReplicatedHealthState snapshot = _replicatedHealthState.Value; // 하나의 NetworkVariable로 묶여 도착한 서버 확정 체력 상태입니다.
        if (snapshot.Revision <= _lastAppliedHealthRevision)
        {
            return;
        }

        float safeMaxHealth = Mathf.Max(1f, snapshot.MaxHealth);
        float safeCurrentHealth = Mathf.Clamp(snapshot.CurrentHealth, 0f, safeMaxHealth);

        _healthComponent.SetMaxHealth(safeMaxHealth, false);

        if (snapshot.IsDead)
        {
            _healthComponent.SetCurrentHealth(0f);
            _healthComponent.NotifyCurrentHealthState();
            _lastAppliedHealthRevision = snapshot.Revision;
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

        _healthComponent.NotifyCurrentHealthState();
        _lastAppliedHealthRevision = snapshot.Revision;
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
        bool actionStateChanged = _replicatedActionType.Value != actionTypeValue || _replicatedActionRunning.Value != isRunning; // 이번 수락 상태가 관찰자에게 새 액션 시작 이벤트로 전달돼야 하는지 판정합니다.

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

        if (!actionStateChanged)
        {
            return;
        }

        _replicatedActionStartType.Value = actionTypeValue;
        _replicatedActionStartRevision.Value++;
    }

    /// <summary>
    /// 최신 서버 복제 액션 상태가 Owner 로컬 액션 상태와 일치하는지 확인합니다.
    /// </summary>
    private bool IsActionStateConfirmedByServer(E_ActionType actionType, bool isRunning)
    {
        if (_replicatedActionRunning.Value != isRunning)
        {
            return false;
        }

        E_ActionType replicatedActionType = isRunning ? (E_ActionType)_replicatedActionType.Value : E_ActionType.None; // Owner 전송 확인에 사용할 서버 복제 액션 상태를 정규화한 값입니다.
        return replicatedActionType == actionType;
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
    /// 공격 판정 RPC를 서버에서 실행할 AttackExecutor 참조를 보정합니다.
    /// </summary>
    private bool TryResolveAttackExecutor()
    {
        if (_attackExecutor == null)
        {
            _attackExecutor = GetComponentInChildren<AttackExecutor>(true);
        }

        if (_attackExecutor != null)
        {
            return true;
        }

        Debug.LogWarning($"[PlayerNetworkSync] AttackExecutor not found. Server attack execution skipped. object={name}", this);
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

    /// <summary>
    /// 서버에서 확정한 전투 통계 증가분을 해당 플레이어 Owner의 로컬 저장 런타임에 전달합니다.
    /// </summary>
    public static bool TryReportCombatStatsToOwner(GameObject playerObject, float damageDealt, int damageTakenCount)
    {
        return TryReportCombatStatsToParticipants(playerObject, damageDealt, damageTakenCount);
    }

    /// <summary>
    /// 서버에서 확정한 전투 통계 증가분을 모든 참여자의 ClientId별 로컬 통계 저장소에 복제합니다.
    /// </summary>
    public static bool TryReportCombatStatsToParticipants(GameObject playerObject, float damageDealt, int damageTakenCount)
    {
        if (playerObject == null)
        {
            return false;
        }

        PlayerNetworkSync playerNetworkSync = playerObject.GetComponentInParent<PlayerNetworkSync>();
        if (playerNetworkSync == null || !playerNetworkSync.IsSpawned || !playerNetworkSync.IsServer)
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            return false;
        }

        playerNetworkSync.ReportCombatStatsToParticipantsRpc(playerNetworkSync.OwnerClientId, damageDealt, damageTakenCount);
        return true;
    }

    /// <summary>
    /// 서버가 확정한 전투 통계 증가분을 Owner Client에 전달합니다.
    /// </summary>
    [Rpc(SendTo.Everyone)]
    private void ReportCombatStatsToParticipantsRpc(ulong ownerClientId, float damageDealt, int damageTakenCount)
    {
        ApplyCombatStatsToLocalRuntime(ownerClientId, damageDealt, damageTakenCount);
    }

    /// <summary>
    /// 전달받은 전투 통계 증가분을 현재 피어의 로컬 런타임에 누적합니다.
    /// </summary>
    private static void ApplyCombatStatsToLocalRuntime(ulong ownerClientId, float damageDealt, int damageTakenCount)
    {
        PlayerCombatStatsRuntime statsRuntime = PlayerCombatStatsRuntime.Instance; // 현재 피어의 개인 전투 통계를 저장할 런타임입니다.
        statsRuntime.RecordCombatStatsForClient(ownerClientId, damageDealt, damageTakenCount);
    }
}
