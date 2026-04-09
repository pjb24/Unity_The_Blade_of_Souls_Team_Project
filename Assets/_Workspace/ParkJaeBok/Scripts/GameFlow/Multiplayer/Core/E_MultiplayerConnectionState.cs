/// <summary>
/// 멀티플레이 세션 연결 상태를 표현하는 열거형입니다.
/// </summary>
public enum E_MultiplayerConnectionState
{
    None = 0,
    Hosting = 1,
    JoinedAsClient = 2,
    Reconnecting = 3,
    Closed = 4
}
