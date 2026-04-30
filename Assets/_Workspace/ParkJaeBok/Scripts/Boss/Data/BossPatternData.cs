using UnityEngine;

/// <summary>
/// Stores designer-authored boss pattern configuration without scene references or runtime state.
/// </summary>
[CreateAssetMenu(fileName = "BossPatternData", menuName = "Boss/Pattern Data")]
public class BossPatternData : ScriptableObject
{
    [Header("Common Pattern Settings")]
    [Tooltip("Common settings shared by every boss pattern entry.")]
    [SerializeField] private PatternCommonSettings[] _commonSettings = new PatternCommonSettings[0]; // Pattern-level common settings edited by designers.

    [Tooltip("Health phase settings that define which pattern ids are available per phase.")]
    [SerializeField] private HealthPhaseSettings[] _healthPhaseSettings = new HealthPhaseSettings[0]; // Health ratio based phase settings for future selection.

    [Tooltip("Usage limits that future pattern selection can apply.")]
    [SerializeField] private PatternUsageLimit[] _usageLimits = new PatternUsageLimit[0]; // Pattern usage limits for future selection logic.

    [Header("Pattern 1 - Fan Projectile")]
    [Tooltip("Pure settings entries for fan projectile patterns. Each entry is resolved by PatternId.")]
    [SerializeField] private FanProjectilePatternSettings[] _fanProjectilePatterns = new FanProjectilePatternSettings[0]; // Pattern 1 pure configuration entries resolved by PatternId.

    [Header("Pattern 2 - Ground Spike")]
    [Tooltip("Pure settings entries for ground spike patterns. Each entry is resolved by PatternId.")]
    [SerializeField] private GroundSpikePatternSettings[] _groundSpikePatterns = new GroundSpikePatternSettings[0]; // Pattern 2 pure configuration entries resolved by PatternId.

    [Header("Pattern 3 - Summon Monster")]
    [Tooltip("Pure settings entries for summon monster patterns. Each entry is resolved by PatternId.")]
    [SerializeField] private SummonMonsterPatternSettings[] _summonMonsterPatterns = new SummonMonsterPatternSettings[0]; // Pattern 3 pure configuration entries resolved by PatternId.

    [Header("Pattern 4 - Weak Point")]
    [Tooltip("Pure settings entries for weak point patterns. Each entry is resolved by PatternId.")]
    [SerializeField] private WeakPointPatternSettings[] _weakPointPatterns = new WeakPointPatternSettings[0]; // Pattern 4 pure configuration entries resolved by PatternId.

    /// <summary>
    /// Corrects invalid inspector values and reports data authoring issues.
    /// </summary>
    private void OnValidate()
    {
        ValidateCommonSettings();
        ValidateUsageLimits();
        ValidateHealthPhaseSettings();
        ValidatePatternSpecificSettings();
    }

    /// <summary>
    /// Gets the common pattern settings array.
    /// </summary>
    public PatternCommonSettings[] CommonSettings => _commonSettings;

    /// <summary>
    /// Gets the health phase settings array.
    /// </summary>
    public HealthPhaseSettings[] HealthPhaseSettings => _healthPhaseSettings;

    /// <summary>
    /// Gets the usage limit settings array.
    /// </summary>
    public PatternUsageLimit[] UsageLimits => _usageLimits;

    /// <summary>
    /// Gets the first fan projectile pattern settings for legacy inspector/debug reads.
    /// </summary>
    public FanProjectilePatternSettings FanProjectilePattern => _fanProjectilePatterns != null && _fanProjectilePatterns.Length > 0 ? _fanProjectilePatterns[0] : default;

    /// <summary>
    /// Gets the fan projectile pattern settings array.
    /// </summary>
    public FanProjectilePatternSettings[] FanProjectilePatterns => _fanProjectilePatterns;

