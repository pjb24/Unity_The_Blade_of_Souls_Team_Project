using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 액션 상태의 Source of Truth 역할을 담당하는 범용 액션 컨트롤러입니다.
/// </summary>
public class ActionController : MonoBehaviour
{
    [SerializeField] private E_ActionActorType _actorType = E_ActionActorType.Player; // 이 컨트롤러를 사용하는 액터 타입
    [SerializeField] private bool _autoStartDefaultAction = true; // 시작 시 기본 액션 자동 진입 여부
    [SerializeField] private E_ActionType _defaultActionType = E_ActionType.Idle; // 시작 시 자동 요청할 기본 액션 타입
    [SerializeField] private ActionRuleProfile _actionRuleProfile; // 액션 정책을 제공하는 ScriptableObject Rule 프로필
    [SerializeField] private ActionInterruptPolicyProfile _interruptPolicyProfile; // 현재/요청 액션 조합별 인터럽트 허용 정책 프로필
    [SerializeField] private E_ActionType[] _ignoreMarkerEndActions = new E_ActionType[] {
        E_ActionType.Jump,
        E_ActionType.Land,
        E_ActionType.Dash,
        E_ActionType.Falling,
        E_ActionType.WallSlide,
        E_ActionType.WallJump,
        E_ActionType.Slide
    }; // 마커 기반 Complete/Cancel을 무시할 액션 목록

    private readonly List<IActionListener> _listeners = new List<IActionListener>(); // 액션 변경 알림 리스너 목록
    private readonly Dictionary<E_ActionType, ActionRuleData> _ruleMap = new Dictionary<E_ActionType, ActionRuleData>(); // 빠른 규칙 조회용 맵
    private readonly ActionRuntime _runtime = new ActionRuntime(); // 현재 액션 런타임 상태
    private bool _isComboInputWindowOpen; // 현재 Combo 입력 허용 구간이 열린 상태인지 여부
    private bool _isHitWindowOpen; // 현재 공격 Hit 판정 허용 구간이 열린 상태인지 여부
    private bool _isDispatchingListenerCallbacks; // 리스너 콜백 전파 중인지 여부(재진입 요청 지연 처리용)
    private bool _hasDeferredActionRequest; // 콜백 전파 중 지연된 액션 요청이 존재하는지 여부
    private E_ActionType _deferredActionType = E_ActionType.None; // 콜백 전파 종료 후 재시도할 지연 액션 타입

    public System.Action<bool> OnComboInputWindowChanged; // Combo 입력 허용 구간 상태 변경 알림 이벤트
    public System.Action<bool> OnHitWindowChanged; // Hit 판정 허용 구간 상태 변경 알림 이벤트

    /// <summary>
    /// 현재 액션 런타임 상태를 반환합니다.
    /// </summary>
    public ActionRuntime Runtime => _runtime;

    /// <summary>
    /// 현재 컨트롤러의 액터 타입을 반환합니다.
    /// </summary>
    public E_ActionActorType ActorType => _actorType;

    /// <summary>
    /// 현재 Combo 입력 허용 구간이 열려 있는지 반환합니다.
    /// </summary>
    public bool IsComboInputWindowOpen => _isComboInputWindowOpen;

    /// <summary>
    /// 현재 Hit 판정 허용 구간이 열려 있는지 반환합니다.
    /// </summary>
    public bool IsHitWindowOpen => _isHitWindowOpen;

    /// <summary>
    /// 액션 규칙을 초기화하고 기본 액션 진입을 준비합니다.
    /// </summary>
    private void Awake()
    {
        BuildRuleMap();
    }

    /// <summary>
    /// 설정에 따라 기본 액션을 자동 시작합니다.
    /// </summary>
    private void Start()
    {
        if (_autoStartDefaultAction)
        {
            RequestAction(_defaultActionType);
        }
    }

    /// <summary>
    /// 액션 리스너를 등록합니다.
    /// </summary>
    public void AddListener(IActionListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[ActionController] Cannot add null listener.");
            return;
        }

        if (_listeners.Contains(listener))
        {
            Debug.LogWarning("[ActionController] Duplicate listener registration ignored.");
            return;
        }

