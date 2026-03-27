/// <summary>
/// 타이틀 메뉴에서 확인/경고 대화상자 요청을 처리하는 인터페이스입니다.
/// </summary>
public interface ITitleDialogService
{
    /// <summary>
    /// 새 게임 시작 전 덮어쓰기 확인 결과를 반환합니다.
    /// </summary>
    bool ConfirmStartNewGameWithOverwrite();

    /// <summary>
    /// 게임 종료 확인 결과를 반환합니다.
    /// </summary>
    bool ConfirmQuit();
}