    /// <summary>
    /// Gets the first ground spike pattern settings for legacy inspector/debug reads.
    /// </summary>
    public GroundSpikePatternSettings GroundSpikePattern => _groundSpikePatterns != null && _groundSpikePatterns.Length > 0 ? _groundSpikePatterns[0] : default;

    /// <summary>
    /// Gets the ground spike pattern settings array.
    /// </summary>
    public GroundSpikePatternSettings[] GroundSpikePatterns => _groundSpikePatterns;

    /// <summary>
    /// Gets the first summon monster pattern settings for legacy inspector/debug reads.
    /// </summary>
    public SummonMonsterPatternSettings SummonMonsterPattern => _summonMonsterPatterns != null && _summonMonsterPatterns.Length > 0 ? _summonMonsterPatterns[0] : default;

    /// <summary>
    /// Gets the summon monster pattern settings array.
    /// </summary>
    public SummonMonsterPatternSettings[] SummonMonsterPatterns => _summonMonsterPatterns;

    /// <summary>
    /// Gets the first weak point pattern settings for legacy inspector/debug reads.
    /// </summary>
    public WeakPointPatternSettings WeakPointPattern => _weakPointPatterns != null && _weakPointPatterns.Length > 0 ? _weakPointPatterns[0] : default;

    /// <summary>
    /// Gets the weak point pattern settings array.
    /// </summary>
    public WeakPointPatternSettings[] WeakPointPatterns => _weakPointPatterns;

    /// <summary>
    /// Finds the first fan projectile settings entry with the requested PatternId.
    /// </summary>
    public bool TryGetFanProjectilePattern(string patternId, out FanProjectilePatternSettings settings)
    {
        settings = default;
        return TryGetPatternSettingsById(_fanProjectilePatterns, patternId, out settings);
    }

    /// <summary>
    /// Finds the first ground spike settings entry with the requested PatternId.
    /// </summary>
    public bool TryGetGroundSpikePattern(string patternId, out GroundSpikePatternSettings settings)
    {
        settings = default;
        return TryGetPatternSettingsById(_groundSpikePatterns, patternId, out settings);
    }

    /// <summary>
    /// Finds the first summon monster settings entry with the requested PatternId.
    /// </summary>
    public bool TryGetSummonMonsterPattern(string patternId, out SummonMonsterPatternSettings settings)
    {
        settings = default;
        return TryGetPatternSettingsById(_summonMonsterPatterns, patternId, out settings);
    }

    /// <summary>
    /// Finds the first weak point settings entry with the requested PatternId.
    /// </summary>
    public bool TryGetWeakPointPattern(string patternId, out WeakPointPatternSettings settings)
    {
        settings = default;
        return TryGetPatternSettingsById(_weakPointPatterns, patternId, out settings);
    }

    /// <summary>
    /// Corrects common pattern settings and reports invalid or duplicate pattern ids.
    /// </summary>
    private void ValidateCommonSettings()
    {
        if (_commonSettings == null)
        {
            return;
        }

        for (int index = 0; index < _commonSettings.Length; index++)
        {
            PatternCommonSettings settings = _commonSettings[index]; // Struct copy used so corrected values can be assigned back to the serialized array.
            settings.ValidateOnValidate(this, index);
            _commonSettings[index] = settings;
        }

        for (int outerIndex = 0; outerIndex < _commonSettings.Length; outerIndex++)
        {
            string outerPatternId = _commonSettings[outerIndex].PatternId; // Pattern id currently checked for duplicate entries.
            if (string.IsNullOrWhiteSpace(outerPatternId))
            {
                Debug.LogWarning($"[BossPatternData] PatternCommonSettings PatternId is empty. index={outerIndex}", this);
                continue;
            }

            for (int innerIndex = outerIndex + 1; innerIndex < _commonSettings.Length; innerIndex++)
            {
                if (_commonSettings[innerIndex].PatternId != outerPatternId)
                {
                    continue;
                }

                Debug.LogWarning($"[BossPatternData] Duplicate PatternId found in PatternCommonSettings. patternId={outerPatternId}, firstIndex={outerIndex}, duplicateIndex={innerIndex}. The first entry is used and the duplicate is ignored at runtime.", this);
                break;
            }
        }
    }

