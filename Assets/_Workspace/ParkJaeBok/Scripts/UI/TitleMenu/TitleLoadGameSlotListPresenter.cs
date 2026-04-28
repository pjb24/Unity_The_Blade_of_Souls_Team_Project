using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 세이브 시스템 제거 상태에서 Load Game 슬롯 목록을 모두 Empty로 표시하는 Presenter입니다.
/// </summary>
public class TitleLoadGameSlotListPresenter : MonoBehaviour
{
    [Serializable]
    private class SlotItemView
    {
        [Tooltip("UI 항목이 표시하는 슬롯 번호입니다.")]
        [Min(1)]
        public int SlotIndex = 1; // 슬롯 목록 정렬과 표시용 번호입니다.

        [Tooltip("슬롯 선택 버튼입니다.")]
        public Button SelectButton; // 저장 기능 제거 후에는 비활성화됩니다.

        [Tooltip("슬롯 상태를 표시할 텍스트입니다.")]
        public Text StateText; // Empty 상태 표시용 텍스트입니다.

        [Tooltip("슬롯 진행 정보를 표시할 텍스트입니다.")]
        public Text ProgressText; // 저장 기능 제거 안내 표시용 텍스트입니다.
    }

    [Tooltip("Load Game 슬롯 UI 항목 목록입니다.")]
    [SerializeField] private SlotItemView[] _slotItems = Array.Empty<SlotItemView>(); // Load Game 슬롯별 버튼/텍스트 UI 묶음입니다.

    /// <summary>
    /// 패널 활성화 시 모든 슬롯을 저장 데이터 없음 상태로 갱신합니다.
    /// </summary>
    private void OnEnable()
    {
        RefreshSlotViews();
    }

    /// <summary>
    /// 모든 슬롯 UI를 Empty와 비활성 상태로 갱신합니다.
    /// </summary>
    public void RefreshSlotViews()
    {
        for (int i = 0; i < _slotItems.Length; i++)
        {
            SlotItemView slotItem = _slotItems[i]; // 현재 갱신 중인 슬롯 UI 항목입니다.
            if (slotItem == null)
            {
                continue;
            }

            if (slotItem.StateText != null)
            {
                slotItem.StateText.text = "Empty";
            }

            if (slotItem.ProgressText != null)
            {
                slotItem.ProgressText.text = "Save Removed";
            }

            if (slotItem.SelectButton != null)
            {
                slotItem.SelectButton.interactable = false;
            }
        }
    }

    /// <summary>
    /// 저장 시스템이 제거되어 슬롯 선택 요청을 무시합니다.
    /// </summary>
    public void SelectSlot(int slotIndex)
    {
        Debug.LogWarning($"[TitleLoadGameSlotListPresenter] Save system has been removed. Slot selection is ignored. slot={Mathf.Max(1, slotIndex)}", this);
    }
}
