using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로컬 플레이어 Buff 게이지를 UI(Slider/Image/TMP_Text)에 바인딩하는 브리지 컴포넌트입니다.
/// </summary>
public class BuffGaugeUiBridge : MonoBehaviour, IPlayerBuffGaugeListener
{
    [Header("Dependencies")]
    [Tooltip("로컬 플레이어 Buff를 해석해 제공할 Provider 참조입니다. 비어 있으면 런타임에서 자동 탐색/생성합니다.")]
    [SerializeField] private LocalPlayerBuffProvider _localPlayerBuffProvider; // 로컬 플레이어 Buff 해석을 위임할 Provider 참조입니다.

    [Tooltip("로컬 플레이어에서 표시할 PlayerBuffGauge 참조입니다. 비어 있으면 런타임에서 자동 탐색합니다.")]
    [SerializeField] private PlayerBuffGauge _targetGauge; // 현재 UI가 구독 중인 로컬 플레이어 Buff 게이지 참조입니다.

    [Tooltip("현재 대상 게이지와 연결된 PlayerBuffController 참조입니다.")]
    [SerializeField] private PlayerBuffController _targetController; // UI 정책 확인에 사용할 대상 BuffController 참조입니다.

    [Header("UI")]
    [Tooltip("게이지 값을 표시할 Slider입니다.")]
    [SerializeField] private Slider _gaugeSlider; // Buff 게이지를 표시할 Slider 참조입니다.

    [Tooltip("게이지 정규화 값을 표시할 Image(fillAmount)입니다.")]
    [SerializeField] private Image _gaugeFillImage; // Buff 게이지를 표시할 Fill Image 참조입니다.

    [Tooltip("게이지 텍스트를 표시할 TMP_Text입니다.")]
    [SerializeField] private TMP_Text _gaugeText; // Buff 게이지 텍스트를 표시할 TMP_Text 참조입니다.

    [Tooltip("UseBuffGauge=false일 때 UI를 자동 비활성화할지 여부입니다.")]
    [SerializeField] private bool _hideUiWhenGaugeDisabled = true; // 게이지 비사용 설정에서 UI 숨김 여부입니다.

    [Tooltip("게이지 UI를 토글할 루트 오브젝트입니다. 비어 있으면 현재 오브젝트를 사용합니다.")]
    [SerializeField] private GameObject _uiRoot; // 게이지 UI 전체를 활성/비활성할 루트 오브젝트입니다.

    [Header("Availability UI")]
    [Tooltip("Buff 사용 가능/불가능 상태를 스프라이트로 표시할 Image입니다.")]
    [SerializeField] private Image _buffAvailabilityImage; // Buff 사용 가능/불가능 스프라이트를 반영할 Image 참조입니다.

    [Tooltip("Buff 사용 불가능 상태일 때 표시할 스프라이트입니다.")]
    [SerializeField] private Sprite _buffUnavailableSprite; // Buff 사용 불가능 상태에서 사용할 스프라이트입니다.

    [Tooltip("싱글플레이 또는 Host에서 Buff 사용 가능 상태일 때 표시할 스프라이트입니다.")]
    [SerializeField] private Sprite _buffAvailableHostSprite; // Host/싱글플레이 Buff 사용 가능 상태에서 사용할 스프라이트입니다.

    [Tooltip("멀티플레이 Client에서 Buff 사용 가능 상태일 때 표시할 스프라이트입니다.")]
    [SerializeField] private Sprite _buffAvailableClientSprite; // Client Buff 사용 가능 상태에서 사용할 스프라이트입니다.

    [Header("Bind Retry")]
    [Tooltip("로컬 플레이어 Buff 생성 지연 시 재시도 간격(초)입니다.")]
    [SerializeField] private float _retryInterval = 0.1f; // 바인딩 재시도 간격입니다.

    [Tooltip("로컬 플레이어 Buff 생성 지연 시 재시도 최대 횟수입니다.")]
    [SerializeField] private int _maxRetryCount = 30; // 바인딩 재시도 최대 횟수입니다.

    private Coroutine _bindCoroutine; // 바인딩 재시도 코루틴 핸들입니다.
    private bool _isGaugeListenerRegistered; // 게이지 리스너 등록 여부입니다.
    private bool _isBuffStateListenerRegistered; // Buff 활성 상태 변경 리스너 등록 여부입니다.

