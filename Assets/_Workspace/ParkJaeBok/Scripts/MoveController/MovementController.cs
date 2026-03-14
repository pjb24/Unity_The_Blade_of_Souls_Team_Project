using UnityEngine;
using UnityEditor;
using System;

public struct CollisionState
{
    public bool IsGrounded; // 현재 프레임에 바닥을 밟고 있는지 여부
    public bool WasGroundedLastFrame; // 이전 프레임에 바닥을 밟고 있었는지 여부

    public bool IsAgainstWall; // 현재 벽에 접촉 중인지 여부
    public bool WasAgainstWallLastFrame; // 이전 프레임에 벽에 접촉했는지 여부
    public int WallDirection; // 접촉한 벽의 방향(-1: 왼쪽, 1: 오른쪽)
    public float WallAngle; // 벽의 기울기 각도

    public bool IsOnSlope; // 경사면 위에 있는지 여부
    public float SlopeAngle; // 경사면 각도
    public Vector2 SlopeNormal; // 경사면의 법선 벡터

    public bool IsAgainstSteepSlope; // 너무 가파른 경사면에 닿았는지 여부

    public bool IsHittingCeiling; // 천장과 충돌했는지 여부
    public float CeilingAngle; // 천장의 기울기 각도
    public Vector2 CeilingNormal; // 천장 충돌 지점의 법선 벡터

    public Vector2 AveragedVisualNormal; // 시각 연출용 평균 지면 법선

    /// <summary>충돌 상태를 다음 프레임 계산을 위해 초기화하고 이전 프레임 값을 보존합니다.</summary>
    public void Reset()
    {
        WasGroundedLastFrame = IsGrounded;
        WasAgainstWallLastFrame = IsAgainstWall;

        IsGrounded = false;
        IsAgainstWall = false;
        WallDirection = 0;
        WallAngle = 0f;

        IsOnSlope = false;
        SlopeAngle = 0f;
        SlopeNormal = Vector2.zero;

        IsAgainstSteepSlope = false;

        IsHittingCeiling = false;
        CeilingAngle = 0f;
        CeilingNormal = Vector2.zero;

        AveragedVisualNormal = Vector2.up;
    }
}

[RequireComponent(typeof(PlayerMovement))]
public class MovementController : MonoBehaviour
{
    // 현재 미끄러지는 경사 상태인지 여부
    public bool IsSliding => _internalState.IsOnSlope && _internalState.SlopeAngle > _moveStats.MaxSlopeAngle;

    public const float CollisionPadding = 0.015f; // 레이캐스트/충돌 계산 시 사용하는 여유 오차
    private const float AIRBORNE_ANGLE_MEMORY = -999f; // 공중 상태를 표시하기 위한 특수 각도값

    [Range(2, 100)] public int NumOfHorizontalRays = 4; // 수평 충돌 감지에 사용할 레이 개수
    [Range(2, 100)] public int NumOfVerticalRays = 4; // 수직 충돌 감지에 사용할 레이 개수
    public int NumOfVerticalRaysForVisualNormals = 9; // 시각용 평균 법선 계산에 사용할 수직 레이 개수

    [Header("Sensors")]
    [SerializeField] private float _verticalProbeDistance = 0.1f; // 상하 방향 프로브 기본 거리
    [SerializeField] private float _horizontalProbeDistance = 0.1f; // 좌우 방향 프로브 기본 거리

    [Header("Safety")]
    [SerializeField] private float _safetyGraceDuration = 0.08f; // 지면 재검출 보정에 사용하는 유예 시간

    private float _horizontalRaySpace; // 수평 레이 간격
    private float _verticalRaySpace; // 수직 레이 간격

    private BoxCollider2D _coll; // 플레이어 충돌체 참조
    public RaycastCorners RayCastCorners; // 현재 콜라이더 모서리 좌표
    private PlayerMovementStats _moveStats; // 이동/충돌 관련 설정값

    public Action OnCrush; // 외부 힘으로 압사될 때 호출되는 이벤트

    public bool IsClimbingSlope { get; private set; } // 오르막 경사 처리 중인지 여부
    public bool WasClimbingSlopeLastFrame { get; private set; } // 이전 프레임 오르막 처리 여부
    public bool IsDescendingSlope { get; private set; } // 내리막 경사 처리 중인지 여부
    public float SlopeAngle { get; private set; } // 현재 경사 각도 캐시
    public Vector2 SlopeNormal { get; private set; } // 현재 경사 법선 캐시
    public float LastLandingTime { get; set; } // 마지막 착지 시각
    public int FaceDirection { get; private set; } // 바라보는 방향(-1/1)

    private bool _isCornerCorrectingThisFrame; // 헤드 코너 보정 적용 여부
    private bool _isHorizontalCornerCorrectingThisFrame; // 수평 코너 보정 적용 여부

    private float _rearCornerSlopeAngle; // 후면 코너 기준 경사 기억값
    private float _slopeCurveAccumulator; // 급격한 경사 변화 누적값

    private bool _forceAirborneNextFrame; // 다음 프레임에 강제로 비지면 상태 처리 여부

    private PlayerMovement _playerMovement; // 상위 이동 로직 참조
    private Rigidbody2D _rb; // 물리 위치 이동에 사용하는 리지드바디 참조

    public CollisionState State { get; private set; } // 외부에 공개되는 충돌 상태 스냅샷
    private CollisionState _internalState; // 내부 계산용 충돌 상태

    private float _lastSafetyGroundFixedTime = -Mathf.Infinity; // 마지막 안전 지면 판정 시각
    private RaycastHit2D _lastSafetyGroundHit; // 마지막 안전 지면 히트 정보

    private bool _wasPushedThisFrame; // 이번 프레임 외부 밀림 적용 여부
    private Vector2 _pushAmountThisFrame; // 이번 프레임 외부 밀림 벡터

    private Collider2D[] _overlapBuffer = new Collider2D[1]; // Overlap 검사 재사용 버퍼

    public IVelocityInheritable LastKnownPlatform; // 현재/최근에 발을 딛은 플랫폼 참조
    public IVelocityInheritable PlatformFromLastFrame { get; private set; } // 이전 프레임 플랫폼 참조
    public bool IsOnPlatform => LastKnownPlatform != null; // 플랫폼 위에 있는지 여부

    public struct RaycastCorners
    {
        public Vector2 topLeft;
        public Vector2 topRight;
        public Vector2 bottomLeft;
        public Vector2 bottomRight;
    }

    /// <summary>필수 컴포넌트 참조를 캐싱하고 초기 방향을 설정합니다.</summary>
    private void Awake()
    {
        _coll = GetComponent<BoxCollider2D>();
        _rb = GetComponent<Rigidbody2D>();
        _playerMovement = GetComponent<PlayerMovement>();
        _moveStats = _playerMovement.MoveStats;

        FaceDirection = 1;
    }

    private void Start()
    {
        CalculateRaySpacing();
    }

    /// <summary>레이캐스트 센서를 갱신해 지면/벽/천장 충돌 상태를 수집합니다.</summary>
    public void PollSensors(Vector2 moveDelta)
    {
        _internalState.Reset();

        IVelocityInheritable predictedPlatform = LastKnownPlatform;
        PlatformFromLastFrame = LastKnownPlatform;
        LastKnownPlatform = null;

        UpdateRaycastCorners();

        if (moveDelta.x != 0)
        {
            FaceDirection = (int)Mathf.Sign(moveDelta.x);
        }

        IVelocityInheritable foundWallPlatform;
        HorizontalProbes(moveDelta, predictedPlatform, out foundWallPlatform);

        CeilingProbes(moveDelta);

        IVelocityInheritable foundGroundPlatform;
        if (_forceAirborneNextFrame)
        {
            _internalState.IsGrounded = false;
            _forceAirborneNextFrame = false;
            foundGroundPlatform = null;
        }
        else
        {
            GroundProbes(moveDelta, predictedPlatform, out foundGroundPlatform);
        }

        if (_internalState.IsGrounded)
        {
            LastKnownPlatform = foundGroundPlatform;
        }
        else if (_internalState.IsAgainstWall)
        {
            LastKnownPlatform = foundWallPlatform;
        }
        else
        {
            LastKnownPlatform = null;
        }

        if (_internalState.IsHittingCeiling && DetectHeadCornerCorrection(moveDelta))
        {
            _internalState.IsHittingCeiling = false;
        }

        if (_moveStats.MatchVisualsToSlope)
        {
            DetectSlopeNormalsForVisuals(moveDelta);
        }

        State = _internalState;
    }

