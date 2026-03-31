using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Load Game 슬롯 목록의 Empty/진행도 표기를 갱신하고 슬롯 선택을 처리하는 Presenter입니다.
/// </summary>
public class TitleLoadGameSlotListPresenter : MonoBehaviour
{
    [Serializable]
    private class SlotItemView
    {
        [Tooltip("이 UI 항목이 나타내는 저장 슬롯 번호입니다.")]
        [Min(1)]
        public int SlotIndex = 1; // 슬롯 선택/조회 시 사용할 저장 슬롯 번호입니다.

        [Tooltip("슬롯 선택 버튼입니다.")]
        public Button SelectButton; // 슬롯 선택 입력을 수신할 버튼 참조입니다.

        [Tooltip("슬롯 상태(예: Empty/Used)를 표시할 텍스트입니다.")]
        public Text StateText; // 슬롯 사용 여부를 표시할 텍스트 컴포넌트 참조입니다.

        [Tooltip("슬롯 진행도 정보를 표시할 텍스트입니다.")]
        public Text ProgressText; // 슬롯 진행도 문자열을 표시할 텍스트 컴포넌트 참조입니다.
    }

    [Tooltip("슬롯 진행도 조회에 사용할 ITitleSaveQueryService 구현 컴포넌트입니다.")]
    [SerializeField] private MonoBehaviour _saveQueryComponent; // 슬롯 진행도 조회를 수행할 서비스 컴포넌트 참조입니다.

    [Tooltip("선택된 슬롯을 SaveCoordinator에 반영할 대상입니다. 비어 있으면 SaveCoordinator.Instance를 사용합니다.")]
    [SerializeField] private SaveCoordinator _saveCoordinator; // 슬롯 선택 결과를 적용할 SaveCoordinator 참조입니다.

    [Tooltip("슬롯 UI 항목 목록입니다.")]
    [SerializeField] private SlotItemView[] _slotItems = Array.Empty<SlotItemView>(); // Load Game 슬롯별 버튼/텍스트 UI 묶음 목록입니다.

    private ITitleSaveQueryService _saveQueryService; // 슬롯 진행 요약 조회에 사용할 런타임 서비스 참조입니다.

    /// <summary>
    /// 인터페이스 참조를 해석하고 버튼 이벤트를 연결합니다.
    /// </summary>
    private void Awake()
    {
        _saveQueryService = _saveQueryComponent as ITitleSaveQueryService;
        if (_saveQueryComponent != null && _saveQueryService == null)
        {
            Debug.LogWarning("[TitleLoadGameSlotListPresenter] saveQueryComponent가 ITitleSaveQueryService를 구현하지 않았습니다.", this);
        }

        BindSlotButtonEvents();
    }

    /// <summary>
    /// 패널 활성 시 슬롯 표시 상태를 최신 세이브 기준으로 갱신합니다.
    /// </summary>
    private void OnEnable()
    {
        RefreshSlotViews();
    }

    /// <summary>
    /// 슬롯 상태/진행도 텍스트와 버튼 활성 상태를 갱신합니다.
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

            int safeSlotIndex = Mathf.Max(1, slotItem.SlotIndex); // 조회/선택 로직에 사용할 보정 슬롯 번호입니다.
            SaveSlotProgressSummary summary = default; // 슬롯 진행 텍스트 구성에 사용할 조회 결과 캐시입니다.
            bool hasSummary = _saveQueryService != null && _saveQueryService.TryGetSlotProgressSummary(safeSlotIndex, out summary);
            bool hasUsedData = hasSummary && summary.HasUsedData; // Empty/Used 표기에 사용할 슬롯 사용 여부입니다.

            if (slotItem.StateText != null)
            {
                slotItem.StateText.text = hasUsedData ? "Used" : "Empty";
            }

            if (slotItem.ProgressText != null)
            {
                slotItem.ProgressText.text = hasUsedData
                    ? $"Progress {Mathf.Max(0, summary.TotalClearCount)} | Stage {summary.SelectedStageId}"
                    : "Progress -";
            }

            if (slotItem.SelectButton != null)
            {
                slotItem.SelectButton.interactable = hasUsedData;
            }
        }
    }

    /// <summary>
    /// 외부에서 호출 가능한 슬롯 선택 API입니다.
    /// </summary>
    public void SelectSlot(int slotIndex)
    {
        SaveCoordinator coordinator = ResolveCoordinator(); // 슬롯 선택 결과를 적용할 SaveCoordinator 인스턴스입니다.
        if (coordinator == null)
        {
            return;
        }

        coordinator.SetActiveSaveSlot(Mathf.Max(1, slotIndex), true);
    }

    /// <summary>
    /// 슬롯 버튼 클릭 시 SelectSlot 호출이 연결되도록 이벤트를 바인딩합니다.
    /// </summary>
    private void BindSlotButtonEvents()
    {
        for (int i = 0; i < _slotItems.Length; i++)
        {
            SlotItemView slotItem = _slotItems[i]; // 버튼 이벤트를 연결할 슬롯 UI 항목입니다.
            if (slotItem == null || slotItem.SelectButton == null)
            {
                continue;
            }

            int capturedSlotIndex = Mathf.Max(1, slotItem.SlotIndex); // 람다 캡처에 사용할 슬롯 번호 복사본입니다.
            slotItem.SelectButton.onClick.AddListener(() => SelectSlot(capturedSlotIndex));
        }
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
            Debug.LogWarning("[TitleLoadGameSlotListPresenter] SaveCoordinator를 찾지 못했습니다.", this);
            return null;
        }

        return SaveCoordinator.Instance;
    }
}
