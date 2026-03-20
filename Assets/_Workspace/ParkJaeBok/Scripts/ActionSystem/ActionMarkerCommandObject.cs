using UnityEngine;

/// <summary>
/// Animation Event가 전달할 Action 마커 명령 타입을 정의합니다.
/// </summary>
public enum E_ActionMarkerCommandType
{
    CompleteCurrentAction,
    CancelCurrentAction,
    ComboStart,
    ComboEnd,
    HitStart,
    HitEnd,
}

/// <summary>
/// Animation Event에서 Object로 전달할 액션 명령 데이터를 보관하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "ActionMarkerCommand", menuName = "ActionSystem/Action Marker Command")]
public class ActionMarkerCommandObject : ScriptableObject
{
    [SerializeField] private E_ActionMarkerCommandType _commandType = E_ActionMarkerCommandType.ComboStart; // 수신 시 실행할 액션 명령 타입
    [SerializeField] private string _cancelReason = "Animation marker cancel"; // CancelCurrentAction 명령일 때 전달할 취소 사유 문자열

    /// <summary>
    /// 수신 시 실행할 액션 명령 타입을 반환합니다.
    /// </summary>
    public E_ActionMarkerCommandType CommandType => _commandType;

    /// <summary>
    /// CancelCurrentAction 명령일 때 사용할 취소 사유 문자열을 반환합니다.
    /// </summary>
    public string CancelReason => _cancelReason;
}
