/// <summary>
/// View가 렌더링할 Client Join 실패 메시지 표시 데이터를 담는 모델입니다.
/// </summary>
public readonly struct MultiplayerJoinFailurePresentationModel
{
    public readonly string Title; // View 제목 영역에 표시할 문자열입니다.
    public readonly string Body; // View 본문 영역에 표시할 문자열입니다.
    public readonly bool UseTitle; // 제목 영역 표시 여부입니다.
    public readonly bool AutoClose; // 자동 닫힘 사용 여부입니다.
    public readonly float AutoCloseDelaySeconds; // 자동 닫힘 지연 시간(초)입니다.
    public readonly bool UseManualCloseButton; // 수동 닫기 버튼 표시 여부입니다.

    public MultiplayerJoinFailurePresentationModel(
        string title,
        string body,
        bool useTitle,
        bool autoClose,
        float autoCloseDelaySeconds,
        bool useManualCloseButton)
    {
        Title = title;
        Body = body;
        UseTitle = useTitle;
        AutoClose = autoClose;
        AutoCloseDelaySeconds = autoCloseDelaySeconds;
        UseManualCloseButton = useManualCloseButton;
    }
}
