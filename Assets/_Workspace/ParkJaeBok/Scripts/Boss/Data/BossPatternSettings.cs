using System;
using UnityEngine;

/// <summary>
/// 모든 보스 패턴에서 공통으로 사용하는 디자이너 설정 값을 저장한다.
/// </summary>
[Serializable]
public struct PatternCommonSettings
{
    [Tooltip("로그 및 런타임에서 이 패턴을 식별하기 위한 고정 ID")]
    [SerializeField] private string _patternId; // 다른 보스 패턴과 구분하기 위한 고정 ID

    [Tooltip("이 패턴이 검증 및 선택 대상에 포함되는지 여부")]
    [SerializeField] private bool _enabled; // 검증 및 패턴 선택에 사용되는 활성화 여부

    [Tooltip("패턴 실행 로직을 결정하는 패턴 타입")]
    [SerializeField] private E_BossPatternType _patternType; // 실행 분기를 결정하는 패턴 타입

    [Tooltip("패턴 선택 로직에서 사용하는 기본 가중치")]
    [Min(0f)]
    [SerializeField] private float _selectionWeight; // 패턴 선택 시 사용하는 가중치

    [Tooltip("패턴 선택 시 우선순위 (높을수록 먼저 선택됨)")]
    [Min(0)]
    [SerializeField] private int _priority; // 선택 우선순위

    [Tooltip("이 패턴이 선택되기 위해 타겟이 필요한지 여부")]
    [SerializeField] private bool _requireTarget; // 타겟 필요 여부

    [Tooltip("약점 패턴 활성 상태에서도 이 패턴을 선택할 수 있는지 여부")]
    [SerializeField] private bool _allowDuringWeakPointActive; // 약점 패턴 중복 허용 여부

    [Tooltip("이 패턴을 다시 사용할 수 있기까지의 최소 쿨타임 (초)")]
    [Min(0f)]
    [SerializeField] private float _cooldownSeconds; // 패턴 재사용 쿨타임

    [Tooltip("이 패턴을 사용할 수 있는 최소 타겟 거리 (제곱 거리)")]
    [Min(0f)]
    [SerializeField] private float _minimumTargetSqrDistance; // 최소 거리 조건 (제곱 값)

    [Tooltip("이 패턴을 사용할 수 있는 최대 타겟 거리 (제곱 거리)")]
    [Min(0f)]
    [SerializeField] private float _maximumTargetSqrDistance; // 최대 거리 조건 (제곱 값)

    [Tooltip("패턴 시작 전 대기 시간 (초)")]
    [Min(0f)]
    [SerializeField] private float _startupSeconds; // 시작 전 딜레이

    [Tooltip("패턴이 실제로 동작하는 시간 (초)")]
    [Min(0f)]
    [SerializeField] private float _activeSeconds; // 활성 시간

    [Tooltip("패턴 종료 후 회복 시간 (초)")]
    [Min(0f)]
    [SerializeField] private float _recoverySeconds; // 회복 시간

    /// <summary>
    /// 패턴 ID 반환
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// 활성화 여부 반환
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// 패턴 타입 반환
    /// </summary>
    public E_BossPatternType PatternType => _patternType;

    /// <summary>
    /// 선택 가중치 반환
    /// </summary>
    public float SelectionWeight => _selectionWeight;

    /// <summary>
    /// 우선순위 반환
    /// </summary>
    public int Priority => _priority;

    /// <summary>
    /// 타겟 필요 여부 반환
    /// </summary>
    public bool RequireTarget => _requireTarget;

    /// <summary>
    /// 약점 패턴 중 선택 가능 여부 반환
    /// </summary>
    public bool AllowDuringWeakPointActive => _allowDuringWeakPointActive;

    /// <summary>
    /// 쿨타임 반환
    /// </summary>
    public float CooldownSeconds => _cooldownSeconds;

    /// <summary>
    /// 최소 거리 반환
    /// </summary>
    public float MinimumTargetSqrDistance => _minimumTargetSqrDistance;

    /// <summary>
    /// 최대 거리 반환
    /// </summary>
    public float MaximumTargetSqrDistance => _maximumTargetSqrDistance;

    /// <summary>
    /// 시작 시간 반환
    /// </summary>
    public float StartupSeconds => _startupSeconds;

    /// <summary>
    /// 활성 시간 반환
    /// </summary>
    public float ActiveSeconds => _activeSeconds;

    /// <summary>
    /// 회복 시간 반환
    /// </summary>
    public float RecoverySeconds => _recoverySeconds;

