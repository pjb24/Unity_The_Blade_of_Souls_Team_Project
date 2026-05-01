using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 투사체 풀을 관리하고 생성/반환 요청을 수행하는 서비스입니다.
/// </summary>
[DisallowMultipleComponent]
public class PooledProjectileSpawnService : MonoBehaviour, IProjectileSpawnService
{
    [Header("Pool")]
    [Tooltip("비활성 풀에 사용할 인스턴스가 부족할 때 자동 확장을 허용할지 여부입니다.")]
    [SerializeField] private bool _allowAutoExpand = true; // 풀 부족 시 자동 확장 허용 여부입니다.
    [Tooltip("0 이하면 무제한, 1 이상이면 프리팹별 최대 생성 개수 제한입니다.")]
    [SerializeField] private int _maxCountPerPrefab = 0; // 프리팹별 최대 생성 개수 제한값입니다.

    [Tooltip("프리팹별 비활성 Projectile 큐를 보관하는 맵입니다.")]
    private readonly Dictionary<GameObject, Queue<PooledRangedProjectile>> _poolByPrefab = new Dictionary<GameObject, Queue<PooledRangedProjectile>>(); // 프리팹별 풀 큐입니다.
    [Tooltip("프리팹별 누적 생성 개수를 추적하는 맵입니다.")]
    private readonly Dictionary<GameObject, int> _createdCountByPrefab = new Dictionary<GameObject, int>(); // 프리팹별 생성 수 추적 맵입니다.
    [Tooltip("생성된 Projectile 인스턴스와 원본 프리팹의 역참조 맵입니다.")]
    private readonly Dictionary<PooledRangedProjectile, GameObject> _sourcePrefabByInstance = new Dictionary<PooledRangedProjectile, GameObject>(); // 인스턴스-프리팹 역참조 맵입니다.
    [Tooltip("관찰자 시각 전용 Projectile을 복제 ID로 추적하는 맵입니다.")]
    private readonly Dictionary<int, PooledRangedProjectile> _visualProjectileById = new Dictionary<int, PooledRangedProjectile>(); // 복제 시각 Projectile 조회 맵입니다.
    private bool _hasLoggedNetworkObjectPoolMissingWarning; // Prevents repeated warnings when NetworkObject pooling is unavailable.
    private bool _hasLoggedClientNetworkSpawnBlockedWarning; // Prevents repeated warnings when a client tries to spawn a NetworkObject projectile.

