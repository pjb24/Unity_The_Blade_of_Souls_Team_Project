using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 기존 Animator, EffectService, AudioManager 시스템을 사용하여
/// 보스 연출 Cue를 클라이언트 및 호스트에서 재생한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossPresentationController : NetworkBehaviour
{
    [Serializable]
    private struct BossPresentationCueSettings
    {
        [Tooltip("이 매핑이 처리하는 연출 Cue")]
        [SerializeField] private E_BossPresentationCue _cue; // 보스 상태 및 패턴 흐름에서 전달되는 Cue 식별자

        [Tooltip("선택적 패턴 필터. None이면 모든 패턴에서 사용 가능")]
        [SerializeField] private E_BossPatternType _patternType; // 특정 패턴에만 적용할 때 사용하는 필터

        [Tooltip("Cue 재생 시 실행할 Animator 트리거 이름. 비어 있으면 Animator 재생 생략")]
        [SerializeField] private string _animatorTriggerName; // 보스 연출에서 사용하는 기존 Animator 트리거

        [Tooltip("동기화된 위치에서 재생할 EffectService ID. None이면 VFX 생략")]
        [SerializeField] private E_EffectId _effectId; // VFX 재생에 사용하는 기존 시스템 ID

        [Tooltip("동기화된 위치에서 재생할 사운드 ID. None이면 SFX 생략")]
        [SerializeField] private E_SoundId _soundId; // AudioManager에서 사용하는 사운드 ID

        /// <summary>
        /// 이 매핑이 처리하는 Cue 반환
        /// </summary>
        public E_BossPresentationCue Cue => _cue;

        /// <summary>
        /// 이 매핑의 패턴 필터 반환
        /// </summary>
        public E_BossPatternType PatternType => _patternType;

        /// <summary>
        /// Animator 트리거 이름 반환
        /// </summary>
        public string AnimatorTriggerName => _animatorTriggerName;

        /// <summary>
        /// EffectService ID 반환
        /// </summary>
        public E_EffectId EffectId => _effectId;

        /// <summary>
        /// AudioManager 사운드 ID 반환
        /// </summary>
        public E_SoundId SoundId => _soundId;
    }

    [Header("필수 참조")]
    [Tooltip("보스 연출 트리거를 받을 Animator. 비어 있으면 런타임에 자식에서 자동 탐색")]
    [SerializeField] private Animator _animator; // 보스 연출에 사용되는 Animator 대상

    [Tooltip("Cue에 위치가 없을 때 기본으로 사용할 연출 위치")]
    [SerializeField] private Transform _presentationOrigin; // VFX, SFX 기본 위치

    [Header("연출 매핑")]
    [Tooltip("보스 연출 Cue → Animator, VFX, SFX 매핑 데이터")]
    [SerializeField] private BossPresentationCueSettings[] _cueSettings; // 할당 없이 평가되는 매핑 배열

    private bool _hasLoggedMissingCueSettingsWarning; // 매핑 누락 경고 중복 방지
    private bool _hasLoggedEffectServiceMissingWarning; // EffectService 없음 경고 중복 방지
    private bool _hasLoggedAudioManagerMissingWarning; // AudioManager 없음 경고 중복 방지
    private bool _hasLoggedNetworkFallbackWarning; // 네트워크 실패 → 로컬 fallback 경고 중복 방지
    private bool _hasLoggedInvalidPositionWarning; // 잘못된 위치 fallback 경고 중복 방지

    /// <summary>
    /// 연출 실행 전에 참조를 초기화한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 에디터에서 값 수정 시 참조를 갱신한다.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 싱글에서는 로컬 재생,
    /// 멀티에서는 클라이언트/호스트에 동기화하여 Cue를 재생한다.
    /// </summary>
    public void PlayCue(E_BossPresentationCue cue, E_BossPatternType patternType, Vector3 worldPosition)
    {
        if (cue == E_BossPresentationCue.None)
        {
            Debug.LogWarning($"[BossPresentationController] PlayCue에 None이 전달됨. object={name}", this);
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // NGO 싱글톤
        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;

        if (shouldUseNetwork && IsSpawned)
        {
            PlayCueRpc((int)cue, (int)patternType, worldPosition);
            return;
        }

        if (shouldUseNetwork && !IsSpawned && !_hasLoggedNetworkFallbackWarning)
        {
            Debug.LogWarning($"[BossPresentationController] NetworkObject가 Spawn되지 않아 로컬 재생으로 fallback. object={name}, cue={cue}", this);
            _hasLoggedNetworkFallbackWarning = true;
        }

        PlayCueLocal(cue, patternType, worldPosition);
    }

    /// <summary>
    /// 서버에서 승인된 Cue를 받아 로컬에서 재생한다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void PlayCueRpc(int cueValue, int patternTypeValue, Vector3 worldPosition)
    {
        E_BossPresentationCue cue = (E_BossPresentationCue)cueValue; // 네트워크 값 → enum 변환
        E_BossPatternType patternType = (E_BossPatternType)patternTypeValue; // 네트워크 값 → enum 변환
        PlayCueLocal(cue, patternType, worldPosition);
    }

    /// <summary>
    /// Animator, VFX, SFX를 실제로 재생한다.
    /// 전투 상태는 변경하지 않는다.
    /// </summary>
    private void PlayCueLocal(E_BossPresentationCue cue, E_BossPatternType patternType, Vector3 worldPosition)
    {
        ResolveReferences();

        if (!TryGetCueSettings(cue, patternType, out BossPresentationCueSettings settings))
        {
            return;
        }

        Vector3 cuePosition = ResolvePresentationPosition(worldPosition); // 최종 사용 위치

        PlayAnimatorTrigger(settings);
        PlayVfx(settings, cuePosition);
        PlaySfx(settings, cuePosition);
    }

    /// <summary>
    /// 유효한 위치를 반환한다.
    /// 잘못된 값이면 origin으로 fallback한다.
    /// </summary>
    private Vector3 ResolvePresentationPosition(Vector3 worldPosition)
    {
        if (IsFinitePosition(worldPosition))
        {
            return worldPosition;
        }

        if (!_hasLoggedInvalidPositionWarning)
        {
            Debug.LogWarning($"[BossPresentationController] 잘못된 위치 → origin으로 fallback. object={name}", this);
            _hasLoggedInvalidPositionWarning = true;
        }

        return _presentationOrigin != null ? _presentationOrigin.position : transform.position;
    }

    /// <summary>
    /// 위치 값이 정상인지 검사한다.
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
    /// Cue + Pattern에 맞는 설정을 찾는다.
    /// </summary>
    private bool TryGetCueSettings(E_BossPresentationCue cue, E_BossPatternType patternType, out BossPresentationCueSettings settings)
    {
        settings = default;

        if (_cueSettings == null || _cueSettings.Length == 0)
        {
            LogMissingCueSettingsOnce(cue, patternType);
            return false;
        }

        int fallbackIndex = -1; // PatternType None fallback

        for (int index = 0; index < _cueSettings.Length; index++)
        {
            BossPresentationCueSettings candidate = _cueSettings[index];

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
    /// Animator 트리거 실행
    /// </summary>
    private void PlayAnimatorTrigger(BossPresentationCueSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AnimatorTriggerName))
        {
            return;
        }

        if (_animator == null)
        {
            Debug.LogWarning($"[BossPresentationController] Animator 없음. object={name}, trigger={settings.AnimatorTriggerName}", this);
            return;
        }

        _animator.ResetTrigger(settings.AnimatorTriggerName);
        _animator.SetTrigger(settings.AnimatorTriggerName);
    }

    /// <summary>
    /// VFX 재생
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
                Debug.LogWarning($"[BossPresentationController] EffectService 없음. object={name}, effectId={settings.EffectId}", this);
                _hasLoggedEffectServiceMissingWarning = true;
            }

            return;
        }

        EffectService.Instance.Play(settings.EffectId, worldPosition);
    }

    /// <summary>
    /// SFX 재생
    /// </summary>
    private void PlaySfx(BossPresentationCueSettings settings, Vector3 worldPosition)
    {
        if (settings.SoundId == E_SoundId.None)
        {
            return;
        }

        AudioManager audioManager = AudioManager.Instance;

        if (audioManager == null)
        {
            if (!_hasLoggedAudioManagerMissingWarning)
            {
                Debug.LogWarning($"[BossPresentationController] AudioManager 없음. object={name}, soundId={settings.SoundId}", this);
                _hasLoggedAudioManagerMissingWarning = true;
            }

            return;
        }

        audioManager.PlaySfx(settings.SoundId, worldPosition);
    }

    /// <summary>
    /// 매핑 누락 경고 (1회만 출력)
    /// </summary>
    private void LogMissingCueSettingsOnce(E_BossPresentationCue cue, E_BossPatternType patternType)
    {
        if (_hasLoggedMissingCueSettingsWarning)
        {
            return;
        }

        Debug.LogWarning($"[BossPresentationController] Cue 매핑 없음. object={name}, cue={cue}, patternType={patternType}", this);
        _hasLoggedMissingCueSettingsWarning = true;
    }

    /// <summary>
    /// 참조 자동 설정
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
