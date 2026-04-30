/// <summary>
/// 보스 씬 전환 트리거의 활성화 방식 정의.
/// </summary>
public enum E_BossSceneTriggerActivationMode
{
    /// <summary>
    /// 유효한 플레이어가 트리거에 진입하는 즉시 씬 전환 요청을 시작한다.
    /// </summary>
    OnEnter = 0,

    /// <summary>
    /// 유효한 플레이어가 트리거 내부에 머무른 상태에서 Interact 입력을 눌렀을 때 씬 전환 요청을 시작한다.
    /// </summary>
    InteractInsideTrigger = 1
}
