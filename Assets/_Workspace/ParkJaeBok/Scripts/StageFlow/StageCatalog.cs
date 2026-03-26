using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프로젝트에서 사용하는 스테이지 정의 목록을 관리하는 카탈로그입니다.
/// </summary>
[CreateAssetMenu(fileName = "StageCatalog", menuName = "Game/Stage Flow/Stage Catalog")]
public class StageCatalog : ScriptableObject
{
    [Tooltip("게임에서 선택/진입 가능한 StageDefinition 목록입니다.")]
    [SerializeField] private List<StageDefinition> _stages = new List<StageDefinition>(); // 스테이지 조회 시 참조할 StageDefinition 목록입니다.

    [Tooltip("Town 스테이지로 사용할 StageId입니다. 비어 있으면 IsTownStage=true인 항목을 자동 탐색합니다.")]
    [SerializeField] private string _townStageId; // Town 스테이지를 명시적으로 지정할 때 사용하는 StageId입니다.

    /// <summary>
    /// 모든 스테이지 목록을 읽기 전용으로 반환합니다.
    /// </summary>
    public IReadOnlyList<StageDefinition> Stages => _stages;

    /// <summary>
    /// stageId로 스테이지 정의를 조회합니다.
    /// </summary>
    public bool TryGetById(string stageId, out StageDefinition stage)
    {
        stage = null;

        if (string.IsNullOrWhiteSpace(stageId))
        {
            return false;
        }

        for (int i = 0; i < _stages.Count; i++)
        {
            StageDefinition candidate = _stages[i]; // 현재 비교 중인 스테이지 정의입니다.
            if (candidate == null)
            {
                continue;
            }

            if (candidate.StageId == stageId)
            {
                stage = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 인덱스로 스테이지 정의를 조회합니다.
    /// </summary>
    public bool TryGetByIndex(int index, out StageDefinition stage)
    {
        stage = null;

        if (index < 0 || index >= _stages.Count)
        {
            return false;
        }

        stage = _stages[index];
        return stage != null;
    }

    /// <summary>
    /// Town 스테이지 정의를 조회합니다.
    /// </summary>
    public bool TryGetTownStage(out StageDefinition townStage)
    {
        townStage = null;

        if (string.IsNullOrWhiteSpace(_townStageId) == false)
        {
            if (TryGetById(_townStageId, out StageDefinition explicitTownStage))
            {
                townStage = explicitTownStage;
                return true;
            }
        }

        for (int i = 0; i < _stages.Count; i++)
        {
            StageDefinition candidate = _stages[i]; // Town 플래그를 검사할 스테이지 후보입니다.
            if (candidate == null)
            {
                continue;
            }

            if (candidate.IsTownStage)
            {
                townStage = candidate;
                return true;
            }
        }

        return false;
    }
}
