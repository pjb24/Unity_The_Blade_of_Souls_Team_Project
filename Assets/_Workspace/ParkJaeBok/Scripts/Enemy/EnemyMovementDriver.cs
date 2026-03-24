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

    [Header("Runtime")]
    [Tooltip("현재 프레임에서 유효한 목적지 보유 여부입니다.")]
    [SerializeField] private bool _hasTargetPosition; // 현재 프레임에서 유효한 목적지 보유 여부입니다.
    [Tooltip("추적/복귀에 사용할 현재 목적지 월드 좌표입니다.")]
    [SerializeField] private Vector2 _targetPosition; // 추적/복귀에 사용할 현재 목적지 월드 좌표입니다.

    private Rigidbody2D _rigidbody2D; // 물리 기반 이동 적용에 사용할 리지드바디 참조입니다.
    private Vector2 _currentVelocity; // 현재 프레임 계산된 이동 속도 벡터입니다.

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
    /// 리지드바디 참조를 캐싱합니다.
    /// </summary>
    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 목적지 기준 이동 속도를 계산해 리지드바디에 반영합니다.
    /// </summary>
    private void FixedUpdate()
    {
        if (_hasTargetPosition == false)
        {
            _currentVelocity = Vector2.zero;
            _rigidbody2D.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 currentPosition = _rigidbody2D.position; // 이동 계산 기준이 되는 현재 위치입니다.
        Vector2 toTarget = _targetPosition - currentPosition; // 현재 위치에서 목적지로 향하는 방향 벡터입니다.

        if (toTarget.magnitude <= Mathf.Max(0.01f, _stoppingDistance))
        {
            _currentVelocity = Vector2.zero;
            _rigidbody2D.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 desiredDirection = toTarget.normalized; // 이동에 사용할 정규화 방향 벡터입니다.
        _currentVelocity = desiredDirection * Mathf.Max(0f, _moveSpeed);
        _rigidbody2D.linearVelocity = _currentVelocity;

        UpdateFacingByVelocity(_currentVelocity.x);
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
    }

    /// <summary>
    /// 지정 좌표를 목적지로 설정해 이동을 시작합니다.
    /// </summary>
    public void SetTargetPosition(Vector2 targetPosition)
    {
        _targetPosition = targetPosition;
        _hasTargetPosition = true;
    }

    /// <summary>
    /// 목적지를 제거하고 즉시 이동을 정지합니다.
    /// </summary>
    public void ClearTargetPosition()
    {
        _hasTargetPosition = false;
        _currentVelocity = Vector2.zero;

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// 이동 입력값을 기반으로 좌우 바라보기 방향을 갱신합니다.
    /// </summary>
    private void UpdateFacingByVelocity(float velocityX)
    {
        if (Mathf.Abs(velocityX) < 0.001f)
        {
            return;
        }

        Vector3 localScale = transform.localScale; // 방향 전환에 사용할 현재 로컬 스케일입니다.
        localScale.x = Mathf.Abs(localScale.x) * Mathf.Sign(velocityX);
        transform.localScale = localScale;
    }
}
