using UnityEngine;

/// <summary>
/// Pattern 4의 개별 약점(Weak Point) Health 사망 이벤트를
/// 해당 보스 패턴으로 전달하는 브리지 역할을 수행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossWeakPointObject : MonoBehaviour, IHealthListener, IPoolableObject
{
    [Tooltip("기존 Health 시스템을 통해 플레이어 공격 데미지를 받는 HealthComponent")]
    [SerializeField] private HealthComponent _healthComponent; // 이 약점의 생명값으로 사용하는 기존 Health 시스템 컴포넌트

    [Tooltip("기존 Hit 시스템을 통해 플레이어 공격을 받을 수 있게 하는 HitReceiver")]
    [SerializeField] private HitReceiver _hitReceiver; // 이 약점이 피격될 수 있는 진입점 역할을 하는 기존 Hit 시스템 컴포넌트

    private BossWeakPointPattern _ownerPattern; // 이 약점을 소유한 Pattern 4 인스턴스
    private int _weakPointIndex = -1; // ownerPattern 내에서의 약점 인덱스
    private bool _isInitialized; // Initialize를 통해 owner와 연결되었는지 여부
    private bool _isDestroyedNotified; // 사망 이벤트가 이미 owner에게 전달되었는지 여부
    private bool _isHealthListenerRegistered; // HealthComponent에 Listener로 등록되었는지 여부

    /// <summary>
    /// ObjectPool에서 대여될 때 이전 Pattern 연결 상태가 남지 않도록 초기 상태를 정리합니다.
    /// </summary>
    public void OnPoolSpawned(PoolSpawnContext context)
    {
        Release();
        _isDestroyedNotified = false;
        ResolveRuntimeComponents();
    }

    /// <summary>
    /// ObjectPool로 반환될 때 Health Listener와 owner 참조를 정리합니다.
    /// </summary>
    public void OnPoolDespawned()
    {
        Release();
    }

    /// <summary>
    /// 약점을 owner 패턴과 연결하고 Health/Hit 컴포넌트를 초기화한다.
    /// </summary>
    public void Initialize(BossWeakPointPattern ownerPattern, int weakPointIndex)
    {
        _ownerPattern = ownerPattern;
        _weakPointIndex = weakPointIndex;
        _isInitialized = true;
        _isDestroyedNotified = false;

        ResolveRuntimeComponents();
        ResetHealth();
        RegisterHealthListener();
    }

    /// <summary>
    /// 약점이 제거되거나 반환되기 전에 Listener 연결을 해제한다.
    /// </summary>
    public void Release()
    {
        UnregisterHealthListener();

        _ownerPattern = null;
        _weakPointIndex = -1;
        _isInitialized = false;
    }

    /// <summary>
    /// 오브젝트 생성 시 필요한 컴포넌트 참조를 확보한다.
    /// </summary>
    private void Awake()
    {
        ResolveRuntimeComponents();
    }

    /// <summary>
    /// Unity에서 비활성화될 때 Listener를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterHealthListener();
    }

    /// <summary>
    /// Health 변경 이벤트 수신 (약점 파괴는 OnDied만 사용)
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
    }

    /// <summary>
    /// 데미지 이벤트 수신 (약점 파괴는 OnDied만 사용)
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// 회복 이벤트 수신 (약점 파괴는 OnDied만 사용)
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 약점이 파괴되었을 때 owner 패턴에 알린다.
    /// </summary>
    public void OnDied()
    {
        if (_isDestroyedNotified)
        {
            return;
        }

        _isDestroyedNotified = true;

        if (!_isInitialized || _ownerPattern == null)
        {
            Debug.LogWarning($"[BossWeakPointObject] owner 초기화 전에 약점이 파괴됨. object={name}", this);
            return;
        }

        _ownerPattern.HandleWeakPointDestroyed(this, _weakPointIndex);
    }

    /// <summary>
    /// 부활 시 다시 사망 이벤트를 전달할 수 있도록 상태를 초기화한다.
    /// </summary>
    public void OnRevived()
    {
        _isDestroyedNotified = false;
    }

    /// <summary>
    /// 최대 체력 변경 이벤트 수신 (Pattern 4에서는 사용하지 않음)
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }

    /// <summary>
    /// 기존 데미지 흐름을 위해 HealthComponent와 HitReceiver를 확보하거나 생성한다.
    /// </summary>
    private void ResolveRuntimeComponents()
    {
        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }

        if (_healthComponent == null)
        {
            Debug.LogWarning($"[BossWeakPointObject] HealthComponent가 없어 런타임에 추가됨. object={name}", this);
            _healthComponent = gameObject.AddComponent<HealthComponent>();
        }

        if (_hitReceiver == null)
        {
            _hitReceiver = GetComponent<HitReceiver>();
        }

        if (_hitReceiver == null)
        {
            Debug.LogWarning($"[BossWeakPointObject] HitReceiver가 없어 런타임에 추가됨. object={name}", this);
            _hitReceiver = gameObject.AddComponent<HitReceiver>();
        }
    }

    /// <summary>
    /// 약점 재사용 시 체력을 초기 상태로 되돌린다.
    /// </summary>
    private void ResetHealth()
    {
        if (_healthComponent == null)
        {
            Debug.LogWarning($"[BossWeakPointObject] Health 초기화 실패 - HealthComponent 없음. object={name}", this);
            return;
        }

        _healthComponent.ResetHealthToInitialHealth();
    }

    /// <summary>
    /// HealthComponent 이벤트에 Listener로 등록한다.
    /// </summary>
    private void RegisterHealthListener()
    {
        if (_isHealthListenerRegistered || _healthComponent == null)
        {
            return;
        }

        _healthComponent.AddListener(this);
        _isHealthListenerRegistered = true;
    }

    /// <summary>
    /// HealthComponent 이벤트 Listener 등록을 해제한다.
    /// </summary>
    private void UnregisterHealthListener()
    {
        if (!_isHealthListenerRegistered || _healthComponent == null)
        {
            return;
        }

        _healthComponent.RemoveListener(this);
        _isHealthListenerRegistered = false;
    }
}
