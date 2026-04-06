using UnityEngine;

/// <summary>
/// Load Game 요청 시 연결된 로드 UI 진입점을 활성화하고 다른 모달 패널 충돌을 정리하는 액션입니다.
/// </summary>
public class OpenLoadGameAction : MonoBehaviour, ITitleMenuAction
{
    [Tooltip("Load Game 선택 시 활성화할 로드 UI 루트 오브젝트입니다.")]
    [SerializeField] private GameObject _loadGameEntryPoint; // Load Game 진입 시 표시할 UI 루트 오브젝트 참조입니다.

    [Tooltip("Load Game 패널 표시 시 함께 활성화할 모달 백드롭 오브젝트입니다.")]
    [SerializeField] private GameObject _modalBackdrop; // Load Game 패널 표시 시 배경 입력 차단을 위해 활성화할 백드롭 참조입니다.

    [Tooltip("Load Game 패널을 열기 전에 닫을 다른 패널 루트 오브젝트입니다.")]
    [SerializeField] private GameObject _panelToCloseOnOpen; // 로드 패널 오픈 시 충돌 방지를 위해 비활성화할 다른 패널 루트 참조입니다.

    /// <summary>
    /// 현재 문맥에서 Load Game 실행 가능 여부를 반환합니다.
    /// </summary>
    public bool CanExecute(TitleMenuActionContext context)
    {
        return context != null;
    }

    /// <summary>
    /// 로드 UI 진입점을 활성화하고 다른 모달 패널을 정리합니다.
    /// </summary>
    public bool Execute(TitleMenuActionContext context)
    {
        if (CanExecute(context) == false)
        {
            return false;
        }

        if (_loadGameEntryPoint == null)
        {
            Debug.LogWarning("[OpenLoadGameAction] Load Game 진입점이 비어 있어 동작을 건너뜁니다.", this);
            return false;
        }

        if (_panelToCloseOnOpen != null && _panelToCloseOnOpen.activeSelf)
        {
            _panelToCloseOnOpen.SetActive(false);
        }

        _loadGameEntryPoint.SetActive(true);

        if (_modalBackdrop != null)
        {
            _modalBackdrop.SetActive(true);
        }

        return true;
    }
}