    /// <summary>플랫폼 이동과 충돌 보정을 반영해 최종 위치를 이동시킵니다.</summary>
    public void Move(Vector2 velocity)
    {
        Vector2 platformMoveAmount = Vector2.zero;

        if (IsOnPlatform)
        {
            Vector2 platformVel = LastKnownPlatform.GetVelocity();
            platformMoveAmount = platformVel * Time.fixedDeltaTime;

            //wall guard
            if (platformMoveAmount.x != 0 && LastKnownPlatform.NeedsFuturePositionBoxcastCheck)
            {
                float directionX = Mathf.Sign(platformMoveAmount.x);
                float distance = Mathf.Abs(platformMoveAmount.x) + CollisionPadding;
                Vector2 origin = _coll.bounds.center;
                Vector2 size = _coll.bounds.size - (Vector3.one * CollisionPadding * 2f);

                RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector3.right * directionX, distance, _moveStats.GroundLayer);

                if (hit)
                {
                    float adjustedDist = Mathf.Max(0, hit.distance - CollisionPadding);
                    platformVel.x = (adjustedDist * directionX) / Time.fixedDeltaTime;
                }
            }

            if (_internalState.IsAgainstWall && _internalState.IsGrounded)
            {
                float wallNormalX = -_internalState.WallDirection;

                if (Vector2.Dot(platformVel, new Vector2(wallNormalX, 0)) < -0.01f)
                {
                    platformVel.x = 0f;
                }
            }

            if (_internalState.IsGrounded)
            {
                if (Vector2.Dot(platformVel, _internalState.SlopeNormal) > 0.01f)
                {
                    platformVel.y = 0f;
                }
            }

            if (_wasPushedThisFrame)
            {
                if (Mathf.Abs(_pushAmountThisFrame.x) > 0f)
                {
                    platformVel.x = 0f;
                }
                if (Mathf.Abs(_pushAmountThisFrame.y) > 0f)
                {
                    platformVel.y = 0f;
                }
            }

            platformMoveAmount = platformVel * Time.fixedDeltaTime;
            _rb.position += platformMoveAmount;
        }

        UpdateRaycastCorners();
        ResetCollisionStates();

        if (velocity.y <= 0f && !_playerMovement.IsDashing && !WasClimbingSlopeLastFrame)
        {
            DescendSlope(ref velocity);
        }

        ApplyHorizontalCornerCorrection(ref velocity);
        ApplyHeadCornerCorrection(ref velocity);

        ResolveHorizontalMovement(ref velocity);
        ResolveVerticalMovement(ref velocity);

        _rb.MovePosition(_rb.position + velocity);

