using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 공격 입력을 버퍼링해 Attack Combo 액션을 순차 요청하는 컨트롤러입니다.
/// </summary>
public class ActionComboController : MonoBehaviour, IActionListener
{
    [SerializeField] private ActionController _actionController; // 콤보 액션 요청을 전달할 액션 컨트롤러
    [SerializeField] private InputActionReference _attackInputAction; // 콤보 공격 입력 액션 참조
    [SerializeField] private E_ActionType[] _comboActions = new E_ActionType[] { E_ActionType.AttackCombo1, E_ActionType.AttackCombo2, E_ActionType.AttackCombo3 }; // 콤보 단계별 액션 목록

    private bool _isComboWindowOpen; // 현재 콤보 입력 허용 창이 열린 상태인지 여부
    private bool _hasBufferedInput; // 콤보 다음 단계 요청이 버퍼링되어 있는지 여부

    /// <summary>
    /// 활성화 시 입력 구독과 액션 리스너 등록을 수행합니다.
    /// </summary>
    private void OnEnable()
    {
        RegisterInputAction();
        RegisterListener();
    }

    /// <summary>
    /// 비활성화 시 입력 구독과 액션 리스너 등록을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterInputAction();
        UnregisterListener();
        ResetBufferedState();
    }

    /// <summary>
    /// 공격 입력 발생 시 콤보 시작 또는 다음 단계 버퍼링을 처리합니다.
    /// </summary>
    private void OnAttackInputPerformed(InputAction.CallbackContext context)
    {
        if (_actionController == null)
        {
            Debug.LogWarning("[ActionComboController] ActionController is not assigned.");
            return;
        }

        ActionRuntime runtime = _actionController.Runtime;

        if (!runtime.IsRunning || !IsComboAction(runtime.ActionType))
        {
            TryRequestComboAction(E_ActionType.AttackCombo1);
            return;
        }

        if (_isComboWindowOpen)
        {
            _hasBufferedInput = true;
            return;
        }

        Debug.LogWarning("[ActionComboController] Combo input ignored because combo window is closed.");
    }

    /// <summary>
    /// Animation Event에서 호출해 콤보 입력 허용 창을 엽니다.
    /// </summary>
    public void OpenComboWindow()
    {
        _isComboWindowOpen = true;
    }

    /// <summary>
    /// Animation Event에서 호출해 콤보 입력 허용 창을 닫고 필요 시 다음 콤보를 요청합니다.
    /// </summary>
    public void CloseComboWindow()
    {
        _isComboWindowOpen = false;

        if (!_hasBufferedInput)
        {
            return;
        }

        _hasBufferedInput = false;
        RequestNextComboAction();
    }

    /// <summary>
    /// 액션 시작 콜백에서 현재 구현은 별도 처리를 수행하지 않습니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
    }

    /// <summary>
    /// 액션 단계 변경 콜백에서 현재 구현은 별도 처리를 수행하지 않습니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
    }

    /// <summary>
    /// 콤보 액션 완료 시 입력 버퍼 상태를 정리합니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
        if (!IsComboAction(runtime.ActionType))
        {
            return;
        }

        _isComboWindowOpen = false;
        _hasBufferedInput = false;
    }

    /// <summary>
    /// 콤보 액션 취소 시 입력 버퍼 상태를 정리합니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
        if (!IsComboAction(runtime.ActionType))
        {
            return;
        }

        _isComboWindowOpen = false;
        _hasBufferedInput = false;
    }

    /// <summary>
    /// 현재 콤보 단계 기준 다음 콤보 액션을 요청합니다.
    /// </summary>
    private void RequestNextComboAction()
    {
        if (_actionController == null)
        {
            Debug.LogWarning("[ActionComboController] ActionController is not assigned.");
            return;
        }

        E_ActionType currentActionType = _actionController.Runtime.ActionType;
        int currentIndex = FindComboIndex(currentActionType);
        if (currentIndex < 0)
        {
            Debug.LogWarning($"[ActionComboController] Current action {currentActionType} is not part of combo.");
            return;
        }

        int nextIndex = currentIndex + 1;
        if (nextIndex >= _comboActions.Length)
        {
            return;
        }

        TryRequestComboAction(_comboActions[nextIndex]);
    }

    /// <summary>
    /// 지정한 콤보 액션 요청을 시도합니다.
    /// </summary>
    private void TryRequestComboAction(E_ActionType actionType)
    {
        if (_actionController == null)
        {
            Debug.LogWarning("[ActionComboController] ActionController is not assigned.");
            return;
        }

        if (!_actionController.RequestAction(actionType))
        {
            Debug.LogWarning($"[ActionComboController] Failed to request combo action {actionType}.");
        }
    }

    /// <summary>
    /// 지정 액션이 콤보 액션 목록에 포함되는지 판정합니다.
    /// </summary>
    private bool IsComboAction(E_ActionType actionType)
    {
        return FindComboIndex(actionType) >= 0;
    }

    /// <summary>
    /// 콤보 액션 목록에서 지정 액션의 인덱스를 찾습니다.
    /// </summary>
    private int FindComboIndex(E_ActionType actionType)
    {
        for (int i = 0; i < _comboActions.Length; i++)
        {
            if (_comboActions[i] == actionType)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 입력 구독과 액션 Enable을 수행합니다.
    /// </summary>
    private void RegisterInputAction()
    {
        if (_attackInputAction == null || _attackInputAction.action == null)
        {
            Debug.LogWarning("[ActionComboController] Attack input action is missing.");
            return;
        }

        _attackInputAction.action.performed += OnAttackInputPerformed;
        _attackInputAction.action.Enable();
    }

    /// <summary>
    /// 입력 구독 해제와 액션 Disable을 수행합니다.
    /// </summary>
    private void UnregisterInputAction()
    {
        if (_attackInputAction == null || _attackInputAction.action == null)
        {
            return;
        }

        _attackInputAction.action.performed -= OnAttackInputPerformed;
        _attackInputAction.action.Disable();
    }

    /// <summary>
    /// 액션 컨트롤러 리스너 등록을 수행합니다.
    /// </summary>
    private void RegisterListener()
    {
        if (_actionController == null)
        {
            Debug.LogWarning("[ActionComboController] ActionController is not assigned.");
            return;
        }

        _actionController.AddListener(this);
    }

    /// <summary>
    /// 액션 컨트롤러 리스너 해제를 수행합니다.
    /// </summary>
    private void UnregisterListener()
    {
        if (_actionController == null)
        {
            return;
        }

        _actionController.RemoveListener(this);
    }

    /// <summary>
    /// 콤보 입력 버퍼 상태를 초기화합니다.
    /// </summary>
    private void ResetBufferedState()
    {
        _isComboWindowOpen = false;
        _hasBufferedInput = false;
    }
}
