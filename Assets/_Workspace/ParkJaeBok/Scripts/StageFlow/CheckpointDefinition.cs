using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 체크포인트 1개를 데이터로 정의하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "CheckpointDefinition", menuName = "Game/Stage Flow/Checkpoint Definition")]
public class CheckpointDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("체크포인트를 식별하는 고유 ID입니다.")]
    [SerializeField] private string _checkpointId = "CP_Default"; // 체크포인트를 코드/데이터에서 식별할 때 사용할 고유 ID입니다.

    [Tooltip("이 체크포인트가 속한 씬 이름입니다.")]
    [SerializeField] private string _sceneName; // 체크포인트가 유효한 씬 이름입니다.

    [Header("Spawn")]
    [Tooltip("체크포인트 스폰 시 우선 참조할 StageEntryPoint ID입니다.")]
    [SerializeField] private string _entryPointId = "Default"; // 체크포인트가 우선 참조할 스테이지 엔트리 포인트 ID입니다.

    [Tooltip("EntryPoint 매칭 실패 시 사용할 월드 좌표입니다.")]
    [SerializeField] private Vector3 _worldPosition; // 엔트리 포인트를 찾지 못했을 때 사용할 월드 좌표입니다.

    [Tooltip("동일 조건 후보가 여러 개일 때 우선순위를 결정하는 값입니다. 값이 클수록 우선됩니다.")]
    [SerializeField] private int _priority; // 동일 체크포인트 후보 간 선택 우선순위 값입니다.

    [Tooltip("체크포인트 분류/검색에 사용할 태그 문자열 목록입니다.")]
    [SerializeField] private List<string> _tags = new List<string>(); // 체크포인트를 분류하기 위한 태그 목록입니다.

    public string CheckpointId => _checkpointId;
    public string SceneName => _sceneName;
    public string EntryPointId => _entryPointId;
    public Vector3 WorldPosition => _worldPosition;
    public int Priority => _priority;
    public IReadOnlyList<string> Tags => _tags;
}
