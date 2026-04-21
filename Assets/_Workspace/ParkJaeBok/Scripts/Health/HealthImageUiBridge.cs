using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로컬 플레이어 HealthComponent 이벤트를 수신해 슬롯형 Image UI를 갱신하는 브리지 컴포넌트입니다.
/// </summary>
public class HealthImageUiBridge : MonoBehaviour, IHealthListener
{
    [Header("Dependencies")]
    [Tooltip("로컬 플레이어 Health를 해석해 제공할 Provider 참조입니다. 비어 있으면 런타임에서 자동 탐색/생성합니다.")]
    [SerializeField] private LocalPlayerHealthProvider _localPlayerHealthProvider; // 로컬 플레이어 Health 해석을 위임할 Provider 참조입니다.

    [Tooltip("현재 UI가 구독 중인 로컬 플레이어 HealthComponent 참조입니다.")]
    [SerializeField] private HealthComponent _targetHealth; // 현재 UI가 구독 중인 로컬 플레이어 HealthComponent 참조입니다.

    [Header("UI")]
    [Tooltip("체력을 표시할 슬롯 Image 배열입니다. 인덱스가 낮을수록 앞 슬롯으로 취급합니다.")]
    [SerializeField] private Image[] _healthImages = new Image[5]; // 체력 표시를 담당하는 Image 슬롯 배열입니다.

    [Tooltip("UI 체력 슬롯 수입니다. 배열 길이보다 크면 배열 길이까지만 사용됩니다.")]
    [SerializeField] private int _slotCount = 5; // 체력 UI 슬롯 수입니다.

    [Header("Bind Retry")]
    [Tooltip("로컬 플레이어/Health 생성 지연 시 바인딩 재시도 간격(초)입니다.")]
    [SerializeField] private float _retryInterval = 0.1f; // 바인딩 재시도 간격(초)입니다.

    [Tooltip("로컬 플레이어/Health 생성 지연 시 바인딩 재시도 최대 횟수입니다.")]
    [SerializeField] private int _maxRetryCount = 30; // 바인딩 재시도 최대 횟수입니다.

    private Coroutine _registerCoroutine; // 리스너 등록 지연 처리 코루틴 핸들입니다.
    private bool _isRegistered; // Target HealthComponent에 리스너 등록되어 있는지 추적하는 플래그입니다.

    /// <summary>
    /// 현재 UI가 구독 중인 HealthComponent를 외부에서 조회합니다.
    /// </summary>
    public HealthComponent TargetHealth => _targetHealth;

    /// <summary>
    /// 의존성 보정과 슬롯 구성 검증을 수행합니다.
    /// </summary>
    private void Awake()
    {
        ResolveLocalPlayerHealthProvider();
        ValidateSlotConfiguration();
    }

    /// <summary>
    /// 활성화 시 Provider 변경 알림을 구독하고 바인딩 코루틴을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveLocalPlayerHealthProvider();

        if (_localPlayerHealthProvider != null)
        {
            _localPlayerHealthProvider.AddLocalHealthChangedListener(HandleLocalHealthChanged);
        }

