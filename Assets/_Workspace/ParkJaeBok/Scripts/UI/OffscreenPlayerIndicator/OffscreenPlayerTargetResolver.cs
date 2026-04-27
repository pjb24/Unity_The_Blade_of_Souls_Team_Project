using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로컬 플레이어를 제외한 다른 플레이어 Transform을 NGO NetworkObject 기준으로 해석합니다.
/// </summary>
[DisallowMultipleComponent]
public class OffscreenPlayerTargetResolver : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("플레이어 탐색에 사용할 NetworkManager입니다. 비어 있으면 NetworkManager.Singleton을 사용합니다.")]
    [SerializeField] private NetworkManager _networkManager; // NGO 플레이어 목록 조회에 사용할 NetworkManager 참조입니다.

    [Header("Target Policy")]
    [Tooltip("NetworkManager의 PlayerObject가 아직 준비되지 않았을 때 NetworkObject 전체 스캔 폴백을 허용할지 여부입니다.")]
    [SerializeField] private bool _allowNetworkObjectFallbackScan = true; // 지연 Spawn 상황에서 NetworkObject 스캔으로 대상 탐색을 보정할지 여부입니다.

    [Tooltip("대상 Transform을 찾지 못했을 때 재탐색할 주기(초)입니다.")]
    [SerializeField] private float _resolveRetryInterval = 0.5f; // 대상 플레이어 재탐색 간격입니다.

    [Tooltip("플레이어 탐색 폴백 경로를 사용할 때 Warning 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnOnFallbackPath = true; // 폴백 탐색 사용 여부를 Warning으로 알릴지 제어하는 플래그입니다.

    [Header("Debug")]
    [Tooltip("현재 표시 대상으로 해석된 원격 플레이어 Transform입니다.")]
    [SerializeField] private Transform _resolvedTargetTransform; // 현재 로컬 화면에서 표시해야 하는 원격 플레이어 Transform 참조입니다.

    [Tooltip("현재 로컬 플레이어로 해석된 NetworkObject입니다.")]
    [SerializeField] private NetworkObject _resolvedLocalPlayerObject; // 자기 자신 제외 판정에 사용할 로컬 PlayerObject 참조입니다.

    [Tooltip("현재 로컬 플레이어 캐릭터 기준점으로 해석된 Transform입니다.")]
    [SerializeField] private Transform _resolvedLocalPlayerTransform; // Indicator 방향 계산의 기준점으로 사용할 로컬 플레이어 캐릭터 Transform 참조입니다.

    [Tooltip("현재 표시 대상으로 해석된 원격 플레이어 NetworkObject입니다.")]
    [SerializeField] private NetworkObject _resolvedTargetNetworkObject; // 표시 대상 플레이어의 NetworkObject 참조입니다.

    private event Action<Transform> TargetChanged; // 대상 변경 알림을 내부에 캡슐화한 이벤트입니다.
    private bool _isSceneLoadedHookRegistered; // sceneLoaded 콜백 등록 상태를 추적하는 플래그입니다.
    private bool _networkCallbacksRegistered; // NetworkManager 연결/해제 콜백 등록 상태를 추적하는 플래그입니다.
    private bool _hasLoggedNetworkObjectFallbackWarning; // NetworkObject 폴백 Warning 중복 출력을 방지하는 플래그입니다.
    private bool _hasLoggedMissingTargetWarning; // 멀티플레이 대상 없음 Warning 중복 출력을 방지하는 플래그입니다.
    private float _nextResolveTime; // 다음 재탐색이 가능한 Time.time 기준 시각입니다.

    /// <summary>
    /// 현재 표시 대상 Transform을 반환합니다.
    /// </summary>
    public Transform CurrentTargetTransform => _resolvedTargetTransform;

    /// <summary>
    /// 현재 로컬 플레이어 캐릭터 기준 Transform을 반환합니다.
    /// </summary>
    public Transform CurrentLocalPlayerTransform => _resolvedLocalPlayerTransform;

    /// <summary>
    /// 대상 변경 알림을 구독합니다.
    /// </summary>
    public void AddListener(Action<Transform> listener)
    {
        if (listener == null)
        {
            return;
        }

        TargetChanged += listener;
        listener.Invoke(_resolvedTargetTransform);
    }

    /// <summary>
    /// 대상 변경 알림 구독을 해제합니다.
    /// </summary>
    public void RemoveListener(Action<Transform> listener)
    {
        if (listener == null)
        {
            return;
        }

        TargetChanged -= listener;
    }

    /// <summary>
    /// 컴포넌트 활성화 시 네트워크 콜백을 등록하고 즉시 대상을 탐색합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveDependencies();
        RegisterHooks();
        ForceResolveTarget();
    }

    /// <summary>
    /// 컴포넌트 비활성화 시 등록한 콜백을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterHooks();
    }

    /// <summary>
    /// Inspector 값 변경 시 재탐색 주기 값을 안전 범위로 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        _resolveRetryInterval = Mathf.Max(0.05f, _resolveRetryInterval);
    }

    /// <summary>
    /// 현재 대상이 유효하면 반환하고, 필요 시 지정 주기에 맞춰 재탐색합니다.
    /// </summary>
    public bool TryGetTargetTransform(out Transform targetTransform)
    {
        if (!IsTargetStillValid())
        {
            TryResolveTargetByInterval();
        }

        targetTransform = _resolvedTargetTransform;
        return targetTransform != null;
    }

    /// <summary>
    /// 현재 로컬 플레이어 캐릭터 기준 Transform을 반환하고, 필요 시 재탐색합니다.
    /// </summary>
    public bool TryGetLocalPlayerTransform(out Transform localPlayerTransform)
    {
        if (_resolvedLocalPlayerTransform == null)
        {
            TryResolveTargetByInterval();
        }

        localPlayerTransform = _resolvedLocalPlayerTransform;
        return localPlayerTransform != null;
    }

    /// <summary>
    /// 즉시 대상 재탐색을 수행합니다.
    /// </summary>
    public void ForceResolveTarget()
    {
        _nextResolveTime = Time.time + Mathf.Max(0.05f, _resolveRetryInterval);
        SetResolvedTarget(ResolveTargetInternal());
    }

    /// <summary>
    /// NetworkManager 참조를 보정합니다.
    /// </summary>
    private void ResolveDependencies()
    {
        if (_networkManager == null)
        {
            _networkManager = NetworkManager.Singleton;
        }
    }

    /// <summary>
    /// 씬 전환과 네트워크 연결 변경 콜백을 등록합니다.
    /// </summary>
    private void RegisterHooks()
    {
        if (!_isSceneLoadedHookRegistered)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _isSceneLoadedHookRegistered = true;
        }

        if (!TryResolveNetworkManager(out NetworkManager networkManager) || _networkCallbacksRegistered)
        {
            return;
        }

        networkManager.OnClientConnectedCallback += HandleClientConnectionChanged;
        networkManager.OnClientDisconnectCallback += HandleClientConnectionChanged;
        _networkCallbacksRegistered = true;
    }

    /// <summary>
    /// 등록된 씬 전환과 네트워크 연결 변경 콜백을 해제합니다.
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

        networkManager.OnClientConnectedCallback -= HandleClientConnectionChanged;
        networkManager.OnClientDisconnectCallback -= HandleClientConnectionChanged;
        _networkCallbacksRegistered = false;
    }

    /// <summary>
    /// 씬 전환 직후 플레이어 참조를 다시 탐색합니다.
    /// </summary>
    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        ResolveDependencies();
        ForceResolveTarget();
    }

    /// <summary>
    /// 클라이언트 연결 상태 변경 시 플레이어 참조를 다시 탐색합니다.
    /// </summary>
    private void HandleClientConnectionChanged(ulong _)
    {
        ResolveDependencies();
        ForceResolveTarget();
    }

    /// <summary>
    /// 재탐색 주기에 도달했을 때만 대상 탐색을 수행합니다.
    /// </summary>
    private void TryResolveTargetByInterval()
    {
        if (Time.time < _nextResolveTime)
        {
            return;
        }

        ForceResolveTarget();
    }

    /// <summary>
    /// 현재 저장된 대상 NetworkObject와 Transform이 계속 유효한지 확인합니다.
    /// </summary>
    private bool IsTargetStillValid()
    {
        if (_resolvedTargetTransform == null || _resolvedTargetNetworkObject == null)
        {
            return false;
        }

        return _resolvedTargetNetworkObject.IsSpawned && _resolvedTargetNetworkObject.OwnerClientId != ResolveLocalClientId();
    }

    /// <summary>
    /// NGO 상태를 기준으로 로컬 플레이어가 아닌 첫 번째 원격 플레이어 Transform을 찾습니다.
    /// </summary>
    private Transform ResolveTargetInternal()
    {
        _resolvedLocalPlayerObject = null;
        _resolvedLocalPlayerTransform = null;
        _resolvedTargetNetworkObject = null;

        if (!TryResolveNetworkManager(out NetworkManager networkManager) || !networkManager.IsListening)
        {
            _hasLoggedMissingTargetWarning = false;
            return null;
        }

        _resolvedLocalPlayerObject = networkManager.LocalClient != null ? networkManager.LocalClient.PlayerObject : null;
        ulong localClientId = networkManager.LocalClientId; // 자기 자신 제외 판정에 사용할 로컬 ClientId입니다.
        if (TryResolveTargetFromNetworkObject(_resolvedLocalPlayerObject, out Transform localPlayerTransform))
        {
            _resolvedLocalPlayerTransform = localPlayerTransform;
        }
        else if (_allowNetworkObjectFallbackScan)
        {
            TryResolveLocalPlayerByNetworkObjectScan(networkManager, out _resolvedLocalPlayerTransform);
        }

        for (int index = 0; index < networkManager.ConnectedClientsList.Count; index++)
        {
            NetworkClient client = networkManager.ConnectedClientsList[index]; // NGO가 보유한 연결 클라이언트 정보입니다.
            if (client == null || client.ClientId == localClientId)
            {
                continue;
            }

            if (TryResolveTargetFromNetworkObject(client.PlayerObject, out Transform targetTransform))
            {
                _resolvedTargetNetworkObject = client.PlayerObject;
                _hasLoggedMissingTargetWarning = false;
                return targetTransform;
            }
        }

        if (_allowNetworkObjectFallbackScan && TryResolveTargetByNetworkObjectScan(networkManager, out Transform fallbackTarget))
        {
            if (_warnOnFallbackPath && !_hasLoggedNetworkObjectFallbackWarning)
            {
                Debug.LogWarning($"[OffscreenPlayerTargetResolver] PlayerObject가 준비되지 않아 NetworkObject 스캔 폴백을 사용합니다. object={name}, localClientId={localClientId}", this);
                _hasLoggedNetworkObjectFallbackWarning = true;
            }

            _hasLoggedMissingTargetWarning = false;
            return fallbackTarget;
        }

        if (networkManager.ConnectedClientsList.Count > 1 && !_hasLoggedMissingTargetWarning)
        {
            Debug.LogWarning($"[OffscreenPlayerTargetResolver] 원격 플레이어 Transform을 찾지 못했습니다. 재탐색을 대기합니다. object={name}", this);
            _hasLoggedMissingTargetWarning = true;
        }

        return null;
    }

    /// <summary>
    /// NetworkObject 루트, 자식, 부모에서 플레이어 기준 Transform을 해석합니다.
    /// </summary>
    private bool TryResolveTargetFromNetworkObject(NetworkObject networkObject, out Transform targetTransform)
    {
        targetTransform = null;
        if (networkObject == null || !networkObject.IsSpawned)
        {
            return false;
        }

        PlayerInputDriver inputDriver = networkObject.GetComponent<PlayerInputDriver>();
        if (inputDriver == null)
        {
            inputDriver = networkObject.GetComponentInChildren<PlayerInputDriver>(true);
        }

        if (inputDriver != null)
        {
            targetTransform = inputDriver.transform;
            return true;
        }

        PlayerMovement playerMovement = networkObject.GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            playerMovement = networkObject.GetComponentInChildren<PlayerMovement>(true);
        }

        if (playerMovement != null)
        {
            targetTransform = playerMovement.transform;
            return true;
        }

        targetTransform = networkObject.transform;
        return true;
    }

    /// <summary>
    /// Spawn된 NetworkObject 전체에서 로컬 소유자가 아닌 플레이어 오브젝트를 탐색합니다.
    /// </summary>
    private bool TryResolveTargetByNetworkObjectScan(NetworkManager networkManager, out Transform targetTransform)
    {
        targetTransform = null;
        if (networkManager == null || networkManager.SpawnManager == null)
        {
            return false;
        }

        ulong localClientId = networkManager.LocalClientId; // 자기 자신 제외 판정에 사용할 로컬 ClientId입니다.
        foreach (NetworkObject candidate in networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (candidate == null || !candidate.IsSpawned || candidate.OwnerClientId == localClientId)
            {
                continue;
            }

            if (candidate.GetComponent<PlayerNetworkRoot>() == null && candidate.GetComponentInChildren<PlayerInputDriver>(true) == null)
            {
                continue;
            }

            if (TryResolveTargetFromNetworkObject(candidate, out targetTransform))
            {
                _resolvedTargetNetworkObject = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Spawn된 NetworkObject 전체에서 로컬 소유자의 플레이어 Transform을 탐색합니다.
    /// </summary>
    private bool TryResolveLocalPlayerByNetworkObjectScan(NetworkManager networkManager, out Transform localPlayerTransform)
    {
        localPlayerTransform = null;
        if (networkManager == null || networkManager.SpawnManager == null)
        {
            return false;
        }

        ulong localClientId = networkManager.LocalClientId; // 로컬 플레이어를 식별할 ClientId입니다.
        foreach (NetworkObject candidate in networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (candidate == null || !candidate.IsSpawned || candidate.OwnerClientId != localClientId)
            {
                continue;
            }

            if (candidate.GetComponent<PlayerNetworkRoot>() == null && candidate.GetComponentInChildren<PlayerInputDriver>(true) == null)
            {
                continue;
            }

            if (TryResolveTargetFromNetworkObject(candidate, out localPlayerTransform))
            {
                _resolvedLocalPlayerObject = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 현재 로컬 ClientId를 반환합니다.
    /// </summary>
    private ulong ResolveLocalClientId()
    {
        return TryResolveNetworkManager(out NetworkManager networkManager) ? networkManager.LocalClientId : ulong.MaxValue;
    }

    /// <summary>
    /// NetworkManager 참조를 보정하고 유효 여부를 반환합니다.
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

    /// <summary>
    /// 대상 Transform 변경을 저장하고 구독자에게 알립니다.
    /// </summary>
    private void SetResolvedTarget(Transform targetTransform)
    {
        if (_resolvedTargetTransform == targetTransform)
        {
            return;
        }

        _resolvedTargetTransform = targetTransform;
        TargetChanged?.Invoke(_resolvedTargetTransform);
    }
}
