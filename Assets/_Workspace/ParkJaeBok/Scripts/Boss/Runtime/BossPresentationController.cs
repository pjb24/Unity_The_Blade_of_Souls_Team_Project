using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 기존 Animator, EffectService, AudioManager 시스템을 사용해 보스 연출 Cue와 비주얼 위치 변화를 동기화한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossPresentationController : NetworkBehaviour
{
    [Serializable]
    private struct BossPresentationCueSettings
    {
        [Tooltip("이 매핑이 처리하는 연출 Cue")]
        [SerializeField] private E_BossPresentationCue _cue; // 보스 상태 및 패턴 흐름에서 전달되는 Cue 식별값
        [Tooltip("선택 패턴 필터입니다. None이면 모든 패턴에서 사용할 수 있습니다.")]
        [SerializeField] private E_BossPatternType _patternType; // 특정 패턴에만 적용할지 결정하는 필터

        [Tooltip("Cue 재생 시 실행할 Animator Trigger 이름입니다. 비워두면 Animator 재생을 생략합니다.")]
        [SerializeField] private string _animatorTriggerName; // 보스 연출에서 사용하는 Animator Trigger
        [Tooltip("동기화된 위치에서 재생할 EffectService ID입니다. None이면 VFX를 생략합니다.")]
        [SerializeField] private E_EffectId _effectId; // VFX 재생에 사용하는 EffectService ID

        [Tooltip("동기화된 위치에서 재생할 Sound ID입니다. None이면 SFX를 생략합니다.")]
        [SerializeField] private E_SoundId _soundId; // AudioManager에서 사용하는 사운드 ID

        /// <summary>
        /// 이 매핑이 처리하는 Cue를 반환한다.
        /// </summary>
        public E_BossPresentationCue Cue => _cue;

        /// <summary>
        /// 이 매핑의 패턴 필터를 반환한다.
        /// </summary>
        public E_BossPatternType PatternType => _patternType;

        /// <summary>
        /// Animator Trigger 이름을 반환한다.
        /// </summary>
        public string AnimatorTriggerName => _animatorTriggerName;

        /// <summary>
        /// EffectService ID를 반환한다.
        /// </summary>
        public E_EffectId EffectId => _effectId;

        /// <summary>
        /// AudioManager 사운드 ID를 반환한다.
        /// </summary>
        public E_SoundId SoundId => _soundId;
    }

    [Header("Required References")]
    [Tooltip("보스 연출 Trigger를 받을 Animator입니다. 비워두면 하위 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private Animator _animator; // 보스 연출에 사용하는 Animator 대상
    [Tooltip("Cue의 위치가 없을 때 기본으로 사용할 연출 위치입니다.")]
    [SerializeField] private Transform _presentationOrigin; // VFX, SFX 기본 위치

    [Header("Weak Point Visual Motion")]
    [Tooltip("약점 패턴 중 Y축으로 이동시킬 보스 비주얼 루트입니다. 비워두면 Animator Transform을 사용합니다.")]
    [SerializeField] private Transform _bossVisualRoot; // 약점 패턴 중 실제로 위아래 이동하는 보스 비주얼 Transform
    [Tooltip("약점 패턴이 활성화되었을 때 보스 비주얼이 원래 로컬 위치에서 위로 이동할 Y값입니다.")]
    [SerializeField] private float _weakPointVisualYOffset = 2f; // 약점 패턴 중 원래 위치에서 추가되는 로컬 Y 오프셋
    [Tooltip("보스 비주얼이 약점 패턴 위치까지 올라가는 데 걸리는 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _weakPointVisualRaiseSeconds = 0.5f; // 보스 비주얼 상승 보간 시간
    [Tooltip("보스 비주얼이 원래 위치로 내려오는 데 걸리는 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _weakPointVisualLowerSeconds = 0.5f; // 보스 비주얼 하강 보간 시간

    [Header("Cue Mapping")]
    [Tooltip("보스 연출 Cue 별 Animator, VFX, SFX 매핑 데이터입니다.")]
    [SerializeField] private BossPresentationCueSettings[] _cueSettings; // Cue별 연출 매핑 배열

    private Coroutine _weakPointVisualMoveCoroutine; // 보스 비주얼 Y 이동을 처리하는 현재 코루틴
    private Vector3 _bossVisualRestLocalPosition; // 약점 패턴이 없을 때 보스 비주얼의 기준 로컬 위치
    private bool _hasCapturedBossVisualRestPosition; // 기준 로컬 위치를 이미 기록했는지 여부
    private bool _hasLoggedMissingBossVisualRootWarning; // 보스 비주얼 루트 누락 경고 중복 방지
    private bool _hasLoggedMissingCueSettingsWarning; // 매핑 누락 경고 중복 방지
    private bool _hasLoggedEffectServiceMissingWarning; // EffectService 없음 경고 중복 방지
    private bool _hasLoggedAudioManagerMissingWarning; // AudioManager 없음 경고 중복 방지
    private bool _hasLoggedNetworkFallbackWarning; // 네트워크 실패 시 로컬 fallback 경고 중복 방지
    private bool _hasLoggedInvalidPositionWarning; // 잘못된 위치 fallback 경고 중복 방지

    /// <summary>
    /// 연출 실행 전에 참조와 비주얼 기준 위치를 초기화한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        CaptureBossVisualRestPosition();
    }

    /// <summary>
    /// 에디터에서 값이 수정될 때 참조와 설정값을 갱신한다.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
        ValidateWeakPointVisualSettings();
    }

    /// <summary>
    /// 컴포넌트가 비활성화될 때 진행 중인 비주얼 이동 코루틴을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        StopWeakPointVisualMoveCoroutine();
    }

    /// <summary>
    /// 싱글플레이에서는 로컬 재생, 멀티플레이에서는 클라이언트와 호스트에 Cue를 동기화해 재생한다.
    /// </summary>
    public void PlayCue(E_BossPresentationCue cue, E_BossPatternType patternType, Vector3 worldPosition)
    {
        if (cue == E_BossPresentationCue.None)
        {
            Debug.LogWarning($"[BossPresentationController] PlayCue에 None이 전달됨. object={name}", this);
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // NGO 세션 상태 확인용 매니저
        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;

        if (shouldUseNetwork && IsSpawned)
        {
            PlayCueRpc((int)cue, (int)patternType, worldPosition);
            return;
        }

        LogNetworkFallbackIfNeeded(shouldUseNetwork, "PlayCue");
        PlayCueLocal(cue, patternType, worldPosition);
    }

    /// <summary>
    /// 약점 패턴 활성화에 맞춰 보스 비주얼을 설정된 Y 오프셋까지 상승시킨다.
    /// </summary>
    public void RaiseWeakPointVisual()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // NGO 세션 상태 확인용 매니저
        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;

        if (shouldUseNetwork && IsSpawned)
        {
            RaiseWeakPointVisualRpc();
            return;
        }

        LogNetworkFallbackIfNeeded(shouldUseNetwork, "RaiseWeakPointVisual");
        MoveWeakPointVisualLocal(true);
    }

    /// <summary>
    /// 약점 패턴 종료에 맞춰 보스 비주얼을 기준 위치로 하강시킨다.
    /// </summary>
    public void LowerWeakPointVisual()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // NGO 세션 상태 확인용 매니저
        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;

        if (shouldUseNetwork && IsSpawned)
        {
            LowerWeakPointVisualRpc();
            return;
        }

        LogNetworkFallbackIfNeeded(shouldUseNetwork, "LowerWeakPointVisual");
        MoveWeakPointVisualLocal(false);
    }

    /// <summary>
    /// 서버에서 승인한 Cue를 받아 로컬에서 재생한다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void PlayCueRpc(int cueValue, int patternTypeValue, Vector3 worldPosition)
    {
        E_BossPresentationCue cue = (E_BossPresentationCue)cueValue; // 네트워크 전송 enum 복원값
        E_BossPatternType patternType = (E_BossPatternType)patternTypeValue; // 네트워크 전송 enum 복원값
        PlayCueLocal(cue, patternType, worldPosition);
    }

    /// <summary>
    /// 서버가 확정한 약점 패턴 비주얼 상승을 각 클라이언트 로컬에서 실행한다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void RaiseWeakPointVisualRpc()
    {
        MoveWeakPointVisualLocal(true);
    }

    /// <summary>
    /// 서버가 확정한 약점 패턴 비주얼 하강을 각 클라이언트 로컬에서 실행한다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void LowerWeakPointVisualRpc()
    {
        MoveWeakPointVisualLocal(false);
    }

    /// <summary>
    /// Animator, VFX, SFX를 실제로 재생한다. 전투 상태는 변경하지 않는다.
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
    /// 약점 패턴 상태에 맞춰 보스 비주얼 로컬 Y 위치를 보간한다.
    /// </summary>
    private void MoveWeakPointVisualLocal(bool shouldRaise)
    {
        ResolveReferences();

        if (_bossVisualRoot == null)
        {
            LogMissingBossVisualRootOnce();
            return;
        }

        CaptureBossVisualRestPosition();

        float durationSeconds = shouldRaise ? _weakPointVisualRaiseSeconds : _weakPointVisualLowerSeconds; // 현재 이동 방향의 보간 시간
        Vector3 targetLocalPosition = shouldRaise
            ? _bossVisualRestLocalPosition + Vector3.up * _weakPointVisualYOffset
            : _bossVisualRestLocalPosition; // 이동 방향에 따른 목표 로컬 위치

        StartWeakPointVisualMove(targetLocalPosition, durationSeconds);
    }

    /// <summary>
    /// 기존 이동 코루틴을 중지하고 새 목표 위치로 보스 비주얼 이동을 시작한다.
    /// </summary>
    private void StartWeakPointVisualMove(Vector3 targetLocalPosition, float durationSeconds)
    {
        StopWeakPointVisualMoveCoroutine();

        float safeDuration = Mathf.Max(0f, durationSeconds); // 런타임에서 사용할 보정된 이동 시간
        if (safeDuration <= 0f)
        {
            _bossVisualRoot.localPosition = targetLocalPosition;
            return;
        }

        _weakPointVisualMoveCoroutine = StartCoroutine(RunWeakPointVisualMove(targetLocalPosition, safeDuration));
    }

    /// <summary>
    /// 보스 비주얼을 현재 로컬 위치에서 목표 로컬 위치까지 시간 기반으로 보간한다.
    /// </summary>
    private IEnumerator RunWeakPointVisualMove(Vector3 targetLocalPosition, float durationSeconds)
    {
        Vector3 startLocalPosition = _bossVisualRoot.localPosition; // 보간 시작 시점의 로컬 위치
        float elapsedSeconds = 0f; // 이동 시작 후 누적 시간

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedSeconds / durationSeconds); // 0..1 보간 진행률
            _bossVisualRoot.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, normalizedTime);
            yield return null;
        }

        _bossVisualRoot.localPosition = targetLocalPosition;
        _weakPointVisualMoveCoroutine = null;
    }

    /// <summary>
    /// 진행 중인 약점 패턴 비주얼 이동 코루틴을 중지한다.
    /// </summary>
    private void StopWeakPointVisualMoveCoroutine()
    {
        if (_weakPointVisualMoveCoroutine == null)
        {
            return;
        }

        StopCoroutine(_weakPointVisualMoveCoroutine);
        _weakPointVisualMoveCoroutine = null;
    }

    /// <summary>
    /// 약점 패턴 전후에 복귀할 보스 비주얼 기준 로컬 위치를 기록한다.
    /// </summary>
    private void CaptureBossVisualRestPosition()
    {
        if (_hasCapturedBossVisualRestPosition || _bossVisualRoot == null)
        {
            return;
        }

        _bossVisualRestLocalPosition = _bossVisualRoot.localPosition;
        _hasCapturedBossVisualRestPosition = true;
    }

    /// <summary>
    /// 유효한 위치를 반환하고, 잘못된 값이면 origin으로 fallback한다.
    /// </summary>
    private Vector3 ResolvePresentationPosition(Vector3 worldPosition)
    {
        if (IsFinitePosition(worldPosition))
        {
            return worldPosition;
        }

        if (!_hasLoggedInvalidPositionWarning)
        {
            Debug.LogWarning($"[BossPresentationController] 잘못된 위치 값이 전달되어 origin으로 fallback. object={name}", this);
            _hasLoggedInvalidPositionWarning = true;
        }

        return _presentationOrigin != null ? _presentationOrigin.position : transform.position;
    }

    /// <summary>
    /// 위치 값이 정상적인지 검사한다.
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
    /// Cue와 Pattern에 맞는 설정을 찾는다.
    /// </summary>
    private bool TryGetCueSettings(E_BossPresentationCue cue, E_BossPatternType patternType, out BossPresentationCueSettings settings)
    {
        settings = default;

        if (_cueSettings == null || _cueSettings.Length == 0)
        {
            LogMissingCueSettingsOnce(cue, patternType);
            return false;
        }

        int fallbackIndex = -1; // PatternType None fallback 인덱스

        for (int index = 0; index < _cueSettings.Length; index++)
        {
            BossPresentationCueSettings candidate = _cueSettings[index]; // 현재 검사 중인 매핑

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
    /// Animator Trigger를 실행한다.
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
    /// VFX를 재생한다.
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
    /// SFX를 재생한다.
    /// </summary>
    private void PlaySfx(BossPresentationCueSettings settings, Vector3 worldPosition)
    {
        if (settings.SoundId == E_SoundId.None)
        {
            return;
        }

        AudioManager audioManager = AudioManager.Instance; // SFX 재생을 담당하는 오디오 매니저

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
    /// Cue 매핑 누락 경고를 한 번만 출력한다.
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
    /// 약점 패턴 비주얼 루트 누락 경고를 한 번만 출력한다.
    /// </summary>
    private void LogMissingBossVisualRootOnce()
    {
        if (_hasLoggedMissingBossVisualRootWarning)
        {
            return;
        }

        Debug.LogWarning($"[BossPresentationController] 약점 패턴 비주얼 이동 대상 Transform이 없습니다. object={name}", this);
        _hasLoggedMissingBossVisualRootWarning = true;
    }

    /// <summary>
    /// 네트워크 세션에서 NetworkObject가 Spawn되지 않은 경우 로컬 실행 fallback을 경고한다.
    /// </summary>
    private void LogNetworkFallbackIfNeeded(bool shouldUseNetwork, string operationName)
    {
        if (!shouldUseNetwork || IsSpawned || _hasLoggedNetworkFallbackWarning)
        {
            return;
        }

        Debug.LogWarning($"[BossPresentationController] NetworkObject가 Spawn되지 않아 로컬 연출로 fallback. object={name}, operation={operationName}", this);
        _hasLoggedNetworkFallbackWarning = true;
    }

    /// <summary>
    /// 약점 패턴 비주얼 이동 설정값을 유효 범위로 보정한다.
    /// </summary>
    private void ValidateWeakPointVisualSettings()
    {
        if (_weakPointVisualRaiseSeconds < 0f)
        {
            _weakPointVisualRaiseSeconds = 0f;
        }

        if (_weakPointVisualLowerSeconds < 0f)
        {
            _weakPointVisualLowerSeconds = 0f;
        }
    }

    /// <summary>
    /// 참조를 자동 설정한다.
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

        if (_bossVisualRoot == null && _animator != null)
        {
            _bossVisualRoot = _animator.transform;
        }
    }
}
