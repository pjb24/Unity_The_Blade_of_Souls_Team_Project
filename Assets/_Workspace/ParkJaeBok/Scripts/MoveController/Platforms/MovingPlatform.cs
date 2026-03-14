using UnityEngine;

[DefaultExecutionOrder(-100), SelectionBase]
public class MovingPlatform : MonoBehaviour, IVelocityInheritable
{
    [Header("Configuration")]
    // 시작 위치에서 끝 위치까지 이동할 로컬 오프셋이다.
    [SerializeField] private Vector3 _moveOffset = new Vector3(0, 3, 0);
    // 시작점과 끝점을 왕복(또는 단방향) 이동하는 데 걸리는 시간이다.
    [SerializeField] private float _duration = 2f;
    // 이동 보간 값에 적용할 가속/감속 곡선이다.
    [SerializeField] private AnimationCurve _easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    // true면 왕복 반복, false면 끝점에서 멈춘다.
    [SerializeField] private bool _loop = true;
    // 이동 예측 시 벽 충돌 체크를 수행할지 여부이다.
    [SerializeField] private bool _checkForWalls = false;

    [Header("Collision Logic")]
    // 플랫폼 이동 중 탑승 대상을 물리적으로 밀어낼지 여부이다.
    [SerializeField] private bool _pushPassenger = true;
    // 밀어낼 대상(승객)으로 판정할 레이어 마스크이다.
    [SerializeField] private LayerMask _passengerMask;
    // 박스캐스트 거리 계산 시 추가로 더할 여유 패딩 값이다.
    [SerializeField] private float _pushCollisionPadding = 0.015f;

    // 플랫폼 이동 시작 월드 위치를 저장한다.
    private Vector3 _startPosition;
    // 플랫폼 이동 종료 월드 위치를 저장한다.
    private Vector3 _endPosition;
    // 고정 업데이트 기준 누적 시간을 저장한다.
    private float _timer;
    // 플랫폼 이동을 담당하는 키네마틱 리지드바디 참조이다.
    private Rigidbody2D _rb;
    // 승객 충돌 판정을 위한 플랫폼 박스 콜라이더 참조이다.
    private BoxCollider2D _collider;

    // 물리 위치와 비주얼 위치 보간 동기화를 담당하는 컴포넌트 참조이다.
    private VisualInterpolator _visuals;

    // 충돌 프로브를 이동 방향 선행 배치할지 여부를 저장한다.
    public bool ProbesShouldLead { get; set; } = true;
    // 플랫폼 이탈 시 운동량 전달 여부를 저장한다.
    public bool ImpartMomentumOnExit { get; set; } = true;

    // 이 플랫폼은 수직 발사 보정을 사용하지 않음을 반환한다.
    public bool LaunchVerticallyOnExit { get; } = false;

    // 직전 FixedUpdate 대비 플랫폼 위치 변화량을 외부에 제공한다.
    public Vector3 PositionDelta { get; private set; }

    // 미래 위치 박스캐스트 벽 체크 필요 여부를 반환한다.
    public bool NeedsFuturePositionBoxcastCheck => _checkForWalls;

    // 현재 프레임의 위치 변화량을 시간으로 나눈 플랫폼 속도를 반환한다.
    public Vector2 GetVelocity() => PositionDelta / Time.fixedDeltaTime;

    // 필수 컴포넌트를 캐싱하고 이동 시작/종료 위치를 초기화한다.
    private void Awake()
    {
        _visuals = GetComponentInChildren<VisualInterpolator>();
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _collider = GetComponent<BoxCollider2D>();

        _startPosition = _rb.position;
        _endPosition = _startPosition + _moveOffset;
    }

    // 고정 틱마다 플랫폼 목표 위치를 계산하고 필요 시 승객을 밀어낸 뒤 위치를 갱신한다.
    private void FixedUpdate()
    {
        Vector2 oldPos = _rb.position;

        _timer += Time.fixedDeltaTime;
        float t = _loop ? Mathf.PingPong(_timer / _duration, 1f) : Mathf.Clamp01(_timer / _duration);
        float easedT = _easeCurve.Evaluate(t);
        Vector2 targetPos = Vector2.Lerp(_startPosition, _endPosition, easedT);

        Vector2 moveVector = targetPos - oldPos;

        if (moveVector != Vector2.zero && _pushPassenger)
        {
            PushPassengers(moveVector);
        }

        _rb.position = targetPos;
        PositionDelta = _rb.position - oldPos;

        if (_visuals != null)
        {
            _visuals.UpdatePhysicsState();
        }
    }

    // 이동 경로 앞쪽으로 박스캐스트를 수행해 충돌한 승객에게 외부 밀림 힘을 적용한다.
    private void PushPassengers(Vector2 moveAmount)
    {
        Bounds bounds = _collider.bounds;
        Vector2 center = bounds.center;
        Vector2 size = bounds.size;

        float distance = moveAmount.magnitude + _pushCollisionPadding;
        Vector2 direction = moveAmount.normalized;

        RaycastHit2D hit = Physics2D.BoxCast(center, size, 0f, direction, distance, _passengerMask);

        if (hit)
        {
            if (hit.transform == transform) return;

            float dot = Vector2.Dot(direction, hit.normal);

            if (dot >= -0.01f)
            {
                return;
            }

            MovementController controller = hit.transform.GetComponent<MovementController>();
            if (controller != null)
            {
                controller.ApplyExternalPush(moveAmount, transform);
            }
        }
    }

    // 씬 뷰에서 플랫폼의 시작점과 이동 목표점을 선으로 시각화한다.
    private void OnDrawGizmos()
    {
        Vector3 start = Application.isPlaying ? _startPosition : transform.position;
        Vector3 end = start + _moveOffset;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, end);
    }
}
