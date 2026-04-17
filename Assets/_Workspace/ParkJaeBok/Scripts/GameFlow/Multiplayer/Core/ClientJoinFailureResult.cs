using System;

/// <summary>
/// Client Join 실패 정보를 UI/로그 계층으로 전달하는 표준 결과 모델입니다.
/// </summary>
[Serializable]
public struct ClientJoinFailureResult
{
    public E_ClientJoinFailureType FailureType; // 표준화된 실패 유형입니다.
    public string FailureCode; // 원본 실패 코드 또는 정규화 코드입니다.
    public string LogDetail; // 로그 추적에 사용할 상세 문자열입니다.
    public bool IsFallback; // 매핑되지 않은 실패를 일반 실패로 폴백했는지 여부입니다.

    public ClientJoinFailureResult(E_ClientJoinFailureType failureType, string failureCode, string logDetail, bool isFallback)
    {
        FailureType = failureType;
        FailureCode = failureCode;
        LogDetail = logDetail;
        IsFallback = isFallback;
    }
}
