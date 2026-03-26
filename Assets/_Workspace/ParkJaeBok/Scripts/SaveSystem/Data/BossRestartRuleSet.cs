using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스전 재시작/복원 규칙을 기본값 + ID 오버라이드로 제어하는 정책 데이터입니다.
/// </summary>
[CreateAssetMenu(fileName = "BossRestartRuleSet", menuName = "Game/Save System/Rule Sets/Boss Restart Rule Set")]
public class BossRestartRuleSet : ScriptableObject
{
    /// <summary>
    /// 보스전 재시작 모드입니다.
    /// </summary>
    public enum RestartMode
    {
        FullReset = 0,
        PhaseCheckpoint = 1,
        KeepDefeated = 2,
    }

    /// <summary>
    /// 보스 체력 복원 모드입니다.
    /// </summary>
    public enum HpRestoreMode
    {
        Full = 0,
        Saved = 1,
        KeepDefeatedZero = 2,
    }

    /// <summary>
    /// 보스 재시작 규칙 해석 결과 데이터입니다.
    /// </summary>
    [Serializable]
    public struct ResolvedRule
    {
        public RestartMode RestartMode; // 보스전 재시작 모드입니다.
        public HpRestoreMode HpRestoreMode; // 보스 체력 복원 모드입니다.
        public bool ArenaReset; // 보스 아레나 연출/상태 초기화 여부입니다.

        /// <summary>
        /// 기본 보스 재시작 규칙 결과를 생성합니다.
        /// </summary>
        public static ResolvedRule CreateDefault()
        {
            return new ResolvedRule
            {
                RestartMode = RestartMode.FullReset,
                HpRestoreMode = HpRestoreMode.Full,
                ArenaReset = true
            };
        }
    }

    [Serializable]
    private struct BossOverrideEntry
    {
        [Tooltip("복원 정책 오버라이드를 적용할 보스전 ID입니다.")]
        [SerializeField] private string _bossEncounterId; // 오버라이드 대상 보스전 식별자입니다.

        [Tooltip("해당 보스전의 재시작 모드입니다.")]
        [SerializeField] private RestartMode _restartMode; // 오버라이드 대상 보스전 재시작 모드입니다.

        [Tooltip("해당 보스전의 체력 복원 모드입니다.")]
        [SerializeField] private HpRestoreMode _hpRestoreMode; // 오버라이드 대상 보스전 체력 복원 모드입니다.

        [Tooltip("해당 보스전 복원 시 아레나 상태를 초기화할지 여부입니다.")]
        [SerializeField] private bool _arenaReset; // 오버라이드 대상 보스전 아레나 초기화 여부입니다.

        public string BossEncounterId => _bossEncounterId;
        public RestartMode RestartMode => _restartMode;
        public HpRestoreMode HpRestoreMode => _hpRestoreMode;
        public bool ArenaReset => _arenaReset;
    }

    [Header("Default Rule")]
    [Tooltip("오버라이드가 없을 때 적용할 기본 보스 재시작 모드입니다.")]
    [SerializeField] private RestartMode _defaultRestartMode = RestartMode.FullReset; // 오버라이드 미매칭 시 보스 재시작 모드입니다.

    [Tooltip("오버라이드가 없을 때 적용할 기본 체력 복원 모드입니다.")]
    [SerializeField] private HpRestoreMode _defaultHpRestoreMode = HpRestoreMode.Full; // 오버라이드 미매칭 시 보스 체력 복원 모드입니다.

    [Tooltip("오버라이드가 없을 때 적용할 기본 아레나 초기화 여부입니다.")]
    [SerializeField] private bool _defaultArenaReset = true; // 오버라이드 미매칭 시 아레나 초기화 여부입니다.

    [Header("Overrides")]
    [Tooltip("보스전 ID별 재시작/체력/아레나 오버라이드 규칙 목록입니다.")]
    [SerializeField] private List<BossOverrideEntry> _overrides = new List<BossOverrideEntry>(); // 보스전별 오버라이드 규칙 목록입니다.

    /// <summary>
    /// 보스전 ID 기반으로 재시작 규칙을 해석합니다.
    /// </summary>
    public ResolvedRule ResolveRule(string bossEncounterId)
    {
        if (TryFindOverride(bossEncounterId, out BossOverrideEntry overrideEntry))
        {
            return new ResolvedRule
            {
                RestartMode = overrideEntry.RestartMode,
                HpRestoreMode = overrideEntry.HpRestoreMode,
                ArenaReset = overrideEntry.ArenaReset
            };
        }

        return new ResolvedRule
        {
            RestartMode = _defaultRestartMode,
            HpRestoreMode = _defaultHpRestoreMode,
            ArenaReset = _defaultArenaReset
        };
    }

    /// <summary>
    /// 보스전 ID에 대응하는 오버라이드 규칙을 탐색합니다.
    /// </summary>
    private bool TryFindOverride(string bossEncounterId, out BossOverrideEntry overrideEntry)
    {
        overrideEntry = default;

        if (string.IsNullOrWhiteSpace(bossEncounterId) || _overrides == null)
        {
            return false;
        }

        for (int index = 0; index < _overrides.Count; index++)
        {
            BossOverrideEntry entry = _overrides[index]; // 현재 검사 중인 보스전 오버라이드 엔트리입니다.
            if (string.Equals(entry.BossEncounterId, bossEncounterId, StringComparison.Ordinal))
            {
                overrideEntry = entry;
                return true;
            }
        }

        return false;
    }
}
