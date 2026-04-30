using UnityEngine;

/// <summary>
/// Bridges an individual Pattern 4 weak point Health death event back to the owning boss pattern.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossWeakPointObject : MonoBehaviour, IHealthListener
{
    [Tooltip("HealthComponent that receives Player attack damage through the existing Health system.")]
    [SerializeField] private HealthComponent _healthComponent; // Existing Health system component used as this weak point's life source.

    [Tooltip("HitReceiver that lets Player attacks damage this weak point through the existing Hit system.")]
    [SerializeField] private HitReceiver _hitReceiver; // Existing Hit system receiver used as this weak point's damageable entry point.

    private BossWeakPointPattern _ownerPattern; // Pattern 4 instance that owns this weak point.
    private int _weakPointIndex = -1; // Index in the owner pattern weak point buffer.
    private bool _isInitialized; // Whether Initialize has connected this weak point to an owner.
    private bool _isDestroyedNotified; // Whether death has already been reported to the owner.
    private bool _isHealthListenerRegistered; // Whether this object is subscribed to HealthComponent callbacks.

    /// <summary>
    /// Connects this weak point to the owner pattern and existing Health/Hit components.
    /// </summary>
    public void Initialize(BossWeakPointPattern ownerPattern, int weakPointIndex)
    {
        _ownerPattern = ownerPattern;
        _weakPointIndex = weakPointIndex;
        _isInitialized = true;
        _isDestroyedNotified = false;
        ResolveRuntimeComponents();
        RegisterHealthListener();
    }

    /// <summary>
    /// Releases listener connections before this weak point is destroyed or returned.
    /// </summary>
    public void Release()
    {
        UnregisterHealthListener();
        _ownerPattern = null;
        _weakPointIndex = -1;
        _isInitialized = false;
    }

    /// <summary>
    /// Resolves weak point component references when the object wakes.
    /// </summary>
    private void Awake()
    {
        ResolveRuntimeComponents();
    }

    /// <summary>
    /// Ensures listener cleanup when Unity disables this weak point.
    /// </summary>
    private void OnDisable()
    {
        UnregisterHealthListener();
    }

    /// <summary>
    /// Receives Health changed notifications; weak point destruction only uses OnDied.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
    }

    /// <summary>
    /// Receives damage notifications; weak point destruction only uses OnDied.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// Receives heal notifications; weak point destruction only uses OnDied.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// Reports weak point destruction to the owner pattern on the authority instance.
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
            Debug.LogWarning($"[BossWeakPointObject] Weak point died before owner initialization. object={name}", this);
            return;
        }

        _ownerPattern.HandleWeakPointDestroyed(this, _weakPointIndex);
    }

    /// <summary>
    /// Receives revive notifications and allows future death notification again.
    /// </summary>
    public void OnRevived()
    {
        _isDestroyedNotified = false;
    }

    /// <summary>
    /// Receives max health changed notifications; no Pattern 4 behavior is required here.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }

    /// <summary>
    /// Resolves or creates the HealthComponent and HitReceiver needed for existing damage flow.
    /// </summary>
    private void ResolveRuntimeComponents()
    {
        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }

        if (_healthComponent == null)
        {
            Debug.LogWarning($"[BossWeakPointObject] HealthComponent was missing and added at runtime. object={name}", this);
            _healthComponent = gameObject.AddComponent<HealthComponent>();
        }

        if (_hitReceiver == null)
        {
            _hitReceiver = GetComponent<HitReceiver>();
        }

        if (_hitReceiver == null)
        {
            Debug.LogWarning($"[BossWeakPointObject] HitReceiver was missing and added at runtime. object={name}", this);
            _hitReceiver = gameObject.AddComponent<HitReceiver>();
        }
    }

    /// <summary>
    /// Subscribes to HealthComponent events through the existing AddListener API.
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
    /// Unsubscribes from HealthComponent events through the existing RemoveListener API.
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
