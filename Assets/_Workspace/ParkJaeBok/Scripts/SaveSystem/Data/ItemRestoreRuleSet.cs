using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 상태 복원 규칙을 종류/ID 기반 기본값 + 오버라이드로 제어하는 정책 데이터입니다.
/// </summary>
[CreateAssetMenu(fileName = "ItemRestoreRuleSet", menuName = "Game/Save System/Rule Sets/Item Restore Rule Set")]
public class ItemRestoreRuleSet : ScriptableObject
{
    /// <summary>
    /// 수량 복원 적용 방식입니다.
    /// </summary>
    public enum QuantityRestoreMode
    {
        KeepSavedQuantity = 0,
        ResetToDefault = 1,
        ForceZero = 2,
    }

    /// <summary>
    /// 아이템 복원 규칙 해석 결과 데이터입니다.
    /// </summary>
    [Serializable]
    public struct ResolvedRule
    {
        public bool RestoreCollectedState; // 획득 상태 복원 적용 여부입니다.
        public bool RespawnAfterRecovery; // Recovery 시 강제 리스폰 여부입니다.
        public QuantityRestoreMode QuantityRestoreMode; // 수량 복원 모드입니다.

        /// <summary>
        /// 기본 복원 규칙 결과를 생성합니다.
        /// </summary>
        public static ResolvedRule CreateDefault()
        {
            return new ResolvedRule
            {
                RestoreCollectedState = true,
                RespawnAfterRecovery = false,
                QuantityRestoreMode = QuantityRestoreMode.KeepSavedQuantity
            };
        }
    }

    [Serializable]
    private struct ItemOverrideEntry
    {
        [Tooltip("오버라이드 매칭에 사용할 아이템 종류 문자열입니다.")]
        [SerializeField] private string _itemType; // 오버라이드 규칙 매칭에 사용할 아이템 종류 키입니다.

        [Tooltip("오버라이드 매칭에 사용할 아이템 ID(픽업 ID) 문자열입니다. 비워두면 종류 전체 규칙으로 동작합니다.")]
        [SerializeField] private string _itemId; // 오버라이드 규칙 매칭에 사용할 아이템 개별 ID 키입니다.

        [Tooltip("해당 키의 획득 상태 복원 적용 여부입니다.")]
        [SerializeField] private bool _restoreCollectedState; // 오버라이드 대상 아이템의 획득 상태 복원 여부입니다.

        [Tooltip("Recovery 시 해당 아이템을 강제로 리스폰할지 여부입니다.")]
        [SerializeField] private bool _respawnAfterRecovery; // 오버라이드 대상 아이템의 Recovery 리스폰 여부입니다.

        [Tooltip("해당 아이템의 수량 복원 모드입니다.")]
        [SerializeField] private QuantityRestoreMode _quantityRestoreMode; // 오버라이드 대상 아이템의 수량 복원 모드입니다.

        public string ItemType => _itemType;
        public string ItemId => _itemId;
        public bool RestoreCollectedState => _restoreCollectedState;
        public bool RespawnAfterRecovery => _respawnAfterRecovery;
        public QuantityRestoreMode QuantityRestoreMode => _quantityRestoreMode;
    }

    [Header("Default Rule")]
    [Tooltip("오버라이드가 없을 때 획득 상태 복원 적용 여부입니다.")]
    [SerializeField] private bool _defaultRestoreCollectedState = true; // 오버라이드 미매칭 시 획득 상태 복원 여부입니다.

    [Tooltip("오버라이드가 없을 때 Recovery 강제 리스폰 여부입니다.")]
    [SerializeField] private bool _defaultRespawnAfterRecovery; // 오버라이드 미매칭 시 Recovery 강제 리스폰 여부입니다.

    [Tooltip("오버라이드가 없을 때 적용할 수량 복원 모드입니다.")]
    [SerializeField] private QuantityRestoreMode _defaultQuantityRestoreMode = QuantityRestoreMode.KeepSavedQuantity; // 오버라이드 미매칭 시 수량 복원 모드입니다.

    [Header("Overrides")]
    [Tooltip("itemType/itemId 기반 오버라이드 규칙 목록입니다.")]
    [SerializeField] private List<ItemOverrideEntry> _overrides = new List<ItemOverrideEntry>(); // 아이템 종류/개별 ID 기반 오버라이드 규칙 목록입니다.

    /// <summary>
    /// itemType/itemId 기반으로 아이템 복원 규칙을 해석합니다.
    /// </summary>
    public ResolvedRule ResolveRule(string itemType, string itemId)
    {
        if (TryFindOverride(itemType, itemId, out ItemOverrideEntry overrideEntry))
        {
            return new ResolvedRule
            {
                RestoreCollectedState = overrideEntry.RestoreCollectedState,
                RespawnAfterRecovery = overrideEntry.RespawnAfterRecovery,
                QuantityRestoreMode = overrideEntry.QuantityRestoreMode
            };
        }

        return new ResolvedRule
        {
            RestoreCollectedState = _defaultRestoreCollectedState,
            RespawnAfterRecovery = _defaultRespawnAfterRecovery,
            QuantityRestoreMode = _defaultQuantityRestoreMode
        };
    }

    /// <summary>
    /// 아이템 종류/ID 기준 오버라이드 규칙을 우선순위에 따라 탐색합니다.
    /// </summary>
    private bool TryFindOverride(string itemType, string itemId, out ItemOverrideEntry overrideEntry)
    {
        overrideEntry = default;

        bool found = false;
        int bestScore = -1; // 더 구체적인 오버라이드(종류+ID)에 높은 점수를 부여하기 위한 기준값입니다.

        for (int index = 0; index < _overrides.Count; index++)
        {
            ItemOverrideEntry candidate = _overrides[index]; // 현재 검사 중인 아이템 오버라이드 엔트리입니다.
            if (!IsTypeMatch(candidate.ItemType, itemType))
            {
                continue;
            }

            bool hasCandidateItemId = !string.IsNullOrWhiteSpace(candidate.ItemId);
            if (hasCandidateItemId && !string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal))
            {
                continue;
            }

            int score = hasCandidateItemId ? 2 : 1; // itemId까지 일치하는 규칙을 itemType-only 규칙보다 우선하기 위한 점수입니다.
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            overrideEntry = candidate;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// 오버라이드 항목의 itemType이 대상 itemType과 일치하는지 검사합니다.
    /// </summary>
    private static bool IsTypeMatch(string ruleItemType, string targetItemType)
    {
        if (string.IsNullOrWhiteSpace(ruleItemType))
        {
            return string.IsNullOrWhiteSpace(targetItemType);
        }

        return string.Equals(ruleItemType, targetItemType, StringComparison.Ordinal);
    }
}
