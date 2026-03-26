using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬의 IGimmickStateProvider 상태를 수집/저장/복원하는 participant입니다.
/// </summary>
public class GimmickStateSaveParticipant : MonoBehaviour, ISaveParticipant
{
    [Tooltip("저장 레코드에서 GimmickState participant를 식별하는 고유 ID입니다.")]
    [SerializeField] private string _participantId = "world.gimmick_state"; // GimmickState participant 식별자입니다.

    [Tooltip("Recovery 복원 시 RecoveryPolicy의 기믹 RuleSet을 따를지 여부입니다.")]
    [SerializeField] private bool _respectRecoveryPolicy = true; // 복구 채널 복원 시 기믹 정책 적용 여부입니다.

    [Tooltip("기믹 상태 적용 시 디버그 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLog; // 기믹 상태 복원 디버그 로그 출력 여부입니다.

    private static readonly List<DeferredRestoreRecord> _deferredRestoreRecords = new List<DeferredRestoreRecord>(); // 지연 복원 타이밍까지 대기할 기믹 복원 레코드 목록입니다.

    [System.Serializable]
    private class GimmickStatePayload
    {
        public List<GimmickStateRecord> Records = new List<GimmickStateRecord>(); // 기믹 상태 저장 레코드 목록입니다.
    }

    [System.Serializable]
    private class GimmickStateRecord
    {
        public string GimmickId; // 기믹 상태 제공자 식별자입니다.
        public string StateJson; // 기믹 상태 JSON 문자열입니다.
    }

    private struct DeferredRestoreRecord
    {
        public string GimmickId; // 지연 복원 대상 기믹 식별자입니다.
        public string StateJson; // 지연 복원 대상 기믹 상태 JSON입니다.
        public GimmickRestoreRuleSet.RestoreTiming Timing; // 지연 복원 시점 규칙입니다.
    }

    public string ParticipantId => _participantId;
    public int PayloadVersion => 1;

    /// <summary>
    /// 현재 저장 문맥에서 저장 가능 여부를 반환합니다.
    /// </summary>
    public bool CanSave(in SaveContext context)
    {
        return true;
    }

    /// <summary>
    /// 씬의 모든 기믹 상태 제공자 상태를 JSON 문자열로 직렬화합니다.
    /// </summary>
    public string CaptureAsJson(in SaveContext context)
    {
        GimmickStatePayload payload = new GimmickStatePayload();

        List<IGimmickStateProvider> providers = CollectProviders(); // 현재 씬에서 찾은 기믹 상태 제공자 목록입니다.
        for (int index = 0; index < providers.Count; index++)
        {
            IGimmickStateProvider provider = providers[index]; // 상태를 캡처할 현재 기믹 제공자입니다.
            if (provider == null || string.IsNullOrWhiteSpace(provider.GimmickId))
            {
                continue;
            }

            payload.Records.Add(new GimmickStateRecord
            {
                GimmickId = provider.GimmickId,
                StateJson = provider.CaptureStateJson()
            });
        }

        return JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// JSON 문자열의 기믹 상태를 RuleSet 기반으로 즉시/지연 복원합니다.
    /// </summary>
    public void RestoreFromJson(string payloadJson, in SaveContext context)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return;
        }

        GimmickStatePayload payload = JsonUtility.FromJson<GimmickStatePayload>(payloadJson);
        if (payload == null || payload.Records == null)
        {
            return;
        }

        List<IGimmickStateProvider> providers = CollectProviders(); // 복원 적용 대상 기믹 제공자 목록입니다.

        for (int index = 0; index < payload.Records.Count; index++)
        {
            GimmickStateRecord record = payload.Records[index]; // 복원 처리할 현재 기믹 상태 레코드입니다.
            if (record == null || string.IsNullOrWhiteSpace(record.GimmickId))
            {
                continue;
            }

            GimmickRestoreRuleSet.ResolvedRule resolvedRule = ResolveRule(record.GimmickId, context.ChannelType); // 현재 기믹 ID에 대응하는 복원 규칙 결과입니다.
            if (!resolvedRule.AllowRestore)
            {
                continue;
            }

            if (resolvedRule.Timing == GimmickRestoreRuleSet.RestoreTiming.AfterSceneLoad)
            {
                ApplyToProvider(providers, record.GimmickId, record.StateJson);
                if (_verboseLog)
                {
                    Debug.Log($"[GimmickStateSaveParticipant] Immediate restore applied. gimmickId={record.GimmickId}");
                }
                continue;
            }

            EnqueueDeferredRestore(record.GimmickId, record.StateJson, resolvedRule.Timing);
            if (_verboseLog)
            {
                Debug.Log($"[GimmickStateSaveParticipant] Deferred restore queued. gimmickId={record.GimmickId}, timing={resolvedRule.Timing}");
            }
        }
    }

    /// <summary>
    /// 지정 시점의 지연 복원 레코드를 씬의 기믹 제공자에게 적용합니다.
    /// </summary>
    public static void ApplyDeferredRestoresInScene(GimmickRestoreRuleSet.RestoreTiming timing)
    {
        if (_deferredRestoreRecords.Count == 0)
        {
            return;
        }

        List<IGimmickStateProvider> providers = CollectProvidersStatic(); // 지연 복원을 적용할 현재 씬 기믹 제공자 목록입니다.

        for (int index = _deferredRestoreRecords.Count - 1; index >= 0; index--)
        {
            DeferredRestoreRecord record = _deferredRestoreRecords[index]; // 이번 시점에서 적용 여부를 판단할 지연 복원 레코드입니다.
            if (record.Timing != timing)
            {
                continue;
            }

            ApplyToProviderStatic(providers, record.GimmickId, record.StateJson);
            _deferredRestoreRecords.RemoveAt(index);
        }
    }

    /// <summary>
    /// 기믹 ID와 복원 문맥을 기준으로 복원 규칙을 해석합니다.
    /// </summary>
    private GimmickRestoreRuleSet.ResolvedRule ResolveRule(string gimmickId, E_SaveChannelType channelType)
    {
        if (channelType != E_SaveChannelType.Recovery || !_respectRecoveryPolicy)
        {
            return GimmickRestoreRuleSet.ResolvedRule.CreateDefault();
        }

        RecoveryPolicy recoveryPolicy = SaveCoordinator.Instance != null ? SaveCoordinator.Instance.RecoveryPolicy : null;
        return RecoveryPolicyRuleHelper.ResolveGimmickRestoreRule(recoveryPolicy, gimmickId);
    }

    /// <summary>
    /// 지연 복원 큐에 기믹 상태 레코드를 등록합니다.
    /// </summary>
    private void EnqueueDeferredRestore(string gimmickId, string stateJson, GimmickRestoreRuleSet.RestoreTiming timing)
    {
        _deferredRestoreRecords.Add(new DeferredRestoreRecord
        {
            GimmickId = gimmickId,
            StateJson = stateJson,
            Timing = timing
        });
    }

    /// <summary>
    /// 현재 씬에서 IGimmickStateProvider 구현체를 수집합니다.
    /// </summary>
    private static List<IGimmickStateProvider> CollectProvidersStatic()
    {
        List<IGimmickStateProvider> providers = new List<IGimmickStateProvider>();

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index]; // 기믹 상태 제공자 캐스팅을 시도할 현재 컴포넌트입니다.
            if (behaviour is IGimmickStateProvider provider)
            {
                providers.Add(provider);
            }
        }

