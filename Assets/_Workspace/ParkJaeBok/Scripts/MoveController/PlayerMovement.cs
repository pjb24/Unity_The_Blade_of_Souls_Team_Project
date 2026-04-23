using System;
using UnityEngine;

[SelectionBase]
public class PlayerMovement : MonoBehaviour
{
    /// <summary>
    /// 바라보는 방향이 갱신될 때마다 통지되는 이벤트입니다.
    /// </summary>
    public event Action<bool> FacingDirectionChanged;

    [Header("References")]
    // 캐릭터 이동에 사용되는 모든 수치 데이터(속도, 가속, 점프값)를 담는다.
    public PlayerMovementStats MoveStats;
    // 플레이어의 실제 충돌체 참조이다.
    [SerializeField] private Collider2D _coll;
    // 플레이어 외형(스프라이트) 루트 트랜스폼이다.
    [SerializeField] private Transform _visualsTransform;
    // 사망/압사 시 복귀할 기준 위치이다.
    [SerializeField] private Transform _respawnPoint;

    // 물리 이동을 처리하는 Rigidbody2D 컴포넌트 참조이다.
    private Rigidbody2D _rb;

    //movement vars
    // 입력 방향 반전, 벽 상호작용, 스프라이트 좌우 플립 기준이 되는 현재 바라보는 방향이다.
    public bool IsFacingRight { get; private set; }
    // 지면/벽/경사 센서를 관리하는 이동 컨트롤러 참조이다.
    public MovementController Controller { get; private set; }
    // 현재 프레임에서 계산된 최종 속도 벡터이다.
    [HideInInspector] public Vector2 Velocity;
    public Vector2 MoveInput => _moveInput;

    //input
    // 매 프레임 수집한 이동 입력값으로, 가속 계산과 방향 전환 판정의 원본 데이터이다.
    private Vector2 _moveInput;
    // 달리기 입력이 현재 유지 중인지 기록한다.
    private bool _runHeld;
    // 점프 눌림 입력을 물리 프레임까지 버퍼링한다.
    private bool _jumpPressed;
    // 점프 해제 입력을 물리 프레임까지 버퍼링한다.
    private bool _jumpReleased;
    // 대시 눌림 입력을 물리 프레임까지 버퍼링한다.
    private bool _dashPressed;

    // 외부 드라이버(PlayerInputDriver) 사용 시 이동 입력을 전달받는 버퍼이다.
    private Vector2 _drivenMoveInput;
    // 외부 드라이버(PlayerInputDriver) 사용 시 달리기 유지 입력을 전달받는 버퍼이다.
    private bool _drivenRunHeld;
    // 외부 드라이버(PlayerInputDriver) 사용 시 점프 눌림 입력을 전달받는 버퍼이다.
    private bool _drivenJumpPressed;
    // 외부 드라이버(PlayerInputDriver) 사용 시 점프 해제 입력을 전달받는 버퍼이다.
    private bool _drivenJumpReleased;
    // 외부 드라이버(PlayerInputDriver) 사용 시 대시 눌림 입력을 전달받는 버퍼이다.
    private bool _drivenDashPressed;
    // 입력 소스를 InputManager 직접 읽기에서 외부 드라이버 주입으로 전환할지 여부를 제어한다.
    private bool _useDrivenInput;

    //jump vars
    // 점프 상승 구간이 아직 유효한지 나타내며 중력/정점 처리 분기를 결정한다.
    private bool _isJumping;
    // 점프 컷 이후 빠른 낙하 단계인지 나타낸다.
    private bool _isFastFalling;
    // 일반 낙하 상태 진입 여부를 나타낸다.
    private bool _isFalling;
    // 빠른 낙하 보간에 사용되는 경과 시간이다.
    private float _fastFallTime;
    // 점프 버튼 해제 시점의 상향 속도 스냅샷이다.
    private float _fastFallReleaseSpeed;
    // 공중 점프를 소모한 횟수를 누적한다.
    private int _numberOfAirJumpsUsed;

    //apex vars
    // 점프 상승 속도를 0~1로 정규화한 정점 진행률로, 정점 보정(행타임/가감속)에 사용된다.
    private float _apexPoint;
    // 점프 정점 임계 구간에 머문 시간을 기록한다.
    private float _timePastApexThreshold;
    // 점프 정점 임계값을 통과했는지 나타낸다.
    private bool _isPastApexThreshold;

    //jump buffer vars
    // 점프 입력을 짧게 보존해 착지 직후에도 점프가 발동되도록 하는 버퍼 남은 시간이다.
    private float _jumpBufferTimer;
    // 점프 버퍼 구간 중 버튼 해제가 발생했는지 기록한다.
    private bool _jumpReleasedDuringBuffer;

    //coyote time vars
    // 발판을 막 벗어난 뒤에도 점프를 허용하기 위한 코요테 타임의 남은 시간이다.
    private float _coyoteTimer;

    //wall slide vars
    // 벽 접촉 상태에서 하강 속도를 제한하는 벽 슬라이드 모드 활성 여부이다.
    private bool _isWallSliding;
    // 벽 슬라이드 종료 후 낙하 전환 상태인지 나타낸다.
    private bool _isWallSlideFalling;

    //wall jump vars
    // 벽 점프 직후 짧은 구간에 전용 가속/감속 스탯을 적용할지 제어하는 플래그이다.
    private bool _useWallJumpMoveStats;
    // 벽 점프 동작이 현재 진행 중인지 나타낸다.
    private bool _isWallJumping;
    // 벽 점프 지속 시간을 누적한다.
    private float _wallJumpTime;
    // 벽 점프 중 점프 컷 빠른 낙하 상태인지 나타낸다.
    private bool _isWallJumpFastFalling;
    // 벽 점프 상승이 끝나고 낙하 단계인지 나타낸다.
    private bool _isWallJumpFalling;
    // 벽 점프 빠른 낙하 보간 경과 시간이다.
    private float _wallJumpFastFallTime;
    // 벽 점프 해제 시점 속도값을 저장한다.
    private float _wallJumpFastFallReleaseSpeed;
    // 마지막으로 접촉한 벽의 방향을 보존한다.
    private int _lastWallDir;

    // 벽 이탈 직후 점프 허용 시간(코요테 타임)이다.
    private float _wallJumpCoyoteTimer;

    // 벽 점프 정점 진행률(0~1) 계산값이다.
    private float _wallJumpApexPoint;
    // 벽 점프 정점 임계 구간 경과 시간이다.
    private float _timePastWallJumpApexThreshold;
    // 벽 점프 정점 임계값 통과 여부를 나타낸다.
    private bool _isPastWallJumpApexThreshold;

    //dash vars
    // 현재 대시 물리(방향 고정/속도 우선 적용)가 활성화된 상태인지 외부에 노출한다.
    public bool IsDashing { get; private set; }
    // 공중에서 시작된 대시인지 구분한다.
    private bool _isAirDashing;
    // 현재 대시의 남은 지속 시간이다.
    private float _dashTimer;
    // 착지 직후 대시 보정에 쓰이는 타이머이다.
    private float _dashOnGroundTimer;
    // 현재 공중 체류 중 사용한 대시 횟수이다.
    private int _numberOfDashesUsed;
    // 실제로 적용되는 대시 방향 벡터이다.
    private Vector2 _dashDirection;
    // 대시 종료 후 빠른 낙하 보정 상태인지 나타낸다.
    private bool _isDashFastFalling;
    // 대시 후 빠른 낙하 보간 시간이다.
    private float _dashFastFallTime;
    // 대시 종료 시점 수직 속도 스냅샷이다.
    private float _dashFastFallReleaseSpeed;
    // 대시 입력 버퍼 유효 시간을 관리한다.
    private float _dashBufferTimer;

