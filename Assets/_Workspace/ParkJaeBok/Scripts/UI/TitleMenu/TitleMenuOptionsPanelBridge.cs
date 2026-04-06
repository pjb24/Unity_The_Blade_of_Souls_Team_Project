using UnityEngine;

/// <summary>
/// TitleMenu Options 패널과 OptionManager 사이의 데이터 동기화를 담당하는 브리지 컴포넌트입니다.
/// </summary>
public class TitleMenuOptionsPanelBridge : MonoBehaviour, ITitleMenuOptionsPanelBridge
{
    [Header("Dependencies")]
    [Tooltip("옵션 런타임 상태를 제공하는 OptionManager입니다. 비워두면 OptionManager.Instance를 사용합니다.")]
    [SerializeField] private OptionManager _optionManager; // 옵션 로드/저장/조회에 사용할 OptionManager 참조입니다.

    [Tooltip("옵션 UI 값을 주고받을 View 컴포넌트입니다. IOptionsPanelBindingView 구현체를 연결해야 합니다.")]
    [SerializeField] private MonoBehaviour _optionsViewComponent; // 옵션 패널 바인딩 인터페이스를 구현한 View 컴포넌트 참조입니다.

    [Header("Open Behavior")]
    [Tooltip("옵션 패널 오픈 직전 Persistent 채널 로드를 재시도할지 여부입니다.")]
    [SerializeField] private bool _reloadOptionsOnOpen = true; // 옵션 패널 오픈 시 저장값 재로드를 수행할지 여부입니다.

    [Tooltip("옵션 패널 오픈 시 현재 런타임 스냅샷을 UI에 자동 반영할지 여부입니다.")]
    [SerializeField] private bool _applySnapshotToViewOnOpen = true; // 오픈 직후 UI에 옵션 스냅샷을 반영할지 여부입니다.

    [Header("Save Behavior")]
    [Tooltip("Apply 버튼 API 호출 시 OptionManager.SaveCurrentOptions를 함께 호출할지 여부입니다.")]
    [SerializeField] private bool _saveAfterApply = true; // UI Apply 시 저장까지 즉시 수행할지 여부입니다.

    private IOptionsPanelBindingView _optionsView; // 옵션 스냅샷과 UI 입력값을 주고받는 View 인터페이스 참조입니다.

    /// <summary>
    /// View 인터페이스 구현체를 해석하고 의존성 유효성을 검증합니다.
    /// </summary>
    private void Awake()
    {
        _optionsView = _optionsViewComponent as IOptionsPanelBindingView;

        if (_optionsViewComponent != null && _optionsView == null)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] optionsViewComponent가 IOptionsPanelBindingView를 구현하지 않았습니다.", this);
        }
    }

    /// <summary>
    /// 옵션 패널 활성화 전에 옵션 로드와 UI 바인딩 준비를 수행합니다.
    /// </summary>
    public void HandleBeforeOpen(TitleMenuActionContext context)
    {
        OptionManager optionManager = ResolveOptionManager(); // 옵션 동기화에 사용할 런타임 OptionManager 참조입니다.
        if (optionManager == null)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] OptionManager를 찾지 못해 오픈 전 동기화를 건너뜁니다.", this);
            return;
        }

        if (_reloadOptionsOnOpen)
        {
            optionManager.LoadCurrentOptions("TitleMenu.Options.Open");
        }

        if (_applySnapshotToViewOnOpen)
        {
            ApplyCurrentRuntimeOptionsToView();
        }
    }

    /// <summary>
    /// 옵션 패널 활성화 직후 후처리를 수행합니다.
    /// </summary>
    public void HandleAfterOpen(TitleMenuActionContext context)
    {
    }

    /// <summary>
    /// 현재 OptionManager 런타임 스냅샷을 UI에 반영합니다.
    /// </summary>
    public void ApplyCurrentRuntimeOptionsToView()
    {
        OptionManager optionManager = ResolveOptionManager(); // 현재 옵션 스냅샷 조회에 사용할 OptionManager 참조입니다.
        if (optionManager == null)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] OptionManager를 찾지 못해 UI 반영을 건너뜁니다.", this);
            return;
        }

        if (_optionsView == null)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] Options View가 연결되지 않아 UI 반영을 건너뜁니다.", this);
            return;
        }

        OptionSaveData snapshot = optionManager.GetCurrentOptions(); // 현재 런타임 옵션 스냅샷입니다.
        _optionsView.ApplyOptionsToView(snapshot);
    }


    /// <summary>
    /// Unity Button OnClick에서 호출하기 위한 Apply 래퍼 메서드입니다.
    /// </summary>
    public void ApplyViewOptionsToRuntimeAndSaveFromButton()
    {
        bool succeeded = ApplyViewOptionsToRuntimeAndSave(); // 버튼 클릭 시 내부 Apply 수행 결과입니다.
        if (succeeded == false)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] Button OnClick Apply 처리에 실패했습니다.", this);
        }
    }

    /// <summary>
    /// UI 입력값을 OptionManager 런타임 상태에 반영하고 필요 시 저장합니다.
    /// </summary>
    public bool ApplyViewOptionsToRuntimeAndSave()
    {
        OptionManager optionManager = ResolveOptionManager(); // UI 값을 반영할 대상 OptionManager 참조입니다.
        if (optionManager == null)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] OptionManager를 찾지 못해 Apply를 수행할 수 없습니다.", this);
            return false;
        }

        if (_optionsView == null)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] Options View가 연결되지 않아 Apply를 수행할 수 없습니다.", this);
            return false;
        }

        if (_optionsView.TryBuildOptions(out OptionSaveData viewOptions) == false)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] UI 입력값 구성에 실패해 Apply를 중단합니다.", this);
            return false;
        }

        optionManager.SetAllOptions(viewOptions);

        if (_saveAfterApply == false)
        {
            return true;
        }

        return optionManager.SaveCurrentOptions("TitleMenu.Options.Apply");
    }

    /// <summary>
    /// 직렬화 참조 또는 전역 Instance에서 OptionManager를 해석합니다.
    /// </summary>
    private OptionManager ResolveOptionManager()
    {
        if (_optionManager != null)
        {
            return _optionManager;
        }

        _optionManager = OptionManager.Instance;
        return _optionManager;
    }
}
