using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사망 UI 루트의 표시 상태, 상호작용 상태, 버튼 요청 이벤트를 제어합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DeathPanelView : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("사망 UI 전체를 켜고 끌 루트 오브젝트입니다. 비어 있으면 이 GameObject를 사용합니다.")]
    [SerializeField] private GameObject _panelRoot; // 사망 UI 표시 상태를 제어할 루트 오브젝트입니다.

    [Tooltip("사망 UI의 투명도와 Raycast 차단을 제어할 CanvasGroup입니다. 비어 있으면 활성 상태만 제어합니다.")]
    [SerializeField] private CanvasGroup _canvasGroup; // 사망 UI 상호작용과 표시 상태를 제어할 CanvasGroup입니다.

    [Header("Initial State")]
    [Tooltip("Awake 시점에 사망 UI를 숨길지 여부입니다.")]
    [SerializeField] private bool _hideOnAwake = true; // 씬 시작 시 사망 UI를 숨겨 둘지 결정합니다.

    [Header("Buttons")]
    [Tooltip("이번 스테이지를 마지막 체크포인트에서 다시 시작하는 버튼입니다. 비어 있으면 자동 바인딩을 건너뜁니다.")]
    [SerializeField] private Button _restartLastCheckpointButton; // 마지막 체크포인트 재시작 명령을 발생시키는 버튼입니다.

    [Tooltip("마을로 이동하는 버튼입니다. 비어 있으면 자동 바인딩을 건너뜁니다.")]
    [SerializeField] private Button _returnToTownButton; // 마을 이동 명령을 발생시키는 버튼입니다.

    [Tooltip("게임을 종료하는 버튼입니다. 비어 있으면 자동 바인딩을 건너뜁니다.")]
    [SerializeField] private Button _quitGameButton; // 게임 종료 명령을 발생시키는 버튼입니다.

    public event Action RestartLastCheckpointRequested; // 마지막 체크포인트 재시작 버튼 입력을 외부 흐름에 전달하는 이벤트입니다.
    public event Action ReturnToTownRequested; // 마을 이동 버튼 입력을 외부 흐름에 전달하는 이벤트입니다.
    public event Action QuitGameRequested; // 게임 종료 버튼 입력을 외부 흐름에 전달하는 이벤트입니다.

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
    /// 마지막 체크포인트 재시작 요청을 발생시킵니다.
    /// </summary>
    public void RequestRestartLastCheckpoint()
    {
        RestartLastCheckpointRequested?.Invoke();
    }

    /// <summary>
    /// 마을 이동 요청을 발생시킵니다.
    /// </summary>
    public void RequestReturnToTown()
    {
        ReturnToTownRequested?.Invoke();
    }

    /// <summary>
    /// 게임 종료 요청을 발생시킵니다.
    /// </summary>
    public void RequestQuitGame()
    {
        QuitGameRequested?.Invoke();
    }

    /// <summary>
    /// 사망 UI 표시 상태를 변경합니다.
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        GameObject root = ResolvePanelRoot(); // 실제 활성 상태를 적용할 UI 루트입니다.
        root.SetActive(isVisible);

        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = isVisible ? 1f : 0f;
        SetInteractable(isVisible);
    }

    /// <summary>
    /// 사망 UI의 입력 상호작용 가능 여부를 변경합니다.
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
    /// Inspector에 연결된 버튼을 View 이벤트에 바인딩합니다.
    /// </summary>
    private void BindButtons()
    {
        UnbindButtons();

        if (_restartLastCheckpointButton != null)
        {
            _restartLastCheckpointButton.onClick.AddListener(RequestRestartLastCheckpoint);
        }

        if (_returnToTownButton != null)
        {
            _returnToTownButton.onClick.AddListener(RequestReturnToTown);
        }

        if (_quitGameButton != null)
        {
            _quitGameButton.onClick.AddListener(RequestQuitGame);
        }
    }

    /// <summary>
    /// 버튼 리스너 중복 등록을 방지하기 위해 기존 바인딩을 해제합니다.
    /// </summary>
    private void UnbindButtons()
    {
        if (_restartLastCheckpointButton != null)
        {
            _restartLastCheckpointButton.onClick.RemoveListener(RequestRestartLastCheckpoint);
        }

        if (_returnToTownButton != null)
        {
            _returnToTownButton.onClick.RemoveListener(RequestReturnToTown);
        }

        if (_quitGameButton != null)
        {
            _quitGameButton.onClick.RemoveListener(RequestQuitGame);
        }
    }
}
