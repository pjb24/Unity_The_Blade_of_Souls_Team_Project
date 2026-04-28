using UnityEngine;

/// <summary>
/// Title 메뉴에서 저장 데이터 존재 여부를 조회하는 서비스입니다.
/// </summary>
public class TitleSaveQueryService : MonoBehaviour, ITitleSaveQueryService
{
    [Tooltip("저장 데이터 존재 여부를 조회할 SaveDataStore입니다. 비어 있으면 Instance를 사용합니다.")]
    [SerializeField] private SaveDataStore _saveDataStore; // Title 메뉴의 Continue/Load 활성화 판단에 사용할 저장소입니다.

    /// <summary>
    /// Continue 가능한 저장 데이터가 있는지 반환합니다.
    /// </summary>
    public bool HasContinueData()
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore(); // Continue 데이터 존재 여부를 조회할 저장소입니다.
        return saveDataStore != null && saveDataStore.HasProgressData();
    }

    /// <summary>
    /// Load Game 가능한 저장 데이터가 있는지 반환합니다.
    /// </summary>
    public bool HasLoadableData()
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore(); // Load Game 데이터 존재 여부를 조회할 저장소입니다.
        return saveDataStore != null && saveDataStore.HasProgressData();
    }

    /// <summary>
    /// 지정한 슬롯에 진행 데이터가 있는지 반환합니다.
    /// </summary>
    public bool HasUsedProgressInSlot(int slotIndex)
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore(); // 슬롯별 진행 데이터 존재 여부를 조회할 저장소입니다.
        return saveDataStore != null && saveDataStore.HasSlotData((E_SaveSlot)slotIndex);
    }

    /// <summary>
    /// 마지막으로 사용한 슬롯 번호를 반환합니다.
    /// </summary>
    public bool TryGetLastUsedSlotIndex(out int slotIndex)
    {
        slotIndex = 1;
        SaveDataStore saveDataStore = ResolveSaveDataStore(); // 마지막 사용 슬롯 추정에 사용할 저장소입니다.
        if (saveDataStore == null)
        {
            return false;
        }

        slotIndex = (int)saveDataStore.GetCurrentSlot();
        return saveDataStore.HasSlotData(saveDataStore.GetCurrentSlot());
    }

    /// <summary>
    /// 새 게임 덮어쓰기 경고에 사용할 기존 진행 데이터 존재 여부를 반환합니다.
    /// </summary>
    public bool HasExistingProgress()
    {
        return HasLoadableData();
    }

    /// <summary>
    /// 저장 데이터 조회에 사용할 SaveDataStore를 해석합니다.
    /// </summary>
    private SaveDataStore ResolveSaveDataStore()
    {
        if (_saveDataStore != null)
        {
            return _saveDataStore;
        }

        _saveDataStore = SaveDataStore.Instance;
        if (_saveDataStore == null)
        {
            Debug.LogWarning("[TitleSaveQueryService] SaveDataStore를 찾을 수 없어 저장 데이터 존재 여부를 false로 처리합니다.", this);
        }

        return _saveDataStore;
    }
}
