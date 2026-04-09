using System;

/// <summary>
/// 멀티 세션 입장 정책(정원 제한, Stage 중 Join 금지)을 판정하는 서비스입니다.
/// </summary>
public sealed class SessionAdmissionGuardService
{
    private readonly int _maxPlayerCount; // 세션에서 허용할 최대 플레이어 수(Host 포함)입니다.

    /// <summary>
    /// 입장 정책 서비스를 생성합니다.
    /// </summary>
    public SessionAdmissionGuardService(int maxPlayerCount)
    {
        _maxPlayerCount = Math.Max(1, maxPlayerCount);
    }

    /// <summary>
    /// 현재 상태 기준으로 새 참가 요청을 허용할지 판정합니다.
    /// </summary>
    public bool CanJoin(bool isStageInProgress, int currentPlayerCount, out string reason)
    {
        if (isStageInProgress)
        {
            reason = "StageInProgress";
            return false;
        }

        if (currentPlayerCount >= _maxPlayerCount)
        {
            reason = "SessionFull";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
