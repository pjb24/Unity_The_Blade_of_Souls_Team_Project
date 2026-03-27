using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 타이틀 메뉴 UI 표시와 버튼 입력 이벤트 발행을 담당하는 View입니다.
/// </summary>
public class TitleMenuView : MonoBehaviour
{
    [Header("Buttons")]
    [Tooltip("New Game 버튼입니다.")]
    [SerializeField] private Button _newGameButton; // New Game 요청 이벤트를 발행할 버튼 참조입니다.

    [Tooltip("Continue 버튼입니다.")]
    [SerializeField] private Button _continueButton; // Continue 요청 이벤트를 발행할 버튼 참조입니다.

    [Tooltip("Load Game 버튼입니다.")]
    [SerializeField] private Button _loadGameButton; // Load Game 요청 이벤트를 발행할 버튼 참조입니다.

    [Tooltip("Option 버튼입니다.")]
    [SerializeField] private Button _optionButton; // Option 요청 이벤트를 발행할 버튼 참조입니다.

    [Tooltip("Quit 버튼입니다.")]
    [SerializeField] private Button _quitButton; // Quit 요청 이벤트를 발행할 버튼 참조입니다.

    [Header("Selection")]
    [Tooltip("최초 진입/복귀 시 기본 선택 대상으로 둘 Selectable입니다.")]
    [SerializeField] private Selectable _defaultSelected; // 메뉴 활성화 시 기본 포커스로 지정할 Selectable입니다.

    [Header("Overlay")]
    [Tooltip("로딩 중 입력 차단에 사용할 오버레이 오브젝트입니다.")]
    [SerializeField] private GameObject _loadingBlocker; // 액션 처리 중 입력 차단을 표시할 오버레이입니다.

    public event Action OnNewGameRequested;
    public event Action OnContinueRequested;
    public event Action OnLoadGameRequested;
    public event Action OnOptionRequested;
    public event Action OnQuitRequested;

    /// <summary>
    /// 버튼 클릭 이벤트를 View 이벤트로 연결합니다.
    /// </summary>
    private void Awake()
    {
        BindButton(_newGameButton, () => OnNewGameRequested?.Invoke(), "NewGame");
        BindButton(_continueButton, () => OnContinueRequested?.Invoke(), "Continue");
        BindButton(_loadGameButton, () => OnLoadGameRequested?.Invoke(), "LoadGame");
        BindButton(_optionButton, () => OnOptionRequested?.Invoke(), "Option");
        BindButton(_quitButton, () => OnQuitRequested?.Invoke(), "Quit");
    }

    /// <summary>
    /// Continue/Load 버튼 활성 상태를 갱신합니다.
    /// </summary>
    public void SetAvailability(bool canContinue, bool canLoad)
    {
        if (_continueButton != null)
        {
            _continueButton.interactable = canContinue;
        }

        if (_loadGameButton != null)
        {
            _loadGameButton.interactable = canLoad;
        }
    }

    /// <summary>
    /// 액션 처리 중 입력 차단 오버레이 표시 여부를 설정합니다.
    /// </summary>
    public void SetLoadingBlockerVisible(bool isVisible)
    {
        if (_loadingBlocker != null)
        {
            _loadingBlocker.SetActive(isVisible);
        }
    }

    /// <summary>
    /// 메뉴의 기본 선택 대상을 EventSystem에 적용합니다.
    /// </summary>
    public void SetDefaultSelection()
    {
        GameObject target = _defaultSelected != null ? _defaultSelected.gameObject : (_newGameButton != null ? _newGameButton.gameObject : null); // 기본 선택 대상 오브젝트입니다.
        if (target == null)
        {
            Debug.LogWarning("[TitleMenuView] 기본 선택 대상을 찾지 못했습니다.", this);
            return;
        }

        if (EventSystem.current == null)
        {
            Debug.LogWarning("[TitleMenuView] EventSystem.current가 null이라 기본 선택을 적용하지 못했습니다.", this);
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }

    /// <summary>
    /// 버튼 참조가 유효할 때 클릭 리스너를 연결합니다.
    /// </summary>
    private void BindButton(Button button, Action onClick, string buttonName)
    {
        if (button == null)
        {
            Debug.LogWarning($"[TitleMenuView] 버튼 참조가 비어 있습니다. name={buttonName}", this);
            return;
        }

        button.onClick.AddListener(() => onClick?.Invoke());
    }
}
