using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Controls boss battle authority, basic runtime state, and battle lifecycle entry points.
/// </summary>
[DisallowMultipleComponent]
public class BossController : NetworkBehaviour, IBossPatternExecutionListener
{
    [Header("Required References")]
    [Tooltip("Boss pattern configuration asset that stores pure pattern settings.")]
    [SerializeField] private BossPatternData _patternData; // ScriptableObject configuration used by future boss pattern logic.

    [Tooltip("Scene anchor set that stores projectile, monster, and weak point area references.")]
    [SerializeField] private BossPatternAnchorSet _anchorSet; // Scene reference holder used by future boss pattern logic.

    [Tooltip("Existing HealthComponent used as the boss health source.")]
    [SerializeField] private HealthComponent _healthComponent; // Existing health system component reused by the boss.

    [Tooltip("Existing HitReceiver used as the boss damage entry point.")]
    [SerializeField] private HitReceiver _hitReceiver; // Existing hit system component reused by the boss.

    [Tooltip("Common Player target provider used by boss patterns during execution.")]
    [SerializeField] private BossPlayerTargetProvider _playerTargetProvider; // Shared Player target search provider used by boss pattern execution.

    [Header("Common Pattern Settings")]
    [Tooltip("Additional common cooldown in seconds applied between future boss pattern requests.")]
    [Min(0f)]
    [SerializeField] private float _commonPatternCooldownSeconds; // Boss-wide common cooldown value for future pattern selection.

    [Header("Runtime State")]
    [Tooltip("Current high-level boss state for debugging and future presentation sync.")]
    [SerializeField] private E_BossState _currentState = E_BossState.None; // Current boss runtime state decided only by the authority instance.

    [Tooltip("Whether boss damage intake should currently be blocked by future hit handling.")]
    [SerializeField] private bool _isInvincible; // Runtime flag used by future hit handling to block damage intake.

    [Tooltip("Whether the weak point pattern is currently active.")]
    [SerializeField] private bool _isWeakPointPatternActive; // Runtime flag used by future weak point pattern handling.

    [Tooltip("Pattern type currently being executed by the boss.")]
    [SerializeField] private E_BossPatternType _currentPatternType = E_BossPatternType.None; // Runtime pattern type currently owned by the authority instance.

    [Tooltip("HealthPhase array index recorded when the current pattern execution was confirmed.")]
    [SerializeField] private int _currentPatternHealthPhaseIndex = -1; // HealthPhase index captured at pattern execution confirmation time.

    [Header("Debug")]
    [Tooltip("Whether missing required references should be reported during inspector validation.")]
    [SerializeField] private bool _warnMissingRequiredReferences = true; // Inspector validation warning toggle for missing references.

    private BossPatternBase _currentPattern; // Runtime pattern instance currently reporting lifecycle results to this controller.
    private Coroutine _patternSelectionCoroutine; // Coroutine handle reserved for future pattern selection timing.
    private Coroutine _currentPatternCoroutine; // Coroutine handle reserved for future active pattern execution.
    private Coroutine _commonCooldownCoroutine; // Coroutine handle reserved for future common cooldown timing.
    private Coroutine _groggyTimerCoroutine; // Coroutine handle used by Pattern 4 Groggy duration timing.
    private readonly BossPatternSelector _patternSelector = new BossPatternSelector(); // Reusable selector that owns candidate buffers for pattern selection.
    private int _healthPhaseUsageResetVersion; // Reset marker for future health phase usage counters.
    private int _individualCooldownResetVersion; // Reset marker for future per-pattern cooldown state.
    private float _globalCooldownEndTime; // Time.time timestamp when boss-wide pattern cooldown ends.
    private string _lastGlobalCooldownReason = string.Empty; // Last authority-side reason that started the boss-wide cooldown.
    private float[] _patternCooldownEndTimeByType; // Time.time timestamps indexed by E_BossPatternType for individual cooldown checks.
    private int[] _healthPhasePatternUseCounts; // Flattened use counts indexed by HealthPhaseSettings index and PatternCommonSettings index.
    private int _currentPatternCommonSettingsIndex = -1; // Common settings index captured at pattern execution confirmation time.
    private bool _isBattleActive; // Whether the boss battle lifecycle is currently active on the authority instance.
    private bool _isPatternSelectionEnabled; // Whether future pattern selection is allowed to run.
    private bool _isCurrentPatternListenerRegistered; // Whether this controller is registered to the current pattern report callbacks.
    private bool _hasLoggedAuthorityWarning; // Prevents repeated authority warning logs from the same controller state.
    private bool _hasLoggedHealthRatioFallbackWarning; // Prevents repeated health ratio fallback warnings from this controller state.
    private bool _hasLoggedHealthPhaseLookupWarning; // Prevents repeated HealthPhase lookup warnings from this controller state.
    private bool _hasLoggedCommonSettingsLookupWarning; // Prevents repeated common settings lookup warnings from this controller state.

    /// <summary>
    /// Gets the boss pattern data asset reference.
    /// </summary>
    public BossPatternData PatternData => _patternData;

    /// <summary>
    /// Gets the scene anchor set reference.
    /// </summary>
    public BossPatternAnchorSet AnchorSet => _anchorSet;

    /// <summary>
    /// Gets the existing health component reference.
    /// </summary>
    public HealthComponent HealthComponent => _healthComponent;

    /// <summary>
    /// Gets the existing hit receiver reference.
    /// </summary>
    public HitReceiver HitReceiver => _hitReceiver;

    /// <summary>
    /// Gets the shared Player target provider reference.
    /// </summary>
    public BossPlayerTargetProvider PlayerTargetProvider => _playerTargetProvider;

    /// <summary>
    /// Gets the boss-wide common pattern cooldown in seconds.
    /// </summary>
    public float CommonPatternCooldownSeconds => _commonPatternCooldownSeconds;

    /// <summary>
    /// Gets the Time.time timestamp when the boss-wide cooldown ends.
    /// </summary>
    public float GlobalCooldownEndTime => _globalCooldownEndTime;

    /// <summary>
    /// Gets the last recorded reason for starting the boss-wide cooldown.
    /// </summary>
    public string LastGlobalCooldownReason => _lastGlobalCooldownReason;

    /// <summary>
    /// Gets the current high-level boss state.
    /// </summary>
    public E_BossState CurrentState => _currentState;

    /// <summary>
    /// Gets whether the battle lifecycle is currently active.
    /// </summary>
    public bool IsBattleActive => _isBattleActive;

