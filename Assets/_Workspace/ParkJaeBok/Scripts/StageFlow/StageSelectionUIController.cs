using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 스테이지 선택 UI의 열림/닫힘, 입력 차단, 멀티플레이 동기화, 버튼 상태를 관리합니다.
/// </summary>
[DisallowMultipleComponent]
public class StageSelectionUIController : NetworkBehaviour
{
    [Serializable]
    private sealed class StageButtonBinding
    {
        [Header("Stage")]
        [Tooltip("버튼이 진입 요청할 스테이지 식별자입니다.")]
        [SerializeField] private string _stageId; // 버튼이 대표하는 스테이지 ID입니다.

        [Tooltip("현재 스테이지 진입 가능 여부입니다. 런타임 갱신 메서드로 변경할 수 있습니다.")]
        [SerializeField] private bool _isEnterable = true; // 버튼의 잠금/해제 상태를 나타냅니다.

        [Tooltip("최근 StageDefinition Unlock 조건 판정 사유입니다. 런타임 확인용입니다.")]
        [SerializeField] private string _lastAvailabilityReason; // 최근 자동 진입 가능 판정 결과를 Inspector에서 확인하기 위한 문자열입니다.

        [Header("UI References")]
        [Tooltip("클릭 입력을 받을 Button 컴포넌트입니다.")]
        [SerializeField] private Button _button; // 스테이지 선택 클릭을 받는 버튼입니다.

        [Tooltip("버튼 배경 또는 잠금 표현에 사용할 Image 컴포넌트입니다.")]
        [SerializeField] private Image _buttonImage; // 버튼 자체의 기본 색상을 적용할 이미지입니다.

        [Tooltip("버튼 내부 아이콘, 텍스트 배경 등 잠금 색상을 같이 적용할 자식 Image 목록입니다.")]
        [SerializeField] private List<Image> _childImages = new List<Image>(); // 버튼 하위 이미지의 색상을 일괄 갱신하기 위한 목록입니다.

        [Header("Colors")]
        [Tooltip("진입 가능한 상태에서 버튼 Image에 적용할 기본 색상입니다.")]
        [SerializeField] private Color _enterableButtonColor = new Color(1f, 1f, 1f, 0f); // 해제 상태 버튼 이미지 기본 색상입니다.

        [Tooltip("진입 불가능한 상태에서 버튼 Image에 적용할 기본 색상입니다.")]
        [SerializeField] private Color _lockedButtonColor = new Color(0.62f, 0f, 0f, 0f); // 잠금 상태 버튼 이미지 기본 색상입니다.

        [Tooltip("진입 가능한 상태에서 자식 Image에 적용할 기본 색상입니다.")]
        [SerializeField] private Color _enterableChildColor = new Color(1f, 1f, 1f, 1f); // 해제 상태 자식 이미지 기본 색상입니다.

        [Tooltip("진입 불가능한 상태에서 자식 Image에 적용할 기본 색상입니다.")]
        [SerializeField] private Color _lockedChildColor = new Color(0.46f, 0.46f, 0.46f, 1f); // 잠금 상태 자식 이미지 기본 색상입니다.

        [Header("Entry")]
        [Tooltip("기존 스테이지 진입 기능을 호출할 어댑터입니다.")]
        [SerializeField] private StageSelectionStageEntryAdapter _entryAdapter; // 기존 StageFlow 진입 기능을 호출하는 연결 지점입니다.

        private StageSelectionUIController _owner; // 클릭 콜백을 전달할 소유 컨트롤러입니다.

        /// <summary>
        /// Inspector에 설정된 스테이지 ID를 반환합니다.
        /// </summary>
        public string StageId => _stageId;

        /// <summary>
        /// 현재 버튼이 진입 가능한지 반환합니다.
        /// </summary>
        public bool IsEnterable => _isEnterable;

        /// <summary>
        /// 최근 진입 가능 판정 사유를 반환합니다.
        /// </summary>
        public string LastAvailabilityReason => _lastAvailabilityReason;

