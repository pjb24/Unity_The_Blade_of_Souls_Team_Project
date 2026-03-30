using UnityEngine;

/// <summary>
/// GameFlow 실패 상황별 재시도/폴백 규칙을 정의하는 정책 에셋입니다.
/// </summary>
[CreateAssetMenu(fileName = "ErrorRecoveryPolicy", menuName = "Game/GameFlow/Error Recovery Policy")]
public class ErrorRecoveryPolicy : ScriptableObject
{
    [Header("Logging")]
    [Tooltip("정책 기반 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _enableWarningLogs = true; // 정책 처리 중 Warning 로그 출력 여부입니다.

    [Header("Scene Load Failure")]
    [Tooltip("스테이지 로딩 실패 시 재시도할 최대 횟수입니다.")]
    [Min(0)]
    [SerializeField] private int _maxSceneLoadRetryCount = 1; // 씬 로딩 실패 시 허용할 최대 재시도 횟수입니다.

    [Tooltip("씬 로딩 재시도 전 대기할 시간(초)입니다.")]
    [Min(0f)]
    [SerializeField] private float _sceneLoadRetryIntervalSeconds = 0.25f; // 씬 로딩 재시도 간 대기 시간입니다.

    [Tooltip("씬 로딩 재시도 모두 실패 시 이동할 폴백 상태입니다.")]
    [SerializeField] private GameFlowState _sceneLoadFailureFallbackState = GameFlowState.Title; // 씬 로딩 재시도 초과 시 적용할 폴백 상태입니다.

    [Tooltip("씬 로딩 재시도 소진 후 Recovery 서킷브레이커를 활성화할지 여부입니다.")]
    [SerializeField] private bool _enableRecoveryCircuitBreaker = true; // 재시도 소진 후 일정 시간 fail-fast를 활성화할지 여부입니다.

    [Tooltip("Recovery 서킷브레이커가 열린 상태를 유지할 시간(초)입니다.")]
    [Min(0f)]
    [SerializeField] private float _recoveryCircuitOpenSeconds = 15f; // 서킷브레이커 오픈 상태를 유지할 초 단위 지속 시간입니다.

    [Header("Death Recovery Failure")]
    [Tooltip("플레이어 사망 후 복귀 실패 시 이동할 폴백 상태입니다.")]
    [SerializeField] private GameFlowState _deathRecoveryFailureFallbackState = GameFlowState.Town; // 사망 복구 실패 시 적용할 폴백 상태입니다.

    [Header("Save Failure")]
    [Tooltip("저장 실패 발생 시 SaveFailureDirty 플래그를 유지할지 여부입니다.")]
    [SerializeField] private bool _markSaveFailureDirty = true; // 저장 실패 발생 시 런타임 더티 플래그를 유지할지 여부입니다.

    [Header("Title Return Mismatch")]
    [Tooltip("타이틀 복귀 중 상태 꼬임이 감지되면 강제 타이틀 리셋을 수행할지 여부입니다.")]
    [SerializeField] private bool _forceResetOnTitleMismatch = true; // 타이틀 복귀 상태 불일치 감지 시 강제 리셋 수행 여부입니다.

    [Header("Duplicate Exit")]
    [Tooltip("중복 종료 요청이 들어오면 무시할지 여부입니다.")]
    [SerializeField] private bool _ignoreDuplicateExitRequest = true; // 중복 종료 요청을 무시할지 여부입니다.

    /// <summary>
    /// 정책 기반 경고 로그 출력 여부를 반환합니다.
    /// </summary>
    public bool EnableWarningLogs => _enableWarningLogs;

    /// <summary>
    /// 씬 로딩 실패 시 최대 재시도 횟수를 반환합니다.
    /// </summary>
    public int MaxSceneLoadRetryCount => Mathf.Max(0, _maxSceneLoadRetryCount);

    /// <summary>
    /// 씬 로딩 실패 재시도 간 대기 시간을 반환합니다.
    /// </summary>
    public float SceneLoadRetryIntervalSeconds => Mathf.Max(0f, _sceneLoadRetryIntervalSeconds);

    /// <summary>
    /// 씬 로딩 재시도 초과 시 폴백 상태를 반환합니다.
    /// </summary>
    public GameFlowState SceneLoadFailureFallbackState => _sceneLoadFailureFallbackState;

    /// <summary>
    /// Recovery 서킷브레이커 활성화 여부를 반환합니다.
    /// </summary>
    public bool EnableRecoveryCircuitBreaker => _enableRecoveryCircuitBreaker;

    /// <summary>
    /// Recovery 서킷브레이커 오픈 유지 시간을 반환합니다.
    /// </summary>
    public float RecoveryCircuitOpenSeconds => Mathf.Max(0f, _recoveryCircuitOpenSeconds);

    /// <summary>
    /// 사망 복구 실패 시 폴백 상태를 반환합니다.
    /// </summary>
    public GameFlowState DeathRecoveryFailureFallbackState => _deathRecoveryFailureFallbackState;

    /// <summary>
    /// 저장 실패 시 더티 플래그 유지 여부를 반환합니다.
    /// </summary>
    public bool MarkSaveFailureDirty => _markSaveFailureDirty;

    /// <summary>
    /// 타이틀 복귀 상태 불일치 시 강제 리셋 여부를 반환합니다.
    /// </summary>
    public bool ForceResetOnTitleMismatch => _forceResetOnTitleMismatch;

    /// <summary>
    /// 중복 종료 요청 무시 여부를 반환합니다.
    /// </summary>
    public bool IgnoreDuplicateExitRequest => _ignoreDuplicateExitRequest;
}