    // 입력으로 의도한 대시 방향(정규화 전 포함)을 저장한다.
    private Vector2 _dashIntentDirection;
    // 낙하 직후에도 대시를 허용하는 코요테 타임이다.
    private float _dashCoyoteTimer;
    // 입력 후 실제 대시 시작 전 지연 시간을 관리한다.
    private float _dashDelayTimer;
    // 다음 대시를 허용하기까지 남은 쿨타임(초)이다.
    private float _dashCooldownTimer;

    //head bump slide vars
    // 디버그 점프 측정 중 기록된 최고 Y값으로 실제 도달 높이 계산에 사용된다.
    private float _debugMaxHeightY;
    // 디버그 점프 궤적 측정이 활성화되었는지 나타낸다.
    private bool _debugTrackingJump;
    // 디버그 점프 시작 높이(Y) 기록값이다.
    private float _debugJumpStartY;

    //slopes
    // 대시를 경사면 법선 기준으로 보정해 수행 중인지 나타내는 상태 플래그이다.
    private bool _isPerformingSlopeDash;
    // 경사면 대시 시 고정 적용할 이동 각도이다.
    private float _slopeDashAngle;
    // 비주얼이 보간될 목표 회전값이다.
    private Quaternion _targetRotation = Quaternion.identity;

    //platforms
    // 최근 탑승 발판의 속도를 저장해 이탈 직후 관성/런치 보정에 재사용한다.
    private Vector2 _storedPlatformVelocity;
    // 발판 이탈 후 관성을 유지할 남은 시간이다.
    private float _platformMomentumRetentionTimer;

    //visuals
    // 시각 보간(회전/위치 보정)을 담당하는 컴포넌트이다.
    private VisualInterpolator _visuals;

    // 현재 달리기 입력 유지 여부를 외부에 노출한다.
    public bool IsRunning => InputManager.RunIsHeld;
    // 점프 상승 단계 진행 여부를 외부 액션 동기화 로직에 제공한다.
    public bool IsJumpingState => _isJumping;
    // 일반 낙하 단계 진행 여부를 외부 액션 동기화 로직에 제공한다.
    public bool IsFallingState => _isFalling;
    // 벽 슬라이드 단계 진행 여부를 외부 액션 동기화 로직에 제공한다.
    public bool IsWallSlidingState => _isWallSliding;
    // 벽 점프 단계 진행 여부를 외부 액션 동기화 로직에 제공한다.
    public bool IsWallJumpingState => _isWallJumping;

    // 필수 컴포넌트 참조를 캐싱하고 초기 바라보는 방향을 설정한다.
    private void Awake()
    {
        IsFacingRight = true;

        if (_visualsTransform == null)
        {
            Debug.LogWarning($"[PlayerMovement] Visuals Transform이 비어 있어 방향 반전을 적용할 수 없습니다. object={name}", this);
        }

        _rb = GetComponent<Rigidbody2D>();
        Controller = GetComponent<MovementController>();
        _visuals = GetComponentInChildren<VisualInterpolator>();
    }

    // 활성화 시 압사 이벤트를 구독한다.
    private void OnEnable()
    {
        Controller.OnCrush += HandleCrush;
    }

    // 비활성화 시 압사 이벤트 구독을 해제한다.
    private void OnDisable()
    {
        Controller.OnCrush -= HandleCrush;
    }

    // 입력을 수집해 물리 프레임에서 소비할 버퍼 플래그를 설정한다.
    private void Update()
    {
        if (_useDrivenInput)
        {
            _moveInput = _drivenMoveInput;
            _runHeld = _drivenRunHeld;
            if (_drivenJumpPressed) _jumpPressed = true;
            if (_drivenJumpReleased) _jumpReleased = true;
            if (_drivenDashPressed) _dashPressed = true;

            _drivenJumpPressed = false;
            _drivenJumpReleased = false;
            _drivenDashPressed = false;
            return;
        }

        _moveInput = InputManager.Movement;
        _runHeld = InputManager.RunIsHeld;
        if (InputManager.JumpWasPressed) _jumpPressed = true;
        if (InputManager.JumpWasReleased) _jumpReleased = true;
        if (InputManager.DashWasPressed) _dashPressed = true;
    }

    /// <summary>
    /// 입력 소스를 외부 드라이버(PlayerInputDriver) 기반으로 전환하거나 해제합니다.
    /// </summary>
    public void SetDrivenInputEnabled(bool enabled)
    {
        _useDrivenInput = enabled;
    }

    /// <summary>
    /// 외부 드라이버가 수집한 프레임 입력을 PlayerMovement에 주입합니다.
    /// </summary>
    public void SetDrivenInputFrame(Vector2 movement, bool runHeld, bool jumpPressed, bool jumpReleased, bool dashPressed)
    {
        _drivenMoveInput = movement;
        _drivenRunHeld = runHeld;
        _drivenJumpPressed |= jumpPressed;
        _drivenJumpReleased |= jumpReleased;
        _drivenDashPressed |= dashPressed;
    }

    // 경사면 옵션이 켜진 경우 비주얼 회전을 마지막 단계에서 보간한다.
    private void LateUpdate()
    {
        if (MoveStats.MatchVisualsToSlope)
        {
            RotateVisualTarget(Time.deltaTime);
        }
    }

    // 센서 갱신 → 상태 판정 → 속도 계산 → 이동 적용 순서로 물리 루프를 수행한다.
    private void FixedUpdate()
    {
        Controller.PollSensors(Velocity * Time.fixedDeltaTime);

        CountTimers(Time.fixedDeltaTime);

        JumpChecks();
        LandCheck();
        WallJumpCheck();
        WallSlideCheck();
        DashCheck();
        VelocityReset();
        PreventWallStick();
        PreventCeilingStick();
        CalculateRunOffMomentum();

        CalculateTargetRotation();

        HandleHorizontalMovement(Time.fixedDeltaTime);
        Jump(Time.fixedDeltaTime);
        WallSlide(Time.fixedDeltaTime);
        WallJump(Time.fixedDeltaTime);
        Dash(Time.fixedDeltaTime);
        Fall(Time.fixedDeltaTime);
        HandleSlide(Time.fixedDeltaTime);

        ClampVelocity();

        Controller.Move(Velocity * Time.fixedDeltaTime);

        TrackJump();

        //reset input bools
        _jumpPressed = false;
        _jumpReleased = false;
        _dashPressed = false;

        _visuals.UpdatePhysicsState();
    }

    // 디버그 점프 최대 높이를 추적하고 정점 도달 시 로그를 출력한다.
    private void TrackJump()
    {
        if (_debugTrackingJump)
        {
            if (_rb.position.y > _debugMaxHeightY)
            {
                _debugMaxHeightY = _rb.position.y;
            }

            if (Velocity.y <= 0)
            {
                float totalHeight = _debugMaxHeightY - _debugJumpStartY;
                string collisionNote = "";
                if (Controller.State.IsHittingCeiling)
                {
                    collisionNote = " (Hit Ceiling)";
                }

                Debug.Log($"[Jump Debug] REACHED APEX. Max Height: {totalHeight:F5} units.{collisionNote}");

                _debugTrackingJump = false;
            }
        }
    }

