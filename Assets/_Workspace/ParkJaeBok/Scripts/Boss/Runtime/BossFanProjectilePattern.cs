using UnityEngine;

/// <summary>
/// Executes Pattern 1 by firing a fan of pooled projectiles toward the nearest valid Player.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossFanProjectilePattern : BossPatternBase
{
    [Header("Required References")]
    [Tooltip("Boss controller that owns authority, pattern data, anchors, and Player search.")]
    [SerializeField] private BossController _bossController; // Boss authority and shared data source for Pattern 1.

    [Tooltip("Scene anchor set that provides projectile spawn points.")]
    [SerializeField] private BossPatternAnchorSet _anchorSet; // Scene projectile spawn point source used by Pattern 1.

    [Tooltip("Projectile spawn service source. Must implement IProjectileSpawnService.")]
    [SerializeField] private MonoBehaviour _projectileSpawnServiceSource; // Serialized MonoBehaviour source for the projectile spawn service interface.

    [Header("Execution")]
    [Tooltip("Range used to search the nearest Player when Pattern 1 starts.")]
    [Min(0f)]
    [SerializeField] private float _executionRange = 20f; // Player search range used only at Pattern 1 execution time.

    [Tooltip("Whether scene-wide projectile spawn service fallback lookup is allowed when no service is assigned.")]
    [SerializeField] private bool _allowSceneProjectileSpawnServiceFallback = true; // Fallback toggle for resolving existing projectile spawn service.

    private IProjectileSpawnService _projectileSpawnService; // Existing projectile spawn service reused by Pattern 1.
    private bool _hasLoggedProjectileSpawnServiceFallback; // Prevents repeated spawn service fallback warnings from this pattern.

    /// <summary>
    /// Resolves required runtime references before execution starts.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Corrects invalid Pattern 1 inspector values and refreshes references.
    /// </summary>
    private void OnValidate()
    {
        if (_executionRange < 0f)
        {
            Debug.LogWarning($"[BossFanProjectilePattern] ExecutionRange was below zero and clamped. object={name}, value={_executionRange}", this);
            _executionRange = 0f;
        }

        ResolveReferences();
    }

    /// <summary>
    /// Runs Pattern 1 once when the common pattern execution API starts it.
    /// </summary>
    protected override void OnPatternExecutionStarted()
    {
        ResolveReferences();

        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            ReportPatternFailed("PatternAuthorityMissing");
            return;
        }

        if (!TryGetSettings(out FanProjectilePatternSettings settings))
        {
            CancelPatternWithWarning("MissingFanProjectileSettings");
            return;
        }

        if (!ValidateRequiredExecutionData(settings, out Transform[] projectileSpawnPoints))
        {
            return;
        }

        if (!TryResolveTarget(out Transform targetTransform))
        {
            CancelPatternWithWarning("TargetPlayerMissing");
            return;
        }

        Transform spawnPoint = SelectClosestSpawnPoint(projectileSpawnPoints, targetTransform);
        if (spawnPoint == null)
        {
            CancelPatternWithWarning("ProjectileSpawnPointValidEntryMissing");
            return;
        }

        if (!TryResolveProjectileSpawnService())
        {
            ReportPatternFailed("ProjectileSpawnServiceMissing");
            return;
        }

        _bossController.PlayPresentationCue(E_BossPresentationCue.PatternAttack, E_BossPatternType.FanProjectile, spawnPoint.position);
        if (!FireProjectiles(settings, spawnPoint, targetTransform))
        {
            ReportPatternFailed("ProjectileSpawnFailed");
            return;
        }

        MarkPatternEffectApplied();
        ReportPatternCompleted("FanProjectileCompleted");
    }

    /// <summary>
    /// Resolves Pattern 1 settings from the boss pattern data asset.
    /// </summary>
    private bool TryGetSettings(out FanProjectilePatternSettings settings)
    {
        settings = default;
        ResolveReferences();

        if (_bossController == null || _bossController.PatternData == null)
        {
            return false;
        }

        if (!_bossController.PatternData.TryGetFanProjectilePattern(_bossController.CurrentPatternId, out settings))
        {
            Debug.LogWarning($"[BossFanProjectilePattern] FanProjectile settings were not found for PatternId. object={name}, patternId={_bossController.CurrentPatternId}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates prefab and spawn point data required before Pattern 1 can fire.
    /// </summary>
    private bool ValidateRequiredExecutionData(FanProjectilePatternSettings settings, out Transform[] projectileSpawnPoints)
    {
        projectileSpawnPoints = null;

        if (settings.ProjectilePrefab == null)
        {
            CancelPatternWithWarning("ProjectilePrefabMissing");
            return false;
        }

        if (_anchorSet == null)
        {
            CancelPatternWithWarning("ProjectileSpawnPointsMissing");
            return false;
        }

        projectileSpawnPoints = _anchorSet.ProjectileSpawnPoints;
        if (projectileSpawnPoints == null)
        {
            CancelPatternWithWarning("ProjectileSpawnPointsNull");
            return false;
        }

        if (projectileSpawnPoints.Length == 0)
        {
            CancelPatternWithWarning("ProjectileSpawnPointsEmpty");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Finds the nearest valid Player through the shared boss Player target provider.
    /// </summary>
    private bool TryResolveTarget(out Transform targetTransform)
    {
        targetTransform = null;
        if (_bossController == null)
        {
            return false;
        }

        return _bossController.TryFindNearestPlayerForExecution(_executionRange, out targetTransform, out _, out _);
    }

    /// <summary>
    /// Selects the projectile spawn point closest to the target Player.
    /// </summary>
    private Transform SelectClosestSpawnPoint(Transform[] projectileSpawnPoints, Transform targetTransform)
    {
        if (projectileSpawnPoints == null || targetTransform == null)
        {
            return null;
        }

        Vector3 targetPosition = targetTransform.position; // Target position used for spawn point distance comparison.
        float nearestSqrDistance = float.MaxValue; // Best squared distance found while evaluating spawn points.
        Transform nearestSpawnPoint = null; // Spawn point closest to the target Player.

        for (int index = 0; index < projectileSpawnPoints.Length; index++)
        {
            Transform candidateSpawnPoint = projectileSpawnPoints[index]; // Current projectile spawn point candidate.
            if (candidateSpawnPoint == null)
            {
                continue;
            }

            float sqrDistance = (candidateSpawnPoint.position - targetPosition).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
            {
                continue;
            }

            nearestSqrDistance = sqrDistance;
            nearestSpawnPoint = candidateSpawnPoint;
        }

        return nearestSpawnPoint;
    }

    /// <summary>
    /// Fires the configured number of projectiles in an even fan centered on the target direction.
    /// </summary>
    private bool FireProjectiles(FanProjectilePatternSettings settings, Transform spawnPoint, Transform targetTransform)
    {
        Vector2 spawnPosition = spawnPoint.position; // World position used for every projectile in this fan.
        Vector2 baseDirection = (Vector2)(targetTransform.position - spawnPoint.position); // Direction from spawn point to target.
        if (baseDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            baseDirection = Vector2.right;
            LogFailureOnce("TargetDirectionFallback");
        }

        baseDirection.Normalize();

        int projectileCount = Mathf.Max(1, settings.ProjectileCount);
        float totalSpreadAngle = Mathf.Max(0f, settings.FanAngleDegrees);
        float angleStep = projectileCount <= 1 ? 0f : totalSpreadAngle / (projectileCount - 1);
        float firstAngle = projectileCount <= 1 ? 0f : -totalSpreadAngle * 0.5f;

        for (int index = 0; index < projectileCount; index++)
        {
            float angle = firstAngle + (angleStep * index); // Current projectile angle offset from the center direction.
            Vector2 projectileDirection = RotateDirection(baseDirection, angle);
            PooledRangedProjectile projectile = _projectileSpawnService.RequestSpawn(
                settings.ProjectilePrefab,
                spawnPosition,
                projectileDirection,
                gameObject,
                settings.ProjectileSpeed,
                settings.ProjectileLifetime,
                false,
                0);

            if (projectile == null)
            {
                return false;
            }

            projectile.ConfigureHitSettings(settings.Damage, settings.ProjectileCollisionLayerMask, settings.StatusTag);
        }

        return true;
    }

    /// <summary>
    /// Rotates a normalized 2D direction by the supplied angle in degrees.
    /// </summary>
    private Vector2 RotateDirection(Vector2 direction, float angleDegrees)
    {
        if (Mathf.Approximately(angleDegrees, 0f))
        {
            return direction;
        }

        float radians = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(
            (direction.x * cos) - (direction.y * sin),
            (direction.x * sin) + (direction.y * cos));
    }

    /// <summary>
    /// Reports a cancellation reason with a one-time warning for this execution.
    /// </summary>
    private void CancelPatternWithWarning(string reason)
    {
        LogFailureOnce(reason);
        ReportPatternCancelled(reason);
    }

    /// <summary>
    /// Resolves the projectile spawn service from assigned sources or the scene fallback.
    /// </summary>
    private bool TryResolveProjectileSpawnService()
    {
        if (_projectileSpawnService != null)
        {
            return true;
        }

        if (_projectileSpawnServiceSource != null)
        {
            _projectileSpawnService = _projectileSpawnServiceSource as IProjectileSpawnService;
            if (_projectileSpawnService != null)
            {
                return true;
            }

            Debug.LogWarning($"[BossFanProjectilePattern] Assigned projectile spawn service source does not implement IProjectileSpawnService. object={name}", this);
        }

        _projectileSpawnService = GetComponent<IProjectileSpawnService>();
        if (_projectileSpawnService != null)
        {
            return true;
        }

        if (!_allowSceneProjectileSpawnServiceFallback)
        {
            return false;
        }

        PooledProjectileSpawnService sceneSpawnService = FindFirstObjectByType<PooledProjectileSpawnService>();
        if (sceneSpawnService == null)
        {
            return false;
        }

        if (!_hasLoggedProjectileSpawnServiceFallback)
        {
            Debug.LogWarning($"[BossFanProjectilePattern] Used scene fallback PooledProjectileSpawnService. object={name}, service={sceneSpawnService.name}", this);
            _hasLoggedProjectileSpawnServiceFallback = true;
        }

        _projectileSpawnService = sceneSpawnService;
        return true;
    }

    /// <summary>
    /// Resolves optional references from the same boss GameObject.
    /// </summary>
    private void ResolveReferences()
    {
        if (_bossController == null)
        {
            _bossController = GetComponent<BossController>();
        }

        if (_anchorSet == null && _bossController != null)
        {
            _anchorSet = _bossController.AnchorSet;
        }

        if (_anchorSet == null)
        {
            _anchorSet = GetComponent<BossPatternAnchorSet>();
        }

        if (_projectileSpawnServiceSource != null && _projectileSpawnService == null)
        {
            _projectileSpawnService = _projectileSpawnServiceSource as IProjectileSpawnService;
        }
    }
}
