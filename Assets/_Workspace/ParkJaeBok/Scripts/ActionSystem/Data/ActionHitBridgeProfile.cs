using UnityEngine;

/// <summary>
/// ActionHitBridge가 HitCore 결과를 액션 요청으로 변환할 때 사용하는 ScriptableObject 프로필입니다.
/// </summary>
[CreateAssetMenu(fileName = "ActionHitBridgeProfile", menuName = "ActionSystem/Action Hit Bridge Profile")]
public class ActionHitBridgeProfile : ScriptableObject
{
    [SerializeField] private E_ActionType _hitActionType = E_ActionType.Hit; // 일반 피격 시 요청할 액션 타입
    [SerializeField] private E_ActionType _breakActionType = E_ActionType.Break; // 강인도 감쇄/브레이크 시 요청할 액션 타입
    [SerializeField] private E_ActionType _deadActionType = E_ActionType.Die; // 사망 시 요청할 액션 타입
    [SerializeField] private string[] _breakStatusTags = new string[] { "PoiseBreak", "GuardBreak", "Break" }; // 브레이크로 해석할 StatusTag 문자열 목록

    /// <summary>
    /// 일반 피격 시 요청할 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType HitActionType => _hitActionType;

    /// <summary>
    /// 브레이크 시 요청할 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType BreakActionType => _breakActionType;

    /// <summary>
    /// 사망 시 요청할 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType DeadActionType => _deadActionType;

    /// <summary>
    /// 브레이크 판정에 사용할 StatusTag 배열을 반환합니다.
    /// </summary>
    public string[] BreakStatusTags => _breakStatusTags;
}
