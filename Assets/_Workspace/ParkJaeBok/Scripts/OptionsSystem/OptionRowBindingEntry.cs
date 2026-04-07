using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 옵션 1개 Row를 구성하는 Inspector 바인딩 설정입니다.
/// </summary>
[Serializable]
public class OptionRowBindingEntry
{
    [Tooltip("이 Row가 담당할 옵션 키입니다.")]
    [SerializeField] private E_OptionBindingKey _bindingKey = E_OptionBindingKey.None; // 이 Row가 다루는 OptionSaveData 필드 식별자입니다.

    [Tooltip("이 Row가 사용하는 UI 위젯 타입입니다.")]
    [SerializeField] private E_OptionBindingWidgetType _widgetType = E_OptionBindingWidgetType.Enum; // Row 입력 위젯 타입 식별자입니다.

    [Header("Enum Widget")]
    [Tooltip("Enum 타입 Row에서 현재 값을 표시할 텍스트입니다.")]
    [SerializeField] private TMP_Text _enumValueText; // Enum 선택값을 화면에 표시하는 텍스트 참조입니다.

    [Tooltip("Enum 타입 Row에서 이전 값을 선택할 버튼입니다.")]
    [SerializeField] private Button _enumPrevButton; // Enum 인덱스를 감소시키는 버튼 참조입니다.

    [Tooltip("Enum 타입 Row에서 다음 값을 선택할 버튼입니다.")]
    [SerializeField] private Button _enumNextButton; // Enum 인덱스를 증가시키는 버튼 참조입니다.

    [Tooltip("Enum 인덱스별로 표시할 라벨 목록입니다. 비어 있으면 인덱스 숫자를 표시합니다.")]
    [SerializeField] private string[] _enumDisplayLabels = Array.Empty<string>(); // Enum 인덱스에 대응하는 표시 문자열 배열입니다.

    [Header("Numeric Widget")]
    [Tooltip("Numeric 타입 Row에서 값 진행도를 표시할 Fill 이미지입니다.")]
    [SerializeField] private Image _numericFillImage; // Numeric 값을 시각화할 게이지 Fill 이미지 참조입니다.

    [Tooltip("Numeric 타입 Row에서 현재 값을 표시할 텍스트입니다.")]
    [SerializeField] private TMP_Text _numericValueText; // Numeric 현재 값을 표시하는 텍스트 참조입니다.

    [Tooltip("Numeric 타입 Row에서 값을 감소시키는 버튼입니다.")]
    [SerializeField] private Button _numericDecreaseButton; // Numeric 값을 Step 단위로 감소시키는 버튼 참조입니다.

    [Tooltip("Numeric 타입 Row에서 값을 증가시키는 버튼입니다.")]
    [SerializeField] private Button _numericIncreaseButton; // Numeric 값을 Step 단위로 증가시키는 버튼 참조입니다.

    [Tooltip("Numeric 타입 Row에서 클릭/드래그 입력을 받을 영역 RectTransform입니다. 비어 있으면 Fill 이미지 RectTransform을 사용합니다.")]
    [SerializeField] private RectTransform _numericPointerInputArea; // Numeric 값을 포인터 입력으로 변경할 클릭/드래그 영역 참조입니다.

    [Tooltip("Numeric 값의 최소 범위입니다.")]
    [SerializeField] private float _numericMinValue = 0f; // Numeric 값 하한선입니다.

    [Tooltip("Numeric 값의 최대 범위입니다.")]
    [SerializeField] private float _numericMaxValue = 1f; // Numeric 값 상한선입니다.

    [Tooltip("Numeric 버튼 증감 시 사용할 Step 값입니다.")]
    [SerializeField] private float _numericStep = 0.1f; // Numeric 버튼 입력 시 증가/감소 단위입니다.

    [Tooltip("Image Type이 Filled가 아닐 때 사용할 최대 Fill Width(px)입니다.")]
    [SerializeField] private float _numericFillMaxWidth = 160f; // 이미지 너비 기반 표시 모드에서 최대 Fill 폭입니다.

    [Tooltip("Action 타입 Row에서 사용할 Button 참조입니다.")]
    [SerializeField] private Button _actionWidget; // Action 실행 트리거용 Button 참조입니다.

    [Tooltip("이 Row를 런타임 바인딩 대상에 포함할지 여부입니다.")]
    [SerializeField] private bool _isActive = true; // 런타임에 이 Row를 활성 바인딩 대상으로 처리할지 여부입니다.

    [Header("Description")]
    [Tooltip("우측 설명 패널에 표시할 Row 제목입니다.")]
    [SerializeField] private string _descriptionTitle = string.Empty; // Row hover 시 설명 패널 제목으로 표시할 문자열입니다.

    [Tooltip("우측 설명 패널에 표시할 Row 상세 설명입니다.")]
    [TextArea(2, 6)]
    [SerializeField] private string _descriptionBody = string.Empty; // Row hover 시 설명 패널 본문으로 표시할 문자열입니다.

    [Tooltip("우측 설명 패널에 표시할 기본값 안내 텍스트입니다. 예: 기본값 100%.")]
    [SerializeField] private string _descriptionDefaultText = string.Empty; // Row hover 시 기본값 안내로 표시할 문자열입니다.

    [Tooltip("설명 표시 트리거로 사용할 Hover 대상 Transform입니다. 비우면 Anchor 오브젝트를 사용합니다.")]
    [SerializeField] private Transform _descriptionHoverTarget; // Row hover 진입/이탈 이벤트를 받을 대상 Transform 참조입니다.

    public E_OptionBindingKey BindingKey => _bindingKey;
    public E_OptionBindingWidgetType WidgetType => _widgetType;
    public TMP_Text EnumValueText => _enumValueText;
    public Button EnumPrevButton => _enumPrevButton;
    public Button EnumNextButton => _enumNextButton;
    public string[] EnumDisplayLabels => _enumDisplayLabels;
    public Image NumericFillImage => _numericFillImage;
    public TMP_Text NumericValueText => _numericValueText;
    public Button NumericDecreaseButton => _numericDecreaseButton;
    public Button NumericIncreaseButton => _numericIncreaseButton;
    public RectTransform NumericPointerInputArea => _numericPointerInputArea;
    public float NumericMinValue => _numericMinValue;
    public float NumericMaxValue => _numericMaxValue;
    public float NumericStep => _numericStep;
    public float NumericFillMaxWidth => _numericFillMaxWidth;
    public Button ActionWidget => _actionWidget;
    public bool IsActive => _isActive;
    public string DescriptionTitle => _descriptionTitle;
    public string DescriptionBody => _descriptionBody;
    public string DescriptionDefaultText => _descriptionDefaultText;
    public Transform DescriptionHoverTarget => _descriptionHoverTarget;
}
