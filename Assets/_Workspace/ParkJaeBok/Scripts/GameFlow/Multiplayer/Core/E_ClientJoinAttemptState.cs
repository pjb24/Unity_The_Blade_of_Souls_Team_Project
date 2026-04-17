/// <summary>
/// Client Join 시도의 현재 진행 상태를 표현하는 열거형입니다.
/// </summary>
public enum E_ClientJoinAttemptState
{
    Idle = 0,
    Attempting = 1,
    Succeeded = 2,
    Failed = 3
}
