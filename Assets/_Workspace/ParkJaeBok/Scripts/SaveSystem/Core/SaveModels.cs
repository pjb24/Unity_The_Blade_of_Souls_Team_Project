using System;
using System.Collections.Generic;

/// <summary>
/// 전체 저장 파일의 루트 스냅샷 모델입니다.
/// </summary>
[Serializable]
public class SaveSnapshot
{
    public string SchemaVersion; // 파일 호환성 검증에 사용할 저장 스키마 버전 문자열입니다.
    public long SavedUnixTimeUtc; // 저장 시각(UTC)을 Unix Time(초)로 기록한 값입니다.
    public string SavedIsoUtc; // 디버그 확인용 ISO-8601 UTC 시각 문자열입니다.
    public E_SaveTriggerType TriggerType; // 이번 저장을 유발한 트리거 유형입니다.
    public string TriggerContext; // 트리거에 대한 부가 정보(씬 이름 등)입니다.
    public List<SaveParticipantRecord> Records = new List<SaveParticipantRecord>(); // participant 단위 페이로드 기록 목록입니다.
}

/// <summary>
/// participant 1개의 저장 페이로드를 표현하는 모델입니다.
/// </summary>
[Serializable]
public class SaveParticipantRecord
{
    public string ParticipantId; // participant를 식별하는 고유 ID입니다.
    public int PayloadVersion; // participant 페이로드 버전입니다.
    public string PayloadJson; // participant가 직렬화한 JSON 문자열입니다.
}

/// <summary>
/// 저장/복원 시점의 호출 문맥 정보입니다.
/// </summary>
public readonly struct SaveContext
{
    public readonly E_SaveChannelType ChannelType; // 현재 처리 중인 저장 채널 유형입니다.
    public readonly E_SaveTriggerType TriggerType; // 저장/복원을 발생시킨 트리거 유형입니다.
    public readonly string TriggerContext; // 트리거 상세 문맥 문자열입니다.

    public SaveContext(E_SaveChannelType channelType, E_SaveTriggerType triggerType, string triggerContext)
    {
        ChannelType = channelType;
        TriggerType = triggerType;
        TriggerContext = triggerContext;
    }
}
