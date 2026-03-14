using UnityEngine;

[CreateAssetMenu(menuName ="Player Movement")]
public class PlayerMovementStats : ScriptableObject
{
    [Header("Walk")]
    [Tooltip("이동 입력으로 걷기 상태로 전환되는 최소 입력 세기입니다.")]
    [Range(0f, 1f)] public float MoveThreshold = 0.25f;
    [Tooltip("걷기 상태에서의 최대 이동 속도입니다.")]
    [Range(1f, 100f)] public float MaxWalkSpeed = 12.5f;
    [Tooltip("지상에서 목표 속도까지 가속하는 속도입니다.")]
    [Range(0.25f, 50f)] public float GroundAcceleration = 5f;
    [Tooltip("지상에서 감속할 때 속도가 줄어드는 정도입니다.")]
    [Range(0.25f, 50f)] public float GroundDeceleration = 20f;
    [Tooltip("공중에서 목표 속도까지 가속하는 속도입니다.")]
    [Range(0.25f, 50f)] public float AirAcceleration = 5f;
    [Tooltip("공중에서 감속할 때 속도가 줄어드는 정도입니다.")]
    [Range(0.25f, 50f)] public float AirDeceleration = 5f;
    [Tooltip("벽 점프 후 공중 이동 가속도입니다.")]
    [Range(0.25f, 50f)] public float WallJumpMoveAcceleration = 5f;
    [Tooltip("벽 점프 후 공중 이동 감속도입니다.")]
    [Range(0.25f, 50f)] public float WallJumpMoveDeceleration = 5f;

    [Header("Run")]
    [Tooltip("달리기 상태에서의 최대 이동 속도입니다.")]
    [Range(1f, 100f)] public float MaxRunSpeed = 20f;

    [Header("Platforms")]
    [Tooltip("움직이는 플랫폼의 속도를 플레이어에게 상속할지 여부입니다.")]
    public bool InheritPlatformMomentum = true;
    [Tooltip("플랫폼에서 이탈한 뒤 관성을 유지하는 시간입니다.")]
    [Range(0f, 0.5f)] public float PlatformMomentumRetentionTime = 0.15f;
    [Tooltip("플랫폼 수평 관성에 적용되는 배율입니다.")]
    [Range(0f, 2f)] public float PlatformHorizontalMomentumMultiplier = 1f;
    [Tooltip("플랫폼 수직 관성에 적용되는 배율입니다.")]
    [Range(0f, 2f)] public float PlatformVerticalMomentumMultiplier = 1f;
    [Tooltip("플랫폼 관성으로 추가될 수 있는 최대 수직 속도입니다.")]
    [Range(0f, 100f)] public float MaxVerticalBoost = 10f;
    [Tooltip("플랫폼 이탈 시 수직 발사 속도에 곱해지는 배율입니다.")]
    [Range(1f, 5f)] public float VerticalLaunchMultiplierOnLaunchExit = 2f;

    [Header("Slopes")]
    [Tooltip("대시 방향을 경사면 방향에 맞출지 여부입니다.")]
    public bool DashDirectionMatchesSlopeDirection = true;
    [Tooltip("최대 경사면에서도 점프를 허용할지 여부입니다.")]
    public bool CanJumpOnMaxSlopes = false;
    [Tooltip("머리가 닿는 상황에서 점프가 경사면을 따르도록 할지 여부입니다.")]
    public bool JumpFollowSlopesWhenHeadTouching = true;
    [Tooltip("머리가 닿는 상황에서 대시가 경사면을 따르도록 할지 여부입니다.")]
    public bool DashFollowSlopesWhenHeadTouching = true;
    [Tooltip("지면으로 인식 가능한 최대 경사 각도입니다.")]
    [Range(0f, 90f)] public float MaxSlopeAngle = 70f;
    [Tooltip("경사면에서 미끄러질 때의 속도입니다.")]
    [Range(1f, 100f)] public float SlideSpeed = 30f;
    [Tooltip("경사면을 타고 이탈(run-off)하기 위한 최소 이동 속도입니다.")]
    [Range(1f, 100f)] public float SpeedForRunOff = 15f;
    [Tooltip("경사 이탈 판정에 필요한 최소 각도 변화량입니다.")]
    [Range(0f, 90f)] public float MinAngleDeltaForRunOff = 30f;
    [Tooltip("경사 이탈 판정에 적용할 최대 각도 변화량입니다.")]
    [Range(0f, 90f)] public float MaxAngleDeltaForRunOff = 60f;
    [Tooltip("착지 직후 추가 입력을 허용하는 유예 시간입니다.")]
    [Range(0.05f, 0.15f)] public float LandingGraceTime = 0.08f;
    [Tooltip("경사 곡률 누적값의 최대 한계입니다.")]
    [Range(10f, 90f)] public float MaxSlopeCurveAccumulation = 30f;
    [Tooltip("누적된 경사 곡률이 감소하는 속도입니다.")]
    [Range(10f, 100f)] public float SlopeCurveDecayRate = 90f;