    // 상태별 최대 낙하 속도를 적용해 수직 속도 폭주를 방지한다.
    private void ClampVelocity()
    {
        if (Controller.IsSliding)
        {
            Velocity.y = Mathf.Clamp(Velocity.y, -MoveStats.SlideSpeed, 50f);
        }
        else if (IsDashing)
        {
            Velocity.y = Mathf.Clamp(Velocity.y, -50f, 50f);
        }
        else
        {
            float dynamicFallSpeed = MoveStats.MaxFallSpeed;

            if (_platformMomentumRetentionTimer > 0f)
            {
                if (_storedPlatformVelocity.y < -dynamicFallSpeed)
                {
                    dynamicFallSpeed = Mathf.Abs(_storedPlatformVelocity.y);
                }
            }

            Velocity.y = Mathf.Clamp(Velocity.y, -dynamicFallSpeed, 100f);
        }
    }

    // 에디터에서 점프 예상 궤적 기즈모를 그린다.
    private void OnDrawGizmos()
    {
        if (MoveStats.ShowWalkJumpArc)
        {
            DrawJumpArc(MoveStats.MaxWalkSpeed, Color.white);
        }
    }

    #region Movement

    // 좌우 입력과 가속/감속 값을 반영해 수평 속도를 계산한다.
    private void HandleHorizontalMovement(float timeStep)
    {
        if (!IsDashing)
        {
            float acceleration = Controller.IsGrounded() ? MoveStats.GroundAcceleration : MoveStats.AirAcceleration;
            float deceleration = Controller.IsGrounded() ? MoveStats.GroundDeceleration : MoveStats.AirDeceleration;

            if (_useWallJumpMoveStats)
            {
                acceleration = MoveStats.WallJumpMoveAcceleration;
                deceleration = MoveStats.WallJumpMoveDeceleration;
            }

            if (Mathf.Abs(_moveInput.x) >= MoveStats.MoveThreshold)
            {
                TurnCheck(_moveInput);

                float moveDirection = Mathf.Sign(_moveInput.x);
                float targetVelocityX = 0f;
                targetVelocityX = _runHeld ? moveDirection * MoveStats.MaxRunSpeed : moveDirection * MoveStats.MaxWalkSpeed;

                float t = Mathf.Clamp01(acceleration * timeStep);
                Velocity.x = Mathf.Lerp(Velocity.x, targetVelocityX, t);

                if (Mathf.Abs(Velocity.x - targetVelocityX) <= 0.01f)
                {
                    Velocity.x = targetVelocityX;
                }
            }
            else
            {
                float t = Mathf.Clamp01(deceleration * timeStep);
                Velocity.x = Mathf.Lerp(Velocity.x, 0, t);

                if (Mathf.Abs(Velocity.x) <= 0.01f)
                {
                    Velocity.x = 0f;
                }
            }
        }
    }

    // 벽을 향해 파고드는 수평 속도를 제거해 벽 끼임을 방지한다.
    private void PreventWallStick()
    {
        if (Controller.IsTouchingWall())
        {
            if ((Velocity.x > 0 && Controller.State.WallDirection == 1) || (Velocity.x < 0 && Controller.State.WallDirection == -1))
            {
                if (Controller.IsStep(Velocity))
                {
                    return;
                }

                Velocity.x = 0f;
            }
        }
    }

    // 천장 충돌 시 상승 속도를 제거해 천장 붙음 현상을 완화한다.
    private void PreventCeilingStick()
    {
        if (Controller.BumpedHead())
        {
            if (Velocity.y > 0)
            {
                bool isSlideableCeiling = MoveStats.JumpFollowSlopesWhenHeadTouching && Controller.State.CeilingAngle > 0;

                if (!IsDashing)
                {
                    Velocity.y = 0f;
                }
            }
        }
    }

    // 이동 발판에서 이탈한 직후 수평 관성을 반영한다.
    private void CalculateRunOffMomentum()
    {
        if (Controller.IsGrounded() || !Controller.State.WasGroundedLastFrame || _isJumping || _isWallJumping || Controller.PlatformFromLastFrame == null) return;

        if (_platformMomentumRetentionTimer > 0f)
        {
            Vector2 platformVel = _storedPlatformVelocity;

            float hBoost = platformVel.x * MoveStats.PlatformHorizontalMomentumMultiplier;
            Velocity.x += hBoost;

            if (platformVel.y < 0f)
            {
                Velocity.y += platformVel.y;
            }
            else if (Controller.PlatformFromLastFrame.LaunchVerticallyOnExit)
            {
                float vBoost = platformVel.y * MoveStats.VerticalLaunchMultiplierOnLaunchExit;
                Velocity.y += vBoost;
            }
        }
    }

    // 입력 방향과 현재 방향이 다르면 캐릭터를 반전시킨다.
    private void TurnCheck(Vector2 moveInput)
    {
        if (IsFacingRight && moveInput.x < 0)
        {
            Turn(false);
        }
        else if (!IsFacingRight && moveInput.x > 0)
        {
            Turn(true);
        }
    }

    // 스프라이트 스케일을 뒤집어 좌우 바라보는 방향을 전환한다.
    private void Turn(bool turnRight)
    {
        SetFacingDirection(turnRight);
    }

    /// <summary>
    /// 좌우 바라보는 방향 상태와 비주얼 반전을 함께 적용합니다.
    /// </summary>
    public void SetFacingDirection(bool isFacingRight)
    {
        if (_visualsTransform == null)
        {
            Debug.LogWarning($"[PlayerMovement] Visuals Transform이 없어 방향 반전을 적용할 수 없습니다. object={name}", this);
            return;
        }

        bool isVisualScaleFacingRight = _visualsTransform.localScale.x >= 0f;
        if (IsFacingRight == isFacingRight && isVisualScaleFacingRight == isFacingRight)
        {
            return;
        }

        IsFacingRight = isFacingRight;
        int multiplier = IsFacingRight ? 1 : -1;
        _visualsTransform.localScale = new Vector3(Mathf.Abs(_visualsTransform.localScale.x) * multiplier, _visualsTransform.localScale.y, _visualsTransform.localScale.z);
        FacingDirectionChanged?.Invoke(IsFacingRight);
    }

    // 지면 법선과 상태를 기준으로 비주얼 목표 회전을 계산한다.
    private void CalculateTargetRotation()
    {
        if (!MoveStats.MatchVisualsToSlope) return;

        Vector3 targetNormal = Controller.State.AveragedVisualNormal;
        float signedAngle = Vector2.SignedAngle(Vector2.up, targetNormal);
        float clampedAngle = Mathf.Clamp(signedAngle, -MoveStats.MaxVisualRotatingAngle, MoveStats.MaxVisualRotatingAngle);
        Quaternion slopeRotation = Quaternion.AngleAxis(clampedAngle, Vector3.forward);

        _targetRotation = slopeRotation;
    }

    // 목표 회전까지 비주얼을 시간 기반으로 부드럽게 보간한다.
    private void RotateVisualTarget(float timeStep)
    {
        _visualsTransform.rotation = Quaternion.Slerp(_visualsTransform.rotation, _targetRotation, MoveStats.SlopeRotationSpeed * timeStep);

        float angleDegrees = _visualsTransform.rotation.eulerAngles.z;
        if (angleDegrees > 180f)
        {
            angleDegrees -= 360f;
        }
        float angleRad = Mathf.Abs(angleDegrees * Mathf.Deg2Rad);

        float halfWidth = _coll.bounds.size.x / 2f;
        float halfHeight = _coll.bounds.size.y / 2f;

        float currentDistanceToBottom = (halfWidth * Mathf.Sin(angleRad)) + (halfHeight * Mathf.Cos(angleRad));

        float liftAmount = currentDistanceToBottom - halfHeight;

        _visuals.PivotOffset = new Vector3(0f, -liftAmount, 0f);
    }

