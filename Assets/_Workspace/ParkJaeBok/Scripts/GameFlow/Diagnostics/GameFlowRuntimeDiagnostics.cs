using System;
using System.Collections.Generic;

/// <summary>
/// GameFlow 운영 진단 지표를 런타임에서 누적/요약하는 저장소 클래스입니다.
/// </summary>
internal sealed class GameFlowRuntimeDiagnostics
{
    private readonly int _warningCapacity; // Warning 환형 버퍼 최대 보관 개수입니다.
    private readonly int _stateTransitionCapacity; // 상태 전이 환형 버퍼 최대 보관 개수입니다.

    private readonly List<GameFlowDebugSnapshot.WarningSummary> _warnings = new List<GameFlowDebugSnapshot.WarningSummary>(); // 최근 Warning 요약 버퍼입니다.
    private readonly List<GameFlowDebugSnapshot.StateTransitionRecord> _stateTransitions = new List<GameFlowDebugSnapshot.StateTransitionRecord>(); // 최근 상태 전이 버퍼입니다.

    private int _recoveryRetryCount; // 최근 Recovery 흐름에서 누적된 재시도 횟수입니다.
    private string _recoveryLastResult = "Unknown"; // 최근 Recovery 최종 결과 문자열입니다.
    private DateTime _recoveryLastUpdatedAtUtc; // Recovery 결과가 마지막으로 갱신된 UTC 시각입니다.
    private bool _recoveryCircuitOpen; // Recovery 서킷브레이커 현재 오픈 여부입니다.
    private DateTime _recoveryCircuitOpenedAtUtc; // Recovery 서킷브레이커가 열린 UTC 시각입니다.
    private DateTime _recoveryCircuitOpenUntilUtc; // Recovery 서킷브레이커 오픈 종료 예정 UTC 시각입니다.
    private int _recoveryCircuitRejectedCount; // Recovery 서킷 오픈으로 차단된 요청 누적 횟수입니다.
    private string _recoveryLastUserGuidance = string.Empty; // 최근 사용자 안내 메시지 문자열입니다.

    private int _saveFailureCount; // Save 실패 누적 횟수입니다.
    private DateTime _saveLastFailureAtUtc; // Save 실패가 마지막으로 발생한 UTC 시각입니다.

    /// <summary>
    /// 지정 용량으로 런타임 진단 저장소를 생성합니다.
    /// </summary>
    internal GameFlowRuntimeDiagnostics(int warningCapacity, int stateTransitionCapacity)
    {
        _warningCapacity = Math.Max(1, warningCapacity);
        _stateTransitionCapacity = Math.Max(1, stateTransitionCapacity);
    }

    /// <summary>
    /// Warning 이벤트를 최근 Warning 버퍼에 기록합니다.
    /// </summary>
    internal void RecordWarning(string message, DateTime timestampUtc)
    {
        AddWithRingBufferLimit(
            _warnings,
            new GameFlowDebugSnapshot.WarningSummary
            {
                Message = message,
                TimestampUtc = timestampUtc.ToString("o")
            },
            _warningCapacity);
    }

    /// <summary>
    /// 상태 전이 이벤트를 최근 전이 버퍼에 기록합니다.
    /// </summary>
    internal void RecordStateTransition(string fromState, string toState, string reason, DateTime timestampUtc)
    {
        AddWithRingBufferLimit(
            _stateTransitions,
            new GameFlowDebugSnapshot.StateTransitionRecord
            {
                FromState = fromState,
                ToState = toState,
                Reason = reason,
                TimestampUtc = timestampUtc.ToString("o")
            },
            _stateTransitionCapacity);
    }

    /// <summary>
    /// Recovery 재시도 횟수를 누적합니다.
    /// </summary>
    internal void IncrementRecoveryRetry(DateTime timestampUtc)
    {
        _recoveryRetryCount++;
        _recoveryLastUpdatedAtUtc = timestampUtc;
    }

