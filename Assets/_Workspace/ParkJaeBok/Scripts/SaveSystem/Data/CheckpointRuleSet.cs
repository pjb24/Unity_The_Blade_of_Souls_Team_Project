using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 체크포인트 복원 여부를 기본값 + ID 오버라이드로 제어하는 정책 데이터입니다.
/// </summary>
[CreateAssetMenu(fileName = "CheckpointRuleSet", menuName = "Game/Save System/Rule Sets/Checkpoint Rule Set")]
public class CheckpointRuleSet : ScriptableObject
{
    [Serializable]
    private struct CheckpointOverrideEntry
    {
        [Tooltip("복원 정책 오버라이드를 적용할 체크포인트 ID입니다.")]
        [SerializeField] private string _checkpointId; // 오버라이드 대상 체크포인트 식별자입니다.

        [Tooltip("해당 체크포인트 ID의 복원 허용 여부입니다.")]
        [SerializeField] private bool _allowRestore; // 오버라이드 대상 체크포인트의 복원 허용 여부입니다.

        public string CheckpointId => _checkpointId;
        public bool AllowRestore => _allowRestore;
    }

    [Tooltip("오버라이드가 없을 때 적용할 체크포인트 기본 복원 동작입니다.")]
    [SerializeField] private bool _defaultRestore = true; // 체크포인트 복원의 기본 허용 여부입니다.

    [Tooltip("체크포인트 ID별 복원 동작 오버라이드 목록입니다.")]
    [SerializeField] private List<CheckpointOverrideEntry> _overrides = new List<CheckpointOverrideEntry>(); // 체크포인트별 복원 오버라이드 규칙 목록입니다.

    /// <summary>
    /// 오버라이드 규칙이 없을 때 사용할 기본 복원 허용 여부를 반환합니다.
    /// </summary>
    public bool DefaultRestore => _defaultRestore;

    /// <summary>
    /// 체크포인트 ID 기반으로 복원 허용 여부를 해석합니다.
    /// </summary>
    public bool Resolve(string checkpointId)
    {
        if (TryFindOverride(checkpointId, out bool allowRestore))
        {
            return allowRestore;
        }

        return _defaultRestore;
    }

    /// <summary>
    /// 체크포인트 ID에 대응하는 오버라이드 값을 탐색합니다.
    /// </summary>
    private bool TryFindOverride(string checkpointId, out bool allowRestore)
    {
        allowRestore = false;

        if (string.IsNullOrWhiteSpace(checkpointId) || _overrides == null)
        {
            return false;
        }

        for (int index = 0; index < _overrides.Count; index++)
        {
            CheckpointOverrideEntry entry = _overrides[index]; // 현재 검사 중인 체크포인트 오버라이드 엔트리입니다.
            if (string.Equals(entry.CheckpointId, checkpointId, StringComparison.Ordinal))
            {
                allowRestore = entry.AllowRestore;
                return true;
            }
        }

        return false;
    }
}
