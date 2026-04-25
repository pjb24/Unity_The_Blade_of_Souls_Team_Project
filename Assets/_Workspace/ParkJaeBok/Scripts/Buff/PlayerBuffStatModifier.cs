using UnityEngine;

/// <summary>
/// Buff 상태에 따라 공격력/이동속도/공격속도(Animator speed)를 보정하는 컴포넌트입니다.
/// </summary>
public class PlayerBuffStatModifier : MonoBehaviour, IAttackDamageModifierProvider, IActionListener
{
    [Header("Dependencies")]
    [Tooltip("Buff 수치를 읽어올 설정 ScriptableObject입니다.")]
    [SerializeField] private BuffConfigSO _buffConfig; // Buff 수치 설정 참조입니다.

    [Tooltip("이동 속도 배율을 전달할 PlayerMovement 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 이동 속도 배율 적용 대상 PlayerMovement 참조입니다.

    [Tooltip("공격속도(Animator speed) 배율을 적용할 Animator 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private Animator _animator; // 공격속도 배율 적용 대상 Animator 참조입니다.

    [Tooltip("액션 시작/종료 시 공격 애니메이션 배율 적용을 위해 구독할 ActionController 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private ActionController _actionController; // 액션 이벤트 구독을 담당할 ActionController 참조입니다.

    [Header("Runtime")]
    [Tooltip("디버그용: 현재 Buff 스탯 보정 적용 여부입니다.")]
    [SerializeField] private bool _isBuffApplied; // 현재 Buff 보정 적용 여부입니다.

    [Tooltip("디버그용: Animator speed 원본 캐시값입니다.")]
    [SerializeField] private float _cachedAnimatorSpeed = 1f; // Buff 해제 시 복구할 Animator speed 원본값입니다.

    [Tooltip("ActionController 리스너 등록 재시도 간격(초)입니다.")]
    [SerializeField] private float _listenerRetryInterval = 0.1f; // ActionController 리스너 등록 재시도 간격입니다.

    [Tooltip("ActionController 리스너 등록 최대 재시도 횟수입니다.")]
    [SerializeField] private int _listenerMaxRetryCount = 30; // ActionController 리스너 등록 최대 재시도 횟수입니다.

    private bool _hasAnimatorSpeedCache; // Animator speed 원본값을 캐시했는지 여부입니다.
    private bool _isActionListenerRegistered; // ActionController 리스너 등록 여부입니다.
    private Coroutine _registerListenerCoroutine; // ActionController 리스너 지연 등록 코루틴 핸들입니다.

    /// <summary>
    /// 의존성 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
            if (_playerMovement == null)
            {
                _playerMovement = GetComponentInParent<PlayerMovement>();
            }
        }

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null)
            {
                _animator = GetComponentInParent<Animator>();
            }

            if (_animator == null)
            {
                Transform root = transform.root; // 플레이어 루트 기준 Animator 탐색에 사용할 루트 트랜스폼입니다.
                if (root != null)
                {
                    _animator = root.GetComponentInChildren<Animator>(true);
                }
            }
        }

        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
            if (_actionController == null)
            {
                _actionController = GetComponentInParent<ActionController>();
            }
        }
    }

    /// <summary>
    /// 활성화 시 ActionController 리스너 등록을 시도합니다.
    /// </summary>
    private void OnEnable()
    {
        RestartRegisterListenerCoroutine();
    }

    /// <summary>
    /// 비활성화/파괴 시 원본 스탯 복구와 리스너 해제를 보장합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerListenerCoroutine);
        TryUnregisterActionListener();
        SetBuffApplied(false);
    }

    /// <summary>
    /// Buff 보정 적용/해제를 수행합니다.
    /// </summary>
    public void SetBuffApplied(bool isApplied)
    {
        if (_isBuffApplied == isApplied)
        {
            return;
        }

        _isBuffApplied = isApplied;

        ApplyMoveSpeedModifier();
        if (!_isBuffApplied)
        {
            RestoreAnimatorSpeed();
        }
    }

    /// <summary>
    /// 현재 Buff 상태와 설정값을 반영해 공격 데미지를 보정합니다.
    /// </summary>
    public float ModifyDamage(float baseDamage, AttackSpec attackSpec, E_ActionType actionType)
    {
        if (!_isBuffApplied || _buffConfig == null)
        {
            return baseDamage;
        }

        float multipliedDamage = baseDamage * Mathf.Max(0f, _buffConfig.AttackDamageMultiplier); // 공격력 배율 적용 결과입니다.
        float finalDamage = multipliedDamage + _buffConfig.AttackDamageAdditive; // 공격력 추가값 적용 결과입니다.
        return Mathf.Max(0f, finalDamage);
    }

