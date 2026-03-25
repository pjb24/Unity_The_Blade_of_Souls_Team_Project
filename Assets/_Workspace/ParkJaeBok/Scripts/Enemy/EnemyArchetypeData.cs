using UnityEngine;

/// <summary>
/// Enemy 공통 스탯/행동 규칙/액션 매핑을 데이터로 관리하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "EnemyArchetypeData", menuName = "Enemy/Enemy Archetype Data")]
public class EnemyArchetypeData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("아키타입을 식별하기 위한 문자열 키입니다.")]
    [SerializeField] private string _archetypeId = "Enemy_Normal"; // 아키타입을 식별하기 위한 문자열 키입니다.

    [Header("Movement")]
    [Tooltip("추적/복귀/순찰 이동 시 사용할 기본 이동 속도입니다.")]
    [SerializeField] private float _moveSpeed = 2.5f; // 추적/복귀/순찰 이동 시 사용할 기본 이동 속도입니다.
    [Tooltip("목표 지점 도달로 판정할 최소 거리입니다.")]
    [SerializeField] private float _stoppingDistance = 0.1f; // 목표 지점 도달로 판정할 최소 거리입니다.

    [Header("Locomotion")]
    [Tooltip("초기 로코모션 기본 타입입니다.")]
    [SerializeField] private E_EnemyLocomotionType _defaultLocomotionType = E_EnemyLocomotionType.Grounded; // 초기 로코모션 기본 타입입니다.
    [Tooltip("로코모션 전환 허용 여부입니다.")]
    [SerializeField] private bool _canSwitchLocomotion = true; // 로코모션 전환 허용 여부입니다.
    [Tooltip("로코모션 전환 최소 쿨다운 시간(초)입니다.")]
    [SerializeField] private float _switchCooldown = 0.8f; // 로코모션 전환 최소 쿨다운 시간(초)입니다.
    [Tooltip("공중 상태 최소 체류 시간(초)입니다.")]
    [SerializeField] private float _minAirTime = 0.6f; // 공중 상태 최소 체류 시간(초)입니다.

    [Header("Locomotion - Grounded")]
    [Tooltip("지상 이동 접지 판정 프로브 거리입니다.")]
    [SerializeField] private float _groundProbeDistance = 0.3f; // 지상 이동 접지 판정 프로브 거리입니다.
    [Tooltip("지상 이동에서 허용할 최대 경사 각도(도)입니다.")]
    [SerializeField] private float _slopeLimit = 45f; // 지상 이동에서 허용할 최대 경사 각도(도)입니다.
    [Tooltip("지상 이동에서 엣지 정지 판정 전방 거리입니다.")]
    [SerializeField] private float _edgeStopDistance = 0.35f; // 지상 이동에서 엣지 정지 판정 전방 거리입니다.

    [Header("Locomotion - Floating")]
    [Tooltip("부유 이동 시 유지할 목표 고도 오프셋입니다.")]
    [SerializeField] private float _hoverHeight = 1.5f; // 부유 이동 시 유지할 목표 고도 오프셋입니다.
    [Tooltip("부유 이동 수직 감쇠 계수입니다.")]
    [SerializeField] private float _hoverDamping = 4f; // 부유 이동 수직 감쇠 계수입니다.
    [Tooltip("부유 이동 수직 속도 최대값입니다.")]
    [SerializeField] private float _verticalMaxSpeed = 3.5f; // 부유 이동 수직 속도 최대값입니다.
    [Tooltip("공중 상태 최대 체류 시간(초)입니다.")]
    [SerializeField] private float _maxAirTime = 4f; // 공중 상태 최대 체류 시간(초)입니다.

    [Header("Movement Transition (Legacy Compatibility)")]
    [Tooltip("공격 상태 중 이동 모드 전환 허용 여부입니다.")]
    [SerializeField] private bool _allowMovementTransitionDuringAttack; // 공격 상태 중 이동 모드 전환 허용 여부입니다.
    [Tooltip("부유 이동 모드를 사용할지 여부입니다.")]
    [SerializeField] private bool _useFloatingMovement = true; // 부유 이동 모드를 사용할지 여부입니다.
    [Tooltip("이동 모드 전환 상태를 유지할 시간(초)입니다.")]
    [SerializeField] private float _movementSwitchDuration = 0.2f; // 이동 모드 전환 상태를 유지할 시간(초)입니다.
    [Tooltip("전방 장애물 감지에 사용할 레이 길이 값입니다.")]
    [SerializeField] private float _locomotionObstacleCheckDistance = 0.75f; // 전방 장애물 감지에 사용할 레이 길이 값입니다.
    [Tooltip("접지/착지 지면 감지에 사용할 레이 길이 값입니다.")]
    [SerializeField] private float _locomotionGroundCheckDistance = 1.25f; // 접지/착지 지면 감지에 사용할 레이 길이 값입니다.
    [Tooltip("유효 착지 후보가 없을 때 재탐색까지 대기할 쿨다운 시간(초)입니다.")]
    [SerializeField] private float _landingRequeryCooldown = 0.3f; // 유효 착지 후보가 없을 때 재탐색까지 대기할 쿨다운 시간(초)입니다.

    [Header("Perception")]
    [Tooltip("타겟을 추적 상태로 전환할 감지 반경입니다.")]
    [SerializeField] private float _detectRange = 6f; // 타겟을 추적 상태로 전환할 감지 반경입니다.
    [Tooltip("추적 중 타겟을 상실했다고 판단할 반경입니다.")]
    [SerializeField] private float _loseRange = 8f; // 추적 중 타겟을 상실했다고 판단할 반경입니다.

    [Header("Combat")]
    [Tooltip("공격 액션 요청을 시도할 최대 거리입니다.")]
    [SerializeField] private float _attackRange = 1.5f; // 공격 액션 요청을 시도할 최대 거리입니다.
    [Tooltip("연속 공격 방지를 위한 최소 간격 시간(초)입니다.")]
    [SerializeField] private float _attackCooldown = 1.2f; // 연속 공격 방지를 위한 최소 간격 시간(초)입니다.
    [Tooltip("공격 직후 Recover 상태를 유지할 시간(초)입니다.")]
    [SerializeField] private float _recoverDuration = 0.25f; // 공격 직후 Recover 상태를 유지할 시간(초)입니다.
    [Tooltip("피격 시 HitStun 상태를 유지할 기본 시간(초)입니다.")]
    [SerializeField] private float _hitStunDuration = 0.15f; // 피격 시 HitStun 상태를 유지할 기본 시간(초)입니다.

    [Header("Leash")]
    [Tooltip("스폰 지점 기준 행동 반경 제한 사용 여부입니다.")]
    [SerializeField] private bool _useLeash = true; // 스폰 지점 기준 행동 반경 제한 사용 여부입니다.
    [Tooltip("스폰 지점에서 허용할 최대 이탈 거리입니다.")]
    [SerializeField] private float _leashDistance = 10f; // 스폰 지점에서 허용할 최대 이탈 거리입니다.

    [Header("Patrol")]
    [Tooltip("타겟이 없을 때 순찰 로직 사용 여부입니다.")]
    [SerializeField] private bool _usePatrol = true; // 타겟이 없을 때 순찰 로직 사용 여부입니다.
    [Tooltip("스폰 지점 기준 랜덤 순찰 반경입니다.")]
    [SerializeField] private float _patrolRadius = 2.5f; // 스폰 지점 기준 랜덤 순찰 반경입니다.
    [Tooltip("순찰 지점 도달 후 다음 순찰까지 대기 시간(초)입니다.")]
    [SerializeField] private float _patrolWaitTime = 1f; // 순찰 지점 도달 후 다음 순찰까지 대기 시간(초)입니다.

    [Header("Action Mapping")]
    [Tooltip("스폰 시 요청할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _spawnAction = E_ActionType.Spawn; // 스폰 시 요청할 액션 타입입니다.
    [Tooltip("대기 상태에서 유지할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _idleAction = E_ActionType.Idle; // 대기 상태에서 유지할 액션 타입입니다.
    [Tooltip("순찰 상태에서 유지할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _patrolAction = E_ActionType.Move; // 순찰 상태에서 유지할 액션 타입입니다.
    [Tooltip("추적 이동 상태에서 유지할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _moveAction = E_ActionType.Move; // 추적 이동 상태에서 유지할 액션 타입입니다.
    [Tooltip("공격 상태에서 요청할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _attackAction = E_ActionType.Attack; // 공격 상태에서 요청할 액션 타입입니다.
    [Tooltip("공격 후딜 상태에서 유지할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _recoverAction = E_ActionType.IdleBreakA; // 공격 후딜 상태에서 유지할 액션 타입입니다.
    [Tooltip("피격 경직 상태에서 유지할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _hitStunAction = E_ActionType.Hit; // 피격 경직 상태에서 유지할 액션 타입입니다.
    [Tooltip("사망 시 요청할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _deadAction = E_ActionType.Die; // 사망 시 요청할 액션 타입입니다.

    /// <summary>
    /// 아키타입 식별 문자열을 반환합니다.
    /// </summary>
    public string ArchetypeId => _archetypeId;

    /// <summary>
    /// 이동 속도를 안전 값으로 반환합니다.
    /// </summary>
    public float MoveSpeed => Mathf.Max(0f, _moveSpeed);

    /// <summary>
    /// 목표 도달 판정 거리를 안전 값으로 반환합니다.
    /// </summary>
    public float StoppingDistance => Mathf.Max(0.01f, _stoppingDistance);

    /// <summary>
    /// 공격 상태 중 이동 모드 전환 허용 여부를 반환합니다.
    /// </summary>
    public bool AllowMovementTransitionDuringAttack => _allowMovementTransitionDuringAttack;

    /// <summary>
    /// 기본 로코모션 타입을 반환합니다.
    /// </summary>
    public E_EnemyLocomotionType DefaultLocomotionType => _defaultLocomotionType;

    /// <summary>
    /// 로코모션 전환 허용 여부를 반환합니다.
    /// </summary>
    public bool CanSwitchLocomotion => _canSwitchLocomotion;

    /// <summary>
    /// 로코모션 전환 최소 쿨다운 시간을 안전 값으로 반환합니다.
    /// </summary>
    public float SwitchCooldown => Mathf.Max(0f, _switchCooldown);

    /// <summary>
    /// 공중 최소 체류 시간을 안전 값으로 반환합니다.
    /// </summary>
    public float MinAirTime => Mathf.Max(0f, _minAirTime);

    /// <summary>
    /// 지상 이동 접지 판정 프로브 거리를 안전 값으로 반환합니다.
    /// </summary>
    public float GroundProbeDistance => Mathf.Max(0.05f, _groundProbeDistance);

    /// <summary>
    /// 지상 이동 경사 제한 각도를 안전 값으로 반환합니다.
    /// </summary>
    public float SlopeLimit => Mathf.Clamp(_slopeLimit, 0f, 89f);

    /// <summary>
    /// 지상 이동 엣지 정지 판정 거리를 안전 값으로 반환합니다.
    /// </summary>
    public float EdgeStopDistance => Mathf.Max(0.05f, _edgeStopDistance);

    /// <summary>
    /// 부유 이동 모드 사용 여부를 반환합니다.
    /// </summary>
    public bool UseFloatingMovement => _useFloatingMovement;

    /// <summary>
    /// 부유 이동 시 유지할 고도 오프셋 값을 반환합니다.
    /// </summary>
    public float FloatingAltitude => _hoverHeight;

    /// <summary>
    /// 부유 이동 수직 감쇠 계수를 안전 값으로 반환합니다.
    /// </summary>
    public float HoverDamping => Mathf.Max(0f, _hoverDamping);

    /// <summary>
    /// 부유 이동 수직 속도 최대값을 안전 값으로 반환합니다.
    /// </summary>
    public float VerticalMaxSpeed => Mathf.Max(0f, _verticalMaxSpeed);

    /// <summary>
    /// 공중 최대 체류 시간을 안전 값으로 반환합니다.
    /// </summary>
    public float MaxAirTime => Mathf.Max(MinAirTime, _maxAirTime);

    /// <summary>
    /// 이동 모드 전환 시간을 안전 값으로 반환합니다.
    /// </summary>
    public float MovementSwitchDuration => Mathf.Max(0f, _movementSwitchDuration);

    /// <summary>
    /// 전방 장애물 감지 레이 길이 값을 안전 값으로 반환합니다.
    /// </summary>
    public float LocomotionObstacleCheckDistance => Mathf.Max(0.1f, _locomotionObstacleCheckDistance);

    /// <summary>
    /// 접지/착지 지면 감지 레이 길이 값을 안전 값으로 반환합니다.
    /// </summary>
    public float LocomotionGroundCheckDistance => Mathf.Max(0.1f, _locomotionGroundCheckDistance);

    /// <summary>
    /// 착지 후보 재탐색 쿨다운 시간을 안전 값으로 반환합니다.
    /// </summary>
    public float LandingRequeryCooldown => Mathf.Max(0f, _landingRequeryCooldown);

    /// <summary>
    /// 감지 반경을 안전 값으로 반환합니다.
    /// </summary>
    public float DetectRange => Mathf.Max(0.05f, _detectRange);

    /// <summary>
    /// 상실 반경을 감지 반경 이상으로 보정해 반환합니다.
    /// </summary>
    public float LoseRange => Mathf.Max(DetectRange, _loseRange);

    /// <summary>
    /// 공격 반경을 안전 값으로 반환합니다.
    /// </summary>
    public float AttackRange => Mathf.Max(0.05f, _attackRange);

    /// <summary>
    /// 공격 쿨다운 시간을 안전 값으로 반환합니다.
    /// </summary>
    public float AttackCooldown => Mathf.Max(0f, _attackCooldown);

    /// <summary>
    /// 공격 후딜 시간을 안전 값으로 반환합니다.
    /// </summary>
    public float RecoverDuration => Mathf.Max(0f, _recoverDuration);

    /// <summary>
    /// 피격 경직 시간을 안전 값으로 반환합니다.
    /// </summary>
    public float HitStunDuration => Mathf.Max(0f, _hitStunDuration);

    /// <summary>
    /// 행동 반경 제한 사용 여부를 반환합니다.
    /// </summary>
    public bool UseLeash => _useLeash;

    /// <summary>
    /// 행동 반경 제한 거리를 안전 값으로 반환합니다.
    /// </summary>
    public float LeashDistance => Mathf.Max(0.1f, _leashDistance);

    /// <summary>
    /// 순찰 로직 사용 여부를 반환합니다.
    /// </summary>
    public bool UsePatrol => _usePatrol;

    /// <summary>
    /// 순찰 반경을 안전 값으로 반환합니다.
    /// </summary>
    public float PatrolRadius => Mathf.Max(0f, _patrolRadius);

    /// <summary>
    /// 순찰 대기 시간을 안전 값으로 반환합니다.
    /// </summary>
    public float PatrolWaitTime => Mathf.Max(0f, _patrolWaitTime);

    /// <summary>
    /// 스폰 시 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType SpawnAction => _spawnAction;

    /// <summary>
    /// 대기 상태 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType IdleAction => _idleAction;

    /// <summary>
    /// 순찰 상태 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType PatrolAction => _patrolAction;

    /// <summary>
    /// 추적 이동 상태 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType MoveAction => _moveAction;

    /// <summary>
    /// 공격 상태 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType AttackAction => _attackAction;

    /// <summary>
    /// 공격 후딜 상태 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType RecoverAction => _recoverAction;

    /// <summary>
    /// 피격 경직 상태 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType HitStunAction => _hitStunAction;

    /// <summary>
    /// 사망 상태 액션 타입을 반환합니다.
    /// </summary>
    public E_ActionType DeadAction => _deadAction;
}