        /// <summary>
        /// StageDefinition과 진행도 기반 Unlock 조건을 평가해 버튼 진입 가능 상태를 갱신합니다.
        /// </summary>
        public void RefreshAvailabilityFromDefinition(StageCatalog stageCatalog, StageProgressRuntime progressRuntime, UnityEngine.Object warningContext)
        {
            if (stageCatalog == null)
            {
                _lastAvailabilityReason = "StageCatalog is missing. Inspector value kept.";
                Debug.LogWarning($"[StageSelectionUIController] StageCatalog is missing. Inspector enterable value is kept. stageId={_stageId}", warningContext);
                return;
            }

            if (string.IsNullOrWhiteSpace(_stageId))
            {
                _isEnterable = false;
                _lastAvailabilityReason = "Stage id is empty.";
                Debug.LogWarning("[StageSelectionUIController] Stage id is empty. Button is locked.", warningContext);
                return;
            }

            if (!stageCatalog.TryGetById(_stageId, out StageDefinition stageDefinition) || stageDefinition == null)
            {
                _isEnterable = false;
                _lastAvailabilityReason = $"StageDefinition not found: {_stageId}";
                Debug.LogWarning($"[StageSelectionUIController] StageDefinition not found. Button is locked. stageId={_stageId}", warningContext);
                return;
            }

            StageAvailabilityResult availability = StageAvailabilityService.Evaluate(stageDefinition, progressRuntime); // StageDefinition SO의 Unlock 조건을 기존 서비스로 판정한 결과입니다.
            _isEnterable = availability.IsEnterable;
            _lastAvailabilityReason = string.IsNullOrWhiteSpace(availability.Reason) ? "Enterable" : availability.Reason;
        }

        /// <summary>
        /// 버튼 클릭 리스너를 등록합니다.
        /// </summary>
        public void Bind(StageSelectionUIController owner)
        {
            if (_button == null)
            {
                Debug.LogWarning($"[StageSelectionUIController] Button is missing for stageId={_stageId}.", owner);
                return;
            }

            _owner = owner;
            _button.onClick.RemoveListener(HandleClicked);
            _button.onClick.AddListener(HandleClicked);
            BindPointerRelay(owner);
        }

        /// <summary>
        /// 버튼 클릭 리스너를 해제합니다.
        /// </summary>
        public void Unbind()
        {
            if (_button == null)
            {
                return;
            }

            _button.onClick.RemoveListener(HandleClicked);
            UnbindPointerRelay();
            _owner = null;
        }

        /// <summary>
        /// Button.onClick에서 호출되어 소유 컨트롤러에 클릭 처리를 위임합니다.
        /// </summary>
        private void HandleClicked()
        {
            if (_owner == null)
            {
                Debug.LogWarning($"[StageSelectionUIController] Button owner is missing for stageId={_stageId}.");
                return;
            }

            _owner.HandleStageButtonClicked(this);
        }

        /// <summary>
        /// Button 오브젝트에 Hover 입력 릴레이를 연결합니다.
        /// </summary>
        private void BindPointerRelay(StageSelectionUIController owner)
        {
            if (_button == null)
            {
                return;
            }

            StageSelectionButtonPointerRelay relay = _button.GetComponent<StageSelectionButtonPointerRelay>();
            if (relay == null)
            {
                relay = _button.gameObject.AddComponent<StageSelectionButtonPointerRelay>();
            }

            relay.Bind(owner, _stageId);
        }

        /// <summary>
        /// Button 오브젝트의 Hover 입력 릴레이 연결을 해제합니다.
        /// </summary>
        private void UnbindPointerRelay()
        {
            if (_button == null)
            {
                return;
            }

            StageSelectionButtonPointerRelay relay = _button.GetComponent<StageSelectionButtonPointerRelay>();
            if (relay != null)
            {
                relay.Unbind(_owner);
            }
        }

        /// <summary>
        /// 진입 가능 여부를 변경하고 UI 상태를 갱신합니다.
        /// </summary>
        public void SetEnterable(bool isEnterable, UnityEngine.Object warningContext)
        {
            _isEnterable = isEnterable;
            RefreshVisualState(warningContext);
        }

        /// <summary>
        /// Host에서 동기화된 진입 가능 상태를 버튼에 적용합니다.
        /// </summary>
        public void ApplySyncedAvailability(bool isEnterable, string reason, UnityEngine.Object warningContext)
        {
            _isEnterable = isEnterable;
            _lastAvailabilityReason = string.IsNullOrWhiteSpace(reason) ? "Synced from host" : reason;
            RefreshVisualState(warningContext);
        }

