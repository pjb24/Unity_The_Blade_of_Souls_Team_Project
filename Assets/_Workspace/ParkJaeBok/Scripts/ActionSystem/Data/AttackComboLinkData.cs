using UnityEngine;

/// <summary>
/// 콤보 원본 액션과 다음 액션, 입력 예약 허용 시간을 정의하는 링크 데이터입니다.
/// </summary>
[System.Serializable]
public struct AttackComboLinkData
{
    public string Name; // 인스펙터에서 보일 이름
    [Tooltip("현재 콤보 입력을 수집한 원본 액션 타입입니다.")]
    public E_ActionType FromActionType; // 현재 콤보 입력을 수집한 원본 액션 타입입니다.
    [Tooltip("원본 액션이 완료된 뒤 요청할 다음 콤보 액션 타입입니다.")]
    public E_ActionType ToActionType; // 원본 액션이 완료된 뒤 요청할 다음 콤보 액션 타입입니다.
    [Tooltip("원본 액션 완료 시점까지 입력 예약을 유효로 판단할 시간(초)입니다.")]
    [Min(0f)]
    public float InputBufferSeconds; // 원본 액션 완료 시점까지 입력 예약을 유효로 판단할 시간(초)입니다.
}
