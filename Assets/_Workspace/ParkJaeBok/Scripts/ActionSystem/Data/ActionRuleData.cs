using UnityEngine;

/// <summary>
/// ActionController가 액션 허용/인터럽트/자동완료를 판정할 때 사용하는 단일 Rule 데이터입니다.
/// </summary>
[System.Serializable]
public struct ActionRuleData
{
    [Tooltip("인스펙터에서 구분하기 위한 규칙 이름입니다.")]
    public string Name; // 인스펙터에서 표시될 이름
    [Tooltip("규칙이 적용될 액션 타입입니다.")]
    public E_ActionType ActionType; // 규칙이 적용될 액션 타입
    [Tooltip("현재 프로필에서 해당 액션을 사용할지 여부입니다.")]
    public bool Enabled; // 현재 프로필에서 액션 허용 여부
    [Tooltip("액션 우선순위입니다. 값이 높을수록 우선합니다.")]
    public int Priority; // 액션 우선순위(높을수록 우선)
    [Tooltip("해당 액션이 다른 액션에 의해 인터럽트 가능한지 여부입니다.")]
    public bool IsInterruptible; // 다른 액션에 의해 인터럽트 가능한지 여부
    [Tooltip("자동 완료 시간(초)입니다. 0 이하이면 자동 완료를 사용하지 않습니다.")]
    public float AutoCompleteSeconds; // 자동 완료 시간(0 이하이면 자동 완료 비활성)
    [Tooltip("이 값이 켜져 있으면 UseDefaultMovementLockSetting 대신 아래 LockMovementDuringAction 값을 사용합니다.")]
    public bool OverrideMovementLockSetting; // 이동 잠금 설정을 액션별로 명시적으로 오버라이드할지 여부
    [Tooltip("공격 중 이동 잠금 등 액션 실행 중 이동 잠금을 적용할지 여부입니다. OverrideMovementLockSetting이 켜져 있을 때만 사용됩니다.")]
    public bool LockMovementDuringAction; // 액션 실행 중 이동 잠금을 적용할지 여부
}
