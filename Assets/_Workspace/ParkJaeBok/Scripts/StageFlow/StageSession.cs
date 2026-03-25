using UnityEngine;

/// <summary>
/// 씬 전환 사이에서 스테이지 이동 문맥을 유지하는 세션 저장소입니다.
/// </summary>
public class StageSession : MonoBehaviour
{
    private static StageSession _instance; // 전역 접근을 위한 StageSession 싱글톤 인스턴스입니다.

    [Tooltip("씬 전환 후에도 StageSession 오브젝트를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // StageSession의 생명주기를 씬 전환과 분리할지 여부입니다.

    private string _selectedStageId; // 플레이어가 마지막으로 선택한 스테이지 ID입니다.
    private string _targetStageEntryPointId; // 다음 로드 씬에서 사용할 스테이지 진입 포인트 ID입니다.
    private string _targetTownReturnPointId; // 마을 복귀 시 사용할 포인트 ID입니다.
    private E_BgmContextType _requestedBgmContextType = E_BgmContextType.None; // 다음 씬에서 적용 요청된 BGM 컨텍스트 타입입니다.

    /// <summary>
    /// 전역 StageSession 인스턴스를 반환합니다.
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
                    GameObject sessionObject = new GameObject("--- Stage Session ---"); // 런타임 자동 생성용 StageSession 오브젝트입니다.
                    _instance = sessionObject.AddComponent<StageSession>();
                }
            }

            return _instance;
        }
    }

    /// <summary>
    /// 현재 선택된 스테이지 ID를 반환합니다.
    /// </summary>
    public string SelectedStageId => _selectedStageId;

    /// <summary>
    /// 다음 씬에서 사용할 스테이지 진입 포인트 ID를 반환합니다.
    /// </summary>
    public string TargetStageEntryPointId => _targetStageEntryPointId;

    /// <summary>
    /// 마을 복귀 시 사용할 포인트 ID를 반환합니다.
    /// </summary>
    public string TargetTownReturnPointId => _targetTownReturnPointId;

    /// <summary>
    /// 다음 씬 진입 시 요청된 BGM 컨텍스트 타입을 반환합니다.
    /// </summary>
    public E_BgmContextType RequestedBgmContextType => _requestedBgmContextType;

    /// <summary>
    /// 싱글톤 중복을 방지하고 필요 시 DDOL을 설정합니다.
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
        if (stageDefinition == null)
        {
            Debug.LogWarning("[StageSession] stageDefinition이 null이라 SetNextStage를 건너뜁니다.", this);
            return;
        }

        _selectedStageId = stageDefinition.StageId;
        _targetStageEntryPointId = stageDefinition.StageEntryPointId;
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
        E_BgmContextType consumedContextType = _requestedBgmContextType; // 소비 후 반환할 요청 컨텍스트 값입니다.
        _requestedBgmContextType = E_BgmContextType.None;
        return consumedContextType;
    }
}
