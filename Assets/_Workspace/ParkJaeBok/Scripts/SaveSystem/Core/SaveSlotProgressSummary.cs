using System;

/// <summary>
/// 타이틀/로드 UI에서 표시할 저장 슬롯 진행 요약 정보입니다.
/// </summary>
[Serializable]
public struct SaveSlotProgressSummary
{
    public int SlotIndex; // 요약 정보가 가리키는 저장 슬롯 번호입니다.
    public bool HasUsedData; // 슬롯에 실제 진행 데이터가 존재하는지 여부입니다.
    public string SelectedStageId; // 마지막으로 진행 중이던 스테이지 ID입니다.
    public int TotalClearCount; // 누적 스테이지 클리어 횟수 기반 진행도 값입니다.
    public long LastSavedUnixTimeUtc; // 슬롯의 마지막 저장 시각(UTC UnixTime)입니다.
}
