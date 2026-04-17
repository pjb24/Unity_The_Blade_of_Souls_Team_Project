using UnityEngine;

/// <summary>
/// CameraEffect 시스템 경고 메시지 키/포맷을 일관되게 관리하는 카탈로그입니다.
/// </summary>
public static class CameraEffectWarningCatalog
{
    public const string KeyInvalidRequest = "CE.Request.Invalid"; // 요청 유효성 검증 실패 시 사용할 경고 키입니다.
    public const string KeyDependencyMissing = "CE.Dependency.Missing"; // 필수 의존성 누락 시 사용할 경고 키입니다.
    public const string KeyInvalidPresetData = "CE.Preset.InvalidData"; // 프리셋 데이터가 잘못되었을 때 사용할 경고 키입니다.
    public const string KeyLifecycleState = "CE.Lifecycle.State"; // 재생/종료 수명주기 상태 경고 시 사용할 경고 키입니다.
    public const string KeyInvalidModuleData = "CE.Module.InvalidData"; // 모듈 설정값이 잘못되었을 때 사용할 경고 키입니다.
    public const string KeyChannelState = "CE.Channel.State"; // 채널 라우팅/충돌 상태 경고 시 사용할 경고 키입니다.
    public const string KeyPriorityState = "CE.Priority.State"; // 우선순위 비교/정책 처리 경고 시 사용할 경고 키입니다.
    public const string KeyAccessibilityState = "CE.Accessibility.State"; // 접근성 옵션 연동/폴백 처리 경고 시 사용할 경고 키입니다.

    /// <summary>
    /// 경고 키와 메시지를 결합해 표준 경고 문자열을 생성합니다.
    /// </summary>
    public static string BuildKeyed(string warningKey, string message)
    {
        return $"[{warningKey}] {message}";
    }

    /// <summary>
    /// 효과 재생 요청 실패 메시지를 포맷합니다.
    /// </summary>
    public static string BuildPlayRejected(string reason)
    {
        return $"효과 재생 요청이 거절되었습니다. reason={reason}";
    }

    /// <summary>
    /// 활성 효과 종료 메시지를 포맷합니다.
    /// </summary>
    public static string BuildStopReason(string reason)
    {
        return $"활성 효과를 종료합니다. reason={reason}";
    }
}
