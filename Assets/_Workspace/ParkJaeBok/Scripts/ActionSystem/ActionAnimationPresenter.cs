using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 액션 상태를 Animator 표현 계층으로 변환하는 전용 프리젠터입니다.
/// </summary>
public class ActionAnimationPresenter : MonoBehaviour, IActionListener
{
    [SerializeField] private ActionController _actionController; // 액션 상태를 제공하는 컨트롤러
    [SerializeField] private Animator _animator; // 애니메이션 재생 대상 Animator
    [SerializeField] private AnimationStateMapProfile _stateMapProfile; // 액션-애니메이션 매핑을 제공하는 ScriptableObject 프로필
    [SerializeField] private bool _playDefaultOnActionEnd = true; // 액션 완료/취소 후 기본 애니메이션 복귀 여부
    [SerializeField] private E_ActionType _defaultPresentationAction = E_ActionType.Idle; // 완료/취소 시 재생할 기본 표현 액션
    [SerializeField] private E_ActionType[] _skipDefaultOnEndActions = new E_ActionType[0]; // 완료/취소 시 기본 복귀를 생략할 액션 목록

    [Header("Presentation Lock")]
    [SerializeField] private bool _enablePresentationLock = false; // 특정 액션 재생 이후 애니메이션 전환 잠금 기능 활성화 여부
    [SerializeField] private E_ActionType[] _presentationLockTriggerActions = new E_ActionType[0]; // 재생 시 애니메이션 전환 잠금을 시작할 액션 목록
    [SerializeField] private E_ActionType[] _presentationLockReleaseActions = new E_ActionType[0]; // 잠금 상태에서 전환을 허용하고 잠금을 해제할 액션 목록

    [Header("Idle Break")]
    [SerializeField] private bool _enableIdleBreak = false; // Idle 장기 지속 시 Idle Break 재생 기능 활성화 여부
    [SerializeField] private E_ActionType _idleBreakBaseAction = E_ActionType.Idle; // Idle Break를 감시할 기준 액션 타입
    [SerializeField] private E_ActionType[] _idleBreakActions = new E_ActionType[0]; // 랜덤 재생할 Idle Break 액션 후보 목록
    [SerializeField] private float _idleBreakMinDelaySeconds = 4f; // Idle Break 랜덤 대기 최소 시간(초)
    [SerializeField] private float _idleBreakMaxDelaySeconds = 8f; // Idle Break 랜덤 대기 최대 시간(초)

    [Header("Listener Bind Retry")]
    [SerializeField] private float _retryInterval = 0.1f; // 리스너 등록 재시도 코루틴 간격(초)
    [SerializeField] private int _maxRetryCount = 30; // 리스너 등록 재시도 최대 횟수

    private readonly Dictionary<E_ActionType, int> _stateHashByAction = new Dictionary<E_ActionType, int>(); // 액션별 Animator 상태 해시 맵
    private readonly Dictionary<E_ActionType, int> _layerByAction = new Dictionary<E_ActionType, int>(); // 액션별 Animator 레이어 맵
    private readonly Dictionary<E_ActionType, bool> _isOneShotByAction = new Dictionary<E_ActionType, bool>(); // 액션별 1회 재생 여부 맵

    private Coroutine _registerCoroutine; // 리스너 등록 지연 처리 코루틴 핸들
    private Coroutine _oneShotCompleteCoroutine; // 1회 재생 애니메이션 완료 감시 코루틴 핸들
    private Coroutine _idleBreakCoroutine; // Idle Break 랜덤 재생 감시 코루틴 핸들
    private bool _isListenerRegistered; // 현재 ActionController 리스너 등록 여부
    private bool _isPresentationLocked; // 현재 애니메이션 전환 잠금이 활성화된 상태인지 여부
    private E_ActionType _presentationLockOwnerAction; // 현재 애니메이션 전환 잠금을 시작한 액션 타입

    /// <summary>
    /// 매핑 테이블을 해시 형태로 초기화합니다.
    /// </summary>
    private void Awake()
    {
        BuildStateHashMap();
    }

