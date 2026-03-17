using UnityEngine;

/// <summary>
/// 현재 액션 실행의 런타임 상태를 보관하는 데이터 클래스입니다.
/// </summary>
public class ActionRuntime
{
    private E_ActionType _actionType; // 현재 실행 중인 액션 타입
    private E_ActionPhase _phase; // 현재 액션 단계
    private E_ActionRunState _runState; // 현재 액션 실행 상태
    private int _executionId; // 액션 실행 식별자(코루틴/마커 동기화용)
    private float _startedTime; // 액션 시작 시각
    private string _cancelReason; // 마지막 취소 사유

    /// <summary>
    /// 현재 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType ActionType => _actionType;

    /// <summary>
    /// 현재 액션 단계를 반환합니다.
    /// </summary>
    public E_ActionPhase Phase => _phase;

    /// <summary>
    /// 현재 실행 상태를 반환합니다.
    /// </summary>
    public E_ActionRunState RunState => _runState;

    /// <summary>
    /// 현재 실행 식별자를 반환합니다.
    /// </summary>
    public int ExecutionId => _executionId;

    /// <summary>
    /// 액션 시작 시각을 반환합니다.
    /// </summary>
    public float StartedTime => _startedTime;

    /// <summary>
    /// 마지막 취소 사유를 반환합니다.
    /// </summary>
    public string CancelReason => _cancelReason;

    /// <summary>
    /// 액션이 실행 중인지 반환합니다.
    /// </summary>
    public bool IsRunning => _runState == E_ActionRunState.Running;

    /// <summary>
    /// 새 액션 실행을 시작합니다.
    /// </summary>
    public void Begin(E_ActionType actionType)
    {
        _actionType = actionType;
        _phase = E_ActionPhase.Start;
        _runState = E_ActionRunState.Running;
        _executionId++;
        _startedTime = Time.time;
        _cancelReason = string.Empty;
    }

    /// <summary>
    /// 액션 단계를 변경합니다.
    /// </summary>
    public E_ActionPhase SetPhase(E_ActionPhase phase)
    {
        E_ActionPhase previousPhase = _phase;
        _phase = phase;
        return previousPhase;
    }

    /// <summary>
    /// 현재 액션을 완료 상태로 종료합니다.
    /// </summary>
    public void Complete()
    {
        _phase = E_ActionPhase.Complete;
        _runState = E_ActionRunState.Completed;
        _cancelReason = string.Empty;
    }

    /// <summary>
    /// 현재 액션을 취소 상태로 종료합니다.
    /// </summary>
    public void Cancel(string reason)
    {
        _phase = E_ActionPhase.Cancel;
        _runState = E_ActionRunState.Cancelled;
        _cancelReason = string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason;
    }
}
