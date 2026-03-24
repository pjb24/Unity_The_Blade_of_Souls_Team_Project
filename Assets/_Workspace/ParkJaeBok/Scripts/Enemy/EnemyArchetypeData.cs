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
