using UnityEngine;

/// <summary>
/// 플레이어가 트리거에 진입했을 때 체크포인트 도달을 저장 participant에 보고하는 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CheckpointTriggerReporter : MonoBehaviour
{
    [Header("Checkpoint")]
    [Tooltip("트리거 진입 시 기록할 체크포인트 ID입니다.")]
    [SerializeField] private string _checkpointId = "CP_A"; // 트리거 진입 시 StageSession에 반영할 체크포인트 ID입니다.

    [Tooltip("한 번 활성화된 뒤 동일 플레이 세션에서 중복 발동을 막을지 여부입니다.")]
    [SerializeField] private bool _triggerOnce = true; // 중복 저장 요청을 줄이기 위한 1회 발동 옵션입니다.

    [Header("Filter")]
    [Tooltip("플레이어 판별에 사용할 태그입니다.")]
    [SerializeField] private string _playerTag = "Player"; // 체크포인트 트리거 대상 판별용 플레이어 태그입니다.

    [Tooltip("트리거 대상의 부모 오브젝트에서 Participant를 탐색할지 여부입니다.")]
    [SerializeField] private bool _searchParticipantInParent = true; // 플레이어 루트/자식 구조를 모두 지원하기 위한 부모 탐색 옵션입니다.

    [Tooltip("트리거 대상의 자식 오브젝트에서 Participant를 탐색할지 여부입니다.")]
    [SerializeField] private bool _searchParticipantInChildren = true; // participant가 플레이어 자식에 붙은 구조를 지원하기 위한 자식 탐색 옵션입니다.

    [Header("Optional Save")]
    [Tooltip("체크포인트 반영 직후 Recovery 채널 저장을 즉시 수행할지 여부입니다.")]
    [SerializeField] private bool _saveRecoveryAfterTrigger = false; // 체크포인트 반영 즉시 Recovery 스냅샷 저장을 수행할지 여부입니다.

    [Tooltip("Recovery 저장 호출 시 사용할 트리거 컨텍스트 문자열입니다.")]
    [SerializeField] private string _saveTriggerContext = "CheckpointTrigger"; // 저장 로그 식별을 위한 컨텍스트 문자열입니다.

    private bool _alreadyTriggered; // triggerOnce 옵션일 때 중복 발동을 차단하기 위한 런타임 상태입니다.

    /// <summary>
    /// 2D 트리거 진입 시 플레이어 여부를 확인하고 체크포인트를 기록합니다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryReportCheckpoint(other != null ? other.gameObject : null);
    }

    /// <summary>
    /// 트리거 입력 오브젝트를 검사해 체크포인트 반영 가능하면 participant에 전달합니다.
    /// </summary>
    private void TryReportCheckpoint(GameObject sourceObject)
    {
        if (_triggerOnce && _alreadyTriggered)
        {
            return;
        }

        GameObject playerObject = ResolvePlayerObject(sourceObject);
        if (playerObject == null)
        {
            return;
        }

        PlayerCheckpointSaveParticipant participant = ResolveParticipant(playerObject);
        if (participant == null)
        {
            Debug.LogWarning($"[CheckpointTriggerReporter] PlayerCheckpointSaveParticipant를 찾지 못했습니다. object={playerObject.name}", this);
            return;
        }

        participant.ReportCheckpointReached(_checkpointId);
        _alreadyTriggered = true;

        if (_saveRecoveryAfterTrigger && SaveCoordinator.Instance != null)
        {
            SaveCoordinator.Instance.SaveChannel(E_SaveChannelType.Recovery, E_SaveTriggerType.Manual, _saveTriggerContext);
        }
    }

    /// <summary>
    /// 트리거 입력 오브젝트를 기준으로 Player 태그를 가진 기준 오브젝트를 탐색해 반환합니다.
    /// </summary>
    private GameObject ResolvePlayerObject(GameObject sourceObject)
    {
        if (sourceObject == null)
        {
            return null;
        }

        if (sourceObject.CompareTag(_playerTag))
        {
            return sourceObject;
        }

        Transform current = sourceObject.transform.parent; // Player 태그가 부모 계층에 있는지 확인하기 위한 순회 포인터입니다.
        while (current != null)
        {
            if (current.CompareTag(_playerTag))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }

    /// <summary>
    /// 플레이어 오브젝트에서 체크포인트 participant를 탐색해 반환합니다.
    /// </summary>
    private PlayerCheckpointSaveParticipant ResolveParticipant(GameObject sourceObject)
    {
        PlayerCheckpointSaveParticipant participant = sourceObject.GetComponent<PlayerCheckpointSaveParticipant>();
        if (participant != null)
        {
            return participant;
        }

        if (_searchParticipantInParent)
        {
            participant = sourceObject.GetComponentInParent<PlayerCheckpointSaveParticipant>();
            if (participant != null)
            {
                return participant;
            }
        }

        if (_searchParticipantInChildren)
        {
            participant = sourceObject.GetComponentInChildren<PlayerCheckpointSaveParticipant>(true);
            if (participant != null)
            {
                return participant;
            }
        }

        return null;
    }

    /// <summary>
    /// 플레이 상태를 재시작할 때 트리거 발동 상태를 초기화합니다.
    /// </summary>
    public void ResetTriggerState()
    {
        _alreadyTriggered = false;
    }
}
