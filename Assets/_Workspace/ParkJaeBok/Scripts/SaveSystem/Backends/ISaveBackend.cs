/// <summary>
/// 저장 매체 구현체가 따라야 할 인터페이스입니다.
/// </summary>
public interface ISaveBackend
{
    /// <summary>
    /// 지정 파일로 스냅샷을 저장합니다.
    /// </summary>
    bool TryWrite(string fileName, string json, bool useAtomicReplace, int backupCount);

    /// <summary>
    /// 지정 파일에서 스냅샷 JSON을 읽어옵니다.
    /// </summary>
    bool TryRead(string fileName, out string json);

    /// <summary>
    /// 저장 루트 경로를 반환합니다.
    /// </summary>
    string GetSaveRootPath();
}
