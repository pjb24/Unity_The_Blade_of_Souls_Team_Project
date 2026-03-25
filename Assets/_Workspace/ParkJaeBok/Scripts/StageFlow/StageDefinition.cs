using UnityEngine;

/// <summary>
/// 스테이지 1개를 데이터로 정의하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "StageDefinition", menuName = "Game/Stage Flow/Stage Definition")]
public class StageDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("스테이지를 식별하는 고유 ID입니다.")]
    [SerializeField] private string _stageId; // 스테이지를 코드/데이터에서 식별할 때 사용할 고유 ID입니다.

    [Tooltip("실제로 로드할 씬 이름입니다. Build Settings에 등록되어 있어야 합니다.")]
    [SerializeField] private string _sceneName; // SceneTransitionService가 로드할 씬 이름입니다.

    [Header("Entry / Return")]
    [Tooltip("스테이지 씬에서 플레이어를 배치할 진입 포인트 ID입니다.")]
    [SerializeField] private string _stageEntryPointId = "Default"; // 스테이지 진입 시 StageEntryPoint와 매칭할 ID입니다.

    [Tooltip("마을로 복귀할 때 플레이어를 배치할 포인트 ID입니다.")]
    [SerializeField] private string _townReturnPointId = "Town_Default"; // 마을 복귀 시 사용할 포인트 ID입니다.

    [Header("Audio Context")]
    [Tooltip("이 스테이지가 활성화될 때 권장되는 BGM 컨텍스트 타입입니다.")]
    [SerializeField] private E_BgmContextType _bgmContextType = E_BgmContextType.Exploration; // 스테이지 진입 시 활성화할 BGM 컨텍스트 타입입니다.

    /// <summary>
    /// 스테이지 고유 ID를 반환합니다.
    /// </summary>
    public string StageId => _stageId;

    /// <summary>
    /// 로드 대상 씬 이름을 반환합니다.
    /// </summary>
    public string SceneName => _sceneName;

    /// <summary>
    /// 스테이지 진입 포인트 ID를 반환합니다.
    /// </summary>
    public string StageEntryPointId => _stageEntryPointId;

    /// <summary>
    /// 마을 복귀 포인트 ID를 반환합니다.
    /// </summary>
    public string TownReturnPointId => _townReturnPointId;

    /// <summary>
    /// 스테이지 권장 BGM 컨텍스트 타입을 반환합니다.
    /// </summary>
    public E_BgmContextType BgmContextType => _bgmContextType;
}
