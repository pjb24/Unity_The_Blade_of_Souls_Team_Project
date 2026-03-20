using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ActionSystem과 HitSystem을 연결해 실제 공격 판정과 HitRequest 전송을 수행하는 공용 실행기입니다.
/// </summary>
public class AttackExecutor : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ActionController _actionController; // 현재 액터의 액션 상태/윈도우를 조회할 ActionController 참조입니다.

    [Header("Action -> AttackSpec Mapping")]
    [SerializeField] private AttackActionMap[] _actionMaps; // 액션 타입별 공격 스펙 매핑 배열입니다.

    [Header("Execution")]
    [SerializeField] private bool _autoExecuteOnHitWindowOpen = true; // HitWindow가 열릴 때 자동으로 현재 액션 공격을 1회 실행할지 여부입니다.
    [SerializeField] private bool _drawGizmos; // 씬 뷰에서 마지막 판정 영역을 Gizmos로 시각화할지 여부입니다.

    private readonly Dictionary<E_ActionType, AttackSpec> _specMap = new Dictionary<E_ActionType, AttackSpec>(); // 런타임 빠른 조회를 위한 액션-스펙 딕셔너리입니다.
    private readonly HashSet<int> _hitTargetsInCurrentSwing = new HashSet<int>(); // 현재 공격 실행에서 이미 타격한 타겟 InstanceId 집합입니다.
    private readonly List<HitReceiver> _targetBuffer = new List<HitReceiver>(); // 타겟 탐지 결과를 임시 보관하는 버퍼 목록입니다.

    private int _currentSwingExecutionId = -1; // 현재 공격 실행 식별자(중복 타격 방지 스코프 구분용)입니다.
    private E_ActionType _currentSwingActionType = E_ActionType.None; // 현재 공격 실행에 대응하는 액션 타입입니다.
    private int _currentSwingHitSerial; // 현재 스윙 내에서 HitRequest 고유성 보장을 위해 증가시키는 시리얼 값입니다.
    private Vector3 _lastGizmoCenter; // 마지막 탐지 중심 좌표(Gizmos 표시용)입니다.
    private Vector2 _lastGizmoBoxSize = Vector2.one; // 마지막 박스 크기(Gizmos 표시용)입니다.
    private float _lastGizmoRadius = 0.5f; // 마지막 원형 반경(Gizmos 표시용)입니다.
    private E_AttackAreaType _lastGizmoAreaType = E_AttackAreaType.Circle; // 마지막 판정 도형 타입(Gizmos 표시용)입니다.

    /// <summary>
    /// 의존성 보정과 액션-공격 매핑 초기화를 수행합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveActionController();
        RebuildSpecMap();
    }

    /// <summary>
    /// 활성화 시 HitWindow 이벤트를 구독해 자동 공격 실행을 준비합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_actionController == null)
        {
            return;
        }

        _actionController.OnHitWindowChanged += HandleHitWindowChanged;
    }

    /// <summary>
    /// 비활성화 시 HitWindow 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_actionController == null)
        {
            return;
        }

        _actionController.OnHitWindowChanged -= HandleHitWindowChanged;
    }

    /// <summary>
    /// 현재 액션 타입에 대응하는 공격 실행을 수동으로 시도합니다.
    /// </summary>
    public bool TryExecuteCurrentActionAttack()
    {
        if (!TryResolveActionController())
        {
            Debug.LogWarning($"[AttackExecutor] Missing ActionController on {name}.");
            return false;
        }

        if (!_actionController.Runtime.IsRunning)
        {
            Debug.LogWarning($"[AttackExecutor] No running action on {name}. Attack execution skipped.");
            return false;
        }

        E_ActionType currentActionType = _actionController.Runtime.ActionType;
        return TryExecuteActionAttack(currentActionType, _actionController.Runtime.ExecutionId);
    }

    /// <summary>
    /// 지정 액션 타입과 실행 식별자로 공격 실행을 시도합니다.
    /// </summary>
    public bool TryExecuteActionAttack(E_ActionType actionType, int executionId)
    {
        if (!TryResolveActionController())
        {
            Debug.LogWarning($"[AttackExecutor] Missing ActionController on {name}.");
            return false;
        }

        if (!_actionController.IsHitWindowOpen)
        {
            Debug.LogWarning($"[AttackExecutor] HitWindow is closed. action={actionType}, actor={name}");
            return false;
        }

        if (!TryGetAttackSpec(actionType, out AttackSpec attackSpec))
        {
            Debug.LogWarning($"[AttackExecutor] No AttackSpec mapped for action={actionType} on {name}.");
            return false;
        }

        BeginSwingScopeIfNeeded(actionType, executionId);

        _targetBuffer.Clear();
        CollectTargets(attackSpec, _targetBuffer);

        if (_targetBuffer.Count == 0)
        {
            Debug.Log($"[AttackExecutor] No targets found. action={actionType}, spec={attackSpec.AttackTypeId}, actor={name}");
            return false;
        }

        bool didSendAnyHit = false;
        for (int i = 0; i < _targetBuffer.Count; i++)
        {
            HitReceiver receiver = _targetBuffer[i];
            if (receiver == null)
            {
                continue;
            }

            int targetId = receiver.gameObject.GetInstanceID();
            if (!attackSpec.AllowMultiHitPerSwing && _hitTargetsInCurrentSwing.Contains(targetId))
            {
                continue;
            }

            HitRequest request = BuildHitRequest(attackSpec, actionType, executionId, receiver, _currentSwingHitSerial);
            _currentSwingHitSerial++;
            HitResult result = receiver.ReceiveHit(request);

            if (result.IsAccepted)
            {
                _hitTargetsInCurrentSwing.Add(targetId);
                didSendAnyHit = true;
            }
        }

        return didSendAnyHit;
    }

    /// <summary>
    /// HitWindow 상태 변경 이벤트를 받아 자동 실행 설정일 때 공격을 트리거합니다.
    /// </summary>
    private void HandleHitWindowChanged(bool isOpen)
    {
        if (!_autoExecuteOnHitWindowOpen)
        {
            return;
        }

        if (!isOpen)
        {
            return;
        }

        TryExecuteCurrentActionAttack();
    }

    /// <summary>
    /// 액션 타입별 공격 스펙 딕셔너리를 재구성합니다.
    /// </summary>
    private void RebuildSpecMap()
    {
        _specMap.Clear();

        if (_actionMaps == null || _actionMaps.Length == 0)
        {
            Debug.LogWarning($"[AttackExecutor] Action map is empty on {name}.");
            return;
        }

        for (int i = 0; i < _actionMaps.Length; i++)
        {
            AttackActionMap map = _actionMaps[i];
            if (map.AttackSpec == null)
            {
                Debug.LogWarning($"[AttackExecutor] Null AttackSpec in action map index={i} on {name}.");
                continue;
            }

            _specMap[map.ActionType] = map.AttackSpec;
        }
    }

    /// <summary>
    /// 지정 액션 타입에 대응하는 AttackSpec을 조회합니다.
    /// </summary>
    private bool TryGetAttackSpec(E_ActionType actionType, out AttackSpec attackSpec)
    {
        if (_specMap.Count == 0)
        {
            RebuildSpecMap();
        }

        return _specMap.TryGetValue(actionType, out attackSpec);
    }

    /// <summary>
    /// 액션 실행 경계가 바뀌었을 때 스윙 스코프를 갱신하고 중복 타격 기록을 초기화합니다.
    /// </summary>
    private void BeginSwingScopeIfNeeded(E_ActionType actionType, int executionId)
    {
        if (_currentSwingExecutionId == executionId && _currentSwingActionType == actionType)
        {
            return;
        }

        _currentSwingExecutionId = executionId;
        _currentSwingActionType = actionType;
        _currentSwingHitSerial = 0;
        _hitTargetsInCurrentSwing.Clear();
    }

    /// <summary>
    /// 공격 스펙의 도형/필터 규칙으로 HitReceiver 타겟 목록을 수집합니다.
    /// </summary>
    private void CollectTargets(AttackSpec attackSpec, List<HitReceiver> targets)
    {
        Vector3 center = transform.TransformPoint(attackSpec.LocalOffset);
        _lastGizmoCenter = center;
        _lastGizmoAreaType = attackSpec.AreaType;
        _lastGizmoRadius = attackSpec.GetSafeRadius();
        _lastGizmoBoxSize = attackSpec.GetSafeBoxSize();

        Collider2D[] colliders;
        if (attackSpec.AreaType == E_AttackAreaType.Box)
        {
            colliders = Physics2D.OverlapBoxAll(center, attackSpec.GetSafeBoxSize(), 0f, attackSpec.TargetLayerMask);
        }
        else
        {
            colliders = Physics2D.OverlapCircleAll(center, attackSpec.GetSafeRadius(), attackSpec.TargetLayerMask);
        }

        if (colliders == null || colliders.Length == 0)
        {
            return;
        }

        int safeMaxTargets = attackSpec.GetSafeMaxTargets();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (targets.Count >= safeMaxTargets)
            {
                break;
            }

            Collider2D candidateCollider = colliders[i];
            if (candidateCollider == null)
            {
                continue;
            }

            if (attackSpec.RequireTargetTag && !candidateCollider.CompareTag(attackSpec.TargetTag))
            {
                continue;
            }

            HitReceiver receiver = candidateCollider.GetComponentInParent<HitReceiver>();
            if (receiver == null)
            {
                continue;
            }

            if (receiver.gameObject == gameObject)
            {
                continue;
            }

            if (targets.Contains(receiver))
            {
                continue;
            }

            targets.Add(receiver);
        }
    }

    /// <summary>
    /// 공격 스펙/실행 정보/타겟 정보를 조합해 HitRequest를 생성합니다.
    /// </summary>
    private HitRequest BuildHitRequest(AttackSpec attackSpec, E_ActionType actionType, int executionId, HitReceiver receiver, int hitSerial)
    {
        Vector3 attackerPosition = transform.position;
        Vector3 targetPosition = receiver.transform.position;
        Vector2 direction2D = ((Vector2)(targetPosition - attackerPosition)).normalized;

        string hitId = BuildHitId(actionType, executionId, receiver.gameObject.GetInstanceID(), hitSerial, attackSpec.AllowMultiHitPerSwing);
        float safeDamage = attackSpec.GetSafeBaseDamage();

        return new HitRequest(
            hitId: hitId,
            rawDamage: safeDamage,
            attacker: gameObject,
            hitPoint: targetPosition,
            hitDirection: new Vector3(direction2D.x, direction2D.y, 0f),
            statusTag: attackSpec.StatusTag,
            requestTime: Time.time);
    }

    /// <summary>
    /// 공격자/실행/타겟 정보를 합성해 중복 방지용 고유 HitId를 생성합니다.
    /// </summary>
    private string BuildHitId(E_ActionType actionType, int executionId, int targetId, int hitSerial, bool allowMultiHitPerSwing)
    {
        int attackerId = gameObject.GetInstanceID();
        if (allowMultiHitPerSwing)
        {
            return $"{attackerId}:{executionId}:{targetId}:{actionType}:{hitSerial}";
        }

        return $"{attackerId}:{executionId}:{targetId}:{actionType}";
    }

    /// <summary>
    /// ActionController 참조가 비어있을 때 동일 오브젝트에서 자동 보정합니다.
    /// </summary>
    private bool TryResolveActionController()
    {
        if (_actionController != null)
        {
            return true;
        }

        _actionController = GetComponent<ActionController>();
        return _actionController != null;
    }

    /// <summary>
    /// 마지막 판정 영역을 Gizmos로 표시해 디버깅 가시성을 제공합니다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!_drawGizmos)
        {
            return;
        }

        Gizmos.color = Color.red;
        if (_lastGizmoAreaType == E_AttackAreaType.Box)
        {
            Gizmos.DrawWireCube(_lastGizmoCenter, _lastGizmoBoxSize);
            return;
        }

        Gizmos.DrawWireSphere(_lastGizmoCenter, _lastGizmoRadius);
    }
}
