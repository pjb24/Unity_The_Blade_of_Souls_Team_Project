using System.Threading;

/// <summary>
/// 종료 요청의 중복 진입을 원자적으로 차단하는 가드입니다.
/// </summary>
internal sealed class FlowExitGuard
{
    private int _gate; // 0이면 미점유, 1이면 종료 요청 처리 중 상태를 의미하는 원자적 게이트 값입니다.

    /// <summary>
    /// 종료 요청 처리 진입을 시도합니다.
    /// </summary>
    internal bool TryEnter()
    {
        int previousValue = Interlocked.CompareExchange(ref _gate, 1, 0); // 종료 처리 진입 성공 여부를 판정할 이전 게이트 값입니다.
        return previousValue == 0;
    }

    /// <summary>
    /// 종료 요청 처리 게이트를 해제합니다.
    /// </summary>
    internal void Release()
    {
        Interlocked.Exchange(ref _gate, 0);
    }

    /// <summary>
    /// 현재 종료 요청 처리 중인지 여부를 반환합니다.
    /// </summary>
    internal bool IsEntered()
    {
        return Interlocked.CompareExchange(ref _gate, 0, 0) != 0;
    }
}
