/// <summary>
/// 멀티 세션 생성/참가/종료를 수행하는 백엔드 어댑터 인터페이스입니다.
/// </summary>
public interface IMultiplayerSessionBackend
{
    /// <summary>
    /// Host 세션을 생성하고 Join Code를 발급합니다.
    /// </summary>
    bool TryCreateSession(string hostClientId, int maxPlayerCount, out string joinCode, out string reason);

    /// <summary>
    /// Join Code 기반 참가를 시도합니다.
    /// </summary>
    bool TryJoinSession(string joinCode, string clientId, out string reason);

    /// <summary>
    /// 세션의 Stage 진행 상태를 갱신합니다.
    /// </summary>
    void SetStageInProgress(string joinCode, bool isInProgress);

    /// <summary>
    /// 세션의 현재 참가 인원 수를 조회합니다.
    /// </summary>
    bool TryGetPlayerCount(string joinCode, out int playerCount);

    /// <summary>
    /// Client가 로드 완료/준비 완료 상태임을 세션에 기록합니다.
    /// </summary>
    bool TryMarkClientReady(string joinCode, string clientId, out string reason);

    /// <summary>
    /// Host가 Stage 진입을 시작해도 되는 세션 준비 완료 상태인지 조회합니다.
    /// </summary>
    bool TryGetStageEntryReady(string joinCode, out bool isReady, out string reason);

    /// <summary>
    /// 세션을 종료합니다.
    /// </summary>
    void CloseSession(string joinCode);
}
