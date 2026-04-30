using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 기존 GameFlow 스테이지 진입 경로를 통해 보스 전용 스테이지 진입을 시작하는 씬 배치 트리거.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class BossSceneTrigger : NetworkBehaviour
{
    [Header("Transition")]
    [Tooltip("이 트리거가 사용된 후 로드될 씬의 이름.")]
    [SerializeField] private string _targetSceneName; // 이 트리거가 정상적으로 사용되었을 때 로드되는 씬 이름.

    [Tooltip("트리거 사용 후 씬 전환 요청까지 대기할 시간(초).")]
    [Min(0f)]
    [SerializeField] private float _transitionDelaySeconds; // 트리거 사용 승인 후 실제 씬 전환 요청까지의 지연 시간.

    [Tooltip("플레이어가 이 트리거를 활성화하는 방식.")]
    [SerializeField] private E_BossSceneTriggerActivationMode _activationMode = E_BossSceneTriggerActivationMode.OnEnter; // 디자이너가 선택한 트리거 활성화 방식.

    [Header("Interactor Filter")]
    [Tooltip("Transform 계층 내에서 이 태그를 가진 오브젝트만 트리거를 사용할 수 있음. 비어있으면 태그 검사 생략.")]
    [SerializeField] private string _allowedInteractorTag = "Player"; // 트리거를 활성화할 수 있는 오브젝트를 필터링하는 선택적 태그.

    [Tooltip("활성화 시 멀티플레이에서는 Host 플레이어만 이 트리거를 사용할 수 있음.")]
    [SerializeField] private bool _requireHostInMultiplayer = true; // 보스 씬 전환을 Host가 제어하도록 제한하는 멀티플레이 권한 가드.

    [Header("Duplicate Guard")]
    [Tooltip("활성화 시 최초 사용 이후의 모든 추가 요청을 무시.")]
    [SerializeField] private bool _blockDuplicateUse = true; // 동일 트리거에서 반복적인 씬 전환 요청을 방지.

    [Header("Input")]
    [Tooltip("InteractInsideTrigger 모드에서 사용할 Interact 입력 액션. 비어있으면 InputManager.PlayerInput에서 이름으로 검색.")]
    [SerializeField] private InputActionReference _interactAction; // 디자이너가 지정하는 상호작용 입력 액션 참조.

    [Tooltip("Interact Action이 지정되지 않았을 때 사용할 InputManager.PlayerInput 액션 이름.")]
    [SerializeField] private string _interactActionName = "Interact"; // 기존 InputManager PlayerInput에서 찾을 fallback 액션 이름.

    [Header("Stage Entry Flow")]
    [Tooltip("TargetSceneName을 StageDefinition으로 변환하기 위해 사용하는 StageCatalog.")]
    [SerializeField] private StageCatalog _stageCatalog; // TargetSceneName과 일치하는 StageDefinition을 찾기 위한 카탈로그.

    [Tooltip("타이틀 → 스테이지 진입 흐름을 담당하는 GameFlowController. 비어있으면 Instance 사용.")]
    [SerializeField] private GameFlowController _gameFlowController; // 공용 스테이지 진입 경로를 사용하는 게임 흐름 컨트롤러.

    private Collider2D _triggerCollider2D; // 트리거 설정 검증에 사용하는 2D Collider 참조.
    private GameObject _currentInteractablePlayer; // InteractInsideTrigger 모드에서 트리거 내부에 있는 현재 플레이어.
    private InputAction _cachedInteractAction; // InputActionReference 또는 InputManager에서 캐싱된 상호작용 액션.
    private Coroutine _transitionRoutine; // 트리거 승인 후 실행 중인 지연 코루틴.
    private bool _isUseInProgress; // 중복 실행 방지를 위한 런타임 상태.
    private bool _warnedMissingInteractAction; // 입력 누락 경고 반복 방지.
    private bool _warnedEmptyAllowedTag; // 빈 태그 경고 반복 방지.

    /// <summary>
    /// 설정된 타겟 씬 이름에 대한 읽기 전용 접근.
    /// </summary>
    public string TargetSceneName => _targetSceneName;

    /// <summary>
    /// 설정된 전환 지연 시간에 대한 읽기 전용 접근.
    /// </summary>
    public float TransitionDelaySeconds => _transitionDelaySeconds;

    /// <summary>
    /// 설정된 활성화 방식에 대한 읽기 전용 접근.
    /// </summary>
    public E_BossSceneTriggerActivationMode ActivationMode => _activationMode;

    /// <summary>
    /// 설정된 중복 방지 여부에 대한 읽기 전용 접근.
    /// </summary>
    public bool BlockDuplicateUse => _blockDuplicateUse;

    /// <summary>
    /// 로컬 컴포넌트 참조를 초기화하고 트리거 설정을 검증한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        ValidateTriggerCollider();
        CorrectDelayIfNeeded("Awake");
    }

    /// <summary>
    /// Inspector에서 지정된 입력 액션이 있으면 활성화한다.
    /// </summary>
    private void OnEnable()
    {
        InputAction action = ResolveInteractAction();
        if (action != null)
        {
            action.Enable();
        }
    }

    /// <summary>
    /// 트리거 비활성화 시 런타임 상태를 초기화한다.
    /// </summary>
    private void OnDisable()
    {
        _currentInteractablePlayer = null;

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }
    }

    /// <summary>
    /// 유효한 플레이어가 트리거 내부에 있을 때만 상호작용 입력을 처리한다.
    /// </summary>
    private void Update()
    {
        if (_activationMode != E_BossSceneTriggerActivationMode.InteractInsideTrigger)
        {
            return;
        }

        if (_currentInteractablePlayer == null)
        {
            return;
        }

        if (!WasInteractPressedThisFrame())
        {
            return;
        }

        RequestUse(_currentInteractablePlayer, "InteractInsideTrigger");
    }

    /// <summary>
    /// 2D 트리거 진입 처리 및 OnEnter 모드일 경우 즉시 실행.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTriggerEnter(other);
    }

    /// <summary>
    /// 2D 트리거 이탈 처리 및 현재 플레이어 초기화.
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        HandleTriggerExit(other);
    }

    /// <summary>
    /// 서버 경로에서 Host의 트리거 사용 요청을 수신하고 권한 있는 씬 전환을 시작한다.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestBossSceneTransitionRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId; // 트리거 사용 요청을 보낸 NGO 클라이언트 ID.
        if (_requireHostInMultiplayer && senderClientId != NetworkManager.ServerClientId)
        {
            Debug.LogWarning($"[BossSceneTrigger] 서버에서 클라이언트 트리거 사용 요청이 무시됨. senderClientId={senderClientId}, scene={_targetSceneName}", this);
            return;
        }

        BeginTransitionIfValid("ServerRoute");
    }

    /// <summary>
    /// 클라이언트에서 트리거 사용 시도를 서버 로그로만 전달하고 씬 전환은 수행하지 않는다.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ReportClientUseIgnoredRpc(RpcParams rpcParams = default)
    {
        Debug.LogWarning($"[BossSceneTrigger] 클라이언트가 Host 전용 보스 트리거를 사용하려고 시도함. senderClientId={rpcParams.Receive.SenderClientId}, scene={_targetSceneName}", this);
    }

    /// <summary>
    /// 런타임 의존성이 없는 안전한 Inspector 값 보정을 수행한다.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
        CorrectDelayIfNeeded("OnValidate");
    }

    /// <summary>
    /// 2D 트리거 진입을 공통 처리한다.
    /// </summary>
    private void HandleTriggerEnter(Component other)
    {
        if (!TryResolveLocalInteractor(other, out GameObject playerObject))
        {
            return;
        }

        _currentInteractablePlayer = playerObject;

        if (_activationMode == E_BossSceneTriggerActivationMode.OnEnter)
        {
            RequestUse(playerObject, "OnEnter");
        }
    }

    /// <summary>
    /// 2D 트리거 이탈을 공통 처리한다.
    /// </summary>
    private void HandleTriggerExit(Component other)
    {
        if (_currentInteractablePlayer == null)
        {
            return;
        }

        if (!TryResolveLocalInteractor(other, out GameObject playerObject))
        {
            return;
        }

        if (playerObject == _currentInteractablePlayer)
        {
            _currentInteractablePlayer = null;
        }
    }

    /// <summary>
    /// 충돌 또는 입력을 통해 로컬에서 트리거 사용을 요청한다.
    /// </summary>
    private void RequestUse(GameObject playerObject, string reason)
    {
        if (playerObject == null)
        {
            Debug.LogWarning($"[BossSceneTrigger] playerObject가 null이라 트리거 사용이 무시됨. reason={reason}", this);
            return;
        }

        if (_blockDuplicateUse && _isUseInProgress)
        {
            Debug.LogWarning($"[BossSceneTrigger] 중복 트리거 사용 요청이 무시됨. reason={reason}, scene={_targetSceneName}", this);
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션 상태 (싱글, Host, Client 분기용).
        if (networkManager == null || !networkManager.IsListening)
        {
            BeginTransitionIfValid(reason);
            return;
        }

        if (!networkManager.IsServer)
        {
            Debug.LogWarning($"[BossSceneTrigger] 클라이언트 트리거 사용 무시됨. Host만 사용 가능. reason={reason}, scene={_targetSceneName}", this);
            TryReportClientUseIgnoredToServer();
            return;
        }

        if (!IsSpawned)
        {
            Debug.LogWarning($"[BossSceneTrigger] 네트워크 세션 중이지만 NetworkObject가 Spawn되지 않음. 서버 fallback 경로 사용. scene={_targetSceneName}", this);
            BeginTransitionIfValid($"{reason}.UnspawnedServerFallback");
            return;
        }

        RequestBossSceneTransitionRpc();
    }

    /// <summary>
    /// 모든 권한 및 설정 검증이 통과된 경우 전환을 시작한다.
    /// </summary>
    private void BeginTransitionIfValid(string reason)
    {
        if (_blockDuplicateUse && _isUseInProgress)
        {
            Debug.LogWarning($"[BossSceneTrigger] 중복 트리거 사용 요청이 무시됨. reason={reason}, scene={_targetSceneName}", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(_targetSceneName))
        {
            Debug.LogWarning($"[BossSceneTrigger] TargetSceneName이 비어 있음. 스테이지 진입 무시. reason={reason}", this);
            return;
        }

        if (_stageCatalog == null)
        {
            Debug.LogWarning($"[BossSceneTrigger] StageCatalog 없음. 타이틀과 동일한 카탈로그를 할당해야 함. scene={_targetSceneName}", this);
            return;
        }

        if (!TryResolveTargetStageDefinition(out StageDefinition targetStageDefinition))
        {
            Debug.LogWarning($"[BossSceneTrigger] TargetSceneName과 일치하는 StageDefinition이 없음. scene={_targetSceneName}", this);
            return;
        }

        CorrectDelayIfNeeded("Runtime");
        _isUseInProgress = true;
        _transitionRoutine = StartCoroutine(EnterStageAfterDelayRoutine(targetStageDefinition, _transitionDelaySeconds));
    }

    /// <summary>
    /// 설정된 지연 후 GameFlowController를 통해 실제 스테이지 진입을 수행한다.
    /// </summary>
    private IEnumerator EnterStageAfterDelayRoutine(StageDefinition targetStageDefinition, float delaySeconds)
    {
        float safeDelaySeconds = Mathf.Max(0f, delaySeconds); // 안전한 지연 시간.
        if (safeDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(safeDelaySeconds);
        }

        GameFlowController gameFlowController = ResolveGameFlowController(); // 공용 스테이지 진입 흐름.
        if (gameFlowController == null)
        {
            Debug.LogWarning($"[BossSceneTrigger] GameFlowController 없음. 스테이지 진입 무시. scene={_targetSceneName}", this);
            _isUseInProgress = false;
            _transitionRoutine = null;
            yield break;
        }

        bool started = gameFlowController.RequestEnterStage(targetStageDefinition);
        if (!started)
        {
            Debug.LogWarning($"[BossSceneTrigger] GameFlowController가 스테이지 진입을 거부함. stageId={targetStageDefinition.StageId}, scene={targetStageDefinition.SceneName}", this);
            _isUseInProgress = false;
        }

        _transitionRoutine = null;
    }

    /// <summary>
    /// 공용 타이틀 → 스테이지 진입 흐름에 사용하는 GameFlowController를 가져온다.
    /// </summary>
    private GameFlowController ResolveGameFlowController()
    {
        if (_gameFlowController != null)
        {
            return _gameFlowController;
        }

        _gameFlowController = GameFlowController.Instance;
        if (_gameFlowController == null)
        {
            _gameFlowController = FindAnyObjectByType<GameFlowController>();
        }

        return _gameFlowController;
    }

    /// <summary>
    /// TargetSceneName을 StageCatalog에서 StageDefinition으로 변환한다.
    /// </summary>
    private bool TryResolveTargetStageDefinition(out StageDefinition stageDefinition)
    {
        stageDefinition = null;
        if (_stageCatalog == null || _stageCatalog.Stages == null)
        {
            return false;
        }

        string normalizedTargetSceneName = _targetSceneName.Trim(); // StageDefinition과 비교하기 위한 씬 이름.
        for (int index = 0; index < _stageCatalog.Stages.Count; index++)
        {
            StageDefinition candidate = _stageCatalog.Stages[index]; // 현재 검사 중인 StageDefinition.
            if (candidate == null)
            {
                continue;
            }

            if (string.Equals(candidate.SceneName, normalizedTargetSceneName, System.StringComparison.Ordinal))
            {
                stageDefinition = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 콜라이더 대상에서 트리거 사용이 가능한 로컬 플레이어를 찾아낸다.
    /// </summary>
    private bool TryResolveLocalInteractor(Component candidate, out GameObject playerObject)
    {
        playerObject = null;
        if (candidate == null)
        {
            Debug.LogWarning("[BossSceneTrigger] null 콜라이더가 트리거에 진입함.", this);
            return false;
        }

        if (!PassesAllowedTag(candidate))
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // 로컬 플레이어 필터링에 사용하는 NGO 세션 상태.
        NetworkObject networkObject = candidate.GetComponentInParent<NetworkObject>();
        if (networkManager != null && networkManager.IsListening)
        {
            if (networkObject == null)
            {
                Debug.LogWarning($"[BossSceneTrigger] NetworkObject가 없는 멀티플레이 인터랙터는 무시됨. collider={candidate.name}", this);
                return false;
            }

            if (!networkObject.IsOwner)
            {
                return false;
            }

            playerObject = networkObject.gameObject;
            return true;
        }

        if (networkObject != null)
        {
            playerObject = networkObject.gameObject;
            return true;
        }

        PlayerMovement movement = candidate.GetComponentInParent<PlayerMovement>();
        if (movement != null)
        {
            playerObject = movement.gameObject;
            return true;
        }

        return false;
    }

    /// <summary>
    /// AllowedInteractorTag 조건을 만족하는지 검사한다.
    /// </summary>
    private bool PassesAllowedTag(Component candidate)
    {
        if (string.IsNullOrWhiteSpace(_allowedInteractorTag))
        {
            if (!_warnedEmptyAllowedTag)
            {
                _warnedEmptyAllowedTag = true;
                Debug.LogWarning("[BossSceneTrigger] AllowedInteractorTag가 비어 있음. 태그 필터링 생략됨.", this);
            }

            return true;
        }

        Transform current = candidate.transform; // 상위 계층으로 올라가며 태그 검사.
        while (current != null)
        {
            if (current.gameObject.tag == _allowedInteractorTag)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    /// <summary>
    /// 이번 프레임에 Interact 입력이 발생했는지 확인한다.
    /// </summary>
    private bool WasInteractPressedThisFrame()
    {
        InputAction action = ResolveInteractAction();
        if (action != null)
        {
            return action.WasPerformedThisFrame();
        }

        if (!_warnedMissingInteractAction)
        {
            _warnedMissingInteractAction = true;
            Debug.LogWarning($"[BossSceneTrigger] Interact 액션을 찾을 수 없음. Interact Action을 할당하거나 InputManager actionName={_interactActionName} 확인 필요.", this);
        }

        return false;
    }

    /// <summary>
    /// Inspector → InputManager 순서로 상호작용 입력 액션을 찾는다.
    /// </summary>
    private InputAction ResolveInteractAction()
    {
        if (_interactAction != null && _interactAction.action != null)
        {
            return _interactAction.action;
        }

        if (_cachedInteractAction != null)
        {
            return _cachedInteractAction;
        }

        if (InputManager.PlayerInput != null && !string.IsNullOrWhiteSpace(_interactActionName))
        {
            _cachedInteractAction = InputManager.PlayerInput.actions.FindAction(_interactActionName, false);
            return _cachedInteractAction;
        }

        return null;
    }

    /// <summary>
    /// 클라이언트가 트리거를 사용했을 때 서버로 진단 로그를 전송한다.
    /// </summary>
    private void TryReportClientUseIgnoredToServer()
    {
        if (IsSpawned)
        {
            ReportClientUseIgnoredRpc();
            return;
        }

        Debug.LogWarning($"[BossSceneTrigger] NetworkObject가 Spawn되지 않아 클라이언트 사용 리포트를 전송할 수 없음. scene={_targetSceneName}", this);
    }

    /// <summary>
    /// 검증에 필요한 로컬 콜라이더 참조를 설정한다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_triggerCollider2D == null)
        {
            _triggerCollider2D = GetComponent<Collider2D>();
        }
    }

    /// <summary>
    /// 트리거 콜라이더 설정이 올바른지 검사한다.
    /// </summary>
    private void ValidateTriggerCollider()
    {
        if (_triggerCollider2D == null)
        {
            Debug.LogWarning("[BossSceneTrigger] Collider2D 없음. 트리거 콜라이더 추가 필요.", this);
            return;
        }

        if (_triggerCollider2D != null && !_triggerCollider2D.isTrigger)
        {
            Debug.LogWarning("[BossSceneTrigger] Collider2D가 Trigger로 설정되지 않음. 정상 동작하지 않을 수 있음.", this);
        }
    }

    /// <summary>
    /// 음수 지연값을 0으로 보정하고 로그를 남긴다.
    /// </summary>
    private void CorrectDelayIfNeeded(string source)
    {
        if (_transitionDelaySeconds >= 0f)
        {
            return;
        }

        Debug.LogWarning($"[BossSceneTrigger] TransitionDelaySeconds가 음수여서 0으로 보정됨. source={source}", this);
        _transitionDelaySeconds = 0f;
    }
}
