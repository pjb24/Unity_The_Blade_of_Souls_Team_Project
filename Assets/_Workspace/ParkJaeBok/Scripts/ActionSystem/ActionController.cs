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
    [SerializeField] private AnimationMarkerProfile _animationMarkerProfile; // Animation Event marker 해석을 제공하는 ScriptableObject 프로필

    private readonly List<IActionListener> _listeners = new List<IActionListener>(); // 액션 변경 알림 리스너 목록
    private readonly Dictionary<E_ActionType, ActionRuleData> _ruleMap = new Dictionary<E_ActionType, ActionRuleData>(); // 빠른 규칙 조회용 맵
    private readonly Dictionary<string, AnimationMarkerMapData> _animationMarkerMap = new Dictionary<string, AnimationMarkerMapData>(); // 빠른 marker 해석 조회용 맵
    private readonly ActionRuntime _runtime = new ActionRuntime(); // 현재 액션 런타임 상태

    /// <summary>
    /// 현재 액션 런타임 상태를 반환합니다.
    /// </summary>
    public ActionRuntime Runtime => _runtime;

    /// <summary>
    /// 현재 컨트롤러의 액터 타입을 반환합니다.
    /// </summary>
    public E_ActionActorType ActorType => _actorType;

    /// <summary>
    /// 액션 규칙을 초기화하고 기본 액션 진입을 준비합니다.
    /// </summary>
    private void Awake()
    {
        BuildRuleMap();
        BuildAnimationMarkerMap();
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
        if (!IsActionEnabled(actionType))
        {
            Debug.LogWarning($"[ActionController] Action {actionType} is disabled for actorType {_actorType}.");
            return false;
        }

        if (_runtime.IsRunning)
        {
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
    /// Animation Event에서 전달된 마커 문자열을 처리합니다.
    /// </summary>
    public void OnAnimationMarker(string marker)
    {
        if (string.IsNullOrWhiteSpace(marker))
        {
            Debug.LogWarning("[ActionController] Empty animation marker received.");
            return;
        }

        string normalizedMarker = marker.Trim().ToLowerInvariant(); // marker 매핑 조회에 사용할 정규화 문자열

        if (TryHandleConfiguredMarker(normalizedMarker, marker))
        {
            return;
        }

        if (TryHandleLegacyMarker(normalizedMarker, marker))
        {
            return;
        }

        Debug.LogWarning($"[ActionController] Unknown animation marker: {marker}");
    }

    /// <summary>
    /// ScriptableObject marker 프로필 기반으로 marker를 처리하고 성공 여부를 반환합니다.
    /// </summary>
    private bool TryHandleConfiguredMarker(string normalizedMarker, string sourceMarker)
    {
        if (!_animationMarkerMap.TryGetValue(normalizedMarker, out AnimationMarkerMapData markerData))
        {
            return false;
        }

        ExecuteMarkerCommand(markerData, sourceMarker);
        return true;
    }

    /// <summary>
    /// 기존 하드코딩 marker 규칙으로 처리하고 성공 여부를 반환합니다.
    /// </summary>
    private bool TryHandleLegacyMarker(string normalizedMarker, string sourceMarker)
    {
        if (normalizedMarker == "start")
        {
            ApplyPhaseFromMarker(E_ActionPhase.Start);
            return true;
        }

        if (normalizedMarker == "progress")
        {
            ApplyPhaseFromMarker(E_ActionPhase.Progress);
            return true;
        }

        if (normalizedMarker == "complete")
        {
            CompleteCurrentAction();
            return true;
        }

        if (normalizedMarker == "cancel")
        {
            CancelCurrentAction("Animation marker cancel");
            return true;
        }

        if (normalizedMarker == "jump")
        {
            RequestActionFromMarker(E_ActionType.Jump, sourceMarker);
            return true;
        }

        if (normalizedMarker == "land")
        {
            RequestActionFromMarker(E_ActionType.Land, sourceMarker);
            return true;
        }

        if (normalizedMarker == "dash")
        {
            RequestActionFromMarker(E_ActionType.Dash, sourceMarker);
            return true;
        }

        if (normalizedMarker == "falling")
        {
            RequestActionFromMarker(E_ActionType.Falling, sourceMarker);
            return true;
        }

        if (normalizedMarker == "wallslide")
        {
            RequestActionFromMarker(E_ActionType.WallSlide, sourceMarker);
            return true;
        }

        if (normalizedMarker == "walljump")
        {
            RequestActionFromMarker(E_ActionType.WallJump, sourceMarker);
            return true;
        }

        if (normalizedMarker == "slide")
        {
            RequestActionFromMarker(E_ActionType.Slide, sourceMarker);
            return true;
        }

        return false;
    }

    /// <summary>
    /// marker 매핑 데이터의 명령 타입에 맞춰 액션/단계 전환 명령을 실행합니다.
    /// </summary>
    private void ExecuteMarkerCommand(AnimationMarkerMapData markerData, string sourceMarker)
    {
        switch (markerData.CommandType)
        {
            case E_AnimationMarkerCommandType.SetPhase:
                ApplyPhaseFromMarker(markerData.TargetPhase);
                return;
            case E_AnimationMarkerCommandType.Complete:
                CompleteCurrentAction();
                return;
            case E_AnimationMarkerCommandType.Cancel:
                string cancelReason = string.IsNullOrWhiteSpace(markerData.CancelReason) ? "Animation marker cancel" : markerData.CancelReason; // marker 취소 명령 실행 시 사용할 취소 사유 문자열
                CancelCurrentAction(cancelReason);
                return;
            case E_AnimationMarkerCommandType.RequestAction:
                RequestActionFromMarker(markerData.TargetActionType, sourceMarker);
                return;
            default:
                Debug.LogWarning($"[ActionController] Unsupported marker command type: {markerData.CommandType}");
                return;
        }
    }

    /// <summary>
    /// marker 명령으로 액션 단계를 변경하고 리스너에게 변경 이벤트를 전파합니다.
    /// </summary>
    private void ApplyPhaseFromMarker(E_ActionPhase targetPhase)
    {
        E_ActionPhase previousPhase = _runtime.SetPhase(targetPhase);
        NotifyActionPhaseChanged(previousPhase, targetPhase);
    }

    /// <summary>
    /// 애니메이션 마커 기반 액션 요청을 수행하고 실패 시 원인을 로그로 남깁니다.
    /// </summary>
    private void RequestActionFromMarker(E_ActionType actionType, string sourceMarker)
    {
        bool requestResult = RequestAction(actionType); // 마커가 지시한 액션 전환 성공 여부
        if (!requestResult)
        {
            Debug.LogWarning($"[ActionController] Marker action request failed: marker={sourceMarker}, action={actionType}");
        }
    }

    /// <summary>
    /// ScriptableObject marker 프로필을 Dictionary 형태로 구성합니다.
    /// </summary>
    private void BuildAnimationMarkerMap()
    {
        _animationMarkerMap.Clear();

        if (_animationMarkerProfile == null)
        {
            Debug.LogWarning($"[ActionController] AnimationMarkerProfile is not assigned on {name}. Fallback to legacy marker rules.");
            return;
        }

        AnimationMarkerMapData[] markerMaps = _animationMarkerProfile.MarkerMaps; // 현재 컨트롤러가 사용할 marker 매핑 프로필의 원본 배열
        for (int i = 0; i < markerMaps.Length; i++)
        {
            AnimationMarkerMapData mapData = markerMaps[i];
            if (string.IsNullOrWhiteSpace(mapData.Marker))
            {
                Debug.LogWarning("[ActionController] Empty marker string found in AnimationMarkerProfile.");
                continue;
            }

            string normalizedMarker = mapData.Marker.Trim().ToLowerInvariant(); // marker 조회 맵에 저장할 정규화 문자열
            if (_animationMarkerMap.ContainsKey(normalizedMarker))
            {
                Debug.LogWarning($"[ActionController] Duplicate animation marker mapping for {normalizedMarker}. Last one wins.");
            }

            _animationMarkerMap[normalizedMarker] = mapData;
        }
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
        ActionRuleData currentRule = GetRule(currentAction);
        ActionRuleData requestedRule = GetRule(requestedAction);

        if (!currentRule.IsInterruptible)
        {
            return false;
        }

        return requestedRule.Priority >= currentRule.Priority;
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
        };
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
        for (int i = 0; i < _listeners.Count; i++)
        {
            _listeners[i].OnActionStarted(_runtime);
        }
    }

    /// <summary>
    /// 단계 변경 알림을 전체 리스너에 전파합니다.
    /// </summary>
    private void NotifyActionPhaseChanged(E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
        for (int i = 0; i < _listeners.Count; i++)
        {
            _listeners[i].OnActionPhaseChanged(_runtime, previousPhase, currentPhase);
        }
    }

    /// <summary>
    /// 완료 알림을 전체 리스너에 전파합니다.
    /// </summary>
    private void NotifyActionCompleted()
    {
        for (int i = 0; i < _listeners.Count; i++)
        {
            _listeners[i].OnActionCompleted(_runtime);
        }
    }

    /// <summary>
    /// 취소 알림을 전체 리스너에 전파합니다.
    /// </summary>
    private void NotifyActionCancelled(string reason)
    {
        for (int i = 0; i < _listeners.Count; i++)
        {
            _listeners[i].OnActionCancelled(_runtime, reason);
        }
    }
}
