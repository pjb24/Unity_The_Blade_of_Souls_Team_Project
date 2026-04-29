using System;
using UnityEngine;

/// <summary>
/// Stores common designer-tuned values shared by every boss pattern.
/// </summary>
[Serializable]
public struct PatternCommonSettings
{
    [Tooltip("Stable id used to identify this pattern in logs and runtime lookup.")]
    [SerializeField] private string _patternId; // Stable id used to distinguish this pattern from other boss patterns.

    [Tooltip("Whether this pattern entry is available for validation and future selection.")]
    [SerializeField] private bool _enabled; // Availability flag used by validation and future pattern selection.

    [Tooltip("Pattern behavior category that decides which detailed settings group is used.")]
    [SerializeField] private E_BossPatternType _patternType; // Pattern category used to route execution to the correct future runner.

    [Tooltip("Base score used by future pattern selection logic.")]
    [Min(0f)]
    [SerializeField] private float _selectionWeight; // Designer-controlled selection weight for future pattern choice.

    [Tooltip("Priority used by boss pattern selection. The highest priority candidate is selected first.")]
    [Min(0)]
    [SerializeField] private int _priority; // Priority value used by BossPatternSelector to pick the strongest candidate.

    [Tooltip("Whether this pattern requires a valid target Transform to be selected.")]
    [SerializeField] private bool _requireTarget; // Target requirement used by BossPatternSelector before distance checks.

    [Tooltip("Whether this pattern can be selected while the weak point pattern is active.")]
    [SerializeField] private bool _allowDuringWeakPointActive; // Weak point overlap rule used by BossPatternSelector.

    [Tooltip("Minimum cooldown in seconds before this pattern can be selected again.")]
    [Min(0f)]
    [SerializeField] private float _cooldownSeconds; // Cooldown value that future selection logic will read.

    [Tooltip("Minimum squared distance from target required to allow this pattern.")]
    [Min(0f)]
    [SerializeField] private float _minimumTargetSqrDistance; // Minimum target distance threshold stored as squared distance.

    [Tooltip("Maximum squared distance from target allowed for this pattern.")]
    [Min(0f)]
    [SerializeField] private float _maximumTargetSqrDistance; // Maximum target distance threshold stored as squared distance.

    [Tooltip("Delay in seconds before the pattern starts its active timing.")]
    [Min(0f)]
    [SerializeField] private float _startupSeconds; // Startup duration before a future pattern runner applies the main action.

    [Tooltip("Active duration in seconds used by future pattern execution.")]
    [Min(0f)]
    [SerializeField] private float _activeSeconds; // Active duration that future pattern execution will use.

    [Tooltip("Recovery duration in seconds after the pattern finishes.")]
    [Min(0f)]
    [SerializeField] private float _recoverySeconds; // Recovery duration after the pattern active window ends.

    /// <summary>
    /// Gets the stable pattern id.
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// Gets whether this pattern entry is enabled.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// Gets the pattern behavior category.
    /// </summary>
    public E_BossPatternType PatternType => _patternType;

    /// <summary>
    /// Gets the designer selection weight.
    /// </summary>
    public float SelectionWeight => _selectionWeight;

    /// <summary>
    /// Gets the pattern selection priority.
    /// </summary>
    public int Priority => _priority;

    /// <summary>
    /// Gets whether this pattern requires a target.
    /// </summary>
    public bool RequireTarget => _requireTarget;

    /// <summary>
    /// Gets whether this pattern can be selected while weak points are active.
    /// </summary>
    public bool AllowDuringWeakPointActive => _allowDuringWeakPointActive;

    /// <summary>
    /// Gets the cooldown duration in seconds.
    /// </summary>
    public float CooldownSeconds => _cooldownSeconds;

    /// <summary>
    /// Gets the minimum target distance threshold as squared distance.
    /// </summary>
    public float MinimumTargetSqrDistance => _minimumTargetSqrDistance;

    /// <summary>
    /// Gets the maximum target distance threshold as squared distance.
    /// </summary>
    public float MaximumTargetSqrDistance => _maximumTargetSqrDistance;

