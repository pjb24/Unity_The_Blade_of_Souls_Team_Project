using UnityEngine;
using UnityEngine.EventSystems;

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

    [Header("Default Behavior")]
    [Tooltip("DefaultAll 버튼에서 현재 탭 식별에 사용할 탭 컨트롤러입니다.")]
    [SerializeField] private OptionsTabSectionController _tabSectionController; // DefaultAll 처리 시 현재 선택 탭을 조회할 탭 컨트롤러 참조입니다.

    [Tooltip("Default 버튼에서 마지막 hover/선택 Row를 보조 식별할 설명 Presenter입니다.")]
    [SerializeField] private OptionsRowDescriptionPresenter _rowDescriptionPresenter; // Default 단일 적용 시 마지막 선택 Row 키를 조회할 설명 Presenter 참조입니다.

    private IOptionsPanelBindingView _optionsView; // 옵션 스냅샷과 UI 입력값을 주고받는 View 인터페이스 참조입니다.

    /// <summary>
    /// View 인터페이스 구현체를 해석하고 의존성 유효성을 검증합니다.
    /// </summary>
    private void Awake()
    {
        ResolveOptionsView();

        if (_optionsViewComponent != null && _optionsView == null)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] optionsViewComponent가 IOptionsPanelBindingView를 구현하지 않았습니다.", this);
        }
    }

    /// <summary>
    /// 옵션 패널 활성화 전에 옵션 로드와 UI 바인딩 준비를 수행합니다.
    /// </summary>
    public void HandleBeforeOpen()
    {
        OptionManager optionManager = ResolveOptionManager(); // 옵션 동기화에 사용할 런타임 OptionManager 참조입니다.
        if (optionManager == null)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] OptionManager를 찾지 못해 오픈 전 동기화를 건너뜁니다.", this);
            return;
        }

        ResolveOptionsView();
        if (_optionsView != null)
        {
            _optionsView.BindOptionManager(optionManager);
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
    public void HandleAfterOpen()
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

        ResolveOptionsView();
        if (_optionsView == null)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] Options View가 연결되지 않아 UI 반영을 건너뜁니다.", this);
            return;
        }

        _optionsView.BindOptionManager(optionManager);
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
    /// Unity Button OnClick에서 호출하기 위한 Default(현재 선택 옵션 1개 기본값 반영) 래퍼 메서드입니다.
    /// </summary>
    public void ApplyDefaultSelectedOptionToViewFromButton()
    {
        bool succeeded = ApplyDefaultSelectedOptionToView(); // 버튼 클릭 시 선택 옵션 기본값 반영 처리 결과입니다.
        if (succeeded == false)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] Button OnClick Default 처리에 실패했습니다.", this);
        }
    }

    /// <summary>
    /// Unity Button OnClick에서 호출하기 위한 DefaultAll(현재 탭 옵션 기본값 반영) 래퍼 메서드입니다.
    /// </summary>
    public void ApplyDefaultCurrentTabOptionsToViewFromButton()
    {
        bool succeeded = ApplyDefaultCurrentTabOptionsToView(); // 버튼 클릭 시 현재 탭 기본값 반영 처리 결과입니다.
        if (succeeded == false)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] Button OnClick DefaultAll 처리에 실패했습니다.", this);
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

        ResolveOptionsView();
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
    /// 현재 선택된 옵션 1개를 기본 프로필 값으로 되돌려 UI에 반영합니다.
    /// </summary>
    public bool ApplyDefaultSelectedOptionToView()
    {
        if (TryResolveBridgeContext(out OptionManager optionManager) == false)
        {
            return false;
        }

        if (TryResolveSelectedBindingKey(out E_OptionBindingKey selectedKey) == false)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] 현재 선택된 옵션 Row를 찾지 못해 Default를 적용할 수 없습니다.", this);
            return false;
        }

        if (TryResolveWorkingSnapshot(optionManager, out OptionSaveData workingOptions) == false)
        {
            return false;
        }

        if (optionManager.TryGetDefaultOptions(out OptionSaveData defaultOptions) == false)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] 기본 옵션 스냅샷 생성에 실패해 Default를 적용할 수 없습니다.", this);
            return false;
        }

        if (TryCopyBindingValueFromSource(ref workingOptions, defaultOptions, selectedKey) == false)
        {
            Debug.LogWarning($"[TitleMenuOptionsPanelBridge] 선택 옵션 기본값 반영에 실패했습니다. key={selectedKey}", this);
            return false;
        }

        _optionsView.ApplyOptionsToView(workingOptions);
        return true;
    }

    /// <summary>
    /// 현재 탭에 속한 옵션들을 기본 프로필 값으로 되돌려 UI에 반영합니다.
    /// </summary>
    public bool ApplyDefaultCurrentTabOptionsToView()
    {
        if (TryResolveBridgeContext(out OptionManager optionManager) == false)
        {
            return false;
        }

        if (TryResolveWorkingSnapshot(optionManager, out OptionSaveData workingOptions) == false)
        {
            return false;
        }

        if (optionManager.TryGetDefaultOptions(out OptionSaveData defaultOptions) == false)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] 기본 옵션 스냅샷 생성에 실패해 DefaultAll을 적용할 수 없습니다.", this);
            return false;
        }

        OptionsTabSectionController.E_OptionsTab currentTab = _tabSectionController != null
            ? _tabSectionController.GetCurrentTab()
            : OptionsTabSectionController.E_OptionsTab.Display; // DefaultAll 반영 대상 현재 탭 값입니다.

        E_OptionBindingKey[] tabKeys = ResolveTabBindingKeys(currentTab); // 현재 탭에 속한 바인딩 키 목록입니다.
        for (int i = 0; i < tabKeys.Length; i++)
        {
            E_OptionBindingKey key = tabKeys[i]; // 기본값으로 되돌릴 현재 바인딩 키입니다.
            TryCopyBindingValueFromSource(ref workingOptions, defaultOptions, key);
        }

        _optionsView.ApplyOptionsToView(workingOptions);
        return true;
    }

    /// <summary>
    /// 기본 동작에 필요한 OptionManager/View 참조를 검증합니다.
    /// </summary>
    private bool TryResolveBridgeContext(out OptionManager optionManager)
    {
        optionManager = ResolveOptionManager();
        if (optionManager == null)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] OptionManager를 찾지 못했습니다.", this);
            return false;
        }

        ResolveOptionsView();
        if (_optionsView == null)
        {
            Debug.LogWarning("[TitleMenuOptionsPanelBridge] Options View가 연결되지 않았습니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 현재 UI 편집 스냅샷을 가져오고 실패 시 런타임 스냅샷으로 폴백합니다.
    /// </summary>
    private bool TryResolveWorkingSnapshot(OptionManager optionManager, out OptionSaveData optionData)
    {
        if (_optionsView.TryBuildOptions(out optionData))
        {
            return true;
        }

        optionData = optionManager.GetCurrentOptions();
        return true;
    }

    /// <summary>
    /// 설명 Presenter가 기록한 마지막 hover Row의 BindingKey를 Default 대상 키로 해석합니다.
    /// </summary>
    private bool TryResolveSelectedBindingKey(out E_OptionBindingKey bindingKey)
    {
        bindingKey = E_OptionBindingKey.None;
        if (_rowDescriptionPresenter != null && _rowDescriptionPresenter.TryGetLastFocusedBindingKey(out E_OptionBindingKey lastFocusedKey))
        {
            bindingKey = lastFocusedKey;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 원본 스냅샷의 특정 BindingKey 값을 대상 스냅샷으로 복사합니다.
    /// </summary>
    private bool TryCopyBindingValueFromSource(ref OptionSaveData target, in OptionSaveData source, E_OptionBindingKey key)
    {
        if (OptionBindingDataAccessor.TryGetFloat(source, key, out float floatValue))
        {
            return OptionBindingDataAccessor.TrySetFloat(ref target, key, floatValue);
        }

        if (OptionBindingDataAccessor.TryGetInt(source, key, out int intValue))
        {
            return OptionBindingDataAccessor.TrySetInt(ref target, key, intValue);
        }

        return false;
    }

    /// <summary>
    /// 지정 탭에 속한 BindingKey 목록을 반환합니다.
    /// </summary>
    private E_OptionBindingKey[] ResolveTabBindingKeys(OptionsTabSectionController.E_OptionsTab tab)
    {
        switch (tab)
        {
            case OptionsTabSectionController.E_OptionsTab.Display:
                return new[]
                {
                    E_OptionBindingKey.DisplayScreenMode,
                    E_OptionBindingKey.DisplayVSync,
                    E_OptionBindingKey.DisplayFrameLimit,
                    E_OptionBindingKey.DisplayGammaBrightness,
                    E_OptionBindingKey.DisplayGraphicsPreset,
                    E_OptionBindingKey.DisplayGraphicsDetailMode
                };
            case OptionsTabSectionController.E_OptionsTab.Audio:
                return new[]
                {
                    E_OptionBindingKey.AudioMasterVolume,
                    E_OptionBindingKey.AudioBgmVolume,
                    E_OptionBindingKey.AudioSfxVolume
                };
            case OptionsTabSectionController.E_OptionsTab.Input:
                return new[]
                {
                    E_OptionBindingKey.InputHoldBehavior
                };
            case OptionsTabSectionController.E_OptionsTab.Accessibility:
                return new[]
                {
                    E_OptionBindingKey.AccessibilitySubtitleEnabled,
                    E_OptionBindingKey.AccessibilitySubtitleSize,
                    E_OptionBindingKey.AccessibilitySubtitleSpeed,
                    E_OptionBindingKey.AccessibilityFlashReduction,
                    E_OptionBindingKey.AccessibilityColorBlindMode,
                    E_OptionBindingKey.AccessibilityUiScale,
                    E_OptionBindingKey.AccessibilityCameraShake,
                    E_OptionBindingKey.AccessibilityScreenShake,
                    E_OptionBindingKey.AccessibilityShakeReduction,
                    E_OptionBindingKey.AccessibilityHighContrastMode
                };
            case OptionsTabSectionController.E_OptionsTab.Gameplay:
                return new[]
                {
                    E_OptionBindingKey.GameplayDifficulty,
                    E_OptionBindingKey.GameplayAutoSaveNotification
                };
            default:
                return new E_OptionBindingKey[0];
        }
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

    /// <summary>
    /// 직렬화 참조가 없으면 현재 오브젝트/자식에서 IOptionsPanelBindingView를 자동 탐색합니다.
    /// </summary>
    private void ResolveOptionsView()
    {
        _optionsView = _optionsViewComponent as IOptionsPanelBindingView;
        if (_optionsView != null)
        {
            return;
        }

        MonoBehaviour[] candidates = GetComponentsInChildren<MonoBehaviour>(true); // 현재 오브젝트와 비활성 자식에서 탐색한 컴포넌트 후보 목록입니다.
        for (int i = 0; i < candidates.Length; i++)
        {
            MonoBehaviour candidate = candidates[i]; // 인터페이스 구현 여부를 확인할 현재 컴포넌트 후보입니다.
            if (candidate is IOptionsPanelBindingView view)
            {
                _optionsViewComponent = candidate;
                _optionsView = view;
                return;
            }
        }
    }
}