    /// <summary>
    /// 잘못된 값을 보정한다.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext, int index)
    {
        if (_cooldownSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 쿨타임이 0보다 작아서 보정됨. index={index}, patternId={_patternId}, value={_cooldownSeconds}", logContext);
            _cooldownSeconds = 0f;
        }

        if (_priority < 0)
        {
            Debug.LogWarning($"[BossPatternData] 우선순위가 0보다 작아서 보정됨. index={index}, patternId={_patternId}, value={_priority}", logContext);
            _priority = 0;
        }

        if (_startupSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 시작 시간이 0보다 작아서 보정됨. index={index}, patternId={_patternId}, value={_startupSeconds}", logContext);
            _startupSeconds = 0f;
        }

        if (_activeSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 활성 시간이 0보다 작아서 보정됨. index={index}, patternId={_patternId}, value={_activeSeconds}", logContext);
            _activeSeconds = 0f;
        }

        if (_recoverySeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 회복 시간이 0보다 작아서 보정됨. index={index}, patternId={_patternId}, value={_recoverySeconds}", logContext);
            _recoverySeconds = 0f;
        }
    }
}

/// <summary>
/// 보스의 체력 비율 조건으로 페이즈 범위를 정의하는 설정을 저장한다.
/// </summary>
[Serializable]
public struct HealthPhaseSettings
{
    [Tooltip("보스 페이즈 로직에서 사용할 페이즈 번호")]
    [Min(0)]
    [SerializeField] private int _phaseIndex; // 패턴 사용 가능 여부를 묶는 페이즈 인덱스

