using UnityEngine;

/// <summary>
/// 호출부가 EffectInstance를 직접 참조하지 않고 제어할 수 있는 핸들.
/// </summary>
public sealed class EffectHandle
{
    private EffectService _service; // 핸들 명령을 전달할 서비스 참조
    private int _token; // 서비스 내부 인스턴스 식별 토큰

    internal EffectHandle(EffectService service, int token)
    {
        _service = service;
        _token = token;
    }

    /// <summary>
    /// 현재 핸들이 유효한 인스턴스를 가리키는지 반환한다.
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (_service == null)
            {
                return false;
            }

            return _service.IsHandleValid(_token);
        }
    }

    /// <summary>
    /// 이펙트 재생을 정지하고 풀로 반환한다.
    /// </summary>
    public void Stop()
    {
        if (IsValid == false)
        {
            return;
        }

        _service.StopByHandle(_token);
    }

    /// <summary>
    /// 이펙트 재생을 일시정지한다.
    /// </summary>
    public void Pause()
    {
        if (IsValid == false)
        {
            return;
        }

        _service.PauseByHandle(_token);
    }

    /// <summary>
    /// 일시정지된 이펙트 재생을 재개한다.
    /// </summary>
    public void Resume()
    {
        if (IsValid == false)
        {
            return;
        }

        _service.ResumeByHandle(_token);
    }

    /// <summary>
    /// Follow 대상 Transform을 런타임에 교체한다.
    /// </summary>
    public void SetFollowTarget(Transform followTarget)
    {
        if (IsValid == false)
        {
            return;
        }

        _service.SetFollowTargetByHandle(_token, followTarget);
    }

    /// <summary>
    /// 월드 좌표를 강제로 설정한다.
    /// </summary>
    public void SetPosition(Vector3 position)
    {
        if (IsValid == false)
        {
            return;
        }

        _service.SetPositionByHandle(_token, position);
    }

    internal void Invalidate()
    {
        _service = null;
        _token = 0;
    }
}
