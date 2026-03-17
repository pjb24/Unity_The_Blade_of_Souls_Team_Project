using UnityEngine;

/// <summary>
/// ActionAnimationPresenter가 재사용할 액션-애니메이션 상태 매핑 세트를 보관하는 ScriptableObject 프로필입니다.
/// </summary>
[CreateAssetMenu(fileName = "AnimationStateMapProfile", menuName = "ActionSystem/Animation State Map Profile")]
public class AnimationStateMapProfile : ScriptableObject
{
    [SerializeField] private AnimationStateMapData[] _stateMaps = new AnimationStateMapData[0]; // 프로필에서 제공하는 액션-애니메이션 매핑 배열

    /// <summary>
    /// 프로필에 저장된 액션-애니메이션 매핑 배열을 반환합니다.
    /// </summary>
    public AnimationStateMapData[] StateMaps => _stateMaps;
}
