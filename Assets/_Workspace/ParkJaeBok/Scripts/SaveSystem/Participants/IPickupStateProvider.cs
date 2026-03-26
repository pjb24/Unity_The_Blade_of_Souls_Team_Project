/// <summary>
/// 월드 픽업 상태를 저장/복원 가능한 형태로 제공하는 표준 인터페이스입니다.
/// </summary>
public interface IPickupStateProvider
{
    /// <summary>
    /// 픽업 오브젝트 고유 식별자를 반환합니다.
    /// </summary>
    string PickupId { get; }

    /// <summary>
    /// 픽업 아이템 종류 식별자를 반환합니다.
    /// </summary>
    string ItemType { get; }

    /// <summary>
    /// 픽업 기본 수량을 반환합니다.
    /// </summary>
    int DefaultQuantity { get; }

    /// <summary>
    /// 현재 획득 상태를 반환합니다.
    /// </summary>
    bool IsCollected { get; }

    /// <summary>
    /// 현재 수량 상태를 반환합니다.
    /// </summary>
    int CurrentQuantity { get; }

    /// <summary>
    /// 획득 상태와 수량 상태를 복원 적용합니다.
    /// </summary>
    void ApplyRestoredState(bool isCollected, int quantity);
}
