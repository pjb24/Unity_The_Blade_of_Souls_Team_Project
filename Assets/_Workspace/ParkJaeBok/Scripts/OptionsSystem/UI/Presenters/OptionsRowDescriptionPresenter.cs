using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 옵션 Row hover 상태에 따라 우측 설명 패널 텍스트를 갱신하는 Presenter 컴포넌트입니다.
/// </summary>
public class OptionsRowDescriptionPresenter : MonoBehaviour
{
    [Header("Description Panel")]
    [Tooltip("현재 선택/호버된 옵션의 제목을 표시할 텍스트입니다.")]
    [SerializeField] private TMP_Text _descriptionTitleText; // 우측 설명 패널 제목을 출력할 TMP_Text 참조입니다.

    [Tooltip("현재 선택/호버된 옵션의 상세 설명을 표시할 텍스트입니다.")]
    [SerializeField] private TMP_Text _descriptionBodyText; // 우측 설명 패널 본문을 출력할 TMP_Text 참조입니다.

    [Tooltip("현재 선택/호버된 옵션의 기본값 안내를 표시할 텍스트입니다.")]
    [SerializeField] private TMP_Text _descriptionDefaultText; // 우측 설명 패널 기본값 안내를 출력할 TMP_Text 참조입니다.

    [Tooltip("호버된 Row가 없을 때 표시할 기본 제목입니다.")]
    [SerializeField] private string _defaultTitle = "옵션 설명"; // Hover 대상이 없을 때 표시할 기본 제목 문자열입니다.

    [Tooltip("호버된 Row가 없을 때 표시할 기본 본문입니다.")]
    [TextArea(2, 6)]
    [SerializeField] private string _defaultBody = "옵션 항목에 마우스를 올리면 설명이 표시됩니다."; // Hover 대상이 없을 때 표시할 기본 설명 문자열입니다.

    [Tooltip("호버된 Row가 없을 때 표시할 기본값 안내 텍스트입니다.")]
    [SerializeField] private string _defaultValueGuide = string.Empty; // Hover 대상이 없을 때 표시할 기본값 안내 문자열입니다.

    [Header("Row Source")]
    [Tooltip("OptionRowBindingAnchor를 수집할 루트입니다. 비워두면 현재 오브젝트를 루트로 사용합니다.")]
    [SerializeField] private Transform _anchorsRoot; // 설명 대상 Row 앵커를 자동 수집할 루트 Transform입니다.

    [Tooltip("비활성 오브젝트의 Row 앵커도 설명 대상에 포함할지 여부입니다.")]
    [SerializeField] private bool _includeInactiveAnchors = true; // 자동 수집 시 비활성 Row를 포함할지 여부입니다.

    private readonly List<OptionRowHoverRelay> _hoverRelays = new List<OptionRowHoverRelay>(); // 등록된 hover 릴레이 컴포넌트 목록입니다.
    private bool _hasLastFocusedBindingKey; // 마지막으로 hover 진입한 Row의 BindingKey 기록 여부입니다.
    private E_OptionBindingKey _lastFocusedBindingKey; // 마지막으로 hover 진입한 Row의 BindingKey 값입니다.

    /// <summary>
    /// 시작 시 Row hover 릴레이를 구성하고 기본 설명을 표시합니다.
    /// </summary>
    private void Awake()
    {
        RebuildHoverRelayBindings();
        ShowDefaultDescription();
    }

    /// <summary>
    /// OnEnable 시 기본 설명 패널 상태를 다시 적용합니다.
    /// </summary>
    private void OnEnable()
    {
        ShowDefaultDescription();
    }

    /// <summary>
    /// 등록된 hover 릴레이 리스너를 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        for (int i = 0; i < _hoverRelays.Count; i++)
        {
            OptionRowHoverRelay relay = _hoverRelays[i]; // 해제 대상 hover 릴레이 컴포넌트입니다.
            if (relay == null)
            {
                continue;
            }

            relay.SetEnterListener(null);
            relay.SetExitListener(null);
        }

