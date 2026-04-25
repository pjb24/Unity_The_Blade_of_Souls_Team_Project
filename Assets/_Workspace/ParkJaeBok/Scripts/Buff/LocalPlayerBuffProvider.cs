using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 싱글/멀티 런타임 상태를 기준으로 현재 로컬 플레이어의 PlayerBuffController를 해석해 제공하는 Provider입니다.
/// </summary>
[DisallowMultipleComponent]
public class LocalPlayerBuffProvider : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("멀티플레이 로컬 플레이어 판별에 사용할 NetworkManager 참조입니다. 비어 있으면 NetworkManager.Singleton을 사용합니다.")]
    [SerializeField] private NetworkManager _networkManager; // 네트워크 로컬 플레이어 식별에 사용할 NetworkManager 참조입니다.

    [Header("Binding Policy")]
    [Tooltip("씬 로드 직후 로컬 플레이어 Buff 재해석을 수행할지 여부입니다.")]
    [SerializeField] private bool _resolveOnSceneLoaded = true; // 씬 로드 시점 재해석 수행 여부를 제어하는 플래그입니다.

    [Tooltip("멀티플레이에서 NetworkManager.LocalClient.PlayerObject 미존재 시 OwnerClientId 스캔 폴백을 허용할지 여부입니다.")]
    [SerializeField] private bool _allowNetworkOwnerFallbackScan = true; // 멀티플레이 폴백 스캔 활성화 여부를 제어하는 플래그입니다.

    [Tooltip("싱글플레이에서 PlayerInputDriver 미검출 시 BuffController 전체 스캔 폴백을 허용할지 여부입니다.")]
    [SerializeField] private bool _allowSinglePlayerFallbackScan = true; // 싱글플레이 폴백 스캔 활성화 여부를 제어하는 플래그입니다.

    [Header("Debug")]
    [Tooltip("Provider가 폴백 경로를 사용할 때 Warning 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnOnFallbackPath = true; // 폴백 경로 사용 시 경고 로그 출력 여부입니다.

    [Tooltip("디버그용: 가장 최근에 해석된 로컬 플레이어 PlayerBuffController 참조입니다.")]
    [SerializeField] private PlayerBuffController _resolvedLocalBuffController; // 가장 최근에 해석된 로컬 플레이어 PlayerBuffController 참조입니다.

    private readonly List<Action<PlayerBuffController>> _listeners = new List<Action<PlayerBuffController>>(); // 로컬 Buff 변경 알림을 수신할 리스너 목록입니다.
    private bool _isSceneLoadedHookRegistered; // sceneLoaded 콜백 등록 상태를 추적하는 플래그입니다.
    private bool _networkCallbacksRegistered; // NetworkManager 콜백 등록 상태를 추적하는 플래그입니다.

    /// <summary>
    /// 현재 해석된 로컬 플레이어 Buff 컨트롤러를 반환합니다.
    /// </summary>
    public PlayerBuffController CurrentLocalBuffController => _resolvedLocalBuffController;

    /// <summary>
    /// 활성화 시 콜백을 연결하고 현재 로컬 플레이어 Buff 해석을 시도합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveDependencies();
        RegisterHooks();
        ResolveAndNotifyIfChanged();
    }

    /// <summary>
    /// 비활성화 시 콜백을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterHooks();
    }

    /// <summary>
    /// 외부 리스너를 등록하고 현재 해석 결과를 즉시 전달합니다.
    /// </summary>
    public void AddLocalBuffChangedListener(Action<PlayerBuffController> listener)
    {
        if (listener == null)
        {
            return;
        }

        if (!_listeners.Contains(listener))
        {
            _listeners.Add(listener);
        }

        listener.Invoke(_resolvedLocalBuffController);
    }

    /// <summary>
    /// 외부 리스너 등록을 해제합니다.
    /// </summary>
    public void RemoveLocalBuffChangedListener(Action<PlayerBuffController> listener)
    {
        if (listener == null)
        {
            return;
        }

        _listeners.Remove(listener);
    }

    /// <summary>
    /// 현재 로컬 플레이어 Buff 컨트롤러를 조회하고 성공 여부를 반환합니다.
    /// </summary>
    public bool TryGetCurrentLocalBuffController(out PlayerBuffController targetController)
    {
        ResolveAndNotifyIfChanged();
        targetController = _resolvedLocalBuffController;
        return targetController != null;
    }

    /// <summary>
    /// 의존성 참조를 자동 보정합니다.
    /// </summary>
    private void ResolveDependencies()
    {
        if (_networkManager == null)
        {
            _networkManager = NetworkManager.Singleton;
        }
    }

    /// <summary>
    /// 씬/네트워크 콜백을 등록합니다.
    /// </summary>
    private void RegisterHooks()
    {
        if (_resolveOnSceneLoaded && !_isSceneLoadedHookRegistered)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _isSceneLoadedHookRegistered = true;
        }

        if (!TryResolveNetworkManager(out NetworkManager networkManager))
        {
            return;
        }

        if (_networkCallbacksRegistered)
        {
            return;
        }

        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        _networkCallbacksRegistered = true;
    }

    /// <summary>
    /// 등록된 씬/네트워크 콜백을 해제합니다.
    /// </summary>
    private void UnregisterHooks()
    {
        if (_isSceneLoadedHookRegistered)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _isSceneLoadedHookRegistered = false;
        }

        if (!TryResolveNetworkManager(out NetworkManager networkManager) || !_networkCallbacksRegistered)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        _networkCallbacksRegistered = false;
    }

    /// <summary>
    /// 씬 로드 직후 로컬 플레이어 Buff 컨트롤러를 재해석합니다.
    /// </summary>
    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        ResolveAndNotifyIfChanged();
    }

    /// <summary>
    /// 네트워크 클라이언트 연결 시 로컬 플레이어 Buff 컨트롤러를 재해석합니다.
    /// </summary>
    private void HandleClientConnected(ulong _)
    {
        ResolveAndNotifyIfChanged();
    }

    /// <summary>
    /// 네트워크 클라이언트 해제 시 로컬 플레이어 Buff 컨트롤러를 재해석합니다.
    /// </summary>
    private void HandleClientDisconnected(ulong _)
    {
        ResolveAndNotifyIfChanged();
    }

    /// <summary>
    /// 현재 런타임 상황에 맞는 로컬 플레이어 Buff 컨트롤러를 해석하고 변경 시 리스너에 알립니다.
    /// </summary>
    private void ResolveAndNotifyIfChanged()
    {
        PlayerBuffController previousController = _resolvedLocalBuffController; // 변경 여부 비교를 위한 직전 로컬 Buff 컨트롤러 참조입니다.
        _resolvedLocalBuffController = ResolveLocalBuffControllerInternal();

        if (previousController == _resolvedLocalBuffController)
        {
            return;
        }

        NotifyLocalBuffChanged(_resolvedLocalBuffController);
    }

    /// <summary>
    /// 등록된 모든 리스너에 로컬 플레이어 Buff 컨트롤러 변경을 알립니다.
    /// </summary>
    private void NotifyLocalBuffChanged(PlayerBuffController localController)
    {
        for (int index = 0; index < _listeners.Count; index++)
        {
            Action<PlayerBuffController> listener = _listeners[index]; // 호출할 리스너 참조입니다.
            listener?.Invoke(localController);
        }
    }

    /// <summary>
    /// 네트워크/싱글플레이 분기 기준으로 로컬 플레이어 Buff 컨트롤러를 해석합니다.
    /// </summary>
    private PlayerBuffController ResolveLocalBuffControllerInternal()
    {
        if (TryResolveLocalBuffControllerFromNetwork(out PlayerBuffController networkLocalController))
        {
            return networkLocalController;
        }

        return ResolveLocalBuffControllerForSinglePlayer();
    }

    /// <summary>
    /// NGO 로컬 플레이어 정보를 사용해 로컬 플레이어 Buff 컨트롤러를 해석합니다.
    /// </summary>
    private bool TryResolveLocalBuffControllerFromNetwork(out PlayerBuffController localController)
    {
        localController = null;

        if (!TryResolveNetworkManager(out NetworkManager networkManager) || !networkManager.IsListening)
        {
            return false;
        }

        NetworkObject localPlayerObject = networkManager.LocalClient != null ? networkManager.LocalClient.PlayerObject : null;
        if (TryResolveBuffControllerFromNetworkObject(localPlayerObject, out localController))
        {
            return true;
        }

        if (_allowNetworkOwnerFallbackScan && TryResolveLocalBuffControllerByOwnerClientId(networkManager, out localController))
        {
            if (_warnOnFallbackPath)
            {
                Debug.LogWarning($"[LocalPlayerBuffProvider] LocalClient.PlayerObject was null. Used OwnerClientId fallback scan. localClientId={networkManager.LocalClientId}", this);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// NetworkObject 루트/자식/부모에서 PlayerBuffController를 해석합니다.
    /// </summary>
    private bool TryResolveBuffControllerFromNetworkObject(NetworkObject networkObject, out PlayerBuffController localController)
    {
        localController = null;
        if (networkObject == null)
        {
            return false;
        }

        if (networkObject.TryGetComponent(out localController))
        {
            return true;
        }

        localController = networkObject.GetComponentInChildren<PlayerBuffController>(true);
        if (localController != null)
        {
            return true;
        }

        localController = networkObject.GetComponentInParent<PlayerBuffController>();
        return localController != null;
    }

    /// <summary>
    /// OwnerClientId 기반 스캔으로 로컬 플레이어 Buff 컨트롤러를 해석합니다.
    /// </summary>
    private bool TryResolveLocalBuffControllerByOwnerClientId(NetworkManager networkManager, out PlayerBuffController localController)
    {
        localController = null;
        if (networkManager == null)
        {
            return false;
        }

        PlayerBuffController[] buffControllers = FindObjectsByType<PlayerBuffController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 씬에서 탐색한 Buff 컨트롤러 후보 목록입니다.
        for (int index = 0; index < buffControllers.Length; index++)
        {
            PlayerBuffController candidateController = buffControllers[index]; // 소유권 매칭을 확인할 Buff 컨트롤러 후보입니다.
            if (candidateController == null)
            {
                continue;
            }

            NetworkObject candidateNetworkObject = candidateController.GetComponentInParent<NetworkObject>();
            if (candidateNetworkObject == null || !candidateNetworkObject.IsSpawned)
            {
                continue;
            }

            if (candidateNetworkObject.OwnerClientId != networkManager.LocalClientId)
            {
                continue;
            }

            localController = candidateController;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 싱글플레이 기준으로 로컬 플레이어 Buff 컨트롤러를 해석합니다.
    /// </summary>
    private PlayerBuffController ResolveLocalBuffControllerForSinglePlayer()
    {
        PlayerInputDriver[] inputDrivers = FindObjectsByType<PlayerInputDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 로컬 플레이어 후보를 찾기 위한 입력 드라이버 목록입니다.
        for (int index = 0; index < inputDrivers.Length; index++)
        {
            PlayerInputDriver inputDriver = inputDrivers[index]; // 현재 검사 중인 입력 드라이버입니다.
            if (inputDriver == null)
            {
                continue;
            }

            PlayerBuffController buffController = inputDriver.GetComponent<PlayerBuffController>();
            if (buffController == null)
            {
                buffController = inputDriver.GetComponentInChildren<PlayerBuffController>(true);
            }

            if (buffController != null)
            {
                return buffController;
            }
        }

        if (_allowSinglePlayerFallbackScan)
        {
            PlayerBuffController fallbackController = FindAnyObjectByType<PlayerBuffController>(FindObjectsInactive.Include); // 폴백 스캔에서 발견한 Buff 컨트롤러입니다.
            if (_warnOnFallbackPath && fallbackController != null)
            {
                Debug.LogWarning($"[LocalPlayerBuffProvider] PlayerInputDriver was not found. Used single-player fallback scan. object={fallbackController.name}", this);
            }

            return fallbackController;
        }

        return null;
    }

    /// <summary>
    /// 유효한 NetworkManager 참조를 반환합니다.
    /// </summary>
    private bool TryResolveNetworkManager(out NetworkManager networkManager)
    {
        networkManager = _networkManager != null ? _networkManager : NetworkManager.Singleton;
        return networkManager != null;
    }
}
