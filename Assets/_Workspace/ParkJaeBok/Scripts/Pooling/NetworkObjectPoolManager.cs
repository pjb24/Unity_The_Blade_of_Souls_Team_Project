using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NGO NetworkObject Prefab을 서버 권한으로 Spawn하고 Despawn 시 Pool로 반환하는 네트워크 전용 Pool 관리자입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkObjectPoolManager : MonoBehaviour
{
    [Header("Network Pool Settings")]
    [Tooltip("씬 전환 후에도 NetworkObject Pool 관리자를 유지할지 결정합니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // 씬 전환 중 Network Pool 유지 여부입니다.

    [Tooltip("NGO NetworkObject가 붙은 Prefab별 초기 개수, 최대 개수, 확장 허용 여부를 설정합니다.")]
    [SerializeField] private List<PoolPrefabConfig> _networkPrefabConfigs = new List<PoolPrefabConfig>(); // NetworkObject Prefab별 Pool 설정입니다.

    [Header("Unregistered Prefab Policy")]
    [Tooltip("등록되지 않은 NetworkObject Prefab 요청을 Warning 후 런타임 Pool로 허용할지 결정합니다.")]
    [SerializeField] private bool _allowRuntimePoolForUnregisteredPrefab = true; // 미등록 Network Prefab 런타임 Pool 생성 허용 여부입니다.

    [Tooltip("미등록 NetworkObject Prefab 런타임 Pool의 권장 최대 생성 개수입니다. 런타임에서 초과 생성이 필요하면 Warning 후 Pool 관리 대상으로 추가합니다. 0 이하는 제한 없음입니다.")]
    [Min(0)]
    [SerializeField] private int _runtimePoolMaxCount = 0; // 런타임 Network Pool 초과 생성 Warning 기준이 되는 권장 최대 개수입니다.

    private static NetworkObjectPoolManager _instance; // 전역 접근용 Network Pool 관리자입니다.

    private readonly Dictionary<GameObject, NetworkRuntimePool> _poolByPrefab = new Dictionary<GameObject, NetworkRuntimePool>(); // Prefab별 네트워크 Pool입니다.
    private readonly Dictionary<GameObject, PoolPrefabConfig> _configByPrefab = new Dictionary<GameObject, PoolPrefabConfig>(); // Inspector 설정 캐시입니다.
    private readonly Dictionary<NetworkObject, GameObject> _sourcePrefabByNetworkObject = new Dictionary<NetworkObject, GameObject>(); // Spawn된 NetworkObject의 원본 Prefab 추적입니다.
    private readonly HashSet<GameObject> _registeredHandlerPrefabs = new HashSet<GameObject>(); // 현재 NetworkManager PrefabHandler에 등록된 Prefab 집합입니다.
    private NetworkManager _registeredNetworkManager; // PrefabHandler 등록 대상 NetworkManager입니다.

    public static NetworkObjectPoolManager Instance => ResolveInstance();

    /// <summary>
    /// 씬의 NetworkObjectPoolManager를 찾고 없으면 Warning과 함께 런타임 관리자를 생성합니다.
    /// </summary>
    private static NetworkObjectPoolManager ResolveInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        _instance = FindAnyObjectByType<NetworkObjectPoolManager>();
        if (_instance != null)
        {
            return _instance;
        }

        GameObject managerObject = new GameObject("NetworkObjectPoolManager_Runtime");
        _instance = managerObject.AddComponent<NetworkObjectPoolManager>();
        Debug.LogWarning("[NetworkObjectPoolManager] Scene manager missing. Runtime manager was created; configure a scene manager for designer-owned network pool settings.", _instance);
        return _instance;
    }

    /// <summary>
    /// 설정된 NetworkObject Prefab Pool을 초기화합니다.
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[NetworkObjectPoolManager] Duplicate manager detected. object={name}", this);
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        InitializeConfiguredPools();
        TryRegisterPrefabHandlers();
    }

    /// <summary>
    /// NetworkManager가 늦게 준비되는 경우 PrefabHandler 등록을 재시도합니다.
    /// </summary>
    private void Update()
    {
        TryRegisterPrefabHandlers();
    }

    /// <summary>
    /// 서버 또는 Host에서 NetworkObject를 Pool에서 대여하고 NGO Spawn을 수행합니다.
    /// </summary>
    public NetworkObject SpawnNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation, ulong? ownerClientId = null, GameObject owner = null, object payload = null)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[NetworkObjectPoolManager] Spawn failed. prefab is null.", this);
            return null;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션 권한 확인용 NetworkManager입니다.
        if (networkManager == null || !networkManager.IsListening)
        {
            Debug.LogWarning($"[NetworkObjectPoolManager] Network spawn requested without active NetworkManager. prefab={prefab.name}", this);
            return null;
        }

        if (!networkManager.IsServer)
        {
            Debug.LogWarning($"[NetworkObjectPoolManager] Client tried to spawn NetworkObject. prefab={prefab.name}", this);
            return null;
        }

        NetworkRuntimePool pool = GetOrCreatePool(prefab);
        if (pool == null)
        {
            return null;
        }

        NetworkObject networkObject = pool.Acquire(position, rotation, null, owner, payload);
        if (networkObject == null)
        {
            return null;
        }

        _sourcePrefabByNetworkObject[networkObject] = prefab;

        if (!networkObject.IsSpawned)
        {
            if (ownerClientId.HasValue)
            {
                networkObject.SpawnWithOwnership(ownerClientId.Value, true);
            }
            else
            {
                networkObject.Spawn(true);
            }
        }

        return networkObject;
    }

    /// <summary>
    /// 서버 또는 Host에서 Spawn된 NetworkObject를 Despawn하고 Pool로 반환합니다.
    /// </summary>
    public bool DespawnNetworkObject(NetworkObject networkObject)
    {
        if (networkObject == null)
        {
            Debug.LogWarning("[NetworkObjectPoolManager] Despawn failed. NetworkObject is null.", this);
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션 권한 확인용 NetworkManager입니다.
        if (networkManager != null && networkManager.IsListening && !networkManager.IsServer)
        {
            Debug.LogWarning($"[NetworkObjectPoolManager] Client tried to despawn NetworkObject. object={networkObject.name}", networkObject);
            return false;
        }

        if (!_sourcePrefabByNetworkObject.TryGetValue(networkObject, out GameObject prefab) || prefab == null)
        {
            PooledObjectMarker marker = networkObject.GetComponent<PooledObjectMarker>();
            prefab = marker != null ? marker.SourcePrefab : null;
        }

        if (prefab == null || !_poolByPrefab.TryGetValue(prefab, out NetworkRuntimePool pool))
        {
            Debug.LogWarning($"[NetworkObjectPoolManager] Despawn failed. object was not created by network pool. object={networkObject.name}", networkObject);
            return false;
        }

        if (networkObject.IsSpawned)
        {
            networkObject.Despawn(false);
        }

        _sourcePrefabByNetworkObject.Remove(networkObject);
        pool.Release(networkObject);
        return true;
    }

    /// <summary>
    /// 등록된 Network Prefab 설정을 검증하고 초기 Pool을 생성합니다.
    /// </summary>
    private void InitializeConfiguredPools()
    {
        _configByPrefab.Clear();

        for (int i = 0; i < _networkPrefabConfigs.Count; i++)
        {
            PoolPrefabConfig config = _networkPrefabConfigs[i];
            if (config == null || config.Prefab == null)
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Network pool config has missing prefab. index={i}", this);
                continue;
            }

            if (config.Prefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Config prefab has no NetworkObject. prefab={config.Prefab.name}", this);
                continue;
            }

            if (_configByPrefab.ContainsKey(config.Prefab))
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Duplicate network pool prefab config found. prefab={config.Prefab.name}", this);
                continue;
            }

            _configByPrefab.Add(config.Prefab, config);
            GetOrCreatePool(config.Prefab);
        }
    }

    /// <summary>
    /// NGO PrefabHandler에 Pool 핸들러를 등록하여 Client 측 Spawn/Despawn도 Pool을 사용하게 합니다.
    /// </summary>
    private void TryRegisterPrefabHandlers()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            return;
        }

        if (_registeredNetworkManager != networkManager)
        {
            _registeredHandlerPrefabs.Clear();
            _registeredNetworkManager = networkManager;
        }

        foreach (GameObject prefab in _poolByPrefab.Keys)
        {
            if (prefab == null || prefab.GetComponent<NetworkObject>() == null)
            {
                continue;
            }

            if (_registeredHandlerPrefabs.Contains(prefab))
            {
                continue;
            }

            networkManager.PrefabHandler.AddHandler(prefab, _poolByPrefab[prefab]);
            _registeredHandlerPrefabs.Add(prefab);
        }
    }

    /// <summary>
    /// Prefab에 대응하는 NetworkRuntimePool을 반환하거나 정책에 따라 새로 생성합니다.
    /// </summary>
    private NetworkRuntimePool GetOrCreatePool(GameObject prefab)
    {
        if (_poolByPrefab.TryGetValue(prefab, out NetworkRuntimePool pool))
        {
            return pool;
        }

        if (prefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogWarning($"[NetworkObjectPoolManager] Network pool requested for prefab without NetworkObject. prefab={prefab.name}", this);
            return null;
        }

        if (_configByPrefab.TryGetValue(prefab, out PoolPrefabConfig config))
        {
            pool = new NetworkRuntimePool(prefab, config, transform);
            _poolByPrefab.Add(prefab, pool);
            TryRegisterPrefabHandlers();
            return pool;
        }

        Debug.LogWarning($"[NetworkObjectPoolManager] Unregistered network prefab requested. prefab={prefab.name}", this);
        if (!_allowRuntimePoolForUnregisteredPrefab)
        {
            Debug.LogWarning($"[NetworkObjectPoolManager] Runtime network pool creation disabled. Spawn denied. prefab={prefab.name}", this);
            return null;
        }

        pool = new NetworkRuntimePool(prefab, _runtimePoolMaxCount, transform);
        _poolByPrefab.Add(prefab, pool);
        TryRegisterPrefabHandlers();
        return pool;
    }

    /// <summary>
    /// 단일 NetworkObject Prefab의 서버/클라이언트 Pool과 NGO PrefabHandler를 담당합니다.
    /// </summary>
    private sealed class NetworkRuntimePool : INetworkPrefabInstanceHandler
    {
        private readonly GameObject _prefab; // 관리 대상 NetworkObject Prefab입니다.
        private readonly Transform _poolRoot; // 비활성 NetworkObject 보관 부모입니다.
        private readonly Queue<NetworkObject> _inactiveObjects = new Queue<NetworkObject>(); // 재사용 가능한 NetworkObject Queue입니다.
        private readonly int _maxCount; // 런타임 초과 생성 Warning 기준이 되는 권장 최대 수입니다.
        private readonly bool _allowExpand; // 부족 시 추가 생성 허용 여부입니다.
        private int _createdCount; // 현재까지 생성된 인스턴스 수입니다.

        /// <summary>
        /// Inspector 설정 기반 NetworkRuntimePool을 생성합니다.
        /// </summary>
        public NetworkRuntimePool(GameObject prefab, PoolPrefabConfig config, Transform serviceRoot)
        {
            _prefab = prefab;
            _maxCount = config.MaxCount;
            _allowExpand = config.AllowExpand;
            _poolRoot = CreatePoolRoot(prefab, serviceRoot);
            Prewarm(config.InitialCount);
        }

        /// <summary>
        /// 미등록 NetworkObject Prefab 요청용 RuntimePool을 생성합니다.
        /// </summary>
        public NetworkRuntimePool(GameObject prefab, int maxCount, Transform serviceRoot)
        {
            _prefab = prefab;
            _maxCount = maxCount;
            _allowExpand = true;
            _poolRoot = CreatePoolRoot(prefab, serviceRoot);
        }

        /// <summary>
        /// NGO가 Client Spawn 수신 시 호출하는 인스턴스 생성 핸들러입니다.
        /// </summary>
        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            return Acquire(position, rotation, null, null, null);
        }

        /// <summary>
        /// NGO가 Client Despawn 수신 시 호출하는 인스턴스 반환 핸들러입니다.
        /// </summary>
        public void Destroy(NetworkObject networkObject)
        {
            Release(networkObject);
        }

        /// <summary>
        /// Pool에서 NetworkObject를 꺼내고 활성화 전 초기화 콜백을 호출합니다.
        /// </summary>
        public NetworkObject Acquire(Vector3 position, Quaternion rotation, Transform parent, GameObject owner, object payload)
        {
            NetworkObject networkObject = DequeueOrCreate();
            if (networkObject == null)
            {
                return null;
            }

            PooledObjectMarker marker = networkObject.GetComponent<PooledObjectMarker>();
            marker.MarkBorrowed();

            Transform instanceTransform = networkObject.transform;
            if (parent != null)
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Parent assignment is ignored for pooled NetworkObject before Spawn. prefab={_prefab.name}, object={networkObject.name}", networkObject);
            }

            instanceTransform.position = position;
            instanceTransform.rotation = rotation;
            instanceTransform.localScale = Vector3.one;

            PoolSpawnContext context = new PoolSpawnContext(_prefab, position, rotation, parent, owner, payload);
            IPoolableObject[] poolables = networkObject.GetComponentsInChildren<IPoolableObject>(true);
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnPoolSpawned(context);
            }

            networkObject.gameObject.SetActive(true);
            return networkObject;
        }

        /// <summary>
        /// NetworkObject를 비활성화하고 Pool Queue로 반환합니다.
        /// </summary>
        public void Release(NetworkObject networkObject)
        {
            if (networkObject == null)
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Release failed. networkObject is null. prefab={_prefab.name}");
                return;
            }

            PooledObjectMarker marker = networkObject.GetComponent<PooledObjectMarker>();
            if (marker == null || marker.SourcePrefab != _prefab)
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Release failed. object was not created by this pool. prefab={_prefab.name}, object={networkObject.name}", networkObject);
                return;
            }

            if (marker.IsInPool)
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Duplicate return detected. prefab={_prefab.name}, object={networkObject.name}", networkObject);
                return;
            }

            IPoolableObject[] poolables = networkObject.GetComponentsInChildren<IPoolableObject>(true);
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnPoolDespawned();
            }

            ResetUnityState(networkObject.gameObject);
            marker.MarkReturned();

            ResetReturnedNetworkObjectTransform(networkObject);
            networkObject.gameObject.SetActive(false);
            _inactiveObjects.Enqueue(networkObject);
        }

        /// <summary>
        /// Despawn된 NetworkObject는 NGO 부모 변경 제한을 피하기 위해 부모 Transform을 변경하지 않고 스케일만 초기화합니다.
        /// </summary>
        private void ResetReturnedNetworkObjectTransform(NetworkObject networkObject)
        {
            if (networkObject == null)
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Returned NetworkObject transform reset skipped because object is null. prefab={_prefab.name}");
                return;
            }

            networkObject.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 사용 가능한 NetworkObject를 반환하거나 정책에 따라 새로 생성합니다.
        /// </summary>
        private NetworkObject DequeueOrCreate()
        {
            while (_inactiveObjects.Count > 0)
            {
                NetworkObject networkObject = _inactiveObjects.Dequeue();
                if (networkObject != null)
                {
                    return networkObject;
                }
            }

            if (!_allowExpand && _createdCount > 0)
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Network pool empty and expand disabled. prefab={_prefab.name}");
                return null;
            }

            if (_maxCount > 0 && _createdCount >= _maxCount)
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Max count exceeded. Overflow NetworkObject will be created and managed by pool. prefab={_prefab.name}, max={_maxCount}, newCount={_createdCount + 1}");
            }
            else
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Network pool expanded. prefab={_prefab.name}, newCount={_createdCount + 1}");
            }

            return CreateInstance();
        }

        /// <summary>
        /// 초기 Network Pool 크기만큼 인스턴스를 미리 생성합니다.
        /// </summary>
        private void Prewarm(int initialCount)
        {
            for (int i = 0; i < initialCount; i++)
            {
                if (_maxCount > 0 && _createdCount >= _maxCount)
                {
                    Debug.LogWarning($"[NetworkObjectPoolManager] Prewarm stopped by max count. prefab={_prefab.name}, max={_maxCount}");
                    return;
                }

                _inactiveObjects.Enqueue(CreateInstance());
            }
        }

        /// <summary>
        /// 새 NetworkObject 인스턴스를 생성하고 Pool 마커를 연결합니다.
        /// </summary>
        private NetworkObject CreateInstance()
        {
            GameObject instance = Object.Instantiate(_prefab);
            instance.name = $"{_prefab.name}_NetworkPooled_{_createdCount}";

            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogWarning($"[NetworkObjectPoolManager] Created instance has no NetworkObject. prefab={_prefab.name}", instance);
                Object.Destroy(instance);
                return null;
            }

            PooledObjectMarker marker = instance.GetComponent<PooledObjectMarker>();
            if (marker == null)
            {
                marker = instance.AddComponent<PooledObjectMarker>();
            }

            marker.BindSourcePrefab(_prefab);
            ResetUnityState(instance);
            instance.SetActive(false);
            _createdCount++;
            return networkObject;
        }

        /// <summary>
        /// Network Pool 보관용 부모 Transform을 생성합니다.
        /// </summary>
        private static Transform CreatePoolRoot(GameObject prefab, Transform serviceRoot)
        {
            GameObject rootObject = new GameObject($"Pool_Network_{prefab.name}");
            Transform rootTransform = rootObject.transform;
            rootTransform.SetParent(serviceRoot, false);
            return rootTransform;
        }

        /// <summary>
        /// Coroutine, Invoke, 물리, Collider, Particle, Audio 상태를 다음 대여에 안전하도록 정리합니다.
        /// </summary>
        private static void ResetUnityState(GameObject instance)
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                behaviours[i].StopAllCoroutines();
                behaviours[i].CancelInvoke();
            }

            Rigidbody[] rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].linearVelocity = Vector3.zero;
                rigidbodies[i].angularVelocity = Vector3.zero;
            }

            Rigidbody2D[] rigidbodies2D = instance.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < rigidbodies2D.Length; i++)
            {
                rigidbodies2D[i].linearVelocity = Vector2.zero;
                rigidbodies2D[i].angularVelocity = 0f;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = true;
            }

            Collider2D[] colliders2D = instance.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders2D.Length; i++)
            {
                colliders2D[i].enabled = true;
            }

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            AudioSource[] audioSources = instance.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                audioSources[i].Stop();
                audioSources[i].clip = null;
            }
        }
    }
}
