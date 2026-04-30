using UnityEngine;

/// <summary>
/// 가장 가까운 유효한 Player를 향해 풀링된 투사체를 부채꼴 형태로 발사하여 패턴 1을 실행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossFanProjectilePattern : BossPatternBase
{
    [Header("필수 참조")]
    [Tooltip("권한, 패턴 데이터, 앵커, Player 탐색을 소유하는 보스 컨트롤러")]
    [SerializeField] private BossController _bossController; // 패턴 1 실행에 필요한 보스 권한 및 공통 데이터 소스

    [Tooltip("투사체 생성 위치를 제공하는 씬 앵커 세트")]
    [SerializeField] private BossPatternAnchorSet _anchorSet; // 패턴 1에서 사용하는 씬 투사체 생성 위치 소스

    [Tooltip("투사체 생성 서비스 소스. IProjectileSpawnService를 구현해야 한다")]
    [SerializeField] private MonoBehaviour _projectileSpawnServiceSource; // 투사체 생성 서비스 인터페이스를 위한 직렬화된 MonoBehaviour 소스

    [Header("실행")]
    [Tooltip("패턴 1 시작 시 가장 가까운 Player를 탐색할 범위")]
    [Min(0f)]
    [SerializeField] private float _executionRange = 20f; // 패턴 1 실행 시 Player 탐색에 사용하는 거리

    [Tooltip("서비스가 할당되지 않았을 경우 씬 전체에서 투사체 생성 서비스를 탐색하는 fallback 허용 여부")]
    [SerializeField] private bool _allowSceneProjectileSpawnServiceFallback = true; // 기존 투사체 생성 서비스 fallback 허용 여부

    private IProjectileSpawnService _projectileSpawnService; // 패턴 1에서 재사용하는 기존 투사체 생성 서비스
    private bool _hasLoggedProjectileSpawnServiceFallback; // 투사체 생성 서비스 fallback 경고 중복 방지

    /// <summary>
    /// 실행 시작 전에 필요한 런타임 참조를 해결한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 잘못된 인스펙터 값을 보정하고 참조를 다시 설정한다.
    /// </summary>
    private void OnValidate()
    {
        if (_executionRange < 0f)
        {
            Debug.LogWarning($"[BossFanProjectilePattern] ExecutionRange가 0보다 작아서 보정됨. object={name}, value={_executionRange}", this);
            _executionRange = 0f;
        }

        ResolveReferences();
    }

    /// <summary>
    /// 공통 패턴 실행 API가 호출되면 패턴 1을 1회 실행한다.
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
    /// 보스 패턴 데이터에서 패턴 1 설정을 가져온다.
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
            Debug.LogWarning($"[BossFanProjectilePattern] PatternId에 해당하는 FanProjectile 설정이 없음. object={name}, patternId={_bossController.CurrentPatternId}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 패턴 실행 전에 필요한 프리팹 및 생성 위치 데이터를 검증한다.
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
    /// 공통 Player 타겟 제공자를 통해 가장 가까운 Player를 찾는다.
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
    /// Player와 가장 가까운 투사체 생성 위치를 선택한다.
    /// </summary>
    private Transform SelectClosestSpawnPoint(Transform[] projectileSpawnPoints, Transform targetTransform)
    {
        if (projectileSpawnPoints == null || targetTransform == null)
        {
            return null;
        }

        Vector3 targetPosition = targetTransform.position; // 거리 비교에 사용하는 Player 위치
        float nearestSqrDistance = float.MaxValue; // 현재까지 가장 가까운 거리 제곱값
        Transform nearestSpawnPoint = null; // 가장 가까운 생성 위치

        for (int index = 0; index < projectileSpawnPoints.Length; index++)
        {
            Transform candidateSpawnPoint = projectileSpawnPoints[index]; // 현재 검사 대상 생성 위치
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
    /// 설정된 개수의 투사체를 타겟 방향을 중심으로 균등한 부채꼴 형태로 발사한다.
    /// </summary>
    private bool FireProjectiles(FanProjectilePatternSettings settings, Transform spawnPoint, Transform targetTransform)
    {
        Vector2 spawnPosition = spawnPoint.position; // 이 부채꼴 발사에서 모든 투사체가 사용할 월드 위치
        Vector2 baseDirection = (Vector2)(targetTransform.position - spawnPoint.position); // 생성 위치에서 타겟으로 향하는 방향
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
            float angle = firstAngle + (angleStep * index); // 중심 방향을 기준으로 한 현재 투사체 각도 오프셋
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
    /// 정규화된 2D 방향을 지정한 각도만큼 회전한다.
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
    /// 취소 사유를 1회 경고로 기록하고 패턴 취소를 보고한다.
    /// </summary>
    private void CancelPatternWithWarning(string reason)
    {
        LogFailureOnce(reason);
        ReportPatternCancelled(reason);
    }

    /// <summary>
    /// 할당된 소스 또는 씬 fallback을 통해 투사체 생성 서비스를 찾는다.
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

            Debug.LogWarning($"[BossFanProjectilePattern] 할당된 투사체 생성 서비스 소스가 IProjectileSpawnService를 구현하지 않음. object={name}", this);
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
            Debug.LogWarning($"[BossFanProjectilePattern] 씬 fallback PooledProjectileSpawnService 사용됨. object={name}, service={sceneSpawnService.name}", this);
            _hasLoggedProjectileSpawnServiceFallback = true;
        }

        _projectileSpawnService = sceneSpawnService;
        return true;
    }

    /// <summary>
    /// 동일한 보스 GameObject에서 선택적 참조를 찾는다.
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
