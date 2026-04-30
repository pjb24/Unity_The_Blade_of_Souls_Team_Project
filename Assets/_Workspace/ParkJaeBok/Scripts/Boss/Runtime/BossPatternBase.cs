using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 최종 보스 상태를 결정하지 않고 라이프사이클 결과만 보고하는 보스 패턴의 기본 클래스이다.
/// </summary>
public abstract class BossPatternBase : NetworkBehaviour
{
    [Header("패턴 식별")]
    [Tooltip("패턴 실행 라이프사이클 콜백에서 BossController로 전달되는 패턴 타입")]
    [SerializeField] private E_BossPatternType _patternType = E_BossPatternType.None; // 실행 리포트에 사용되는 패턴 타입

    [Tooltip("이 패턴이 경고 로그를 출력할 때 사용하는 선택적 로그 라벨")]
    [SerializeField] private string _patternLogLabel; // 경고 로그에서 사용하는 사람이 읽기 쉬운 라벨

    private readonly List<IBossPatternExecutionListener> _listeners = new List<IBossPatternExecutionListener>(); // AddListener를 통해 등록된 라이프사이클 리포트 리스너 목록

    protected bool _hasLoggedFailureInCurrentExecution; // 현재 실행에서 실패 사유를 이미 로그로 출력했는지 여부
    private string _loggedFailureReasonInCurrentExecution; // 현재 실행에서 이미 로그로 출력된 실패 사유
    private int _nextExecutionId; // 이 패턴 인스턴스에서 사용하는 증가형 실행 ID 소스
    private int _currentExecutionId; // 현재 실행 중인 실행 ID
    private bool _isExecuting; // 현재 이 패턴이 실행 중인지 여부
    private bool _hasAppliedEffectInCurrentExecution; // 현재 실행에서 실제 게임플레이 효과가 발생했는지 여부 (취소 시 UsageLimit 판단에 사용)

    /// <summary>
    /// 리포트에 사용되는 패턴 타입을 반환한다.
    /// </summary>
    public E_BossPatternType PatternType => _patternType;

    /// <summary>
    /// 현재 패턴이 실행 중인지 여부를 반환한다.
    /// </summary>
    public bool IsExecuting => _isExecuting;

    /// <summary>
    /// 현재 실행 중인 실행 ID를 반환한다.
    /// </summary>
    public int CurrentExecutionId => _currentExecutionId;

    /// <summary>
    /// 패턴 라이프사이클 리포트를 수신할 리스너를 등록한다.
    /// </summary>
    public void AddListener(IBossPatternExecutionListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[BossPatternBase] AddListener에 null이 전달됨. pattern={GetPatternLogName()}", this);
            return;
        }

        if (_listeners.Contains(listener))
        {
            Debug.LogWarning($"[BossPatternBase] AddListener에 중복 리스너가 전달됨. pattern={GetPatternLogName()}", this);
            return;
        }

