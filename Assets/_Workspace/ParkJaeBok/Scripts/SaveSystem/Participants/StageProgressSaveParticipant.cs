using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// StageProgressRuntime 상태를 저장/복원하는 participant입니다.
/// </summary>
public class StageProgressSaveParticipant : MonoBehaviour, ISaveParticipant
{
    [Tooltip("저장 레코드에서 StageProgress participant를 식별하는 고유 ID입니다.")]
    [SerializeField] private string _participantId = "core.stage_progress"; // StageProgress participant 식별자입니다.

    [Tooltip("Recovery 복원 시 StageProgress를 복원할지 여부입니다.")]
    [SerializeField] private bool _restoreOnRecovery = true; // Recovery 채널에서 진행도 복원을 허용할지 여부입니다.

    public string ParticipantId => _participantId;
    public int PayloadVersion => 1;

    [System.Serializable]
    private class StageProgressPayload
    {
        public List<StageProgressRecord> Records = new List<StageProgressRecord>(); // StageProgressRuntime 레코드를 담는 저장 페이로드 목록입니다.
    }

    /// <summary>
    /// 현재 저장 문맥에서 저장 가능 여부를 반환합니다.
    /// </summary>
    public bool CanSave(in SaveContext context)
    {
        return StageProgressRuntime.TryGetExistingInstance(out _);
    }

    /// <summary>
    /// StageProgressRuntime 상태를 JSON 문자열로 직렬화합니다.
    /// </summary>
    public string CaptureAsJson(in SaveContext context)
    {
        if (!StageProgressRuntime.TryGetExistingInstance(out StageProgressRuntime runtime))
        {
            return string.Empty;
        }

        StageProgressRuntime.SnapshotData snapshot = runtime.CreateSnapshot();
        StageProgressPayload payload = new StageProgressPayload
        {
            Records = snapshot.Records ?? new List<StageProgressRecord>()
        };

        return JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// JSON 문자열에서 StageProgressRuntime 상태를 역직렬화해 적용합니다.
    /// </summary>
    public void RestoreFromJson(string payloadJson, in SaveContext context)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return;
        }

        if (context.ChannelType == E_SaveChannelType.Recovery && _restoreOnRecovery == false)
        {
            return;
        }

        StageProgressPayload payload = JsonUtility.FromJson<StageProgressPayload>(payloadJson);
        if (payload == null)
        {
            return;
        }

        StageProgressRuntime runtime = StageProgressRuntime.Instance; // 역직렬화 결과를 적용할 진행도 런타임 인스턴스입니다.
        runtime.ApplySnapshot(new StageProgressRuntime.SnapshotData
        {
            Records = payload.Records ?? new List<StageProgressRecord>()
        });
    }
}
