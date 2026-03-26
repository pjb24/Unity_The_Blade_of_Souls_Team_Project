using UnityEngine;

/// <summary>
/// 월드 드랍/픽업 오브젝트의 메타/상태를 저장 시스템과 연결하는 컴포넌트입니다.
/// </summary>
public class WorldPickupState : MonoBehaviour, IPickupStateProvider
{
    [Header("Identity")]
    [Tooltip("월드 픽업 오브젝트를 안정적으로 식별하는 고유 pickupId입니다.")]
    [SerializeField] private string _pickupId = "pickup.default"; // 저장/복원에서 사용할 월드 픽업 고유 식별자입니다.

    [Tooltip("픽업 아이템 종류 식별자입니다. 예: Gold, Potion, Material")]
    [SerializeField] private string _itemType = "Item"; // 종류별 복원 정책 매칭에 사용할 아이템 타입 문자열입니다.

    [Header("State")]
    [Tooltip("픽업의 기본 수량입니다.")]
    [SerializeField] private int _defaultQuantity = 1; // 초기 상태에서의 기본 픽업 수량 값입니다.

    [Tooltip("현재 픽업 수량입니다.")]
    [SerializeField] private int _currentQuantity = 1; // 런타임 저장 대상 현재 픽업 수량 값입니다.

    [Tooltip("현재 픽업이 획득되었는지 여부입니다.")]
    [SerializeField] private bool _isCollected; // 런타임 저장 대상 현재 획득 상태 값입니다.

    public string PickupId => _pickupId;
    public string ItemType => _itemType;
    public int DefaultQuantity => Mathf.Max(0, _defaultQuantity);
    public bool IsCollected => _isCollected;
    public int CurrentQuantity => Mathf.Max(0, _currentQuantity);

    /// <summary>
    /// 획득 처리 상태를 적용합니다.
    /// </summary>
    public void MarkCollected(int consumedQuantity)
    {
        int safeConsumedQuantity = Mathf.Max(0, consumedQuantity); // 음수 입력을 방지한 안전 소비 수량 값입니다.
        _currentQuantity = Mathf.Max(0, _currentQuantity - safeConsumedQuantity);
        _isCollected = _currentQuantity <= 0;
        gameObject.SetActive(!_isCollected);
    }

    /// <summary>
    /// 저장된 획득 상태/수량을 월드 픽업 오브젝트에 복원 적용합니다.
    /// </summary>
    public void ApplyRestoredState(bool isCollected, int quantity)
    {
        _isCollected = isCollected;
        _currentQuantity = Mathf.Max(0, quantity);
        gameObject.SetActive(!_isCollected);
    }

    /// <summary>
    /// 초기 수량과 미획득 상태로 리셋합니다.
    /// </summary>
    public void ResetToDefaultState()
    {
        _isCollected = false;
        _currentQuantity = DefaultQuantity;
        gameObject.SetActive(true);
    }
}
