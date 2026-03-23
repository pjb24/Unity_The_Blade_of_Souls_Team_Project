using System.Collections;
using UnityEngine;

/// <summary>
/// HitSystem의 피격 결과 이벤트를 받아 VFX를 재생하는 브리지 컴포넌트입니다.
/// </summary>
public class HitVfxBridge : MonoBehaviour, IHitListener
{
    [Header("Dependencies")]
    [SerializeField]
    private HitReceiver _hitReceiver; // 피격 결과를 구독할 HitReceiver 참조

    [Header("Register Retry")]
    [SerializeField]
    private float _retryInterval = 0.1f; // 리스너 등록/해제 재시도 간격(초)

    [SerializeField]
    private int _maxRetryCount = 30; // 리스너 등록/해제 재시도 최대 횟수

    [Header("Effect Ids")]
    [SerializeField]
    private E_EffectId _acceptedHitEffectId = E_EffectId.HitSmall; // 피격 수락 시 재생할 이펙트 ID

    [SerializeField]
    private E_EffectId _deathEffectId = E_EffectId.EnemyDeath; // 피격 결과로 사망했을 때 재생할 이펙트 ID

    [SerializeField]
    private bool _playRejectedEffect; // 피격 거부 결과에 대해서도 이펙트 재생 여부

    [SerializeField]
    private E_EffectId _rejectedEffectId = E_EffectId.WorldBurst; // 피격 거부 시 재생할 이펙트 ID

    [Header("Spawn")]
    [SerializeField]
    private bool _useHitPoint = true; // HitRequest.HitPoint를 우선 사용해 스폰할지 여부

    [SerializeField]
    private Transform _fallbackSpawnPoint; // 히트포인트가 유효하지 않을 때 사용할 대체 스폰 기준점

    [Header("Facing")]
    [SerializeField]
    private bool _useHitDirectionForFacing = true; // HitRequest.HitDirection으로 좌/우 방향을 자동 판정할지 여부

    [SerializeField]
    private E_EffectFacingDirection _defaultFacingDirection = E_EffectFacingDirection.Right; // 자동 판정을 사용하지 않을 때 적용할 기본 방향

    private Coroutine _registerCoroutine; // 지연 등록 코루틴 핸들
    private Coroutine _unregisterCoroutine; // 지연 해제 코루틴 핸들

