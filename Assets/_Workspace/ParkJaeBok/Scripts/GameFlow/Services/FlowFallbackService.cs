using System;

/// <summary>
/// 폴백 상태 전환과 강제 타이틀 리셋을 전담하는 서비스입니다.
/// </summary>
internal sealed class FlowFallbackService
{
    private readonly Func<bool> _requestReturnToTown; // Town 폴백 시 호출할 콜백입니다.
    private readonly Func<string, bool> _forceResetToTitle; // 타이틀 강제 리셋 시 호출할 콜백입니다.
    private readonly Func<GameFlowState, string, bool> _tryTransition; // 일반 상태 폴백 전환을 시도할 콜백입니다.

    /// <summary>
    /// FlowFallbackService를 생성합니다.
    /// </summary>
    internal FlowFallbackService(
        Func<bool> requestReturnToTown,
        Func<string, bool> forceResetToTitle,
        Func<GameFlowState, string, bool> tryTransition)
    {
        _requestReturnToTown = requestReturnToTown;
        _forceResetToTitle = forceResetToTitle;
        _tryTransition = tryTransition;
    }

    /// <summary>
    /// 정책 대상 상태에 따라 폴백 실행을 시도합니다.
    /// </summary>
    internal bool ExecuteFallback(GameFlowState fallbackState, string reason)
    {
        if (fallbackState == GameFlowState.Title)
        {
            return _forceResetToTitle != null && _forceResetToTitle.Invoke(reason);
        }

        if (fallbackState == GameFlowState.Town)
        {
            bool movedTown = _requestReturnToTown != null && _requestReturnToTown.Invoke();
            if (movedTown)
            {
                return true;
            }

            return _forceResetToTitle != null && _forceResetToTitle.Invoke($"FallbackTownFailed.{reason}");
        }

        return _tryTransition != null && _tryTransition.Invoke(fallbackState, reason);
    }
}
