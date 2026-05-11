using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔딩 UI 루트의 표시 상태와 상호작용 상태를 제어합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingPanelView : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("엔딩 UI 전체를 켜고 끌 루트 오브젝트입니다. 비어 있으면 이 GameObject를 사용합니다.")]
    [SerializeField] private GameObject _panelRoot; // 엔딩 UI 표시 상태를 제어할 루트 오브젝트입니다.

    [Tooltip("엔딩 UI의 투명도와 Raycast 차단을 제어할 CanvasGroup입니다. 비어 있으면 활성 상태만 제어합니다.")]
    [SerializeField] private CanvasGroup _canvasGroup; // 엔딩 UI 상호작용과 표시 상태를 제어할 CanvasGroup입니다.

    [Header("Initial State")]
    [Tooltip("Awake 시점에 엔딩 UI를 숨길지 여부입니다.")]
    [SerializeField] private bool _hideOnAwake = true; // 씬 시작 시 엔딩 UI를 숨겨 둘지 결정합니다.

    [Header("Mode Visibility")]
    [Tooltip("싱글플레이 엔딩 UI에서는 표시하지 않을 UI 오브젝트 목록입니다. 멀티플레이 Host/Client에서는 다시 표시됩니다.")]
    [SerializeField] private GameObject[] _hiddenInSinglePlayerObjects = Array.Empty<GameObject>(); // 싱글플레이 엔딩에서 숨길 디자이너 지정 UI 오브젝트 목록입니다.

    [Header("Combat Stats")]
    [Tooltip("엔딩 UI에 플레이어 전투 통계를 표시할 텍스트 바인딩 목록입니다.")]
    [SerializeField] private EndingCombatStatTextBinding[] _combatStatTextBindings = Array.Empty<EndingCombatStatTextBinding>(); // 엔딩 UI에서 전투 통계를 표시할 텍스트 바인딩 목록입니다.

    [Header("Buttons")]
    [Tooltip("다음 스테이지로 이동하는 버튼입니다. 비어 있으면 자동 바인딩을 건너뜁니다.")]
    [SerializeField] private Button _nextStageButton; // 엔딩 후 다음 스테이지 이동 명령을 발생시키는 버튼입니다.

    [Tooltip("마을로 이동하는 버튼입니다. 비어 있으면 자동 바인딩을 건너뜁니다.")]
    [SerializeField] private Button _returnToTownButton; // 엔딩 후 마을 이동 명령을 발생시키는 버튼입니다.

    [Tooltip("이번 스테이지를 스테이지 진입 체크포인트에서 다시 시작하는 버튼입니다. 비어 있으면 자동 바인딩을 건너뜁니다.")]
    [SerializeField] private Button _restartStageEntryButton; // 엔딩 후 스테이지 진입 체크포인트 재시작 명령을 발생시키는 버튼입니다.

    public event Action NextStageRequested; // 다음 스테이지 이동 버튼 입력을 외부 흐름에 전달하는 이벤트입니다.
    public event Action ReturnToTownRequested; // 마을 이동 버튼 입력을 외부 흐름에 전달하는 이벤트입니다.
    public event Action RestartStageEntryRequested; // 스테이지 진입 체크포인트 재시작 버튼 입력을 외부 흐름에 전달하는 이벤트입니다.

    private bool _isCurrentlyVisible; // 현재 엔딩 UI가 표시 중인지 추적해 통계 변경 시 텍스트 갱신에 사용합니다.
    private bool _isPanelInteractable; // 현재 엔딩 UI 전체 상호작용 허용 상태를 저장합니다.
    private bool _canSelectFlowButtons = true; // 현재 로컬 플레이어가 Ending UI 진행 버튼을 선택할 수 있는지 저장합니다.

    /// <summary>
    /// 컴포넌트 초기화 시 디자이너가 지정한 초기 표시 상태를 적용합니다.
    /// </summary>
    private void Awake()
    {
        ResolveOptionalReferences();
        BindButtons();

        if (_hideOnAwake)
        {
            SetVisible(false);
        }
    }

    /// <summary>
    /// Inspector 값이 바뀔 때 누락된 보조 참조를 자동 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        ResolveOptionalReferences();
    }

    /// <summary>
    /// 전투 통계 변경 알림을 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        PlayerCombatStatsRuntime.Instance.RemoveListener(HandleCombatStatsChanged);
        PlayerCombatStatsRuntime.Instance.AddListener(HandleCombatStatsChanged);
    }

    /// <summary>
    /// 전투 통계 변경 알림 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (PlayerCombatStatsRuntime.TryGetExistingInstance(out PlayerCombatStatsRuntime statsRuntime) && statsRuntime != null)
        {
            statsRuntime.RemoveListener(HandleCombatStatsChanged);
        }
    }

    /// <summary>
    /// 컴포넌트 제거 시 버튼 리스너를 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        UnbindButtons();
    }

    /// <summary>
    /// 다음 스테이지 이동 요청을 발생시킵니다.
    /// </summary>
    public void RequestNextStage()
    {
        NextStageRequested?.Invoke();
    }

    /// <summary>
    /// 마을 이동 요청을 발생시킵니다.
    /// </summary>
    public void RequestReturnToTown()
    {
        ReturnToTownRequested?.Invoke();
    }

    /// <summary>
    /// 스테이지 진입 체크포인트 재시작 요청을 발생시킵니다.
    /// </summary>
    public void RequestRestartStageEntry()
    {
        RestartStageEntryRequested?.Invoke();
    }

    /// <summary>
    /// 엔딩 UI 표시 상태를 변경합니다.
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        _isCurrentlyVisible = isVisible;
        _isPanelInteractable = isVisible;
        GameObject root = ResolvePanelRoot(); // 실제 활성 상태를 적용할 UI 루트입니다.
        root.SetActive(isVisible);
        ApplyModeVisibility(isVisible);
        RefreshCombatStatTexts(isVisible);

        if (_canvasGroup == null)
        {
            ApplyFlowButtonAccess();
            return;
        }

        _canvasGroup.alpha = isVisible ? 1f : 0f;
        SetInteractable(isVisible);
        ApplyFlowButtonAccess();
    }

    /// <summary>
    /// 엔딩 UI의 입력 상호작용 가능 여부를 변경합니다.
    /// </summary>
    public void SetInteractable(bool isInteractable)
    {
        _isPanelInteractable = isInteractable;

        if (_canvasGroup == null)
        {
            ApplyFlowButtonAccess();
            return;
        }

        _canvasGroup.interactable = isInteractable;
        _canvasGroup.blocksRaycasts = isInteractable;
        ApplyFlowButtonAccess();
    }

    /// <summary>
    /// Ending UI의 씬 전환 버튼을 현재 로컬 권한에 맞게 선택 가능/불가능 상태로 갱신합니다.
    /// </summary>
    public void SetFlowButtonsSelectable(bool canSelect)
    {
        if (_canSelectFlowButtons == canSelect)
        {
            return;
        }

        _canSelectFlowButtons = canSelect;
        ApplyFlowButtonAccess();
    }

    /// <summary>
    /// 비어 있는 선택 참조를 현재 오브젝트 기준으로 자동 탐색합니다.
    /// </summary>
    private void ResolveOptionalReferences()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// 표시 제어에 사용할 최종 루트 오브젝트를 반환합니다.
    /// </summary>
    private GameObject ResolvePanelRoot()
    {
        return _panelRoot != null ? _panelRoot : gameObject;
    }

    /// <summary>
    /// 현재 플레이 모드에 따라 엔딩 UI 하위 선택 오브젝트의 표시 상태를 적용합니다.
    /// </summary>
    private void ApplyModeVisibility(bool isPanelVisible)
    {
        if (_hiddenInSinglePlayerObjects == null || _hiddenInSinglePlayerObjects.Length == 0)
        {
            return;
        }

        bool shouldShowRestrictedObjects = isPanelVisible && !IsSinglePlayerMode(); // 싱글플레이에서 숨겨야 하는 UI를 실제로 표시할지 여부입니다.
        for (int index = 0; index < _hiddenInSinglePlayerObjects.Length; index++)
        {
            GameObject targetObject = _hiddenInSinglePlayerObjects[index]; // 플레이 모드별 표시 상태를 제어할 UI 오브젝트입니다.
            if (targetObject == null)
            {
                continue;
            }

            targetObject.SetActive(shouldShowRestrictedObjects);
        }
    }

    /// <summary>
    /// 현재 런타임이 싱글플레이 엔딩 표시 조건인지 판정합니다.
    /// </summary>
    private bool IsSinglePlayerMode()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // NGO 세션 활성 여부를 확인할 네트워크 관리자입니다.
        if (networkManager != null && networkManager.IsListening)
        {
            return false;
        }

        GameFlowController gameFlowController = GameFlowController.Instance; // 상위 게임 플레이 모드 설정을 확인할 컨트롤러입니다.
        return gameFlowController == null || gameFlowController.CurrentPlayMode == E_GamePlayMode.SinglePlayer;
    }

    /// <summary>
    /// Host/Single 권한이 없는 Client가 Ending UI 진행 버튼을 선택하지 못하도록 Button 상태를 적용합니다.
    /// </summary>
    private void ApplyFlowButtonAccess()
    {
        bool canInteract = _isCurrentlyVisible && _isPanelInteractable && _canSelectFlowButtons; // 패널 표시 상태, 전체 상호작용 상태, 권한을 함께 반영한 실제 버튼 선택 가능 여부입니다.
        SetButtonInteractable(_returnToTownButton, canInteract);
        SetButtonInteractable(_restartStageEntryButton, canInteract);
    }

    /// <summary>
    /// null 안전성을 유지하면서 Button 선택 가능 상태를 변경합니다.
    /// </summary>
    private void SetButtonInteractable(Button targetButton, bool isInteractable)
    {
        if (targetButton == null)
        {
            return;
        }

        targetButton.interactable = isInteractable;
    }

    /// <summary>
    /// 엔딩 UI에 연결된 전투 통계 텍스트들을 현재 런타임 값으로 갱신합니다.
    /// </summary>
    private void RefreshCombatStatTexts(bool isPanelVisible)
    {
        if (!isPanelVisible || _combatStatTextBindings == null || _combatStatTextBindings.Length == 0)
        {
            return;
        }

        PlayerCombatStatsRuntime statsRuntime = PlayerCombatStatsRuntime.Instance; // 저장/런타임에서 로드된 로컬 플레이어 전투 통계입니다.
        for (int index = 0; index < _combatStatTextBindings.Length; index++)
        {
            EndingCombatStatTextBinding binding = _combatStatTextBindings[index]; // 갱신할 텍스트와 표시 데이터 설정입니다.
            if (binding == null)
            {
                continue;
            }

            binding.Apply(statsRuntime);
        }
    }

    /// <summary>
    /// 전투 통계가 바뀌면 엔딩 UI 표시 중인 텍스트를 즉시 갱신합니다.
    /// </summary>
    private void HandleCombatStatsChanged()
    {
        RefreshCombatStatTexts(_isCurrentlyVisible);
    }

    /// <summary>
    /// Inspector에 연결된 버튼을 View 이벤트에 바인딩합니다.
    /// </summary>
    private void BindButtons()
    {
        UnbindButtons();

        if (_nextStageButton != null)
        {
            _nextStageButton.onClick.AddListener(RequestNextStage);
        }

        if (_returnToTownButton != null)
        {
            _returnToTownButton.onClick.AddListener(RequestReturnToTown);
        }

        if (_restartStageEntryButton != null)
        {
            _restartStageEntryButton.onClick.AddListener(RequestRestartStageEntry);
        }
    }

    /// <summary>
    /// 버튼 리스너 중복 등록을 방지하기 위해 기존 바인딩을 해제합니다.
    /// </summary>
    private void UnbindButtons()
    {
        if (_nextStageButton != null)
        {
            _nextStageButton.onClick.RemoveListener(RequestNextStage);
        }

        if (_returnToTownButton != null)
        {
            _returnToTownButton.onClick.RemoveListener(RequestReturnToTown);
        }

        if (_restartStageEntryButton != null)
        {
            _restartStageEntryButton.onClick.RemoveListener(RequestRestartStageEntry);
        }
    }

}

