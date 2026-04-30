using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene-placed trigger that starts a boss-only stage entry through the existing GameFlow stage entry path.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class BossSceneTrigger : NetworkBehaviour
{
    [Header("Transition")]
    [Tooltip("Name of the scene that will be loaded after this trigger is used.")]
    [SerializeField] private string _targetSceneName; // Scene name loaded when this trigger is successfully used.

    [Tooltip("Seconds to wait after trigger use before requesting the scene transition.")]
    [Min(0f)]
    [SerializeField] private float _transitionDelaySeconds; // Delay between accepted trigger use and the actual scene transition request.

    [Tooltip("How the player activates this trigger.")]
    [SerializeField] private E_BossSceneTriggerActivationMode _activationMode = E_BossSceneTriggerActivationMode.OnEnter; // Designer-selected activation mode for this trigger.

    [Header("Interactor Filter")]
    [Tooltip("Only objects with this tag in their transform hierarchy can use this trigger. Leave empty to skip tag checking.")]
    [SerializeField] private string _allowedInteractorTag = "Player"; // Optional tag used to filter objects that can activate the trigger.

    [Tooltip("When enabled, multiplayer sessions only allow the Host player to use this trigger.")]
    [SerializeField] private bool _requireHostInMultiplayer = true; // Multiplayer authority guard that keeps boss scene transitions Host-controlled.

    [Header("Duplicate Guard")]
    [Tooltip("When enabled, this trigger ignores every later use request after the first accepted request starts.")]
    [SerializeField] private bool _blockDuplicateUse = true; // Prevents repeated scene transition requests from the same scene trigger.

    [Header("Input")]
    [Tooltip("Optional Interact input action used by InteractInsideTrigger mode. If empty, InputManager.PlayerInput is searched by action name.")]
    [SerializeField] private InputActionReference _interactAction; // Optional explicit action reference for designer-assigned interaction input.

    [Tooltip("InputManager.PlayerInput action name used when Interact Action is not assigned.")]
    [SerializeField] private string _interactActionName = "Interact"; // Fallback action name resolved from the existing InputManager player input asset.

    [Header("Stage Entry Flow")]
    [Tooltip("StageCatalog used to resolve TargetSceneName into the same StageDefinition flow used by title and town stage entry.")]
    [SerializeField] private StageCatalog _stageCatalog; // Catalog used to find the StageDefinition whose scene name matches TargetSceneName.

    [Tooltip("GameFlowController that owns the title-to-stage entry flow. If empty, GameFlowController.Instance is used.")]
    [SerializeField] private GameFlowController _gameFlowController; // Existing game flow controller used to enter the target stage through the shared stage entry path.

    private Collider _triggerCollider3D; // 3D collider reference used to validate trigger setup.
    private Collider2D _triggerCollider2D; // 2D collider reference used to validate trigger setup.
    private GameObject _currentInteractablePlayer; // Local valid player currently inside the trigger for InteractInsideTrigger mode.
    private InputAction _cachedInteractAction; // Cached interaction action resolved from InputActionReference or InputManager.PlayerInput.
    private Coroutine _transitionRoutine; // Running delay coroutine for an accepted trigger use.
    private bool _isUseInProgress; // Runtime duplicate guard state for an accepted trigger use.
    private bool _warnedMissingInteractAction; // Prevents repeated missing input warnings every frame.
    private bool _warnedEmptyAllowedTag; // Prevents repeated tag-skip warnings for empty AllowedInteractorTag.

    /// <summary>
    /// Public read-only access to the configured target scene name.
    /// </summary>
    public string TargetSceneName => _targetSceneName;

    /// <summary>
    /// Public read-only access to the configured transition delay.
    /// </summary>
    public float TransitionDelaySeconds => _transitionDelaySeconds;

    /// <summary>
    /// Public read-only access to the configured activation mode.
    /// </summary>
    public E_BossSceneTriggerActivationMode ActivationMode => _activationMode;

    /// <summary>
    /// Public read-only access to the configured duplicate guard.
    /// </summary>
    public bool BlockDuplicateUse => _blockDuplicateUse;

    /// <summary>
    /// Resolves local component references and validates scene trigger setup.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        ValidateTriggerCollider();
        CorrectDelayIfNeeded("Awake");
    }

    /// <summary>
    /// Enables the explicit interaction action when one is assigned in the Inspector.
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
    /// Clears runtime state when this trigger is disabled.
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
    /// Processes interaction input only while a valid player is inside the trigger.
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
    /// Handles 3D trigger entry and starts OnEnter mode immediately for valid local players.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        HandleTriggerEnter(other);
    }

    /// <summary>
    /// Handles 3D trigger exit and clears the current interactable player when it leaves.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        HandleTriggerExit(other);
    }

    /// <summary>
    /// Handles 2D trigger entry and starts OnEnter mode immediately for valid local players.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTriggerEnter(other);
    }

    /// <summary>
    /// Handles 2D trigger exit and clears the current interactable player when it leaves.
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        HandleTriggerExit(other);
    }

    /// <summary>
    /// Receives an accepted Host use request on the server route and starts the authoritative transition.
    /// </summary>
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void RequestBossSceneTransitionRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId; // NGO client id that submitted this trigger use request.
        if (_requireHostInMultiplayer && senderClientId != NetworkManager.ServerClientId)
        {
            Debug.LogWarning($"[BossSceneTrigger] Client trigger use was ignored on the server. senderClientId={senderClientId}, scene={_targetSceneName}", this);
            return;
        }

        BeginTransitionIfValid("ServerRoute");
    }

    /// <summary>
    /// Reports a client-side use attempt to the server log without starting a scene transition.
    /// </summary>
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void ReportClientUseIgnoredRpc(RpcParams rpcParams = default)
    {
        Debug.LogWarning($"[BossSceneTrigger] Client attempted to use a Host-only boss scene trigger. senderClientId={rpcParams.Receive.SenderClientId}, scene={_targetSceneName}", this);
    }

    /// <summary>
    /// Applies safe Inspector corrections that can be handled without runtime dependencies.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
        CorrectDelayIfNeeded("OnValidate");
    }

    /// <summary>
    /// Handles entry from either 2D or 3D trigger callbacks.
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
    /// Handles exit from either 2D or 3D trigger callbacks.
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
    /// Requests trigger use from the local peer after collision or input activation.
    /// </summary>
    private void RequestUse(GameObject playerObject, string reason)
    {
        if (playerObject == null)
        {
            Debug.LogWarning($"[BossSceneTrigger] Trigger use ignored because playerObject is null. reason={reason}", this);
            return;
        }

        if (_blockDuplicateUse && _isUseInProgress)
        {
            Debug.LogWarning($"[BossSceneTrigger] Duplicate trigger use ignored. reason={reason}, scene={_targetSceneName}", this);
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // Current NGO session state used to choose single-player, Host, or Client behavior.
        if (networkManager == null || !networkManager.IsListening)
        {
            BeginTransitionIfValid(reason);
            return;
        }

        if (!networkManager.IsServer)
        {
            Debug.LogWarning($"[BossSceneTrigger] Client trigger use ignored. Only Host can use boss scene triggers. reason={reason}, scene={_targetSceneName}", this);
            TryReportClientUseIgnoredToServer();
            return;
        }

        if (!IsSpawned)
        {
            Debug.LogWarning($"[BossSceneTrigger] Network session is active but trigger NetworkObject is not spawned. Server fallback path is used. scene={_targetSceneName}", this);
            BeginTransitionIfValid($"{reason}.UnspawnedServerFallback");
            return;
        }

        RequestBossSceneTransitionRpc();
    }

    /// <summary>
    /// Starts the transition delay and stage entry after all local authority and configuration checks pass.
    /// </summary>
    private void BeginTransitionIfValid(string reason)
    {
        if (_blockDuplicateUse && _isUseInProgress)
        {
            Debug.LogWarning($"[BossSceneTrigger] Duplicate trigger use ignored. reason={reason}, scene={_targetSceneName}", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(_targetSceneName))
        {
            Debug.LogWarning($"[BossSceneTrigger] TargetSceneName is empty. Stage entry ignored. reason={reason}", this);
            return;
        }

        if (_stageCatalog == null)
        {
            Debug.LogWarning($"[BossSceneTrigger] StageCatalog is missing. Assign the same StageCatalog used by title stage entry. scene={_targetSceneName}", this);
            return;
        }

        if (!TryResolveTargetStageDefinition(out StageDefinition targetStageDefinition))
        {
            Debug.LogWarning($"[BossSceneTrigger] TargetSceneName did not match any StageDefinition.SceneName in StageCatalog. scene={_targetSceneName}", this);
            return;
        }

        CorrectDelayIfNeeded("Runtime");
        _isUseInProgress = true;
        _transitionRoutine = StartCoroutine(EnterStageAfterDelayRoutine(targetStageDefinition, _transitionDelaySeconds));
    }

    /// <summary>
    /// Waits for the configured delay and delegates the actual stage entry to GameFlowController.
    /// </summary>
    private IEnumerator EnterStageAfterDelayRoutine(StageDefinition targetStageDefinition, float delaySeconds)
    {
        float safeDelaySeconds = Mathf.Max(0f, delaySeconds); // Runtime-safe delay value used by the wait instruction.
        if (safeDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(safeDelaySeconds);
        }

        GameFlowController gameFlowController = ResolveGameFlowController(); // Existing shared flow used by title and town stage entry.
        if (gameFlowController == null)
        {
            Debug.LogWarning($"[BossSceneTrigger] GameFlowController is missing. Stage entry request ignored. scene={_targetSceneName}", this);
            _isUseInProgress = false;
            _transitionRoutine = null;
            yield break;
        }

        bool started = gameFlowController.RequestEnterStage(targetStageDefinition);
        if (!started)
        {
            Debug.LogWarning($"[BossSceneTrigger] GameFlowController rejected boss stage entry. stageId={targetStageDefinition.StageId}, scene={targetStageDefinition.SceneName}", this);
            _isUseInProgress = false;
        }

        _transitionRoutine = null;
    }

    /// <summary>
    /// Resolves the GameFlowController used by the shared title-to-stage entry flow.
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
    /// Resolves TargetSceneName to a StageDefinition from the shared StageCatalog.
    /// </summary>
    private bool TryResolveTargetStageDefinition(out StageDefinition stageDefinition)
    {
        stageDefinition = null;
        if (_stageCatalog == null || _stageCatalog.Stages == null)
        {
            return false;
        }

        string normalizedTargetSceneName = _targetSceneName.Trim(); // Scene name used to match a StageDefinition entry.
        for (int index = 0; index < _stageCatalog.Stages.Count; index++)
        {
            StageDefinition candidate = _stageCatalog.Stages[index]; // Current stage definition candidate from the shared stage catalog.
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
    /// Resolves a collider callback target into the local player that is allowed to interact with this trigger.
    /// </summary>
    private bool TryResolveLocalInteractor(Component candidate, out GameObject playerObject)
    {
        playerObject = null;
        if (candidate == null)
        {
            Debug.LogWarning("[BossSceneTrigger] Null collider entered the boss scene trigger.", this);
            return false;
        }

        if (!PassesAllowedTag(candidate))
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // Current NGO session state used to filter local owner players.
        NetworkObject networkObject = candidate.GetComponentInParent<NetworkObject>();
        if (networkManager != null && networkManager.IsListening)
        {
            if (networkObject == null)
            {
                Debug.LogWarning($"[BossSceneTrigger] Multiplayer interactor without NetworkObject was ignored. collider={candidate.name}", this);
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
    /// Checks the optional AllowedInteractorTag against the candidate transform hierarchy.
    /// </summary>
    private bool PassesAllowedTag(Component candidate)
    {
        if (string.IsNullOrWhiteSpace(_allowedInteractorTag))
        {
            if (!_warnedEmptyAllowedTag)
            {
                _warnedEmptyAllowedTag = true;
                Debug.LogWarning("[BossSceneTrigger] AllowedInteractorTag is empty. Tag filtering is skipped.", this);
            }

            return true;
        }

        Transform current = candidate.transform; // Current transform in the upward hierarchy tag search.
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
    /// Checks whether the configured Interact action was performed this frame.
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
            Debug.LogWarning($"[BossSceneTrigger] Interact action was not found. Assign Interact Action or verify InputManager actionName={_interactActionName}.", this);
        }

        return false;
    }

    /// <summary>
    /// Resolves the interaction action from the Inspector reference first and InputManager.PlayerInput second.
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
    /// Sends a diagnostic report to the server when a Client attempts to use the trigger.
    /// </summary>
    private void TryReportClientUseIgnoredToServer()
    {
        if (IsSpawned)
        {
            ReportClientUseIgnoredRpc();
            return;
        }

        Debug.LogWarning($"[BossSceneTrigger] Client use report could not be sent because this trigger NetworkObject is not spawned. scene={_targetSceneName}", this);
    }

    /// <summary>
    /// Resolves local collider references used by validation.
    /// </summary>
    private void ResolveReferences()
    {
        if (_triggerCollider3D == null)
        {
            _triggerCollider3D = GetComponent<Collider>();
        }

        if (_triggerCollider2D == null)
        {
            _triggerCollider2D = GetComponent<Collider2D>();
        }
    }

    /// <summary>
    /// Warns when the scene object is not configured with a trigger collider.
    /// </summary>
    private void ValidateTriggerCollider()
    {
        if (_triggerCollider3D == null && _triggerCollider2D == null)
        {
            Debug.LogWarning("[BossSceneTrigger] Collider or Collider2D is missing. Add a trigger collider to use this scene trigger.", this);
            return;
        }

        if (_triggerCollider3D != null && !_triggerCollider3D.isTrigger)
        {
            Debug.LogWarning("[BossSceneTrigger] Collider is not marked as Trigger. Boss scene transition will not activate as expected.", this);
        }

        if (_triggerCollider2D != null && !_triggerCollider2D.isTrigger)
        {
            Debug.LogWarning("[BossSceneTrigger] Collider2D is not marked as Trigger. Boss scene transition will not activate as expected.", this);
        }
    }

    /// <summary>
    /// Corrects negative delay values and logs the correction.
    /// </summary>
    private void CorrectDelayIfNeeded(string source)
    {
        if (_transitionDelaySeconds >= 0f)
        {
            return;
        }

        Debug.LogWarning($"[BossSceneTrigger] TransitionDelaySeconds was negative and has been clamped to 0. source={source}", this);
        _transitionDelaySeconds = 0f;
    }
}
