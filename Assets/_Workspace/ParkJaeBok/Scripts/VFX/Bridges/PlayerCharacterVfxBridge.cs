using UnityEngine;

/// <summary>
/// 플레이어 행동(버프/이동/점프/피격/공격) 이벤트를 수집해 요구된 VFX를 재생/정지하는 통합 브리지입니다.
/// </summary>
public class PlayerCharacterVfxBridge : MonoBehaviour, IActionListener, IHitListener
{
    [Header("Dependencies")]
    [Tooltip("행동 이벤트를 구독할 ActionController 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private ActionController _actionController; // 행동 이벤트를 구독할 ActionController 참조입니다.
    [Tooltip("이동/점프 상태를 조회할 PlayerMovement 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 이동/점프 상태를 조회할 PlayerMovement 참조입니다.
    [Tooltip("피격 결과 이벤트를 구독할 HitReceiver 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private HitReceiver _hitReceiver; // 피격 결과 이벤트를 구독할 HitReceiver 참조입니다.

    [Header("Spawn / Attach Points")]
    [Tooltip("혼안 버프 EyeEffect를 Attach할 눈 위치 기준 Transform입니다. 비어 있으면 본인 Transform을 사용합니다.")]
    [SerializeField] private Transform _eyeAttachPoint; // 혼안 버프 EyeEffect 부착 위치 기준 Transform입니다.
    [Tooltip("WalkDust/JumpDust를 생성할 발 위치 기준 Transform입니다. 비어 있으면 본인 Transform을 사용합니다.")]
    [SerializeField] private Transform _footSpawnPoint; // 이동/점프 먼지 생성 위치 기준 Transform입니다.
    [Tooltip("피격 VFX(HitEffect)를 생성할 기본 위치 기준 Transform입니다. 비어 있으면 본인 Transform을 사용합니다.")]
    [SerializeField] private Transform _hitSpawnPoint; // 피격 이펙트 기본 생성 위치 기준 Transform입니다.
    [Tooltip("SwordEffect를 Attach할 검 끝 위치 기준 Transform입니다. 비어 있으면 본인 Transform을 사용합니다.")]
    [SerializeField] private Transform _swordAttachPoint; // 검 궤적 이펙트 부착 위치 기준 Transform입니다.

    [Header("Effect Ids")]
    [Tooltip("혼안 버프 시작 시 부착할 EyeEffect 이펙트 ID입니다.")]
    [SerializeField] private E_EffectId _eyeEffectId = E_EffectId.EyeEffect; // 혼안 버프 시작 시 재생할 눈 이펙트 ID입니다.
    [Tooltip("지면 이동 중 주기적으로 생성할 WalkDust 이펙트 ID입니다.")]
    [SerializeField] private E_EffectId _walkDustEffectId = E_EffectId.WalkDust; // 지면 이동 중 생성할 먼지 이펙트 ID입니다.
    [Tooltip("점프 시작 순간에 생성할 JumpDust 이펙트 ID입니다.")]
    [SerializeField] private E_EffectId _jumpDustEffectId = E_EffectId.JumpDust; // 점프 시작 시 생성할 먼지 이펙트 ID입니다.
    [Tooltip("피격 수락 시 생성할 HitEffect 이펙트 ID입니다.")]
    [SerializeField] private E_EffectId _hitEffectId = E_EffectId.HitEffect; // 피격 시 생성할 이펙트 ID입니다.
    [Tooltip("공격 수행 중 Attach할 SwordEffect 이펙트 ID입니다.")]
    [SerializeField] private E_EffectId _swordEffectId = E_EffectId.SwordEffect; // 공격 시 부착할 검 궤적 이펙트 ID입니다.

    [Header("WalkDust")]
    [Tooltip("지면 이동 중 WalkDust 생성 간격(초)입니다.")]
    [SerializeField] private float _walkDustInterval = 0.12f; // WalkDust 반복 생성 간격(초)입니다.
    [Tooltip("WalkDust를 생성할 최소 수평 속도 절대값입니다.")]
    [SerializeField] private float _walkDustMinSpeed = 0.3f; // WalkDust 생성 판정 최소 수평 속도입니다.

    [Header("JumpDust")]
    [Tooltip("지면 이탈 시 점프 시작으로 판정할 최소 상승 속도입니다.")]
    [SerializeField] private float _jumpDustMinUpwardVelocity = 0.1f; // 점프 시작 판정 최소 상승 속도입니다.

    [Header("HitEffect")]
    [Tooltip("true면 HitRequest.HitPoint가 유효할 때 해당 좌표를 우선 사용합니다.")]
    [SerializeField] private bool _useHitPointForHitEffect = true; // 피격 VFX 생성 시 HitPoint 우선 사용 여부입니다.

    [Header("SwordEffect")]
    [Tooltip("SwordEffect를 시작/종료할 공격 액션 타입 목록입니다.")]
    [SerializeField] private E_ActionType[] _swordEffectActionTypes = { E_ActionType.Attack, E_ActionType.AttackCombo1, E_ActionType.AttackCombo2, E_ActionType.AttackCombo3, E_ActionType.AttackAir, E_ActionType.AttackDash, E_ActionType.AttackWall }; // 검 궤적 이펙트를 재생할 액션 타입 목록입니다.

    private EffectHandle _eyeEffectHandle; // 현재 부착 중인 EyeEffect 핸들입니다.
    private EffectHandle _swordEffectHandle; // 현재 부착 중인 SwordEffect 핸들입니다.
    private bool _isActionListenerRegistered; // ActionController 리스너 등록 여부입니다.
    private bool _isHitListenerRegistered; // HitReceiver 리스너 등록 여부입니다.
    private bool _wasGroundedLastFrame; // 직전 프레임 지면 접촉 상태입니다.
    private float _walkDustTimer; // WalkDust 반복 생성을 제어하는 누적 타이머입니다.

    /// <summary>
    /// 의존성 참조를 보정하고 초기 상태를 캐시합니다.
    /// </summary>
    private void Awake()
    {
        ValidateSettings();
        TryResolveDependencies();
        _wasGroundedLastFrame = IsGroundedNow();
    }

    /// <summary>
    /// 활성화 시 액션/피격 리스너를 등록하고 반복 타이머를 초기화합니다.
    /// </summary>
    private void OnEnable()
    {
        TryResolveDependencies();
        RegisterActionListener();
        RegisterHitListener();
        _walkDustTimer = 0f;
        _wasGroundedLastFrame = IsGroundedNow();
    }

    /// <summary>
    /// 비활성화 시 액션/피격 리스너를 해제하고 지속형 이펙트를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterActionListener();
        UnregisterHitListener();
        StopSwordEffect();
        StopEyeEffect();
    }

    /// <summary>
    /// 매 프레임 이동/점프 상태를 검사해 WalkDust와 JumpDust를 생성합니다.
    /// </summary>
    private void Update()
    {
        HandleWalkDust();
        HandleJumpDust();
    }

    /// <summary>
    /// 혼안 버프 시작 시 EyeEffect를 눈 위치에 Attach하여 재생합니다.
    /// </summary>
    public void OnEyeBuffStart()
    {
        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] EffectService가 없어 EyeEffect를 재생하지 못했습니다. target={name}", this);
            return;
        }

        StopEyeEffect();

        Transform attachTarget = _eyeAttachPoint != null ? _eyeAttachPoint : transform;

        EffectRequest request = new EffectRequest();
        request.EffectId = _eyeEffectId;
        request.PlayMode = E_EffectPlayMode.Attach;
        request.AttachTarget = attachTarget;
        request.Owner = gameObject;
        request.AutoReturnOverrideEnabled = true;
        request.AutoReturn = false;
        request.LifetimeOverride = 0f;
        request.LocalOffset = Vector3.zero;
        request.IgnoreDuplicateGuard = false;
        request.FacingDirection = E_EffectFacingDirection.UsePrefab;

        _eyeEffectHandle = EffectService.Instance.Play(request);
    }

    /// <summary>
    /// 혼안 버프 종료 시 현재 부착된 EyeEffect를 정지합니다.
    /// </summary>
    public void OnEyeBuffEnd()
    {
        StopEyeEffect();
    }

    /// <summary>
    /// 액션 시작 이벤트를 수신해 SwordEffect 대상 액션이면 검 궤적 이펙트를 시작합니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
        if (IsSwordAction(runtime.ActionType) == false)
        {
            return;
        }

        PlaySwordEffect();
    }

    /// <summary>
    /// 액션 단계 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
        // 단계 변경 자체에는 별도 처리 없이 시작/완료/취소 이벤트만 사용합니다.
    }

    /// <summary>
    /// 액션 완료 이벤트를 수신해 SwordEffect 대상 액션이면 검 궤적 이펙트를 정지합니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
        if (IsSwordAction(runtime.ActionType) == false)
        {
            return;
        }

        StopSwordEffect();
    }

