using UnityEngine;

/// <summary>
/// 플레이어 행동(버프/이동/점프/피격/공격) 이벤트를 수집해 CharacterVfxController 요청으로 변환하는 브리지입니다.
/// </summary>
public class PlayerCharacterVfxBridge : MonoBehaviour, IActionListener, IHitListener
{
    [Header("Dependencies")]
    [Tooltip("행동 이벤트를 구독할 ActionController 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private ActionController _actionController; // 액션 이벤트를 수신할 ActionController 참조입니다.
    [Tooltip("지면 상태를 조회할 PlayerMovement 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 이동/점프 상태를 조회할 PlayerMovement 참조입니다.
    [Tooltip("피격 결과 이벤트를 구독할 HitReceiver 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private HitReceiver _hitReceiver; // 피격 이벤트를 수신할 HitReceiver 참조입니다.
    [Tooltip("실제 VFX 재생/정지를 수행할 CharacterVfxController 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private CharacterVfxController _vfxController; // VFX 실행을 담당하는 캐릭터 단위 컨트롤러 참조입니다.
    [Tooltip("멀티플레이 동기화를 담당할 CharacterVfxNetworkSync 참조입니다. 비어 있으면 로컬 전용으로 동작합니다.")]
    [SerializeField] private CharacterVfxNetworkSync _networkSync; // VFX 상태/이벤트의 네트워크 복제를 담당하는 동기화 컴포넌트 참조입니다.

    [Header("Spawn / Resolve")]
    [Tooltip("피격 VFX 위치 계산 시 HitRequest.HitPoint가 유효하면 우선 사용할지 여부입니다.")]
    [SerializeField] private bool _useHitPointForHitEffect = true; // 피격 이펙트 위치 계산에서 HitPoint 우선 적용 여부입니다.
    [Tooltip("WalkDust 활성 상태를 갱신할지 여부입니다.")]
    [SerializeField] private bool _syncWalkDustByGroundState = true; // 지면 상태를 기반으로 WalkDust를 자동 제어할지 여부입니다.
    [Tooltip("PlayerMovement의 실제 점프 수행 이벤트를 구독해 JumpDust를 재생할지 여부입니다.")]
    [SerializeField] private bool _useMovementJumpEvent = true; // 실제 점프 수행 이벤트 기반 JumpDust 재생 경로 활성 여부입니다.

    [Header("Action Mapping")]
    [Tooltip("SwordEffect를 시작/종료할 공격 액션 타입 목록입니다.")]
    [SerializeField]
    private E_ActionType[] _swordEffectActionTypes =
    {
        E_ActionType.Attack,
        E_ActionType.AttackCombo1,
        E_ActionType.AttackCombo2,
        E_ActionType.AttackCombo3,
        E_ActionType.AttackAir,
        E_ActionType.AttackDash,
        E_ActionType.AttackWall
    }; // 검 궤적 VFX를 시작/종료할 액션 타입 목록입니다.

    [Tooltip("점프 성공으로 간주해 JumpDust를 재생할 액션 타입 목록입니다.")]
    [SerializeField]
    private E_ActionType[] _jumpStartActionTypes =
    {
        E_ActionType.Jump,
        E_ActionType.WallJump
    }; // 실제 점프 성공 시점으로 사용하는 액션 타입 목록입니다.

    private bool _isActionListenerRegistered; // ActionController 리스너 등록 여부를 추적하는 플래그입니다.
    private bool _isHitListenerRegistered; // HitReceiver 리스너 등록 여부를 추적하는 플래그입니다.
    private bool _hasGroundState; // WalkDust 초기화 여부를 추적하는 플래그입니다.
    private bool _lastGroundedState; // 직전 프레임 지면 접촉 상태를 캐시하는 값입니다.
    private bool _isJumpEventRegistered; // PlayerMovement 점프 이벤트 구독 상태를 추적하는 플래그입니다.

    /// <summary>
    /// 에디터 값 변경 시 핵심 참조 누락을 점검합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_vfxController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] CharacterVfxController 참조가 비어 있습니다. target={name}", this);
        }
    }

    /// <summary>
    /// 의존성 참조를 보정하고 설정 누락 경고를 점검합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveDependencies();
        ValidateDependencies();
    }

    /// <summary>
    /// 활성화 시 리스너 등록 및 WalkDust 초기 상태를 동기화합니다.
    /// </summary>
    private void OnEnable()
    {
        TryResolveDependencies();
        RegisterActionListener();
        RegisterHitListener();
        RegisterJumpEventListener();
        RefreshWalkDustState(forceApply: true);
    }

    /// <summary>
    /// 비활성화 시 리스너를 해제하고 SwordEffect가 남지 않도록 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterActionListener();
        UnregisterHitListener();
        UnregisterJumpEventListener();
        StopSwordEffect();
        SetWalkDustActive(false);
    }

    /// <summary>
    /// 매 프레임 Grounded 상태를 조회해 WalkDust 활성 상태를 갱신합니다.
    /// </summary>
    private void Update()
    {
        RefreshWalkDustState(forceApply: false);
    }

    /// <summary>
    /// 버프 시작 시 EyeEffect를 활성화합니다.
    /// </summary>
    public void OnEyeBuffStart()
    {
        SetEyeEffectActive(true);
    }

    /// <summary>
    /// 버프 종료 시 EyeEffect를 비활성화합니다.
    /// </summary>
    public void OnEyeBuffEnd()
    {
        SetEyeEffectActive(false);
    }

    /// <summary>
    /// 액션 시작 이벤트를 수신해 SwordEffect와 JumpDust를 필요한 경우 시작합니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
        if (IsSwordAction(runtime.ActionType))
        {
            StartSwordEffect();
        }

        if (!_useMovementJumpEvent && IsJumpStartAction(runtime.ActionType))
        {
            PlayJumpDust();
        }
    }

    /// <summary>
    /// 액션 단계 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
        // 단계 변경 자체에는 별도 VFX를 연결하지 않고 시작/완료/취소 이벤트를 사용합니다.
    }

    /// <summary>
    /// 액션 완료 이벤트를 수신해 SwordEffect 대상 액션이면 종료합니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
        if (IsSwordAction(runtime.ActionType))
        {
            StopSwordEffect();
        }
    }

    /// <summary>
    /// 액션 취소 이벤트를 수신해 SwordEffect 대상 액션이면 종료합니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
        if (IsSwordAction(runtime.ActionType))
        {
            StopSwordEffect();
        }
    }

