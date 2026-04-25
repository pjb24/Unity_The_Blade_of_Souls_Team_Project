/// <summary>
/// AttackExecutor의 공격 판정 결과를 수신하는 리스너 인터페이스입니다.
/// </summary>
public interface IAttackExecutionListener
{
    /// <summary>
    /// 공격 판정 결과를 전달받아 후속 시스템(Buff 게이지 등)에서 사용합니다.
    /// </summary>
    void OnAttackExecuted(in AttackExecutionReport report);
}

/// <summary>
/// 공격 실행 1건의 결과를 전달하기 위한 데이터 구조체입니다.
/// </summary>
public readonly struct AttackExecutionReport
{
    public readonly E_ActionType ActionType; // 공격 실행에 대응하는 액션 타입입니다.
    public readonly int ExecutionId; // ActionController 실행 식별자입니다.
    public readonly HitReceiver Target; // 판정 대상 HitReceiver 참조입니다.
    public readonly HitResult Result; // 대상에게 적용된 Hit 결과입니다.

    /// <summary>
    /// 보고서 구조체를 생성합니다.
    /// </summary>
    public AttackExecutionReport(E_ActionType actionType, int executionId, HitReceiver target, in HitResult result)
    {
        ActionType = actionType;
        ExecutionId = executionId;
        Target = target;
        Result = result;
    }
}