    /// <summary>
    /// Recovery 최종 결과를 갱신합니다.
    /// </summary>
    internal void SetRecoveryResult(bool succeeded, DateTime timestampUtc)
    {
        _recoveryLastResult = succeeded ? "Succeeded" : "Failed";
        _recoveryLastUpdatedAtUtc = timestampUtc;
    }

    /// <summary>
    /// Recovery 서킷브레이커 오픈 상태를 갱신합니다.
    /// </summary>
    internal void OpenRecoveryCircuit(DateTime openedAtUtc, DateTime openUntilUtc, string userGuidance)
    {
        _recoveryCircuitOpen = true;
        _recoveryCircuitOpenedAtUtc = openedAtUtc;
        _recoveryCircuitOpenUntilUtc = openUntilUtc;
        _recoveryLastUserGuidance = userGuidance;
    }

    /// <summary>
    /// Recovery 서킷브레이커를 닫고 상태를 갱신합니다.
    /// </summary>
    internal void CloseRecoveryCircuit(DateTime timestampUtc)
    {
        _recoveryCircuitOpen = false;
        _recoveryCircuitOpenUntilUtc = timestampUtc;
    }

    /// <summary>
    /// 서킷브레이커 오픈 상태에서 차단된 요청 횟수를 누적합니다.
    /// </summary>
    internal void RecordRecoveryCircuitRejected()
    {
        _recoveryCircuitRejectedCount++;
    }

    /// <summary>
    /// Save 실패 누적 횟수/마지막 실패 시각을 갱신합니다.
    /// </summary>
    internal void RecordSaveFailure(DateTime timestampUtc)
    {
        _saveFailureCount++;
        _saveLastFailureAtUtc = timestampUtc;
    }

    /// <summary>
    /// 현재 누적 상태를 읽기 전용 스냅샷 DTO로 복사합니다.
    /// </summary>
    internal GameFlowDebugSnapshot CreateSnapshot(DateTime timestampUtc)
    {
        GameFlowDebugSnapshot snapshot = new GameFlowDebugSnapshot
        {
            SnapshotCreatedAtUtc = timestampUtc.ToString("o")
        };

        snapshot.RecentWarnings.AddRange(_warnings);
        snapshot.RecentStateTransitions.AddRange(_stateTransitions);

        snapshot.Recovery.RetryCount = _recoveryRetryCount;
        snapshot.Recovery.LastResult = _recoveryLastResult;
        snapshot.Recovery.LastUpdatedAtUtc = _recoveryLastUpdatedAtUtc == default ? string.Empty : _recoveryLastUpdatedAtUtc.ToString("o");
        snapshot.Recovery.CircuitOpen = _recoveryCircuitOpen;
        snapshot.Recovery.CircuitOpenedAtUtc = _recoveryCircuitOpenedAtUtc == default ? string.Empty : _recoveryCircuitOpenedAtUtc.ToString("o");
        snapshot.Recovery.CircuitOpenUntilUtc = _recoveryCircuitOpenUntilUtc == default ? string.Empty : _recoveryCircuitOpenUntilUtc.ToString("o");
        snapshot.Recovery.CircuitRejectedCount = _recoveryCircuitRejectedCount;
        snapshot.Recovery.LastUserGuidance = _recoveryLastUserGuidance;

        snapshot.SaveFailures.FailureCount = _saveFailureCount;
        snapshot.SaveFailures.LastFailureAtUtc = _saveLastFailureAtUtc == default ? string.Empty : _saveLastFailureAtUtc.ToString("o");

        return snapshot;
    }

    /// <summary>
    /// 리스트를 환형 버퍼처럼 유지하도록 최대 용량을 초과하면 가장 오래된 항목을 제거합니다.
    /// </summary>
    private void AddWithRingBufferLimit<T>(List<T> list, T item, int capacity)
    {
        if (list.Count >= capacity)
        {
            list.RemoveAt(0);
        }

        list.Add(item);
    }
}
