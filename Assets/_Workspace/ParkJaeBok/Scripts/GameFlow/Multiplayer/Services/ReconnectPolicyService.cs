using System;

/// <summary>
/// 클라이언트 재접속 정책(시간 윈도우, 1회 제한)을 판정하는 서비스입니다.
/// </summary>
public sealed class ReconnectPolicyService
{
    private readonly float _reconnectWindowSeconds; // 재접속 허용 시간(초)입니다.
    private bool _hasReconnectTokenConsumed; // 재접속 기회를 이미 사용했는지 추적하는 플래그입니다.
    private DateTime _lastDisconnectUtc; // 마지막 연결 해제 시각(UTC)입니다.

    /// <summary>
    /// 재접속 정책 서비스를 생성합니다.
    /// </summary>
    public ReconnectPolicyService(float reconnectWindowSeconds)
    {
        _reconnectWindowSeconds = Math.Max(1f, reconnectWindowSeconds);
    }

    /// <summary>
    /// 클라이언트 연결 해제 시각을 기록합니다.
    /// </summary>
    public void MarkDisconnected(DateTime utcNow)
    {
        _lastDisconnectUtc = utcNow;
    }

    /// <summary>
    /// 재접속 시도를 허용할지 판정하고 허용 시 1회 토큰을 소비합니다.
    /// </summary>
    public bool TryConsumeReconnectToken(DateTime utcNow, out string reason)
    {
        if (_hasReconnectTokenConsumed)
        {
            reason = "ReconnectAlreadyUsed";
            return false;
        }

        if (_lastDisconnectUtc == default)
        {
            reason = "NoDisconnectRecorded";
            return false;
        }

        double elapsedSeconds = (utcNow - _lastDisconnectUtc).TotalSeconds; // 마지막 이탈 이후 경과 시간(초)입니다.
        if (elapsedSeconds > _reconnectWindowSeconds)
        {
            reason = "ReconnectWindowExpired";
            return false;
        }

        _hasReconnectTokenConsumed = true;
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// 재접속 성공 시 토큰 사용 상태를 해제해 다음 이탈 상황에서 다시 1회 재접속을 허용합니다.
    /// </summary>
    public void MarkReconnectSucceeded()
    {
        _hasReconnectTokenConsumed = false;
        _lastDisconnectUtc = default;
    }

    /// <summary>
    /// 신규 세션 시작 시 재접속 상태를 초기화합니다.
    /// </summary>
    public void Reset()
    {
        _hasReconnectTokenConsumed = false;
        _lastDisconnectUtc = default;
    }
}
