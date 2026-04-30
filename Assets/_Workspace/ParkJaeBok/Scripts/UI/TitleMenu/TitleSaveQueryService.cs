using System;
using UnityEngine;

/// <summary>
/// Provides title menu save data queries by delegating all save inspection to SaveDataStore.
/// </summary>
public class TitleSaveQueryService : MonoBehaviour, ITitleSaveQueryService
{
    [Tooltip("SaveDataStore used to query title menu save state. If empty, SaveDataStore.Instance is used.")]
    [SerializeField] private SaveDataStore _saveDataStore; // Storage service used to decide Continue and Load Game availability.

    public event Action SaveDataChanged;

    private SaveDataStore _listeningSaveDataStore; // SaveDataStore whose change events are currently forwarded by this service.

    /// <summary>
    /// Subscribes to save data changes so title menu buttons can refresh immediately.
    /// </summary>
    private void OnEnable()
    {
        BindSaveDataStoreListener(true);
    }

    /// <summary>
    /// Unsubscribes from save data changes.
    /// </summary>
    private void OnDisable()
    {
        BindSaveDataStoreListener(false);
    }

    /// <summary>
    /// Returns whether Continue can use the last selected valid slot data.
    /// </summary>
    public bool HasContinueData()
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore();
        return saveDataStore != null && saveDataStore.HasLastUsedSlotData();
    }

    /// <summary>
    /// Returns whether Load Game has at least one valid slot data.
    /// </summary>
    public bool HasLoadableData()
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore();
        return saveDataStore != null && saveDataStore.HasAnySlotData();
    }

    /// <summary>
    /// Returns whether the requested slot contains valid progress data.
    /// </summary>
    public bool HasUsedProgressInSlot(int slotIndex)
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore();
        return saveDataStore != null && saveDataStore.HasSlotData((E_SaveSlot)slotIndex);
    }

    /// <summary>
    /// Returns the last selected slot index only when that slot still contains valid data.
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
    /// Returns whether an existing progress save should trigger New Game overwrite warnings.
    /// </summary>
    public bool HasExistingProgress()
    {
        return HasLoadableData();
    }

    /// <summary>
    /// Resolves the SaveDataStore used for title menu save queries.
    /// </summary>
    private SaveDataStore ResolveSaveDataStore()
    {
        if (_saveDataStore == null)
        {
            _saveDataStore = SaveDataStore.Instance;
        }

        if (_saveDataStore == null)
        {
            Debug.LogWarning("[TitleSaveQueryService] SaveDataStore was not found. Title save queries return false.", this);
            return null;
        }

        BindSaveDataStoreListener(true);
        return _saveDataStore;
    }

    /// <summary>
    /// Forwards SaveDataStore change events through the title save query service.
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
    /// Notifies title menu presenters that save-dependent UI state should be recalculated.
    /// </summary>
    private void HandleSaveDataChanged(SaveGameData saveGameData)
    {
        SaveDataChanged?.Invoke();
    }
}
