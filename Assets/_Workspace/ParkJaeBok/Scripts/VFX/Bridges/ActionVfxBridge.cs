using System.Collections;
using UnityEngine;

/// <summary>
/// ActionSystem 이벤트를 받아 액션 연동 VFX를 재생하는 브리지 컴포넌트입니다.
/// </summary>
public class ActionVfxBridge : MonoBehaviour, IActionListener
{
    [System.Serializable]
    private struct ActionEffectMap
    {
        [SerializeField]
        public E_ActionType ActionType; // 매핑 기준이 되는 액션 타입

        [SerializeField]
        public E_EffectId StartEffectId; // 액션 시작 시 재생할 이펙트 ID

        [SerializeField]
        public E_EffectId CompleteEffectId; // 액션 완료 시 재생할 이펙트 ID

        [SerializeField]
        public E_EffectId CancelEffectId; // 액션 취소 시 재생할 이펙트 ID
    }

    [Header("Dependencies")]
    [SerializeField]
    private ActionController _actionController; // 액션 이벤트를 구독할 ActionController 참조

    [Header("Action Event Mapping")]
    [SerializeField]
    private ActionEffectMap[] _actionEffectMaps; // 액션 타입별 시작/완료/취소 이펙트 매핑 배열

    [Header("Hit Window Trail")]
    [SerializeField]
    private bool _useHitWindowTrail = true; // HitWindow 열림/닫힘에 따라 궤적 이펙트 제어를 사용할지 여부

    [SerializeField]
    private E_EffectId _hitWindowTrailEffectId = E_EffectId.WeaponTrail; // HitWindow가 열릴 때 재생할 지속형 궤적 이펙트 ID

    [SerializeField]
    private Transform _trailAttachTarget; // 궤적 이펙트를 Attach할 본/트랜스폼

    [Header("Facing")]
    [SerializeField]
    private bool _useTransformScaleForFacing = true; // 오브젝트 좌우 스케일 부호로 방향을 자동 판정할지 여부

    [SerializeField]
    private E_EffectFacingDirection _defaultFacingDirection = E_EffectFacingDirection.Right; // 자동 판정을 사용하지 않을 때 적용할 기본 방향

    [SerializeField]
    private bool _usePlayerMovementFacing = true; // PlayerMovement.IsFacingRight 값을 우선 사용해 방향을 결정할지 여부

    private EffectHandle _trailHandle; // 현재 활성 궤적 이펙트를 제어하는 핸들
    private Coroutine _delayedRegisterCoroutine; // 활성화 직후 1프레임 지연 등록을 처리하는 코루틴 핸들
    private bool _isSubscribed; // ActionController 이벤트 구독이 완료된 상태인지 여부
    [SerializeField] private PlayerMovement _playerMovement; // PlayerMovement 방향 정보를 조회할 참조