    /// <summary>
    /// 의존성 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveReceiverReference();
    }

    /// <summary>
    /// 활성화 시 리스너 등록 코루틴을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        RestartRegisterCoroutine();
    }

    /// <summary>
    /// 비활성화 시 리스너 해제 코루틴을 시작합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        RestartUnregisterCoroutine();
    }

    /// <summary>
    /// 오브젝트 파괴 시 실행 중 코루틴을 정리하고 리스너 해제를 마지막으로 시도합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        StopRunningCoroutine(ref _unregisterCoroutine);

        if (_hitReceiver != null)
        {
            _hitReceiver.RemoveListener(this);
        }
    }

    /// <summary>
    /// 피격 처리 결과를 받아 규칙에 맞는 VFX를 재생합니다.
    /// </summary>
    public void OnHitResolved(HitRequest request, HitResult result)
    {
        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[HitVfxBridge] EffectService가 없어 VFX를 재생하지 못했습니다. target={name}", this);
            return;
        }

        Vector3 spawnPosition = ResolveSpawnPosition(request);
        E_EffectFacingDirection facingDirection = ResolveFacingDirection(request);

        if (result.IsAccepted)
        {
            PlayOneShot(_acceptedHitEffectId, spawnPosition, facingDirection);

            if (result.IsDeadAfter)
            {
                PlayOneShot(_deathEffectId, spawnPosition, facingDirection);
            }

            return;
        }

        if (_playRejectedEffect == false)
        {
            return;
        }

        PlayOneShot(_rejectedEffectId, spawnPosition, facingDirection);
    }

    /// <summary>
    /// 등록 코루틴을 재시작합니다.
    /// </summary>
    private void RestartRegisterCoroutine()
    {
        StopRunningCoroutine(ref _unregisterCoroutine);
        StopRunningCoroutine(ref _registerCoroutine);
        _registerCoroutine = StartCoroutine(RegisterListenerWithRetryCoroutine());
    }

    /// <summary>
    /// 해제 코루틴을 재시작합니다.
    /// </summary>
    private void RestartUnregisterCoroutine()
    {
        StopRunningCoroutine(ref _unregisterCoroutine);
        _unregisterCoroutine = StartCoroutine(UnregisterListenerWithRetryCoroutine());
    }

    /// <summary>
    /// 리시버가 준비될 때까지 재시도한 뒤 리스너를 등록합니다.
    /// </summary>
    private IEnumerator RegisterListenerWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값

        if (_maxRetryCount < 1 || _retryInterval <= 0f)
        {
            Debug.LogWarning($"[HitVfxBridge] Invalid retry settings on {name}. Fallback maxRetry={safeMaxRetry}, interval={safeInterval}.", this);
        }

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            if (TryResolveReceiverReference())
            {
                _hitReceiver.AddListener(this);
                _registerCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[HitVfxBridge] Receiver is null on {name}. Delaying AddListener registration.", this);
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[HitVfxBridge] AddListener registration failed after retries on {name}.", this);
        _registerCoroutine = null;
    }

    /// <summary>
    /// 리시버가 준비될 때까지 재시도한 뒤 리스너 해제를 수행합니다.
    /// </summary>
    private IEnumerator UnregisterListenerWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            if (TryResolveReceiverReference())
            {
                _hitReceiver.RemoveListener(this);
                _unregisterCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[HitVfxBridge] Receiver is null on {name}. Delaying RemoveListener unregistration.", this);
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[HitVfxBridge] RemoveListener unregistration failed after retries on {name}.", this);
        _unregisterCoroutine = null;
    }

    /// <summary>
    /// 피격 요청 정보를 기반으로 VFX 스폰 위치를 계산합니다.
    /// </summary>
    private Vector3 ResolveSpawnPosition(HitRequest request)
    {
        if (_useHitPoint && IsFiniteVector3(request.HitPoint))
        {
            return request.HitPoint;
        }

        if (_fallbackSpawnPoint != null)
        {
            return _fallbackSpawnPoint.position;
        }

        return transform.position;
    }

    /// <summary>
    /// 벡터가 NaN/Infinity를 포함하지 않는지 확인합니다.
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

    /// <summary>
    /// 지정한 이펙트 ID를 OneShot으로 재생합니다.
    /// </summary>
    private void PlayOneShot(E_EffectId effectId, Vector3 spawnPosition, E_EffectFacingDirection facingDirection)
    {
        if (effectId == E_EffectId.None)
        {
            return;
        }

        EffectRequest request = EffectRequest.CreateSimple(effectId, spawnPosition);
        request.FacingDirection = facingDirection;
        EffectService.Instance.Play(request);
    }

    /// <summary>
    /// 피격 요청 기반으로 이펙트 재생 방향(좌/우)을 결정합니다.
    /// </summary>
    private E_EffectFacingDirection ResolveFacingDirection(HitRequest request)
    {
        if (_useHitDirectionForFacing == false)
        {
            return _defaultFacingDirection;
        }

        if (request.HitDirection.x < 0f)
        {
            return E_EffectFacingDirection.Left;
        }

        if (request.HitDirection.x > 0f)
        {
            return E_EffectFacingDirection.Right;
        }

        return _defaultFacingDirection;
    }

    /// <summary>
    /// 현재 오브젝트 기준으로 HitReceiver 참조를 보정합니다.
    /// </summary>
    private bool TryResolveReceiverReference()
    {
        if (_hitReceiver != null)
        {
            return true;
        }

        _hitReceiver = GetComponent<HitReceiver>();
        if (_hitReceiver != null)
        {
            Debug.LogWarning($"[HitVfxBridge] _hitReceiver was null on {name}. Fallback to same GameObject receiver.", this);
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
