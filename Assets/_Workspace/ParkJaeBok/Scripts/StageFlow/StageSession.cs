using UnityEngine;

/// <summary>
/// 씬 전환 사이에서 스테이지 선택과 복귀 지점 정보를 유지하는 런타임 세션 저장소입니다.
/// </summary>
public class StageSession : MonoBehaviour
{
    /// <summary>
    /// StageSession의 저장 가능한 현재 상태 데이터입니다.
    /// </summary>
    [System.Serializable]
    public struct SnapshotData
    {
        [Tooltip("마지막으로 선택한 스테이지 ID입니다.")]
        public string SelectedStageId; // 마지막으로 선택한 스테이지 ID입니다.

        [Tooltip("다음 씬 진입에 사용할 스테이지 엔트리 포인트 ID입니다.")]
        public string TargetStageEntryPointId; // 다음 씬 진입에 사용할 스테이지 엔트리 포인트 ID입니다.

        [Tooltip("마을 복귀 때 사용할 엔트리 포인트 ID입니다.")]
        public string TargetTownReturnPointId; // 마을 복귀 때 사용할 엔트리 포인트 ID입니다.

        [Tooltip("다음 씬에 적용할 BGM 컨텍스트 타입입니다.")]
        public E_BgmContextType RequestedBgmContextType; // 다음 씬에 적용할 BGM 컨텍스트 타입입니다.
    }

    private static StageSession _instance; // 전역 접근에 사용할 StageSession 단일 인스턴스입니다.

    [Tooltip("씬 전환 뒤에도 StageSession 오브젝트를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // StageSession의 생명주기를 씬 전환과 분리할지 여부입니다.

    [Header("Debug (Runtime State)")]
    [Tooltip("디버그용: 플레이어가 마지막으로 선택한 스테이지 ID입니다.")]
    [SerializeField] private string _selectedStageId; // 플레이어가 마지막으로 선택한 스테이지 ID입니다.

    [Tooltip("디버그용: 다음 로드 씬에서 사용할 스테이지 진입 포인트 ID입니다.")]
    [SerializeField] private string _targetStageEntryPointId; // 다음 로드 씬에서 사용할 스테이지 진입 포인트 ID입니다.

    [Tooltip("디버그용: 마을 복귀 때 사용할 포인트 ID입니다.")]
    [SerializeField] private string _targetTownReturnPointId; // 마을 복귀 때 사용할 포인트 ID입니다.

    [Tooltip("디버그용: 다음 씬에 적용 요청된 BGM 컨텍스트 타입입니다.")]
    [SerializeField] private E_BgmContextType _requestedBgmContextType = E_BgmContextType.None; // 다음 씬에 적용 요청된 BGM 컨텍스트 타입입니다.

    /// <summary>
    /// 전역 StageSession 인스턴스를 반환하고, 없으면 새로 생성합니다.
    /// </summary>
    public static StageSession Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<StageSession>();
                if (_instance == null)
                {
                    GameObject sessionObject = new GameObject("--- Stage Session ---"); // 자동 생성된 StageSession 오브젝트입니다.
                    _instance = sessionObject.AddComponent<StageSession>();
                }
            }

            return _instance;
        }
    }

    /// <summary>
    /// 새 인스턴스를 생성하지 않고 현재 존재하는 StageSession을 반환합니다.
    /// </summary>
    public static bool TryGetExistingInstance(out StageSession session)
    {
        session = _instance != null ? _instance : FindAnyObjectByType<StageSession>();
        return session != null;
    }

    /// <summary>
    /// 현재 선택한 스테이지 ID를 반환합니다.
    /// </summary>
    public string SelectedStageId => _selectedStageId;

    /// <summary>
    /// 다음 씬에서 사용할 스테이지 진입 포인트 ID를 반환합니다.
    /// </summary>
    public string TargetStageEntryPointId => _targetStageEntryPointId;

    /// <summary>
    /// 마을 복귀 때 사용할 포인트 ID를 반환합니다.
    /// </summary>
    public string TargetTownReturnPointId => _targetTownReturnPointId;

    /// <summary>
    /// 다음 씬 진입 때 요청된 BGM 컨텍스트 타입을 반환합니다.
    /// </summary>
    public E_BgmContextType RequestedBgmContextType => _requestedBgmContextType;

    /// <summary>
    /// 싱글턴 중복을 방지하고 필요한 경우 씬 전환 뒤에도 유지되도록 설정합니다.
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[StageSession] 중복 StageSession이 감지되어 새 인스턴스를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    /// <summary>
    /// 다음 스테이지 진입 문맥을 저장합니다.
    /// </summary>
    public void SetNextStage(StageDefinition stageDefinition)
    {
        SetNextStage(stageDefinition, null);
    }

    /// <summary>
    /// 다음 스테이지 진입 문맥을 저장하면서 필요한 엔트리 포인트를 덮어씁니다.
    /// </summary>
    public void SetNextStage(StageDefinition stageDefinition, string entryPointOverrideId)
    {
        if (stageDefinition == null)
        {
            Debug.LogWarning("[StageSession] stageDefinition이 null이라 SetNextStage를 건너뜁니다.", this);
            return;
        }

        _selectedStageId = stageDefinition.StageId;
        _targetStageEntryPointId = string.IsNullOrWhiteSpace(entryPointOverrideId) ? stageDefinition.StageEntryPointId : entryPointOverrideId;
        _targetTownReturnPointId = stageDefinition.TownReturnPointId;
        _requestedBgmContextType = stageDefinition.BgmContextType;
    }

    /// <summary>
    /// 마을 복귀 포인트 ID를 갱신합니다.
    /// </summary>
    public void SetTownReturnPoint(string townReturnPointId)
    {
        _targetTownReturnPointId = townReturnPointId;
    }

    /// <summary>
    /// 씬 진입에 사용했던 임시 진입 포인트 정보를 초기화합니다.
    /// </summary>
    public void ConsumeEntryPoint()
    {
        _targetStageEntryPointId = string.Empty;
    }

    /// <summary>
    /// 요청된 BGM 컨텍스트를 반환하고 세션 값을 초기화합니다.
    /// </summary>
    public E_BgmContextType ConsumeRequestedBgmContextType()
    {
        E_BgmContextType consumedContextType = _requestedBgmContextType; // 소비 뒤 반환할 요청 컨텍스트 값입니다.
        _requestedBgmContextType = E_BgmContextType.None;
        return consumedContextType;
    }

    /// <summary>
    /// 현재 StageSession 상태를 저장 가능한 구조체로 반환합니다.
    /// </summary>
    public SnapshotData CreateSnapshot()
    {
        return new SnapshotData
        {
            SelectedStageId = _selectedStageId,
            TargetStageEntryPointId = _targetStageEntryPointId,
            TargetTownReturnPointId = _targetTownReturnPointId,
            RequestedBgmContextType = _requestedBgmContextType
        };
    }

    /// <summary>
    /// 전달된 구조체 데이터를 StageSession에 적용합니다.
    /// </summary>
    public void ApplySnapshot(SnapshotData snapshot)
    {
        _selectedStageId = snapshot.SelectedStageId;
        _targetStageEntryPointId = snapshot.TargetStageEntryPointId;
        _targetTownReturnPointId = snapshot.TargetTownReturnPointId;
        _requestedBgmContextType = snapshot.RequestedBgmContextType;
    }
}
