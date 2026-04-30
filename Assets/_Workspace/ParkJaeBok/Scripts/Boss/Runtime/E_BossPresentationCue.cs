/// <summary>
/// 보스 연출 전용 Cue를 식별하며,
/// Authority에서 클라이언트와 호스트로 동기화된다.
/// </summary>
public enum E_BossPresentationCue
{
    None = 0, // 사용하지 않는 기본 값
    PatternStarted = 1, // 패턴 시작 연출
    PatternAttack = 2, // 패턴 공격 연출
    PatternEnded = 3, // 패턴 종료 연출
    InvincibleStarted = 4, // 무적 상태 시작 연출
    InvincibleEnded = 5, // 무적 상태 종료 연출
    WeakPointCreated = 6, // 약점 생성 연출
    WeakPointDestroyed = 7, // 약점 파괴 연출
    GroggyStarted = 8, // 그로기 상태 시작 연출
    GroggyEnded = 9, // 그로기 상태 종료 연출
    Dead = 10, // 사망 연출
}
