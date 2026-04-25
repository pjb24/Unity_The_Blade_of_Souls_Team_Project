using UnityEngine;

/// <summary>
/// 플레이어 Buff 시스템의 게이지/스탯/VFX/SFX 정책을 정의하는 설정 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "BuffConfig", menuName = "Player/Buff Config")]
public class BuffConfigSO : ScriptableObject
{
    [Header("Gauge Usage")]
    [Tooltip("true면 Buff 게이지를 사용하고, false면 게이지 없이 토글 입력만으로 Buff를 켜고 끕니다.")]
    [SerializeField] private bool _useBuffGauge = true; // Buff 게이지 사용 여부입니다.

    [Tooltip("UseBuffGauge가 true일 때 게이지가 0 이하가 되면 Buff를 자동 종료할지 여부입니다.")]
    [SerializeField] private bool _endBuffWhenGaugeEmpty = true; // 게이지 소진 시 Buff 자동 종료 여부입니다.

    [Header("Gauge Values")]
    [Tooltip("Buff 게이지 최대값입니다. UseBuffGauge가 true일 때 0이면 런타임에서 Buff 시작이 차단됩니다.")]
    [Min(0f)]
    [SerializeField] private float _maxBuffGauge = 100f; // Buff 게이지 최대값입니다.

    [Tooltip("Buff 시작에 필요한 최소 게이지 값입니다.")]
    [Min(0f)]
    [SerializeField] private float _minBuffStartGauge = 20f; // Buff 시작 최소 게이지 값입니다.

    [Tooltip("Buff 적용 중 초당 감소할 게이지 값입니다. 0이면 자동 감소하지 않습니다.")]
    [Min(0f)]
    [SerializeField] private float _gaugeDrainPerSecond = 10f; // Buff 적용 중 초당 게이지 감소량입니다.

    [Tooltip("공격 Hit 성공 시 증가할 게이지 값입니다.")]
    [Min(0f)]
    [SerializeField] private float _gaugeGainOnSuccessfulHit = 8f; // 공격 성공 시 게이지 증가량입니다.

    [Tooltip("게임 시작 시 Buff 게이지 초기값입니다. UseBuffGauge=true일 때 0~Max 범위로 보정됩니다.")]
    [Min(0f)]
    [SerializeField] private float _initialBuffGauge = 0f; // 시작 시 Buff 게이지 초기값입니다.

    [Header("Stat Modifiers")]
    [Tooltip("Buff 적용 중 공격 데미지에 더할 추가값입니다.")]
    [SerializeField] private float _attackDamageAdditive = 0f; // Buff 공격력 추가값입니다.

    [Tooltip("Buff 적용 중 공격 데미지 배율입니다. 1이면 배율 미적용입니다.")]
    [Min(0f)]
    [SerializeField] private float _attackDamageMultiplier = 1f; // Buff 공격력 배율입니다.

    [Tooltip("Buff 적용 중 이동 속도 배율입니다. 1이면 배율 미적용입니다.")]
    [Min(0f)]
    [SerializeField] private float _moveSpeedMultiplier = 1.1f; // Buff 이동 속도 배율입니다.

    [Tooltip("Buff 적용 중 Animator speed에 적용할 공격속도 배율입니다.")]
    [Min(0f)]
    [SerializeField] private float _animationAttackSpeedMultiplier = 1.2f; // Buff 공격속도(애니메이션) 배율입니다.

    [Header("Audio")]
    [Tooltip("Buff 시작 시 재생할 SFX SoundId입니다.")]
    [SerializeField] private E_SoundId _buffStartSfx = E_SoundId.None; // Buff 시작 SFX입니다.

    [Tooltip("Buff 종료 시 재생할 SFX SoundId입니다.")]
    [SerializeField] private E_SoundId _buffEndSfx = E_SoundId.None; // Buff 종료 SFX입니다.

    [Header("VFX")]
    [Tooltip("Buff가 비활성 상태일 때 VFX 기본 활성 상태입니다.")]
    [SerializeField] private bool _defaultVfxActiveWhenBuffOff = false; // Buff 비활성 상태 기본 VFX 활성 정책입니다.

    /// <summary>
    /// Buff 게이지 사용 여부를 반환합니다.
    /// </summary>
    public bool UseBuffGauge => _useBuffGauge;

    /// <summary>
    /// 게이지 소진 시 Buff 자동 종료 여부를 반환합니다.
    /// </summary>
    public bool EndBuffWhenGaugeEmpty => _endBuffWhenGaugeEmpty;

    /// <summary>
    /// Buff 게이지 최대값을 반환합니다.
    /// </summary>
    public float MaxBuffGauge => _maxBuffGauge;

    /// <summary>
    /// Buff 시작 최소 게이지를 반환합니다.
    /// </summary>
    public float MinBuffStartGauge => _minBuffStartGauge;

