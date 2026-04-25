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
    [SerializeField] private bool _lockAttackActionsByDefault = true; // 액션별 오버라이드가 없을 때 공격 액션 잠금 기본 정책입니다.
    [Tooltip("ActionRuleData Override가 없을 때 이동 잠금을 적용할 기본 공격 액션 목록입니다.")]
    [SerializeField]
    private E_ActionType[] _defaultLockActions =
    {
        E_ActionType.Attack,
        E_ActionType.AttackCombo1,
        E_ActionType.AttackCombo2,
        E_ActionType.AttackCombo3,
        E_ActionType.AttackAir,
        E_ActionType.AttackDash,
        E_ActionType.AttackWall,
    }; // 액션별 오버라이드 미사용 시 기본 잠금을 적용할 액션 목록입니다.
    [Tooltip("액션 잠금 적용 시 수평 속도를 즉시 정지해 공격 중 미끄러짐을 제거할지 여부입니다.")]
    [SerializeField] private bool _clearHorizontalVelocityOnLock = true; // 액션 잠금 추가 시 수평 속도 정지 여부입니다.

    [Header("Diagnostics")]
    [Tooltip("NetworkObject가 없어서 소유권 검증을 생략할 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenOwnershipUnavailable = true; // 소유권 참조 누락 경고 출력 여부입니다.
    [Tooltip("액션별 잠금/해제 판정 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _enableRuntimeLog; // 잠금 판정 디버그 로그 출력 여부입니다.

    // ActionController 리스너 등록 여부를 추적하는 상태값입니다.
    private bool _isActionListenerRegistered;
    // 이 브리지가 Attack 사유 잠금을 현재 적용한 상태인지 추적합니다.
    private bool _isAttackLockApplied;
    // NetworkObject 누락 경고를 중복 출력하지 않기 위한 상태값입니다.
    private bool _hasLoggedMissingNetworkObject;
    // PlayerMovement 누락 경고를 중복 출력하지 않기 위한 상태값입니다.
    private bool _hasLoggedMissingPlayerMovement;

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
    /// 비활성화 시 ActionController 리스너를 해제하고 자신이 건 잠금을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        TryUnregisterActionListener();
        ForceReleaseAttackLock();
    }

    /// <summary>
    /// 매 프레임 현재 액션 런타임을 기준으로 잠금 상태를 재평가합니다.
    /// </summary>
    private void Update()
    {
        ReevaluateMovementLockFromRuntime();
    }

    /// <summary>
    /// 액션 시작 시 현재 액션 타입 기준 이동 잠금 상태를 재평가합니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
        ReevaluateMovementLockFromRuntime();
    }

    /// <summary>
    /// 액션 단계 변경 시 현재 액션 타입 기준 이동 잠금 상태를 재평가합니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
        ReevaluateMovementLockFromRuntime();
    }

    /// <summary>
    /// 액션 완료 시 현재 액션 타입 기준 이동 잠금 상태를 재평가합니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
        ReevaluateMovementLockFromRuntime();
    }

    /// <summary>
    /// 액션 취소 시 현재 액션 타입 기준 이동 잠금 상태를 재평가합니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
        ReevaluateMovementLockFromRuntime();
    }

    /// <summary>
    /// 현재 런타임 액션과 소유권 조건을 바탕으로 Attack 이동 잠금 적용 여부를 동기화합니다.
    /// </summary>
    private void ReevaluateMovementLockFromRuntime()
    {
        if (!TryResolveReferences())
        {
            ForceReleaseAttackLock();
            return;
        }

        if (!CanProcessLocalOwnerLogic())
        {
            ForceReleaseAttackLock();
            return;
        }

        ActionRuntime runtime = _actionController.Runtime; // 현재 액션 실행 상태 스냅샷입니다.
        bool shouldLock = runtime.IsRunning && ShouldLockMovementForAction(runtime.ActionType);

        if (shouldLock)
        {
            ApplyAttackLock(runtime.ActionType);
            return;
        }

        ForceReleaseAttackLock();
    }

    /// <summary>
    /// 지정한 액션 타입에 이동 잠금을 적용해야 하는지 규칙 프로필/기본 설정으로 판정합니다.
    /// </summary>
    private bool ShouldLockMovementForAction(E_ActionType actionType)
    {
        if (_actionController.TryGetActionRule(actionType, out ActionRuleData actionRule) && actionRule.OverrideMovementLockSetting)
        {
            return actionRule.LockMovementDuringAction;
        }

        if (!_lockAttackActionsByDefault)
        {
            return false;
        }

        for (int i = 0; i < _defaultLockActions.Length; i++)
        {
            if (_defaultLockActions[i] == actionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Attack 사유 이동 잠금을 적용하고 필요한 경우 수평 속도를 정지합니다.
    /// </summary>
    private void ApplyAttackLock(E_ActionType actionType)
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

        if (_isAttackLockApplied)
        {
            return;
        }

        _playerMovement.AddMovementLock(E_MovementLockReason.Attack, _clearHorizontalVelocityOnLock);
        _isAttackLockApplied = true;

        if (_enableRuntimeLog)
        {
            Debug.Log($"[ActionMovementLockBridge] Movement lock applied. action={actionType}, object={name}");
        }
    }

    /// <summary>
    /// 이 브리지가 추가한 Attack 사유 이동 잠금을 안전하게 해제합니다.
    /// </summary>
    private void ForceReleaseAttackLock()
    {
        if (!_isAttackLockApplied)
        {
            return;
        }

        if (_playerMovement != null)
        {
            _playerMovement.RemoveMovementLock(E_MovementLockReason.Attack);
        }

        _isAttackLockApplied = false;
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
    /// 네트워크 소유권 기준으로 현재 인스턴스가 로컬 입력 처리 대상인지 판정합니다.
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
    /// NetworkObject가 스폰되지 않은 상황이 오프라인 싱글플레이 경로인지 판정합니다.
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
