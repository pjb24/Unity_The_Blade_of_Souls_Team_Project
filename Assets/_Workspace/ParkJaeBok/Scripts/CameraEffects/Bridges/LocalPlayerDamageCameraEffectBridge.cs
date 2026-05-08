using System.Collections;
using UnityEngine;

/// <summary>
/// 로컬 플레이어 HealthComponent의 피격 이벤트를 CameraEffectPreset 재생으로 연결하는 브리지입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalPlayerDamageCameraEffectBridge : MonoBehaviour, IHealthListener
{
    [Header("Dependencies")]
    [Tooltip("로컬 플레이어 HealthComponent를 찾아 제공하는 Provider입니다. 비어 있으면 현재 오브젝트에서 자동으로 찾거나 생성합니다.")]
    [SerializeField] private LocalPlayerHealthProvider _localPlayerHealthProvider; // 로컬 플레이어 체력 참조를 안정적으로 제공하는 Provider입니다.

    [Header("Camera Effect")]
    [Tooltip("로컬 플레이어가 피격되었을 때 재생할 CameraEffectPreset입니다.")]
    [SerializeField] private CameraEffectPresetBase _damagedCameraEffectPreset; // 피격 순간 재생할 카메라 이펙트 프리셋입니다.

    [Tooltip("연속 피격 시 카메라 이펙트가 과도하게 중첩되지 않도록 제한하는 최소 재생 간격입니다.")]
    [Min(0f)]
    [SerializeField] private float _minimumReplayIntervalSeconds = 0.08f; // 피격 카메라 이펙트 재생 사이의 최소 간격입니다.

    [Header("Bind Retry")]
    [Tooltip("로컬 플레이어 HealthComponent가 늦게 준비될 때 재시도하는 간격입니다.")]
    [Min(0.01f)]
    [SerializeField] private float _retryIntervalSeconds = 0.1f; // HealthComponent 준비 대기 재시도 간격입니다.

    [Tooltip("로컬 플레이어 HealthComponent 등록을 재시도할 최대 횟수입니다.")]
    [Min(1)]
    [SerializeField] private int _maxRetryCount = 30; // HealthComponent 등록 재시도 횟수입니다.

    [Header("Debug")]
    [Tooltip("디버그용: 현재 피격 이벤트를 구독 중인 HealthComponent입니다.")]
    [SerializeField] private HealthComponent _targetHealth; // 현재 구독 중인 로컬 플레이어 HealthComponent입니다.

    private Coroutine _bindCoroutine; // 로컬 플레이어 HealthComponent 등록 지연 처리를 담당하는 코루틴입니다.
    private bool _isRegistered; // 현재 HealthComponent에 IHealthListener가 등록되어 있는지 추적합니다.
    private float _lastPlayedUnscaledTime = -999f; // 마지막 피격 카메라 이펙트 재생 시각입니다.

    /// <summary>
    /// 의존성 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        ResolveLocalPlayerHealthProvider();
    }

    /// <summary>
    /// Provider 변경 알림을 구독하고 로컬 HealthComponent 등록을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveLocalPlayerHealthProvider();

        if (_localPlayerHealthProvider != null)
        {
            _localPlayerHealthProvider.AddLocalHealthChangedListener(HandleLocalHealthChanged);
        }

        RestartBindCoroutine();
    }

    /// <summary>
    /// Provider와 HealthComponent 구독을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        StopBindCoroutine();

        if (_localPlayerHealthProvider != null)
        {
            _localPlayerHealthProvider.RemoveLocalHealthChangedListener(HandleLocalHealthChanged);
        }

        UnregisterHealthListener();
    }

    /// <summary>
    /// 체력 변경 이벤트는 피격 카메라 이펙트에서 사용하지 않습니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
    }

    /// <summary>
    /// 로컬 플레이어 피격이 확정되면 지정된 CameraEffectPreset을 재생합니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
        if (_damagedCameraEffectPreset == null)
        {
            return;
        }

        if (Time.unscaledTime - _lastPlayedUnscaledTime < _minimumReplayIntervalSeconds)
        {
            return;
        }

        _lastPlayedUnscaledTime = Time.unscaledTime;
        CameraEffectPlaybackUtility.Play(_damagedCameraEffectPreset, gameObject);
    }

    /// <summary>
    /// 회복 이벤트는 피격 카메라 이펙트에서 사용하지 않습니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 사망 이벤트는 피격 카메라 이펙트에서 별도 처리하지 않습니다.
    /// </summary>
    public void OnDied()
    {
    }

    /// <summary>
    /// 부활 이벤트는 피격 카메라 이펙트에서 별도 처리하지 않습니다.
    /// </summary>
    public void OnRevived()
    {
    }

    /// <summary>
    /// 최대 체력 변경 이벤트는 피격 카메라 이펙트에서 사용하지 않습니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }

    /// <summary>
    /// Provider가 로컬 HealthComponent 변경을 알릴 때 구독 대상을 교체합니다.
    /// </summary>
    private void HandleLocalHealthChanged(HealthComponent localHealth)
    {
        if (localHealth == null)
        {
            RestartBindCoroutine();
            return;
        }

        BindTargetHealth(localHealth);
    }

    /// <summary>
    /// 로컬 플레이어 HealthComponent를 찾을 때까지 재시도합니다.
    /// </summary>
    private IEnumerator BindWhenReadyCoroutine()
    {
        int safeRetryCount = Mathf.Max(1, _maxRetryCount);
        float safeInterval = Mathf.Max(0.01f, _retryIntervalSeconds);

        for (int retryIndex = 0; retryIndex < safeRetryCount; retryIndex++)
        {
            if (_localPlayerHealthProvider != null &&
                _localPlayerHealthProvider.TryGetCurrentLocalHealth(out HealthComponent localHealth) &&
                localHealth != null)
            {
                BindTargetHealth(localHealth);
                if (_isRegistered)
                {
                    _bindCoroutine = null;
                    yield break;
                }
            }

            yield return new WaitForSecondsRealtime(safeInterval);
        }

        _bindCoroutine = null;
    }

    /// <summary>
    /// 등록 대상 HealthComponent를 교체하고 준비 완료 시 리스너를 등록합니다.
    /// </summary>
    private void BindTargetHealth(HealthComponent newTargetHealth)
    {
        if (newTargetHealth == null)
        {
            return;
        }

        if (_targetHealth != newTargetHealth)
        {
            UnregisterHealthListener();
            _targetHealth = newTargetHealth;
        }

        if (!_targetHealth.IsInitialized || _isRegistered)
        {
            return;
        }

        _targetHealth.AddListener(this);
        _isRegistered = true;
    }

    /// <summary>
    /// LocalPlayerHealthProvider 참조가 없으면 현재 오브젝트에서 찾거나 생성합니다.
    /// </summary>
    private void ResolveLocalPlayerHealthProvider()
    {
        if (_localPlayerHealthProvider != null)
        {
            return;
        }

        _localPlayerHealthProvider = FindAnyObjectByType<LocalPlayerHealthProvider>();
        if (_localPlayerHealthProvider != null)
        {
            return;
        }

        _localPlayerHealthProvider = gameObject.AddComponent<LocalPlayerHealthProvider>();
    }

    /// <summary>
    /// 기존 바인딩 코루틴을 중지하고 새 바인딩 코루틴을 시작합니다.
    /// </summary>
    private void RestartBindCoroutine()
    {
        StopBindCoroutine();
        _bindCoroutine = StartCoroutine(BindWhenReadyCoroutine());
    }

    /// <summary>
    /// 실행 중인 바인딩 코루틴을 중지합니다.
    /// </summary>
    private void StopBindCoroutine()
    {
        if (_bindCoroutine == null)
        {
            return;
        }

        StopCoroutine(_bindCoroutine);
        _bindCoroutine = null;
    }

    /// <summary>
    /// 현재 HealthComponent에서 리스너 등록을 해제합니다.
    /// </summary>
    private void UnregisterHealthListener()
    {
        if (!_isRegistered)
        {
            return;
        }

        if (_targetHealth != null && _targetHealth.IsInitialized)
        {
            _targetHealth.RemoveListener(this);
        }

        _isRegistered = false;
    }
}