    #endregion

    #region Land/Fall

    // 착지 여부를 판정하고 점프/대시/벽 상태를 초기화한다.
    private void LandCheck()
    {
        if (Controller.IsGrounded())
        {
            bool isGroundAWall = Controller.State.SlopeAngle >= MoveStats.MinAngleForWallSlide && Controller.State.SlopeAngle <= MoveStats.MaxAngleForWallSlide;

            if (isGroundAWall)
            {
                return;
            }

            //LANDED
            if ((_isJumping || _isFalling || _isWallJumpFalling || _isWallJumping || _isWallSlideFalling || _isWallSliding || _isDashFastFalling) && Velocity.y <= 0f)
            {
                ResetJumpValues();
                StopWallSlide();
                ResetWallJumpValues();
                ResetDashes();
                DashLand();

                Controller.UpdateSlopeMemory();
            }

            bool isStable = Controller.State.SlopeAngle <= MoveStats.MaxSlopeAngle;
            bool isWedged = Controller.State.IsAgainstWall;

            if (MoveStats.ResetAirJumpsOnMaxSlopeLand || isStable || isWedged)
            {
                _numberOfAirJumpsUsed = 0;
            }
        }
    }

    // 공중 비점프 상태에서 일반 중력을 적용해 낙하를 진행한다.
    private void Fall(float timeStep)
    {
        //NORMAL GRAVITY WHILE FALLING
        if (!Controller.IsGrounded() && !_isJumping && !_isWallSliding && !_isWallJumping && !IsDashing && !_isDashFastFalling)
        {
            if (!_isFalling)
            {
                _isFalling = true;
            }

            Velocity.y += MoveStats.Gravity * timeStep;
        }
    }

    // 착지 안정화를 위해 지면에서 최소 하향 속도를 유지한다.
    private void VelocityReset()
    {
        if (Controller.IsSliding) return;

        if (Controller.IsGrounded())
        {
            if (!IsSlideableSlope(Controller.State.SlopeAngle))
            {
                if (Velocity.y <= 0f)
                {
                    Velocity.y = -2f;
                }
            }
        }
    }

    #endregion

    #region Jump

    // 점프 관련 상태값을 기본값으로 되돌린다.
    private void ResetJumpValues()
    {
        _isJumping = false;
        _isFalling = false;
        _isFastFalling = false;
        _fastFallTime = 0f;
        _isPastApexThreshold = false;
    }

    // 점프 입력 버퍼·해제·코요테 타임을 평가해 점프 가능 상태를 갱신한다.
    private void JumpChecks()
    {
        //WHEN WE PRESS THE JUMP BUTTON
        if (_jumpPressed)
        {
            if (_isWallSlideFalling && _wallJumpCoyoteTimer >= 0f)
            {
                return;
            }
            else if (_isWallSliding || (Controller.IsTouchingWall() && (!Controller.IsGrounded() || Controller.IsSliding || Controller.State.IsAgainstSteepSlope)))
            {
                return;
            }

            _jumpBufferTimer = MoveStats.JumpBufferTime;
            _jumpReleasedDuringBuffer = false;
        }

        //WHEN WE RELEASE THE JUMP BUTTON
        if (_jumpReleased)
        {
            if (_jumpBufferTimer > 0f)
            {
                _jumpReleasedDuringBuffer = true;
            }

            if (_isJumping && Velocity.y > 0f)
            {
                if (_isPastApexThreshold)
                {
                    _isPastApexThreshold = false;
                    _isFastFalling = true;
                    _fastFallTime = MoveStats.TimeForUpwardsCancel;
                    Velocity.y = 0f;
                }
                else
                {
                    _isFastFalling = true;
                    _fastFallReleaseSpeed = Velocity.y;
                }
            }
        }

        //INITIATE JUMP WITH JUMP BUFFERING AND COYOTE TIME
        if (_jumpBufferTimer > 0f && !_isJumping && (Controller.IsGrounded() || _coyoteTimer > 0f) && (MoveStats.CanJumpOnMaxSlopes || Controller.State.SlopeAngle <= MoveStats.MaxSlopeAngle))
        {
            InitiateJump(0);

            if (_jumpReleasedDuringBuffer)
            {
                _isFastFalling = true;
                _fastFallReleaseSpeed = Velocity.y;
            }
        }

        //ACTUAL JUMP WITH DOUBLE JUMP
        else if (_jumpBufferTimer > 0f && !Controller.IsGrounded() && !Controller.IsTouchingWall())
        {
            if (AttemptWallJumpBuffer())
            {
                _jumpBufferTimer = 0f;
                return;
            }

            if (!IsDashing && (_isFalling || _isJumping || _isWallJumping || _isWallSlideFalling || _isAirDashing || _isDashFastFalling || Controller.IsSliding) && _numberOfAirJumpsUsed < MoveStats.NumberOfAirJumpsAllowed)
            {
                _isFastFalling = false;
                InitiateJump(1);

                if (_isDashFastFalling)
                {
                    _isDashFastFalling = false;
                }
            }
        }

        //handle air jump AFTER the coyote time has lapsed (take off an extra jump so we don't get a bonus jump)
        //AIR JUMP AFTER COYOTE TIME LAPSED
        else if (_jumpBufferTimer > 0f && _isFalling && !_isWallSlideFalling && _numberOfAirJumpsUsed < MoveStats.NumberOfAirJumpsAllowed)
        {
            InitiateJump(1);
            _isFastFalling = false;
        }
    }

    // 점프 시작 시 속도와 관련 상태를 초기화하고 상승을 시작한다.
    private void InitiateJump(int numberOfAirJumpsUsed)
    {
        if (!_isJumping)
        {
            _isJumping = true;
        }

        _jumpPressed = false;

        ResetWallJumpValues();

        _fastFallTime = 0f;
        _isFastFalling = false;
        _isPastApexThreshold = false;
        _jumpBufferTimer = 0f;
        _numberOfAirJumpsUsed += numberOfAirJumpsUsed;
        Velocity.y = MoveStats.InitialJumpVelocity;

        IsDashing = false;
        _isAirDashing = false;
        _isDashFastFalling = false;

        if (MoveStats.InheritPlatformMomentum && _platformMomentumRetentionTimer > 0f)
        {
            Vector2 platformVel = _storedPlatformVelocity;

            float hBoost = platformVel.x * MoveStats.PlatformHorizontalMomentumMultiplier;
            float vBoost = platformVel.y * MoveStats.PlatformVerticalMomentumMultiplier;

            vBoost = Mathf.Clamp(vBoost, 0f, MoveStats.MaxVerticalBoost);

            Velocity.x += hBoost;
            Velocity.y += vBoost;
        }

        if (MoveStats.DebugTrackJumpHeight)
        {
            _debugJumpStartY = _rb.position.y;
            _debugMaxHeightY = _rb.position.y;
            _debugTrackingJump = true;
        }
    }

