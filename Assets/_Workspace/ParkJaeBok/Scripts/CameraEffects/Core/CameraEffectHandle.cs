/// <summary>
/// 호출부가 CameraEffectManager 내부 상태를 직접 참조하지 않고 활성 효과를 제어할 수 있는 핸들입니다.
/// </summary>
public readonly struct CameraEffectHandle
{
    private readonly CameraEffectManager _manager; // 핸들 명령을 전달할 카메라 효과 매니저 참조입니다.
    private readonly int _token; // 매니저 내부 활성 요청 식별 토큰입니다.

    /// <summary>
    /// 런타임 제어용 핸들을 생성합니다.
    /// </summary>
    internal CameraEffectHandle(CameraEffectManager manager, int token)
    {
        _manager = manager;
        _token = token;
    }

    /// <summary>
    /// 핸들이 현재 유효한 활성 효과를 가리키는지 반환합니다.
    /// </summary>
    public bool IsValid => _manager != null && _manager.IsHandleValid(_token);

    /// <summary>
    /// 핸들이 가리키는 활성 효과를 종료합니다.
    /// </summary>
    public bool Stop(string reason = "HandleStop")
    {
        if (!IsValid)
        {
            return false;
        }

        return _manager.StopByHandle(_token, reason);
    }
}
