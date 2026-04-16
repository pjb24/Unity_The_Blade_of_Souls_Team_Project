using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 플레이어 이동 상태(액션/방향)를 네트워크로 동기화하기 위한 NetworkTransform 보유/검증 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class PlayerNetworkSync : NetworkBehaviour
{
    [Header("Dependencies")]
    [Tooltip("플레이어 Transform 동기화를 담당하는 NetworkTransform 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private NetworkTransform _networkTransform; // 원격 플레이어 위치 동기화를 처리하는 NetworkTransform 참조입니다.
    [Tooltip("네트워크로 확정된 액션 상태를 조회/적용할 ActionController 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private ActionController _actionController; // 네트워크 액션 상태를 생산/소비할 ActionController 참조입니다.
    [Tooltip("네트워크로 확정된 좌우 방향 상태를 읽고 반영할 PlayerMovement 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 바라보는 방향 동기화 원본/적용을 담당할 PlayerMovement 참조입니다.

    [Header("Action State Sync")]
    [Tooltip("Owner가 확정한 액션 상태를 서버를 통해 전파할지 여부입니다.")]
    [SerializeField] private bool _enableActionStateSync = true; // 액션 상태 네트워크 동기화 활성화 여부입니다.
    [Tooltip("액션 상태 전송 간 최소 간격(초)입니다.")]
    [SerializeField] private float _actionStateSendInterval = 0.033f; // 액션 상태 전송 빈도를 제한하기 위한 최소 간격입니다.
    [Tooltip("ActionController 참조 누락 시 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingActionController = true; // ActionController 참조 누락 경고 출력 여부입니다.

    [Header("Facing Direction Sync")]
    [Tooltip("Owner가 바라보는 방향 상태를 서버로 전송할지 여부입니다.")]
    [SerializeField] private bool _enableFacingDirectionSync = true; // 좌우 방향 네트워크 동기화 활성화 여부입니다.
    [Tooltip("방향 상태 전송 간 최소 간격(초)입니다.")]
    [SerializeField] private float _facingDirectionSendInterval = 0.05f; // 방향 상태 전송 빈도를 제한하기 위한 최소 간격입니다.
    [Tooltip("PlayerMovement 참조 누락 시 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingPlayerMovement = true; // PlayerMovement 참조 누락 경고 출력 여부입니다.
    [Tooltip("Owner 클라이언트에도 서버 복제 방향값을 재적용할지 여부입니다. 기본값(false)은 Owner 입력 응답성을 우선합니다.")]
    [SerializeField] private bool _applyReplicatedFacingToOwner = false; // Owner 로컬 인스턴스에 복제 방향값 재적용 여부를 제어하는 플래그입니다.

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
    }

    /// <summary>
    /// 네트워크 스폰 시 액션/방향 복제 콜백을 바인딩하고 초기 상태를 적용합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        _replicatedActionType.OnValueChanged += HandleReplicatedActionTypeChanged;
        _replicatedActionRunning.OnValueChanged += HandleReplicatedActionRunningChanged;
        _replicatedFacingRight.OnValueChanged += HandleReplicatedFacingDirectionChanged;

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
            TryApplyReplicatedActionState();
        }
    }

    /// <summary>
    /// 네트워크 디스폰 시 액션/방향 복제 콜백 바인딩을 해제합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        _replicatedActionType.OnValueChanged -= HandleReplicatedActionTypeChanged;
        _replicatedActionRunning.OnValueChanged -= HandleReplicatedActionRunningChanged;
        _replicatedFacingRight.OnValueChanged -= HandleReplicatedFacingDirectionChanged;

        if (_playerMovement != null)
        {
            _playerMovement.FacingDirectionChanged -= HandleLocalFacingDirectionChanged;
        }
    }

    /// <summary>
    /// Owner 인스턴스에서 현재 액션/방향 상태를 샘플링하고 서버 확정 동기화를 수행합니다.
    /// </summary>
    private void Update()
    {
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

        if (currentIsRunning && !IsReplicatedMovementAction(currentActionType))
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

        _replicatedActionType.Value = actionTypeValue;
        _replicatedActionRunning.Value = isRunning;
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
        if (!_enableActionStateSync || IsOwner)
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

        if (!IsReplicatedMovementAction(resolvedActionType))
        {
            return;
        }

        ActionRuntime runtime = _actionController.Runtime; // 현재 원격 인스턴스에서 실행 중인 액션 런타임 스냅샷입니다.
        if (runtime.IsRunning && runtime.ActionType == resolvedActionType)
        {
            return;
        }

        _actionController.RequestAction(resolvedActionType);
    }

    /// <summary>
    /// 서버 확정 방향 값을 PlayerMovement에 반영해 모든 참여자의 좌우 반전 상태를 일치시킵니다.
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
    /// 이동/점프/대시 계열처럼 원격 표현 동기화 대상으로 허용된 액션인지 판정합니다.
    /// </summary>
    private bool IsReplicatedMovementAction(E_ActionType actionType)
    {
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
                return false;
        }
    }

    /// <summary>
    /// ActionController 참조를 보정하고 누락 시 경고 로그를 출력합니다.
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