    // 점프 상승/정점/점프컷/낙하 전환의 수직 속도를 프레임별 계산한다.
    private void Jump(float timeStep)
    {
        //APPLY GRAVITY WHILE JUMPING
        if (_isJumping)
        {
            //CHECK FOR HEAD BUMP
            if (Controller.BumpedHead())
            {
                if (MoveStats.JumpFollowSlopesWhenHeadTouching && Controller.State.CeilingAngle > 0f)
                {
                    Vector2 ceilingNormal = Controller.State.CeilingNormal;
                    Velocity = Velocity - (Vector2.Dot(Velocity, ceilingNormal) * ceilingNormal);
                }
                else
                {
                    Velocity.y = 0f;
                    _isFastFalling = true;
                }
            }

            //GRAVITY ON ASCENDING
            if (Velocity.y >= 0f)
            {
                //APEX CONTROLS
                _apexPoint = Mathf.InverseLerp(MoveStats.InitialJumpVelocity, 0f, Velocity.y);

                if (_apexPoint > MoveStats.ApexThreshold)
                {
                    if (!_isPastApexThreshold)
                    {
                        _isPastApexThreshold = true;
                        _timePastApexThreshold = 0f;
                    }

                    if (_isPastApexThreshold)
                    {
                        _timePastApexThreshold += timeStep;
                        if (_timePastApexThreshold < MoveStats.ApexHangTime)
                        {
                            Velocity.y = 0f;
                        }
                        else
                        {
                            Velocity.y = -0.01f;
                        }
                    }
                }
                //GRAVITY ON ASCENDING BUT NOT PAST APEX THRESHOLD
                else if (!_isFastFalling)
                {
                    Velocity.y += MoveStats.Gravity * timeStep;
                    if (_isPastApexThreshold)
                    {
                        _isPastApexThreshold = false;
                    }
                }
            }
            //GRAVITY ON DESCENDING
            else if (!_isFastFalling)
            {
                Velocity.y += MoveStats.Gravity * MoveStats.GravityOnReleaseMultiplier * timeStep;
            }
            else if (Velocity.y < 0f)
            {
                if (!_isFalling)
                {
                    _isFalling = true;
                }
            }
        }

        //JUMP CUT
        if (_isFastFalling)
        {
            if (_fastFallTime >= MoveStats.TimeForUpwardsCancel)
            {
                Velocity.y += MoveStats.Gravity * MoveStats.GravityOnReleaseMultiplier * timeStep;
            }
            else if (_fastFallTime < MoveStats.TimeForUpwardsCancel)
            {
                Velocity.y = Mathf.Lerp(_fastFallReleaseSpeed, 0f, (_fastFallTime / MoveStats.TimeForUpwardsCancel));
            }

            _fastFallTime += timeStep;
        }
    }

    #endregion

    #region Wall Slide

    // 벽 슬라이드 진입/유지/해제 조건을 판정한다.
    private void WallSlideCheck()
    {
        bool isTouchingSideWall = Controller.IsTouchingWall();
        bool isSideWallAngle = Controller.State.WallAngle >= MoveStats.MinAngleForWallSlide && Controller.State.WallAngle <= MoveStats.MaxAngleForWallSlide;

        if (isTouchingSideWall && !MoveStats.CanWallSlideFacingAwayFromWall)
        {
            int facingDir = IsFacingRight ? 1 : -1;
            if (facingDir != Controller.State.WallDirection)
            {
                isTouchingSideWall = false;
            }
        }

        if (!IsDashing && isTouchingSideWall && isSideWallAngle && !Controller.IsGrounded())
        {
            if (Velocity.y < 0f && !_isWallSliding)
            {
                ResetJumpValues();
                ResetWallJumpValues();
                ResetDashValues();

                if (MoveStats.ResetDashOnWallSlide)
                {
                    ResetDashes();
                }

                _isWallSlideFalling = false;
                _isWallSliding = true;

                if (MoveStats.ResetJumpsOnWallSlide)
                {
                    _numberOfAirJumpsUsed = 0;
                }
            }
        }
        else if (_isWallSliding && !isTouchingSideWall)
        {
            _isWallSlideFalling = true;

            if (_platformMomentumRetentionTimer > 0f)
            {
                Velocity.y += _storedPlatformVelocity.y;
                Velocity.x += _storedPlatformVelocity.x;
            }

            StopWallSlide();
        }
        else
        {
            StopWallSlide();
        }
    }

    // 벽 슬라이드 상태와 관련 플래그를 정리한다.
    private void StopWallSlide()
    {
        if (_isWallSliding)
        {
            _isWallSliding = false;
        }
    }

    // 벽 슬라이드 중 목표 하강 속도로 감속 보간한다.
    private void WallSlide(float timeStep)
    {
        if (_isWallSliding)
        {
            Velocity.y = Mathf.Lerp(Velocity.y, -MoveStats.WallSlideSpeed, MoveStats.WallSlideDecelerationSpeed * timeStep);
        }
    }

    #endregion

    #region Wall Jump

    // 벽 근처 입력 버퍼를 검사해 벽 점프 가능 여부를 반환한다.
    private bool AttemptWallJumpBuffer()
    {
        if (MoveStats.WallJumpInputBufferDistance <= 0f) return false;

        int[] directionsToCheck;

        if (MoveStats.CanWallSlideFacingAwayFromWall)
        {
            directionsToCheck = new int[] { 1, -1 };
        }
        else
        {
            directionsToCheck = new int[] { IsFacingRight ? 1 : -1 };
        }

        foreach (int dir in directionsToCheck)
        {
            Vector2 direction = Vector2.right * dir;
            Vector2 origin = _coll.bounds.center;
            Vector2 size = _coll.bounds.size;
            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, direction, MoveStats.WallJumpInputBufferDistance, MoveStats.GroundLayer);

            #region Debug Visualization

            if (MoveStats.DebugShowWallJumpBufferBox)
            {
                float duration = 0.5f;
                float drawDistance = hit ? hit.distance : MoveStats.WallJumpInputBufferDistance;
                bool isValidHit = false;

                if (hit)
                {
                    float angle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.up));
                    if (angle >= MoveStats.MinAngleForWallSlide && angle <= MoveStats.MaxAngleForWallSlide)
                    {
                        isValidHit = true;
                    }
                }

                Color debugColor = isValidHit ? Color.cyan : Color.red;

