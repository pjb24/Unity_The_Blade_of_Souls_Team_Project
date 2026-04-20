using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Relay;

/// <summary>
/// 분산된 백엔드 실패 코드를 Client Join 표준 실패 결과로 변환하는 매퍼입니다.
/// </summary>
public static class ClientJoinFailureResultMapper
{
    /// <summary>
    /// 원본 실패 코드를 표준 실패 결과로 변환합니다.
    /// </summary>
    public static ClientJoinFailureResult Map(string rawReason, string logDetail)
    {
        string normalizedReason = NormalizeReason(rawReason); // 분류 규칙 적용 전 정규화한 실패 코드입니다.
        E_ClientJoinFailureType failureType = ResolveFailureType(normalizedReason); // 정규화 코드에 대응하는 표준 실패 유형입니다.
        bool isFallback = failureType == E_ClientJoinFailureType.Unknown; // 미매핑 케이스로 일반 실패 폴백이 필요한지 여부입니다.

        return new ClientJoinFailureResult(
            failureType,
            normalizedReason,
            string.IsNullOrWhiteSpace(logDetail) ? normalizedReason : logDetail,
            isFallback);
    }

    /// <summary>
    /// 조인 실패 원본 문자열에서 접두사/공백을 제거해 분류 가능한 코드로 정규화합니다.
    /// </summary>
    private static string NormalizeReason(string rawReason)
    {
        if (string.IsNullOrWhiteSpace(rawReason))
        {
            return "Unknown";
        }

        string trimmedReason = rawReason.Trim(); // 분류 대상의 앞뒤 공백을 제거한 문자열입니다.
        int separatorIndex = trimmedReason.LastIndexOf(':'); // 중첩 접두사(예: JoinFailed:SessionNotFound) 분리를 위한 구분자 위치입니다.
        if (separatorIndex < 0 || separatorIndex >= trimmedReason.Length - 1)
        {
            return trimmedReason;
        }

        return trimmedReason.Substring(separatorIndex + 1).Trim();
    }

    /// <summary>
    /// 정규화된 실패 코드를 표준 실패 유형으로 분류합니다.
    /// </summary>
    private static E_ClientJoinFailureType ResolveFailureType(string normalizedReason)
    {
        if (string.Equals(normalizedReason, "JoinCodeEmpty", StringComparison.Ordinal))
        {
            return E_ClientJoinFailureType.JoinCodeEmpty;
        }

        if (string.Equals(normalizedReason, "SessionNotFound", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "LobbyNotJoined", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "LobbyServiceException", StringComparison.Ordinal))
        {
            return E_ClientJoinFailureType.SessionNotFound;
        }

        if (string.Equals(normalizedReason, "SessionFull", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "LobbyFull", StringComparison.Ordinal))
        {
            return E_ClientJoinFailureType.SessionFull;
        }

        if (string.Equals(normalizedReason, "StageInProgress", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "PlayersNotFull", StringComparison.Ordinal))
        {
            return E_ClientJoinFailureType.StageInProgress;
        }

        if (string.Equals(normalizedReason, "InvalidJoinCode", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "InvalidJoinCodeFormat", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "JoinCodeInvalid", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "RelayJoinCodeMissing", StringComparison.Ordinal))
        {
            return E_ClientJoinFailureType.InvalidJoinCode;
        }

        if (string.Equals(normalizedReason, "NetworkManagerMissing", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "NetworkManagerNotListening", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "TransportMissing", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "NetworkError", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "RequestFailedException", StringComparison.Ordinal)
            || string.Equals(normalizedReason, nameof(RelayServiceException), StringComparison.Ordinal))
        {
            return E_ClientJoinFailureType.NetworkUnavailable;
        }

        if (string.Equals(normalizedReason, "UnityServicesInitializeFailed", StringComparison.Ordinal)
            || string.Equals(normalizedReason, nameof(ServicesInitializationException), StringComparison.Ordinal))
        {
            return E_ClientJoinFailureType.ServiceInitializationFailed;
        }

        if (string.Equals(normalizedReason, nameof(AuthenticationException), StringComparison.Ordinal)
            || string.Equals(normalizedReason, "AuthenticationFailed", StringComparison.Ordinal)
            || string.Equals(normalizedReason, "NotAuthorized", StringComparison.Ordinal))
        {
            return E_ClientJoinFailureType.AuthenticationFailed;
        }

        if (string.Equals(normalizedReason, "AdmissionDenied", StringComparison.Ordinal))
        {
            return E_ClientJoinFailureType.AdmissionDenied;
        }

        if (string.Equals(normalizedReason, "StartClientFailed", StringComparison.Ordinal))
        {
            return E_ClientJoinFailureType.StartClientFailed;
        }

        return E_ClientJoinFailureType.Unknown;
    }
}
