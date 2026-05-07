using System;
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
        GameObject root = ResolvePanelRoot(); // 실제 활성 상태를 적용할 UI 루트입니다.
        root.SetActive(isVisible);
        ApplyModeVisibility(isVisible);

        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = isVisible ? 1f : 0f;
        SetInteractable(isVisible);
    }

    /// <summary>
    /// 엔딩 UI의 입력 상호작용 가능 여부를 변경합니다.
    /// </summary>
    public void SetInteractable(bool isInteractable)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.interactable = isInteractable;
        _canvasGroup.blocksRaycasts = isInteractable;
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
