using UnityEngine;

/// <summary>
/// SaveCoordinator를 이용해 타이틀 메뉴의 세이브 존재 여부를 조회하는 서비스입니다.
/// </summary>
public class TitleSaveQueryService : MonoBehaviour, ITitleSaveQueryService
{
    [Tooltip("세이브 조회 시 우선 사용할 SaveCoordinator 참조입니다. 비어 있으면 SaveCoordinator.Instance를 사용합니다.")]
    [SerializeField] private SaveCoordinator _saveCoordinator; // 세이브 조회에 사용할 SaveCoordinator 참조입니다.

    /// <summary>
    /// Continue 버튼 활성화 여부를 반환합니다.
    /// </summary>
    public bool HasContinueData()
    {
        SaveCoordinator coordinator = ResolveCoordinator(); // 세이브 존재 여부 조회에 사용할 Coordinator 인스턴스입니다.
        if (coordinator == null)
        {
            return false;
        }

        return coordinator.HasChannelSnapshot(E_SaveChannelType.Persistent) || coordinator.HasChannelSnapshot(E_SaveChannelType.Session);
    }

    /// <summary>
    /// Load Game 버튼 활성화 여부를 반환합니다.
    /// </summary>
    public bool HasLoadableData()
    {
        return HasContinueData();
    }

    /// <summary>
    /// New Game 덮어쓰기 확인이 필요한 기존 진행 데이터 존재 여부를 반환합니다.
    /// </summary>
    public bool HasExistingProgress()
    {
        return HasContinueData();
    }

    /// <summary>
    /// 직렬화 참조 또는 싱글톤에서 SaveCoordinator를 해석합니다.
    /// </summary>
    private SaveCoordinator ResolveCoordinator()
    {
        if (_saveCoordinator != null)
        {
            return _saveCoordinator;
        }

        if (SaveCoordinator.Instance == null)
        {
            Debug.LogWarning("[TitleSaveQueryService] SaveCoordinator를 찾지 못해 세이브 조회를 false로 처리합니다.", this);
            return null;
        }

        return SaveCoordinator.Instance;
    }
}
