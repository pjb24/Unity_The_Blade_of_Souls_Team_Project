using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Executes Pattern 4 entry and Groggy transitions without creating weak point objects yet.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossWeakPointPattern : BossPatternBase
{
    [Header("Required References")]
    [Tooltip("Boss controller that owns authority, Pattern 4 state flags, and Groggy timing.")]
    [SerializeField] private BossController _bossController; // Boss authority and shared Pattern 4 state owner.

    [Tooltip("Scene anchor set that provides weak point placement areas.")]
    [SerializeField] private BossPatternAnchorSet _anchorSet; // Scene weak point area source used by Pattern 4 position selection.

    private Coroutine _entryFallbackCoroutine; // Coroutine that completes Pattern 4 entry if the animation event is missing.
    private Coroutine _weakPointTimeLimitCoroutine; // Coroutine that resolves Pattern 4 when weak points are not destroyed in time.
    private Vector3[] _weakPointPositionBuffer; // Reusable selected weak point world position buffer.
    private BoxCollider2D[] _weakPointAreaBuffer; // Reusable valid weak point area reference buffer.
    private BossWeakPointObject[] _weakPointObjectBuffer; // Reusable spawned weak point reference buffer.
    private bool[] _weakPointDestroyedBuffer; // Reusable weak point destruction state buffer.
    private HealthComponent[] _timeLimitDamageTargetBuffer; // Reusable Player HealthComponent buffer damaged when Pattern 4 times out.
    private BossPatternData _bufferPatternData; // PatternData instance used when the current buffers were allocated.
    private string _activePatternId = string.Empty; // PatternId captured when Pattern 4 entry starts and reused by post-entry timers.
    private int _selectedWeakPointPositionCount; // Number of valid weak point positions selected for the current execution.
    private int _spawnedWeakPointCount; // Number of weak point objects spawned for the active Pattern 4 flow.
    private int _destroyedWeakPointCount; // Number of spawned weak points destroyed in the active Pattern 4 flow.
    private int _validWeakPointAreaCount; // Number of non-null WeakPointAreas copied into the reusable area buffer.
    private bool _isEntryResolved; // Whether the current Pattern 4 entry already completed or failed.
    private bool _isWeakPointFlowResolved; // Whether the active weak point phase has already timed out or entered Groggy.
    private bool _hasLoggedWeakPointInstantiateFallback; // Prevents repeated weak point Instantiate fallback warnings.
    private bool _hasLoggedWeakPointNetworkPoolFallback; // Prevents repeated weak point NetworkObject pool fallback warnings.

    /// <summary>
    /// Gets the number of selected weak point positions from the current execution.
    /// </summary>
    public int SelectedWeakPointPositionCount => _selectedWeakPointPositionCount;

    /// <summary>
    /// Resolves required runtime references before Pattern 4 starts.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Refreshes Pattern 4 references while designers edit the boss object.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Starts Pattern 4 entry and waits for the animation event or fallback timer.
    /// </summary>
    protected override void OnPatternExecutionStarted()
    {
        ResolveReferences();

        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            ReportPatternFailed("PatternAuthorityMissing");
            return;
        }

        _isEntryResolved = false;
        _isWeakPointFlowResolved = false;
        _activePatternId = _bossController.CurrentPatternId;
        if (!TryGetSettings(out WeakPointPatternSettings settings))
        {
            FailEntry("MissingWeakPointSettings");
            return;
        }

        if (settings.WeakPointPrefab == null)
        {
            FailEntry("WeakPointPrefabMissing");
            return;
        }

        if (!TryPrepareWeakPointPositions(settings))
        {
            FailEntry("WeakPointPositionSelectionFailed");
            return;
        }

        _bossController.NotifyPatternFourEntryStarted();
        StartEntryFallbackTimer(settings.EntryAnimationFallbackSeconds);
    }

    /// <summary>
    /// Stops Pattern 4 entry fallback timing when the common cancellation API cancels this pattern.
    /// </summary>
    protected override void OnPatternExecutionCancelled(string reason)
    {
        StopEntryFallbackTimer();
        StopWeakPointTimeLimitTimer();
        RemoveRemainingWeakPoints();
        ClearWeakPointRuntimeBuffers();
        _isEntryResolved = true;
        _isWeakPointFlowResolved = true;
    }

    /// <summary>
    /// Completes Pattern 4 entry from an animation event.
    /// </summary>
    public void AnimationEvent_WeakPointEntryCompleted()
    {
        CompleteEntry(false);
    }

    /// <summary>
    /// Fails Pattern 4 entry from an animation event.
    /// </summary>
    public void AnimationEvent_WeakPointEntryFailed()
    {
        FailEntry("WeakPointEntryAnimationEventFailed");
    }

    /// <summary>
    /// Enters Groggy for the configured GroggyDuration.
    /// </summary>
    public void EnterGroggy()
    {
        ResolveReferences();
        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("GroggyAuthorityMissing");
            return;
        }

        if (!TryGetSettings(out WeakPointPatternSettings settings))
        {
            LogFailureOnce("MissingWeakPointSettingsForGroggy");
            return;
        }

        _bossController.StartGroggyForDuration(settings.GroggyDurationSeconds, "Pattern4Groggy");
    }

    /// <summary>
    /// Stops every active Pattern 4 runtime object and timer when the boss dies.
    /// </summary>
    public void CleanupForBossDeath()
    {
        ResolveReferences();
        if (_bossController != null && !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("WeakPointDeathCleanupWithoutAuthority");
            return;
        }

        StopEntryFallbackTimer();
        StopWeakPointTimeLimitTimer();
        RemoveRemainingWeakPoints();
        ClearWeakPointRuntimeBuffers();
        _isEntryResolved = true;
        _isWeakPointFlowResolved = true;
    }

    /// <summary>
    /// Receives a weak point destruction report from a spawned weak point object.
    /// </summary>
    public void HandleWeakPointDestroyed(BossWeakPointObject weakPointObject, int weakPointIndex)
    {
        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("WeakPointDestroyedWithoutAuthority");
            return;
        }

        if (weakPointIndex < 0 || weakPointIndex >= _selectedWeakPointPositionCount)
        {
            LogFailureOnce("WeakPointDestroyedIndexInvalid");
            return;
        }

        if (_weakPointObjectBuffer == null || _weakPointObjectBuffer[weakPointIndex] != weakPointObject)
        {
            LogFailureOnce("WeakPointDestroyedReferenceMismatch");
            return;
        }

        if (_weakPointDestroyedBuffer[weakPointIndex])
        {
            return;
        }

        _weakPointDestroyedBuffer[weakPointIndex] = true;
        _destroyedWeakPointCount++;

        Vector3 destroyPosition = weakPointObject != null ? weakPointObject.transform.position : _weakPointPositionBuffer[weakPointIndex]; // Position used for destruction VFX.
        _bossController.PlayPresentationCue(E_BossPresentationCue.WeakPointDestroyed, E_BossPatternType.WeakPoint, destroyPosition);
        PlayWeakPointDestroyVfx(destroyPosition);
        CleanupWeakPointObject(weakPointObject, weakPointIndex);

        if (AreAllWeakPointsDestroyed())
        {
            ResolveAllWeakPointsDestroyed();
        }
    }

    /// <summary>
    /// Resolves Pattern 4 settings from the boss pattern data asset.
    /// </summary>
    private bool TryGetSettings(out WeakPointPatternSettings settings)
    {
        settings = default;
        ResolveReferences();

        if (_bossController == null || _bossController.PatternData == null)
        {
            return false;
        }

        string patternId = !string.IsNullOrWhiteSpace(_activePatternId) ? _activePatternId : _bossController.CurrentPatternId; // Captured PatternId survives after entry completion clears BossController current pattern state.
        if (!_bossController.PatternData.TryGetWeakPointPattern(patternId, out settings))
        {
            Debug.LogWarning($"[BossWeakPointPattern] WeakPoint settings were not found for PatternId. object={name}, patternId={patternId}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a selected weak point position from the current execution.
    /// </summary>
    public bool TryGetSelectedWeakPointPosition(int index, out Vector3 position)
    {
        position = Vector3.zero;
        if (index < 0 || index >= _selectedWeakPointPositionCount || _weakPointPositionBuffer == null)
        {
            Debug.LogWarning($"[BossWeakPointPattern] Selected weak point position index out of range. object={name}, index={index}, count={_selectedWeakPointPositionCount}", this);
            return false;
        }

        position = _weakPointPositionBuffer[index];
        return true;
    }

    /// <summary>
    /// Validates weak point areas and selects positions inside those areas.
    /// </summary>
    private bool TryPrepareWeakPointPositions(WeakPointPatternSettings settings)
    {
        ResolveReferences();
        _selectedWeakPointPositionCount = 0;
        _validWeakPointAreaCount = 0;

        if (_anchorSet == null)
        {
            LogFailureOnce("WeakPointAnchorSetMissing");
            return false;
        }

        BoxCollider2D[] weakPointAreas = _anchorSet.WeakPointAreas; // Scene-authored weak point area references.
        if (weakPointAreas == null)
        {
            LogFailureOnce("WeakPointAreasNull");
            return false;
        }

        if (weakPointAreas.Length == 0)
        {
            LogFailureOnce("WeakPointAreasEmpty");
            return false;
        }

        EnsureWeakPointBuffers(settings, weakPointAreas.Length);
        CollectValidWeakPointAreas(weakPointAreas);
        if (_validWeakPointAreaCount <= 0)
        {
            LogFailureOnce("WeakPointAreasValidEntryMissing");
            return false;
        }

        int targetWeakPointCount = settings.WeakPointCount; // Requested weak point count; several weak points may use different positions inside the same valid area.
        if (targetWeakPointCount <= 0)
        {
            LogFailureOnce("WeakPointCountZero");
            return false;
        }

        SelectWeakPointPositions(settings, targetWeakPointCount);
        if (_selectedWeakPointPositionCount <= 0)
        {
            LogFailureOnce("WeakPointSelectablePositionMissing");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ensures weak point selection buffers match current PatternData and area requirements.
    /// </summary>
    private void EnsureWeakPointBuffers(WeakPointPatternSettings settings, int weakPointAreaCount)
    {
        BossPatternData currentPatternData = _bossController != null ? _bossController.PatternData : null; // PatternData instance used to detect data replacement.
        int requiredPositionCount = Mathf.Max(1, settings.WeakPointCount); // Required selected-position buffer length.
        int requiredAreaCount = Mathf.Max(1, weakPointAreaCount); // Required valid-area buffer length.
        bool shouldRebuildBuffers = _bufferPatternData != currentPatternData;

        if (_weakPointPositionBuffer == null || _weakPointPositionBuffer.Length != requiredPositionCount)
        {
            shouldRebuildBuffers = true;
        }

        if (_weakPointAreaBuffer == null || _weakPointAreaBuffer.Length != requiredAreaCount)
        {
            shouldRebuildBuffers = true;
        }

        if (!shouldRebuildBuffers)
        {
            ClearWeakPointRuntimeBuffers();
            return;
        }

        _weakPointPositionBuffer = new Vector3[requiredPositionCount];
        _weakPointAreaBuffer = new BoxCollider2D[requiredAreaCount];
        _weakPointObjectBuffer = new BossWeakPointObject[requiredPositionCount];
        _weakPointDestroyedBuffer = new bool[requiredPositionCount];
        _bufferPatternData = currentPatternData;
    }

    /// <summary>
    /// Clears reusable weak point object and destruction buffers before a new execution uses them.
    /// </summary>
    private void ClearWeakPointRuntimeBuffers()
    {
        if (_weakPointObjectBuffer != null)
        {
            for (int index = 0; index < _weakPointObjectBuffer.Length; index++)
            {
                _weakPointObjectBuffer[index] = null;
            }
        }

        if (_weakPointDestroyedBuffer != null)
        {
            for (int index = 0; index < _weakPointDestroyedBuffer.Length; index++)
            {
                _weakPointDestroyedBuffer[index] = false;
            }
        }

        _spawnedWeakPointCount = 0;
        _destroyedWeakPointCount = 0;
    }

    /// <summary>
    /// Copies non-null WeakPointAreas into the reusable valid area buffer.
    /// </summary>
    private void CollectValidWeakPointAreas(BoxCollider2D[] weakPointAreas)
    {
        for (int index = 0; index < weakPointAreas.Length; index++)
        {
            BoxCollider2D area = weakPointAreas[index]; // Current scene weak point area candidate.
            if (area == null)
            {
                LogFailureOnce("WeakPointAreaNullEntry");
                continue;
            }

            _weakPointAreaBuffer[_validWeakPointAreaCount] = area;
            _validWeakPointAreaCount++;
        }
    }

    /// <summary>
    /// Selects weak point positions inside valid BoxCollider2D areas with minimum distance validation.
    /// </summary>
    private void SelectWeakPointPositions(WeakPointPatternSettings settings, int targetWeakPointCount)
    {
        float minDistanceSqr = settings.MinDistanceBetweenWeakPoints * settings.MinDistanceBetweenWeakPoints; // Squared minimum spacing used for position validation.
        int retryCount = Mathf.Max(1, settings.WeakPointPositionRetryCount);

        for (int index = 0; index < targetWeakPointCount; index++)
        {
            if (!TrySelectOneWeakPointPosition(retryCount, minDistanceSqr, out Vector3 selectedPosition))
            {
                LogFailureOnce("WeakPointPositionRetryExceeded");
                continue;
            }

            _weakPointPositionBuffer[_selectedWeakPointPositionCount] = selectedPosition;
            _selectedWeakPointPositionCount++;
        }
    }

    /// <summary>
    /// Tries to select one valid weak point position within retry limits.
    /// </summary>
    private bool TrySelectOneWeakPointPosition(int retryCount, float minDistanceSqr, out Vector3 selectedPosition)
    {
        selectedPosition = Vector3.zero;
        for (int retryIndex = 0; retryIndex < retryCount; retryIndex++)
        {
            BoxCollider2D area = _weakPointAreaBuffer[Random.Range(0, _validWeakPointAreaCount)]; // Valid area randomly chosen for this retry.
            Vector3 candidatePosition = GetRandomPointInAreaBounds(area);
            if (!area.OverlapPoint(candidatePosition))
            {
                continue;
            }

            if (!IsFarEnoughFromSelectedWeakPoints(candidatePosition, minDistanceSqr))
            {
                continue;
            }

            selectedPosition = candidatePosition;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates a random world position inside a BoxCollider2D bounds rectangle.
    /// </summary>
    private Vector3 GetRandomPointInAreaBounds(BoxCollider2D area)
    {
        Bounds bounds = area.bounds; // World bounds used for random candidate generation.
        float x = Random.Range(bounds.min.x, bounds.max.x);
        float y = Random.Range(bounds.min.y, bounds.max.y);
        return new Vector3(x, y, area.transform.position.z);
    }

    /// <summary>
    /// Returns whether a candidate position satisfies the minimum spacing from existing weak point positions.
    /// </summary>
    private bool IsFarEnoughFromSelectedWeakPoints(Vector3 candidatePosition, float minDistanceSqr)
    {
        if (minDistanceSqr <= 0f)
        {
            return true;
        }

        for (int index = 0; index < _selectedWeakPointPositionCount; index++)
        {
            if ((candidatePosition - _weakPointPositionBuffer[index]).sqrMagnitude < minDistanceSqr)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Starts the animation event fallback timer for Pattern 4 entry completion.
    /// </summary>
    private void StartEntryFallbackTimer(float fallbackSeconds)
    {
        StopEntryFallbackTimer();

        if (fallbackSeconds < 0f)
        {
            LogFailureOnce("EntryFallbackDurationRuntimeClamp");
            fallbackSeconds = 0f;
        }

        _entryFallbackCoroutine = StartCoroutine(RunEntryFallbackTimer(fallbackSeconds));
    }

    /// <summary>
    /// Waits for the entry animation event and completes entry with a Warning if the event never arrives.
    /// </summary>
    private IEnumerator RunEntryFallbackTimer(float fallbackSeconds)
    {
        if (fallbackSeconds > 0f)
        {
            yield return new WaitForSeconds(fallbackSeconds);
        }

        _entryFallbackCoroutine = null;
        CompleteEntry(true);
    }

    /// <summary>
    /// Completes Pattern 4 entry and activates weak point state flags without creating weak points.
    /// </summary>
    private void CompleteEntry(bool usedFallback)
    {
        if (_isEntryResolved)
        {
            return;
        }

        if (!IsExecuting)
        {
            LogFailureOnce("WeakPointEntryCompletedWhileNotExecuting");
            return;
        }

        StopEntryFallbackTimer();

        if (usedFallback)
        {
            LogFailureOnce("WeakPointEntryAnimationEventFallback");
        }

        if (_bossController == null)
        {
            ReportPatternFailed("BossControllerMissingOnWeakPointEntryComplete");
            return;
        }

        if (!TryGetSettings(out WeakPointPatternSettings settings))
        {
            FailEntry("MissingWeakPointSettingsOnEntryComplete");
            return;
        }

        if (!SpawnWeakPoints(settings))
        {
            FailEntry("WeakPointSpawnFailed");
            return;
        }

        _isEntryResolved = true;
        _bossController.NotifyPatternFourEntryCompleted();
        _bossController.PlayPresentationCue(E_BossPresentationCue.PatternAttack, E_BossPatternType.WeakPoint, transform.position);
        MarkPatternEffectApplied();
        StartWeakPointTimeLimitTimer(settings.WeakPointTimeLimit);
        ReportPatternCompleted("WeakPointEntryCompleted");
    }

    /// <summary>
    /// Starts the Pattern 4 weak point time limit timer after entry has completed.
    /// </summary>
    private void StartWeakPointTimeLimitTimer(float timeLimitSeconds)
    {
        StopWeakPointTimeLimitTimer();

        float safeTimeLimit = timeLimitSeconds; // Runtime time limit clamped before starting the timeout coroutine.
        if (safeTimeLimit < 0f)
        {
            LogFailureOnce("WeakPointTimeLimitRuntimeClamp");
            safeTimeLimit = 0f;
        }

        _weakPointTimeLimitCoroutine = StartCoroutine(RunWeakPointTimeLimitTimer(safeTimeLimit));
    }

    /// <summary>
    /// Waits for WeakPointTimeLimit and resolves Pattern 4 as a timeout when weak points remain.
    /// </summary>
    private IEnumerator RunWeakPointTimeLimitTimer(float timeLimitSeconds)
    {
        if (timeLimitSeconds > 0f)
        {
            yield return new WaitForSeconds(timeLimitSeconds);
        }

        _weakPointTimeLimitCoroutine = null;
        ResolveWeakPointTimeOut();
    }

    /// <summary>
    /// Resolves Pattern 4 timeout as a boss-favorable result and damages every living Player.
    /// </summary>
    private void ResolveWeakPointTimeOut()
    {
        if (_isWeakPointFlowResolved)
        {
            return;
        }

        ResolveReferences();
        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("WeakPointTimeoutWithoutAuthority");
            return;
        }

        if (!TryGetSettings(out WeakPointPatternSettings settings))
        {
            LogFailureOnce("WeakPointTimeoutSettingsMissing");
            return;
        }

        _isWeakPointFlowResolved = true;
        StopWeakPointTimeLimitTimer();
        ApplyTimeLimitDamageToLivingPlayers(settings);
        RemoveRemainingWeakPoints();
        ClearWeakPointRuntimeBuffers();
        _bossController.NotifyPatternFourTimedOut();
    }

    /// <summary>
    /// Applies WeakPointTimeLimitDamage to every valid Player collected by the shared target provider.
    /// </summary>
    private void ApplyTimeLimitDamageToLivingPlayers(WeakPointPatternSettings settings)
    {
        if (settings.WeakPointTimeLimitDamage <= 0f)
        {
            LogFailureOnce("WeakPointTimeLimitDamageZero");
            return;
        }

        BossPlayerTargetProvider targetProvider = _bossController.PlayerTargetProvider; // Shared Player discovery path reused for timeout damage.
        if (targetProvider == null)
        {
            LogFailureOnce("WeakPointTimeoutTargetProviderMissing");
            return;
        }

        EnsureTimeLimitDamageTargetBuffer(targetProvider);
        int targetCount = targetProvider.CollectAlivePlayersForExecution(_timeLimitDamageTargetBuffer);
        for (int index = 0; index < targetCount; index++)
        {
            HealthComponent targetHealth = _timeLimitDamageTargetBuffer[index]; // Valid Player HealthComponent collected by the provider.
            _timeLimitDamageTargetBuffer[index] = null;
            if (!IsDamageTargetStillValid(targetHealth))
            {
                continue;
            }

            DamageContext damageContext = new DamageContext(
                settings.WeakPointTimeLimitDamage,
                gameObject,
                "BossPattern4TimeLimit",
                false,
                true,
                E_DamageType.True);
            targetHealth.ApplyDamage(damageContext);
        }
    }

    /// <summary>
    /// Ensures the Player timeout damage buffer matches the shared target provider capacity.
    /// </summary>
    private void EnsureTimeLimitDamageTargetBuffer(BossPlayerTargetProvider targetProvider)
    {
        int requiredLength = Mathf.Max(1, targetProvider.PlayerHealthBufferSize); // Provider-authored target buffer capacity.
        if (_timeLimitDamageTargetBuffer != null && _timeLimitDamageTargetBuffer.Length == requiredLength)
        {
            return;
        }

        _timeLimitDamageTargetBuffer = new HealthComponent[requiredLength];
    }

    /// <summary>
    /// Returns whether a collected Player target is still valid immediately before timeout damage is applied.
    /// </summary>
    private bool IsDamageTargetStillValid(HealthComponent targetHealth)
    {
        if (targetHealth == null || !targetHealth.isActiveAndEnabled || targetHealth.IsDead)
        {
            return false;
        }

        GameObject targetObject = targetHealth.gameObject; // Target object checked for inactive Player exclusion.
        return targetObject.activeInHierarchy && targetHealth.GetCurrentHealth() > 0f;
    }

    /// <summary>
    /// Resolves Pattern 4 when every weak point has been destroyed and starts Groggy timing.
    /// </summary>
    private void ResolveAllWeakPointsDestroyed()
    {
        if (_isWeakPointFlowResolved)
        {
            return;
        }

        _isWeakPointFlowResolved = true;
        StopWeakPointTimeLimitTimer();
        ClearWeakPointRuntimeBuffers();
        _bossController.NotifyPatternFourAllWeakPointsDestroyed();
        EnterGroggy();
    }

    /// <summary>
    /// Spawns weak point objects at the selected positions and stores their bridge references.
    /// </summary>
    private bool SpawnWeakPoints(WeakPointPatternSettings settings)
    {
        if (settings.WeakPointPrefab == null)
        {
            LogFailureOnce("WeakPointPrefabMissing");
            return false;
        }

        _spawnedWeakPointCount = 0;
        _destroyedWeakPointCount = 0;

        for (int index = 0; index < _selectedWeakPointPositionCount; index++)
        {
            if (!TrySpawnWeakPoint(settings.WeakPointPrefab, _weakPointPositionBuffer[index], index, out BossWeakPointObject weakPointObject))
            {
                continue;
            }

            _weakPointObjectBuffer[index] = weakPointObject;
            _weakPointDestroyedBuffer[index] = false;
            _spawnedWeakPointCount++;
        }

        if (_spawnedWeakPointCount <= 0)
        {
            LogFailureOnce("WeakPointSpawnedCountZero");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Spawns one weak point object through the explicit prefab fallback and wires existing Health/Hit components.
    /// </summary>
    private bool TrySpawnWeakPoint(GameObject weakPointPrefab, Vector3 position, int weakPointIndex, out BossWeakPointObject weakPointObject)
    {
        weakPointObject = null;
        if (weakPointPrefab == null)
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // NGO session singleton used to decide network spawn behavior.
        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;
        if (shouldUseNetwork && (_bossController == null || !_bossController.IsBossLogicAuthority()))
        {
            LogFailureOnce("WeakPointNetworkSpawnWithoutAuthority");
            return false;
        }

        LogWeakPointInstantiateFallbackOnce();
        GameObject spawnedObject = Instantiate(weakPointPrefab, position, Quaternion.identity); // Runtime weak point object spawned at a calculated valid position.
        if (spawnedObject == null)
        {
            LogFailureOnce("WeakPointInstantiateFailed");
            return false;
        }

        weakPointObject = EnsureWeakPointBridge(spawnedObject);
        weakPointObject.Initialize(this, weakPointIndex);
        SpawnWeakPointNetworkObjectIfNeeded(weakPointPrefab, spawnedObject, shouldUseNetwork);
        _bossController.PlayPresentationCue(E_BossPresentationCue.WeakPointCreated, E_BossPatternType.WeakPoint, position);
        return true;
    }

    /// <summary>
    /// Ensures a spawned weak point has the bridge component that reports Health death to Pattern 4.
    /// </summary>
    private BossWeakPointObject EnsureWeakPointBridge(GameObject spawnedObject)
    {
        BossWeakPointObject weakPointObject = spawnedObject.GetComponent<BossWeakPointObject>(); // Existing or runtime-added weak point bridge.
        if (weakPointObject != null)
        {
            return weakPointObject;
        }

        Debug.LogWarning($"[BossWeakPointPattern] BossWeakPointObject was missing and added at runtime. object={spawnedObject.name}", spawnedObject);
        return spawnedObject.AddComponent<BossWeakPointObject>();
    }

    /// <summary>
    /// Spawns the weak point NetworkObject on Host or Server when the prefab requires network replication.
    /// </summary>
    private void SpawnWeakPointNetworkObjectIfNeeded(GameObject weakPointPrefab, GameObject spawnedObject, bool shouldUseNetwork)
    {
        if (!shouldUseNetwork || weakPointPrefab.GetComponent<NetworkObject>() == null)
        {
            return;
        }

        NetworkObject spawnedNetworkObject = spawnedObject.GetComponent<NetworkObject>(); // Runtime NetworkObject attached to the spawned weak point.
        if (spawnedNetworkObject == null)
        {
            LogFailureOnce("SpawnedWeakPointNetworkObjectMissing");
            return;
        }

        if (spawnedNetworkObject.IsSpawned)
        {
            return;
        }

        LogWeakPointNetworkPoolFallbackOnce();
        spawnedNetworkObject.Spawn(true);
    }

    /// <summary>
    /// Plays the configured weak point destruction VFX through EffectService or prefab fallback.
    /// </summary>
    private void PlayWeakPointDestroyVfx(Vector3 position)
    {
        if (!TryGetSettings(out WeakPointPatternSettings settings))
        {
            LogFailureOnce("WeakPointDestroyVfxSettingsMissing");
            return;
        }

        if (settings.WeakPointDestroyEffectId != E_EffectId.None)
        {
            if (EffectService.Instance == null)
            {
                LogFailureOnce("WeakPointDestroyEffectServiceMissing");
            }
            else
            {
                EffectService.Instance.Play(settings.WeakPointDestroyEffectId, position);
                return;
            }
        }

        if (settings.WeakPointDestroyVfxPrefab == null)
        {
            LogFailureOnce("WeakPointDestroyVfxMissing");
            return;
        }

        LogFailureOnce("WeakPointDestroyVfxPrefabFallbackUsed");
        Instantiate(settings.WeakPointDestroyVfxPrefab, position, Quaternion.identity);
    }

    /// <summary>
    /// Cleans up a destroyed weak point NetworkObject or GameObject after its destruction has been reported.
    /// </summary>
    private void CleanupWeakPointObject(BossWeakPointObject weakPointObject, int weakPointIndex)
    {
        if (weakPointObject == null)
        {
            return;
        }

        weakPointObject.Release();
        _weakPointObjectBuffer[weakPointIndex] = null;

        NetworkObject networkObject = weakPointObject.GetComponent<NetworkObject>(); // Optional NetworkObject that must be despawned by authority.
        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn(true);
            return;
        }

        Destroy(weakPointObject.gameObject);
    }

    /// <summary>
    /// Removes all remaining weak points when Pattern 4 times out or is cancelled.
    /// </summary>
    private void RemoveRemainingWeakPoints()
    {
        if (_weakPointObjectBuffer == null)
        {
            return;
        }

        for (int index = 0; index < _weakPointObjectBuffer.Length; index++)
        {
            BossWeakPointObject weakPointObject = _weakPointObjectBuffer[index]; // Remaining spawned weak point object to clean up.
            if (weakPointObject == null)
            {
                continue;
            }

            CleanupWeakPointObject(weakPointObject, index);
        }
    }

    /// <summary>
    /// Returns whether every spawned weak point has reported destruction.
    /// </summary>
    private bool AreAllWeakPointsDestroyed()
    {
        if (_spawnedWeakPointCount <= 0 || _destroyedWeakPointCount < _spawnedWeakPointCount)
        {
            return false;
        }

        for (int index = 0; index < _selectedWeakPointPositionCount; index++)
        {
            if (_weakPointObjectBuffer[index] != null && !_weakPointDestroyedBuffer[index])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Logs direct weak point prefab Instantiate fallback once.
    /// </summary>
    private void LogWeakPointInstantiateFallbackOnce()
    {
        if (_hasLoggedWeakPointInstantiateFallback)
        {
            return;
        }

        Debug.LogWarning($"[BossWeakPointPattern] No WeakPoint ObjectPool was found. Pattern 4 uses WeakPointPrefab Instantiate fallback. object={name}", this);
        _hasLoggedWeakPointInstantiateFallback = true;
    }

    /// <summary>
    /// Logs missing NetworkObject Pool fallback once before using NGO Spawn directly.
    /// </summary>
    private void LogWeakPointNetworkPoolFallbackOnce()
    {
        if (_hasLoggedWeakPointNetworkPoolFallback)
        {
            return;
        }

        Debug.LogWarning($"[BossWeakPointPattern] NetworkObject Pool was not found. Host/Server will Instantiate WeakPointPrefab and call NetworkObject.Spawn. object={name}", this);
        _hasLoggedWeakPointNetworkPoolFallback = true;
    }

    /// <summary>
    /// Fails Pattern 4 entry and returns the boss flags to the non-weak-point state.
    /// </summary>
    private void FailEntry(string reason)
    {
        if (_isEntryResolved)
        {
            return;
        }

        _isEntryResolved = true;
        StopEntryFallbackTimer();
        StopWeakPointTimeLimitTimer();
        RemoveRemainingWeakPoints();
        ClearWeakPointRuntimeBuffers();

        if (_bossController != null)
        {
            _bossController.NotifyPatternFourEntryFailed();
        }

        ReportPatternFailed(reason);
    }

    /// <summary>
    /// Stops the entry fallback timer and clears the stored coroutine handle.
    /// </summary>
    private void StopEntryFallbackTimer()
    {
        if (_entryFallbackCoroutine == null)
        {
            return;
        }

        StopCoroutine(_entryFallbackCoroutine);
        _entryFallbackCoroutine = null;
    }

    /// <summary>
    /// Stops the weak point time limit timer and clears the stored coroutine handle.
    /// </summary>
    private void StopWeakPointTimeLimitTimer()
    {
        if (_weakPointTimeLimitCoroutine == null)
        {
            return;
        }

        StopCoroutine(_weakPointTimeLimitCoroutine);
        _weakPointTimeLimitCoroutine = null;
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

        if (_anchorSet == null && _bossController != null)
        {
            _anchorSet = _bossController.AnchorSet;
        }

        if (_anchorSet == null)
        {
            _anchorSet = GetComponent<BossPatternAnchorSet>();
        }
    }
}
