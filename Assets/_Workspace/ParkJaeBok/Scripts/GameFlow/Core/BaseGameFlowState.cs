/// <summary>
/// GameFlow 상태 구현에서 공통 의존성과 기본 동작을 제공하는 베이스 클래스입니다.
/// </summary>
public abstract class BaseGameFlowState : IGameFlowState
{
    protected readonly GameFlowContext Context; // 상태 처리 중 공통으로 참조할 GameFlow 서비스 컨텍스트입니다.
    protected readonly GameFlowStateMachine StateMachine; // 상태 전환 요청을 전달할 상태 머신 참조입니다.

    public abstract GameFlowState StateType { get; }

    /// <summary>
    /// 베이스 상태 생성 시 필수 참조를 바인딩합니다.
    /// </summary>
    protected BaseGameFlowState(GameFlowContext context, GameFlowStateMachine stateMachine)
    {
        Context = context;
        StateMachine = stateMachine;
    }

    /// <summary>
    /// 기본 상태 진입 로직입니다.
    /// </summary>
    public virtual void Enter(string reason)
    {
        GameFlowLogger.Info($"State enter state={StateType}, reason={reason}");
    }

    /// <summary>
    /// 기본 상태 Tick 로직입니다.
    /// </summary>
    public virtual void Tick(float deltaTime)
    {
    }

    /// <summary>
    /// 기본 상태 이탈 로직입니다.
    /// </summary>
    public virtual void Exit(string reason)
    {
        GameFlowLogger.Info($"State exit state={StateType}, reason={reason}");
    }

    /// <summary>
    /// 기본 이벤트 처리 로직으로, 별도 처리되지 않은 이벤트를 Warning으로 기록합니다.
    /// </summary>
    public virtual void HandleEvent(in GameFlowEvent flowEvent)
    {
        GameFlowLogger.Warning($"Unhandled event. state={StateType}, event={flowEvent.EventType}, reason={flowEvent.Reason}");
    }
}