    /// <summary>
    /// Gets the startup duration in seconds.
    /// </summary>
    public float StartupSeconds => _startupSeconds;

    /// <summary>
    /// Gets the active duration in seconds.
    /// </summary>
    public float ActiveSeconds => _activeSeconds;

    /// <summary>
    /// Gets the recovery duration in seconds.
    /// </summary>
    public float RecoverySeconds => _recoverySeconds;

    /// <summary>
    /// Corrects invalid values for this common settings entry.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext, int index)
    {
        if (_cooldownSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Pattern cooldown was below zero and clamped. index={index}, patternId={_patternId}, value={_cooldownSeconds}", logContext);
            _cooldownSeconds = 0f;
        }

        if (_priority < 0)
        {
            Debug.LogWarning($"[BossPatternData] Pattern priority was below zero and clamped. index={index}, patternId={_patternId}, value={_priority}", logContext);
            _priority = 0;
        }

        if (_startupSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Startup duration was below zero and clamped. index={index}, patternId={_patternId}, value={_startupSeconds}", logContext);
            _startupSeconds = 0f;
        }

        if (_activeSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Active duration was below zero and clamped. index={index}, patternId={_patternId}, value={_activeSeconds}", logContext);
            _activeSeconds = 0f;
        }

        if (_recoverySeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Recovery duration was below zero and clamped. index={index}, patternId={_patternId}, value={_recoverySeconds}", logContext);
            _recoverySeconds = 0f;
        }
    }
}

/// <summary>
/// Stores health-ratio conditions that define boss phase ranges.
/// </summary>
[Serializable]
public struct HealthPhaseSettings
{
    [Tooltip("Phase number used by future boss phase logic.")]
    [Min(0)]
    [SerializeField] private int _phaseIndex; // Phase index used to group pattern availability.

    [Tooltip("Inclusive minimum normalized health ratio for this phase.")]
    [Range(0f, 1f)]
    [SerializeField] private float _minHealthRatio; // Minimum normalized health ratio for phase activation.

    [Tooltip("Inclusive maximum normalized health ratio for this phase.")]
    [Range(0f, 1f)]
    [SerializeField] private float _maxHealthRatio; // Maximum normalized health ratio for phase activation.

    [Tooltip("Pattern ids that can be used while this health phase is active.")]
    [SerializeField] private string[] _availablePatternIds; // Pattern ids available during this phase.

    /// <summary>
    /// Gets the phase index.
    /// </summary>
    public int PhaseIndex => _phaseIndex;

    /// <summary>
    /// Gets the minimum normalized health ratio.
    /// </summary>
    public float MinHealthRatio => _minHealthRatio;

    /// <summary>
    /// Gets the maximum normalized health ratio.
    /// </summary>
    public float MaxHealthRatio => _maxHealthRatio;

    /// <summary>
    /// Gets the lower normalized health bound for compatibility with previous data reads.
    /// </summary>
    public float HealthRatioLowerBound => _minHealthRatio;

    /// <summary>
    /// Gets the upper normalized health bound for compatibility with previous data reads.
    /// </summary>
    public float HealthRatioUpperBound => _maxHealthRatio;

    /// <summary>
    /// Gets the available pattern id array for this phase.
    /// </summary>
    public string[] AvailablePatternIds => _availablePatternIds;

    /// <summary>
    /// Corrects invalid health ratio values for this phase settings entry.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext, int index)
    {
        float clampedMin = Mathf.Clamp01(_minHealthRatio);
        if (!Mathf.Approximately(clampedMin, _minHealthRatio))
        {
            Debug.LogWarning($"[BossPatternData] MinHealthRatio was outside 0..1 and clamped. index={index}, phase={_phaseIndex}, value={_minHealthRatio}", logContext);
            _minHealthRatio = clampedMin;
        }

        float clampedMax = Mathf.Clamp01(_maxHealthRatio);
        if (!Mathf.Approximately(clampedMax, _maxHealthRatio))
        {
            Debug.LogWarning($"[BossPatternData] MaxHealthRatio was outside 0..1 and clamped. index={index}, phase={_phaseIndex}, value={_maxHealthRatio}", logContext);
            _maxHealthRatio = clampedMax;
        }

        if (_minHealthRatio > _maxHealthRatio)
        {
            Debug.LogWarning($"[BossPatternData] MinHealthRatio was greater than MaxHealthRatio and values were swapped. index={index}, phase={_phaseIndex}, min={_minHealthRatio}, max={_maxHealthRatio}", logContext);
            float previousMin = _minHealthRatio;
            _minHealthRatio = _maxHealthRatio;
            _maxHealthRatio = previousMin;
        }
    }
}