    /// <summary>
    /// 피격 처리 결과를 수신해 실제 수락된 피격에 대해서만 HitEffect를 재생합니다.
    /// </summary>
    public void OnHitResolved(HitRequest request, HitResult result)
    {
        if (!result.IsAccepted)
        {
            return;
        }

        Vector3 hitPosition = ResolveHitSpawnPosition(request);
        PlayHitEffect(hitPosition);
    }

    /// <summary>
    /// EyeEffect 활성 상태 변경 요청을 처리합니다.
    /// </summary>
    public void SetEyeEffectActive(bool isActive)
    {
        if (_networkSync != null)
        {
            _networkSync.RequestSetEyeEffectActive(isActive);
            return;
        }

        if (_vfxController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] CharacterVfxController가 없어 EyeEffect를 제어할 수 없습니다. target={name}", this);
            return;
        }

        _vfxController.SetEyeEffectActive(isActive);
    }

    /// <summary>
    /// WalkDust 활성 상태 변경 요청을 처리합니다.
    /// </summary>
    public void SetWalkDustActive(bool isActive)
    {
        if (_networkSync != null)
        {
            _networkSync.RequestSetWalkDustActive(isActive);
            return;
        }

        if (_vfxController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] CharacterVfxController가 없어 WalkDust를 제어할 수 없습니다. target={name}", this);
            return;
        }

        _vfxController.SetWalkDustActive(isActive);
    }

    /// <summary>
    /// JumpDust 재생 요청을 처리합니다.
    /// </summary>
    public void PlayJumpDust()
    {
        Vector3 spawnPosition = ResolveFootSpawnPosition();

        if (_networkSync != null)
        {
            _networkSync.RequestPlayJumpDust(spawnPosition);
            return;
        }

        if (_vfxController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] CharacterVfxController가 없어 JumpDust를 재생할 수 없습니다. target={name}", this);
            return;
        }

        _vfxController.PlayJumpDustAt(spawnPosition);
    }

    /// <summary>
    /// 기본 Hit 위치 기준 HitEffect 재생 요청을 처리합니다.
    /// </summary>
    public void PlayHitEffect()
    {
        PlayHitEffect(ResolveFallbackHitPosition());
    }

    /// <summary>
    /// 지정 좌표 기준 HitEffect 재생 요청을 처리합니다.
    /// </summary>
    public void PlayHitEffect(Vector3 worldPosition)
    {
        if (_networkSync != null)
        {
            _networkSync.RequestPlayHitEffect(worldPosition);
            return;
        }

        if (_vfxController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] CharacterVfxController가 없어 HitEffect를 재생할 수 없습니다. target={name}", this);
            return;
        }

        _vfxController.PlayHitEffectAt(worldPosition);
    }

    /// <summary>
    /// SwordEffect 시작 요청을 처리합니다.
    /// </summary>
    public void StartSwordEffect()
    {
        if (_networkSync != null)
        {
            _networkSync.RequestStartSwordEffect();
            return;
        }

        if (_vfxController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] CharacterVfxController가 없어 SwordEffect를 시작할 수 없습니다. target={name}", this);
            return;
        }

        _vfxController.StartSwordEffect();
    }

    /// <summary>
    /// SwordEffect 종료 요청을 처리합니다.
    /// </summary>
    public void StopSwordEffect()
    {
        if (_networkSync != null)
        {
            _networkSync.RequestStopSwordEffect();
            return;
        }

        if (_vfxController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] CharacterVfxController가 없어 SwordEffect를 종료할 수 없습니다. target={name}", this);
            return;
        }

        _vfxController.StopSwordEffect();
    }

    /// <summary>
    /// 의존성 참조가 비어 있으면 같은 오브젝트에서 자동 탐색합니다.
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

        if (_vfxController == null)
        {
            _vfxController = GetComponent<CharacterVfxController>();
        }

        if (_networkSync == null)
        {
            _networkSync = GetComponent<CharacterVfxNetworkSync>();
        }
    }

    /// <summary>
    /// 실행에 필요한 핵심 참조 누락 시 경고 로그를 남깁니다.
    /// </summary>
    private void ValidateDependencies()
    {
        if (_actionController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] ActionController 참조가 없습니다. target={name}", this);
        }

        if (_playerMovement == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] PlayerMovement 참조가 없습니다. target={name}", this);
        }

        if (_hitReceiver == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] HitReceiver 참조가 없습니다. target={name}", this);
        }

        if (_vfxController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] CharacterVfxController 참조가 없습니다. target={name}", this);
        }
    }

    /// <summary>
    /// ActionController 리스너를 안전하게 등록합니다.
    /// </summary>
    private void RegisterActionListener()
    {
        if (_isActionListenerRegistered || _actionController == null)
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
        if (!_isActionListenerRegistered)
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
        if (_isHitListenerRegistered || _hitReceiver == null)
        {
            return;
        }

        _hitReceiver.AddListener(this);
        _isHitListenerRegistered = true;
    }

    /// <summary>
    /// PlayerMovement의 실제 점프 수행 이벤트를 구독합니다.
    /// </summary>
    private void RegisterJumpEventListener()
    {
        if (!_useMovementJumpEvent || _isJumpEventRegistered || _playerMovement == null)
        {
            return;
        }

        _playerMovement.JumpExecuted += HandleJumpExecuted;
        _isJumpEventRegistered = true;
    }

    /// <summary>
    /// PlayerMovement의 점프 이벤트 구독을 해제합니다.
    /// </summary>
    private void UnregisterJumpEventListener()
    {
        if (!_isJumpEventRegistered)
        {
            return;
        }

        if (_playerMovement != null)
        {
            _playerMovement.JumpExecuted -= HandleJumpExecuted;
        }

        _isJumpEventRegistered = false;
    }

    /// <summary>
    /// 실제 점프 수행 이벤트를 수신해 JumpDust를 재생합니다.
    /// </summary>
    private void HandleJumpExecuted(Vector3 jumpWorldPosition)
    {
        if (_networkSync != null)
        {
            _networkSync.RequestPlayJumpDust(jumpWorldPosition);
            return;
        }

        if (_vfxController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] CharacterVfxController가 없어 JumpDust를 재생할 수 없습니다. target={name}", this);
            return;
        }

        _vfxController.PlayJumpDustAt(jumpWorldPosition);
    }

    /// <summary>
    /// HitReceiver 리스너를 안전하게 해제합니다.
    /// </summary>
    private void UnregisterHitListener()
    {
        if (!_isHitListenerRegistered)
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
    /// 지면 상태를 기반으로 WalkDust 활성 상태를 전환합니다.
    /// </summary>
    private void RefreshWalkDustState(bool forceApply)
    {
        if (!_syncWalkDustByGroundState)
        {
            return;
        }

        if (_playerMovement == null || _playerMovement.Controller == null)
        {
            if (forceApply)
            {
                Debug.LogWarning($"[PlayerCharacterVfxBridge] PlayerMovement 또는 Controller가 없어 WalkDust 상태를 갱신할 수 없습니다. target={name}", this);
            }

            return;
        }

        bool groundedNow = _playerMovement.Controller.IsGrounded();
        if (!forceApply && _hasGroundState && groundedNow == _lastGroundedState)
        {
            return;
        }

        _hasGroundState = true;
        _lastGroundedState = groundedNow;
        SetWalkDustActive(groundedNow);
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
    /// 점프 성공으로 간주하는 액션인지 설정 목록 기준으로 판정합니다.
    /// </summary>
    private bool IsJumpStartAction(E_ActionType actionType)
    {
        if (_jumpStartActionTypes == null || _jumpStartActionTypes.Length == 0)
        {
            return actionType == E_ActionType.Jump;
        }

        for (int index = 0; index < _jumpStartActionTypes.Length; index++)
        {
            if (_jumpStartActionTypes[index] == actionType)
            {
                return true;
            }
        }

        return false;
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

        return ResolveFallbackHitPosition();
    }

    /// <summary>
    /// 발 기준 위치를 사용해 JumpDust 스폰 좌표를 계산합니다.
    /// </summary>
    private Vector3 ResolveFootSpawnPosition()
    {
        if (_vfxController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] CharacterVfxController가 없어 Foot 위치를 transform으로 폴백합니다. target={name}", this);
            return transform.position;
        }

        return _vfxController.GetFootVfxWorldPosition();
    }

    /// <summary>
    /// 기본 HitEffect 위치를 계산합니다.
    /// </summary>
    private Vector3 ResolveFallbackHitPosition()
    {
        if (_vfxController == null)
        {
            Debug.LogWarning($"[PlayerCharacterVfxBridge] CharacterVfxController가 없어 Hit 위치를 transform으로 폴백합니다. target={name}", this);
            return transform.position;
        }

        return _vfxController.GetHitVfxWorldPosition();
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
