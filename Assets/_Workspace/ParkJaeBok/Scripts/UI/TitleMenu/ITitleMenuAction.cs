/// <summary>
/// 타이틀 메뉴 버튼 동작이 구현해야 하는 공통 인터페이스입니다.
/// </summary>
public interface ITitleMenuAction
{
    /// <summary>
    /// 현재 문맥에서 액션 실행 가능 여부를 반환합니다.
    /// </summary>
    bool CanExecute(TitleMenuActionContext context);

    /// <summary>
    /// 현재 문맥에서 액션 실행을 시도합니다.
    /// </summary>
    bool Execute(TitleMenuActionContext context);
}
