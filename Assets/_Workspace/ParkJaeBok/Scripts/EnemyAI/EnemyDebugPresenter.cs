using UnityEngine;

/// <summary>
/// Enemy AI 런타임 디버그 정보를 Gizmos/Inspector용 문자열로 제공하는 프레젠터입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyDebugPresenter : MonoBehaviour
{
    [Tooltip("디버그 표시 대상 EnemyAIController 참조입니다.")]
    [SerializeField] private EnemyAIController _aiController; // AI 상태 조회용 컨트롤러 참조입니다.
    [Tooltip("디버그 표시 대상 EnemyMovementController 참조입니다.")]
    [SerializeField] private EnemyMovementController _movementController; // 이동 상태 조회용 컨트롤러 참조입니다.

    [Header("Runtime Snapshot (Read Only)")]
    [SerializeField] private string _stateLabel; // 현재 AI 상태 문자열 스냅샷입니다.
    [SerializeField] private string _targetLabel; // 현재 타겟 존재 여부 문자열 스냅샷입니다.
    [SerializeField] private string _destinationLabel; // 현재 목적지 문자열 스냅샷입니다.
    [SerializeField] private string _pathLabel; // 현재 경로 상태 문자열 스냅샷입니다.
    [SerializeField] private string _arrivalLabel; // 현재 도착 판정 보조 문자열 스냅샷입니다.

    /// <summary>
    /// 의존성 자동 연결을 시도합니다.
    /// </summary>
    private void Awake()
    {
        if (_aiController == null)
        {
            _aiController = GetComponent<EnemyAIController>();
        }

        if (_movementController == null)
        {
            _movementController = GetComponent<EnemyMovementController>();
        }
    }

    /// <summary>
    /// 매 프레임 디버그 문자열 스냅샷을 갱신합니다.
    /// </summary>
    private void Update()
    {
        if (_aiController == null || _movementController == null)
        {
            return;
        }

        _stateLabel = _aiController.CurrentStateId.ToString();
        _targetLabel = _aiController.CurrentTarget != null ? _aiController.CurrentTarget.name : "None";
        _destinationLabel = _movementController.HasDestination ? _movementController.CurrentDestination.ToString("F2") : "None";
        _pathLabel = $"pending={_movementController.IsPathPending}, status={_movementController.CurrentPathStatus}";
        _arrivalLabel = $"vel={_movementController.CurrentVelocityMagnitude:F3}, delta={_movementController.AgentRigidbodyDelta:F3}";
    }
}