        _wasPushedThisFrame = false;
        _pushAmountThisFrame = Vector2.zero;
    }

    /// <summary>하향 프로브로 지면/경사/벽 슬라이드 가능 여부를 판정합니다.</summary>
    private void GroundProbes(Vector2 moveDelta, IVelocityInheritable lastKnownPlatform, out IVelocityInheritable foundPlatform)
    {
        foundPlatform = null;

        float rayLength = _verticalProbeDistance + CollisionPadding;

        if (lastKnownPlatform != null && lastKnownPlatform.ProbesShouldLead)
        {
            float platformMoveDist = Mathf.Abs(lastKnownPlatform.GetVelocity().y * Time.fixedDeltaTime);
            rayLength += platformMoveDist;
        }

        float smallestHitDistance = float.MaxValue;
        bool foundGround = false;
        bool foundWalkableGround = false;
        RaycastHit2D groundHit = new RaycastHit2D();

        float horizontalProjection = 0f;

        Vector2 platformOffset = Vector2.zero;
        if (lastKnownPlatform != null && lastKnownPlatform.ProbesShouldLead)
        {
            platformOffset = lastKnownPlatform.GetVelocity() * Time.fixedDeltaTime;
        }

        if (moveDelta.y <= 0)
        {
            horizontalProjection = moveDelta.x;
            if (_internalState.IsAgainstWall || _internalState.IsAgainstSteepSlope)
            {
                horizontalProjection = 0f;
            }
        }

        for (int i = 0; i < NumOfVerticalRays; i++)
        {
            Vector2 rayOrigin = RayCastCorners.bottomLeft + Vector2.right * (_verticalRaySpace * i + horizontalProjection);
            rayOrigin += platformOffset;

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayLength, _moveStats.GroundLayer);

            #region Debug Visualization
            if (_moveStats.DebugShowIsGrounded)
            {
                bool didHit = hit;
                Color rayColor = didHit ? Color.cyan : Color.red;
                Debug.DrawRay(rayOrigin, Vector2.down * rayLength, rayColor);
            }
            #endregion

            if (hit)
            {
                float hitAngle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.up));
                bool isHitWalkable = hitAngle < _moveStats.MaxSlopeAngle;

                if (!foundGround)
                {
                    smallestHitDistance = hit.distance;
                    groundHit = hit;
                    foundGround = true;
                    foundWalkableGround = isHitWalkable;
                }
                else
                {
                    if (!foundWalkableGround && isHitWalkable)
                    {
                        smallestHitDistance = hit.distance;
                        groundHit = hit;
                        foundWalkableGround = true;
                    }
                    else if (foundWalkableGround == isHitWalkable && hit.distance < smallestHitDistance)
                    {
                        smallestHitDistance = hit.distance;
                        groundHit = hit;
                    }
                }
            }
            else if (IsClimbingSlope || IsDescendingSlope && !IsOnPlatform)
            {
                _internalState.IsGrounded = true;
                _internalState.SlopeAngle = SlopeAngle;
                _internalState.SlopeNormal = SlopeNormal;
                if (_internalState.SlopeAngle > 0.01f)
                {
                    _internalState.IsOnSlope = true;
                }

                continue;
            }
        }

        if (foundGround)
        {
            var platform = groundHit.collider.GetComponent<IVelocityInheritable>();
            if (platform != null)
            {
                foundPlatform = platform;
            }

            float slopeAngle = Mathf.Round(Vector2.Angle(groundHit.normal, Vector2.up));

            bool isVerticalWall = Mathf.Abs(groundHit.normal.y) < Mathf.Epsilon;

            bool isWallSlideable = _playerMovement.IsWallSlideable(slopeAngle) && !isVerticalWall;
            bool isFacingWall = Mathf.Sign(groundHit.normal.x) != FaceDirection;

            bool shouldWallSlide = isFacingWall || _moveStats.CanWallSlideFacingAwayFromWall;

            if (isWallSlideable && shouldWallSlide)
            {
                _internalState.IsAgainstWall = true;
                _internalState.WallAngle = slopeAngle;
                _internalState.WallDirection = (int)-Mathf.Sign(groundHit.normal.x);
            }
            else
            {
                _internalState.IsGrounded = true;
                _internalState.SlopeAngle = slopeAngle;
                _internalState.SlopeNormal = groundHit.normal;
                if (slopeAngle > 0.01f)
                {
                    _internalState.IsOnSlope = true;
                }
            }
        }
        else
        {
            if (WasClimbingSlopeLastFrame)
            {
                float smallestSafetyDistance = float.MaxValue;
                bool foundSafetyGround = false;
                RaycastHit2D safetyGroundHit = new RaycastHit2D();

                for (int i = 0; i < NumOfVerticalRays; i++)
                {
                    Vector2 rayOriginSafety = RayCastCorners.bottomLeft + Vector2.right * (_verticalRaySpace * i);
                    RaycastHit2D hitSafety = Physics2D.Raycast(rayOriginSafety, Vector2.down, rayLength * 5f, _moveStats.GroundLayer);
                    if (hitSafety)
                    {
                        safetyGroundHit = hitSafety;
                        smallestSafetyDistance = safetyGroundHit.distance;
                        foundSafetyGround = true;
                        break;
                    }
                }

                if (foundSafetyGround)
                {
                    var platform = safetyGroundHit.collider.GetComponent<IVelocityInheritable>();
                    if (platform != null)
                    {
                        foundPlatform = platform;
                    }

                    float slopeAngle = Mathf.Round(Vector2.Angle(safetyGroundHit.normal, Vector2.up));
                    if (!_playerMovement.IsWallSlideable(slopeAngle))
                    {
                        _lastSafetyGroundFixedTime = Time.fixedTime;
                        _lastSafetyGroundHit = safetyGroundHit;
                        _internalState.IsGrounded = true;
                        _internalState.SlopeAngle = slopeAngle;
                        _internalState.SlopeNormal = safetyGroundHit.normal;
                        if (slopeAngle > 0.01f)
                        {
                            _internalState.IsOnSlope = true;
                        }
                    }
                }
            }

            //bool usedRecentSafety = false;
            if (Time.fixedTime - _lastSafetyGroundFixedTime <= _safetyGraceDuration)
            {
                float reuseSlopeAngle = Mathf.Round(Vector2.Angle(_lastSafetyGroundHit.normal, Vector2.up));
                if (!_playerMovement.IsWallSlideable(reuseSlopeAngle))
                {
                    //usedRecentSafety = true;
                    _internalState.IsGrounded = true;
                    _internalState.SlopeAngle = reuseSlopeAngle;
                    _internalState.SlopeNormal = _lastSafetyGroundHit.normal;
                    if (reuseSlopeAngle > 0.01f)
                    {
                        _internalState.IsOnSlope = true;
                    }
                }
            }

            //if (!foundGround && !usedRecentSafety)
            //{
            //    _internalState.IsGrounded = false;
            //    _internalState.IsOnSlope = false;
            //}
        }

        #region Debug Slope Normal

        if (_moveStats.DebugShowSlopeNormal)
        {
            Vector2 drawOrigin = new Vector2(_coll.bounds.center.x, _coll.bounds.min.y);
            float drawLength = _moveStats.ExtraRayDebugDistance * 3f;

            Debug.DrawRay(drawOrigin, _internalState.SlopeNormal * drawLength, Color.yellow);
        }

        #endregion
    }

    /// <summary>상향 프로브로 천장 충돌과 천장 각도를 판정합니다.</summary>
    private void CeilingProbes(Vector2 moveDelta)
    {
        if (moveDelta.y >= 0)
        {
            float rayLength = _verticalProbeDistance + CollisionPadding;

            for (int i = 0; i < NumOfVerticalRays; i++)
            {
                Vector2 rayOrigin = RayCastCorners.topLeft + Vector2.right * (_verticalRaySpace * i);
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up, rayLength, _moveStats.GroundLayer);

                if (hit)
                {
                    float rawHitAngle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.down));

                    if (rawHitAngle > 85f)
                    {
                        continue;
                    }

                    bool isEdgeRay = (i == 0 || i == NumOfVerticalRays - 1);
                    if (isEdgeRay && hit.distance == 0 && rawHitAngle == 0)
                    {
                        continue;
                    }

                    float currentCeilingAngle;
                    if (hit.distance == 0f)
                    {
                        Vector2 safetyRayOrigin = rayOrigin + (Vector2.down * CollisionPadding * 2);
                        RaycastHit2D safetyHit = Physics2D.Raycast(safetyRayOrigin, Vector2.up, CollisionPadding * 3, _moveStats.GroundLayer);

                        if (safetyHit)
                        {
                            currentCeilingAngle = Mathf.Round(Vector2.Angle(safetyHit.normal, Vector2.down));
                            _internalState.CeilingNormal = safetyHit.normal;
                        }
                        else
                        {
                            currentCeilingAngle = 0f;
                            _internalState.CeilingNormal = Vector2.down;
                        }
                    }
                    else
                    {
                        currentCeilingAngle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.down));
                        _internalState.CeilingNormal = hit.normal;
                    }

                    _internalState.IsHittingCeiling = true;

                    if (currentCeilingAngle > _internalState.CeilingAngle)
                    {
                        _internalState.CeilingAngle = currentCeilingAngle;
                        _internalState.CeilingNormal = hit.normal;
                    }
                }

                #region Debug Visualization

                if (_moveStats.DebugShowHeadRays)
                {
                    float drawLength = hit ? hit.distance : (rayLength + _moveStats.ExtraRayDebugDistance);

                    Color rayColor = hit ? Color.cyan : Color.red;

                    if (i == 0 || i == NumOfVerticalRays - 1)
                    {
                        rayColor = hit ? Color.green : Color.magenta;
                    }

                    Debug.DrawRay(rayOrigin, Vector2.up * drawLength, rayColor);
                }

                #endregion
            }
        }
    }

    /// <summary>좌우 프로브로 벽 충돌과 가파른 경사 접촉을 판정합니다.</summary>
    private void HorizontalProbes(Vector2 moveDelta, IVelocityInheritable lastKnownPlatform, out IVelocityInheritable foundPlatform)
    {
        foundPlatform = null;

        float rayLength = Mathf.Abs(moveDelta.x) + CollisionPadding;
        if (rayLength < _horizontalProbeDistance)
        {
            rayLength = _horizontalProbeDistance;
        }

        if (lastKnownPlatform != null && lastKnownPlatform.ProbesShouldLead)
        {
            float platformMoveDist = Mathf.Abs(lastKnownPlatform.GetVelocity().x * Time.fixedDeltaTime);
            rayLength += platformMoveDist;
        }

        Vector2 platformOffset = Vector2.zero;
        if (lastKnownPlatform != null && lastKnownPlatform.ProbesShouldLead)
        {
            platformOffset = lastKnownPlatform.GetVelocity() * Time.fixedDeltaTime;
        }

        for (int i = 0; i < NumOfHorizontalRays; i++)
        {
            //check left
            Vector2 rayOriginLeft = RayCastCorners.bottomLeft + Vector2.up * (_horizontalRaySpace * i);
            if (Vector2.Dot(platformOffset, Vector2.left) > 0)
            {
                rayOriginLeft += platformOffset;
            }

            RaycastHit2D hitLeft = Physics2D.Raycast(rayOriginLeft, Vector2.left, rayLength, _moveStats.GroundLayer);

            #region Debug Visualization

            if (_moveStats.DebugShowWallHit)
            {
                Debug.DrawRay(rayOriginLeft, Vector2.left * rayLength, hitLeft ? Color.cyan : Color.red);
            }

            #endregion

            bool foundWall = false;
            if (hitLeft)
            {
                float wallAngle = Mathf.Round(Vector2.Angle(hitLeft.normal, Vector2.up));

                if (_playerMovement.IsSlideableSlope(wallAngle))
                {
                    _internalState.IsAgainstSteepSlope = true;
                }

                if (_playerMovement.IsWallSlideable(wallAngle))
                {
                    _internalState.IsAgainstWall = true;
                    _internalState.WallDirection = -1;
                    _internalState.WallAngle = wallAngle;
                    foundWall = true;
                }

                var platform = hitLeft.collider.GetComponent<IVelocityInheritable>();
                if (platform != null)
                {
                    foundPlatform = platform;
                }
            }

            //check right
            Vector2 rayOriginRight = RayCastCorners.bottomRight + Vector2.up * (_horizontalRaySpace * i);
            if (Vector2.Dot(platformOffset, Vector2.right) > 0)
            {
                rayOriginRight += platformOffset;
            }

            RaycastHit2D hitRight = Physics2D.Raycast(rayOriginRight, Vector2.right, rayLength, _moveStats.GroundLayer);

            #region Debug Visualization

            if (_moveStats.DebugShowWallHit)
            {
                Debug.DrawRay(rayOriginRight, Vector2.right * rayLength, hitRight ? Color.cyan : Color.red);
            }

            #endregion

            if (hitRight)
            {
                float wallAngle = Mathf.Round(Vector2.Angle(hitRight.normal, Vector2.up));

                if (_playerMovement.IsSlideableSlope(wallAngle))
                {
                    _internalState.IsAgainstSteepSlope = true;
                }

                if (_playerMovement.IsWallSlideable(wallAngle))
                {
                    _internalState.IsAgainstWall = true;
                    _internalState.WallDirection = 1;
                    _internalState.WallAngle = wallAngle;
                    foundWall = true;
                }

                var platform = hitRight.collider.GetComponent<IVelocityInheritable>();
                if (platform != null)
                {
                    foundPlatform = platform;
                }
            }

            if (foundWall)
            {
                break;
            }
        }
    }

    /// <summary>프레임 이동 계산 전에 경사/코너 보정 상태를 초기화합니다.</summary>
    private void ResetCollisionStates()
    {
        WasClimbingSlopeLastFrame = IsClimbingSlope;
        IsClimbingSlope = false;
        IsDescendingSlope = false;
        SlopeAngle = 0f;
        SlopeNormal = Vector2.zero;
        _isCornerCorrectingThisFrame = false;
        _isHorizontalCornerCorrectingThisFrame = false;

        if (!_internalState.IsGrounded)
        {
            _slopeCurveAccumulator = 0f;
        }
    }

    /// <summary>수평 이동 시 벽/계단/경사 충돌을 반영해 속도를 보정합니다.</summary>
    private void ResolveHorizontalMovement(ref Vector2 velocity)
    {
        if (_isHorizontalCornerCorrectingThisFrame)
        {
            return;
        }

        float originalVelocityX = velocity.x;

        float directionX = Mathf.Sign(velocity.x);

        if (velocity.x == 0f)
        {
            directionX = FaceDirection;
        }

        float rayLength = Mathf.Abs(velocity.x) + CollisionPadding;

        if (Mathf.Abs(velocity.x) < CollisionPadding)
        {
            rayLength = CollisionPadding * 2;
        }

        if (LastKnownPlatform != null && LastKnownPlatform.ProbesShouldLead)
        {
            rayLength += Mathf.Abs(LastKnownPlatform.GetVelocity().x * Time.fixedDeltaTime);
        }

        for (int i = 0; i < NumOfHorizontalRays; i++)
        {
            Vector2 rayOrigin = (directionX == -1) ? RayCastCorners.bottomLeft : RayCastCorners.bottomRight;
            rayOrigin += Vector2.up * (_horizontalRaySpace * i);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, rayLength, _moveStats.GroundLayer);

            if (hit)
            {
                //priority 1 - climb slopes
                float slopeAngle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.up));
                bool isLowerBodyHit = i <= (NumOfHorizontalRays / 2);
                if (isLowerBodyHit && _playerMovement.IsWalkableSlope(slopeAngle) && slopeAngle > 0f)
                {
                    ClimbSlope(ref velocity, slopeAngle, hit.normal, originalVelocityX);
                    continue;
                }

                if (IsClimbingSlope && slopeAngle <= _moveStats.MaxSlopeAngle)
                {
                    continue;
                }

                //priority 2 - step up
                if (i == 0 && hit.distance > 0.001f && _internalState.IsGrounded)
                {
                    if (AttemptStepUp(hit, ref velocity, directionX, originalVelocityX))
                    {
                        break;
                    }
                }

                //priority 3 - shin guard
                if (i == 0)
                {
                    Vector2 shinOrigin = rayOrigin + (Vector2.up * _horizontalRaySpace);
                    RaycastHit2D shinHit = Physics2D.Raycast(shinOrigin, Vector2.right * directionX, rayLength, _moveStats.GroundLayer);

                    if (shinHit)
                    {
                        float shinAngle = Mathf.Round(Vector2.Angle(shinHit.normal, Vector2.up));
                        if (shinAngle <= _moveStats.MaxSlopeAngle)
                        {
                            ClimbSlope(ref velocity, shinAngle, shinHit.normal, originalVelocityX);
                            continue;
                        }
                    }
                }

                //rest
                bool isSlideableSlope = slopeAngle > _moveStats.MaxSlopeAngle && slopeAngle < _moveStats.MinAngleForWallSlide;
                if (isSlideableSlope)
                {
                    velocity.x = (hit.distance - CollisionPadding) * directionX;

                    if (IsClimbingSlope)
                    {
                        velocity.y = Mathf.Tan(SlopeAngle * Mathf.Deg2Rad) * Mathf.Abs(velocity.x);
                    }

                    continue;
                }

                if (IsSliding)
                {
                    bool isWall = _playerMovement.IsWallSlideable(slopeAngle);

                    if (!isWall)
                    {
                        continue;
                    }
                }

                velocity.x = (hit.distance - CollisionPadding) * directionX;
                rayLength = hit.distance;

                if (IsClimbingSlope)
                {
                    velocity.y = Mathf.Tan(SlopeAngle * Mathf.Deg2Rad) * Mathf.Abs(velocity.x);
                }
            }
        }
    }

    /// <summary>수직 이동 시 천장/지면 충돌을 반영해 속도를 보정합니다.</summary>
    private void ResolveVerticalMovement(ref Vector2 velocity)
    {
        #region Ceiling Check

        if (velocity.y >= 0f)
        {
            if (_isCornerCorrectingThisFrame)
            {
                return;
            }

            float upwardRayLength = Mathf.Abs(velocity.y) + CollisionPadding;
            for (int i = 0; i < NumOfVerticalRays; i++)
            {
                Vector2 rayOrigin = RayCastCorners.topLeft;

                rayOrigin += Vector2.right * (_verticalRaySpace * i);
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up, upwardRayLength, _moveStats.GroundLayer);

                if (hit)
                {
                    float angle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.down));

                    if (hit.distance == 0)
                    {
                        Vector2 shiftDir = Vector2.zero;

                        if (i == 0)
                        {
                            shiftDir = Vector2.right * CollisionPadding * 2f;
                        }
                        else if (i == NumOfVerticalRays - 1)
                        {
                            shiftDir = Vector2.left * CollisionPadding * 2f;
                        }

                        if (shiftDir != Vector2.zero)
                        {
                            Vector2 safetyOrigin = rayOrigin + shiftDir;

                            RaycastHit2D safetyHit = Physics2D.Raycast(safetyOrigin, Vector2.up, upwardRayLength, _moveStats.GroundLayer);

                            if (safetyHit)
                            {
                                angle = Mathf.Round(Vector2.Angle(safetyHit.normal, Vector2.down));
                            }
                            else
                            {
                                angle = 90f;
                            }
                        }
                    }

                    if (angle > 85f)
                    {
                        continue;
                    }

                    if (hit.distance == 0)
                    {
                        velocity.y = 0f;
                    }
                    else
                    {
                        velocity.y = (hit.distance - CollisionPadding);
                        upwardRayLength = hit.distance;
                    }
                }
            }
        }

        #endregion

        #region Ground Check

        float downwardRayLength = Mathf.Abs(velocity.y) + CollisionPadding;

        if (LastKnownPlatform != null && LastKnownPlatform.ProbesShouldLead)
        {
            downwardRayLength += Mathf.Abs(LastKnownPlatform.GetVelocity().y * Time.fixedDeltaTime);
        }

        float smallestHitDistance = float.MaxValue;
        RaycastHit2D groundHit = new RaycastHit2D();
        bool foundGround = false;
        bool foundWalkableGround = false;

        for (int i = 0; i < NumOfVerticalRays; i++)
        {
            Vector2 rayOrigin = RayCastCorners.bottomLeft;
            rayOrigin += Vector2.right * (_verticalRaySpace * i + velocity.x);

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, downwardRayLength, _moveStats.GroundLayer);

            if (hit)
            {
                float hitAngle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.up));

                bool isHitWalkable = hitAngle < _moveStats.MaxSlopeAngle;
                bool isSinking = hit.distance < CollisionPadding;

                if (!isSinking && !isHitWalkable)
                {
                    if (IsSliding || _internalState.IsGrounded && _internalState.SlopeAngle < _moveStats.MaxSlopeAngle)
                    {
                        continue;
                    }
                }

                if (!foundGround)
                {
                    smallestHitDistance = hit.distance;
                    groundHit = hit;
                    foundGround = true;
                    foundWalkableGround = isHitWalkable;
                }
                else
                {
                    if (!foundWalkableGround && isHitWalkable)
                    {
                        smallestHitDistance = hit.distance;
                        groundHit = hit;
                        foundWalkableGround = true;
                    }
                    else if (foundWalkableGround == isHitWalkable && hit.distance < smallestHitDistance)
                    {
                        smallestHitDistance = hit.distance;
                        groundHit = hit;
                    }
                }
            }
        }

        if (foundGround)
        {
            float calculation = (groundHit.distance - CollisionPadding) * -1;
            if (calculation > 0 && calculation <= CollisionPadding + 0.001f)
            {
                //do nothing
            }
            else
            {
                if (Mathf.Abs(calculation) <= 0.001f)
                {
                    calculation = 0f;
                }
            }

            if (velocity.y <= 0f)
            {
                float distanceToFloor = groundHit.distance - CollisionPadding;
                if (IsSliding && distanceToFloor > 0f && _internalState.SlopeAngle < 89.9f)
                {
                    float slopeAngleRad = _internalState.SlopeAngle * Mathf.Deg2Rad;
                    float pushOutX = distanceToFloor / Mathf.Tan(slopeAngleRad);
                    velocity.x += pushOutX * Mathf.Sign(_internalState.SlopeNormal.x);
                }

                velocity.y = calculation;
            }
            else if (IsClimbingSlope)
            {
                velocity.y += calculation;
            }
        }

        #endregion

        if (IsClimbingSlope)
        {
            float directionX = Mathf.Sign(velocity.x);
            float rayLength = Mathf.Abs(velocity.x) + CollisionPadding;
            Vector2 rayOrigin = ((directionX == -1) ? RayCastCorners.bottomLeft : RayCastCorners.bottomRight) + Vector2.up * velocity.y;
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, rayLength, _moveStats.GroundLayer);

            if (hit)
            {
                float slopeAngle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.up));
                if (slopeAngle != SlopeAngle)
                {
                    velocity.x = (hit.distance - CollisionPadding) * directionX;
                    SlopeAngle = slopeAngle;
                    SlopeNormal = hit.normal;
                }
            }
        }
    }

    /// <summary>현재 콜라이더 경계 기반으로 레이 시작 모서리를 갱신합니다.</summary>
    private void UpdateRaycastCorners()
    {
        Bounds bounds = _coll.bounds;
        bounds.Expand(CollisionPadding * -2);

        RayCastCorners.bottomLeft = new Vector2(bounds.min.x, bounds.min.y);
        RayCastCorners.bottomRight = new Vector2(bounds.max.x, bounds.min.y);
        RayCastCorners.topLeft = new Vector2(bounds.min.x, bounds.max.y);
        RayCastCorners.topRight = new Vector2(bounds.max.x, bounds.max.y);
    }

    /// <summary>수평/수직 레이 간격을 콜라이더 크기에 맞춰 계산합니다.</summary>
    private void CalculateRaySpacing()
    {
        Bounds bounds = _coll.bounds;
        bounds.Expand(CollisionPadding * -2);

        _horizontalRaySpace = bounds.size.y / (NumOfHorizontalRays - 1);
        _verticalRaySpace = bounds.size.x / (NumOfVerticalRays - 1);
    }

    #region Slopes

    /// <summary>오르막 경사에 맞게 속도를 분해해 상승 이동으로 변환합니다.</summary>
    private void ClimbSlope(ref Vector2 velocity, float slopeAngle, Vector2 slopeNormal, float originalInputX)
    {
        float moveDistance = Mathf.Abs(originalInputX);
        float climbVelocityY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

        if (velocity.y <= climbVelocityY)
        {
            velocity.y = climbVelocityY;
            velocity.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(velocity.x);

            IsClimbingSlope = true;
            SlopeAngle = slopeAngle;
            SlopeNormal = slopeNormal;
            _rearCornerSlopeAngle = slopeAngle;
            _slopeCurveAccumulator = 0f;
        }
        else
        {
            float dot = Vector2.Dot(velocity.normalized, slopeNormal);
            if (Mathf.Abs(dot) < 0.05f && _playerMovement.IsDashing)
            {
                IsClimbingSlope = true;
                SlopeAngle = slopeAngle;
                SlopeNormal = slopeNormal;
                _rearCornerSlopeAngle = slopeAngle;
                _slopeCurveAccumulator = 0f;
            }
        }
    }

    /// <summary>내리막 경사/벽 경사에 따라 하강 이동 또는 슬라이딩을 적용합니다.</summary>
    private void DescendSlope(ref Vector2 velocity)
    {
        float directionX = FaceDirection;

        Vector2 rayOrigin = (directionX == -1) ? RayCastCorners.bottomRight : RayCastCorners.bottomLeft;

        float maxExpectedVerticalDrop = Mathf.Tan(_moveStats.MinAngleForWallSlide * Mathf.Deg2Rad) * Mathf.Abs(velocity.x);
        float dynamicRayLength = Mathf.Abs(velocity.y) + CollisionPadding + maxExpectedVerticalDrop;
        float rayLength = Mathf.Max(dynamicRayLength, Mathf.Abs(velocity.y), CollisionPadding * 2f);

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayLength, _moveStats.GroundLayer);

        #region Debug Visualization

        if (_moveStats.DebugShowDescendSlopeRay)
        {
            bool isSticking = IsDescendingSlope;
            Color rayColor = Color.blue;

            if (hit && isSticking) rayColor = Color.green;
            else if (hit && IsSliding) rayColor = Color.yellow;
            else if (hit && !isSticking) rayColor = Color.red;

            float debugRayLength = hit ? hit.distance + _moveStats.ExtraRayDebugDistance : _moveStats.ExtraRayDebugDistance * 5f;
            Debug.DrawRay(rayOrigin, Vector2.down * debugRayLength, rayColor);
        }

        #endregion

        if (hit)
        {
            float slopeAngle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.up));
            float horizSpeed = Mathf.Abs(velocity.x) / Time.fixedDeltaTime;

            bool inLandingGrace = (Time.time - LastLandingTime) < _moveStats.LandingGraceTime;
            bool wasAirborne = _rearCornerSlopeAngle == AIRBORNE_ANGLE_MEMORY;
            float angleDelta = wasAirborne ? 0f : slopeAngle - _rearCornerSlopeAngle;

            int wallDirection = (int)Mathf.Sign(hit.normal.x);
            bool isFacingWall = (wallDirection == -1 && _playerMovement.IsFacingRight) || (wallDirection == 1 && !_playerMovement.IsFacingRight);

            bool isNormalSlideableSlope = _playerMovement.IsSlideableSlope(slopeAngle);
            bool isWallSlope = slopeAngle >= _moveStats.MinAngleForWallSlide;
            bool shouldSlide = isNormalSlideableSlope || (isWallSlope && !isFacingWall);

            if (!wasAirborne && angleDelta > 0.1f)
            {
                _slopeCurveAccumulator += angleDelta;
            }
            else
            {
                _slopeCurveAccumulator -= _moveStats.SlopeCurveDecayRate * Time.fixedDeltaTime;
            }

            _slopeCurveAccumulator = Mathf.Clamp(_slopeCurveAccumulator, 0f, 180f);

            //landing check
            if (inLandingGrace && wasAirborne)
            {
                if (shouldSlide)
                {
                    SlideDownMaxSlope(hit, ref velocity);
                    _rearCornerSlopeAngle = slopeAngle;
                    return;
                }
                else if (slopeAngle <= _moveStats.MaxSlopeAngle)
                {
                    ApplySlopeStick(ref velocity, slopeAngle, hit);
                    _rearCornerSlopeAngle = slopeAngle;
                }
                else
                {
                    _rearCornerSlopeAngle = slopeAngle;
                }
            }
            //dramatic angle change
            else if (!wasAirborne && angleDelta >= _moveStats.MaxAngleDeltaForRunOff)
            {
                _rearCornerSlopeAngle = AIRBORNE_ANGLE_MEMORY;
                _forceAirborneNextFrame = true;
            }
            //speed check
            else if (!wasAirborne && (angleDelta >= _moveStats.MinAngleDeltaForRunOff || _slopeCurveAccumulator > _moveStats.MaxSlopeCurveAccumulation))
            {
                if (horizSpeed >= _moveStats.SpeedForRunOff)
                {
                    _rearCornerSlopeAngle = AIRBORNE_ANGLE_MEMORY;
                    _forceAirborneNextFrame = true;
                    _slopeCurveAccumulator = 0f;
                }
                else
                {
                    if (shouldSlide)
                    {
                        SlideDownMaxSlope(hit, ref velocity);
                        _rearCornerSlopeAngle = slopeAngle;
                        return;
                    }
                    else if (slopeAngle <= _moveStats.MaxSlopeAngle)
                    {
                        ApplySlopeStick(ref velocity, slopeAngle, hit);
                        _rearCornerSlopeAngle = slopeAngle;
                    }
                    else
                    {
                        _rearCornerSlopeAngle = slopeAngle;
                    }
                }
            }
            //airborne runOff
            else if (wasAirborne && !inLandingGrace)
            {
                float checkDirection = (Mathf.Abs(velocity.x) < 0.01f) ? FaceDirection : Mathf.Sign(velocity.x);
                bool isMovingDownSlope = Mathf.Sign(hit.normal.x) == checkDirection;

                if (!isMovingDownSlope)
                {
                    _slopeCurveAccumulator = 0f;
                }

                if (shouldSlide && isMovingDownSlope)
                {
                    SlideDownMaxSlope(hit, ref velocity);
                    _rearCornerSlopeAngle = slopeAngle;
                    return;
                }
                else if (slopeAngle <= _moveStats.MaxSlopeAngle)
                {
                    _rearCornerSlopeAngle = slopeAngle;
                }
                else
                {
                    _rearCornerSlopeAngle = AIRBORNE_ANGLE_MEMORY;
                }
            }
            //steep slope
            else if (slopeAngle > _moveStats.MaxSlopeAngle)
            {
                float checkDirection = (Mathf.Abs(velocity.x) < 0.01f) ? FaceDirection : Mathf.Sign(velocity.x);
                bool isMovingDownSlope = Mathf.Sign(hit.normal.x) == checkDirection;

                if (!isMovingDownSlope)
                {
                    _slopeCurveAccumulator = 0f;
                    _rearCornerSlopeAngle = slopeAngle;
                }
                else if (slopeAngle < _moveStats.MinAngleForWallSlide)
                {
                    SlideDownMaxSlope(hit, ref velocity);
                    _rearCornerSlopeAngle = slopeAngle;
                    return;
                }
                else
                {
                    _rearCornerSlopeAngle = slopeAngle;
                }
            }
            //standard walkable slope
            else
            {
                ApplySlopeStick(ref velocity, slopeAngle, hit);
                _rearCornerSlopeAngle = slopeAngle;
            }
        }
        else
        {
            _slopeCurveAccumulator = 0f;
            _rearCornerSlopeAngle = AIRBORNE_ANGLE_MEMORY;
        }

        bool isStable = false;

        float guardRayLength = Mathf.Max(Mathf.Abs(velocity.y) + CollisionPadding, _verticalProbeDistance);

        for (int i = 0; i < NumOfVerticalRays; i++)
        {
            Vector2 guardOrigin = RayCastCorners.bottomLeft + Vector2.right * (_verticalRaySpace * i);
            RaycastHit2D guardHit = Physics2D.Raycast(guardOrigin, Vector2.down, guardRayLength, _moveStats.GroundLayer);

            if (guardHit)
            {
                float guardAngle = Mathf.Round(Vector2.Angle(guardHit.normal, Vector2.up));
                if (guardAngle <= _moveStats.MaxSlopeAngle)
                {
                    isStable = true;
                    break;
                }
            }
        }

        if (!isStable)
        {
            RaycastHit2D maxSlopeHitLeft = Physics2D.Raycast(RayCastCorners.bottomLeft, Vector2.down, Mathf.Abs(velocity.y) + CollisionPadding, _moveStats.GroundLayer);
            RaycastHit2D maxSlopeHitRight = Physics2D.Raycast(RayCastCorners.bottomRight, Vector2.down, Mathf.Abs(velocity.y) + CollisionPadding, _moveStats.GroundLayer);

            if (maxSlopeHitLeft ^ maxSlopeHitRight)
            {
                SlideDownMaxSlope(maxSlopeHitLeft, ref velocity);
                SlideDownMaxSlope(maxSlopeHitRight, ref velocity);
            }
        }
    }

    /// <summary>경사 기억값을 현재 접지 상태에 맞게 업데이트합니다.</summary>
    public void UpdateSlopeMemory()
    {
        _slopeCurveAccumulator = 0f;
        _rearCornerSlopeAngle = AIRBORNE_ANGLE_MEMORY;

        LastLandingTime = Time.time;
    }

    /// <summary>착지 직후 경사면에 밀착되도록 이동량을 보정합니다.</summary>
    private void ApplySlopeStick(ref Vector2 moveAmount, float slopeAngle, RaycastHit2D hit)
    {
        if (_playerMovement.IsWallSlideable(slopeAngle))
        {
            bool isFacingWall = Mathf.Sign(hit.normal.x) != FaceDirection;
            bool shouldWallSlide = isFacingWall || _moveStats.CanWallSlideFacingAwayFromWall;

            if (shouldWallSlide)
            {
                return;
            }
            else
            {
                SlideDownMaxSlope(hit, ref moveAmount);
            }
        }

        float tanDist = Mathf.Tan(slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(moveAmount.x);
        float hitDistCheck = hit.distance - CollisionPadding;

        if (hitDistCheck <= tanDist && slopeAngle > 0.001f)
        {
            float moveDistance = Mathf.Abs(moveAmount.x);
            float descendMoveAmountY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

            moveAmount.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(moveAmount.x);
            moveAmount.y -= descendMoveAmountY;

            IsDescendingSlope = true;
            SlopeNormal = hit.normal;
            SlopeAngle = slopeAngle;
        }
    }

    /// <summary>한계 각도 이상의 경사면에서 미끄러지는 속도를 계산합니다.</summary>
    private void SlideDownMaxSlope(RaycastHit2D hit, ref Vector2 velocity)
    {
        if (hit)
        {
            float slopeAngle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.up));

            int wallDirection = (int)Mathf.Sign(hit.normal.x);
            bool isFacingWall = (wallDirection == -1 && _playerMovement.IsFacingRight) || (wallDirection == 1 && !_playerMovement.IsFacingRight);

            bool isNormalSlideableSlope = slopeAngle > _moveStats.MaxSlopeAngle && slopeAngle < _moveStats.MinAngleForWallSlide;
            bool isWallSlope = slopeAngle >= _moveStats.MinAngleForWallSlide;

            if (isNormalSlideableSlope || (isWallSlope && !isFacingWall))
            {
                Vector2 slopeTangent = new Vector2(hit.normal.y, -hit.normal.x);

                if (slopeTangent.y > 0)
                {
                    slopeTangent = -slopeTangent;
                }
                slopeTangent.Normalize();

                float currentSpeed = velocity.magnitude;

                Vector2 slideVelocity = slopeTangent * currentSpeed;

                slideVelocity.y -= (hit.distance - CollisionPadding);

                velocity.x = slideVelocity.x;
                velocity.y = slideVelocity.y;

                SlopeAngle = slopeAngle;
                SlopeNormal = hit.normal;
                IsDescendingSlope = true;
            }
        }
    }

    #endregion

    #region Step Up

    /// <summary>앞쪽 충돌 지점이 계단인지 검사하고 계단 높이를 계산합니다.</summary>
    private bool GetStepInfo(float hitDistance, float directionX, out float stepHeight)
    {
        stepHeight = 0f;

        Vector2 stepProbeUpperOrigin = (directionX == -1) ? RayCastCorners.bottomLeft : RayCastCorners.bottomRight;
        stepProbeUpperOrigin.y += _moveStats.StepMaxHeight;
        float checkDist = hitDistance + _moveStats.StepDetectionRayWidth;

        RaycastHit2D stepHitUpper = Physics2D.Raycast(stepProbeUpperOrigin, Vector2.right * directionX, checkDist, _moveStats.GroundLayer);
        if (stepHitUpper) return false;

        Vector2 stepProbeLowerOrigin = stepProbeUpperOrigin;
        stepProbeLowerOrigin.x += directionX * checkDist;

        RaycastHit2D surfaceHit = Physics2D.Raycast(stepProbeLowerOrigin, Vector2.down, _moveStats.StepMaxHeight + CollisionPadding, _moveStats.GroundLayer);
        if (!surfaceHit) return false;

        float surfaceAngle = Mathf.Round(Vector2.Angle(surfaceHit.normal, Vector2.up));
        if (!_playerMovement.IsWalkableSlope(surfaceAngle)) return false;

        float characterHeight = _coll.bounds.size.y;
        Vector2 finalHeadroomStart = surfaceHit.point + (Vector2.up * CollisionPadding);
        RaycastHit2D finalHeadroomHit = Physics2D.Raycast(finalHeadroomStart, Vector2.up, characterHeight - CollisionPadding, _moveStats.GroundLayer);
        if (finalHeadroomHit) return false;

        Vector2 currentHeadPos = (directionX == -1) ? RayCastCorners.topLeft : RayCastCorners.topRight;
        Vector2 targetHeadPos = finalHeadroomStart + (Vector2.up * (characterHeight - CollisionPadding));

        Vector2 diagonalDir = targetHeadPos - currentHeadPos;
        float diagonalDist = diagonalDir.magnitude;

        RaycastHit2D diagonalHit = Physics2D.Raycast(currentHeadPos, diagonalDir.normalized, diagonalDist, _moveStats.GroundLayer);
        if (diagonalHit) return false;

        float currentFeetY = (directionX == -1) ? RayCastCorners.bottomLeft.y : RayCastCorners.bottomRight.y;
        stepHeight = surfaceHit.point.y - currentFeetY;

        return true;
    }

    /// <summary>계단 오르기 가능 시 위치 보정과 속도 조정을 수행합니다.</summary>
    private bool AttemptStepUp(RaycastHit2D hit, ref Vector2 velocity, float directionX, float originalVelocityX)
    {
        if (GetStepInfo(hit.distance, directionX, out float stepHeight))
        {
            float minStepUpX = (hit.distance + _moveStats.StepDetectionRayWidth) * directionX;
            float targetX = minStepUpX;

            if (Mathf.Abs(originalVelocityX) > Mathf.Abs(minStepUpX))
            {
                targetX = originalVelocityX;
            }

            bool isSmallBump = stepHeight <= _moveStats.VaultMinHeight;
            bool isMovingFast = _playerMovement.IsRunning || _playerMovement.IsDashing;
            bool canVault = !_moveStats.OnlyVaultWhenRunning || isMovingFast;

            if (isSmallBump || canVault)
            {
                velocity.y = stepHeight + 0.001f;
                velocity.x = targetX;
                return true;
            }
        }

        return false;
    }

    /// <summary>현재 속도 기준으로 계단 처리 대상인지 판단합니다.</summary>
    public bool IsStep(Vector2 currentVelocity)
    {
        float directionX = Mathf.Sign(currentVelocity.x);
        if (Mathf.Abs(currentVelocity.x) < 0.001f)
        {
            directionX = FaceDirection;
        }

        float lookAhead = Mathf.Abs(currentVelocity.x) * Time.fixedDeltaTime + CollisionPadding;
        Vector2 rayOrigin = (directionX == -1) ? RayCastCorners.bottomLeft : RayCastCorners.bottomRight;

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, lookAhead, _moveStats.GroundLayer);

        if (!hit)
        {
            return false;
        }

        return GetStepInfo(hit.distance, directionX, out _);
    }

    #endregion

    #region Corner Correction

    /// <summary>천장 모서리 걸림 해소를 위한 수평 보정량을 계산합니다.</summary>
    private bool CalculateHeadCornerCorrection(Vector3 velocity, out float correctionAmount)
    {
        correctionAmount = 0f;

        if (velocity.y <= 0 || !_moveStats.EnableCornerCorrection) return false;

        float rayLength = Mathf.Abs(velocity.y) + CollisionPadding;
        rayLength = Mathf.Max(rayLength, _verticalProbeDistance);

        Vector2 leftOuter = RayCastCorners.topLeft;
        Vector2 rightOuter = RayCastCorners.topRight;

        RaycastHit2D hitLeftOuter = Physics2D.Raycast(leftOuter, Vector2.up, rayLength, _moveStats.GroundLayer);
        RaycastHit2D hitRightOuter = Physics2D.Raycast(rightOuter, Vector2.up, rayLength, _moveStats.GroundLayer);

        bool leftBlocked = hitLeftOuter;
        bool rightBlocked = hitRightOuter;

        if (leftBlocked && rightBlocked)
        {
            return false;
        }
        if (!leftBlocked && !rightBlocked)
        {
            return false;
        }

        float nudgeDir = leftBlocked ? 1f : -1f;
        float pushAmount = _moveStats.CornerCorrectionWidth + CollisionPadding;

        RaycastHit2D blockingHit = leftBlocked ? hitLeftOuter : hitRightOuter;
        float currentHitDist = blockingHit.distance;
        Vector2 normal = blockingHit.normal;

        float deltaX = pushAmount * nudgeDir;

        float projectedRise = 0f;
        if (Mathf.Abs(normal.y) > 0.001f)
        {
            projectedRise = -deltaX * (normal.x / normal.y);
        }

        float safetyBuffer = 0.3f;
        float testHeight = currentHitDist + projectedRise + safetyBuffer;

        Vector2 potentialNudge = Vector2.right * deltaX;
        Vector2 potentialVerticalMove = Vector2.up * testHeight;
        Vector2 testPos = _rb.position + potentialNudge + potentialVerticalMove;

        if (IsSpaceClear(testPos))
        {
            correctionAmount = pushAmount * nudgeDir;

            if (_moveStats.DebugShowCornerCorrectionRays)
            {
                DrawDebugBox(testPos, _coll.bounds.size, Color.green, 0.05f);
            }

            return true;
        }
        else
        {
            if (_moveStats.DebugShowCornerCorrectionRays)
            {
                DrawDebugBox(testPos, _coll.bounds.size, Color.red, 0.05f);
            }

            return false;
        }
    }

    /// <summary>디버그 시각화를 위해 박스 라인을 그립니다.</summary>
    private void DrawDebugBox(Vector2 center, Vector2 size, Color color, float duration)
    {
        Vector2 halfSize = size / 2;
        Vector2 topLeft = center + new Vector2(-halfSize.x, halfSize.y);
        Vector2 topRight = center + new Vector2(halfSize.x, halfSize.y);
        Vector2 bottomLeft = center + new Vector2(-halfSize.x, -halfSize.y);
        Vector2 bottomRight = center + new Vector2(halfSize.x, -halfSize.y);

        Debug.DrawLine(topLeft, topRight, color, duration);
        Debug.DrawLine(topRight, bottomRight, color, duration);
        Debug.DrawLine(bottomRight, bottomLeft, color, duration);
        Debug.DrawLine(bottomLeft, topLeft, color, duration);
    }

    /// <summary>지정한 위치가 다른 충돌체와 겹치지 않는지 검사합니다.</summary>
    private bool IsSpaceClear(Vector2 targetPosition)
    {
        Vector2 checkSize = _coll.bounds.size - (Vector3.one * (CollisionPadding * 2f));
        Vector2 center = targetPosition + _coll.offset;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_moveStats.GroundLayer);
        filter.useTriggers = false;

        int hitCount = Physics2D.OverlapBox(center, checkSize, 0f, filter, _overlapBuffer);

        return hitCount == 0;
    }

    /// <summary>헤드 코너 보정이 필요한지 감지합니다.</summary>
    private bool DetectHeadCornerCorrection(Vector2 velocity)
    {
        return CalculateHeadCornerCorrection(velocity, out _);
    }

    /// <summary>계산된 헤드 코너 보정량을 이동 속도에 적용합니다.</summary>
    private void ApplyHeadCornerCorrection(ref Vector2 velocity)
    {
        if (CalculateHeadCornerCorrection(velocity, out float amount))
        {
            velocity.x += amount;
            _isCornerCorrectingThisFrame = true;
        }
    }

    /// <summary>수평 이동 중 바닥 모서리 보정량을 계산합니다.</summary>
    private bool CalculateHorizontalCornerCorrection(Vector2 velocity, out float correctionAmount)
    {
        //feet
        correctionAmount = 0f;

        if (!_playerMovement.IsDashing || IsClimbingSlope) return false;

        float directionX = Mathf.Sign(velocity.x);
        float rayLength = Mathf.Abs(velocity.x) + CollisionPadding;
        if (Mathf.Abs(velocity.x) < CollisionPadding)
        {
            rayLength = CollisionPadding * 2;
        }

        Vector2 footOrigin = (directionX == -1) ? RayCastCorners.bottomLeft : RayCastCorners.bottomRight;
        Vector2 headOrigin = (directionX == -1) ? RayCastCorners.topLeft : RayCastCorners.topRight;

        bool validLift = false;
        float potentialLiftAmount = 0f;

        RaycastHit2D footHit = Physics2D.Raycast(footOrigin, Vector2.right * directionX, rayLength, _moveStats.GroundLayer);

        if (_moveStats.DebugShowCornerCorrectionRays)
        {
            Debug.DrawRay(footOrigin, Vector2.right * directionX * rayLength, footHit ? Color.red : Color.green);
        }

        if (footHit)
        {
            float hitAngle = Mathf.Round(Vector2.Angle(footHit.normal, Vector2.up));
            if (hitAngle <= _moveStats.MaxSlopeAngle)
            {
                return false;
            }

            float clearanceHeight = _moveStats.HorizontalCornerCorrectionHeight;
            Vector2 highProbeOrigin = footOrigin + Vector2.up * clearanceHeight;
            float checkDist = footHit.distance + CollisionPadding;

            bool highHit = Physics2D.Raycast(highProbeOrigin, Vector2.right * directionX, checkDist, _moveStats.GroundLayer);

            if (_moveStats.DebugShowCornerCorrectionRays)
            {
                Debug.DrawRay(highProbeOrigin, Vector2.right * directionX * checkDist, highHit ? Color.red : Color.green);
            }

            if (!highHit)
            {
                Vector2 downRayOrigin = footHit.point + (Vector2.right * directionX * CollisionPadding) + (Vector2.up * clearanceHeight);
                RaycastHit2D surfaceHit = Physics2D.Raycast(downRayOrigin, Vector2.down, clearanceHeight + CollisionPadding, _moveStats.GroundLayer);

                if (_moveStats.DebugShowCornerCorrectionRays)
                {
                    Debug.DrawRay(downRayOrigin, Vector2.down * (clearanceHeight + CollisionPadding), Color.cyan);
                }

                if (surfaceHit)
                {
                    float lift = (surfaceHit.point.y + CollisionPadding) - footOrigin.y;
                    if (lift > 0 && lift <= clearanceHeight + CollisionPadding)
                    {
                        Vector2 targetHeadPos = headOrigin + (Vector2.right * directionX * CollisionPadding) + (Vector2.up * lift);
                        bool ceilingBlocked = Physics2D.Raycast(targetHeadPos, Vector2.up, CollisionPadding, _moveStats.GroundLayer);

                        if (!ceilingBlocked)
                        {
                            potentialLiftAmount = lift;
                            validLift = true;
                        }
                    }
                }
            }
        }

        //head
        bool validPush = false;
        float potentialPushAmount = 0f;

        RaycastHit2D headHit = Physics2D.Raycast(headOrigin, Vector2.right * directionX, rayLength, _moveStats.GroundLayer);

        if (_moveStats.DebugShowCornerCorrectionRays)
        {
            Debug.DrawRay(headOrigin, Vector2.right * directionX * rayLength, headHit ? Color.red : Color.green);
        }

        if (headHit)
        {
            float clearanceHeight = _moveStats.HorizontalCornerCorrectionHeight;
            Vector2 lowProbeOrigin = headOrigin - Vector2.up * clearanceHeight;
            float checkDist = headHit.distance + CollisionPadding;

            bool lowHit = Physics2D.Raycast(lowProbeOrigin, Vector2.right * directionX, checkDist, _moveStats.GroundLayer);

            if (_moveStats.DebugShowCornerCorrectionRays)
            {
                Debug.DrawRay(lowProbeOrigin, Vector2.right * directionX * checkDist, lowHit ? Color.red : Color.green);
            }

            if (!lowHit)
            {
                Vector2 upRayOrigin = headHit.point + (Vector2.right * directionX * CollisionPadding) - (Vector2.up * clearanceHeight);
                RaycastHit2D ceilingHit = Physics2D.Raycast(upRayOrigin, Vector2.up, clearanceHeight + CollisionPadding, _moveStats.GroundLayer);

                if (_moveStats.DebugShowCornerCorrectionRays)
                {
                    Debug.DrawRay(upRayOrigin, Vector2.up * (clearanceHeight + CollisionPadding), Color.cyan);
                }

                if (ceilingHit)
                {
                    float push = (ceilingHit.point.y - CollisionPadding) - headOrigin.y;
                    if (push < 0 && Mathf.Abs(push) <= clearanceHeight + CollisionPadding)
                    {
                        if (Mathf.Abs(push) > _moveStats.HorizontalPushDownMaximum)
                        {
                            validPush = false;
                        }

                        Vector2 targetFootPos = footOrigin + (Vector2.right * directionX * CollisionPadding) + (Vector2.up * push);
                        bool floorBlocked = Physics2D.Raycast(targetFootPos, Vector2.down, CollisionPadding, _moveStats.GroundLayer);

                        if (!floorBlocked)
                        {
                            potentialPushAmount = push;
                            validPush = true;
                        }
                    }
                }
            }
        }

        if (validLift && validPush)
        {
            return false;
        }

        if (validLift)
        {
            correctionAmount = potentialLiftAmount;
            return true;
        }

        if (validPush)
        {
            correctionAmount = potentialPushAmount;
            return true;
        }

        return false;
    }

    /// <summary>수평 코너 보정량을 수직 이동으로 반영합니다.</summary>
    private void ApplyHorizontalCornerCorrection(ref Vector2 velocity)
    {
        if (CalculateHorizontalCornerCorrection(velocity, out float liftAmount))
        {
            velocity.y = liftAmount;
            _isHorizontalCornerCorrectingThisFrame = true;
        }
    }

    #endregion

    #region Visuals

    /// <summary>시각 보간에 사용할 평균 지면 법선을 계산합니다.</summary>
    private void DetectSlopeNormalsForVisuals(Vector2 velocity)
    {
        if (_internalState.IsGrounded)
        {
            float downwardRayLength = _moveStats.SlopeAveragedNormalsRayLength;

            Vector2 compositeNormal = Vector2.zero;
            int hitCount = 0;

            float visualWidth = _moveStats.VisualRaycastWidth;
            float startX = _coll.bounds.center.x - (visualWidth / 2);
            float bottomY = _coll.bounds.min.y;

            float visualRaySpace = 0;
            if (NumOfVerticalRaysForVisualNormals > 1)
            {
                visualRaySpace = visualWidth / (NumOfVerticalRaysForVisualNormals - 1);
            }

            for (int i = 0; i < NumOfVerticalRaysForVisualNormals; i++)
            {
                float xPos = startX + (visualRaySpace * i);
                Vector2 rayOrigin = new Vector2(xPos, bottomY) + (Vector2.right * velocity.x);

                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, downwardRayLength, _moveStats.GroundLayer);

                if (hit)
                {
                    compositeNormal += hit.normal;
                    hitCount++;
                }
            }

            if (hitCount > 0)
            {
                compositeNormal /= hitCount;
                compositeNormal.Normalize();
                _internalState.AveragedVisualNormal = compositeNormal;
            }
            else
            {
                _internalState.AveragedVisualNormal = Vector2.up;
            }
        }
        else
        {
            _internalState.AveragedVisualNormal = Vector2.Lerp(_internalState.AveragedVisualNormal, Vector2.up, _moveStats.SlopeRotationSpeed * Time.fixedDeltaTime);
        }
    }

    #endregion

    #region Velocity Inheritable

    /// <summary>외부 오브젝트가 가한 밀림/압사 처리를 적용합니다.</summary>
    public void ApplyExternalPush(Vector2 pushAmount, Transform pusher)
    {
        Vector2 direction = pushAmount.normalized;
        float distance = pushAmount.magnitude;

        if (distance > 0.001f)
        {
            Vector2 origin = _coll.bounds.center;
            Vector2 size = _coll.bounds.size - (Vector3.one * CollisionPadding * 2f);

            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, direction, distance, _moveStats.GroundLayer);

            if (hit)
            {
                bool hitsPusher = pusher != null && hit.transform == pusher;

                if (!hitsPusher)
                {
                    bool steppedUp = false;
                    if (Mathf.Abs(pushAmount.x) > 0.001f)
                    {
                        float directionX = Mathf.Sign(pushAmount.x);

                        if (GetStepInfo(hit.distance, directionX, out float stepHeight))
                        {
                            _rb.position += Vector2.up * (stepHeight + CollisionPadding + 0.001f);
                            steppedUp = true;
                        }
                    }

                    if (!steppedUp)
                    {
                        OnCrush?.Invoke();
                    }
                }
            }
        }

        _rb.position += pushAmount;

        _wasPushedThisFrame = true;
        _pushAmountThisFrame = pushAmount;
    }

    #endregion

    #region Helpers Methods

    /// <summary>현재 접지 상태를 반환합니다.</summary>
    public bool IsGrounded() => _internalState.IsGrounded;
    /// <summary>현재 천장 충돌 상태를 반환합니다.</summary>
    public bool BumpedHead() => _internalState.IsHittingCeiling;
    /// <summary>현재 벽 접촉 상태를 반환합니다.</summary>
    public bool IsTouchingWall() => _internalState.IsAgainstWall;
    /// <summary>현재 접촉 중인 벽 방향을 반환합니다.</summary>
    public int GetWallDirection()
    {
        return _internalState.WallDirection;
    }

    #endregion
}

