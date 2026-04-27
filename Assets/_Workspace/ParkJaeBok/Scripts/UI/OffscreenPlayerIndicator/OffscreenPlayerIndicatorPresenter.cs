using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 밖 플레이어 방향 표시 UI의 표시 상태, 위치, 회전, 아이콘과 이름 출력을 담당합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class OffscreenPlayerIndicatorPresenter : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Indicator 위치와 회전을 적용할 RectTransform입니다. 비어 있으면 현재 오브젝트의 RectTransform을 사용합니다.")]
    [SerializeField] private RectTransform _indicatorRect; // 화면 가장자리 위치와 회전을 실제로 반영할 UI RectTransform 참조입니다.

    [Tooltip("Indicator 전체 표시/숨김을 제어할 GameObject입니다. 비어 있으면 현재 오브젝트를 사용합니다.")]
    [SerializeField] private GameObject _indicatorRoot; // Indicator 전체 활성 상태를 제어할 루트 오브젝트 참조입니다.

    [Tooltip("대상 플레이어 아이콘을 표시할 Image입니다. 비어 있으면 Sprite 교체를 건너뜁니다.")]
    [SerializeField] private Image _iconImage; // 대상 플레이어 아이콘 Sprite를 출력할 Image 참조입니다.

    [Tooltip("대상 플레이어 이름을 표시할 TMP_Text입니다. 비어 있으면 이름 출력을 건너뜁니다.")]
    [SerializeField] private TMP_Text _nameText; // 대상 플레이어 표시 이름을 출력할 TMP_Text 참조입니다.

    [Tooltip("Alpha 기반 표시/숨김을 사용할 CanvasGroup입니다. 비어 있으면 GameObject 활성 상태만 사용합니다.")]
    [SerializeField] private CanvasGroup _canvasGroup; // Indicator 투명도와 입력 차단 상태를 제어할 CanvasGroup 참조입니다.

    [Header("Presentation")]
    [Tooltip("기본으로 사용할 대상 아이콘 Sprite입니다. 대상별 Sprite가 없을 때 이 값을 사용합니다.")]
    [SerializeField] private Sprite _defaultIconSprite; // 대상별 아이콘이 없을 때 사용할 기본 Sprite 참조입니다.

    [Tooltip("표시 이름이 비어 있을 때 사용할 기본 이름입니다.")]
    [SerializeField] private string _defaultDisplayName = "Player"; // 표시 이름 데이터가 없을 때 출력할 기본 문자열입니다.

    [Tooltip("Icon이 위쪽을 바라보는 Sprite인지 여부입니다. 꺼져 있으면 오른쪽을 바라보는 Sprite로 간주해 회전 보정을 적용합니다.")]
    [SerializeField] private bool _iconSpritePointsUp = true; // 회전 기준 Sprite 방향 보정을 위한 플래그입니다.

    private bool _isVisible; // 현재 Indicator가 표시 상태인지 추적하는 런타임 플래그입니다.

    /// <summary>
    /// RectTransform 참조를 보정하고 Indicator를 초기 숨김 상태로 만듭니다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        Hide();
    }

    /// <summary>
    /// Inspector 값 변경 시 필수 참조를 자동 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Indicator 위치와 회전을 적용할 RectTransform을 반환합니다.
    /// </summary>
    public RectTransform RectTransform
    {
        get
        {
            ResolveReferences();
            return _indicatorRect;
        }
    }

    /// <summary>
    /// Indicator 표시 이름과 아이콘 Sprite를 갱신합니다.
    /// </summary>
    public void SetTargetPresentation(string displayName, Sprite iconSprite)
    {
        if (_nameText != null)
        {
            _nameText.text = string.IsNullOrWhiteSpace(displayName) ? _defaultDisplayName : displayName;
        }

        if (_iconImage == null)
        {
            return;
        }

        Sprite resolvedSprite = iconSprite != null ? iconSprite : _defaultIconSprite; // 대상별 Sprite와 기본 Sprite 중 실제 적용할 Sprite입니다.
        if (resolvedSprite != null)
        {
            _iconImage.sprite = resolvedSprite;
            _iconImage.enabled = true;
        }
    }

    /// <summary>
    /// Indicator를 화면 가장자리 좌표로 이동시키고 필요하면 대상 방향으로 회전합니다.
    /// </summary>
    public void Show(Vector2 anchoredPosition, Vector2 direction, bool useRotation)
    {
        ResolveReferences();
        if (_indicatorRect == null)
        {
            Debug.LogWarning($"[OffscreenPlayerIndicatorPresenter] RectTransform이 없어 표시를 건너뜁니다. object={name}", this);
            return;
        }

        _indicatorRect.anchoredPosition = anchoredPosition;

        if (useRotation && direction.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg; // 오른쪽 기준 방향 각도입니다.
            if (_iconSpritePointsUp)
            {
                angle -= 90f;
            }

            _indicatorRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        else
        {
            _indicatorRect.localRotation = Quaternion.identity;
        }

        SetVisible(true);
    }

    /// <summary>
    /// Indicator를 숨깁니다.
    /// </summary>
    public void Hide()
    {
        SetVisible(false);
    }

    /// <summary>
    /// 필수 UI 참조를 현재 오브젝트 기준으로 보정합니다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_indicatorRect == null)
        {
            _indicatorRect = GetComponent<RectTransform>();
        }

        if (_indicatorRoot == null)
        {
            _indicatorRoot = gameObject;
        }
    }

    /// <summary>
    /// CanvasGroup 또는 GameObject 활성 상태로 표시 상태를 변경합니다.
    /// </summary>
    private void SetVisible(bool isVisible)
    {
        _isVisible = isVisible;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = isVisible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_indicatorRoot != null && _indicatorRoot.activeSelf != isVisible)
        {
            _indicatorRoot.SetActive(isVisible);
        }
    }
}
