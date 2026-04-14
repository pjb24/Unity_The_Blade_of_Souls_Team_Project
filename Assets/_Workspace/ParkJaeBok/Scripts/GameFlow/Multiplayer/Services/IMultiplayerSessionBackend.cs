using System.Threading.Tasks;

/// <summary>
/// 멀티 세션 생성/참가/종료를 수행하는 백엔드 어댑터 인터페이스입니다.
/// </summary>
public interface IMultiplayerSessionBackend
{
    /// <summary>
    /// Host 세션을 생성하고 Join Code를 발급합니다.
    /// </summary>
    Task<SessionCreateResult> CreateSessionAsync(string hostClientId, int maxPlayerCount);

    /// <summary>
    /// Join Code 기반 참가를 시도합니다.
    /// </summary>
    Task<SessionOperationResult> JoinSessionAsync(string joinCode, string clientId);

    /// <summary>
    /// 세션의 Stage 진행 상태를 갱신합니다.
    /// </summary>
    Task<SessionOperationResult> SetStageInProgressAsync(string joinCode, bool isInProgress);

    /// <summary>
    /// 세션의 현재 참가 인원 수를 조회합니다.
    /// </summary>
    Task<PlayerCountResult> GetPlayerCountAsync(string joinCode);

    /// <summary>
    /// Client가 로드 완료/준비 완료 상태임을 세션에 기록합니다.
    /// </summary>
    Task<SessionOperationResult> MarkClientReadyAsync(string joinCode, string clientId);

    /// <summary>
    /// Host가 Stage 진입을 시작해도 되는 세션 준비 완료 상태인지 조회합니다.
    /// </summary>
    Task<StageEntryReadyResult> GetStageEntryReadyAsync(string joinCode);

    /// <summary>
    /// 세션을 종료합니다.
    /// </summary>
    Task<SessionOperationResult> CloseSessionAsync(string joinCode);
}

/// <summary>
/// 세션 생성 결과(성공 여부/JoinCode/사유 코드)를 반환하는 DTO입니다.
/// </summary>
public readonly struct SessionCreateResult
{
    public readonly bool IsSuccess; // 세션 생성 성공 여부입니다.
    public readonly string JoinCode; // 성공 시 발급된 Join Code입니다.
    public readonly string Reason; // 실패 또는 부가 상태를 설명하는 코드 문자열입니다.

    public SessionCreateResult(bool isSuccess, string joinCode, string reason)
    {
        IsSuccess = isSuccess;
        JoinCode = joinCode;
        Reason = reason;
    }
}

/// <summary>
/// 일반 세션 동작의 성공 여부/사유 코드를 반환하는 DTO입니다.
/// </summary>
public readonly struct SessionOperationResult
{
    public readonly bool IsSuccess; // 동작 성공 여부입니다.
    public readonly string Reason; // 실패 또는 부가 상태를 설명하는 코드 문자열입니다.

    public SessionOperationResult(bool isSuccess, string reason)
    {
        IsSuccess = isSuccess;
        Reason = reason;
    }
}

/// <summary>
/// 세션 인원 수 조회 결과를 담는 DTO입니다.
/// </summary>
public readonly struct PlayerCountResult
{
    public readonly bool IsSuccess; // 조회 성공 여부입니다.
    public readonly int PlayerCount; // 성공 시 현재 참가 인원 수입니다.
    public readonly string Reason; // 실패 또는 부가 상태를 설명하는 코드 문자열입니다.

    public PlayerCountResult(bool isSuccess, int playerCount, string reason)
    {
        IsSuccess = isSuccess;
        PlayerCount = playerCount;
        Reason = reason;
    }
}

/// <summary>
/// Stage 진입 준비 상태 조회 결과를 담는 DTO입니다.
/// </summary>
public readonly struct StageEntryReadyResult
{
    public readonly bool IsSuccess; // 조회 호출 자체의 성공 여부입니다.
    public readonly bool IsReady; // Stage 진입 가능 여부입니다.
    public readonly string Reason; // 실패 또는 미준비 사유 코드입니다.

    public StageEntryReadyResult(bool isSuccess, bool isReady, string reason)
    {
        IsSuccess = isSuccess;
        IsReady = isReady;
        Reason = reason;
    }
}