                DrawDebugBox(origin + (direction * drawDistance), size, debugColor, duration);
                Debug.DrawLine(origin, origin + (direction * drawDistance), debugColor, duration);
            }

            #endregion

            if (hit)
            {
                float wallAngle = Mathf.Round(Vector2.Angle(hit.normal, Vector2.up));
                bool isValidWall = wallAngle >= MoveStats.MinAngleForWallSlide && wallAngle <= MoveStats.MaxAngleForWallSlide;

                if (isValidWall)
                {
                    var platform = hit.collider.GetComponent<IVelocityInheritable>();
                    if (platform != null)
                    {
                        _storedPlatformVelocity = platform.GetVelocity();
                        _platformMomentumRetentionTimer = MoveStats.PlatformMomentumRetentionTime;
                    }

                    _lastWallDir = -(int)Mathf.Sign(hit.normal.x);

                    InitiateWallJump();
                    return true;
                }
            }
        }

        return false;
    }

    // 벽 점프 시작 조건과 버퍼/코요테 타이밍을 판정한다.
    private void WallJumpCheck()
    {
        if (ShouldWallJumpCoyote())
        {
            _wallJumpCoyoteTimer = MoveStats.WallJumpCoyoteTime;
        }

        //wall jump fast falling
        if (_jumpReleased && !_isWallSliding && !Controller.IsTouchingWall() && _isWallJumping)
        {
            if (Velocity.y > 0f)
            {
                if (_isPastWallJumpApexThreshold)
                {
                    _isPastWallJumpApexThreshold = false;
                    _isWallJumpFastFalling = true;
                    _wallJumpFastFallTime = MoveStats.TimeForUpwardsCancel;

                    Velocity.y = 0f;
                }
                else
                {
                    _isWallJumpFastFalling = true;
                    _wallJumpFastFallReleaseSpeed = Velocity.y;
                }
            }
        }

        //actual jump with post wall jump buffer time
        if (_jumpPressed && _wallJumpCoyoteTimer > 0f)
        {
            InitiateWallJump();
        }
    }

    // 벽 점프 방향·속도를 확정하고 벽 점프 상태를 시작한다.
    private void InitiateWallJump()
    {
        if (!_isWallJumping)
        {
            _isWallJumping = true;
            _useWallJumpMoveStats = true;
        }

        _jumpPressed = false;

        StopWallSlide();
        ResetJumpValues();
        _wallJumpTime = 0f;
        _numberOfAirJumpsUsed = 0;

        float calculatedBaseX = Mathf.Abs(MoveStats.WallJumpDirection.x) * -_lastWallDir;

        Velocity.y = MoveStats.InitialWallJumpVelocity;
        Velocity.x = calculatedBaseX;

        if (MoveStats.InheritPlatformMomentum && _platformMomentumRetentionTimer > 0f)
        {
            Vector2 platformVel = _storedPlatformVelocity;

            float hBoost = platformVel.x * MoveStats.PlatformHorizontalMomentumMultiplier;
            float vBoost = platformVel.y * MoveStats.PlatformVerticalMomentumMultiplier;

            vBoost = Mathf.Clamp(vBoost, 0f, MoveStats.MaxVerticalBoost);

            if (Mathf.Sign(hBoost) != Mathf.Sign(calculatedBaseX) && Mathf.Abs(hBoost) > 0.01f)
            {
                hBoost = 0f;
            }

            Velocity.x += hBoost;
            Velocity.y += vBoost;
        }
    }

    // 벽 점프 중 시간 경과에 따른 수평·수직 속도를 갱신한다.
    private void WallJump(float timeStep)
    {
        //APPLY WALL JUMP GRAVITY
        if (_isWallJumping)
        {
            //TIME TO TAKE OVER MOVEMENT CONTROLS WHILE WALL JUMPING
            _wallJumpTime += timeStep;
            if (_wallJumpTime >= MoveStats.TimeTillJumpApex)
            {
                _useWallJumpMoveStats = false;
            }

            //HIT HEAD
            if (Controller.BumpedHead())
            {
                if (MoveStats.JumpFollowSlopesWhenHeadTouching && Controller.State.CeilingAngle > 0f)
                {
                    Vector2 ceilingNormal = Controller.State.CeilingNormal;
                    Velocity = Velocity - (Vector2.Dot(Velocity, ceilingNormal) * ceilingNormal);
                }
                else
                {
                    Velocity.y = 0f;
                    _isWallJumpFastFalling = true;
                    _useWallJumpMoveStats = false;
                }
            }

            //GRAVITY IN ASCENDING
            if (Velocity.y >= 0f)
            {
                //APEX CONTROLS
                _wallJumpApexPoint = Mathf.InverseLerp(MoveStats.WallJumpDirection.y, 0f, Velocity.y);

                if (_wallJumpApexPoint > MoveStats.ApexThreshold)
                {
                    if (!_isPastWallJumpApexThreshold)
                    {
                        _isPastWallJumpApexThreshold = true;
                        _timePastWallJumpApexThreshold = 0f;
                    }

                    if (_isPastWallJumpApexThreshold)
                    {
                        _timePastWallJumpApexThreshold += timeStep;
                        if (_timePastWallJumpApexThreshold < MoveStats.ApexHangTime)
                        {
                            Velocity.y = 0f;
                        }
                        else
                        {
                            Velocity.y = -0.01f;
                        }
                    }
                }
                //GRAVITY IN ASCENDING BUT NOT PAST APEX THRESHOLD
                else if (!_isWallJumpFastFalling)
                {
                    Velocity.y += MoveStats.WallJumpGravity * timeStep;

                    if (_isPastWallJumpApexThreshold)
                    {
                        _isPastWallJumpApexThreshold = false;
                    }
                }
            }
            //GRAVITY ON DESENDING
            else if (!_isWallJumpFastFalling)
            {
                Velocity.y += MoveStats.WallJumpGravity * timeStep;
            }
            else if (Velocity.y < 0f)
            {
                if (!_isWallJumpFalling)
                {
                    _isWallJumpFalling = true;
                }
            }
        }

        //HANDLE WALL JUMP CUT TIME
        if (_isWallJumpFastFalling)
        {
            if (_wallJumpFastFallTime >= MoveStats.TimeForUpwardsCancel)
            {
                Velocity.y += MoveStats.WallJumpGravity * MoveStats.WallJumpGravityOnReleaseMultiplier * timeStep;
            }
            else if (_wallJumpFastFallTime < MoveStats.TimeForUpwardsCancel)
            {
                Velocity.y = Mathf.Lerp(_wallJumpFastFallReleaseSpeed, 0f, (_wallJumpFastFallTime / MoveStats.TimeForUpwardsCancel));
            }

            _wallJumpFastFallTime += timeStep;
        }
    }

    // 벽에서 떨어진 직후 코요테 타임 벽 점프 허용 여부를 반환한다.
    private bool ShouldWallJumpCoyote()
    {
        bool isWallAngleValid = Controller.State.WallAngle >= MoveStats.MinAngleForWallSlide && Controller.State.WallAngle <= MoveStats.MaxAngleForWallSlide;

        if (Controller.IsTouchingWall() && isWallAngleValid || _isWallSliding)
        {
            if (Controller.State.WallDirection != 0)
            {
                _lastWallDir = Controller.GetWallDirection();
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    // 벽 점프 관련 타이머와 상태 플래그를 초기화한다.
    private void ResetWallJumpValues()
    {
        _isWallSlideFalling = false;
        _useWallJumpMoveStats = false;
        _isWallJumping = false;
        _isWallJumpFastFalling = false;
        _isWallJumpFalling = false;
        _isPastWallJumpApexThreshold = false;

        _wallJumpFastFallTime = 0f;
        _wallJumpTime = 0f;
    }

    #endregion

    #region Dash

    /// <summary>
    /// 대시 입력 버퍼를 검사하고, 대시 가능 조건 및 쿨타임을 만족할 때 대시를 시작한다.
    /// </summary>
    private void DashCheck()
    {
        if (_dashPressed)
        {
            _dashBufferTimer = MoveStats.DashBufferTime;
        }

        if (_dashBufferTimer > 0f)
        {
            if (_dashCooldownTimer > 0f)
            {
                return;
            }

            //ground dash
            if ((Controller.IsGrounded() || _dashCoyoteTimer > 0f) && _dashOnGroundTimer < 0 && !IsDashing)
            {
                InitiateDash();
                _dashBufferTimer = 0f;

                if (!Controller.IsGrounded())
                {
                    _numberOfDashesUsed--;
                }
            }
            //air dash
            else if (!Controller.IsGrounded() && !IsDashing && _numberOfDashesUsed < MoveStats.NumberOfDashes)
            {
                _isAirDashing = true;
                InitiateDash();
                _dashBufferTimer = 0f;
            }
        }
    }

    /// <summary>
    /// 입력/설정에 따라 대시 방향을 계산하고, 필요 시 좌/우 2방향으로 제한한다.
    /// </summary>
    private void CalculateDashDirection()
    {
        Vector2 input = _moveInput;
        //if (input.magnitude > 0.1f) input.Normalize();

        TurnCheck(input);

        if (input == Vector2.zero)
        {
            _dashDirection = IsFacingRight ? Vector2.right : Vector2.left;
            return;
        }

        if (MoveStats.DashLeftRightOnly)
        {
            Vector2 horizontalDashDirection = IsFacingRight ? Vector2.right : Vector2.left;

            if (Mathf.Abs(input.x) >= MoveStats.MoveThreshold)
            {
                horizontalDashDirection = Mathf.Sign(input.x) > 0f ? Vector2.right : Vector2.left;
            }

            _dashDirection = horizontalDashDirection;
            _dashIntentDirection = horizontalDashDirection;
            return;
        }

        bool isUpperHemisphere = input.y >= 0;
        Vector2 verticalDirection = isUpperHemisphere ? Vector2.up : Vector2.down;

        float verticalAngleTolerance = isUpperHemisphere ? MoveStats.DashUpwardAngleTolerance : MoveStats.DashDownwardAngleTolerance;
        float verticalThreshold = Mathf.Cos(verticalAngleTolerance * Mathf.Deg2Rad);

        float horizontalThreshold = Mathf.Cos(MoveStats.DashHorizontalAngleTolerance * Mathf.Deg2Rad);

        Vector2 finalDir = Vector2.zero;

        if (Vector2.Dot(input, verticalDirection) >= verticalThreshold)
        {
            finalDir = verticalDirection;
        }
        else if (Mathf.Abs(Vector2.Dot(input, Vector2.right)) >= horizontalThreshold)
        {
            finalDir = Mathf.Sign(input.x) == 1 ? Vector2.right : Vector2.left;
        }
        else
        {
            finalDir = new Vector2(Mathf.Sign(input.x), isUpperHemisphere ? 1 : -1).normalized;
        }

        _dashDirection = finalDir;
        _dashIntentDirection = finalDir;

        if (Controller.IsGrounded() && finalDir.y < 0 && finalDir.x != 0)
        {
            _dashDirection = new Vector2(Mathf.Sign(finalDir.x), 0);
        }
    }

    /// <summary>
    /// 대시 상태를 시작하고, 이동/점프 상태를 정리한 뒤 대시 쿨타임을 설정한다.
    /// </summary>
    private void InitiateDash()
    {
        _dashPressed = false;

        _numberOfDashesUsed++;
        IsDashing = true;

        _dashDelayTimer = MoveStats.DashFreezeTime;

        _dashTimer = 0f;
        _dashOnGroundTimer = MoveStats.TimeBtwDashesOnGround;
        _dashCooldownTimer = MoveStats.DashCooldown;

        ResetJumpValues();
        ResetWallJumpValues();
        StopWallSlide();

        Velocity.y = 0f;
        Velocity.x = 0f;
    }

    // 대시 진행/종료/후속 낙하 보정까지 포함한 대시 물리를 갱신한다.
    private void Dash(float timeStep)
    {
        if (IsDashing)
        {
            if (_dashDelayTimer > 0f)
            {
                CalculateDashDirection();
                Velocity.y = 0f;
                Velocity.x = 0f;

                _dashDelayTimer -= timeStep;

                if (_dashDelayTimer <= 0f)
                {
                    _isPerformingSlopeDash = Controller.IsGrounded() && Controller.State.SlopeAngle > 0f && _dashDirection.y == 0f && !_isJumping && Mathf.Sign(_dashDirection.x) != Mathf.Sign(Controller.State.SlopeNormal.x);
                    if (_isPerformingSlopeDash)
                    {
                        _slopeDashAngle = Controller.State.SlopeAngle;
                    }
                }
                else
                {
                    return;
                }
            }

            if (Controller.BumpedHead())
            {
                if (MoveStats.DashFollowSlopesWhenHeadTouching && Controller.State.CeilingAngle > 0f)
                {
                    Vector2 ceilingNormal = Controller.State.CeilingNormal;
                    Velocity = Velocity - (Vector2.Dot(Velocity, ceilingNormal) * ceilingNormal);
                }
                else
                {
                    if (MoveStats.CancelDashWhenYouHitCeiling)
                    {
                        Velocity.y = 0;
                        IsDashing = false;
                        _isAirDashing = false;
                        _dashTimer = MoveStats.DashTime;
                    }
                    else
                    {
                        //do nothing
                    }
                }
            }

            //stop the dash after the timer
            _dashTimer += timeStep;
            if (_dashTimer >= MoveStats.DashTime)
            {
                if (_dashIntentDirection.y >= 0)
                {
                    if (Mathf.Abs(Velocity.x) > MoveStats.MaxRunSpeed)
                    {
                        Velocity.x = MoveStats.MaxRunSpeed * Mathf.Sign(Velocity.x);
                    }
                }

                if (Controller.IsGrounded())
                {
                    ResetDashes();
                }

                _isAirDashing = false;
                IsDashing = false;

                if (!_isJumping && _isWallJumping)
                {
                    _dashFastFallTime = 0f;
                    _dashFastFallReleaseSpeed = Velocity.y;

                    if (!Controller.IsGrounded())
                    {
                        _isDashFastFalling = true;
                    }
                    else
                    {
                        Velocity.y = 0f;
                    }
                }

                return;
            }

            if (MoveStats.DashDirectionMatchesSlopeDirection && _isPerformingSlopeDash)
            {
                Velocity.x = Mathf.Cos(_slopeDashAngle * Mathf.Deg2Rad) * MoveStats.DashSpeed * _dashDirection.x;
                Velocity.y = Mathf.Sin(_slopeDashAngle * Mathf.Deg2Rad) * MoveStats.DashSpeed;
            }
            else
            {
                Velocity.x = MoveStats.DashSpeed * _dashDirection.x;

                if (_dashDirection.y != 0f || _isAirDashing)
                {
                    Velocity.y = MoveStats.DashSpeed * _dashDirection.y;
                }
                else if (!_isJumping && _dashDirection.y == 0f)
                {
                    Velocity.y = -0.001f;
                }
            }

            #region Debug Dash Angle Visualization

            if (MoveStats.DebugShowDashAngle)
            {
                Vector2 drawOrigin = _coll.bounds.center;
                Vector2 drawDirection = Velocity.normalized;
                float drawLength = MoveStats.ExtraRayDebugDistance * 4f;

                Debug.DrawRay(drawOrigin, drawDirection * drawLength, Color.cyan);
            }

            #endregion
        }
        //HANDLE DASH CUT TIME
        else if (_isDashFastFalling)
        {
            if (Velocity.y > 0f)
            {
                if (_dashFastFallTime < MoveStats.DashTimeForUpwardsCancel)
                {
                    Velocity.y = Mathf.Lerp(_dashFastFallReleaseSpeed, 0f, (_dashFastFallTime / MoveStats.DashTimeForUpwardsCancel));
                }
                else if (_dashFastFallTime >= MoveStats.DashTimeForUpwardsCancel)
                {
                    Velocity.y += MoveStats.Gravity * MoveStats.DashGravityOnReleaseMultiplier * timeStep;
                }

                _dashFastFallTime += timeStep;
            }
            else
            {
                Velocity.y += MoveStats.Gravity * MoveStats.DashGravityOnReleaseMultiplier * timeStep;
            }
        }
    }

    // 대시 종료 후 대시 관련 상태값을 기본값으로 되돌린다.
    private void ResetDashValues()
    {
        _isDashFastFalling = false;
        _dashOnGroundTimer = -0.01f;

        _dashFastFallReleaseSpeed = 0f;
        _dashFastFallTime = 0f;
        _dashDirection = Vector2.zero;
        _isPerformingSlopeDash = false;
    }

    // 착지 시 대시 후처리 상태를 정리해 일반 이동으로 복귀시킨다.
    private void DashLand()
    {
        _isDashFastFalling = false;
        _dashOnGroundTimer = -0.01f;
        _dashFastFallReleaseSpeed = 0f;
        _dashFastFallTime = 0f;
        _isPerformingSlopeDash = false;
    }

    // 사용한 대시 횟수를 초기화해 재사용 가능 상태로 만든다.
    private void ResetDashes()
    {
        _numberOfDashesUsed = 0;
    }

    #endregion

    #region Slide

    // 슬라이드 중 경사면 중력 적용을 처리한다.
    private void HandleSlide(float timeStep)
    {
        if (Controller.IsSliding)
        {
            if (_isJumping) return;
            if (_isWallJumping) return;

            Velocity.y += MoveStats.Gravity * timeStep;
        }
    }

    #endregion

    #region Timers

    // 이동 관련 모든 타이머를 프레임 단위로 감소/갱신한다.
    private void CountTimers(float timeStep)
    {
        //jump buffer
        _jumpBufferTimer -= timeStep;

        //jump coyote time
        HandleCoyoteTimer(timeStep);

        //wall jump buffer timer
        _wallJumpCoyoteTimer -= timeStep;

        //dash timer
        HandleDashOnGroundTimer(timeStep);
        HandleDashCoyoteTimer(timeStep);
        HandleDashCooldownTimer(timeStep);

        //dash buffer timer
        _dashBufferTimer -= timeStep;

        //platform momentum timer
        ManagePlatformMomentum(timeStep);
    }

    // 지면 접지 여부에 따라 점프 코요테 타임을 충전/감소시킨다.
    private void HandleCoyoteTimer(float timeStep)
    {
        if (Controller.IsGrounded() && !Controller.IsSliding)
        {
            _coyoteTimer = MoveStats.JumpCoyoteTime;
        }
        else
        {
            _coyoteTimer -= timeStep;
        }
    }

    // 지상 상태에서 대시 지면 타이머를 감소시킨다.
    private void HandleDashOnGroundTimer(float timeStep)
    {
        if (Controller.IsGrounded() && !Controller.IsSliding)
        {
            _dashOnGroundTimer -= timeStep;
        }
    }

    // 지면 이탈 상태에 따라 대시 코요테 타이머를 갱신한다.
    private void HandleDashCoyoteTimer(float timeStep)
    {
        if (Controller.IsGrounded() && !Controller.IsSliding)
        {
            _dashCoyoteTimer = MoveStats.DashCoyoteTime;
        }
        else
        {
            _dashCoyoteTimer -= timeStep;
        }
    }

    /// <summary>
    /// 대시 전역 쿨타임 타이머를 감소시킨다.
    /// </summary>
    private void HandleDashCooldownTimer(float timeStep)
    {
        if (_dashCooldownTimer > 0f)
        {
            _dashCooldownTimer -= timeStep;
        }
    }

    // 최근 발판 속도를 저장하고 관성 유지 시간을 관리한다.
    private void ManagePlatformMomentum(float timeStep)
    {
        if (Controller.LastKnownPlatform != null)
        {
            _storedPlatformVelocity = Controller.LastKnownPlatform.GetVelocity();
            _platformMomentumRetentionTimer = MoveStats.PlatformMomentumRetentionTime;
        }
        else
        {
            if (_platformMomentumRetentionTimer > 0f)
            {
                _platformMomentumRetentionTimer -= timeStep;

                if (_platformMomentumRetentionTimer <= 0f)
                {
                    _storedPlatformVelocity = Vector2.zero;
                }
            }
        }
    }

    #endregion

    #region Crush

    // 압사 이벤트 발생 시 리스폰 위치로 플레이어를 이동시킨다.
    public void HandleCrush()
    {
        Debug.Log("crushed");
        _visuals.ForceTeleport(_respawnPoint.position);
    }

    #endregion

    #region Helper Methods

    // 주어진 경사 각도가 슬라이드 가능한 각도인지 판정한다.
    public bool IsSlideableSlope(float slopeAngle)
    {
        if (slopeAngle >= MoveStats.MaxSlopeAngle && slopeAngle < MoveStats.MinAngleForWallSlide)
        {
            return true;
        }

        return false;
    }

    // 주어진 각도가 보행 가능한 경사 범위인지 판정한다.
    public bool IsWalkableSlope(float angle)
    {
        return angle <= MoveStats.MaxSlopeAngle && angle < MoveStats.MinAngleForWallSlide;
    }

    // 주어진 각도가 벽 슬라이드 가능한 경사 범위인지 판정한다.
    public bool IsWallSlideable(float angle)
    {
        return angle >= MoveStats.MinAngleForWallSlide && angle <= MoveStats.MaxAngleForWallSlide;
    }

    #endregion

    #region Visualization

    // 현재 점프 설정 기준으로 예측 포물선을 기즈모로 그린다.
    private void DrawJumpArc(float moveSpeed, Color gizmoColor)
    {
        Vector2 startPosition = new Vector2(_coll.bounds.center.x, _coll.bounds.min.y);
        Vector2 previousPosition = startPosition;
        float speed = 0f;
        if (MoveStats.DrawRight)
        {
            speed = moveSpeed;
        }
        else
        {
            speed = -moveSpeed;
        }
        Vector2 velocity = new Vector2(speed, MoveStats.InitialJumpVelocity);

        Gizmos.color = gizmoColor;

        float timeStep = 2 * MoveStats.TimeTillJumpApex / MoveStats.ArcResolution; // time step for the simulation
        //float totalTime = (2 * MoveStats.TimeTillJumpApex) + MoveStats.ApexHangTime; // total time of the arc including hang time

        for (int i = 0; i < MoveStats.VisualizationSteps; i++)
        {
            float simulationTime = i * timeStep;
            Vector2 displacement;
            Vector2 drawPoint;

            if (simulationTime < MoveStats.TimeTillJumpApex) // Ascending
            {
                displacement = velocity * simulationTime + 0.5f * new Vector2(0, MoveStats.Gravity) * simulationTime * simulationTime;
            }
            else if (simulationTime < MoveStats.TimeTillJumpApex + MoveStats.ApexHangTime) // Apex hang time
            {
                float apexTime = simulationTime - MoveStats.TimeTillJumpApex;
                displacement = velocity * MoveStats.TimeTillJumpApex + 0.5f * new Vector2(0, MoveStats.Gravity) * MoveStats.TimeTillJumpApex * MoveStats.TimeTillJumpApex;
                displacement += new Vector2(speed, 0) * apexTime; // No vertical movement during hang time
            }
            else // Descending
            {
                float descendTime = simulationTime - (MoveStats.TimeTillJumpApex + MoveStats.ApexHangTime);
                displacement = velocity * MoveStats.TimeTillJumpApex + 0.5f * new Vector2(0, MoveStats.Gravity) * MoveStats.TimeTillJumpApex * MoveStats.TimeTillJumpApex;
                displacement += new Vector2(speed, 0) * MoveStats.ApexHangTime; // Horizontal movement during hang time
                displacement += new Vector2(speed, 0) * descendTime + 0.5f * new Vector2(0, MoveStats.Gravity) * descendTime * descendTime;
            }

            drawPoint = startPosition + displacement;

            if (MoveStats.StopOnCollision)
            {
                RaycastHit2D hit = Physics2D.Raycast(previousPosition, drawPoint - previousPosition, Vector2.Distance(previousPosition, drawPoint), MoveStats.GroundLayer);
                if (hit.collider != null)
                {
                    // If a hit is detected, stop drawing the arc at the hit point
                    Gizmos.DrawLine(previousPosition, hit.point);
                    break;
                }
            }

            Gizmos.DrawLine(previousPosition, drawPoint);
            previousPosition = drawPoint;
        }
    }

    // 디버그 박스를 지정 시간 동안 선으로 시각화한다.
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

    #endregion
}