    /// <summary>
    /// Buff 적용 중 초당 게이지 감소량을 반환합니다.
    /// </summary>
    public float GaugeDrainPerSecond => _gaugeDrainPerSecond;

    /// <summary>
    /// 공격 성공 시 게이지 증가량을 반환합니다.
    /// </summary>
    public float GaugeGainOnSuccessfulHit => _gaugeGainOnSuccessfulHit;

    /// <summary>
    /// Buff 시작 시 초기 게이지 값을 반환합니다.
    /// </summary>
    public float InitialBuffGauge => _initialBuffGauge;

    /// <summary>
    /// Buff 공격력 추가값을 반환합니다.
    /// </summary>
    public float AttackDamageAdditive => _attackDamageAdditive;

    /// <summary>
    /// Buff 공격력 배율을 반환합니다.
    /// </summary>
    public float AttackDamageMultiplier => _attackDamageMultiplier;

    /// <summary>
    /// Buff 이동속도 배율을 반환합니다.
    /// </summary>
    public float MoveSpeedMultiplier => _moveSpeedMultiplier;

    /// <summary>
    /// Buff 공격속도(Animator speed) 배율을 반환합니다.
    /// </summary>
    public float AnimationAttackSpeedMultiplier => _animationAttackSpeedMultiplier;

    /// <summary>
    /// Buff 시작 SFX를 반환합니다.
    /// </summary>
    public E_SoundId BuffStartSfx => _buffStartSfx;

    /// <summary>
    /// Buff 종료 SFX를 반환합니다.
    /// </summary>
    public E_SoundId BuffEndSfx => _buffEndSfx;

    /// <summary>
    /// Buff 비활성 상태 VFX 기본 활성 정책을 반환합니다.
    /// </summary>
    public bool DefaultVfxActiveWhenBuffOff => _defaultVfxActiveWhenBuffOff;

    /// <summary>
    /// 런타임에서 사용할 최소 시작 게이지 보정값을 반환합니다.
    /// </summary>
    public float GetRuntimeMinBuffStartGauge()
    {
        if (!_useBuffGauge)
        {
            return 0f;
        }

        float safeMaxGauge = Mathf.Max(0f, _maxBuffGauge); // 런타임 비교에 사용할 최대 게이지 안전값입니다.
        return Mathf.Clamp(_minBuffStartGauge, 0f, safeMaxGauge);
    }

    /// <summary>
    /// 런타임에서 사용할 초기 게이지 보정값을 반환합니다.
    /// </summary>
    public float GetRuntimeInitialGauge()
    {
        if (!_useBuffGauge)
        {
            return 0f;
        }

        float safeMaxGauge = Mathf.Max(0f, _maxBuffGauge); // 초기 게이지 보정에 사용할 최대 게이지 안전값입니다.
        return Mathf.Clamp(_initialBuffGauge, 0f, safeMaxGauge);
    }

    /// <summary>
    /// 설정값 유효성 보정을 수행합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_useBuffGauge)
        {
            if (_minBuffStartGauge < 0f)
            {
                _minBuffStartGauge = 0f;
            }

            if (_minBuffStartGauge > _maxBuffGauge)
            {
                Debug.LogWarning($"[BuffConfigSO] MinBuffStartGauge({_minBuffStartGauge}) is greater than MaxBuffGauge({_maxBuffGauge}) on {name}. Clamped to max.", this);
                _minBuffStartGauge = _maxBuffGauge;
            }

            if (_gaugeDrainPerSecond < 0f)
            {
                _gaugeDrainPerSecond = 0f;
            }

            if (_initialBuffGauge < 0f)
            {
                _initialBuffGauge = 0f;
            }

            if (_initialBuffGauge > _maxBuffGauge)
            {
                _initialBuffGauge = _maxBuffGauge;
            }
        }
        else
        {
            if (_minBuffStartGauge < 0f)
            {
                _minBuffStartGauge = 0f;
            }

            if (_gaugeDrainPerSecond < 0f)
            {
                _gaugeDrainPerSecond = 0f;
            }

            if (_initialBuffGauge < 0f)
            {
                _initialBuffGauge = 0f;
            }
        }

        if (_maxBuffGauge < 0f)
        {
            _maxBuffGauge = 0f;
        }

        if (_gaugeGainOnSuccessfulHit < 0f)
        {
            _gaugeGainOnSuccessfulHit = 0f;
        }

        if (_attackDamageMultiplier < 0f)
        {
            _attackDamageMultiplier = 0f;
        }

        if (_moveSpeedMultiplier < 0f)
        {
            _moveSpeedMultiplier = 0f;
        }

        if (_animationAttackSpeedMultiplier < 0f)
        {
            _animationAttackSpeedMultiplier = 0f;
        }
    }
}
