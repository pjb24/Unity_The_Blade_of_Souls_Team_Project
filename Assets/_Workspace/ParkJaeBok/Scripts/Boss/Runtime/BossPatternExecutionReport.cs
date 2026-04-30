/// <summary>
/// 보스 패턴 실행이 완료, 취소, 실패될 때 전달되는 불변(Immutable) 리포트 데이터이다.
/// </summary>
public readonly struct BossPatternExecutionReport
{
    public readonly BossPatternBase Pattern; // 이 리포트를 생성한 패턴 인스턴스
    public readonly E_BossPatternType PatternType; // 리포트를 생성한 패턴에 할당된 패턴 타입
    public readonly int ExecutionId; // 실행 시작 시 생성된 패턴 실행 ID
    public readonly string Reason; // 완료, 취소 또는 실패 사유 (선택적)
    public readonly bool HasAppliedEffect; // 리포트 전에 공격, 소환, 이동, 상태 변경 등의 효과가 발생했는지 여부

    /// <summary>
    /// 보스 패턴 실행 리포트를 생성한다.
    /// </summary>
    public BossPatternExecutionReport(BossPatternBase pattern, E_BossPatternType patternType, int executionId, string reason, bool hasAppliedEffect)
    {
        Pattern = pattern;
        PatternType = patternType;
        ExecutionId = executionId;
        Reason = reason;
        HasAppliedEffect = hasAppliedEffect;
    }
}
