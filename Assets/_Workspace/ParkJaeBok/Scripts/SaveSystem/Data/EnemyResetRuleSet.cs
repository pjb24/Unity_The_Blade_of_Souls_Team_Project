using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 복원/리셋 규칙과 Save 대상 필터를 정의하는 정책 데이터입니다.
/// </summary>
[CreateAssetMenu(fileName = "EnemyResetRuleSet", menuName = "Game/Save System/Rule Sets/Enemy Reset Rule Set")]
public class EnemyResetRuleSet : ScriptableObject
{
    /// <summary>
    /// 복원 규칙 매칭 시 사용할 기준 키 타입입니다.
    /// </summary>
    public enum MatchKeyType
    {
        EnemyRuntimeId = 0,
        ArchetypeId = 1,
    }

    /// <summary>
    /// 복원 시 위치 적용 모드입니다.
    /// </summary>
    public enum RestorePositionMode
    {
        Spawn = 0,
        LastKnown = 1,
        CheckpointArea = 2,
    }

    /// <summary>
    /// Enemy 리셋/복원 규칙 해석 결과 데이터입니다.
    /// </summary>
    [Serializable]
    public struct ResolvedRule
    {
        public bool ResetOnRecovery; // 복구 시 Enemy를 초기화할지 여부입니다.
        public bool RespawnIfDead; // 사망 상태 Enemy를 부활시킬지 여부입니다.
        public float RestoreHpPercent; // 복원 시 적용할 체력 비율 배수입니다.
        public RestorePositionMode RestorePositionMode; // 복원 시 적용할 위치 모드입니다.

        /// <summary>
        /// 기본 복원 규칙 결과를 생성합니다.
        /// </summary>
        public static ResolvedRule CreateDefault()
        {
            return new ResolvedRule
            {
                ResetOnRecovery = true,
                RespawnIfDead = true,
                RestoreHpPercent = 1f,
                RestorePositionMode = RestorePositionMode.Spawn
            };
        }
    }

    [Serializable]
    private struct EnemyOverrideEntry
    {
        [Tooltip("오버라이드 매칭 기준 키 타입입니다.")]
        [SerializeField] private MatchKeyType _matchKeyType; // 오버라이드 규칙 매칭에 사용할 키 타입입니다.

        [Tooltip("오버라이드 매칭 키 문자열입니다. EnemyRuntimeId 또는 ArchetypeId를 입력합니다.")]
        [SerializeField] private string _matchKey; // 오버라이드 규칙 매칭에 사용할 키 문자열입니다.

        [Tooltip("복구 시 Enemy를 초기화할지 여부입니다.")]
        [SerializeField] private bool _resetOnRecovery; // 오버라이드 대상 Enemy의 복구 초기화 여부입니다.

        [Tooltip("사망 상태인 Enemy를 복구 시 부활시킬지 여부입니다.")]
        [SerializeField] private bool _respawnIfDead; // 오버라이드 대상 Enemy의 사망 상태 부활 허용 여부입니다.

        [Tooltip("복구 시 적용할 체력 비율 배수입니다. 1이면 원본 유지, 0.5면 절반 복원입니다.")]
        [SerializeField, Range(0f, 1f)] private float _restoreHpPercent; // 오버라이드 대상 Enemy 복원 체력 비율 배수입니다.

        [Tooltip("복구 시 적용할 위치 모드입니다.")]
        [SerializeField] private RestorePositionMode _restorePositionMode; // 오버라이드 대상 Enemy 복원 위치 모드입니다.

        public MatchKeyType MatchKeyType => _matchKeyType;
        public string MatchKey => _matchKey;
        public bool ResetOnRecovery => _resetOnRecovery;
        public bool RespawnIfDead => _respawnIfDead;
        public float RestoreHpPercent => _restoreHpPercent;
        public RestorePositionMode RestorePositionMode => _restorePositionMode;
    }

    [Header("Default Rule")]
    [Tooltip("오버라이드가 없을 때 복구 시 Enemy를 초기화할지 여부입니다.")]
    [SerializeField] private bool _defaultResetOnRecovery = true; // 오버라이드 미매칭 시 적용할 Enemy 초기화 여부입니다.

    [Tooltip("오버라이드가 없을 때 사망 Enemy를 부활시킬지 여부입니다.")]
    [SerializeField] private bool _defaultRespawnIfDead = true; // 오버라이드 미매칭 시 적용할 사망 Enemy 부활 허용 여부입니다.

    [Tooltip("오버라이드가 없을 때 적용할 복원 체력 비율 배수입니다.")]
    [SerializeField, Range(0f, 1f)] private float _defaultRestoreHpPercent = 1f; // 오버라이드 미매칭 시 적용할 복원 체력 비율 배수입니다.