        _hoverRelays.Clear();
    }

    /// <summary>
    /// 앵커 목록을 순회하며 hover 릴레이를 부착하고 설명 콜백을 등록합니다.
    /// </summary>
    public void RebuildHoverRelayBindings()
    {
        for (int i = 0; i < _hoverRelays.Count; i++)
        {
            OptionRowHoverRelay relay = _hoverRelays[i]; // 기존 등록 해제를 수행할 hover 릴레이 컴포넌트입니다.
            if (relay == null)
            {
                continue;
            }

            relay.SetEnterListener(null);
            relay.SetExitListener(null);
        }

        _hoverRelays.Clear();

        Transform root = _anchorsRoot != null ? _anchorsRoot : transform; // 앵커 수집 시작 루트 Transform입니다.
        OptionRowBindingAnchor[] anchors = root.GetComponentsInChildren<OptionRowBindingAnchor>(_includeInactiveAnchors); // 루트 하위에서 찾은 Row 바인딩 앵커 목록입니다.
        for (int i = 0; i < anchors.Length; i++)
        {
            OptionRowBindingAnchor anchor = anchors[i]; // hover 이벤트를 연결할 현재 Row 앵커입니다.
            if (anchor == null || anchor.BindingEntry == null || anchor.BindingEntry.IsActive == false)
            {
                continue;
            }

            Transform hoverTarget = anchor.BindingEntry.DescriptionHoverTarget != null
                ? anchor.BindingEntry.DescriptionHoverTarget
                : anchor.transform; // 설명 hover 이벤트를 받을 대상 Transform입니다.

            OptionRowHoverRelay relay = hoverTarget.GetComponent<OptionRowHoverRelay>(); // hover 대상에서 재사용하거나 신규 추가할 릴레이 컴포넌트입니다.
            if (relay == null)
            {
                relay = hoverTarget.gameObject.AddComponent<OptionRowHoverRelay>();
            }

            string title = string.IsNullOrWhiteSpace(anchor.BindingEntry.DescriptionTitle)
                ? anchor.BindingEntry.BindingKey.ToString()
                : anchor.BindingEntry.DescriptionTitle; // hover 시 제목 텍스트로 사용할 문자열입니다.
            string body = string.IsNullOrWhiteSpace(anchor.BindingEntry.DescriptionBody)
                ? "설명이 설정되지 않았습니다."
                : anchor.BindingEntry.DescriptionBody; // hover 시 본문 텍스트로 사용할 문자열입니다.
            string defaultText = string.IsNullOrWhiteSpace(anchor.BindingEntry.DescriptionDefaultText)
                ? "기본값 안내가 설정되지 않았습니다."
                : anchor.BindingEntry.DescriptionDefaultText; // hover 시 기본값 안내 텍스트로 사용할 문자열입니다.
            E_OptionBindingKey bindingKey = anchor.BindingEntry.BindingKey; // hover 진입 시 마지막 선택 대상으로 기록할 바인딩 키입니다.

            relay.SetEnterListener(() => ShowDescriptionForBinding(bindingKey, title, body, defaultText));
            relay.SetExitListener(HandleRowHoverExit);
            _hoverRelays.Add(relay);
        }
    }

    /// <summary>
    /// Row hover 이탈 시 현재 설명을 유지하고 추가 동작을 수행하지 않습니다.
    /// </summary>
    private void HandleRowHoverExit()
    {
    }

    /// <summary>
    /// 기본 설명 제목/본문을 설명 패널에 표시합니다.
    /// </summary>
    public void ShowDefaultDescription()
    {
        ShowDescription(_defaultTitle, _defaultBody, _defaultValueGuide);
    }

    /// <summary>
    /// 바인딩 키와 함께 전달받은 설명 텍스트를 패널에 표시하고 마지막 선택 키를 기록합니다.
    /// </summary>
    public void ShowDescriptionForBinding(E_OptionBindingKey bindingKey, string title, string body, string defaultText)
    {
        _hasLastFocusedBindingKey = true;
        _lastFocusedBindingKey = bindingKey;
        ShowDescription(title, body, defaultText);
    }

    /// <summary>
    /// 전달받은 제목/본문/기본값 안내 텍스트를 설명 패널에 표시합니다.
    /// </summary>
    public void ShowDescription(string title, string body, string defaultText)
    {
        if (_descriptionTitleText != null)
        {
            _descriptionTitleText.text = title;
        }

        if (_descriptionBodyText != null)
        {
            _descriptionBodyText.text = body;
        }

        if (_descriptionDefaultText != null)
        {
            _descriptionDefaultText.text = defaultText;
        }
    }

    /// <summary>
    /// 마지막 hover 진입 Row의 BindingKey를 조회합니다.
    /// </summary>
    public bool TryGetLastFocusedBindingKey(out E_OptionBindingKey bindingKey)
    {
        bindingKey = _lastFocusedBindingKey;
        return _hasLastFocusedBindingKey;
    }
}
