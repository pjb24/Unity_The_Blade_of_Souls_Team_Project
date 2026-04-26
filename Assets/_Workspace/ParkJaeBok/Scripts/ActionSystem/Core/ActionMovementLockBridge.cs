using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ActionController 실행 상태를 PlayerMovement 이동 잠금 API와 연결하는 브리지입니다.
/// </summary>
[DisallowMultipleComponent]
public class ActionMovementLockBridge : MonoBehaviour, IActionListener
{
    [Header("References")]
    [Tooltip("액션 실행 상태를 조회하고 리스너를 등록할 ActionController 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private ActionController _actionController; // 액션 실행 상태를 제공하는 ActionController 참조입니다.
    [Tooltip("이동 잠금 API를 적용할 PlayerMovement 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 이동 잠금 적용 대상 PlayerMovement 참조입니다.
    [Tooltip("로컬 소유권 판정을 위한 NetworkObject 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private NetworkObject _networkObject; // 로컬 소유권 기반 적용 범위를 판정하는 NetworkObject 참조입니다.

    [Header("Movement Lock Defaults")]
    [Tooltip("ActionRuleData Override가 없을 때 공격 계열 액션에 이동 잠금을 적용할지 여부입니다.")]
    [SerializeField] private bool _lockAttackActionsByDefault = true; // 공격 계열 액션 기본 잠금 적용 여부입니다.
    [Tooltip("ActionRuleData Override가 없을 때 피격/브레이크 계열 액션에 이동 잠금을 적용할지 여부입니다.")]
    [SerializeField] private bool _lockHitActionsByDefault = true; // 피격/브레이크 계열 액션 기본 잠금 적용 여부입니다.
    [Tooltip("ActionRuleData Override가 없을 때 사망 계열 액션에 이동 잠금을 적용할지 여부입니다.")]
    [SerializeField] private bool _lockDeathActionsByDefault = true; // 사망 계열 액션 기본 잠금 적용 여부입니다.
    [Tooltip("기본 규칙에서 이동 잠금을 적용할 공격 계열 액션 목록입니다.")]
    [SerializeField]
    private E_ActionType[] _defaultAttackLockActions =
    {
        E_ActionType.Attack,
        E_ActionType.AttackCombo1,
        E_ActionType.AttackCombo2,
        E_ActionType.AttackCombo3,
        E_ActionType.AttackAir,
        E_ActionType.AttackDash,
        E_ActionType.AttackWall,
    }; // 기본 공격 잠금을 적용할 액션 목록입니다.
    [Tooltip("기본 규칙에서 이동 잠금을 적용할 피격/브레이크 계열 액션 목록입니다.")]
    [SerializeField]
    private E_ActionType[] _defaultHitLockActions =
    {
        E_ActionType.Hit,
        E_ActionType.Break,
    }; // 기본 피격 잠금을 적용할 액션 목록입니다.
    [Tooltip("기본 규칙에서 이동 잠금을 적용할 사망 계열 액션 목록입니다.")]
    [SerializeField]
    private E_ActionType[] _defaultDeathLockActions =
    {
        E_ActionType.Die,
    }; // 기본 사망 잠금을 적용할 액션 목록입니다.
    [Tooltip("액션 잠금 적용 시 수평 속도를 즉시 정지해 피격/공격 중 미끄러짐을 제거할지 여부입니다.")]
    [SerializeField] private bool _clearHorizontalVelocityOnLock = true; // 잠금 적용 시 수평 속도 정지 여부입니다.

    [Header("Diagnostics")]
    [Tooltip("NetworkObject가 없어 소유권 검증을 생략할 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenOwnershipUnavailable = true; // 소유권 참조 누락 경고 출력 여부입니다.
    [Tooltip("액션별 잠금/해제 판정 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _enableRuntimeLog; // 런타임 이동 잠금 판정 로그 출력 여부입니다.

    private bool _isActionListenerRegistered; // ActionController 리스너 등록 상태를 추적하는 값입니다.
    private E_MovementLockReason _appliedMovementLockReason = E_MovementLockReason.None; // 현재 이 브리지가 적용 중인 이동 잠금 사유입니다.
    private bool _hasLoggedMissingNetworkObject; // NetworkObject 누락 경고의 중복 출력을 방지하는 값입니다.
    private bool _hasLoggedMissingPlayerMovement; // PlayerMovement 누락 경고의 중복 출력을 방지하는 값입니다.

    /// <summary>
    /// 활성화 시 참조를 보정하고 ActionController 리스너를 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        TryResolveReferences();
        TryRegisterActionListener();
        ReevaluateMovementLockFromRuntime();
    }

    /// <summary>
    /// 비활성화 시 ActionController 리스너를 해제하고 현재 적용 중인 잠금을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        TryUnregisterActionListener();
        ForceReleaseMovementLock();
    }

    /// <summary>
    /// 매 프레임 현재 액션 실행 상태를 기준으로 이동 잠금 상태를 재평가합니다.
    /// </summary>
    private void Update()
    {
        ReevaluateMovementLockFromRuntime();
    }

    /// <summary>
    /// 액션 시작 시 이동 잠금 상태를 재평가합니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
        ReevaluateMovementLockFromRuntime();
    }

    /// <summary>
    /// 액션 단계 변경 시 이동 잠금 상태를 재평가합니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
        ReevaluateMovementLockFromRuntime();
    }

    /// <summary>
    /// 액션 완료 시 이동 잠금 상태를 재평가합니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
        ReevaluateMovementLockFromRuntime();
    }

    /// <summary>
    /// 액션 취소 시 이동 잠금 상태를 재평가합니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
        ReevaluateMovementLockFromRuntime();
    }

    /// <summary>
    /// 현재 런타임 액션과 소유권 조건을 바탕으로 이동 잠금 적용 여부를 동기화합니다.
    /// </summary>
    private void ReevaluateMovementLockFromRuntime()
    {
        if (!TryResolveReferences())
        {
            ForceReleaseMovementLock();
            return;
        }

        if (!CanProcessLocalOwnerLogic())
        {
            ForceReleaseMovementLock();
            return;
        }

        ActionRuntime runtime = _actionController.Runtime; // 현재 액션 실행 상태 런타임입니다.
        E_MovementLockReason movementLockReason = E_MovementLockReason.None;
        bool shouldLock = runtime.IsRunning && TryResolveMovementLockForAction(runtime.ActionType, out movementLockReason);

        if (shouldLock)
        {
            ApplyMovementLock(runtime.ActionType, movementLockReason);
            return;
        }

        ForceReleaseMovementLock();
    }

    /// <summary>
    /// 지정한 액션 타입이 이동 잠금을 유발하는지와 잠금 사유를 함께 판정합니다.
    /// </summary>
    private bool TryResolveMovementLockForAction(E_ActionType actionType, out E_MovementLockReason movementLockReason)
    {
        movementLockReason = ResolveMovementLockReason(actionType);

        if (_actionController.TryGetActionRule(actionType, out ActionRuleData actionRule) && actionRule.OverrideMovementLockSetting)
        {
            return actionRule.LockMovementDuringAction;
        }

        if (_lockAttackActionsByDefault && ContainsAction(_defaultAttackLockActions, actionType))
        {
            movementLockReason = E_MovementLockReason.Attack;
            return true;
        }

        if (_lockHitActionsByDefault && ContainsAction(_defaultHitLockActions, actionType))
        {
            movementLockReason = E_MovementLockReason.Hit;
            return true;
        }

        if (_lockDeathActionsByDefault && ContainsAction(_defaultDeathLockActions, actionType))
        {
            movementLockReason = E_MovementLockReason.Die;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 액션 타입 의미를 기준으로 기본 이동 잠금 사유를 계산합니다.
    /// </summary>
    private E_MovementLockReason ResolveMovementLockReason(E_ActionType actionType)
    {
        if (ContainsAction(_defaultHitLockActions, actionType))
        {
            return E_MovementLockReason.Hit;
        }

        if (ContainsAction(_defaultDeathLockActions, actionType))
        {
            return E_MovementLockReason.Die;
        }

        return E_MovementLockReason.Attack;
    }

    /// <summary>
    /// 지정한 액션 타입이 액션 목록에 포함되어 있는지 검사합니다.
    /// </summary>
    private bool ContainsAction(E_ActionType[] actionTypes, E_ActionType actionType)
    {
        if (actionTypes == null)
        {
            return false;
        }

        for (int index = 0; index < actionTypes.Length; index++)
        {
            if (actionTypes[index] == actionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 지정한 잠금 사유로 이동 잠금을 적용하고 필요 시 수평 속도를 정지합니다.
    /// </summary>
    private void ApplyMovementLock(E_ActionType actionType, E_MovementLockReason movementLockReason)
    {
        if (_playerMovement == null)
        {
            if (!_hasLoggedMissingPlayerMovement)
            {
                Debug.LogWarning($"[ActionMovementLockBridge] Action={actionType} requires movement lock, but PlayerMovement is missing. object={name}");
                _hasLoggedMissingPlayerMovement = true;
            }

            return;
        }

        _hasLoggedMissingPlayerMovement = false;

        if (_appliedMovementLockReason == movementLockReason)
        {
            return;
        }

        ForceReleaseMovementLock();
        _playerMovement.AddMovementLock(movementLockReason, _clearHorizontalVelocityOnLock);
        _appliedMovementLockReason = movementLockReason;

        if (_enableRuntimeLog)
        {
            Debug.Log($"[ActionMovementLockBridge] Movement lock applied. action={actionType}, reason={movementLockReason}, object={name}");
        }
    }

    /// <summary>
    /// 현재 이 브리지가 적용한 이동 잠금을 안전하게 해제합니다.
    /// </summary>
    private void ForceReleaseMovementLock()
    {
        if (_appliedMovementLockReason == E_MovementLockReason.None)
        {
            return;
        }

        if (_playerMovement != null)
        {
            _playerMovement.RemoveMovementLock(_appliedMovementLockReason);
        }

        _appliedMovementLockReason = E_MovementLockReason.None;
    }

    /// <summary>
    /// 필요한 참조(ActionController/PlayerMovement/NetworkObject)를 자동 보정합니다.
    /// </summary>
    private bool TryResolveReferences()
    {
        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
        }

        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }

        if (_networkObject == null)
        {
            _networkObject = GetComponent<NetworkObject>();

            if (_networkObject == null && _warnWhenOwnershipUnavailable && !_hasLoggedMissingNetworkObject)
            {
                Debug.LogWarning($"[ActionMovementLockBridge] NetworkObject가 없어 소유권 검증 없이 동작합니다. object={name}");
                _hasLoggedMissingNetworkObject = true;
            }
        }

        return _actionController != null;
    }

    /// <summary>
    /// ActionController 리스너를 중복 없이 등록합니다.
    /// </summary>
    private void TryRegisterActionListener()
    {
        if (_isActionListenerRegistered || _actionController == null)
        {
            return;
        }

        _actionController.AddListener(this);
        _isActionListenerRegistered = true;
    }

    /// <summary>
    /// ActionController 리스너 등록을 안전하게 해제합니다.
    /// </summary>
    private void TryUnregisterActionListener()
    {
        if (!_isActionListenerRegistered || _actionController == null)
        {
            return;
        }

        _actionController.RemoveListener(this);
        _isActionListenerRegistered = false;
    }

    /// <summary>
    /// 현재 인스턴스가 로컬 소유자 기준 이동 잠금 판정을 처리해야 하는지 확인합니다.
    /// </summary>
    private bool CanProcessLocalOwnerLogic()
    {
        if (_networkObject == null)
        {
            return true;
        }

        if (!_networkObject.IsSpawned)
        {
            return IsOfflineSingleRoute();
        }

        return _networkObject.IsOwner;
    }

    /// <summary>
    /// NetworkObject가 Spawn되지 않은 상황이 오프라인 싱글 루트인지 판정합니다.
    /// </summary>
    private bool IsOfflineSingleRoute()
    {
        if (NetworkManager.Singleton == null)
        {
            return true;
        }

        return !NetworkManager.Singleton.IsListening;
    }
}
