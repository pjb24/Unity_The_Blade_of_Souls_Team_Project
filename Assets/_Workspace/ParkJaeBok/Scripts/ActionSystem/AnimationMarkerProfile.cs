using UnityEngine;

/// <summary>
/// ActionController가 사용할 Animation Event marker 매핑 세트를 보관하는 ScriptableObject 프로필입니다.
/// </summary>
[CreateAssetMenu(fileName = "AnimationMarkerProfile", menuName = "ActionSystem/Animation Marker Profile")]
public class AnimationMarkerProfile : ScriptableObject
{
    [SerializeField] private AnimationMarkerMapData[] _markerMaps = new AnimationMarkerMapData[0]; // 프로필에서 제공하는 marker-명령 매핑 배열

    /// <summary>
    /// 프로필에 저장된 marker-명령 매핑 배열을 반환합니다.
    /// </summary>
    public AnimationMarkerMapData[] MarkerMaps => _markerMaps;
}
