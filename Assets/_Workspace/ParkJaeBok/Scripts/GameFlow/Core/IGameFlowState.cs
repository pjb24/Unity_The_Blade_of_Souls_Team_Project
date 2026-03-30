/// <summary>
/// GameFlow 상태 1개가 구현해야 할 라이프사이클/이벤트 처리 계약입니다.
/// </summary>
public interface IGameFlowState
{
    GameFlowState StateType { get; } // 현재 상태 객체가 표현하는 GameFlow 상태 타입입니다.

    /// <summary>
    /// 상태 진입 시 초기화/구독/가드 로직을 수행합니다.
    /// </summary>
    void Enter(string reason);

    /// <summary>
    /// 프레임 단위 상태 업데이트가 필요할 때 호출됩니다.
    /// </summary>
    void Tick(float deltaTime);

    /// <summary>
    /// 상태 이탈 시 정리/구독 해제 로직을 수행합니다.
    /// </summary>
    void Exit(string reason);

    /// <summary>
    /// 외부에서 전달되는 이벤트를 상태 내부 규칙으로 처리합니다.
    /// </summary>
    void HandleEvent(in GameFlowEvent flowEvent);
}