    /// <summary>
    /// 의존성 보정과 초기 UI 상태 동기화를 수행합니다.
    /// </summary>
    private void Awake()
    {
        ResolveLocalPlayerBuffProvider();
        ApplyGaugeUiVisibilityPolicy();
        RefreshAvailabilitySprite();
    }

    /// <summary>
    /// 활성화 시 Provider 변경 알림을 구독하고 바인딩 코루틴을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveLocalPlayerBuffProvider();

        if (_localPlayerBuffProvider != null)
        {
            _localPlayerBuffProvider.AddLocalBuffChangedListener(HandleLocalBuffChanged);
        }

        RestartBindCoroutine();
    }

    /// <summary>
    /// 비활성화 시 코루틴/리스너를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _bindCoroutine);

        if (_localPlayerBuffProvider != null)
        {
            _localPlayerBuffProvider.RemoveLocalBuffChangedListener(HandleLocalBuffChanged);
        }

        UnbindGauge();
    }

    /// <summary>
    /// 파괴 시 코루틴/리스너를 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _bindCoroutine);

        if (_localPlayerBuffProvider != null)
        {
            _localPlayerBuffProvider.RemoveLocalBuffChangedListener(HandleLocalBuffChanged);
        }

        UnbindGauge();
    }

    /// <summary>
    /// 게이지 변경 알림을 받아 UI를 즉시 갱신합니다.
    /// </summary>
    public void OnBuffGaugeChanged(float currentGauge, float maxGauge, float normalizedGauge)
    {
        RefreshUi(currentGauge, maxGauge, normalizedGauge);
        RefreshAvailabilitySprite();
    }

    /// <summary>
    /// Provider에서 로컬 플레이어 Buff 컨트롤러 참조가 변경되었을 때 재바인딩합니다.
    /// </summary>
    private void HandleLocalBuffChanged(PlayerBuffController localController)
    {
        if (localController == null)
        {
            Debug.LogWarning($"[BuffGaugeUiBridge] Local player BuffController is not available yet. object={name}", this);
            RestartBindCoroutine();
            return;
        }

        TryResolveGaugeFromController(localController, out PlayerBuffGauge localGauge);
        BindGauge(localGauge, localController);
    }

    /// <summary>
    /// 게이지 바인딩 코루틴을 재시작합니다.
    /// </summary>
    private void RestartBindCoroutine()
    {
        StopRunningCoroutine(ref _bindCoroutine);
        _bindCoroutine = StartCoroutine(BindGaugeWhenReadyCoroutine());
    }

    /// <summary>
    /// 로컬 플레이어 게이지를 재시도 기반으로 해석해 바인딩합니다.
    /// </summary>
    private IEnumerator BindGaugeWhenReadyCoroutine()
    {
        int safeRetryCount = Mathf.Max(1, _maxRetryCount); // 재시도 횟수 보정값입니다.
        float safeRetryInterval = Mathf.Max(0.01f, _retryInterval); // 재시도 간격 보정값입니다.

        if (_maxRetryCount < 1 || _retryInterval <= 0f)
        {
            Debug.LogWarning($"[BuffGaugeUiBridge] Invalid retry settings on {name}. Fallback maxRetry={safeRetryCount}, interval={safeRetryInterval}.", this);
        }

        for (int index = 0; index < safeRetryCount; index++)
        {
            if (TryResolveLocalGauge(out PlayerBuffGauge resolvedGauge, out PlayerBuffController resolvedController))
            {
                BindGauge(resolvedGauge, resolvedController);

                if (_isGaugeListenerRegistered)
                {
                    _bindCoroutine = null;
                    yield break;
                }
            }

            if (index == 0)
            {
                Debug.LogWarning($"[BuffGaugeUiBridge] Waiting for local player Buff gauge binding. object={name}", this);
            }

            yield return new WaitForSeconds(safeRetryInterval);
        }

        Debug.LogWarning($"[BuffGaugeUiBridge] Failed to resolve local PlayerBuffGauge after retries. object={name}", this);
        _bindCoroutine = null;
    }

    /// <summary>
    /// LocalPlayerBuffProvider 참조를 자동 보정합니다.
    /// </summary>
    private void ResolveLocalPlayerBuffProvider()
    {
        if (_localPlayerBuffProvider != null)
        {
            return;
        }

        _localPlayerBuffProvider = FindAnyObjectByType<LocalPlayerBuffProvider>();
        if (_localPlayerBuffProvider != null)
        {
            return;
        }

        _localPlayerBuffProvider = gameObject.AddComponent<LocalPlayerBuffProvider>();
        Debug.LogWarning($"[BuffGaugeUiBridge] LocalPlayerBuffProvider was missing. Added runtime provider on {name}.", this);
    }

    /// <summary>
    /// Provider 기반으로 로컬 플레이어 게이지/컨트롤러를 해석합니다.
    /// </summary>
    private bool TryResolveLocalGauge(out PlayerBuffGauge resolvedGauge, out PlayerBuffController resolvedController)
    {
        resolvedGauge = null;
        resolvedController = null;

        ResolveLocalPlayerBuffProvider();
        if (_localPlayerBuffProvider == null)
        {
            Debug.LogWarning($"[BuffGaugeUiBridge] LocalPlayerBuffProvider is missing. object={name}", this);
            return false;
        }

        if (!_localPlayerBuffProvider.TryGetCurrentLocalBuffController(out resolvedController) || resolvedController == null)
        {
            return false;
        }

        return TryResolveGaugeFromController(resolvedController, out resolvedGauge);
    }

    /// <summary>
    /// Buff 컨트롤러에서 게이지 컴포넌트를 해석합니다.
    /// </summary>
    private bool TryResolveGaugeFromController(PlayerBuffController controller, out PlayerBuffGauge gauge)
    {
        gauge = null;
        if (controller == null)
        {
            return false;
        }

        gauge = controller.GetComponent<PlayerBuffGauge>();
        if (gauge != null)
        {
            return true;
        }

        gauge = controller.GetComponentInChildren<PlayerBuffGauge>(true);
        if (gauge != null)
        {
            return true;
        }

        gauge = controller.GetComponentInParent<PlayerBuffGauge>();
        return gauge != null;
    }

    /// <summary>
    /// 대상 게이지를 교체하고 리스너를 등록합니다.
    /// </summary>
    private void BindGauge(PlayerBuffGauge gauge, PlayerBuffController controller)
    {
        if (gauge == null || controller == null)
        {
            return;
        }

        if (_targetController != null && _targetController != controller)
        {
            UnbindControllerStateListener();
        }

        if (_targetGauge != gauge)
        {
            UnbindGauge();
            _targetGauge = gauge;
        }

        _targetController = controller;
        BindControllerStateListener();

        if (!_isGaugeListenerRegistered)
        {
            _targetGauge.AddListener(this);
            _isGaugeListenerRegistered = true;
        }

        ApplyGaugeUiVisibilityPolicy();
        RefreshUi(_targetGauge.CurrentGauge, _targetGauge.MaxGauge, _targetGauge.NormalizedGauge);
        RefreshAvailabilitySprite();
    }

    /// <summary>
    /// 게이지 리스너를 해제합니다.
    /// </summary>
    private void UnbindGauge()
    {
        if (_targetGauge != null && _isGaugeListenerRegistered)
        {
            _targetGauge.RemoveListener(this);
        }

        _isGaugeListenerRegistered = false;
        UnbindControllerStateListener();
        _targetGauge = null;
        _targetController = null;
        RefreshAvailabilitySprite();
    }

    /// <summary>
    /// 대상 BuffController 활성 상태 변경 이벤트를 구독합니다.
    /// </summary>
    private void BindControllerStateListener()
    {
        if (_targetController == null || _isBuffStateListenerRegistered)
        {
            return;
        }

        _targetController.BuffActiveStateChanged += HandleBuffActiveStateChanged;
        _isBuffStateListenerRegistered = true;
    }

    /// <summary>
    /// 대상 BuffController 활성 상태 변경 이벤트 구독을 해제합니다.
    /// </summary>
    private void UnbindControllerStateListener()
    {
        if (_targetController == null || !_isBuffStateListenerRegistered)
        {
            return;
        }

        _targetController.BuffActiveStateChanged -= HandleBuffActiveStateChanged;
        _isBuffStateListenerRegistered = false;
    }

    /// <summary>
    /// Buff 활성 상태가 변경될 때 사용 가능/불가능 스프라이트를 즉시 갱신합니다.
    /// </summary>
    private void HandleBuffActiveStateChanged(bool isBuffActive)
    {
        RefreshAvailabilitySprite();
    }

    /// <summary>
    /// UseBuffGauge 정책에 맞춰 UI 루트 표시 상태를 반영합니다.
    /// </summary>
    private void ApplyGaugeUiVisibilityPolicy()
    {
        if (!_hideUiWhenGaugeDisabled)
        {
            SetUiRootVisible(true);
            return;
        }

        if (_targetController == null)
        {
            SetUiRootVisible(true);
            return;
        }

        if (_uiRoot == null)
        {
            Debug.LogWarning($"[BuffGaugeUiBridge] _uiRoot is null. Visibility toggle is skipped to avoid disabling this bridge object. object={name}", this);
            return;
        }

        SetUiRootVisible(_targetController.IsUsingBuffGauge);
    }

    /// <summary>
    /// UI 컴포넌트에 현재 게이지 값을 반영합니다.
    /// </summary>
    private void RefreshUi(float currentGauge, float maxGauge, float normalizedGauge)
    {
        if (_gaugeSlider != null)
        {
            _gaugeSlider.minValue = 0f;
            _gaugeSlider.maxValue = Mathf.Max(1f, maxGauge);
            _gaugeSlider.value = Mathf.Clamp(currentGauge, 0f, _gaugeSlider.maxValue);
        }

        if (_gaugeFillImage != null)
        {
            _gaugeFillImage.fillAmount = Mathf.Clamp01(normalizedGauge);
        }

        if (_gaugeText != null)
        {
            _gaugeText.text = $"{currentGauge:0}/{maxGauge:0}";
        }
    }

    /// <summary>
    /// Buff 사용 가능/불가능 상태를 계산해 상태 표시 Image 스프라이트를 갱신합니다.
    /// </summary>
    private void RefreshAvailabilitySprite()
    {
        if (_buffAvailabilityImage == null)
        {
            return;
        }

        bool isBuffUsable = EvaluateBuffUsableState(); // 현재 Buff 사용 가능 상태 계산 결과입니다.
        Sprite targetSprite = ResolveAvailabilitySprite(isBuffUsable); // 상태/런타임 역할에 따라 적용할 목표 스프라이트입니다.
        if (targetSprite == null)
        {
            return;
        }

        _buffAvailabilityImage.sprite = targetSprite;
    }

    /// <summary>
    /// 요청된 Buff 사용 가능 상태에 맞는 스프라이트를 반환합니다.
    /// </summary>
    private Sprite ResolveAvailabilitySprite(bool isBuffUsable)
    {
        if (!isBuffUsable)
        {
            return _buffUnavailableSprite;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // 싱글/멀티 및 호스트/클라이언트 판정을 위한 NetworkManager 참조입니다.
        if (networkManager == null || !networkManager.IsListening)
        {
            return _buffAvailableHostSprite;
        }

        if (networkManager.IsHost || networkManager.IsServer)
        {
            return _buffAvailableHostSprite;
        }

        if (networkManager.IsClient)
        {
            return _buffAvailableClientSprite != null ? _buffAvailableClientSprite : _buffAvailableHostSprite;
        }

        return _buffAvailableHostSprite;
    }

    /// <summary>
    /// 현재 Buff 상태와 게이지를 기준으로 사용 가능/불가능 상태를 계산합니다.
    /// </summary>
    private bool EvaluateBuffUsableState()
    {
        if (_targetController == null || _targetGauge == null)
        {
            return false;
        }

        if (_targetController.IsBuffActive)
        {
            return true;
        }

        float minBuffStartGauge = _targetController.MinBuffStartGauge; // Buff 활성 시작 가능 판정 최소 게이지 값입니다.
        return _targetGauge.CurrentGauge >= minBuffStartGauge;
    }

    /// <summary>
    /// 게이지 UI 루트의 활성 상태를 설정합니다.
    /// </summary>
    private void SetUiRootVisible(bool isVisible)
    {
        if (_uiRoot == null)
        {
            return;
        }

        _uiRoot.SetActive(isVisible);
    }

    /// <summary>
    /// 실행 중 코루틴을 안전하게 중지합니다.
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
