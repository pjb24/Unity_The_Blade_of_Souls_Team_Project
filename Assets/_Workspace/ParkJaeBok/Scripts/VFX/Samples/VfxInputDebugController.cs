#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class VfxInputDebugController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField]
    private InputActionReference _hitAction; // 피격 이펙트 테스트 입력 액션 참조

    [SerializeField]
    private InputActionReference _landingAction; // 착지 먼지 이펙트 테스트 입력 액션 참조

    [SerializeField]
    private InputActionReference _buffToggleAction; // 버프 오라 토글 테스트 입력 액션 참조

    [Header("Debug Targets")]
    [SerializeField]
    private HitEffectEmitter _hitEmitter; // 피격 이펙트를 실제 재생할 발행기 참조

    [SerializeField]
    private LandingDustEmitter _landingEmitter; // 착지 먼지를 실제 재생할 발행기 참조

    [SerializeField]
    private BuffAuraController _buffAuraController; // 버프 오라 시작/종료를 제어할 컨트롤러 참조

    [SerializeField]
    private Transform _hitPoint; // 피격 이펙트 생성 기준 위치

    [Header("Listener Retry")]
    [SerializeField]
    private float _retryInterval = 0.1f; // 액션 참조가 null일 때 재시도할 간격(초)

    [SerializeField]
    private int _maxRetryCount = 30; // 액션 참조가 null일 때 재시도할 최대 횟수

    private Coroutine _hitBindCoroutine; // 피격 액션 지연 등록 코루틴 핸들
    private Coroutine _landingBindCoroutine; // 착지 액션 지연 등록 코루틴 핸들
    private Coroutine _buffBindCoroutine; // 버프 토글 액션 지연 등록 코루틴 핸들

    private bool _isBuffActive; // 버프 오라 활성 상태 토글 플래그

    /// <summary>
    /// 액션 리스너 지연 등록 코루틴을 시작한다.
    /// </summary>
    private void OnEnable()
    {
        RestartBindCoroutine(ref _hitBindCoroutine, _hitAction, OnHitPerformed, "Hit");
        RestartBindCoroutine(ref _landingBindCoroutine, _landingAction, OnLandingPerformed, "Landing");
        RestartBindCoroutine(ref _buffBindCoroutine, _buffToggleAction, OnBuffTogglePerformed, "BuffToggle");
    }

    /// <summary>
    /// 실행 중 코루틴을 정리하고 가능한 경우 즉시 리스너를 해제한다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _hitBindCoroutine);
        StopRunningCoroutine(ref _landingBindCoroutine);
        StopRunningCoroutine(ref _buffBindCoroutine);

        TryImmediateUnbind(_hitAction, OnHitPerformed, "Hit");
        TryImmediateUnbind(_landingAction, OnLandingPerformed, "Landing");
        TryImmediateUnbind(_buffToggleAction, OnBuffTogglePerformed, "BuffToggle");
    }

    /// <summary>
    /// 파괴 시점에 모든 리스너 해제를 마지막으로 시도한다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _hitBindCoroutine);
        StopRunningCoroutine(ref _landingBindCoroutine);
        StopRunningCoroutine(ref _buffBindCoroutine);

        TryImmediateUnbind(_hitAction, OnHitPerformed, "Hit");
        TryImmediateUnbind(_landingAction, OnLandingPerformed, "Landing");
        TryImmediateUnbind(_buffToggleAction, OnBuffTogglePerformed, "BuffToggle");
    }

    /// <summary>
    /// 피격 테스트 입력 시 피격 이펙트를 재생한다.
    /// </summary>
    private void OnHitPerformed(InputAction.CallbackContext context)
    {
        if (_hitEmitter == null)
        {
            Debug.LogWarning("[VfxInputDebugController] HitEmitter가 비어 있습니다.", this);
            return;
        }

        Vector3 hitPosition = _hitPoint == null ? _hitEmitter.transform.position : _hitPoint.position;
        _hitEmitter.OnHitConfirmed(hitPosition);
    }

    /// <summary>
    /// 착지 테스트 입력 시 먼지 이펙트를 재생한다.
    /// </summary>
    private void OnLandingPerformed(InputAction.CallbackContext context)
    {
        if (_landingEmitter == null)
        {
            Debug.LogWarning("[VfxInputDebugController] LandingEmitter가 비어 있습니다.", this);
            return;
        }

        _landingEmitter.OnLanded();
    }

    /// <summary>
    /// 버프 토글 입력 시 오라를 시작/종료한다.
    /// </summary>
    private void OnBuffTogglePerformed(InputAction.CallbackContext context)
    {
        if (_buffAuraController == null)
        {
            Debug.LogWarning("[VfxInputDebugController] BuffAuraController가 비어 있습니다.", this);
            return;
        }

        _isBuffActive = !_isBuffActive;

        if (_isBuffActive)
        {
            _buffAuraController.OnBuffStart();
        }
        else
        {
            _buffAuraController.OnBuffEnd();
        }
    }

    /// <summary>
    /// 지정한 액션 리스너 등록 코루틴을 재시작한다.
    /// </summary>
    private void RestartBindCoroutine(
        ref Coroutine coroutineHandle,
        InputActionReference actionReference,
        Action<InputAction.CallbackContext> callback,
        string actionLabel)
    {
        StopRunningCoroutine(ref coroutineHandle);
        coroutineHandle = StartCoroutine(BindActionWithRetryCoroutine(actionReference, callback, actionLabel));
    }

    /// <summary>
    /// 액션 참조가 준비될 때까지 재시도한 뒤 리스너를 등록한다.
    /// </summary>
    private IEnumerator BindActionWithRetryCoroutine(
        InputActionReference actionReference,
        Action<InputAction.CallbackContext> callback,
        string actionLabel)
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값

        if (_maxRetryCount < 1 || _retryInterval <= 0f)
        {
            Debug.LogWarning($"[VfxInputDebugController] Invalid retry settings. Fallback maxRetry={safeMaxRetry}, interval={safeInterval}", this);
        }

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            if (TryResolveActionReference(actionReference, out InputAction action))
            {
                action.performed += callback;
                action.Enable();
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[VfxInputDebugController] {actionLabel} action is null. Delaying listener registration.", this);
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[VfxInputDebugController] {actionLabel} action listener registration failed after retries.", this);
    }

    /// <summary>
    /// 비활성화/파괴 시점에 코루틴 없이 안전하게 리스너 해제를 시도한다.
    /// </summary>
    private void TryImmediateUnbind(
        InputActionReference actionReference,
        Action<InputAction.CallbackContext> callback,
        string actionLabel)
    {
        if (TryResolveActionReference(actionReference, out InputAction action) == false)
        {
            Debug.LogWarning($"[VfxInputDebugController] {actionLabel} action is null. RemoveListener skipped.", this);
            return;
        }

        action.performed -= callback;
        action.Disable();
    }

    /// <summary>
    /// 입력 액션 참조를 안전하게 해석한다.
    /// </summary>
    private bool TryResolveActionReference(InputActionReference actionReference, out InputAction action)
    {
        action = null;

        if (actionReference == null)
        {
            return false;
        }

        action = actionReference.action;
        return action != null;
    }

    /// <summary>
    /// 실행 중인 코루틴을 안전하게 중지하고 참조를 정리한다.
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
#else
using UnityEngine;

public class VfxInputDebugController : MonoBehaviour
{
    /// <summary>
    /// New Input System이 비활성화된 환경임을 경고한다.
    /// </summary>
    private void Awake()
    {
        Debug.LogWarning("[VfxInputDebugController] ENABLE_INPUT_SYSTEM이 비활성화되어 동작하지 않습니다.", this);
    }
}
#endif
