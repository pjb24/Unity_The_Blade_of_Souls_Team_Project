/// <summary>
/// GameFlow 상태 머신에 전달되는 이벤트 타입 열거형입니다.
/// </summary>
public enum GameFlowEventType
{
    None = 0,
    SceneLoadStarted = 1,
    SceneLoaded = 2,
    SceneLoadFailed = 3,
    StartNewGameRequested = 4,
    ContinueRequested = 5,
    EnterStageRequested = 6,
    ReturnToTownRequested = 7,
    ExitRequested = 8,
    PlayerDead = 9,
    SaveFailed = 10,
    DeathRecoveryFailed = 11,
    TitleReturnStateMismatch = 12
}

/// <summary>
/// GameFlow 상태 처리에 사용되는 공통 이벤트 데이터입니다.
/// </summary>
public readonly struct GameFlowEvent
{
    public readonly GameFlowEventType EventType; // 상태 머신에서 분기할 이벤트 타입입니다.
    public readonly string Reason; // 디버깅/로그 추적용 이벤트 사유 문자열입니다.
    public readonly string SceneName; // 씬 관련 이벤트에서 전달하는 대상 씬 이름입니다.
    public readonly StageDefinition StageDefinition; // 스테이지 진입 이벤트에서 전달하는 대상 스테이지 정의입니다.

    /// <summary>
    /// 전달받은 값으로 GameFlow 이벤트를 생성합니다.
    /// </summary>
    public GameFlowEvent(GameFlowEventType eventType, string reason = "", string sceneName = "", StageDefinition stageDefinition = null)
    {
        EventType = eventType;
        Reason = reason;
        SceneName = sceneName;
        StageDefinition = stageDefinition;
    }
}
