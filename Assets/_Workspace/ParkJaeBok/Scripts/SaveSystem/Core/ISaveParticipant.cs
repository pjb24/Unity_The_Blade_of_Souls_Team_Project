/// <summary>
/// 저장 시스템에 참여하는 객체가 구현할 인터페이스입니다.
/// </summary>
public interface ISaveParticipant
{
    /// <summary>
    /// participant 고유 식별자를 반환합니다.
    /// </summary>
    string ParticipantId { get; }

    /// <summary>
    /// participant의 페이로드 버전을 반환합니다.
    /// </summary>
    int PayloadVersion { get; }

    /// <summary>
    /// 현재 문맥에 대해 저장 가능한지 여부를 반환합니다.
    /// </summary>
    bool CanSave(in SaveContext context);

    /// <summary>
    /// 현재 상태를 JSON 문자열로 직렬화합니다.
    /// </summary>
    string CaptureAsJson(in SaveContext context);

    /// <summary>
    /// 전달된 JSON 문자열로 상태를 복원합니다.
    /// </summary>
    void RestoreFromJson(string payloadJson, in SaveContext context);
}
