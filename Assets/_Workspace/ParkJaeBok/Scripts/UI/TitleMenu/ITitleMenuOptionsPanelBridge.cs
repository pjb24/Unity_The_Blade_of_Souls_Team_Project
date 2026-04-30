/// <summary>
/// 타이틀 옵션 패널 오픈 시점에 OptionSystem과 UI 바인딩을 연결하는 브리지 인터페이스입니다.
/// </summary>
public interface ITitleMenuOptionsPanelBridge
{
    /// <summary>
    /// 옵션 패널 활성화 전에 옵션 로드/바인딩 준비를 수행합니다.
    /// </summary>
    void HandleBeforeOpen();

    /// <summary>
    /// 옵션 패널 활성화 직후 후처리를 수행합니다.
    /// </summary>
    void HandleAfterOpen();
}