    /// <summary>
    /// Corrects usage limit settings and reports duplicate PatternId entries inside the same HealthPhase.
    /// </summary>
    private void ValidateUsageLimits()
    {
        if (_usageLimits == null)
        {
            return;
        }

        for (int index = 0; index < _usageLimits.Length; index++)
        {
            PatternUsageLimit usageLimit = _usageLimits[index]; // Struct copy used so corrected values can be assigned back to the serialized array.
            usageLimit.ValidateOnValidate(this, index);
            _usageLimits[index] = usageLimit;
        }

        for (int outerIndex = 0; outerIndex < _usageLimits.Length; outerIndex++)
        {
            PatternUsageLimit outer = _usageLimits[outerIndex]; // Usage limit currently checked for duplicate phase-local PatternId entries.
            if (string.IsNullOrWhiteSpace(outer.PatternId))
            {
                continue;
            }

            for (int innerIndex = outerIndex + 1; innerIndex < _usageLimits.Length; innerIndex++)
            {
                PatternUsageLimit inner = _usageLimits[innerIndex]; // Later usage limit compared against the first matching entry.
                if (inner.PhaseIndex != outer.PhaseIndex || inner.PatternId != outer.PatternId)
                {
                    continue;
                }

                Debug.LogWarning($"[BossPatternData] Duplicate UsageLimit found for the same PhaseIndex and PatternId. phaseIndex={outer.PhaseIndex}, patternId={outer.PatternId}, firstIndex={outerIndex}, duplicateIndex={innerIndex}. The first entry is used.", this);
                break;
            }
        }
    }

    /// <summary>
    /// Corrects health phase ranges and reports empty or overlapping ranges.
    /// </summary>
    private void ValidateHealthPhaseSettings()
    {
        if (_healthPhaseSettings == null || _healthPhaseSettings.Length == 0)
        {
            Debug.LogWarning("[BossPatternData] HealthPhaseSettings is empty. Boss phase availability cannot be validated.", this);
            return;
        }

        for (int index = 0; index < _healthPhaseSettings.Length; index++)
        {
            HealthPhaseSettings settings = _healthPhaseSettings[index]; // Struct copy used so corrected values can be assigned back to the serialized array.
            settings.ValidateOnValidate(this, index);
            _healthPhaseSettings[index] = settings;

            ValidateHealthPhasePatternIds(settings, index);
        }

        for (int outerIndex = 0; outerIndex < _healthPhaseSettings.Length; outerIndex++)
        {
            HealthPhaseSettings outer = _healthPhaseSettings[outerIndex]; // First health phase range used for overlap comparison.
            for (int innerIndex = outerIndex + 1; innerIndex < _healthPhaseSettings.Length; innerIndex++)
            {
                HealthPhaseSettings inner = _healthPhaseSettings[innerIndex]; // Second health phase range used for overlap comparison.
                if (!DoHealthPhaseRangesOverlap(outer, inner))
                {
                    continue;
                }

                Debug.LogWarning($"[BossPatternData] HealthPhaseSettings ranges overlap. firstIndex={outerIndex}, secondIndex={innerIndex}, first=({outer.MinHealthRatio}, {outer.MaxHealthRatio}], second=({inner.MinHealthRatio}, {inner.MaxHealthRatio}]", this);
                break;
            }
        }

        ValidateHealthPhaseCoverage();
    }

    /// <summary>
    /// Corrects pattern-specific settings and validates enabled pattern prefab references.
    /// </summary>
    private void ValidatePatternSpecificSettings()
    {
        ValidateFanProjectileSettings();
        ValidateGroundSpikeSettings();
        ValidateSummonMonsterSettings();
        ValidateWeakPointSettings();
    }

