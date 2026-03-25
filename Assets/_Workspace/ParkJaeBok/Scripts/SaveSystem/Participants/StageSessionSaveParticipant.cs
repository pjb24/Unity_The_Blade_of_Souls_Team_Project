using UnityEngine;

/// <summary>
/// StageSession 상태를 저장/복원하는 participant입니다.
/// </summary>
public class StageSessionSaveParticipant : MonoBehaviour, ISaveParticipant
{
    [Tooltip("저장 레코드에서 StageSession participant를 식별하는 고유 ID입니다.")]
    [SerializeField] private string _participantId = "core.stage_session"; // StageSession participant 식별자입니다.

    [Tooltip("Recovery 복원 시 RecoveryPolicy의 StageSession 복원 플래그를 따를지 여부입니다.")]
    [SerializeField] private bool _respectRecoveryPolicy = true; // 복구 채널 복원 시 정책 적용 여부입니다.

    public string ParticipantId => _participantId;
    public int PayloadVersion => 1;

    [System.Serializable]
    private class StageSessionPayload
    {
        public string SelectedStageId; // 마지막으로 선택된 스테이지 ID입니다.
        public string TargetStageEntryPointId; // 다음 씬 진입 포인트 ID입니다.
        public string TargetTownReturnPointId; // 마을 복귀 포인트 ID입니다.
        public E_BgmContextType RequestedBgmContextType; // 다음 씬 적용 예정 BGM 컨텍스트입니다.
    }

    /// <summary>
    /// 현재 저장 문맥에서 저장 가능 여부를 반환합니다.
    /// </summary>
    public bool CanSave(in SaveContext context)
    {
        return StageSession.Instance != null;
    }

    /// <summary>
    /// StageSession 상태를 JSON 문자열로 직렬화합니다.
    /// </summary>
    public string CaptureAsJson(in SaveContext context)
    {
        StageSession.SnapshotData snapshot = StageSession.Instance.CreateSnapshot();
        StageSessionPayload payload = new StageSessionPayload
        {
            SelectedStageId = snapshot.SelectedStageId,
            TargetStageEntryPointId = snapshot.TargetStageEntryPointId,
            TargetTownReturnPointId = snapshot.TargetTownReturnPointId,
            RequestedBgmContextType = snapshot.RequestedBgmContextType
        };

        return JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// JSON 문자열에서 StageSession 상태를 역직렬화해 적용합니다.
    /// </summary>
    public void RestoreFromJson(string payloadJson, in SaveContext context)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return;
        }

        if (context.ChannelType == E_SaveChannelType.Recovery && _respectRecoveryPolicy)
        {
            RecoveryPolicy recoveryPolicy = SaveCoordinator.Instance != null ? SaveCoordinator.Instance.RecoveryPolicy : null;
            if (recoveryPolicy != null && !recoveryPolicy.RestoreStageSession)
            {
                return;
            }
        }

        StageSessionPayload payload = JsonUtility.FromJson<StageSessionPayload>(payloadJson);
        if (payload == null)
        {
            return;
        }

        StageSession.SnapshotData snapshot = new StageSession.SnapshotData
        {
            SelectedStageId = payload.SelectedStageId,
            TargetStageEntryPointId = payload.TargetStageEntryPointId,
            TargetTownReturnPointId = payload.TargetTownReturnPointId,
            RequestedBgmContextType = payload.RequestedBgmContextType
        };

        StageSession.Instance.ApplySnapshot(snapshot);
    }
}
