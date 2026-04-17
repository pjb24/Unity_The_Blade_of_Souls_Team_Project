using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라 효과 요청의 재생/갱신/종료를 단일 진입점으로 관리하는 중앙 런타임 시스템입니다.
/// </summary>
[DisallowMultipleComponent]
public class CameraEffectManager : MonoBehaviour
{
    private enum E_ChannelConflictPolicy
    {
        ReplaceExisting = 0
    }

    private enum E_LowPriorityPolicy
    {
        Ignore = 0,
        WeakenAndPlay = 1
    }

    private struct ChannelRuntimeState
    {
        public bool IsPlaying; // 현재 채널에 활성 효과가 존재하는지 여부입니다.
        public int Token; // 채널 활성 효과를 식별하는 토큰 값입니다.
        public string EffectName; // 디버그/로그에 사용할 활성 효과 이름입니다.
        public float ElapsedSeconds; // 활성 효과 누적 재생 시간입니다.
        public float DurationSeconds; // 활성 효과 지속 시간 기준값입니다.
        public int Priority; // 활성 효과 우선순위 값입니다.
        public CameraEffectPresetBase.E_CameraEffectLifecycleMode LifecycleMode; // 활성 효과 수명주기 타입입니다.
        public CameraEffectRequest RuntimeRequest; // 활성 효과 실행에 사용하는 런타임 요청 스냅샷입니다.
        public CameraEffectPresetBase RuntimePresetInstance; // 채널 독립 실행을 위한 런타임 프리셋 인스턴스입니다.
    }

    private struct AccessibilityRuntimeState
    {
        public float ShakeScale; // 흔들림 모듈에 적용할 접근성 강도 배율(0~1)입니다.
        public float FlashScale; // 플래시/페이드 계열 모듈에 적용할 접근성 강도 배율(0~1)입니다.
        public float ColorScale; // 색상 변화 계열 모듈에 적용할 접근성 강도 배율(0~1)입니다.
        public bool IsUsingOptionSnapshot; // OptionManager 스냅샷 기반 배율인지 여부입니다.
    }

    private static readonly E_CameraEffectChannel[] ChannelValues =
    {
        E_CameraEffectChannel.System,
        E_CameraEffectChannel.Combat,
        E_CameraEffectChannel.Ability,
        E_CameraEffectChannel.Movement,
        E_CameraEffectChannel.Warning
    }; // Update 순회/초기화에 사용할 고정 채널 목록입니다.

    [Header("Dependencies")]
    [Tooltip("효과를 적용할 대상 카메라입니다. 비어 있으면 Main Camera를 자동 탐색합니다.")]
    [SerializeField] private Camera _targetCamera; // 효과를 적용할 대상 카메라 참조입니다.

