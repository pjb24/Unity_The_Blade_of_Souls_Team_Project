/// <summary>
/// 액션 시스템을 사용하는 주체 타입(Player/Enemy/Boss/Item/DestructibleObject) 정의입니다.
/// </summary>
public enum E_ActionActorType
{
    Player,
    Enemy,
    Boss,
    Item,
    DestructibleObject,
}

/// <summary>
/// 액션 시스템에서 공통으로 사용할 수 있는 액션 타입 정의입니다.
/// </summary>
public enum E_ActionType
{
    None,
    Idle,
    Move,
    Jump,
    Land,
    Dash,
    Falling,
    WallSlide,
    WallJump,
    Slide,
    IdleBreakA,
    IdleBreakB,
    IdleBreakC,
    Attack,
    Hit,
    Die,
    Spawn,
    Activate,
    Use,
    Break,
    Open,
    Close,
    AttackCombo1,
    AttackCombo2,
    AttackCombo3,
    BossPatternIntro,
    BossPatternLoopA,
    BossPatternLoopB,
    BossPatternFinish,

    AttackAir,
    AttackDash,
    AttackWall,
}

/// <summary>
/// 액션 진행 단계(시작/진행/완료/취소) 정의입니다.
/// </summary>
public enum E_ActionPhase
{
    None,
    Start,
    Progress,
    Complete,
    Cancel,
}

/// <summary>
/// 액션 런타임 실행 상태 정의입니다.
/// </summary>
public enum E_ActionRunState
{
    None,
    Running,
    Completed,
    Cancelled,
}

/// <summary>
/// 액션 인터럽트 규칙 평가 결과를 정의하는 enum입니다.
/// </summary>
public enum E_ActionInterruptDecision
{
    UseDefault,
    Allow,
    Deny,
}

/// <summary>
/// 공격 입력 시점의 이동/상태 맥락을 비트 플래그로 표현한 enum입니다.
/// </summary>
[System.Flags]
public enum E_AttackContextFlags
{
    None = 0,
    Grounded = 1 << 0,
    Airborne = 1 << 1,
    Moving = 1 << 2,
    Jumping = 1 << 3,
    Falling = 1 << 4,
    Dashing = 1 << 5,
    WallSliding = 1 << 6,
    WallJumping = 1 << 7,
    Sliding = 1 << 8,
}
