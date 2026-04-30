using System;

/// <summary>
/// UI 컨트롤러에 세이브 파일 파싱을 노출하지 않고 타이틀 메뉴용 세이브 데이터 조회 기능을 제공한다.
/// </summary>
public interface ITitleSaveQueryService
{
    /// <summary>
    /// 세이브 슬롯 데이터 또는 마지막 선택 슬롯이 변경되었을 때 호출된다.
    /// </summary>
    event Action SaveDataChanged;

    /// <summary>
    /// Continue 기능이 마지막 선택된 슬롯 데이터를 사용할 수 있는지 여부를 반환한다.
    /// </summary>
    bool HasContinueData();

    /// <summary>
    /// Load Game에서 표시할 수 있는 유효한 슬롯이 최소 하나 이상 존재하는지 여부를 반환한다.
    /// </summary>
    bool HasLoadableData();

    /// <summary>
    /// 요청된 슬롯에 유효한 진행 데이터가 존재하는지 여부를 반환한다.
    /// </summary>
    bool HasUsedProgressInSlot(int slotIndex);

    /// <summary>
    /// 마지막으로 선택된 슬롯이 유효한 경우 해당 슬롯 인덱스를 반환한다.
    /// </summary>
    bool TryGetLastUsedSlotIndex(out int slotIndex);

    /// <summary>
    /// 기존 진행 데이터가 존재하여 New Game 시작 시 덮어쓰기 경고가 필요한지 여부를 반환한다.
    /// </summary>
    bool HasExistingProgress();
}