    [Tooltip("오버라이드가 없을 때 적용할 복원 위치 모드입니다.")]
    [SerializeField] private RestorePositionMode _defaultRestorePositionMode = RestorePositionMode.Spawn; // 오버라이드 미매칭 시 적용할 복원 위치 모드입니다.

    [Header("Overrides")]
    [Tooltip("EnemyRuntimeId/ArchetypeId 기반 오버라이드 규칙 목록입니다.")]
    [SerializeField] private List<EnemyOverrideEntry> _overrides = new List<EnemyOverrideEntry>(); // 개별 Enemy/아키타입 오버라이드 규칙 목록입니다.

    [Header("Save Target Filter")]
    [Tooltip("비활성 Enemy는 저장 대상에서 제외할지 여부입니다.")]
    [SerializeField] private bool _saveOnlyActiveEnemies = true; // 저장 시 비활성 Enemy 제외 여부입니다.

    [Tooltip("비어 있지 않으면 해당 태그를 가진 Enemy만 저장 대상으로 허용합니다.")]
    [SerializeField] private string _requiredTagForSave = string.Empty; // 저장 대상을 태그로 제한할 때 사용할 필수 태그입니다.

    [Tooltip("플레이어와 거리 기반 저장 대상 제한 거리입니다. 0 이하면 거리 제한을 사용하지 않습니다.")]
    [SerializeField] private float _maxSaveDistanceFromPlayer = 0f; // 저장 대상을 플레이어와의 거리로 제한할 최대 거리입니다.

    /// <summary>
    /// EnemyRuntimeId/ArchetypeId를 기준으로 복원 규칙을 해석합니다.
    /// </summary>
    public ResolvedRule Resolve(string enemyRuntimeId, string archetypeId)
    {
        if (TryFindOverride(enemyRuntimeId, archetypeId, out EnemyOverrideEntry overrideEntry))
        {
            return new ResolvedRule
            {
                ResetOnRecovery = overrideEntry.ResetOnRecovery,
                RespawnIfDead = overrideEntry.RespawnIfDead,
                RestoreHpPercent = Mathf.Clamp01(overrideEntry.RestoreHpPercent),
                RestorePositionMode = overrideEntry.RestorePositionMode
            };
        }

        return new ResolvedRule
        {
            ResetOnRecovery = _defaultResetOnRecovery,
            RespawnIfDead = _defaultRespawnIfDead,
            RestoreHpPercent = Mathf.Clamp01(_defaultRestoreHpPercent),
            RestorePositionMode = _defaultRestorePositionMode
        };
    }

    /// <summary>
    /// Save 대상 Enemy를 활성/태그/거리 기준으로 필터링합니다.
    /// </summary>
    public bool ShouldIncludeForSave(Transform enemyTransform, Transform playerTransform)
    {
        if (enemyTransform == null)
        {
            return false;
        }

        if (_saveOnlyActiveEnemies && !enemyTransform.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_requiredTagForSave) && !enemyTransform.CompareTag(_requiredTagForSave))
        {
            return false;
        }

        if (_maxSaveDistanceFromPlayer > 0f && playerTransform != null)
        {
            float distance = Vector3.Distance(enemyTransform.position, playerTransform.position); // 거리 기반 필터링에 사용할 Enemy-플레이어 거리입니다.
            if (distance > _maxSaveDistanceFromPlayer)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 오버라이드 목록에서 EnemyRuntimeId/ArchetypeId 매칭 규칙을 탐색합니다.
    /// </summary>
    private bool TryFindOverride(string enemyRuntimeId, string archetypeId, out EnemyOverrideEntry overrideEntry)
    {
        overrideEntry = default;

        if (_overrides == null || _overrides.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < _overrides.Count; index++)
        {
            EnemyOverrideEntry candidate = _overrides[index]; // 현재 검사 중인 Enemy 오버라이드 엔트리입니다.
            if (string.IsNullOrWhiteSpace(candidate.MatchKey))
            {
                continue;
            }

            if (candidate.MatchKeyType == MatchKeyType.EnemyRuntimeId && string.Equals(candidate.MatchKey, enemyRuntimeId, StringComparison.Ordinal))
            {
                overrideEntry = candidate;
                return true;
            }

            if (candidate.MatchKeyType == MatchKeyType.ArchetypeId && string.Equals(candidate.MatchKey, archetypeId, StringComparison.Ordinal))
            {
                overrideEntry = candidate;
                return true;
            }
        }

        return false;
    }
}
