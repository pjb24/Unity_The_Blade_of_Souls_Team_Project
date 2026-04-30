using UnityEngine;

/// <summary>
/// Selects a boss pattern from reusable candidate storage using authority-owned runtime conditions.
/// </summary>
public sealed class BossPatternSelector
{
    private int[] _candidateIndices = new int[0]; // Reusable buffer that stores PatternCommonSettings indices for the highest-priority candidates.
    private int _candidateCount; // Number of valid entries currently stored in the reusable candidate buffer.
    private bool _hasLoggedMissingControllerWarning; // Prevents repeated missing BossController warnings from this selector.
    private bool _hasLoggedMissingPatternDataWarning; // Prevents repeated missing BossPatternData warnings from this selector.
    private bool _hasLoggedMissingBossTransformWarning; // Prevents repeated missing boss Transform warnings from this selector.
    private bool _hasLoggedClientRandomWarning; // Prevents repeated client-side random selection warnings from this selector.

    /// <summary>
    /// Selects a pattern using the BossController transform as the boss position.
    /// </summary>
    public bool TrySelectPattern(BossController bossController, Transform target, out PatternCommonSettings selectedSettings)
    {
        Transform bossTransform = bossController != null ? bossController.transform : null; // Boss transform used as the distance origin for target conditions.
        return TrySelectPattern(bossController, bossTransform, target, out selectedSettings);
    }

    /// <summary>
    /// Selects the highest-priority pattern that satisfies state, cooldown, health, usage, and target conditions.
    /// </summary>
    public bool TrySelectPattern(BossController bossController, Transform bossTransform, Transform target, out PatternCommonSettings selectedSettings)
    {
        selectedSettings = default;
        ClearCandidateBuffer();

        if (!TryGetCommonSettings(bossController, out PatternCommonSettings[] commonSettings))
        {
            return false;
        }

        if (!bossController.CanSelectPattern())
        {
            return false;
        }

        if (bossTransform == null)
        {
            if (!_hasLoggedMissingBossTransformWarning)
            {
                Debug.LogWarning("[BossPatternSelector] Boss Transform is missing. Target distance conditions cannot be evaluated.");
                _hasLoggedMissingBossTransformWarning = true;
            }

            return false;
        }

        InitializeCandidateBuffer(commonSettings.Length);

        int highestPriority = int.MinValue; // Highest candidate priority found during this selection call.
        for (int index = 0; index < commonSettings.Length; index++)
        {
            PatternCommonSettings settings = commonSettings[index]; // Current pattern settings entry evaluated as a selection candidate.
            if (!IsCandidateAllowed(bossController, bossTransform, target, settings, index))
            {
                continue;
            }

            if (settings.Priority > highestPriority)
            {
                highestPriority = settings.Priority;
                ClearCandidateBuffer();
                AddCandidateIndex(index);
                continue;
            }

            if (settings.Priority == highestPriority)
            {
                AddCandidateIndex(index);
            }
        }

        if (_candidateCount <= 0)
        {
            Debug.LogWarning($"[BossPatternSelector] No selectable boss pattern candidates were found. object={bossController.name}, state={bossController.CurrentState}, phaseIndex={bossController.GetCurrentHealthPhaseIndex()}");
            bossController.ReportNoSelectablePatternFallback();
            return false;
        }

        int selectedCandidateBufferIndex = ResolveSelectedCandidateBufferIndex(bossController);
        if (selectedCandidateBufferIndex < 0)
        {
            return false;
        }

        int selectedCommonSettingsIndex = _candidateIndices[selectedCandidateBufferIndex]; // CommonSettings index selected from the candidate buffer.
        selectedSettings = commonSettings[selectedCommonSettingsIndex];
        return true;
    }

    /// <summary>
    /// Clears the reusable candidate buffer count without allocating a new list.
    /// </summary>
    public void ClearCandidateBuffer()
    {
        _candidateCount = 0;
    }

    /// <summary>
    /// Ensures the reusable candidate buffer can store the requested candidate count.
    /// </summary>
    public void InitializeCandidateBuffer(int requiredCapacity)
    {
        int safeCapacity = Mathf.Max(0, requiredCapacity); // Required buffer capacity clamped for array allocation.
        if (_candidateIndices != null && _candidateIndices.Length >= safeCapacity)
        {
            return;
        }

        _candidateIndices = new int[safeCapacity];
    }

