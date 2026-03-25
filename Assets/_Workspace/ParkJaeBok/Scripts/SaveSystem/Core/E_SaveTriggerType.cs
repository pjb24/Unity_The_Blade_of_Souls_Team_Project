/// <summary>
/// 저장이 발생한 트리거 종류를 표현하는 열거형입니다.
/// </summary>
public enum E_SaveTriggerType
{
    Manual = 0,
    SceneTransition = 1,
    Interval = 2,
    Pause = 3,
    FocusLost = 4,
    ApplicationQuit = 5,
    Death = 6
}