    /// <summary>
    /// Validates every fan projectile settings entry and reports duplicate PatternIds.
    /// </summary>
    private void ValidateFanProjectileSettings()
    {
        if (_fanProjectilePatterns == null)
        {
            return;
        }

        for (int index = 0; index < _fanProjectilePatterns.Length; index++)
        {
            FanProjectilePatternSettings settings = _fanProjectilePatterns[index]; // Struct copy used so corrected values can be assigned back to the serialized array.
            settings.ValidateOnValidate(this);
            _fanProjectilePatterns[index] = settings;
        }

        ValidatePatternSettingsIds(_fanProjectilePatterns, "FanProjectile");
    }

    /// <summary>
    /// Validates every ground spike settings entry and reports duplicate PatternIds.
    /// </summary>
    private void ValidateGroundSpikeSettings()
    {
        if (_groundSpikePatterns == null)
        {
            return;
        }

        for (int index = 0; index < _groundSpikePatterns.Length; index++)
        {
            GroundSpikePatternSettings settings = _groundSpikePatterns[index]; // Struct copy used so corrected values can be assigned back to the serialized array.
            settings.ValidateOnValidate(this);
            _groundSpikePatterns[index] = settings;
        }

        ValidatePatternSettingsIds(_groundSpikePatterns, "GroundSpike");
    }

    /// <summary>
    /// Validates every summon monster settings entry and reports duplicate PatternIds.
    /// </summary>
    private void ValidateSummonMonsterSettings()
    {
        if (_summonMonsterPatterns == null)
        {
            return;
        }

        for (int index = 0; index < _summonMonsterPatterns.Length; index++)
        {
            SummonMonsterPatternSettings settings = _summonMonsterPatterns[index]; // Struct copy used so corrected values can be assigned back to the serialized array.
            settings.ValidateOnValidate(this);
            _summonMonsterPatterns[index] = settings;
        }

        ValidatePatternSettingsIds(_summonMonsterPatterns, "SummonMonster");
    }

    /// <summary>
    /// Validates every weak point settings entry and reports duplicate PatternIds.
    /// </summary>
    private void ValidateWeakPointSettings()
    {
        if (_weakPointPatterns == null)
        {
            return;
        }

        for (int index = 0; index < _weakPointPatterns.Length; index++)
        {
            WeakPointPatternSettings settings = _weakPointPatterns[index]; // Struct copy used so corrected values can be assigned back to the serialized array.
            settings.ValidateOnValidate(this);
            _weakPointPatterns[index] = settings;
        }

        ValidatePatternSettingsIds(_weakPointPatterns, "WeakPoint");
    }

    /// <summary>
    /// Returns whether two health phase ranges overlap.
    /// </summary>
    private bool DoHealthPhaseRangesOverlap(HealthPhaseSettings first, HealthPhaseSettings second)
    {
        return first.MinHealthRatio < second.MaxHealthRatio && second.MinHealthRatio < first.MaxHealthRatio;
    }

