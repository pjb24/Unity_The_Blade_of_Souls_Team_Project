using UnityEngine;

/// <summary>
/// RecoveryPolicy의 Rule Set/하위호환 플래그를 함께 해석하는 공통 헬퍼입니다.
/// </summary>
public static class RecoveryPolicyRuleHelper
{
    /// <summary>
    /// 체력 복원 허용 여부를 Rule Set 또는 하위호환 플래그 기반으로 해석합니다.
    /// </summary>
    public static bool ShouldRestoreHealth(RecoveryPolicy recoveryPolicy, string subjectId = null)
    {
        if (recoveryPolicy == null)
        {
            return true;
        }

        return recoveryPolicy.RestoreHealth;
    }

    /// <summary>
    /// StageSession 복원 허용 여부를 Rule Set 또는 하위호환 플래그 기반으로 해석합니다.
    /// </summary>
    public static bool ShouldRestoreStageSession(RecoveryPolicy recoveryPolicy, string subjectId = null)
    {
        if (recoveryPolicy == null)
        {
            return true;
        }

        return recoveryPolicy.RestoreStageSession;
    }

    /// <summary>
    /// 체크포인트 복원 허용 여부를 Rule Set 또는 기본값 기반으로 해석합니다.
    /// </summary>
    public static bool ShouldRestoreCheckpoint(RecoveryPolicy recoveryPolicy, string checkpointId)
    {
        return ResolveWithFallback<CheckpointRuleSet>(
            recoveryPolicy,
            checkpointId,
            set => set != null ? set.Resolve(checkpointId) : true,
            fallbackDefault: true,
            fallbackLegacy: null);
    }

    /// <summary>
    /// 적 리셋/복원 허용 여부를 Rule Set 또는 기본값 기반으로 해석합니다.
    /// </summary>
    public static bool ShouldRestoreEnemy(RecoveryPolicy recoveryPolicy, string enemyId)
    {
        EnemyResetRuleSet.ResolvedRule resolvedRule = ResolveEnemyResetRule(recoveryPolicy, enemyId, string.Empty);
        return resolvedRule.ResetOnRecovery || resolvedRule.RespawnIfDead;
    }

    /// <summary>
    /// EnemyRuntimeId/ArchetypeId를 기준으로 Enemy 리셋 규칙을 해석합니다.
    /// </summary>
    public static EnemyResetRuleSet.ResolvedRule ResolveEnemyResetRule(RecoveryPolicy recoveryPolicy, string enemyRuntimeId, string archetypeId)
    {
        if (recoveryPolicy == null || recoveryPolicy.EnemyResetRuleSet == null)
        {
            return EnemyResetRuleSet.ResolvedRule.CreateDefault();
        }

        return recoveryPolicy.EnemyResetRuleSet.Resolve(enemyRuntimeId, archetypeId);
    }

    /// <summary>
    /// 기믹 상태 복원 허용 여부를 Rule Set 또는 기본값 기반으로 해석합니다.
    /// </summary>
    public static bool ShouldRestoreGimmick(RecoveryPolicy recoveryPolicy, string gimmickId)
    {
        GimmickRestoreRuleSet.ResolvedRule resolvedRule = ResolveGimmickRestoreRule(recoveryPolicy, gimmickId);
        return resolvedRule.AllowRestore;
    }

    /// <summary>
    /// 기믹 ID를 기준으로 기믹 복원 규칙을 해석합니다.
    /// </summary>
    public static GimmickRestoreRuleSet.ResolvedRule ResolveGimmickRestoreRule(RecoveryPolicy recoveryPolicy, string gimmickId)
    {
        if (recoveryPolicy == null || recoveryPolicy.GimmickRestoreRuleSet == null)
        {
            return GimmickRestoreRuleSet.ResolvedRule.CreateDefault();
        }

        return recoveryPolicy.GimmickRestoreRuleSet.ResolveRule(gimmickId);
    }

    /// <summary>
    /// 아이템 상태 복원 허용 여부를 Rule Set 또는 기본값 기반으로 해석합니다.
    /// </summary>
    public static bool ShouldRestoreItem(RecoveryPolicy recoveryPolicy, string itemId)
    {
        ItemRestoreRuleSet.ResolvedRule resolvedRule = ResolveItemRestoreRule(recoveryPolicy, string.Empty, itemId);
        return resolvedRule.RestoreCollectedState || resolvedRule.RespawnAfterRecovery;
    }

