using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IOptionsPanelBindingView 구현체로서 Options 패널의 UI 바인딩 진입점을 제공합니다.
/// </summary>
public class OptionsPanelBindingView : MonoBehaviour, IOptionsPanelBindingView
{
    [Header("Dependencies")]
    [Tooltip("옵션 스냅샷 seed를 조회할 OptionManager입니다. 비어 있으면 OptionManager.Instance를 사용합니다.")]
    [SerializeField] private OptionManager _optionManager; // TryBuild 시 seed 스냅샷을 얻을 OptionManager 참조입니다.

    [Header("Row Bindings")]
    [Tooltip("옵션 패널에 배치된 각 Row의 UI 바인딩 설정 목록입니다.")]
    [SerializeField] private List<OptionRowBindingEntry> _rowEntries = new List<OptionRowBindingEntry>(); // Inspector에서 연결한 옵션 Row 바인딩 설정 목록입니다.

    [Header("Auto Collect")]
    [Tooltip("On일 때 OptionRowBindingAnchor를 자동 수집해 Row Bindings 대신 사용합니다.")]
    [SerializeField] private bool _useBindingAnchors = true; // Row 앵커 자동 수집 사용 여부입니다.

    [Tooltip("Row 앵커를 수집할 루트입니다. 비워두면 현재 오브젝트 Transform을 루트로 사용합니다.")]
    [SerializeField] private Transform _bindingAnchorsRoot; // 바인딩 앵커 검색 시작점 Transform 참조입니다.

    [Tooltip("자동 수집 시 비활성 오브젝트의 Row 앵커도 포함할지 여부입니다.")]
    [SerializeField] private bool _includeInactiveAnchors = true; // 자동 수집에서 비활성 Row를 포함할지 여부입니다.

    [Header("Validation")]
    [Tooltip("Awake 시 바인딩 오류를 발견하면 Warning 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _logValidationWarnings = true; // 런타임 바인딩 검증 실패 시 경고 로그 출력 여부입니다.

    [Header("Runtime Sync")]
    [Tooltip("OptionManager의 런타임 옵션이 로드되거나 변경될 때 현재 UI 값을 자동으로 동기화할지 여부입니다.")]
    [SerializeField] private bool _syncViewWhenRuntimeOptionsChanged = true; // 저장 데이터 로드처럼 View 밖에서 발생한 옵션 변경을 UI에 반영할지 결정하는 값입니다.

    private readonly List<IOptionRowBinder> _runtimeBinders = new List<IOptionRowBinder>(); // 런타임에 생성된 유효 Row Binder 목록입니다.
    private readonly List<OptionRowBindingEntry> _collectedEntries = new List<OptionRowBindingEntry>(); // 자동 수집 모드에서 사용한 Row 바인딩 임시 목록입니다.
    private OptionsPanelPresenter _presenter; // Row Binder 조합 흐름을 제어하는 Presenter 인스턴스입니다.
    private bool _isApplyingSnapshot; // ApplyOptionsToView 처리 중 재진입을 방지하기 위한 가드 플래그입니다.
    private bool _isSubscribedToOptionManager; // OptionManager 변경 이벤트 중복 구독을 막기 위한 상태값입니다.
    private OptionManager _subscribedOptionManager; // 현재 변경 이벤트를 구독 중인 OptionManager 참조입니다.

    /// <summary>
    /// 런타임 시작 시 바인딩 유효성 검사와 Presenter 초기화를 수행합니다.
    /// </summary>
    private void Awake()
    {
        ResolveOptionManager();
        RebuildRuntimeBinders();
        ApplyOptionsFromCurrentRuntime();
    }

    /// <summary>
    /// View가 활성화될 때 런타임 옵션 변경을 구독하고 현재 스냅샷을 UI에 반영합니다.
    /// </summary>
    private void OnEnable()
    {
        SubscribeOptionManagerChanges();
        ApplyOptionsFromCurrentRuntime();
    }

    /// <summary>
    /// 다른 런타임 싱글톤의 Awake 로드가 끝난 뒤 최신 옵션 스냅샷을 다시 UI에 반영합니다.
    /// </summary>
    private void Start()
    {
        ResolveOptionManager();
        SubscribeOptionManagerChanges();
        ApplyOptionsFromCurrentRuntime();
    }

    /// <summary>
    /// View가 비활성화될 때 런타임 옵션 변경 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeOptionManagerChanges();
    }

    /// <summary>
    /// View 파괴 시 남아 있는 OptionManager 변경 구독을 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        UnsubscribeOptionManagerChanges();
    }