/// <summary>
/// 엔딩 UI에서 표시할 플레이어 전투 통계 종류입니다.
/// </summary>
public enum E_EndingCombatStatType
{
    TotalDamageDealt = 0,
    DamageTakenCount = 1
}

/// <summary>
/// 엔딩 UI 텍스트가 표시할 멀티플레이 전투 통계 소스입니다.
/// </summary>
public enum E_EndingCombatStatSource
{
    LocalPlayer = 0,
    HostPlayer = 1,
    FirstClientPlayer = 2,
    SpecificClientId = 3
}

/// <summary>
/// 하나의 텍스트 UI와 표시할 전투 통계 값을 연결하는 바인딩입니다.
/// </summary>
[Serializable]
public sealed class EndingCombatStatTextBinding
{
    public string Name;

    [Tooltip("멀티플레이 엔딩 UI에서 이 바인딩이 표시할 플레이어 통계 소스입니다.")]
    [SerializeField] private E_EndingCombatStatSource _statSource; // Host/Client/Local 중 어떤 플레이어의 통계를 표시할지 결정하는 소스입니다.

    [Tooltip("Stat Source가 Specific Client Id일 때 사용할 NGO ClientId입니다.")]
    [SerializeField] private ulong _specificClientId; // 특정 ClientId의 통계를 직접 표시해야 할 때 사용하는 NGO ClientId입니다.

