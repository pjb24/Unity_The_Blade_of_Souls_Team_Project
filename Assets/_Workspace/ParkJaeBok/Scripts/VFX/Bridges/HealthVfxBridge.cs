using System.Collections;
using UnityEngine;

/// <summary>
/// HealthSystem 이벤트를 받아 체력 변화 관련 VFX를 재생하는 브리지 컴포넌트입니다.
/// </summary>
public class HealthVfxBridge : MonoBehaviour, IHealthListener
{
    [Header("Dependencies")]
    [SerializeField]
    private HealthComponent _healthComponent; // 체력 이벤트를 구독할 HealthComponent 참조

    [Header("Register Retry")]
    [SerializeField]
    private float _retryInterval = 0.1f; // 리스너 등록/해제 재시도 간격(초)

    [SerializeField]
    private int _maxRetryCount = 30; // 리스너 등록/해제 재시도 최대 횟수

    [Header("Effect Ids")]
    [SerializeField]
    private E_EffectId _damagedEffectId = E_EffectId.HitSmall; // 데미지 적용 성공 시 재생할 이펙트 ID

    [SerializeField]
    private E_EffectId _healedEffectId = E_EffectId.BuffAura; // 회복 적용 성공 시 재생할 이펙트 ID

    [SerializeField]
    private E_EffectId _diedEffectId = E_EffectId.EnemyDeath; // 사망 이벤트에서 재생할 이펙트 ID

    [SerializeField]
    private E_EffectId _revivedEffectId = E_EffectId.WorldBurst; // 부활 이벤트에서 재생할 이펙트 ID

    [Header("Spawn")]
    [SerializeField]
    private Transform _spawnPoint; // 체력 이벤트 이펙트를 생성할 기준점

    [Header("Facing")]
    [SerializeField]
    private bool _useTransformScaleForFacing = true; // 오브젝트 좌우 스케일 부호로 방향을 자동 판정할지 여부

    [SerializeField]
    private E_EffectFacingDirection _defaultFacingDirection = E_EffectFacingDirection.Right; // 자동 판정을 사용하지 않을 때 적용할 기본 방향

    private Coroutine _registerCoroutine; // 지연 등록 코루틴 핸들
    private Coroutine _unregisterCoroutine; // 지연 해제 코루틴 핸들

    /// <summary>
    /// 의존성 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveHealthComponentReference();
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

        if (_healthComponent != null)
        {
            _healthComponent.RemoveListener(this);
        }
    }

    /// <summary>
    /// 체력 값 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
        // 체력 변화 수치 자체에는 기본적으로 VFX를 연결하지 않습니다.
    }

    /// <summary>
    /// 데미지 적용 이벤트를 받아 이펙트를 재생합니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
        if (result.IsInvalid || result.IsBlocked)
        {
            return;
        }

        PlayOneShot(_damagedEffectId, ResolveSpawnPosition(), ResolveFacingDirection());
    }

    /// <summary>
    /// 회복 적용 이벤트를 받아 이펙트를 재생합니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
        if (result.IsInvalid || result.IsBlocked)
        {
            return;
        }

        PlayOneShot(_healedEffectId, ResolveSpawnPosition(), ResolveFacingDirection());
    }

    /// <summary>
    /// 사망 이벤트를 받아 이펙트를 재생합니다.
    /// </summary>
    public void OnDied()
    {
        PlayOneShot(_diedEffectId, ResolveSpawnPosition(), ResolveFacingDirection());
    }

    /// <summary>
    /// 부활 이벤트를 받아 이펙트를 재생합니다.
    /// </summary>
    public void OnRevived()
    {
        PlayOneShot(_revivedEffectId, ResolveSpawnPosition(), ResolveFacingDirection());
    }

    /// <summary>
    /// 최대 체력 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
        // 최대 체력 변경은 UI 반영 중심으로 사용하므로 기본 VFX는 재생하지 않습니다.
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
    /// HealthComponent가 준비될 때까지 재시도한 뒤 리스너를 등록합니다.
    /// </summary>
    private IEnumerator RegisterListenerWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값

        if (_maxRetryCount < 1 || _retryInterval <= 0f)
        {
            Debug.LogWarning($"[HealthVfxBridge] Invalid retry settings on {name}. Fallback maxRetry={safeMaxRetry}, interval={safeInterval}.", this);
        }

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            if (TryResolveHealthComponentReference())
            {
                _healthComponent.AddListener(this);
                _registerCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[HealthVfxBridge] HealthComponent is null on {name}. Delaying AddListener registration.", this);
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[HealthVfxBridge] AddListener registration failed after retries on {name}.", this);
        _registerCoroutine = null;
    }

    /// <summary>
    /// HealthComponent가 준비될 때까지 재시도한 뒤 리스너 해제를 수행합니다.
    /// </summary>
    private IEnumerator UnregisterListenerWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            if (TryResolveHealthComponentReference())
            {
                _healthComponent.RemoveListener(this);
                _unregisterCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[HealthVfxBridge] HealthComponent is null on {name}. Delaying RemoveListener unregistration.", this);
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[HealthVfxBridge] RemoveListener unregistration failed after retries on {name}.", this);
        _unregisterCoroutine = null;
    }

    /// <summary>
    /// 체력 이벤트 이펙트의 스폰 위치를 계산합니다.
    /// </summary>
    private Vector3 ResolveSpawnPosition()
    {
        if (_spawnPoint != null)
        {
            return _spawnPoint.position;
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
            Debug.LogWarning($"[HealthVfxBridge] EffectService가 없어 VFX를 재생하지 못했습니다. target={name}", this);
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
    /// 현재 오브젝트 기준으로 HealthComponent 참조를 보정합니다.
    /// </summary>
    private bool TryResolveHealthComponentReference()
    {
        if (_healthComponent != null)
        {
            return true;
        }

        _healthComponent = GetComponent<HealthComponent>();
        if (_healthComponent != null)
        {
            Debug.LogWarning($"[HealthVfxBridge] _healthComponent was null on {name}. Fallback to same GameObject HealthComponent.", this);
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
