using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resolves valid Player targets for boss pattern execution using existing targeting first and a reusable fallback scan only when needed.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossPlayerTargetProvider : MonoBehaviour
{
    [Header("Required References")]
    [Tooltip("Boss controller used for authority checks before Player search runs.")]
    [SerializeField] private BossController _bossController; // Authority source that prevents clients from deciding Player targets.

    [Tooltip("Existing EnemyTargetDetector reused as the first Player targeting source when available.")]
    [SerializeField] private EnemyTargetDetector _enemyTargetDetector; // Existing targeting component reused before fallback scanning.

    [Tooltip("Boss Transform used as the origin for execution range and nearest target checks.")]
    [SerializeField] private Transform _bossTransform; // Origin transform for target distance comparisons.

    [Header("Fallback Search")]
    [Tooltip("Player LayerMask used only when the existing target detector cannot provide a valid target.")]
    [SerializeField] private LayerMask _playerLayerMask; // Fallback scan layer mask for Player candidate colliders.

    [Tooltip("Player tag used to filter fallback scan candidates. Empty value allows every tag.")]
    [SerializeField] private string _playerTag = "Player"; // Fallback scan tag filter for Player roots.

    [Tooltip("Default execution range used by parameterless target searches.")]
    [Min(0f)]
    [SerializeField] private float _defaultExecutionRange = 20f; // Default radius used when a pattern does not pass a specific range.

    [Tooltip("Reusable fallback collider buffer size for Player candidate scans.")]
    [Min(1)]
    [SerializeField] private int _candidateBufferSize = 16; // Reusable Physics2D result buffer capacity.

    [Tooltip("Reusable HealthComponent buffer size for multi-target Player collection such as Pattern 4 timeout damage.")]
    [Min(1)]
    [SerializeField] private int _playerHealthBufferSize = 4; // Reusable Player HealthComponent buffer capacity for all-target operations.

    [Tooltip("Whether fallback scan usage should be reported as a Warning.")]
    [SerializeField] private bool _warnWhenFallbackScanUsed = true; // Warning toggle for fallback target scans.

    private Collider2D[] _candidateBuffer = new Collider2D[0]; // Reusable collider buffer used by fallback target scans.
    private ContactFilter2D _playerContactFilter; // Reusable layer and trigger filter for fallback target scans.
    private bool _hasLoggedFallbackScanWarning; // Prevents repeated fallback scan warnings from this provider state.
    private bool _hasLoggedAuthorityWarning; // Prevents repeated client-side target search warnings from this provider state.
    private bool _hasLoggedMissingReferenceWarning; // Prevents repeated missing reference warnings from this provider state.
    private bool _hasLoggedPlayerHealthBufferOverflowWarning; // Prevents repeated warnings when the reusable Player health buffer is full.

    /// <summary>
    /// Gets the default execution range used by parameterless target searches.
    /// </summary>
    public float DefaultExecutionRange => _defaultExecutionRange;

    /// <summary>
    /// Gets the recommended reusable Player HealthComponent buffer capacity.
    /// </summary>
    public int PlayerHealthBufferSize => _playerHealthBufferSize;

    /// <summary>
    /// Prepares reusable fallback search storage and resolves optional references.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        EnsureCandidateBuffer();
        RefreshContactFilter();
    }

    /// <summary>
    /// Corrects invalid inspector values and refreshes reusable search configuration.
    /// </summary>
    private void OnValidate()
    {
        if (_candidateBufferSize < 1)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] CandidateBufferSize was below 1 and clamped. object={name}, value={_candidateBufferSize}", this);
            _candidateBufferSize = 1;
        }

        if (_playerHealthBufferSize < 1)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] PlayerHealthBufferSize was below 1 and clamped. object={name}, value={_playerHealthBufferSize}", this);
            _playerHealthBufferSize = 1;
        }

        if (_defaultExecutionRange < 0f)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] DefaultExecutionRange was below zero and clamped. object={name}, value={_defaultExecutionRange}", this);
            _defaultExecutionRange = 0f;
        }

        ResolveReferences();
        EnsureCandidateBuffer();
        RefreshContactFilter();
    }

    /// <summary>
    /// Finds the nearest valid Player using the default execution range.
    /// </summary>
    public bool TryFindNearestPlayerForExecution(out Transform targetTransform)
    {
        return TryFindNearestPlayerForExecution(_defaultExecutionRange, out targetTransform, out _, out _);
    }

    /// <summary>
    /// Finds the nearest valid Player using a pattern-specific execution range.
    /// </summary>
    public bool TryFindNearestPlayerForExecution(float executionRange, out Transform targetTransform)
    {
        return TryFindNearestPlayerForExecution(executionRange, out targetTransform, out _, out _);
    }

    /// <summary>
    /// Finds the nearest valid Player and returns its resolved Transform, HealthComponent, and NetworkObject.
    /// </summary>
    public bool TryFindNearestPlayerForExecution(float executionRange, out Transform targetTransform, out HealthComponent targetHealth, out NetworkObject targetNetworkObject)
    {
        targetTransform = null;
        targetHealth = null;
        targetNetworkObject = null;

        if (!CanSearchForPlayer())
        {
            return false;
        }

        float safeExecutionRange = Mathf.Max(0f, executionRange); // Pattern execution range clamped for overlap and squared checks.
        Vector3 bossPosition = _bossTransform.position; // Boss world position used as the distance origin.

        if (TryFindByExistingTargetDetector(safeExecutionRange, bossPosition, out targetTransform, out targetHealth, out targetNetworkObject))
        {
            return true;
        }

        return TryFindByFallbackScan(safeExecutionRange, bossPosition, out targetTransform, out targetHealth, out targetNetworkObject);
    }

    /// <summary>
    /// Collects every currently valid Player health target into a caller-owned reusable buffer.
    /// </summary>
    public int CollectAlivePlayersForExecution(HealthComponent[] playerHealthBuffer)
    {
        if (playerHealthBuffer == null || playerHealthBuffer.Length == 0)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] CollectAlivePlayersForExecution received an empty target buffer. object={name}", this);
            return 0;
        }

        for (int index = 0; index < playerHealthBuffer.Length; index++)
        {
            playerHealthBuffer[index] = null;
        }

        if (!CanSearchForPlayer())
        {
            return 0;
        }

        Vector3 bossPosition = _bossTransform.position; // Boss world position used when fallback scans need a search origin.
        int collectedCount = 0; // Number of unique Player HealthComponents copied into the caller buffer.
        NetworkManager networkManager = NetworkManager.Singleton; // NGO singleton used to decide whether PlayerObjects are the authoritative player registry.
        CollectNetworkPlayers(playerHealthBuffer, ref collectedCount);
        if (networkManager == null || !networkManager.IsListening || collectedCount <= 0)
        {
            CollectFallbackScanPlayers(bossPosition, playerHealthBuffer, ref collectedCount);
        }

        return collectedCount;
    }

    /// <summary>
    /// Returns whether this provider can run target search on the current instance.
    /// </summary>
    private bool CanSearchForPlayer()
    {
        ResolveReferences();

        if (_bossController == null || _bossTransform == null)
        {
            if (!_hasLoggedMissingReferenceWarning)
            {
                Debug.LogWarning($"[BossPlayerTargetProvider] Player search failed because BossController or BossTransform is missing. object={name}", this);
                _hasLoggedMissingReferenceWarning = true;
            }

            return false;
        }

        if (!_bossController.IsBossLogicAuthority())
        {
            if (!_hasLoggedAuthorityWarning)
            {
                Debug.LogWarning($"[BossPlayerTargetProvider] Player search was blocked because this instance has no boss authority. object={name}", this);
                _hasLoggedAuthorityWarning = true;
            }

            return false;
        }

        _hasLoggedAuthorityWarning = false;
        return true;
    }

    /// <summary>
    /// Attempts to use the existing EnemyTargetDetector before fallback scanning.
    /// </summary>
    private bool TryFindByExistingTargetDetector(float executionRange, Vector3 bossPosition, out Transform targetTransform, out HealthComponent targetHealth, out NetworkObject targetNetworkObject)
    {
        targetTransform = null;
        targetHealth = null;
        targetNetworkObject = null;

        if (_enemyTargetDetector == null)
        {
            return false;
        }

        _enemyTargetDetector.TickSearch(Time.time, bossPosition, executionRange, 0.01f);
        Transform detectorTarget = _enemyTargetDetector.CurrentTarget; // Existing detector result selected from its own reusable buffer.
        return TryResolveValidTarget(detectorTarget, bossPosition, executionRange * executionRange, out targetTransform, out targetHealth, out targetNetworkObject);
    }

    /// <summary>
    /// Scans Player candidates with a reusable collider buffer when the existing detector cannot provide a valid target.
    /// </summary>
    private bool TryFindByFallbackScan(float executionRange, Vector3 bossPosition, out Transform targetTransform, out HealthComponent targetHealth, out NetworkObject targetNetworkObject)
    {
        targetTransform = null;
        targetHealth = null;
        targetNetworkObject = null;

        if (_warnWhenFallbackScanUsed && !_hasLoggedFallbackScanWarning)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] Existing target detector did not provide a valid target. Fallback scan is running. object={name}", this);
            _hasLoggedFallbackScanWarning = true;
        }

        EnsureCandidateBuffer();
        RefreshContactFilter();

        int hitCount = Physics2D.OverlapCircle((Vector2)bossPosition, executionRange, _playerContactFilter, _candidateBuffer);
        float executionRangeSqr = executionRange * executionRange; // Squared execution range used for candidate filtering.
        float nearestSqrDistance = float.MaxValue; // Best squared distance found in this execution search.
        bool foundTarget = false; // Whether a valid Player candidate has been found.

        for (int index = 0; index < hitCount; index++)
        {
            Collider2D candidateCollider = _candidateBuffer[index]; // Candidate collider returned by Physics2D into the reusable buffer.
            _candidateBuffer[index] = null;
            if (candidateCollider == null)
            {
                continue;
            }

            Transform candidateSource = ResolveCandidateSourceTransform(candidateCollider);
            if (!TryResolveValidTarget(candidateSource, bossPosition, executionRangeSqr, out Transform resolvedTransform, out HealthComponent resolvedHealth, out NetworkObject resolvedNetworkObject))
            {
                continue;
            }

            float sqrDistance = (resolvedTransform.position - bossPosition).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
            {
                continue;
            }

            nearestSqrDistance = sqrDistance;
            targetTransform = resolvedTransform;
            targetHealth = resolvedHealth;
            targetNetworkObject = resolvedNetworkObject;
            foundTarget = true;
        }

        return foundTarget;
    }

    /// <summary>
    /// Collects PlayerObjects from NGO when a multiplayer session is active.
    /// </summary>
    private void CollectNetworkPlayers(HealthComponent[] playerHealthBuffer, ref int collectedCount)
    {
        NetworkManager networkManager = NetworkManager.Singleton; // NGO singleton used to enumerate connected PlayerObjects without deciding on clients.
        if (networkManager == null || !networkManager.IsListening)
        {
            return;
        }

        for (int index = 0; index < networkManager.ConnectedClientsList.Count; index++)
        {
            NetworkClient client = networkManager.ConnectedClientsList[index]; // Connected NGO client that may own a PlayerObject.
            if (client == null || client.PlayerObject == null)
            {
                continue;
            }

            if (!TryResolveValidTargetWithoutRange(client.PlayerObject.transform, out HealthComponent targetHealth))
            {
                continue;
            }

            AddUniquePlayerHealth(playerHealthBuffer, ref collectedCount, targetHealth);
        }
    }

    /// <summary>
    /// Collects valid Player candidates with the reusable Physics2D fallback buffer.
    /// </summary>
    private void CollectFallbackScanPlayers(Vector3 bossPosition, HealthComponent[] playerHealthBuffer, ref int collectedCount)
    {
        if (_warnWhenFallbackScanUsed && !_hasLoggedFallbackScanWarning)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] Player registry was not found. Fallback scan is collecting Player targets. object={name}", this);
            _hasLoggedFallbackScanWarning = true;
        }

        EnsureCandidateBuffer();
        RefreshContactFilter();

        float safeExecutionRange = Mathf.Max(0f, _defaultExecutionRange); // Default execution range used as the reusable fallback collection radius.
        int hitCount = Physics2D.OverlapCircle((Vector2)bossPosition, safeExecutionRange, _playerContactFilter, _candidateBuffer);
        float executionRangeSqr = safeExecutionRange * safeExecutionRange; // Squared range used by valid target filtering.
        for (int index = 0; index < hitCount; index++)
        {
            Collider2D candidateCollider = _candidateBuffer[index]; // Candidate collider returned by Physics2D into the reusable buffer.
            _candidateBuffer[index] = null;
            if (candidateCollider == null)
            {
                continue;
            }

            Transform candidateSource = ResolveCandidateSourceTransform(candidateCollider);
            if (!TryResolveValidTarget(candidateSource, bossPosition, executionRangeSqr, out _, out HealthComponent targetHealth, out _))
            {
                continue;
            }

            AddUniquePlayerHealth(playerHealthBuffer, ref collectedCount, targetHealth);
        }
    }

    /// <summary>
    /// Resolves and validates a Player candidate without range checks.
    /// </summary>
    private bool TryResolveValidTargetWithoutRange(Transform candidateSource, out HealthComponent targetHealth)
    {
        targetHealth = null;
        if (candidateSource == null || !MatchesPlayerTag(candidateSource))
        {
            return false;
        }

        targetHealth = ResolveHealthComponent(candidateSource);
        if (!IsHealthValid(targetHealth))
        {
            return false;
        }

        NetworkObject targetNetworkObject = candidateSource.GetComponentInParent<NetworkObject>(); // Optional network root used to resolve the authoritative transform.
        Transform targetTransform = ResolveTargetTransform(candidateSource, targetNetworkObject);
        return IsTransformValid(targetTransform);
    }

    /// <summary>
    /// Adds one Player HealthComponent to the caller buffer if it has not already been collected.
    /// </summary>
    private void AddUniquePlayerHealth(HealthComponent[] playerHealthBuffer, ref int collectedCount, HealthComponent targetHealth)
    {
        if (targetHealth == null)
        {
            return;
        }

        for (int index = 0; index < collectedCount; index++)
        {
            if (playerHealthBuffer[index] == targetHealth)
            {
                return;
            }
        }

        if (collectedCount >= playerHealthBuffer.Length)
        {
            if (!_hasLoggedPlayerHealthBufferOverflowWarning)
            {
                Debug.LogWarning($"[BossPlayerTargetProvider] Player health buffer is full. Increase PlayerHealthBufferSize or the caller buffer size. object={name}, capacity={playerHealthBuffer.Length}", this);
                _hasLoggedPlayerHealthBufferOverflowWarning = true;
            }

            return;
        }

        playerHealthBuffer[collectedCount] = targetHealth;
        collectedCount++;
    }

    /// <summary>
    /// Resolves and validates a Player candidate transform for pattern execution targeting.
    /// </summary>
    private bool TryResolveValidTarget(Transform candidateSource, Vector3 bossPosition, float executionRangeSqr, out Transform targetTransform, out HealthComponent targetHealth, out NetworkObject targetNetworkObject)
    {
        targetTransform = null;
        targetHealth = null;
        targetNetworkObject = null;

        if (candidateSource == null)
        {
            return false;
        }

        if (!MatchesPlayerTag(candidateSource))
        {
            return false;
        }

        targetHealth = ResolveHealthComponent(candidateSource);
        if (!IsHealthValid(targetHealth))
        {
            return false;
        }

        targetNetworkObject = candidateSource.GetComponentInParent<NetworkObject>();
        targetTransform = ResolveTargetTransform(candidateSource, targetNetworkObject);
        if (targetTransform == null)
        {
            return false;
        }

        if (!IsTransformValid(targetTransform))
        {
            return false;
        }

        float sqrDistance = (targetTransform.position - bossPosition).sqrMagnitude;
        return sqrDistance <= executionRangeSqr;
    }

    /// <summary>
    /// Resolves the Transform used for candidate position checks.
    /// </summary>
    private Transform ResolveTargetTransform(Transform candidateSource, NetworkObject targetNetworkObject)
    {
        if (targetNetworkObject != null)
        {
            return targetNetworkObject.transform;
        }

        return candidateSource.root;
    }

    /// <summary>
    /// Resolves the most useful source Transform from a candidate collider.
    /// </summary>
    private Transform ResolveCandidateSourceTransform(Collider2D candidateCollider)
    {
        if (candidateCollider == null)
        {
            return null;
        }

        if (candidateCollider.attachedRigidbody != null)
        {
            return candidateCollider.attachedRigidbody.transform;
        }

        return candidateCollider.transform;
    }

    /// <summary>
    /// Returns whether the candidate hierarchy matches the configured Player tag.
    /// </summary>
    private bool MatchesPlayerTag(Transform candidateTransform)
    {
        if (candidateTransform == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_playerTag))
        {
            return true;
        }

        Transform current = candidateTransform; // Current hierarchy node checked for the configured Player tag.
        while (current != null)
        {
            if (current.CompareTag(_playerTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    /// <summary>
    /// Resolves HealthComponent from the candidate hierarchy.
    /// </summary>
    private HealthComponent ResolveHealthComponent(Transform candidateTransform)
    {
        if (candidateTransform == null)
        {
            return null;
        }

        HealthComponent health = candidateTransform.GetComponent<HealthComponent>(); // Direct HealthComponent candidate on the resolved transform.
        if (health != null)
        {
            return health;
        }

        health = candidateTransform.GetComponentInParent<HealthComponent>();
        if (health != null)
        {
            return health;
        }

        return candidateTransform.GetComponentInChildren<HealthComponent>(true);
    }

    /// <summary>
    /// Returns whether the candidate health state is valid for boss targeting.
    /// </summary>
    private bool IsHealthValid(HealthComponent health)
    {
        if (health == null)
        {
            return false;
        }

        if (!health.isActiveAndEnabled || health.IsDead)
        {
            return false;
        }

        return health.GetCurrentHealth() > 0f;
    }

    /// <summary>
    /// Returns whether the resolved target Transform can be used for boss targeting.
    /// </summary>
    private bool IsTransformValid(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            return false;
        }

        GameObject targetObject = targetTransform.gameObject; // Resolved Player object used for active and scene validation.
        if (!targetObject.activeInHierarchy)
        {
            return false;
        }

        Scene targetScene = targetObject.scene; // Scene containing the resolved Player target.
        return targetScene.IsValid() && targetScene == gameObject.scene && targetScene == SceneManager.GetActiveScene();
    }

    /// <summary>
    /// Resolves optional references from this GameObject when inspector references are missing.
    /// </summary>
    private void ResolveReferences()
    {
        if (_bossController == null)
        {
            _bossController = GetComponent<BossController>();
        }

        if (_enemyTargetDetector == null)
        {
            _enemyTargetDetector = GetComponent<EnemyTargetDetector>();
        }

        if (_bossTransform == null)
        {
            _bossTransform = transform;
        }
    }

    /// <summary>
    /// Ensures the reusable candidate collider buffer matches the inspector capacity.
    /// </summary>
    private void EnsureCandidateBuffer()
    {
        if (_candidateBufferSize < 1)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] CandidateBufferSize was below 1 and fell back to 1. object={name}", this);
            _candidateBufferSize = 1;
        }

        if (_candidateBuffer != null && _candidateBuffer.Length == _candidateBufferSize)
        {
            return;
        }

        _candidateBuffer = new Collider2D[_candidateBufferSize];
    }

    /// <summary>
    /// Refreshes the reusable fallback scan contact filter from inspector values.
    /// </summary>
    private void RefreshContactFilter()
    {
        _playerContactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _playerLayerMask,
            useTriggers = Physics2D.queriesHitTriggers
        };
    }
}
