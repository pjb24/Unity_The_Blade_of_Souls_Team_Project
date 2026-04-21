using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 싱글/멀티 런타임 상태를 기준으로 현재 로컬 플레이어의 HealthComponent를 해석해 제공하는 Provider입니다.
/// </summary>
[DisallowMultipleComponent]
public class LocalPlayerHealthProvider : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("플레이 모드 판별에 사용할 GameFlowController 참조입니다. 비어 있으면 런타임에서 자동 탐색합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // 플레이 모드 판별에 사용할 GameFlowController 참조입니다.

    [Tooltip("멀티플레이 로컬 플레이어 판별에 사용할 NetworkManager 참조입니다. 비어 있으면 NetworkManager.Singleton을 사용합니다.")]
    [SerializeField] private NetworkManager _networkManager; // 네트워크 로컬 플레이어 식별에 사용할 NetworkManager 참조입니다.

    [Header("Binding Policy")]
    [Tooltip("씬 로드 직후 로컬 플레이어 Health 재해석을 수행할지 여부입니다.")]
    [SerializeField] private bool _resolveOnSceneLoaded = true; // 씬 로드 시점 재해석 수행 여부를 제어하는 플래그입니다.

    [Tooltip("멀티플레이에서 NetworkManager.LocalClient.PlayerObject 미존재 시 OwnerClientId 스캔 폴백을 허용할지 여부입니다.")]
    [SerializeField] private bool _allowNetworkOwnerFallbackScan = true; // 멀티플레이 폴백 스캔 활성화 여부를 제어하는 플래그입니다.

    [Tooltip("싱글플레이에서 PlayerInputDriver 미검출 시 태그/컴포넌트 폴백 탐색을 허용할지 여부입니다.")]
    [SerializeField] private bool _allowSinglePlayerFallbackScan = true; // 싱글플레이 폴백 스캔 활성화 여부를 제어하는 플래그입니다.

    [Header("Debug")]
    [Tooltip("Provider가 폴백 경로를 사용할 때 Warning 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnOnFallbackPath = true; // 폴백 경로 사용 시 경고 로그 출력 여부입니다.

    [Tooltip("디버그용: 가장 최근에 해석된 로컬 플레이어 HealthComponent 참조입니다.")]
    [SerializeField] private HealthComponent _resolvedLocalHealth; // 가장 최근에 해석된 로컬 플레이어 HealthComponent 참조입니다.

    private readonly List<Action<HealthComponent>> _listeners = new List<Action<HealthComponent>>(); // 로컬 Health 변경 알림을 수신할 리스너 목록입니다.
    private bool _isSceneLoadedHookRegistered; // sceneLoaded 콜백 등록 상태를 추적하는 플래그입니다.
    private bool _networkCallbacksRegistered; // NetworkManager 콜백 등록 상태를 추적하는 플래그입니다.

    /// <summary>
    /// 현재 해석된 로컬 플레이어 HealthComponent를 반환합니다.
    /// </summary>
    public HealthComponent CurrentLocalHealth => _resolvedLocalHealth;

    /// <summary>
    /// 활성화 시 콜백을 연결하고 현재 로컬 플레이어 Health 해석을 시도합니다.
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
    public void AddLocalHealthChangedListener(Action<HealthComponent> listener)
    {
        if (listener == null)
        {
            return;
        }

        if (!_listeners.Contains(listener))
        {
            _listeners.Add(listener);
        }

        listener.Invoke(_resolvedLocalHealth);
    }

    /// <summary>
    /// 외부 리스너 등록을 해제합니다.
    /// </summary>
    public void RemoveLocalHealthChangedListener(Action<HealthComponent> listener)
    {
        if (listener == null)
        {
            return;
        }

        _listeners.Remove(listener);
    }

    /// <summary>
    /// 현재 로컬 플레이어 HealthComponent를 조회하고 성공 여부를 반환합니다.
    /// </summary>
    public bool TryGetCurrentLocalHealth(out HealthComponent targetHealth)
    {
        ResolveAndNotifyIfChanged();
        targetHealth = _resolvedLocalHealth;
        return targetHealth != null;
    }

    /// <summary>
    /// 의존성 참조를 자동 보정합니다.
    /// </summary>
    private void ResolveDependencies()
    {
        if (_gameFlowController == null)
        {
            _gameFlowController = GameFlowController.Instance != null
                ? GameFlowController.Instance
                : FindAnyObjectByType<GameFlowController>();
        }

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
    /// 씬 로드 직후 로컬 플레이어 Health를 재해석합니다.
    /// </summary>
    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        ResolveAndNotifyIfChanged();
    }

    /// <summary>
    /// 네트워크 클라이언트 연결 시 로컬 플레이어 Health를 재해석합니다.
    /// </summary>
    private void HandleClientConnected(ulong _)
    {
        ResolveAndNotifyIfChanged();
    }

    /// <summary>
    /// 네트워크 클라이언트 해제 시 로컬 플레이어 Health를 재해석합니다.
    /// </summary>
    private void HandleClientDisconnected(ulong _)
    {
        ResolveAndNotifyIfChanged();
    }

    /// <summary>
    /// 현재 런타임 상황에 맞는 로컬 플레이어 Health를 해석하고 변경 시 리스너에 알립니다.
    /// </summary>
    private void ResolveAndNotifyIfChanged()
    {
        HealthComponent previousHealth = _resolvedLocalHealth; // 변경 여부 비교를 위한 직전 로컬 Health 참조입니다.
        _resolvedLocalHealth = ResolveLocalHealthInternal();

        if (previousHealth == _resolvedLocalHealth)
        {
            return;
        }

        NotifyLocalHealthChanged(_resolvedLocalHealth);
    }

    /// <summary>
    /// 등록된 모든 리스너에 로컬 플레이어 Health 변경을 알립니다.
    /// </summary>
    private void NotifyLocalHealthChanged(HealthComponent localHealth)
    {
        for (int index = 0; index < _listeners.Count; index++)
        {
            Action<HealthComponent> listener = _listeners[index]; // 호출할 리스너 참조입니다.
            listener?.Invoke(localHealth);
        }
    }

    /// <summary>
    /// 네트워크/싱글플레이 분기 기준으로 로컬 플레이어 Health를 해석합니다.
    /// </summary>
    private HealthComponent ResolveLocalHealthInternal()
    {
        if (TryResolveLocalHealthFromNetwork(out HealthComponent networkLocalHealth))
        {
            return networkLocalHealth;
        }

        return ResolveLocalHealthForSinglePlayer();
    }

    /// <summary>
    /// NGO 로컬 플레이어 정보를 사용해 로컬 플레이어 Health를 해석합니다.
    /// </summary>
    private bool TryResolveLocalHealthFromNetwork(out HealthComponent localHealth)
    {
        localHealth = null;

        if (!TryResolveNetworkManager(out NetworkManager networkManager) || !networkManager.IsListening)
        {
            return false;
        }

        NetworkObject localPlayerObject = networkManager.LocalClient != null ? networkManager.LocalClient.PlayerObject : null;
        if (TryResolveHealthFromNetworkObject(localPlayerObject, out localHealth))
        {
            return true;
        }

        if (_allowNetworkOwnerFallbackScan && TryResolveLocalHealthByOwnerClientId(networkManager, out localHealth))
        {
            if (_warnOnFallbackPath)
            {
                Debug.LogWarning($"[LocalPlayerHealthProvider] LocalClient.PlayerObject was null. Used OwnerClientId fallback scan. localClientId={networkManager.LocalClientId}", this);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// NetworkObject 루트/자식/부모에서 HealthComponent를 해석합니다.
    /// </summary>
    private bool TryResolveHealthFromNetworkObject(NetworkObject networkObject, out HealthComponent localHealth)
    {
        localHealth = null;
        if (networkObject == null)
        {
            return false;
        }

        if (networkObject.TryGetComponent(out localHealth))
        {
            return true;
        }

        localHealth = networkObject.GetComponentInChildren<HealthComponent>(true);
        if (localHealth != null)
        {
            return true;
        }

        localHealth = networkObject.GetComponentInParent<HealthComponent>();
        return localHealth != null;
    }

    /// <summary>
    /// OwnerClientId 기반 스캔으로 로컬 플레이어 Health를 해석합니다.
    /// </summary>
    private bool TryResolveLocalHealthByOwnerClientId(NetworkManager networkManager, out HealthComponent localHealth)
    {
        localHealth = null;
        if (networkManager == null)
        {
            return false;
        }

        HealthComponent[] healthComponents = FindObjectsByType<HealthComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 씬에서 탐색한 HealthComponent 후보 목록입니다.
        for (int index = 0; index < healthComponents.Length; index++)
        {
            HealthComponent candidateHealth = healthComponents[index]; // 소유권 매칭을 확인할 HealthComponent 후보입니다.
            if (candidateHealth == null)
            {
                continue;
            }

            NetworkObject candidateNetworkObject = candidateHealth.GetComponentInParent<NetworkObject>();
            if (candidateNetworkObject == null || !candidateNetworkObject.IsSpawned)
            {
                continue;
            }

            if (candidateNetworkObject.OwnerClientId != networkManager.LocalClientId)
            {
                continue;
            }

            localHealth = candidateHealth;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 싱글플레이 모드 기준으로 로컬 플레이어 Health를 해석합니다.
    /// </summary>
    private HealthComponent ResolveLocalHealthForSinglePlayer()
    {
        if (!IsSinglePlayerMode())
        {
            return null;
        }

        if (TryResolveSinglePlayerHealthFromInputDriver(out HealthComponent inputDriverHealth))
        {
            return inputDriverHealth;
        }

        if (!_allowSinglePlayerFallbackScan)
        {
            return null;
        }

        HealthComponent fallbackHealth = ResolveSinglePlayerFallbackHealth();
        if (fallbackHealth != null && _warnOnFallbackPath)
        {
            Debug.LogWarning($"[LocalPlayerHealthProvider] PlayerInputDriver path failed. Used single-player fallback scan. healthObject={fallbackHealth.name}", this);
        }

        return fallbackHealth;
    }

    /// <summary>
    /// PlayerInputDriver 기준으로 싱글플레이 로컬 플레이어 Health를 해석합니다.
    /// </summary>
    private bool TryResolveSinglePlayerHealthFromInputDriver(out HealthComponent localHealth)
    {
        localHealth = null;

        PlayerInputDriver[] inputDrivers = FindObjectsByType<PlayerInputDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 씬에서 탐색한 PlayerInputDriver 후보 목록입니다.
        for (int index = 0; index < inputDrivers.Length; index++)
        {
            PlayerInputDriver inputDriver = inputDrivers[index]; // 현재 검사 중인 PlayerInputDriver 후보입니다.
            if (inputDriver == null)
            {
                continue;
            }

            HealthComponent candidateHealth = inputDriver.GetComponent<HealthComponent>();
            if (candidateHealth == null)
            {
                candidateHealth = inputDriver.GetComponentInChildren<HealthComponent>(true);
            }

            if (candidateHealth == null)
            {
                candidateHealth = inputDriver.GetComponentInParent<HealthComponent>();
            }

            if (candidateHealth == null)
            {
                continue;
            }

            localHealth = candidateHealth;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 싱글플레이 폴백 경로(Tag/컴포넌트)로 로컬 플레이어 Health를 해석합니다.
    /// </summary>
    private HealthComponent ResolveSinglePlayerFallbackHealth()
    {
        GameObject taggedPlayer = GameObject.FindWithTag("Player"); // 태그 기반으로 탐색한 플레이어 루트 오브젝트입니다.
        if (taggedPlayer != null)
        {
            HealthComponent taggedHealth = taggedPlayer.GetComponent<HealthComponent>();
            if (taggedHealth == null)
            {
                taggedHealth = taggedPlayer.GetComponentInChildren<HealthComponent>(true);
            }

            if (taggedHealth != null)
            {
                return taggedHealth;
            }
        }

        PlayerMovement[] playerMovements = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 컴포넌트 기반으로 탐색한 플레이어 이동 후보 목록입니다.
        for (int index = 0; index < playerMovements.Length; index++)
        {
            PlayerMovement movement = playerMovements[index]; // 현재 검사 중인 PlayerMovement 후보입니다.
            if (movement == null)
            {
                continue;
            }

            HealthComponent candidateHealth = movement.GetComponent<HealthComponent>();
            if (candidateHealth == null)
            {
                candidateHealth = movement.GetComponentInChildren<HealthComponent>(true);
            }

            if (candidateHealth == null)
            {
                candidateHealth = movement.GetComponentInParent<HealthComponent>();
            }

            if (candidateHealth != null)
            {
                return candidateHealth;
            }
        }

        return null;
    }

    /// <summary>
    /// 현재 런타임이 싱글플레이 모드인지 판별합니다.
    /// </summary>
    private bool IsSinglePlayerMode()
    {
        if (_gameFlowController == null)
        {
            return true;
        }

        return _gameFlowController.CurrentPlayMode == E_GamePlayMode.SinglePlayer;
    }

    /// <summary>
    /// NetworkManager 참조를 보정하고 유효성을 반환합니다.
    /// </summary>
    private bool TryResolveNetworkManager(out NetworkManager networkManager)
    {
        if (_networkManager == null)
        {
            _networkManager = NetworkManager.Singleton;
        }

        networkManager = _networkManager;
        return networkManager != null;
    }
}