    /// <summary>
    /// Finds the first fan projectile settings entry by PatternId.
    /// </summary>
    private bool TryGetPatternSettingsById(FanProjectilePatternSettings[] settingsArray, string patternId, out FanProjectilePatternSettings settings)
    {
        settings = default;
        if (settingsArray == null || string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        for (int index = 0; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            settings = settingsArray[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the first ground spike settings entry by PatternId.
    /// </summary>
    private bool TryGetPatternSettingsById(GroundSpikePatternSettings[] settingsArray, string patternId, out GroundSpikePatternSettings settings)
    {
        settings = default;
        if (settingsArray == null || string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        for (int index = 0; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            settings = settingsArray[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the first summon monster settings entry by PatternId.
    /// </summary>
    private bool TryGetPatternSettingsById(SummonMonsterPatternSettings[] settingsArray, string patternId, out SummonMonsterPatternSettings settings)
    {
        settings = default;
        if (settingsArray == null || string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        for (int index = 0; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            settings = settingsArray[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the first weak point settings entry by PatternId.
    /// </summary>
    private bool TryGetPatternSettingsById(WeakPointPatternSettings[] settingsArray, string patternId, out WeakPointPatternSettings settings)
    {
        settings = default;
        if (settingsArray == null || string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        for (int index = 0; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            settings = settingsArray[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reports invalid and duplicate PatternIds in fan projectile settings.
    /// </summary>
    private void ValidatePatternSettingsIds(FanProjectilePatternSettings[] settingsArray, string label)
    {
        for (int index = 0; index < settingsArray.Length; index++)
        {
            ValidatePatternSettingsId(settingsArray[index].PatternId, index, label);
            ValidateDuplicatePatternSettingsId(settingsArray, index, label);
        }
    }

    /// <summary>
    /// Reports invalid and duplicate PatternIds in ground spike settings.
    /// </summary>
    private void ValidatePatternSettingsIds(GroundSpikePatternSettings[] settingsArray, string label)
    {
        for (int index = 0; index < settingsArray.Length; index++)
        {
            ValidatePatternSettingsId(settingsArray[index].PatternId, index, label);
            ValidateDuplicatePatternSettingsId(settingsArray, index, label);
        }
    }

    /// <summary>
    /// Reports invalid and duplicate PatternIds in summon monster settings.
    /// </summary>
    private void ValidatePatternSettingsIds(SummonMonsterPatternSettings[] settingsArray, string label)
    {
        for (int index = 0; index < settingsArray.Length; index++)
        {
            ValidatePatternSettingsId(settingsArray[index].PatternId, index, label);
            ValidateDuplicatePatternSettingsId(settingsArray, index, label);
        }
    }

    /// <summary>
    /// Reports invalid and duplicate PatternIds in weak point settings.
    /// </summary>
    private void ValidatePatternSettingsIds(WeakPointPatternSettings[] settingsArray, string label)
    {
        for (int index = 0; index < settingsArray.Length; index++)
        {
            ValidatePatternSettingsId(settingsArray[index].PatternId, index, label);
            ValidateDuplicatePatternSettingsId(settingsArray, index, label);
        }
    }

    /// <summary>
    /// Reports an empty PatternId in a pattern-specific settings array.
    /// </summary>
    private void ValidatePatternSettingsId(string patternId, int index, string label)
    {
        if (!string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        Debug.LogWarning($"[BossPatternData] {label} settings PatternId is empty. index={index}", this);
    }

    /// <summary>
    /// Reports duplicate PatternIds in fan projectile settings.
    /// </summary>
    private void ValidateDuplicatePatternSettingsId(FanProjectilePatternSettings[] settingsArray, int firstIndex, string label)
    {
        string patternId = settingsArray[firstIndex].PatternId; // PatternId used to detect duplicate detail settings entries.
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        for (int index = firstIndex + 1; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            Debug.LogWarning($"[BossPatternData] Duplicate {label} settings PatternId found. patternId={patternId}, firstIndex={firstIndex}, duplicateIndex={index}. The first entry is used.", this);
            return;
        }
    }

    /// <summary>
    /// Reports duplicate PatternIds in ground spike settings.
    /// </summary>
    private void ValidateDuplicatePatternSettingsId(GroundSpikePatternSettings[] settingsArray, int firstIndex, string label)
    {
        string patternId = settingsArray[firstIndex].PatternId; // PatternId used to detect duplicate detail settings entries.
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        for (int index = firstIndex + 1; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            Debug.LogWarning($"[BossPatternData] Duplicate {label} settings PatternId found. patternId={patternId}, firstIndex={firstIndex}, duplicateIndex={index}. The first entry is used.", this);
            return;
        }
    }

    /// <summary>
    /// Reports duplicate PatternIds in summon monster settings.
    /// </summary>
    private void ValidateDuplicatePatternSettingsId(SummonMonsterPatternSettings[] settingsArray, int firstIndex, string label)
    {
        string patternId = settingsArray[firstIndex].PatternId; // PatternId used to detect duplicate detail settings entries.
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        for (int index = firstIndex + 1; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            Debug.LogWarning($"[BossPatternData] Duplicate {label} settings PatternId found. patternId={patternId}, firstIndex={firstIndex}, duplicateIndex={index}. The first entry is used.", this);
            return;
        }
    }

    /// <summary>
    /// Reports duplicate PatternIds in weak point settings.
    /// </summary>
    private void ValidateDuplicatePatternSettingsId(WeakPointPatternSettings[] settingsArray, int firstIndex, string label)
    {
        string patternId = settingsArray[firstIndex].PatternId; // PatternId used to detect duplicate detail settings entries.
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        for (int index = firstIndex + 1; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            Debug.LogWarning($"[BossPatternData] Duplicate {label} settings PatternId found. patternId={patternId}, firstIndex={firstIndex}, duplicateIndex={index}. The first entry is used.", this);
            return;
        }
    }

    /// <summary>
    /// Reports missing PatternId references inside one HealthPhase entry.
    /// </summary>
    private void ValidateHealthPhasePatternIds(HealthPhaseSettings settings, int phaseArrayIndex)
    {
        string[] availablePatternIds = settings.AvailablePatternIds; // PatternId list that this HealthPhase can select from.
        if (availablePatternIds == null || availablePatternIds.Length == 0)
        {
            Debug.LogWarning($"[BossPatternData] HealthPhaseSettings has no available PatternIds. index={phaseArrayIndex}, phase={settings.PhaseIndex}", this);
            return;
        }

        for (int index = 0; index < availablePatternIds.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(availablePatternIds[index]))
            {
                continue;
            }

            Debug.LogWarning($"[BossPatternData] HealthPhaseSettings contains an empty PatternId. phaseIndex={settings.PhaseIndex}, arrayIndex={phaseArrayIndex}, patternIndex={index}", this);
        }
    }

    /// <summary>
    /// Reports positive health-ratio gaps that cannot match any configured HealthPhase.
    /// </summary>
    private void ValidateHealthPhaseCoverage()
    {
        float cursor = 1f; // Highest uncovered positive health ratio boundary while scanning downward.
        for (int step = 0; step < _healthPhaseSettings.Length; step++)
        {
            int bestIndex = -1; // Index of the phase with the highest MaxHealthRatio not above the current cursor.
            float bestMax = -1f; // MaxHealthRatio of the selected phase.
            for (int index = 0; index < _healthPhaseSettings.Length; index++)
            {
                float candidateMax = _healthPhaseSettings[index].MaxHealthRatio; // Candidate upper bound compared without allocating sorted copies.
                if (candidateMax > cursor || candidateMax <= bestMax)
                {
                    continue;
                }

                bestMax = candidateMax;
                bestIndex = index;
            }

            if (bestIndex < 0)
            {
                if (cursor > 0f)
                {
                    Debug.LogWarning($"[BossPatternData] HealthPhaseSettings has an uncovered health ratio gap. gap=(0, {cursor}]", this);
                }

                return;
            }

            HealthPhaseSettings phase = _healthPhaseSettings[bestIndex]; // Phase that should cover the current upper cursor.
            if (phase.MaxHealthRatio < cursor)
            {
                Debug.LogWarning($"[BossPatternData] HealthPhaseSettings has an uncovered health ratio gap. gap=({phase.MaxHealthRatio}, {cursor}]", this);
            }

            cursor = phase.MinHealthRatio;
            if (cursor <= 0f)
            {
                return;
            }
        }

        if (cursor > 0f)
        {
            Debug.LogWarning($"[BossPatternData] HealthPhaseSettings has an uncovered health ratio gap. gap=(0, {cursor}]", this);
        }
    }
}
