/// <summary>
/// 고급 기믹 Enemy가 프레임 단위로 추가 규칙을 주입할 때 사용하는 모듈 인터페이스입니다.
/// </summary>
public interface IEnemyGimmickModule
{
    /// <summary>
    /// 현재 문맥 기반으로 기믹 상태를 갱신합니다.
    /// </summary>
    void OnBrainTick(in EnemyBrainContext context);
}
