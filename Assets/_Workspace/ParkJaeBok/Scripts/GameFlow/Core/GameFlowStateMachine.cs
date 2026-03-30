using System.Collections.Generic;

/// <summary>
/// GameFlow 상위 상태를 등록/전환/이벤트 라우팅하는 순수 C# 상태 머신입니다.
/// </summary>
public sealed class GameFlowStateMachine
{
    private readonly Dictionary<GameFlowState, IGameFlowState> _stateByType = new Dictionary<GameFlowState, IGameFlowState>(); // 상태 타입별 상태 객체 조회 맵입니다.
    private bool _isTransitioning; // 상태 전환 재진입 방지를 위한 전환 중 플래그입니다.

    public GameFlowState CurrentStateType { get; private set; } = GameFlowState.Boot; // 현재 활성 상태 타입입니다.
    public IGameFlowState CurrentState { get; private set; } // 현재 활성 상태 객체 참조입니다.

    /// <summary>
    /// 상태 객체를 타입 키로 등록합니다.
    /// </summary>
    public void RegisterState(IGameFlowState state)
    {
        if (state == null)
        {
            GameFlowLogger.Warning("RegisterState에 null state가 전달되었습니다.");
            return;
        }

        _stateByType[state.StateType] = state;
    }

    /// <summary>
    /// 초기 상태를 강제로 설정하고 Enter를 호출합니다.
    /// </summary>
    public bool Initialize(GameFlowState initialState, string reason)
    {
        if (!_stateByType.TryGetValue(initialState, out IGameFlowState state) || state == null)
        {
            GameFlowLogger.Warning($"초기 상태를 찾지 못했습니다. state={initialState}");
            return false;
        }

        CurrentStateType = initialState;
        CurrentState = state;
        CurrentState.Enter(reason);
        return true;
    }

    /// <summary>
    /// 현재 상태의 Tick을 호출합니다.
    /// </summary>
    public void Tick(float deltaTime)
    {
        CurrentState?.Tick(deltaTime);
    }

    /// <summary>
    /// 현재 상태에 이벤트를 전달합니다.
    /// </summary>
    public void DispatchEvent(in GameFlowEvent flowEvent)
    {
        CurrentState?.HandleEvent(flowEvent);
    }

    /// <summary>
    /// 지정 상태로 전환을 시도합니다.
    /// </summary>
    public bool TryTransitionTo(GameFlowState nextState, string reason)
    {
        if (_isTransitioning)
        {
            GameFlowLogger.Warning($"상태 전환 중 재요청을 무시합니다. current={CurrentStateType}, next={nextState}, reason={reason}");
            return false;
        }

        if (CurrentStateType == nextState)
        {
            GameFlowLogger.Warning($"동일 상태 전환 요청을 무시합니다. state={nextState}, reason={reason}");
            return false;
        }

        if (!_stateByType.TryGetValue(nextState, out IGameFlowState next) || next == null)
        {
            GameFlowLogger.Warning($"대상 상태를 찾지 못해 전환에 실패했습니다. next={nextState}, reason={reason}");
            return false;
        }

        _isTransitioning = true;

        GameFlowState previousStateType = CurrentStateType; // 전환 로그 출력에 사용할 이전 상태 타입입니다.
        IGameFlowState previousState = CurrentState; // Exit 호출에 사용할 이전 상태 객체 참조입니다.

        previousState?.Exit(reason);

        CurrentStateType = nextState;
        CurrentState = next;
        CurrentState.Enter(reason);

        _isTransitioning = false;

        GameFlowLogger.StateTransition(previousStateType, nextState, reason);
        return true;
    }
}
