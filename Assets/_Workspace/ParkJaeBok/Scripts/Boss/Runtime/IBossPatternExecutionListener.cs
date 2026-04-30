/// <summary>
/// 명시적인 Listener 등록을 통해 보스 패턴 실행 생명주기 보고를 수신한다.
/// </summary>
public interface IBossPatternExecutionListener
{
    /// <summary>
    /// 보스 패턴이 정상적으로 완료되었을 때 호출된다.
    /// </summary>
    void OnBossPatternCompleted(BossPatternExecutionReport report);

    /// <summary>
    /// 보스 패턴이 취소되었을 때 호출된다.
    /// </summary>
    void OnBossPatternCancelled(BossPatternExecutionReport report);

    /// <summary>
    /// 보스 패턴이 실패했을 때 호출된다.
    /// </summary>
    void OnBossPatternFailed(BossPatternExecutionReport report);
}
