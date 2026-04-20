using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMeshAgent 경로 계산과 Rigidbody2D Kinematic 이동을 분리해 관리하는 이동 컨트롤러입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyMovementController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("실제 이동 주체(일반적으로 Parent 루트) Transform 참조입니다. 비어 있으면 Rigidbody2D Transform을 사용합니다.")]
    [SerializeField] private Transform _movementRoot; // 실제 이동 기준으로 사용할 루트 Transform 참조입니다.
    [Tooltip("Kinematic 이동을 수행할 Rigidbody2D 참조입니다.")]
    [SerializeField] private Rigidbody2D _rigidbody2D; // 실제 이동을 담당하는 Rigidbody2D 참조입니다.
    [Tooltip("경로 계산 전용으로 사용할 NavMeshAgent 참조입니다.")]
    [SerializeField] private NavMeshAgent _agent; // 경로 계산 전용 NavMeshAgent 참조입니다.

    [Header("Agent Runtime Settings")]
    [Tooltip("NavMeshAgent 이동 속도입니다. 실제 이동량 계산에 사용됩니다.")]
    [SerializeField] private float _agentSpeed = 3.5f; // NavMeshAgent 속도 설정 값입니다.
    [Tooltip("NavMeshAgent 가속도 설정 값입니다.")]
    [SerializeField] private float _agentAcceleration = 30f; // NavMeshAgent 가속도 설정 값입니다.
    [Tooltip("NavMeshAgent 각속도 설정 값입니다. 2D에서는 주로 경로 품질에 영향이 있습니다.")]
    [SerializeField] private float _agentAngularSpeed = 720f; // NavMeshAgent 각속도 설정 값입니다.
    [Tooltip("NavMeshAgent stoppingDistance입니다. 공격 범위와 별도로 경로 종단 근접 판단에 사용됩니다.")]
    [SerializeField] private float _agentStoppingDistance = 0.1f; // NavMeshAgent stoppingDistance 설정 값입니다.
    [Tooltip("agent.nextPosition과 Rigidbody2D.position의 동기화 허용 오차입니다.")]
    [SerializeField] private float _positionSyncTolerance = 0.2f; // Agent/Rigidbody 위치 오차 허용 범위입니다.
    [Tooltip("Path Pending 상태가 이 시간을 초과하면 경고 로그를 출력합니다.")]
    [SerializeField] private float _pathPendingWarningThreshold = 1.0f; // PathPending 장기 지속 경고 임계 시간입니다.

    private bool _movementEnabled; // 이동 적용 활성화 여부입니다.
    private Vector2 _currentDestination; // 현재 목적지 캐시 값입니다.
    private bool _hasDestination; // 목적지 유효 여부입니다.
    private float _pathPendingStartedAt = -1f; // PathPending 시작 시각 캐시입니다.

    /// <summary>
    /// 현재 목적지 유효 여부를 반환합니다.
    /// </summary>
    public bool HasDestination => _hasDestination;

    /// <summary>
    /// 현재 목적지를 반환합니다.
    /// </summary>
    public Vector2 CurrentDestination => _currentDestination;

    /// <summary>
    /// 현재 NavMesh 경로 대기 상태 여부를 반환합니다.
    /// </summary>
    public bool IsPathPending => _agent != null && _agent.pathPending;

    /// <summary>
    /// 현재 NavMesh 경로 상태를 반환합니다.
    /// </summary>
    public NavMeshPathStatus CurrentPathStatus => _agent != null ? _agent.pathStatus : NavMeshPathStatus.PathInvalid;

    /// <summary>
    /// Agent와 Rigidbody의 현재 위치 차이를 반환합니다.
    /// </summary>
    public float AgentRigidbodyDelta => _agent == null || _rigidbody2D == null
        ? float.PositiveInfinity
        : Vector2.Distance(_agent.nextPosition, _rigidbody2D.position);

    /// <summary>
    /// 현재 이동 속도 크기를 반환합니다.
    /// </summary>
    public float CurrentVelocityMagnitude => _rigidbody2D == null ? 0f : _rigidbody2D.linearVelocity.magnitude;

    /// <summary>
    /// 런타임에서 사용하는 현재 월드 위치를 반환합니다.
    /// </summary>
    public Vector2 RuntimeWorldPosition => _rigidbody2D != null
        ? _rigidbody2D.position
        : (_movementRoot != null ? (Vector2)_movementRoot.position : (Vector2)transform.position);

    /// <summary>
    /// 컴포넌트 자동 연결과 NavMeshAgent 정책 적용을 수행합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveDependencies();
        ApplyAgentPolicy();
    }

    /// <summary>
    /// 에디터 값 변경 시 설정값 유효성을 보정하고 정책을 적용합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_agentSpeed <= 0f)
        {
            Debug.LogWarning($"[EnemyMovementController] Invalid _agentSpeed({_agentSpeed}) on {name}. Fallback to 1.");
            _agentSpeed = 1f;
        }

        if (_agentAcceleration <= 0f)
        {
            Debug.LogWarning($"[EnemyMovementController] Invalid _agentAcceleration({_agentAcceleration}) on {name}. Fallback to 1.");
            _agentAcceleration = 1f;
        }

        if (_agentStoppingDistance < 0f)
        {
            Debug.LogWarning($"[EnemyMovementController] Invalid _agentStoppingDistance({_agentStoppingDistance}) on {name}. Fallback to 0.");
            _agentStoppingDistance = 0f;
        }

        if (_positionSyncTolerance <= 0f)
        {
            Debug.LogWarning($"[EnemyMovementController] Invalid _positionSyncTolerance({_positionSyncTolerance}) on {name}. Fallback to 0.01.");
            _positionSyncTolerance = 0.01f;
        }

        ApplyAgentPolicy();
    }

    /// <summary>
    /// 물리 프레임에서 NavMesh 결과를 기반으로 Rigidbody2D.MovePosition 이동을 수행합니다.
    /// </summary>
    private void FixedUpdate()
    {
        if (!_movementEnabled)
        {
            return;
        }

        if (!ValidateMoveRuntime())
        {
            return;
        }

        if (_agent.pathPending)
        {
            if (_pathPendingStartedAt < 0f)
            {
                _pathPendingStartedAt = Time.time;
            }
            else if (Time.time - _pathPendingStartedAt >= _pathPendingWarningThreshold)
            {
                Debug.LogWarning($"[EnemyMovementController] Path pending too long on {name}. Check NavMesh bake and agent settings.");
                _pathPendingStartedAt = Time.time;
            }

            return;
        }

        _pathPendingStartedAt = -1f;

        if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            Debug.LogWarning($"[EnemyMovementController] Invalid path on {name}. movement stopped.");
            StopMovement();
            return;
        }

        Vector2 desired = _agent.desiredVelocity;
        Vector2 clamped = Vector2.ClampMagnitude(desired, _agent.speed);
        Vector2 next = _rigidbody2D.position + clamped * Time.fixedDeltaTime;
        _rigidbody2D.MovePosition(next);
        _rigidbody2D.linearVelocity = clamped;
        SyncAgentToRigidbodyIfNeeded();
    }

    /// <summary>
    /// 의존성 자동 연결을 시도합니다.
    /// </summary>
    public void TryResolveDependencies()
    {
        if (_movementRoot == null)
        {
            _movementRoot = transform;
        }

        if (_rigidbody2D == null)
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
        }

        if (_rigidbody2D == null)
        {
            _rigidbody2D = GetComponentInParent<Rigidbody2D>();
        }

        if (_agent == null)
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        if (_rigidbody2D == null)
        {
            Debug.LogWarning($"[EnemyMovementController] Missing Rigidbody2D on {name}.");
        }
        else if (_movementRoot == transform && _rigidbody2D.transform != transform)
        {
            _movementRoot = _rigidbody2D.transform;
        }

        if (_agent == null)
        {
            Debug.LogWarning($"[EnemyMovementController] Missing NavMeshAgent on {name}. NavMeshPlus pathing is unavailable.");
        }
        else if (_rigidbody2D != null && _rigidbody2D.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning($"[EnemyMovementController] Rigidbody2D bodyType should be Kinematic on {name}.");
        }
    }

    /// <summary>
    /// NavMeshAgent를 2D Kinematic 정책에 맞게 강제 설정합니다.
    /// </summary>
    public void ApplyAgentPolicy()
    {
        if (_agent == null)
        {
            return;
        }

        _agent.updatePosition = false;
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
        _agent.speed = _agentSpeed;
        _agent.acceleration = _agentAcceleration;
        _agent.angularSpeed = _agentAngularSpeed;
        _agent.stoppingDistance = _agentStoppingDistance;
    }

    /// <summary>
    /// 현재 위치를 기준으로 Agent와 Rigidbody를 즉시 동기화합니다.
    /// </summary>
    public void ForceSyncNow()
    {
        if (_agent == null || _rigidbody2D == null)
        {
            Debug.LogWarning($"[EnemyMovementController] ForceSyncNow failed due to missing dependencies on {name}.");
            return;
        }

        Vector2 currentPosition = RuntimeWorldPosition;
        _agent.nextPosition = currentPosition;
        _agent.Warp(currentPosition);
    }

    /// <summary>
    /// NavMesh 목적지를 갱신합니다.
    /// </summary>
    public bool TrySetDestination(Vector2 destination)
    {
        if (_agent == null)
        {
            Debug.LogWarning($"[EnemyMovementController] TrySetDestination failed because NavMeshAgent is missing on {name}.");
            return false;
        }

        if (!_agent.isActiveAndEnabled)
        {
            Debug.LogWarning($"[EnemyMovementController] TrySetDestination failed because NavMeshAgent is disabled on {name}.");
            return false;
        }

        if (!_agent.isOnNavMesh)
        {
            Debug.LogWarning($"[EnemyMovementController] TrySetDestination failed because agent is not on NavMesh on {name}. Check NavMeshPlus bake.");
            return false;
        }

        bool success = _agent.SetDestination(destination);
        if (!success)
        {
            Debug.LogWarning($"[EnemyMovementController] SetDestination failed on {name}. destination={destination}");
            return false;
        }

        _currentDestination = destination;
        _hasDestination = true;
        return true;
    }

    /// <summary>
    /// 이동을 활성/비활성으로 전환합니다.
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        _movementEnabled = enabled;

        if (!enabled)
        {
            StopMovement();
        }
    }

    /// <summary>
    /// 목적지를 제거하고 이동을 정지합니다.
    /// </summary>
    public void StopMovement()
    {
        _hasDestination = false;

        if (_agent != null)
        {
            _agent.ResetPath();
            _agent.nextPosition = RuntimeWorldPosition;
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// 복귀 완료 판정을 수행합니다.
    /// </summary>
    public bool IsArrived(float stopDistance, float arrivalVelocityEpsilon)
    {
        if (_agent == null || _rigidbody2D == null)
        {
            return false;
        }

        if (_agent.pathPending)
        {
            return false;
        }

        if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            return false;
        }

        bool distanceReached = _agent.remainingDistance <= stopDistance;
        bool speedReached = _rigidbody2D.linearVelocity.magnitude <= arrivalVelocityEpsilon;
        bool positionSynced = AgentRigidbodyDelta <= _positionSyncTolerance;
        return distanceReached && speedReached && positionSynced;
    }

    /// <summary>
    /// 현재 이동 관련 런타임 유효성을 검사합니다.
    /// </summary>
    private bool ValidateMoveRuntime()
    {
        if (_agent == null || _rigidbody2D == null)
        {
            Debug.LogWarning($"[EnemyMovementController] Move skipped due to missing dependencies on {name}.");
            return false;
        }

        if (!_agent.isOnNavMesh)
        {
            Debug.LogWarning($"[EnemyMovementController] Move skipped because agent is not on NavMesh on {name}.");
            return false;
        }

        if (!_hasDestination)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Agent와 Rigidbody 위치가 틀어졌을 때 agent.nextPosition을 동기화합니다.
    /// </summary>
    private void SyncAgentToRigidbodyIfNeeded()
    {
        if (_agent == null || _rigidbody2D == null)
        {
            return;
        }

        if (Vector2.Distance(_agent.nextPosition, _rigidbody2D.position) > _positionSyncTolerance)
        {
            _agent.nextPosition = _rigidbody2D.position;
        }
    }
}
