using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Replays boss presentation cues on clients and host through existing Animator, EffectService, and AudioManager systems.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossPresentationController : NetworkBehaviour
{
    [Serializable]
    private struct BossPresentationCueSettings
    {
        [Tooltip("Presentation cue this mapping handles.")]
        [SerializeField] private E_BossPresentationCue _cue; // Cue identifier received from boss state and pattern flow.

        [Tooltip("Optional pattern filter. None means this mapping can be used for every pattern type.")]
        [SerializeField] private E_BossPatternType _patternType; // Pattern filter used when pattern-specific presentation is needed.

        [Tooltip("Animator trigger name invoked when this cue is played. Empty value skips Animator playback.")]
        [SerializeField] private string _animatorTriggerName; // Existing Animator trigger used by boss presentation.

        [Tooltip("EffectService id played at the synchronized cue position. None skips VFX playback.")]
        [SerializeField] private E_EffectId _effectId; // Existing VFX system id used for cue playback.

        [Tooltip("Sound id played at the synchronized cue position. None skips SFX playback.")]
        [SerializeField] private E_SoundId _soundId; // Existing AudioManager sound id used for cue playback.

        /// <summary>
        /// Gets the cue handled by this mapping.
        /// </summary>
        public E_BossPresentationCue Cue => _cue;

        /// <summary>
        /// Gets the pattern filter for this mapping.
        /// </summary>
        public E_BossPatternType PatternType => _patternType;

        /// <summary>
        /// Gets the Animator trigger name.
        /// </summary>
        public string AnimatorTriggerName => _animatorTriggerName;

        /// <summary>
        /// Gets the EffectService id.
        /// </summary>
        public E_EffectId EffectId => _effectId;

        /// <summary>
        /// Gets the AudioManager sound id.
        /// </summary>
        public E_SoundId SoundId => _soundId;
    }

    [Header("Required References")]
    [Tooltip("Animator that receives boss presentation triggers. Empty value is resolved from children at runtime.")]
    [SerializeField] private Animator _animator; // Existing Animation system target used by boss presentation cues.

    [Tooltip("Transform used as the default presentation position when a cue does not provide a specific position.")]
    [SerializeField] private Transform _presentationOrigin; // Default world position source for VFX and SFX.

    [Header("Presentation Mapping")]
    [Tooltip("Designer-authored mapping from boss presentation cues to Animator, VFX, and Sound playback.")]
    [SerializeField] private BossPresentationCueSettings[] _cueSettings; // Cue mapping array evaluated without allocations when presentation is played.

    private bool _hasLoggedMissingCueSettingsWarning; // Prevents repeated warnings when a cue has no configured mapping.
    private bool _hasLoggedEffectServiceMissingWarning; // Prevents repeated warnings when EffectService is missing.
    private bool _hasLoggedAudioManagerMissingWarning; // Prevents repeated warnings when AudioManager is missing.
    private bool _hasLoggedNetworkFallbackWarning; // Prevents repeated warnings when network presentation falls back to local playback.
    private bool _hasLoggedInvalidPositionWarning; // Prevents repeated warnings when cue position falls back to presentation origin.

    /// <summary>
    /// Resolves optional presentation references before cues are played.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Refreshes presentation references while designers edit the boss object.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Plays a presentation cue locally in single-player or synchronizes it to clients and host in a network session.
    /// </summary>
    public void PlayCue(E_BossPresentationCue cue, E_BossPatternType patternType, Vector3 worldPosition)
    {
        if (cue == E_BossPresentationCue.None)
        {
            Debug.LogWarning($"[BossPresentationController] PlayCue received None. object={name}", this);
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // NGO singleton used to decide whether presentation must be synchronized.
        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;
        if (shouldUseNetwork && IsSpawned)
        {
            PlayCueRpc((int)cue, (int)patternType, worldPosition);
            return;
        }

        if (shouldUseNetwork && !IsSpawned && !_hasLoggedNetworkFallbackWarning)
        {
            Debug.LogWarning($"[BossPresentationController] NetworkObject is not spawned. Presentation cue falls back to local playback. object={name}, cue={cue}", this);
            _hasLoggedNetworkFallbackWarning = true;
        }

        PlayCueLocal(cue, patternType, worldPosition);
    }

    /// <summary>
    /// Receives an authority-confirmed presentation cue and replays it only as local presentation.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void PlayCueRpc(int cueValue, int patternTypeValue, Vector3 worldPosition)
    {
        E_BossPresentationCue cue = (E_BossPresentationCue)cueValue; // Network payload converted back to presentation cue.
        E_BossPatternType patternType = (E_BossPatternType)patternTypeValue; // Network payload converted back to pattern type.
        PlayCueLocal(cue, patternType, worldPosition);
    }

    /// <summary>
    /// Plays Animator, VFX, and SFX for one cue without mutating boss combat state.
    /// </summary>
    private void PlayCueLocal(E_BossPresentationCue cue, E_BossPatternType patternType, Vector3 worldPosition)
    {
        ResolveReferences();
        if (!TryGetCueSettings(cue, patternType, out BossPresentationCueSettings settings))
        {
            return;
        }

        Vector3 cuePosition = ResolvePresentationPosition(worldPosition); // Final world position used by local VFX and SFX playback.
        PlayAnimatorTrigger(settings);
        PlayVfx(settings, cuePosition);
        PlaySfx(settings, cuePosition);
    }

    /// <summary>
    /// Returns a valid world position for presentation playback and falls back to the configured origin when needed.
    /// </summary>
    private Vector3 ResolvePresentationPosition(Vector3 worldPosition)
    {
        if (IsFinitePosition(worldPosition))
        {
            return worldPosition;
        }

        if (!_hasLoggedInvalidPositionWarning)
        {
            Debug.LogWarning($"[BossPresentationController] Presentation cue position was invalid and fell back to origin. object={name}", this);
            _hasLoggedInvalidPositionWarning = true;
        }

        return _presentationOrigin != null ? _presentationOrigin.position : transform.position;
    }

    /// <summary>
    /// Returns whether a world position can be used by Animator, VFX, and SFX playback.
    /// </summary>
    private bool IsFinitePosition(Vector3 worldPosition)
    {
        return float.IsNaN(worldPosition.x) == false
            && float.IsNaN(worldPosition.y) == false
            && float.IsNaN(worldPosition.z) == false
            && float.IsInfinity(worldPosition.x) == false
            && float.IsInfinity(worldPosition.y) == false
            && float.IsInfinity(worldPosition.z) == false;
    }

    /// <summary>
    /// Finds the best cue settings entry for a cue and pattern type.
    /// </summary>
    private bool TryGetCueSettings(E_BossPresentationCue cue, E_BossPatternType patternType, out BossPresentationCueSettings settings)
    {
        settings = default;
        if (_cueSettings == null || _cueSettings.Length == 0)
        {
            LogMissingCueSettingsOnce(cue, patternType);
            return false;
        }

        int fallbackIndex = -1; // Index for a cue mapping that accepts every pattern type.
        for (int index = 0; index < _cueSettings.Length; index++)
        {
            BossPresentationCueSettings candidate = _cueSettings[index]; // Current designer-authored presentation mapping candidate.
            if (candidate.Cue != cue)
            {
                continue;
            }

            if (candidate.PatternType == patternType)
            {
                settings = candidate;
                return true;
            }

            if (candidate.PatternType == E_BossPatternType.None && fallbackIndex < 0)
            {
                fallbackIndex = index;
            }
        }

        if (fallbackIndex >= 0)
        {
            settings = _cueSettings[fallbackIndex];
            return true;
        }

        LogMissingCueSettingsOnce(cue, patternType);
        return false;
    }

    /// <summary>
    /// Plays the configured Animator trigger if one exists.
    /// </summary>
    private void PlayAnimatorTrigger(BossPresentationCueSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AnimatorTriggerName))
        {
            return;
        }

        if (_animator == null)
        {
            Debug.LogWarning($"[BossPresentationController] Animator is missing for cue playback. object={name}, trigger={settings.AnimatorTriggerName}", this);
            return;
        }

        _animator.ResetTrigger(settings.AnimatorTriggerName);
        _animator.SetTrigger(settings.AnimatorTriggerName);
    }

    /// <summary>
    /// Plays the configured VFX through EffectService if one exists.
    /// </summary>
    private void PlayVfx(BossPresentationCueSettings settings, Vector3 worldPosition)
    {
        if (settings.EffectId == E_EffectId.None)
        {
            return;
        }

        if (EffectService.Instance == null)
        {
            if (!_hasLoggedEffectServiceMissingWarning)
            {
                Debug.LogWarning($"[BossPresentationController] EffectService is missing for boss presentation. object={name}, effectId={settings.EffectId}", this);
                _hasLoggedEffectServiceMissingWarning = true;
            }

            return;
        }

        EffectService.Instance.Play(settings.EffectId, worldPosition);
    }

    /// <summary>
    /// Plays the configured SFX through AudioManager if one exists.
    /// </summary>
    private void PlaySfx(BossPresentationCueSettings settings, Vector3 worldPosition)
    {
        if (settings.SoundId == E_SoundId.None)
        {
            return;
        }

        AudioManager audioManager = AudioManager.Instance; // Existing sound system used for boss presentation SFX.
        if (audioManager == null)
        {
            if (!_hasLoggedAudioManagerMissingWarning)
            {
                Debug.LogWarning($"[BossPresentationController] AudioManager is missing for boss presentation. object={name}, soundId={settings.SoundId}", this);
                _hasLoggedAudioManagerMissingWarning = true;
            }

            return;
        }

        audioManager.PlaySfx(settings.SoundId, worldPosition);
    }

    /// <summary>
    /// Logs a missing cue mapping once when designers have not configured presentation for a cue.
    /// </summary>
    private void LogMissingCueSettingsOnce(E_BossPresentationCue cue, E_BossPatternType patternType)
    {
        if (_hasLoggedMissingCueSettingsWarning)
        {
            return;
        }

        Debug.LogWarning($"[BossPresentationController] Presentation cue mapping is missing. object={name}, cue={cue}, patternType={patternType}", this);
        _hasLoggedMissingCueSettingsWarning = true;
    }

    /// <summary>
    /// Resolves optional presentation references from the boss hierarchy.
    /// </summary>
    private void ResolveReferences()
    {
        if (_presentationOrigin == null)
        {
            _presentationOrigin = transform;
        }

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>(true);
        }
    }
}
