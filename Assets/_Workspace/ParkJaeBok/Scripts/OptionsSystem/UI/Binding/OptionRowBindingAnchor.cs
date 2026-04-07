using UnityEngine;

/// <summary>
/// OptionRow 단위 바인딩 설정을 해당 Row 오브젝트에 붙여 관리하기 위한 앵커 컴포넌트입니다.
/// </summary>
public class OptionRowBindingAnchor : MonoBehaviour
{
    [Tooltip("이 Row에 적용할 바인딩 설정입니다. Row별로 BindingKey/Widget 참조를 로컬에서 관리합니다.")]
    [SerializeField] private OptionRowBindingEntry _bindingEntry = new OptionRowBindingEntry(); // Row 로컬 바인딩 정보를 담는 설정 객체입니다.

    public OptionRowBindingEntry BindingEntry => _bindingEntry;
}