    [Header("Slope Visual Rotation")]
    [Tooltip("캐릭터 비주얼 회전을 경사면에 맞출지 여부입니다.")]
    public bool MatchVisualsToSlope = true;
    [Tooltip("경사 평균 노멀 계산에 사용하는 레이 길이입니다.")]
    [Range(0.001f, 2f)] public float SlopeAveragedNormalsRayLength = 1f;
    [Tooltip("비주얼 경사 감지용 레이캐스트의 가로 폭입니다.")]
    [Range(0.1f, 5f)] public float VisualRaycastWidth = 1.5f;
    [Tooltip("비주얼이 경사 각도로 회전하는 속도입니다.")]
    [Range(0.05f, 100f)] public float SlopeRotationSpeed = 20f;
    [Tooltip("비주얼 회전에 허용되는 최대 각도입니다.")]
    [Range(0f, 70f)] public float MaxVisualRotatingAngle = 45f;

    [Header("Step & Vault")]
    [Tooltip("달리기 중에만 볼트를 허용할지 여부입니다.")]
    public bool OnlyVaultWhenRunning = true;
    [Tooltip("볼트 동작이 시작되는 최소 장애물 높이입니다.")]
    [Range(0.1f, 2f)] public float VaultMinHeight = 0.25f;
    [Tooltip("계단처럼 오를 수 있는 최대 높이입니다.")]
    [Range(0.12f, 2f)] public float StepMaxHeight = 1.15f;
    [Tooltip("계단 감지에 사용하는 레이 너비입니다.")]
    [Range(0.01f, 0.5f)] public float StepDetectionRayWidth = 0.1f;

    [Header("Grounded/Collision Checks")]
    [Tooltip("지면 판정에 사용할 레이어 마스크입니다.")]
    public LayerMask GroundLayer;

    [Header("Corner Correction")]
    [Tooltip("모서리 보정 기능 사용 여부입니다.")]
    public bool EnableCornerCorrection = true;
    [Tooltip("모서리 보정 탐지 폭입니다.")]
    [Range(0.01f, 1f)] public float CornerCorrectionWidth = 0.3f;
    [Tooltip("수평 모서리 보정 탐지 높이입니다.")]
    [Range(0.01f, 1f)] public float HorizontalCornerCorrectionHeight = 0.6f;
    [Tooltip("수평 보정 시 아래로 밀어낼 수 있는 최대 거리입니다.")]
    [Range(0.01f, 1f)] public float HorizontalPushDownMaximum = 0.4f;

    [Header("Jump")]
    [Tooltip("점프의 목표 높이입니다.")]
    public float JumpHeight = 6.5f;
    [Tooltip("점프 높이 보정 계수입니다.")]
    [Range(1f, 1.1f)] public float JumpHeightCompensationFactor = 1.054f;
    [Tooltip("점프 정점까지 도달하는 시간입니다.")]
    public float TimeTillJumpApex = 0.35f;
    [Tooltip("점프 버튼을 놓았을 때 적용되는 중력 배율입니다.")]
    [Range(0.01f, 5f)] public float GravityOnReleaseMultiplier = 2f;
    [Tooltip("낙하 시 도달 가능한 최대 하강 속도입니다.")]
    public float MaxFallSpeed = 26f;
    [Tooltip("허용되는 추가 공중 점프 횟수입니다.")]
    [Range(0, 5)] public int NumberOfAirJumpsAllowed = 1;

    [Header("Reset Jump Options")]
    [Tooltip("벽 슬라이드 시 점프 횟수를 초기화할지 여부입니다.")]
    public bool ResetJumpsOnWallSlide = true;
    [Tooltip("최대 경사면 착지 시 공중 점프 횟수를 초기화할지 여부입니다.")]
    public bool ResetAirJumpsOnMaxSlopeLand = false;

    [Header("Jump Cut")]
    [Tooltip("상승 점프를 캔슬할 때까지의 입력 허용 시간입니다.")]
    [Range(0.02f, 0.3f)] public float TimeForUpwardsCancel = 0.027f;

    [Header("Jump Apex")]
    [Tooltip("점프 정점으로 판정하는 속도 비율 임계값입니다.")]
    [Range(0.5f, 1f)] public float ApexThreshold = 0.97f;
    [Tooltip("점프 정점에서 잠시 머무는 시간입니다.")]
    [Range(0.01f, 1f)] public float ApexHangTime = 0.075f;

