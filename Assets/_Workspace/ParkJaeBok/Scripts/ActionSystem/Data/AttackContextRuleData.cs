using UnityEngine;

/// <summary>
/// 공격 입력을 어떤 액션으로 해석할지 결정하는 단일 맥락 규칙 데이터입니다.
/// </summary>
[System.Serializable]
public struct AttackContextRuleData
{
    [Tooltip("인스펙터와 로그에서 규칙을 식별하기 위한 표시 이름")]
    public string Name; // 인스펙터와 로그에서 규칙을 식별하기 위한 표시 이름
    [Tooltip("현재 규칙의 활성화 여부")]
    public bool Enabled; // 현재 규칙의 활성화 여부
    [Tooltip("규칙 우선순위(높을수록 우선)")]
    public int Priority; // 규칙 우선순위(높을수록 우선)
    [Tooltip("반드시 포함되어야 하는 E_AttackContextFlags")]
    public E_AttackContextFlags RequiredFlags; // 반드시 포함되어야 하는 E_AttackContextFlags
    [Tooltip("포함되면 규칙이 무효가 되는 E_AttackContextFlags")]
    public E_AttackContextFlags ExcludedFlags; // 포함되면 규칙이 무효가 되는 E_AttackContextFlags
    [Tooltip("현재 재생 중인 액션이 이 목록에 포함될 때만 적용(비어 있으면 제한 없음)")]
    public E_ActionType[] AllowedCurrentActions; // 현재 재생 중인 액션이 이 목록에 포함될 때만 적용(비어 있으면 제한 없음)
    [Tooltip("콤보 입력 Window가 열려 있을 때만 규칙 적용 여부")]
    public bool RequireComboWindowOpen; // 콤보 입력 Window가 열려 있을 때만 규칙 적용 여부
    [Tooltip("히트 판정 Window가 열려 있을 때만 규칙 적용 여부")]
    public bool RequireHitWindowOpen; // 히트 판정 Window가 열려 있을 때만 규칙 적용 여부
    [Tooltip("규칙 일치 시 공격 입력을 막을지 여부")]
    public bool BlockAttack; // 규칙 일치 시 공격 입력을 막을지 여부
    [Tooltip("BlockAttack 시 입력을 버퍼링할지 여부")]
    public bool BufferWhenBlocked; // BlockAttack 시 입력을 버퍼링할지 여부
    [Tooltip("규칙 일치 시 요청할 액션 타입")]
    public E_ActionType OutputActionType; // 규칙 일치 시 요청할 액션 타입
}