        /// <summary>
        /// 원격 Hover 상태를 Button의 Transition/Animator에 적용합니다.
        /// </summary>
        public void ApplyHoverVisual(bool isHovered, UnityEngine.Object warningContext)
        {
            if (_button == null)
            {
                Debug.LogWarning($"[StageSelectionUIController] Cannot apply hover visual because Button is missing. stageId={_stageId}", warningContext);
                return;
            }

            if (EventSystem.current == null)
            {
                Debug.LogWarning($"[StageSelectionUIController] EventSystem is missing. Hover visual sync skipped. stageId={_stageId}", warningContext);
                return;
            }

            PointerEventData eventData = new PointerEventData(EventSystem.current); // Button Transition/Animator에 전달할 가상 포인터 이벤트입니다.
            if (isHovered)
            {
                ExecuteEvents.Execute(_button.gameObject, eventData, ExecuteEvents.pointerEnterHandler);
                return;
            }

            ExecuteEvents.Execute(_button.gameObject, eventData, ExecuteEvents.pointerExitHandler);
        }

        /// <summary>
        /// 현재 진입 가능 여부에 맞춰 Button hover 입력과 Image 기본 색상을 갱신합니다.
        /// </summary>
        public void RefreshVisualState(UnityEngine.Object warningContext)
        {
            if (_button == null)
            {
                Debug.LogWarning($"[StageSelectionUIController] Button is missing for stageId={_stageId}.", warningContext);
            }
            else
            {
                // 잠긴 버튼도 Hover Animator를 재생해야 하므로 클릭 차단은 컨트롤러의 선택 검증에서 처리합니다.
                _button.interactable = true;
            }

            if (_buttonImage == null)
            {
                Debug.LogWarning($"[StageSelectionUIController] Button Image is missing for stageId={_stageId}.", warningContext);
            }
            else
            {
                _buttonImage.color = _isEnterable ? _enterableButtonColor : _lockedButtonColor;
            }

            Color childColor = _isEnterable ? _enterableChildColor : _lockedChildColor; // 자식 Image에 적용할 현재 상태 색상입니다.
            for (int i = 0; i < _childImages.Count; i++)
            {
                Image childImage = _childImages[i]; // 현재 갱신할 자식 Image 참조입니다.
                if (childImage == null)
                {
                    Debug.LogWarning($"[StageSelectionUIController] Child Image is missing. stageId={_stageId}, index={i}", warningContext);
                    continue;
                }

                childImage.color = childColor;
            }
        }

        /// <summary>
        /// 연결된 기존 스테이지 진입 기능을 호출합니다.
        /// </summary>
        public bool RequestEntry(UnityEngine.Object warningContext)
        {
            if (_entryAdapter == null)
            {
                Debug.LogWarning($"[StageSelectionUIController] Stage entry adapter is missing for stageId={_stageId}.", warningContext);
                return false;
            }

            return _entryAdapter.RequestEnterStage(_stageId);
        }

        /// <summary>
        /// Inspector 편집 시 버튼 하위 이미지와 어댑터 참조를 가능한 범위에서 자동 보정합니다.
        /// </summary>
        public void OnValidate(Component owner)
        {
            if (_button == null && owner != null)
            {
                _button = owner.GetComponentInChildren<Button>(true);
            }

            if (_buttonImage == null && _button != null)
            {
                _buttonImage = _button.GetComponent<Image>();
            }

            if (_entryAdapter == null && owner != null)
            {
                _entryAdapter = owner.GetComponentInParent<StageSelectionStageEntryAdapter>();
            }
        }
    }

    [Header("UI")]
    [Tooltip("스테이지 선택 UI 전체 루트 GameObject입니다.")]
    [SerializeField] private GameObject _uiRoot; // 열림/닫힘 상태를 적용할 UI 루트입니다.

    [Tooltip("Awake 시 UI 루트를 비활성화할지 여부입니다.")]
    [SerializeField] private bool _hideOnAwake = true; // 씬 진입 시 UI가 닫힌 상태로 시작하도록 제어합니다.

