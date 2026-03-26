using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스전 상태(페이즈/체력/처치/진입)를 저장/복원하는 participant입니다.
/// </summary>
public class BossEncounterStateSaveParticipant : MonoBehaviour, ISaveParticipant
{
    [Tooltip("저장 레코드에서 BossEncounterState participant를 식별하는 고유 ID입니다.")]
    [SerializeField] private string _participantId = "boss.encounter_state"; // BossEncounterState participant 식별자입니다.

    [Tooltip("보스전 런타임 상태를 제공할 BossEncounterRuntime 참조입니다.")]
    [SerializeField] private BossEncounterRuntime _bossEncounterRuntime; // 보스전 저장/복원 상태를 제공할 런타임 참조입니다.

    [Tooltip("보스 페이즈 복원 적용을 수행할 BossPhaseController 참조입니다.")]
    [SerializeField] private BossPhaseController _bossPhaseController; // 페이즈 복원 API를 제공할 컨트롤러 참조입니다.

    [Tooltip("보스 체력 상태 복원을 수행할 HealthComponent 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // 보스 체력 상태 저장/복원 대상 참조입니다.

    [Tooltip("Recovery 복원 시 RecoveryPolicy의 Boss RuleSet을 따를지 여부입니다.")]
    [SerializeField] private bool _respectRecoveryPolicy = true; // 복구 채널 복원 시 보스 정책 적용 여부입니다.

    private static readonly List<string> _pendingArenaResetEncounterIds = new List<string>(); // Recovery 후 아레나 초기화를 지연 적용할 보스전 ID 목록입니다.

    [System.Serializable]
    private class BossEncounterStatePayload
    {
        public string BossEncounterId; // 저장 시점 보스전 식별자입니다.
        public int CurrentPhaseIndex; // 저장 시점 보스 페이즈 인덱스입니다.
        public float HpNormalized; // 저장 시점 보스 체력 정규화 값입니다.
        public bool IsDefeated; // 저장 시점 보스 처치 여부입니다.
        public bool IsEncounterStarted; // 저장 시점 보스전 진입 여부입니다.
    }

    public string ParticipantId => _participantId;
    public int PayloadVersion => 1;

    /// <summary>
    /// 참조가 비어 있을 경우 동일 오브젝트 기준으로 자동 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_bossEncounterRuntime == null)
        {
            _bossEncounterRuntime = GetComponent<BossEncounterRuntime>();
        }

        if (_bossPhaseController == null)
        {
            _bossPhaseController = GetComponent<BossPhaseController>();
        }

        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }
    }

    /// <summary>
    /// 현재 저장 문맥에서 저장 가능 여부를 반환합니다.
    /// </summary>
    public bool CanSave(in SaveContext context)
    {
        return _bossEncounterRuntime != null && _healthComponent != null && _healthComponent.IsInitialized;
    }

