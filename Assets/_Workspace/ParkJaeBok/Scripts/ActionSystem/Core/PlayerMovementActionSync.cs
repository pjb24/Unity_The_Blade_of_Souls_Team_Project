using Unity.Netcode;
using UnityEngine;

/// <summary>
/// PlayerMovement의 물리 상태를 ActionController 액션 상태로 동기화하는 브리지입니다.
/// </summary>
public class PlayerMovementActionSync : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ActionController _actionController; // 이동 상태 액션 요청을 전달할 액션 컨트롤러 참조
    [SerializeField] private PlayerMovement _playerMovement; // 점프/대시/낙하/벽 상호작용 상태를 제공할 이동 컴포넌트 참조

    [Header("Grounded Presentation")]
    [SerializeField] private float _landPresentationDuration = 0.1f; // 착지 액션을 유지할 최소 시간(초)
    [SerializeField] private float _moveInputThreshold = 0.1f; // Move 액션 전환에 사용할 최소 입력 크기 임계값
    [SerializeField] private bool _suspendWhileNonMovementActionRunning = true; // 비이동 액션 실행 중 이동 동기화 요청을 일시 중단할지 여부
    [Tooltip("NetworkObject를 찾지 못해 소유권 검증 없이 동작할 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenOwnershipUnavailable = true; // 네트워크 오브젝트 소유권을 확인할 수 없을 때 경고 로그 출력 여부
    [Tooltip("싱글플레이/멀티플레이 이동 액션 동기화 경로 판정 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _enableOwnershipRouteLog = false; // 이동 액션 동기화 로컬 권한 판정 로그 출력 여부입니다.

    private bool _wasGroundedLastFrame; // 이전 프레임 지면 접촉 여부
    private float _landActionTimer; // Land 액션 유지 시간 타이머
    [SerializeField] private NetworkObject _networkObject; // 소유권 기반 입력 처리 판정을 위한 NetworkObject 참조

    /// <summary>
    /// 초기 참조를 보정하고 시작 시 지면 상태를 스냅샷합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveReferences();

        if (_networkObject == null)
        {
            _networkObject = GetComponent<NetworkObject>();
        }

        if (_networkObject == null && _warnWhenOwnershipUnavailable)
        {
            Debug.LogWarning($"[PlayerMovementActionSync] NetworkObject가 없어 소유권 검증 없이 동작합니다. object={name}");
        }

        if (_playerMovement != null && _playerMovement.Controller != null)
        {
            _wasGroundedLastFrame = _playerMovement.Controller.IsGrounded();
        }
    }

    /// <summary>
    /// 매 프레임 이동 상태를 해석해 액션 요청을 수행합니다.
    /// </summary>
    private void Update()
    {
        if (!TryResolveReferences())
        {
            return;
        }

        if (!CanProcessActionSync())
        {
            _wasGroundedLastFrame = _playerMovement.Controller.IsGrounded();
            _landActionTimer = 0f;
            return;
        }

        if (ShouldSuspendMovementSync())
        {
            _wasGroundedLastFrame = _playerMovement.Controller.IsGrounded();
            _landActionTimer = 0f;
            return;
        }

        bool isGrounded = _playerMovement.Controller.IsGrounded(); // 현재 프레임 지면 접촉 여부
        E_ActionType targetAction = ResolveTargetAction(isGrounded); // 현재 이동 상태에서 표현해야 할 목표 액션 타입

        if (isGrounded && !_wasGroundedLastFrame)
        {
            _landActionTimer = Mathf.Max(0f, _landPresentationDuration);
            targetAction = E_ActionType.Land;
        }

        if (_landActionTimer > 0f)
        {
            _landActionTimer -= Time.deltaTime;
            targetAction = E_ActionType.Land;
        }

        TryRequestAction(targetAction);
        _wasGroundedLastFrame = isGrounded;
    }

    /// <summary>
    /// 현재 실행 중인 액션이 비이동 계열이면 이동 동기화를 중단할지 판정합니다.
    /// </summary>
    private bool ShouldSuspendMovementSync()
    {
        if (!_suspendWhileNonMovementActionRunning)
        {
            return false;
        }

        ActionRuntime runtime = _actionController.Runtime; // 현재 액션 런타임 스냅샷
        if (!runtime.IsRunning)
        {
            return false;
        }

        return !IsMovementAction(runtime.ActionType);
    }

    /// <summary>
    /// 액션 타입이 이동 동기화에서 관리하는 이동 계열인지 판정합니다.
    /// </summary>
    private bool IsMovementAction(E_ActionType actionType)
    {
        switch (actionType)
        {
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
    /// 현재 이동 상태를 우선순위 기반으로 해석해 목표 액션을 반환합니다.
    /// </summary>
    private E_ActionType ResolveTargetAction(bool isGrounded)
    {
        if (_playerMovement.IsDashing)
        {
            return E_ActionType.Dash;
        }

        if (_playerMovement.IsWallJumpingState)
        {
            return E_ActionType.WallJump;
        }

        if (_playerMovement.IsWallSlidingState)
        {
            return E_ActionType.WallSlide;
        }

        if (_playerMovement.IsJumpingState)
        {
            return E_ActionType.Jump;
        }

        if (_playerMovement.IsFallingState)
        {
            return E_ActionType.Falling;
        }

        if (_playerMovement.Controller.IsSliding)
        {
            return E_ActionType.Slide;
        }

        if (!isGrounded)
        {
            return _playerMovement.Velocity.y > 0f ? E_ActionType.Jump : E_ActionType.Falling;
        }

        return IsMoveInputActive() ? E_ActionType.Move : E_ActionType.Idle;
    }

    /// <summary>
    /// 동일 액션 중복 요청을 방지하며 필요한 경우에만 액션 전환을 요청합니다.
    /// </summary>
    private void TryRequestAction(E_ActionType actionType)
    {
        ActionRuntime runtime = _actionController.Runtime; // 현재 실행 중인 액션 런타임 스냅샷
        if (runtime.IsRunning && runtime.ActionType == actionType)
        {
            return;
        }

        _actionController.RequestAction(actionType);
    }

    /// <summary>
    /// 이동 입력이 유효 임계값을 넘는지 판정합니다.
    /// </summary>
    private bool IsMoveInputActive()
    {
        float horizontalInput = _playerMovement.MoveInput.x; // 현재 프레임 이동 입력 판정에 사용할 수평 입력값
        return Mathf.Abs(horizontalInput) >= _moveInputThreshold;
    }

    /// <summary>
    /// 네트워크 소유권 기준으로 현재 인스턴스가 이동 액션 동기화를 수행할 수 있는지 판정합니다.
    /// </summary>
    private bool CanProcessActionSync()
    {
        if (_networkObject == null)
        {
            if (_enableOwnershipRouteLog)
            {
                Debug.Log($"[PlayerMovementActionSync] Movement route: network object missing -> treat as single/local. object={name}");
            }

            return true;
        }

        if (!_networkObject.IsSpawned)
        {
            if (!IsOfflineSingleRoute())
            {
                if (_enableOwnershipRouteLog)
                {
                    Debug.Log($"[PlayerMovementActionSync] Movement route blocked: waiting network spawn. object={name}");
                }

                return false;
            }

            if (_enableOwnershipRouteLog)
            {
                Debug.Log($"[PlayerMovementActionSync] Movement route: offline single fallback (network unspawned). object={name}");
            }

            return true;
        }

        if (_enableOwnershipRouteLog)
        {
            Debug.Log($"[PlayerMovementActionSync] Movement route: network owner={_networkObject.IsOwner}. object={name}");
        }

        return _networkObject.IsOwner;
    }

    /// <summary>
    /// 네트워크 오브젝트가 스폰되지 않은 상황이 오프라인 싱글플레이인지 판정합니다.
    /// </summary>
    private bool IsOfflineSingleRoute()
    {
        if (NetworkManager.Singleton == null)
        {
            if (_warnWhenOwnershipUnavailable)
            {
                Debug.LogWarning($"[PlayerMovementActionSync] NetworkManager.Singleton is null. Fallback to offline local movement route. object={name}");
            }

            return true;
        }

        return !NetworkManager.Singleton.IsListening;
    }

    /// <summary>
    /// 참조가 비어 있을 때 동일 오브젝트 기준으로 자동 보정을 시도합니다.
    /// </summary>
    private bool TryResolveReferences()
    {
        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
        }

        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }

        return _actionController != null && _playerMovement != null && _playerMovement.Controller != null;
    }
}
