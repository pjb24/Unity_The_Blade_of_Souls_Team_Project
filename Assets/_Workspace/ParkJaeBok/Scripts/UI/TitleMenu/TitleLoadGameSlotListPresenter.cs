using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Load Game 슬롯 목록을 단일 JSON 저장 파일 기준으로 표시하는 Presenter입니다.
/// </summary>
public class TitleLoadGameSlotListPresenter : MonoBehaviour
{
    [Serializable]
    private class SlotItemView
    {
        [Tooltip("UI 항목이 표시하는 슬롯 번호입니다. 단일 JSON 저장은 1번 슬롯만 사용합니다.")]
        [Min(1)]
        public int SlotIndex = 1; // 단일 저장 파일과 매핑되는 UI 슬롯 번호입니다.

        [Tooltip("슬롯 선택 버튼입니다.")]
        public Button SelectButton; // 저장 데이터가 있을 때 활성화할 슬롯 선택 버튼입니다.

        [Tooltip("슬롯 상태를 표시할 텍스트입니다.")]
        public Text StateText; // Empty 또는 Saved 상태를 표시하는 텍스트입니다.

        [Tooltip("슬롯 진행 정보를 표시할 텍스트입니다.")]
        public Text ProgressText; // 저장 파일 요약 정보를 표시하는 텍스트입니다.
    }

    [Tooltip("Load Game 슬롯 UI 항목 목록입니다.")]
    [SerializeField] private SlotItemView[] _slotItems = Array.Empty<SlotItemView>(); // Load Game 슬롯별 버튼/텍스트 UI 묶음입니다.

    [Tooltip("저장 데이터 존재 여부를 조회할 SaveDataStore입니다. 비어 있으면 Instance를 사용합니다.")]
    [SerializeField] private SaveDataStore _saveDataStore; // 슬롯 UI 상태를 판단할 저장소입니다.

    [Tooltip("슬롯 선택 시 Continue 요청을 보낼 GameFlowController입니다. 비어 있으면 Instance를 사용합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // Load Game 실행을 위임할 플로우 컨트롤러입니다.

    /// <summary>
    /// 패널 활성화 시 모든 슬롯 표시를 갱신합니다.
    /// </summary>
    private void OnEnable()
    {
        RefreshSlotViews();
    }

    /// <summary>
    /// 모든 슬롯 UI를 현재 저장 데이터 존재 여부에 맞게 갱신합니다.
    /// </summary>
    public void RefreshSlotViews()
    {
        SaveDataStore saveDataStore = ResolveSaveDataStore(); // 슬롯 상태 조회에 사용할 저장소입니다.
        bool hasProgress = saveDataStore != null && saveDataStore.HasProgressData(); // 단일 저장 파일 진행 데이터 존재 여부입니다.

        for (int i = 0; i < _slotItems.Length; i++)
        {
            SlotItemView slotItem = _slotItems[i]; // 현재 갱신 중인 슬롯 UI 항목입니다.
            if (slotItem == null)
            {
                continue;
            }

            bool isPrimarySlot = slotItem.SlotIndex == 1; // 단일 JSON 저장 파일과 매핑되는 슬롯 여부입니다.
            bool isLoadable = isPrimarySlot && hasProgress; // 버튼 활성화 여부입니다.

            if (slotItem.StateText != null)
            {
                slotItem.StateText.text = isLoadable ? "Saved" : "Empty";
            }

            if (slotItem.ProgressText != null)
            {
                slotItem.ProgressText.text = isLoadable ? "Local Save" : "No Data";
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
        if (slotIndex != 1)
        {
            Debug.LogWarning($"[TitleLoadGameSlotListPresenter] 단일 JSON 저장 파일만 사용하므로 slotIndex=1 외 슬롯은 로드할 수 없습니다. slot={slotIndex}", this);
            return;
        }

        GameFlowController controller = ResolveGameFlowController(); // Load Game 요청을 처리할 플로우 컨트롤러입니다.
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
            Debug.LogWarning("[TitleLoadGameSlotListPresenter] SaveDataStore를 찾을 수 없어 슬롯을 Empty로 표시합니다.", this);
        }

        return _saveDataStore;
    }

    /// <summary>
    /// 슬롯 선택 요청을 처리할 GameFlowController를 해석합니다.
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
