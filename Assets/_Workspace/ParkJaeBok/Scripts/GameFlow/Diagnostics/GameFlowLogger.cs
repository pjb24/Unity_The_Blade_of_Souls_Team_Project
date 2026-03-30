using System;
using UnityEngine;

/// <summary>
/// GameFlow 전용 로그 포맷을 표준화하는 정적 로거 유틸입니다.
/// </summary>
public static class GameFlowLogger
{
    /// <summary>
    /// GameFlow 로그 이벤트 전달에 사용하는 엔트리 DTO입니다.
    /// </summary>
    internal readonly struct GameFlowLogEntry
    {
        public readonly string Level; // 로그 레벨 문자열(Info/Warning/Error/State/Recovery)입니다.
        public readonly string Message; // 포맷 전 원본 메시지 문자열입니다.
        public readonly string FormattedMessage; // 콘솔 출력과 동일한 포맷 문자열입니다.
        public readonly DateTime TimestampUtc; // 로그가 생성된 UTC 시각입니다.

        /// <summary>
        /// 로그 엔트리 DTO를 생성합니다.
        /// </summary>
        public GameFlowLogEntry(string level, string message, string formattedMessage, DateTime timestampUtc)
        {
            Level = level;
            Message = message;
            FormattedMessage = formattedMessage;
            TimestampUtc = timestampUtc;
        }
    }

    private static bool _verboseEnabled = true; // 상세 Info 로그 출력 허용 여부입니다.
    private static Action<string, string> _externalLogSink; // 테스트/텔레메트리에서 로그 패턴을 수집할 외부 캡처 훅입니다.
    internal static event Action<GameFlowLogEntry> OnLogEmitted; // 운영 진단 지표 수집기가 구독할 로그 발행 이벤트입니다.

    /// <summary>
    /// 상세 Info 로그 출력 모드를 설정합니다.
    /// </summary>
    public static void SetVerbose(bool isEnabled)
    {
        _verboseEnabled = isEnabled;
    }

    /// <summary>
    /// 로그 메시지/레벨을 수집할 외부 캡처 훅을 등록합니다.
    /// </summary>
    internal static void SetExternalLogSink(Action<string, string> sink)
    {
        _externalLogSink = sink;
    }

    /// <summary>
    /// 표준 Info 로그를 출력합니다.
    /// </summary>
    public static void Info(string message, UnityEngine.Object context = null, bool force = false)
    {
        if (!force && !_verboseEnabled)
        {
            return;
        }

        if (context != null)
        {
            string formattedMessage = $"[GameFlow][Info] {message}"; // Info 로그 출력 포맷 문자열입니다.
            Debug.Log(formattedMessage, context);
            _externalLogSink?.Invoke("Info", message);
            EmitLog("Info", message, formattedMessage);
            return;
        }

        string formattedMessageWithoutContext = $"[GameFlow][Info] {message}"; // Info 로그 출력 포맷 문자열입니다.
        Debug.Log(formattedMessageWithoutContext);
        _externalLogSink?.Invoke("Info", message);
        EmitLog("Info", message, formattedMessageWithoutContext);
    }

    /// <summary>
    /// 표준 Warning 로그를 출력합니다.
    /// </summary>
    public static void Warning(string message, UnityEngine.Object context = null)
    {
        if (context != null)
        {
            string formattedMessage = $"[GameFlow][Warning] {message}"; // Warning 로그 출력 포맷 문자열입니다.
            Debug.LogWarning(formattedMessage, context);
            _externalLogSink?.Invoke("Warning", message);
            EmitLog("Warning", message, formattedMessage);
            return;
        }

        string formattedMessageWithoutContext = $"[GameFlow][Warning] {message}"; // Warning 로그 출력 포맷 문자열입니다.
        Debug.LogWarning(formattedMessageWithoutContext);
        _externalLogSink?.Invoke("Warning", message);
        EmitLog("Warning", message, formattedMessageWithoutContext);
    }

    /// <summary>
    /// 표준 Error 로그를 출력합니다.
    /// </summary>
    public static void Error(string message, UnityEngine.Object context = null)
    {
        if (context != null)
        {
            string formattedMessage = $"[GameFlow][Error] {message}"; // Error 로그 출력 포맷 문자열입니다.
            Debug.LogError(formattedMessage, context);
            _externalLogSink?.Invoke("Error", message);
            EmitLog("Error", message, formattedMessage);
            return;
        }

        string formattedMessageWithoutContext = $"[GameFlow][Error] {message}"; // Error 로그 출력 포맷 문자열입니다.
        Debug.LogError(formattedMessageWithoutContext);
        _externalLogSink?.Invoke("Error", message);
        EmitLog("Error", message, formattedMessageWithoutContext);
    }

    /// <summary>
    /// 상태 전이 로그를 표준 포맷으로 출력합니다.
    /// </summary>
    public static void StateTransition(GameFlowState fromState, GameFlowState toState, string reason, UnityEngine.Object context = null)
    {
        string message = $"[GameFlow][State] from={fromState}, to={toState}, reason={reason}"; // 상태 전이 로그 본문 문자열입니다.
        if (context != null)
        {
            Debug.Log(message, context);
            _externalLogSink?.Invoke("State", message);
            EmitLog("State", message, message);
            return;
        }

        Debug.Log(message);
        _externalLogSink?.Invoke("State", message);
        EmitLog("State", message, message);
    }

    /// <summary>
    /// 복구/폴백 관련 로그를 표준 포맷으로 출력합니다.
    /// </summary>
    public static void Recovery(string message, UnityEngine.Object context = null, bool force = false)
    {
        if (!force && !_verboseEnabled)
        {
            return;
        }

        string formattedMessage = $"[GameFlow][Recovery] {message}"; // 복구 로그 포맷 문자열입니다.
        if (context != null)
        {
            Debug.Log(formattedMessage, context);
            _externalLogSink?.Invoke("Recovery", message);
            EmitLog("Recovery", message, formattedMessage);
            return;
        }

        Debug.Log(formattedMessage);
        _externalLogSink?.Invoke("Recovery", message);
        EmitLog("Recovery", message, formattedMessage);
    }

    /// <summary>
    /// 로그 발행 이벤트를 구독자에게 전달합니다.
    /// </summary>
    private static void EmitLog(string level, string message, string formattedMessage)
    {
        OnLogEmitted?.Invoke(new GameFlowLogEntry(level, message, formattedMessage, DateTime.UtcNow));
    }
}
