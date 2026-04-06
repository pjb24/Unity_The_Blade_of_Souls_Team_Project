using UnityEngine;

/// <summary>
/// Option 요청 시 연결된 옵션 UI 진입점을 활성화하고 OptionSystem 오픈 동기화를 수행하는 액션입니다.
/// </summary>
public class OpenOptionsAction : MonoBehaviour, ITitleMenuAction
{
    [Tooltip("Option 선택 시 활성화할 옵션 UI 루트 오브젝트입니다.")]
    [SerializeField] private GameObject _optionsEntryPoint; // 옵션 메뉴 진입 시 표시할 UI 루트 오브젝트 참조입니다.

    [Tooltip("Options 패널 표시 시 함께 활성화할 모달 백드롭 오브젝트입니다.")]
    [SerializeField] private GameObject _modalBackdrop; // Options 패널 표시 시 배경 입력 차단을 위해 활성화할 백드롭 참조입니다.

    [Tooltip("Options 패널을 열기 전에 닫을 다른 패널 루트 오브젝트입니다.")]
    [SerializeField] private GameObject _panelToCloseOnOpen; // 옵션 패널 오픈 시 충돌 방지를 위해 비활성화할 다른 패널 루트 참조입니다.

    [Tooltip("Options 패널 오픈 전/후 OptionSystem 동기화를 수행할 브리지 컴포넌트입니다.")]
    [SerializeField] private MonoBehaviour _optionsBridgeComponent; // 옵션 패널 오픈 시점 동기화용 브리지 컴포넌트 참조입니다.

    private ITitleMenuOptionsPanelBridge _optionsBridge; // 옵션 오픈 시점 동기화를 수행할 브리지 인터페이스 참조입니다.

    /// <summary>
    /// 브리지 인터페이스 구현체를 해석하고 유효성을 검증합니다.
    /// </summary>
    private void Awake()
    {
        _optionsBridge = _optionsBridgeComponent as ITitleMenuOptionsPanelBridge;
        if (_optionsBridgeComponent != null && _optionsBridge == null)
        {
            Debug.LogWarning("[OpenOptionsAction] optionsBridgeComponent가 ITitleMenuOptionsPanelBridge를 구현하지 않았습니다.", this);
        }
    }

    /// <summary>
    /// 현재 문맥에서 Option 실행 가능 여부를 반환합니다.
    /// </summary>
    public bool CanExecute(TitleMenuActionContext context)
    {
        return context != null;
    }

    /// <summary>
    /// 옵션 UI 진입점을 활성화하고 오픈 동기화 브리지를 호출합니다.
    /// </summary>
    public bool Execute(TitleMenuActionContext context)
    {
        if (CanExecute(context) == false)
        {
            return false;
        }

        if (_optionsEntryPoint == null)
        {
            Debug.LogWarning("[OpenOptionsAction] Options 진입점이 비어 있어 동작을 건너뜁니다.", this);
            return false;
        }

        _optionsBridge?.HandleBeforeOpen(context);

        if (_panelToCloseOnOpen != null && _panelToCloseOnOpen.activeSelf)
        {
            _panelToCloseOnOpen.SetActive(false);
        }

        _optionsEntryPoint.SetActive(true);

        if (_modalBackdrop != null)
        {
            _modalBackdrop.SetActive(true);
        }

        _optionsBridge?.HandleAfterOpen(context);
        return true;
    }
}