    [Header("Jump Buffer")]
    [Tooltip("점프 입력을 유효하게 보관하는 시간입니다.")]
    [Range(0f, 1f)] public float JumpBufferTime = 0.125f;

    [Header("Jump Coyote Time")]
    [Tooltip("지면 이탈 후 점프를 허용하는 코요테 타임입니다.")]
    [Range(0f, 1f)] public float JumpCoyoteTime = 0.1f;

    [Header("Wall Slide")]
    [Tooltip("벽을 등진 상태에서도 벽 슬라이드를 허용할지 여부입니다.")]
    public bool CanWallSlideFacingAwayFromWall = false;
    [Tooltip("벽 슬라이드 시 하강 속도입니다.")]
    [Min(0.01f)] public float WallSlideSpeed = 5f;
    [Tooltip("벽 슬라이드 속도로 감속하는 속도입니다.")]
    [Range(0.25f, 50f)] public float WallSlideDecelerationSpeed = 50f;
    [Tooltip("벽 슬라이드로 인식되는 최소 표면 각도입니다.")]
    [Range(70f, 90f)] public float MinAngleForWallSlide = 85f;
    [Tooltip("벽 슬라이드로 인식되는 최대 표면 각도입니다.")]
    [Range(90f, 135f)] public float MaxAngleForWallSlide = 95f;

    [Header("Wall Jump")]
    [Tooltip("벽 점프 시 적용되는 초기 방향과 세기입니다.")]
    public Vector2 WallJumpDirection = new Vector2(-20f, 6.5f);
    [Tooltip("벽 이탈 직후 벽 점프를 허용하는 시간입니다.")]
    [Range(0f, 1f)] public float WallJumpCoyoteTime = 0.125f;
    [Tooltip("벽 점프 입력을 미리 저장할 감지 거리입니다.")]
    [Range(0f, 0.5f)] public float WallJumpInputBufferDistance = 0.3f;
    [Tooltip("벽 점프 버튼 해제 시 적용되는 중력 배율입니다.")]
    [Range(0.01f, 5f)] public float WallJumpGravityOnReleaseMultiplier = 1f;

    [Header("Dash Feel")]
    [Tooltip("대시 시작 직전 잠깐 정지하는 연출 시간입니다.")]
    [Range(0f, 0.5f)] public float DashFreezeTime = 0.05f;

    [Header("Dash Coyote Time")]
    [Tooltip("지면 이탈 후 대시를 허용하는 코요테 타임입니다.")]
    [Range(0f, 1f)] public float DashCoyoteTime = 0.125f;

    [Header("Dash Cancel Time")]
    [Tooltip("대시 입력 해제 시 적용되는 중력 배율입니다.")]
    [Range(0.01f, 5f)] public float DashGravityOnReleaseMultiplier = 1f;
    [Tooltip("상향 대시를 캔슬할 때까지의 입력 허용 시간입니다.")]
    [Range(0.02f, 0.3f)] public float DashTimeForUpwardsCancel = 0.027f;

    [Header("Trackers")]
    [Tooltip("점프 높이 디버그 추적 기능 사용 여부입니다.")]
    public bool DebugTrackJumpHeight;

    [Header("Debug")]
    [Tooltip("지면 접지 상태 디버그 표시 여부입니다.")]
    public bool DebugShowIsGrounded;
    [Tooltip("머리 충돌 감지 레이 디버그 표시 여부입니다.")]
    public bool DebugShowHeadRays;
    [Tooltip("모서리 보정 레이 디버그 표시 여부입니다.")]
    public bool DebugShowCornerCorrectionRays;
    [Tooltip("벽 충돌 디버그 표시 여부입니다.")]
    public bool DebugShowWallHit;
    [Tooltip("벽 점프 버퍼 박스 디버그 표시 여부입니다.")]
    public bool DebugShowWallJumpBufferBox;
    [Tooltip("경사 하강 감지 레이 디버그 표시 여부입니다.")]
    public bool DebugShowDescendSlopeRay;
    [Tooltip("경사 노멀 벡터 디버그 표시 여부입니다.")]
    public bool DebugShowSlopeNormal;
    [Tooltip("대시 각도 디버그 표시 여부입니다.")]
    public bool DebugShowDashAngle;
    [Tooltip("디버그 레이 길이에 추가할 여유 거리입니다.")]
    [Range(0f, 1f)] public float ExtraRayDebugDistance = 0.25f;

