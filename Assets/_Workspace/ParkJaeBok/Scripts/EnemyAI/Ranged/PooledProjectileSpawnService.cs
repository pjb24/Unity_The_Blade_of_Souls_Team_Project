using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 투사체 프리팹 단위 풀을 관리하고 생성 요청을 실행하는 서비스입니다.
/// </summary>
[DisallowMultipleComponent]
public class PooledProjectileSpawnService : MonoBehaviour, IProjectileSpawnService
{
    [Header("Pool")]
    [Tooltip("비활성 풀에 오브젝트가 부족할 때 자동 확장을 허용할지 여부입니다.")]
    [SerializeField] private bool _allowAutoExpand = true; // 풀 부족 시 자동 확장 허용 여부입니다.
    [Tooltip("0 이하면 무제한, 1 이상이면 프리팹별 최대 생성 개수 제한입니다.")]
    [SerializeField] private int _maxCountPerPrefab = 0; // 프리팹별 최대 생성 개수 제한 값입니다.

    [Tooltip("프리팹별 비활성 인스턴스 큐를 보관하는 맵입니다.")]
    private readonly Dictionary<GameObject, Queue<PooledRangedProjectile>> _poolByPrefab = new Dictionary<GameObject, Queue<PooledRangedProjectile>>(); // 프리팹별 비활성 큐 맵입니다.
    [Tooltip("프리팹별 총 생성 인스턴스 수를 추적하는 맵입니다.")]
    private readonly Dictionary<GameObject, int> _createdCountByPrefab = new Dictionary<GameObject, int>(); // 프리팹별 생성 누적 개수입니다.
    [Tooltip("활성 인스턴스가 어떤 프리팹에서 생성되었는지 추적하는 맵입니다.")]
    private readonly Dictionary<PooledRangedProjectile, GameObject> _sourcePrefabByInstance = new Dictionary<PooledRangedProjectile, GameObject>(); // 인스턴스-원본 프리팹 역참조 맵입니다.

    /// <summary>
    /// 요청된 스펙으로 투사체를 스폰/재사용합니다.
    /// </summary>
    public void RequestSpawn(GameObject prefab, Vector2 position, Vector2 direction, GameObject owner, float speed, float lifetime)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[PooledProjectileSpawnService] RequestSpawn failed: prefab is null.");
            return;
        }

        PooledRangedProjectile projectile = Acquire(prefab);
        if (projectile == null)
        {
            return;
        }

        projectile.transform.position = position;
        projectile.gameObject.SetActive(true);
        projectile.Initialize(direction, speed, lifetime, owner);
    }

    /// <summary>
    /// 풀에서 사용 가능한 투사체를 가져오거나 필요 시 새로 생성합니다.
    /// </summary>
    private PooledRangedProjectile Acquire(GameObject prefab)
    {
        Queue<PooledRangedProjectile> pool = GetOrCreatePool(prefab);

        while (pool.Count > 0)
        {
            PooledRangedProjectile pooled = pool.Dequeue();
            if (pooled != null)
            {
                return pooled;
            }
        }

        int createdCount = GetCreatedCount(prefab);
        bool hasMaxLimit = _maxCountPerPrefab > 0;
        if (hasMaxLimit && createdCount >= _maxCountPerPrefab)
        {
            Debug.LogWarning($"[PooledProjectileSpawnService] MaxCount exceeded for {prefab.name}. Spawn denied.");
            return null;
        }

        if (!_allowAutoExpand && createdCount > 0)
        {
            Debug.LogWarning($"[PooledProjectileSpawnService] Pool empty for {prefab.name} and auto expand disabled.");
            return null;
        }

        Debug.LogWarning($"[PooledProjectileSpawnService] Pool expanded for {prefab.name}.");
        GameObject created = Instantiate(prefab, transform);
        PooledRangedProjectile createdProjectile = created.GetComponent<PooledRangedProjectile>();
        if (createdProjectile == null)
        {
            createdProjectile = created.AddComponent<PooledRangedProjectile>();
            Debug.LogWarning($"[PooledProjectileSpawnService] Added missing PooledRangedProjectile on {created.name}.");
        }

        createdProjectile.BindReturnHandler(ReturnToPool);
        created.SetActive(false);

        _createdCountByPrefab[prefab] = createdCount + 1;
        _sourcePrefabByInstance[createdProjectile] = prefab;

        return createdProjectile;
    }

    /// <summary>
    /// 사용 종료된 투사체를 비활성화해 풀로 반환합니다.
    /// </summary>
    private void ReturnToPool(PooledRangedProjectile projectile)
    {
        if (projectile == null)
        {
            return;
        }

        if (!_sourcePrefabByInstance.TryGetValue(projectile, out GameObject prefab) || prefab == null)
        {
            Destroy(projectile.gameObject);
            return;
        }

        Queue<PooledRangedProjectile> pool = GetOrCreatePool(prefab);
        projectile.gameObject.SetActive(false);
        projectile.transform.SetParent(transform);
        pool.Enqueue(projectile);
    }

    /// <summary>
    /// 지정 프리팹의 풀 큐를 가져오거나 새로 생성합니다.
    /// </summary>
    private Queue<PooledRangedProjectile> GetOrCreatePool(GameObject prefab)
    {
        if (!_poolByPrefab.TryGetValue(prefab, out Queue<PooledRangedProjectile> pool))
        {
            pool = new Queue<PooledRangedProjectile>();
            _poolByPrefab[prefab] = pool;
        }

        return pool;
    }

    /// <summary>
    /// 지정 프리팹의 누적 생성 개수를 반환합니다.
    /// </summary>
    private int GetCreatedCount(GameObject prefab)
    {
        return _createdCountByPrefab.TryGetValue(prefab, out int count) ? count : 0;
    }
}