    /// <summary>
    /// Inspector 변경 시 바인딩 필수 참조와 키 중복을 검증합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_rowEntries == null)
        {
            _rowEntries = new List<OptionRowBindingEntry>();
        }

        List<OptionRowBindingEntry> validationEntries = BuildValidationEntries(); // OnValidate 검증에 사용할 최종 Row 목록입니다.
        HashSet<E_OptionBindingKey> bindingKeys = new HashSet<E_OptionBindingKey>(); // Inspector 목록에서 중복 키 감지를 위한 집합입니다.

        for (int i = 0; i < validationEntries.Count; i++)
        {
            OptionRowBindingEntry entry = validationEntries[i]; // 현재 검증 중인 Row 바인딩 항목입니다.
            if (entry == null || entry.IsActive == false)
            {
                continue;
            }

            if (entry.BindingKey == E_OptionBindingKey.None)
            {
                Debug.LogWarning($"[OptionsPanelBindingView] BindingKey None 항목이 있습니다. index={i}", this);
                continue;
            }

            if (bindingKeys.Add(entry.BindingKey) == false)
            {
                Debug.LogWarning($"[OptionsPanelBindingView] BindingKey 중복이 감지되었습니다. key={entry.BindingKey}", this);
            }

            if (entry.WidgetType == E_OptionBindingWidgetType.Enum && entry.EnumValueText == null)
            {
                Debug.LogWarning($"[OptionsPanelBindingView] Enum Row 텍스트가 비어 있습니다. key={entry.BindingKey}", this);
            }

            if (entry.WidgetType == E_OptionBindingWidgetType.Enum && entry.EnumPrevButton == null && entry.EnumNextButton == null)
            {
                Debug.LogWarning($"[OptionsPanelBindingView] Enum Row 증감 버튼이 모두 비어 있습니다. key={entry.BindingKey}", this);
            }

            if (entry.WidgetType == E_OptionBindingWidgetType.Numeric && entry.NumericFillImage == null)
            {
                Debug.LogWarning($"[OptionsPanelBindingView] Numeric Row Fill 이미지가 비어 있습니다. key={entry.BindingKey}", this);
            }

            if (entry.WidgetType == E_OptionBindingWidgetType.Numeric && entry.NumericDecreaseButton == null && entry.NumericIncreaseButton == null && entry.NumericPointerInputArea == null && entry.NumericFillImage == null)
            {
                Debug.LogWarning($"[OptionsPanelBindingView] Numeric Row 입력 위젯(버튼/포인터 영역/Fill 이미지)이 모두 비어 있습니다. key={entry.BindingKey}", this);
            }

            if (entry.WidgetType == E_OptionBindingWidgetType.Action && entry.ActionWidget == null)
            {
                Debug.LogWarning($"[OptionsPanelBindingView] Action Row 위젯이 비어 있습니다. key={entry.BindingKey}", this);
            }

            if (entry.WidgetType == E_OptionBindingWidgetType.Numeric && entry.NumericMinValue > entry.NumericMaxValue)
            {
                Debug.LogWarning($"[OptionsPanelBindingView] Numeric 범위가 반대로 설정되었습니다. key={entry.BindingKey}", this);
            }
        }
    }

    /// <summary>
    /// 런타임 옵션 스냅샷을 UI 위젯 상태에 반영합니다.
    /// </summary>
    public void ApplyOptionsToView(OptionSaveData optionData)
    {
        if (_isApplyingSnapshot)
        {
            return;
        }

        if (_presenter == null)
        {
            RebuildRuntimeBinders();
        }

        OptionSaveData safeData = optionData ?? BuildSeedData(); // null 입력 방지를 위한 안전 스냅샷입니다.

        _isApplyingSnapshot = true;
        _presenter?.ApplyToView(safeData);
        _isApplyingSnapshot = false;
    }

    /// <summary>
    /// 현재 UI 입력값으로 OptionSaveData를 구성합니다.
    /// </summary>
    public bool TryBuildOptions(out OptionSaveData optionData)
    {
        optionData = BuildSeedData();

        if (_presenter == null)
        {
            RebuildRuntimeBinders();
        }

        if (_presenter == null)
        {
            Debug.LogWarning("[OptionsPanelBindingView] Presenter가 초기화되지 않아 TryBuildOptions를 실패 처리합니다.", this);
            return false;
        }

        bool succeeded = _presenter.TryBuildFromView(optionData, out OptionSaveData builtData, out string errorMessage); // 현재 위젯 입력값을 옵션 데이터로 구성한 결과입니다.
        if (succeeded == false)
        {
            Debug.LogWarning($"[OptionsPanelBindingView] TryBuildOptions 실패: {errorMessage}", this);
            return false;
        }

        optionData = builtData;
        return true;
    }

    /// <summary>
    /// 외부 Bridge가 해석한 OptionManager를 연결하고 현재 런타임 옵션을 UI에 즉시 반영합니다.
    /// </summary>
    public void BindOptionManager(OptionManager optionManager)
    {
        if (_optionManager == optionManager)
        {
            SubscribeOptionManagerChanges();
            ApplyOptionsFromCurrentRuntime();
            return;
        }

        UnsubscribeOptionManagerChanges();
        _optionManager = optionManager;
        SubscribeOptionManagerChanges();
        ApplyOptionsFromCurrentRuntime();
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
    /// OptionManager의 옵션 변경 이벤트를 중복 없이 구독합니다.
    /// </summary>
    private void SubscribeOptionManagerChanges()
    {
        if (_syncViewWhenRuntimeOptionsChanged == false || _isSubscribedToOptionManager)
        {
            return;
        }

        OptionManager optionManager = ResolveOptionManager(); // UI 자동 동기화 이벤트를 받을 런타임 옵션 매니저입니다.
        if (optionManager == null)
        {
            return;
        }

        optionManager.RemoveListener(HandleRuntimeOptionsChanged);
        optionManager.AddListener(HandleRuntimeOptionsChanged);
        _subscribedOptionManager = optionManager;
        _isSubscribedToOptionManager = true;
    }

    /// <summary>
    /// OptionManager의 옵션 변경 이벤트 구독을 해제합니다.
    /// </summary>
    private void UnsubscribeOptionManagerChanges()
    {
        if (_isSubscribedToOptionManager == false)
        {
            return;
        }

        if (_subscribedOptionManager != null)
        {
            _subscribedOptionManager.RemoveListener(HandleRuntimeOptionsChanged);
        }

        _subscribedOptionManager = null;
        _isSubscribedToOptionManager = false;
    }

    /// <summary>
    /// 런타임 옵션이 변경되면 최신 스냅샷을 UI 위젯에 반영합니다.
    /// </summary>
    private void HandleRuntimeOptionsChanged(OptionSaveData optionData)
    {
        if (_syncViewWhenRuntimeOptionsChanged == false || _isApplyingSnapshot)
        {
            return;
        }

        ApplyOptionsToView(optionData);
    }

    /// <summary>
    /// 연결된 OptionManager의 현재 스냅샷을 가져와 UI에 반영합니다.
    /// </summary>
    private void ApplyOptionsFromCurrentRuntime()
    {
        OptionManager optionManager = ResolveOptionManager(); // 이미 로드된 글로벌 옵션을 보유한 런타임 옵션 매니저입니다.
        if (optionManager == null)
        {
            return;
        }

        ApplyOptionsToView(optionManager.GetCurrentOptions());
    }

    /// <summary>
    /// RowEntry 목록을 런타임 Binder 목록으로 변환하고 Presenter를 재생성합니다.
    /// </summary>
    private void RebuildRuntimeBinders()
    {
        _runtimeBinders.Clear();
        List<OptionRowBindingEntry> buildEntries = BuildValidationEntries(); // 런타임 Binder 생성에 사용할 최종 Row 목록입니다.
        if (buildEntries == null)
        {
            _presenter = new OptionsPanelPresenter(_runtimeBinders);
            return;
        }

        for (int i = 0; i < buildEntries.Count; i++)
        {
            OptionRowBindingEntry entry = buildEntries[i]; // Binder 생성 대상 Row 설정 항목입니다.
            if (entry == null || entry.IsActive == false)
            {
                continue;
            }

            if (TryCreateBinder(entry, out IOptionRowBinder binder, out string errorMessage) == false)
            {
                if (_logValidationWarnings)
                {
                    Debug.LogWarning($"[OptionsPanelBindingView] Row Binder 생성 실패: {errorMessage}", this);
                }

                continue;
            }

            _runtimeBinders.Add(binder);
        }

        _presenter = new OptionsPanelPresenter(_runtimeBinders);
    }

    /// <summary>
    /// 검증/빌드에 사용할 Row 엔트리 목록을 구성합니다.
    /// </summary>
    private List<OptionRowBindingEntry> BuildValidationEntries()
    {
        if (_useBindingAnchors == false)
        {
            return _rowEntries;
        }

        _collectedEntries.Clear();

        Transform root = _bindingAnchorsRoot != null ? _bindingAnchorsRoot : transform; // 자동 수집 시작 루트 Transform입니다.
        OptionRowBindingAnchor[] anchors = root.GetComponentsInChildren<OptionRowBindingAnchor>(_includeInactiveAnchors); // 루트 하위에서 발견한 Row 바인딩 앵커 목록입니다.
        for (int i = 0; i < anchors.Length; i++)
        {
            OptionRowBindingAnchor anchor = anchors[i]; // 현재 수집 중인 Row 앵커 컴포넌트입니다.
            if (anchor == null || anchor.BindingEntry == null)
            {
                continue;
            }

            _collectedEntries.Add(anchor.BindingEntry);
        }

        return _collectedEntries;
    }

    /// <summary>
    /// RowEntry 1건으로부터 위젯 타입에 맞는 Binder를 생성합니다.
    /// </summary>
    private bool TryCreateBinder(OptionRowBindingEntry entry, out IOptionRowBinder binder, out string errorMessage)
    {
        binder = null;
        errorMessage = string.Empty;

        if (entry.BindingKey == E_OptionBindingKey.None)
        {
            errorMessage = "BindingKey가 None입니다.";
            return false;
        }

        switch (entry.WidgetType)
        {
            case E_OptionBindingWidgetType.Enum:
                if (entry.EnumValueText == null)
                {
                    errorMessage = $"Enum 텍스트 참조 누락. key={entry.BindingKey}";
                    return false;
                }

                binder = new EnumOptionRowBinder(entry);
                return true;

            case E_OptionBindingWidgetType.Numeric:
                if (entry.NumericFillImage == null)
                {
                    errorMessage = $"Numeric Fill 이미지 참조 누락. key={entry.BindingKey}";
                    return false;
                }

                binder = new NumericOptionRowBinder(entry);
                return true;

            case E_OptionBindingWidgetType.Action:
                if (entry.ActionWidget == null)
                {
                    errorMessage = $"Action 위젯 참조 누락. key={entry.BindingKey}";
                    return false;
                }

                binder = new ActionOptionRowBinder(entry);
                return true;

            default:
                errorMessage = $"알 수 없는 WidgetType입니다. type={entry.WidgetType}";
                return false;
        }
    }

    /// <summary>
    /// TryBuildOptions의 seed로 사용할 안전한 옵션 스냅샷을 생성합니다.
    /// </summary>
    private OptionSaveData BuildSeedData()
    {
        OptionManager optionManager = ResolveOptionManager(); // seed 스냅샷을 가져올 OptionManager 참조입니다.
        if (optionManager != null)
        {
            OptionSaveData currentOptions = optionManager.GetCurrentOptions(); // 런타임 최신 옵션 스냅샷입니다.
            if (currentOptions != null)
            {
                return currentOptions;
            }
        }

        return new OptionSaveData();
    }
}