    /// <summary>
    /// 액션 취소 이벤트를 수신해 SwordEffect 대상 액션이면 검 궤적 이펙트를 정지합니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
        if (IsSwordAction(runtime.ActionType) == false)
        {
            return;
        }

        StopSwordEffect();
    }

    /// <summary>
    /// 피격 처리 결과를 수신해 수락된 경우 HitEffect를 지정 위치에 생성합니다.
    /// </summary>
    public void OnHitResolved(HitRequest request, HitResult result)
    {
        if (result.IsAccepted == false)
        {
            return;
        }

        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] EffectService가 없어 HitEffect를 재생하지 못했습니다. target={name}", this);
            return;
        }

        Vector3 spawnPosition = ResolveHitSpawnPosition(request);
        EffectService.Instance.Play(_hitEffectId, spawnPosition);
    }

    /// <summary>
    /// 설정값의 최소 제약을 보정합니다.
    /// </summary>
    private void ValidateSettings()
    {
        _walkDustInterval = Mathf.Max(0.01f, _walkDustInterval);
        _walkDustMinSpeed = Mathf.Max(0f, _walkDustMinSpeed);
        _jumpDustMinUpwardVelocity = Mathf.Max(0f, _jumpDustMinUpwardVelocity);
    }

    /// <summary>
    /// 비어 있는 의존성 참조를 같은 오브젝트에서 자동 보정합니다.
    /// </summary>
    private void TryResolveDependencies()
    {
        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
        }

        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }

        if (_hitReceiver == null)
        {
            _hitReceiver = GetComponent<HitReceiver>();
        }
    }

    /// <summary>
    /// ActionController 리스너를 안전하게 등록합니다.
    /// </summary>
    private void RegisterActionListener()
    {
        if (_isActionListenerRegistered)
        {
            return;
        }

        if (_actionController == null)
        {
            return;
        }

        _actionController.AddListener(this);
        _isActionListenerRegistered = true;
    }

    /// <summary>
    /// ActionController 리스너를 안전하게 해제합니다.
    /// </summary>
    private void UnregisterActionListener()
    {
        if (_isActionListenerRegistered == false)
        {
            return;
        }

        if (_actionController != null)
        {
            _actionController.RemoveListener(this);
        }

        _isActionListenerRegistered = false;
    }

    /// <summary>
    /// HitReceiver 리스너를 안전하게 등록합니다.
    /// </summary>
    private void RegisterHitListener()
    {
        if (_isHitListenerRegistered)
        {
            return;
        }

        if (_hitReceiver == null)
        {
            return;
        }

        _hitReceiver.AddListener(this);
        _isHitListenerRegistered = true;
    }

    /// <summary>
    /// HitReceiver 리스너를 안전하게 해제합니다.
    /// </summary>
    private void UnregisterHitListener()
    {
        if (_isHitListenerRegistered == false)
        {
            return;
        }

        if (_hitReceiver != null)
        {
            _hitReceiver.RemoveListener(this);
        }

        _isHitListenerRegistered = false;
    }

    /// <summary>
    /// 지면 이동 상태에서 일정 간격으로 WalkDust를 생성합니다.
    /// </summary>
    private void HandleWalkDust()
    {
        if (EffectService.Instance == null || _playerMovement == null || _playerMovement.Controller == null)
        {
            return;
        }

        bool isGrounded = _playerMovement.Controller.IsGrounded();
        float horizontalSpeed = Mathf.Abs(_playerMovement.Velocity.x);

        if (isGrounded == false || horizontalSpeed < _walkDustMinSpeed)
        {
            _walkDustTimer = 0f;
            return;
        }

        _walkDustTimer += Time.deltaTime;
        if (_walkDustTimer < _walkDustInterval)
        {
            return;
        }

        _walkDustTimer = 0f;
        Vector3 spawnPosition = _footSpawnPoint != null ? _footSpawnPoint.position : transform.position;
        EffectService.Instance.Play(_walkDustEffectId, spawnPosition);
    }

    /// <summary>
    /// 지면 이탈 시 상승 속도를 확인해 점프 시작으로 판정되면 JumpDust를 1회 생성합니다.
    /// </summary>
    private void HandleJumpDust()
    {
        if (EffectService.Instance == null || _playerMovement == null || _playerMovement.Controller == null)
        {
            return;
        }

        bool isGrounded = _playerMovement.Controller.IsGrounded();
        float upwardVelocity = _playerMovement.Velocity.y;

        if (_wasGroundedLastFrame && isGrounded == false && upwardVelocity >= _jumpDustMinUpwardVelocity)
        {
            Vector3 spawnPosition = _footSpawnPoint != null ? _footSpawnPoint.position : transform.position;
            EffectService.Instance.Play(_jumpDustEffectId, spawnPosition);
        }

        _wasGroundedLastFrame = isGrounded;
    }

    /// <summary>
    /// SwordEffect 대상 액션인지 설정 목록 기준으로 판정합니다.
    /// </summary>
    private bool IsSwordAction(E_ActionType actionType)
    {
        if (_swordEffectActionTypes == null || _swordEffectActionTypes.Length == 0)
        {
            return actionType == E_ActionType.Attack;
        }

        for (int index = 0; index < _swordEffectActionTypes.Length; index++)
        {
            if (_swordEffectActionTypes[index] == actionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// SwordEffect를 검 끝 위치에 Attach 모드로 시작합니다.
    /// </summary>
    private void PlaySwordEffect()
    {
        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] EffectService가 없어 SwordEffect를 재생하지 못했습니다. target={name}", this);
            return;
        }

        StopSwordEffect();

        Transform attachTarget = _swordAttachPoint != null ? _swordAttachPoint : transform;

        EffectRequest request = new EffectRequest();
        request.EffectId = _swordEffectId;
        request.PlayMode = E_EffectPlayMode.Attach;
        request.AttachTarget = attachTarget;
        request.Owner = gameObject;
        request.AutoReturnOverrideEnabled = true;
        request.AutoReturn = false;
        request.LifetimeOverride = 0f;
        request.LocalOffset = Vector3.zero;
        request.IgnoreDuplicateGuard = false;
        request.FacingDirection = E_EffectFacingDirection.UsePrefab;

        _swordEffectHandle = EffectService.Instance.Play(request);
    }

    /// <summary>
    /// 현재 재생 중인 SwordEffect를 정지합니다.
    /// </summary>
    private void StopSwordEffect()
    {
        if (_swordEffectHandle == null || _swordEffectHandle.IsValid == false)
        {
            return;
        }

        _swordEffectHandle.Stop();
    }

    /// <summary>
    /// 현재 재생 중인 EyeEffect를 정지합니다.
    /// </summary>
    private void StopEyeEffect()
    {
        if (_eyeEffectHandle == null || _eyeEffectHandle.IsValid == false)
        {
            return;
        }

        _eyeEffectHandle.Stop();
    }

    /// <summary>
    /// 현재 프레임의 지면 접촉 상태를 안전하게 반환합니다.
    /// </summary>
    private bool IsGroundedNow()
    {
        if (_playerMovement == null || _playerMovement.Controller == null)
        {
            return false;
        }

        return _playerMovement.Controller.IsGrounded();
    }

    /// <summary>
    /// HitRequest와 설정값을 기반으로 HitEffect 스폰 위치를 계산합니다.
    /// </summary>
    private Vector3 ResolveHitSpawnPosition(HitRequest request)
    {
        if (_useHitPointForHitEffect && IsFiniteVector3(request.HitPoint))
        {
            return request.HitPoint;
        }

        if (_hitSpawnPoint != null)
        {
            return _hitSpawnPoint.position;
        }

        return transform.position;
    }

    /// <summary>
    /// 전달받은 Vector3가 NaN/Infinity를 포함하지 않는지 확인합니다.
    /// </summary>
    private bool IsFiniteVector3(Vector3 value)
    {
        return float.IsNaN(value.x) == false &&
               float.IsNaN(value.y) == false &&
               float.IsNaN(value.z) == false &&
               float.IsInfinity(value.x) == false &&
               float.IsInfinity(value.y) == false &&
               float.IsInfinity(value.z) == false;
    }
}