    [Tooltip("씬 전환 시 매니저를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // 씬 전환 간 매니저 유지 여부를 제어하는 플래그입니다.

    [Header("Channel")]
    [Tooltip("동일 채널에 이미 효과가 재생 중일 때 충돌 처리 정책입니다.")]
    [SerializeField] private E_ChannelConflictPolicy _channelConflictPolicy = E_ChannelConflictPolicy.ReplaceExisting; // 동일 채널 충돌 시 처리 정책입니다.

    [Header("Priority")]
    [Tooltip("낮은 우선순위 요청이 높은 우선순위 요청과 충돌할 때 처리 정책입니다.")]
    [SerializeField] private E_LowPriorityPolicy _lowPriorityPolicy = E_LowPriorityPolicy.Ignore; // 낮은 우선순위 충돌 시 처리 정책입니다.

    [Tooltip("WeakenAndPlay 정책에서 낮은 우선순위 효과 Duration에 곱할 배율입니다.")]
    [SerializeField] private float _lowPriorityDurationScale = 0.5f; // 낮은 우선순위 약화 재생 시 지속 시간 축소 배율입니다.

    [Header("Lifecycle Guard")]
    [Tooltip("Loop 효과 종료 누락 방지용 안전 타임아웃을 사용할지 여부입니다.")]
    [SerializeField] private bool _enableLoopSafetyTimeout = true; // Loop 효과 종료 누락 방지 타임아웃 활성화 여부입니다.

    [Tooltip("Loop 효과가 이 시간(초)을 초과하면 누락 방지 로직이 Warning과 함께 자동 종료합니다.")]
    [SerializeField] private float _loopSafetyTimeoutSeconds = 30f; // Loop 효과 자동 종료를 강제할 안전 타임아웃 값입니다.

    [Header("Accessibility")]
    [Tooltip("접근성 옵션(흔들림/플래시/색상 변화)을 CameraEffect 요청에 반영할지 여부입니다.")]
    [SerializeField] private bool _enableAccessibilityIntegration = true; // 접근성 시스템 연동 활성화 여부입니다.

    [Tooltip("FlashReduction이 On일 때 Fade/Overlay 강도에 곱할 배율입니다.")]
    [SerializeField] private float _flashScaleWhenReductionOn = 0.35f; // FlashReduction On 적용 시 플래시 계열 효과 강도 배율입니다.

    [Tooltip("ColorBlindMode가 Off가 아닐 때 색상 변화 강도에 곱할 배율입니다.")]
    [SerializeField] private float _colorScaleWhenColorBlindModeOn = 0.7f; // 색약 보정 모드 활성 시 색상 변화 강도 배율입니다.

    [Tooltip("HighContrastMode가 On일 때 색상 변화 강도에 곱할 배율입니다.")]
    [SerializeField] private float _colorScaleWhenHighContrastOn = 0.8f; // 고대비 모드 활성 시 색상 변화 강도 배율입니다.

    [Tooltip("OptionManager를 찾지 못했을 때 사용할 흔들림 강도 폴백 배율입니다.")]
    [SerializeField] private float _fallbackShakeScale = 1f; // OptionManager 미연결 시 흔들림 강도 폴백 배율입니다.

    [Tooltip("OptionManager를 찾지 못했을 때 사용할 플래시 강도 폴백 배율입니다.")]
    [SerializeField] private float _fallbackFlashScale = 1f; // OptionManager 미연결 시 플래시 강도 폴백 배율입니다.

    [Tooltip("OptionManager를 찾지 못했을 때 사용할 색상 변화 강도 폴백 배율입니다.")]
    [SerializeField] private float _fallbackColorScale = 1f; // OptionManager 미연결 시 색상 변화 강도 폴백 배율입니다.

    [Header("Debug")]
    [Tooltip("카메라 효과 수명주기 로그를 상세 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLogging; // 상세 디버그 로그 출력 여부를 제어하는 플래그입니다.

    [Tooltip("디버그용: 현재 활성 효과 개수입니다.")]
    [SerializeField] private int _activeChannelEffectCount; // 현재 재생 중인 채널 활성 효과 개수입니다.

    [Tooltip("디버그용: 마지막으로 재생을 시작한 채널입니다.")]
    [SerializeField] private E_CameraEffectChannel _lastPlayedChannel = E_CameraEffectChannel.System; // 최근 재생 시작 채널 디버그 값입니다.

    [Tooltip("디버그용: 현재 접근성 흔들림 스케일 값입니다.")]
    [SerializeField] private float _debugAccessibilityShakeScale = 1f; // 현재 접근성 흔들림 스케일 디버그 값입니다.

    [Tooltip("디버그용: 현재 접근성 플래시 스케일 값입니다.")]
    [SerializeField] private float _debugAccessibilityFlashScale = 1f; // 현재 접근성 플래시 스케일 디버그 값입니다.

    [Tooltip("디버그용: 현재 접근성 색상 스케일 값입니다.")]
    [SerializeField] private float _debugAccessibilityColorScale = 1f; // 현재 접근성 색상 스케일 디버그 값입니다.

    [Tooltip("디버그용: 접근성 스케일의 OptionManager 연동 여부입니다.")]
    [SerializeField] private bool _debugUsingOptionAccessibility; // 현재 접근성 스케일이 OptionManager 스냅샷 기반인지 여부입니다.

    private readonly Dictionary<E_CameraEffectChannel, ChannelRuntimeState> _runtimeByChannel = new Dictionary<E_CameraEffectChannel, ChannelRuntimeState>(); // 채널별 활성 런타임 상태 맵입니다.
    private readonly Dictionary<int, E_CameraEffectChannel> _channelByToken = new Dictionary<int, E_CameraEffectChannel>(); // handle token -> 채널 매핑입니다.
    private int _nextToken = 1; // 다음 활성 효과에 할당할 토큰 값입니다.
    private OptionManager _optionManager; // 접근성 옵션 스냅샷 수신에 사용할 OptionManager 참조입니다.
    private bool _accessibilityListenerBound; // OptionManager 리스너가 연결된 상태인지 여부입니다.
    private AccessibilityRuntimeState _accessibilityRuntimeState; // 현재 접근성 옵션 기반 스케일 스냅샷입니다.
    private float _nextOptionManagerBindRetryTime; // OptionManager 지연 생성 상황에서 재바인드 시도할 다음 시간(초)입니다.
    private bool _didWarnOptionManagerMissing; // OptionManager 미존재 경고 중복 출력을 억제하기 위한 상태값입니다.

    /// <summary>
    /// 싱글톤 스타일 접근을 위한 런타임 인스턴스입니다.
    /// </summary>
    public static CameraEffectManager Instance { get; private set; }

    /// <summary>
    /// 매니저 초기화와 필수 의존성 확인을 수행합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyLifecycleState,
                "중복 CameraEffectManager 인스턴스를 제거합니다."), this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        InitializeChannelStates();
        ResolveTargetCamera("Awake");
        ValidateLifecycleGuardSettings();
        ValidatePrioritySettings();
        ValidateAccessibilitySettings();
        ApplyAccessibilityFallback("Awake");
    }

    /// <summary>
    /// 첫 프레임에서 접근성 옵션 매니저를 연결하고 초기 스냅샷을 반영합니다.
    /// </summary>
    private void Start()
    {
        TryBindOptionManager("Start");
    }

    /// <summary>
    /// 매 프레임 채널별 활성 효과를 갱신하고 종료 조건을 평가합니다.
    /// </summary>
    private void Update()
    {
        if (_enableAccessibilityIntegration && _optionManager == null && Time.unscaledTime >= _nextOptionManagerBindRetryTime)
        {
            _nextOptionManagerBindRetryTime = Time.unscaledTime + 2f;
            TryBindOptionManager("UpdateRetry");
        }

        for (int index = 0; index < ChannelValues.Length; index++)
        {
            E_CameraEffectChannel channel = ChannelValues[index];
            if (!_runtimeByChannel.TryGetValue(channel, out ChannelRuntimeState state) || !state.IsPlaying)
            {
                continue;
            }

            if (state.RuntimePresetInstance == null)
            {
                Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                    CameraEffectWarningCatalog.KeyLifecycleState,
                    $"채널 활성 프리셋 인스턴스가 null이라 즉시 종료합니다. channel={channel}"), this);
                StopChannelEffectInternal(channel, "RuntimePresetInstanceMissing");
                continue;
            }

            state.ElapsedSeconds += Time.deltaTime;
            float normalizedTime; // 프리셋 업데이트에 전달할 0~1 시간 값입니다.
            if (state.LifecycleMode == CameraEffectPresetBase.E_CameraEffectLifecycleMode.Loop && state.DurationSeconds > 0f)
            {
                normalizedTime = Mathf.Repeat(state.ElapsedSeconds, state.DurationSeconds) / state.DurationSeconds;
            }
            else
            {
                normalizedTime = state.DurationSeconds > 0f
                    ? Mathf.Clamp01(state.ElapsedSeconds / state.DurationSeconds)
                    : 1f; // 분모 0 방지를 포함한 정규화 시간 값입니다.
            }

            state.RuntimePresetInstance.OnEffectUpdated(_targetCamera, state.RuntimeRequest, state.ElapsedSeconds, normalizedTime);
            _runtimeByChannel[channel] = state;

            if (state.LifecycleMode == CameraEffectPresetBase.E_CameraEffectLifecycleMode.Timed && state.ElapsedSeconds >= state.DurationSeconds)
            {
                StopChannelEffectInternal(channel, "TimedDurationCompleted");
                continue;
            }

            if (state.LifecycleMode == CameraEffectPresetBase.E_CameraEffectLifecycleMode.Loop)
            {
                TryApplyLoopSafetyGuard(channel, state);
            }
        }

        RefreshActiveChannelCountDebugValue();
    }

