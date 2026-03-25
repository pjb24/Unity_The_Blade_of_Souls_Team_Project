using UnityEngine;

/// <summary>
/// 사망 후 복구 채널 로드를 호출하는 단순 서비스입니다.
/// </summary>
public class RecoveryService : MonoBehaviour
{
    [Tooltip("Enable 시 자동으로 Recovery 채널 복원을 시도할지 여부입니다.")]
    [SerializeField] private bool _loadRecoveryOnEnable = false; // 활성화 시 자동 복구 로드 실행 여부입니다.

    [Tooltip("UI 버튼 테스트 호출 결과를 로그로 출력할지 여부입니다.")]
    [SerializeField] private bool _logUiTestResult = true; // UI 테스트용 Load 호출 결과를 콘솔 로그로 출력할지 여부입니다.

    /// <summary>
    /// 활성화 시 설정에 따라 Recovery 채널 로드를 시도합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_loadRecoveryOnEnable)
        {
            TryRecoverNow();
        }
    }

    /// <summary>
    /// SaveCoordinator를 통해 Recovery 채널 복원을 시도합니다.
    /// </summary>
    public bool TryRecoverNow()
    {
        if (SaveCoordinator.Instance == null)
        {
            Debug.LogWarning("[RecoveryService] SaveCoordinator가 없어 복구를 수행할 수 없습니다.", this);
            return false;
        }

        return SaveCoordinator.Instance.TryRecoverAfterDeath();
    }

    /// <summary>
    /// Unity UI Button OnClick에서 호출할 수 있는 복구 진입 메서드입니다.
    /// </summary>
    public void RecoverNowFromUi()
    {
        TryRecoverNow();
    }

    /// <summary>
    /// Persistent 채널 로드를 시도하고 성공 여부를 반환합니다.
    /// </summary>
    public bool TryLoadPersistentNow()
    {
        if (SaveCoordinator.Instance == null)
        {
            Debug.LogWarning("[RecoveryService] SaveCoordinator가 없어 Persistent Load를 수행할 수 없습니다.", this);
            return false;
        }

        bool result = SaveCoordinator.Instance.LoadChannel(E_SaveChannelType.Persistent, E_SaveTriggerType.Manual, "UI.LoadPersistent");
        LogUiLoadResultIfNeeded(E_SaveChannelType.Persistent, result);
        return result;
    }

    /// <summary>
    /// Session 채널 로드를 시도하고 성공 여부를 반환합니다.
    /// </summary>
    public bool TryLoadSessionNow()
    {
        if (SaveCoordinator.Instance == null)
        {
            Debug.LogWarning("[RecoveryService] SaveCoordinator가 없어 Session Load를 수행할 수 없습니다.", this);
            return false;
        }

        bool result = SaveCoordinator.Instance.LoadChannel(E_SaveChannelType.Session, E_SaveTriggerType.Manual, "UI.LoadSession");
        LogUiLoadResultIfNeeded(E_SaveChannelType.Session, result);
        return result;
    }

    /// <summary>
    /// Unity UI Button OnClick에서 호출할 수 있는 Persistent Load 진입 메서드입니다.
    /// </summary>
    public void LoadPersistentFromUi()
    {
        TryLoadPersistentNow();
    }

    /// <summary>
    /// Unity UI Button OnClick에서 호출할 수 있는 Session Load 진입 메서드입니다.
    /// </summary>
    public void LoadSessionFromUi()
    {
        TryLoadSessionNow();
    }

    /// <summary>
    /// UI 테스트용 Load 호출 결과를 설정에 따라 로그로 출력합니다.
    /// </summary>
    private void LogUiLoadResultIfNeeded(E_SaveChannelType channelType, bool result)
    {
        if (!_logUiTestResult)
        {
            return;
        }

        Debug.Log($"[RecoveryService] UI Load Result channel={channelType}, success={result}", this);
    }
}
