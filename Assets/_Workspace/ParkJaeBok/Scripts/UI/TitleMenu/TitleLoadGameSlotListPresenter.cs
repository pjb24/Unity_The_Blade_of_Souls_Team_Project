using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 불러오기 슬롯 목록을 3개의 슬롯 저장 파일 상태에 맞춰 표시하는 Presenter입니다.
/// </summary>
public class TitleLoadGameSlotListPresenter : MonoBehaviour
{
    [Serializable]
    private class SlotItemView
    {
        [Tooltip("이 UI 항목이 표시할 저장 슬롯 번호입니다. 유효 범위는 1~3입니다.")]
        [Min(1)]
        public int SlotIndex = 1; // 이 UI 항목과 연결된 슬롯 저장 파일 번호입니다.

        [Tooltip("해당 슬롯을 선택해 Load Game을 요청하는 버튼입니다.")]
        public Button SelectButton; // 저장 데이터가 있을 때 활성화되는 슬롯 선택 버튼입니다.

        [Tooltip("슬롯이 비어 있는지 저장 데이터가 있는지 표시하는 텍스트입니다.")]
        public Text StateText; // 비어 있음 또는 저장됨 상태를 표시하는 텍스트입니다.

        [Tooltip("슬롯 번호나 진행 데이터 요약을 표시하는 텍스트입니다.")]
        public Text ProgressText; // 저장 파일의 간단한 요약 정보를 표시하는 텍스트입니다.
    }

    [Tooltip("게임 불러오기 화면에 표시할 슬롯 UI 항목 목록입니다.")]
    [SerializeField] private SlotItemView[] _slotItems = Array.Empty<SlotItemView>(); // 게임 불러오기 슬롯별 버튼과 텍스트 UI 묶음입니다.

    [Tooltip("슬롯 저장 데이터 존재 여부를 조회할 SaveDataStore입니다. 비어 있으면 SaveDataStore.Instance를 사용합니다.")]
    [SerializeField] private SaveDataStore _saveDataStore; // 슬롯 UI 상태를 판단할 저장소입니다.

    [Tooltip("슬롯 선택 후 게임 불러오기 요청을 전달할 GameFlowController입니다. 비어 있으면 GameFlowController.Instance를 사용합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // 게임 불러오기 실행을 위임할 플로우 컨트롤러입니다.

    /// <summary>
    /// 패널이 활성화될 때 모든 슬롯 표시를 최신 저장 상태로 갱신합니다.
    /// </summary>
    private void OnEnable()
    {
        RefreshSlotViews();
    }

    /// <summary>
    /// 모든 슬롯 UI를 현재 슬롯별 저장 데이터 존재 여부에 맞게 갱신합니다.
    /// </summary>
    public void RefreshSlotViews()
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore(); // 슬롯 상태 조회에 사용할 저장소입니다.
        for (int i = 0; i < _slotItems.Length; i++)
        {
            SlotItemView slotItem = _slotItems[i]; // 현재 갱신 중인 슬롯 UI 항목입니다.
            if (slotItem == null)
            {
                continue;
            }

            bool isLoadable = saveDataStore != null && saveDataStore.HasSlotData((E_SaveSlot)slotItem.SlotIndex); // 선택 버튼을 활성화할 수 있는지 여부입니다.

            if (slotItem.StateText != null)
            {
                slotItem.StateText.text = isLoadable ? "Saved" : "Empty";
            }

            if (slotItem.ProgressText != null)
            {
                slotItem.ProgressText.text = isLoadable ? $"Slot {slotItem.SlotIndex}" : "No Data";
            }

            if (slotItem.SelectButton != null)
            {
                slotItem.SelectButton.interactable = isLoadable;
            }
        }
    }

    /// <summary>
    /// 지정한 슬롯의 저장 데이터를 로드하도록 GameFlow에 요청합니다.
    /// </summary>
    public void SelectSlot(int slotIndex)
    {
        GameFlowController controller = ResolveGameFlowController(); // 게임 불러오기 요청을 처리할 플로우 컨트롤러입니다.
        if (controller == null)
        {
            Debug.LogWarning("[TitleLoadGameSlotListPresenter] GameFlowController를 찾을 수 없어 슬롯 로드를 수행하지 못했습니다.", this);
            return;
        }

        if (!controller.RequestStartLoadGameInSlot(slotIndex))
        {
            Debug.LogWarning($"[TitleLoadGameSlotListPresenter] 슬롯 로드 요청이 실패했습니다. slot={slotIndex}", this);
        }
    }

    /// <summary>
    /// 저장 데이터 조회에 사용할 SaveDataStore를 찾습니다.
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
            Debug.LogWarning("[TitleLoadGameSlotListPresenter] SaveDataStore를 찾을 수 없어 모든 슬롯을 Empty로 표시합니다.", this);
        }

        return _saveDataStore;
    }

    /// <summary>
    /// 슬롯 선택 요청을 처리할 GameFlowController를 찾습니다.
    /// </summary>
    private GameFlowController ResolveGameFlowController()
    {
        if (_gameFlowController != null)
        {
            return _gameFlowController;
        }

        _gameFlowController = GameFlowController.Instance;
        return _gameFlowController;
    }
}