        return providers;
    }

    /// <summary>
    /// 인스턴스 컨텍스트에서 현재 씬 IGimmickStateProvider 목록을 수집합니다.
    /// </summary>
    private List<IGimmickStateProvider> CollectProviders()
    {
        return CollectProvidersStatic();
    }

    /// <summary>
    /// 기믹 ID에 해당하는 제공자를 찾아 상태 복원을 적용합니다.
    /// </summary>
    private void ApplyToProvider(List<IGimmickStateProvider> providers, string gimmickId, string stateJson)
    {
        ApplyToProviderStatic(providers, gimmickId, stateJson);
    }

    /// <summary>
    /// 정적 컨텍스트에서 기믹 ID에 해당하는 제공자를 찾아 상태 복원을 적용합니다.
    /// </summary>
    private static void ApplyToProviderStatic(List<IGimmickStateProvider> providers, string gimmickId, string stateJson)
    {
        if (providers == null || string.IsNullOrWhiteSpace(gimmickId))
        {
            return;
        }

        for (int index = 0; index < providers.Count; index++)
        {
            IGimmickStateProvider provider = providers[index]; // ID 매칭으로 복원 적용을 시도할 현재 기믹 제공자입니다.
            if (provider == null)
            {
                continue;
            }

            if (provider.GimmickId != gimmickId)
            {
                continue;
            }

            provider.RestoreStateJson(stateJson);
            return;
        }
    }
}
