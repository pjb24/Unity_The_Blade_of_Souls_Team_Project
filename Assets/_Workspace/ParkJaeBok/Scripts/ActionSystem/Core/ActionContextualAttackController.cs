using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 공격 입력 시점의 이동 맥락을 평가해 상황별 공격 액션을 요청하는 컨트롤러입니다.
/// </summary>
public class ActionContextualAttackController : MonoBehaviour, IActionListener
{
    [Header("References")]
    [Tooltip("공격 액션 요청을 전달할 ActionController 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private ActionController _actionController; // 공격 액션 요청을 전달할 액션 컨트롤러 참조
    [Tooltip("공격 맥락 플래그 계산에 사용할 PlayerMovement 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 공격 맥락 판정에 사용할 이동 상태 제공자 참조
    [Tooltip("InputManager Attack 입력에 매핑해 사용할 기본 공격 규칙 프로필입니다.")]
    [SerializeField] private AttackContextRuleProfile _defaultAttackRuleProfile; // InputManager Attack 입력 처리에 사용할 기본 공격 규칙 프로필입니다.

    [Header("Runtime Options")]
    [Tooltip("공격 규칙 선택/차단 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _enableRuntimeLog = false; // 공격 규칙 선택/차단 로그를 출력할지 여부
    [Tooltip("ActionController 리스너 지연 등록 재시도 간격(초)입니다.")]
    [SerializeField] private float _listenerRetryInterval = 0.1f; // ActionController 리스너 지연 등록 재시도 간격(초)
    [Tooltip("ActionController 리스너 지연 등록 최대 재시도 횟수입니다.")]
    [SerializeField] private int _listenerMaxRetryCount = 30; // ActionController 리스너 지연 등록 최대 재시도 횟수
    [Tooltip("NetworkObject를 찾지 못해 소유권 검증 없이 동작할 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenOwnershipUnavailable = true; // 네트워크 오브젝트 소유권을 확인할 수 없을 때 경고 로그 출력 여부
    [Tooltip("InputManager 공격 입력 라우트 이름입니다. 로그와 디버깅 용도로 사용됩니다.")]
    [SerializeField] private string _inputManagerAttackRouteName = "InputManagerAttack"; // InputManager 공격 입력 라우트 표시 이름입니다.
    [Tooltip("싱글플레이/멀티플레이 로컬 실행 경로 판정 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _enableOwnerRouteLog = false; // 로컬 실행 권한 판정(싱글/멀티) 로그 출력 여부입니다.

    private Coroutine _registerActionListenerCoroutine; // ActionController 리스너 지연 등록 코루틴 핸들
    private bool _isActionListenerRegistered; // ActionController 리스너가 현재 등록된 상태인지 여부
    private bool _hasBufferedAttackInput; // 현재 버퍼링된 공격 입력이 존재하는지 여부
    private float _attackInputBufferTimer; // 버퍼링된 공격 입력의 남은 유효 시간
    private string _bufferedRouteName; // 현재 버퍼 입력이 속한 라우트 이름
    private AttackContextRuleProfile _bufferedRuleProfile; // 현재 버퍼 입력에 적용할 규칙 프로필
    [SerializeField] private NetworkObject _networkObject; // 소유권 기반 입력 처리 판정을 위한 NetworkObject 참조

    /// <summary>
    /// 활성화 시 입력/리스너 구독을 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        EnsureNetworkOwnershipReference();
        RestartActionListenerRegisterCoroutine();
    }

    /// <summary>
    /// 비활성화 시 입력/리스너 구독을 해제하고 버퍼 상태를 초기화합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerActionListenerCoroutine);
        TryImmediateUnregisterActionListenerOnDisable();
        ResetBufferedInput();
    }

    /// <summary>
    /// 파괴 시 실행 중 코루틴을 정리하고 리스너 해제를 마지막으로 시도합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _registerActionListenerCoroutine);

        if (_isActionListenerRegistered && _actionController != null)
        {
            _actionController.RemoveListener(this);
            _isActionListenerRegistered = false;
        }
    }

    /// <summary>
    /// 매 프레임 버퍼 입력 만료와 재시도 처리를 수행합니다.
    /// </summary>
    private void Update()
    {
        if (!CanProcessLocalOwnerLogic())
        {
            ResetBufferedInput();
            return;
        }

        if (InputManager.AttackWasPressed)
        {
            string routeName = string.IsNullOrWhiteSpace(_inputManagerAttackRouteName) ? "InputManagerAttack" : _inputManagerAttackRouteName; // 로그와 버퍼 식별에 사용할 입력 라우트 이름입니다.
            TryHandleAttackInput("InputManager", routeName, _defaultAttackRuleProfile);
        }

        if (!_hasBufferedAttackInput)
        {
            return;
        }

        _attackInputBufferTimer -= Time.deltaTime;
        if (_attackInputBufferTimer <= 0f)
        {
            ResetBufferedInput();
            return;
        }

        TryHandleAttackInput("BufferedInput", _bufferedRouteName, _bufferedRuleProfile);
    }

    /// <summary>
    /// 입력 시점의 맥락 규칙을 평가해 공격 요청/차단/버퍼링을 처리합니다.
    /// </summary>
    private void TryHandleAttackInput(string source, string routeName, AttackContextRuleProfile ruleProfile)
    {
        if (!TryResolveCoreReferences())
        {
            return;
        }

        if (ruleProfile == null)
        {
            Debug.LogWarning($"[ActionContextualAttackController] RuleProfile is null. route={routeName}, source={source}");
            return;
        }

        ActionRuntime runtime = _actionController.Runtime; // 입력 시점 액션 런타임 스냅샷
        E_AttackContextFlags contextFlags = BuildAttackContextFlags(); // 입력 시점 이동 맥락 플래그 스냅샷

        if (!TryResolveRule(ruleProfile, runtime.ActionType, contextFlags, out AttackContextRuleData rule))
        {
            E_ActionType fallbackActionType = ruleProfile.FallbackActionType; // 규칙 미일치 시 사용할 기본 공격 액션
            TryRequestOrBuffer(fallbackActionType, true, source, "Fallback", routeName, ruleProfile);
            return;
        }

        if (rule.BlockAttack)
        {
            if (_enableRuntimeLog)
            {
                Debug.Log($"[ActionContextualAttackController] Attack blocked by rule={rule.Name}, route={routeName}, source={source}, context={contextFlags}");
            }

            if (rule.BufferWhenBlocked)
            {
                BufferAttackInput(ruleProfile, routeName);
            }
            else
            {
                ResetBufferedInput();
            }

            return;
        }

        TryRequestOrBuffer(rule.OutputActionType, rule.BufferWhenBlocked, source, rule.Name, routeName, ruleProfile);
    }

    /// <summary>
    /// 액션 요청 결과에 따라 입력 버퍼를 유지하거나 해제합니다.
    /// </summary>
    private void TryRequestOrBuffer(E_ActionType actionType, bool allowBufferWhenDenied, string source, string ruleName, string routeName, AttackContextRuleProfile ruleProfile)
    {
        if (actionType == E_ActionType.None)
        {
            if (_enableRuntimeLog)
            {
                Debug.LogWarning($"[ActionContextualAttackController] Rule={ruleName} resolved to None. route={routeName}, source={source}");
            }

            ResetBufferedInput();
            return;
        }

        bool requestAccepted = _actionController.RequestAction(actionType); // 상황별 공격 액션 요청 수락 여부
        if (requestAccepted)
        {
            if (_enableRuntimeLog)
            {
                Debug.Log($"[ActionContextualAttackController] Attack action requested: {actionType}, rule={ruleName}, route={routeName}, source={source}");
            }

            ResetBufferedInput();
            return;
        }

        if (allowBufferWhenDenied)
        {
            BufferAttackInput(ruleProfile, routeName);
        }
        else
        {
            ResetBufferedInput();
        }
    }

    /// <summary>
    /// 현재 이동/물리 상태를 공격 규칙 평가용 비트 플래그로 변환합니다.
    /// </summary>
    private E_AttackContextFlags BuildAttackContextFlags()
    {
        E_AttackContextFlags flags = E_AttackContextFlags.None;

        bool isGrounded = _playerMovement.Controller != null && _playerMovement.Controller.IsGrounded(); // 지면 접촉 상태
        if (isGrounded)
        {
            flags |= E_AttackContextFlags.Grounded;
        }
        else
        {
            flags |= E_AttackContextFlags.Airborne;
        }

        if (_playerMovement.Velocity.sqrMagnitude > 0.01f)
        {
            flags |= E_AttackContextFlags.Moving;
        }

        if (_playerMovement.IsJumpingState)
        {
            flags |= E_AttackContextFlags.Jumping;
        }

        if (_playerMovement.IsFallingState)
        {
            flags |= E_AttackContextFlags.Falling;
        }

        if (_playerMovement.IsDashing)
        {
            flags |= E_AttackContextFlags.Dashing;
        }

        if (_playerMovement.IsWallSlidingState)
        {
            flags |= E_AttackContextFlags.WallSliding;
        }

        if (_playerMovement.IsWallJumpingState)
        {
            flags |= E_AttackContextFlags.WallJumping;
        }

        if (_playerMovement.Controller != null && _playerMovement.Controller.IsSliding)
        {
            flags |= E_AttackContextFlags.Sliding;
        }

        return flags;
    }

    /// <summary>
    /// NetworkObject 참조를 캐시하고 누락 시 경고를 출력합니다.
    /// </summary>
    private void EnsureNetworkOwnershipReference()
    {
        if (_networkObject != null)
        {
            return;
        }

        _networkObject = GetComponent<NetworkObject>();
        if (_networkObject == null && _warnWhenOwnershipUnavailable)
        {
            Debug.LogWarning($"[ActionContextualAttackController] NetworkObject가 없어 소유권 검증 없이 동작합니다. object={name}");
        }
    }

    private bool CanProcessLocalOwnerLogic()
    {
        EnsureNetworkOwnershipReference();

        if (_networkObject == null)
        {
            if (_enableOwnerRouteLog)
            {
                Debug.Log($"[ActionContextualAttackController] Local attack route: network object missing -> treat as single/local. object={name}");
            }

            return true;
        }

        if (!_networkObject.IsSpawned)
        {
            if (!IsOfflineSingleRoute())
            {
                if (_enableOwnerRouteLog)
                {
                    Debug.Log($"[ActionContextualAttackController] Local attack route blocked: waiting network spawn. object={name}");
                }

                return false;
            }

            if (_enableOwnerRouteLog)
            {
                Debug.Log($"[ActionContextualAttackController] Local attack route: offline single fallback (network unspawned). object={name}");
            }

            return true;
        }

        if (_enableOwnerRouteLog)
        {
            Debug.Log($"[ActionContextualAttackController] Local attack route: network owner={_networkObject.IsOwner}. object={name}");
        }

        return _networkObject.IsOwner;
    }

    /// <summary>
    /// 네트워크 오브젝트가 스폰되지 않은 상황이 오프라인 싱글플레이인지 판정합니다.
    /// </summary>
    private bool IsOfflineSingleRoute()
    {
        if (NetworkManager.Singleton == null)
        {
            if (_warnWhenOwnershipUnavailable)
            {
                Debug.LogWarning($"[ActionContextualAttackController] NetworkManager.Singleton is null. Fallback to offline local attack route. object={name}");
            }

            return true;
        }

        return !NetworkManager.Singleton.IsListening;
    }

    /// <summary>
    /// 현재 액션과 맥락 플래그에 맞는 최우선 규칙을 탐색합니다.
    /// </summary>
    private bool TryResolveRule(AttackContextRuleProfile ruleProfile, E_ActionType currentActionType, E_AttackContextFlags contextFlags, out AttackContextRuleData selectedRule)
    {
        selectedRule = default;
        AttackContextRuleData[] rules = ruleProfile.Rules; // 현재 공격 맥락 평가에 사용할 규칙 배열
        if (rules == null || rules.Length == 0)
        {
            return false;
        }

        int bestPriority = int.MinValue; // 현재까지 선택된 규칙의 우선순위
        bool hasRule = false;

        for (int i = 0; i < rules.Length; i++)
        {
            AttackContextRuleData candidate = rules[i];
            if (!candidate.Enabled)
            {
                continue;
            }

            if (!MatchesRequiredFlags(candidate.RequiredFlags, contextFlags))
            {
                continue;
            }

            if (HasAnyFlag(candidate.ExcludedFlags, contextFlags))
            {
                continue;
            }

            if (!IsCurrentActionAllowed(candidate.AllowedCurrentActions, currentActionType))
            {
                continue;
            }

            if (candidate.RequireComboWindowOpen && !_actionController.IsComboInputWindowOpen)
            {
                continue;
            }

            if (candidate.RequireHitWindowOpen && !_actionController.IsHitWindowOpen)
            {
                continue;
            }

            if (!hasRule || candidate.Priority > bestPriority)
            {
                selectedRule = candidate;
                bestPriority = candidate.Priority;
                hasRule = true;
            }
        }

        return hasRule;
    }

    /// <summary>
    /// required 플래그가 현재 맥락 플래그에 모두 포함되는지 판정합니다.
    /// </summary>
    private bool MatchesRequiredFlags(E_AttackContextFlags requiredFlags, E_AttackContextFlags contextFlags)
    {
        if (requiredFlags == E_AttackContextFlags.None)
        {
            return true;
        }

        return (contextFlags & requiredFlags) == requiredFlags;
    }

    /// <summary>
    /// 플래그 교집합이 하나라도 존재하는지 판정합니다.
    /// </summary>
    private bool HasAnyFlag(E_AttackContextFlags lhs, E_AttackContextFlags rhs)
    {
        return (lhs & rhs) != 0;
    }

    /// <summary>
    /// 현재 실행 액션이 규칙의 허용 액션 목록에 포함되는지 판정합니다.
    /// </summary>
    private bool IsCurrentActionAllowed(E_ActionType[] allowedActions, E_ActionType currentActionType)
    {
        if (allowedActions == null || allowedActions.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < allowedActions.Length; i++)
        {
            if (allowedActions[i] == currentActionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 입력 버퍼를 활성화하고 유효 시간을 초기화합니다.
    /// </summary>
    private void BufferAttackInput(AttackContextRuleProfile ruleProfile, string routeName)
    {
        _hasBufferedAttackInput = true;
        _bufferedRuleProfile = ruleProfile;
        _bufferedRouteName = routeName;
        float bufferSeconds = Mathf.Max(0f, ruleProfile.AttackInputBufferSeconds); // 설정값 보정이 적용된 버퍼 유지 시간
        _attackInputBufferTimer = bufferSeconds;
    }

    /// <summary>
    /// 입력 버퍼 상태를 초기화합니다.
    /// </summary>
    private void ResetBufferedInput()
    {
        _hasBufferedAttackInput = false;
        _attackInputBufferTimer = 0f;
        _bufferedRouteName = string.Empty;
        _bufferedRuleProfile = null;
    }

    /// <summary>
    /// ActionController 리스너 등록 코루틴을 재시작합니다.
    /// </summary>
    private void RestartActionListenerRegisterCoroutine()
    {
        StopRunningCoroutine(ref _registerActionListenerCoroutine);
        _registerActionListenerCoroutine = StartCoroutine(RegisterActionListenerWithRetryCoroutine());
    }

    /// <summary>
    /// 비활성화 시점에 코루틴 없이 안전하게 ActionController 리스너 해제를 시도합니다.
    /// </summary>
    private void TryImmediateUnregisterActionListenerOnDisable()
    {
        if (TryResolveActionControllerReference())
        {
            if (_isActionListenerRegistered)
            {
                _actionController.RemoveListener(this);
                _isActionListenerRegistered = false;
            }

            return;
        }

        Debug.LogWarning($"[ActionContextualAttackController] OnDisable could not resolve ActionController on {name}. RemoveListener skipped.");
    }

    /// <summary>
    /// ActionController가 준비될 때까지 재시도한 뒤 리스너를 등록합니다.
    /// </summary>
    private IEnumerator RegisterActionListenerWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _listenerMaxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값
        float safeInterval = Mathf.Max(0.01f, _listenerRetryInterval); // 잘못된 재시도 간격을 보정한 안전 값

        if (_listenerMaxRetryCount < 1 || _listenerRetryInterval <= 0f)
        {
            Debug.LogWarning($"[ActionContextualAttackController] Invalid listener retry settings on {name}. Fallback maxRetry={safeMaxRetry}, interval={safeInterval}.");
        }

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            if (TryResolveActionControllerReference())
            {
                if (!_isActionListenerRegistered)
                {
                    _actionController.AddListener(this);
                    _isActionListenerRegistered = true;
                }

                _registerActionListenerCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[ActionContextualAttackController] ActionController is null on {name}. Delaying AddListener registration.");
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[ActionContextualAttackController] AddListener registration failed after retries on {name}.");
        _registerActionListenerCoroutine = null;
    }

    /// <summary>
    /// 현재 오브젝트 기준으로 ActionController 참조를 보정합니다.
    /// </summary>
    private bool TryResolveActionControllerReference()
    {
        if (_actionController != null)
        {
            return true;
        }

        _actionController = GetComponent<ActionController>();
        if (_actionController != null)
        {
            Debug.LogWarning($"[ActionContextualAttackController] _actionController was null on {name}. Fallback to same GameObject ActionController.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 실행 중인 코루틴을 안전하게 중지하고 참조를 정리합니다.
    /// </summary>
    private void StopRunningCoroutine(ref Coroutine coroutineHandle)
    {
        if (coroutineHandle == null)
        {
            return;
        }

        StopCoroutine(coroutineHandle);
        coroutineHandle = null;
    }

    /// <summary>
    /// 공격 입력 처리에 필요한 핵심 참조(ActionController/PlayerMovement)를 보정합니다.
    /// </summary>
    private bool TryResolveCoreReferences()
    {
        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
        }

        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }

        return _actionController != null && _playerMovement != null;
    }

    /// <summary>
    /// 액션 시작 콜백에서 현재 구현은 별도 처리를 수행하지 않습니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
    }

    /// <summary>
    /// 액션 단계 변경 콜백에서 현재 구현은 별도 처리를 수행하지 않습니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
    }

    /// <summary>
    /// 액션 완료 시 버퍼 입력이 남아 있으면 즉시 재시도합니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
        if (_hasBufferedAttackInput)
        {
            TryHandleAttackInput("ActionCompleted", _bufferedRouteName, _bufferedRuleProfile);
        }
    }

    /// <summary>
    /// 액션 취소 시 버퍼 입력이 남아 있으면 즉시 재시도합니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
        if (_hasBufferedAttackInput)
        {
            TryHandleAttackInput("ActionCancelled", _bufferedRouteName, _bufferedRuleProfile);
        }
    }
}