    /// <summary>
    /// 보스전 상태를 JSON 문자열로 직렬화합니다.
    /// </summary>
    public string CaptureAsJson(in SaveContext context)
    {
        BossEncounterStatePayload payload = new BossEncounterStatePayload
        {
            BossEncounterId = _bossEncounterRuntime.BossEncounterId,
            CurrentPhaseIndex = _bossPhaseController != null ? _bossPhaseController.CurrentPhaseIndex : _bossEncounterRuntime.PhaseCheckpointIndex,
            HpNormalized = _healthComponent.GetHealthNormalized(),
            IsDefeated = _bossEncounterRuntime.IsDefeated,
            IsEncounterStarted = _bossEncounterRuntime.IsEncounterStarted
        };

        return JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// JSON 문자열에서 보스전 상태를 RuleSet 기반으로 복원합니다.
    /// </summary>
    public void RestoreFromJson(string payloadJson, in SaveContext context)
    {
        if (string.IsNullOrWhiteSpace(payloadJson) || _bossEncounterRuntime == null || _healthComponent == null)
        {
            return;
        }

        BossEncounterStatePayload payload = JsonUtility.FromJson<BossEncounterStatePayload>(payloadJson);
        if (payload == null)
        {
            return;
        }

        BossRestartRuleSet.ResolvedRule resolvedRule = ResolveRule(payload.BossEncounterId, context.ChannelType); // 현재 보스전에 적용할 재시작 규칙 결과입니다.
        ApplyRule(payload, resolvedRule);

        if (resolvedRule.ArenaReset)
        {
            EnqueueArenaReset(payload.BossEncounterId);
        }
    }

    /// <summary>
    /// Recovery 완료 후 지연된 보스 아레나 초기화를 현재 씬에 적용합니다.
    /// </summary>
    public static void ApplyDeferredArenaResetInScene()
    {
        if (_pendingArenaResetEncounterIds.Count == 0)
        {
            return;
        }

        BossEncounterRuntime[] runtimes = FindObjectsByType<BossEncounterRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 아레나 초기화 적용을 시도할 현재 씬 보스 런타임 목록입니다.
        for (int runtimeIndex = 0; runtimeIndex < runtimes.Length; runtimeIndex++)
        {
            BossEncounterRuntime runtime = runtimes[runtimeIndex]; // 아레나 초기화 적용을 시도할 현재 보스 런타임입니다.
            if (runtime == null)
            {
                continue;
            }

            if (!_pendingArenaResetEncounterIds.Contains(runtime.BossEncounterId))
            {
                continue;
            }

            runtime.ResetArenaState();
        }

        _pendingArenaResetEncounterIds.Clear();
    }

    /// <summary>
    /// 보스전 페이로드와 규칙을 결합해 복원 상태를 적용합니다.
    /// </summary>
    private void ApplyRule(BossEncounterStatePayload payload, BossRestartRuleSet.ResolvedRule resolvedRule)
    {
        if (resolvedRule.RestartMode == BossRestartRuleSet.RestartMode.KeepDefeated && payload.IsDefeated)
        {
            _bossEncounterRuntime.MarkDefeated(true);
            _bossEncounterRuntime.EndEncounter();
            ApplyHealthByMode(payload.HpNormalized, true, resolvedRule.HpRestoreMode);
            return;
        }

        if (resolvedRule.RestartMode == BossRestartRuleSet.RestartMode.FullReset)
        {
            _bossEncounterRuntime.ResetEncounterRuntime();
            _bossPhaseController?.SetPhaseIndexForRecovery(0);
            _bossEncounterRuntime.SetPhaseCheckpoint(0);
            _bossEncounterRuntime.StartEncounter();
            ApplyHealthByMode(payload.HpNormalized, false, resolvedRule.HpRestoreMode);
            return;
        }

        int checkpointPhaseIndex = Mathf.Max(payload.CurrentPhaseIndex, _bossEncounterRuntime.PhaseCheckpointIndex); // 페이즈 체크포인트 복원에서 사용할 최종 페이즈 인덱스입니다.
        _bossEncounterRuntime.SetPhaseCheckpoint(checkpointPhaseIndex);
        _bossPhaseController?.SetPhaseIndexForRecovery(checkpointPhaseIndex);

        if (payload.IsEncounterStarted)
        {
            _bossEncounterRuntime.StartEncounter();
        }
        else
        {
            _bossEncounterRuntime.EndEncounter();
        }

        _bossEncounterRuntime.MarkDefeated(payload.IsDefeated);
        ApplyHealthByMode(payload.HpNormalized, payload.IsDefeated, resolvedRule.HpRestoreMode);
    }

    /// <summary>
    /// 체력 복원 모드 기준으로 보스 체력 상태를 적용합니다.
    /// </summary>
    private void ApplyHealthByMode(float savedHpNormalized, bool isDefeated, BossRestartRuleSet.HpRestoreMode hpRestoreMode)
    {
        float maxHealth = Mathf.Max(1f, _healthComponent.GetMaxHealth()); // 복원 적용 계산에 사용할 최대 체력 값입니다.
        float targetHealth = maxHealth; // 복원 모드 계산 후 적용할 최종 체력 값입니다.

        if (hpRestoreMode == BossRestartRuleSet.HpRestoreMode.Saved)
        {
            targetHealth = Mathf.Clamp01(savedHpNormalized) * maxHealth;
        }
        else if (hpRestoreMode == BossRestartRuleSet.HpRestoreMode.KeepDefeatedZero && isDefeated)
        {
            targetHealth = 0f;
        }

        if (targetHealth <= 0f)
        {
            _healthComponent.SetCurrentHealth(0f);
            return;
        }

        if (_healthComponent.IsDead)
        {
            _healthComponent.Revive(targetHealth);
            return;
        }

        _healthComponent.SetCurrentHealth(targetHealth);
    }

    /// <summary>
    /// 보스전 ID와 복원 문맥을 기준으로 복원 규칙을 해석합니다.
    /// </summary>
    private BossRestartRuleSet.ResolvedRule ResolveRule(string bossEncounterId, E_SaveChannelType channelType)
    {
        if (channelType != E_SaveChannelType.Recovery || !_respectRecoveryPolicy)
        {
            return BossRestartRuleSet.ResolvedRule.CreateDefault();
        }

        RecoveryPolicy recoveryPolicy = SaveCoordinator.Instance != null ? SaveCoordinator.Instance.RecoveryPolicy : null;
        return RecoveryPolicyRuleHelper.ResolveBossRestartRule(recoveryPolicy, bossEncounterId);
    }

    /// <summary>
    /// 보스 아레나 초기화 요청을 지연 큐에 등록합니다.
    /// </summary>
    private static void EnqueueArenaReset(string bossEncounterId)
    {
        if (string.IsNullOrWhiteSpace(bossEncounterId))
        {
            return;
        }

        if (_pendingArenaResetEncounterIds.Contains(bossEncounterId))
        {
            return;
        }

        _pendingArenaResetEncounterIds.Add(bossEncounterId);
    }
}