    /// <summary>
    /// 요청된 조건으로 Projectile을 스폰하고 인스턴스를 반환합니다.
    /// </summary>
    public PooledRangedProjectile RequestSpawn(GameObject prefab, Vector2 position, Vector2 direction, GameObject owner, float speed, float lifetime, bool isVisualOnly = false, int visualInstanceId = 0)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[PooledProjectileSpawnService] RequestSpawn failed: prefab is null.");
            return null;
        }

        bool shouldSpawnNetworkObject = ShouldSpawnNetworkObjectProjectile(prefab, isVisualOnly);
        if (shouldSpawnNetworkObject && !CanSpawnNetworkObjectProjectile())
        {
            return null;
        }

        PooledRangedProjectile projectile = shouldSpawnNetworkObject
            ? AcquireNetworkProjectile(prefab, position, direction, owner, speed, lifetime, isVisualOnly, visualInstanceId)
            : Acquire(prefab);
        if (projectile == null)
        {
            return null;
        }

        if (!shouldSpawnNetworkObject)
        {
            projectile.transform.position = position;
            projectile.gameObject.SetActive(true);
            projectile.Initialize(direction, speed, lifetime, owner, isVisualOnly, visualInstanceId);
        }

        if (isVisualOnly && visualInstanceId > 0)
        {
            _visualProjectileById[visualInstanceId] = projectile;
        }

        return projectile;
    }

    /// <summary>
    /// 관찰자 시각 전용 Projectile을 복제 ID 기준으로 찾아 강제 종료합니다.
    /// </summary>
    public bool TryDespawnVisual(int visualInstanceId)
    {
        if (visualInstanceId <= 0)
        {
            return false;
        }

        if (!_visualProjectileById.TryGetValue(visualInstanceId, out PooledRangedProjectile projectile) || projectile == null)
        {
            _visualProjectileById.Remove(visualInstanceId);
            return false;
        }

        projectile.ForceDespawn(E_ProjectileDespawnReason.HitEnvironment);
        return true;
    }

    /// <summary>
    /// 풀에서 사용 가능한 Projectile을 가져오거나 필요 시 새로 생성합니다.
    /// </summary>
    private PooledRangedProjectile Acquire(GameObject prefab)
    {
        Queue<PooledRangedProjectile> pool = GetOrCreatePool(prefab);

        while (pool.Count > 0)
        {
            PooledRangedProjectile pooledProjectile = pool.Dequeue();
            if (pooledProjectile != null)
            {
                return pooledProjectile;
            }
        }

        int createdCount = GetCreatedCount(prefab);
        if (!_allowAutoExpand && createdCount > 0)
        {
            Debug.LogWarning($"[PooledProjectileSpawnService] Pool empty for {prefab.name} and auto expand disabled.");
            return null;
        }

        bool hasMaxLimit = _maxCountPerPrefab > 0;
        if (hasMaxLimit && createdCount >= _maxCountPerPrefab)
        {
            Debug.LogWarning($"[PooledProjectileSpawnService] MaxCount exceeded for {prefab.name}. Overflow projectile will be created and managed by pool. max={_maxCountPerPrefab}, newCount={createdCount + 1}");
        }
        else
        {
            Debug.LogWarning($"[PooledProjectileSpawnService] Pool expanded for {prefab.name}.");
        }

        GameObject createdObject = Instantiate(prefab, transform);
        PooledRangedProjectile createdProjectile = createdObject.GetComponent<PooledRangedProjectile>();
        if (createdProjectile == null)
        {
            createdProjectile = createdObject.AddComponent<PooledRangedProjectile>();
            Debug.LogWarning($"[PooledProjectileSpawnService] Added missing PooledRangedProjectile on {createdObject.name}.");
        }

        createdProjectile.BindReturnHandler(ReturnToPool);
        createdObject.SetActive(false);

        _createdCountByPrefab[prefab] = createdCount + 1;
        _sourcePrefabByInstance[createdProjectile] = prefab;

        return createdProjectile;
    }

    /// <summary>
    /// NetworkObject Projectile을 NetworkObjectPoolManager를 통해 서버 권한으로 대여하고 Spawn합니다.
    /// </summary>
    private PooledRangedProjectile AcquireNetworkProjectile(GameObject prefab, Vector2 position, Vector2 direction, GameObject owner, float speed, float lifetime, bool isVisualOnly, int visualInstanceId)
    {
        NetworkObjectPoolManager networkPoolManager = NetworkObjectPoolManager.Instance; // NetworkObject 전용 Pool 관리자입니다.
        if (networkPoolManager == null)
        {
            Debug.LogWarning($"[PooledProjectileSpawnService] NetworkObjectPoolManager missing. prefab={prefab.name}", this);
            return null;
        }

        NetworkObject networkObject = networkPoolManager.SpawnNetworkObject(prefab, position, Quaternion.identity, null, owner);
        if (networkObject == null)
        {
            return null;
        }

        PooledRangedProjectile projectile = networkObject.GetComponent<PooledRangedProjectile>();
        if (projectile == null)
        {
            Debug.LogWarning($"[PooledProjectileSpawnService] Network projectile has no PooledRangedProjectile. prefab={prefab.name}", networkObject);
            networkPoolManager.DespawnNetworkObject(networkObject);
            return null;
        }

        projectile.BindReturnHandler(ReturnToPool);
        projectile.Initialize(direction, speed, lifetime, owner, isVisualOnly, visualInstanceId);
        return projectile;
    }

    /// <summary>
    /// 사용 종료된 Projectile을 비활성화하고 풀로 반환합니다.
    /// </summary>
    private void ReturnToPool(PooledRangedProjectile projectile)
    {
        if (projectile == null)
        {
            return;
        }

        if (projectile.VisualInstanceId > 0)
        {
            _visualProjectileById.Remove(projectile.VisualInstanceId);
        }

        NetworkObject networkObject = projectile.GetComponent<NetworkObject>();
        PooledObjectMarker marker = projectile.GetComponent<PooledObjectMarker>();
        if (networkObject != null && marker != null)
        {
            NetworkObjectPoolManager networkPoolManager = NetworkObjectPoolManager.Instance;
            if (networkPoolManager != null && networkPoolManager.DespawnNetworkObject(networkObject))
            {
                return;
            }

            Debug.LogWarning($"[PooledProjectileSpawnService] Network pool return failed. projectile={projectile.name}", projectile);
            return;
        }

        if (!_sourcePrefabByInstance.TryGetValue(projectile, out GameObject prefab) || prefab == null)
        {
            Debug.LogWarning($"[PooledProjectileSpawnService] Return failed because source prefab is missing. projectile={projectile.name}", projectile);
            return;
        }

        DespawnNetworkObjectIfNeeded(projectile);

        Queue<PooledRangedProjectile> pool = GetOrCreatePool(prefab);
        projectile.gameObject.SetActive(false);
        projectile.transform.SetParent(transform);
        pool.Enqueue(projectile);
    }

    /// <summary>
    /// 지정 프리팹의 풀 큐를 반환하거나 새로 생성합니다.
    /// </summary>
    /// <summary>
    /// Returns whether the requested projectile should be spawned through NGO.
    /// </summary>
    private bool ShouldSpawnNetworkObjectProjectile(GameObject prefab, bool isVisualOnly)
    {
        if (isVisualOnly || prefab == null || prefab.GetComponent<NetworkObject>() == null)
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // NGO singleton used to distinguish single-player from networked sessions.
        return networkManager != null && networkManager.IsListening;
    }

    /// <summary>
    /// Returns whether this instance is allowed to spawn authoritative NetworkObject projectiles.
    /// </summary>
    private bool CanSpawnNetworkObjectProjectile()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // NGO singleton used to validate server authority.
        if (networkManager == null || !networkManager.IsListening)
        {
            return true;
        }

        if (networkManager.IsServer)
        {
            if (!_hasLoggedNetworkObjectPoolMissingWarning)
            {
                Debug.LogWarning("[PooledProjectileSpawnService] Host/Server will route NetworkObject projectile through NetworkObjectPoolManager.");
                _hasLoggedNetworkObjectPoolMissingWarning = true;
            }

            return true;
        }

        if (!_hasLoggedClientNetworkSpawnBlockedWarning)
        {
            Debug.LogWarning("[PooledProjectileSpawnService] Client projectile NetworkObject spawn request was blocked.");
            _hasLoggedClientNetworkSpawnBlockedWarning = true;
        }

        return false;
    }

    /// <summary>
    /// Spawns a projectile NetworkObject from the server when required.
    /// </summary>
    private bool TrySpawnNetworkObjectProjectile(PooledRangedProjectile projectile)
    {
        if (projectile == null)
        {
            return false;
        }

        NetworkObject networkObject = projectile.GetComponent<NetworkObject>(); // NetworkObject attached to the projectile instance.
        if (networkObject == null)
        {
            Debug.LogWarning($"[PooledProjectileSpawnService] Network projectile spawn failed because NetworkObject is missing. projectile={projectile.name}");
            return false;
        }

        if (networkObject.IsSpawned)
        {
            return true;
        }

        networkObject.Spawn(true);
        return true;
    }

    /// <summary>
    /// Despawns a projectile NetworkObject before returning it to the local pool.
    /// </summary>
    private void DespawnNetworkObjectIfNeeded(PooledRangedProjectile projectile)
    {
        NetworkObject networkObject = projectile.GetComponent<NetworkObject>(); // NetworkObject attached to the pooled projectile instance.
        if (networkObject == null || !networkObject.IsSpawned)
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // NGO singleton used to ensure only server despawns authoritative projectiles.
        if (networkManager != null && networkManager.IsListening && !networkManager.IsServer)
        {
            Debug.LogWarning($"[PooledProjectileSpawnService] Client attempted to return a spawned NetworkObject projectile. projectile={projectile.name}");
            return;
        }

        networkObject.Despawn(false);
    }

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
