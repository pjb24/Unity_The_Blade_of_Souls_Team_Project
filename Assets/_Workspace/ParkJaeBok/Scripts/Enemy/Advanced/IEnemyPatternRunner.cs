/// <summary>
/// 보스 패턴 시퀀스가 EnemyBrain 기본 흐름을 일시적으로 덮어쓸 때 사용하는 인터페이스입니다.
/// </summary>
public interface IEnemyPatternRunner
{
    /// <summary>
    /// 패턴 로직을 진행하고 Brain 기본 판단을 오버라이드할지 여부를 반환합니다.
    /// </summary>
    bool TickAndShouldOverride(in EnemyBrainContext context);
}
