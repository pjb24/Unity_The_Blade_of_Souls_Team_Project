using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 HealthComponent의 현재 체력 비율을 Filled Type Image UI의 fillAmount에 반영하는 UI 브리지입니다.
/// </summary>
[DisallowMultipleComponent]
public class BossHealthFillImageUiBridge : MonoBehaviour, IHealthListener
{
    [Header("Boss Source")]
    [Tooltip("체력 UI와 연결할 보스 HealthComponent입니다. 비어 있으면 BossController 또는 BossHealthNetworkSync에서 자동 탐색합니다.")]
    [SerializeField] private HealthComponent _bossHealthComponent; // UI가 구독할 보스 체력 컴포넌트 참조입니다.

    [Tooltip("보스 HealthComponent 자동 탐색에 사용할 BossController입니다. 비어 있으면 현재 씬에서 자동 탐색합니다.")]
    [SerializeField] private BossController _bossController; // 보스 체력 소스를 찾기 위한 BossController 참조입니다.

    [Tooltip("멀티플레이 Client에서 서버 복제 체력이 적용되는 보스 체력 동기화 컴포넌트입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private BossHealthNetworkSync _bossHealthNetworkSync; // NGO 복제 체력 상태와 연결된 보스 동기화 컴포넌트 참조입니다.

    [Header("UI")]
    [Tooltip("보스 체력 비율을 표시할 Filled Type Image입니다. Inspector에서 원하는 UI Image를 연결합니다.")]
    [SerializeField] private Image _targetFillImage; // fillAmount를 갱신할 보스 체력 Image UI 참조입니다.

    [Tooltip("활성화 시 Image Type을 Filled로 자동 설정할지 여부입니다.")]
    [SerializeField] private bool _configureImageAsFilled = true; // 디자이너가 Image Type 설정을 누락했을 때 자동 보정할지 여부입니다.

    [Tooltip("보스 체력 소스를 찾기 전까지 표시할 fillAmount 값입니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _fallbackFillAmount = 1f; // 체력 소스 연결 전 UI에 표시할 기본 체력 비율입니다.

    [Header("Binding Retry")]
    [Tooltip("보스 오브젝트가 늦게 생성될 때 체력 소스 연결을 다시 시도하는 간격(초)입니다.")]
    [Min(0.01f)]
    [SerializeField] private float _retryInterval = 0.1f; // 보스 체력 소스 재탐색 간격입니다.

    [Tooltip("보스 오브젝트가 늦게 생성될 때 체력 소스 연결을 다시 시도하는 최대 횟수입니다.")]
    [Min(1)]
    [SerializeField] private int _maxRetryCount = 50; // 보스 체력 소스 재탐색 최대 횟수입니다.

    [Tooltip("NGO Client에서는 서버 확정 보스 체력 스냅샷을 우선 사용합니다.")]
    [SerializeField] private bool _preferNetworkSyncOnClient = true; // Client 로컬 HealthComponent보다 BossHealthNetworkSync 스냅샷을 우선 사용할지 여부입니다.

    [Tooltip("이벤트 수신 여부와 무관하게 현재 보스 체력 값을 주기적으로 UI에 다시 반영합니다.")]
    [SerializeField] private bool _enableContinuousRefresh = true; // 빌드 환경에서 이벤트 순서 차이가 생겨도 UI가 현재 체력을 따라가도록 유지할지 여부입니다.

    [Tooltip("지속 갱신 간격입니다. 0이면 매 프레임 갱신합니다.")]
    [Min(0f)]
    [SerializeField] private float _continuousRefreshInterval = 0.05f; // 보스 체력 UI를 현재 체력 값과 다시 맞추는 주기입니다.

    [Header("Debug")]
    [Tooltip("마지막으로 UI에 적용된 보스 체력 비율입니다.")]
    [SerializeField] private float _debugLastAppliedFillAmount = 1f; // Inspector 확인용 최근 적용 fillAmount 값입니다.

    private Coroutine _bindingCoroutine; // 보스 체력 소스 지연 바인딩 코루틴 핸들입니다.
    private float _nextContinuousRefreshTime; // 다음 지속 갱신이 허용되는 Time.unscaledTime 기준 시각입니다.
    private bool _isHealthListenerRegistered; // HealthComponent에 Listener로 등록되었는지 추적하는 플래그입니다.
    private bool _isNetworkSyncListenerRegistered; // BossHealthNetworkSync 체력 스냅샷 이벤트에 등록되었는지 추적하는 플래그입니다.
    private bool _hasWarnedMissingImage; // Image 누락 경고 중복 출력을 막는 플래그입니다.
    private bool _hasWarnedMissingBossHealth; // 보스 체력 누락 경고 중복 출력을 막는 플래그입니다.

    /// <summary>
    /// 현재 UI가 구독 중인 보스 HealthComponent를 반환합니다.
    /// </summary>
    public HealthComponent BossHealthComponent => _bossHealthComponent;

    /// <summary>
    /// 초기화 시 Image 설정을 보정하고 기본 표시값을 적용합니다.
    /// </summary>
    private void Awake()
    {
        ConfigureFillImage();
        ApplyFillAmount(_fallbackFillAmount);
    }

    /// <summary>
    /// 활성화 시 보스 체력 소스 연결을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        ConfigureFillImage();
        RestartBindingCoroutine();
        ForceRefreshFromBestSource();
    }

    /// <summary>
    /// 비활성화 시 체력 이벤트 구독과 지연 바인딩을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        StopBindingCoroutine();
        UnregisterNetworkSyncListener();
        UnregisterHealthListener();
    }

    /// <summary>
    /// 파괴 시 체력 이벤트 구독과 지연 바인딩을 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopBindingCoroutine();
        UnregisterNetworkSyncListener();
        UnregisterHealthListener();
    }

    /// <summary>
    /// 이벤트가 누락되거나 빌드에서 초기화 순서가 달라져도 현재 보스 체력 값을 UI에 계속 반영합니다.
    /// </summary>
    private void Update()
    {
        RefreshContinuouslyIfNeeded();
    }

    /// <summary>
    /// Inspector 값 변경 시 Filled Image 설정과 기본 표시값을 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        _fallbackFillAmount = Mathf.Clamp01(_fallbackFillAmount);
        _continuousRefreshInterval = Mathf.Max(0f, _continuousRefreshInterval);

        if (_targetFillImage != null && _configureImageAsFilled)
        {
            _targetFillImage.type = Image.Type.Filled;
        }
    }

