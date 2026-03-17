using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// New Input System 액션으로 액션 요청을 테스트하는 테스트 컴포넌트입니다.
/// </summary>
public class ActionSystemInputTest : MonoBehaviour
{
    [SerializeField] private ActionController _actionController; // 입력 결과를 전달할 액션 컨트롤러

    [Header("Request Actions")]
    [SerializeField] private InputActionReference _idleAction; // Idle 요청 입력 액션
    [SerializeField] private InputActionReference _moveAction; // Move 요청 입력 액션
    [SerializeField] private InputActionReference _jumpAction; // Jump 요청 입력 액션
    [SerializeField] private InputActionReference _landAction; // Land 요청 입력 액션
    [SerializeField] private InputActionReference _dashAction; // Dash 요청 입력 액션
    [SerializeField] private InputActionReference _fallingAction; // Falling 요청 입력 액션
    [SerializeField] private InputActionReference _wallSlideAction; // WallSlide 요청 입력 액션
    [SerializeField] private InputActionReference _wallJumpAction; // WallJump 요청 입력 액션
    [SerializeField] private InputActionReference _slideAction; // Slide 요청 입력 액션
    [SerializeField] private InputActionReference _attackAction; // Attack 요청 입력 액션
    [SerializeField] private InputActionReference _hitAction; // Hit 요청 입력 액션
    [SerializeField] private InputActionReference _dieAction; // Die 요청 입력 액션
    [SerializeField] private InputActionReference _useAction; // Use 요청 입력 액션
    [SerializeField] private InputActionReference _breakAction; // Break 요청 입력 액션

    [Header("Lifecycle Actions")]
    [SerializeField] private InputActionReference _completeAction; // 현재 액션 완료 입력 액션
    [SerializeField] private InputActionReference _cancelAction; // 현재 액션 취소 입력 액션

    /// <summary>
    /// 활성화 시 입력 액션을 등록하고 Enable 합니다.
    /// </summary>
    private void OnEnable()
    {
        RegisterAction(_idleAction, OnIdlePerformed);
        RegisterAction(_moveAction, OnMovePerformed);
        RegisterAction(_jumpAction, OnJumpPerformed);
        RegisterAction(_landAction, OnLandPerformed);
        RegisterAction(_dashAction, OnDashPerformed);
        RegisterAction(_fallingAction, OnFallingPerformed);
        RegisterAction(_wallSlideAction, OnWallSlidePerformed);
        RegisterAction(_wallJumpAction, OnWallJumpPerformed);
        RegisterAction(_slideAction, OnSlidePerformed);
        RegisterAction(_attackAction, OnAttackPerformed);
        RegisterAction(_hitAction, OnHitPerformed);
        RegisterAction(_dieAction, OnDiePerformed);
        RegisterAction(_useAction, OnUsePerformed);
        RegisterAction(_breakAction, OnBreakPerformed);
        RegisterAction(_completeAction, OnCompletePerformed);
        RegisterAction(_cancelAction, OnCancelPerformed);
    }