        _listeners.Add(listener);
    }

    /// <summary>
    /// 패턴 라이프사이클 리포트 리스너 등록을 해제한다.
    /// </summary>
    public void RemoveListener(IBossPatternExecutionListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[BossPatternBase] RemoveListener에 null이 전달됨. pattern={GetPatternLogName()}", this);
            return;
        }

        if (!_listeners.Remove(listener))
        {
            Debug.LogWarning($"[BossPatternBase] RemoveListener에서 리스너를 찾지 못함. pattern={GetPatternLogName()}", this);
        }
    }

    /// <summary>
    /// 패턴 실행을 시작하고 실행 단위 실패 로그 상태를 초기화한다.
    /// </summary>
    public bool StartPatternExecution()
    {
        if (_isExecuting)
        {
            LogFailureOnce("AlreadyExecuting");
            return false;
        }

        _nextExecutionId++;
        if (_nextExecutionId <= 0)
        {
            _nextExecutionId = 1;
            Debug.LogWarning($"[BossPatternBase] 실행 ID overflow fallback 적용됨. pattern={GetPatternLogName()}", this);
        }

        _currentExecutionId = _nextExecutionId;
        _isExecuting = true;
        ResetFailureLogState();
        OnPatternExecutionStarted();
        return true;
    }

    /// <summary>
    /// 공통 취소 API를 통해 현재 패턴 실행을 취소하고 예약된 작업을 정리한다.
    /// </summary>
    public void CancelPattern(string reason)
    {
        if (!_isExecuting)
        {
            LogFailureOnce("CancelRequestedWhileNotExecuting");
            return;
        }

        OnPatternExecutionCancelled(reason);
        ClearScheduledPatternWork();
        ReportPatternCancelled(reason);
    }

    /// <summary>
    /// 파생 클래스가 실행 시작 시 동작을 정의할 수 있도록 한다.
    /// </summary>
    protected virtual void OnPatternExecutionStarted()
    {
    }

    /// <summary>
    /// 파생 클래스가 취소 전에 리소스를 정리할 수 있도록 한다.
    /// </summary>
    protected virtual void OnPatternExecutionCancelled(string reason)
    {
    }

    /// <summary>
    /// 이 패턴에서 예약한 코루틴과 Invoke 호출을 정리한다.
    /// </summary>
    private void ClearScheduledPatternWork()
    {
        StopAllCoroutines();
        CancelInvoke();
    }

    /// <summary>
    /// 정상적인 패턴 완료를 리스너에게 보고한다.
    /// </summary>
    protected void ReportPatternCompleted(string reason)
    {
        if (!TryPrepareReport("CompleteRequestedWhileNotExecuting"))
        {
            return;
        }

        BossPatternExecutionReport report = CreateReport(reason);
        _isExecuting = false;
        NotifyCompleted(report);
    }

    /// <summary>
    /// 패턴 취소를 리스너에게 보고한다.
    /// </summary>
    protected void ReportPatternCancelled(string reason)
    {
        if (!TryPrepareReport("CancelRequestedWhileNotExecuting"))
        {
            return;
        }

        BossPatternExecutionReport report = CreateReport(reason);
        _isExecuting = false;
        NotifyCancelled(report);
    }

    /// <summary>
    /// 패턴 실패를 리스너에게 보고하고 실패 사유를 1회 로그로 출력한다.
    /// </summary>
    protected void ReportPatternFailed(string reason)
    {
        if (!TryPrepareReport("FailRequestedWhileNotExecuting"))
        {
            return;
        }

        string safeReason = string.IsNullOrWhiteSpace(reason) ? "UnknownFailure" : reason; // 리포트 및 경고에 사용하는 정규화된 실패 사유
        LogFailureOnce(safeReason);
        BossPatternExecutionReport report = CreateReport(safeReason);
        _isExecuting = false;
        NotifyFailed(report);
    }

    /// <summary>
    /// 현재 실행 동안 동일한 실패 사유를 1회만 로그로 출력한다.
    /// </summary>
    protected void LogFailureOnce(string reason)
    {
        string safeReason = string.IsNullOrWhiteSpace(reason) ? "UnknownFailure" : reason; // 중복 방지를 위한 정규화된 사유
        if (_hasLoggedFailureInCurrentExecution && _loggedFailureReasonInCurrentExecution == safeReason)
        {
            return;
        }

        _hasLoggedFailureInCurrentExecution = true;
        _loggedFailureReasonInCurrentExecution = safeReason;
        Debug.LogWarning($"[BossPatternBase] 패턴 실패. pattern={GetPatternLogName()}, executionId={_currentExecutionId}, reason={safeReason}", this);
    }

    /// <summary>
    /// 현재 패턴 실행에서 실제 게임플레이 효과가 발생했음을 표시한다.
    /// </summary>
    protected void MarkPatternEffectApplied()
    {
        if (!_isExecuting)
        {
            LogFailureOnce("EffectAppliedWhileNotExecuting");
            return;
        }

        _hasAppliedEffectInCurrentExecution = true;
    }

    /// <summary>
    /// 다음 실행에서 동일한 사유를 다시 로그로 출력할 수 있도록 상태를 초기화한다.
    /// </summary>
    private void ResetFailureLogState()
    {
        _hasLoggedFailureInCurrentExecution = false;
        _loggedFailureReasonInCurrentExecution = string.Empty;
        _hasAppliedEffectInCurrentExecution = false;
    }

    /// <summary>
    /// 현재 실행에서 리포트를 보낼 수 있는지 여부를 반환한다.
    /// </summary>
    private bool TryPrepareReport(string notExecutingFailureReason)
    {
        if (_isExecuting)
        {
            return true;
        }

        LogFailureOnce(notExecutingFailureReason);
        return false;
    }

    /// <summary>
    /// 현재 실행에 대한 라이프사이클 리포트를 생성한다.
    /// </summary>
    private BossPatternExecutionReport CreateReport(string reason)
    {
        string safeReason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason; // 리포트에 사용하는 정규화된 사유
        return new BossPatternExecutionReport(this, _patternType, _currentExecutionId, safeReason, _hasAppliedEffectInCurrentExecution);
    }

    /// <summary>
    /// 패턴 완료를 리스너에게 알린다.
    /// </summary>
    private void NotifyCompleted(BossPatternExecutionReport report)
    {
        for (int index = 0; index < _listeners.Count; index++)
        {
            IBossPatternExecutionListener listener = _listeners[index];
            if (listener == null)
            {
                Debug.LogWarning($"[BossPatternBase] 완료 리포트 중 null 리스너 스킵. pattern={GetPatternLogName()}", this);
                continue;
            }

            listener.OnBossPatternCompleted(report);
        }
    }

    /// <summary>
    /// 패턴 취소를 리스너에게 알린다.
    /// </summary>
    private void NotifyCancelled(BossPatternExecutionReport report)
    {
        for (int index = 0; index < _listeners.Count; index++)
        {
            IBossPatternExecutionListener listener = _listeners[index];
            if (listener == null)
            {
                Debug.LogWarning($"[BossPatternBase] 취소 리포트 중 null 리스너 스킵. pattern={GetPatternLogName()}", this);
                continue;
            }

            listener.OnBossPatternCancelled(report);
        }
    }

    /// <summary>
    /// 패턴 실패를 리스너에게 알린다.
    /// </summary>
    private void NotifyFailed(BossPatternExecutionReport report)
    {
        for (int index = 0; index < _listeners.Count; index++)
        {
            IBossPatternExecutionListener listener = _listeners[index];
            if (listener == null)
            {
                Debug.LogWarning($"[BossPatternBase] 실패 리포트 중 null 리스너 스킵. pattern={GetPatternLogName()}", this);
                continue;
            }

            listener.OnBossPatternFailed(report);
        }
    }

    /// <summary>
    /// 이 패턴에서 사용할 안정적인 로그 이름을 반환한다.
    /// </summary>
    private string GetPatternLogName()
    {
        if (!string.IsNullOrWhiteSpace(_patternLogLabel))
        {
            return _patternLogLabel;
        }

        return name;
    }
}
