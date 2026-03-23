using UnityEngine;

/// <summary>
/// 액션 인터럽트 허용/거부 정책을 데이터로 제공하는 ScriptableObject 프로필입니다.
/// </summary>
[CreateAssetMenu(fileName = "ActionInterruptPolicyProfile", menuName = "ActionSystem/Action Interrupt Policy Profile")]
public class ActionInterruptPolicyProfile : ScriptableObject
{
    [Tooltip("현재/요청 액션 조합별 인터럽트 정책 규칙 배열")]
    [SerializeField] private ActionInterruptRuleData[] _rules = new ActionInterruptRuleData[0]; // 현재/요청 액션 조합별 인터럽트 정책 규칙 배열

    /// <summary>
    /// 인터럽트 정책 규칙 배열을 반환합니다.
    /// </summary>
    public ActionInterruptRuleData[] Rules => _rules;
}
