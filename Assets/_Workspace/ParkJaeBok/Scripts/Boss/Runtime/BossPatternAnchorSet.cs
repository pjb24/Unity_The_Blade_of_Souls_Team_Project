using UnityEngine;

/// <summary>
/// Holds scene references used by boss pattern execution while keeping ScriptableObject data pure.
/// </summary>
[DisallowMultipleComponent]
public class BossPatternAnchorSet : MonoBehaviour
{
    [Header("Projectile Anchors")]
    [Tooltip("Scene spawn points used by projectile boss patterns.")]
    [SerializeField] private Transform[] _projectileSpawnPoints = new Transform[0]; // Scene projectile spawn point references used by future pattern runners.

    [Header("Monster Anchors")]
    [Tooltip("Scene spawn points used by monster summon boss patterns.")]
    [SerializeField] private Transform[] _monsterSpawnPoints = new Transform[0]; // Scene monster spawn point references used by future summon runners.

    [Header("Weak Point Areas")]
    [Tooltip("Scene BoxCollider2D areas used by weak point boss patterns.")]
    [SerializeField] private BoxCollider2D[] _weakPointAreas = new BoxCollider2D[0]; // Scene weak point area references used by future weak point runners.

    /// <summary>
    /// Gets the projectile spawn point array.
    /// </summary>
    public Transform[] ProjectileSpawnPoints => _projectileSpawnPoints;

    /// <summary>
    /// Gets the monster spawn point array.
    /// </summary>
    public Transform[] MonsterSpawnPoints => _monsterSpawnPoints;

    /// <summary>
    /// Gets the weak point area array.
    /// </summary>
    public BoxCollider2D[] WeakPointAreas => _weakPointAreas;

    /// <summary>
    /// Gets the number of projectile spawn points assigned in the scene.
    /// </summary>
    public int ProjectileSpawnPointCount => _projectileSpawnPoints != null ? _projectileSpawnPoints.Length : 0;

    /// <summary>
    /// Gets the number of monster spawn points assigned in the scene.
    /// </summary>
    public int MonsterSpawnPointCount => _monsterSpawnPoints != null ? _monsterSpawnPoints.Length : 0;

    /// <summary>
    /// Gets the number of weak point areas assigned in the scene.
    /// </summary>
    public int WeakPointAreaCount => _weakPointAreas != null ? _weakPointAreas.Length : 0;

    /// <summary>
    /// Attempts to resolve a projectile spawn point by index.
    /// </summary>
    public bool TryGetProjectileSpawnPoint(int index, out Transform spawnPoint)
    {
        return TryGetTransform(_projectileSpawnPoints, index, "ProjectileSpawnPoint", out spawnPoint);
    }

    /// <summary>
    /// Attempts to resolve a monster spawn point by index.
    /// </summary>
    public bool TryGetMonsterSpawnPoint(int index, out Transform spawnPoint)
    {
        return TryGetTransform(_monsterSpawnPoints, index, "MonsterSpawnPoint", out spawnPoint);
    }

    /// <summary>
    /// Attempts to resolve a weak point area by index.
    /// </summary>
    public bool TryGetWeakPointArea(int index, out BoxCollider2D weakPointArea)
    {
        weakPointArea = null;
        if (_weakPointAreas == null)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] WeakPointArea array is null on {name}.", this);
            return false;
        }

        if (index < 0 || index >= _weakPointAreas.Length)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] WeakPointArea index out of range. object={name}, index={index}, count={_weakPointAreas.Length}", this);
            return false;
        }

        weakPointArea = _weakPointAreas[index];
        if (weakPointArea == null)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] WeakPointArea reference is null. object={name}, index={index}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Attempts to resolve a Transform from the given anchor array by index.
    /// </summary>
    private bool TryGetTransform(Transform[] anchors, int index, string label, out Transform anchor)
    {
        anchor = null;
        if (anchors == null)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] {label} array is null on {name}.", this);
            return false;
        }

        if (index < 0 || index >= anchors.Length)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] {label} index out of range. object={name}, index={index}, count={anchors.Length}", this);
            return false;
        }

        anchor = anchors[index];
        if (anchor == null)
        {
            Debug.LogWarning($"[BossPatternAnchorSet] {label} reference is null. object={name}, index={index}", this);
            return false;
        }

        return true;
    }
}
