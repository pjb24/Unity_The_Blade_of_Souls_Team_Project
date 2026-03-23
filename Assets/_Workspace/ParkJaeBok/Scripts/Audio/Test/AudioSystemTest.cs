using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class AudioSystemTest : MonoBehaviour
{
    [Header("Test Sound IDs")]
    [SerializeField]
    private E_SoundId _testBgmId = E_SoundId.BGM_Stage01; // Play/CrossFade 테스트에 사용할 BGM ID

    [SerializeField]
    private E_SoundId _testBgmCrossFadeId = E_SoundId.BGM_Boss; // CrossFade 대상 BGM ID

    [SerializeField]
    private E_SoundId _testSfxId = E_SoundId.SFX_Attack; // SFX 재생 테스트에 사용할 ID

    [Header("Test SFX Position")]
    [SerializeField]
    private Transform _testSfxEmitter; // SFX 거리 감쇠 테스트에 사용할 발신 위치

    [Header("Keyboard (New Input System)")]
    [SerializeField]
    private Key _playSfxKey = Key.Digit1; // SFX 재생 트리거 키

    [SerializeField]
    private Key _playBgmKey = Key.Digit2; // BGM 재생 트리거 키

    [SerializeField]
    private Key _crossFadeBgmKey = Key.Digit3; // BGM 크로스페이드 트리거 키

    [SerializeField]
    private Key _fadeOutBgmKey = Key.Digit4; // BGM 페이드아웃 트리거 키

    [SerializeField]
    private Key _stopBgmKey = Key.Digit5; // BGM 정지 트리거 키

    [SerializeField]
    private Key _masterDownKey = Key.Minus; // 마스터 볼륨 감소 키

    [SerializeField]
    private Key _masterUpKey = Key.Equals; // 마스터 볼륨 증가 키

    [SerializeField]
    [Range(0.01f, 0.2f)]
    private float _volumeStep = 0.1f; // 키 입력 시 볼륨 증감 폭

    [Header("Listener Bind Retry")]
    [SerializeField]
    [Min(0.01f)]
    private float _retryInterval = 0.1f; // AudioManager 재탐색 코루틴의 재시도 간격(초)

    [SerializeField]
    [Min(1)]
    private int _maxRetryCount = 30; // AudioManager 재탐색 코루틴의 최대 재시도 횟수

    private bool _isKeyboardMissingLogged = false; // Keyboard 장치 없음 경고를 1회만 출력하기 위한 플래그
    private bool _isVolumeListenerRegistered = false; // AudioManager 볼륨 리스너 등록 상태 플래그
    private Coroutine _registerCoroutine; // AudioManager 준비 대기 후 리스너 등록을 수행하는 코루틴 핸들
    private AudioManager _cachedAudioManager; // 경고 로그 없는 접근을 위해 캐시해두는 AudioManager 참조

    /// <summary>
    /// 활성화 시 AudioManager 준비 완료까지 기다렸다가 볼륨 리스너를 등록한다.
    /// </summary>
    private void OnEnable()
    {
        RestartRegisterCoroutine();
    }

    /// <summary>
    /// 비활성화 시 등록 대기 코루틴을 중단하고 볼륨 리스너를 해제한다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        TryImmediateUnregisterOnDisable();
    }

    /// <summary>
    /// 파괴 시 등록 대기 코루틴을 정리하고 마지막으로 리스너 해제를 시도한다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _registerCoroutine);

        if (_isVolumeListenerRegistered && _cachedAudioManager != null)
        {
            _cachedAudioManager.RemoveVolumeChangedListener(OnVolumeChanged);
            _isVolumeListenerRegistered = false;
        }
    }

    /// <summary>
    /// 리스너 등록 코루틴을 재시작한다.
    /// </summary>
    private void RestartRegisterCoroutine()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        _registerCoroutine = StartCoroutine(RegisterVolumeListenerWithRetryCoroutine());
    }

    /// <summary>
    /// 비활성화 시점에 코루틴 없이 안전하게 리스너 해제를 시도한다.
    /// </summary>
    private void TryImmediateUnregisterOnDisable()
    {
        if (_isVolumeListenerRegistered == false)
        {
            return;
        }

        if (TryResolveAudioManager(out AudioManager audioManager))
        {
            audioManager.RemoveVolumeChangedListener(OnVolumeChanged);
            _isVolumeListenerRegistered = false;
            return;
        }

        _isVolumeListenerRegistered = false;
        Debug.LogWarning($"[AudioSystemTest] OnDisable could not resolve AudioManager on {name}. RemoveVolumeChangedListener skipped.", this);
    }

    /// <summary>
    /// AudioManager가 준비될 때까지 재시도한 뒤 볼륨 리스너를 등록한다.
    /// </summary>
    private IEnumerator RegisterVolumeListenerWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값

        if (_maxRetryCount < 1 || _retryInterval <= 0f)
        {
            Debug.LogWarning($"[AudioSystemTest] Invalid retry settings on {name}. Fallback maxRetry={safeMaxRetry}, interval={safeInterval}.", this);
        }

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            if (TryResolveAudioManager(out AudioManager audioManager))
            {
                RegisterVolumeListenerInternal(audioManager);
                _registerCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[AudioSystemTest] AudioManager is null on {name}. Delaying AddVolumeChangedListener registration.", this);
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[AudioSystemTest] AddVolumeChangedListener registration failed after retries on {name}.", this);
        _registerCoroutine = null;
    }

    /// <summary>
    /// 경고 로그 없이 AudioManager 참조를 해석한다.
    /// </summary>
    private bool TryResolveAudioManager(out AudioManager audioManager)
    {
        if (_cachedAudioManager != null)
        {
            audioManager = _cachedAudioManager;
            return true;
        }

        audioManager = FindAnyObjectByType<AudioManager>();
        if (audioManager == null)
        {
            return false;
        }

        _cachedAudioManager = audioManager;
        return true;
    }

    /// <summary>
    /// 볼륨 리스너 실제 등록을 수행한다.
    /// </summary>
    private void RegisterVolumeListenerInternal(AudioManager audioManager)
    {
        if (_isVolumeListenerRegistered)
        {
            Debug.LogWarning($"[AudioSystemTest] Volume listener is already registered on {name}.", this);
            return;
        }

        audioManager.AddVolumeChangedListener(OnVolumeChanged);
        _isVolumeListenerRegistered = true;
    }

    /// <summary>
    /// 실행 중인 코루틴을 안전하게 중지하고 참조를 정리한다.
    /// </summary>
    private void StopRunningCoroutine(ref Coroutine coroutineHandle)
    {
        if (coroutineHandle == null)
        {
            return;
        }

        StopCoroutine(coroutineHandle);
        coroutineHandle = null;
    }

    /// <summary>
    /// AudioManager 볼륨 변경 이벤트를 로그로 출력한다.
    /// </summary>
    private void OnVolumeChanged(float master, float bgm, float sfx)
    {
        Debug.Log($"[AudioSystemTest] VolumeChanged master:{master:0.00} bgm:{bgm:0.00} sfx:{sfx:0.00}", this);
    }

    /// <summary>
    /// New Input System 키 입력을 감지해 AudioManager API를 수동 검증한다.
    /// </summary>
    private void Update()
    {
        if (TryResolveAudioManager(out AudioManager audioManager) == false)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            if (_isKeyboardMissingLogged == false)
            {
                Debug.LogWarning("[AudioSystemTest] Keyboard.current가 없어 입력 테스트를 수행할 수 없습니다.", this);
                _isKeyboardMissingLogged = true;
            }

            return;
        }

        if (WasPressedThisFrame(keyboard, _playSfxKey))
        {
            if (_testSfxEmitter != null)
            {
                audioManager.PlaySfx(_testSfxId, _testSfxEmitter);
            }
            else
            {
                Debug.LogWarning("[AudioSystemTest] _testSfxEmitter가 없어 AudioManager 위치 기준으로 SFX를 재생합니다.", this);
                audioManager.PlaySfx(_testSfxId);
            }
        }

        if (WasPressedThisFrame(keyboard, _playBgmKey))
        {
            audioManager.PlayBgm(_testBgmId);
        }

        if (WasPressedThisFrame(keyboard, _crossFadeBgmKey))
        {
            audioManager.CrossFadeBgm(_testBgmCrossFadeId, 1.5f);
        }

        if (WasPressedThisFrame(keyboard, _fadeOutBgmKey))
        {
            audioManager.FadeOutBgm(1.0f);
        }

        if (WasPressedThisFrame(keyboard, _stopBgmKey))
        {
            audioManager.StopBgm();
        }

        if (WasPressedThisFrame(keyboard, _masterDownKey))
        {
            float next = audioManager.GetMasterVolume() - _volumeStep;
            audioManager.SetMasterVolume(next);
        }

        if (WasPressedThisFrame(keyboard, _masterUpKey))
        {
            float next = audioManager.GetMasterVolume() + _volumeStep;
            audioManager.SetMasterVolume(next);
        }
    }

    /// <summary>
    /// 지정한 New Input System 키의 이번 프레임 입력 여부를 반환한다.
    /// </summary>
    private bool WasPressedThisFrame(Keyboard keyboard, Key key)
    {
        return keyboard[key].wasPressedThisFrame;
    }
}