    [Header("Input")]
    [Tooltip("UI가 열려 있는 동안 InputManager gameplay 입력 게이트를 차단할지 여부입니다.")]
    [SerializeField] private bool _blockGameplayInputWhileOpen = true; // 이동, 공격, 점프 등 gameplay 입력 차단 여부입니다.

    [Header("Stage Availability")]
    [Tooltip("버튼 진입 가능 여부를 StageDefinition SO의 Unlock 조건과 진행도 기준으로 자동 판정할지 여부입니다.")]
    [SerializeField] private bool _useStageDefinitionAvailability = true; // StageDefinition Unlock 조건 기반 버튼 잠금 판정 사용 여부입니다.

    [Tooltip("StageDefinition을 조회할 StageCatalog입니다.")]
    [SerializeField] private StageCatalog _stageCatalog; // 버튼 StageId로 StageDefinition SO를 찾기 위한 카탈로그입니다.

    [Tooltip("Bootstrap에서 DDOL로 유지되는 진행도 런타임 참조입니다. 비어 있으면 기존 인스턴스만 탐색하며 새로 생성하지 않습니다.")]
    [SerializeField] private StageProgressRuntime _stageProgressRuntime; // Unlock 조건 판정에 사용할 Bootstrap DDOL 진행도 런타임입니다.

    [Header("Stage Buttons")]
    [Tooltip("Inspector에서 관리할 스테이지 버튼 목록입니다.")]
    [SerializeField] private List<StageButtonBinding> _stageButtons = new List<StageButtonBinding>(); // 스테이지 버튼 상태와 진입 연결 정보 목록입니다.

    [Header("Debug")]
    [Tooltip("중복 열기/닫기 요청도 Warning 로그로 남길지 여부입니다.")]
    [SerializeField] private bool _warnDuplicateStateRequests = true; // 중복 상태 요청 진단 로그 출력 여부입니다.

    private static readonly List<StageSelectionUIController> ActiveControllers = new List<StageSelectionUIController>(); // Pause 입력 우선순위 판정을 위한 활성 컨트롤러 목록입니다.
    private bool _isOpen; // 현재 UI 열림 상태입니다.
    private bool _isBlockingGameplayInput; // 현재 이 컨트롤러가 gameplay 입력을 차단 중인지 추적합니다.
    private bool _isApplyingHostAvailabilitySnapshot; // Host에서 받은 버튼 상태를 적용 중인지 추적합니다.
    private bool _isApplyingRemoteHoverVisual; // RPC로 받은 Hover 시각 상태를 적용 중인지 추적합니다.

    /// <summary>
    /// 현재 UI가 열려 있는지 반환합니다.
    /// </summary>
    public bool IsOpen => _isOpen;

    /// <summary>
    /// 씬 시작 시 UI 기본 상태를 닫힘으로 초기화합니다.
    /// </summary>
    private void Awake()
    {
        if (_uiRoot == null)
        {
            _uiRoot = gameObject;
        }

        if (_hideOnAwake)
        {
            ApplyOpenState(false, "Awake");
        }

        RefreshAllButtonStates();
    }

    /// <summary>
    /// 버튼 리스너를 등록하고 활성 컨트롤러 목록에 추가합니다.
    /// </summary>
    private void OnEnable()
    {
        if (!ActiveControllers.Contains(this))
        {
            ActiveControllers.Add(this);
        }

        BindButtons(true);
    }