/// <summary>
/// Stores usage limit settings for a single boss pattern.
/// </summary>
[Serializable]
public struct PatternUsageLimit
{
    [Tooltip("Common pattern id that these usage limits belong to.")]
    [SerializeField] private string _patternId; // Common pattern id linked to this usage limit.

    [Tooltip("Maximum consecutive uses allowed for the pattern. Zero means no limit.")]
    [Min(0)]
    [SerializeField] private int _maxConsecutiveUseCount; // Consecutive use cap for future pattern selection.

    [Tooltip("Maximum total uses allowed during one boss encounter. Zero means no limit.")]
    [Min(0)]
    [SerializeField] private int _maxEncounterUseCount; // Encounter-wide use cap for future pattern selection.

    [Tooltip("Minimum number of other patterns that must run before this pattern can repeat.")]
    [Min(0)]
    [SerializeField] private int _minimumOtherPatternCountBeforeRepeat; // Repeat spacing rule for future pattern selection.

    /// <summary>
    /// Gets the common pattern id for this usage limit.
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// Gets the maximum consecutive use count.
    /// </summary>
    public int MaxConsecutiveUseCount => _maxConsecutiveUseCount;

    /// <summary>
    /// Gets the maximum encounter use count.
    /// </summary>
    public int MaxEncounterUseCount => _maxEncounterUseCount;

    /// <summary>
    /// Gets the minimum count of other patterns before this pattern can repeat.
    /// </summary>
    public int MinimumOtherPatternCountBeforeRepeat => _minimumOtherPatternCountBeforeRepeat;
}

/// <summary>
/// Stores pure configuration values for a fan-shaped projectile pattern.
/// </summary>
[Serializable]
public struct FanProjectilePatternSettings
{
    [Tooltip("Whether this fan projectile pattern is enabled.")]
    [SerializeField] private bool _enabled; // Enables validation for required fan projectile references.

    [Tooltip("Common pattern id that these fan projectile settings belong to.")]
    [SerializeField] private string _patternId; // Common pattern id linked to this fan projectile setting group.

    [Tooltip("Projectile prefab used by a future runner. This is an asset reference, not a scene reference.")]
    [SerializeField] private GameObject _projectilePrefab; // Projectile prefab asset used by future projectile spawning.

    [Tooltip("Number of projectile spawn anchors to use from BossPatternAnchorSet.")]
    [Min(1)]
    [SerializeField] private int _spawnPointCount; // Number of scene spawn anchors to consume at execution time.

    [Tooltip("Number of projectile directions generated inside the fan.")]
    [Min(1)]
    [SerializeField] private int _projectileCount; // Number of projectiles emitted by this pattern.

    [Tooltip("Total fan angle in degrees.")]
    [Min(0f)]
    [SerializeField] private float _fanAngleDegrees; // Total spread angle for generated projectile directions.

    [Tooltip("Projectile speed in world units per second.")]
    [Min(0f)]
    [SerializeField] private float _projectileSpeed; // Projectile speed passed to the existing projectile spawn service.

    [Tooltip("Projectile lifetime in seconds.")]
    [Min(0f)]
    [SerializeField] private float _projectileLifetime; // Projectile lifetime passed to the existing projectile spawn service.

    [Tooltip("Raw damage applied by each projectile hit.")]
    [Min(0f)]
    [SerializeField] private float _damage; // Raw damage value for each projectile hit.

    [Tooltip("LayerMask used by spawned projectiles to decide valid collision targets.")]
    [SerializeField] private LayerMask _projectileCollisionLayerMask; // Collision target layer mask passed to spawned projectiles.

