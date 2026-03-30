/// <summary>
/// 게임 전체 루프를 제어하는 상위 상태 열거형입니다.
/// </summary>
public enum GameFlowState
{
    Boot = 0,
    Title = 1,
    Town = 2,
    StageLoading = 3,
    StagePlaying = 4,
    ReturnToTown = 5,
    Exiting = 6,
    ErrorRecovery = 7
}
