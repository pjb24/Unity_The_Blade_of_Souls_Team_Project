/// <summary>
/// 이동 잠금 사유를 원인 기반으로 구분하기 위한 enum입니다.
/// </summary>
public enum E_MovementLockReason
{
    None = 0,
    Attack = 1,
    Hit = 2,
    Die = 3,
    Stun = 4,
    Cutscene = 5,
    UI = 6,
}