    /// <summary>
    /// Gets whether future pattern selection is currently enabled.
    /// </summary>
    public bool IsPatternSelectionEnabled => _isPatternSelectionEnabled;

    /// <summary>
    /// Gets whether the boss is currently invincible.
    /// </summary>
    public bool IsInvincible => _isInvincible;

    /// <summary>
    /// Gets whether the weak point pattern is currently active.
    /// </summary>
    public bool IsWeakPointPatternActive => _isWeakPointPatternActive;

    /// <summary>
    /// Gets the pattern type currently being executed.
    /// </summary>
    public E_BossPatternType CurrentPatternType => _currentPatternType;

    /// <summary>
    /// Gets the HealthPhase index captured when the current pattern execution was confirmed.
    /// </summary>
    public int CurrentPatternHealthPhaseIndex => _currentPatternHealthPhaseIndex;

    /// <summary>
    /// Gets the current pattern instance handle reserved for future pattern execution.
    /// </summary>
    public BossPatternBase CurrentPattern => _currentPattern;

    /// <summary>
    /// Gets the current pattern instance for compatibility with previous call sites.
    /// </summary>
    public BossPatternBase CurrentPatternInstance => _currentPattern;

    /// <summary>
    /// Gets the reusable boss pattern selector.
    /// </summary>
    public BossPatternSelector PatternSelector => _patternSelector;

    /// <summary>
    /// Prepares reusable cooldown storage before runtime pattern selection reads it.
    /// </summary>
    private void Awake()
    {
        ResolveOptionalRuntimeReferences();
        EnsurePatternCooldownStorage();
        EnsureHealthPhaseUsageStorage();
    }

    /// <summary>
    /// Corrects invalid boss-level common settings and reports missing required references.
    /// </summary>
    private void OnValidate()
    {
        ValidateCommonSettings();
        ValidateRequiredReferences();
    }

    /// <summary>
    /// Returns whether this instance is allowed to decide boss state and combat logic.
    /// </summary>
    public bool IsBossLogicAuthority()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // NGO session singleton used to decide single-player versus server-authoritative execution.
        if (networkManager == null)
        {
            return true;
        }

        if (!networkManager.IsListening)
        {
            return true;
        }

