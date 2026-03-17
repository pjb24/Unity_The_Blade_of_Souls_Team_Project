using UnityEngine;

/// <summary>
/// ActionController가 Animation Event marker 문자열을 실행 동작으로 변환할 때 사용하는 단일 매핑 데이터입니다.
/// </summary>
[System.Serializable]
public struct AnimationMarkerMapData
{
    public string Marker; // Animation Event에서 전달되는 marker 문자열 원본 값
    public E_AnimationMarkerCommandType CommandType; // marker 수신 시 실행할 명령 타입
    public E_ActionPhase TargetPhase; // Phase 변경 명령일 때 설정할 목표 단계
    public E_ActionType TargetActionType; // 액션 요청 명령일 때 요청할 목표 액션 타입
    public string CancelReason; // 취소 명령일 때 전달할 취소 사유 문자열
}

/// <summary>
/// Animation Event marker가 실행할 명령 타입 정의입니다.
/// </summary>
public enum E_AnimationMarkerCommandType
{
    SetPhase,
    Complete,
    Cancel,
    RequestAction,
}
