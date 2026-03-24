using UnityEngine;

/// <summary>
/// Enemy 이동 상태를 ActionController의 Idle/Move 액션으로 동기화하는 브리지입니다.
/// </summary>
public class EnemyMovementActionSync : MonoBehaviour
{
    [Header("References")]
    [Tooltip("이동 상태 액션 요청을 전달할 액션 컨트롤러 참조입니다.")]
    [SerializeField] private ActionController _actionController; // 이동 상태 액션 요청을 전달할 액션 컨트롤러 참조입니다.
    [Tooltip("이동 중 여부를 제공할 Enemy 이동 드라이버 참조입니다.")]
    [SerializeField] private EnemyMovementDriver _movementDriver; // 이동 중 여부를 제공할 Enemy 이동 드라이버 참조입니다.

    [Header("Behavior")]
    [Tooltip("공격/피격 등 비이동 액션 실행 중 동기화를 중단할지 여부입니다.")]
    [SerializeField] private bool _suspendWhileNonMovementActionRunning = true; // 공격/피격 등 비이동 액션 실행 중 동기화를 중단할지 여부입니다.
    [Tooltip("정지 상태에서 요청할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _idleActionType = E_ActionType.Idle; // 정지 상태에서 요청할 액션 타입입니다.
    [Tooltip("이동 상태에서 요청할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _moveActionType = E_ActionType.Move; // 이동 상태에서 요청할 액션 타입입니다.

    /// <summary>
    /// 매 프레임 이동 상태를 읽어 액션 요청을 수행합니다.
    /// </summary>
    private void Update()
    {
        if (!TryResolveReferences())
        {
            return;
        }

        if (ShouldSuspendSync())
        {
            return;
        }

        E_ActionType targetAction = _movementDriver.IsMoving ? _moveActionType : _idleActionType;
        RequestIfNeeded(targetAction);
    }

    /// <summary>
    /// 현재 런타임 액션과 비교해 필요한 경우에만 액션 전환을 요청합니다.
    /// </summary>
    private void RequestIfNeeded(E_ActionType actionType)
    {
        ActionRuntime runtime = _actionController.Runtime; // 중복 요청 방지를 위한 현재 액션 런타임 스냅샷입니다.

        if (runtime.IsRunning && runtime.ActionType == actionType)
        {
            return;
        }

        _actionController.RequestAction(actionType);
    }

    /// <summary>
    /// 현재 액션이 비이동 계열일 때 동기화를 중단할지 판정합니다.
    /// </summary>
    private bool ShouldSuspendSync()
    {
        if (_suspendWhileNonMovementActionRunning == false)
        {
            return false;
        }

        ActionRuntime runtime = _actionController.Runtime; // 동기화 중단 판단에 사용할 현재 액션 런타임 정보입니다.
        if (!runtime.IsRunning)
        {
            return false;
        }

        return runtime.ActionType != _idleActionType && runtime.ActionType != _moveActionType;
    }

    /// <summary>
    /// 참조가 비어 있으면 동일 오브젝트에서 자동 보정을 시도합니다.
    /// </summary>
    private bool TryResolveReferences()
    {
        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
        }

        if (_movementDriver == null)
        {
            _movementDriver = GetComponent<EnemyMovementDriver>();
        }

        return _actionController != null && _movementDriver != null;
    }
}
