using UnityEngine;

/// <summary>
/// ActionAnimationPresenter가 액션 타입별 Animator 상태를 조회할 때 사용하는 단일 매핑 데이터입니다.
/// </summary>
[System.Serializable]
public struct AnimationStateMapData
{
    public E_ActionType ActionType; // 매핑 대상 액션 타입
    public string StateName; // Animator 상태 이름
    public int LayerIndex; // 재생할 Animator 레이어 인덱스
    public bool IsOneShot; // 1회 재생 애니메이션 여부
}
