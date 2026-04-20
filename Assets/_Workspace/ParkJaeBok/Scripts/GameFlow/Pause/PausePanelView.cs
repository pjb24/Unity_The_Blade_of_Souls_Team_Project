using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Pause 패널의 버튼/표시 상태를 담당하는 View입니다.
/// </summary>
public class PausePanelView : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("Pause 패널 전체 루트 오브젝트입니다.")]
    [SerializeField] private GameObject _panelRoot; // Pause 패널 표시/숨김을 제어할 루트 오브젝트입니다.

    [Header("Buttons")]
    [Tooltip("Resume 버튼 참조입니다.")]
    [SerializeField] private Button _resumeButton; // Resume 요청을 전달할 버튼입니다.

    [Tooltip("Settings 버튼 참조입니다.")]
    [SerializeField] private Button _settingsButton; // Settings 요청을 전달할 버튼입니다.

    [Tooltip("Main Menu 버튼 참조입니다.")]
    [SerializeField] private Button _mainMenuButton; // Main Menu 복귀 요청을 전달할 버튼입니다.

    private readonly Dictionary<Action, UnityAction> _resumeListenerMap = new Dictionary<Action, UnityAction>(); // Resume 버튼 리스너 해제를 위해 Action-UnityAction 매핑을 보관하는 딕셔너리입니다.
    private readonly Dictionary<Action, UnityAction> _settingsListenerMap = new Dictionary<Action, UnityAction>(); // Settings 버튼 리스너 해제를 위해 Action-UnityAction 매핑을 보관하는 딕셔너리입니다.
    private readonly Dictionary<Action, UnityAction> _mainMenuListenerMap = new Dictionary<Action, UnityAction>(); // Main Menu 버튼 리스너 해제를 위해 Action-UnityAction 매핑을 보관하는 딕셔너리입니다.

    /// <summary>
    /// Pause 패널 표시 상태를 제어합니다.
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        GameObject root = _panelRoot != null ? _panelRoot : gameObject; // 패널 루트 미지정 시 자기 자신을 루트로 사용할 대상입니다.
        root.SetActive(isVisible);
    }

    /// <summary>
    /// Pause 패널 버튼 상호작용 가능 여부를 일괄 제어합니다.
    /// </summary>
    public void SetInteractable(bool isInteractable)
    {
        SetButtonInteractable(_resumeButton, isInteractable);
        SetButtonInteractable(_settingsButton, isInteractable);
        SetButtonInteractable(_mainMenuButton, isInteractable);
    }

    /// <summary>
    /// Resume 버튼 클릭 리스너를 등록합니다.
    /// </summary>
    public void AddResumeListener(Action listener)
    {
        AddListener(_resumeButton, listener, _resumeListenerMap, "Resume");
    }

    /// <summary>
    /// Resume 버튼 클릭 리스너를 해제합니다.
    /// </summary>
    public void RemoveResumeListener(Action listener)
    {
        RemoveListener(_resumeButton, listener, _resumeListenerMap);
    }

    /// <summary>
    /// Settings 버튼 클릭 리스너를 등록합니다.
    /// </summary>
    public void AddSettingsListener(Action listener)
    {
        AddListener(_settingsButton, listener, _settingsListenerMap, "Settings");
    }

    /// <summary>
    /// Settings 버튼 클릭 리스너를 해제합니다.
    /// </summary>
    public void RemoveSettingsListener(Action listener)
    {
        RemoveListener(_settingsButton, listener, _settingsListenerMap);
    }

    /// <summary>
    /// Main Menu 버튼 클릭 리스너를 등록합니다.
    /// </summary>
    public void AddMainMenuListener(Action listener)
    {
        AddListener(_mainMenuButton, listener, _mainMenuListenerMap, "MainMenu");
    }

    /// <summary>
    /// Main Menu 버튼 클릭 리스너를 해제합니다.
    /// </summary>
    public void RemoveMainMenuListener(Action listener)
    {
        RemoveListener(_mainMenuButton, listener, _mainMenuListenerMap);
    }

    /// <summary>
    /// Button 참조가 유효할 때 클릭 리스너를 연결합니다.
    /// </summary>
    private void AddListener(Button button, Action listener, Dictionary<Action, UnityAction> listenerMap, string buttonName)
    {
        if (button == null)
        {
            Debug.LogWarning($"[PausePanelView] 버튼 참조가 비어 있어 리스너 등록을 건너뜁니다. button={buttonName}", this);
            return;
        }

        if (listener == null)
        {
            Debug.LogWarning($"[PausePanelView] null 리스너 등록 요청을 건너뜁니다. button={buttonName}", this);
            return;
        }

        if (listenerMap.ContainsKey(listener))
        {
            return;
        }

        UnityAction unityAction = () => listener.Invoke(); // Button.onClick 등록/해제에 사용할 UnityAction 래퍼입니다.
        listenerMap.Add(listener, unityAction);
        button.onClick.AddListener(unityAction);
    }

    /// <summary>
    /// Button 참조가 유효할 때 클릭 리스너를 해제합니다.
    /// </summary>
    private void RemoveListener(Button button, Action listener, Dictionary<Action, UnityAction> listenerMap)
    {
        if (button == null || listener == null)
        {
            return;
        }

        if (listenerMap.TryGetValue(listener, out UnityAction unityAction) == false)
        {
            return;
        }

        button.onClick.RemoveListener(unityAction);
        listenerMap.Remove(listener);
    }

    /// <summary>
    /// 개별 버튼의 interactable 값을 안전하게 반영합니다.
    /// </summary>
    private void SetButtonInteractable(Button button, bool isInteractable)
    {
        if (button != null)
        {
            button.interactable = isInteractable;
        }
    }
}
