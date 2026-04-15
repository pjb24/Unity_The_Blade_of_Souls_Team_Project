using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 플레이어 이동 상태를 네트워크로 동기화하기 위한 NetworkTransform 보유/검증 컴포넌트입니다.
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

    [Header("Action State Sync")]
    [Tooltip("Owner가 확정한 액션 상태를 서버를 통해 전파할지 여부입니다.")]
    [SerializeField] private bool _enableActionStateSync = true; // 액션 상태 네트워크 동기화 활성화 여부입니다.
    [Tooltip("액션 상태 전송 간 최소 간격(초)입니다.")]
    [SerializeField] private float _actionStateSendInterval = 0.033f; // 액션 상태 전송 빈도를 제한하기 위한 최소 간격입니다.
    [Tooltip("ActionController 참조 누락 시 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingActionController = true; // ActionController 참조 누락 경고 출력 여부입니다.

    private readonly NetworkVariable<int> _replicatedActionType = new NetworkVariable<int>(
        (int)E_ActionType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 액션 타입을 모든 관찰자에게 복제하는 네트워크 변수입니다.
    private readonly NetworkVariable<bool> _replicatedActionRunning = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 액션 실행 여부를 모든 관찰자에게 복제하는 네트워크 변수입니다.

    private E_ActionType _lastSentActionType = E_ActionType.None; // Owner가 서버에 마지막으로 전송한 액션 타입 캐시입니다.
    private bool _lastSentIsRunning; // Owner가 서버에 마지막으로 전송한 액션 실행 여부 캐시입니다.
    private float _nextActionStateSendTime; // 다음 액션 상태 전송 가능 시각(초)입니다.

    /// <summary>
    /// 초기화 시 NetworkTransform 참조를 캐시합니다.
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
    }

    /// <summary>
    /// 네트워크 스폰 시 액션 상태 복제 콜백을 바인딩하고 초기 상태를 적용합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        _replicatedActionType.OnValueChanged += HandleReplicatedActionTypeChanged;
        _replicatedActionRunning.OnValueChanged += HandleReplicatedActionRunningChanged;

        if (!_enableActionStateSync)
        {
            return;
        }

        TryApplyReplicatedActionState();
    }

    /// <summary>
    /// 네트워크 디스폰 시 액션 상태 복제 콜백 바인딩을 해제합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        _replicatedActionType.OnValueChanged -= HandleReplicatedActionTypeChanged;
        _replicatedActionRunning.OnValueChanged -= HandleReplicatedActionRunningChanged;
    }

    /// <summary>
    /// Owner 인스턴스에서 현재 액션 상태를 샘플링하고 서버 확정 동기화를 수행합니다.
    /// </summary>
    private void Update()
    {
        if (!_enableActionStateSync || !IsSpawned || !IsOwner)
        {
            return;
        }

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

        SubmitActionStateServerRpc((int)currentActionType, currentIsRunning);
        _lastSentActionType = currentActionType;
        _lastSentIsRunning = currentIsRunning;
        _nextActionStateSendTime = Time.time + Mathf.Max(0.01f, _actionStateSendInterval);
    }

    /// <summary>
    /// Owner가 보고한 액션 상태를 서버에서 소유권 검증 후 확정해 복제 변수에 기록합니다.
    /// </summary>
    [ServerRpc]
    private void SubmitActionStateServerRpc(int actionTypeValue, bool isRunning, ServerRpcParams serverRpcParams = default)
    {
        if (serverRpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning($"[PlayerNetworkSync] Unauthorized action sync sender. object={name}, sender={serverRpcParams.Receive.SenderClientId}, owner={OwnerClientId}", this);
            return;
        }

        _replicatedActionType.Value = actionTypeValue;
        _replicatedActionRunning.Value = isRunning;
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
}
