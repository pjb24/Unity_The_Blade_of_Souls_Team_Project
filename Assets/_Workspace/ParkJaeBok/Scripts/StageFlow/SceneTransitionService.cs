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
    [Tooltip("씬을 나가기 직전에 재생할 FadeOut CameraEffectPreset입니다. 비어 있으면 기존 시간 대기만 사용합니다.")]
    [SerializeField] private CameraEffectPresetBase _sceneExitFadeOutPreset; // 씬 로드 직전에 재생할 카메라 FadeOut 프리셋입니다.

    [Tooltip("새 씬에 진입한 직후 재생할 FadeIn CameraEffectPreset입니다. 비어 있으면 기존 시간 대기만 사용합니다.")]
    [SerializeField] private CameraEffectPresetBase _sceneEnterFadeInPreset; // 씬 로드 직후 재생할 카메라 FadeIn 프리셋입니다.

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

    [Header("Scene Entry Cover")]
    [Tooltip("새 씬의 첫 화면을 FadeIn 시작 전까지 덮을 색상입니다.")]
    [SerializeField] private Color _sceneEntryCoverColor = Color.black; // 씬 진입 첫 프레임을 보장하기 위한 전환용 검은 화면 색상입니다.

    [Tooltip("씬 진입 덮개가 다른 CameraEffect 오버레이보다 우선 보이도록 사용할 우선순위입니다.")]
    [SerializeField] private int _sceneEntryCoverPriority = int.MaxValue - 1; // FadeIn 프리셋이 시작되기 전까지 유지할 전환용 오버레이 우선순위입니다.

    [Tooltip("FadeIn 프리셋이 비어 있을 때 전환용 검은 덮개를 자동으로 제거할지 여부입니다.")]
    [SerializeField] private bool _clearSceneEntryCoverWhenFadeInMissing = true; // FadeIn 프리셋 미지정 시 검은 화면이 영구 유지되지 않도록 정리할지 여부입니다.

    [Tooltip("멀티플레이 씬 진입 후 CameraEffectManager와 Main Camera가 준비될 때까지 FadeIn 재생을 기다릴 최대 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _networkFadeInReadyTimeoutSeconds = 3f; // NGO 씬 완료 이벤트가 카메라 준비보다 먼저 오는 상황을 보정하는 최대 대기 시간입니다.

    private bool _isTransitioning; // 현재 씬 전환이 진행 중인지 여부입니다.
    private bool _isNetworkTransitionInProgress; // 현재 씬 전환이 NGO NetworkSceneManager 경로로 진행 중인지 여부입니다.
    private string _pendingNetworkSceneName; // NGO 씬 전환 완료 콜백과 매칭할 씬 이름입니다.
    private NetworkManager _boundNetworkManager; // NGO 씬 이벤트를 구독 중인 NetworkManager 참조입니다.
    private int _sceneEntryCoverSourceId; // 전환용 검은 화면 오버레이를 등록하고 해제하는 데 사용할 고유 식별자입니다.
    private Coroutine _sceneEntryFadeInRoutine; // 네트워크 씬 완료 후 FadeIn 재생과 덮개 정리를 순차 처리하는 코루틴입니다.
    private bool _hasCompletedNetworkSceneLoad; // NGO LoadComplete/SynchronizeComplete 중복 이벤트에서 후처리를 한 번만 수행하기 위한 플래그입니다.
    private string _completedNetworkSceneName; // 이미 후처리한 NGO 씬 이름입니다.

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
        _sceneEntryCoverSourceId = GetInstanceID();

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
        StopSceneEntryFadeInRoutine();
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

        yield return PlaySceneExitFadeOut();

        EnsureSceneEntryCover(false);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); // 실제 비동기 씬 로드 작업입니다.
        if (operation == null)
        {
            Debug.LogWarning($"[SceneTransitionService] LoadSceneAsync가 null을 반환했습니다. scene={sceneName}", this);
            ClearSceneEntryCover();
            _isTransitioning = false;
            ToggleInput(true);
            yield break;
        }

        while (!operation.isDone)
        {
            yield return null;
        }

        OnAfterSceneLoad?.Invoke(sceneName);

        yield return PlaySceneEnterFadeIn();
        ClearSceneEntryCover();

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

        StartCoroutine(LoadNetworkSceneRoutine(sceneName, networkManager));
        return true;
    }

    /// <summary>
    /// Host/Server 권한에서 FadeOut을 완료한 뒤 NGO NetworkSceneManager로 씬 전환을 시작합니다.
    /// </summary>
    private IEnumerator LoadNetworkSceneRoutine(string sceneName, NetworkManager networkManager)
    {
        _isTransitioning = true;
        _isNetworkTransitionInProgress = true;
        _pendingNetworkSceneName = sceneName;
        _hasCompletedNetworkSceneLoad = false;
        _completedNetworkSceneName = string.Empty;
        ToggleInput(false);
        OnBeforeSceneLoad?.Invoke(sceneName);

        yield return PlaySceneExitFadeOut();
        EnsureSceneEntryCover(false);

        if (networkManager == null || networkManager.SceneManager == null)
        {
            Debug.LogError($"[SceneTransitionService] NetworkSceneManager가 사라져 네트워크 씬 전환을 중단합니다. scene={sceneName}", this);
            ClearSceneEntryCover();
            ToggleInput(true);
            _isTransitioning = false;
            _isNetworkTransitionInProgress = false;
            _pendingNetworkSceneName = string.Empty;
            yield break;
        }

        SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single); // Host/Server 권한으로 모든 클라이언트 씬 전환을 시작하는 NGO API 호출 결과입니다.
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError($"[SceneTransitionService] NetworkSceneManager.LoadScene 시작 실패. scene={sceneName}, status={status}", this);
            ClearSceneEntryCover();
            ToggleInput(true);
            _isTransitioning = false;
            _isNetworkTransitionInProgress = false;
            _pendingNetworkSceneName = string.Empty;
            yield break;
        }
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

        if (_isNetworkTransitionInProgress && string.Equals(_pendingNetworkSceneName, sceneName, StringComparison.Ordinal))
        {
            return;
        }

        _isTransitioning = true;
        _isNetworkTransitionInProgress = true;
        _pendingNetworkSceneName = sceneName;
        _hasCompletedNetworkSceneLoad = false;
        _completedNetworkSceneName = string.Empty;
        ToggleInput(false);
        EnsureSceneEntryCover(true);
        CameraEffectPlaybackUtility.Play(_sceneExitFadeOutPreset, gameObject);
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

        if (IsDuplicateNetworkSceneCompletion(completedSceneName))
        {
            return;
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
        if (IsDuplicateNetworkSceneCompletion(sceneName))
        {
            return;
        }

        _hasCompletedNetworkSceneLoad = true;
        _completedNetworkSceneName = sceneName;

        OnAfterSceneLoad?.Invoke(sceneName);
        PlaySceneEnterFadeInAndReleaseCover();
        ToggleInput(true);
        _isTransitioning = false;
        _isNetworkTransitionInProgress = false;
        _pendingNetworkSceneName = string.Empty;
    }

    /// <summary>
    /// NGO LoadComplete와 SynchronizeComplete가 같은 씬에 대해 중복 도착했는지 판별합니다.
    /// </summary>
    private bool IsDuplicateNetworkSceneCompletion(string sceneName)
    {
        if (!_hasCompletedNetworkSceneLoad)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return true;
        }

        return string.Equals(_completedNetworkSceneName, sceneName, StringComparison.Ordinal);
    }

    /// <summary>
    /// 씬을 나가기 직전에 설정된 FadeOut 프리셋을 재생하고 완료 시간만큼 대기합니다.
    /// </summary>
    private IEnumerator PlaySceneExitFadeOut()
    {
        yield return CameraEffectPlaybackUtility.PlayAndWait(_sceneExitFadeOutPreset, gameObject, _fadeOutDuration);
    }

    /// <summary>
    /// 새 씬 진입 직후 설정된 FadeIn 프리셋을 재생하고 완료 시간만큼 대기합니다.
    /// </summary>
    private IEnumerator PlaySceneEnterFadeIn()
    {
        yield return CameraEffectPlaybackUtility.PlayAndWait(_sceneEnterFadeInPreset, gameObject, _fadeInDuration);
    }

    /// <summary>
    /// 새 씬 첫 화면이 보이기 전에 전환용 검은 화면 오버레이를 등록하고 유지합니다.
    /// </summary>
    private void EnsureSceneEntryCover(bool protectFromEffectCleanup)
    {
        Color coverColor = _sceneEntryCoverColor; // Inspector에서 지정한 씬 진입 덮개 색상입니다.
        coverColor.a = Mathf.Clamp01(coverColor.a);

        if (protectFromEffectCleanup)
        {
            CameraEffectScreenOverlay.SetProtectedOverlay(_sceneEntryCoverSourceId, _sceneEntryCoverPriority, coverColor);
        }
        else
        {
            CameraEffectScreenOverlay.SetOverlay(_sceneEntryCoverSourceId, _sceneEntryCoverPriority, coverColor);
        }

        CameraEffectScreenOverlay.RetainOverlay(_sceneEntryCoverSourceId);
    }

    /// <summary>
    /// 전환용 검은 화면 오버레이를 즉시 제거합니다.
    /// </summary>
    private void ClearSceneEntryCover()
    {
        CameraEffectScreenOverlay.ClearOverlay(_sceneEntryCoverSourceId);
    }

    /// <summary>
    /// 네트워크 씬 완료 콜백에서 FadeIn 재생과 전환용 덮개 해제를 비동기로 처리합니다.
    /// </summary>
    private void PlaySceneEnterFadeInAndReleaseCover()
    {
        StopSceneEntryFadeInRoutine();
        _sceneEntryFadeInRoutine = StartCoroutine(PlaySceneEnterFadeInAndReleaseCoverRoutine());
    }

    /// <summary>
    /// FadeIn 프리셋을 재생한 뒤 전환용 덮개가 남지 않도록 정리합니다.
    /// </summary>
    private IEnumerator PlaySceneEnterFadeInAndReleaseCoverRoutine()
    {
        if (_sceneEnterFadeInPreset == null)
        {
            if (_clearSceneEntryCoverWhenFadeInMissing)
            {
                ClearSceneEntryCover();
            }

            _sceneEntryFadeInRoutine = null;
            yield break;
        }

        EnsureSceneEntryCover(true);

        CameraEffectHandle fadeInHandle = default; // FadeIn이 실제로 CameraEffectManager에 등록되었는지 확인하기 위한 핸들입니다.
        float waitStartedAt = Time.unscaledTime; // 카메라/매니저 준비 대기 시간 측정을 위한 시작 시각입니다.
        while (!TryPlaySceneEnterFadeIn(out fadeInHandle))
        {
            if (_networkFadeInReadyTimeoutSeconds <= 0f ||
                Time.unscaledTime - waitStartedAt >= _networkFadeInReadyTimeoutSeconds)
            {
                Debug.LogWarning($"[SceneTransitionService] 멀티플레이 씬 진입 FadeIn 재생 대기 시간이 초과되어 전환용 검은 덮개를 해제합니다. scene={SceneManager.GetActiveScene().name}, timeout={_networkFadeInReadyTimeoutSeconds:F2}", this);
                break;
            }

            yield return null;
        }

        ClearSceneEntryCover();

        float waitSeconds = CameraEffectPlaybackUtility.ResolveWaitSeconds(_sceneEnterFadeInPreset, _fadeInDuration); // FadeIn 재생 완료까지 유지할 대기 시간입니다.
        if (waitSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(waitSeconds);
        }

        _sceneEntryFadeInRoutine = null;
    }

    /// <summary>
    /// CameraEffectManager와 대상 카메라가 준비된 경우에만 씬 진입 FadeIn 재생을 시작합니다.
    /// </summary>
    private bool TryPlaySceneEnterFadeIn(out CameraEffectHandle fadeInHandle)
    {
        fadeInHandle = default;

        if (CameraEffectManager.Instance == null)
        {
            return false;
        }

        fadeInHandle = CameraEffectPlaybackUtility.Play(_sceneEnterFadeInPreset, gameObject);
        return fadeInHandle.IsValid;
    }

    /// <summary>
    /// 진행 중인 씬 진입 FadeIn 코루틴을 중지합니다.
    /// </summary>
    private void StopSceneEntryFadeInRoutine()
    {
        if (_sceneEntryFadeInRoutine == null)
        {
            return;
        }

        StopCoroutine(_sceneEntryFadeInRoutine);
        _sceneEntryFadeInRoutine = null;
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