    /// <summary>
    /// 이동 속도 배율을 PlayerMovement에 반영합니다.
    /// </summary>
    private void ApplyMoveSpeedModifier()
    {
        if (_playerMovement == null)
        {
            return;
        }

        if (_isBuffApplied && _buffConfig != null)
        {
            _playerMovement.SetExternalMoveSpeedMultiplier(_buffConfig.MoveSpeedMultiplier);
            return;
        }

        _playerMovement.SetExternalMoveSpeedMultiplier(1f);
    }

    /// <summary>
    /// Animator speed 배율을 적용/복구합니다.
    /// </summary>
    private void ApplyAnimatorSpeedModifier()
    {
        if (_animator == null)
        {
            return;
        }

        if (_isBuffApplied)
        {
            if (!_hasAnimatorSpeedCache)
            {
                _cachedAnimatorSpeed = _animator.speed;
                _hasAnimatorSpeedCache = true;
            }

            float speedMultiplier = _buffConfig != null ? Mathf.Max(0f, _buffConfig.AnimationAttackSpeedMultiplier) : 1f; // Buff 공격속도 배율입니다.
            _animator.speed = _cachedAnimatorSpeed * speedMultiplier;
            return;
        }

        RestoreAnimatorSpeed();
    }

    /// <summary>
    /// ActionController 리스너 등록 코루틴을 재시작합니다.
    /// </summary>
    private void RestartRegisterListenerCoroutine()
    {
        StopRunningCoroutine(ref _registerListenerCoroutine);
        _registerListenerCoroutine = StartCoroutine(RegisterListenerWhenReadyCoroutine());
    }

    /// <summary>
    /// ActionController 준비 완료까지 재시도 후 리스너를 등록합니다.
    /// </summary>
    private System.Collections.IEnumerator RegisterListenerWhenReadyCoroutine()
    {
        int safeRetryCount = Mathf.Max(1, _listenerMaxRetryCount); // 재시도 횟수 보정값입니다.
        float safeRetryInterval = Mathf.Max(0.01f, _listenerRetryInterval); // 재시도 간격 보정값입니다.

        for (int index = 0; index < safeRetryCount; index++)
        {
            if (TryResolveActionController())
            {
                TryRegisterActionListener();
                if (_isActionListenerRegistered)
                {
                    _registerListenerCoroutine = null;
                    yield break;
                }
            }

            yield return new WaitForSeconds(safeRetryInterval);
        }

        _registerListenerCoroutine = null;
    }

    /// <summary>
    /// ActionController 참조를 런타임에서 보정합니다.
    /// </summary>
    private bool TryResolveActionController()
    {
        if (_actionController != null)
        {
            return true;
        }

        _actionController = GetComponent<ActionController>();
        if (_actionController == null)
        {
            _actionController = GetComponentInParent<ActionController>();
        }

        return _actionController != null;
    }

    /// <summary>
    /// ActionController에 리스너를 등록합니다.
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
    /// ActionController 리스너를 해제합니다.
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
    /// Animator speed를 원본 값으로 복구합니다.
    /// </summary>
    private void RestoreAnimatorSpeed()
    {
        if (_animator == null || !_hasAnimatorSpeedCache)
        {
            return;
        }

        _animator.speed = _cachedAnimatorSpeed;
        _hasAnimatorSpeedCache = false;
    }

    /// <summary>
    /// 액션 시작 이벤트를 수신해 공격 계열 액션에서만 Animator speed 배율을 적용합니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
        if (!_isBuffApplied)
        {
            RestoreAnimatorSpeed();
            return;
        }

        if (IsAttackAction(runtime.ActionType))
        {
            ApplyAnimatorSpeedModifier();
            return;
        }

        RestoreAnimatorSpeed();
    }

    /// <summary>
    /// 액션 단계 변경 이벤트는 현재 사용하지 않습니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
    }

    /// <summary>
    /// 액션 완료 시 Animator speed를 원복합니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
        RestoreAnimatorSpeed();
    }

    /// <summary>
    /// 액션 취소 시 Animator speed를 원복합니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
        RestoreAnimatorSpeed();
    }

    /// <summary>
    /// 공격 계열 액션인지 판정합니다.
    /// </summary>
    private bool IsAttackAction(E_ActionType actionType)
    {
        switch (actionType)
        {
            case E_ActionType.Attack:
            case E_ActionType.AttackCombo1:
            case E_ActionType.AttackCombo2:
            case E_ActionType.AttackCombo3:
            case E_ActionType.AttackAir:
            case E_ActionType.AttackDash:
            case E_ActionType.AttackWall:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 실행 중 코루틴을 안전하게 중지합니다.
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
}
