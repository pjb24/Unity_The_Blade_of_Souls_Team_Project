using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Base class for boss patterns that reports lifecycle results without deciding final boss state.
/// </summary>
public abstract class BossPatternBase : NetworkBehaviour
{
    [Header("Pattern Identity")]
    [Tooltip("Pattern type reported to BossController during execution lifecycle callbacks.")]
    [SerializeField] private E_BossPatternType _patternType = E_BossPatternType.None; // Pattern type used in execution reports.

    [Tooltip("Optional log label used when this pattern reports warnings.")]
    [SerializeField] private string _patternLogLabel; // Human-readable label used by warning logs.

    private readonly List<IBossPatternExecutionListener> _listeners = new List<IBossPatternExecutionListener>(); // Lifecycle report listeners registered through AddListener.

    protected bool _hasLoggedFailureInCurrentExecution; // Whether the current execution has already logged its current failure reason.
    private string _loggedFailureReasonInCurrentExecution; // Failure reason already logged in this execution.
    private int _nextExecutionId; // Monotonic execution id source for this pattern instance.
    private int _currentExecutionId; // Execution id currently running.
    private bool _isExecuting; // Whether this pattern is currently executing.

    /// <summary>
    /// Gets the pattern type used in reports.
    /// </summary>
    public E_BossPatternType PatternType => _patternType;

    /// <summary>
    /// Gets whether the pattern is currently executing.
    /// </summary>
    public bool IsExecuting => _isExecuting;

    /// <summary>
    /// Gets the currently running execution id.
    /// </summary>
    public int CurrentExecutionId => _currentExecutionId;

    /// <summary>
    /// Registers a listener that receives pattern lifecycle reports.
    /// </summary>
    public void AddListener(IBossPatternExecutionListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[BossPatternBase] AddListener received null. pattern={GetPatternLogName()}", this);
            return;
        }

        if (_listeners.Contains(listener))
        {
            Debug.LogWarning($"[BossPatternBase] AddListener received duplicate listener. pattern={GetPatternLogName()}", this);
            return;
        }

        _listeners.Add(listener);
    }

    /// <summary>
    /// Unregisters a listener from pattern lifecycle reports.
    /// </summary>
    public void RemoveListener(IBossPatternExecutionListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[BossPatternBase] RemoveListener received null. pattern={GetPatternLogName()}", this);
            return;
        }

        if (!_listeners.Remove(listener))
        {
            Debug.LogWarning($"[BossPatternBase] RemoveListener could not find listener. pattern={GetPatternLogName()}", this);
        }
    }

    /// <summary>
    /// Starts a pattern execution and resets per-execution failure log state.
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
            Debug.LogWarning($"[BossPatternBase] Execution id overflow fallback applied. pattern={GetPatternLogName()}", this);
        }

        _currentExecutionId = _nextExecutionId;
        _isExecuting = true;
        ResetFailureLogState();
        OnPatternExecutionStarted();
        return true;
    }

    /// <summary>
    /// Cancels the current pattern execution through the common cancellation API and clears scheduled work owned by this pattern component.
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
    /// Allows derived patterns to react when execution starts without deciding boss state.
    /// </summary>
    protected virtual void OnPatternExecutionStarted()
    {
    }

    /// <summary>
    /// Allows derived patterns to clean up execution resources before a common cancellation report is sent.
    /// </summary>
    protected virtual void OnPatternExecutionCancelled(string reason)
    {
    }

    /// <summary>
    /// Clears coroutines and Invoke calls scheduled by this pattern without touching spawned combat objects owned by other components.
    /// </summary>
    private void ClearScheduledPatternWork()
    {
        StopAllCoroutines();
        CancelInvoke();
    }

    /// <summary>
    /// Reports normal pattern completion to listeners.
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
    /// Reports pattern cancellation to listeners.
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
    /// Reports pattern failure to listeners and logs the failure reason once per execution.
    /// </summary>
    protected void ReportPatternFailed(string reason)
    {
        if (!TryPrepareReport("FailRequestedWhileNotExecuting"))
        {
            return;
        }

        string safeReason = string.IsNullOrWhiteSpace(reason) ? "UnknownFailure" : reason; // Normalized failure reason used for report and one-time warning.
        LogFailureOnce(safeReason);
        BossPatternExecutionReport report = CreateReport(safeReason);
        _isExecuting = false;
        NotifyFailed(report);
    }

    /// <summary>
    /// Logs a failure reason only once during the current execution.
    /// </summary>
    protected void LogFailureOnce(string reason)
    {
        string safeReason = string.IsNullOrWhiteSpace(reason) ? "UnknownFailure" : reason; // Normalized reason used for duplicate suppression.
        if (_hasLoggedFailureInCurrentExecution && _loggedFailureReasonInCurrentExecution == safeReason)
        {
            return;
        }

        _hasLoggedFailureInCurrentExecution = true;
        _loggedFailureReasonInCurrentExecution = safeReason;
        Debug.LogWarning($"[BossPatternBase] Pattern failure. pattern={GetPatternLogName()}, executionId={_currentExecutionId}, reason={safeReason}", this);
    }

    /// <summary>
    /// Clears per-execution failure log state so the next execution can log the same reason again.
    /// </summary>
    private void ResetFailureLogState()
    {
        _hasLoggedFailureInCurrentExecution = false;
        _loggedFailureReasonInCurrentExecution = string.Empty;
    }

    /// <summary>
    /// Returns whether a report can be sent for the current execution.
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
    /// Creates a lifecycle report for the current execution.
    /// </summary>
    private BossPatternExecutionReport CreateReport(string reason)
    {
        string safeReason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason; // Normalized report reason.
        return new BossPatternExecutionReport(this, _patternType, _currentExecutionId, safeReason);
    }

    /// <summary>
    /// Notifies listeners that the pattern completed.
    /// </summary>
    private void NotifyCompleted(BossPatternExecutionReport report)
    {
        for (int index = 0; index < _listeners.Count; index++)
        {
            IBossPatternExecutionListener listener = _listeners[index];
            if (listener == null)
            {
                Debug.LogWarning($"[BossPatternBase] Null listener skipped during completion report. pattern={GetPatternLogName()}", this);
                continue;
            }

            listener.OnBossPatternCompleted(report);
        }
    }

    /// <summary>
    /// Notifies listeners that the pattern was cancelled.
    /// </summary>
    private void NotifyCancelled(BossPatternExecutionReport report)
    {
        for (int index = 0; index < _listeners.Count; index++)
        {
            IBossPatternExecutionListener listener = _listeners[index];
            if (listener == null)
            {
                Debug.LogWarning($"[BossPatternBase] Null listener skipped during cancellation report. pattern={GetPatternLogName()}", this);
                continue;
            }

            listener.OnBossPatternCancelled(report);
        }
    }

    /// <summary>
    /// Notifies listeners that the pattern failed.
    /// </summary>
    private void NotifyFailed(BossPatternExecutionReport report)
    {
        for (int index = 0; index < _listeners.Count; index++)
        {
            IBossPatternExecutionListener listener = _listeners[index];
            if (listener == null)
            {
                Debug.LogWarning($"[BossPatternBase] Null listener skipped during failure report. pattern={GetPatternLogName()}", this);
                continue;
            }

            listener.OnBossPatternFailed(report);
        }
    }

    /// <summary>
    /// Resolves a stable log name for this pattern.
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
