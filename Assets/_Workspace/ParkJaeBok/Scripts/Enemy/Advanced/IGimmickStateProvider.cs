/// <summary>
/// 기믹 상태를 저장/복원 가능한 JSON으로 제공하는 표준 인터페이스입니다.
/// </summary>
public interface IGimmickStateProvider
{
    /// <summary>
    /// 기믹을 식별하는 고유 ID를 반환합니다.
    /// </summary>
    string GimmickId { get; }

    /// <summary>
    /// 현재 기믹 상태를 JSON 문자열로 직렬화해 반환합니다.
    /// </summary>
    string CaptureStateJson();

    /// <summary>
    /// JSON 문자열에서 기믹 상태를 복원합니다.
    /// </summary>
    void RestoreStateJson(string json);
}
