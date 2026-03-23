using UnityEngine;

/// <summary>
/// 상황별 공격 액션 결정 규칙을 제공하는 ScriptableObject 프로필입니다.
/// </summary>
[CreateAssetMenu(menuName = "ActionSystem/Attack Context Rule Profile", fileName = "AttackContextRuleProfile")]
public class AttackContextRuleProfile : ScriptableObject
{
    [Tooltip("어떤 규칙도 일치하지 않을 때 요청할 기본 공격 액션")]
    [SerializeField] private E_ActionType _fallbackActionType = E_ActionType.Attack; // 어떤 규칙도 일치하지 않을 때 요청할 기본 공격 액션
    [Tooltip("입력이 즉시 처리되지 못했을 때 재시도할 버퍼 유지 시간")]
    [SerializeField] private float _attackInputBufferSeconds = 0.15f; // 입력이 즉시 처리되지 못했을 때 재시도할 버퍼 유지 시간
    [Tooltip("우선순위 기반으로 평가할 상황별 공격 규칙 배열")]
    [SerializeField] private AttackContextRuleData[] _rules = new AttackContextRuleData[0]; // 우선순위 기반으로 평가할 상황별 공격 규칙 배열

    /// <summary>
    /// 규칙 미일치 시 사용할 기본 공격 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType FallbackActionType => _fallbackActionType;

    /// <summary>
    /// 입력 버퍼 유지 시간을 반환합니다.
    /// </summary>
    public float AttackInputBufferSeconds => _attackInputBufferSeconds;

    /// <summary>
    /// 상황별 공격 규칙 배열을 반환합니다.
    /// </summary>
    public AttackContextRuleData[] Rules => _rules;
}
