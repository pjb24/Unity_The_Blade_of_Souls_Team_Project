using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 개방 조건 유형입니다.
/// </summary>
public enum E_StageUnlockConditionType
{
    None = 0,
    ClearSpecificStage = 1,
    ReachTotalClearCount = 2
}

/// <summary>
/// 스테이지 개방 판정에 사용하는 조건 데이터입니다.
/// </summary>
[Serializable]
public class StageUnlockConditionData
{
    [Tooltip("개방 조건 판정 방식입니다.")]
    [SerializeField] private E_StageUnlockConditionType _conditionType = E_StageUnlockConditionType.None; // 개방 조건 판정에 사용할 유형입니다.

    [Tooltip("ClearSpecificStage 조건에서 요구하는 스테이지 ID입니다.")]
    [SerializeField] private string _requiredStageId; // 선행 클리어가 필요한 스테이지 ID입니다.

    [Tooltip("ReachTotalClearCount 조건에서 요구하는 누적 클리어 횟수입니다.")]
    [Min(0)]
    [SerializeField] private int _requiredTotalClearCount = 1; // 누적 클리어 횟수 조건에서 요구하는 최소 값입니다.

    /// <summary>
    /// 개방 조건 유형을 반환합니다.
    /// </summary>
    public E_StageUnlockConditionType ConditionType => _conditionType;

    /// <summary>
    /// 요구 스테이지 ID를 반환합니다.
    /// </summary>
    public string RequiredStageId => _requiredStageId;

    /// <summary>
    /// 요구 누적 클리어 횟수를 반환합니다.
    /// </summary>
    public int RequiredTotalClearCount => _requiredTotalClearCount;
}

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

    [Tooltip("UI에 표시할 스테이지 이름입니다.")]
    [SerializeField] private string _displayName; // 맵 UI에서 사용할 스테이지 표시 이름입니다.

    [Tooltip("Town 씬 여부를 나타내는 플래그입니다. StageCatalog의 Town 검색에 사용됩니다.")]
    [SerializeField] private bool _isTownStage = false; // 이 스테이지를 Town Stage로 취급할지 여부입니다.

    [Header("Entry / Return")]
    [Tooltip("스테이지 씬에서 플레이어를 배치할 진입 포인트 ID입니다.")]
    [SerializeField] private string _stageEntryPointId = "Default"; // 스테이지 진입 시 StageEntryPoint와 매칭할 ID입니다.

    [Tooltip("마을로 복귀할 때 플레이어를 배치할 포인트 ID입니다.")]
    [SerializeField] private string _townReturnPointId = "Town_Default"; // 마을 복귀 시 사용할 포인트 ID입니다.

    [Tooltip("현재 빌드/운영 상태에서 해당 스테이지 입장을 허용할지 여부입니다.")]
    [SerializeField] private bool _isEntryEnabled = true; // 운영 이슈 대응을 위해 스테이지 입장 허용 여부를 제어하는 플래그입니다.

    [Header("Unlock")]
    [Tooltip("true면 조건과 관계없이 기본 개방 상태로 취급합니다.")]
    [SerializeField] private bool _isInitiallyUnlocked = true; // 개방 조건 이전에 기본 개방 여부를 제어하는 플래그입니다.

    [Tooltip("개방 판정에 사용할 조건 목록입니다. 비어 있으면 기본 개방 플래그만 사용합니다.")]
    [SerializeField] private List<StageUnlockConditionData> _unlockConditions = new List<StageUnlockConditionData>(); // 스테이지 개방 여부를 계산할 조건 목록입니다.

    [Header("Multiplayer")]
    [Tooltip("이 스테이지에서 멀티플레이 입장을 허용할지 여부입니다.")]
    [SerializeField] private bool _allowMultiplayer = false; // 스테이지 멀티플레이 허용 여부입니다.

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
    /// UI 표시 이름을 반환합니다.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? _stageId : _displayName;

    /// <summary>
    /// Town 스테이지 여부를 반환합니다.
    /// </summary>
    public bool IsTownStage => _isTownStage;

    /// <summary>
    /// 스테이지 진입 포인트 ID를 반환합니다.
    /// </summary>
    public string StageEntryPointId => _stageEntryPointId;

    /// <summary>
    /// 마을 복귀 포인트 ID를 반환합니다.
    /// </summary>
    public string TownReturnPointId => _townReturnPointId;

    /// <summary>
    /// 기본 개방 상태 여부를 반환합니다.
    /// </summary>
    public bool IsInitiallyUnlocked => _isInitiallyUnlocked;

    /// <summary>
    /// 스테이지 개방 조건 목록을 읽기 전용으로 반환합니다.
    /// </summary>
    public IReadOnlyList<StageUnlockConditionData> UnlockConditions => _unlockConditions;

    /// <summary>
    /// 스테이지 입장 허용 여부를 반환합니다.
    /// </summary>
    public bool IsEntryEnabled => _isEntryEnabled;

    /// <summary>
    /// 스테이지 멀티플레이 허용 여부를 반환합니다.
    /// </summary>
    public bool AllowMultiplayer => _allowMultiplayer;

    /// <summary>
    /// 스테이지 권장 BGM 컨텍스트 타입을 반환합니다.
    /// </summary>
    public E_BgmContextType BgmContextType => _bgmContextType;
}