    /// <summary>
    /// 아이템 종류/ID를 기준으로 아이템 복원 규칙을 해석합니다.
    /// </summary>
    public static ItemRestoreRuleSet.ResolvedRule ResolveItemRestoreRule(RecoveryPolicy recoveryPolicy, string itemType, string itemId)
    {
        if (recoveryPolicy == null || recoveryPolicy.ItemRestoreRuleSet == null)
        {
            return ItemRestoreRuleSet.ResolvedRule.CreateDefault();
        }

        return recoveryPolicy.ItemRestoreRuleSet.ResolveRule(itemType, itemId);
    }

    /// <summary>
    /// 보스전 재시작/복원 허용 여부를 Rule Set 또는 기본값 기반으로 해석합니다.
    /// </summary>
    public static bool ShouldRestoreBossEncounter(RecoveryPolicy recoveryPolicy, string bossEncounterId)
    {
        BossRestartRuleSet.ResolvedRule resolvedRule = ResolveBossRestartRule(recoveryPolicy, bossEncounterId);
        return resolvedRule.RestartMode != BossRestartRuleSet.RestartMode.KeepDefeated || resolvedRule.HpRestoreMode == BossRestartRuleSet.HpRestoreMode.KeepDefeatedZero;
    }

    /// <summary>
    /// 보스전 ID를 기준으로 보스 재시작 규칙을 해석합니다.
    /// </summary>
    public static BossRestartRuleSet.ResolvedRule ResolveBossRestartRule(RecoveryPolicy recoveryPolicy, string bossEncounterId)
    {
        if (recoveryPolicy == null || recoveryPolicy.BossRestartRuleSet == null)
        {
            return BossRestartRuleSet.ResolvedRule.CreateDefault();
        }

        return recoveryPolicy.BossRestartRuleSet.ResolveRule(bossEncounterId);
    }

    /// <summary>
    /// Rule Set 우선, 미설정 시 하위호환 플래그, 마지막으로 기본값을 적용해 복원 허용 여부를 해석합니다.
    /// </summary>
    private static bool ResolveWithFallback<TRuleSet>(
        RecoveryPolicy recoveryPolicy,
        string subjectId,
        System.Func<TRuleSet, bool> resolveFromRuleSet,
        bool fallbackDefault,
        System.Func<RecoveryPolicy, bool> fallbackLegacy)
        where TRuleSet : UnityEngine.Object
    {
        if (recoveryPolicy == null)
        {
            return fallbackDefault;
        }

        TRuleSet ruleSet = GetRuleSet<TRuleSet>(recoveryPolicy); // 복원 판단에 사용할 Rule Set 참조입니다.
        if (ruleSet != null && resolveFromRuleSet != null)
        {
            return resolveFromRuleSet(ruleSet);
        }

        if (fallbackLegacy != null)
        {
            return fallbackLegacy(recoveryPolicy);
        }

        return fallbackDefault;
    }

    /// <summary>
    /// RecoveryPolicy에서 요청한 타입의 Rule Set 참조를 반환합니다.
    /// </summary>
    private static TRuleSet GetRuleSet<TRuleSet>(RecoveryPolicy recoveryPolicy) where TRuleSet : UnityEngine.Object
    {
        if (typeof(TRuleSet) == typeof(CheckpointRuleSet))
        {
            return recoveryPolicy.CheckpointRuleSet as TRuleSet;
        }

        if (typeof(TRuleSet) == typeof(EnemyResetRuleSet))
        {
            return recoveryPolicy.EnemyResetRuleSet as TRuleSet;
        }

        if (typeof(TRuleSet) == typeof(GimmickRestoreRuleSet))
        {
            return recoveryPolicy.GimmickRestoreRuleSet as TRuleSet;
        }

        if (typeof(TRuleSet) == typeof(ItemRestoreRuleSet))
        {
            return recoveryPolicy.ItemRestoreRuleSet as TRuleSet;
        }

        if (typeof(TRuleSet) == typeof(BossRestartRuleSet))
        {
            return recoveryPolicy.BossRestartRuleSet as TRuleSet;
        }

        Debug.LogWarning($"[RecoveryPolicyRuleHelper] 지원하지 않는 Rule Set 타입 요청입니다. type={typeof(TRuleSet).Name}");
        return null;
    }
}
