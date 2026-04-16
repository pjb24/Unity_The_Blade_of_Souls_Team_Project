using System;
using UnityEngine;

/// <summary>
/// 싱글플레이 인게임(Town/Stage) 전용 Pause 상태를 공통으로 제어하는 서비스입니다.
/// </summary>
public class SinglePlayerPauseService : MonoBehaviour, IPauseStateReader
{
    [Header("Lifecycle")]
    [Tooltip("씬 전환 후에도 Pause 서비스를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // Pause 서비스를 DDOL로 유지할지 여부입니다.

    [Header("Pause Policy")]
    [Tooltip("Pause 진입 시 Time.timeScale을 0으로 제어할지 여부입니다.")]
    [SerializeField] private bool _useTimeScalePause = true; // Pause 상태에서 timeScale 제어를 사용할지 여부입니다.

    [Tooltip("Pause 진입 시 InputManager.PlayerInput을 비활성화할지 여부입니다.")]
    [SerializeField] private bool _disablePlayerInputWhilePaused = true; // Pause 상태에서 플레이 입력 차단을 위해 PlayerInput을 비활성화할지 여부입니다.

    [Tooltip("Pause/Resume 상태 변경 로그를 상세 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLog = true; // Pause 상태 변화 로그 출력 여부입니다.

    [Header("Dependencies")]
    [Tooltip("플레이 모드/상태 판정을 위한 GameFlowController 참조입니다. 비어 있으면 런타임에서 해석합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // Pause 가능 상태를 판정할 GameFlowController 참조입니다.

    [Tooltip("씬 전환 중 Pause 진입 차단을 위한 SceneTransitionService 참조입니다. 비어 있으면 런타임에서 해석합니다.")]
    [SerializeField] private SceneTransitionService _sceneTransitionService; // 씬 전환 상태를 조회할 서비스 참조입니다.

    private float _resumeTimeScale = 1f; // Pause 해제 시 복구할 timeScale 값입니다.
    private bool _wasPlayerInputEnabledBeforePause = true; // Pause 전 PlayerInput 활성 상태를 저장하는 값입니다.

    /// <summary>
    /// 싱글톤 접근용 Pause 서비스 인스턴스입니다.
    /// </summary>
    public static SinglePlayerPauseService Instance { get; private set; }

    /// <summary>
    /// 현재 Pause 상태 여부를 반환합니다.
    /// </summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// Pause 상태 변경 시점에 구독자에게 전달할 이벤트입니다.
    /// </summary>
    public event Action<bool> PauseStateChanged;

    /// <summary>
    /// 싱글톤 중복 생성을 방지하고 의존성 해석을 수행합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SinglePlayerPauseService] 중복 인스턴스를 감지해 새 인스턴스를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        ResolveDependencies();
    }

    /// <summary>
    /// 서비스 파괴 시 정적 인스턴스를 정리하고 Pause 상태를 안전하게 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            ForceResume("ServiceDestroyed");
            Instance = null;
        }
    }

    /// <summary>
    /// 현재 문맥에서 Pause 진입 가능 여부를 반환합니다.
    /// </summary>
    public bool CanPauseNow()
    {
        ResolveDependencies();

        if (_gameFlowController == null)
        {
            Debug.LogWarning("[SinglePlayerPauseService] GameFlowController를 찾지 못해 Pause 가능 여부를 false로 처리합니다.", this);
            return false;
        }

        if (_gameFlowController.CurrentPlayMode != E_GamePlayMode.SinglePlayer)
        {
            return false;
        }

        GameFlowState currentState = _gameFlowController.CurrentState; // Pause 가능 상태(Town/StagePlaying) 판정에 사용할 현재 GameFlow 상태입니다.
        bool isGameplayState = currentState == GameFlowState.Town || currentState == GameFlowState.StagePlaying;
        if (!isGameplayState)
        {
            return false;
        }

        if (_sceneTransitionService != null && _sceneTransitionService.IsTransitioning())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 싱글플레이 인게임 문맥에서 Pause 상태 진입을 시도합니다.
    /// </summary>
    public bool TryEnterPause(string reason)
    {
        if (IsPaused)
        {
            return true;
        }

        if (CanPauseNow() == false)
        {
            Debug.LogWarning($"[SinglePlayerPauseService] Pause 진입 조건을 만족하지 못했습니다. reason={reason}", this);
            return false;
        }

        _resumeTimeScale = Time.timeScale;
        _wasPlayerInputEnabledBeforePause = InputManager.PlayerInput == null || InputManager.PlayerInput.enabled;

        if (_useTimeScalePause)
        {
            Time.timeScale = 0f;
        }

        if (_disablePlayerInputWhilePaused && InputManager.PlayerInput != null)
        {
            InputManager.PlayerInput.enabled = false;
        }

        IsPaused = true;
        PauseStateChanged?.Invoke(true);

        if (_verboseLog)
        {
            Debug.Log($"[SinglePlayerPauseService] Pause entered. reason={reason}", this);
        }

        return true;
    }

    /// <summary>
    /// Pause 상태를 정상 해제하고 게임 진행을 재개합니다.
    /// </summary>
    public bool TryResume(string reason)
    {
        if (IsPaused == false)
        {
            return true;
        }

        if (_useTimeScalePause)
        {
            Time.timeScale = Mathf.Approximately(_resumeTimeScale, 0f) ? 1f : _resumeTimeScale;
        }

        if (_disablePlayerInputWhilePaused && InputManager.PlayerInput != null)
        {
            InputManager.PlayerInput.enabled = _wasPlayerInputEnabledBeforePause;
        }

        IsPaused = false;
        PauseStateChanged?.Invoke(false);

        if (_verboseLog)
        {
            Debug.Log($"[SinglePlayerPauseService] Pause resumed. reason={reason}", this);
        }

        return true;
    }

    /// <summary>
    /// 씬 전환/강제 종료 전 안전성을 위해 Pause 상태를 즉시 해제합니다.
    /// </summary>
    public void ForceResume(string reason)
    {
        bool resumed = TryResume(reason); // 강제 해제 호출에서도 공통 Resume 로직을 재사용한 결과입니다.
        if (!resumed)
        {
            Debug.LogWarning($"[SinglePlayerPauseService] ForceResume 처리에 실패했습니다. reason={reason}", this);
        }
    }

    /// <summary>
    /// 직렬화 참조 또는 싱글톤에서 런타임 의존성을 해석합니다.
    /// </summary>
    private void ResolveDependencies()
    {
        if (_gameFlowController == null)
        {
            _gameFlowController = GameFlowController.Instance;
        }

        if (_sceneTransitionService == null)
        {
            _sceneTransitionService = SceneTransitionService.Instance;
        }
    }
}
