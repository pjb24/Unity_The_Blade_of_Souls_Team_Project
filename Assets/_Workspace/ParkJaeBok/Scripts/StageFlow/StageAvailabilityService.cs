using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 입장 가능 여부 평가 결과입니다.
/// </summary>
public readonly struct StageAvailabilityResult
{
    public readonly bool IsUnlocked; // 스테이지 개방 여부입니다.
    public readonly bool IsEnterable; // 스테이지 실제 입장 가능 여부입니다.
    public readonly bool IsMultiplayerAvailable; // 스테이지 멀티플레이 허용 여부입니다.
    public readonly string Reason; // 잠금/입장 불가 사유를 표현하는 문자열입니다.

    /// <summary>
    /// 스테이지 가능 상태 결과를 생성합니다.
    /// </summary>
    public StageAvailabilityResult(bool isUnlocked, bool isEnterable, bool isMultiplayerAvailable, string reason)
    {
        IsUnlocked = isUnlocked;
        IsEnterable = isEnterable;
        IsMultiplayerAvailable = isMultiplayerAvailable;
        Reason = reason;
    }
}

/// <summary>
/// 스테이지 개방/입장/멀티플레이 가능 여부를 판정하는 서비스입니다.
/// </summary>
public static class StageAvailabilityService
{
    /// <summary>
    /// 전달된 스테이지 정의와 진행도를 기준으로 현재 입장 가능 상태를 평가합니다.
    /// </summary>
    public static StageAvailabilityResult Evaluate(StageDefinition stageDefinition, StageProgressRuntime progressRuntime)
    {
        if (stageDefinition == null)
        {
            return new StageAvailabilityResult(false, false, false, "StageDefinition is null");
        }

        bool isUnlocked = EvaluateUnlock(stageDefinition, progressRuntime, out string unlockReason);
        if (isUnlocked == false)
        {
            return new StageAvailabilityResult(false, false, false, unlockReason);
        }

        if (stageDefinition.IsEntryEnabled == false)
        {
            return new StageAvailabilityResult(true, false, false, "Entry disabled");
        }

        bool isMultiplayerAvailable = stageDefinition.AllowMultiplayer;
        return new StageAvailabilityResult(true, true, isMultiplayerAvailable, string.Empty);
    }

    /// <summary>
    /// 스테이지 개방 조건을 모두 검사해 개방 여부를 반환합니다.
    /// </summary>
    private static bool EvaluateUnlock(StageDefinition stageDefinition, StageProgressRuntime progressRuntime, out string reason)
    {
        reason = string.Empty;

        IReadOnlyList<StageUnlockConditionData> conditions = stageDefinition.UnlockConditions;
        if ((conditions == null || conditions.Count == 0) && stageDefinition.IsInitiallyUnlocked)
        {
            return true;
        }

        bool passedByDefault = stageDefinition.IsInitiallyUnlocked; // 조건 목록이 있어도 기본 개방으로 취급할지 결정하는 플래그 값입니다.
        bool hasAnyCondition = conditions != null && conditions.Count > 0;

        if (!hasAnyCondition)
        {
            if (passedByDefault)
            {
                return true;
            }

            reason = "No unlock condition met";
            return false;
        }

        for (int i = 0; i < conditions.Count; i++)
        {
            StageUnlockConditionData condition = conditions[i]; // 현재 판정 중인 개방 조건 데이터입니다.
            if (condition == null)
            {
                continue;
            }

            if (!EvaluateCondition(condition, progressRuntime, out string failReason))
            {
                reason = failReason;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 조건 1개를 진행도에 대입해 만족 여부를 반환합니다.
    /// </summary>
    private static bool EvaluateCondition(StageUnlockConditionData condition, StageProgressRuntime progressRuntime, out string reason)
    {
        reason = string.Empty;

        switch (condition.ConditionType)
        {
            case E_StageUnlockConditionType.None:
                return true;

            case E_StageUnlockConditionType.ClearSpecificStage:
                {
                    string requiredStageId = condition.RequiredStageId;
                    if (string.IsNullOrWhiteSpace(requiredStageId))
                    {
                        reason = "Required stage id is empty";
                        return false;
                    }

                    bool isCleared = progressRuntime != null && progressRuntime.IsStageCleared(requiredStageId);
                    if (!isCleared)
                    {
                        reason = $"Need clear stage: {requiredStageId}";
                        return false;
                    }

                    return true;
                }

            case E_StageUnlockConditionType.ReachTotalClearCount:
                {
                    int requiredCount = Mathf.Max(0, condition.RequiredTotalClearCount);
                    int currentCount = progressRuntime != null ? progressRuntime.GetTotalClearCount() : 0;
                    if (currentCount < requiredCount)
                    {
                        reason = $"Need total clear count: {requiredCount}";
                        return false;
                    }

                    return true;
                }

            default:
                reason = $"Unsupported condition type: {condition.ConditionType}";
                return false;
        }
    }
}