        return networkManager.IsServer;
    }

    /// <summary>
    /// Starts the boss battle lifecycle on the authority instance.
    /// </summary>
    public void StartBattle()
    {
        if (!TryEnsureAuthority("StartBattle"))
        {
            return;
        }

        ResetHealthPhaseUsageCounters();
        ResetCommonCooldown();
        ResetIndividualCooldowns();
        ResetRuntimeWarningState();

        _isBattleActive = true;
        _isPatternSelectionEnabled = true;
        _isInvincible = false;
        _isWeakPointPatternActive = false;
        EnterIdleState();
    }

    /// <summary>
    /// Resets every boss runtime state value owned by the authority instance.
    /// </summary>
    public void ResetBattle()
    {
        if (!TryEnsureAuthority("ResetBattle"))
        {
            return;
        }

        StopAllRuntimeTimers();
        ResetRuntimeState();
    }

    /// <summary>
    /// Stops the battle lifecycle and cancels future pattern selection or active pattern work.
    /// </summary>
    public void StopBattle()
    {
        if (!TryEnsureAuthority("StopBattle"))
        {
            return;
        }

        _isPatternSelectionEnabled = false;
        CancelCurrentPattern("StopBattle");
        StopAllRuntimeTimers();
        _isBattleActive = false;

        if (_currentState != E_BossState.Dead)
        {
            EnterIdleState();
        }
    }

    /// <summary>
    /// Marks the boss as pattern-executing without running any pattern logic.
    /// </summary>
    public void SetPatternExecutingState(E_BossPatternType patternType, BossPatternBase patternInstance)
    {
        if (!TryEnsureAuthority("SetPatternExecutingState"))
        {
            return;
        }

        EnterPatternExecutingState(patternType, patternInstance);
    }

    /// <summary>
    /// Starts a boss pattern through the common pattern execution API.
    /// </summary>
    public bool TryStartPatternExecution(BossPatternBase pattern)
    {
        if (!TryEnsureAuthority("TryStartPatternExecution"))
        {
            return false;
        }

        if (pattern == null)
        {
            Debug.LogWarning($"[BossController] TryStartPatternExecution failed because pattern is null. object={name}", this);
            return false;
        }

        if (_currentState != E_BossState.Idle)
        {
            Debug.LogWarning($"[BossController] TryStartPatternExecution blocked by boss state. object={name}, state={_currentState}", this);
            return false;
        }

        if (_currentPattern != null)
        {
            Debug.LogWarning($"[BossController] TryStartPatternExecution blocked because another pattern is active. object={name}, activeType={_currentPatternType}", this);
            return false;
        }

        if (pattern.PatternType == E_BossPatternType.WeakPoint && _isWeakPointPatternActive)
        {
            Debug.LogWarning($"[BossController] TryStartPatternExecution blocked because Pattern 4 is already active. object={name}", this);
            return false;
        }

        if (IsGlobalCooldownActive())
        {
            Debug.LogWarning($"[BossController] TryStartPatternExecution blocked by global cooldown. object={name}, patternType={pattern.PatternType}, remaining={GetGlobalCooldownRemainingSeconds()}", this);
            return false;
        }

        if (IsPatternCooldownActive(pattern.PatternType))
        {
            Debug.LogWarning($"[BossController] TryStartPatternExecution blocked by individual pattern cooldown. object={name}, patternType={pattern.PatternType}, remaining={GetPatternCooldownRemainingSeconds(pattern.PatternType)}", this);
            return false;
        }

        if (!TryRecordPatternUseForConfirmedExecution(pattern.PatternType))
        {
            Debug.LogWarning($"[BossController] TryStartPatternExecution blocked by HealthPhase or usage limits. object={name}, patternType={pattern.PatternType}", this);
            return false;
        }

        SetCurrentPatternReference(pattern.PatternType, pattern, true);
        SetState(E_BossState.PatternExecuting);

        if (!pattern.StartPatternExecution())
        {
            ClearCurrentPatternReference();
            ClearCurrentPatternSelectionContext();
            SetState(E_BossState.Idle);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Receives normal pattern completion and returns the boss to Idle.
    /// </summary>
    public void OnBossPatternCompleted(BossPatternExecutionReport report)
    {
        HandlePatternResult(report, "Completed");
    }

    /// <summary>
    /// Receives pattern cancellation and returns the boss to Idle.
    /// </summary>
    public void OnBossPatternCancelled(BossPatternExecutionReport report)
    {
        HandlePatternResult(report, "Cancelled");
    }

    /// <summary>
    /// Receives pattern failure and returns the boss to Idle.
    /// </summary>
    public void OnBossPatternFailed(BossPatternExecutionReport report)
    {
        HandlePatternResult(report, "Failed");
    }

    /// <summary>
    /// Marks the boss as groggy and stops future pattern selection.
    /// </summary>
    public void SetGroggyState()
    {
        if (!TryEnsureAuthority("SetGroggyState"))
        {
            return;
        }

        EnterGroggyState();
    }

    /// <summary>
    /// Marks the boss as dead and stops future pattern selection.
    /// </summary>
    public void SetDeadState()
    {
        if (!TryEnsureAuthority("SetDeadState"))
        {
            return;
        }

        EnterDeadState();
    }

    /// <summary>
    /// Starts or overwrites the boss-wide cooldown from an authority-owned operation.
    /// </summary>
    public void StartGlobalCooldown(string reason)
    {
        if (!TryEnsureAuthority("StartGlobalCooldown"))
        {
            return;
        }

        StartGlobalCooldownInternal(reason);
    }

    /// <summary>
    /// Starts or overwrites the individual cooldown for one pattern type.
    /// </summary>
    public void StartPatternCooldown(E_BossPatternType patternType, string reason)
    {
        if (!TryEnsureAuthority("StartPatternCooldown"))
        {
            return;
        }

        StartPatternCooldownInternal(patternType, reason);
    }

    /// <summary>
    /// Records that Pattern 4 entered its weak point phase and starts the required global cooldown.
    /// </summary>
    public void NotifyPatternFourEntryCompleted()
    {
        if (!TryEnsureAuthority("NotifyPatternFourEntryCompleted"))
        {
            return;
        }

        _isWeakPointPatternActive = true;
        _isInvincible = true;
        StartGlobalCooldownInternal("Pattern4EntryCompleted");
    }

    /// <summary>
    /// Records that Pattern 4 entry started without activating weak points or starting the weak point timer.
    /// </summary>
    public void NotifyPatternFourEntryStarted()
    {
        if (!TryEnsureAuthority("NotifyPatternFourEntryStarted"))
        {
            return;
        }

        _isWeakPointPatternActive = false;
        _isInvincible = false;
    }

    /// <summary>
    /// Records that Pattern 4 timed out and starts the required global cooldown.
    /// </summary>
    public void NotifyPatternFourTimedOut()
    {
        if (!TryEnsureAuthority("NotifyPatternFourTimedOut"))
        {
            return;
        }

        CancelCurrentRegularPatternForPatternFourEnd("Pattern4TimedOut");
        _isWeakPointPatternActive = false;
        _isInvincible = false;
        _isPatternSelectionEnabled = true;
        EnterIdleState();
        StartGlobalCooldownInternal("Pattern4TimedOut");
    }

    /// <summary>
    /// Records that Pattern 4 failed before entry and starts the required global cooldown.
    /// </summary>
    public void NotifyPatternFourEntryFailed()
    {
        if (!TryEnsureAuthority("NotifyPatternFourEntryFailed"))
        {
            return;
        }

        _isWeakPointPatternActive = false;
        _isInvincible = false;
        StartGlobalCooldownInternal("Pattern4EntryFailed");
    }

    /// <summary>
    /// Records that every weak point was destroyed without starting the global cooldown immediately.
    /// </summary>
    public void NotifyPatternFourAllWeakPointsDestroyed()
    {
        if (!TryEnsureAuthority("NotifyPatternFourAllWeakPointsDestroyed"))
        {
            return;
        }

        CancelCurrentRegularPatternForPatternFourEnd("Pattern4AllWeakPointsDestroyed");
        _isWeakPointPatternActive = false;
        _isInvincible = false;
    }

    /// <summary>
    /// Enters Groggy state for a fixed duration and lets the authority return to Idle afterward.
    /// </summary>
    public void StartGroggyForDuration(float groggyDurationSeconds, string reason)
    {
        if (!TryEnsureAuthority("StartGroggyForDuration"))
        {
            return;
        }

        float safeDuration = groggyDurationSeconds; // Duration used by the authority-owned Groggy timer.
        if (safeDuration < 0f)
        {
            Debug.LogWarning($"[BossController] Groggy duration was below zero at runtime and clamped. object={name}, value={safeDuration}", this);
            safeDuration = 0f;
        }

        StopRuntimeCoroutine(ref _groggyTimerCoroutine);
        _isWeakPointPatternActive = false;
        _isInvincible = false;
        EnterGroggyState();
        _groggyTimerCoroutine = StartCoroutine(RunGroggyTimer(safeDuration, reason));
    }

    /// <summary>
    /// Ends Groggy state and starts the required global cooldown before returning to Idle.
    /// </summary>
    public void EndGroggyState()
    {
        if (!TryEnsureAuthority("EndGroggyState"))
        {
            return;
        }

        if (_currentState != E_BossState.Groggy)
        {
            Debug.LogWarning($"[BossController] EndGroggyState called while boss is not Groggy. object={name}, state={_currentState}", this);
            return;
        }

        _isPatternSelectionEnabled = true;
        StartGlobalCooldownInternal("GroggyEnded");
        EnterIdleState();
    }

    /// <summary>
    /// Returns whether future pattern selection may run in the current boss state.
    /// </summary>
    public bool CanSelectPattern()
    {
        if (!_isBattleActive || !_isPatternSelectionEnabled)
        {
            return false;
        }

        if (_currentState != E_BossState.Idle)
        {
            return false;
        }

        if (IsGlobalCooldownActive())
        {
            return false;
        }

        return IsBossLogicAuthority();
    }

    /// <summary>
    /// Returns whether a specific pattern can be selected after state and cooldown checks.
    /// </summary>
    public bool CanSelectPatternType(E_BossPatternType patternType)
    {
        if (!CanSelectPattern())
        {
            return false;
        }

        if (GetPatternCooldownIndex(patternType) < 0)
        {
            return false;
        }

        if (patternType == E_BossPatternType.WeakPoint && _isWeakPointPatternActive)
        {
            return false;
        }

        if (IsPatternCooldownActive(patternType))
        {
            return false;
        }

        return TryGetPatternSelectionContext(patternType, out _, out _);
    }

    /// <summary>
    /// Returns whether a concrete common settings entry can be selected by the boss selector.
    /// </summary>
    public bool CanSelectPatternSettings(PatternCommonSettings settings)
    {
        if (!CanSelectPattern())
        {
            return false;
        }

        if (GetPatternCooldownIndex(settings.PatternType) < 0)
        {
            return false;
        }

        if (settings.PatternType == E_BossPatternType.WeakPoint && _isWeakPointPatternActive)
        {
            return false;
        }

        if (IsPatternCooldownActive(settings.PatternType))
        {
            return false;
        }

        return TryGetPatternSelectionContext(settings, out _, out _);
    }

    /// <summary>
    /// Selects the next pattern candidate without starting pattern execution.
    /// </summary>
    public bool TrySelectPattern(Transform target, out PatternCommonSettings selectedSettings)
    {
        return _patternSelector.TrySelectPattern(this, target, out selectedSettings);
    }

    /// <summary>
    /// Finds the nearest valid Player for a boss pattern execution through the shared target provider.
    /// </summary>
    public bool TryFindNearestPlayerForExecution(float executionRange, out Transform targetTransform, out HealthComponent targetHealth, out NetworkObject targetNetworkObject)
    {
        ResolveOptionalRuntimeReferences();
        if (_playerTargetProvider == null)
        {
            Debug.LogWarning($"[BossController] Player target search failed because BossPlayerTargetProvider is missing. object={name}", this);
            targetTransform = null;
            targetHealth = null;
            targetNetworkObject = null;
            return false;
        }

        return _playerTargetProvider.TryFindNearestPlayerForExecution(executionRange, out targetTransform, out targetHealth, out targetNetworkObject);
    }

    /// <summary>
    /// Returns whether boss-wide pattern cooldown is currently active.
    /// </summary>
    public bool IsGlobalCooldownActive()
    {
        return Time.time < _globalCooldownEndTime;
    }

    /// <summary>
    /// Returns remaining boss-wide cooldown seconds based on Time.time.
    /// </summary>
    public float GetGlobalCooldownRemainingSeconds()
    {
        float remainingSeconds = _globalCooldownEndTime - Time.time; // Time.time based cooldown difference for selection gating.
        if (remainingSeconds <= 0f)
        {
            return 0f;
        }

        return remainingSeconds;
    }

    /// <summary>
    /// Returns whether the given pattern type is still under individual cooldown.
    /// </summary>
    public bool IsPatternCooldownActive(E_BossPatternType patternType)
    {
        int cooldownIndex = GetPatternCooldownIndex(patternType); // Enum-backed index used to read the reusable cooldown array.
        if (cooldownIndex < 0)
        {
            return false;
        }

        EnsurePatternCooldownStorage();
        return Time.time < _patternCooldownEndTimeByType[cooldownIndex];
    }

    /// <summary>
    /// Returns remaining individual cooldown seconds for the given pattern type.
    /// </summary>
    public float GetPatternCooldownRemainingSeconds(E_BossPatternType patternType)
    {
        int cooldownIndex = GetPatternCooldownIndex(patternType); // Enum-backed index used to read the reusable cooldown array.
        if (cooldownIndex < 0)
        {
            return 0f;
        }

        EnsurePatternCooldownStorage();
        float remainingSeconds = _patternCooldownEndTimeByType[cooldownIndex] - Time.time; // Time.time based cooldown difference for selection gating.
        if (remainingSeconds <= 0f)
        {
            return 0f;
        }

        return remainingSeconds;
    }

    /// <summary>
    /// Returns the Time.time timestamp when the given pattern type cooldown ends.
    /// </summary>
    public float GetPatternCooldownEndTime(E_BossPatternType patternType)
    {
        int cooldownIndex = GetPatternCooldownIndex(patternType); // Enum-backed index used to read the reusable cooldown array.
        if (cooldownIndex < 0)
        {
            return 0f;
        }

        EnsurePatternCooldownStorage();
        return _patternCooldownEndTimeByType[cooldownIndex];
    }

    /// <summary>
    /// Returns the boss current health ratio clamped to the 0..1 range.
    /// </summary>
    public float GetCurrentHealthRatio()
    {
        if (_healthComponent == null)
        {
            if (!_hasLoggedHealthRatioFallbackWarning)
            {
                Debug.LogWarning($"[BossController] Health ratio fell back to 1 because HealthComponent is missing. object={name}", this);
                _hasLoggedHealthRatioFallbackWarning = true;
            }

            return 1f;
        }

        return Mathf.Clamp01(_healthComponent.GetHealthNormalized());
    }

    /// <summary>
    /// Returns the first HealthPhaseSettings index that contains the current health ratio.
    /// </summary>
    public int GetCurrentHealthPhaseIndex()
    {
        return GetHealthPhaseIndex(GetCurrentHealthRatio());
    }

    /// <summary>
    /// Returns the first HealthPhaseSettings index that contains the supplied health ratio.
    /// </summary>
    public int GetHealthPhaseIndex(float healthRatio)
    {
        float clampedHealthRatio = Mathf.Clamp01(healthRatio); // Normalized health value used for ordered phase lookup.
        if (_patternData == null || _patternData.HealthPhaseSettings == null || _patternData.HealthPhaseSettings.Length == 0)
        {
            if (!_hasLoggedHealthPhaseLookupWarning)
            {
                Debug.LogWarning($"[BossController] HealthPhase lookup failed because HealthPhaseSettings is missing or empty. object={name}", this);
                _hasLoggedHealthPhaseLookupWarning = true;
            }

            return -1;
        }

        HealthPhaseSettings[] healthPhaseSettings = _patternData.HealthPhaseSettings; // Designer-authored HealthPhase array evaluated in order.
        for (int index = 0; index < healthPhaseSettings.Length; index++)
        {
            HealthPhaseSettings settings = healthPhaseSettings[index]; // Current HealthPhase candidate checked against the ratio.
            if (clampedHealthRatio < settings.MinHealthRatio || clampedHealthRatio > settings.MaxHealthRatio)
            {
                continue;
            }

            return index;
        }

        if (!_hasLoggedHealthPhaseLookupWarning)
        {
            Debug.LogWarning($"[BossController] HealthPhase lookup found no matching range. object={name}, healthRatio={clampedHealthRatio}", this);
            _hasLoggedHealthPhaseLookupWarning = true;
        }

        return -1;
    }

    /// <summary>
    /// Returns the use count recorded for a pattern type in a HealthPhase index.
    /// </summary>
    public int GetHealthPhasePatternUseCount(int healthPhaseIndex, E_BossPatternType patternType)
    {
        if (!TryGetCommonSettingsIndex(patternType, out int commonSettingsIndex))
        {
            return 0;
        }

        return GetHealthPhasePatternUseCountByIndex(healthPhaseIndex, commonSettingsIndex);
    }

    /// <summary>
    /// Enters the Idle state and clears active pattern ownership.
    /// </summary>
    private void EnterIdleState()
    {
        ClearCurrentPatternReference();
        SetState(E_BossState.Idle);
    }

    /// <summary>
    /// Enters the PatternExecuting state and stores the active pattern identity.
    /// </summary>
    private void EnterPatternExecutingState(E_BossPatternType patternType, BossPatternBase patternInstance)
    {
        SetCurrentPatternReference(patternType, patternInstance, patternInstance != null);
        SetState(E_BossState.PatternExecuting);
    }

    /// <summary>
    /// Enters the Groggy state and clears active pattern ownership.
    /// </summary>
    private void EnterGroggyState()
    {
        CancelCurrentPattern("EnterGroggyState");
        SetState(E_BossState.Groggy);
    }

    /// <summary>
    /// Enters the Dead state and clears active pattern ownership.
    /// </summary>
    private void EnterDeadState()
    {
        CancelCurrentPattern("EnterDeadState");
        _isBattleActive = false;
        SetState(E_BossState.Dead);
    }

    /// <summary>
    /// Changes the current boss state on the authority instance.
    /// </summary>
    private void SetState(E_BossState nextState)
    {
        _currentState = nextState;

        if (_currentState == E_BossState.Dead || _currentState == E_BossState.Groggy)
        {
            _isPatternSelectionEnabled = false;
        }
    }

    /// <summary>
    /// Resets all runtime-only state without touching inspector-authored configuration.
    /// </summary>
    private void ResetRuntimeState()
    {
        _isBattleActive = false;
        _isPatternSelectionEnabled = false;
        _isInvincible = false;
        _isWeakPointPatternActive = false;
        _currentPatternType = E_BossPatternType.None;
        ClearCurrentPatternSelectionContext();
        ClearCurrentPatternReference();
        ResetCommonCooldown();
        ResetIndividualCooldowns();
        ResetHealthPhaseUsageCounters();
        ResetRuntimeWarningState();
        SetState(E_BossState.None);
    }

    /// <summary>
    /// Resets the health phase usage counter storage.
    /// </summary>
    private void ResetHealthPhaseUsageCounters()
    {
        _healthPhaseUsageResetVersion++;
        ClearCurrentPatternSelectionContext();
        EnsureHealthPhaseUsageStorage();

        for (int index = 0; index < _healthPhasePatternUseCounts.Length; index++)
        {
            _healthPhasePatternUseCounts[index] = 0;
        }
    }

    /// <summary>
    /// Resets the common cooldown runtime value.
    /// </summary>
    private void ResetCommonCooldown()
    {
        _globalCooldownEndTime = 0f;
        _lastGlobalCooldownReason = string.Empty;
    }

    /// <summary>
    /// Resets the individual pattern cooldown storage values.
    /// </summary>
    private void ResetIndividualCooldowns()
    {
        _individualCooldownResetVersion++;
        EnsurePatternCooldownStorage();

        for (int index = 0; index < _patternCooldownEndTimeByType.Length; index++)
        {
            _patternCooldownEndTimeByType[index] = 0f;
        }
    }

    /// <summary>
    /// Resets one-shot runtime warning guards used by boss selection checks.
    /// </summary>
    private void ResetRuntimeWarningState()
    {
        _hasLoggedAuthorityWarning = false;
        _hasLoggedHealthRatioFallbackWarning = false;
        _hasLoggedHealthPhaseLookupWarning = false;
        _hasLoggedCommonSettingsLookupWarning = false;
    }

    /// <summary>
    /// Cancels the current pattern handle reserved for future pattern execution.
    /// </summary>
    private void CancelCurrentPattern(string reason)
    {
        if (_currentPattern != null && _currentPattern.IsExecuting)
        {
            _currentPattern.CancelPattern(reason);
            return;
        }

        ClearCurrentPatternReference();
    }

    /// <summary>
    /// Cancels only the currently executing non-Pattern-4 pattern through the common cancellation API.
    /// </summary>
    private void CancelCurrentRegularPatternForPatternFourEnd(string reason)
    {
        if (_currentPattern == null || !_currentPattern.IsExecuting)
        {
            ClearCurrentPatternReference();
            return;
        }

        if (IsPatternFourPatternType(_currentPatternType))
        {
            Debug.LogWarning($"[BossController] Pattern 4 end skipped self-cancellation for the weak point pattern. object={name}, reason={reason}", this);
            return;
        }

        _currentPattern.CancelPattern(reason);
    }

    /// <summary>
    /// Clears current pattern references and unregisters this controller from reports.
    /// </summary>
    private void ClearCurrentPatternReference()
    {
        if (_currentPattern != null && _isCurrentPatternListenerRegistered)
        {
            _currentPattern.RemoveListener(this);
        }

        _currentPatternType = E_BossPatternType.None;
        _currentPattern = null;
        _isCurrentPatternListenerRegistered = false;
    }

    /// <summary>
    /// Clears the HealthPhase and common settings indices captured for the current pattern.
    /// </summary>
    private void ClearCurrentPatternSelectionContext()
    {
        _currentPatternHealthPhaseIndex = -1;
        _currentPatternCommonSettingsIndex = -1;
    }

    /// <summary>
    /// Stores the current pattern reference and optionally registers for result reports.
    /// </summary>
    private void SetCurrentPatternReference(E_BossPatternType patternType, BossPatternBase pattern, bool registerListener)
    {
        ClearCurrentPatternReference();
        _currentPatternType = patternType;
        _currentPattern = pattern;

        if (_currentPattern == null || !registerListener)
        {
            _isCurrentPatternListenerRegistered = false;
            return;
        }

        _currentPattern.AddListener(this);
        _isCurrentPatternListenerRegistered = true;
    }

    /// <summary>
    /// Handles any terminal pattern result and returns the boss to Idle.
    /// </summary>
    private void HandlePatternResult(BossPatternExecutionReport report, string resultLabel)
    {
        if (report.Pattern != _currentPattern)
        {
            Debug.LogWarning($"[BossController] Ignored stale pattern result. object={name}, result={resultLabel}, reportType={report.PatternType}, currentType={_currentPatternType}", this);
            return;
        }

        ApplyCooldownsForPatternResult(report.PatternType, resultLabel);
        ClearCurrentPatternReference();
        ClearCurrentPatternSelectionContext();

        if (_currentState != E_BossState.Dead && _currentState != E_BossState.Groggy)
        {
            SetState(E_BossState.Idle);
        }
    }

    /// <summary>
    /// Stops every active runtime timer coroutine owned by this controller.
    /// </summary>
    private void StopAllRuntimeTimers()
    {
        StopRuntimeCoroutine(ref _patternSelectionCoroutine);
        StopRuntimeCoroutine(ref _currentPatternCoroutine);
        StopRuntimeCoroutine(ref _commonCooldownCoroutine);
        StopRuntimeCoroutine(ref _groggyTimerCoroutine);
    }

    /// <summary>
    /// Waits for GroggyDuration and returns the boss to Idle with a global cooldown.
    /// </summary>
    private IEnumerator RunGroggyTimer(float groggyDurationSeconds, string reason)
    {
        if (groggyDurationSeconds > 0f)
        {
            yield return new WaitForSeconds(groggyDurationSeconds);
        }

        _groggyTimerCoroutine = null;
        EndGroggyState();
    }

    /// <summary>
    /// Stops a coroutine if it is running and clears the stored handle.
    /// </summary>
    private void StopRuntimeCoroutine(ref Coroutine coroutine)
    {
        if (coroutine == null)
        {
            return;
        }

        StopCoroutine(coroutine);
        coroutine = null;
    }

    /// <summary>
    /// Records HealthPhase usage for a pattern after authority confirms execution.
    /// </summary>
    private bool TryRecordPatternUseForConfirmedExecution(E_BossPatternType patternType)
    {
        if (!IsBossLogicAuthority())
        {
            return false;
        }

        if (_currentState == E_BossState.Dead)
        {
            return false;
        }

        if (!TryGetPatternSelectionContext(patternType, out int healthPhaseIndex, out int commonSettingsIndex))
        {
            return false;
        }

        EnsureHealthPhaseUsageStorage();
        int usageIndex = GetHealthPhaseUsageIndex(healthPhaseIndex, commonSettingsIndex);
        if (usageIndex < 0)
        {
            Debug.LogWarning($"[BossController] HealthPhase usage count was not recorded because the index was invalid. object={name}, phaseIndex={healthPhaseIndex}, commonIndex={commonSettingsIndex}", this);
            return false;
        }

        _healthPhasePatternUseCounts[usageIndex]++;
        _currentPatternHealthPhaseIndex = healthPhaseIndex;
        _currentPatternCommonSettingsIndex = commonSettingsIndex;
        return true;
    }

    /// <summary>
    /// Resolves the HealthPhase and common settings indices required for pattern selection.
    /// </summary>
    private bool TryGetPatternSelectionContext(E_BossPatternType patternType, out int healthPhaseIndex, out int commonSettingsIndex)
    {
        healthPhaseIndex = -1;
        commonSettingsIndex = -1;

        if (!TryGetCommonSettingsIndex(patternType, out commonSettingsIndex))
        {
            return false;
        }

        healthPhaseIndex = GetCurrentHealthPhaseIndex();
        if (healthPhaseIndex < 0)
        {
            return false;
        }

        PatternCommonSettings commonSettings = _patternData.CommonSettings[commonSettingsIndex]; // Common settings entry linked to the candidate pattern.
        if (!IsPatternAvailableInHealthPhase(healthPhaseIndex, commonSettings.PatternId))
        {
            return false;
        }

        if (IsPatternUsageLimitExceeded(healthPhaseIndex, commonSettingsIndex, commonSettings.PatternId))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves the HealthPhase and common settings indices required for a concrete settings entry.
    /// </summary>
    private bool TryGetPatternSelectionContext(PatternCommonSettings settings, out int healthPhaseIndex, out int commonSettingsIndex)
    {
        healthPhaseIndex = -1;
        commonSettingsIndex = -1;

        if (!TryGetCommonSettingsIndex(settings.PatternId, settings.PatternType, out commonSettingsIndex))
        {
            return false;
        }

        healthPhaseIndex = GetCurrentHealthPhaseIndex();
        if (healthPhaseIndex < 0)
        {
            return false;
        }

        if (!IsPatternAvailableInHealthPhase(healthPhaseIndex, settings.PatternId))
        {
            return false;
        }

        if (IsPatternUsageLimitExceeded(healthPhaseIndex, commonSettingsIndex, settings.PatternId))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns whether a pattern id is listed in the selected HealthPhase settings.
    /// </summary>
    private bool IsPatternAvailableInHealthPhase(int healthPhaseIndex, string patternId)
    {
        if (string.IsNullOrEmpty(patternId))
        {
            Debug.LogWarning($"[BossController] Pattern availability check failed because PatternId is empty. object={name}, phaseIndex={healthPhaseIndex}", this);
            return false;
        }

        if (_patternData == null || _patternData.HealthPhaseSettings == null || healthPhaseIndex < 0 || healthPhaseIndex >= _patternData.HealthPhaseSettings.Length)
        {
            return false;
        }

        string[] availablePatternIds = _patternData.HealthPhaseSettings[healthPhaseIndex].AvailablePatternIds; // Pattern ids allowed by the selected HealthPhase.
        if (availablePatternIds == null || availablePatternIds.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < availablePatternIds.Length; index++)
        {
            if (availablePatternIds[index] != patternId)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether the HealthPhase use count has reached the configured usage limit.
    /// </summary>
    private bool IsPatternUsageLimitExceeded(int healthPhaseIndex, int commonSettingsIndex, string patternId)
    {
        if (!TryGetPatternUsageLimit(patternId, out PatternUsageLimit usageLimit))
        {
            return false;
        }

        int maxUseCount = usageLimit.MaxEncounterUseCount; // Phase-local maximum use count; zero means unlimited.
        if (maxUseCount <= 0)
        {
            return false;
        }

        int currentUseCount = GetHealthPhasePatternUseCountByIndex(healthPhaseIndex, commonSettingsIndex);
        return currentUseCount >= maxUseCount;
    }

    /// <summary>
    /// Finds the first common settings entry for a pattern type.
    /// </summary>
    private bool TryGetCommonSettingsIndex(E_BossPatternType patternType, out int commonSettingsIndex)
    {
        commonSettingsIndex = -1;
        if (GetPatternCooldownIndex(patternType) < 0)
        {
            return false;
        }

        if (_patternData == null || _patternData.CommonSettings == null || _patternData.CommonSettings.Length == 0)
        {
            if (!_hasLoggedCommonSettingsLookupWarning)
            {
                Debug.LogWarning($"[BossController] Common settings lookup failed because PatternData or CommonSettings is missing. object={name}, patternType={patternType}", this);
                _hasLoggedCommonSettingsLookupWarning = true;
            }

            return false;
        }

        PatternCommonSettings[] commonSettings = _patternData.CommonSettings; // Designer-authored common settings array evaluated in order.
        for (int index = 0; index < commonSettings.Length; index++)
        {
            if (commonSettings[index].PatternType != patternType)
            {
                continue;
            }

            commonSettingsIndex = index;
            return true;
        }

        if (!_hasLoggedCommonSettingsLookupWarning)
        {
            Debug.LogWarning($"[BossController] Common settings lookup found no matching PatternType. object={name}, patternType={patternType}", this);
            _hasLoggedCommonSettingsLookupWarning = true;
        }

        return false;
    }

    /// <summary>
    /// Finds the first common settings entry matching both pattern id and pattern type.
    /// </summary>
    private bool TryGetCommonSettingsIndex(string patternId, E_BossPatternType patternType, out int commonSettingsIndex)
    {
        commonSettingsIndex = -1;
        if (string.IsNullOrEmpty(patternId) || GetPatternCooldownIndex(patternType) < 0)
        {
            return false;
        }

        if (_patternData == null || _patternData.CommonSettings == null || _patternData.CommonSettings.Length == 0)
        {
            if (!_hasLoggedCommonSettingsLookupWarning)
            {
                Debug.LogWarning($"[BossController] Common settings lookup failed because PatternData or CommonSettings is missing. object={name}, patternId={patternId}, patternType={patternType}", this);
                _hasLoggedCommonSettingsLookupWarning = true;
            }

            return false;
        }

        PatternCommonSettings[] commonSettings = _patternData.CommonSettings; // Designer-authored common settings array evaluated in order.
        for (int index = 0; index < commonSettings.Length; index++)
        {
            if (commonSettings[index].PatternType != patternType || commonSettings[index].PatternId != patternId)
            {
                continue;
            }

            commonSettingsIndex = index;
            return true;
        }

        if (!_hasLoggedCommonSettingsLookupWarning)
        {
            Debug.LogWarning($"[BossController] Common settings lookup found no matching PatternId and PatternType. object={name}, patternId={patternId}, patternType={patternType}", this);
            _hasLoggedCommonSettingsLookupWarning = true;
        }

        return false;
    }

    /// <summary>
    /// Finds the usage limit settings for a pattern id.
    /// </summary>
    private bool TryGetPatternUsageLimit(string patternId, out PatternUsageLimit usageLimit)
    {
        usageLimit = default;
        if (_patternData == null || _patternData.UsageLimits == null || _patternData.UsageLimits.Length == 0 || string.IsNullOrEmpty(patternId))
        {
            return false;
        }

        PatternUsageLimit[] usageLimits = _patternData.UsageLimits; // Designer-authored usage limit array searched by pattern id.
        for (int index = 0; index < usageLimits.Length; index++)
        {
            if (usageLimits[index].PatternId != patternId)
            {
                continue;
            }

            usageLimit = usageLimits[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns a recorded use count by HealthPhase and common settings index.
    /// </summary>
    private int GetHealthPhasePatternUseCountByIndex(int healthPhaseIndex, int commonSettingsIndex)
    {
        EnsureHealthPhaseUsageStorage();
        int usageIndex = GetHealthPhaseUsageIndex(healthPhaseIndex, commonSettingsIndex);
        if (usageIndex < 0)
        {
            return 0;
        }

        return _healthPhasePatternUseCounts[usageIndex];
    }

    /// <summary>
    /// Ensures HealthPhase pattern usage storage matches the current pattern data shape.
    /// </summary>
    private void EnsureHealthPhaseUsageStorage()
    {
        int healthPhaseCount = GetHealthPhaseSettingsCount(); // Number of HealthPhase rows in the flattened usage table.
        int commonSettingsCount = GetCommonSettingsCount(); // Number of pattern columns in the flattened usage table.
        int requiredLength = healthPhaseCount * commonSettingsCount; // Flattened table length for phase-pattern usage counts.
        if (_healthPhasePatternUseCounts != null && _healthPhasePatternUseCounts.Length == requiredLength)
        {
            return;
        }

        _healthPhasePatternUseCounts = new int[requiredLength];
    }

    /// <summary>
    /// Converts HealthPhase and common settings indices into a flattened usage array index.
    /// </summary>
    private int GetHealthPhaseUsageIndex(int healthPhaseIndex, int commonSettingsIndex)
    {
        int commonSettingsCount = GetCommonSettingsCount(); // Current common settings count used as flattened table stride.
        int healthPhaseCount = GetHealthPhaseSettingsCount(); // Current HealthPhase count used to validate row bounds.
        if (healthPhaseIndex < 0 || healthPhaseIndex >= healthPhaseCount || commonSettingsIndex < 0 || commonSettingsIndex >= commonSettingsCount)
        {
            return -1;
        }

        return healthPhaseIndex * commonSettingsCount + commonSettingsIndex;
    }

    /// <summary>
    /// Returns the current HealthPhaseSettings array length.
    /// </summary>
    private int GetHealthPhaseSettingsCount()
    {
        if (_patternData == null || _patternData.HealthPhaseSettings == null)
        {
            return 0;
        }

        return _patternData.HealthPhaseSettings.Length;
    }

    /// <summary>
    /// Returns the current PatternCommonSettings array length.
    /// </summary>
    private int GetCommonSettingsCount()
    {
        if (_patternData == null || _patternData.CommonSettings == null)
        {
            return 0;
        }

        return _patternData.CommonSettings.Length;
    }

    /// <summary>
    /// Starts cooldowns required by a terminal pattern result.
    /// </summary>
    private void ApplyCooldownsForPatternResult(E_BossPatternType patternType, string resultLabel)
    {
        if (IsPatternFourPatternType(patternType))
        {
            return;
        }

        if (resultLabel == "Completed" || resultLabel == "Cancelled")
        {
            StartPatternCooldownInternal(patternType, resultLabel);
            StartGlobalCooldownInternal(resultLabel);
            return;
        }

        if (resultLabel == "Failed")
        {
            StartGlobalCooldownInternal(resultLabel);
        }
    }

    /// <summary>
    /// Starts or overwrites the boss-wide cooldown without an additional authority check.
    /// </summary>
    private void StartGlobalCooldownInternal(string reason)
    {
        float cooldownSeconds = _commonPatternCooldownSeconds; // Designer-authored boss-wide cooldown duration.
        if (cooldownSeconds < 0f)
        {
            Debug.LogWarning($"[BossController] Common pattern cooldown was below zero at runtime and clamped. object={name}, value={cooldownSeconds}", this);
            cooldownSeconds = 0f;
        }

        _globalCooldownEndTime = Time.time + cooldownSeconds;
        _lastGlobalCooldownReason = string.IsNullOrWhiteSpace(reason) ? "Unspecified" : reason;
    }

    /// <summary>
    /// Starts or overwrites one pattern type cooldown without an additional authority check.
    /// </summary>
    private void StartPatternCooldownInternal(E_BossPatternType patternType, string reason)
    {
        int cooldownIndex = GetPatternCooldownIndex(patternType); // Enum-backed index used to write the reusable cooldown array.
        if (cooldownIndex < 0)
        {
            Debug.LogWarning($"[BossController] Pattern cooldown was not started because pattern type is invalid. object={name}, patternType={patternType}, reason={reason}", this);
            return;
        }

        EnsurePatternCooldownStorage();
        float cooldownSeconds = GetPatternCooldownSeconds(patternType);
        _patternCooldownEndTimeByType[cooldownIndex] = Time.time + cooldownSeconds;
    }

    /// <summary>
    /// Returns the configured individual cooldown seconds for a pattern type.
    /// </summary>
    private float GetPatternCooldownSeconds(E_BossPatternType patternType)
    {
        if (_patternData == null || _patternData.CommonSettings == null)
        {
            Debug.LogWarning($"[BossController] Pattern cooldown lookup fell back to zero because PatternData or CommonSettings is missing. object={name}, patternType={patternType}", this);
            return 0f;
        }

        PatternCommonSettings[] commonSettings = _patternData.CommonSettings; // Serialized common settings array used for individual pattern cooldown lookup.
        for (int index = 0; index < commonSettings.Length; index++)
        {
            if (commonSettings[index].PatternType != patternType)
            {
                continue;
            }

            return commonSettings[index].CooldownSeconds;
        }

        Debug.LogWarning($"[BossController] Pattern cooldown lookup fell back to zero because no PatternCommonSettings entry was found. object={name}, patternType={patternType}", this);
        return 0f;
    }

    /// <summary>
    /// Ensures individual pattern cooldown storage is allocated once for all enum-backed pattern types.
    /// </summary>
    private void EnsurePatternCooldownStorage()
    {
        int requiredLength = (int)E_BossPatternType.WeakPoint + 1; // Current maximum enum value plus one for direct index lookup.
        if (_patternCooldownEndTimeByType != null && _patternCooldownEndTimeByType.Length == requiredLength)
        {
            return;
        }

        _patternCooldownEndTimeByType = new float[requiredLength];
    }

    /// <summary>
    /// Converts a pattern type into a cooldown array index.
    /// </summary>
    private int GetPatternCooldownIndex(E_BossPatternType patternType)
    {
        int cooldownIndex = (int)patternType; // Enum numeric value used as a stable array index.
        if (cooldownIndex <= (int)E_BossPatternType.None || cooldownIndex > (int)E_BossPatternType.WeakPoint)
        {
            return -1;
        }

        return cooldownIndex;
    }

    /// <summary>
    /// Returns whether the pattern type belongs to the Pattern 4 weak point flow.
    /// </summary>
    private bool IsPatternFourPatternType(E_BossPatternType patternType)
    {
        return patternType == E_BossPatternType.WeakPoint;
    }

    /// <summary>
    /// Checks authority before mutating boss-owned runtime state.
    /// </summary>
    private bool TryEnsureAuthority(string operationName)
    {
        if (IsBossLogicAuthority())
        {
            _hasLoggedAuthorityWarning = false;
            return true;
        }

        if (!_hasLoggedAuthorityWarning)
        {
            Debug.LogWarning($"[BossController] {operationName} ignored because this instance has no boss authority. object={name}", this);
            _hasLoggedAuthorityWarning = true;
        }

        return false;
    }

    /// <summary>
    /// Corrects boss-level common settings edited in the inspector.
    /// </summary>
    private void ValidateCommonSettings()
    {
        if (_commonPatternCooldownSeconds < 0f)
        {
            Debug.LogWarning($"[BossController] Common pattern cooldown was below zero and clamped. object={name}, value={_commonPatternCooldownSeconds}", this);
            _commonPatternCooldownSeconds = 0f;
        }
    }

    /// <summary>
    /// Reports missing required references so authoring issues can be found before runtime.
    /// </summary>
    private void ValidateRequiredReferences()
    {
        if (!_warnMissingRequiredReferences)
        {
            return;
        }

        if (_patternData == null)
        {
            Debug.LogWarning($"[BossController] PatternData is missing on {name}.", this);
        }

        if (_anchorSet == null)
        {
            Debug.LogWarning($"[BossController] BossPatternAnchorSet is missing on {name}.", this);
        }

        if (_healthComponent == null)
        {
            Debug.LogWarning($"[BossController] HealthComponent is missing on {name}. Boss damage cannot use the existing Health system.", this);
        }

        if (_hitReceiver == null)
        {
            Debug.LogWarning($"[BossController] HitReceiver is missing on {name}. Boss damage entry cannot use the existing Hit system.", this);
        }

        if (_playerTargetProvider == null)
        {
            Debug.LogWarning($"[BossController] BossPlayerTargetProvider is missing on {name}. Boss patterns cannot use the shared Player search path.", this);
        }
    }

    /// <summary>
    /// Resolves optional runtime helper references from the boss GameObject.
    /// </summary>
    private void ResolveOptionalRuntimeReferences()
    {
        if (_playerTargetProvider == null)
        {
            _playerTargetProvider = GetComponent<BossPlayerTargetProvider>();
        }
    }
}
