/// <summary>
/// Boss scene transition trigger activation modes.
/// </summary>
public enum E_BossSceneTriggerActivationMode
{
    /// <summary>
    /// Starts the scene transition request as soon as a valid player enters the trigger.
    /// </summary>
    OnEnter = 0,

    /// <summary>
    /// Starts the scene transition request when a valid player presses Interact while staying inside the trigger.
    /// </summary>
    InteractInsideTrigger = 1
}
