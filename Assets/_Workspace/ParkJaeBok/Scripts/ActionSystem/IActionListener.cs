/// <summary>
/// 액션 상태 변경을 수신하는 리스너 인터페이스입니다.
/// </summary>
public interface IActionListener
{
    /// <summary>
    /// 액션 시작 시 호출됩니다.
    /// </summary>
    void OnActionStarted(ActionRuntime runtime);

    /// <summary>
    /// 액션 단계 변경 시 호출됩니다.
    /// </summary>
    void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase);

    /// <summary>
    /// 액션 완료 시 호출됩니다.
    /// </summary>
    void OnActionCompleted(ActionRuntime runtime);

    /// <summary>
    /// 액션 취소 시 호출됩니다.
    /// </summary>
    void OnActionCancelled(ActionRuntime runtime, string reason);
}
