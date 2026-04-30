using System;

/// <summary>
/// Provides title menu save data queries without exposing save file parsing to UI controllers.
/// </summary>
public interface ITitleSaveQueryService
{
    /// <summary>
    /// Raised when save slot data or the last selected slot changes.
    /// </summary>
    event Action SaveDataChanged;

    /// <summary>
    /// Returns whether Continue can use the last selected slot data.
    /// </summary>
    bool HasContinueData();

    /// <summary>
    /// Returns whether Load Game has at least one valid slot to show.
    /// </summary>
    bool HasLoadableData();

    /// <summary>
    /// Returns whether the requested slot contains valid progress data.
    /// </summary>
    bool HasUsedProgressInSlot(int slotIndex);

    /// <summary>
    /// Returns the last selected slot index when that slot is valid.
    /// </summary>
    bool TryGetLastUsedSlotIndex(out int slotIndex);

    /// <summary>
    /// Returns whether an existing progress save should trigger New Game overwrite warnings.
    /// </summary>
    bool HasExistingProgress();
}
