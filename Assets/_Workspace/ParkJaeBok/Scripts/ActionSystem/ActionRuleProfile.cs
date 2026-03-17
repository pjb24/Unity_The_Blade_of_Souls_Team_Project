using UnityEngine;

/// <summary>
/// 액터 타입별 Action Rule 세트를 자산으로 보관해 재사용하기 위한 ScriptableObject 프로필입니다.
/// </summary>
[CreateAssetMenu(fileName = "ActionRuleProfile", menuName = "ActionSystem/Action Rule Profile")]
public class ActionRuleProfile : ScriptableObject
{
    [SerializeField] private E_ActionActorType _actorType = E_ActionActorType.Player; // 이 Rule 프로필이 대상으로 하는 액터 타입
    [SerializeField] private ActionRuleData[] _rules = new ActionRuleData[0]; // 프로필에서 제공하는 액션 Rule 목록

    /// <summary>
    /// 이 프로필의 대상 액터 타입을 반환합니다.
    /// </summary>
    public E_ActionActorType ActorType => _actorType;

    /// <summary>
    /// 이 프로필의 액션 Rule 배열을 반환합니다.
    /// </summary>
    public ActionRuleData[] Rules => _rules;
}