    [Tooltip("Status tag included in HitRequest for this pattern.")]
    [SerializeField] private string _statusTag; // Hit status tag used by future damage and reaction routing.

    /// <summary>
    /// Gets whether this fan projectile pattern is enabled.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// Gets the common pattern id for this setting group.
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// Gets the projectile prefab asset reference.
    /// </summary>
    public GameObject ProjectilePrefab => _projectilePrefab;

    /// <summary>
    /// Gets the number of spawn anchors to use.
    /// </summary>
    public int SpawnPointCount => _spawnPointCount;

    /// <summary>
    /// Gets the number of projectiles to emit.
    /// </summary>
    public int ProjectileCount => _projectileCount;

    /// <summary>
    /// Gets the total fan angle in degrees.
    /// </summary>
    public float FanAngleDegrees => _fanAngleDegrees;

    /// <summary>
    /// Gets the projectile speed.
    /// </summary>
    public float ProjectileSpeed => _projectileSpeed;

    /// <summary>
    /// Gets the projectile lifetime.
    /// </summary>
    public float ProjectileLifetime => _projectileLifetime;

    /// <summary>
    /// Gets the raw projectile damage.
    /// </summary>
    public float Damage => _damage;

    /// <summary>
    /// Gets the projectile collision target layer mask.
    /// </summary>
    public LayerMask ProjectileCollisionLayerMask => _projectileCollisionLayerMask;

    /// <summary>
    /// Gets the hit status tag.
    /// </summary>
    public string StatusTag => _statusTag;

    /// <summary>
    /// Corrects invalid values and validates required prefab references for this pattern.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext)
    {
        if (_projectileCount < 1)
        {
            Debug.LogWarning($"[BossPatternData] ProjectileCount was less than 1 and clamped. patternId={_patternId}, value={_projectileCount}", logContext);
            _projectileCount = 1;
        }

        if (_projectileCount % 2 == 0)
        {
            Debug.LogWarning($"[BossPatternData] ProjectileCount was even and increased by 1. patternId={_patternId}, value={_projectileCount}", logContext);
            _projectileCount++;
        }

        if (_projectileLifetime < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Projectile duration was below zero and clamped. patternId={_patternId}, value={_projectileLifetime}", logContext);
            _projectileLifetime = 0f;
        }

        if (_damage < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Fan projectile damage was below zero and clamped. patternId={_patternId}, value={_damage}", logContext);
            _damage = 0f;
        }

        if (_enabled && _projectilePrefab == null)
        {
            Debug.LogWarning($"[BossPatternData] FanProjectile pattern is enabled but ProjectilePrefab is missing. patternId={_patternId}", logContext);
        }
    }
}

/// <summary>
/// Stores pure configuration values for a ground spike pattern.
/// </summary>
[Serializable]
public struct GroundSpikePatternSettings
{
    [Tooltip("Whether this ground spike pattern is enabled.")]
    [SerializeField] private bool _enabled; // Enables validation for this ground spike pattern.

    [Tooltip("Common pattern id that these ground spike settings belong to.")]
    [SerializeField] private string _patternId; // Common pattern id linked to this ground spike setting group.

    [Tooltip("Spike prefab created after the warning duration. This is an asset reference, not a scene reference.")]
    [SerializeField] private GameObject _spikePrefab; // Spike prefab asset spawned by Pattern 2 after warning presentation.

    [Tooltip("Optional warning VFX prefab shown before the spike hit becomes active.")]
    [SerializeField] private GameObject _warningVfxPrefab; // Optional warning VFX prefab spawned before spike creation.

    [Tooltip("EffectService id used to play the warning VFX through the existing pooled VFX system.")]
    [SerializeField] private E_EffectId _warningEffectId; // Existing VFX system id used for pooled warning presentation.

    [Tooltip("Optional attack VFX prefab shown when the spike hit becomes active.")]
    [SerializeField] private GameObject _attackVfxPrefab; // Optional attack VFX prefab spawned when spike damage timing starts.

