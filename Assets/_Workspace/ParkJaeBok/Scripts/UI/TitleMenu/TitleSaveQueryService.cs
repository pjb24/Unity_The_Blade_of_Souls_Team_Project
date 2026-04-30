using System;
using UnityEngine;

/// <summary>
/// 모든 세이브 데이터 조회를 SaveDataStore에 위임하여 타이틀 메뉴용 세이브 데이터 조회 기능을 제공한다.
/// </summary>
public class TitleSaveQueryService : MonoBehaviour, ITitleSaveQueryService
{
    [Tooltip("타이틀 메뉴 세이브 상태 조회에 사용할 SaveDataStore. 비어있으면 SaveDataStore.Instance를 사용한다.")]
    [SerializeField] private SaveDataStore _saveDataStore; // Continue 및 Load Game 활성 여부 판단에 사용하는 저장소 서비스.

    public event Action SaveDataChanged;

    private SaveDataStore _listeningSaveDataStore; // 현재 이 서비스가 변경 이벤트를 전달받고 있는 SaveDataStore.

    /// <summary>
    /// 세이브 데이터 변경 이벤트를 구독하여 타이틀 메뉴 버튼이 즉시 갱신되도록 한다.
    /// </summary>
    private void OnEnable()
    {
        BindSaveDataStoreListener(true);
    }

    /// <summary>
    /// 세이브 데이터 변경 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        BindSaveDataStoreListener(false);
    }

    /// <summary>
    /// 마지막으로 선택된 유효한 슬롯 데이터를 Continue에서 사용할 수 있는지 여부를 반환한다.
    /// </summary>
    public bool HasContinueData()
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore();
        return saveDataStore != null && saveDataStore.HasLastUsedSlotData();
    }

    /// <summary>
    /// Load Game에서 사용할 수 있는 유효한 슬롯 데이터가 하나 이상 존재하는지 여부를 반환한다.
    /// </summary>
    public bool HasLoadableData()
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore();
        return saveDataStore != null && saveDataStore.HasAnySlotData();
    }

    /// <summary>
    /// 요청된 슬롯에 유효한 진행 데이터가 존재하는지 여부를 반환한다.
    /// </summary>
    public bool HasUsedProgressInSlot(int slotIndex)
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore();
        return saveDataStore != null && saveDataStore.HasSlotData((E_SaveSlot)slotIndex);
    }

    /// <summary>
    /// 해당 슬롯이 여전히 유효한 데이터일 때만 마지막으로 선택된 슬롯 인덱스를 반환한다.
    /// </summary>
    public bool TryGetLastUsedSlotIndex(out int slotIndex)
    {
        slotIndex = 1;
        SaveDataStore saveDataStore = ResolveSaveDataStore();
        if (saveDataStore == null)
        {
            return false;
        }

        slotIndex = (int)saveDataStore.GetCurrentSlot();
        return saveDataStore.HasLastUsedSlotData();
    }

    /// <summary>
    /// 기존 진행 데이터가 존재하여 New Game 시작 시 덮어쓰기 경고가 필요한지 여부를 반환한다.
    /// </summary>
    public bool HasExistingProgress()
    {
        return HasLoadableData();
    }

    /// <summary>
    /// 타이틀 메뉴 세이브 조회에 사용할 SaveDataStore를 가져온다.
    /// </summary>
    private SaveDataStore ResolveSaveDataStore()
    {
        if (_saveDataStore == null)
        {
            _saveDataStore = SaveDataStore.Instance;
        }

        if (_saveDataStore == null)
        {
            Debug.LogWarning("[TitleSaveQueryService] SaveDataStore를 찾을 수 없음. 타이틀 세이브 조회는 false를 반환함.", this);
            return null;
        }

        BindSaveDataStoreListener(true);
        return _saveDataStore;
    }

    /// <summary>
    /// SaveDataStore의 변경 이벤트를 이 서비스로 전달한다.
    /// </summary>
    private void BindSaveDataStoreListener(bool shouldBind)
    {
        SaveDataStore targetStore = shouldBind ? _saveDataStore ?? SaveDataStore.Instance : _listeningSaveDataStore;
        if (targetStore == null)
        {
            return;
        }

        if (_listeningSaveDataStore != null && _listeningSaveDataStore != targetStore)
        {
            _listeningSaveDataStore.RemoveListener(HandleSaveDataChanged);
            _listeningSaveDataStore = null;
        }

        if (shouldBind)
        {
            if (_listeningSaveDataStore == targetStore)
            {
                return;
            }

            targetStore.RemoveListener(HandleSaveDataChanged);
            targetStore.AddListener(HandleSaveDataChanged);
            _listeningSaveDataStore = targetStore;
            return;
        }

        targetStore.RemoveListener(HandleSaveDataChanged);
        _listeningSaveDataStore = null;
    }

    /// <summary>
    /// 세이브 데이터 변경 시 타이틀 UI가 다시 계산되도록 알린다.
    /// </summary>
    private void HandleSaveDataChanged(SaveGameData saveGameData)
    {
        SaveDataChanged?.Invoke();
    }
}