        _listeners.Add(listener);
    }

    /// <summary>
    /// 액션 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(IActionListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[ActionController] Cannot remove null listener.");
            return;
        }

        if (!_listeners.Remove(listener))
        {
            Debug.LogWarning("[ActionController] Tried to remove unknown listener.");
        }
    }

    /// <summary>
    /// 요청된 액션으로 전환을 시도합니다.
    /// </summary>
    public bool RequestAction(E_ActionType actionType)
    {
        if (_isDispatchingListenerCallbacks)
        {
            QueueDeferredActionRequest(actionType);
            return true;
        }

        if (!IsActionEnabled(actionType))
        {
            Debug.LogWarning($"[ActionController] Action {actionType} is disabled for actorType {_actorType}.");
            return false;
        }

        if (_runtime.IsRunning)
        {
            if (_runtime.ActionType == actionType)
            {
                Debug.Log($"[ActionController] Request ignored: already running action {actionType}.");
                return true;
            }

            if (!CanInterrupt(_runtime.ActionType, actionType))
            {
                Debug.LogWarning($"[ActionController] Request denied: {_runtime.ActionType} -> {actionType}");
                return false;
            }

            CancelCurrentAction($"Interrupted by {actionType}");
        }

        _runtime.Begin(actionType);
        NotifyActionStarted();

        E_ActionPhase previousPhase = _runtime.SetPhase(E_ActionPhase.Progress);
        NotifyActionPhaseChanged(previousPhase, E_ActionPhase.Progress);

        TryScheduleAutoComplete(_runtime.ExecutionId, actionType);
        return true;
    }

    /// <summary>
    /// 네트워크로 확정된 액션 시작을 로컬 규칙 검사 없이 그대로 반영합니다.
    /// </summary>
    public bool ApplyReplicatedActionStart(E_ActionType actionType, string sourceContext)
    {
        if (_isDispatchingListenerCallbacks)
        {
            Debug.LogWarning($"[ActionController] Replicated action start ignored during listener dispatch. action={actionType}, source={sourceContext}");
            return false;
        }

        if (actionType == E_ActionType.None)
        {
            Debug.LogWarning($"[ActionController] Replicated action start ignored: invalid action type None. source={sourceContext}");
            return false;
        }

        if (_runtime.IsRunning)
        {
            CancelCurrentAction($"Replicated override by {actionType} ({sourceContext})");
        }

        _runtime.Begin(actionType);
        NotifyActionStarted();

        E_ActionPhase previousPhase = _runtime.SetPhase(E_ActionPhase.Progress);
        NotifyActionPhaseChanged(previousPhase, E_ActionPhase.Progress);

        TryScheduleAutoComplete(_runtime.ExecutionId, actionType);
        return true;
    }

    /// <summary>
    /// 네트워크로 확정된 액션 종료를 반영하고 필요 시 기본 액션으로 복귀시킵니다.
    /// </summary>
    public bool ApplyReplicatedActionStop(E_ActionType fallbackActionType, string sourceContext)
    {
        if (_isDispatchingListenerCallbacks)
        {
            Debug.LogWarning($"[ActionController] Replicated action stop ignored during listener dispatch. fallback={fallbackActionType}, source={sourceContext}");
            return false;
        }

        if (_runtime.IsRunning)
        {
            CompleteCurrentAction();
        }

        if (fallbackActionType == E_ActionType.None)
        {
            return true;
        }

        return RequestAction(fallbackActionType);
    }

    /// <summary>
    /// 리스너 콜백 전파 중 발생한 액션 요청을 단일 슬롯에 저장합니다.
    /// </summary>
    private void QueueDeferredActionRequest(E_ActionType actionType)
    {
        if (_hasDeferredActionRequest && _deferredActionType != actionType)
        {
            Debug.LogWarning($"[ActionController] Deferred action request overwritten: {_deferredActionType} -> {actionType}");
        }

        _deferredActionType = actionType;
        _hasDeferredActionRequest = true;
        Debug.Log($"[ActionController] Request deferred during listener dispatch: {actionType}");
    }

    /// <summary>
    /// 콜백 전파가 끝난 뒤 지연된 액션 요청이 있으면 재실행합니다.
    /// </summary>
    private void FlushDeferredActionRequest()
    {
        if (!_hasDeferredActionRequest)
        {
            return;
        }

        E_ActionType deferredActionType = _deferredActionType; // 전파 종료 후 재실행할 지연 액션 타입 스냅샷
        _hasDeferredActionRequest = false;
        _deferredActionType = E_ActionType.None;

        bool accepted = RequestAction(deferredActionType); // 지연 액션 요청의 재실행 결과
        if (!accepted)
        {
            Debug.LogWarning($"[ActionController] Deferred action request denied: {deferredActionType}");
        }
    }

    /// <summary>
    /// 현재 실행 중인 액션을 완료 처리합니다.
    /// </summary>
    public void CompleteCurrentAction()
    {
        if (!_runtime.IsRunning)
        {
            Debug.LogWarning("[ActionController] CompleteCurrentAction ignored: no running action.");
            return;
        }

        E_ActionPhase previousPhase = _runtime.Phase;
        _runtime.Complete();
        NotifyActionPhaseChanged(previousPhase, E_ActionPhase.Complete);
        NotifyActionCompleted();
    }

    /// <summary>
    /// 현재 실행 중인 액션을 취소 처리합니다.
    /// </summary>
    public void CancelCurrentAction(string reason)
    {
        if (!_runtime.IsRunning)
        {
            Debug.LogWarning("[ActionController] CancelCurrentAction ignored: no running action.");
            return;
        }

        E_ActionPhase previousPhase = _runtime.Phase;
        _runtime.Cancel(reason);
        NotifyActionPhaseChanged(previousPhase, E_ActionPhase.Cancel);
        NotifyActionCancelled(_runtime.CancelReason);
    }

    /// <summary>
    /// Animation Event에서 전달된 Object 마커를 해석해 액션 명령을 실행합니다.
    /// </summary>
    public void ReceiveMarker(Object markerObject)
    {
        if (markerObject == null)
        {
            Debug.LogWarning("[ActionController] Null marker object received.");
            return;
        }

        if (markerObject is not ActionMarkerCommandObject markerCommandObject)
        {
            Debug.LogWarning($"[ActionController] Unsupported marker object type: {markerObject.GetType().Name}");
            return;
        }

        ExecuteMarkerCommandObject(markerCommandObject);
    }

    /// <summary>
    /// Object 마커에 정의된 enum 명령을 실행합니다.
    /// </summary>
    private void ExecuteMarkerCommandObject(ActionMarkerCommandObject markerCommandObject)
    {
        switch (markerCommandObject.CommandType)
        {
            case E_ActionMarkerCommandType.CompleteCurrentAction:
                if (ShouldIgnoreMarkerDrivenEnd())
                {
                    Debug.Log($"[ActionController] Complete marker ignored for runtime action {_runtime.ActionType}.");
                    return;
                }

                CompleteCurrentAction();
                return;
            case E_ActionMarkerCommandType.CancelCurrentAction:
                if (ShouldIgnoreMarkerDrivenEnd())
                {
                    Debug.Log($"[ActionController] Cancel marker ignored for runtime action {_runtime.ActionType}.");
                    return;
                }

                string cancelReason = string.IsNullOrWhiteSpace(markerCommandObject.CancelReason) ? "Animation marker cancel" : markerCommandObject.CancelReason; // Cancel 명령 실행 시 사용할 취소 사유 문자열
                CancelCurrentAction(cancelReason);
                return;
            case E_ActionMarkerCommandType.ComboStart:
                SetComboInputWindow(true, markerCommandObject.name);
                return;
            case E_ActionMarkerCommandType.ComboEnd:
                SetComboInputWindow(false, markerCommandObject.name);
                return;
            case E_ActionMarkerCommandType.HitStart:
                SetHitWindow(true, markerCommandObject.name);
                return;
            case E_ActionMarkerCommandType.HitEnd:
                SetHitWindow(false, markerCommandObject.name);
                return;
            default:
                Debug.LogWarning($"[ActionController] Unsupported marker command type: {markerCommandObject.CommandType}");
                return;
        }
    }

    /// <summary>
    /// 현재 실행 액션이 마커 기반 종료 무시 대상인지 판정합니다.
    /// </summary>
    private bool ShouldIgnoreMarkerDrivenEnd()
    {
        if (!_runtime.IsRunning)
        {
            return false;
        }

        for (int i = 0; i < _ignoreMarkerEndActions.Length; i++)
        {
            if (_ignoreMarkerEndActions[i] == _runtime.ActionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Combo 입력 허용 구간의 시작/종료를 반영하고 변경 시 이벤트를 전파합니다.
    /// </summary>
    private void SetComboInputWindow(bool isOpen, string sourceMarker)
    {
        if (_isComboInputWindowOpen == isOpen)
        {
            return;
        }

        _isComboInputWindowOpen = isOpen;
        OnComboInputWindowChanged?.Invoke(_isComboInputWindowOpen);
        Debug.Log($"[ActionController] Combo input window set to {_isComboInputWindowOpen} by marker={sourceMarker}");
    }

    /// <summary>
    /// 공격 Hit 판정 허용 구간의 시작/종료를 반영하고 변경 시 이벤트를 전파합니다.
    /// </summary>
    private void SetHitWindow(bool isOpen, string sourceMarker)
    {
        if (_isHitWindowOpen == isOpen)
        {
            return;
        }

        _isHitWindowOpen = isOpen;
        OnHitWindowChanged?.Invoke(_isHitWindowOpen);
        Debug.Log($"[ActionController] Hit window set to {_isHitWindowOpen} by marker={sourceMarker}");
    }

    /// <summary>
    /// ScriptableObject Rule 프로필을 Dictionary 형태로 구성합니다.
    /// </summary>
    private void BuildRuleMap()
    {
        _ruleMap.Clear();

        if (_actionRuleProfile == null)
        {
            Debug.LogWarning($"[ActionController] ActionRuleProfile is not assigned on {name}. All actions will use fallback rules.");
            return;
        }

        if (_actionRuleProfile.ActorType != _actorType)
        {
            Debug.LogWarning($"[ActionController] ActorType mismatch: controller={_actorType}, profile={_actionRuleProfile.ActorType} on {name}.");
        }

        ActionRuleData[] profileRules = _actionRuleProfile.Rules; // 현재 컨트롤러가 사용할 Rule 프로필의 원본 배열
        if (profileRules == null)
        {
            Debug.LogWarning($"[ActionController] ActionRuleProfile.Rules is null on {name}. All actions will use fallback rules.");
            return;
        }

        for (int i = 0; i < profileRules.Length; i++)
        {
            ActionRuleData rule = profileRules[i];
            if (_ruleMap.ContainsKey(rule.ActionType))
            {
                Debug.LogWarning($"[ActionController] Duplicate rule for {rule.ActionType}. Last one wins.");
            }

            _ruleMap[rule.ActionType] = rule;
        }
    }

    /// <summary>
    /// 요청 액션의 사용 가능 여부를 규칙 기반으로 판정합니다.
    /// </summary>
    private bool IsActionEnabled(E_ActionType actionType)
    {
        ActionRuleData rule = GetRule(actionType);
        return rule.Enabled;
    }

    /// <summary>
    /// 현재 액션이 다음 액션으로 인터럽트 가능한지 판정합니다.
    /// </summary>
    private bool CanInterrupt(E_ActionType currentAction, E_ActionType requestedAction)
    {
        if (TryResolveInterruptPolicyDecision(currentAction, requestedAction, out bool policyDecision))
        {
            return policyDecision;
        }

        ActionRuleData currentRule = GetRule(currentAction);
        ActionRuleData requestedRule = GetRule(requestedAction);

        if (!currentRule.IsInterruptible)
        {
            return false;
        }

        return requestedRule.Priority >= currentRule.Priority;
    }

    /// <summary>
    /// 인터럽트 정책 프로필 규칙을 평가해 허용/거부 결정을 반환합니다.
    /// </summary>
    private bool TryResolveInterruptPolicyDecision(E_ActionType currentAction, E_ActionType requestedAction, out bool decision)
    {
        decision = false;

        if (_interruptPolicyProfile == null)
        {
            return false;
        }

        ActionInterruptRuleData[] rules = _interruptPolicyProfile.Rules; // 현재/요청 액션 인터럽트 정책 평가에 사용할 규칙 배열
        if (rules == null || rules.Length == 0)
        {
            return false;
        }

        int bestPriority = int.MinValue; // 현재까지 선택된 규칙 우선순위
        bool hasMatch = false;
        ActionInterruptRuleData selectedRule = default;

        for (int i = 0; i < rules.Length; i++)
        {
            ActionInterruptRuleData candidate = rules[i];
            if (!candidate.Enabled)
            {
                continue;
            }

            if (!IsInterruptRuleActionMatch(candidate.CurrentActionType, currentAction))
            {
                continue;
            }

            if (!IsInterruptRuleActionMatch(candidate.RequestedActionType, requestedAction))
            {
                continue;
            }

            if (candidate.RequireComboWindowOpen && !_isComboInputWindowOpen)
            {
                continue;
            }

            if (candidate.RequireHitWindowOpen && !_isHitWindowOpen)
            {
                continue;
            }

            if (candidate.RequireCurrentPhaseMatch && _runtime.Phase != candidate.RequiredCurrentPhase)
            {
                continue;
            }

            if (!hasMatch || candidate.Priority > bestPriority)
            {
                selectedRule = candidate;
                bestPriority = candidate.Priority;
                hasMatch = true;
            }
        }

        if (!hasMatch)
        {
            return false;
        }

        if (selectedRule.Decision == E_ActionInterruptDecision.UseDefault)
        {
            return false;
        }

        decision = selectedRule.Decision == E_ActionInterruptDecision.Allow;
        return true;
    }

    /// <summary>
    /// 인터럽트 정책 규칙 액션 필터와 현재 평가 대상 액션의 일치 여부를 판정합니다.
    /// </summary>
    private bool IsInterruptRuleActionMatch(E_ActionType ruleActionType, E_ActionType evaluatedActionType)
    {
        if (ruleActionType == E_ActionType.None)
        {
            return true;
        }

        return ruleActionType == evaluatedActionType;
    }

    /// <summary>
    /// 액션 정책을 조회하며 누락 시 기본 정책으로 폴백합니다.
    /// </summary>
    private ActionRuleData GetRule(E_ActionType actionType)
    {
        if (_ruleMap.TryGetValue(actionType, out ActionRuleData rule))
        {
            return rule;
        }

        Debug.LogWarning($"[ActionController] Missing rule for {actionType} on actorType {_actorType}. Fallback to default rule.");
        return new ActionRuleData
        {
            ActionType = actionType,
            Enabled = true,
            Priority = 0,
            IsInterruptible = true,
            AutoCompleteSeconds = 0f,
            OverrideMovementLockSetting = false,
            LockMovementDuringAction = false,
        };
    }

    /// <summary>
    /// 지정한 액션 타입의 Rule을 프로필에서 직접 조회합니다.
    /// </summary>
    public bool TryGetActionRule(E_ActionType actionType, out ActionRuleData rule)
    {
        return _ruleMap.TryGetValue(actionType, out rule);
    }

    /// <summary>
    /// 자동 완료 시간이 설정된 액션에 대해 완료 코루틴을 시작합니다.
    /// </summary>
    private void TryScheduleAutoComplete(int executionId, E_ActionType actionType)
    {
        ActionRuleData rule = GetRule(actionType);
        if (rule.AutoCompleteSeconds <= 0f)
        {
            return;
        }

        StartCoroutine(AutoCompleteRoutine(executionId, rule.AutoCompleteSeconds));
    }

    /// <summary>
    /// 지정 시간이 지난 뒤 동일 실행 ID가 유지되면 액션을 완료 처리합니다.
    /// </summary>
    private IEnumerator AutoCompleteRoutine(int executionId, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!_runtime.IsRunning)
        {
            yield break;
        }

        if (_runtime.ExecutionId != executionId)
        {
            yield break;
        }

        CompleteCurrentAction();
    }

    /// <summary>
    /// 시작 알림을 전체 리스너에 전파합니다.
    /// </summary>
    private void NotifyActionStarted()
    {
        _isDispatchingListenerCallbacks = true;

        try
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i].OnActionStarted(_runtime);
            }
        }
        finally
        {
            _isDispatchingListenerCallbacks = false;
        }

        FlushDeferredActionRequest();
    }

    /// <summary>
    /// 단계 변경 알림을 전체 리스너에 전파합니다.
    /// </summary>
    private void NotifyActionPhaseChanged(E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
        _isDispatchingListenerCallbacks = true;

        try
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i].OnActionPhaseChanged(_runtime, previousPhase, currentPhase);
            }
        }
        finally
        {
            _isDispatchingListenerCallbacks = false;
        }

        FlushDeferredActionRequest();
    }

    /// <summary>
    /// 완료 알림을 전체 리스너에 전파합니다.
    /// </summary>
    private void NotifyActionCompleted()
    {
        _isDispatchingListenerCallbacks = true;

        try
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i].OnActionCompleted(_runtime);
            }
        }
        finally
        {
            _isDispatchingListenerCallbacks = false;
        }

        FlushDeferredActionRequest();
    }

    /// <summary>
    /// 취소 알림을 전체 리스너에 전파합니다.
    /// </summary>
    private void NotifyActionCancelled(string reason)
    {
        _isDispatchingListenerCallbacks = true;

        try
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i].OnActionCancelled(_runtime, reason);
            }
        }
        finally
        {
            _isDispatchingListenerCallbacks = false;
        }

        FlushDeferredActionRequest();
    }
}
