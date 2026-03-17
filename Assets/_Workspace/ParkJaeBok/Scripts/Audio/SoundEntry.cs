using System;
using UnityEngine;

[Serializable]
public class SoundEntry
{
    [SerializeField]
    private E_SoundId _soundId = E_SoundId.None; // 사운드 조회/재생에 사용하는 고유 ID

    [SerializeField]
    private AudioClip _clip; // 실제로 재생될 오디오 클립

    [SerializeField]
    [Range(0f, 1f)]
    private float _volume = 1f; // 이 사운드의 기본 볼륨 배율

    [SerializeField]
    [Range(0.1f, 3f)]
    private float _basePitch = 1f; // 랜덤 피치 적용 전 기준 피치 값

    [SerializeField]
    private bool _loop = false; // AudioSource.loop에 전달할 반복 재생 여부

    [SerializeField]
    private bool _useRandomPitch = false; // 랜덤 피치 사용 여부

    [SerializeField]
    [Range(-1f, 1f)]
    private float _randomPitchOffsetMin = -0.05f; // 랜덤 피치 최소 오프셋

    [SerializeField]
    [Range(-1f, 1f)]
    private float _randomPitchOffsetMax = 0.05f; // 랜덤 피치 최대 오프셋

    [SerializeField]
    [Range(0f, 5f)]
    private float _cooldownSeconds = 0f; // 동일 SoundId 재생 쿨다운 시간

    public E_SoundId SoundId => _soundId;
    public AudioClip Clip => _clip;
    public float Volume => _volume;
    public float BasePitch => _basePitch;
    public bool Loop => _loop;
    public bool UseRandomPitch => _useRandomPitch;
    public float RandomPitchOffsetMin => _randomPitchOffsetMin;
    public float RandomPitchOffsetMax => _randomPitchOffsetMax;
    public float CooldownSeconds => _cooldownSeconds;

    /// <summary>
    /// 랜덤 피치 설정을 반영해 최종 피치를 계산한다.
    /// </summary>
    public float GetFinalPitch()
    {
        if (_useRandomPitch == false)
        {
            return _basePitch;
        }

        return _basePitch + UnityEngine.Random.Range(_randomPitchOffsetMin, _randomPitchOffsetMax);
    }

    /// <summary>
    /// OnValidate에서 랜덤 오프셋 최소/최대 값을 강제로 동기화한다.
    /// </summary>
    public void SetRandomPitchOffsets(float min, float max)
    {
        _randomPitchOffsetMin = min;
        _randomPitchOffsetMax = max;
    }

    /// <summary>
    /// OnValidate에서 비정상 피치 값을 교정한다.
    /// </summary>
    public void SetBasePitch(float basePitch)
    {
        _basePitch = basePitch;
    }
}
