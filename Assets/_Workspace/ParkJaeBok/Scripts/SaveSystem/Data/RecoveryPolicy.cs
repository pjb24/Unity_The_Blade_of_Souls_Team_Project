using UnityEngine;

/// <summary>
/// 사망 후 복구 시 어떤 데이터를 복원할지 선택하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "RecoveryPolicy", menuName = "Game/Save System/Recovery Policy")]
public class RecoveryPolicy : ScriptableObject
{
    [Tooltip("사망 이벤트 발생 즉시 Recovery 채널 스냅샷을 기록할지 여부입니다.")]
    [SerializeField] private bool _snapshotOnDeath = true; // 사망 시점 자동 스냅샷 생성 여부입니다.

    [Tooltip("복구 수행 시 체력 값을 복원할지 여부입니다.")]
    [SerializeField] private bool _restoreHealth = true; // 복구 시 체력 데이터를 적용할지 여부입니다.

    [Tooltip("복구 수행 시 StageSession 상태를 복원할지 여부입니다.")]
    [SerializeField] private bool _restoreStageSession = true; // 복구 시 스테이지 세션 데이터를 적용할지 여부입니다.

    public bool SnapshotOnDeath => _snapshotOnDeath;
    public bool RestoreHealth => _restoreHealth;
    public bool RestoreStageSession => _restoreStageSession;
}
