using UnityEngine;

/// <summary>
/// 옵션/로드 패널의 Close 버튼에서 호출되어 패널 닫기와 포커스 복귀를 처리하는 핸들러입니다.
/// </summary>
public class TitleMenuPanelCloseHandler : MonoBehaviour
{
    [Tooltip("Close 버튼 클릭 시 비활성화할 대상 패널 루트 오브젝트입니다.")]
    [SerializeField] private GameObject _targetPanelRoot; // Close 클릭으로 비활성화할 대상 패널 루트 참조입니다.

    [Tooltip("패널이 모두 닫혔을 때 비활성화할 공통 모달 백드롭입니다. 사용하지 않으면 비워둘 수 있습니다.")]
    [SerializeField] private GameObject _modalBackdrop; // 패널 닫힘 이후 가림막 비활성화를 처리할 모달 백드롭 참조입니다.

    [Tooltip("백드롭을 숨길지 판단할 때 함께 확인할 다른 패널 루트 오브젝트입니다.")]
    [SerializeField] private GameObject _otherPanelRoot; // 다른 패널 활성 상태를 확인하기 위한 보조 패널 루트 참조입니다.

    [Tooltip("패널 닫힘 후 기본 메뉴 복귀를 수행할 TitlePlayModePresenter입니다.")]
    [SerializeField] private TitlePlayModePresenter _titlePlayModePresenter; // 패널 닫힘 직후 타이틀 기본 메뉴 복귀를 요청할 프레젠터 참조입니다.

    /// <summary>
    /// Close 버튼 클릭 시 대상 패널을 닫고 필요 시 모달 백드롭을 숨깁니다.
    /// </summary>
    public void ClosePanel()
    {
        if (_targetPanelRoot == null)
        {
            Debug.LogWarning("[TitleMenuPanelCloseHandler] targetPanelRoot가 비어 있어 ClosePanel을 건너뜁니다.", this);
            return;
        }

        _targetPanelRoot.SetActive(false);
        TryHideBackdrop();
        _titlePlayModePresenter?.OpenTopMenu();
    }

    /// <summary>
    /// 다른 패널이 열려 있지 않을 때만 모달 백드롭을 비활성화합니다.
    /// </summary>
    private void TryHideBackdrop()
    {
        if (_modalBackdrop == null)
        {
            return;
        }

        bool shouldKeepBackdrop = _otherPanelRoot != null && _otherPanelRoot.activeSelf; // 다른 패널이 열려 있어 백드롭 유지가 필요한지 여부입니다.
        if (shouldKeepBackdrop)
        {
            return;
        }

        _modalBackdrop.SetActive(false);
    }
}