    /// <summary>
    /// 파괴 시점에 모든 채널 활성 효과를 안전 종료하고 인스턴스 참조를 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        for (int index = 0; index < ChannelValues.Length; index++)
        {
            StopChannelEffectInternal(ChannelValues[index], "ManagerDestroyed");
        }

        if (_accessibilityListenerBound && _optionManager != null)
        {
            _optionManager.RemoveListener(OnOptionSnapshotChanged);
            _accessibilityListenerBound = false;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 프리셋만 전달받아 간단 재생 요청을 수행합니다.
    /// </summary>
    public CameraEffectHandle Play(CameraEffectPresetBase preset, GameObject requester = null)
    {
        CameraEffectRequest request = CameraEffectRequest.Create(preset, requester);
        return Play(request);
    }

    /// <summary>
    /// 요청 구조체 기반 카메라 효과 재생을 시작합니다.
    /// </summary>
    public CameraEffectHandle Play(in CameraEffectRequest request)
    {
        if (!ValidateRequest(request, out string rejectReason))
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyInvalidRequest,
                CameraEffectWarningCatalog.BuildPlayRejected(rejectReason)), this);
            return new CameraEffectHandle(this, 0);
        }

        CameraEffectRequest mutableRequest = request;
        ApplyAccessibilityToRequest(ref mutableRequest);
        E_CameraEffectChannel targetChannel = ResolveTargetChannel(mutableRequest);
        if (!ResolvePriorityConflict(targetChannel, ref mutableRequest))
        {
            return new CameraEffectHandle(this, 0);
        }

        CameraEffectPresetBase runtimePresetInstance = ScriptableObject.Instantiate(mutableRequest.Preset); // 채널 독립 실행을 위한 프리셋 런타임 복제 인스턴스입니다.
        CameraEffectRequest runtimeRequest = mutableRequest;
        runtimeRequest.Preset = runtimePresetInstance;

        ChannelRuntimeState newState = new ChannelRuntimeState();
        newState.IsPlaying = true;
        newState.Token = GenerateNextToken();
        newState.EffectName = string.IsNullOrWhiteSpace(runtimePresetInstance.EffectName) ? runtimePresetInstance.name : runtimePresetInstance.EffectName;
        newState.ElapsedSeconds = 0f;
        newState.DurationSeconds = runtimePresetInstance.ResolveDurationSeconds(runtimeRequest);
        newState.Priority = runtimePresetInstance.ResolvePriority(runtimeRequest);
        newState.LifecycleMode = runtimePresetInstance.ResolveLifecycleMode(runtimeRequest);
        newState.RuntimeRequest = runtimeRequest;
        newState.RuntimePresetInstance = runtimePresetInstance;

        if (newState.LifecycleMode == CameraEffectPresetBase.E_CameraEffectLifecycleMode.Timed && newState.DurationSeconds <= 0f)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyInvalidPresetData,
                $"Timed 효과인데 지속 시간이 0 이하입니다. duration={newState.DurationSeconds}. 0.01초로 폴백합니다. channel={targetChannel}"), this);
            newState.DurationSeconds = 0.01f;
        }

        if (newState.LifecycleMode == CameraEffectPresetBase.E_CameraEffectLifecycleMode.Loop && newState.DurationSeconds <= 0f)
        {
            newState.DurationSeconds = 1f;
        }

        _runtimeByChannel[targetChannel] = newState;
        _channelByToken[newState.Token] = targetChannel;
        _lastPlayedChannel = targetChannel;

        newState.RuntimePresetInstance.OnEffectStarted(_targetCamera, newState.RuntimeRequest);

        if (newState.LifecycleMode == CameraEffectPresetBase.E_CameraEffectLifecycleMode.Instant)
        {
            newState.RuntimePresetInstance.OnEffectUpdated(_targetCamera, newState.RuntimeRequest, 0f, 1f);
            StopChannelEffectInternal(targetChannel, "InstantCompleted");
            return new CameraEffectHandle(this, 0);
        }

        if (_verboseLogging)
        {
            Debug.Log($"[CameraEffectManager] Play start. token={newState.Token}, effect={newState.EffectName}, channel={targetChannel}, priority={newState.Priority}, lifecycle={newState.LifecycleMode}, duration={newState.DurationSeconds}", this);
        }

        RefreshActiveChannelCountDebugValue();
        return new CameraEffectHandle(this, newState.Token);
    }

    /// <summary>
    /// 모든 채널의 활성 효과를 종료합니다.
    /// </summary>
    public bool StopActiveEffect(string reason = "ManualStop")
    {
        bool stoppedAny = false; // 호출 중 최소 1개 채널 효과 종료 성공 여부입니다.
        for (int index = 0; index < ChannelValues.Length; index++)
        {
            if (StopChannelEffectInternal(ChannelValues[index], reason))
            {
                stoppedAny = true;
            }
        }

        if (!stoppedAny)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyLifecycleState,
                "활성 효과가 없어 Stop 요청을 무시합니다."), this);
        }

        return stoppedAny;
    }

    /// <summary>
    /// 지정 채널의 활성 효과를 종료합니다.
    /// </summary>
    public bool StopChannelEffect(E_CameraEffectChannel channel, string reason = "ManualStopByChannel")
    {
        return StopChannelEffectInternal(channel, reason);
    }

    /// <summary>
    /// 특정 핸들 토큰이 현재 유효한 채널 활성 효과를 가리키는지 판별합니다.
    /// </summary>
    public bool IsHandleValid(int token)
    {
        if (token <= 0)
        {
            return false;
        }

        if (!_channelByToken.TryGetValue(token, out E_CameraEffectChannel channel))
        {
            return false;
        }

        if (!_runtimeByChannel.TryGetValue(channel, out ChannelRuntimeState state))
        {
            return false;
        }

        return state.IsPlaying && state.Token == token;
    }

    /// <summary>
    /// 핸들 토큰을 통해 해당 채널의 활성 효과 종료를 시도합니다.
    /// </summary>
    public bool StopByHandle(int token, string reason)
    {
        if (!_channelByToken.TryGetValue(token, out E_CameraEffectChannel channel))
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyInvalidRequest,
                $"유효하지 않은 handle token으로 Stop 요청이 들어왔습니다. token={token}"), this);
            return false;
        }

        if (!_runtimeByChannel.TryGetValue(channel, out ChannelRuntimeState state) || !state.IsPlaying || state.Token != token)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyInvalidRequest,
                $"handle token과 채널 활성 상태가 일치하지 않습니다. token={token}, channel={channel}"), this);
            return false;
        }

        return StopChannelEffectInternal(channel, $"{reason}.token={token}");
    }

    /// <summary>
    /// 요청 유효성을 검증하고 실패 사유를 반환합니다.
    /// </summary>
    private bool ValidateRequest(in CameraEffectRequest request, out string rejectReason)
    {
        rejectReason = string.Empty;

        if (request.Preset == null)
        {
            rejectReason = "preset is null";
            return false;
        }

        if (!ResolveTargetCamera("Play"))
        {
            rejectReason = "target camera is missing";
            return false;
        }

        E_CameraEffectChannel targetChannel = ResolveTargetChannel(request);
        if (!Enum.IsDefined(typeof(E_CameraEffectChannel), targetChannel))
        {
            rejectReason = $"channel is not defined. channel={targetChannel}";
            return false;
        }

        CameraEffectPresetBase.E_CameraEffectLifecycleMode lifecycleMode = request.Preset.ResolveLifecycleMode(request);
        if (request.UseDurationOverride && request.DurationOverrideSeconds <= 0f && lifecycleMode == CameraEffectPresetBase.E_CameraEffectLifecycleMode.Timed)
        {
            rejectReason = $"duration override must be > 0 for timed preset. override={request.DurationOverrideSeconds}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 요청에서 채널 오버라이드를 우선 적용해 최종 채널을 계산합니다.
    /// </summary>
    private E_CameraEffectChannel ResolveTargetChannel(in CameraEffectRequest request)
    {
        if (request.UseChannelOverride)
        {
            return request.Channel;
        }

        return request.Preset.DefaultChannel;
    }

    /// <summary>
    /// 채널 충돌 상태에서 우선순위를 비교해 요청 처리 여부를 결정합니다.
    /// </summary>
    private bool ResolvePriorityConflict(E_CameraEffectChannel channel, ref CameraEffectRequest request)
    {
        if (!_runtimeByChannel.TryGetValue(channel, out ChannelRuntimeState existingState) || !existingState.IsPlaying)
        {
            return true;
        }

        int incomingPriority = request.Preset.ResolvePriority(request);
        int existingPriority = existingState.Priority;

        if (incomingPriority >= existingPriority)
        {
            HandleChannelConflict(channel);
            return true;
        }

        if (_lowPriorityPolicy == E_LowPriorityPolicy.Ignore)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyPriorityState,
                $"낮은 우선순위 요청을 무시합니다. channel={channel}, incoming={incomingPriority}, existing={existingPriority}, policy={_lowPriorityPolicy}"), this);
            return false;
        }

        if (_lowPriorityPolicy == E_LowPriorityPolicy.WeakenAndPlay)
        {
            CameraEffectPresetBase.E_CameraEffectLifecycleMode lifecycleMode = request.Preset.ResolveLifecycleMode(request);
            if (lifecycleMode == CameraEffectPresetBase.E_CameraEffectLifecycleMode.Loop)
            {
                Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                    CameraEffectWarningCatalog.KeyPriorityState,
                    $"Loop 낮은 우선순위 요청은 약화 재생을 지원하지 않아 무시합니다. channel={channel}, incoming={incomingPriority}, existing={existingPriority}"), this);
                return false;
            }

            float baseDuration = request.Preset.ResolveDurationSeconds(request);
            if (baseDuration <= 0f)
            {
                baseDuration = 0.01f;
            }

            request.UseDurationOverride = true;
            request.DurationOverrideSeconds = Mathf.Max(0.01f, baseDuration * _lowPriorityDurationScale);

            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyPriorityState,
                $"낮은 우선순위 요청을 약화해 재생합니다. channel={channel}, incoming={incomingPriority}, existing={existingPriority}, weakenedDuration={request.DurationOverrideSeconds:F3}"), this);

            HandleChannelConflict(channel);
            return true;
        }

        Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
            CameraEffectWarningCatalog.KeyPriorityState,
            $"알 수 없는 낮은 우선순위 정책으로 요청을 무시합니다. channel={channel}, policy={_lowPriorityPolicy}"), this);
        return false;
    }

    /// <summary>
    /// 동일 채널 효과 충돌 시 정책에 맞게 기존 활성 효과를 처리합니다.
    /// </summary>
    private void HandleChannelConflict(E_CameraEffectChannel channel)
    {
        if (_channelConflictPolicy == E_ChannelConflictPolicy.ReplaceExisting)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyChannelState,
                $"동일 채널 충돌로 기존 효과를 교체합니다. channel={channel}, policy={_channelConflictPolicy}"), this);
            StopChannelEffectInternal(channel, "ChannelConflictReplace");
            return;
        }

        Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
            CameraEffectWarningCatalog.KeyChannelState,
            $"정의되지 않은 채널 충돌 정책으로 교체 폴백을 수행합니다. channel={channel}, policy={_channelConflictPolicy}"), this);
        StopChannelEffectInternal(channel, "UnknownConflictPolicyFallback");
    }

    /// <summary>
    /// Loop 효과 종료 누락 방지를 위한 안전 타임아웃 종료 조건을 평가합니다.
    /// </summary>
    private void TryApplyLoopSafetyGuard(E_CameraEffectChannel channel, in ChannelRuntimeState state)
    {
        if (!_enableLoopSafetyTimeout)
        {
            return;
        }

        if (state.ElapsedSeconds < _loopSafetyTimeoutSeconds)
        {
            return;
        }

        Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
            CameraEffectWarningCatalog.KeyLifecycleState,
            $"Loop 효과 종료 누락이 감지되어 자동 종료합니다. channel={channel}, effect={state.EffectName}, elapsed={state.ElapsedSeconds:F2}, timeout={_loopSafetyTimeoutSeconds:F2}"), this);
        StopChannelEffectInternal(channel, "LoopSafetyTimeout");
    }

    /// <summary>
    /// Lifecycle Guard 설정값 이상치를 보정합니다.
    /// </summary>
    private void ValidateLifecycleGuardSettings()
    {
        if (_loopSafetyTimeoutSeconds > 0f)
        {
            return;
        }

        Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
            CameraEffectWarningCatalog.KeyInvalidPresetData,
            $"LoopSafetyTimeout 값이 0 이하라 30초로 폴백합니다. value={_loopSafetyTimeoutSeconds}"), this);
        _loopSafetyTimeoutSeconds = 30f;
    }

    /// <summary>
    /// 우선순위 정책 관련 설정값 이상치를 보정합니다.
    /// </summary>
    private void ValidatePrioritySettings()
    {
        if (_lowPriorityDurationScale > 0f)
        {
            return;
        }

        Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
            CameraEffectWarningCatalog.KeyPriorityState,
            $"LowPriorityDurationScale 값이 0 이하라 0.5로 폴백합니다. value={_lowPriorityDurationScale}"), this);
        _lowPriorityDurationScale = 0.5f;
    }

    /// <summary>
    /// 접근성 관련 Inspector 설정값 이상치를 검증하고 안전 범위로 보정합니다.
    /// </summary>
    private void ValidateAccessibilitySettings()
    {
        _flashScaleWhenReductionOn = ClampAndWarn01("FlashScaleWhenReductionOn", _flashScaleWhenReductionOn);
        _colorScaleWhenColorBlindModeOn = ClampAndWarn01("ColorScaleWhenColorBlindModeOn", _colorScaleWhenColorBlindModeOn);
        _colorScaleWhenHighContrastOn = ClampAndWarn01("ColorScaleWhenHighContrastOn", _colorScaleWhenHighContrastOn);
        _fallbackShakeScale = ClampAndWarn01("FallbackShakeScale", _fallbackShakeScale);
        _fallbackFlashScale = ClampAndWarn01("FallbackFlashScale", _fallbackFlashScale);
        _fallbackColorScale = ClampAndWarn01("FallbackColorScale", _fallbackColorScale);
    }

    /// <summary>
    /// 대상 카메라 참조를 확인하고 필요 시 Main Camera로 자동 해석합니다.
    /// </summary>
    private bool ResolveTargetCamera(string context)
    {
        if (_targetCamera != null)
        {
            return true;
        }

        _targetCamera = Camera.main;
        if (_targetCamera != null)
        {
            if (_verboseLogging)
            {
                Debug.Log($"[CameraEffectManager] TargetCamera를 Main Camera로 자동 해석했습니다. context={context}, camera={_targetCamera.name}", this);
            }

            return true;
        }

        Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
            CameraEffectWarningCatalog.KeyDependencyMissing,
            $"Target Camera가 비어 있고 Main Camera도 찾지 못했습니다. context={context}"), this);
        return false;
    }

    /// <summary>
    /// 채널 상태 맵을 기본값으로 초기화합니다.
    /// </summary>
    private void InitializeChannelStates()
    {
        _runtimeByChannel.Clear();
        _channelByToken.Clear();

        for (int index = 0; index < ChannelValues.Length; index++)
        {
            _runtimeByChannel[ChannelValues[index]] = default;
        }

        _activeChannelEffectCount = 0;
    }

    /// <summary>
    /// OptionManager를 찾아 접근성 옵션 리스너를 연결하고 즉시 현재 값을 적용합니다.
    /// </summary>
    private void TryBindOptionManager(string context)
    {
        if (!_enableAccessibilityIntegration)
        {
            return;
        }

        if (_optionManager == null)
        {
            _optionManager = OptionManager.Instance;
        }

        if (_optionManager == null)
        {
            if (!_didWarnOptionManagerMissing)
            {
                Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                    CameraEffectWarningCatalog.KeyAccessibilityState,
                    $"OptionManager를 찾지 못해 접근성 폴백 배율을 사용합니다. context={context}"), this);
                _didWarnOptionManagerMissing = true;
            }

            ApplyAccessibilityFallback($"OptionManagerMissing.{context}");
            return;
        }

        _didWarnOptionManagerMissing = false;

        if (!_accessibilityListenerBound)
        {
            _optionManager.AddListener(OnOptionSnapshotChanged);
            _accessibilityListenerBound = true;
        }

        OptionSaveData snapshot = _optionManager.GetCurrentOptions(); // OptionManager 최신 접근성 스냅샷입니다.
        if (snapshot == null)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyAccessibilityState,
                $"OptionManager 스냅샷이 null이라 접근성 폴백 배율을 사용합니다. context={context}"), this);
            ApplyAccessibilityFallback($"OptionSnapshotNull.{context}");
            return;
        }

        ApplyAccessibilitySnapshot(snapshot.Accessibility, $"BindSnapshot.{context}");
    }

    /// <summary>
    /// OptionManager 옵션 변경 알림을 수신해 접근성 스케일을 실시간 갱신합니다.
    /// </summary>
    private void OnOptionSnapshotChanged(OptionSaveData snapshot)
    {
        if (snapshot == null)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyAccessibilityState,
                "OptionManager 변경 알림 스냅샷이 null이라 접근성 폴백 배율을 사용합니다."), this);
            ApplyAccessibilityFallback("OptionChanged.NullSnapshot");
            return;
        }

        ApplyAccessibilitySnapshot(snapshot.Accessibility, "OptionChanged");
    }

    /// <summary>
    /// 접근성 데이터 스냅샷을 CameraEffect 런타임 스케일 상태로 변환해 반영합니다.
    /// </summary>
    private void ApplyAccessibilitySnapshot(in AccessibilityOptionsData accessibility, string context)
    {
        float cameraShakeScale = ClampAndWarn01("Accessibility.CameraShake", accessibility.CameraShake);
        float screenShakeScale = ClampAndWarn01("Accessibility.ScreenShake", accessibility.ScreenShake);
        float shakeReduction = ClampAndWarn01("Accessibility.ShakeReduction", accessibility.ShakeReduction);
        float shakeScale = cameraShakeScale * screenShakeScale * (1f - shakeReduction);

        float flashScale = accessibility.FlashReduction == E_OptionFlashReduction.On ? _flashScaleWhenReductionOn : 1f;
        float colorScale = 1f;
        if (accessibility.ColorBlindMode != E_OptionColorBlindMode.Off)
        {
            colorScale *= _colorScaleWhenColorBlindModeOn;
        }

        if (accessibility.HighContrastMode == E_OptionHighContrast.On)
        {
            colorScale *= _colorScaleWhenHighContrastOn;
        }

        AccessibilityRuntimeState nextState = new AccessibilityRuntimeState();
        nextState.ShakeScale = ClampAndWarn01("ComputedShakeScale", shakeScale);
        nextState.FlashScale = ClampAndWarn01("ComputedFlashScale", flashScale);
        nextState.ColorScale = ClampAndWarn01("ComputedColorScale", colorScale);
        nextState.IsUsingOptionSnapshot = true;

        SetAccessibilityRuntimeState(nextState, context);
    }

    /// <summary>
    /// 접근성 옵션을 사용할 수 없을 때 Inspector 폴백 배율을 런타임 상태에 반영합니다.
    /// </summary>
    private void ApplyAccessibilityFallback(string context)
    {
        AccessibilityRuntimeState fallbackState = new AccessibilityRuntimeState();
        fallbackState.ShakeScale = _fallbackShakeScale;
        fallbackState.FlashScale = _fallbackFlashScale;
        fallbackState.ColorScale = _fallbackColorScale;
        fallbackState.IsUsingOptionSnapshot = false;

        SetAccessibilityRuntimeState(fallbackState, context);
    }

    /// <summary>
    /// 접근성 런타임 상태를 교체하고 활성 채널 요청에도 즉시 반영합니다.
    /// </summary>
    private void SetAccessibilityRuntimeState(in AccessibilityRuntimeState state, string context)
    {
        _accessibilityRuntimeState = state;
        _debugAccessibilityShakeScale = state.ShakeScale;
        _debugAccessibilityFlashScale = state.FlashScale;
        _debugAccessibilityColorScale = state.ColorScale;
        _debugUsingOptionAccessibility = state.IsUsingOptionSnapshot;
        ApplyAccessibilityToActiveStates();

        if (_verboseLogging)
        {
            Debug.Log($"[CameraEffectManager] Accessibility applied. context={context}, shake={state.ShakeScale:F2}, flash={state.FlashScale:F2}, color={state.ColorScale:F2}, fromOption={state.IsUsingOptionSnapshot}", this);
        }
    }

    /// <summary>
    /// 요청 구조체에 현재 접근성 배율을 주입해 모듈 계산 시 반영되도록 설정합니다.
    /// </summary>
    private void ApplyAccessibilityToRequest(ref CameraEffectRequest request)
    {
        if (!_enableAccessibilityIntegration)
        {
            request.UseAccessibilityOverride = false;
            return;
        }

        request.UseAccessibilityOverride = true;
        request.AccessibilityShakeScale = _accessibilityRuntimeState.ShakeScale;
        request.AccessibilityFlashScale = _accessibilityRuntimeState.FlashScale;
        request.AccessibilityColorScale = _accessibilityRuntimeState.ColorScale;
    }

    /// <summary>
    /// 접근성 값이 변경되면 현재 재생 중인 모든 채널의 런타임 요청 스냅샷을 즉시 갱신합니다.
    /// </summary>
    private void ApplyAccessibilityToActiveStates()
    {
        for (int index = 0; index < ChannelValues.Length; index++)
        {
            E_CameraEffectChannel channel = ChannelValues[index];
            if (!_runtimeByChannel.TryGetValue(channel, out ChannelRuntimeState state) || !state.IsPlaying)
            {
                continue;
            }

            CameraEffectRequest updatedRequest = state.RuntimeRequest; // 접근성 값을 최신으로 반영할 요청 복사본입니다.
            ApplyAccessibilityToRequest(ref updatedRequest);
            state.RuntimeRequest = updatedRequest;
            _runtimeByChannel[channel] = state;
        }
    }

    /// <summary>
    /// 0~1 범위를 벗어난 설정값을 경고와 함께 클램프해 반환합니다.
    /// </summary>
    private float ClampAndWarn01(string fieldName, float rawValue)
    {
        if (!float.IsNaN(rawValue) && !float.IsInfinity(rawValue) && rawValue >= 0f && rawValue <= 1f)
        {
            return rawValue;
        }

        float clampedValue = float.IsNaN(rawValue) || float.IsInfinity(rawValue) ? 1f : Mathf.Clamp01(rawValue);
        Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
            CameraEffectWarningCatalog.KeyAccessibilityState,
            $"접근성 값이 0~1 범위를 벗어나 클램프합니다. field={fieldName}, raw={rawValue}, clamped={clampedValue}"), this);
        return clampedValue;
    }

    /// <summary>
    /// 현재 활성 채널 수 디버그 값을 갱신합니다.
    /// </summary>
    private void RefreshActiveChannelCountDebugValue()
    {
        int activeCount = 0; // 활성 채널 집계를 위한 임시 카운터입니다.
        for (int index = 0; index < ChannelValues.Length; index++)
        {
            if (_runtimeByChannel.TryGetValue(ChannelValues[index], out ChannelRuntimeState state) && state.IsPlaying)
            {
                activeCount++;
            }
        }

        _activeChannelEffectCount = activeCount;
    }

    /// <summary>
    /// 다음 고유 토큰 값을 생성합니다.
    /// </summary>
    private int GenerateNextToken()
    {
        int token = _nextToken; // 반환할 현재 토큰 값입니다.
        _nextToken++;

        if (_nextToken <= 0)
        {
            _nextToken = 1;
        }

        return token;
    }

    /// <summary>
    /// 지정 채널의 활성 효과를 내부적으로 종료하고 상태/토큰 매핑을 정리합니다.
    /// </summary>
    private bool StopChannelEffectInternal(E_CameraEffectChannel channel, string reason)
    {
        if (!_runtimeByChannel.TryGetValue(channel, out ChannelRuntimeState state) || !state.IsPlaying)
        {
            return false;
        }

        CameraEffectPresetBase runtimePreset = state.RuntimePresetInstance; // 종료 콜백에 전달할 런타임 프리셋 인스턴스 참조입니다.
        CameraEffectRequest runtimeRequest = state.RuntimeRequest; // 종료 콜백에 전달할 요청 스냅샷입니다.

        _runtimeByChannel[channel] = default;
        _channelByToken.Remove(state.Token);

        if (runtimePreset != null)
        {
            runtimePreset.OnEffectStopped(_targetCamera, runtimeRequest, reason);
            Destroy(runtimePreset);
        }

        if (_verboseLogging)
        {
            Debug.Log($"[CameraEffectManager] Play stop. token={state.Token}, effect={state.EffectName}, channel={channel}, priority={state.Priority}, reason={reason}", this);
        }

        RefreshActiveChannelCountDebugValue();
        return true;
    }
}
