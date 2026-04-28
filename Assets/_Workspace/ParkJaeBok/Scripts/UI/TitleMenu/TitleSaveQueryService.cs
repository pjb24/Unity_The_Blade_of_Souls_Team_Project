using UnityEngine;

/// <summary>
/// 세이브 시스템 제거 상태에서 타이틀 메뉴의 저장 데이터 조회를 일관되게 false로 응답하는 서비스입니다.
/// </summary>
public class TitleSaveQueryService : MonoBehaviour, ITitleSaveQueryService
{
    [Tooltip("저장 시스템 제거 후 Load/Continue 버튼을 항상 비활성화할지 여부입니다.")]
    [SerializeField] private bool _disableLoadAndContinue = true; // 저장 기능 제거 정책을 Inspector에서 확인하기 위한 표시용 플래그입니다.

    /// <summary>
    /// Continue 가능한 저장 데이터가 없음을 반환합니다.
    /// </summary>
    public bool HasContinueData()
    {
        return !_disableLoadAndContinue && false;
    }

    /// <summary>
    /// Load Game 가능한 저장 데이터가 없음을 반환합니다.
    /// </summary>
    public bool HasLoadableData()
    {
        return !_disableLoadAndContinue && false;
    }

    /// <summary>
    /// 지정 슬롯에 저장된 진행 데이터가 없음을 반환합니다.
    /// </summary>
    public bool HasUsedProgressInSlot(int slotIndex)
    {
        return false;
    }

    /// <summary>
    /// 저장 시스템이 제거되어 마지막 사용 슬롯을 제공하지 않습니다.
    /// </summary>
    public bool TryGetLastUsedSlotIndex(out int slotIndex)
    {
        slotIndex = 1;
        return false;
    }

    /// <summary>
    /// 저장 시스템이 제거되어 기존 진행 데이터가 없음을 반환합니다.
    /// </summary>
    public bool HasExistingProgress()
    {
        return false;
    }
}
