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
