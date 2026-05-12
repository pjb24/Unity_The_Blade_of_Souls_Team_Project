using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버에서 확정된 보스 HealthComponent 상태를 NGO NetworkVariable로 복제하고 Client의 로컬 HealthComponent에 반영합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class BossHealthNetworkSync : NetworkBehaviour, IHealthListener
{
    private struct ReplicatedBossHealthState : INetworkSerializable, System.IEquatable<ReplicatedBossHealthState>
    {
        public float CurrentHealth; // 서버가 확정한 현재 체력 값입니다.
        public float MaxHealth; // 서버가 확정한 최대 체력 값입니다.
        public bool IsDead; // 서버가 확정한 사망 상태입니다.
        public int Revision; // 체력 스냅샷 적용 순서를 판단하기 위한 증가 순번입니다.

        /// <summary>
        /// 서버 확정 보스 체력 스냅샷을 하나의 네트워크 페이로드로 생성합니다.
        /// </summary>
        public ReplicatedBossHealthState(float currentHealth, float maxHealth, bool isDead, int revision)
        {
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            IsDead = isDead;
            Revision = revision;
        }

        /// <summary>
        /// NGO가 보스 체력 스냅샷을 원자적인 값으로 복제할 수 있도록 직렬화합니다.
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CurrentHealth);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref IsDead);
            serializer.SerializeValue(ref Revision);
        }

        /// <summary>
        /// NetworkVariable이 스냅샷 변경 여부를 정확히 판단할 수 있도록 값 동등성을 비교합니다.
        /// </summary>
        public bool Equals(ReplicatedBossHealthState other)
        {
            return Mathf.Approximately(CurrentHealth, other.CurrentHealth)
                && Mathf.Approximately(MaxHealth, other.MaxHealth)
                && IsDead == other.IsDead
                && Revision == other.Revision;
        }
    }

    [Header("Dependencies")]
    [Tooltip("네트워크로 복제할 보스 체력 컴포넌트입니다. 비어 있으면 같은 오브젝트의 BossController 또는 HealthComponent에서 자동 탐색합니다.")]
    [SerializeField] private HealthComponent _bossHealthComponent; // 서버 확정 보스 체력 상태를 읽고 Client 로컬 상태를 갱신할 HealthComponent 참조입니다.

    [Tooltip("보스 체력 컴포넌트를 찾을 때 우선 사용할 BossController입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private BossController _bossController; // BossController가 보유한 HealthComponent 참조를 재사용하기 위한 보스 컨트롤러 참조입니다.

    [Header("Sync Policy")]
    [Tooltip("서버 확정 보스 체력 상태를 네트워크로 복제할지 여부입니다.")]
    [SerializeField] private bool _enableHealthStateSync = true; // 보스 체력 네트워크 복제 기능의 활성화 여부입니다.

    [Tooltip("Client에서 수신한 서버 확정 체력 상태를 로컬 HealthComponent에 반영할지 여부입니다.")]
    [SerializeField] private bool _applyReplicatedHealthOnClient = true; // Client HealthComponent를 서버 스냅샷과 동기화할지 여부입니다.

    [Tooltip("HealthComponent 참조가 없을 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnMissingHealthComponent = true; // 보스 HealthComponent 누락 경고 출력 여부입니다.

    [Header("Debug")]
    [Tooltip("마지막으로 복제된 현재 체력입니다.")]
    [SerializeField] private float _debugReplicatedCurrentHealth; // Inspector 확인용 최근 복제 현재 체력 값입니다.

    [Tooltip("마지막으로 복제된 최대 체력입니다.")]
    [SerializeField] private float _debugReplicatedMaxHealth = 1f; // Inspector 확인용 최근 복제 최대 체력 값입니다.

    [Tooltip("마지막으로 복제된 사망 상태입니다.")]
    [SerializeField] private bool _debugReplicatedIsDead; // Inspector 확인용 최근 복제 사망 상태입니다.

    private readonly NetworkVariable<ReplicatedBossHealthState> _replicatedHealthState = new NetworkVariable<ReplicatedBossHealthState>(
        new ReplicatedBossHealthState(0f, 1f, false, 0),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 보스 체력 스냅샷을 하나의 순서 보장 단위로 복제하는 네트워크 변수입니다.

    private bool _isHealthListenerRegistered; // 서버 HealthComponent에 Listener로 등록되었는지 추적하는 플래그입니다.
    private int _lastAppliedHealthRevision = -1; // Client 로컬 HealthComponent에 마지막으로 적용한 스냅샷 순번입니다.
    private bool _hasWarnedMissingHealthComponent; // HealthComponent 누락 경고 중복 출력을 막는 플래그입니다.

    public event System.Action ReplicatedHealthSnapshotChanged; // UI가 HealthComponent 이벤트 손실 없이 네트워크 체력 스냅샷 변경을 직접 수신하기 위한 이벤트입니다.

    /// <summary>
    /// 현재 로컬에서 확인 가능한 보스 HealthComponent를 반환합니다.
    /// </summary>
    public HealthComponent BossHealthComponent => _bossHealthComponent;

    /// <summary>
    /// 복제된 현재 체력을 반환합니다.
    /// </summary>
    public float ReplicatedCurrentHealth => _replicatedHealthState.Value.CurrentHealth;

    /// <summary>
    /// 복제된 최대 체력을 반환합니다.
    /// </summary>
    public float ReplicatedMaxHealth => _replicatedHealthState.Value.MaxHealth;

    /// <summary>
    /// 복제된 체력 스냅샷의 최신 순번을 반환합니다.
    /// </summary>
    public int ReplicatedHealthRevision => _replicatedHealthState.Value.Revision;

    /// <summary>
    /// 복제된 체력 비율을 0~1 범위로 반환합니다.
    /// </summary>
    public float ReplicatedNormalizedHealth
    {
        get
        {
            float safeMaxHealth = Mathf.Max(1f, _replicatedHealthState.Value.MaxHealth); // 0 나눗셈을 막기 위한 보정 최대 체력입니다.
            return Mathf.Clamp01(_replicatedHealthState.Value.CurrentHealth / safeMaxHealth);
        }
    }

    /// <summary>
    /// 컴포넌트 초기화 시 보스 체력 참조를 자동 보정합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveBossHealthComponent();
    }

    /// <summary>
    /// 활성화 시 서버 권한 인스턴스에서 HealthComponent 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        RegisterHealthListenerIfServer();
    }

    /// <summary>
    /// 비활성화 시 HealthComponent 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterHealthListener();
    }

    /// <summary>
    /// 네트워크 스폰 이후 늦게 준비되는 HealthComponent와 복제 스냅샷을 지속적으로 보정합니다.
    /// </summary>
    private void Update()
    {
        MaintainHealthSyncBinding();
    }

    /// <summary>
    /// Inspector 값 변경 시 자동 참조와 디자이너 입력값을 검증합니다.
    /// </summary>
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            TryResolveBossHealthComponent();
        }
    }

    /// <summary>
    /// 네트워크 스폰 후 체력 스냅샷 콜백을 연결하고 현재 값을 즉시 동기화합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        _replicatedHealthState.OnValueChanged += HandleReplicatedHealthStateChanged;

        if (IsServer)
        {
            RegisterHealthListenerIfServer();
            PublishHealthSnapshot();
            return;
        }

        ApplyReplicatedHealthSnapshot();
    }

    /// <summary>
    /// 네트워크 디스폰 시 체력 스냅샷 콜백과 HealthComponent 구독을 정리합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        _replicatedHealthState.OnValueChanged -= HandleReplicatedHealthStateChanged;
        UnregisterHealthListener();
    }

    /// <summary>
    /// 체력 수치 변경 시 서버가 새 체력 스냅샷을 발행합니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 데미지 이벤트 후 서버가 새 체력 스냅샷을 발행합니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 회복 이벤트 후 서버가 새 체력 스냅샷을 발행합니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 사망 이벤트 후 서버가 새 체력 스냅샷을 발행합니다.
    /// </summary>
    public void OnDied()
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 부활 이벤트 후 서버가 새 체력 스냅샷을 발행합니다.
    /// </summary>
    public void OnRevived()
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 최대 체력 변경 시 서버가 새 체력 스냅샷을 발행합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
        PublishHealthSnapshot();
    }

    /// <summary>
    /// 서버 HealthComponent 이벤트를 구독합니다.
    /// </summary>
    private void RegisterHealthListenerIfServer()
    {
        if (!_enableHealthStateSync || _isHealthListenerRegistered)
        {
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
        {
            return;
        }

        if (!TryResolveBossHealthComponent())
        {
            return;
        }

        _bossHealthComponent.AddListener(this);
        _isHealthListenerRegistered = true;
    }

    /// <summary>
    /// 서버에서는 HealthComponent 구독을 보장하고 Client에서는 아직 적용하지 못한 최신 스냅샷을 반영합니다.
    /// </summary>
    private void MaintainHealthSyncBinding()
    {
        if (!_enableHealthStateSync || !IsSpawned)
        {
            return;
        }

        if (IsServer)
        {
            if (!_isHealthListenerRegistered)
            {
                RegisterHealthListenerIfServer();

                if (_isHealthListenerRegistered)
                {
                    PublishHealthSnapshot();
                }
            }

            return;
        }

        if (_lastAppliedHealthRevision == _replicatedHealthState.Value.Revision)
        {
            return;
        }

        ApplyReplicatedHealthSnapshot();
    }

    /// <summary>
    /// HealthComponent 이벤트 구독을 해제합니다.
    /// </summary>
    private void UnregisterHealthListener()
    {
        if (!_isHealthListenerRegistered)
        {
            return;
        }

        if (_bossHealthComponent != null && _bossHealthComponent.IsInitialized)
        {
            _bossHealthComponent.RemoveListener(this);
        }

        _isHealthListenerRegistered = false;
    }

    /// <summary>
    /// 서버가 현재 보스 체력 상태를 네트워크 변수에 기록합니다.
    /// </summary>
    private void PublishHealthSnapshot()
    {
        if (!_enableHealthStateSync || !IsServer || !TryResolveBossHealthComponent())
        {
            return;
        }

        ReplicatedBossHealthState previousSnapshot = _replicatedHealthState.Value; // 새 순번을 계산하기 위한 이전 스냅샷입니다.
        ReplicatedBossHealthState nextSnapshot = new ReplicatedBossHealthState(
            _bossHealthComponent.GetCurrentHealth(),
            Mathf.Max(1f, _bossHealthComponent.GetMaxHealth()),
            _bossHealthComponent.IsDead,
            previousSnapshot.Revision + 1);

        _replicatedHealthState.Value = nextSnapshot;
        CacheDebugSnapshot();
        NotifyReplicatedHealthSnapshotChanged();
    }

    /// <summary>
    /// Client에서 체력 스냅샷 값이 바뀌면 로컬 HealthComponent에 최신 값을 적용합니다.
    /// </summary>
    private void HandleReplicatedHealthStateChanged(ReplicatedBossHealthState previousValue, ReplicatedBossHealthState currentValue)
    {
        ApplyReplicatedHealthSnapshot();
    }

    /// <summary>
    /// 복제된 체력 스냅샷을 Client 로컬 HealthComponent에 반영합니다.
    /// </summary>
    private void ApplyReplicatedHealthSnapshot()
    {
        CacheDebugSnapshot();
        NotifyReplicatedHealthSnapshotChanged();

        if (!_enableHealthStateSync || !_applyReplicatedHealthOnClient || IsServer)
        {
            return;
        }

        ReplicatedBossHealthState snapshot = _replicatedHealthState.Value; // 하나의 NetworkVariable로 묶여 도착한 서버 확정 체력 상태입니다.

        if (_lastAppliedHealthRevision == snapshot.Revision)
        {
            return;
        }

        if (!TryResolveBossHealthComponent())
        {
            return;
        }

        float safeMaxHealth = Mathf.Max(1f, snapshot.MaxHealth);
        float safeCurrentHealth = Mathf.Clamp(snapshot.CurrentHealth, 0f, safeMaxHealth);

        _bossHealthComponent.SetMaxHealth(safeMaxHealth, false);
        _bossHealthComponent.SetCurrentHealth(safeCurrentHealth);
        _bossHealthComponent.NotifyCurrentHealthState();
        _lastAppliedHealthRevision = snapshot.Revision;
    }

    /// <summary>
    /// Inspector 디버그 필드에 최근 복제 값을 캐시합니다.
    /// </summary>
    private void CacheDebugSnapshot()
    {
        ReplicatedBossHealthState snapshot = _replicatedHealthState.Value; // Inspector 디버그 값에 반영할 최신 네트워크 스냅샷입니다.
        _debugReplicatedCurrentHealth = snapshot.CurrentHealth;
        _debugReplicatedMaxHealth = snapshot.MaxHealth;
        _debugReplicatedIsDead = snapshot.IsDead;
    }

    /// <summary>
    /// 네트워크 체력 스냅샷 변경을 외부 UI 브리지에 알립니다.
    /// </summary>
    private void NotifyReplicatedHealthSnapshotChanged()
    {
        ReplicatedHealthSnapshotChanged?.Invoke();
    }

    /// <summary>
    /// BossController 또는 같은 계층에서 보스 HealthComponent 참조를 찾습니다.
    /// </summary>
    private bool TryResolveBossHealthComponent()
    {
        if (_bossHealthComponent != null)
        {
            return true;
        }

        if (_bossController == null)
        {
            _bossController = GetComponent<BossController>();
        }

        if (_bossController != null && _bossController.HealthComponent != null)
        {
            _bossHealthComponent = _bossController.HealthComponent;
            return true;
        }

        _bossHealthComponent = GetComponent<HealthComponent>();
        if (_bossHealthComponent == null)
        {
            _bossHealthComponent = GetComponentInChildren<HealthComponent>(true);
        }

        if (_bossHealthComponent != null)
        {
            return true;
        }

        if (_warnMissingHealthComponent && !_hasWarnedMissingHealthComponent)
        {
            Debug.LogWarning($"[BossHealthNetworkSync] 보스 HealthComponent를 찾을 수 없어 체력 동기화를 수행할 수 없습니다. object={name}", this);
            _hasWarnedMissingHealthComponent = true;
        }

        return false;
    }
}
