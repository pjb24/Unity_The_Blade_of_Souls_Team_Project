/// <summary>
/// 타이틀 메뉴에서 세이브 데이터 존재 여부를 조회하는 인터페이스입니다.
/// </summary>
public interface ITitleSaveQueryService
{
    /// <summary>
    /// Continue 버튼 활성화에 사용할 데이터 존재 여부를 반환합니다.
    /// </summary>
    bool HasContinueData();

    /// <summary>
    /// Load Game 버튼 활성화에 사용할 데이터 존재 여부를 반환합니다.
    /// </summary>
    bool HasLoadableData();

    /// <summary>
    /// New Game 덮어쓰기 경고에 사용할 기존 진행 데이터 존재 여부를 반환합니다.
    /// </summary>
    bool HasExistingProgress();
}