    [Header("JumpVisualization Tool")]
    [Tooltip("걷기 점프 궤적 시각화 표시 여부입니다.")]
    public bool ShowWalkJumpArc = false;
    [Tooltip("달리기 점프 궤적 시각화 표시 여부입니다.")]
    public bool ShowRunJumpArc = false;
    [Tooltip("궤적 시각화 중 충돌 시 계산을 중단할지 여부입니다.")]
    public bool StopOnCollision = true;
    [Tooltip("점프 궤적을 오른쪽 방향으로 그릴지 여부입니다.")]
    public bool DrawRight = true;
    [Tooltip("점프 궤적 선분의 해상도입니다.")]
    [Range(5, 100)] public int ArcResolution = 20;
    [Tooltip("점프 궤적 계산 반복 횟수입니다.")]
    [Range(0, 500)] public int VisualizationSteps = 90;

    [Header("Dash")]
    [Tooltip("천장에 부딪혔을 때 대시를 즉시 취소할지 여부입니다.")]
    public bool CancelDashWhenYouHitCeiling = false;
    [Tooltip("한 번의 대시가 유지되는 시간입니다.")]
    [Range(0f, 1f)] public float DashTime = 0.11f;
    [Tooltip("대시 속도입니다.")]
    [Range(1f, 200f)] public float DashSpeed = 40f;
    [Tooltip("각 대시 시작 간격에 적용되는 전역 쿨타임(초)입니다.")]
    [Range(0f, 5f)] public float DashCooldown = 1.5f;
    [Tooltip("지상에서 연속 대시 간 최소 간격입니다.")]
    [Range(0f, 1f)] public float TimeBtwDashesOnGround = 0.225f;
    [Tooltip("벽 슬라이드 시 대시 횟수를 초기화할지 여부입니다.")]
    public bool ResetDashOnWallSlide = true;
    [Tooltip("연속으로 사용할 수 있는 대시 횟수입니다.")]
    [Range(0, 5)] public int NumberOfDashes = 2;
    [Tooltip("대시 입력을 유효하게 보관하는 시간입니다.")]
    [Range(0f, 1f)] public float DashBufferTime = 0.125f;
    [Tooltip("위쪽 대시로 인정되는 각도 허용 범위입니다.")]
    [Range(0f, 45f)] public float DashUpwardAngleTolerance = 22.5f;
    [Tooltip("아래쪽 대시로 인정되는 각도 허용 범위입니다.")]
    [Range(0f, 45f)] public float DashDownwardAngleTolerance = 22.5f;
    [Tooltip("수평 대시로 인정되는 각도 허용 범위입니다.")]
    [Range(0f, 45f)] public float DashHorizontalAngleTolerance = 22.5f;
    [Tooltip("대시 입력을 좌/우 2방향으로만 제한할지 여부입니다.")]
    public bool DashLeftRightOnly = false;

    //Jump
    // 점프 계산에 사용되는 중력 값이다.
    public float Gravity { get; private set; }
    // 점프 시작 시 적용되는 초기 수직 속도이다.
    public float InitialJumpVelocity { get; private set; }
    // 보정 계수가 반영된 점프 높이 값이다.
    public float AdjustedJumpHeight { get; private set; }

    //Wall Jump
    // 벽 점프 계산에 사용되는 중력 값이다.
    public float WallJumpGravity { get; private set; }
    // 벽 점프 시작 시 적용되는 초기 수직 속도이다.
    public float InitialWallJumpVelocity { get; private set; }
    // 보정 계수가 반영된 벽 점프 높이 값이다.
    public float AdjustedWallJumpHeight { get; private set; }

    // 인스펙터 값이 변경될 때 파생 물리값을 다시 계산한다.
    private void OnValidate()
    {
        CalculateValues();
    }

    // ScriptableObject가 활성화될 때 파생 물리값을 초기화한다.
    private void OnEnable()
    {
        CalculateValues();
    }

    // 점프와 벽 점프에 필요한 파생 물리값을 계산한다.
    private void CalculateValues()
    {
        //jump
        AdjustedJumpHeight = JumpHeight * JumpHeightCompensationFactor;
        Gravity = -(2f * AdjustedJumpHeight) / Mathf.Pow(TimeTillJumpApex, 2f);
        InitialJumpVelocity = Mathf.Abs(Gravity) * TimeTillJumpApex;

        //wall jump
        AdjustedWallJumpHeight = WallJumpDirection.y * JumpHeightCompensationFactor;
        WallJumpGravity = -(2f * AdjustedWallJumpHeight) / Mathf.Pow(TimeTillJumpApex, 2f);
        InitialWallJumpVelocity = Mathf.Abs(WallJumpGravity) * TimeTillJumpApex;
    }
}