    [Tooltip("이 페이즈가 활성화되는 최소 체력 비율입니다. 이 값은 포함하지 않습니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _minHealthRatio; // 페이즈 활성화에 사용하는 최소 정규화 체력 비율

    [Tooltip("이 페이즈가 활성화되는 최대 체력 비율입니다. 이 값은 포함합니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _maxHealthRatio; // 페이즈 활성화에 사용하는 최대 정규화 체력 비율

    [Tooltip("이 체력 페이즈가 활성화된 동안 사용할 수 있는 패턴 ID 목록")]
    [SerializeField] private string[] _availablePatternIds; // 이 페이즈에서 사용할 수 있는 패턴 ID 목록

    /// <summary>
    /// 페이즈 인덱스를 반환한다.
    /// </summary>
    public int PhaseIndex => _phaseIndex;

    /// <summary>
    /// 최소 정규화 체력 비율을 반환한다.
    /// </summary>
    public float MinHealthRatio => _minHealthRatio;

    /// <summary>
    /// 최대 정규화 체력 비율을 반환한다.
    /// </summary>
    public float MaxHealthRatio => _maxHealthRatio;

    /// <summary>
    /// 기존 데이터 읽기 호환성을 위해 정규화 체력 하한값을 반환한다.
    /// </summary>
    public float HealthRatioLowerBound => _minHealthRatio;

    /// <summary>
    /// 기존 데이터 읽기 호환성을 위해 정규화 체력 상한값을 반환한다.
    /// </summary>
    public float HealthRatioUpperBound => _maxHealthRatio;

    /// <summary>
    /// 이 페이즈에서 사용할 수 있는 패턴 ID 배열을 반환한다.
    /// </summary>
    public string[] AvailablePatternIds => _availablePatternIds;

    /// <summary>
    /// 이 페이즈 설정 항목의 잘못된 체력 비율 값을 보정한다.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext, int index)
    {
        float clampedMin = Mathf.Clamp01(_minHealthRatio);
        if (!Mathf.Approximately(clampedMin, _minHealthRatio))
        {
            Debug.LogWarning($"[BossPatternData] MinHealthRatio가 0..1 범위를 벗어나 보정됨. index={index}, phase={_phaseIndex}, value={_minHealthRatio}", logContext);
            _minHealthRatio = clampedMin;
        }

        float clampedMax = Mathf.Clamp01(_maxHealthRatio);
        if (!Mathf.Approximately(clampedMax, _maxHealthRatio))
        {
            Debug.LogWarning($"[BossPatternData] MaxHealthRatio가 0..1 범위를 벗어나 보정됨. index={index}, phase={_phaseIndex}, value={_maxHealthRatio}", logContext);
            _maxHealthRatio = clampedMax;
        }

        if (_minHealthRatio >= _maxHealthRatio)
        {
            Debug.LogWarning($"[BossPatternData] MinHealthRatio는 MaxHealthRatio보다 작아야 하므로 보정됨. index={index}, phase={_phaseIndex}, min={_minHealthRatio}, max={_maxHealthRatio}", logContext);
            _maxHealthRatio = Mathf.Clamp01(_minHealthRatio + 0.01f);
            if (_maxHealthRatio <= _minHealthRatio)
            {
                _minHealthRatio = Mathf.Clamp01(_maxHealthRatio - 0.01f);
            }
        }
    }
}

/// <summary>
/// 단일 보스 패턴의 사용 제한 설정을 저장한다.
/// </summary>
[Serializable]
public struct PatternUsageLimit
{
    [Tooltip("이 사용 제한을 소유하는 체력 페이즈 인덱스")]
    [Min(0)]
    [SerializeField] private int _phaseIndex; // 이 PatternId 사용 제한을 소유하는 HealthPhaseSettings의 PhaseIndex

    [Tooltip("이 사용 제한이 적용되는 공통 패턴 ID")]
    [SerializeField] private string _patternId; // 이 사용 제한과 연결된 공통 패턴 ID

    [Tooltip("이 체력 페이즈에서 허용되는 최대 사용 횟수입니다. 0이면 이 페이즈에서 패턴을 비활성화하고, 음수이면 무제한입니다.")]
    [SerializeField] private int _maxUseCount; // 이 PatternId에 대한 페이즈 내 최대 사용 횟수

    /// <summary>
    /// 이 사용 제한을 소유하는 체력 페이즈 인덱스를 반환한다.
    /// </summary>
    public int PhaseIndex => _phaseIndex;

    /// <summary>
    /// 이 사용 제한의 공통 패턴 ID를 반환한다.
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// 설정된 체력 페이즈 안에서 이 PatternId가 사용할 수 있는 최대 횟수를 반환한다.
    /// </summary>
    public int MaxUseCount => _maxUseCount;

    /// <summary>
    /// 이전 코드 경로와의 호환성을 위해 전투 내 최대 사용 횟수를 반환한다.
    /// </summary>
    public int MaxEncounterUseCount => _maxUseCount;

    /// <summary>
    /// 잘못된 사용 제한 값을 보정하고 데이터 작성 문제를 로그로 알린다.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext, int index)
    {
        if (_phaseIndex < 0)
        {
            Debug.LogWarning($"[BossPatternData] UsageLimit PhaseIndex가 0보다 작아서 보정됨. index={index}, patternId={_patternId}, value={_phaseIndex}", logContext);
            _phaseIndex = 0;
        }

        if (string.IsNullOrWhiteSpace(_patternId))
        {
            Debug.LogWarning($"[BossPatternData] UsageLimit PatternId가 비어있다. index={index}, phaseIndex={_phaseIndex}", logContext);
        }
    }
}

/// <summary>
/// 부채꼴 투사체 패턴의 순수 설정 값을 저장한다.
/// </summary>
[Serializable]
public struct FanProjectilePatternSettings
{
    [Tooltip("이 부채꼴 투사체 패턴의 활성화 여부")]
    [SerializeField] private bool _enabled; // 필수 부채꼴 투사체 참조 검증을 활성화한다.

    [Tooltip("이 부채꼴 투사체 설정이 속한 공통 패턴 ID")]
    [SerializeField] private string _patternId; // 이 부채꼴 투사체 설정 그룹과 연결된 공통 패턴 ID

    [Tooltip("이후 실행 로직에서 사용할 투사체 프리팹입니다. 씬 참조가 아닌 애셋 참조입니다.")]
    [SerializeField] private GameObject _projectilePrefab; // 이후 투사체 생성에 사용할 투사체 프리팹 애셋

    [Tooltip("BossPatternAnchorSet에서 사용할 투사체 생성 앵커 개수")]
    [Min(1)]
    [SerializeField] private int _spawnPointCount; // 실행 시 사용할 씬 생성 앵커 개수

    [Tooltip("부채꼴 범위 안에서 생성할 투사체 방향 개수")]
    [Min(1)]
    [SerializeField] private int _projectileCount; // 이 패턴이 발사하는 투사체 개수

    [Tooltip("전체 부채꼴 각도")]
    [Min(0f)]
    [SerializeField] private float _fanAngleDegrees; // 생성되는 투사체 방향의 전체 확산 각도

    [Tooltip("투사체 이동 속도")]
    [Min(0f)]
    [SerializeField] private float _projectileSpeed; // 기존 투사체 생성 서비스에 전달할 투사체 속도

    [Tooltip("투사체 생존 시간")]
    [Min(0f)]
    [SerializeField] private float _projectileLifetime; // 기존 투사체 생성 서비스에 전달할 투사체 생존 시간

    [Tooltip("투사체 1회 명중 시 적용되는 기본 피해량")]
    [Min(0f)]
    [SerializeField] private float _damage; // 투사체 1회 명중 피해량

    [Tooltip("생성된 투사체가 유효한 충돌 대상을 판정할 때 사용하는 LayerMask")]
    [SerializeField] private LayerMask _projectileCollisionLayerMask; // 생성된 투사체에 전달할 충돌 대상 레이어 마스크

    [Tooltip("이 패턴의 HitRequest에 포함할 상태 태그")]
    [SerializeField] private string _statusTag; // 이후 피해 및 리액션 분기에 사용할 피격 상태 태그

    /// <summary>
    /// 이 부채꼴 투사체 패턴의 활성화 여부를 반환한다.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// 이 설정 그룹의 공통 패턴 ID를 반환한다.
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// 투사체 프리팹 애셋 참조를 반환한다.
    /// </summary>
    public GameObject ProjectilePrefab => _projectilePrefab;

    /// <summary>
    /// 사용할 생성 앵커 개수를 반환한다.
    /// </summary>
    public int SpawnPointCount => _spawnPointCount;

    /// <summary>
    /// 발사할 투사체 개수를 반환한다.
    /// </summary>
    public int ProjectileCount => _projectileCount;

    /// <summary>
    /// 전체 부채꼴 각도를 반환한다.
    /// </summary>
    public float FanAngleDegrees => _fanAngleDegrees;

    /// <summary>
    /// 투사체 속도를 반환한다.
    /// </summary>
    public float ProjectileSpeed => _projectileSpeed;

    /// <summary>
    /// 투사체 생존 시간을 반환한다.
    /// </summary>
    public float ProjectileLifetime => _projectileLifetime;

    /// <summary>
    /// 투사체 기본 피해량을 반환한다.
    /// </summary>
    public float Damage => _damage;

    /// <summary>
    /// 투사체 충돌 대상 LayerMask를 반환한다.
    /// </summary>
    public LayerMask ProjectileCollisionLayerMask => _projectileCollisionLayerMask;

    /// <summary>
    /// 피격 상태 태그를 반환한다.
    /// </summary>
    public string StatusTag => _statusTag;

    /// <summary>
    /// 잘못된 값을 보정하고 이 패턴에 필요한 프리팹 참조를 검증한다.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext)
    {
        if (_projectileCount < 1)
        {
            Debug.LogWarning($"[BossPatternData] ProjectileCount가 1보다 작아서 보정됨. patternId={_patternId}, value={_projectileCount}", logContext);
            _projectileCount = 1;
        }

        if (_projectileCount % 2 == 0)
        {
            Debug.LogWarning($"[BossPatternData] ProjectileCount가 짝수라서 1 증가됨. patternId={_patternId}, value={_projectileCount}", logContext);
            _projectileCount++;
        }

        if (_projectileLifetime < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 투사체 지속 시간이 0보다 작아서 보정됨. patternId={_patternId}, value={_projectileLifetime}", logContext);
            _projectileLifetime = 0f;
        }

        if (_damage < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 부채꼴 투사체 피해량이 0보다 작아서 보정됨. patternId={_patternId}, value={_damage}", logContext);
            _damage = 0f;
        }

        if (_enabled && _projectilePrefab == null)
        {
            Debug.LogWarning($"[BossPatternData] FanProjectile 패턴이 활성화되어 있지만 ProjectilePrefab이 비어있다. patternId={_patternId}", logContext);
        }
    }
}

/// <summary>
/// 지면 가시 패턴의 순수 설정 값을 저장한다.
/// </summary>
[Serializable]
public struct GroundSpikePatternSettings
{
    [Tooltip("이 지면 가시 패턴의 활성화 여부")]
    [SerializeField] private bool _enabled; // 이 지면 가시 패턴의 검증을 활성화한다.

    [Tooltip("이 지면 가시 설정이 속한 공통 패턴 ID")]
    [SerializeField] private string _patternId; // 이 지면 가시 설정 그룹과 연결된 공통 패턴 ID

    [Tooltip("경고 시간이 지난 뒤 생성할 가시 프리팹입니다. 씬 참조가 아닌 애셋 참조입니다.")]
    [SerializeField] private GameObject _spikePrefab; // 패턴 2에서 경고 표시 후 생성할 가시 프리팹 애셋

    [Tooltip("가시 피격이 활성화되기 전에 보여줄 선택 경고 VFX 프리팹")]
    [SerializeField] private GameObject _warningVfxPrefab; // 가시 생성 전에 표시할 선택 경고 VFX 프리팹

    [Tooltip("기존 풀링 VFX 시스템을 통해 경고 VFX를 재생할 EffectService ID")]
    [SerializeField] private E_EffectId _warningEffectId; // 풀링 경고 표시용 기존 VFX 시스템 ID

    [Tooltip("가시 피격이 활성화될 때 보여줄 선택 공격 VFX 프리팹")]
    [SerializeField] private GameObject _attackVfxPrefab; // 가시 피해 타이밍이 시작될 때 생성할 선택 공격 VFX 프리팹

    [Tooltip("기존 풀링 VFX 시스템을 통해 공격 VFX를 재생할 EffectService ID")]
    [SerializeField] private E_EffectId _attackEffectId; // 풀링 공격 표시용 기존 VFX 시스템 ID

    [Tooltip("아래 방향 지면 Raycast 시작 전에 타겟 Player 위치에 더할 Y 오프셋")]
    [SerializeField] private float _raycastStartYOffset; // 타겟 Player 위쪽에서 지면 검색을 시작하기 위한 오프셋

    [Tooltip("아래 방향 지면 Raycast에 사용할 최대 거리")]
    [Min(0f)]
    [SerializeField] private float _groundRaycastDistance; // 바닥에 가시를 배치하기 위해 사용할 지면 검색 거리

    [Tooltip("아래 방향 지면 Raycast에 사용할 LayerMask")]
    [SerializeField] private LayerMask _groundLayerMask; // 패턴 2 배치 Raycast가 허용하는 지면 충돌 레이어

    [Tooltip("경고 표시 후 피해 적용까지의 지연 시간")]
    [Min(0f)]
    [SerializeField] private float _warningSeconds; // 이후 지면 가시 피해 전 경고 시간

    [Tooltip("생성된 가시 피격 Collider가 활성 상태를 유지하는 시간")]
    [Min(0f)]
    [SerializeField] private float _spikeHitDuration; // 패턴 2에서 가시 충돌을 비활성화하기 전까지의 활성 피격 시간

    [Tooltip("이 패턴 중 생성할 가시 피격 횟수")]
    [Min(1)]
    [SerializeField] private int _spikeCount; // 이 패턴에서 생성할 가시 영역 개수

    [Tooltip("순차 가시 피격 사이의 지연 시간")]
    [Min(0f)]
    [SerializeField] private float _intervalSeconds; // 가시 활성화 사이의 시간 간격

    [Tooltip("각 가시 피격에 사용할 박스 영역 크기")]
    [SerializeField] private Vector2 _boxSize; // 각 가시의 박스 피격 영역 크기

    [Tooltip("가시에 맞은 HitReceiver 대상을 찾을 때 사용하는 LayerMask")]
    [SerializeField] private LayerMask _spikeTargetLayerMask; // 패턴 2 권한 측 Overlap 검사에 사용할 피격 대상 레이어

    [Tooltip("가시 1회 피격 시 적용되는 기본 피해량")]
    [Min(0f)]
    [SerializeField] private float _damage; // 지면 가시 1회 피격 피해량

    [Tooltip("이 패턴의 HitRequest에 포함할 상태 태그")]
    [SerializeField] private string _statusTag; // 이후 피해 및 리액션 분기에 사용할 피격 상태 태그

    /// <summary>
    /// 이 지면 가시 패턴의 활성화 여부를 반환한다.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// 이 설정 그룹의 공통 패턴 ID를 반환한다.
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// 가시 프리팹 애셋 참조를 반환한다.
    /// </summary>
    public GameObject SpikePrefab => _spikePrefab;

    /// <summary>
    /// 선택 경고 VFX 프리팹 애셋 참조를 반환한다.
    /// </summary>
    public GameObject WarningVfxPrefab => _warningVfxPrefab;

    /// <summary>
    /// 풀링 경고 VFX ID를 반환한다.
    /// </summary>
    public E_EffectId WarningEffectId => _warningEffectId;

    /// <summary>
    /// 선택 공격 VFX 프리팹 애셋 참조를 반환한다.
    /// </summary>
    public GameObject AttackVfxPrefab => _attackVfxPrefab;

    /// <summary>
    /// 풀링 공격 VFX ID를 반환한다.
    /// </summary>
    public E_EffectId AttackEffectId => _attackEffectId;

    /// <summary>
    /// 아래 방향 지면 Raycast 시작 전에 사용할 Y 오프셋을 반환한다.
    /// </summary>
    public float RaycastStartYOffset => _raycastStartYOffset;

    /// <summary>
    /// 지면 Raycast 거리를 반환한다.
    /// </summary>
    public float GroundRaycastDistance => _groundRaycastDistance;

    /// <summary>
    /// 지면 Raycast LayerMask를 반환한다.
    /// </summary>
    public LayerMask GroundLayerMask => _groundLayerMask;

    /// <summary>
    /// 경고 시간을 초 단위로 반환한다.
    /// </summary>
    public float WarningSeconds => _warningSeconds;

    /// <summary>
    /// 패턴 2 명명 호환성을 위해 경고 시간을 초 단위로 반환한다.
    /// </summary>
    public float SpikeWarningDuration => _warningSeconds;

    /// <summary>
    /// 가시 피격 Collider가 활성 상태를 유지하는 시간을 반환한다.
    /// </summary>
    public float SpikeHitDuration => _spikeHitDuration;

    /// <summary>
    /// 가시 피격 횟수를 반환한다.
    /// </summary>
    public int SpikeCount => _spikeCount;

    /// <summary>
    /// 가시 피격 사이의 지연 시간을 반환한다.
    /// </summary>
    public float IntervalSeconds => _intervalSeconds;

    /// <summary>
    /// 가시 박스 크기를 반환한다.
    /// </summary>
    public Vector2 BoxSize => _boxSize;

    /// <summary>
    /// 가시 피격 대상 LayerMask를 반환한다.
    /// </summary>
    public LayerMask SpikeTargetLayerMask => _spikeTargetLayerMask;

    /// <summary>
    /// 가시 기본 피해량을 반환한다.
    /// </summary>
    public float Damage => _damage;

    /// <summary>
    /// 가시 기본 피해량을 반환한다.
    /// </summary>
    public float SpikeDamage => _damage;

    /// <summary>
    /// 피격 상태 태그를 반환한다.
    /// </summary>
    public string StatusTag => _statusTag;

    /// <summary>
    /// 이 패턴의 잘못된 값을 보정한다.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext)
    {
        if (_enabled && _spikePrefab == null)
        {
            Debug.LogWarning($"[BossPatternData] GroundSpike 패턴이 활성화되어 있지만 SpikePrefab이 비어있다. patternId={_patternId}", logContext);
        }

        if (_groundRaycastDistance < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 지면 가시 Raycast 거리가 0보다 작아서 보정됨. patternId={_patternId}, value={_groundRaycastDistance}", logContext);
            _groundRaycastDistance = 0f;
        }

        if (_warningSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 지면 가시 경고 시간이 0보다 작아서 보정됨. patternId={_patternId}, value={_warningSeconds}", logContext);
            _warningSeconds = 0f;
        }

        if (_spikeHitDuration < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 지면 가시 피격 시간이 0보다 작아서 보정됨. patternId={_patternId}, value={_spikeHitDuration}", logContext);
            _spikeHitDuration = 0f;
        }

        if (_intervalSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 지면 가시 간격 시간이 0보다 작아서 보정됨. patternId={_patternId}, value={_intervalSeconds}", logContext);
            _intervalSeconds = 0f;
        }

        if (_damage < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 지면 가시 피해량이 0보다 작아서 보정됨. patternId={_patternId}, value={_damage}", logContext);
            _damage = 0f;
        }
    }
}

/// <summary>
/// 몬스터 소환 패턴의 순수 설정 값을 저장한다.
/// </summary>
[Serializable]
public struct SummonMonsterPatternSettings
{
    [Tooltip("이 몬스터 소환 패턴의 활성화 여부")]
    [SerializeField] private bool _enabled; // 필수 몬스터 소환 참조 검증을 활성화한다.

    [Tooltip("이 소환 설정이 속한 공통 패턴 ID")]
    [SerializeField] private string _patternId; // 이 소환 설정 그룹과 연결된 공통 패턴 ID

    [Tooltip("이후 소환 실행 로직에서 사용할 몬스터 프리팹 애셋")]
    [SerializeField] private GameObject _monsterPrefab; // 이후 생성 로직에서 사용할 몬스터 프리팹 애셋

    [Tooltip("패턴 3을 1회 실행할 때 BossPatternAnchorSet에서 선택할 몬스터 생성 앵커 개수")]
    [Min(1)]
    [SerializeField] private int _spawnPointCount; // 패턴 3이 선택할 몬스터 생성 앵커 개수를 결정하는 SpawnCount 값

    [Tooltip("이 패턴으로 살아있을 수 있는 최대 소환 몬스터 수입니다. 0이면 패턴 단위 제한이 없습니다.")]
    [Min(0)]
    [SerializeField] private int _maxAliveCount; // 이후 패턴 로직에서 사용할 생존 소환 몬스터 제한

    [Tooltip("순차 몬스터 생성 사이의 지연 시간")]
    [Min(0f)]
    [SerializeField] private float _spawnIntervalSeconds; // 몬스터 생성 요청 사이의 시간 간격

    /// <summary>
    /// 이 몬스터 소환 패턴의 활성화 여부를 반환한다.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// 이 설정 그룹의 공통 패턴 ID를 반환한다.
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// 몬스터 프리팹 애셋 참조를 반환한다.
    /// </summary>
    public GameObject MonsterPrefab => _monsterPrefab;

    /// <summary>
    /// 사용할 몬스터 생성 앵커 개수를 반환한다.
    /// </summary>
    public int SpawnPointCount => _spawnPointCount;

    /// <summary>
    /// 선택할 몬스터 생성 앵커 개수를 반환한다.
    /// </summary>
    public int SpawnCount => _spawnPointCount;

    /// <summary>
    /// 살아있을 수 있는 최대 소환 몬스터 수를 반환한다.
    /// </summary>
    public int MaxAliveCount => _maxAliveCount;

    /// <summary>
    /// 몬스터 생성 요청 사이의 지연 시간을 반환한다.
    /// </summary>
    public float SpawnIntervalSeconds => _spawnIntervalSeconds;

    /// <summary>
    /// 잘못된 값을 보정하고 이 패턴에 필요한 프리팹 참조를 검증한다.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext)
    {
        if (_spawnPointCount < 1)
        {
            Debug.LogWarning($"[BossPatternData] Summon monster SpawnCount가 1보다 작아서 보정됨. patternId={_patternId}, value={_spawnPointCount}", logContext);
            _spawnPointCount = 1;
        }

        if (_spawnIntervalSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 몬스터 소환 간격 시간이 0보다 작아서 보정됨. patternId={_patternId}, value={_spawnIntervalSeconds}", logContext);
            _spawnIntervalSeconds = 0f;
        }

        if (_enabled && _monsterPrefab == null)
        {
            Debug.LogWarning($"[BossPatternData] SummonMonster 패턴이 활성화되어 있지만 MonsterPrefab이 비어있다. patternId={_patternId}", logContext);
        }
    }
}

/// <summary>
/// 약점 패턴의 순수 설정 값을 저장한다.
/// </summary>
[Serializable]
public struct WeakPointPatternSettings
{
    [Tooltip("이 약점 패턴의 활성화 여부")]
    [SerializeField] private bool _enabled; // 이 약점 패턴의 검증을 활성화한다.

    [Tooltip("이 약점 설정이 속한 공통 패턴 ID")]
    [SerializeField] private string _patternId; // 이 약점 설정 그룹과 연결된 공통 패턴 ID

    [Tooltip("패턴 4 위치에 생성할 약점 프리팹입니다. 이 프리팹은 HealthComponent와 HitReceiver를 사용해야 합니다.")]
    [SerializeField] private GameObject _weakPointPrefab; // 선택된 위치에 생성할 약점 프리팹 애셋

    [Tooltip("약점이 파괴될 때 재생할 선택 파괴 VFX 프리팹")]
    [SerializeField] private GameObject _weakPointDestroyVfxPrefab; // 약점 파괴 시 사용할 선택 폴백 VFX 프리팹

    [Tooltip("약점이 파괴될 때 재생할 EffectService ID")]
    [SerializeField] private E_EffectId _weakPointDestroyEffectId; // 약점 파괴에 사용할 기존 풀링 VFX ID

    [Tooltip("BossPatternAnchorSet에서 활성화할 약점 영역 개수")]
    [Min(0)]
    [SerializeField] private int _weakPointCount; // 실행 시 활성화할 씬 약점 영역 개수

    [Tooltip("이후 약점 위치 선택에서 사용할 재시도 횟수")]
    [Min(1)]
    [SerializeField] private int _weakPointPositionRetryCount; // 이후 약점 배치 검증에서 사용할 재시도 횟수

    [Tooltip("선택된 약점 위치 사이에 필요한 최소 거리")]
    [Min(0f)]
    [SerializeField] private float _minDistanceBetweenWeakPoints; // 약점 위치 선택 중 적용할 최소 간격

    [Tooltip("약점 영역이 활성 상태를 유지하는 시간")]
    [Min(0f)]
    [SerializeField] private float _activeSeconds; // 이후 약점 취약 상태 지속 시간

    [Tooltip("패턴 4 진입이 완료된 뒤 생성된 모든 약점을 파괴해야 하는 제한 시간")]
    [Min(0f)]
    [SerializeField] private float _weakPointTimeLimit; // 패턴 4를 보스에게 유리한 시간 초과로 처리하는 제한 시간

    [Tooltip("패턴 4 약점 제한 시간이 만료될 때 살아있는 모든 Player에게 적용되는 피해량")]
    [Min(0f)]
    [SerializeField] private float _weakPointTimeLimitDamage; // 패턴 4 시간 초과 시 권한 측에서 모든 유효 Player에게 적용하는 피해량

    [Tooltip("패턴 4 진입 애니메이션 이벤트를 받지 못했을 때 사용할 폴백 시간")]
    [Min(0f)]
    [SerializeField] private float _entryAnimationFallbackSeconds; // 패턴 4 진입 완료용 Animation Event 폴백 시간

    [Tooltip("패턴 4가 Groggy로 해결된 뒤 보스가 Groggy 상태를 유지하는 시간")]
    [Min(0f)]
    [SerializeField] private float _groggyDurationSeconds; // 약점 흐름이 해결된 뒤 Groggy 상태 지속 시간

    [Tooltip("약점이 활성화된 동안 적용되는 받는 피해 배율")]
    [Min(0f)]
    [SerializeField] private float _incomingDamageMultiplier; // 이후 약점 피해 처리에서 사용할 피해 배율

    [Tooltip("이후 피격 처리에서 약점 피격으로 취급할 상태 태그")]
    [SerializeField] private string _weakPointStatusTag; // 약점 피격 식별에 사용할 피격 상태 태그

    /// <summary>
    /// 이 약점 패턴의 활성화 여부를 반환한다.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// 이 설정 그룹의 공통 패턴 ID를 반환한다.
    /// </summary>
    public string PatternId => _patternId;

    /// <summary>
    /// 약점 프리팹 애셋 참조를 반환한다.
    /// </summary>
    public GameObject WeakPointPrefab => _weakPointPrefab;

    /// <summary>
    /// 선택 약점 파괴 VFX 프리팹을 반환한다.
    /// </summary>
    public GameObject WeakPointDestroyVfxPrefab => _weakPointDestroyVfxPrefab;

    /// <summary>
    /// 약점 파괴 EffectService ID를 반환한다.
    /// </summary>
    public E_EffectId WeakPointDestroyEffectId => _weakPointDestroyEffectId;

    /// <summary>
    /// 활성화할 약점 영역 개수를 반환한다.
    /// </summary>
    public int WeakPointCount => _weakPointCount;

    /// <summary>
    /// 기존 데이터 읽기 호환성을 위해 활성화할 약점 영역 개수를 반환한다.
    /// </summary>
    public int ActiveAreaCount => _weakPointCount;

    /// <summary>
    /// 약점 위치 재시도 횟수를 반환한다.
    /// </summary>
    public int WeakPointPositionRetryCount => _weakPointPositionRetryCount;

    /// <summary>
    /// 약점 위치 사이에 필요한 최소 거리를 반환한다.
    /// </summary>
    public float MinDistanceBetweenWeakPoints => _minDistanceBetweenWeakPoints;

    /// <summary>
    /// 활성 시간을 초 단위로 반환한다.
    /// </summary>
    public float ActiveSeconds => _activeSeconds;

    /// <summary>
    /// 패턴 4 약점 파괴 제한 시간을 초 단위로 반환한다.
    /// </summary>
    public float WeakPointTimeLimit => _weakPointTimeLimit;

    /// <summary>
    /// 패턴 4 시간 초과 시 살아있는 Player에게 적용할 피해량을 반환한다.
    /// </summary>
    public float WeakPointTimeLimitDamage => _weakPointTimeLimitDamage;

    /// <summary>
    /// 패턴 4 진입 애니메이션 폴백 시간을 반환한다.
    /// </summary>
    public float EntryAnimationFallbackSeconds => _entryAnimationFallbackSeconds;

    /// <summary>
    /// Groggy 지속 시간을 초 단위로 반환한다.
    /// </summary>
    public float GroggyDurationSeconds => _groggyDurationSeconds;

    /// <summary>
    /// 받는 피해 배율을 반환한다.
    /// </summary>
    public float IncomingDamageMultiplier => _incomingDamageMultiplier;

    /// <summary>
    /// 약점 상태 태그를 반환한다.
    /// </summary>
    public string WeakPointStatusTag => _weakPointStatusTag;

    /// <summary>
    /// 이 패턴의 잘못된 값을 보정한다.
    /// </summary>
    public void ValidateOnValidate(UnityEngine.Object logContext)
    {
        if (_weakPointCount < 0)
        {
            Debug.LogWarning($"[BossPatternData] WeakPointCount가 0보다 작아서 보정됨. patternId={_patternId}, value={_weakPointCount}", logContext);
            _weakPointCount = 0;
        }

        if (_enabled && _weakPointPrefab == null)
        {
            Debug.LogWarning($"[BossPatternData] WeakPoint 패턴이 활성화되어 있지만 WeakPointPrefab이 비어있다. patternId={_patternId}", logContext);
        }

        if (_weakPointPositionRetryCount < 1)
        {
            Debug.LogWarning($"[BossPatternData] WeakPointPositionRetryCount가 1보다 작아서 보정됨. patternId={_patternId}, value={_weakPointPositionRetryCount}", logContext);
            _weakPointPositionRetryCount = 1;
        }

        if (_minDistanceBetweenWeakPoints < 0f)
        {
            Debug.LogWarning($"[BossPatternData] MinDistanceBetweenWeakPoints가 0보다 작아서 보정됨. patternId={_patternId}, value={_minDistanceBetweenWeakPoints}", logContext);
            _minDistanceBetweenWeakPoints = 0f;
        }

        if (_activeSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 약점 활성 시간이 0보다 작아서 보정됨. patternId={_patternId}, value={_activeSeconds}", logContext);
            _activeSeconds = 0f;
        }

        if (_weakPointTimeLimit < 0f)
        {
            Debug.LogWarning($"[BossPatternData] WeakPointTimeLimit가 0보다 작아서 보정됨. patternId={_patternId}, value={_weakPointTimeLimit}", logContext);
            _weakPointTimeLimit = 0f;
        }

        if (_weakPointTimeLimitDamage < 0f)
        {
            Debug.LogWarning($"[BossPatternData] WeakPointTimeLimitDamage가 0보다 작아서 보정됨. patternId={_patternId}, value={_weakPointTimeLimitDamage}", logContext);
            _weakPointTimeLimitDamage = 0f;
        }

        if (_entryAnimationFallbackSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] 약점 진입 애니메이션 폴백 시간이 0보다 작아서 보정됨. patternId={_patternId}, value={_entryAnimationFallbackSeconds}", logContext);
            _entryAnimationFallbackSeconds = 0f;
        }

        if (_groggyDurationSeconds < 0f)
        {
            Debug.LogWarning($"[BossPatternData] Groggy 지속 시간이 0보다 작아서 보정됨. patternId={_patternId}, value={_groggyDurationSeconds}", logContext);
            _groggyDurationSeconds = 0f;
        }
    }
}
