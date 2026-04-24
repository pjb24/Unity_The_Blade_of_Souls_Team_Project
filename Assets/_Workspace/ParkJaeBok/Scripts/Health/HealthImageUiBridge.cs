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
    [Tooltip("체력이 채워진 슬롯에 사용할 Sprite입니다.")]
    [SerializeField] private Sprite _filledHealthSprite; // 채워진 체력 슬롯 표시용 Sprite 참조입니다.

    [Tooltip("체력이 비어 있는 슬롯에 사용할 Sprite입니다.")]
    [SerializeField] private Sprite _emptyHealthSprite; // 비어 있는 체력 슬롯 표시용 Sprite 참조입니다.

    [Tooltip("체력을 표시할 공용 슬롯 Image 배열입니다. 싱글/멀티(Host/Client) 모두 이 배열을 사용합니다.")]
    [SerializeField] private Image[] _healthImages = new Image[5]; // 체력 표시를 담당하는 Image 슬롯 배열입니다.

    [Tooltip("멀티플레이 Host에서 채워진 슬롯에 사용할 Sprite입니다. 비어 있으면 기본 Filled Sprite를 사용합니다.")]
    [SerializeField] private Sprite _multiplayerHostFilledHealthSprite; // 멀티플레이 Host 채움 슬롯 Sprite 참조입니다.

    [Tooltip("멀티플레이 Host에서 비어 있는 슬롯에 사용할 Sprite입니다. 비어 있으면 기본 Empty Sprite를 사용합니다.")]
    [SerializeField] private Sprite _multiplayerHostEmptyHealthSprite; // 멀티플레이 Host 비움 슬롯 Sprite 참조입니다.

    [Tooltip("멀티플레이 Client에서 채워진 슬롯에 사용할 Sprite입니다. 비어 있으면 기본 Filled Sprite를 사용합니다.")]
    [SerializeField] private Sprite _multiplayerClientFilledHealthSprite; // 멀티플레이 Client 채움 슬롯 Sprite 참조입니다.

    [Tooltip("멀티플레이 Client에서 비어 있는 슬롯에 사용할 Sprite입니다. 비어 있으면 기본 Empty Sprite를 사용합니다.")]
    [SerializeField] private Sprite _multiplayerClientEmptyHealthSprite; // 멀티플레이 Client 비움 슬롯 Sprite 참조입니다.

    [Tooltip("UI 체력 슬롯 수입니다. 배열 길이보다 크면 배열 길이까지만 사용됩니다.")]
    [SerializeField] private int _slotCount = 5; // 체력 UI 슬롯 수입니다.

    [Header("Bind Retry")]
    [Tooltip("로컬 플레이어/Health 생성 지연 시 바인딩 재시도 간격(초)입니다.")]
    [SerializeField] private float _retryInterval = 0.1f; // 바인딩 재시도 간격(초)입니다.

    [Tooltip("로컬 플레이어/Health 생성 지연 시 바인딩 재시도 최대 횟수입니다.")]
    [SerializeField] private int _maxRetryCount = 30; // 바인딩 재시도 최대 횟수입니다.

    private Coroutine _registerCoroutine; // 리스너 등록 지연 처리 코루틴 핸들입니다.
    private bool _isRegistered; // Target HealthComponent에 리스너 등록되어 있는지 추적하는 플래그입니다.
    private bool _hasWarnedMissingFilledSprite; // Filled Sprite 누락 경고 중복 출력을 방지하는 플래그입니다.
    private bool _hasWarnedMissingEmptySprite; // Empty Sprite 누락 경고 중복 출력을 방지하는 플래그입니다.
    private bool _hasWarnedDefaultImageArrayMissing; // 기본 슬롯 Image 배열 누락 경고 중복 출력을 방지하는 플래그입니다.
    private bool _hasWarnedMissingHostFilledSprite; // Host Filled Sprite 폴백 경고 중복 출력을 방지하는 플래그입니다.
    private bool _hasWarnedMissingHostEmptySprite; // Host Empty Sprite 폴백 경고 중복 출력을 방지하는 플래그입니다.
    private bool _hasWarnedMissingClientFilledSprite; // Client Filled Sprite 폴백 경고 중복 출력을 방지하는 플래그입니다.
    private bool _hasWarnedMissingClientEmptySprite; // Client Empty Sprite 폴백 경고 중복 출력을 방지하는 플래그입니다.

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
    /// 사망 이벤트를 받아 모든 슬롯을 비어 있는 상태 Sprite로 갱신합니다.
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

        if (_filledHealthSprite == null)
        {
            Debug.LogWarning($"[HealthImageUiBridge] Filled health sprite is missing on {name}. Filled slot rendering will fallback to existing Image sprite.", this);
            _hasWarnedMissingFilledSprite = true;
        }

        if (_emptyHealthSprite == null)
        {
            Debug.LogWarning($"[HealthImageUiBridge] Empty health sprite is missing on {name}. Empty slot rendering will fallback to existing Image sprite.", this);
            _hasWarnedMissingEmptySprite = true;
        }

        ValidateModeSpriteConfiguration();
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
            if (!_hasWarnedDefaultImageArrayMissing)
            {
                Debug.LogWarning($"[HealthImageUiBridge] Default health image array is missing/empty on {name}. Slot rendering will be skipped.", this);
                _hasWarnedDefaultImageArrayMissing = true;
            }

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
    /// 계산된 슬롯 수를 기준으로 모든 슬롯 Image를 활성화한 뒤 Sprite를 교체합니다.
    /// </summary>
    private void SetFilledSlots(int filledSlots)
    {
        if (_healthImages == null || _healthImages.Length == 0)
        {
            return;
        }

        int safeSlotCount = Mathf.Clamp(_slotCount, 0, _healthImages.Length);
        int clampedFilledSlots = Mathf.Clamp(filledSlots, 0, safeSlotCount);

        for (int slotIndex = 0; slotIndex < _healthImages.Length; slotIndex++)
        {
            Image healthImage = _healthImages[slotIndex];
            if (healthImage == null)
            {
                continue;
            }

            bool isFilledSlot = slotIndex < clampedFilledSlots;
            healthImage.enabled = true;

            Sprite resolvedSprite = ResolveSlotSprite(isFilledSlot); // 현재 슬롯 상태에 적용할 목표 Sprite입니다.
            if (resolvedSprite != null)
            {
                healthImage.sprite = resolvedSprite;
            }
        }
    }

    /// <summary>
    /// 슬롯 상태(채움/비움)에 맞는 Sprite를 반환하고 누락 시 경고를 출력합니다.
    /// </summary>
    private Sprite ResolveSlotSprite(bool isFilledSlot)
    {
        if (isFilledSlot)
        {
            if (TryResolveModeFilledSprite(out Sprite modeFilledSprite))
            {
                return modeFilledSprite;
            }

            if (!_hasWarnedMissingFilledSprite)
            {
                Debug.LogWarning($"[HealthImageUiBridge] Filled health sprite is missing on {name}. Using existing Image sprite as fallback.", this);
                _hasWarnedMissingFilledSprite = true;
            }

            return null;
        }

        if (TryResolveModeEmptySprite(out Sprite modeEmptySprite))
        {
            return modeEmptySprite;
        }

        if (!_hasWarnedMissingEmptySprite)
        {
            Debug.LogWarning($"[HealthImageUiBridge] Empty health sprite is missing on {name}. Using existing Image sprite as fallback.", this);
            _hasWarnedMissingEmptySprite = true;
        }

        return null;
    }

    /// <summary>
    /// 현재 플레이 모드에 맞는 채움 슬롯 Sprite를 해석합니다.
    /// </summary>
    private bool TryResolveModeFilledSprite(out Sprite resolvedSprite)
    {
        resolvedSprite = null;

        E_GamePlayMode playMode = ResolveCurrentPlayMode(); // 현재 런타임 플레이 모드 스냅샷입니다.
        if (playMode == E_GamePlayMode.MultiplayerHost)
        {
            if (_multiplayerHostFilledHealthSprite != null)
            {
                resolvedSprite = _multiplayerHostFilledHealthSprite;
                return true;
            }

            if (!_hasWarnedMissingHostFilledSprite)
            {
                Debug.LogWarning($"[HealthImageUiBridge] MultiplayerHost filled sprite is missing on {name}. Fallback to default filled sprite.", this);
                _hasWarnedMissingHostFilledSprite = true;
            }
        }
        else if (playMode == E_GamePlayMode.MultiplayerClient)
        {
            if (_multiplayerClientFilledHealthSprite != null)
            {
                resolvedSprite = _multiplayerClientFilledHealthSprite;
                return true;
            }

            if (!_hasWarnedMissingClientFilledSprite)
            {
                Debug.LogWarning($"[HealthImageUiBridge] MultiplayerClient filled sprite is missing on {name}. Fallback to default filled sprite.", this);
                _hasWarnedMissingClientFilledSprite = true;
            }
        }

        if (_filledHealthSprite == null)
        {
            return false;
        }

        resolvedSprite = _filledHealthSprite;
        return true;
    }

    /// <summary>
    /// 현재 플레이 모드에 맞는 비움 슬롯 Sprite를 해석합니다.
    /// </summary>
    private bool TryResolveModeEmptySprite(out Sprite resolvedSprite)
    {
        resolvedSprite = null;

        E_GamePlayMode playMode = ResolveCurrentPlayMode(); // 현재 런타임 플레이 모드 스냅샷입니다.
        if (playMode == E_GamePlayMode.MultiplayerHost)
        {
            if (_multiplayerHostEmptyHealthSprite != null)
            {
                resolvedSprite = _multiplayerHostEmptyHealthSprite;
                return true;
            }

            if (!_hasWarnedMissingHostEmptySprite)
            {
                Debug.LogWarning($"[HealthImageUiBridge] MultiplayerHost empty sprite is missing on {name}. Fallback to default empty sprite.", this);
                _hasWarnedMissingHostEmptySprite = true;
            }
        }
        else if (playMode == E_GamePlayMode.MultiplayerClient)
        {
            if (_multiplayerClientEmptyHealthSprite != null)
            {
                resolvedSprite = _multiplayerClientEmptyHealthSprite;
                return true;
            }

            if (!_hasWarnedMissingClientEmptySprite)
            {
                Debug.LogWarning($"[HealthImageUiBridge] MultiplayerClient empty sprite is missing on {name}. Fallback to default empty sprite.", this);
                _hasWarnedMissingClientEmptySprite = true;
            }
        }

        if (_emptyHealthSprite == null)
        {
            return false;
        }

        resolvedSprite = _emptyHealthSprite;
        return true;
    }

    /// <summary>
    /// 플레이 모드 판별을 위해 GameFlowController 상태를 조회합니다.
    /// </summary>
    private E_GamePlayMode ResolveCurrentPlayMode()
    {
        GameFlowController controller = GameFlowController.Instance != null
            ? GameFlowController.Instance
            : FindAnyObjectByType<GameFlowController>(); // 싱글톤 미초기화 시 fallback 탐색 결과입니다.

        if (controller == null)
        {
            return E_GamePlayMode.SinglePlayer;
        }

        return controller.CurrentPlayMode;
    }

    /// <summary>
    /// 플레이 모드별 Sprite 구성을 검사하고 누락 시 경고를 출력합니다.
    /// </summary>
    private void ValidateModeSpriteConfiguration()
    {
        if (_multiplayerHostFilledHealthSprite == null)
        {
            Debug.LogWarning($"[HealthImageUiBridge] MultiplayerHost filled sprite is not assigned on {name}. Default filled sprite fallback can be used.", this);
        }

        if (_multiplayerHostEmptyHealthSprite == null)
        {
            Debug.LogWarning($"[HealthImageUiBridge] MultiplayerHost empty sprite is not assigned on {name}. Default empty sprite fallback can be used.", this);
        }

        if (_multiplayerClientFilledHealthSprite == null)
        {
            Debug.LogWarning($"[HealthImageUiBridge] MultiplayerClient filled sprite is not assigned on {name}. Default filled sprite fallback can be used.", this);
        }

        if (_multiplayerClientEmptyHealthSprite == null)
        {
            Debug.LogWarning($"[HealthImageUiBridge] MultiplayerClient empty sprite is not assigned on {name}. Default empty sprite fallback can be used.", this);
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
