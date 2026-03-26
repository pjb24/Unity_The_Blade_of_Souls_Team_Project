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

    [Header("Inspector Test")]
    [Tooltip("Inspector 테스트에서 Persistent 로드 시 사용할 TriggerContext 문자열입니다.")]
    [SerializeField] private string _inspectorPersistentLoadTriggerContext = "Inspector.LoadPersistent"; // Inspector Persistent 로드 테스트에서 사용할 트리거 컨텍스트 문자열입니다.

    [Tooltip("Inspector 테스트에서 Session 로드 시 사용할 TriggerContext 문자열입니다.")]
    [SerializeField] private string _inspectorSessionLoadTriggerContext = "Inspector.LoadSession"; // Inspector Session 로드 테스트에서 사용할 트리거 컨텍스트 문자열입니다.

    [Tooltip("Inspector 테스트에서 Recovery 로드 시 사용할 TriggerContext 문자열입니다.")]
    [SerializeField] private string _inspectorRecoveryLoadTriggerContext = "Inspector.LoadRecovery"; // Inspector Recovery 로드 테스트에서 사용할 트리거 컨텍스트 문자열입니다.

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
        return TryLoadRecoveryWithDeferredRestore(E_SaveTriggerType.Death, "Recovery.Load");
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
        bool result = TryLoadChannel(E_SaveChannelType.Persistent, E_SaveTriggerType.Manual, "UI.LoadPersistent");
        LogUiLoadResultIfNeeded(E_SaveChannelType.Persistent, result);
        return result;
    }

    /// <summary>
    /// Session 채널 로드를 시도하고 성공 여부를 반환합니다.
    /// </summary>
    public bool TryLoadSessionNow()
    {
        bool result = TryLoadChannel(E_SaveChannelType.Session, E_SaveTriggerType.Manual, "UI.LoadSession");
        LogUiLoadResultIfNeeded(E_SaveChannelType.Session, result);
        return result;
    }

    /// <summary>
    /// Inspector ContextMenu에서 Persistent 채널 로드를 테스트합니다.
    /// </summary>
    [ContextMenu("Test Load/Persistent")]
    public void TestLoadPersistentFromInspector()
    {
        bool result = TryLoadChannel(E_SaveChannelType.Persistent, E_SaveTriggerType.Manual, _inspectorPersistentLoadTriggerContext);
        LogUiLoadResultIfNeeded(E_SaveChannelType.Persistent, result);
    }

    /// <summary>
    /// Inspector ContextMenu에서 Session 채널 로드를 테스트합니다.
    /// </summary>
    [ContextMenu("Test Load/Session")]
    public void TestLoadSessionFromInspector()
    {
        bool result = TryLoadChannel(E_SaveChannelType.Session, E_SaveTriggerType.Manual, _inspectorSessionLoadTriggerContext);
        LogUiLoadResultIfNeeded(E_SaveChannelType.Session, result);
    }

    /// <summary>
    /// Inspector ContextMenu에서 Recovery 채널 로드와 지연 복원을 테스트합니다.
    /// </summary>
    [ContextMenu("Test Load/Recovery")]
    public void TestLoadRecoveryFromInspector()
    {
        bool result = TryLoadRecoveryWithDeferredRestore(E_SaveTriggerType.Manual, _inspectorRecoveryLoadTriggerContext);
        LogUiLoadResultIfNeeded(E_SaveChannelType.Recovery, result);
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

    /// <summary>
    /// 지정 채널 로드를 공통 경로로 수행하고 성공 여부를 반환합니다.
    /// </summary>
    private bool TryLoadChannel(E_SaveChannelType channelType, E_SaveTriggerType triggerType, string triggerContext)
    {
        if (SaveCoordinator.Instance == null)
        {
            Debug.LogWarning($"[RecoveryService] SaveCoordinator가 없어 {channelType} Load를 수행할 수 없습니다.", this);
            return false;
        }

        return SaveCoordinator.Instance.LoadChannel(channelType, triggerType, triggerContext);
    }

    /// <summary>
    /// Recovery 채널 로드 후 지연 복원을 함께 적용합니다.
    /// </summary>
    private bool TryLoadRecoveryWithDeferredRestore(E_SaveTriggerType triggerType, string triggerContext)
    {
        bool recovered = TryLoadChannel(E_SaveChannelType.Recovery, triggerType, triggerContext); // Recovery 채널 로드 실행 결과입니다.
        if (!recovered)
        {
            return false;
        }

        StageSpawnResolver spawnResolver = FindAnyObjectByType<StageSpawnResolver>(); // Recovery 직후 플레이어를 체크포인트 위치로 재배치할 StageSpawnResolver 참조입니다.
        bool spawnResolved = spawnResolver != null && spawnResolver.TryResolveSpawnNow(); // 체크포인트/엔트리포인트 기반 즉시 스폰 해석 성공 여부입니다.

        BossEncounterStateSaveParticipant.ApplyDeferredArenaResetInScene();

        if (!spawnResolved)
        {
            GimmickStateSaveParticipant.ApplyDeferredRestoresInScene(GimmickRestoreRuleSet.RestoreTiming.AfterPlayerSpawn);
        }

        return true;
    }
}
