using UnityEngine;

/// <summary>
/// Enemy AI가 전달한 목적지를 기준으로 Rigidbody2D 이동을 수행하는 드라이버입니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovementDriver : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("이동 계산에 사용할 기본 속도 값입니다.")]
    [SerializeField] private float _moveSpeed = 2.5f; // 이동 계산에 사용할 기본 속도 값입니다.
    [Tooltip("목적지 도달로 판정할 최소 거리 값입니다.")]
    [SerializeField] private float _stoppingDistance = 0.1f; // 목적지 도달로 판정할 최소 거리 값입니다.

    [Header("Grounded Solver")]
    [Tooltip("접지 판정에 사용할 하단 프로브 시작 오프셋입니다.")]
    [SerializeField] private Vector2 _groundProbeOffset = new Vector2(0f, 0.1f); // 접지 판정에 사용할 하단 프로브 시작 오프셋입니다.
    [Tooltip("접지 판정에 사용할 하단 프로브 거리입니다.")]
    [SerializeField] private float _groundProbeDistance = 0.3f; // 접지 판정에 사용할 하단 프로브 거리입니다.
    [Tooltip("Grounded 접지/엣지 판정에 사용할 지면 LayerMask입니다.")]
    [SerializeField] private LayerMask _groundLayerMask = Physics2D.DefaultRaycastLayers; // Grounded 접지/엣지 판정에 사용할 지면 LayerMask입니다.
    [Tooltip("엣지 감지에 사용할 전방 오프셋 거리입니다.")]
    [SerializeField] private float _edgeProbeForwardOffset = 0.35f; // 엣지 감지에 사용할 전방 오프셋 거리입니다.
    [Tooltip("지상 이동에서 허용할 최대 경사 각도(도)입니다.")]
    [SerializeField] private float _slopeLimit = 45f; // 지상 이동에서 허용할 최대 경사 각도(도)입니다.
    [Tooltip("지상 상태에서 바닥 스냅 보정을 적용할 최대 거리입니다.")]
    [SerializeField] private float _groundSnapDistance = 0.2f; // 지상 상태에서 바닥 스냅 보정을 적용할 최대 거리입니다.
    [Tooltip("엣지 앞 정지 대신 우회 후보를 생성할지 여부입니다.")]
    [SerializeField] private bool _enableEdgeBypassCandidate = true; // 엣지 앞 정지 대신 우회 후보를 생성할지 여부입니다.
    [Tooltip("Grounded 이동에서 중력형 수직 속도 계산을 사용할지 여부입니다.")]
    [SerializeField] private bool _useGroundedGravity = true; // Grounded 이동에서 중력형 수직 속도 계산을 사용할지 여부입니다.
    [Tooltip("Grounded 이동 중 적용할 중력 가속도 값입니다.")]
    [SerializeField] private float _groundGravityAcceleration = 25f; // Grounded 이동 중 적용할 중력 가속도 값입니다.
    [Tooltip("Grounded 이동 중 허용할 최대 낙하 속도 절대값입니다.")]
    [SerializeField] private float _groundMaxFallSpeed = 12f; // Grounded 이동 중 허용할 최대 낙하 속도 절대값입니다.
    [Tooltip("Grounded 접지 상태 유지 시 적용할 최소 하향 속도 값입니다.")]
    [SerializeField] private float _groundStickVelocity = 1.5f; // Grounded 접지 상태 유지 시 적용할 최소 하향 속도 값입니다.

    [Header("Grounded Auto Tuning")]
    [Tooltip("Collider 크기 기반 Ground Snap 파라미터 자동 보정 사용 여부입니다.")]
    [SerializeField] private bool _autoTuneGroundSnapByCollider = true; // Collider 크기 기반 Ground Snap 파라미터 자동 보정 사용 여부입니다.
    [Tooltip("자동 보정 시 기준으로 사용할 Collider2D 참조입니다.")]
    [SerializeField] private Collider2D _groundReferenceCollider; // 자동 보정 시 기준으로 사용할 Collider2D 참조입니다.
    [Tooltip("Collider 높이에 곱해 Ground Probe Distance를 계산하는 비율입니다.")]
    [SerializeField] private float _probeDistanceByColliderHeight = 0.28f; // Collider 높이에 곱해 Ground Probe Distance를 계산하는 비율입니다.
    [Tooltip("Collider 높이에 곱해 Ground Snap Distance를 계산하는 비율입니다.")]
    [SerializeField] private float _snapDistanceByColliderHeight = 0.18f; // Collider 높이에 곱해 Ground Snap Distance를 계산하는 비율입니다.
    [Tooltip("Collider 너비에 곱해 Edge Stop Distance를 계산하는 비율입니다.")]
    [SerializeField] private float _edgeStopByColliderWidth = 0.25f; // Collider 너비에 곱해 Edge Stop Distance를 계산하는 비율입니다.
    [Tooltip("자동 계산된 Ground Probe Distance 최소값입니다.")]
    [SerializeField] private float _autoProbeDistanceMin = 0.1f; // 자동 계산된 Ground Probe Distance 최소값입니다.
    [Tooltip("자동 계산된 Ground Probe Distance 최대값입니다.")]
    [SerializeField] private float _autoProbeDistanceMax = 1.25f; // 자동 계산된 Ground Probe Distance 최대값입니다.
    [Tooltip("자동 계산된 Ground Snap Distance 최소값입니다.")]
    [SerializeField] private float _autoSnapDistanceMin = 0.05f; // 자동 계산된 Ground Snap Distance 최소값입니다.
    [Tooltip("자동 계산된 Ground Snap Distance 최대값입니다.")]
    [SerializeField] private float _autoSnapDistanceMax = 0.8f; // 자동 계산된 Ground Snap Distance 최대값입니다.
    [Tooltip("자동 계산된 Edge Stop Distance 최소값입니다.")]
    [SerializeField] private float _autoEdgeStopMin = 0.1f; // 자동 계산된 Edge Stop Distance 최소값입니다.
    [Tooltip("자동 계산된 Edge Stop Distance 최대값입니다.")]
    [SerializeField] private float _autoEdgeStopMax = 1.5f; // 자동 계산된 Edge Stop Distance 최대값입니다.
    [Tooltip("런타임 콜라이더 크기 변화 재계산 판정에 사용할 허용 오차입니다.")]
    [SerializeField] private float _autoTuneColliderSizeEpsilon = 0.001f; // 런타임 콜라이더 크기 변화 재계산 판정에 사용할 허용 오차입니다.
    [Tooltip("Collider 기준 Ground Probe Offset 자동 보정 사용 여부입니다.")]
    [SerializeField] private bool _autoTuneGroundProbeOffsetByCollider = true; // Collider 기준 Ground Probe Offset 자동 보정 사용 여부입니다.
    [Tooltip("Collider 높이에 곱해 Ground Probe 시작점의 하단 여유 높이를 계산하는 비율입니다.")]
    [SerializeField] private float _probeOffsetLiftByColliderHeight = 0.1f; // Collider 높이에 곱해 Ground Probe 시작점의 하단 여유 높이를 계산하는 비율입니다.
    [Tooltip("자동 계산된 Ground Probe 시작점 하단 여유 높이 최소값입니다.")]
    [SerializeField] private float _autoProbeOffsetLiftMin = 0.02f; // 자동 계산된 Ground Probe 시작점 하단 여유 높이 최소값입니다.
    [Tooltip("자동 계산된 Ground Probe 시작점 하단 여유 높이 최대값입니다.")]
    [SerializeField] private float _autoProbeOffsetLiftMax = 0.25f; // 자동 계산된 Ground Probe 시작점 하단 여유 높이 최대값입니다.

    [Header("Floating Solver")]
    [Tooltip("부유 이동 시 유지하려는 기본 호버 높이 값입니다.")]
    [SerializeField] private float _hoverHeight = 1.5f; // 부유 이동 시 유지하려는 기본 호버 높이 값입니다.
    [Tooltip("부유 이동에서 수직 오차 보정 감쇠 계수입니다.")]
    [SerializeField] private float _hoverDamping = 4f; // 부유 이동에서 수직 오차 보정 감쇠 계수입니다.
    [Tooltip("부유 이동에서 수직 속도 최대값입니다.")]
    [SerializeField] private float _verticalMaxSpeed = 3.5f; // 부유 이동에서 수직 속도 최대값입니다.

    [Header("Runtime")]
    [Tooltip("현재 프레임에서 유효한 목적지 보유 여부입니다.")]
    [SerializeField] private bool _hasTargetPosition; // 현재 프레임에서 유효한 목적지 보유 여부입니다.
    [Tooltip("추적/복귀에 사용할 현재 목적지 월드 좌표입니다.")]
    [SerializeField] private Vector2 _targetPosition; // 추적/복귀에 사용할 현재 목적지 월드 좌표입니다.
    [Tooltip("현재 프레임에서 계산에 반영할 이동 속도 값입니다.")]
    [SerializeField] private float _commandMoveSpeed = 2.5f; // 현재 프레임에서 계산에 반영할 이동 속도 값입니다.
    [Tooltip("고도 명령 사용 여부입니다.")]
    [SerializeField] private bool _useAltitudeCommand; // 고도 명령 사용 여부입니다.
    [Tooltip("고도 명령 사용 시 유지할 목표 Y 좌표입니다.")]
    [SerializeField] private float _targetAltitude; // 고도 명령 사용 시 유지할 목표 Y 좌표입니다.
    [Tooltip("Grounded Solver가 계산한 현재 엣지 우회 후보 지점입니다.")]
    [SerializeField] private Vector2 _edgeBypassCandidate; // Grounded Solver가 계산한 현재 엣지 우회 후보 지점입니다.
    [Tooltip("현재 프레임에서 유효한 엣지 우회 후보가 존재하는지 여부입니다.")]
    [SerializeField] private bool _hasEdgeBypassCandidate; // 현재 프레임에서 유효한 엣지 우회 후보가 존재하는지 여부입니다.
    [Tooltip("Grounded Solver에 전달할 최종 접지 프로브 거리 런타임 값입니다.")]
    [SerializeField] private float _resolvedGroundProbeDistance; // Grounded Solver에 전달할 최종 접지 프로브 거리 런타임 값입니다.
    [Tooltip("Grounded Solver에 전달할 최종 바닥 스냅 거리 런타임 값입니다.")]
    [SerializeField] private float _resolvedGroundSnapDistance; // Grounded Solver에 전달할 최종 바닥 스냅 거리 런타임 값입니다.
    [Tooltip("Grounded Solver에 전달할 최종 엣지 전방 오프셋 런타임 값입니다.")]
    [SerializeField] private float _resolvedEdgeProbeForwardOffset; // Grounded Solver에 전달할 최종 엣지 전방 오프셋 런타임 값입니다.
    [Tooltip("Ground Snap 자동 보정 기준 콜라이더의 이전 프레임 크기 캐시입니다.")]
    [SerializeField] private Vector2 _lastGroundReferenceColliderSize; // Ground Snap 자동 보정 기준 콜라이더의 이전 프레임 크기 캐시입니다.
    [Tooltip("Grounded 중력형 계산에서 누적 관리하는 현재 수직 속도 값입니다.")]
    [SerializeField] private float _groundVerticalVelocity; // Grounded 중력형 계산에서 누적 관리하는 현재 수직 속도 값입니다.
    [Tooltip("Grounded 스냅 계산에 사용할 현재 기준 Collider bounds.min.y 값입니다.")]
    [SerializeField] private float _groundColliderBoundsMinY; // Grounded 스냅 계산에 사용할 현재 기준 Collider bounds.min.y 값입니다.

    private Rigidbody2D _rigidbody2D; // 물리 기반 이동 적용에 사용할 리지드바디 참조입니다.
    private Vector2 _currentVelocity; // 현재 프레임 계산된 이동 속도 벡터입니다.
    private GroundedLocomotionSolver _groundedSolver; // 지상 이동 계산에 사용할 전략 솔버 인스턴스입니다.
    private FloatingLocomotionSolver _floatingSolver; // 부유 이동 계산에 사용할 전략 솔버 인스턴스입니다.

    /// <summary>
    /// 현재 이동 중인지 반환합니다.
    /// </summary>
    public bool IsMoving => _currentVelocity.sqrMagnitude > 0.0001f;

    /// <summary>
    /// 현재 이동 속도 벡터를 반환합니다.
    /// </summary>
    public Vector2 CurrentVelocity => _currentVelocity;

    /// <summary>
    /// 현재 바라보는 방향을 좌우 스케일 부호로 판정해 반환합니다.
    /// </summary>
    public int FacingDirection => transform.localScale.x >= 0f ? 1 : -1;

    /// <summary>
    /// 현재 명령 기반 로코모션 타입(지상/부유)을 반환합니다.
    /// </summary>
    public E_EnemyLocomotionType CurrentLocomotionType => _useAltitudeCommand ? E_EnemyLocomotionType.Floating : E_EnemyLocomotionType.Grounded;

    /// <summary>
    /// 리지드바디와 이동 솔버를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _groundedSolver = new GroundedLocomotionSolver();
        _floatingSolver = new FloatingLocomotionSolver();
        TryResolveGroundReferenceCollider();
        ApplyGroundSnapAutoTuningByCollider();
        CacheCurrentGroundReferenceColliderSize();
    }

    /// <summary>
    /// 인스펙터 값 변경 시 Ground Snap 자동 보정 설정의 참조를 정리하고 값을 갱신합니다.
    /// </summary>
    private void OnValidate()
    {
        TryResolveGroundReferenceCollider();
        ApplyGroundSnapAutoTuningByCollider();
        CacheCurrentGroundReferenceColliderSize();
    }

    /// <summary>
    /// 활성 이동 전략을 선택해 이동 속도/방향/도달 여부를 계산하고 리지드바디에 반영합니다.
    /// </summary>
    private void FixedUpdate()
    {
        UpdateAutoTuningIfColliderChanged();

        if (_hasTargetPosition == false)
        {
            _hasEdgeBypassCandidate = false;
            ApplyStop();
            return;
        }

        LocomotionSolveContext context = BuildSolveContext(); // 솔버 전략 계산에 전달할 입력 스냅샷입니다.
        LocomotionSolveResult result = _useAltitudeCommand
            ? _floatingSolver.Solve(context)
            : _groundedSolver.Solve(context); // 활성 이동 전략이 계산한 이번 프레임 결과입니다.

        _currentVelocity = result.Velocity;
        if (!_useAltitudeCommand)
        {
            _groundVerticalVelocity = result.NextGroundVerticalVelocity;
        }
        else
        {
            _groundVerticalVelocity = 0f;
        }

        if (result.HasSnapPosition)
        {
            _rigidbody2D.position = result.SnapPosition;
        }
        _rigidbody2D.linearVelocity = _currentVelocity;

        if (_enableEdgeBypassCandidate && !result.ReachedTarget && _groundedSolver.HasBypassCandidate)
        {
            _edgeBypassCandidate = _groundedSolver.BypassCandidate;
            _hasEdgeBypassCandidate = true;
        }
        else
        {
            _hasEdgeBypassCandidate = false;
        }

        UpdateFacingByDirection(result.FacingDirection, _currentVelocity.x);
    }

    /// <summary>
    /// 현재 설정값으로 솔버 공통 입력 컨텍스트를 구성합니다.
    /// </summary>
    private LocomotionSolveContext BuildSolveContext()
    {
        Vector2 currentPosition = _rigidbody2D.position; // 이동 계산 기준이 되는 현재 위치입니다.
        Vector2 targetPosition = _targetPosition; // 이동 계산에 사용할 현재 목적지 좌표입니다.
        float desiredAltitude = _useAltitudeCommand ? _targetAltitude : currentPosition.y + _hoverHeight; // 부유 이동에서 사용할 목표 고도 좌표입니다.
        _groundColliderBoundsMinY = ResolveGroundColliderBoundsMinY(); // Grounded 스냅 계산에 사용할 현재 기준 Collider bounds.min.y 값입니다.

        return new LocomotionSolveContext(
            transform,
            currentPosition,
            targetPosition,
            desiredAltitude,
            Mathf.Max(0.01f, _stoppingDistance),
            Mathf.Max(0f, _commandMoveSpeed),
            _hoverHeight,
            Mathf.Max(0f, _hoverDamping),
            Mathf.Max(0f, _verticalMaxSpeed),
            _groundProbeOffset,
            Mathf.Max(0.01f, _resolvedGroundProbeDistance),
            _groundLayerMask,
            Mathf.Max(0.01f, _resolvedEdgeProbeForwardOffset),
            Mathf.Clamp(_slopeLimit, 0f, 89f),
            Mathf.Max(0f, _resolvedGroundSnapDistance),
            _enableEdgeBypassCandidate,
            Time.fixedDeltaTime,
            _useGroundedGravity,
            Mathf.Max(0f, _groundGravityAcceleration),
            Mathf.Max(0f, _groundMaxFallSpeed),
            Mathf.Max(0f, _groundStickVelocity),
            _groundVerticalVelocity,
            _groundColliderBoundsMinY);
    }

    /// <summary>
    /// 이동을 즉시 정지하고 속도 상태를 초기화합니다.
    /// </summary>
    private void ApplyStop()
    {
        _currentVelocity = Vector2.zero;
        _groundVerticalVelocity = 0f;
        _rigidbody2D.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 아키타입 데이터의 이동 파라미터를 런타임 값에 반영합니다.
    /// </summary>
    public void ApplyArchetype(EnemyArchetypeData archetype)
    {
        if (archetype == null)
        {
            return;
        }

        _moveSpeed = archetype.MoveSpeed;
        _stoppingDistance = archetype.StoppingDistance;
        _commandMoveSpeed = _moveSpeed;
        _hoverHeight = archetype.FloatingAltitude;
        _hoverDamping = archetype.HoverDamping;
        _verticalMaxSpeed = archetype.VerticalMaxSpeed;
        _groundProbeDistance = archetype.GroundProbeDistance;
        _edgeProbeForwardOffset = archetype.EdgeStopDistance;
        _slopeLimit = archetype.SlopeLimit;
        ApplyGroundSnapAutoTuningByCollider();
        CacheCurrentGroundReferenceColliderSize();
    }

    /// <summary>
    /// EnemyBrain에서 계산한 최종 목표점/속도/고도 명령을 적용합니다.
    /// </summary>
    public void ApplyBrainCommand(Vector2 targetPosition, bool hasTargetPosition, float moveSpeed, bool useAltitudeCommand, float targetAltitude)
    {
        _targetPosition = targetPosition;
        _hasTargetPosition = hasTargetPosition;
        _commandMoveSpeed = Mathf.Max(0f, moveSpeed);
        _useAltitudeCommand = useAltitudeCommand;
        _targetAltitude = targetAltitude;

        if (!hasTargetPosition)
        {
            ApplyStop();
        }
    }

    /// <summary>
    /// 지정 좌표를 목적지로 설정해 이동을 시작합니다.
    /// </summary>
    public void SetTargetPosition(Vector2 targetPosition)
    {
        _targetPosition = targetPosition;
        _hasTargetPosition = true;
        _commandMoveSpeed = Mathf.Max(0f, _moveSpeed);
    }

    /// <summary>
    /// 목적지를 제거하고 즉시 이동을 정지합니다.
    /// </summary>
    public void ClearTargetPosition()
    {
        _hasTargetPosition = false;
        _hasEdgeBypassCandidate = false;
        ApplyStop();
    }

    /// <summary>
    /// 현재 프레임 Grounded Solver가 계산한 유효한 엣지 우회 후보 지점을 반환합니다.
    /// </summary>
    public bool TryGetEdgeBypassCandidate(out Vector2 edgeBypassCandidate)
    {
        if (_hasEdgeBypassCandidate)
        {
            edgeBypassCandidate = _edgeBypassCandidate;
            return true;
        }

        edgeBypassCandidate = Vector2.zero;
        return false;
    }

    /// <summary>
    /// 솔버가 반환한 방향 우선값과 속도값으로 좌우 바라보기를 갱신합니다.
    /// </summary>
    private void UpdateFacingByDirection(int preferredFacingDirection, float velocityX)
    {
        if (preferredFacingDirection != 0)
        {
            Vector3 preferredScale = transform.localScale; // 솔버가 반환한 선호 방향을 반영할 로컬 스케일입니다.
            preferredScale.x = Mathf.Abs(preferredScale.x) * Mathf.Sign(preferredFacingDirection);
            transform.localScale = preferredScale;
            return;
        }

        if (Mathf.Abs(velocityX) < 0.001f)
        {
            return;
        }

        Vector3 velocityScale = transform.localScale; // 속도 기반 방향 갱신에 사용할 로컬 스케일입니다.
        velocityScale.x = Mathf.Abs(velocityScale.x) * Mathf.Sign(velocityX);
        transform.localScale = velocityScale;
    }

    /// <summary>
    /// Ground Snap 자동 보정에 사용할 Collider 참조가 비어 있으면 동일 오브젝트에서 보정합니다.
    /// </summary>
    private void TryResolveGroundReferenceCollider()
    {
        if (_groundReferenceCollider == null)
        {
            _groundReferenceCollider = GetComponent<Collider2D>();
        }
    }

    /// <summary>
    /// Collider 크기를 기반으로 Ground Probe/Snap/Edge Stop 값을 자동 계산해 반영합니다.
    /// </summary>
    private void ApplyGroundSnapAutoTuningByCollider()
    {
        if (!_autoTuneGroundSnapByCollider || _groundReferenceCollider == null)
        {
            SyncResolvedGroundedParametersWithManualSource();
            return;
        }

        Vector2 colliderSize = _groundReferenceCollider.bounds.size; // 자동 보정 계산에 사용할 기준 Collider 크기입니다.
        float colliderHeight = Mathf.Max(0.01f, colliderSize.y); // Ground Probe/Snap 계산에 사용할 Collider 높이 값입니다.
        float colliderWidth = Mathf.Max(0.01f, colliderSize.x); // Edge Stop 계산에 사용할 Collider 너비 값입니다.

        _resolvedGroundProbeDistance = Mathf.Clamp(colliderHeight * Mathf.Max(0f, _probeDistanceByColliderHeight), Mathf.Min(_autoProbeDistanceMin, _autoProbeDistanceMax), Mathf.Max(_autoProbeDistanceMin, _autoProbeDistanceMax));
        _resolvedGroundSnapDistance = Mathf.Clamp(colliderHeight * Mathf.Max(0f, _snapDistanceByColliderHeight), Mathf.Min(_autoSnapDistanceMin, _autoSnapDistanceMax), Mathf.Max(_autoSnapDistanceMin, _autoSnapDistanceMax));
        _resolvedEdgeProbeForwardOffset = Mathf.Clamp(colliderWidth * Mathf.Max(0f, _edgeStopByColliderWidth), Mathf.Min(_autoEdgeStopMin, _autoEdgeStopMax), Mathf.Max(_autoEdgeStopMin, _autoEdgeStopMax));
        ApplyGroundProbeOffsetAutoTuningByCollider(colliderHeight);
    }

    /// <summary>
    /// 수동 입력 원본 Grounded 파라미터를 솔버 전달용 런타임 값으로 동기화합니다.
    /// </summary>
    private void SyncResolvedGroundedParametersWithManualSource()
    {
        _resolvedGroundProbeDistance = _groundProbeDistance;
        _resolvedGroundSnapDistance = _groundSnapDistance;
        _resolvedEdgeProbeForwardOffset = _edgeProbeForwardOffset;
    }

    /// <summary>
    /// 기준 Collider bounds를 바탕으로 Ground Probe Offset 값을 자동 계산해 반영합니다.
    /// </summary>
    private void ApplyGroundProbeOffsetAutoTuningByCollider(float colliderHeight)
    {
        if (!_autoTuneGroundProbeOffsetByCollider || _groundReferenceCollider == null)
        {
            return;
        }

        Vector2 rigidbodyPosition = _rigidbody2D != null ? _rigidbody2D.position : (Vector2)transform.position; // Ground Probe Offset 기준 좌표로 사용할 리지드바디/트랜스폼 위치 값입니다.
        Bounds colliderBounds = _groundReferenceCollider.bounds; // Ground Probe Offset 자동 계산에 사용할 기준 Collider bounds 값입니다.
        float probeLift = Mathf.Clamp(colliderHeight * Mathf.Max(0f, _probeOffsetLiftByColliderHeight), Mathf.Min(_autoProbeOffsetLiftMin, _autoProbeOffsetLiftMax), Mathf.Max(_autoProbeOffsetLiftMin, _autoProbeOffsetLiftMax)); // Collider 하단에서 Probe 시작점까지 유지할 여유 높이 값입니다.

        float probeOriginWorldX = colliderBounds.center.x; // Ground Probe 시작점의 월드 X 좌표 기준값입니다.
        float probeOriginWorldY = colliderBounds.min.y + probeLift; // Ground Probe 시작점의 월드 Y 좌표 기준값입니다.
        _groundProbeOffset = new Vector2(probeOriginWorldX - rigidbodyPosition.x, probeOriginWorldY - rigidbodyPosition.y);
    }

    /// <summary>
    /// 런타임에 기준 Collider 크기 변화를 감지해 필요할 때만 Ground Snap 자동 보정을 재적용합니다.
    /// </summary>
    private void UpdateAutoTuningIfColliderChanged()
    {
        if (!_autoTuneGroundSnapByCollider)
        {
            return;
        }

        TryResolveGroundReferenceCollider();
        if (!TryGetGroundReferenceColliderSize(out Vector2 currentColliderSize))
        {
            return;
        }

        float epsilon = Mathf.Max(0f, _autoTuneColliderSizeEpsilon); // 콜라이더 크기 변화 재계산 판정에 사용할 허용 오차 값입니다.
        float sizeDeltaSqrMagnitude = (currentColliderSize - _lastGroundReferenceColliderSize).sqrMagnitude; // 이전 프레임 캐시 대비 현재 크기 변화량 제곱값입니다.
        if (sizeDeltaSqrMagnitude <= epsilon * epsilon)
        {
            return;
        }

        ApplyGroundSnapAutoTuningByCollider();
        _lastGroundReferenceColliderSize = currentColliderSize;
    }

    /// <summary>
    /// 현재 기준 Collider 크기를 캐시 필드에 저장합니다.
    /// </summary>
    private void CacheCurrentGroundReferenceColliderSize()
    {
        if (!TryGetGroundReferenceColliderSize(out Vector2 currentColliderSize))
        {
            return;
        }

        _lastGroundReferenceColliderSize = currentColliderSize;
    }

    /// <summary>
    /// 자동 보정 기준 Collider의 현재 bounds 크기를 안전하게 반환합니다.
    /// </summary>
    private bool TryGetGroundReferenceColliderSize(out Vector2 colliderSize)
    {
        if (_groundReferenceCollider == null)
        {
            colliderSize = Vector2.zero;
            return false;
        }

        colliderSize = _groundReferenceCollider.bounds.size;
        return true;
    }

    /// <summary>
    /// 현재 기준 Collider bounds를 사용해 Grounded 스냅 기준이 되는 bounds.min.y 값을 계산합니다.
    /// </summary>
    private float ResolveGroundColliderBoundsMinY()
    {
        if (_groundReferenceCollider == null)
        {
            return _rigidbody2D != null ? _rigidbody2D.position.y : transform.position.y;
        }

        Bounds colliderBounds = _groundReferenceCollider.bounds; // Grounded 스냅 보정 기준으로 사용할 현재 Collider bounds 값입니다.
        return colliderBounds.min.y;
    }

    /// <summary>
    /// Grounded/Floating 솔버 공통 입력 데이터입니다.
    /// </summary>
    private readonly struct LocomotionSolveContext
    {
        public readonly Transform Transform; // 이동 계산에 사용할 Enemy 트랜스폼 참조입니다.
        public readonly Vector2 CurrentPosition; // 이동 계산 기준이 되는 현재 위치 좌표입니다.
        public readonly Vector2 TargetPosition; // 이동 대상 목적지 좌표입니다.
        public readonly float DesiredAltitude; // 부유 이동에서 유지해야 할 목표 고도 좌표입니다.
        public readonly float StoppingDistance; // 도달 판정에 사용할 허용 거리입니다.
        public readonly float MoveSpeed; // 이동 계산에 사용할 목표 속도 값입니다.
        public readonly float HoverHeight; // 부유 상태 기본 유지 고도 값입니다.
        public readonly float HoverDamping; // 수직 감쇠 계산에 사용할 계수입니다.
        public readonly float VerticalMaxSpeed; // 수직 속도 제한 값입니다.
        public readonly Vector2 GroundProbeOffset; // 하단 접지 프로브 시작 오프셋입니다.
        public readonly float GroundProbeDistance; // 하단 접지 프로브 거리입니다.
        public readonly LayerMask GroundLayerMask; // 접지/엣지 판정에 사용할 지면 LayerMask입니다.
        public readonly float EdgeProbeForwardOffset; // 엣지 프로브 전방 오프셋 거리입니다.
        public readonly float SlopeLimit; // 지상 이동 경사 제한 각도입니다.
        public readonly float GroundSnapDistance; // 바닥 스냅 보정 최대 거리입니다.
        public readonly bool EnableEdgeBypassCandidate; // 엣지 우회 후보 생성 사용 여부입니다.
        public readonly float DeltaTime; // Grounded 중력형 계산에 사용할 고정 델타타임 값입니다.
        public readonly bool UseGroundedGravity; // Grounded 이동에서 중력형 수직 속도 계산 사용 여부입니다.
        public readonly float GroundGravityAcceleration; // Grounded 중력형 계산에 사용할 중력 가속도 값입니다.
        public readonly float GroundMaxFallSpeed; // Grounded 중력형 계산에 사용할 최대 낙하 속도 절대값입니다.
        public readonly float GroundStickVelocity; // Grounded 접지 상태 유지 시 사용할 최소 하향 속도 값입니다.
        public readonly float CurrentGroundVerticalVelocity; // Grounded 중력형 계산 입력으로 전달할 현재 수직 속도 값입니다.
        public readonly float GroundColliderBoundsMinY; // Grounded 스냅 보정에 사용할 현재 콜라이더 bounds.min.y 값입니다.

        /// <summary>
        /// 솔버 공통 계산에 사용할 입력 컨텍스트를 생성합니다.
        /// </summary>
        public LocomotionSolveContext(
            Transform transform,
            Vector2 currentPosition,
            Vector2 targetPosition,
            float desiredAltitude,
            float stoppingDistance,
            float moveSpeed,
            float hoverHeight,
            float hoverDamping,
            float verticalMaxSpeed,
            Vector2 groundProbeOffset,
            float groundProbeDistance,
            LayerMask groundLayerMask,
            float edgeProbeForwardOffset,
            float slopeLimit,
            float groundSnapDistance,
            bool enableEdgeBypassCandidate,
            float deltaTime,
            bool useGroundedGravity,
            float groundGravityAcceleration,
            float groundMaxFallSpeed,
            float groundStickVelocity,
            float currentGroundVerticalVelocity,
            float groundColliderBoundsMinY)
        {
            Transform = transform;
            CurrentPosition = currentPosition;
            TargetPosition = targetPosition;
            DesiredAltitude = desiredAltitude;
            StoppingDistance = stoppingDistance;
            MoveSpeed = moveSpeed;
            HoverHeight = hoverHeight;
            HoverDamping = hoverDamping;
            VerticalMaxSpeed = verticalMaxSpeed;
            GroundProbeOffset = groundProbeOffset;
            GroundProbeDistance = groundProbeDistance;
            GroundLayerMask = groundLayerMask;
            EdgeProbeForwardOffset = edgeProbeForwardOffset;
            SlopeLimit = slopeLimit;
            GroundSnapDistance = groundSnapDistance;
            EnableEdgeBypassCandidate = enableEdgeBypassCandidate;
            DeltaTime = deltaTime;
            UseGroundedGravity = useGroundedGravity;
            GroundGravityAcceleration = groundGravityAcceleration;
            GroundMaxFallSpeed = groundMaxFallSpeed;
            GroundStickVelocity = groundStickVelocity;
            CurrentGroundVerticalVelocity = currentGroundVerticalVelocity;
            GroundColliderBoundsMinY = groundColliderBoundsMinY;
        }
    }

    /// <summary>
    /// Grounded/Floating 솔버 공통 출력 데이터입니다.
    /// </summary>
    private readonly struct LocomotionSolveResult
    {
        public readonly Vector2 Velocity; // 최종 적용할 이동 속도 벡터입니다.
        public readonly int FacingDirection; // Flip 갱신에 사용할 방향 값입니다.
        public readonly bool ReachedTarget; // 목적지 도달 여부입니다.
        public readonly bool HasSnapPosition; // 바닥 스냅 보정 위치 적용 여부입니다.
        public readonly Vector2 SnapPosition; // 바닥 스냅 보정에 사용할 위치 좌표입니다.
        public readonly float NextGroundVerticalVelocity; // 다음 프레임 Grounded 중력형 계산에 사용할 수직 속도 값입니다.

        /// <summary>
        /// 솔버 결과 데이터를 생성합니다.
        /// </summary>
        public LocomotionSolveResult(Vector2 velocity, int facingDirection, bool reachedTarget, bool hasSnapPosition, Vector2 snapPosition, float nextGroundVerticalVelocity = 0f)
        {
            Velocity = velocity;
            FacingDirection = facingDirection;
            ReachedTarget = reachedTarget;
            HasSnapPosition = hasSnapPosition;
            SnapPosition = snapPosition;
            NextGroundVerticalVelocity = nextGroundVerticalVelocity;
        }

        /// <summary>
        /// 즉시 정지 결과를 생성합니다.
        /// </summary>
        public static LocomotionSolveResult Stop(int facingDirection = 0, float nextGroundVerticalVelocity = 0f)
        {
            return new LocomotionSolveResult(Vector2.zero, facingDirection, true, false, Vector2.zero, nextGroundVerticalVelocity);
        }
    }

    /// <summary>
    /// 지상 이동 규칙(접지/지면 접선 속도/엣지 감지)을 계산하는 전략 솔버입니다.
    /// </summary>
    private struct GroundedLocomotionSolver
    {
        public bool HasBypassCandidate { get; private set; } // 이번 프레임 우회 후보 생성 여부입니다.
        public Vector2 BypassCandidate { get; private set; } // 이번 프레임 계산된 우회 후보 좌표입니다.

        /// <summary>
        /// 지상 규칙에 따라 이동 속도/방향/도달 여부를 계산합니다.
        /// </summary>
        public LocomotionSolveResult Solve(in LocomotionSolveContext context)
        {
            HasBypassCandidate = false;
            BypassCandidate = context.TargetPosition;

            bool isGrounded = ProbeGround(context, context.CurrentPosition, out Vector2 groundNormal, out Vector2 groundPoint); // 현재 위치 하단 접지 판정 결과입니다.
            float nextGroundVerticalVelocity = ResolveGroundedVerticalVelocity(context, isGrounded); // Grounded 중력형 이동에서 다음 프레임에 사용할 수직 속도 값입니다.
            Vector2 toTarget = context.TargetPosition - context.CurrentPosition; // 현재 위치에서 목적지로 향하는 벡터입니다.
            if (toTarget.magnitude <= context.StoppingDistance)
            {
                return LocomotionSolveResult.Stop(0, nextGroundVerticalVelocity);
            }

            Vector2 tangentDirection = ResolveGroundTangent(groundNormal, toTarget.x); // 지면 접선 기준 이동 방향 벡터입니다.

            int desiredFacing = toTarget.x >= 0f ? 1 : -1; // 목적지 기준 선호 좌우 방향 값입니다.
            if (!isGrounded)
            {
                Vector2 fallbackDirection = new Vector2(Mathf.Sign(toTarget.x), 0f); // 비접지 시 사용할 수평 이동 방향 벡터입니다.
                Vector2 fallbackVelocity = fallbackDirection * context.MoveSpeed;
                if (context.UseGroundedGravity)
                {
                    fallbackVelocity.y = nextGroundVerticalVelocity;
                }

                return new LocomotionSolveResult(fallbackVelocity, desiredFacing, false, false, Vector2.zero, nextGroundVerticalVelocity);
            }

            if (Vector2.Angle(groundNormal, Vector2.up) > context.SlopeLimit)
            {
                Vector2 slopeStopVelocity = context.UseGroundedGravity ? new Vector2(0f, nextGroundVerticalVelocity) : Vector2.zero; // 경사 제한 초과 시 유지할 속도 값입니다.
                return new LocomotionSolveResult(slopeStopVelocity, desiredFacing, false, false, Vector2.zero, nextGroundVerticalVelocity);
            }

            if (IsEdgeAhead(context, desiredFacing, out Vector2 bypassCandidate))
            {
                if (context.EnableEdgeBypassCandidate)
                {
                    HasBypassCandidate = true;
                    BypassCandidate = bypassCandidate;
                }

                Vector2 edgeStopVelocity = context.UseGroundedGravity ? new Vector2(0f, nextGroundVerticalVelocity) : Vector2.zero; // 엣지 정지 시 유지할 속도 값입니다.
                return new LocomotionSolveResult(edgeStopVelocity, desiredFacing, false, false, Vector2.zero, nextGroundVerticalVelocity);
            }

            Vector2 velocity = tangentDirection * context.MoveSpeed;
            if (context.UseGroundedGravity)
            {
                velocity = new Vector2(velocity.x, nextGroundVerticalVelocity);
            }

            float currentBottomY = context.GroundColliderBoundsMinY; // 현재 프레임 기준 Collider bounds 하단 월드 Y 좌표입니다.
            bool canSnapToGround = Mathf.Abs(currentBottomY - groundPoint.y) <= context.GroundSnapDistance; // 바닥 스냅 허용 거리 내 여부입니다.
            float snapDeltaY = groundPoint.y - currentBottomY; // 기준 Collider bounds 하단을 지면에 맞추기 위해 필요한 리지드바디 이동량입니다.
            Vector2 snapPosition = canSnapToGround ? new Vector2(context.CurrentPosition.x, context.CurrentPosition.y + snapDeltaY) : Vector2.zero; // 바닥 스냅 보정 시 적용할 위치입니다.
            if (context.UseGroundedGravity && canSnapToGround)
            {
                velocity.y = 0f;
                nextGroundVerticalVelocity = 0f;
            }

            return new LocomotionSolveResult(velocity, desiredFacing, false, canSnapToGround, snapPosition, nextGroundVerticalVelocity);
        }

        /// <summary>
        /// Grounded 이동에서 접지 여부와 중력 파라미터를 기준으로 다음 프레임 수직 속도를 계산합니다.
        /// </summary>
        private float ResolveGroundedVerticalVelocity(in LocomotionSolveContext context, bool isGrounded)
        {
            if (!context.UseGroundedGravity)
            {
                return 0f;
            }

            if (isGrounded)
            {
                return 0f;
            }

            float fallVelocity = context.CurrentGroundVerticalVelocity - context.GroundGravityAcceleration * Mathf.Max(0f, context.DeltaTime); // 공중 상태에서 중력을 누적 반영한 낙하 속도 값입니다.
            return Mathf.Max(-context.GroundMaxFallSpeed, fallVelocity);
        }

        /// <summary>
        /// 하단 프로브 레이캐스트로 현재 접지 여부와 법선을 반환합니다.
        /// </summary>
        private bool ProbeGround(in LocomotionSolveContext context, Vector2 probeBasePosition, out Vector2 normal, out Vector2 groundPoint)
        {
            Vector2 origin = probeBasePosition + context.GroundProbeOffset; // 하단 접지 판정 시작 좌표입니다.
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, context.GroundProbeDistance, context.GroundLayerMask); // 하단 접지 판정 레이캐스트 결과입니다.
            if (!hit.collider || hit.collider.transform == context.Transform)
            {
                normal = Vector2.up;
                groundPoint = probeBasePosition;
                return false;
            }

            normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector2.up;
            groundPoint = hit.point;
            return true;
        }

        /// <summary>
        /// 지면 법선과 이동 방향 부호를 이용해 지면 접선 이동 벡터를 계산합니다.
        /// </summary>
        private Vector2 ResolveGroundTangent(Vector2 normal, float targetDeltaX)
        {
            Vector2 tangent = new Vector2(normal.y, -normal.x); // 법선에 수직한 지면 접선 벡터입니다.
            if (Mathf.Abs(targetDeltaX) < 0.001f)
            {
                return tangent.normalized;
            }

            if (Mathf.Sign(tangent.x) != Mathf.Sign(targetDeltaX))
            {
                tangent = -tangent;
            }

            return tangent.normalized;
        }

        /// <summary>
        /// 전방 하단 프로브로 엣지를 감지하고 우회 후보를 계산합니다.
        /// </summary>
        private bool IsEdgeAhead(in LocomotionSolveContext context, int desiredFacing, out Vector2 bypassCandidate)
        {
            float facing = desiredFacing == 0 ? 1f : Mathf.Sign(desiredFacing); // 엣지 판정에 사용할 전방 방향 값입니다.
            Vector2 edgeOrigin = context.CurrentPosition + new Vector2(context.EdgeProbeForwardOffset * facing, context.GroundProbeOffset.y); // 엣지 감지 프로브 시작 좌표입니다.
            RaycastHit2D edgeHit = Physics2D.Raycast(edgeOrigin, Vector2.down, context.GroundProbeDistance + 0.1f, context.GroundLayerMask); // 엣지 하단 접지 확인 결과입니다.

            bypassCandidate = context.CurrentPosition + new Vector2(context.EdgeProbeForwardOffset * facing, 0.5f); // 엣지 감지 시 생성하는 우회 후보 좌표입니다.
            return !edgeHit.collider || edgeHit.collider.transform == context.Transform;
        }
    }

    /// <summary>
    /// 부유 이동 규칙(호버 고도 유지/수직 감쇠/수직 속도 제한)을 계산하는 전략 솔버입니다.
    /// </summary>
    private struct FloatingLocomotionSolver
    {
        /// <summary>
        /// 부유 규칙에 따라 이동 속도/방향/도달 여부를 계산합니다.
        /// </summary>
        public LocomotionSolveResult Solve(in LocomotionSolveContext context)
        {
            float desiredAltitude = context.DesiredAltitude;
            Vector2 target = new Vector2(context.TargetPosition.x, desiredAltitude); // 부유 규칙에서 사용할 최종 목표 좌표입니다.
            Vector2 toTarget = target - context.CurrentPosition; // 현재 위치 기준 최종 목표 오차 벡터입니다.

            if (toTarget.magnitude <= context.StoppingDistance)
            {
                return LocomotionSolveResult.Stop();
            }

            float horizontalDirection = Mathf.Abs(toTarget.x) < 0.001f ? 0f : Mathf.Sign(toTarget.x); // 수평 추적에 사용할 방향 부호 값입니다.
            float horizontalSpeed = horizontalDirection * context.MoveSpeed; // 수평 추적 속도 값입니다.

            float verticalError = desiredAltitude - context.CurrentPosition.y; // 목표 고도 대비 수직 오차 값입니다.
            float verticalSpeed = Mathf.Clamp(verticalError * context.HoverDamping, -context.VerticalMaxSpeed, context.VerticalMaxSpeed); // 감쇠/속도 제한이 적용된 수직 속도 값입니다.

            Vector2 velocity = new Vector2(horizontalSpeed, verticalSpeed);
            int facingDirection = horizontalDirection == 0f ? 0 : (horizontalDirection > 0f ? 1 : -1);
            return new LocomotionSolveResult(velocity, facingDirection, false, false, Vector2.zero);
        }
    }
}