    /// <summary>
    /// 버튼 리스너와 입력 차단 상태를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        ActiveControllers.Remove(this);
        BindButtons(false);
        ReleaseGameplayInputBlock();
    }

    /// <summary>
    /// Pause 입력이 열린 스테이지 선택 UI를 우선 닫도록 시도합니다.
    /// </summary>
    public static bool TryCloseAnyOpenFromPauseInput()
    {
        for (int i = ActiveControllers.Count - 1; i >= 0; i--)
        {
            StageSelectionUIController controller = ActiveControllers[i]; // 현재 검사할 스테이지 선택 UI 컨트롤러입니다.
            if (controller == null || !controller.IsOpen)
            {
                continue;
            }

            controller.RequestCloseFromPauseInput();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 상호작용 트리거에서 UI 열기를 요청합니다.
    /// </summary>
    public void RequestOpenFromInteraction(GameObject requester)
    {
        if (!CanLocalPeerControlStageSelection("open"))
        {
            TryReportUnauthorizedOpenToServer();
            return;
        }

        RequestSetOpenState(true, "Interaction", requester);
    }

    /// <summary>
    /// Pause 입력에서 UI 닫기를 요청합니다.
    /// </summary>
    public void RequestCloseFromPauseInput()
    {
        if (!CanLocalPeerControlStageSelection("close"))
        {
            TryReportUnauthorizedCloseToServer();
            return;
        }

        RequestSetOpenState(false, "PauseInput", null);
    }

    /// <summary>
    /// 런타임 중 특정 스테이지 버튼의 진입 가능 여부를 갱신합니다.
    /// </summary>
    public void SetStageEnterable(string stageId, bool isEnterable)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogWarning("[StageSelectionUIController] Cannot update an empty stage id.", this);
            return;
        }

        bool found = false; // 대상 스테이지 버튼을 찾았는지 기록합니다.
        for (int i = 0; i < _stageButtons.Count; i++)
        {
            StageButtonBinding binding = _stageButtons[i]; // 현재 검사할 버튼 바인딩입니다.
            if (binding == null || binding.StageId != stageId)
            {
                continue;
            }

            binding.SetEnterable(isEnterable, this);
            found = true;
        }

        if (!found)
        {
            Debug.LogWarning($"[StageSelectionUIController] Stage button not found. stageId={stageId}", this);
        }
    }

    /// <summary>
    /// Host 포인터 릴레이에서 전달된 Hover 상태를 모든 화면에 동기화합니다.
    /// </summary>
    public void NotifyButtonHoverChangedFromPointer(string stageId, bool isHovered)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogWarning("[StageSelectionUIController] Hover sync ignored because stageId is empty.", this);
            return;
        }

        if (!CanLocalPeerBroadcastHover())
        {
            return;
        }

        if (IsNetworkSessionActive())
        {
            if (!IsSpawned)
            {
                Debug.LogWarning($"[StageSelectionUIController] Network session is active but controller is not spawned. Hover sync skipped. stageId={stageId}", this);
                return;
            }

            ApplyHoverStateRpc(stageId, isHovered);
            return;
        }

        ApplyHoverState(stageId, isHovered);
    }

    /// <summary>
    /// 모든 스테이지 버튼의 표시 상태를 현재 Inspector 값 기준으로 다시 적용합니다.
    /// </summary>
    public void RefreshAllButtonStates()
    {
        RefreshStageAvailabilityFromDefinitions();

        for (int i = 0; i < _stageButtons.Count; i++)
        {
            StageButtonBinding binding = _stageButtons[i]; // 현재 갱신할 버튼 바인딩입니다.
            if (binding == null)
            {
                Debug.LogWarning($"[StageSelectionUIController] Stage button binding is null. index={i}", this);
                continue;
            }

            binding.RefreshVisualState(this);
        }

        BroadcastAvailabilitySnapshotIfAuthoritative();
    }

    /// <summary>
    /// StageDefinition SO의 Unlock 조건과 진행도 상태를 기준으로 모든 버튼의 진입 가능 여부를 갱신합니다.
    /// </summary>
    public void RefreshStageAvailabilityFromDefinitions()
    {
        if (!_useStageDefinitionAvailability)
        {
            return;
        }

        if (IsNetworkSessionActive()
            && NetworkManager.Singleton != null
            && !NetworkManager.Singleton.IsServer
            && !_isApplyingHostAvailabilitySnapshot)
        {
            return;
        }

        ResolveAvailabilityDependencies();

        for (int i = 0; i < _stageButtons.Count; i++)
        {
            StageButtonBinding binding = _stageButtons[i]; // Unlock 조건을 평가할 버튼 바인딩입니다.
            if (binding == null)
            {
                Debug.LogWarning($"[StageSelectionUIController] Stage button binding is null during availability refresh. index={i}", this);
                continue;
            }

            binding.RefreshAvailabilityFromDefinition(_stageCatalog, _stageProgressRuntime, this);
        }
    }

    /// <summary>
    /// Host가 판정한 스테이지 버튼 진입 가능 상태를 모든 Client와 Host에 동기화합니다.
    /// </summary>
    private void BroadcastAvailabilitySnapshotIfAuthoritative()
    {
        if (_isApplyingHostAvailabilitySnapshot)
        {
            return;
        }

        if (!IsNetworkSessionActive() || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (!IsSpawned)
        {
            Debug.LogWarning("[StageSelectionUIController] Cannot sync stage availability because controller is not spawned.", this);
            return;
        }

        for (int i = 0; i < _stageButtons.Count; i++)
        {
            StageButtonBinding binding = _stageButtons[i]; // Host 판정 결과를 동기화할 버튼 바인딩입니다.
            if (binding == null)
            {
                continue;
            }

            ApplyStageAvailabilityRpc(binding.StageId, binding.IsEnterable, binding.LastAvailabilityReason);
        }
    }

    /// <summary>
    /// 버튼 클릭 요청을 권한과 잠금 상태 기준으로 검증한 뒤 기존 스테이지 진입 기능으로 전달합니다.
    /// </summary>
    private void HandleStageButtonClicked(StageButtonBinding binding)
    {
        if (binding == null)
        {
            Debug.LogWarning("[StageSelectionUIController] Null stage button click ignored.", this);
            return;
        }

        if (!binding.IsEnterable)
        {
            Debug.LogWarning($"[StageSelectionUIController] Locked stage button click ignored. stageId={binding.StageId}", this);
            return;
        }

        if (!CanLocalPeerControlStageSelection("select"))
        {
            TryReportUnauthorizedSelectToServer(binding.StageId);
            return;
        }

        binding.RequestEntry(this);
    }

    /// <summary>
    /// 현재 네트워크/싱글플레이 상태에 맞춰 열림 상태 변경을 적용하거나 RPC로 동기화합니다.
    /// </summary>
    private void RequestSetOpenState(bool isOpen, string reason, GameObject requester)
    {
        if (_isOpen == isOpen)
        {
            if (_warnDuplicateStateRequests)
            {
                Debug.LogWarning($"[StageSelectionUIController] Duplicate UI state request ignored. isOpen={isOpen}, reason={reason}", this);
            }

            return;
        }

        if (IsNetworkSessionActive())
        {
            if (!IsSpawned)
            {
                Debug.LogWarning("[StageSelectionUIController] Network session is active but controller is not spawned. Applying local fallback only.", this);
                ApplyOpenState(isOpen, $"{reason}.UnspawnedFallback");
                return;
            }

            ApplyOpenStateRpc(isOpen, reason);
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[StageSelectionUIController] NetworkManager is missing. Single-player local UI state path is used.", this);
        }

        ApplyOpenState(isOpen, reason);
    }

    /// <summary>
    /// 모든 클라이언트와 Host에 승인된 UI 열림 상태를 적용합니다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void ApplyOpenStateRpc(bool isOpen, string reason)
    {
        ApplyOpenState(isOpen, reason);
    }

    /// <summary>
    /// Host가 계산한 스테이지 버튼 진입 가능 상태를 모든 화면에 적용합니다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void ApplyStageAvailabilityRpc(string stageId, bool isEnterable, string reason)
    {
        ApplySyncedStageAvailability(stageId, isEnterable, reason);
    }

    /// <summary>
    /// Host의 버튼 Hover 상태를 모든 화면에 적용합니다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void ApplyHoverStateRpc(string stageId, bool isHovered)
    {
        ApplyHoverState(stageId, isHovered);
    }

    /// <summary>
    /// 권한 없는 Client의 열기 요청을 Server 로그로 남깁니다.
    /// </summary>
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void ReportUnauthorizedOpenRpc(RpcParams rpcParams = default)
    {
        Debug.LogWarning($"[StageSelectionUIController] Client is not allowed to open stage selection UI. senderClientId={rpcParams.Receive.SenderClientId}", this);
    }

    /// <summary>
    /// 권한 없는 Client의 닫기 요청을 Server 로그로 남깁니다.
    /// </summary>
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void ReportUnauthorizedCloseRpc(RpcParams rpcParams = default)
    {
        Debug.LogWarning($"[StageSelectionUIController] Client is not allowed to close stage selection UI. senderClientId={rpcParams.Receive.SenderClientId}", this);
    }

    /// <summary>
    /// 권한 없는 Client의 스테이지 선택 요청을 Server 로그로 남깁니다.
    /// </summary>
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void ReportUnauthorizedSelectRpc(string stageId, RpcParams rpcParams = default)
    {
        Debug.LogWarning($"[StageSelectionUIController] Client is not allowed to select stages. senderClientId={rpcParams.Receive.SenderClientId}, stageId={stageId}", this);
    }

    /// <summary>
    /// 실제 UI 활성 상태와 gameplay 입력 차단 상태를 적용합니다.
    /// </summary>
    private void ApplyOpenState(bool isOpen, string reason)
    {
        if (_uiRoot == null)
        {
            Debug.LogWarning($"[StageSelectionUIController] UI root is missing. State change ignored. isOpen={isOpen}, reason={reason}", this);
            return;
        }

        _isOpen = isOpen;
        _uiRoot.SetActive(isOpen);

        if (isOpen)
        {
            RefreshAllButtonStates();
            AcquireGameplayInputBlock();
            return;
        }

        ReleaseGameplayInputBlock();
    }

    /// <summary>
    /// 현재 로컬 피어가 스테이지 선택 UI 권한을 갖는지 판정합니다.
    /// </summary>
    private bool CanLocalPeerControlStageSelection(string requestName)
    {
        if (!IsNetworkSessionActive())
        {
            return true;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            return true;
        }

        Debug.LogWarning($"[StageSelectionUIController] Client request requires Host authority and was ignored. request={requestName}", this);
        return false;
    }

    /// <summary>
    /// 현재 NGO 네트워크 세션이 실제로 동작 중인지 확인합니다.
    /// </summary>
    private bool IsNetworkSessionActive()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    /// <summary>
    /// 현재 로컬 피어가 Hover 상태를 네트워크에 전파할 수 있는지 판정합니다.
    /// </summary>
    private bool CanLocalPeerBroadcastHover()
    {
        if (_isApplyingRemoteHoverVisual)
        {
            return false;
        }

        if (!IsNetworkSessionActive())
        {
            return true;
        }

        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }

    /// <summary>
    /// Host에서 동기화된 버튼 진입 가능 상태를 해당 StageId 버튼에 적용합니다.
    /// </summary>
    private void ApplySyncedStageAvailability(string stageId, bool isEnterable, string reason)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogWarning("[StageSelectionUIController] Synced availability ignored because stageId is empty.", this);
            return;
        }

        _isApplyingHostAvailabilitySnapshot = true;
        bool found = false; // 동기화 대상 버튼을 찾았는지 기록합니다.

        for (int i = 0; i < _stageButtons.Count; i++)
        {
            StageButtonBinding binding = _stageButtons[i]; // 동기화 상태를 적용할 후보 버튼 바인딩입니다.
            if (binding == null || binding.StageId != stageId)
            {
                continue;
            }

            binding.ApplySyncedAvailability(isEnterable, reason, this);
            found = true;
        }

        _isApplyingHostAvailabilitySnapshot = false;

        if (!found)
        {
            Debug.LogWarning($"[StageSelectionUIController] Synced availability target button not found. stageId={stageId}", this);
        }
    }

    /// <summary>
    /// Host Hover 상태를 해당 StageId 버튼의 시각 상태에 적용합니다.
    /// </summary>
    private void ApplyHoverState(string stageId, bool isHovered)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogWarning("[StageSelectionUIController] Hover state ignored because stageId is empty.", this);
            return;
        }

        _isApplyingRemoteHoverVisual = true;
        bool found = false; // Hover 적용 대상 버튼을 찾았는지 기록합니다.

        for (int i = 0; i < _stageButtons.Count; i++)
        {
            StageButtonBinding binding = _stageButtons[i]; // Hover 시각 상태를 적용할 후보 버튼 바인딩입니다.
            if (binding == null || binding.StageId != stageId)
            {
                continue;
            }

            binding.ApplyHoverVisual(isHovered, this);
            found = true;
        }

        _isApplyingRemoteHoverVisual = false;

        if (!found)
        {
            Debug.LogWarning($"[StageSelectionUIController] Hover target button not found. stageId={stageId}", this);
        }
    }

    /// <summary>
    /// 권한 없는 열기 시도를 Server RPC로 보고할 수 있으면 보고합니다.
    /// </summary>
    private void TryReportUnauthorizedOpenToServer()
    {
        if (IsNetworkSessionActive() && IsSpawned)
        {
            ReportUnauthorizedOpenRpc();
        }
    }

    /// <summary>
    /// 권한 없는 닫기 시도를 Server RPC로 보고할 수 있으면 보고합니다.
    /// </summary>
    private void TryReportUnauthorizedCloseToServer()
    {
        if (IsNetworkSessionActive() && IsSpawned)
        {
            ReportUnauthorizedCloseRpc();
        }
    }

    /// <summary>
    /// 권한 없는 선택 시도를 Server RPC로 보고할 수 있으면 보고합니다.
    /// </summary>
    private void TryReportUnauthorizedSelectToServer(string stageId)
    {
        if (IsNetworkSessionActive() && IsSpawned)
        {
            ReportUnauthorizedSelectRpc(stageId);
        }
    }

    /// <summary>
    /// UI가 열려 있는 동안 gameplay 입력 차단자를 등록합니다.
    /// </summary>
    private void AcquireGameplayInputBlock()
    {
        if (!_blockGameplayInputWhileOpen || _isBlockingGameplayInput)
        {
            return;
        }

        InputManager.AddGameplayInputBlocker(this);
        _isBlockingGameplayInput = true;
    }

    /// <summary>
    /// UI가 닫히거나 비활성화될 때 gameplay 입력 차단자를 해제합니다.
    /// </summary>
    private void ReleaseGameplayInputBlock()
    {
        if (!_isBlockingGameplayInput)
        {
            return;
        }

        InputManager.RemoveGameplayInputBlocker(this);
        _isBlockingGameplayInput = false;
    }

    /// <summary>
    /// 모든 버튼의 클릭 리스너 등록 상태를 변경합니다.
    /// </summary>
    private void BindButtons(bool bind)
    {
        for (int i = 0; i < _stageButtons.Count; i++)
        {
            StageButtonBinding binding = _stageButtons[i]; // 리스너를 등록 또는 해제할 버튼 바인딩입니다.
            if (binding == null)
            {
                continue;
            }

            if (bind)
            {
                binding.Bind(this);
            }
            else
            {
                binding.Unbind();
            }
        }
    }

    /// <summary>
    /// StageDefinition Unlock 조건 판정에 필요한 참조를 가능한 범위에서 자동 보정합니다.
    /// </summary>
    private void ResolveAvailabilityDependencies()
    {
        if (_stageProgressRuntime != null)
        {
            return;
        }

        if (StageProgressRuntime.TryGetExistingInstance(out StageProgressRuntime existingRuntime))
        {
            _stageProgressRuntime = existingRuntime;
            return;
        }

        if (TryFindDontDestroyOnLoadStageProgressRuntime(out StageProgressRuntime ddolRuntime))
        {
            _stageProgressRuntime = ddolRuntime;
            return;
        }

        if (_stageProgressRuntime == null)
        {
            Debug.LogWarning("[StageSelectionUIController] Bootstrap DDOL StageProgressRuntime was not found. Unlock conditions are evaluated with empty progress and no runtime object is created.", this);
        }
    }

    /// <summary>
    /// Bootstrap에서 DontDestroyOnLoad로 유지되는 기존 StageProgressRuntime을 생성 없이 탐색합니다.
    /// </summary>
    private bool TryFindDontDestroyOnLoadStageProgressRuntime(out StageProgressRuntime runtime)
    {
        runtime = null;

        StageProgressRuntime[] candidates = Resources.FindObjectsOfTypeAll<StageProgressRuntime>(); // DDOL 씬에 남아 있는 런타임 오브젝트까지 포함해 탐색한 후보 목록입니다.
        for (int i = 0; i < candidates.Length; i++)
        {
            StageProgressRuntime candidate = candidates[i]; // 현재 검사할 StageProgressRuntime 후보입니다.
            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            if (candidate.gameObject.scene.name != "DontDestroyOnLoad")
            {
                continue;
            }

            runtime = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Inspector 편집 시 필수 UI 참조를 가능한 범위에서 자동 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_uiRoot == null)
        {
            _uiRoot = gameObject;
        }

        for (int i = 0; i < _stageButtons.Count; i++)
        {
            StageButtonBinding binding = _stageButtons[i]; // 자동 보정 대상 버튼 바인딩입니다.
            binding?.OnValidate(this);
        }
    }
}
