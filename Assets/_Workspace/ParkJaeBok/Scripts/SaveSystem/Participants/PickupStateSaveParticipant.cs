using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬의 월드 픽업 상태를 저장/복원하는 participant입니다.
/// </summary>
public class PickupStateSaveParticipant : MonoBehaviour, ISaveParticipant
{
    [Tooltip("저장 레코드에서 PickupState participant를 식별하는 고유 ID입니다.")]
    [SerializeField] private string _participantId = "world.pickup_state"; // PickupState participant 식별자입니다.

    [Tooltip("Recovery 복원 시 RecoveryPolicy의 Item RuleSet을 따를지 여부입니다.")]
    [SerializeField] private bool _respectRecoveryPolicy = true; // 복구 채널 복원 시 아이템 정책 적용 여부입니다.

    [System.Serializable]
    private class PickupStatePayload
    {
        public List<PickupStateRecord> Records = new List<PickupStateRecord>(); // 픽업 상태 저장 레코드 목록입니다.
    }

    [System.Serializable]
    private class PickupStateRecord
    {
        public string PickupId; // 픽업 고유 식별자입니다.
        public string ItemType; // 픽업 아이템 종류 식별자입니다.
        public bool IsCollected; // 저장 시점 획득 상태 값입니다.
        public int Quantity; // 저장 시점 수량 값입니다.
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
    /// 씬의 모든 픽업 상태를 JSON 문자열로 직렬화합니다.
    /// </summary>
    public string CaptureAsJson(in SaveContext context)
    {
        PickupStatePayload payload = new PickupStatePayload();
        List<IPickupStateProvider> providers = CollectProviders(); // 현재 씬에서 조회한 픽업 상태 제공자 목록입니다.

        for (int index = 0; index < providers.Count; index++)
        {
            IPickupStateProvider provider = providers[index]; // 상태 캡처를 수행할 현재 픽업 제공자입니다.
            if (provider == null || string.IsNullOrWhiteSpace(provider.PickupId))
            {
                continue;
            }

            payload.Records.Add(new PickupStateRecord
            {
                PickupId = provider.PickupId,
                ItemType = provider.ItemType,
                IsCollected = provider.IsCollected,
                Quantity = provider.CurrentQuantity
            });
        }

        return JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// JSON 문자열에서 픽업 상태를 복원합니다.
    /// </summary>
    public void RestoreFromJson(string payloadJson, in SaveContext context)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return;
        }

        PickupStatePayload payload = JsonUtility.FromJson<PickupStatePayload>(payloadJson);
        if (payload == null || payload.Records == null)
        {
            return;
        }

        List<IPickupStateProvider> providers = CollectProviders(); // 복원 적용 대상 픽업 상태 제공자 목록입니다.

        for (int index = 0; index < payload.Records.Count; index++)
        {
            PickupStateRecord record = payload.Records[index]; // 복원 처리할 현재 픽업 레코드입니다.
            if (record == null || string.IsNullOrWhiteSpace(record.PickupId))
            {
                continue;
            }

            IPickupStateProvider provider = FindByPickupId(providers, record.PickupId);
            if (provider == null)
            {
                continue;
            }

            ItemRestoreRuleSet.ResolvedRule resolvedRule = ResolveRule(record.ItemType, record.PickupId, context.ChannelType); // 현재 픽업에 적용할 아이템 복원 규칙 결과입니다.
            ApplyRuleToProvider(provider, record, resolvedRule);
        }
    }

    /// <summary>
    /// 픽업 레코드와 규칙을 결합해 최종 복원 상태를 적용합니다.
    /// </summary>
    private static void ApplyRuleToProvider(IPickupStateProvider provider, PickupStateRecord record, ItemRestoreRuleSet.ResolvedRule resolvedRule)
    {
        int restoredQuantity = ResolveQuantity(provider, record.Quantity, resolvedRule.QuantityRestoreMode); // 규칙에 따라 계산한 최종 복원 수량 값입니다.
        bool restoredCollectedState = resolvedRule.RestoreCollectedState ? record.IsCollected : provider.IsCollected; // 규칙 반영 후 최종 획득 상태 값입니다.

        if (resolvedRule.RespawnAfterRecovery)
        {
            restoredCollectedState = false;
            if (resolvedRule.QuantityRestoreMode == ItemRestoreRuleSet.QuantityRestoreMode.KeepSavedQuantity)
            {
                restoredQuantity = Mathf.Max(1, restoredQuantity);
            }
        }

        provider.ApplyRestoredState(restoredCollectedState, restoredQuantity);
    }

    /// <summary>
    /// 수량 복원 모드에 따라 최종 수량 값을 계산합니다.
    /// </summary>
    private static int ResolveQuantity(IPickupStateProvider provider, int savedQuantity, ItemRestoreRuleSet.QuantityRestoreMode quantityRestoreMode)
    {
        if (quantityRestoreMode == ItemRestoreRuleSet.QuantityRestoreMode.ResetToDefault)
        {
            return provider.DefaultQuantity;
        }

        if (quantityRestoreMode == ItemRestoreRuleSet.QuantityRestoreMode.ForceZero)
        {
            return 0;
        }

        return Mathf.Max(0, savedQuantity);
    }

    /// <summary>
    /// 아이템 종류/ID와 복원 문맥을 기준으로 복원 규칙을 해석합니다.
    /// </summary>
    private ItemRestoreRuleSet.ResolvedRule ResolveRule(string itemType, string itemId, E_SaveChannelType channelType)
    {
        if (channelType != E_SaveChannelType.Recovery || !_respectRecoveryPolicy)
        {
            return ItemRestoreRuleSet.ResolvedRule.CreateDefault();
        }

        RecoveryPolicy recoveryPolicy = SaveCoordinator.Instance != null ? SaveCoordinator.Instance.RecoveryPolicy : null;
        return RecoveryPolicyRuleHelper.ResolveItemRestoreRule(recoveryPolicy, itemType, itemId);
    }

    /// <summary>
    /// 픽업 ID 기준으로 상태 제공자를 조회합니다.
    /// </summary>
    private static IPickupStateProvider FindByPickupId(List<IPickupStateProvider> providers, string pickupId)
    {
        for (int index = 0; index < providers.Count; index++)
        {
            IPickupStateProvider provider = providers[index]; // ID 비교를 수행할 현재 픽업 상태 제공자입니다.
            if (provider == null)
            {
                continue;
            }

            if (provider.PickupId == pickupId)
            {
                return provider;
            }
        }

        return null;
    }

    /// <summary>
    /// 현재 씬의 픽업 상태 제공자 목록을 수집합니다.
    /// </summary>
    private static List<IPickupStateProvider> CollectProviders()
    {
        List<IPickupStateProvider> providers = new List<IPickupStateProvider>();

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index]; // 픽업 상태 제공자 캐스팅을 시도할 현재 컴포넌트입니다.
            if (behaviour is IPickupStateProvider provider)
            {
                providers.Add(provider);
            }
        }

        return providers;
    }
}
