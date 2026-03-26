using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기믹 상태 복원 허용 여부와 복원 시점을 기본값 + ID 오버라이드로 제어하는 정책 데이터입니다.
/// </summary>
[CreateAssetMenu(fileName = "GimmickRestoreRuleSet", menuName = "Game/Save System/Rule Sets/Gimmick Restore Rule Set")]
public class GimmickRestoreRuleSet : ScriptableObject
{
    /// <summary>
    /// 기믹 상태 복원 적용 시점입니다.
    /// </summary>
    public enum RestoreTiming
    {
        AfterSceneLoad = 0,
        AfterPlayerSpawn = 1,
    }

    /// <summary>
    /// 기믹 복원 규칙 해석 결과 데이터입니다.
    /// </summary>
    [Serializable]
    public struct ResolvedRule
    {
        public bool AllowRestore; // 기믹 상태 복원 허용 여부입니다.
        public RestoreTiming Timing; // 기믹 상태 복원 적용 시점입니다.

        /// <summary>
        /// 기본 복원 규칙 결과를 생성합니다.
        /// </summary>
        public static ResolvedRule CreateDefault()
        {
            return new ResolvedRule
            {
                AllowRestore = true,
                Timing = RestoreTiming.AfterSceneLoad
            };
        }
    }

    [Serializable]
    private struct GimmickOverrideEntry
    {
        [Tooltip("복원 정책 오버라이드를 적용할 기믹 ID입니다.")]
        [SerializeField] private string _gimmickId; // 오버라이드 대상 기믹 식별자입니다.

        [Tooltip("해당 기믹 ID의 복원 허용 여부입니다.")]
        [SerializeField] private bool _allowRestore; // 오버라이드 대상 기믹의 복원 허용 여부입니다.

        [Tooltip("해당 기믹 ID의 복원 적용 시점입니다.")]
        [SerializeField] private RestoreTiming _timing; // 오버라이드 대상 기믹 복원 적용 시점입니다.

        public string GimmickId => _gimmickId;
        public bool AllowRestore => _allowRestore;
        public RestoreTiming Timing => _timing;
    }

    [Tooltip("오버라이드가 없을 때 적용할 기믹 복원 기본 허용 여부입니다.")]
    [SerializeField] private bool _defaultRestore = true; // 기믹 복원의 기본 허용 여부입니다.

    [Tooltip("오버라이드가 없을 때 적용할 기믹 복원 기본 시점입니다.")]
    [SerializeField] private RestoreTiming _defaultRestoreTiming = RestoreTiming.AfterSceneLoad; // 기믹 복원의 기본 적용 시점입니다.

    [Tooltip("기믹 ID별 복원 동작 오버라이드 목록입니다.")]
    [SerializeField] private List<GimmickOverrideEntry> _overrides = new List<GimmickOverrideEntry>(); // 기믹별 복원 오버라이드 규칙 목록입니다.

    /// <summary>
    /// 기믹 ID 기반으로 복원 규칙을 해석합니다.
    /// </summary>
    public ResolvedRule ResolveRule(string gimmickId)
    {
        if (TryFindOverride(gimmickId, out GimmickOverrideEntry overrideEntry))
        {
            return new ResolvedRule
            {
                AllowRestore = overrideEntry.AllowRestore,
                Timing = overrideEntry.Timing
            };
        }

        return new ResolvedRule
        {
            AllowRestore = _defaultRestore,
            Timing = _defaultRestoreTiming
        };
    }

    /// <summary>
    /// 기믹 ID에 대응하는 오버라이드 값을 탐색합니다.
    /// </summary>
    private bool TryFindOverride(string gimmickId, out GimmickOverrideEntry overrideEntry)
    {
        overrideEntry = default;

        if (string.IsNullOrWhiteSpace(gimmickId) || _overrides == null)
        {
            return false;
        }

        for (int index = 0; index < _overrides.Count; index++)
        {
            GimmickOverrideEntry entry = _overrides[index]; // 현재 검사 중인 기믹 오버라이드 엔트리입니다.
            if (string.Equals(entry.GimmickId, gimmickId, StringComparison.Ordinal))
            {
                overrideEntry = entry;
                return true;
            }
        }

        return false;
    }
}