    /// <summary>
    /// 활성화 시 지연 등록 코루틴을 시작해 ActionController에 리스너를 안전하게 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        RestartRegisterCoroutine();
    }

    /// <summary>
    /// 비활성화 시 코루틴을 중지하고 가능한 경우 즉시 리스너를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        StopRunningCoroutine(ref _oneShotCompleteCoroutine);
        StopRunningCoroutine(ref _idleBreakCoroutine);
        TryImmediateUnregisterOnDisable();
    }

    /// <summary>
    /// 오브젝트 파괴 시 코루틴을 정리하고 리스너 해제를 마지막으로 시도합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        StopRunningCoroutine(ref _oneShotCompleteCoroutine);
        StopRunningCoroutine(ref _idleBreakCoroutine);

        if (_isListenerRegistered && _actionController != null)
        {
            _actionController.RemoveListener(this);
            _isListenerRegistered = false;
        }
    }

    /// <summary>
    /// 액션 시작 시 해당 애니메이션 상태를 재생합니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
        if (!TryHandlePresentationLockOnActionStart(runtime.ActionType))
        {
            StopRunningCoroutine(ref _oneShotCompleteCoroutine);
            StopRunningCoroutine(ref _idleBreakCoroutine);
            return;
        }

        PlayActionAnimation(runtime.ActionType);
        TryStartOneShotCompleteMonitor(runtime);
        HandleIdleBreakOnActionStarted(runtime);
        TryActivatePresentationLock(runtime.ActionType);
    }

    /// <summary>
    /// 액션 단계 변경 시 현재 구현에서는 추가 처리를 수행하지 않습니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
    }

    /// <summary>
    /// 액션 완료 시 설정에 따라 기본 표현 액션으로 복귀를 시도합니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
        StopRunningCoroutine(ref _oneShotCompleteCoroutine);
        StopRunningCoroutine(ref _idleBreakCoroutine);

        if (_isPresentationLocked)
        {
            return;
        }

        if (!ShouldPlayDefaultOnEnd(runtime.ActionType))
        {
            return;
        }

        PlayActionAnimation(_defaultPresentationAction);
    }

    /// <summary>
    /// 액션 취소 시 설정에 따라 기본 표현 액션으로 복귀를 시도합니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
        StopRunningCoroutine(ref _oneShotCompleteCoroutine);
        StopRunningCoroutine(ref _idleBreakCoroutine);

        if (_isPresentationLocked)
        {
            return;
        }

        if (!ShouldPlayDefaultOnEnd(runtime.ActionType))
        {
            return;
        }

        PlayActionAnimation(_defaultPresentationAction);
    }

    /// <summary>
    /// 액션 시작 시 애니메이션 전환 잠금 상태를 점검하고 전환 허용 여부를 반환합니다.
    /// </summary>
    private bool TryHandlePresentationLockOnActionStart(E_ActionType startedActionType)
    {
        if (!_enablePresentationLock)
        {
            return true;
        }

        if (!_isPresentationLocked)
        {
            return true;
        }

        if (IsPresentationLockReleaseAction(startedActionType))
        {
            ReleasePresentationLock(startedActionType);
            return true;
        }

        Debug.LogWarning($"[ActionAnimationPresenter] Presentation lock denied transition: owner={_presentationLockOwnerAction}, requested={startedActionType}");
        return false;
    }

    /// <summary>
    /// 시작된 액션이 잠금 트리거 목록에 포함되면 애니메이션 전환 잠금을 활성화합니다.
    /// </summary>
    private void TryActivatePresentationLock(E_ActionType startedActionType)
    {
        if (!_enablePresentationLock)
        {
            return;
        }

        if (!IsPresentationLockTriggerAction(startedActionType))
        {
            return;
        }

        _isPresentationLocked = true;
        _presentationLockOwnerAction = startedActionType;
    }

    /// <summary>
    /// 잠금 해제 허용 액션인지 판정합니다.
    /// </summary>
    private bool IsPresentationLockReleaseAction(E_ActionType actionType)
    {
        for (int i = 0; i < _presentationLockReleaseActions.Length; i++)
        {
            if (_presentationLockReleaseActions[i] == actionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 잠금 시작 트리거 액션인지 판정합니다.
    /// </summary>
    private bool IsPresentationLockTriggerAction(E_ActionType actionType)
    {
        for (int i = 0; i < _presentationLockTriggerActions.Length; i++)
        {
            if (_presentationLockTriggerActions[i] == actionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 잠금 해제 허용 액션으로 전환할 때 잠금 상태를 해제합니다.
    /// </summary>
    private void ReleasePresentationLock(E_ActionType releaseActionType)
    {
        _isPresentationLocked = false;
        _presentationLockOwnerAction = E_ActionType.None;
        Debug.Log($"[ActionAnimationPresenter] Presentation lock released by {releaseActionType}.");
    }



    /// <summary>
    /// 액션 시작 시 Idle Break 감시를 시작하거나 중지합니다.
    /// </summary>
    private void HandleIdleBreakOnActionStarted(ActionRuntime runtime)
    {
        StopRunningCoroutine(ref _idleBreakCoroutine);

        if (!_enableIdleBreak)
        {
            return;
        }

        if (_isPresentationLocked)
        {
            return;
        }

        if (runtime.ActionType != _idleBreakBaseAction)
        {
            return;
        }

        if (_idleBreakActions == null || _idleBreakActions.Length == 0)
        {
            Debug.LogWarning("[ActionAnimationPresenter] Idle Break is enabled but _idleBreakActions is empty.");
            return;
        }

        _idleBreakCoroutine = StartCoroutine(IdleBreakRoutine(runtime.ExecutionId));
    }

    /// <summary>
    /// 기준 액션이 유지되는 동안 랜덤 지연 후 Idle Break 애니메이션을 재생합니다.
    /// </summary>
    private IEnumerator IdleBreakRoutine(int executionId)
    {
        while (_actionController != null)
        {
            ActionRuntime runtime = _actionController.Runtime; // Idle Break 재생 가능 여부를 판정할 현재 액션 런타임
            if (!runtime.IsRunning || runtime.ExecutionId != executionId || runtime.ActionType != _idleBreakBaseAction)
            {
                _idleBreakCoroutine = null;
                yield break;
            }

            float waitSeconds = GetIdleBreakDelaySeconds(); // 다음 Idle Break까지 대기할 랜덤 지연 시간
            yield return new WaitForSeconds(waitSeconds);

            runtime = _actionController.Runtime;
            if (!runtime.IsRunning || runtime.ExecutionId != executionId || runtime.ActionType != _idleBreakBaseAction)
            {
                _idleBreakCoroutine = null;
                yield break;
            }

            E_ActionType selectedBreakAction = SelectIdleBreakAction(); // 이번 사이클에 재생할 Idle Break 액션
            if (!TryPlayMappedAnimation(selectedBreakAction))
            {
                Debug.LogWarning($"[ActionAnimationPresenter] Missing Idle Break mapping for {selectedBreakAction}.");
                continue;
            }

            yield return WaitUntilCurrentStateEnds(selectedBreakAction);

            runtime = _actionController.Runtime;
            if (runtime.IsRunning && runtime.ExecutionId == executionId && runtime.ActionType == _idleBreakBaseAction)
            {
                TryPlayMappedAnimation(_idleBreakBaseAction);
            }
        }

        _idleBreakCoroutine = null;
    }

    /// <summary>
    /// Idle Break 재생까지의 랜덤 대기 시간을 계산합니다.
    /// </summary>
    private float GetIdleBreakDelaySeconds()
    {
        float safeMinDelay = Mathf.Max(0.1f, _idleBreakMinDelaySeconds); // Idle Break 최소 대기 시간의 안전 보정 값
        float safeMaxDelay = Mathf.Max(safeMinDelay, _idleBreakMaxDelaySeconds); // Idle Break 최대 대기 시간의 안전 보정 값
        return Random.Range(safeMinDelay, safeMaxDelay);
    }

    /// <summary>
    /// Idle Break 후보 배열에서 랜덤 액션을 선택합니다.
    /// </summary>
    private E_ActionType SelectIdleBreakAction()
    {
        int randomIndex = Random.Range(0, _idleBreakActions.Length); // Idle Break 후보 배열에서 선택된 랜덤 인덱스
        return _idleBreakActions[randomIndex];
    }

    /// <summary>
    /// 지정한 액션의 매핑이 있으면 Animator 상태를 즉시 재생합니다.
    /// </summary>
    private bool TryPlayMappedAnimation(E_ActionType actionType)
    {
        if (_animator == null)
        {
            return false;
        }

        if (!_stateHashByAction.TryGetValue(actionType, out int stateHash) || !_layerByAction.TryGetValue(actionType, out int layerIndex))
        {
            return false;
        }

        _animator.Play(stateHash, layerIndex, 0f);
        return true;
    }

    /// <summary>
    /// 현재 재생된 액션 상태가 끝날 때까지 normalizedTime을 감시합니다.
    /// </summary>
    private IEnumerator WaitUntilCurrentStateEnds(E_ActionType actionType)
    {
        if (!_stateHashByAction.TryGetValue(actionType, out int stateHash) || !_layerByAction.TryGetValue(actionType, out int layerIndex))
        {
            yield break;
        }

        bool isOneShot = _isOneShotByAction.TryGetValue(actionType, out bool oneShotValue) && oneShotValue; // 액션이 단발 재생인지 여부
        if (!isOneShot)
        {
            yield break;
        }

        while (_animator != null)
        {
            if (_animator.IsInTransition(layerIndex))
            {
                yield return null;
                continue;
            }

            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex); // Idle Break 감시 대상 레이어의 현재 상태 정보
            if (stateInfo.shortNameHash == stateHash && stateInfo.normalizedTime >= 1f)
            {
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// 액션 종료 시 기본 표현 액션을 재생할지 판정합니다.
    /// </summary>
    private bool ShouldPlayDefaultOnEnd(E_ActionType endedActionType)
    {
        if (!_playDefaultOnActionEnd)
        {
            return false;
        }

        for (int i = 0; i < _skipDefaultOnEndActions.Length; i++)
        {
            if (_skipDefaultOnEndActions[i] == endedActionType)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 액션-상태 매핑 테이블을 해시 및 레이어 딕셔너리로 구성합니다.
    /// </summary>
    private void BuildStateHashMap()
    {
        _stateHashByAction.Clear();
        _layerByAction.Clear();
        _isOneShotByAction.Clear();

        if (_stateMapProfile == null)
        {
            Debug.LogWarning($"[ActionAnimationPresenter] AnimationStateMapProfile is not assigned on {name}.");
            return;
        }

        AnimationStateMapData[] profileMaps = _stateMapProfile.StateMaps; // 현재 프리젠터가 사용할 상태 매핑 프로필의 원본 배열
        for (int i = 0; i < profileMaps.Length; i++)
        {
            AnimationStateMapData map = profileMaps[i];

            if (string.IsNullOrWhiteSpace(map.StateName))
            {
                Debug.LogWarning($"[ActionAnimationPresenter] Empty state name for {map.ActionType}.");
                continue;
            }

            if (_stateHashByAction.ContainsKey(map.ActionType))
            {
                Debug.LogWarning($"[ActionAnimationPresenter] Duplicate state mapping for {map.ActionType}. Last one wins.");
            }

            _stateHashByAction[map.ActionType] = Animator.StringToHash(map.StateName);
            _layerByAction[map.ActionType] = map.LayerIndex;
            _isOneShotByAction[map.ActionType] = map.IsOneShot;
        }
    }

    /// <summary>
    /// 시작된 액션이 1회 재생 애니메이션이면 완료 감시 코루틴을 시작합니다.
    /// </summary>
    private void TryStartOneShotCompleteMonitor(ActionRuntime runtime)
    {
        StopRunningCoroutine(ref _oneShotCompleteCoroutine);

        if (!_isOneShotByAction.TryGetValue(runtime.ActionType, out bool isOneShot) || !isOneShot)
        {
            return;
        }

        if (_actionController == null)
        {
            Debug.LogWarning("[ActionAnimationPresenter] Cannot monitor one-shot completion: ActionController is null.");
            return;
        }

        if (!_stateHashByAction.TryGetValue(runtime.ActionType, out int stateHash) || !_layerByAction.TryGetValue(runtime.ActionType, out int layerIndex))
        {
            Debug.LogWarning($"[ActionAnimationPresenter] Cannot monitor one-shot completion: missing map for {runtime.ActionType}.");
            return;
        }

        _oneShotCompleteCoroutine = StartCoroutine(OneShotCompleteMonitorRoutine(runtime.ExecutionId, runtime.ActionType, stateHash, layerIndex));
    }

    /// <summary>
    /// 1회 재생 애니메이션의 normalizedTime을 감시해 종료 시 액션 완료를 요청합니다.
    /// </summary>
    private IEnumerator OneShotCompleteMonitorRoutine(int executionId, E_ActionType actionType, int stateHash, int layerIndex)
    {
        while (_actionController != null)
        {
            ActionRuntime runtime = _actionController.Runtime;
            if (!runtime.IsRunning)
            {
                _oneShotCompleteCoroutine = null;
                yield break;
            }

            if (runtime.ExecutionId != executionId)
            {
                _oneShotCompleteCoroutine = null;
                yield break;
            }

            if (runtime.ActionType != actionType)
            {
                _oneShotCompleteCoroutine = null;
                yield break;
            }

            if (_animator != null && !_animator.IsInTransition(layerIndex))
            {
                AnimatorStateInfo currentStateInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex); // 현재 레이어 상태 정보
                if (currentStateInfo.shortNameHash == stateHash && currentStateInfo.normalizedTime >= 1f)
                {
                    _actionController.CompleteCurrentAction();
                    _oneShotCompleteCoroutine = null;
                    yield break;
                }
            }

            yield return null;
        }

        _oneShotCompleteCoroutine = null;
    }

    /// <summary>
    /// 주어진 액션에 대응되는 Animator 상태를 재생합니다.
    /// </summary>
    private void PlayActionAnimation(E_ActionType actionType)
    {
        if (_animator == null)
        {
            Debug.LogWarning("[ActionAnimationPresenter] Animator is not assigned.");
            return;
        }

        if (_stateHashByAction.TryGetValue(actionType, out int stateHash) && _layerByAction.TryGetValue(actionType, out int layerIndex))
        {
            _animator.Play(stateHash, layerIndex, 0f);
            return;
        }

        Debug.LogWarning($"[ActionAnimationPresenter] Missing animation mapping for {actionType}. Fallback to {_defaultPresentationAction}.");

        if (_stateHashByAction.TryGetValue(_defaultPresentationAction, out int fallbackHash) && _layerByAction.TryGetValue(_defaultPresentationAction, out int fallbackLayer))
        {
            _animator.Play(fallbackHash, fallbackLayer, 0f);
            return;
        }

        Debug.LogWarning("[ActionAnimationPresenter] Default fallback mapping is also missing.");
    }

    /// <summary>
    /// 리스너 등록 코루틴을 재시작합니다.
    /// </summary>
    private void RestartRegisterCoroutine()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        _registerCoroutine = StartCoroutine(RegisterListenerWithRetryCoroutine());
    }

    /// <summary>
    /// 비활성화 시점에 코루틴 없이 안전하게 리스너 해제를 시도합니다.
    /// </summary>
    private void TryImmediateUnregisterOnDisable()
    {
        if (!_isListenerRegistered)
        {
            return;
        }

        if (TryResolveActionControllerReference())
        {
            _actionController.RemoveListener(this);
            _isListenerRegistered = false;
            return;
        }

        _isListenerRegistered = false;
        Debug.LogWarning($"[ActionAnimationPresenter] OnDisable could not resolve ActionController on {name}. RemoveListener skipped.");
    }

    /// <summary>
    /// ActionController가 준비될 때까지 재시도한 뒤 리스너를 등록합니다.
    /// </summary>
    private IEnumerator RegisterListenerWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값

        if (_maxRetryCount < 1 || _retryInterval <= 0f)
        {
            Debug.LogWarning($"[ActionAnimationPresenter] Invalid retry settings on {name}. Fallback maxRetry={safeMaxRetry}, interval={safeInterval}.");
        }

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            if (TryResolveActionControllerReference())
            {
                if (!_isListenerRegistered)
                {
                    _actionController.AddListener(this);
                    _isListenerRegistered = true;
                }

                _registerCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[ActionAnimationPresenter] ActionController is null on {name}. Delaying AddListener registration.");
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[ActionAnimationPresenter] AddListener registration failed after retries on {name}.");
        _registerCoroutine = null;
    }

    /// <summary>
    /// 현재 오브젝트 기준으로 ActionController 참조를 보정합니다.
    /// </summary>
    private bool TryResolveActionControllerReference()
    {
        if (_actionController != null)
        {
            return true;
        }

        _actionController = GetComponent<ActionController>();
        if (_actionController != null)
        {
            Debug.LogWarning($"[ActionAnimationPresenter] _actionController was null on {name}. Fallback to same GameObject ActionController.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 실행 중인 코루틴을 안전하게 중지하고 참조를 정리합니다.
    /// </summary>
    private void StopRunningCoroutine(ref Coroutine coroutineHandle)
    {
        if (coroutineHandle == null)
        {
            return;
        }

        StopCoroutine(coroutineHandle);
        coroutineHandle = null;
    }
}
