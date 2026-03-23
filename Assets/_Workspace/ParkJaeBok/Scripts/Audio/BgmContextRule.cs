using System;
using UnityEngine;

[Serializable]
public class BgmContextRule
{
    [SerializeField]
    private E_BgmContextType _contextType = E_BgmContextType.None; // 규칙이 대응하는 BGM 컨텍스트 타입

    [SerializeField]
    private E_SoundId _soundId = E_SoundId.None; // 컨텍스트가 활성화되었을 때 재생할 BGM ID

    [SerializeField]
    private int _priority = 0; // 여러 컨텍스트가 동시에 활성화될 때 비교할 우선순위 값

    [SerializeField]
    [Min(0f)]
    private float _crossFadeDuration = 1.0f; // 이 컨텍스트로 전환할 때 적용할 크로스페이드 시간

    [SerializeField]
    [Min(0f)]
    private float _minHoldDuration = 0.25f; // 이 컨텍스트가 선택된 뒤 유지해야 하는 최소 재생 시간

    public E_BgmContextType ContextType => _contextType;
    public E_SoundId SoundId => _soundId;
    public int Priority => _priority;
    public float CrossFadeDuration => _crossFadeDuration;
    public float MinHoldDuration => _minHoldDuration;
}
