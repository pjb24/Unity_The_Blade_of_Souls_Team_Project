using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로컬 전용 GameObject를 Prefab 단위로 대여하고 반환하는 ObjectPool 관리자입니다.
/// NetworkObject Prefab을 싱글플레이에서 로컬로 사용할 때는 NGO 부모 변경 콜백 충돌을 피하기 위해 부모 Transform을 변경하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalObjectPoolManager : MonoBehaviour
{
    [Header("Pool Settings")]
    [Tooltip("씬 전환 후에도 로컬 Pool 관리자를 유지할지 결정합니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // 씬 전환 중 Pool 유지 여부입니다.

    [Tooltip("Inspector에서 Prefab별 초기 개수, 최대 개수, 확장 허용 여부, 자동 반환 시간을 설정합니다.")]
    [SerializeField] private List<PoolPrefabConfig> _prefabConfigs = new List<PoolPrefabConfig>(); // Prefab별 Pool 생성 정책 목록입니다.

    [Header("Unregistered Prefab Policy")]
    [Tooltip("등록되지 않은 Prefab 요청이 들어왔을 때 Warning을 출력하고 런타임 Pool 생성을 허용할지 결정합니다.")]
    [SerializeField] private bool _allowRuntimePoolForUnregisteredPrefab = true; // 미등록 Prefab에 대한 명시적 경고 후 런타임 Pool 생성 허용 여부입니다.

    [Tooltip("미등록 Prefab 런타임 Pool의 권장 최대 생성 개수입니다. 런타임에서 초과 생성이 필요하면 Warning 후 Pool 관리 대상으로 추가합니다. 0 이하는 제한 없음입니다.")]
    [Min(0)]
    [SerializeField] private int _runtimePoolMaxCount = 0; // 런타임 Pool 초과 생성 Warning 기준이 되는 권장 최대 개수입니다.

    private static LocalObjectPoolManager _instance; // 전역 접근용 Pool 관리자 인스턴스입니다.

    private readonly Dictionary<GameObject, RuntimePool> _poolByPrefab = new Dictionary<GameObject, RuntimePool>(); // Prefab별 Pool 자료구조입니다.
    private readonly Dictionary<GameObject, PoolPrefabConfig> _configByPrefab = new Dictionary<GameObject, PoolPrefabConfig>(); // Prefab별 Inspector 설정 캐시입니다.

    public static LocalObjectPoolManager Instance => ResolveInstance();

    /// <summary>
    /// 씬에 배치된 Pool 관리자를 찾고 없으면 Warning과 함께 런타임 관리자를 생성합니다.
    /// </summary>
    private static LocalObjectPoolManager ResolveInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        _instance = FindAnyObjectByType<LocalObjectPoolManager>();
        if (_instance != null)
        {
            return _instance;
        }

        GameObject managerObject = new GameObject("LocalObjectPoolManager_Runtime");
        _instance = managerObject.AddComponent<LocalObjectPoolManager>();
        Debug.LogWarning("[LocalObjectPoolManager] Scene manager missing. Runtime manager was created; configure a scene manager for designer-owned pool settings.", _instance);
        return _instance;
    }

    /// <summary>
    /// Inspector 설정을 검증하고 등록된 Prefab Pool을 미리 생성합니다.
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[LocalObjectPoolManager] Duplicate manager detected. object={name}", this);
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        InitializeConfiguredPools();
    }

    /// <summary>
    /// Prefab을 기준으로 Pool에서 GameObject를 대여하고 활성화합니다.
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null, GameObject owner = null, object payload = null)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[LocalObjectPoolManager] Spawn failed. prefab is null.", this);
            return null;
        }

        RuntimePool pool = GetOrCreatePool(prefab);
        if (pool == null)
        {
            return null;
        }

        GameObject instance = pool.Acquire();
        if (instance == null)
        {
            return null;
        }

        PooledObjectMarker marker = instance.GetComponent<PooledObjectMarker>();
        marker.MarkBorrowed();

        if (!pool.ShouldSkipParentingForNetworkObject)
        {
            instance.transform.SetParent(parent, false);
        }

        instance.transform.position = position;
        instance.transform.rotation = rotation;
        instance.transform.localScale = Vector3.one;

        PoolSpawnContext context = new PoolSpawnContext(prefab, position, rotation, parent, owner, payload);
        InvokeSpawnedCallbacks(instance, context);

        instance.SetActive(true);
        pool.ScheduleAutoReturnIfNeeded(instance, this);
        SchedulePayloadAutoReturnIfNeeded(pool, instance, payload);
        return instance;
    }

    /// <summary>
    /// Pool에서 생성한 GameObject를 원본 Prefab Pool로 반환합니다.
    /// </summary>
    public bool Return(GameObject instance)
    {
        if (instance == null)
        {
            Debug.LogWarning("[LocalObjectPoolManager] Return failed. instance is null.", this);
            return false;
        }

        PooledObjectMarker marker = instance.GetComponent<PooledObjectMarker>();
        if (marker == null || marker.SourcePrefab == null)
        {
            Debug.LogWarning($"[LocalObjectPoolManager] Return failed. object was not created by pool. object={instance.name}", instance);
            return false;
        }

        if (marker.IsInPool)
        {
            Debug.LogWarning($"[LocalObjectPoolManager] Duplicate return detected. prefab={marker.SourcePrefab.name}, object={instance.name}", instance);
            return false;
        }

        if (!_poolByPrefab.TryGetValue(marker.SourcePrefab, out RuntimePool pool))
        {
            Debug.LogWarning($"[LocalObjectPoolManager] Return failed. source prefab is not registered. prefab={marker.SourcePrefab.name}, object={instance.name}", instance);
            return false;
        }

        pool.Release(instance);
        return true;
    }

    /// <summary>
    /// Inspector에 등록된 Pool 설정을 캐시하고 초기 인스턴스를 생성합니다.
    /// </summary>
    private void InitializeConfiguredPools()
    {
        _configByPrefab.Clear();

        for (int i = 0; i < _prefabConfigs.Count; i++)
        {
            PoolPrefabConfig config = _prefabConfigs[i];
            if (config == null || config.Prefab == null)
            {
                Debug.LogWarning($"[LocalObjectPoolManager] Pool config has missing prefab. index={i}", this);
                continue;
            }

            if (_configByPrefab.ContainsKey(config.Prefab))
            {
                Debug.LogWarning($"[LocalObjectPoolManager] Duplicate pool prefab config found. prefab={config.Prefab.name}", this);
                continue;
            }

            _configByPrefab.Add(config.Prefab, config);
            GetOrCreatePool(config.Prefab);
        }
    }

    /// <summary>
    /// Prefab에 대응하는 RuntimePool을 반환하거나 정책에 따라 새로 생성합니다.
    /// </summary>
    private RuntimePool GetOrCreatePool(GameObject prefab)
    {
        if (_poolByPrefab.TryGetValue(prefab, out RuntimePool pool))
        {
            return pool;
        }

        if (_configByPrefab.TryGetValue(prefab, out PoolPrefabConfig config))
        {
            pool = new RuntimePool(prefab, config, transform);
            _poolByPrefab.Add(prefab, pool);
            return pool;
        }

        Debug.LogWarning($"[LocalObjectPoolManager] Unregistered prefab requested. prefab={prefab.name}", this);
        if (!_allowRuntimePoolForUnregisteredPrefab)
        {
            Debug.LogWarning($"[LocalObjectPoolManager] Runtime pool creation disabled. Spawn denied. prefab={prefab.name}", this);
            return null;
        }

        pool = new RuntimePool(prefab, _runtimePoolMaxCount, transform);
        _poolByPrefab.Add(prefab, pool);
        return pool;
    }

    /// <summary>
    /// 대여된 개체의 Pool 초기화 콜백을 호출합니다.
    /// </summary>
    private void InvokeSpawnedCallbacks(GameObject instance, PoolSpawnContext context)
    {
        IPoolableObject[] poolables = instance.GetComponentsInChildren<IPoolableObject>(true);
        for (int i = 0; i < poolables.Length; i++)
        {
            poolables[i].OnPoolSpawned(context);
        }
    }

    /// <summary>
    /// Prefab Config 자동 반환이 없을 때 payload로 전달된 초 단위 반환 시간을 적용합니다.
    /// </summary>
    private void SchedulePayloadAutoReturnIfNeeded(RuntimePool pool, GameObject instance, object payload)
    {
        if (pool == null || pool.UsesAutoReturn)
        {
            return;
        }

        if (payload is float autoReturnSeconds && autoReturnSeconds > 0f)
        {
            PooledAutoReturn autoReturn = instance.GetComponent<PooledAutoReturn>();
            if (autoReturn == null)
            {
                autoReturn = instance.AddComponent<PooledAutoReturn>();
            }

            autoReturn.Schedule(this, autoReturnSeconds);
        }
    }

    /// <summary>
    /// 단일 Prefab에 대한 실제 Queue와 생성 카운트를 보관합니다.
    /// </summary>
    private sealed class RuntimePool
    {
        private readonly GameObject _prefab; // 이 Pool이 관리하는 원본 Prefab입니다.
        private readonly Transform _poolRoot; // 비활성 인스턴스를 보관할 부모 Transform입니다.
        private readonly Queue<GameObject> _inactiveObjects = new Queue<GameObject>(); // 재사용 가능한 비활성 인스턴스 Queue입니다.
        private readonly int _maxCount; // 런타임 초과 생성 Warning 기준이 되는 권장 최대 수입니다.
        private readonly bool _allowExpand; // Queue가 비었을 때 추가 생성 허용 여부입니다.
        private readonly bool _useAutoReturn; // 대여 후 자동 반환 사용 여부입니다.
        private readonly float _autoReturnSeconds; // 자동 반환까지의 대기 시간입니다.
        private readonly bool _shouldSkipParentingForNetworkObject; // 오프라인 NetworkObject가 NGO 부모 변경 콜백을 타지 않도록 부모 변경을 생략할지 여부입니다.
        private int _createdCount; // 현재까지 생성한 인스턴스 수입니다.

        public bool UsesAutoReturn => _useAutoReturn;
        public bool ShouldSkipParentingForNetworkObject => _shouldSkipParentingForNetworkObject;

        /// <summary>
        /// Inspector 설정 기반 RuntimePool을 생성하고 초기 개수만큼 미리 생성합니다.
        /// </summary>
        public RuntimePool(GameObject prefab, PoolPrefabConfig config, Transform serviceRoot)
        {
            _prefab = prefab;
            _maxCount = config.MaxCount;
            _allowExpand = config.AllowExpand;
            _useAutoReturn = config.UseAutoReturn;
            _autoReturnSeconds = config.AutoReturnSeconds;
            _shouldSkipParentingForNetworkObject = prefab.GetComponent<Unity.Netcode.NetworkObject>() != null;
            _poolRoot = CreatePoolRoot(prefab, serviceRoot);
            Prewarm(config.InitialCount);
        }

        /// <summary>
        /// 미등록 Prefab 요청용 RuntimePool을 생성합니다.
        /// </summary>
        public RuntimePool(GameObject prefab, int maxCount, Transform serviceRoot)
        {
            _prefab = prefab;
            _maxCount = maxCount;
            _allowExpand = true;
            _useAutoReturn = false;
            _autoReturnSeconds = 0f;
            _shouldSkipParentingForNetworkObject = prefab.GetComponent<Unity.Netcode.NetworkObject>() != null;
            _poolRoot = CreatePoolRoot(prefab, serviceRoot);
        }

        /// <summary>
        /// 사용 가능한 인스턴스를 반환하거나 정책에 따라 새 인스턴스를 생성합니다.
        /// </summary>
        public GameObject Acquire()
        {
            while (_inactiveObjects.Count > 0)
            {
                GameObject instance = _inactiveObjects.Dequeue();
                if (instance != null)
                {
                    return instance;
                }
            }

            if (!_allowExpand && _createdCount > 0)
            {
                Debug.LogWarning($"[LocalObjectPoolManager] Pool empty and expand disabled. prefab={_prefab.name}");
                return null;
            }

            if (_maxCount > 0 && _createdCount >= _maxCount)
            {
                Debug.LogWarning($"[LocalObjectPoolManager] Max count exceeded. Overflow instance will be created and managed by pool. prefab={_prefab.name}, max={_maxCount}, newCount={_createdCount + 1}");
            }
            else
            {
                Debug.LogWarning($"[LocalObjectPoolManager] Pool expanded. prefab={_prefab.name}, newCount={_createdCount + 1}");
            }

            return CreateInstance();
        }

        /// <summary>
        /// 사용 완료된 인스턴스 상태를 정리하고 Queue로 되돌립니다.
        /// </summary>
        public void Release(GameObject instance)
        {
            PooledAutoReturn autoReturn = instance.GetComponent<PooledAutoReturn>();
            if (autoReturn != null)
            {
                autoReturn.CancelSchedule();
            }

            IPoolableObject[] poolables = instance.GetComponentsInChildren<IPoolableObject>(true);
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnPoolDespawned();
            }

            ResetUnityState(instance);

            PooledObjectMarker marker = instance.GetComponent<PooledObjectMarker>();
            marker.MarkReturned();

            if (!_shouldSkipParentingForNetworkObject)
            {
                instance.transform.SetParent(_poolRoot, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
            }

            instance.transform.localScale = Vector3.one;
            instance.SetActive(false);
            _inactiveObjects.Enqueue(instance);
        }

        /// <summary>
        /// 설정된 Prefab에 자동 반환이 켜져 있으면 반환 예약을 시작합니다.
        /// </summary>
        public void ScheduleAutoReturnIfNeeded(GameObject instance, LocalObjectPoolManager manager)
        {
            if (!_useAutoReturn)
            {
                return;
            }

            PooledAutoReturn autoReturn = instance.GetComponent<PooledAutoReturn>();
            if (autoReturn == null)
            {
                autoReturn = instance.AddComponent<PooledAutoReturn>();
            }

            autoReturn.Schedule(manager, _autoReturnSeconds);
        }

        /// <summary>
        /// 초기 Pool 크기만큼 인스턴스를 미리 생성합니다.
        /// </summary>
        private void Prewarm(int initialCount)
        {
            for (int i = 0; i < initialCount; i++)
            {
                if (_maxCount > 0 && _createdCount >= _maxCount)
                {
                    Debug.LogWarning($"[LocalObjectPoolManager] Prewarm stopped by max count. prefab={_prefab.name}, max={_maxCount}");
                    return;
                }

                _inactiveObjects.Enqueue(CreateInstance());
            }
        }

        /// <summary>
        /// 새 인스턴스를 생성하고 Pool 마커를 연결합니다.
        /// </summary>
        private GameObject CreateInstance()
        {
            GameObject instance = _shouldSkipParentingForNetworkObject
                ? Object.Instantiate(_prefab)
                : Object.Instantiate(_prefab, _poolRoot);
            instance.name = $"{_prefab.name}_Pooled_{_createdCount}";

            PooledObjectMarker marker = instance.GetComponent<PooledObjectMarker>();
            if (marker == null)
            {
                marker = instance.AddComponent<PooledObjectMarker>();
            }

            marker.BindSourcePrefab(_prefab);
            ResetUnityState(instance);
            instance.SetActive(false);
            _createdCount++;
            return instance;
        }

        /// <summary>
        /// Pool 보관용 부모 Transform을 생성합니다.
        /// </summary>
        private static Transform CreatePoolRoot(GameObject prefab, Transform serviceRoot)
        {
            GameObject rootObject = new GameObject($"Pool_Local_{prefab.name}");
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
