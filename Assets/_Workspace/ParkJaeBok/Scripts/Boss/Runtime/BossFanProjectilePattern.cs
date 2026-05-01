using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 가장 가까운 유효 Player를 향해 조준된 투사체를 부채꼴 형태로 발사하여 패턴 1을 실행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossFanProjectilePattern : BossPatternBase
{
    [Header("필수 참조")]
    [Tooltip("권한, 패턴 데이터, 앵커, Player 탐색을 소유하는 보스 컨트롤러입니다.")]
    [SerializeField] private BossController _bossController; // 패턴 1 실행에 필요한 보스 권한 및 공통 데이터 소스입니다.

    [Tooltip("투사체 생성 위치를 제공하는 보스 패턴 앵커 세트입니다.")]
    [SerializeField] private BossPatternAnchorSet _anchorSet; // 패턴 1에서 사용하는 투사체 생성 위치 목록입니다.

    [Tooltip("투사체 생성 서비스입니다. IProjectileSpawnService를 구현해야 합니다.")]
    [SerializeField] private MonoBehaviour _projectileSpawnServiceSource; // 투사체 생성 서비스 인터페이스를 위한 직렬화된 MonoBehaviour 참조입니다.

    [Header("실행")]
    [Tooltip("패턴 1 시작 시 가장 가까운 Player를 탐색할 범위입니다.")]
    [Min(0f)]
    [SerializeField] private float _executionRange = 20f; // 패턴 1 실행 중 Player 탐색에 사용하는 거리입니다.

    [Tooltip("서비스가 할당되지 않은 경우 씬 전체에서 투사체 생성 서비스를 검색할지 여부입니다.")]
    [SerializeField] private bool _allowSceneProjectileSpawnServiceFallback = true; // 기존 투사체 생성 서비스 fallback 허용 여부입니다.

    private IProjectileSpawnService _projectileSpawnService; // 패턴 1에서 투사체 생성/소멸을 요청하는 기존 서비스입니다.
    private int _projectileVisualSequence; // 서버가 생성한 보스 투사체와 Client 시각 투사체를 연결하는 복제 ID 시퀀스입니다.
    private bool _hasLoggedProjectileSpawnServiceFallback; // 투사체 생성 서비스 fallback 경고 중복 방지 플래그입니다.

    /// <summary>
    /// 실행 시작 전에 필요한 런타임 참조를 해결한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Inspector 값과 참조를 검증하고 보정한다.
    /// </summary>
    private void OnValidate()
    {
        if (_executionRange < 0f)
        {
            Debug.LogWarning($"[BossFanProjectilePattern] ExecutionRange가 0보다 작아 보정됨. object={name}, value={_executionRange}", this);
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
    /// 보스 패턴 데이터에서 현재 PatternId에 맞는 패턴 1 설정을 가져온다.
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
    /// 공통 Player 대상 제공자를 통해 가장 가까운 Player를 찾는다.
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

        Vector3 targetPosition = targetTransform.position; // 거리 비교에 사용하는 Player 위치입니다.
        float nearestSqrDistance = float.MaxValue; // 현재까지 가장 가까운 거리 제곱값입니다.
        Transform nearestSpawnPoint = null; // 현재까지 가장 가까운 생성 위치입니다.

        for (int index = 0; index < projectileSpawnPoints.Length; index++)
        {
            Transform candidateSpawnPoint = projectileSpawnPoints[index]; // 현재 검사 중인 생성 위치입니다.
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
    /// 설정된 개수의 투사체를 대상 방향을 중심으로 균등한 부채꼴 형태로 발사한다.
    /// </summary>
    private bool FireProjectiles(FanProjectilePatternSettings settings, Transform spawnPoint, Transform targetTransform)
    {
        Vector2 spawnPosition = spawnPoint.position; // 모든 투사체가 사용하는 월드 생성 위치입니다.
        Vector2 baseDirection = (Vector2)(targetTransform.position - spawnPoint.position); // 생성 위치에서 대상까지의 기준 방향입니다.
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
            float angle = firstAngle + (angleStep * index); // 기준 방향에서 현재 투사체가 회전할 각도입니다.
            Vector2 projectileDirection = RotateDirection(baseDirection, angle);
            int projectileVisualId = ShouldReplicateProjectileVisuals() ? NextProjectileVisualId() : 0;
            PooledRangedProjectile projectile = _projectileSpawnService.RequestSpawn(
                settings.ProjectilePrefab,
                spawnPosition,
                projectileDirection,
                gameObject,
                settings.ProjectileSpeed,
                settings.ProjectileLifetime,
                false,
                projectileVisualId);

            if (projectile == null)
            {
                return false;
            }

            projectile.ConfigureHitSettings(settings.Damage, settings.ProjectileCollisionLayerMask, settings.StatusTag);

            if (projectileVisualId > 0)
            {
                projectile.AddListener(HandleAuthoritativeProjectileDespawned);
                SpawnProjectileVisualRpc(projectileVisualId, settings.PatternId, spawnPosition, projectileDirection, settings.ProjectileSpeed, settings.ProjectileLifetime);
            }
        }

        return true;
    }

    /// <summary>
    /// 서버가 확정한 보스 투사체 발사 결과를 Client 시각 투사체로 생성한다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void SpawnProjectileVisualRpc(int projectileVisualId, string patternId, Vector2 firePosition, Vector2 direction, float projectileSpeed, float projectileLifetime)
    {
        if (!TryGetVisualProjectilePrefab(patternId, out GameObject projectilePrefab))
        {
            return;
        }

        SpawnProjectileVisual(projectileVisualId, projectilePrefab, firePosition, direction, projectileSpeed, projectileLifetime);
    }

    /// <summary>
    /// 서버가 확정한 보스 투사체 종료 결과를 Client 시각 투사체에 적용한다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void DespawnProjectileVisualRpc(int projectileVisualId)
    {
        if (projectileVisualId <= 0)
        {
            Debug.LogWarning($"[BossFanProjectilePattern] 잘못된 시각 투사체 ID로 Despawn 요청됨. object={name}, visualId={projectileVisualId}", this);
            return;
        }

        if (!TryResolveProjectileSpawnService())
        {
            Debug.LogWarning($"[BossFanProjectilePattern] Client에서 ProjectileSpawnService를 찾지 못해 시각 투사체 Despawn을 건너뜁니다. object={name}, visualId={projectileVisualId}", this);
            return;
        }

        _projectileSpawnService.TryDespawnVisual(projectileVisualId);
    }

    /// <summary>
    /// Client 인스턴스에서 데미지 없는 시각 전용 보스 투사체를 생성한다.
    /// </summary>
    private void SpawnProjectileVisual(int projectileVisualId, GameObject projectilePrefab, Vector2 firePosition, Vector2 direction, float projectileSpeed, float projectileLifetime)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"[BossFanProjectilePattern] Client 시각 투사체 프리팹이 null입니다. object={name}, visualId={projectileVisualId}", this);
            return;
        }

        if (!TryResolveProjectileSpawnService())
        {
            Debug.LogWarning($"[BossFanProjectilePattern] Client에서 ProjectileSpawnService를 찾지 못해 시각 투사체 Spawn을 건너뜁니다. object={name}, visualId={projectileVisualId}", this);
            return;
        }

        _projectileSpawnService.RequestSpawn(
            projectilePrefab,
            firePosition,
            direction,
            gameObject,
            projectileSpeed,
            projectileLifetime,
            true,
            projectileVisualId);
    }

    /// <summary>
    /// Client가 수신한 PatternId를 기준으로 동일한 투사체 프리팹을 보스 패턴 데이터에서 찾는다.
    /// </summary>
    private bool TryGetVisualProjectilePrefab(string patternId, out GameObject projectilePrefab)
    {
        projectilePrefab = null;
        ResolveReferences();

        if (_bossController == null || _bossController.PatternData == null)
        {
            Debug.LogWarning($"[BossFanProjectilePattern] Client에서 BossPatternData를 찾지 못해 투사체 프리팹 조회 실패. object={name}, patternId={patternId}", this);
            return false;
        }

        if (!_bossController.PatternData.TryGetFanProjectilePattern(patternId, out FanProjectilePatternSettings settings))
        {
            Debug.LogWarning($"[BossFanProjectilePattern] Client에서 PatternId에 해당하는 FanProjectile 설정을 찾지 못했습니다. object={name}, patternId={patternId}", this);
            return false;
        }

        projectilePrefab = settings.ProjectilePrefab;
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"[BossFanProjectilePattern] Client에서 조회한 FanProjectile 프리팹이 null입니다. object={name}, patternId={patternId}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 서버 투사체가 종료되면 연결된 Client 시각 투사체에도 종료를 복제한다.
    /// </summary>
    private void HandleAuthoritativeProjectileDespawned(PooledRangedProjectile projectile, E_ProjectileDespawnReason reason)
    {
        if (projectile == null)
        {
            return;
        }

        projectile.RemoveListener(HandleAuthoritativeProjectileDespawned);

        if (!ShouldReplicateProjectileVisuals() || projectile.VisualInstanceId <= 0)
        {
            return;
        }

        DespawnProjectileVisualRpc(projectile.VisualInstanceId);
    }

    /// <summary>
    /// 네트워크 세션에서 서버가 Client 시각 투사체를 복제해야 하는지 판정한다.
    /// </summary>
    private bool ShouldReplicateProjectileVisuals()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // NGO 세션 활성 여부 확인에 사용하는 매니저 참조입니다.
        return networkManager != null && networkManager.IsListening && IsServer && IsSpawned;
    }

    /// <summary>
    /// Client 시각 투사체와 서버 투사체를 연결할 복제 ID를 발급한다.
    /// </summary>
    private int NextProjectileVisualId()
    {
        _projectileVisualSequence++;
        if (_projectileVisualSequence <= 0)
        {
            _projectileVisualSequence = 1;
            Debug.LogWarning($"[BossFanProjectilePattern] Projectile visual sequence overflow. Fallback to 1. object={name}", this);
        }

        return _projectileVisualSequence;
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
            Debug.LogWarning($"[BossFanProjectilePattern] 씬 fallback PooledProjectileSpawnService 사용. object={name}, service={sceneSpawnService.name}", this);
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
