using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Executes Pattern 2 by warning under the nearest valid Player and then spawning a temporary ground spike hit.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossGroundSpikePattern : BossPatternBase
{
    [Header("Required References")]
    [Tooltip("Boss controller that owns authority, pattern data, and Player search.")]
    [SerializeField] private BossController _bossController; // Boss authority and shared data source for Pattern 2.

    [Header("Execution")]
    [Tooltip("Range used to search the nearest Player when Pattern 2 starts.")]
    [Min(0f)]
    [SerializeField] private float _executionRange = 20f; // Player search range used only at Pattern 2 execution time.

    [Tooltip("Maximum Collider2D candidates checked by one spike hit without allocating execution-time arrays.")]
    [Min(1)]
    [SerializeField] private int _maxHitColliderCandidates = 16; // Non-alloc hit detection buffer size used by Pattern 2.

    private Coroutine _executionCoroutine; // Running Pattern 2 coroutine that owns warning delay and hit duration.
    private Collider2D _activeSpikeHitCollider; // Spike hit collider currently enabled for Pattern 2 damage timing.
    private int _nextSpikeHitSerial; // Incrementing hit serial used to build unique HitRequest ids for Pattern 2.
    private bool _hasLoggedSpikeObjectPoolFallback; // Prevents repeated direct Instantiate fallback warnings for spike spawning.
    private bool _hasLoggedNetworkSpawnFallback; // Prevents repeated NetworkObject spawn fallback warnings from this pattern.

    private readonly List<Collider2D> _hitColliderList = new List<Collider2D>(16);
    private readonly List<HitReceiver> _hitReceiverList = new List<HitReceiver>(16);
    private ContactFilter2D _spikeHitFilter;

    /// <summary>
    /// Resolves required runtime references before Pattern 2 starts.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Corrects invalid Pattern 2 inspector values and refreshes references.
    /// </summary>
    private void OnValidate()
    {
        if (_executionRange < 0f)
        {
            Debug.LogWarning($"[BossGroundSpikePattern] ExecutionRange was below zero and clamped. object={name}, value={_executionRange}", this);
            _executionRange = 0f;
        }

        if (_maxHitColliderCandidates < 1)
        {
            Debug.LogWarning($"[BossGroundSpikePattern] MaxHitColliderCandidates was less than 1 and clamped. object={name}, value={_maxHitColliderCandidates}", this);
            _maxHitColliderCandidates = 1;
        }

        ResolveReferences();
    }

    /// <summary>
    /// Starts Pattern 2 once through the common pattern execution API.
    /// </summary>
    protected override void OnPatternExecutionStarted()
    {
        ResolveReferences();

        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            ReportPatternFailed("PatternAuthorityMissing");
            return;
        }

        if (!TryGetSettings(out GroundSpikePatternSettings settings))
        {
            CancelPatternWithWarning("MissingGroundSpikeSettings");
            return;
        }

        if (settings.SpikePrefab == null)
        {
            CancelPatternWithWarning("SpikePrefabMissing");
            return;
        }

        if (!TryResolveTarget(out Transform targetTransform))
        {
            CancelPatternWithWarning("TargetPlayerMissing");
            return;
        }

        Vector3 spikePosition = ResolveSpikePosition(settings, targetTransform); // Final world position where the warning, spike, and attack VFX are played.
        _nextSpikeHitSerial = 0;
        _executionCoroutine = StartCoroutine(ExecuteSpikeSequence(settings, spikePosition));
    }

    /// <summary>
    /// Stops active Pattern 2 timing and disables active hit data when cancellation comes from BossController.
    /// </summary>
    protected override void OnPatternExecutionCancelled(string reason)
    {
        StopExecutionCoroutine();
        DisableActiveSpikeHitCollider();
    }

    /// <summary>
    /// Resolves Pattern 2 settings from the boss pattern data asset.
    /// </summary>
    private bool TryGetSettings(out GroundSpikePatternSettings settings)
    {
        settings = default;
        ResolveReferences();

        if (_bossController == null || _bossController.PatternData == null)
        {
            return false;
        }

        if (!_bossController.PatternData.TryGetGroundSpikePattern(_bossController.CurrentPatternId, out settings))
        {
            Debug.LogWarning($"[BossGroundSpikePattern] GroundSpike settings were not found for PatternId. object={name}, patternId={_bossController.CurrentPatternId}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Finds the nearest valid Player through the shared boss Player target provider.
    /// </summary>
    private bool TryResolveTarget(out Transform targetTransform)
    {
        targetTransform = null;
        if (_bossController == null)
        {
            return false;
        }

        return _bossController.TryFindNearestPlayerForExecution(_executionRange, out targetTransform, out _, out _);
    }

    /// <summary>
    /// Resolves the spike placement position by raycasting downward from above the target Player.
    /// </summary>
    private Vector3 ResolveSpikePosition(GroundSpikePatternSettings settings, Transform targetTransform)
    {
        Vector3 targetPosition = targetTransform.position; // Target Player position used as fallback spike placement.
        Vector2 raycastStart = new Vector2(targetPosition.x, targetPosition.y + settings.RaycastStartYOffset); // Raycast origin placed above the target Player.
        RaycastHit2D groundHit = Physics2D.Raycast(raycastStart, Vector2.down, settings.GroundRaycastDistance, settings.GroundLayerMask); // Ground hit result used to snap the spike to the floor.
        if (groundHit.collider != null)
        {
            return groundHit.point;
        }

        LogFailureOnce("GroundRaycastFailed");
        return targetPosition;
    }

    /// <summary>
    /// Runs warning presentation, spike creation, attack presentation, hit duration, and completion reporting.
    /// </summary>
    private IEnumerator ExecuteSpikeSequence(GroundSpikePatternSettings settings, Vector3 spikePosition)
    {
        PlaySynchronizedVfxOrWarn(settings.WarningEffectId, settings.WarningVfxPrefab, spikePosition, "WarningVFXMissing", true);

        if (settings.SpikeWarningDuration > 0f)
        {
            yield return new WaitForSeconds(settings.SpikeWarningDuration);
        }

        if (!TrySpawnSpike(settings.SpikePrefab, spikePosition, out GameObject spikeInstance))
        {
            _executionCoroutine = null;
            ReportPatternFailed("SpikeSpawnFailed");
            yield break;
        }

        PlaySynchronizedVfxOrWarn(settings.AttackEffectId, settings.AttackVfxPrefab, spikePosition, "AttackVFXMissing", false);
        _bossController.PlayPresentationCue(E_BossPresentationCue.PatternAttack, E_BossPatternType.GroundSpike, spikePosition);
        MarkPatternEffectApplied();
        EnableSpikeHitCollider(spikeInstance);
        ApplySpikeHit(settings, spikePosition);

        if (settings.SpikeHitDuration > 0f)
        {
            yield return new WaitForSeconds(settings.SpikeHitDuration);
        }

        DisableActiveSpikeHitCollider();
        _executionCoroutine = null;
        ReportPatternCompleted("GroundSpikeCompleted");
    }

    /// <summary>
    /// Plays Pattern 2 VFX locally or broadcasts it to clients when the boss pattern NetworkObject is spawned.
    /// </summary>
    private void PlaySynchronizedVfxOrWarn(E_EffectId effectId, GameObject vfxPrefab, Vector3 position, string missingReason, bool isWarningVfx)
    {
        NetworkManager networkManager = NetworkManager.Singleton; // Current NGO session used to decide whether RPC presentation sync is available.
        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;
        if (shouldUseNetwork && IsSpawned)
        {
            BroadcastGroundSpikeVfxRpc((int)effectId, position, isWarningVfx);
            return;
        }

        if (shouldUseNetwork && !IsSpawned)
        {
            LogFailureOnce("GroundSpikeVfxNetworkObjectNotSpawned");
        }

        PlayLocalVfxOrWarn(effectId, vfxPrefab, position, missingReason);
    }

    /// <summary>
    /// Receives server-confirmed Pattern 2 VFX playback and only performs local presentation on clients and host.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void BroadcastGroundSpikeVfxRpc(int effectIdValue, Vector3 position, bool isWarningVfx)
    {
        ResolveReferences();
        GroundSpikePatternSettings settings = default; // Local settings copy used only to resolve optional prefab fallback presentation.
        if (_bossController != null && _bossController.PatternData != null)
        {
            _bossController.PatternData.TryGetGroundSpikePattern(_bossController.CurrentPatternId, out settings);
        }
        GameObject fallbackPrefab = isWarningVfx ? settings.WarningVfxPrefab : settings.AttackVfxPrefab; // Optional prefab fallback when EffectService id is not configured.
        string missingReason = isWarningVfx ? "WarningVFXMissing" : "AttackVFXMissing"; // Warning reason reported if neither EffectService id nor prefab fallback is available.
        PlayLocalVfxOrWarn((E_EffectId)effectIdValue, fallbackPrefab, position, missingReason);
    }

    /// <summary>
    /// Plays a Pattern 2 VFX through EffectService first and falls back to prefab instantiation only when no EffectId is configured.
    /// </summary>
    private void PlayLocalVfxOrWarn(E_EffectId effectId, GameObject vfxPrefab, Vector3 position, string missingReason)
    {
        if (effectId != E_EffectId.None)
        {
            if (EffectService.Instance == null)
            {
                LogFailureOnce("EffectServiceMissing");
            }
            else
            {
                EffectService.Instance.Play(effectId, position);
                return;
            }
        }

        if (vfxPrefab == null)
        {
            LogFailureOnce(missingReason);
            return;
        }

        LogFailureOnce("VfxPrefabFallbackUsed");
        Instantiate(vfxPrefab, position, Quaternion.identity);
    }

    /// <summary>
    /// Spawns the spike object on the authority instance and spawns its NetworkObject when the prefab requires network replication.
    /// </summary>
    private bool TrySpawnSpike(GameObject spikePrefab, Vector3 spikePosition, out GameObject spikeInstance)
    {
        spikeInstance = null;
        if (spikePrefab == null)
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // Current NGO session used to decide whether NetworkObject Spawn is required.
        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;
        NetworkObject prefabNetworkObject = spikePrefab.GetComponent<NetworkObject>(); // NetworkObject marker that decides network spawn flow.

        if (shouldUseNetwork && !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("SpikeSpawnRequestedWithoutAuthority");
            return false;
        }

        LogSpikeObjectPoolFallbackOnce();
        spikeInstance = Instantiate(spikePrefab, spikePosition, Quaternion.identity);
        if (spikeInstance == null)
        {
            return false;
        }

        if (!shouldUseNetwork)
        {
            return true;
        }

        if (prefabNetworkObject == null)
        {
            LogNetworkSpawnFallbackOnce("Spike prefab has no NetworkObject, so only the authority instance created the spike presentation.");
            return true;
        }

        NetworkObject spawnedNetworkObject = spikeInstance.GetComponent<NetworkObject>(); // Runtime NetworkObject instance spawned by the authority.
        if (spawnedNetworkObject == null)
        {
            LogFailureOnce("SpawnedSpikeNetworkObjectMissing");
            return true;
        }

        if (!spawnedNetworkObject.IsSpawned)
        {
            LogNetworkSpawnFallbackOnce("NetworkObject Pool was not found; Pattern 2 spawned the spike with NGO Instantiate plus Spawn.");
            spawnedNetworkObject.Spawn(true);
        }

        return true;
    }

    /// <summary>
    /// Enables the first available Collider2D on the spawned spike as the temporary attack hit.
    /// </summary>
    private void EnableSpikeHitCollider(GameObject spikeInstance)
    {
        if (spikeInstance == null)
        {
            LogFailureOnce("SpikeInstanceMissingForHitCollider");
            return;
        }

        _activeSpikeHitCollider = spikeInstance.GetComponent<Collider2D>();
        if (_activeSpikeHitCollider == null)
        {
            _activeSpikeHitCollider = spikeInstance.GetComponentInChildren<Collider2D>();
        }

        if (_activeSpikeHitCollider == null)
        {
            LogFailureOnce("SpikeHitColliderMissing");
            return;
        }

        _activeSpikeHitCollider.enabled = true;
    }

    /// <summary>
    /// Applies Pattern 2 spike damage to HitReceiver targets found by the configured overlap box.
    /// </summary>
    private void ApplySpikeHit(GroundSpikePatternSettings settings, Vector3 spikePosition)
    {
        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("SpikeHitAuthorityMissing");
            return;
        }

        _hitColliderList.Clear();
        _hitReceiverList.Clear();

        ConfigureSpikeHitFilter(settings.SpikeTargetLayerMask);

        Physics2D.OverlapBox(
        spikePosition,
        settings.BoxSize,
        0f,
        _spikeHitFilter,
        _hitColliderList);

        for (int index = 0; index < _hitColliderList.Count; index++)
        {
            HitReceiver receiver = ResolveHitReceiver(_hitColliderList[index]);
            if (receiver == null || _hitReceiverList.Contains(receiver))
            {
                continue;
            }

            _hitReceiverList.Add(receiver);
        }

        for (int i = 0; i < _hitReceiverList.Count; i++)
        {
            SendSpikeHit(settings, spikePosition, _hitReceiverList[i]);
        }

        _hitReceiverList.Clear();
    }

    /// <summary>
    /// Sends one HitRequest through the existing HitReceiver and HealthComponent damage path.
    /// </summary>
    private void SendSpikeHit(GroundSpikePatternSettings settings, Vector3 spikePosition, HitReceiver receiver)
    {
        Vector3 targetPosition = receiver.transform.position; // Target position used as HitPoint and direction source.
        Vector3 hitDirection = targetPosition - spikePosition; // Direction from spike origin to the target.
        if (hitDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            hitDirection = Vector3.up;
            LogFailureOnce("SpikeHitDirectionFallback");
        }
        else
        {
            hitDirection.Normalize();
        }

        string hitId = BuildSpikeHitId(receiver);
        HitRequest request = new HitRequest(
            hitId: hitId,
            rawDamage: settings.SpikeDamage,
            attacker: gameObject,
            hitPoint: targetPosition,
            hitDirection: hitDirection,
            statusTag: settings.StatusTag,
            requestTime: Time.time);

        receiver.ReceiveHit(request);
    }

    /// <summary>
    /// Builds a unique Pattern 2 HitId so the existing HitReceiver duplicate guard remains active.
    /// </summary>
    private string BuildSpikeHitId(HitReceiver receiver)
    {
        int receiverId = receiver != null ? receiver.gameObject.GetInstanceID() : 0; // Receiver instance id included in the hit id for duplicate protection.
        int executionId = CurrentExecutionId; // Current BossPatternBase execution id used to scope duplicate protection.
        int hitSerial = _nextSpikeHitSerial; // Per-execution serial used when several receivers are hit.
        _nextSpikeHitSerial++;
        return $"{gameObject.GetInstanceID()}:{executionId}:{receiverId}:GroundSpike:{hitSerial}";
    }

    /// <summary>
    /// Resolves a HitReceiver from a collider by reusing the existing HitSystem receiver component.
    /// </summary>
    private HitReceiver ResolveHitReceiver(Collider2D candidateCollider)
    {
        if (candidateCollider == null)
        {
            return null;
        }

        HitReceiver receiver = candidateCollider.GetComponent<HitReceiver>(); // Direct receiver on the collider object.
        if (receiver != null)
        {
            return receiver;
        }

        receiver = candidateCollider.GetComponentInParent<HitReceiver>();
        if (receiver != null)
        {
            return receiver;
        }

        return candidateCollider.GetComponentInChildren<HitReceiver>();
    }

    /// <summary>
    /// Disables the active spike hit collider when the hit duration ends or the pattern is cancelled.
    /// </summary>
    private void DisableActiveSpikeHitCollider()
    {
        if (_activeSpikeHitCollider == null)
        {
            return;
        }

        _activeSpikeHitCollider.enabled = false;
        _activeSpikeHitCollider = null;
    }

    /// <summary>
    /// Stops the running execution coroutine and clears the stored handle.
    /// </summary>
    private void StopExecutionCoroutine()
    {
        if (_executionCoroutine == null)
        {
            return;
        }

        StopCoroutine(_executionCoroutine);
        _executionCoroutine = null;
    }

    /// <summary>
    /// Logs the missing NetworkObject pool fallback once for this pattern instance.
    /// </summary>
    private void LogNetworkSpawnFallbackOnce(string message)
    {
        if (_hasLoggedNetworkSpawnFallback)
        {
            return;
        }

        Debug.LogWarning($"[BossGroundSpikePattern] {message} object={name}", this);
        _hasLoggedNetworkSpawnFallback = true;
    }

    /// <summary>
    /// Logs the direct spike Instantiate fallback once because this repository has no reusable generic ObjectPool for spike objects.
    /// </summary>
    private void LogSpikeObjectPoolFallbackOnce()
    {
        if (_hasLoggedSpikeObjectPoolFallback)
        {
            return;
        }

        Debug.LogWarning($"[BossGroundSpikePattern] No reusable spike ObjectPool was found; Pattern 2 uses direct Instantiate for SpikePrefab. object={name}", this);
        _hasLoggedSpikeObjectPoolFallback = true;
    }

    /// <summary>
    /// Reports a cancellation reason with a one-time warning for this execution.
    /// </summary>
    private void CancelPatternWithWarning(string reason)
    {
        LogFailureOnce(reason);
        ReportPatternCancelled(reason);
    }

    /// <summary>
    /// Resolves optional references from the same boss GameObject.
    /// </summary>
    private void ResolveReferences()
    {
        if (_bossController == null)
        {
            _bossController = GetComponent<BossController>();
        }
    }

    private void ConfigureSpikeHitFilter(LayerMask targetLayerMask)
    {
        _spikeHitFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = targetLayerMask,
            useTriggers = true
        };
    }
}