    [Tooltip("EffectService id used to play the attack VFX through the existing pooled VFX system.")]
    [SerializeField] private E_EffectId _attackEffectId; // Existing VFX system id used for pooled attack presentation.

    [Tooltip("Vertical offset added to the target Player position before the downward ground raycast starts.")]
    [SerializeField] private float _raycastStartYOffset; // Upward offset used to start the ground search above the target Player.

    [Tooltip("Maximum distance used by the downward ground raycast.")]
    [Min(0f)]
    [SerializeField] private float _groundRaycastDistance; // Ground search distance used to place the spike on the floor.

    [Tooltip("LayerMask used by the downward ground raycast.")]
    [SerializeField] private LayerMask _groundLayerMask; // Ground collision layers accepted by Pattern 2 placement raycasts.

    [Tooltip("Delay in seconds between warning presentation and damage application.")]
    [Min(0f)]
    [SerializeField] private float _warningSeconds; // Warning duration before future ground spike damage.

    [Tooltip("Duration in seconds while the spawned spike hit collider remains active.")]
    [Min(0f)]
    [SerializeField] private float _spikeHitDuration; // Active hit duration before Pattern 2 disables spike collision.

    [Tooltip("Number of spike hits generated during this pattern.")]
    [Min(1)]
    [SerializeField] private int _spikeCount; // Number of spike areas generated by this pattern.

    [Tooltip("Delay in seconds between sequential spike hits.")]
    [Min(0f)]
    [SerializeField] private float _intervalSeconds; // Time gap between spike activations.

    [Tooltip("Box area size used for each spike hit.")]
    [SerializeField] private Vector2 _boxSize; // Box hit area size for each spike.

    [Tooltip("LayerMask used to find HitReceiver targets hit by the spike.")]
    [SerializeField] private LayerMask _spikeTargetLayerMask; // Hit target layers used by Pattern 2 authority-side overlap checks.

    [Tooltip("Raw damage applied by each spike hit.")]
    [Min(0f)]
    [SerializeField] private float _damage; // Raw SpikeDamage value for each ground spike hit.

    [Tooltip("Status tag included in HitRequest for this pattern.")]
    [SerializeField] private string _statusTag; // Hit status tag used by future damage and reaction routing.

    /// <summary>
    /// Gets whether this ground spike pattern is enabled.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// Gets the common pattern id for this setting group.
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// Gets the spike prefab asset reference.
    /// </summary>
    public GameObject SpikePrefab => _spikePrefab;

    /// <summary>
    /// Gets the optional warning VFX prefab asset reference.
    /// </summary>
    public GameObject WarningVfxPrefab => _warningVfxPrefab;

    /// <summary>
    /// Gets the pooled warning VFX id.
    /// </summary>
    public E_EffectId WarningEffectId => _warningEffectId;

    /// <summary>
    /// Gets the optional attack VFX prefab asset reference.
    /// </summary>
    public GameObject AttackVfxPrefab => _attackVfxPrefab;

    /// <summary>
    /// Gets the pooled attack VFX id.
    /// </summary>
    public E_EffectId AttackEffectId => _attackEffectId;

    /// <summary>
    /// Gets the vertical offset used before the downward ground raycast starts.
    /// </summary>
    public float RaycastStartYOffset => _raycastStartYOffset;

    /// <summary>
    /// Gets the ground raycast distance.
    /// </summary>
    public float GroundRaycastDistance => _groundRaycastDistance;

    /// <summary>
    /// Gets the ground raycast LayerMask.
    /// </summary>
    public LayerMask GroundLayerMask => _groundLayerMask;

    /// <summary>
    /// Gets the warning duration in seconds.
    /// </summary>
    public float WarningSeconds => _warningSeconds;

    /// <summary>
    /// Gets the warning duration in seconds for Pattern 2 naming compatibility.
    /// </summary>
    public float SpikeWarningDuration => _warningSeconds;

    /// <summary>
    /// Gets the duration while the spike hit collider remains active.
    /// </summary>
    public float SpikeHitDuration => _spikeHitDuration;

    /// <summary>
    /// Gets the number of spike hits.
    /// </summary>
    public int SpikeCount => _spikeCount;

