/// <summary>
/// Client Join 실패 원인을 사용자 메시지 정책으로 매핑하기 위한 표준화 열거형입니다.
/// </summary>
public enum E_ClientJoinFailureType
{
    None = 0,
    JoinCodeEmpty = 1,
    SessionNotFound = 2,
    SessionFull = 3,
    StageInProgress = 4,
    InvalidJoinCode = 5,
    NetworkUnavailable = 6,
    ServiceInitializationFailed = 7,
    AuthenticationFailed = 8,
    AdmissionDenied = 9,
    StartClientFailed = 10,
    Unknown = 99
}
