using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static PlayerInput PlayerInput;

    public static Vector2 Movement;
    public static bool JumpWasPressed;
    public static bool JumpIsHeld;
    public static bool JumpWasReleased;
    public static bool RunIsHeld;
    public static bool DashWasPressed;
    public static bool AttackWasPressed;

    [Header("Runtime Options")]
    [Tooltip("Attack 액션을 찾지 못했을 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingAttackAction = true; // Attack 액션 미구성 시 경고 로그 출력 여부입니다.

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _runAction;
    private InputAction _dashAction;
    private InputAction _attackAction;

    /// <summary>
    /// InputManager 초기화 시 PlayerInput의 주요 액션 참조를 캐시합니다.
    /// </summary>
    private void Awake()
    {
        PlayerInput = GetComponent<PlayerInput>();

        _moveAction = PlayerInput.actions["Move"];
        _jumpAction = PlayerInput.actions["Jump"];
        _runAction = PlayerInput.actions["Run"];

        _dashAction = PlayerInput.actions["Dash"];

        _attackAction = PlayerInput.actions.FindAction("Attack", false);

        if (_attackAction == null && _warnMissingAttackAction)
        {
            Debug.LogWarning($"[InputManager] Attack action is missing on {name}.");
        }
    }

    /// <summary>
    /// 매 프레임 입력 스냅샷을 정적 상태로 갱신합니다.
    /// </summary>
    private void Update()
    {
        Movement = _moveAction.ReadValue<Vector2>();

        JumpWasPressed = _jumpAction.WasPressedThisFrame();
        JumpIsHeld = _jumpAction.IsPressed();
        JumpWasReleased = _jumpAction.WasReleasedThisFrame();

        RunIsHeld = _runAction.IsPressed();

        //DashWasPressed = _dashAction.WasPressedThisFrame();
        DashWasPressed = _dashAction.WasPerformedThisFrame();

        AttackWasPressed = _attackAction != null && _attackAction.WasPressedThisFrame();
    }
}