    /// <summary>
    /// Returns whether the settings entry satisfies every selector-owned candidate condition.
    /// </summary>
    private bool IsCandidateAllowed(BossController bossController, Transform bossTransform, Transform target, PatternCommonSettings settings, int commonSettingsIndex)
    {
        if (!settings.Enabled)
        {
            return false;
        }

        if (settings.PatternType == E_BossPatternType.None)
        {
            return false;
        }

        if (settings.PatternType == E_BossPatternType.WeakPoint && (bossController.CurrentPatternType == E_BossPatternType.WeakPoint || bossController.IsWeakPointPatternActive))
        {
            return false;
        }

        if (bossController.IsWeakPointPatternActive && !settings.AllowDuringWeakPointActive)
        {
            return false;
        }

        if (!bossController.CanSelectPatternSettings(settings, commonSettingsIndex))
        {
            return false;
        }

        return SatisfiesTargetConditions(bossTransform, target, settings);
    }

    /// <summary>
    /// Returns whether the target requirement and squared distance conditions are satisfied.
    /// </summary>
    private bool SatisfiesTargetConditions(Transform bossTransform, Transform target, PatternCommonSettings settings)
    {
        if (target == null)
        {
            return !settings.RequireTarget;
        }

        Vector3 offset = target.position - bossTransform.position; // Offset used for sqrMagnitude distance checks.
        float sqrDistance = offset.sqrMagnitude;
        if (sqrDistance < settings.MinimumTargetSqrDistance)
        {
            return false;
        }

        if (settings.MaximumTargetSqrDistance > 0f && sqrDistance > settings.MaximumTargetSqrDistance)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Adds a CommonSettings index to the reusable candidate buffer.
    /// </summary>
    private void AddCandidateIndex(int commonSettingsIndex)
    {
        if (_candidateIndices == null || _candidateCount >= _candidateIndices.Length)
        {
            Debug.LogWarning($"[BossPatternSelector] Candidate buffer was too small and expanded at selection time. previousCapacity={(_candidateIndices == null ? 0 : _candidateIndices.Length)}, required={_candidateCount + 1}");
            InitializeCandidateBuffer(_candidateCount + 1);
        }

        _candidateIndices[_candidateCount] = commonSettingsIndex;
        _candidateCount++;
    }

    /// <summary>
    /// Resolves the final candidate buffer index, using server-authoritative random choice for priority ties.
    /// </summary>
    private int ResolveSelectedCandidateBufferIndex(BossController bossController)
    {
        if (_candidateCount == 1)
        {
            return 0;
        }

        if (!bossController.IsBossLogicAuthority())
        {
            if (!_hasLoggedClientRandomWarning)
            {
                Debug.LogWarning("[BossPatternSelector] Random tie selection was blocked because this instance has no boss authority.");
                _hasLoggedClientRandomWarning = true;
            }

            return -1;
        }

        return Random.Range(0, _candidateCount);
    }

    /// <summary>
    /// Resolves PatternCommonSettings from the controller and reports missing references.
    /// </summary>
    private bool TryGetCommonSettings(BossController bossController, out PatternCommonSettings[] commonSettings)
    {
        commonSettings = null;
        if (bossController == null)
        {
            if (!_hasLoggedMissingControllerWarning)
            {
                Debug.LogWarning("[BossPatternSelector] BossController is missing. Pattern selection cannot run.");
                _hasLoggedMissingControllerWarning = true;
            }

            return false;
        }

        BossPatternData patternData = bossController.PatternData; // Boss pattern data asset that owns common pattern settings.
        if (patternData == null || patternData.CommonSettings == null || patternData.CommonSettings.Length == 0)
        {
            if (!_hasLoggedMissingPatternDataWarning)
            {
                Debug.LogWarning($"[BossPatternSelector] PatternData or CommonSettings is missing. object={bossController.name}", bossController);
                _hasLoggedMissingPatternDataWarning = true;
            }

            return false;
        }

        commonSettings = patternData.CommonSettings;
        return true;
    }
}
