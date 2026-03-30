using System;
using System.Collections.Generic;

/// <summary>
/// GameFlow 런타임 진단 지표를 UI/도구에서 조회하기 위한 스냅샷 DTO입니다.
/// </summary>
[Serializable]
public sealed class GameFlowDebugSnapshot
{
    public List<WarningSummary> RecentWarnings = new List<WarningSummary>(); // 최근 Warning 요약 목록입니다.
    public List<StateTransitionRecord> RecentStateTransitions = new List<StateTransitionRecord>(); // 최근 상태 전이 이력 목록입니다.
    public RecoveryDiagnostics Recovery = new RecoveryDiagnostics(); // Recovery 재시도/결과 진단 정보입니다.
    public SaveFailureDiagnostics SaveFailures = new SaveFailureDiagnostics(); // Save 실패 누적/최근 실패 시각 진단 정보입니다.
    public string SnapshotCreatedAtUtc; // 스냅샷 생성 시각(UTC ISO-8601)입니다.

    /// <summary>
    /// Warning 요약 항목 DTO입니다.
    /// </summary>
    [Serializable]
    public sealed class WarningSummary
    {
        public string Message; // 경고 메시지 본문입니다.
        public string TimestampUtc; // 경고 발생 시각(UTC ISO-8601)입니다.
    }

    /// <summary>
    /// 상태 전이 기록 항목 DTO입니다.
    /// </summary>
    [Serializable]
    public sealed class StateTransitionRecord
    {
        public string FromState; // 전이 이전 상태 이름입니다.
        public string ToState; // 전이 이후 상태 이름입니다.
        public string Reason; // 전이 원인 문자열입니다.
        public string TimestampUtc; // 전이 발생 시각(UTC ISO-8601)입니다.
    }

    /// <summary>
    /// Recovery 진단 정보 DTO입니다.
    /// </summary>
    [Serializable]
    public sealed class RecoveryDiagnostics
    {
        public int RetryCount; // 최근 Recovery 재시도 누적 횟수입니다.
        public string LastResult; // 최근 Recovery 최종 결과 문자열(Succeeded/Failed/Unknown)입니다.
        public string LastUpdatedAtUtc; // Recovery 결과가 마지막으로 갱신된 시각(UTC ISO-8601)입니다.
        public bool CircuitOpen; // Recovery 서킷브레이커 현재 오픈 여부입니다.
        public string CircuitOpenedAtUtc; // Recovery 서킷브레이커가 마지막으로 열린 시각(UTC ISO-8601)입니다.
        public string CircuitOpenUntilUtc; // Recovery 서킷브레이커 오픈 종료 예정 시각(UTC ISO-8601)입니다.
        public int CircuitRejectedCount; // 서킷 오픈 상태에서 차단된 요청 누적 횟수입니다.
        public string LastUserGuidance; // 최근 사용자 안내 메시지 문자열입니다.
    }

    /// <summary>
    /// Save 실패 진단 정보 DTO입니다.
    /// </summary>
    [Serializable]
    public sealed class SaveFailureDiagnostics
    {
        public int FailureCount; // Save 실패 누적 횟수입니다.
        public string LastFailureAtUtc; // 마지막 Save 실패 시각(UTC ISO-8601)입니다.
    }
}
