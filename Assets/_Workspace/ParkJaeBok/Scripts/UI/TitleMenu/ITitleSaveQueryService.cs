/// <summary>
/// 타이틀 메뉴에서 세이브 데이터 존재 여부를 조회하는 인터페이스입니다.
/// </summary>
public interface ITitleSaveQueryService
{
    /// <summary>
    /// Continue 버튼 활성화에 사용할 데이터 존재 여부를 반환합니다.
    /// </summary>
    bool HasContinueData();

    /// <summary>
    /// Load Game 버튼 활성화에 사용할 데이터 존재 여부를 반환합니다.
    /// </summary>
    bool HasLoadableData();

    /// <summary>
    /// 지정한 저장 슬롯에 실제 진행 데이터가 존재하는지 반환합니다.
    /// </summary>
    bool HasUsedProgressInSlot(int slotIndex);

    /// <summary>
    /// 지정 슬롯의 진행 요약 정보를 반환합니다.
    /// </summary>
    bool TryGetSlotProgressSummary(int slotIndex, out SaveSlotProgressSummary summary);

    /// <summary>
    /// 마지막으로 사용한 저장 슬롯 번호를 반환합니다.
    /// </summary>
    bool TryGetLastUsedSlotIndex(out int slotIndex);

    /// <summary>
    /// New Game 덮어쓰기 경고에 사용할 기존 진행 데이터 존재 여부를 반환합니다.
    /// </summary>
    bool HasExistingProgress();
}