        RestartRegisterCoroutine();
    }

    /// <summary>
    /// 비활성화 시 코루틴/리스너 구독을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerCoroutine);

        if (_localPlayerHealthProvider != null)
        {
            _localPlayerHealthProvider.RemoveLocalHealthChangedListener(HandleLocalHealthChanged);
        }

        UnregisterListener();
    }

    /// <summary>
    /// 파괴 시점에 코루틴/리스너를 안전하게 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _registerCoroutine);

        if (_localPlayerHealthProvider != null)
        {
            _localPlayerHealthProvider.RemoveLocalHealthChangedListener(HandleLocalHealthChanged);
        }

        UnregisterListener();
    }

    /// <summary>
    /// 체력 변경 이벤트를 받아 슬롯 UI를 갱신합니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
        RefreshHealthImages(data.CurrentHealth, data.MaxHealth);
    }

    /// <summary>
    /// 데미지 이벤트는 현재 슬롯 표시 로직에서 별도 처리하지 않습니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// 회복 이벤트는 현재 슬롯 표시 로직에서 별도 처리하지 않습니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 사망 이벤트를 받아 모든 슬롯을 비활성화합니다.
    /// </summary>
    public void OnDied()
    {
        SetFilledSlots(0);
    }

    /// <summary>
    /// 부활 이벤트를 받아 현재 Health 상태로 UI를 즉시 동기화합니다.
    /// </summary>
    public void OnRevived()
    {
        ForceRefreshFromHealthComponent();
    }

    /// <summary>
    /// 최대 체력 변경 이벤트를 받아 슬롯 표시를 재계산합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
        ForceRefreshFromHealthComponent();
    }

    /// <summary>
    /// Provider에서 로컬 플레이어 Health 참조가 변경되었을 때 재바인딩합니다.
    /// </summary>
    private void HandleLocalHealthChanged(HealthComponent localHealth)
    {
        if (localHealth == null)
        {
            Debug.LogWarning($"[HealthImageUiBridge] Local player Health is not available yet. object={name}", this);
            RestartRegisterCoroutine();
            return;
        }

        BindTargetHealth(localHealth, "ProviderEvent");
    }

    /// <summary>
    /// 바인딩 코루틴을 재시작합니다.
    /// </summary>
    private void RestartRegisterCoroutine()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        _registerCoroutine = StartCoroutine(RegisterListenerWhenReadyCoroutine());
    }

    /// <summary>
    /// 로컬 플레이어 Health 준비 완료까지 재시도 후 리스너를 등록합니다.
    /// </summary>
    private IEnumerator RegisterListenerWhenReadyCoroutine()
    {
        int safeRetryCount = Mathf.Max(1, _maxRetryCount); // 잘못된 재시도 횟수 설정을 보정한 안전 값입니다.
        float safeRetryInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격 설정을 보정한 안전 값입니다.

        if (_maxRetryCount < 1 || _retryInterval <= 0f)
        {
            Debug.LogWarning($"[HealthImageUiBridge] Invalid retry settings on {name}. Fallback maxRetry={safeRetryCount}, interval={safeRetryInterval}.", this);
        }

        for (int retryIndex = 0; retryIndex < safeRetryCount; retryIndex++)
        {
            if (TryResolveHealthComponentReference(out HealthComponent resolvedHealth))
            {
                BindTargetHealth(resolvedHealth, "RetryLoop");

                if (_isRegistered)
                {
                    _registerCoroutine = null;
                    yield break;
                }
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[HealthImageUiBridge] Waiting for local player Health binding. object={name}", this);
            }

            yield return new WaitForSeconds(safeRetryInterval);
        }

        Debug.LogWarning($"[HealthImageUiBridge] Local player Health binding failed after retries. object={name}", this);
        _registerCoroutine = null;
    }

    /// <summary>
    /// 로컬 플레이어 HealthProvider 참조를 자동 보정합니다.
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
        Debug.LogWarning($"[HealthImageUiBridge] LocalPlayerHealthProvider was missing. Added runtime provider on {name}.", this);
    }

    /// <summary>
    /// Provider를 사용해 로컬 플레이어 HealthComponent 참조를 해석합니다.
    /// </summary>
    private bool TryResolveHealthComponentReference(out HealthComponent resolvedHealth)
    {
        resolvedHealth = null;

        ResolveLocalPlayerHealthProvider();
        if (_localPlayerHealthProvider == null)
        {
            Debug.LogWarning($"[HealthImageUiBridge] LocalPlayerHealthProvider is missing. object={name}", this);
            return false;
        }

        if (!_localPlayerHealthProvider.TryGetCurrentLocalHealth(out resolvedHealth) || resolvedHealth == null)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 대상 HealthComponent를 교체하고 리스너 등록/초기 UI 동기화를 수행합니다.
    /// </summary>
    private void BindTargetHealth(HealthComponent newTargetHealth, string reason)
    {
        if (newTargetHealth == null)
        {
            Debug.LogWarning($"[HealthImageUiBridge] Bind request ignored because target health is null. object={name}, reason={reason}", this);
            return;
        }

        if (_targetHealth != newTargetHealth)
        {
            UnregisterListener();
            _targetHealth = newTargetHealth;
        }

        if (!_targetHealth.IsInitialized)
        {
            Debug.LogWarning($"[HealthImageUiBridge] Target health is not initialized yet. object={name}, target={_targetHealth.name}, reason={reason}", this);
            return;
        }

        if (!_isRegistered)
        {
            _targetHealth.AddListener(this);
            _isRegistered = true;
        }

        ForceRefreshFromHealthComponent();
    }

    /// <summary>
    /// 슬롯 구성 유효성을 검사하고 필요 시 경고를 출력합니다.
    /// </summary>
    private void ValidateSlotConfiguration()
    {
        if (_slotCount <= 0)
        {
            Debug.LogWarning($"[HealthImageUiBridge] Slot count({_slotCount}) is invalid on {name}. Fallback to 5.", this);
            _slotCount = 5;
        }

        if (_healthImages == null || _healthImages.Length < _slotCount)
        {
            int configuredLength = _healthImages == null ? 0 : _healthImages.Length;
            Debug.LogWarning($"[HealthImageUiBridge] Health image array length({configuredLength}) is smaller than slot count({_slotCount}) on {name}.", this);
        }
    }

    /// <summary>
    /// Target HealthComponent의 현재 체력 데이터를 읽어 UI를 즉시 갱신합니다.
    /// </summary>
    private void ForceRefreshFromHealthComponent()
    {
        if (_targetHealth == null || !_targetHealth.IsInitialized)
        {
            return;
        }

        float currentHealth = _targetHealth.GetCurrentHealth();
        float maxHealth = _targetHealth.GetMaxHealth();
        RefreshHealthImages(currentHealth, maxHealth);
    }

    /// <summary>
    /// 현재/최대 체력을 슬롯 개수에 맞게 매핑해 표시합니다.
    /// </summary>
    private void RefreshHealthImages(float currentHealth, float maxHealth)
    {
        if (_healthImages == null || _healthImages.Length == 0)
        {
            return;
        }

        if (_slotCount <= 0)
        {
            SetFilledSlots(0);
            return;
        }

        if (maxHealth <= 0f)
        {
            SetFilledSlots(0);
            return;
        }

        float normalizedHealth = Mathf.Clamp01(currentHealth / maxHealth);
        int filledSlots = currentHealth <= 0f ? 0 : Mathf.CeilToInt(normalizedHealth * _slotCount);
        filledSlots = Mathf.Clamp(filledSlots, 0, _slotCount);

        SetFilledSlots(filledSlots);
    }

    /// <summary>
    /// 계산된 슬롯 수만큼 이미지를 활성화하고 나머지를 비활성화합니다.
    /// </summary>
    private void SetFilledSlots(int filledSlots)
    {
        int safeSlotCount = Mathf.Clamp(_slotCount, 0, _healthImages.Length);
        int clampedFilledSlots = Mathf.Clamp(filledSlots, 0, safeSlotCount);

        for (int slotIndex = 0; slotIndex < _healthImages.Length; slotIndex++)
        {
            Image healthImage = _healthImages[slotIndex];
            if (healthImage == null)
            {
                continue;
            }

            bool shouldEnable = slotIndex < clampedFilledSlots;
            healthImage.enabled = shouldEnable;
        }
    }

    /// <summary>
    /// 실행 중 코루틴을 안전하게 중단합니다.
    /// </summary>
    private void StopRunningCoroutine(ref Coroutine routine)
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }

    /// <summary>
    /// 현재 등록된 Health 리스너를 해제합니다.
    /// </summary>
    private void UnregisterListener()
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