    [Tooltip("전투 통계 값을 표시할 TMP 텍스트입니다.")]
    [SerializeField] private TMP_Text _text; // 전투 통계 값을 출력할 TMP 텍스트입니다.

    [Tooltip("이 텍스트에 표시할 전투 통계 종류입니다.")]
    [SerializeField] private E_EndingCombatStatType _statType; // 텍스트가 표시할 전투 통계 종류입니다.

    [Tooltip("표시 형식입니다. {0} 위치에 수치가 들어갑니다.")]
    [SerializeField] private string _format = "{0}"; // 통계 수치를 문자열로 변환할 때 사용할 표시 형식입니다.

    [Tooltip("대미지처럼 실수 값인 통계에 적용할 소수점 자리수입니다.")]
    [Min(0)]
    [SerializeField] private int _decimalPlaces; // 실수 통계 표시 시 사용할 소수점 자리수입니다.

    /// <summary>
    /// 런타임 전투 통계 값을 텍스트 UI에 적용합니다.
    /// </summary>
    public void Apply(PlayerCombatStatsRuntime statsRuntime)
    {
        if (_text == null)
        {
            return;
        }

        string safeFormat = string.IsNullOrWhiteSpace(_format) ? "{0}" : _format; // 비어 있는 포맷의 안전한 폴백입니다.
        PlayerCombatStatsRuntime.SnapshotData snapshot = ResolveSnapshot(statsRuntime); // 선택된 플레이어 소스에 해당하는 전투 통계입니다.
        switch (_statType)
        {
            case E_EndingCombatStatType.TotalDamageDealt:
                float damage = snapshot.TotalDamageDealt; // 표시할 누적 적용 대미지입니다.
                _text.text = string.Format(safeFormat, damage.ToString($"F{Mathf.Max(0, _decimalPlaces)}"));
                break;
            case E_EndingCombatStatType.DamageTakenCount:
                int hitCount = snapshot.DamageTakenCount; // 표시할 누적 피격 횟수입니다.
                _text.text = string.Format(safeFormat, hitCount);
                break;
        }
    }

    /// <summary>
    /// Inspector에서 선택한 통계 소스에 맞는 전투 통계 스냅샷을 반환합니다.
    /// </summary>
    private PlayerCombatStatsRuntime.SnapshotData ResolveSnapshot(PlayerCombatStatsRuntime statsRuntime)
    {
        if (statsRuntime == null)
        {
            return default;
        }

        switch (_statSource)
        {
            case E_EndingCombatStatSource.HostPlayer:
                return statsRuntime.GetHostPlayerSnapshot();
            case E_EndingCombatStatSource.FirstClientPlayer:
                return statsRuntime.GetFirstRemoteClientSnapshot();
            case E_EndingCombatStatSource.SpecificClientId:
                return statsRuntime.GetSnapshotForClientId(_specificClientId);
            case E_EndingCombatStatSource.LocalPlayer:
            default:
                return statsRuntime.GetLocalPlayerSnapshot();
        }
    }
}