    /// <summary>
    /// 의존성 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveActionControllerReference();
        TryResolvePlayerMovementReference();
    }

    /// <summary>
    /// 활성화 시 1프레임 지연 리스너 등록 코루틴을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        RestartDelayedRegisterCoroutine();
    }

    //// <summary>
    /// 비활성화 시 지연 등록 코루틴을 중지하고 리스너를 즉시 해제한 뒤 지속형 이펙트를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _delayedRegisterCoroutine);
        TryImmediateUnregister();
        StopTrailIfPlaying();
    }

    /// <summary>
    /// 오브젝트 파괴 시 지연 등록 코루틴을 정리하고 리스너 해제를 마지막으로 시도합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _delayedRegisterCoroutine);

        TryImmediateUnregister();
        StopTrailIfPlaying();
    }

    /// <summary>
    /// 액션 시작 이벤트를 받아 매핑된 시작 이펙트를 재생합니다.
    /// </summary>
    public void OnActionStarted(ActionRuntime runtime)
    {
        if (TryGetMap(runtime.ActionType, out ActionEffectMap map) == false)
        {
            return;
        }

        PlayOneShot(map.StartEffectId, ResolveSpawnPosition(), ResolveFacingDirection());
    }

    /// <summary>
    /// 액션 단계 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnActionPhaseChanged(ActionRuntime runtime, E_ActionPhase previousPhase, E_ActionPhase currentPhase)
    {
        // 단계 전환 자체에는 기본적으로 별도 VFX를 연결하지 않습니다.
    }

    /// <summary>
    /// 액션 완료 이벤트를 받아 매핑된 완료 이펙트를 재생합니다.
    /// </summary>
    public void OnActionCompleted(ActionRuntime runtime)
    {
        if (TryGetMap(runtime.ActionType, out ActionEffectMap map) == false)
        {
            return;
        }

        PlayOneShot(map.CompleteEffectId, ResolveSpawnPosition(), ResolveFacingDirection());
    }

    /// <summary>
    /// 액션 취소 이벤트를 받아 매핑된 취소 이펙트를 재생합니다.
    /// </summary>
    public void OnActionCancelled(ActionRuntime runtime, string reason)
    {
        if (TryGetMap(runtime.ActionType, out ActionEffectMap map) == false)
        {
            return;
        }

        PlayOneShot(map.CancelEffectId, ResolveSpawnPosition(), ResolveFacingDirection());
    }

    /// <summary>
    /// 활성화 직후 1프레임 지연 등록 코루틴을 재시작합니다.
    /// </summary>
    private void RestartDelayedRegisterCoroutine()
    {
        StopRunningCoroutine(ref _delayedRegisterCoroutine);
        _delayedRegisterCoroutine = StartCoroutine(DelayedRegisterCoroutine());
    }

    /// <summary>
    /// 활성화 직후 1프레임을 기다렸다가 ActionController 리스너 등록을 시도합니다.
    /// </summary>
    private IEnumerator DelayedRegisterCoroutine()
    {
        yield return null;

        if (!isActiveAndEnabled)
        {
            _delayedRegisterCoroutine = null;
            yield break;
        }

        if (TryResolveActionControllerReference())
        {
            RegisterToActionController();
            _delayedRegisterCoroutine = null;
            yield break;
        }

        Debug.LogWarning($"[ActionVfxBridge] ActionController is null on {name}. Delayed AddListener registration skipped.", this);
        _delayedRegisterCoroutine = null;
    }

    /// <summary>
    /// ActionController에 리스너와 이벤트 구독을 등록합니다.
    /// </summary>
    private void RegisterToActionController()
    {
        if (_isSubscribed)
        {
            return;
        }

        _actionController.AddListener(this);
        _actionController.OnHitWindowChanged += HandleHitWindowChanged;
        _isSubscribed = true;
    }

    /// <summary>
    /// ActionController에 등록된 리스너와 이벤트 구독을 해제합니다.
    /// </summary>
    private void UnregisterFromActionController()
    {
        if (_isSubscribed == false)
        {
            return;
        }

        _actionController.RemoveListener(this);
        _actionController.OnHitWindowChanged -= HandleHitWindowChanged;
        _isSubscribed = false;
    }

    /// <summary>
    /// 즉시 가능한 경우 ActionController 구독을 해제합니다.
    /// </summary>
    private void TryImmediateUnregister()
    {
        if (_actionController == null)
        {
            return;
        }

        UnregisterFromActionController();
    }

    /// <summary>
    /// HitWindow 상태를 받아 궤적 이펙트의 시작/종료를 제어합니다.
    /// </summary>
    private void HandleHitWindowChanged(bool isOpen)
    {
        if (_useHitWindowTrail == false)
        {
            return;
        }

        if (isOpen)
        {
            PlayTrailIfNeeded();
            return;
        }

        StopTrailIfPlaying();
    }

    /// <summary>
    /// HitWindow 궤적 이펙트가 비활성 상태일 때 Attach 모드로 재생합니다.
    /// </summary>
    private void PlayTrailIfNeeded()
    {
        if (_hitWindowTrailEffectId == E_EffectId.None)
        {
            return;
        }

        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[ActionVfxBridge] EffectService가 없어 궤적 VFX를 재생하지 못했습니다. target={name}", this);
            return;
        }

        if (_trailHandle != null && _trailHandle.IsValid)
        {
            return;
        }

        Transform attachTarget = _trailAttachTarget == null ? transform : _trailAttachTarget;

        EffectRequest request = new EffectRequest();
        request.EffectId = _hitWindowTrailEffectId;
        request.PlayMode = E_EffectPlayMode.Attach;
        request.AttachTarget = attachTarget;
        request.Owner = gameObject;
        request.LocalOffset = Vector3.zero;
        request.AutoReturnOverrideEnabled = true;
        request.AutoReturn = false;
        request.LifetimeOverride = 0f;
        request.IgnoreDuplicateGuard = false;
        request.FacingDirection = ResolveFacingDirection();

        _trailHandle = EffectService.Instance.Play(request);
    }

    /// <summary>
    /// 현재 활성 궤적 이펙트 핸들을 정지하고 참조를 초기화합니다.
    /// </summary>
    private void StopTrailIfPlaying()
    {
        if (_trailHandle == null)
        {
            return;
        }

        _trailHandle.Stop();
        _trailHandle = null;
    }

    /// <summary>
    /// 매핑 배열에서 지정 액션 타입의 이펙트 매핑 정보를 조회합니다.
    /// </summary>
    private bool TryGetMap(E_ActionType actionType, out ActionEffectMap map)
    {
        if (_actionEffectMaps != null)
        {
            for (int i = 0; i < _actionEffectMaps.Length; i++)
            {
                if (_actionEffectMaps[i].ActionType == actionType)
                {
                    map = _actionEffectMaps[i];
                    return true;
                }
            }
        }

        map = default;
        return false;
    }

    /// <summary>
    /// 액션 이벤트 이펙트의 스폰 위치를 계산합니다.
    /// </summary>
    private Vector3 ResolveSpawnPosition()
    {
        if (_trailAttachTarget != null)
        {
            return _trailAttachTarget.position;
        }

        return transform.position;
    }

    /// <summary>
    /// 지정한 이펙트 ID를 OneShot으로 재생합니다.
    /// </summary>
    private void PlayOneShot(E_EffectId effectId, Vector3 spawnPosition, E_EffectFacingDirection facingDirection)
    {
        if (effectId == E_EffectId.None)
        {
            return;
        }

        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[ActionVfxBridge] EffectService가 없어 VFX를 재생하지 못했습니다. target={name}", this);
            return;
        }

        EffectRequest request = EffectRequest.CreateSimple(effectId, spawnPosition);
        request.FacingDirection = facingDirection;
        EffectService.Instance.Play(request);
    }

    /// <summary>
    /// 현재 오브젝트 상태를 기준으로 이펙트 재생 방향(좌/우)을 결정합니다.
    /// </summary>
    private E_EffectFacingDirection ResolveFacingDirection()
    {
        if (_usePlayerMovementFacing && TryResolvePlayerMovementReference())
        {
            return _playerMovement.IsFacingRight ? E_EffectFacingDirection.Right : E_EffectFacingDirection.Left;
        }

        if (_useTransformScaleForFacing == false)
        {
            return _defaultFacingDirection;
        }

        float sign = transform.lossyScale.x;
        if (sign < 0f)
        {
            return E_EffectFacingDirection.Left;
        }

        if (sign > 0f)
        {
            return E_EffectFacingDirection.Right;
        }

        return _defaultFacingDirection;
    }

    /// <summary>
    /// 현재 오브젝트 기준으로 PlayerMovement 참조를 보정합니다.
    /// </summary>
    private bool TryResolvePlayerMovementReference()
    {
        if (_playerMovement != null)
        {
            return true;
        }

        _playerMovement = GetComponent<PlayerMovement>();
        if (_playerMovement != null)
        {
            return true;
        }

        return false;
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
            Debug.LogWarning($"[ActionVfxBridge] _actionController was null on {name}. Fallback to same GameObject ActionController.", this);
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
}
