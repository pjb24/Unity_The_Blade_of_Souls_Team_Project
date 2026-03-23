using System;
using UnityEngine;

[Serializable]
public class SfxEventRule
{
    [SerializeField]
    private E_SfxEventType _eventType = E_SfxEventType.None; // 게임플레이에서 발생한 SFX 이벤트 타입을 정의하는 값

    [SerializeField]
    [Tooltip("동일 EventType 내에서 추가 분기를 위한 서브 타입 키(예: Surface.Stone, Weapon.Sword)")]
    private string _subTypeKey = string.Empty; // 같은 이벤트 타입 안에서 세부 조건을 구분하기 위한 문자열 키

    [SerializeField]
    private E_SoundId _soundId = E_SoundId.None; // 라우팅 결과로 최종 재생할 SoundDatabase 사운드 ID

    [SerializeField]
    [Min(0f)]
    private float _globalMinInterval = 0f; // 해당 룰이 전체적으로 재생될 때 적용할 최소 간격(초)

    [SerializeField]
    [Min(0f)]
    private float _perEmitterMinInterval = 0f; // 동일 emitter에서 재생될 때 적용할 최소 간격(초)

    [SerializeField]
    [Min(0)]
    private int _maxRequestsPerSecond = 0; // 1초 기준 최대 허용 재생 요청 수(0이면 무제한)

    public E_SfxEventType EventType => _eventType;
    public string SubTypeKey => _subTypeKey;
    public E_SoundId SoundId => _soundId;
    public float GlobalMinInterval => _globalMinInterval;
    public float PerEmitterMinInterval => _perEmitterMinInterval;
    public int MaxRequestsPerSecond => _maxRequestsPerSecond;

    /// <summary>
    /// 룰 매칭 시 사용할 비교 키를 소문자 형태로 정규화해 반환한다.
    /// </summary>
    public string GetNormalizedSubTypeKey()
    {
        if (string.IsNullOrWhiteSpace(_subTypeKey))
        {
            return string.Empty;
        }

        return _subTypeKey.Trim().ToLowerInvariant();
    }
}
