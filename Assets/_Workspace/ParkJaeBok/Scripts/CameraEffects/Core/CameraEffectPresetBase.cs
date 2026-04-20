using UnityEngine;

/// <summary>
/// 카메라 효과 프리셋의 공통 메타데이터와 수명주기 훅을 제공하는 베이스 ScriptableObject입니다.
/// </summary>
public abstract class CameraEffectPresetBase : ScriptableObject
{
    /// <summary>
    /// 카메라 효과 실행 수명주기 타입입니다.
    /// </summary>
    public enum E_CameraEffectLifecycleMode
    {
        Instant = 0,
        Timed = 1,
        Loop = 2
    }

    [Header("Common")]
    [Tooltip("디자이너가 인스펙터에서 식별할 효과 표시 이름입니다.")]
    [SerializeField] private string _effectName = "New Camera Effect"; // 인스펙터/로그에서 사용할 효과 표시 이름입니다.

    [Tooltip("효과 기본 지속 시간(초)입니다. Timed 모드에서 자동 종료 기준으로 사용됩니다.")]
    [SerializeField] private float _durationSeconds = 1f; // Timed 모드 자동 종료 기준으로 사용할 기본 지속 시간입니다.

    [Tooltip("효과를 지속(Loop) 재생할지 여부입니다. 켜지면 명시적 Stop 요청 전까지 유지됩니다.")]
    [SerializeField] private bool _isLoop; // 효과 지속 재생 여부를 나타내는 플래그입니다.

    [Tooltip("즉시 효과(Instant)로 처리할지 여부입니다. 켜지면 시작 후 1프레임 내 종료됩니다.")]
    [SerializeField] private bool _isInstantEffect; // 효과를 즉시 처리할지 제어하는 플래그입니다.

    [Tooltip("요청에서 채널 오버라이드를 사용하지 않을 때 적용할 기본 채널입니다.")]
    [SerializeField] private E_CameraEffectChannel _defaultChannel = E_CameraEffectChannel.System; // 프리셋 기본 채널 라우팅에 사용할 채널 식별자입니다.

    [Tooltip("효과 중요도 우선순위 값입니다. 큰 값일수록 더 높은 우선순위를 가집니다.")]
    [SerializeField] private int _priority = 0; // 채널 충돌 시 비교에 사용할 기본 우선순위 값입니다.

    /// <summary>
    /// 효과 표시 이름을 반환합니다.
    /// </summary>
    public string EffectName => _effectName;

    /// <summary>
    /// 기본 지속 시간을 반환합니다.
    /// </summary>
    public float DurationSeconds => _durationSeconds;

    /// <summary>
    /// Loop 여부를 반환합니다.
    /// </summary>
    public bool IsLoop => _isLoop;

    /// <summary>
    /// Instant 여부를 반환합니다.
    /// </summary>
    public bool IsInstantEffect => _isInstantEffect;

    /// <summary>
    /// 기본 채널을 반환합니다.
    /// </summary>
    public E_CameraEffectChannel DefaultChannel => _defaultChannel;

    /// <summary>
    /// 기본 우선순위를 반환합니다.
    /// </summary>
    public int Priority => _priority;

    /// <summary>
    /// 요청과 프리셋 데이터 기반 최종 지속 시간을 계산합니다.
    /// </summary>
    public float ResolveDurationSeconds(in CameraEffectRequest request)
    {
        if (request.UseDurationOverride)
        {
            return request.DurationOverrideSeconds;
        }

        return _durationSeconds;
    }

    /// <summary>
    /// 프리셋/요청 조합 기준으로 최종 우선순위 값을 계산합니다.
    /// </summary>
    public int ResolvePriority(in CameraEffectRequest request)
    {
        if (request.UsePriorityOverride)
        {
            return request.Priority;
        }

        return _priority;
    }

    /// <summary>
    /// 프리셋/요청 조합 기준으로 실행 수명주기 타입을 계산합니다.
    /// </summary>
    public E_CameraEffectLifecycleMode ResolveLifecycleMode(in CameraEffectRequest request)
    {
        if (_isInstantEffect)
        {
            return E_CameraEffectLifecycleMode.Instant;
        }

        if (_isLoop)
        {
            return E_CameraEffectLifecycleMode.Loop;
        }

        return E_CameraEffectLifecycleMode.Timed;
    }

    /// <summary>
    /// 효과 시작 시점에 1회 호출되는 수명주기 훅입니다.
    /// </summary>
    public virtual void OnEffectStarted(Camera targetCamera, in CameraEffectRequest request)
    {
    }

    /// <summary>
    /// 효과 재생 중 매 프레임 호출되는 수명주기 훅입니다.
    /// </summary>
    public virtual void OnEffectUpdated(Camera targetCamera, in CameraEffectRequest request, float elapsedSeconds, float normalizedTime)
    {
    }

    /// <summary>
    /// 효과 종료 시점에 1회 호출되는 수명주기 훅입니다.
    /// </summary>
    public virtual void OnEffectStopped(Camera targetCamera, in CameraEffectRequest request, string stopReason)
    {
    }
}
