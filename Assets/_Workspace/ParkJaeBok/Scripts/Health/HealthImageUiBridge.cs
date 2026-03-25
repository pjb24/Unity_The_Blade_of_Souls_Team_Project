using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HealthComponent 이벤트를 수신해 5칸 Image UI로 체력을 표시하는 브리지 컴포넌트입니다.
/// </summary>
public class HealthImageUiBridge : MonoBehaviour, IHealthListener
{
    [Header("References")]
    [Tooltip("체력 이벤트를 구독할 HealthComponent 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private HealthComponent _targetHealth; // 체력 이벤트를 구독할 HealthComponent 참조입니다.

    [Tooltip("체력을 표시할 5개의 Image 슬롯 배열입니다. 인덱스가 낮을수록 왼쪽(또는 앞) 슬롯으로 사용됩니다.")]
    [SerializeField] private Image[] _healthImages = new Image[5]; // 체력 표시를 담당하는 Image 슬롯 배열입니다.

    [Header("Register Retry")]
    [Tooltip("HealthComponent 참조/초기화 지연 시 리스너 등록 재시도 간격(초)입니다.")]
    [SerializeField] private float _retryInterval = 0.1f; // 리스너 등록 재시도 간격(초)입니다.

    [Tooltip("HealthComponent 참조/초기화 지연 시 리스너 등록 재시도 최대 횟수입니다.")]
    [SerializeField] private int _maxRetryCount = 30; // 리스너 등록 재시도 최대 횟수입니다.

    [Header("Display")]
    [Tooltip("UI 체력 슬롯 수입니다. 기본 요구사항은 5칸입니다.")]
    [SerializeField] private int _slotCount = 5; // 체력 UI 슬롯 수입니다.

    private Coroutine _registerCoroutine; // 리스너 등록 지연 처리 코루틴 핸들입니다.
    private bool _isRegistered; // HealthComponent에 리스너 등록되었는지 여부입니다.

    /// <summary>
    /// 의존성 참조를 보정하고 슬롯 설정 유효성을 검사합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveHealthComponentReference();
        ValidateSlotConfiguration();
    }

    /// <summary>
    /// 활성화 시 등록 코루틴을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        RestartRegisterCoroutine();
    }

    /// <summary>
    /// 비활성화 시 등록 코루틴을 정리하고 리스너를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        UnregisterListener();
    }

    /// <summary>
    /// 오브젝트 파괴 시 코루틴과 리스너를 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        UnregisterListener();
    }

    /// <summary>
    /// 체력 수치 변경 이벤트를 받아 UI 슬롯 표시를 갱신합니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
        RefreshHealthImages(data.CurrentHealth, data.MaxHealth);
    }

    /// <summary>
    /// 데미지 이벤트는 현재 표시 로직에서 별도 처리하지 않습니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// 회복 이벤트는 현재 표시 로직에서 별도 처리하지 않습니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 사망 이벤트를 받아 모든 체력 슬롯을 비활성화합니다.
    /// </summary>
    public void OnDied()
    {
        SetFilledSlots(0);
    }

    /// <summary>
    /// 부활 이벤트를 받아 현재 체력 상태로 UI를 즉시 동기화합니다.
    /// </summary>
    public void OnRevived()
    {
        ForceRefreshFromHealthComponent();
    }

    /// <summary>
    /// 최대 체력 변경 이벤트를 받아 UI 슬롯 표시를 재계산합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
        ForceRefreshFromHealthComponent();
    }

    /// <summary>
    /// 등록 코루틴을 재시작합니다.
    /// </summary>
    private void RestartRegisterCoroutine()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        _registerCoroutine = StartCoroutine(RegisterListenerWhenReadyCoroutine());
    }

    /// <summary>
    /// HealthComponent가 준비될 때까지 대기 후 리스너를 등록합니다.
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
            if (TryResolveHealthComponentReference() && _targetHealth.IsInitialized)
            {
                if (!_isRegistered)
                {
                    _targetHealth.AddListener(this);
                    _isRegistered = true;
                }

                ForceRefreshFromHealthComponent();
                _registerCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[HealthImageUiBridge] Waiting for HealthComponent initialization on {name}.", this);
            }

            yield return new WaitForSeconds(safeRetryInterval);
        }

        Debug.LogWarning($"[HealthImageUiBridge] Listener registration failed after retries on {name}.", this);
        _registerCoroutine = null;
    }

    /// <summary>
    /// HealthComponent 참조를 자동 보정합니다.
    /// </summary>
    private bool TryResolveHealthComponentReference()
    {
        if (_targetHealth != null)
        {
            return true;
        }

        _targetHealth = GetComponent<HealthComponent>();
        if (_targetHealth != null)
        {
            return true;
        }

        _targetHealth = GetComponentInParent<HealthComponent>();
        return _targetHealth != null;
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
    /// HealthComponent의 현재 체력 데이터를 읽어 UI를 즉시 갱신합니다.
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
    /// 현재 등록된 리스너를 해제합니다.
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
