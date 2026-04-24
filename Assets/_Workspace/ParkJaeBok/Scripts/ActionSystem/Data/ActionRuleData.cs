using UnityEngine;

/// <summary>
/// ActionController가 액션 허용/인터럽트/자동완료를 판정할 때 사용하는 단일 Rule 데이터입니다.
/// </summary>
[System.Serializable]
public struct ActionRuleData
{
    public string Name; // 인스펙터에서 표시될 이름
    public E_ActionType ActionType; // 규칙이 적용될 액션 타입
    public bool Enabled; // 현재 프로필에서 액션 허용 여부
    public int Priority; // 액션 우선순위(높을수록 우선)
    public bool IsInterruptible; // 다른 액션에 의해 인터럽트 가능한지 여부
    public float AutoCompleteSeconds; // 자동 완료 시간(0 이하이면 자동 완료 비활성)
}
