using UnityEngine;

/// <summary>
/// SaveCoordinator를 이용해 타이틀 메뉴의 세이브 존재 여부를 조회하는 서비스입니다.
/// </summary>
public class TitleSaveQueryService : MonoBehaviour, ITitleSaveQueryService
{
    [Tooltip("세이브 조회 시 우선 사용할 SaveCoordinator 참조입니다. 비어 있으면 SaveCoordinator.Instance를 사용합니다.")]
    [SerializeField] private SaveCoordinator _saveCoordinator; // 세이브 조회에 사용할 SaveCoordinator 참조입니다.

    [Tooltip("Load Game 메뉴에서 조회할 저장 슬롯 개수입니다.")]
    [Min(1)]
    [SerializeField] private int _slotCount = 3; // 타이틀에서 진행 데이터 존재 여부를 검사할 슬롯 총 개수입니다.

    /// <summary>
    /// Continue 버튼 활성화 여부를 반환합니다.
    /// </summary>
    public bool HasContinueData()
    {
        SaveCoordinator coordinator = ResolveCoordinator(); // 세이브 존재 여부 조회에 사용할 Coordinator 인스턴스입니다.
        if (coordinator == null)
        {
            return false;
        }

        if (TryGetLastUsedSlotIndex(out int lastUsedSlotIndex) == false)
        {
            lastUsedSlotIndex = coordinator.ActiveSaveSlotIndex;
        }

        return HasUsedProgressInSlot(lastUsedSlotIndex);
    }

    /// <summary>
    /// Load Game 버튼 활성화 여부를 반환합니다.
    /// </summary>
    public bool HasLoadableData()
    {
        SaveCoordinator coordinator = ResolveCoordinator(); // 슬롯 진행도 조회에 사용할 Coordinator 인스턴스입니다.
        if (coordinator == null)
        {
            return false;
        }

        int safeSlotCount = Mathf.Max(1, _slotCount); // 슬롯 존재 여부 루프에서 사용할 보정 슬롯 개수입니다.
        for (int slotIndex = 1; slotIndex <= safeSlotCount; slotIndex++)
        {
            if (coordinator.HasUsedProgressInSlot(slotIndex))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 지정 슬롯에 실제 진행 데이터가 있는지 반환합니다.
    /// </summary>
    public bool HasUsedProgressInSlot(int slotIndex)
    {
        SaveCoordinator coordinator = ResolveCoordinator(); // 슬롯 진행도 존재 여부 조회에 사용할 Coordinator 인스턴스입니다.
        if (coordinator == null)
        {
            return false;
        }

        return coordinator.HasUsedProgressInSlot(slotIndex);
    }

    /// <summary>
    /// 지정 슬롯의 진행 요약 정보를 반환합니다.
    /// </summary>
    public bool TryGetSlotProgressSummary(int slotIndex, out SaveSlotProgressSummary summary)
    {
        SaveCoordinator coordinator = ResolveCoordinator(); // 슬롯 요약 조회에 사용할 Coordinator 인스턴스입니다.
        if (coordinator == null)
        {
            summary = default;
            return false;
        }

        return coordinator.TryGetSlotProgressSummary(slotIndex, out summary);
    }

    /// <summary>
    /// 마지막으로 사용한 저장 슬롯 번호를 반환합니다.
    /// </summary>
    public bool TryGetLastUsedSlotIndex(out int slotIndex)
    {
        SaveCoordinator coordinator = ResolveCoordinator(); // 마지막 사용 슬롯 조회에 사용할 Coordinator 인스턴스입니다.
        if (coordinator == null)
        {
            slotIndex = 1;
            return false;
        }

        return coordinator.TryGetLastUsedSlotIndex(out slotIndex);
    }

    /// <summary>
    /// New Game 덮어쓰기 확인이 필요한 기존 진행 데이터 존재 여부를 반환합니다.
    /// </summary>
    public bool HasExistingProgress()
    {
        return HasLoadableData();
    }

    /// <summary>
    /// 직렬화 참조 또는 싱글톤에서 SaveCoordinator를 해석합니다.
    /// </summary>
    private SaveCoordinator ResolveCoordinator()
    {
        if (_saveCoordinator != null)
        {
            return _saveCoordinator;
        }

        if (SaveCoordinator.Instance == null)
        {
            Debug.LogWarning("[TitleSaveQueryService] SaveCoordinator를 찾지 못해 세이브 조회를 false로 처리합니다.", this);
            return null;
        }

        return SaveCoordinator.Instance;
    }
}