#if UNITY_EDITOR

[CustomEditor(typeof(MovementController))]
public class MovementControllerEditor : Editor
{
    /// <summary>실행 중 충돌 상태를 인스펙터에 읽기 전용으로 출력합니다.</summary>
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        MovementController controller = (MovementController)target;

        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Collision State (Read Only)", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(true);

            EditorGUILayout.Toggle("Is On Platform", controller.LastKnownPlatform != null);
            EditorGUILayout.Space();

            EditorGUILayout.Toggle("Is Grounded", controller.State.IsGrounded);
            EditorGUILayout.Toggle("Is Against Wall", controller.State.IsAgainstWall);
            EditorGUILayout.IntField("Wall Direction", controller.State.WallDirection);
            EditorGUILayout.FloatField("Wall Angle", controller.State.WallAngle);

            EditorGUILayout.Space();
            EditorGUILayout.Toggle("Is On Slope", controller.State.IsOnSlope);
            EditorGUILayout.FloatField("Slope Angle", controller.State.SlopeAngle);
            EditorGUILayout.Vector2Field("Slope Normal", controller.State.SlopeNormal);

            EditorGUILayout.Space();
            EditorGUILayout.Toggle("Is Against Steep Slope", controller.State.IsAgainstSteepSlope);

            EditorGUILayout.Space();
            EditorGUILayout.Toggle("Is Hitting Ceiling", controller.State.IsHittingCeiling);
            EditorGUILayout.FloatField("Ceiling Angle", controller.State.CeilingAngle);
            EditorGUILayout.Vector2Field("Ceiling Normal", controller.State.CeilingNormal);
            EditorGUILayout.Vector2Field("Averaged Visual Normals", controller.State.AveragedVisualNormal);

            EditorGUILayout.Space();
            EditorGUILayout.Toggle("Is Sliding", controller.IsSliding);

            EditorGUILayout.Space();
            EditorGUILayout.Toggle("Is Descending Slope", controller.IsDescendingSlope);
            EditorGUILayout.Toggle("Is Climbing Slope", controller.IsClimbingSlope);

            EditorGUI.EndDisabledGroup();

            Repaint();
        }
    }
}

#endif