    /// <summary>
    /// Gets the delay between spike hits.
    /// </summary>
    public float IntervalSeconds => _intervalSeconds;

    /// <summary>
    /// Gets the spike box size.
    /// </summary>
    public Vector2 BoxSize => _boxSize;

    /// <summary>
    /// Gets the spike hit target LayerMask.
    /// </summary>
    public LayerMask SpikeTargetLayerMask => _spikeTargetLayerMask;

    /// <summary>
    /// Gets the raw spike damage.
    /// </summary>
    public float Damage => _damage;

    /// <summary>
    /// Gets the raw spike damage.
    /// </summary>
    public float SpikeDamage => _damage;

    /// <summary>
    /// Gets the hit status tag.
    /// </summary>
    public string StatusTag => _statusTag;

    /// <summary>
    /// Corrects invalid values for this pattern.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext)
    {
        if (_enabled && _spikePrefab == null)
        {
            Debug.LogWarning($"[BossPatternData] GroundSpike pattern is enabled but SpikePrefab is missing. patternId={_patternId}", logContext);
        }

        if (_groundRaycastDistance < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Ground spike raycast distance was below zero and clamped. patternId={_patternId}, value={_groundRaycastDistance}", logContext);
            _groundRaycastDistance = 0f;
        }

        if (_warningSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Ground spike warning duration was below zero and clamped. patternId={_patternId}, value={_warningSeconds}", logContext);
            _warningSeconds = 0f;
        }

        if (_spikeHitDuration < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Ground spike hit duration was below zero and clamped. patternId={_patternId}, value={_spikeHitDuration}", logContext);
            _spikeHitDuration = 0f;
        }

        if (_intervalSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Ground spike interval duration was below zero and clamped. patternId={_patternId}, value={_intervalSeconds}", logContext);
            _intervalSeconds = 0f;
        }

        if (_damage < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Ground spike damage was below zero and clamped. patternId={_patternId}, value={_damage}", logContext);
            _damage = 0f;
        }
    }
}

/// <summary>
/// Stores pure configuration values for a monster summon pattern.
/// </summary>
[Serializable]
public struct SummonMonsterPatternSettings
{
    [Tooltip("Whether this summon monster pattern is enabled.")]
    [SerializeField] private bool _enabled; // Enables validation for required summon monster references.

    [Tooltip("Common pattern id that these summon settings belong to.")]
    [SerializeField] private string _patternId; // Common pattern id linked to this summon setting group.

    [Tooltip("Monster prefab asset used by a future summon runner.")]
    [SerializeField] private GameObject _monsterPrefab; // Monster prefab asset used for future spawn logic.

    [Tooltip("Number of monster spawn anchors to select from BossPatternAnchorSet during one Pattern 3 execution.")]
    [Min(1)]
    [SerializeField] private int _spawnPointCount; // SpawnCount value that decides how many monster spawn anchors Pattern 3 selects.

    [Tooltip("Maximum alive summoned monsters allowed for this pattern. Zero means no pattern-level cap.")]
    [Min(0)]
    [SerializeField] private int _maxAliveCount; // Alive summon cap for future pattern logic.

    [Tooltip("Delay in seconds between sequential monster spawns.")]
    [Min(0f)]
    [SerializeField] private float _spawnIntervalSeconds; // Time gap between monster spawn requests.

    /// <summary>
    /// Gets whether this summon monster pattern is enabled.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// Gets the common pattern id for this setting group.
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// Gets the monster prefab asset reference.
    /// </summary>
    public GameObject MonsterPrefab => _monsterPrefab;

    /// <summary>
    /// Gets the number of monster spawn anchors to use.
    /// </summary>
    public int SpawnPointCount => _spawnPointCount;

    /// <summary>
    /// Gets the number of monster spawn anchors to select.
    /// </summary>
    public int SpawnCount => _spawnPointCount;

    /// <summary>
    /// Gets the maximum alive summon count.
    /// </summary>
    public int MaxAliveCount => _maxAliveCount;

    /// <summary>
    /// Gets the delay between monster spawn requests.
    /// </summary>
    public float SpawnIntervalSeconds => _spawnIntervalSeconds;

