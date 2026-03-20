using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 패턴 시퀀스를 ActionController 액션 요청으로 연결하는 오케스트레이터입니다.
/// </summary>
public class BossPatternController : MonoBehaviour, IActionListener
{
    [System.Serializable]
    private struct BossPatternStep
    {
        public E_ActionType ActionType; // 현재 단계에서 요청할 액션 타입
        public float DelayBeforeRequest; // 단계 요청 전 대기 시간(초)
        public bool WaitForActionComplete; // 단계 요청 후 액션 완료 대기 여부
    }

    [System.Serializable]
    private struct BossPatternDefinition
    {
        public string PatternId; // 패턴 식별 문자열
        public bool Loop; // 패턴 반복 여부
        public BossPatternStep[] Steps; // 패턴 단계 목록
    }

    [SerializeField] private ActionController _actionController; // 패턴 요청을 전달할 액션 컨트롤러
    [SerializeField] private BossPatternDefinition[] _patterns = new BossPatternDefinition[0]; // 보스 패턴 정의 목록
    [SerializeField] private bool _autoStartPattern = true; // 시작 시 기본 패턴 자동 실행 여부
    [SerializeField] private string _defaultPatternId = "Default"; // 자동 시작할 기본 패턴 ID
    [SerializeField] private E_ActionType[] _patternInterruptActions = new E_ActionType[] { E_ActionType.Hit, E_ActionType.Break, E_ActionType.Die }; // 패턴을 즉시 중단시킬 인터럽트 액션 목록
    [SerializeField] private bool _restartDefaultPatternAfterInterrupt = true; // 인터럽트 처리 후 기본 패턴 재시작 여부
    [SerializeField] private string _interruptRecoveryPatternId = string.Empty; // 인터럽트 직후 우선 실행할 회복 패턴 ID(비어있으면 미사용)

    private Coroutine _patternCoroutine; // 현재 실행 중인 패턴 코루틴 핸들
    private bool _isWaitingForStepComplete; // 현재 단계 완료 대기 상태 여부
    private E_ActionType _waitingActionType; // 완료 대기 중인 액션 타입
    private string _runningPatternId; // 현재 실행 중인 패턴 ID
    private bool _isHandlingInterrupt; // 인터럽트 처리 루프 재진입 방지 플래그

    /// <summary>
    /// 활성화 시 액션 리스너를 등록하고 기본 패턴 실행을 시도합니다.
    /// </summary>
    private void OnEnable()
    {
        RegisterListener();

        if (_autoStartPattern)
        {
            StartPattern(_defaultPatternId);
        }
    }

    /// <summary>
    /// 비활성화 시 액션 리스너를 해제하고 실행 중 패턴을 중지합니다.
    /// </summary>
    private void OnDisable()
    {
        StopPattern();
        UnregisterListener();
    }

    /// <summary>
    /// 지정한 패턴 ID를 찾아 패턴 실행을 시작합니다.
    /// </summary>
    public void StartPattern(string patternId)
    {
        int patternIndex = FindPatternIndex(patternId);
        if (patternIndex < 0)
        {
            Debug.LogWarning($"[BossPatternController] Pattern not found: {patternId}");
            return;
        }

        StopPattern();
        _runningPatternId = patternId;
        _patternCoroutine = StartCoroutine(RunPatternRoutine(_patterns[patternIndex]));
    }

    /// <summary>
    /// 실행 중인 패턴을 중지하고 대기 상태를 초기화합니다.
    /// </summary>
    public void StopPattern()
    {
        if (_patternCoroutine != null)
        {
            StopCoroutine(_patternCoroutine);
            _patternCoroutine = null;
        }

        _isWaitingForStepComplete = false;
        _waitingActionType = E_ActionType.None;
        _runningPatternId = string.Empty;
    }

    /// <summary>
    /// 액션 시작 콜백에서 현재 구현은 별도 처리를 수행하지 않습니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
        if (!IsPatternRunning())
        {
            return;
        }

        if (!IsInterruptAction(runtime.ActionType))
        {
            return;
        }

