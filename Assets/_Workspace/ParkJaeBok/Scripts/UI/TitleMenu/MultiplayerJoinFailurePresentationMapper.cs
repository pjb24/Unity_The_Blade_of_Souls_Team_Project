using UnityEngine;

/// <summary>
/// Client Join 실패 결과를 디자이너 카탈로그 기반 UI 표시 모델로 변환하는 매퍼입니다.
/// </summary>
public class MultiplayerJoinFailurePresentationMapper
{
    private readonly MultiplayerJoinFailureMessageCatalog _catalog; // 실패 유형별 텍스트/표시 정책을 제공하는 카탈로그 참조입니다.

    public MultiplayerJoinFailurePresentationMapper(MultiplayerJoinFailureMessageCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// 실패 결과를 UI 표시 모델로 변환합니다.
    /// </summary>
    public MultiplayerJoinFailurePresentationModel Map(ClientJoinFailureResult failureResult)
    {
        if (_catalog == null)
        {
            Debug.LogWarning("[MultiplayerJoinFailurePresentationMapper] Catalog가 비어 있어 내장 기본 메시지를 사용합니다.");
            return new MultiplayerJoinFailurePresentationModel(
                "접속 실패",
                "세션 입장에 실패했습니다. 잠시 후 다시 시도해 주세요.",
                true,
                false,
                0f,
                true);
        }

        if (_catalog.TryGetEntry(failureResult.FailureType, out MultiplayerJoinFailureMessageCatalog.Entry entry))
        {
            return new MultiplayerJoinFailurePresentationModel(
                entry.Title,
                entry.Body,
                entry.UseTitle,
                entry.AutoClose,
                entry.AutoCloseDelaySeconds,
                entry.UseManualCloseButton);
        }

        Debug.LogWarning($"[MultiplayerJoinFailurePresentationMapper] 실패 유형 매핑 누락: type={failureResult.FailureType}, code={failureResult.FailureCode}");
        MultiplayerJoinFailureMessageCatalog.Entry fallbackEntry = _catalog.BuildFallbackEntry(); // 미매핑 실패에서 사용할 폴백 엔트리입니다.
        return new MultiplayerJoinFailurePresentationModel(
            fallbackEntry.Title,
            fallbackEntry.Body,
            fallbackEntry.UseTitle,
            fallbackEntry.AutoClose,
            fallbackEntry.AutoCloseDelaySeconds,
            fallbackEntry.UseManualCloseButton);
    }
}
