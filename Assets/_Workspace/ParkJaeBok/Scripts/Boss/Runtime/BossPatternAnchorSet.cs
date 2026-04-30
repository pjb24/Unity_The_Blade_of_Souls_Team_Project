using UnityEngine;

/// <summary>
/// ScriptableObject 데이터를 순수하게 유지하면서 보스 패턴 실행에 사용되는 씬 참조를 보관한다.
/// </summary>
[DisallowMultipleComponent]
public class BossPatternAnchorSet : MonoBehaviour
{
    [Header("투사체 앵커")]
    [Tooltip("투사체 보스 패턴에서 사용하는 씬 생성 위치")]
    [SerializeField] private Transform[] _projectileSpawnPoints = new Transform[0]; // 향후 패턴 실행에서 사용하는 씬 투사체 생성 위치 참조

    [Header("몬스터 앵커")]
    [Tooltip("몬스터 소환 보스 패턴에서 사용하는 씬 생성 위치")]
    [SerializeField] private Transform[] _monsterSpawnPoints = new Transform[0]; // 향후 소환 패턴 실행에서 사용하는 씬 몬스터 생성 위치 참조

    [Header("약점 영역")]
    [Tooltip("약점 보스 패턴에서 사용하는 씬 BoxCollider2D 영역")]
    [SerializeField] private BoxCollider2D[] _weakPointAreas = new BoxCollider2D[0]; // 향후 약점 패턴 실행에서 사용하는 씬 약점 영역 참조

    /// <summary>
    /// 투사체 생성 위치 배열을 반환한다.
    /// </summary>
    public Transform[] ProjectileSpawnPoints => _projectileSpawnPoints;

    /// <summary>
    /// 몬스터 생성 위치 배열을 반환한다.
    /// </summary>
    public Transform[] MonsterSpawnPoints => _monsterSpawnPoints;

    /// <summary>
    /// 약점 영역 배열을 반환한다.
    /// </summary>
    public BoxCollider2D[] WeakPointAreas => _weakPointAreas;

    /// <summary>
    /// 씬에 할당된 투사체 생성 위치 개수를 반환한다.
    /// </summary>
    public int ProjectileSpawnPointCount => _projectileSpawnPoints != null ? _projectileSpawnPoints.Length : 0;

    /// <summary>
    /// 씬에 할당된 몬스터 생성 위치 개수를 반환한다.
    /// </summary>
    public int MonsterSpawnPointCount => _monsterSpawnPoints != null ? _monsterSpawnPoints.Length : 0;

    /// <summary>
    /// 씬에 할당된 약점 영역 개수를 반환한다.
    /// </summary>
    public int WeakPointAreaCount => _weakPointAreas != null ? _weakPointAreas.Length : 0;

    /// <summary>
    /// 인덱스를 사용하여 투사체 생성 위치를 가져오려고 시도한다.
    /// </summary>
    public bool TryGetProjectileSpawnPoint(int index, out Transform spawnPoint)
    {
        return TryGetTransform(_projectileSpawnPoints, index, "ProjectileSpawnPoint", out spawnPoint);
    }

    /// <summary>
    /// 인덱스를 사용하여 몬스터 생성 위치를 가져오려고 시도한다.
    /// </summary>
    public bool TryGetMonsterSpawnPoint(int index, out Transform spawnPoint)
    {
        return TryGetTransform(_monsterSpawnPoints, index, "MonsterSpawnPoint", out spawnPoint);
    }

    /// <summary>
    /// 인덱스를 사용하여 약점 영역을 가져오려고 시도한다.
    /// </summary>
    public bool TryGetWeakPointArea(int index, out BoxCollider2D weakPointArea)
    {
        weakPointArea = null;
        if (_weakPointAreas == null)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] WeakPointArea 배열이 null이다. object={name}", this);
            return false;
        }

        if (index < 0 || index >= _weakPointAreas.Length)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] WeakPointArea 인덱스 범위 초과. object={name}, index={index}, count={_weakPointAreas.Length}", this);
            return false;
        }

        weakPointArea = _weakPointAreas[index];
        if (weakPointArea == null)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] WeakPointArea 참조가 null이다. object={name}, index={index}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 전달된 앵커 배열에서 인덱스를 사용하여 Transform을 가져오려고 시도한다.
    /// </summary>
    private bool TryGetTransform(Transform[] anchors, int index, string label, out Transform anchor)
    {
        anchor = null;
        if (anchors == null)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] {label} 배열이 null이다. object={name}", this);
            return false;
        }

        if (index < 0 || index >= anchors.Length)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] {label} 인덱스 범위 초과. object={name}, index={index}, count={anchors.Length}", this);
            return false;
        }

        anchor = anchors[index];
        if (anchor == null)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] {label} 참조가 null이다. object={name}, index={index}", this);
            return false;
        }

        return true;
    }
}
