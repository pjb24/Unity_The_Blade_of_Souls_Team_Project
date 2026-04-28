using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance; // 전역 접근을 위한 싱글톤 인스턴스

    [Header("Database")]
    [SerializeField]
    private SoundDatabase _soundDatabase; // SoundId -> Audio 설정 조회를 위한 데이터베이스

    [Header("Persistence")]
    [SerializeField]
    private bool _dontDestroyOnLoad = true; // Scene 전환 시 AudioManager 유지 여부

    [Header("SFX Pool")]
    [SerializeField]
    [Range(1, 64)]
    private int _initialSfxPoolSize = 12; // 초기 생성할 SFX AudioSource 개수

    [SerializeField]
    [Range(1, 128)]
    private int _maxSfxPoolSize = 32; // 풀 확장 가능한 최대 AudioSource 개수

    [Header("SFX 3D Attenuation")]
    [SerializeField]
    private bool _useDistanceAttenuationForSfx = true; // SFX 거리 감쇠 활성화 여부

    [SerializeField]
    [Range(0f, 1f)]
    private float _sfxSpatialBlend = 1f; // SFX 2D/3D 블렌드 값 (1이면 완전 3D)

    [SerializeField]
    [Min(0.01f)]
    private float _sfxMinDistance = 1f; // 거리 감쇠가 시작되는 최소 거리

    [SerializeField]
    [Min(0.1f)]
    private float _sfxMaxDistance = 20f; // 거리 감쇠가 거의 끝나는 최대 거리

    [SerializeField]
    [Range(0f, 360f)]
    private float _sfxSpread = 0f; // 3D SFX의 스테레오 확산 각도(0~360)

    [SerializeField]
    private AudioRolloffMode _sfxRolloffMode = AudioRolloffMode.Logarithmic; // 거리 감쇠 계산 방식

    [SerializeField]
    private AnimationCurve _sfxCustomRolloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f); // Custom Rolloff 모드에서 사용할 감쇠 커브

    [Header("Volume")]
    [SerializeField]
    [Range(0f, 1f)]
    private float _masterVolume = 1f; // 전체 사운드에 공통 적용되는 마스터 볼륨

    [SerializeField]
    [Range(0f, 1f)]
    private float _bgmVolume = 1f; // BGM 계열에만 적용되는 볼륨 배율

    [SerializeField]
    [Range(0f, 1f)]
    private float _sfxVolume = 1f; // SFX 계열에만 적용되는 볼륨 배율

    private readonly List<AudioSource> _sfxSources = new List<AudioSource>(); // SFX 재생을 담당하는 AudioSource 풀
    private readonly Dictionary<AudioSource, float> _sfxBaseVolumeBySource = new Dictionary<AudioSource, float>(); // 각 SFX Source의 원본 볼륨 캐시
    private readonly Dictionary<AudioSource, ActiveSfxPlayback> _activeSfxBySource = new Dictionary<AudioSource, ActiveSfxPlayback>(); // 각 AudioSource에서 현재 재생 중인 SFX 메타데이터 캐시
    private readonly Dictionary<SfxCooldownKey, float> _sfxCooldownUntil = new Dictionary<SfxCooldownKey, float>(); // SoundId + 발신자 단위 다음 재생 가능 시간

    private readonly AudioSource[] _bgmSources = new AudioSource[2]; // 크로스페이드를 위한 BGM 2채널
    private readonly float[] _bgmMixWeights = new float[2] { 1f, 0f }; // 각 BGM 소스의 페이드 가중치

    private int _activeBgmIndex = 0; // 현재 청취 중인 BGM 소스 인덱스
    private E_SoundId _currentBgmId = E_SoundId.None; // 현재 BGM의 SoundId
    private Coroutine _bgmFadeCoroutine; // 진행 중인 BGM 페이드 코루틴 핸들

    private Action<float, float, float> _onVolumeChanged; // 볼륨 변경 알림 리스너 컬렉션

    private readonly struct SfxCooldownKey
    {
        public readonly E_SoundId SoundId; // 쿨다운을 구분할 사운드 ID
        public readonly int EmitterKey; // 쿨다운을 구분할 발신자 키(Transform InstanceID 등)

        public SfxCooldownKey(E_SoundId soundId, int emitterKey)
        {
            SoundId = soundId;
            EmitterKey = emitterKey;
        }
    }

    private readonly struct ActiveSfxPlayback
    {
        public readonly E_SoundId SoundId; // 현재 AudioSource에 할당된 사운드 ID
        public readonly int EmitterKey; // 현재 AudioSource에 할당된 발신자 식별 키
        public readonly bool IsLoop; // 현재 AudioSource가 루프 재생 중인지 여부

        public ActiveSfxPlayback(E_SoundId soundId, int emitterKey, bool isLoop)
        {
            SoundId = soundId;
            EmitterKey = emitterKey;
            IsLoop = isLoop;
        }
    }

    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<AudioManager>();
                if (_instance == null)
                {
                    Debug.LogWarning("[AudioManager] 씬에서 AudioManager를 찾지 못했습니다.");
                }
            }

            return _instance;
        }
    }

    /// <summary>
    /// 볼륨 변경 콜백 리스너를 등록한다.
    /// </summary>
    public void AddVolumeChangedListener(Action<float, float, float> listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[AudioManager] AddVolumeChangedListener received null listener.", this);
            return;
        }

        _onVolumeChanged += listener;
    }

    /// <summary>
    /// 볼륨 변경 콜백 리스너를 해제한다.
    /// </summary>
    public void RemoveVolumeChangedListener(Action<float, float, float> listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[AudioManager] RemoveVolumeChangedListener received null listener.", this);
            return;
        }

        _onVolumeChanged -= listener;
    }

    /// <summary>
    /// 싱글톤 초기화, 볼륨 로드, 오디오 채널 구성을 수행한다.
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[AudioManager] 중복 AudioManager가 감지되어 새 인스턴스를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (_soundDatabase == null)
        {
            Debug.LogWarning("[AudioManager] SoundDatabase가 비어 있습니다. 재생 요청이 동작하지 않습니다.", this);
        }
        else
        {
            _soundDatabase.InitializeCache();
        }

        ValidateSfxDistanceSettings();
        ValidateSfxRolloffSettings();
        LoadVolumes();
        InitializeBgmSources();
        InitializeSfxPool();
        ApplyAllVolumes();
    }

    /// <summary>
    /// Inspector 변경 시 SFX 거리/롤오프 설정을 검증하고 기존 소스에 즉시 반영한다.
    /// </summary>
    private void OnValidate()
    {
        ValidateSfxDistanceSettings();
        ValidateSfxRolloffSettings();

        for (int i = 0; i < _sfxSources.Count; i++)
        {
            if (_sfxSources[i] == null)
            {
                continue;
            }

            ConfigureSfxSourceSpatialSettings(_sfxSources[i]);
        }
    }

    /// <summary>
    /// SFX 거리 감쇠 설정값의 유효성을 검사하고 필요 시 보정한다.
    /// </summary>
    private void ValidateSfxDistanceSettings()
    {
        if (_sfxMinDistance > _sfxMaxDistance)
        {
            float correctedMin = _sfxMaxDistance;
            float correctedMax = _sfxMinDistance;
            _sfxMinDistance = correctedMin;
            _sfxMaxDistance = correctedMax;
            Debug.LogWarning(
                $"[AudioManager] SFX 거리 범위가 역전되어 자동 보정했습니다. min={_sfxMinDistance}, max={_sfxMaxDistance}",
                this);
        }

        if (_useDistanceAttenuationForSfx == false && _sfxSpatialBlend > 0f)
        {
            Debug.LogWarning("[AudioManager] 거리 감쇠 비활성 상태에서 SpatialBlend가 0보다 큽니다. 재생 시 2D로 강제 적용됩니다.", this);
        }

        float clampedSpread = Mathf.Clamp(_sfxSpread, 0f, 360f);
        if (Mathf.Approximately(clampedSpread, _sfxSpread) == false)
        {
            _sfxSpread = clampedSpread;
            Debug.LogWarning($"[AudioManager] SFX Spread 값이 범위를 벗어나 자동 보정했습니다. spread={_sfxSpread}", this);
        }
    }

    /// <summary>
    /// SFX 롤오프 모드/커브 설정값을 검증하고 필요 시 보정한다.
    /// </summary>
    private void ValidateSfxRolloffSettings()
    {
        if (_sfxCustomRolloffCurve == null)
        {
            _sfxCustomRolloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            Debug.LogWarning("[AudioManager] SFX Custom Rolloff Curve가 null이라 기본 커브로 보정했습니다.", this);
            return;
        }

        if (_sfxCustomRolloffCurve.length <= 0)
        {
            _sfxCustomRolloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            Debug.LogWarning("[AudioManager] SFX Custom Rolloff Curve 키가 없어 기본 커브로 보정했습니다.", this);
        }
    }

    /// <summary>
    /// BGM 전용 AudioSource 2개를 초기화한다.
    /// </summary>
    private void InitializeBgmSources()
    {
        for (int i = 0; i < _bgmSources.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            ConfigureBgmSourceSpatialSettings(source);
            _bgmSources[i] = source;
            _bgmMixWeights[i] = i == _activeBgmIndex ? 1f : 0f;
        }
    }

    /// <summary>
    /// BGM AudioSource를 항상 2D(거리 감쇠 없음)로 고정한다.
    /// </summary>
    private void ConfigureBgmSourceSpatialSettings(AudioSource source)
    {
        source.spatialBlend = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 1f;
        source.dopplerLevel = 0f;
    }

    /// <summary>
    /// 초기 SFX AudioSource 풀을 생성한다.
    /// </summary>
    private void InitializeSfxPool()
    {
        if (_initialSfxPoolSize > _maxSfxPoolSize)
        {
            _initialSfxPoolSize = _maxSfxPoolSize;
            Debug.LogWarning($"[AudioManager] SFX 초기 풀 크기가 최대값을 초과하여 {_initialSfxPoolSize}로 보정되었습니다.", this);
        }

        for (int i = 0; i < _initialSfxPoolSize; i++)
        {
            CreateSfxSource();
        }
    }

    /// <summary>
    /// SFX 재생용 AudioSource 하나를 생성하여 풀에 추가한다.
    /// </summary>
    private AudioSource CreateSfxSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.dopplerLevel = 0f;

        ConfigureSfxSourceSpatialSettings(source);

        _sfxSources.Add(source);
        _sfxBaseVolumeBySource[source] = 1f;
        return source;
    }

    /// <summary>
    /// SFX AudioSource의 공간감/감쇠 관련 설정을 적용한다.
    /// </summary>
    private void ConfigureSfxSourceSpatialSettings(AudioSource source)
    {
        source.spatialBlend = _useDistanceAttenuationForSfx ? _sfxSpatialBlend : 0f;
        source.rolloffMode = _sfxRolloffMode;
        source.minDistance = _sfxMinDistance;
        source.maxDistance = _sfxMaxDistance;
        source.spread = _sfxSpread;

        if (_sfxRolloffMode == AudioRolloffMode.Custom)
        {
            ValidateSfxRolloffSettings();
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _sfxCustomRolloffCurve);
        }
    }

    /// <summary>
    /// 사용 가능한 SFX 소스를 조회하고 없으면 풀을 확장하거나 임시 소스를 생성한다.
    /// </summary>
    private AudioSource GetAvailableSfxSource()
    {
        for (int i = 0; i < _sfxSources.Count; i++)
        {
            if (_sfxSources[i].isPlaying == false)
            {
                return _sfxSources[i];
            }
        }

        if (_sfxSources.Count < _maxSfxPoolSize)
        {
            Debug.LogWarning($"[AudioManager] SFX 풀이 부족해 확장합니다. newSize={_sfxSources.Count + 1}", this);
            return CreateSfxSource();
        }

        Debug.LogWarning("[AudioManager] SFX 풀 최대치에 도달하여 임시 AudioSource를 생성합니다. 기존 재생은 중단하지 않습니다.", this);
        return CreateTemporarySfxSource();
    }

    /// <summary>
    /// 풀 한계를 넘는 동시 재생을 위해 임시 SFX AudioSource를 생성한다.
    /// </summary>
    private AudioSource CreateTemporarySfxSource()
    {
        GameObject temporaryObject = new GameObject("TempSfxSource");
        temporaryObject.transform.SetParent(transform, false);

        AudioSource source = temporaryObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.dopplerLevel = 0f;
        ConfigureSfxSourceSpatialSettings(source);
        return source;
    }

    /// <summary>
    /// SoundId에 대응되는 SFX를 AudioManager 위치 기준으로 재생한다.
    /// </summary>
    public void PlaySfx(E_SoundId soundId)
    {
        PlaySfxInternal(soundId, false, Vector3.zero, 0);
    }

    /// <summary>
    /// SoundId에 대응되는 SFX를 지정 월드 좌표에서 재생한다.
    /// </summary>
    public void PlaySfx(E_SoundId soundId, Vector3 worldPosition)
    {
        PlaySfxInternal(soundId, true, worldPosition, 0);
    }

    /// <summary>
    /// SoundId에 대응되는 SFX를 지정 Emitter Transform 위치에서 재생한다.
    /// </summary>
    public void PlaySfx(E_SoundId soundId, Transform emitterTransform)
    {
        if (emitterTransform == null)
        {
            Debug.LogWarning($"[AudioManager] Transform emitter가 null이라 AudioManager 위치를 사용합니다. id={soundId}", this);
            PlaySfx(soundId);
            return;
        }

        PlaySfxInternal(soundId, true, emitterTransform.position, emitterTransform.GetInstanceID());
    }

    /// <summary>
    /// 지정한 SoundId의 루프 SFX를 즉시 정지한다. emitterTransform을 전달하면 같은 발신자의 재생만 정지한다.
    /// </summary>
    public void StopSfx(E_SoundId soundId, Transform emitterTransform = null)
    {
        int emitterKey = emitterTransform != null ? emitterTransform.GetInstanceID() : int.MinValue;
        bool restrictByEmitter = emitterTransform != null;
        StopSfxInternal(soundId, emitterKey, restrictByEmitter);
    }

    /// <summary>
    /// SFX 재생 준비(쿨다운/피치/볼륨/위치 적용)를 수행하고 재생한다.
    /// </summary>
    private void PlaySfxInternal(E_SoundId soundId, bool useWorldPosition, Vector3 worldPosition, int emitterKey)
    {
        if (TryGetEntry(soundId, out SoundEntry entry) == false)
        {
            return;
        }

        if (entry.Clip == null)
        {
            Debug.LogWarning($"[AudioManager] Clip이 비어 있어 SFX를 재생할 수 없습니다. id={soundId}", this);
            return;
        }

        if (CanPlaySfxByCooldown(soundId, entry.CooldownSeconds, emitterKey) == false)
        {
            return;
        }

        AudioSource source = GetAvailableSfxSource();
        ConfigureSfxSourceSpatialSettings(source);
        _activeSfxBySource.Remove(source);

        if (useWorldPosition)
        {
            source.transform.position = worldPosition;
        }
        else
        {
            source.transform.position = transform.position;
        }

        source.Stop();
        source.clip = entry.Clip;
        source.loop = entry.Loop;
        source.pitch = entry.GetFinalPitch();

        _sfxBaseVolumeBySource[source] = entry.Volume;
        _activeSfxBySource[source] = new ActiveSfxPlayback(soundId, emitterKey, entry.Loop);
        source.volume = CalculateSfxVolume(entry.Volume);
        source.Play();

        if (_sfxSources.Contains(source) == false)
        {
            float destroyDelay = GetTemporarySfxDestroyDelay(source.clip, source.pitch);
            _activeSfxBySource.Remove(source);
            Destroy(source.gameObject, destroyDelay);
        }
    }

    /// <summary>
    /// 조건에 맞는 루프 SFX를 즉시 정지하고 상태 캐시를 정리한다.
    /// </summary>
    private void StopSfxInternal(E_SoundId soundId, int emitterKey, bool restrictByEmitter)
    {
        List<AudioSource> sourcesToStop = new List<AudioSource>();

        foreach (KeyValuePair<AudioSource, ActiveSfxPlayback> pair in _activeSfxBySource)
        {
            AudioSource source = pair.Key;
            ActiveSfxPlayback playback = pair.Value;

            if (source == null)
            {
                continue;
            }

            if (!source.isPlaying)
            {
                continue;
            }

            if (!playback.IsLoop)
            {
                continue;
            }

            if (playback.SoundId != soundId)
            {
                continue;
            }

            if (restrictByEmitter && playback.EmitterKey != emitterKey)
            {
                continue;
            }

            sourcesToStop.Add(source);
        }

        for (int i = 0; i < sourcesToStop.Count; i++)
        {
            AudioSource source = sourcesToStop[i];
            source.Stop();
            _activeSfxBySource.Remove(source);

            if (_sfxSources.Contains(source) == false && source.gameObject != null)
            {
                Destroy(source.gameObject);
            }
        }
    }

    /// <summary>
    /// 임시 SFX 오브젝트의 파괴 지연 시간을 계산한다.
    /// </summary>
    private float GetTemporarySfxDestroyDelay(AudioClip clip, float pitch)
    {
        if (clip == null)
        {
            return 0.1f;
        }

        float safePitch = Mathf.Max(0.01f, Mathf.Abs(pitch));
        return (clip.length / safePitch) + 0.1f;
    }

    /// <summary>
    /// SoundId + 발신자 기준 쿨다운 검사 및 다음 재생 가능 시간을 갱신한다.
    /// </summary>
    private bool CanPlaySfxByCooldown(E_SoundId soundId, float cooldownSeconds, int emitterKey)
    {
        if (cooldownSeconds <= 0f)
        {
            return true;
        }

        SfxCooldownKey cooldownKey = new SfxCooldownKey(soundId, emitterKey);

        if (_sfxCooldownUntil.TryGetValue(cooldownKey, out float nextPlayableTime) && Time.unscaledTime < nextPlayableTime)
        {
            return false;
        }

        _sfxCooldownUntil[cooldownKey] = Time.unscaledTime + cooldownSeconds;
        return true;
    }

    /// <summary>
    /// 지정한 SoundId의 BGM을 즉시 재생한다.
    /// </summary>
    public void PlayBgm(E_SoundId soundId)
    {
        if (TryGetEntry(soundId, out SoundEntry entry) == false)
        {
            return;
        }

        if (entry.Clip == null)
        {
            Debug.LogWarning($"[AudioManager] Clip이 비어 있어 BGM을 재생할 수 없습니다. id={soundId}", this);
            return;
        }

        AudioSource activeSource = _bgmSources[_activeBgmIndex];
        if (_currentBgmId == soundId && activeSource.isPlaying)
        {
            Debug.LogWarning($"[AudioManager] 동일 BGM 재생 요청을 무시했습니다. id={soundId}", this);
            return;
        }

        StopBgmFadeCoroutine();

        ConfigureBgmSourceSpatialSettings(activeSource);
        activeSource.clip = entry.Clip;
        activeSource.pitch = entry.GetFinalPitch();
        activeSource.loop = true;
        _bgmMixWeights[_activeBgmIndex] = 1f;
        _currentBgmId = soundId;

        ApplyBgmVolume(_activeBgmIndex, entry.Volume);
        activeSource.Play();
    }

    /// <summary>
    /// 현재 BGM을 즉시 정지한다.
    /// </summary>
    public void StopBgm()
    {
        StopBgmFadeCoroutine();

        for (int i = 0; i < _bgmSources.Length; i++)
        {
            _bgmSources[i].Stop();
            _bgmMixWeights[i] = i == _activeBgmIndex ? 1f : 0f;
        }

        _currentBgmId = E_SoundId.None;
    }

    /// <summary>
    /// 현재 BGM을 지정한 시간 동안 페이드아웃 후 정지한다.
    /// </summary>
    public void FadeOutBgm(float duration = 1f)
    {
        duration = Mathf.Max(0f, duration);
        StopBgmFadeCoroutine();
        _bgmFadeCoroutine = StartCoroutine(CoFadeOutBgm(duration));
    }

    /// <summary>
    /// 현재 BGM에서 대상 BGM으로 크로스페이드한다.
    /// </summary>
    public void CrossFadeBgm(E_SoundId soundId, float duration = 1f)
    {
        duration = Mathf.Max(0f, duration);

        if (TryGetEntry(soundId, out SoundEntry nextEntry) == false)
        {
            return;
        }

        if (nextEntry.Clip == null)
        {
            Debug.LogWarning($"[AudioManager] Clip이 비어 있어 CrossFade를 수행할 수 없습니다. id={soundId}", this);
            return;
        }

        if (_currentBgmId == soundId && _bgmSources[_activeBgmIndex].isPlaying)
        {
            Debug.LogWarning($"[AudioManager] 동일 BGM CrossFade 요청을 무시했습니다. id={soundId}", this);
            return;
        }

        StopBgmFadeCoroutine();

        int oldIndex = _activeBgmIndex;
        int newIndex = 1 - _activeBgmIndex;

        AudioSource newSource = _bgmSources[newIndex];
        ConfigureBgmSourceSpatialSettings(newSource);
        newSource.clip = nextEntry.Clip;
        newSource.loop = true;
        newSource.pitch = nextEntry.GetFinalPitch();
        _bgmMixWeights[newIndex] = 0f;
        ApplyBgmVolume(newIndex, nextEntry.Volume);
        newSource.Play();

        _activeBgmIndex = newIndex;
        _currentBgmId = soundId;

        _bgmFadeCoroutine = StartCoroutine(CoCrossFade(oldIndex, newIndex, duration, nextEntry.Volume));
    }

    /// <summary>
    /// ID 기반 사운드 엔트리를 안전하게 조회한다.
    /// </summary>
    private bool TryGetEntry(E_SoundId soundId, out SoundEntry entry)
    {
        entry = null;

        if (soundId == E_SoundId.None)
        {
            Debug.LogWarning("[AudioManager] SoundId None 재생 요청은 무시됩니다.", this);
            return false;
        }

        if (_soundDatabase == null)
        {
            Debug.LogWarning("[AudioManager] SoundDatabase가 없어 재생할 수 없습니다.", this);
            return false;
        }

        if (_soundDatabase.TryGetEntry(soundId, out entry) == false)
        {
            Debug.LogWarning($"[AudioManager] SoundDatabase에 등록되지 않은 SoundId입니다. id={soundId}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 모든 볼륨 값(0~1)을 보정하고 즉시 저장/적용한다.
    /// </summary>
    public void SetMasterVolume(float value)
    {
        _masterVolume = ClampVolume(value, nameof(_masterVolume));
        SaveVolumes();
        ApplyAllVolumes();
        NotifyVolumeChanged();
    }

    /// <summary>
    /// BGM 볼륨 값(0~1)을 보정하고 즉시 저장/적용한다.
    /// </summary>
    public void SetBgmVolume(float value)
    {
        _bgmVolume = ClampVolume(value, nameof(_bgmVolume));
        SaveVolumes();
        ApplyAllVolumes();
        NotifyVolumeChanged();
    }

    /// <summary>
    /// SFX 볼륨 값(0~1)을 보정하고 즉시 저장/적용한다.
    /// </summary>
    public void SetSfxVolume(float value)
    {
        _sfxVolume = ClampVolume(value, nameof(_sfxVolume));
        SaveVolumes();
        ApplyAllVolumes();
        NotifyVolumeChanged();
    }

    public float GetMasterVolume() => _masterVolume;
    public float GetBgmVolume() => _bgmVolume;
    public float GetSfxVolume() => _sfxVolume;

    /// <summary>
    /// 지정한 SoundId의 BGM이 현재 재생 중인지 확인한다.
    /// </summary>
    public bool IsBgmPlaying(E_SoundId soundId)
    {
        if (soundId == E_SoundId.None)
        {
            return false;
        }

        AudioSource activeSource = _bgmSources[_activeBgmIndex]; // 현재 메인 BGM 채널로 사용 중인 AudioSource입니다.
        if (activeSource == null)
        {
            return false;
        }

        return _currentBgmId == soundId && activeSource.isPlaying;
    }

    /// <summary>
    /// 저장 시스템 제거 후 Inspector 기본 볼륨값만 검증합니다.
    /// </summary>
    private void LoadVolumes()
    {
        _masterVolume = ClampVolume(_masterVolume, nameof(_masterVolume));
        _bgmVolume = ClampVolume(_bgmVolume, nameof(_bgmVolume));
        _sfxVolume = ClampVolume(_sfxVolume, nameof(_sfxVolume));
    }

    /// <summary>
    /// 저장 시스템 제거 후 볼륨 저장 요청을 수행하지 않습니다.
    /// </summary>
    private void SaveVolumes()
    {
        Debug.LogWarning("[AudioManager] Save system has been removed. Runtime volume changes are not persisted.", this);
    }

    /// <summary>
    /// 입력 볼륨 값을 0~1 범위로 제한하고 필요 시 경고 로그를 남긴다.
    /// </summary>
    private float ClampVolume(float value, string label)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, clamped) == false)
        {
            Debug.LogWarning($"[AudioManager] 볼륨 값이 비정상이라 보정했습니다. label={label}, input={value}, clamped={clamped}", this);
        }

        return clamped;
    }

    /// <summary>
    /// 모든 오디오 채널의 최종 볼륨을 재적용한다.
    /// </summary>
    private void ApplyAllVolumes()
    {
        ApplyBgmVolumes();
        ApplySfxVolumes();
    }

    /// <summary>
    /// 현재 BGM 소스들의 볼륨을 가중치와 전역 볼륨으로 계산해 갱신한다.
    /// </summary>
    private void ApplyBgmVolumes()
    {
        for (int i = 0; i < _bgmSources.Length; i++)
        {
            float entryVolume = 1f;
            if (_bgmSources[i].clip != null && _soundDatabase != null)
            {
                if (TryFindVolumeByClip(_bgmSources[i].clip, out float foundVolume))
                {
                    entryVolume = foundVolume;
                }
            }

            _bgmSources[i].volume = entryVolume * _masterVolume * _bgmVolume * _bgmMixWeights[i];
        }
    }

    /// <summary>
    /// 활성 SFX 소스들의 볼륨을 전역 볼륨 배율로 재계산한다.
    /// </summary>
    private void ApplySfxVolumes()
    {
        for (int i = 0; i < _sfxSources.Count; i++)
        {
            AudioSource source = _sfxSources[i];
            if (_sfxBaseVolumeBySource.TryGetValue(source, out float baseVolume) == false)
            {
                baseVolume = 1f;
                Debug.LogWarning("[AudioManager] SFX 볼륨 캐시 누락으로 기본값을 사용합니다.", this);
            }

            source.volume = CalculateSfxVolume(baseVolume);
        }
    }

    /// <summary>
    /// 특정 인덱스 BGM 소스에 Entry 볼륨 + 전역 볼륨을 적용한다.
    /// </summary>
    private void ApplyBgmVolume(int sourceIndex, float entryVolume)
    {
        _bgmSources[sourceIndex].volume = entryVolume * _masterVolume * _bgmVolume * _bgmMixWeights[sourceIndex];
    }

    /// <summary>
    /// SFX용 최종 볼륨을 계산한다.
    /// </summary>
    private float CalculateSfxVolume(float entryVolume)
    {
        return entryVolume * _masterVolume * _sfxVolume;
    }

    /// <summary>
    /// 데이터베이스 엔트리에서 동일 Clip의 기본 볼륨을 조회한다.
    /// </summary>
    private bool TryFindVolumeByClip(AudioClip clip, out float volume)
    {
        volume = 1f;
        if (_soundDatabase == null)
        {
            return false;
        }

        IReadOnlyList<SoundEntry> entries = _soundDatabase.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            SoundEntry entry = entries[i];
            if (entry != null && entry.Clip == clip)
            {
                volume = entry.Volume;
                return true;
            }
        }

        Debug.LogWarning($"[AudioManager] Clip에 대응하는 Entry를 찾지 못해 기본 볼륨을 사용합니다. clip={clip.name}", this);
        return false;
    }

    /// <summary>
    /// 진행 중인 BGM 페이드 코루틴을 안전하게 중지한다.
    /// </summary>
    private void StopBgmFadeCoroutine()
    {
        if (_bgmFadeCoroutine != null)
        {
            StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = null;
        }
    }

    /// <summary>
    /// BGM 페이드아웃 코루틴.
    /// </summary>
    private IEnumerator CoFadeOutBgm(float duration)
    {
        int sourceIndex = _activeBgmIndex;
        AudioSource source = _bgmSources[sourceIndex];

        if (source.isPlaying == false)
        {
            yield break;
        }

        if (TryFindVolumeByClip(source.clip, out float entryVolume) == false)
        {
            entryVolume = 1f;
        }

        float startWeight = _bgmMixWeights[sourceIndex];
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            _bgmMixWeights[sourceIndex] = Mathf.Lerp(startWeight, 0f, t);
            ApplyBgmVolume(sourceIndex, entryVolume);
            yield return null;
        }

        _bgmMixWeights[sourceIndex] = 0f;
        ApplyBgmVolume(sourceIndex, entryVolume);
        source.Stop();
        _currentBgmId = E_SoundId.None;
        _bgmFadeCoroutine = null;
    }

    /// <summary>
    /// 이전 BGM에서 신규 BGM으로 볼륨을 교차 전환하는 코루틴.
    /// </summary>
    private IEnumerator CoCrossFade(int oldIndex, int newIndex, float duration, float newEntryVolume)
    {
        AudioSource oldSource = _bgmSources[oldIndex];

        float oldEntryVolume = 1f;
        if (oldSource.clip != null)
        {
            TryFindVolumeByClip(oldSource.clip, out oldEntryVolume);
        }

        float elapsed = 0f;
        _bgmMixWeights[newIndex] = 0f;
        _bgmMixWeights[oldIndex] = oldSource.isPlaying ? 1f : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            _bgmMixWeights[newIndex] = t;
            _bgmMixWeights[oldIndex] = 1f - t;

            ApplyBgmVolume(newIndex, newEntryVolume);
            ApplyBgmVolume(oldIndex, oldEntryVolume);
            yield return null;
        }

        _bgmMixWeights[newIndex] = 1f;
        _bgmMixWeights[oldIndex] = 0f;
        ApplyBgmVolume(newIndex, newEntryVolume);
        ApplyBgmVolume(oldIndex, oldEntryVolume);

        if (oldSource.isPlaying)
        {
            oldSource.Stop();
        }

        _bgmFadeCoroutine = null;
    }

    /// <summary>
    /// 등록된 볼륨 변경 리스너에게 최신 값을 전달한다.
    /// </summary>
    private void NotifyVolumeChanged()
    {
        _onVolumeChanged?.Invoke(_masterVolume, _bgmVolume, _sfxVolume);
    }
}
