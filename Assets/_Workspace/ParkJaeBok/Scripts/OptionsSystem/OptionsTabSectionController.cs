using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 옵션 탭 선택에 따라 해당 Section 루트만 활성화하고 나머지를 비활성화하는 컨트롤러입니다.
/// </summary>
public class OptionsTabSectionController : MonoBehaviour
{
    /// <summary>
    /// 옵션 탭 식별 열거형입니다.
    /// </summary>
    public enum E_OptionsTab
    {
        Display = 0,
        Audio = 1,
        Input = 2,
        Accessibility = 3,
        Gameplay = 4
    }

    /// <summary>
    /// 탭과 Section 루트, 버튼 참조를 묶는 설정 구조체입니다.
    /// </summary>
    [Serializable]
    public struct OptionTabSectionEntry
    {
        [Tooltip("이 항목이 담당할 옵션 탭 타입입니다.")]
        public E_OptionsTab Tab; // 섹션과 매핑할 탭 식별자입니다.

        [Tooltip("(권장) 해당 탭이 선택됐을 때 함께 활성화할 Section 루트 오브젝트 목록입니다.")]
        public List<GameObject> SectionRoots; // 탭 선택 시 함께 On/Off 처리할 Section 루트 오브젝트 목록입니다.


        [Tooltip("이 탭을 대표하는 버튼입니다. 비워두면 버튼 상태 업데이트를 건너뜁니다.")]
        public Button TabButton; // 탭 선택 상태에 맞춰 interactable을 조정할 버튼 참조입니다.
    }

    [Header("Tab Mapping")]
    [Tooltip("탭-섹션 매핑 목록입니다. 선택된 탭의 Section만 활성화됩니다.")]
    [SerializeField] private List<OptionTabSectionEntry> _tabEntries = new List<OptionTabSectionEntry>(); // 탭과 Section 루트 간 매핑 목록입니다.

    [Header("Default")]
    [Tooltip("OnEnable 시 자동 선택할 기본 탭입니다.")]
    [SerializeField] private E_OptionsTab _defaultTab = E_OptionsTab.Display; // 패널 오픈 시 기본으로 선택할 탭입니다.

    [Tooltip("OnEnable 시 기본 탭 자동 선택을 수행할지 여부입니다.")]
    [SerializeField] private bool _selectDefaultOnEnable = true; // 활성화 시 기본 탭 자동 선택 수행 여부입니다.

    private bool _hasSelectedTab; // 현재 런타임에서 탭 선택이 1회 이상 수행되었는지 여부입니다.
    private E_OptionsTab _currentTab; // 현재 선택된 옵션 탭 상태입니다.

    private Action<E_OptionsTab> _tabChangedListeners; // 탭 변경 알림 리스너 체인입니다.

    /// <summary>
    /// OnEnable 시 기본 탭 자동 선택을 수행합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_selectDefaultOnEnable)
        {
            SelectTab(_defaultTab);
        }
    }

    /// <summary>
    /// Unity Inspector OnClick에서 int 파라미터를 사용해 탭 전환을 요청할 때 호출합니다.
    /// </summary>
    public void SelectTabByIndexFromInt(int tabIndex)
    {
        if (Enum.IsDefined(typeof(E_OptionsTab), tabIndex) == false)
        {
            Debug.LogWarning($"[OptionsTabSectionController] 잘못된 탭 인덱스가 전달되어 요청을 무시합니다. index={tabIndex}", this);
            return;
        }

        SelectTab((E_OptionsTab)tabIndex);
    }

    /// <summary>
    /// 외부에서 지정 탭 선택을 요청할 때 호출합니다.
    /// </summary>
    public void SelectTab(E_OptionsTab tab)
    {
        _currentTab = tab;
        _hasSelectedTab = true;

        for (int i = 0; i < _tabEntries.Count; i++)
        {
            OptionTabSectionEntry entry = _tabEntries[i]; // 현재 토글 처리 중인 탭-섹션 매핑 항목입니다.
            bool isSelected = entry.Tab == tab; // 현재 항목이 선택된 탭인지 여부입니다.

            ApplySectionActive(in entry, isSelected);

            if (entry.TabButton != null)
            {
                entry.TabButton.interactable = !isSelected;
            }
        }

        _tabChangedListeners?.Invoke(tab);
    }

    /// <summary>
    /// 하나의 매핑 항목에 속한 다중 섹션 목록을 선택 상태에 맞게 토글합니다.
    /// </summary>
    private void ApplySectionActive(in OptionTabSectionEntry entry, bool isSelected)
    {
        bool hasAnySection = false; // 현재 항목에 유효한 섹션 참조가 하나라도 있는지 여부입니다.

        if (entry.SectionRoots != null)
        {
            for (int i = 0; i < entry.SectionRoots.Count; i++)
            {
                GameObject sectionRoot = entry.SectionRoots[i]; // 다중 섹션 목록에서 현재 토글 대상 섹션입니다.
                if (sectionRoot == null)
                {
                    continue;
                }

                sectionRoot.SetActive(isSelected);
                hasAnySection = true;
            }
        }

        if (hasAnySection == false)
        {
            Debug.LogWarning($"[OptionsTabSectionController] SectionRoots가 비어 있어 토글을 건너뜁니다. tab={entry.Tab}", this);
        }
    }

    /// <summary>
    /// 현재 선택된 탭을 반환합니다. 선택 기록이 없으면 기본 탭을 반환합니다.
    /// </summary>
    public E_OptionsTab GetCurrentTab()
    {
        return _hasSelectedTab ? _currentTab : _defaultTab;
    }

    /// <summary>
    /// 탭 변경 알림 리스너를 등록합니다.
    /// </summary>
    public void AddListener(Action<E_OptionsTab> listener)
    {
        _tabChangedListeners += listener;
    }

    /// <summary>
    /// 탭 변경 알림 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(Action<E_OptionsTab> listener)
    {
        _tabChangedListeners -= listener;
    }
}
