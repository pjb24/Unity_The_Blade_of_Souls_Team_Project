/// <summary>
/// 런타임 일시정지 상태를 외부 시스템에서 읽기 전용으로 조회하기 위한 인터페이스입니다.
/// </summary>
public interface IPauseStateReader
{
    bool IsPaused { get; }
}