    /// <summary>
    /// 체력 수치 변경을 받아 Filled Image fillAmount를 갱신합니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
        ApplyFillAmount(data.NormalizedHealth);
    }

    /// <summary>
    /// 데미지 이벤트는 체력 변경 이벤트에서 이미 UI가 갱신되므로 별도 처리를 하지 않습니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// 회복 이벤트는 체력 변경 이벤트에서 이미 UI가 갱신되므로 별도 처리를 하지 않습니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 사망 시 체력 UI를 0으로 표시합니다.
    /// </summary>
    public void OnDied()
    {
        ApplyFillAmount(0f);
    }

    /// <summary>
    /// 부활 시 현재 HealthComponent 상태를 기준으로 UI를 다시 동기화합니다.
    /// </summary>
    public void OnRevived()
    {
        ForceRefreshFromHealthComponent();
    }

    /// <summary>
    /// 최대 체력 변경 시 현재 HealthComponent 상태를 기준으로 UI를 다시 동기화합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
        ForceRefreshFromHealthComponent();
    }

    /// <summary>
    /// 외부에서 보스 체력 소스를 직접 교체할 때 사용합니다.
    /// </summary>
    public void BindBossHealth(HealthComponent bossHealthComponent)
    {
        if (bossHealthComponent == null)
        {
            Debug.LogWarning($"[BossHealthFillImageUiBridge] null HealthComponent는 연결할 수 없습니다. object={name}", this);
            return;
        }

        BindBossHealthInternal(bossHealthComponent);
    }

    /// <summary>
    /// 지연 바인딩 코루틴을 재시작합니다.
    /// </summary>
    private void RestartBindingCoroutine()
    {
        StopBindingCoroutine();
        _bindingCoroutine = StartCoroutine(BindBossHealthWhenReadyCoroutine());
    }

    /// <summary>
    /// 보스 체력 소스가 준비될 때까지 연결을 재시도하고, 늦게 스폰되는 네트워크 보스도 계속 탐색합니다.
    /// </summary>
    private IEnumerator BindBossHealthWhenReadyCoroutine()
    {
        int safeRetryCount = Mathf.Max(1, _maxRetryCount);
        float safeRetryInterval = Mathf.Max(0.01f, _retryInterval);
        int retryIndex = 0; // 경고 출력 시점을 제어하기 위한 재시도 횟수입니다.

        while (isActiveAndEnabled)
        {
            TryResolveBossHealthNetworkSync(out BossHealthNetworkSync resolvedNetworkSync);
            if (resolvedNetworkSync != null)
            {
                BindBossHealthNetworkSyncInternal(resolvedNetworkSync);
            }

            if (TryResolveBossHealthComponent(out HealthComponent resolvedHealth))
            {
                BindBossHealthInternal(resolvedHealth);

                if (CanCompleteBinding())
                {
                    _bindingCoroutine = null;
                    yield break;
                }
            }

            retryIndex++;

            if (retryIndex >= safeRetryCount && !_hasWarnedMissingBossHealth)
            {
                Debug.LogWarning($"[BossHealthFillImageUiBridge] 보스 HealthComponent를 아직 찾지 못했습니다. 네트워크 보스가 늦게 스폰될 수 있어 탐색을 계속합니다. object={name}", this);
                _hasWarnedMissingBossHealth = true;
            }

            yield return new WaitForSeconds(safeRetryInterval);
        }

        _bindingCoroutine = null;
    }

    /// <summary>
    /// 지연 바인딩 코루틴을 중단합니다.
    /// </summary>
    private void StopBindingCoroutine()
    {
        if (_bindingCoroutine == null)
        {
            return;
        }

        StopCoroutine(_bindingCoroutine);
        _bindingCoroutine = null;
    }

    /// <summary>
    /// 설정된 주기에 맞춰 현재 사용 가능한 최선의 체력 소스에서 UI를 다시 갱신합니다.
    /// </summary>
    private void RefreshContinuouslyIfNeeded()
    {
        if (!_enableContinuousRefresh)
        {
            return;
        }

        float safeInterval = Mathf.Max(0f, _continuousRefreshInterval); // 디자이너 입력값을 안전한 갱신 주기로 보정한 값입니다.
        if (safeInterval > 0f && Time.unscaledTime < _nextContinuousRefreshTime)
        {
            return;
        }

        _nextContinuousRefreshTime = Time.unscaledTime + safeInterval;

        if (!HasAnyBoundHealthSource() && _bindingCoroutine == null)
        {
            RestartBindingCoroutine();
        }

        ForceRefreshFromBestSource();
    }

    /// <summary>
    /// 현재 구독 또는 참조 중인 체력 소스가 하나라도 있는지 반환합니다.
    /// </summary>
    private bool HasAnyBoundHealthSource()
    {
        return _bossHealthComponent != null || _bossHealthNetworkSync != null || _bossController != null;
    }

    /// <summary>
    /// 새 보스 HealthComponent로 구독 대상을 교체하고 즉시 UI를 동기화합니다.
    /// </summary>
    private void BindBossHealthInternal(HealthComponent newBossHealth)
    {
        if (newBossHealth == null)
        {
            return;
        }

        if (_bossHealthComponent != newBossHealth)
        {
            UnregisterHealthListener();
            _bossHealthComponent = newBossHealth;
        }

        if (!_bossHealthComponent.IsInitialized)
        {
            return;
        }

        if (!_isHealthListenerRegistered)
        {
            _bossHealthComponent.AddListener(this);
            _isHealthListenerRegistered = true;
        }

        ForceRefreshFromHealthComponent();
    }

    /// <summary>
    /// 새 보스 네트워크 체력 동기화 컴포넌트로 구독 대상을 교체하고 즉시 UI를 동기화합니다.
    /// </summary>
    private void BindBossHealthNetworkSyncInternal(BossHealthNetworkSync newNetworkSync)
    {
        if (newNetworkSync == null)
        {
            return;
        }

        if (_bossHealthNetworkSync != newNetworkSync)
        {
            UnregisterNetworkSyncListener();
            _bossHealthNetworkSync = newNetworkSync;
        }

        if (!_isNetworkSyncListenerRegistered)
        {
            _bossHealthNetworkSync.ReplicatedHealthSnapshotChanged += HandleReplicatedHealthSnapshotChanged;
            _isNetworkSyncListenerRegistered = true;
        }

        ForceRefreshFromNetworkSync();
    }

    /// <summary>
    /// 현재 구독 중인 HealthComponent 이벤트를 해제합니다.
    /// </summary>
    private void UnregisterHealthListener()
    {
        if (!_isHealthListenerRegistered)
        {
            return;
        }

        if (_bossHealthComponent != null && _bossHealthComponent.IsInitialized)
        {
            _bossHealthComponent.RemoveListener(this);
        }

        _isHealthListenerRegistered = false;
    }

    /// <summary>
    /// 현재 구독 중인 BossHealthNetworkSync 이벤트를 해제합니다.
    /// </summary>
    private void UnregisterNetworkSyncListener()
    {
        if (!_isNetworkSyncListenerRegistered)
        {
            return;
        }

        if (_bossHealthNetworkSync != null)
        {
            _bossHealthNetworkSync.ReplicatedHealthSnapshotChanged -= HandleReplicatedHealthSnapshotChanged;
        }

        _isNetworkSyncListenerRegistered = false;
    }

    /// <summary>
    /// Inspector 참조와 씬 자동 탐색으로 보스 HealthComponent를 찾습니다.
    /// </summary>
    private bool TryResolveBossHealthComponent(out HealthComponent resolvedHealth)
    {
        resolvedHealth = null;

        if (_bossHealthComponent != null)
        {
            resolvedHealth = _bossHealthComponent;
            return true;
        }

        if (_bossHealthNetworkSync == null)
        {
            TryResolveBossHealthNetworkSync(out _bossHealthNetworkSync);
        }

        if (_bossHealthNetworkSync != null && _bossHealthNetworkSync.BossHealthComponent != null)
        {
            resolvedHealth = _bossHealthNetworkSync.BossHealthComponent;
            return true;
        }

        if (_bossController == null)
        {
            _bossController = FindAnyObjectByType<BossController>();
        }

        if (_bossController != null && _bossController.HealthComponent != null)
        {
            resolvedHealth = _bossController.HealthComponent;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Inspector 참조와 씬 자동 탐색으로 보스 네트워크 체력 동기화 컴포넌트를 찾습니다.
    /// </summary>
    private bool TryResolveBossHealthNetworkSync(out BossHealthNetworkSync resolvedNetworkSync)
    {
        resolvedNetworkSync = _bossHealthNetworkSync;
        if (resolvedNetworkSync != null)
        {
            return true;
        }

        BossHealthNetworkSync[] networkSyncCandidates = FindObjectsByType<BossHealthNetworkSync>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 비활성 또는 지연 스폰 보스도 후보로 포함합니다.
        for (int index = 0; index < networkSyncCandidates.Length; index++)
        {
            BossHealthNetworkSync candidate = networkSyncCandidates[index]; // 탐색된 보스 체력 동기화 후보입니다.
            if (candidate == null)
            {
                continue;
            }

            if (candidate.IsSpawned)
            {
                resolvedNetworkSync = candidate;
                return true;
            }

            if (resolvedNetworkSync == null)
            {
                resolvedNetworkSync = candidate;
            }
        }

        return resolvedNetworkSync != null;
    }

    /// <summary>
    /// 현재 실행 모드에서 보스 체력 바 바인딩을 완료해도 되는지 판단합니다.
    /// </summary>
    private bool CanCompleteBinding()
    {
        if (ShouldRequireNetworkSyncBinding())
        {
            return _isNetworkSyncListenerRegistered;
        }

        return _isHealthListenerRegistered || _isNetworkSyncListenerRegistered;
    }

    /// <summary>
    /// Client 권한에서는 서버 복제 스냅샷 연결이 준비될 때까지 HealthComponent 직접 바인딩만으로 종료하지 않습니다.
    /// </summary>
    private bool ShouldRequireNetworkSyncBinding()
    {
        if (!_preferNetworkSyncOnClient)
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션 역할을 확인하기 위한 NetworkManager 참조입니다.
        return networkManager != null && networkManager.IsListening && !networkManager.IsServer;
    }

    /// <summary>
    /// Image가 Filled 방식으로 동작하도록 필요한 기본 설정을 보정합니다.
    /// </summary>
    private void ConfigureFillImage()
    {
        if (_targetFillImage == null)
        {
            if (!_hasWarnedMissingImage)
            {
                Debug.LogWarning($"[BossHealthFillImageUiBridge] Target Fill Image가 없어 보스 체력 UI를 갱신할 수 없습니다. object={name}", this);
                _hasWarnedMissingImage = true;
            }

            return;
        }

        if (_configureImageAsFilled)
        {
            _targetFillImage.type = Image.Type.Filled;
        }
    }

    /// <summary>
    /// 현재 HealthComponent 상태를 읽어 Filled Image를 즉시 갱신합니다.
    /// </summary>
    private void ForceRefreshFromHealthComponent()
    {
        if (_bossHealthComponent == null || !_bossHealthComponent.IsInitialized)
        {
            return;
        }

        ApplyFillAmount(_bossHealthComponent.GetHealthNormalized());
    }

    /// <summary>
    /// 현재 실행 역할에 맞는 최선의 체력 소스를 선택해 Filled Image를 즉시 갱신합니다.
    /// </summary>
    private void ForceRefreshFromBestSource()
    {
        if (ShouldUseNetworkSyncForDisplay() && TryRefreshFromNetworkSync())
        {
            return;
        }

        if (TryRefreshFromHealthComponent())
        {
            return;
        }

        if (TryRefreshFromNetworkSync())
        {
            return;
        }

        if (_bossController != null && _bossController.HealthComponent != null)
        {
            BindBossHealthInternal(_bossController.HealthComponent);
            TryRefreshFromHealthComponent();
        }
    }

    /// <summary>
    /// HealthComponent에서 직접 체력 비율을 읽어 UI 갱신을 시도합니다.
    /// </summary>
    private bool TryRefreshFromHealthComponent()
    {
        if (_bossHealthComponent == null)
        {
            TryResolveBossHealthComponent(out _bossHealthComponent);
        }

        if (_bossHealthComponent == null || !_bossHealthComponent.IsInitialized)
        {
            return false;
        }

        ApplyFillAmount(_bossHealthComponent.GetHealthNormalized());
        return true;
    }

    /// <summary>
    /// BossHealthNetworkSync의 서버 확정 스냅샷을 기준으로 Filled Image를 즉시 갱신합니다.
    /// </summary>
    private void ForceRefreshFromNetworkSync()
    {
        if (_bossHealthNetworkSync == null || _bossHealthNetworkSync.ReplicatedHealthRevision <= 0)
        {
            return;
        }

        ApplyFillAmount(_bossHealthNetworkSync.ReplicatedNormalizedHealth);
    }

    /// <summary>
    /// BossHealthNetworkSync의 서버 확정 체력 스냅샷으로 UI 갱신을 시도합니다.
    /// </summary>
    private bool TryRefreshFromNetworkSync()
    {
        if (_bossHealthNetworkSync == null)
        {
            TryResolveBossHealthNetworkSync(out _bossHealthNetworkSync);
        }

        if (_bossHealthNetworkSync == null || _bossHealthNetworkSync.ReplicatedHealthRevision <= 0)
        {
            return false;
        }

        ApplyFillAmount(_bossHealthNetworkSync.ReplicatedNormalizedHealth);
        return true;
    }

    /// <summary>
    /// 현재 역할에서 UI 표시 기준으로 BossHealthNetworkSync를 우선해야 하는지 판단합니다.
    /// </summary>
    private bool ShouldUseNetworkSyncForDisplay()
    {
        return ShouldRequireNetworkSyncBinding();
    }

    /// <summary>
    /// 네트워크로 복제된 보스 체력 스냅샷 변경을 받아 Filled Image fillAmount를 갱신합니다.
    /// </summary>
    private void HandleReplicatedHealthSnapshotChanged()
    {
        ForceRefreshFromNetworkSync();
    }

    /// <summary>
    /// 계산된 체력 비율을 Filled Image fillAmount에 적용합니다.
    /// </summary>
    private void ApplyFillAmount(float normalizedHealth)
    {
        float safeFillAmount = Mathf.Clamp01(normalizedHealth);
        _debugLastAppliedFillAmount = safeFillAmount;

        if (_targetFillImage == null)
        {
            return;
        }

        _targetFillImage.fillAmount = safeFillAmount;
    }
}
