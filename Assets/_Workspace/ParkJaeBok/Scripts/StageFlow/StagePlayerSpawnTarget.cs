using UnityEngine;

/// <summary>
/// StageSpawnResolver가 플레이어 스폰 대상을 명확히 식별하기 위한 마커 컴포넌트입니다.
/// </summary>
public class StagePlayerSpawnTarget : MonoBehaviour
{
    [Tooltip("여러 StagePlayerSpawnTarget이 있을 때 기본 우선 대상으로 취급할지 여부입니다.")]
    [SerializeField] private bool _isPrimary = true; // 다중 후보 상황에서 1순위로 고려할 대상인지 여부입니다.

    [Tooltip("우선순위 비교 값입니다. 값이 클수록 우선됩니다.")]
    [SerializeField] private int _priority = 0; // 동일 조건 후보 간 정렬에 사용할 우선순위 값입니다.

    [Tooltip("플레이어 루트와 스폰 기준점이 다를 때 사용할 대체 Transform입니다. 비워두면 자기 Transform을 사용합니다.")]
    [SerializeField] private Transform _spawnPivot; // 스폰 이동 시 실제로 위치를 맞출 기준 Transform입니다.

    /// <summary>
    /// 기본 우선 대상 여부를 반환합니다.
    /// </summary>
    public bool IsPrimary => _isPrimary;

    /// <summary>
    /// 우선순위 값을 반환합니다.
    /// </summary>
    public int Priority => _priority;

    /// <summary>
    /// 실제 스폰 이동에 사용할 Transform을 반환합니다.
    /// </summary>
    public Transform GetSpawnTransform()
    {
        return _spawnPivot != null ? _spawnPivot : transform;
    }
}