    /// <summary>
    /// 비활성화 시 입력 액션을 해제하고 Disable 합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterAction(_idleAction, OnIdlePerformed);
        UnregisterAction(_moveAction, OnMovePerformed);
        UnregisterAction(_jumpAction, OnJumpPerformed);
        UnregisterAction(_landAction, OnLandPerformed);
        UnregisterAction(_dashAction, OnDashPerformed);
        UnregisterAction(_fallingAction, OnFallingPerformed);
        UnregisterAction(_wallSlideAction, OnWallSlidePerformed);
        UnregisterAction(_wallJumpAction, OnWallJumpPerformed);
        UnregisterAction(_slideAction, OnSlidePerformed);
        UnregisterAction(_attackAction, OnAttackPerformed);
        UnregisterAction(_hitAction, OnHitPerformed);
        UnregisterAction(_dieAction, OnDiePerformed);
        UnregisterAction(_useAction, OnUsePerformed);
        UnregisterAction(_breakAction, OnBreakPerformed);
        UnregisterAction(_completeAction, OnCompletePerformed);
        UnregisterAction(_cancelAction, OnCancelPerformed);
    }

    /// <summary>
    /// Idle 입력 발생 시 Idle 액션 요청을 수행합니다.
    /// </summary>
    private void OnIdlePerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Idle);
    }

    /// <summary>
    /// Move 입력 발생 시 Move 액션 요청을 수행합니다.
    /// </summary>
    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Move);
    }

    /// <summary>
    /// Jump 입력 발생 시 Jump 액션 요청을 수행합니다.
    /// </summary>
    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Jump);
    }

    /// <summary>
    /// Land 입력 발생 시 Land 액션 요청을 수행합니다.
    /// </summary>
    private void OnLandPerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Land);
    }

    /// <summary>
    /// Dash 입력 발생 시 Dash 액션 요청을 수행합니다.
    /// </summary>
    private void OnDashPerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Dash);
    }

    /// <summary>
    /// Falling 입력 발생 시 Falling 액션 요청을 수행합니다.
    /// </summary>
    private void OnFallingPerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Falling);
    }

    /// <summary>
    /// WallSlide 입력 발생 시 WallSlide 액션 요청을 수행합니다.
    /// </summary>
    private void OnWallSlidePerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.WallSlide);
    }

    /// <summary>
    /// WallJump 입력 발생 시 WallJump 액션 요청을 수행합니다.
    /// </summary>
    private void OnWallJumpPerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.WallJump);
    }

    /// <summary>
    /// Slide 입력 발생 시 Slide 액션 요청을 수행합니다.
    /// </summary>
    private void OnSlidePerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Slide);
    }

    /// <summary>
    /// Attack 입력 발생 시 Attack 액션 요청을 수행합니다.
    /// </summary>
    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Attack);
    }

    /// <summary>
    /// Hit 입력 발생 시 Hit 액션 요청을 수행합니다.
    /// </summary>
    private void OnHitPerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Hit);
    }

    /// <summary>
    /// Die 입력 발생 시 Die 액션 요청을 수행합니다.
    /// </summary>
    private void OnDiePerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Die);
    }

    /// <summary>
    /// Use 입력 발생 시 Use 액션 요청을 수행합니다.
    /// </summary>
    private void OnUsePerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Use);
    }

    /// <summary>
    /// Break 입력 발생 시 Break 액션 요청을 수행합니다.
    /// </summary>
    private void OnBreakPerformed(InputAction.CallbackContext context)
    {
        RequestAction(E_ActionType.Break);
    }

    /// <summary>
    /// Complete 입력 발생 시 현재 액션 완료를 수행합니다.
    /// </summary>
    private void OnCompletePerformed(InputAction.CallbackContext context)
    {
        if (_actionController == null)
        {
            Debug.LogWarning("[ActionSystemInputTest] ActionController is not assigned.");
            return;
        }

        _actionController.CompleteCurrentAction();
    }

    /// <summary>
    /// Cancel 입력 발생 시 현재 액션 취소를 수행합니다.
    /// </summary>
    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        if (_actionController == null)
        {
            Debug.LogWarning("[ActionSystemInputTest] ActionController is not assigned.");
            return;
        }

        _actionController.CancelCurrentAction("Manual cancel input");
    }

    /// <summary>
    /// 공통 액션 요청을 수행하며 컨트롤러 누락 시 경고를 출력합니다.
    /// </summary>
    private void RequestAction(E_ActionType actionType)
    {
        if (_actionController == null)
        {
            Debug.LogWarning("[ActionSystemInputTest] ActionController is not assigned.");
            return;
        }

        _actionController.RequestAction(actionType);
    }

    /// <summary>
    /// InputActionReference를 안전하게 등록하고 Enable 합니다.
    /// </summary>
    private void RegisterAction(InputActionReference actionReference, System.Action<InputAction.CallbackContext> callback)
    {
        if (actionReference == null || actionReference.action == null)
        {
            Debug.LogWarning("[ActionSystemInputTest] InputActionReference is missing.");
            return;
        }

        actionReference.action.performed += callback;
        actionReference.action.Enable();
    }

    /// <summary>
    /// InputActionReference를 안전하게 해제하고 Disable 합니다.
    /// </summary>
    private void UnregisterAction(InputActionReference actionReference, System.Action<InputAction.CallbackContext> callback)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return;
        }

        actionReference.action.performed -= callback;
        actionReference.action.Disable();
    }
}
