using UnityEngine;

/// <summary>
/// 엔딩 UI 루트의 표시 상태와 상호작용 상태를 제어합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingPanelView : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("엔딩 UI 전체를 켜고 끌 루트 오브젝트입니다. 비어 있으면 이 GameObject를 사용합니다.")]
    [SerializeField] private GameObject _panelRoot; // 엔딩 UI 표시 상태를 제어할 루트 오브젝트입니다.

    [Tooltip("엔딩 UI의 투명도와 Raycast 차단을 제어할 CanvasGroup입니다. 비어 있으면 활성 상태만 제어합니다.")]
    [SerializeField] private CanvasGroup _canvasGroup; // 엔딩 UI 상호작용과 표시 상태를 제어할 CanvasGroup입니다.

    [Header("Initial State")]
    [Tooltip("Awake 시점에 엔딩 UI를 숨길지 여부입니다.")]
    [SerializeField] private bool _hideOnAwake = true; // 씬 시작 시 엔딩 UI를 숨겨 둘지 결정합니다.

    /// <summary>
    /// 컴포넌트 초기화 시 디자이너가 지정한 초기 표시 상태를 적용합니다.
    /// </summary>
    private void Awake()
    {
        ResolveOptionalReferences();

        if (_hideOnAwake)
        {
            SetVisible(false);
        }
    }

    /// <summary>
    /// Inspector 값이 바뀔 때 누락된 보조 참조를 자동 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        ResolveOptionalReferences();
    }

    /// <summary>
    /// 엔딩 UI 표시 상태를 변경합니다.
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        GameObject root = ResolvePanelRoot(); // 실제 활성 상태를 적용할 UI 루트입니다.
        root.SetActive(isVisible);

        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = isVisible ? 1f : 0f;
        SetInteractable(isVisible);
    }

    /// <summary>
    /// 엔딩 UI의 입력 상호작용 가능 여부를 변경합니다.
    /// </summary>
    public void SetInteractable(bool isInteractable)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.interactable = isInteractable;
        _canvasGroup.blocksRaycasts = isInteractable;
    }

    /// <summary>
    /// 비어 있는 선택 참조를 현재 오브젝트 기준으로 자동 탐색합니다.
    /// </summary>
    private void ResolveOptionalReferences()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// 표시 제어에 사용할 최종 루트 오브젝트를 반환합니다.
    /// </summary>
    private GameObject ResolvePanelRoot()
    {
        return _panelRoot != null ? _panelRoot : gameObject;
    }
}
