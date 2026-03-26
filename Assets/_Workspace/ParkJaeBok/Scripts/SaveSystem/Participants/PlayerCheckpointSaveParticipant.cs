using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 체크포인트 상태를 저장/복원하는 participant입니다.
/// </summary>
public class PlayerCheckpointSaveParticipant : MonoBehaviour, ISaveParticipant
{
    [Tooltip("저장 레코드에서 체크포인트 participant를 식별하는 고유 ID입니다.")]
    [SerializeField] private string _participantId = "player.checkpoint"; // 체크포인트 participant 식별자입니다.

    [Tooltip("체크포인트 좌표를 저장할 플레이어 Transform 참조입니다. 비어 있으면 현재 오브젝트 Transform을 사용합니다.")]
    [SerializeField] private Transform _playerTransform; // 체크포인트 월드 좌표를 캡처할 대상 Transform입니다.

    [Tooltip("체크포인트 ID가 비어 있을 때 사용할 기본 체크포인트 ID입니다.")]
    [SerializeField] private string _defaultCheckpointId = "CP_Default"; // 체크포인트 ID 미지정 상황에서 사용할 기본값입니다.

    [Tooltip("Recovery 복원 시 RecoveryPolicy의 체크포인트 RuleSet을 따를지 여부입니다.")]
    [SerializeField] private bool _respectRecoveryPolicy = true; // 복구 채널 복원 시 체크포인트 정책 적용 여부입니다.

    [System.Serializable]
    private class PlayerCheckpointPayload
    {
        public string CheckpointId; // 저장 시점 마지막 체크포인트 ID입니다.
        public string SceneName; // 저장 시점 체크포인트가 속한 씬 이름입니다.
        public Vector3 WorldPosition; // 저장 시점 체크포인트 월드 좌표입니다.
    }

    public string ParticipantId => _participantId;
    public int PayloadVersion => 1;

    /// <summary>
    /// 플레이어 Transform 참조를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        if (_playerTransform == null)
        {
            _playerTransform = transform;
        }
    }

    /// <summary>
    /// 현재 저장 문맥에서 저장 가능 여부를 반환합니다.
    /// </summary>
    public bool CanSave(in SaveContext context)
    {
        return StageSession.Instance != null;
    }

    /// <summary>
    /// 플레이어 체크포인트 상태를 JSON 문자열로 직렬화합니다.
    /// </summary>
    public string CaptureAsJson(in SaveContext context)
    {
        StageSession.SnapshotData sessionSnapshot = StageSession.Instance.CreateSnapshot(); // 저장 직전 StageSession 스냅샷입니다.

        string checkpointId = string.IsNullOrWhiteSpace(sessionSnapshot.LastCheckpointId)
            ? _defaultCheckpointId
            : sessionSnapshot.LastCheckpointId;

        string sceneName = string.IsNullOrWhiteSpace(sessionSnapshot.LastCheckpointSceneName)
            ? SceneManager.GetActiveScene().name
            : sessionSnapshot.LastCheckpointSceneName;

        Vector3 worldPosition = _playerTransform != null
            ? _playerTransform.position
            : sessionSnapshot.LastCheckpointWorldPosition;

        PlayerCheckpointPayload payload = new PlayerCheckpointPayload
        {
            CheckpointId = checkpointId,
            SceneName = sceneName,
            WorldPosition = worldPosition
        };

        return JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// JSON 문자열에서 플레이어 체크포인트 상태를 복원합니다.
    /// </summary>
    public void RestoreFromJson(string payloadJson, in SaveContext context)
    {
        if (string.IsNullOrWhiteSpace(payloadJson) || StageSession.Instance == null)
        {
            return;
        }

        PlayerCheckpointPayload payload = JsonUtility.FromJson<PlayerCheckpointPayload>(payloadJson);
        if (payload == null)
        {
            return;
        }

        if (context.ChannelType == E_SaveChannelType.Recovery && _respectRecoveryPolicy)
        {
            RecoveryPolicy recoveryPolicy = SaveCoordinator.Instance != null ? SaveCoordinator.Instance.RecoveryPolicy : null;
            if (!RecoveryPolicyRuleHelper.ShouldRestoreCheckpoint(recoveryPolicy, payload.CheckpointId))
            {
                return;
            }
        }

        StageSession.Instance.SetLastCheckpoint(payload.CheckpointId, payload.SceneName, payload.WorldPosition);

        if (context.ChannelType == E_SaveChannelType.Recovery)
        {
            StageSession.Instance.MarkUseCheckpointForNextSpawn(true);
        }
    }

    /// <summary>
    /// 외부 트리거에서 호출해 현재 체크포인트를 StageSession에 반영합니다.
    /// </summary>
    public void ReportCheckpointReached(string checkpointId)
    {
        if (StageSession.Instance == null)
        {
            return;
        }

        Transform sourceTransform = _playerTransform != null ? _playerTransform : transform; // 체크포인트 좌표로 반영할 기준 Transform입니다.
        string safeCheckpointId = string.IsNullOrWhiteSpace(checkpointId) ? _defaultCheckpointId : checkpointId; // 빈 ID 입력을 방지한 안전 체크포인트 ID입니다.
        StageSession.Instance.SetLastCheckpoint(safeCheckpointId, SceneManager.GetActiveScene().name, sourceTransform.position);
    }
}
