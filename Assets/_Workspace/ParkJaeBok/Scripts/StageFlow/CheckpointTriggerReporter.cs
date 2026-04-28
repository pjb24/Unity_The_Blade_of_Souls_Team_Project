using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어가 체크포인트 트리거에 진입하면 StageSession에 마지막 체크포인트를 기록하는 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CheckpointTriggerReporter : MonoBehaviour
{
    [Header("Checkpoint")]
    [Tooltip("트리거 진입 시 기록할 체크포인트 ID입니다.")]
    [SerializeField] private string _checkpointId = "CP_A"; // StageSession에 반영할 체크포인트 ID입니다.

    [Tooltip("한 번 활성화된 뒤 같은 플레이 세션에서 중복 발동을 막을지 여부입니다.")]
    [SerializeField] private bool _triggerOnce = true; // 중복 체크포인트 기록을 막기 위한 1회 발동 옵션입니다.

    [Header("Filter")]
    [Tooltip("플레이어 판별에 사용할 태그입니다.")]
    [SerializeField] private string _playerTag = "Player"; // 체크포인트 트리거 대상 판별용 플레이어 태그입니다.

    [Header("Optional Save")]
    [Tooltip("세이브 시스템 제거 후에는 사용되지 않습니다. 기존 Inspector 값을 보존하기 위한 Deprecated 옵션입니다.")]
    [SerializeField] private bool _saveRecoveryAfterTrigger = false; // 저장 시스템 제거로 더 이상 동작하지 않는 레거시 옵션입니다.

    [Tooltip("세이브 시스템 제거 후에는 사용되지 않습니다. 기존 Inspector 값을 보존하기 위한 Deprecated 문자열입니다.")]
    [SerializeField] private string _saveTriggerContext = "CheckpointTrigger"; // 저장 시스템 제거 후 사용하지 않는 레거시 문맥 문자열입니다.

    private bool _alreadyTriggered; // triggerOnce 옵션에서 중복 발동을 차단하기 위한 런타임 상태입니다.

    /// <summary>
    /// 2D 트리거 진입 시 플레이어 여부를 확인하고 체크포인트를 기록합니다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryReportCheckpoint(other != null ? other.gameObject : null);
    }

    /// <summary>
    /// 입력 오브젝트에서 플레이어를 해석해 StageSession에 체크포인트를 반영합니다.
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

        StageSession session = StageSession.Instance; // 체크포인트 런타임 상태를 기록할 세션 인스턴스입니다.
        Transform playerTransform = playerObject.transform; // 체크포인트 위치로 기록할 플레이어 Transform입니다.
        session.SetLastCheckpoint(_checkpointId, SceneManager.GetActiveScene().name, playerTransform.position);
        _alreadyTriggered = true;

        if (_saveRecoveryAfterTrigger)
        {
            SaveDataStore saveDataStore = SaveDataStore.Instance; // 체크포인트 직후 로컬 진행 저장을 처리할 단일 저장소입니다.
            if (saveDataStore == null)
            {
                Debug.LogWarning($"[CheckpointTriggerReporter] SaveDataStore를 찾을 수 없어 체크포인트 저장을 수행하지 못했습니다. context={_saveTriggerContext}", this);
                return;
            }

            if (!saveDataStore.Save(_saveTriggerContext))
            {
                Debug.LogWarning($"[CheckpointTriggerReporter] 체크포인트 저장 요청이 실패했습니다. context={_saveTriggerContext}", this);
            }
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

        Transform current = sourceObject.transform.parent; // 부모 계층에서 Player 태그를 확인하기 위한 순회 포인터입니다.
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
    /// 플레이 상태 재시작 시 트리거 발동 상태를 초기화합니다.
    /// </summary>
    public void ResetTriggerState()
    {
        _alreadyTriggered = false;
    }
}