    /// <summary>
    /// Corrects invalid values and validates required prefab references for this pattern.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext)
    {
        if (_spawnPointCount < 1)
        {
            Debug.LogWarning($"[BossPatternData] Summon monster SpawnCount was less than 1 and clamped. patternId={_patternId}, value={_spawnPointCount}", logContext);
            _spawnPointCount = 1;
        }

        if (_spawnIntervalSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Summon monster interval duration was below zero and clamped. patternId={_patternId}, value={_spawnIntervalSeconds}", logContext);
            _spawnIntervalSeconds = 0f;
        }

        if (_enabled && _monsterPrefab == null)
        {
            Debug.LogWarning($"[BossPatternData] SummonMonster pattern is enabled but MonsterPrefab is missing. patternId={_patternId}", logContext);
        }
    }
}

/// <summary>
/// Stores pure configuration values for a weak point pattern.
/// </summary>
[Serializable]
public struct WeakPointPatternSettings
{
    [Tooltip("Whether this weak point pattern is enabled.")]
    [SerializeField] private bool _enabled; // Enables validation for this weak point pattern.

    [Tooltip("Common pattern id that these weak point settings belong to.")]
    [SerializeField] private string _patternId; // Common pattern id linked to this weak point setting group.

    [Tooltip("Number of weak point areas to activate from BossPatternAnchorSet.")]
    [Min(0)]
    [SerializeField] private int _weakPointCount; // Number of scene weak point areas to activate at execution time.

    [Tooltip("Retry count used by future weak point position selection.")]
    [Min(1)]
    [SerializeField] private int _weakPointPositionRetryCount; // Retry count for future weak point placement validation.

    [Tooltip("Duration in seconds while weak point areas remain active.")]
    [Min(0f)]
    [SerializeField] private float _activeSeconds; // Duration of future weak point vulnerability.

    [Tooltip("Damage multiplier applied while weak points are active.")]
    [Min(0f)]
    [SerializeField] private float _incomingDamageMultiplier; // Damage multiplier for future weak point damage handling.

    [Tooltip("Status tag that future hit handling treats as a weak point hit.")]
    [SerializeField] private string _weakPointStatusTag; // Hit status tag used to identify weak point hits.

    /// <summary>
    /// Gets whether this weak point pattern is enabled.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// Gets the common pattern id for this setting group.
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// Gets the number of weak point areas to activate.
    /// </summary>
    public int WeakPointCount => _weakPointCount;

    /// <summary>
    /// Gets the number of weak point areas to activate for compatibility with previous data reads.
    /// </summary>
    public int ActiveAreaCount => _weakPointCount;

    /// <summary>
    /// Gets the weak point position retry count.
    /// </summary>
    public int WeakPointPositionRetryCount => _weakPointPositionRetryCount;

    /// <summary>
    /// Gets the active duration in seconds.
    /// </summary>
    public float ActiveSeconds => _activeSeconds;

    /// <summary>
    /// Gets the incoming damage multiplier.
    /// </summary>
    public float IncomingDamageMultiplier => _incomingDamageMultiplier;

    /// <summary>
    /// Gets the weak point status tag.
    /// </summary>
    public string WeakPointStatusTag => _weakPointStatusTag;

    /// <summary>
    /// Corrects invalid values for this pattern.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext)
    {
        if (_weakPointCount < 0)
        {
            Debug.LogWarning($"[BossPatternData] WeakPointCount was below zero and clamped. patternId={_patternId}, value={_weakPointCount}", logContext);
            _weakPointCount = 0;
        }

        if (_weakPointPositionRetryCount < 1)
        {
            Debug.LogWarning($"[BossPatternData] WeakPointPositionRetryCount was less than 1 and clamped. patternId={_patternId}, value={_weakPointPositionRetryCount}", logContext);
            _weakPointPositionRetryCount = 1;
        }

        if (_activeSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Weak point active duration was below zero and clamped. patternId={_patternId}, value={_activeSeconds}", logContext);
            _activeSeconds = 0f;
        }
    }
}
