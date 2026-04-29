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
    [Tooltip("Pure settings for the fan projectile pattern.")]
    [SerializeField] private FanProjectilePatternSettings _fanProjectilePattern; // Pattern 1 pure configuration values.

    [Header("Pattern 2 - Ground Spike")]
    [Tooltip("Pure settings for the ground spike pattern.")]
    [SerializeField] private GroundSpikePatternSettings _groundSpikePattern; // Pattern 2 pure configuration values.

    [Header("Pattern 3 - Summon Monster")]
    [Tooltip("Pure settings for the summon monster pattern.")]
    [SerializeField] private SummonMonsterPatternSettings _summonMonsterPattern; // Pattern 3 pure configuration values.

    [Header("Pattern 4 - Weak Point")]
    [Tooltip("Pure settings for the weak point pattern.")]
    [SerializeField] private WeakPointPatternSettings _weakPointPattern; // Pattern 4 pure configuration values.

    /// <summary>
    /// Corrects invalid inspector values and reports data authoring issues.
    /// </summary>
    private void OnValidate()
    {
        ValidateCommonSettings();
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
    /// Gets the fan projectile pattern settings.
    /// </summary>
    public FanProjectilePatternSettings FanProjectilePattern => _fanProjectilePattern;

    /// <summary>
    /// Gets the ground spike pattern settings.
    /// </summary>
    public GroundSpikePatternSettings GroundSpikePattern => _groundSpikePattern;

    /// <summary>
    /// Gets the summon monster pattern settings.
    /// </summary>
    public SummonMonsterPatternSettings SummonMonsterPattern => _summonMonsterPattern;

    /// <summary>
    /// Gets the weak point pattern settings.
    /// </summary>
    public WeakPointPatternSettings WeakPointPattern => _weakPointPattern;

    /// <summary>
    /// Corrects common pattern settings and reports duplicate pattern types.
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
            E_BossPatternType outerType = _commonSettings[outerIndex].PatternType; // Pattern type currently checked for duplicate entries.
            if (outerType == E_BossPatternType.None)
            {
                continue;
            }

            for (int innerIndex = outerIndex + 1; innerIndex < _commonSettings.Length; innerIndex++)
            {
                if (_commonSettings[innerIndex].PatternType != outerType)
                {
                    continue;
                }

                Debug.LogWarning($"[BossPatternData] Duplicate PatternType found in PatternCommonSettings. type={outerType}, firstIndex={outerIndex}, duplicateIndex={innerIndex}", this);
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

            if (Mathf.Approximately(settings.MinHealthRatio, settings.MaxHealthRatio))
            {
                Debug.LogWarning($"[BossPatternData] HealthPhaseSettings range is empty. index={index}, phase={settings.PhaseIndex}, min={settings.MinHealthRatio}, max={settings.MaxHealthRatio}", this);
            }
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

                Debug.LogWarning($"[BossPatternData] HealthPhaseSettings ranges overlap. firstIndex={outerIndex}, secondIndex={innerIndex}, first={outer.MinHealthRatio}-{outer.MaxHealthRatio}, second={inner.MinHealthRatio}-{inner.MaxHealthRatio}", this);
                break;
            }
        }
    }

    /// <summary>
    /// Corrects pattern-specific settings and validates enabled pattern prefab references.
    /// </summary>
    private void ValidatePatternSpecificSettings()
    {
        _fanProjectilePattern.ValidateOnValidate(this);
        _groundSpikePattern.ValidateOnValidate(this);
        _summonMonsterPattern.ValidateOnValidate(this);
        _weakPointPattern.ValidateOnValidate(this);
    }

    /// <summary>
    /// Returns whether two health phase ranges overlap.
    /// </summary>
    private bool DoHealthPhaseRangesOverlap(HealthPhaseSettings first, HealthPhaseSettings second)
    {
        return first.MinHealthRatio <= second.MaxHealthRatio && second.MinHealthRatio <= first.MaxHealthRatio;
    }
}
