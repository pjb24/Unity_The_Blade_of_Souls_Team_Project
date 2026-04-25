using UnityEngine;

/// <summary>
/// Buff 시작/종료 시 기존 AudioManager를 통해 SFX를 재생하는 컨트롤러입니다.
/// </summary>
public class PlayerBuffAudioController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Buff 수치를 읽어올 설정 ScriptableObject입니다.")]
    [SerializeField] private BuffConfigSO _buffConfig; // Buff 시작/종료 SFX를 읽을 설정 참조입니다.

    [Header("Runtime")]
    [Tooltip("디버그용: 마지막으로 재생한 Buff SFX 프레임 번호입니다.")]
    [SerializeField] private int _lastPlayedFrame = -1; // 동일 프레임 중복 재생 방지용 마지막 재생 프레임입니다.

    /// <summary>
    /// Buff 시작 SFX 재생을 시도합니다.
    /// </summary>
    public void PlayBuffStartSfx()
    {
        E_SoundId soundId = _buffConfig != null ? _buffConfig.BuffStartSfx : E_SoundId.None; // Buff 시작 시 재생할 SFX 식별자입니다.
        TryPlaySfx(soundId, "BuffStart");
    }

    /// <summary>
    /// Buff 종료 SFX 재생을 시도합니다.
    /// </summary>
    public void PlayBuffEndSfx()
    {
        E_SoundId soundId = _buffConfig != null ? _buffConfig.BuffEndSfx : E_SoundId.None; // Buff 종료 시 재생할 SFX 식별자입니다.
        TryPlaySfx(soundId, "BuffEnd");
    }

    /// <summary>
    /// AudioManager 기반으로 SFX 재생을 수행합니다.
    /// </summary>
    private void TryPlaySfx(E_SoundId soundId, string reason)
    {
        if (soundId == E_SoundId.None)
        {
            Debug.LogWarning($"[PlayerBuffAudioController] SoundId is None. reason={reason}, object={name}", this);
            return;
        }

        if (_lastPlayedFrame == Time.frameCount)
        {
            return;
        }

        AudioManager audioManager = AudioManager.Instance; // 기존 오디오 재생을 수행할 AudioManager 싱글톤 참조입니다.
        if (audioManager == null)
        {
            Debug.LogWarning($"[PlayerBuffAudioController] AudioManager is missing. reason={reason}, object={name}", this);
            return;
        }

        audioManager.PlaySfx(soundId, transform);
        _lastPlayedFrame = Time.frameCount;
    }
}
