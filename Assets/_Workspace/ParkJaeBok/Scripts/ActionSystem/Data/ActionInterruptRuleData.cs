using UnityEngine;

/// <summary>
/// 현재 액션과 요청 액션 조합별 인터럽트 허용 정책을 정의하는 단일 규칙 데이터입니다.
/// </summary>
[System.Serializable]
public struct ActionInterruptRuleData
{
    [Tooltip("인스펙터와 로그에서 규칙을 식별하기 위한 표시 이름")]
    public string Name; // 인스펙터와 로그에서 규칙을 식별하기 위한 표시 이름
    [Tooltip("현재 규칙 활성화 여부")]
    public bool Enabled; // 현재 규칙 활성화 여부
    [Tooltip("규칙 우선순위(높을수록 우선)")]
    public int Priority; // 규칙 우선순위(높을수록 우선)
    [Tooltip("현재 실행 중인 액션 필터(None이면 와일드카드)")]
    public E_ActionType CurrentActionType; // 현재 실행 액션 필터(None이면 와일드카드)
    [Tooltip("요청 액션 필터(None이면 와일드카드)")]
    public E_ActionType RequestedActionType; // 요청 액션 필터(None이면 와일드카드)
    [Tooltip("규칙 일치 시 적용할 허용/거부/기본 로직 위임 결정")]
    public E_ActionInterruptDecision Decision; // 규칙 일치 시 적용할 허용/거부/기본 로직 위임 결정
    [Tooltip("콤보 입력 Window가 열려 있을 때만 규칙 적용 여부")]
    public bool RequireComboWindowOpen; // 콤보 입력 Window가 열려 있을 때만 규칙 적용 여부
    [Tooltip("히트 판정 Window가 열려 있을 때만 규칙 적용 여부")]
    public bool RequireHitWindowOpen; // 히트 판정 Window가 열려 있을 때만 규칙 적용 여부
    [Tooltip("현재 액션 단계 일치 조건을 사용할지 여부")]
    public bool RequireCurrentPhaseMatch; // 현재 액션 단계 일치 조건을 사용할지 여부
    [Tooltip("RequireCurrentPhaseMatch가 true일 때 요구되는 현재 액션 단계")]
    public E_ActionPhase RequiredCurrentPhase; // RequireCurrentPhaseMatch가 true일 때 요구되는 현재 액션 단계
}
