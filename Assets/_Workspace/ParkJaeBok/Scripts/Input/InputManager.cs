using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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
    public static bool BuffWasPressed;

    private static readonly HashSet<object> GameplayInputBlockers = new HashSet<object>(); // Gameplay 입력을 차단 중인 시스템 목록입니다.

    [Header("Runtime Options")]
    [Tooltip("Attack 액션을 찾지 못했을 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingAttackAction = true; // Attack 액션 미구성 시 경고 로그 출력 여부입니다.
    [Tooltip("Buff 액션을 찾지 못했을 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingBuffAction = true; // Buff 액션 미구성 시 경고 로그 출력 여부입니다.

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _runAction;
    private InputAction _dashAction;
    private InputAction _attackAction;
    private InputAction _buffAction;

    /// <summary>
    /// 현재 gameplay 입력이 UI나 시스템 상태에 의해 차단되어 있는지 반환합니다.
    /// </summary>
    public static bool IsGameplayInputBlocked => GameplayInputBlockers.Count > 0;

    /// <summary>
    /// 지정한 소유자를 gameplay 입력 차단 목록에 등록합니다.
    /// </summary>
    public static void AddGameplayInputBlocker(object blocker)
    {
        if (blocker == null)
        {
            Debug.LogWarning("[InputManager] Cannot add a null gameplay input blocker.");
            return;
        }

        GameplayInputBlockers.Add(blocker);
    }

    /// <summary>
    /// 지정한 소유자를 gameplay 입력 차단 목록에서 제거합니다.
    /// </summary>
    public static void RemoveGameplayInputBlocker(object blocker)
    {
        if (blocker == null)
        {
            Debug.LogWarning("[InputManager] Cannot remove a null gameplay input blocker.");
            return;
        }

        if (!GameplayInputBlockers.Remove(blocker))
        {
            Debug.LogWarning("[InputManager] Tried to remove an unknown gameplay input blocker.");
        }
    }

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

        _buffAction = PlayerInput.actions.FindAction("Buff", false);
        if (_buffAction == null && _warnMissingBuffAction)
        {
            Debug.LogWarning($"[InputManager] Buff action is missing on {name}.");
        }
    }

    /// <summary>
    /// 매 프레임 입력 스냅샷을 정적 상태로 갱신합니다.
    /// </summary>
    private void Update()
    {
        if (IsGameplayInputBlocked)
        {
            Movement = Vector2.zero;
            JumpWasPressed = false;
            JumpIsHeld = false;
            JumpWasReleased = false;
            RunIsHeld = false;
            DashWasPressed = false;
            AttackWasPressed = false;
            BuffWasPressed = false;
            return;
        }

        Movement = _moveAction.ReadValue<Vector2>();

        JumpWasPressed = _jumpAction.WasPressedThisFrame();
        JumpIsHeld = _jumpAction.IsPressed();
        JumpWasReleased = _jumpAction.WasReleasedThisFrame();

        RunIsHeld = _runAction.IsPressed();

        //DashWasPressed = _dashAction.WasPressedThisFrame();
        DashWasPressed = _dashAction.WasPerformedThisFrame();

        AttackWasPressed = _attackAction != null && _attackAction.WasPressedThisFrame();
        BuffWasPressed = _buffAction != null && _buffAction.WasPressedThisFrame();
    }
}