        HandlePatternInterrupted(runtime.ActionType, "OnActionStarted interrupt action");
    }

    /// <summary>
    /// 액션 단계 변경 콜백에서 현재 구현은 별도 처리를 수행하지 않습니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
        if (!IsPatternRunning())
        {
            return;
        }

        if (currentPhase != E_ActionPhase.Cancel)
        {
            return;
        }

        if (!IsInterruptAction(runtime.ActionType))
        {
            return;
        }

        HandlePatternInterrupted(runtime.ActionType, "OnActionPhaseChanged cancel interrupt action");
    }

    /// <summary>
    /// 액션 완료 콜백에서 패턴 단계 대기 조건을 해제합니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
        if (!_isWaitingForStepComplete)
        {
            return;
        }

        if (runtime.ActionType != _waitingActionType)
        {
            return;
        }

        _isWaitingForStepComplete = false;
    }

    /// <summary>
    /// 액션 취소 콜백에서 패턴 단계 대기 조건을 해제합니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
        if (!_isWaitingForStepComplete)
        {
            return;
        }

        if (runtime.ActionType != _waitingActionType)
        {
            return;
        }

        Debug.LogWarning($"[BossPatternController] Waiting action cancelled: {runtime.ActionType}, reason={reason}");
        _isWaitingForStepComplete = false;
    }

    /// <summary>
    /// 선택된 패턴 정의를 단계별로 실행합니다.
    /// </summary>
    private IEnumerator RunPatternRoutine(BossPatternDefinition pattern)
    {
        if (_actionController == null)
        {
            Debug.LogWarning("[BossPatternController] ActionController is not assigned.");
            _patternCoroutine = null;
            yield break;
        }

        if (pattern.Steps == null || pattern.Steps.Length == 0)
        {
            Debug.LogWarning($"[BossPatternController] Pattern has no steps: {pattern.PatternId}");
            _patternCoroutine = null;
            yield break;
        }

        do
        {
            for (int i = 0; i < pattern.Steps.Length; i++)
            {
                BossPatternStep step = pattern.Steps[i];

                if (step.DelayBeforeRequest > 0f)
                {
                    yield return new WaitForSeconds(step.DelayBeforeRequest);
                }

                bool requestAccepted = _actionController.RequestAction(step.ActionType); // 패턴 단계 액션 요청 성공 여부
                if (!requestAccepted)
                {
                    Debug.LogWarning($"[BossPatternController] Step request denied: {step.ActionType} in pattern {pattern.PatternId}");
                    continue;
                }

                if (!step.WaitForActionComplete)
                {
                    continue;
                }

                _isWaitingForStepComplete = true;
                _waitingActionType = step.ActionType;

                while (_isWaitingForStepComplete)
                {
                    yield return null;
                }
            }
        }
        while (pattern.Loop);

        _patternCoroutine = null;
        _runningPatternId = string.Empty;
    }

    /// <summary>
    /// 현재 보스 패턴이 실행 중인지 반환합니다.
    /// </summary>
    private bool IsPatternRunning()
    {
        return _patternCoroutine != null;
    }

    /// <summary>
    /// 지정 액션이 패턴 중단 대상 인터럽트 액션인지 판정합니다.
    /// </summary>
    private bool IsInterruptAction(E_ActionType actionType)
    {
        for (int i = 0; i < _patternInterruptActions.Length; i++)
        {
            if (_patternInterruptActions[i] == actionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 보스 패턴 인터럽트 발생 시 패턴 중단 및 회복/재시작 흐름을 처리합니다.
    /// </summary>
    private void HandlePatternInterrupted(E_ActionType interruptActionType, string reason)
    {
        if (_isHandlingInterrupt)
        {
            return;
        }

        _isHandlingInterrupt = true;

        string interruptedPatternId = _runningPatternId; // 로그 출력을 위한 중단 패턴 ID
        StopPattern();

        Debug.LogWarning($"[BossPatternController] Pattern interrupted by {interruptActionType}. reason={reason}, interruptedPattern={interruptedPatternId}");

        if (!string.IsNullOrWhiteSpace(_interruptRecoveryPatternId))
        {
            StartPattern(_interruptRecoveryPatternId);
            _isHandlingInterrupt = false;
            return;
        }

        if (_restartDefaultPatternAfterInterrupt)
        {
            StartPattern(_defaultPatternId);
        }

        _isHandlingInterrupt = false;
    }

    /// <summary>
    /// 패턴 목록에서 지정 패턴 ID의 인덱스를 조회합니다.
    /// </summary>
    private int FindPatternIndex(string patternId)
    {
        for (int i = 0; i < _patterns.Length; i++)
        {
            if (_patterns[i].PatternId == patternId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 액션 컨트롤러 리스너 등록을 수행합니다.
    /// </summary>
    private void RegisterListener()
    {
        if (_actionController == null)
        {
            Debug.LogWarning("[BossPatternController] ActionController is not assigned.");
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
}
