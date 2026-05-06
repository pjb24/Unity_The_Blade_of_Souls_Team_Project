using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 요청을 단일 진입점으로 관리하고, 싱글플레이 직접 로드와 NGO 네트워크 씬 로드를 공통 이벤트로 변환하는 서비스입니다.
/// </summary>
public class SceneTransitionService : MonoBehaviour
{
    private static SceneTransitionService _instance; // 전역 접근을 위한 씬 전환 서비스 싱글톤 인스턴스입니다.

    [Header("Lifecycle")]
    [Tooltip("씬 전환 후에도 SceneTransitionService를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // SceneTransitionService 생명주기를 씬 전환과 분리할지 여부입니다.

    [Header("Transition")]
    [Tooltip("씬 로드 전에 대기할 페이드 아웃 시간(초)입니다.")]
    [SerializeField] private float _fadeOutDuration = 0.2f; // 로드 직전 연출을 위한 페이드 아웃 대기 시간입니다.

    [Tooltip("씬 로드 후 대기할 페이드 인 시간(초)입니다.")]
    [SerializeField] private float _fadeInDuration = 0.2f; // 로드 직후 연출을 위한 페이드 인 대기 시간입니다.

    [Tooltip("씬 전환 중 입력을 차단할지 여부입니다.")]
    [SerializeField] private bool _blockInputWhileTransition = true; // 전환 중 PlayerInput 활성 상태를 제어할지 여부입니다.

    [Tooltip("NGO NetworkManager가 활성인 경우 Host/Server에서 NetworkSceneManager.LoadScene을 우선 사용할지 여부입니다.")]
    [SerializeField] private bool _preferNetworkSceneManagement = true; // 멀티플레이 세션에서 NGO 네트워크 씬 전환을 우선 적용할지 여부입니다.

    private bool _isTransitioning; // 현재 씬 전환이 진행 중인지 여부입니다.
    private bool _isNetworkTransitionInProgress; // 현재 씬 전환이 NGO NetworkSceneManager 경로로 진행 중인지 여부입니다.
    private string _pendingNetworkSceneName; // NGO 씬 전환 완료 콜백과 매칭할 씬 이름입니다.
    private NetworkManager _boundNetworkManager; // NGO 씬 이벤트를 구독 중인 NetworkManager 참조입니다.

    /// <summary>
    /// 씬 로드 직전에 호출되는 이벤트입니다.
    /// </summary>
    public event Action<string> OnBeforeSceneLoad;

    /// <summary>
    /// 씬 로드 직후에 호출되는 이벤트입니다.
    /// </summary>
    public event Action<string> OnAfterSceneLoad;

    /// <summary>
    /// 전역 SceneTransitionService 인스턴스를 반환합니다.
    /// </summary>
    public static SceneTransitionService Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<SceneTransitionService>();
                if (_instance == null)
                {
                    GameObject serviceObject = new GameObject("--- Scene Transition Service ---"); // 자동 생성되는 씬 전환 서비스 오브젝트입니다.
                    _instance = serviceObject.AddComponent<SceneTransitionService>();
                }
            }

            return _instance;
        }
    }

    /// <summary>
    /// 새 인스턴스를 생성하지 않고 현재 존재하는 SceneTransitionService를 반환합니다.
    /// </summary>
    public static bool TryGetExistingInstance(out SceneTransitionService service)
    {
        service = _instance != null ? _instance : FindAnyObjectByType<SceneTransitionService>();
        return service != null;
    }

    /// <summary>
    /// 싱글톤 중복을 방지하고 필요 시 DDOL을 적용합니다.
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[SceneTransitionService] 중복 SceneTransitionService가 감지되어 새 인스턴스를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    /// <summary>
    /// Unity 씬 로드와 NGO 씬 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleUnitySceneLoaded;
        TryBindNetworkSceneEvents();
    }

    /// <summary>
    /// Unity 씬 로드와 NGO 씬 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleUnitySceneLoaded;
        UnbindNetworkSceneEvents();
    }

    /// <summary>
    /// NetworkManager가 늦게 초기화되는 경우를 보정해 NGO 씬 이벤트 구독을 시도합니다.
    /// </summary>
    private void Update()
    {
        if (_boundNetworkManager == null)
        {
            TryBindNetworkSceneEvents();
        }
    }

    /// <summary>
    /// 씬 전환 진행 중인지 여부를 반환합니다.
    /// </summary>
    public bool IsTransitioning()
    {
        return _isTransitioning;
    }

    /// <summary>
    /// 지정 씬으로 비동기 전환을 시작합니다.
    /// </summary>
    public bool TryLoadScene(string sceneName)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning($"[SceneTransitionService] 이미 씬 전환 중입니다. scene={sceneName}", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneTransitionService] sceneName이 비어 있어 전환을 건너뜁니다.", this);
            return false;
        }

        if (TryLoadSceneWithNgoNetworkManager(sceneName))
        {
            return true;
        }

        StartCoroutine(LoadSceneRoutine(sceneName));
        return true;
    }

    /// <summary>
    /// StageDefinition 기반으로 스테이지 씬 전환을 시작합니다.
    /// </summary>
    public bool TryLoadStage(StageDefinition stageDefinition)
    {
        if (stageDefinition == null)
        {
            Debug.LogWarning("[SceneTransitionService] stageDefinition이 null이라 TryLoadStage를 건너뜁니다.", this);
            return false;
        }

        StageSession.Instance.SetNextStage(stageDefinition);
        return TryLoadScene(stageDefinition.SceneName);
    }

    /// <summary>
    /// 페이드 대기, 직접 씬 로드, 콜백 호출, 입력 복구를 순서대로 처리합니다.
    /// </summary>
    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        _isTransitioning = true;
        _isNetworkTransitionInProgress = false;
        _pendingNetworkSceneName = string.Empty;

        ToggleInput(false);
        OnBeforeSceneLoad?.Invoke(sceneName);

        float safeFadeOut = Mathf.Max(0f, _fadeOutDuration); // 음수 방지를 적용한 페이드 아웃 시간입니다.
        if (safeFadeOut > 0f)
        {
            yield return new WaitForSecondsRealtime(safeFadeOut);
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); // 실제 비동기 씬 로드 작업입니다.
        if (operation == null)
        {
            Debug.LogWarning($"[SceneTransitionService] LoadSceneAsync가 null을 반환했습니다. scene={sceneName}", this);
            _isTransitioning = false;
            ToggleInput(true);
            yield break;
        }

        while (!operation.isDone)
        {
            yield return null;
        }

        OnAfterSceneLoad?.Invoke(sceneName);

        float safeFadeIn = Mathf.Max(0f, _fadeInDuration); // 음수 방지를 적용한 페이드 인 시간입니다.
        if (safeFadeIn > 0f)
        {
            yield return new WaitForSecondsRealtime(safeFadeIn);
        }

        ToggleInput(true);
        _isTransitioning = false;
    }

    /// <summary>
    /// 멀티플레이 세션에서는 Host/Server 권한으로 NGO 네트워크 씬 전환을 시작합니다.
    /// </summary>
    private bool TryLoadSceneWithNgoNetworkManager(string sceneName)
    {
        if (!_preferNetworkSceneManagement)
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // NGO 네트워크 씬 전환 가능 여부를 판정할 NetworkManager 참조입니다.
        if (networkManager == null || !networkManager.IsListening)
        {
            return false;
        }

        if (!networkManager.NetworkConfig.EnableSceneManagement)
        {
            Debug.LogError($"[SceneTransitionService] NetworkConfig.EnableSceneManagement가 꺼져 있어 네트워크 씬 전환을 시작할 수 없습니다. scene={sceneName}", this);
            return false;
        }

        if (!networkManager.IsServer)
        {
            Debug.LogWarning($"[SceneTransitionService] Client는 게임 진행용 씬 전환을 직접 시작할 수 없습니다. host 결정을 대기합니다. scene={sceneName}", this);
            return false;
        }

        if (networkManager.SceneManager == null)
        {
            Debug.LogError($"[SceneTransitionService] NetworkSceneManager가 null이라 네트워크 씬 전환을 시작할 수 없습니다. scene={sceneName}", this);
            return false;
        }

        SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single); // Host/Server 권한으로 모든 클라이언트 씬 전환을 시작하는 NGO API 호출 결과입니다.
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError($"[SceneTransitionService] NetworkSceneManager.LoadScene 시작 실패. scene={sceneName}, status={status}", this);
            return false;
        }

        _isTransitioning = true;
        _isNetworkTransitionInProgress = true;
        _pendingNetworkSceneName = sceneName;
        ToggleInput(false);
        OnBeforeSceneLoad?.Invoke(sceneName);
        return true;
    }

    /// <summary>
    /// Host가 시작한 NGO 씬 전환 완료를 Host 로컬 전환 완료 이벤트로 반영합니다.
    /// </summary>
    private void HandleUnitySceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
    {
        if (!_isNetworkTransitionInProgress)
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // Host/Client 중복 처리를 방지하기 위한 NetworkManager 참조입니다.
        if (networkManager != null && networkManager.IsListening && !networkManager.IsServer)
        {
            return;
        }

        string loadedSceneName = loadedScene.name; // 네트워크 씬 전환 완료 매칭에 사용할 실제 로드 씬 이름입니다.
        if (!string.Equals(loadedSceneName, _pendingNetworkSceneName, StringComparison.Ordinal))
        {
            Debug.LogWarning($"[SceneTransitionService] NGO 씬 전환 완료 콜백 씬 이름이 예상과 다릅니다. expected={_pendingNetworkSceneName}, actual={loadedSceneName}", this);
        }

        CompleteNetworkSceneLoad(loadedSceneName);
    }

    /// <summary>
    /// NGO NetworkSceneManager 이벤트를 구독합니다.
    /// </summary>
    private void TryBindNetworkSceneEvents()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // NGO 씬 이벤트를 제공할 NetworkManager 참조입니다.
        if (networkManager == null || networkManager.SceneManager == null || _boundNetworkManager == networkManager)
        {
            return;
        }

        UnbindNetworkSceneEvents();
        _boundNetworkManager = networkManager;
        _boundNetworkManager.SceneManager.OnSceneEvent += HandleNetworkSceneEvent;
    }

    /// <summary>
    /// NGO NetworkSceneManager 이벤트 구독을 해제합니다.
    /// </summary>
    private void UnbindNetworkSceneEvents()
    {
        if (_boundNetworkManager == null || _boundNetworkManager.SceneManager == null)
        {
            _boundNetworkManager = null;
            return;
        }

        _boundNetworkManager.SceneManager.OnSceneEvent -= HandleNetworkSceneEvent;
        _boundNetworkManager = null;
    }

    /// <summary>
    /// Client가 NGO 씬 전환을 수신했을 때 로컬 SceneTransitionService 이벤트로 변환합니다.
    /// </summary>
    private void HandleNetworkSceneEvent(SceneEvent sceneEvent)
    {
        NetworkManager networkManager = _boundNetworkManager != null ? _boundNetworkManager : NetworkManager.Singleton; // 이벤트 필터링에 사용할 현재 NetworkManager입니다.
        if (networkManager == null || networkManager.IsServer)
        {
            return;
        }

        if (sceneEvent.SceneEventType == SceneEventType.Load)
        {
            BeginReceivedNetworkSceneLoad(sceneEvent.SceneName);
            return;
        }

        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete && sceneEvent.ClientId == networkManager.LocalClientId)
        {
            CompleteReceivedNetworkSceneLoad(sceneEvent.SceneName);
            return;
        }

        if (sceneEvent.SceneEventType == SceneEventType.SynchronizeComplete && sceneEvent.ClientId == networkManager.LocalClientId)
        {
            CompleteReceivedNetworkSceneLoad(sceneEvent.SceneName);
        }
    }

    /// <summary>
    /// Host가 시작한 NGO 씬 로드를 Client 로컬 전환 시작 이벤트로 반영합니다.
    /// </summary>
    private void BeginReceivedNetworkSceneLoad(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        _isTransitioning = true;
        _isNetworkTransitionInProgress = true;
        _pendingNetworkSceneName = sceneName;
        ToggleInput(false);
        OnBeforeSceneLoad?.Invoke(sceneName);
    }

    /// <summary>
    /// Host가 시작한 NGO 씬 로드 완료를 Client 로컬 전환 완료 이벤트로 반영합니다.
    /// </summary>
    private void CompleteReceivedNetworkSceneLoad(string sceneName)
    {
        string completedSceneName = string.IsNullOrWhiteSpace(sceneName) ? _pendingNetworkSceneName : sceneName; // SynchronizeComplete에서 SceneName이 비어 있는 경우 기존 수신 씬 이름을 사용합니다.
        if (string.IsNullOrWhiteSpace(completedSceneName))
        {
            completedSceneName = SceneManager.GetActiveScene().name;
        }

        if (!_isNetworkTransitionInProgress)
        {
            BeginReceivedNetworkSceneLoad(completedSceneName);
        }

        CompleteNetworkSceneLoad(completedSceneName);
    }

    /// <summary>
    /// NGO 씬 전환 완료 이벤트를 공통 후처리합니다.
    /// </summary>
    private void CompleteNetworkSceneLoad(string sceneName)
    {
        OnAfterSceneLoad?.Invoke(sceneName);
        ToggleInput(true);
        _isTransitioning = false;
        _isNetworkTransitionInProgress = false;
        _pendingNetworkSceneName = string.Empty;
    }

    /// <summary>
    /// InputManager의 PlayerInput 활성/비활성을 처리합니다.
    /// </summary>
    private void ToggleInput(bool isEnabled)
    {
        if (_blockInputWhileTransition == false)
        {
            return;
        }

        if (InputManager.PlayerInput == null)
        {
            return;
        }

        InputManager.PlayerInput.enabled = isEnabled;
    }
}
