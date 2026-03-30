/// <summary>
/// 부팅 상태 진입 시 초기 상태 전환을 수행하는 상태입니다.
/// </summary>
public sealed class GameFlowBootState : BaseGameFlowState
{
    public override GameFlowState StateType => GameFlowState.Boot;

    /// <summary>
    /// Boot 상태 인스턴스를 생성합니다.
    /// </summary>
    public GameFlowBootState(GameFlowContext context, GameFlowStateMachine stateMachine) : base(context, stateMachine)
    {
    }

    /// <summary>
    /// Boot 진입 시 Title 상태로 즉시 전환을 시도합니다.
    /// </summary>
    public override void Enter(string reason)
    {
        base.Enter(reason);
        StateMachine.TryTransitionTo(GameFlowState.Title, "Boot.Completed");
    }
}

/// <summary>
/// 타이틀 입력 대기 상태입니다.
/// </summary>
public sealed class GameFlowTitleState : BaseGameFlowState
{
    public override GameFlowState StateType => GameFlowState.Title;

    /// <summary>
    /// Title 상태 인스턴스를 생성합니다.
    /// </summary>
    public GameFlowTitleState(GameFlowContext context, GameFlowStateMachine stateMachine) : base(context, stateMachine)
    {
    }
}

/// <summary>
/// 마을 입력 대기 상태입니다.
/// </summary>
public sealed class GameFlowTownState : BaseGameFlowState
{
    public override GameFlowState StateType => GameFlowState.Town;

    /// <summary>
    /// Town 상태 인스턴스를 생성합니다.
    /// </summary>
    public GameFlowTownState(GameFlowContext context, GameFlowStateMachine stateMachine) : base(context, stateMachine)
    {
    }
}

/// <summary>
/// 스테이지 씬 로딩 진행 상태입니다.
/// </summary>
public sealed class GameFlowStageLoadingState : BaseGameFlowState
{
    public override GameFlowState StateType => GameFlowState.StageLoading;

    /// <summary>
    /// StageLoading 상태 인스턴스를 생성합니다.
    /// </summary>
    public GameFlowStageLoadingState(GameFlowContext context, GameFlowStateMachine stateMachine) : base(context, stateMachine)
    {
    }
}

/// <summary>
/// 스테이지 플레이 진행 상태입니다.
/// </summary>
public sealed class GameFlowStagePlayingState : BaseGameFlowState
{
    public override GameFlowState StateType => GameFlowState.StagePlaying;

    /// <summary>
    /// StagePlaying 상태 인스턴스를 생성합니다.
    /// </summary>
    public GameFlowStagePlayingState(GameFlowContext context, GameFlowStateMachine stateMachine) : base(context, stateMachine)
    {
    }
}

/// <summary>
/// 마을 복귀 전환 전용 상태입니다.
/// </summary>
public sealed class GameFlowReturnToTownState : BaseGameFlowState
{
    public override GameFlowState StateType => GameFlowState.ReturnToTown;

    /// <summary>
    /// ReturnToTown 상태 인스턴스를 생성합니다.
    /// </summary>
    public GameFlowReturnToTownState(GameFlowContext context, GameFlowStateMachine stateMachine) : base(context, stateMachine)
    {
    }
}

/// <summary>
/// 종료 처리 상태입니다.
/// </summary>
public sealed class GameFlowExitingState : BaseGameFlowState
{
    public override GameFlowState StateType => GameFlowState.Exiting;

    /// <summary>
    /// Exiting 상태 인스턴스를 생성합니다.
    /// </summary>
    public GameFlowExitingState(GameFlowContext context, GameFlowStateMachine stateMachine) : base(context, stateMachine)
    {
    }
}

/// <summary>
/// 예외/실패 상황에서 복구를 시도하는 상태입니다.
/// </summary>
public sealed class GameFlowErrorRecoveryState : BaseGameFlowState
{
    public override GameFlowState StateType => GameFlowState.ErrorRecovery;

    /// <summary>
    /// ErrorRecovery 상태 인스턴스를 생성합니다.
    /// </summary>
    public GameFlowErrorRecoveryState(GameFlowContext context, GameFlowStateMachine stateMachine) : base(context, stateMachine)
    {
    }

    /// <summary>
    /// 복구 상태 진입 시 Title로 폴백 전환을 시도합니다.
    /// </summary>
    public override void Enter(string reason)
    {
        base.Enter(reason);
        bool moved = StateMachine.TryTransitionTo(GameFlowState.Title, "ErrorRecovery.FallbackToTitle");
        if (!moved)
        {
            GameFlowLogger.Warning("ErrorRecovery에서 Title 폴백 전환에 실패했습니다.");
        }
    }
}
