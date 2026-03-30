/// <summary>
/// GameFlow 경고 메시지 포맷/문구를 일관되게 관리하는 카탈로그입니다.
/// </summary>
internal static class GameFlowWarningCatalog
{
    internal const string KeyActionDependencyMissing = "GF.UI.DependencyMissing"; // UI 액션 필수 의존성 누락 경고 키입니다.
    internal const string KeyActionFlowRequestFailed = "GF.UI.FlowRequestFailed"; // UI 액션의 GameFlow 요청 실패 경고 키입니다.
    internal const string KeyActionGameFlowRequired = "GF.UI.GameFlowRequired"; // UI 액션 GameFlow 필수 경로 위반 경고 키입니다.
    internal const string KeyStageInvalidInput = "GF.Stage.InvalidInput"; // Stage 프리젠터 입력 검증 실패 경고 키입니다.
    internal const string KeyStageFlowRequestFailed = "GF.Stage.FlowRequestFailed"; // Stage 프리젠터의 GameFlow 요청 실패 경고 키입니다.
    internal const string KeyStageGameFlowRequired = "GF.Stage.GameFlowRequired"; // Stage 프리젠터 GameFlow 필수 경로 위반 경고 키입니다.

    internal const string DuplicateExitRequestIgnored = "중복 종료 요청을 무시합니다."; // 중복 종료 요청 차단 시 공통으로 사용할 경고 메시지입니다.
    internal const string ContinueSceneResolveFailed = "Continue 대상 씬을 해석하지 못했습니다."; // Continue 요청에서 씬 해석 실패 시 사용할 경고 메시지입니다.
    internal const string ContinueSaveNotFound = "Continue 가능한 저장 데이터가 없습니다."; // Continue 복원 데이터 부재 시 사용할 경고 메시지입니다.

    /// <summary>
    /// 저장 실패 보고 메시지를 포맷합니다.
    /// </summary>
    internal static string BuildSaveFailureReported(string reason)
    {
        return $"저장 실패가 보고되었습니다. reason={reason}";
    }

    /// <summary>
    /// 씬 전환 시작 실패 메시지를 포맷합니다.
    /// </summary>
    internal static string BuildSceneLoadStartFailed(string requestName, string sceneName, int retryCount, int maxRetryCount)
    {
        return $"씬 전환 시작 실패. request={requestName}, scene={sceneName}, retry={retryCount}/{maxRetryCount}";
    }

    /// <summary>
    /// Recovery 서킷브레이커 오픈 중 요청 차단 메시지를 포맷합니다.
    /// </summary>
    internal static string BuildRecoveryCircuitOpenRejected(string requestName, double retryAfterSeconds)
    {
        return $"Recovery 서킷브레이커 오픈 상태로 요청을 차단합니다. request={requestName}, retryAfterSeconds={retryAfterSeconds:F1}. 잠시 후 다시 시도하세요.";
    }

    /// <summary>
    /// 경고 키와 메시지를 결합한 표준 경고 문자열을 생성합니다.
    /// </summary>
    internal static string BuildKeyed(string warningKey, string message)
    {
        return $"[{warningKey}] {message}";
    }
}
