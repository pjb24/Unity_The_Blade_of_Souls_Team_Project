using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 공격 입력 시점의 이동 맥락을 평가해 상황별 공격 액션을 요청하는 컨트롤러입니다.
/// </summary>
public class ActionContextualAttackController : MonoBehaviour, IActionListener
{
    [System.Serializable]
    private struct AttackInputRouteBinding
    {
        public string RouteName; // 인스펙터와 로그에서 입력 라우트를 구분하기 위한 표시 이름
        public bool Enabled; // 해당 입력 라우트 활성화 여부
        public InputActionReference InputAction; // 라우트별 공격 입력 액션 참조
        public AttackContextRuleProfile RuleProfile; // 라우트별 공격 규칙 프로필 참조
    }

    [Header("References")]
    [SerializeField] private ActionController _actionController; // 공격 액션 요청을 전달할 액션 컨트롤러 참조
    [SerializeField] private PlayerMovement _playerMovement; // 공격 맥락 판정에 사용할 이동 상태 제공자 참조

    [Header("Attack Input Routes")]
    [SerializeField] private AttackInputRouteBinding[] _attackInputRoutes = new AttackInputRouteBinding[0]; // 입력 종류별 공격 라우팅 구성을 담는 배열

    [Header("Runtime Options")]
    [SerializeField] private bool _enableRuntimeLog = false; // 공격 규칙 선택/차단 로그를 출력할지 여부
    [SerializeField] private float _listenerRetryInterval = 0.1f; // ActionController 리스너 지연 등록 재시도 간격(초)
    [SerializeField] private int _listenerMaxRetryCount = 30; // ActionController 리스너 지연 등록 최대 재시도 횟수

    private readonly List<AttackInputRouteBinding> _runtimeRoutes = new List<AttackInputRouteBinding>(); // 런타임에서 유효성 검사를 통과한 입력 라우트 목록
    private readonly Dictionary<InputAction, int> _routeIndexByInputAction = new Dictionary<InputAction, int>(); // 콜백으로 들어온 InputAction을 라우트 인덱스로 역조회하기 위한 맵

    private Coroutine _registerActionListenerCoroutine; // ActionController 리스너 지연 등록 코루틴 핸들
    private bool _isActionListenerRegistered; // ActionController 리스너가 현재 등록된 상태인지 여부
    private bool _hasBufferedAttackInput; // 현재 버퍼링된 공격 입력이 존재하는지 여부
    private float _attackInputBufferTimer; // 버퍼링된 공격 입력의 남은 유효 시간
    private string _bufferedRouteName; // 현재 버퍼 입력이 속한 라우트 이름
    private AttackContextRuleProfile _bufferedRuleProfile; // 현재 버퍼 입력에 적용할 규칙 프로필

    /// <summary>
    /// 활성화 시 입력/리스너 구독을 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        RegisterInputAction();
        RestartActionListenerRegisterCoroutine();
    }

    /// <summary>
    /// 비활성화 시 입력/리스너 구독을 해제하고 버퍼 상태를 초기화합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerActionListenerCoroutine);
        TryImmediateUnregisterActionListenerOnDisable();
        UnregisterInputAction();
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
    /// 공격 입력 발생 시 입력 액션에 매핑된 라우트로 공격 액션 요청을 시도합니다.
    /// </summary>
    private void OnAttackInputPerformed(InputAction.CallbackContext context)
    {
        if (!_routeIndexByInputAction.TryGetValue(context.action, out int routeIndex))
        {
            return;
        }

        if (routeIndex < 0 || routeIndex >= _runtimeRoutes.Count)
        {
            return;
        }

        AttackInputRouteBinding route = _runtimeRoutes[routeIndex]; // 현재 입력에 매핑된 라우트 설정 스냅샷
        string routeName = string.IsNullOrWhiteSpace(route.RouteName) ? "UnnamedRoute" : route.RouteName; // 로그/버퍼 키로 사용할 라우트 이름
        TryHandleAttackInput("InputPerformed", routeName, route.RuleProfile);
    }

    /// <summary>
    /// 입력 시점의 맥락 규칙을 평가해 공격 요청/차단/버퍼링을 처리합니다.
    /// </summary>
    private void TryHandleAttackInput(string source, string routeName, AttackContextRuleProfile ruleProfile)
    {
        if (!TryResolveReferences())
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

        if (InputManager.Movement.sqrMagnitude > 0.01f)
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
    /// 활성화된 입력 라우트를 구성해 런타임 리스트를 갱신합니다.
    /// </summary>
    private void BuildRuntimeRoutes()
    {
        _runtimeRoutes.Clear();

        for (int i = 0; i < _attackInputRoutes.Length; i++)
        {
            AttackInputRouteBinding route = _attackInputRoutes[i];
            if (!route.Enabled)
            {
                continue;
            }

            if (route.InputAction == null || route.InputAction.action == null)
            {
                continue;
            }

            if (route.RuleProfile == null)
            {
                continue;
            }

            _runtimeRoutes.Add(route);
        }

        // 레거시 단일 입력 폴백 없이 _attackInputRoutes 기반 구성만 허용합니다.
    }

    /// <summary>
    /// 입력 액션 구독을 등록합니다.
    /// </summary>
    private void RegisterInputAction()
    {
        if (!TryResolveReferences())
        {
            return;
        }

        _routeIndexByInputAction.Clear();

        for (int i = 0; i < _runtimeRoutes.Count; i++)
        {
            AttackInputRouteBinding route = _runtimeRoutes[i];
            InputAction inputAction = route.InputAction.action; // 라우트에 매핑된 실제 입력 액션 인스턴스

            if (_routeIndexByInputAction.ContainsKey(inputAction))
            {
                Debug.LogWarning($"[ActionContextualAttackController] Duplicate InputAction binding ignored: route={route.RouteName}");
                continue;
            }

            inputAction.performed += OnAttackInputPerformed;
            inputAction.Enable();
            _routeIndexByInputAction[inputAction] = i;
        }

        if (_routeIndexByInputAction.Count == 0)
        {
            Debug.LogWarning("[ActionContextualAttackController] No valid attack input routes are configured.");
        }
    }

    /// <summary>
    /// 입력 액션 구독을 해제합니다.
    /// </summary>
    private void UnregisterInputAction()
    {
        if (_routeIndexByInputAction.Count == 0)
        {
            return;
        }

        List<InputAction> keys = new List<InputAction>(_routeIndexByInputAction.Keys); // 구독 해제 대상 입력 액션 키 목록 스냅샷
        for (int i = 0; i < keys.Count; i++)
        {
            InputAction inputAction = keys[i];
            inputAction.performed -= OnAttackInputPerformed;
            inputAction.Disable();
        }

        _routeIndexByInputAction.Clear();
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
    /// 참조가 비어 있을 때 동일 오브젝트 기준으로 자동 보정을 시도합니다.
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

        BuildRuntimeRoutes();

        if (_runtimeRoutes.Count == 0)
        {
            Debug.LogWarning($"[ActionContextualAttackController] No usable attack route/profile pair is configured on {name}.");
            return false;
        }

        // 입력 구독은 PlayerMovement.Controller 초기화 시점과 분리해 유지하고,
        // 실제 맥락 판정에서 Controller null 여부를 안전하게 처리합니다.
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
