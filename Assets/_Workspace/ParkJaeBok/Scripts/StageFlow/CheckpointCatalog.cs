using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프로젝트에서 사용하는 체크포인트 정의 목록을 관리하는 카탈로그입니다.
/// </summary>
[CreateAssetMenu(fileName = "CheckpointCatalog", menuName = "Game/Stage Flow/Checkpoint Catalog")]
public class CheckpointCatalog : ScriptableObject
{
    [Tooltip("게임에서 사용할 CheckpointDefinition 목록입니다.")]
    [SerializeField] private List<CheckpointDefinition> _checkpoints = new List<CheckpointDefinition>(); // 체크포인트 조회 시 참조할 정의 목록입니다.

    /// <summary>
    /// 모든 체크포인트 목록을 읽기 전용으로 반환합니다.
    /// </summary>
    public IReadOnlyList<CheckpointDefinition> Checkpoints => _checkpoints;

    /// <summary>
    /// 체크포인트 ID와 씬 이름 기준으로 가장 우선순위가 높은 체크포인트를 조회합니다.
    /// </summary>
    public bool TryGetByIdAndScene(string checkpointId, string sceneName, out CheckpointDefinition checkpointDefinition)
    {
        checkpointDefinition = null;

        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            return false;
        }

        for (int index = 0; index < _checkpoints.Count; index++)
        {
            CheckpointDefinition candidate = _checkpoints[index]; // 현재 검사 중인 체크포인트 후보입니다.
            if (candidate == null)
            {
                continue;
            }

            if (!string.Equals(candidate.CheckpointId, checkpointId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(sceneName)
                && !string.IsNullOrWhiteSpace(candidate.SceneName)
                && !string.Equals(candidate.SceneName, sceneName, StringComparison.Ordinal))
            {
                continue;
            }

            if (checkpointDefinition == null || candidate.Priority > checkpointDefinition.Priority)
            {
                checkpointDefinition = candidate;
            }
        }

        return checkpointDefinition != null;
    }
}
