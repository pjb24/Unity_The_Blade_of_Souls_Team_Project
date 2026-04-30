using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 보스 전투의 권한, 기본 런타임 상태, 전투 라이프사이클 진입 지점을 제어한다.
/// </summary>
[DisallowMultipleComponent]
public class BossController : NetworkBehaviour, IBossPatternExecutionListener, IHealthListener
{
    [Header("필수 참조")]
    [Tooltip("순수 패턴 설정을 저장하는 보스 패턴 설정 애셋")]
    [SerializeField] private BossPatternData _patternData; // 향후 보스 패턴 로직에서 사용할 ScriptableObject 설정

    [Tooltip("투사체, 몬스터, 약점 영역 참조를 저장하는 씬 앵커 세트")]
    [SerializeField] private BossPatternAnchorSet _anchorSet; // 향후 보스 패턴 로직에서 사용할 씬 참조 묶음

    [Tooltip("보스 체력 소스로 사용하는 기존 HealthComponent")]
    [SerializeField] private HealthComponent _healthComponent; // 기존 체력 시스템 재사용

    [Tooltip("보스 피해 입력 지점으로 사용하는 기존 HitReceiver")]
    [SerializeField] private HitReceiver _hitReceiver; // 기존 히트 시스템 재사용

    [Tooltip("패턴 실행 중 사용할 공통 Player 타겟 제공자")]
    [SerializeField] private BossPlayerTargetProvider _playerTargetProvider; // 보스 패턴 실행에서 사용하는 Player 탐색 제공자

    [Tooltip("Animator, VFX, 사운드 동기화를 담당하는 프레젠테이션 컨트롤러")]
    [SerializeField] private BossPresentationController _presentationController; // 권한 확정 후 연출만 담당하는 브릿지

    [Tooltip("약점 타이머 및 생성된 약점 정리를 담당하는 패턴 4 컴포넌트")]
    [SerializeField] private BossWeakPointPattern _weakPointPattern; // 보스 사망 시 직접 정리되는 패턴 4 런타임 소유자

    [Header("공통 패턴 설정")]
    [Tooltip("패턴 요청 사이에 추가로 적용되는 공통 쿨타임 (초)")]
    [Min(0f)]
    [SerializeField] private float _commonPatternCooldownSeconds; // 보스 전체 공통 쿨타임

    [Header("런타임 상태")]
    [Tooltip("디버깅 및 연출 동기화를 위한 현재 보스 상태")]
    [SerializeField] private E_BossState _currentState = E_BossState.None; // 권한 인스턴스에서만 결정되는 상태

    [Tooltip("현재 보스가 무적 상태인지 여부")]
    [SerializeField] private bool _isInvincible; // 향후 피격 처리에서 데미지 차단에 사용

    [Tooltip("현재 약점 패턴이 활성 상태인지 여부")]
    [SerializeField] private bool _isWeakPointPatternActive; // 약점 패턴 처리용 플래그

    [Tooltip("현재 실행 중인 패턴 타입")]
    [SerializeField] private E_BossPatternType _currentPatternType = E_BossPatternType.None; // 권한 인스턴스가 소유하는 현재 패턴 타입

    [Tooltip("현재 패턴 실행 시 기록된 HealthPhase 인덱스")]
    [SerializeField] private int _currentPatternHealthPhaseIndex = -1; // 패턴 실행 시점의 HealthPhase 인덱스

    [Tooltip("현재 실행 중인 패턴 ID")]
    [SerializeField] private string _currentPatternId = string.Empty; // 권한 인스턴스가 소유하는 PatternId

    [Header("디버그")]
    [Tooltip("필수 참조 누락 시 경고 로그 출력 여부")]
    [SerializeField] private bool _warnMissingRequiredReferences = true; // 인스펙터 검증 경고 토글

    private BossPatternBase _currentPattern; // 현재 실행 중이며 결과를 보고하는 패턴 인스턴스
    private Coroutine _patternSelectionCoroutine; // 패턴 선택 타이밍용 코루틴
    private Coroutine _currentPatternCoroutine; // 패턴 실행 코루틴
    private Coroutine _commonCooldownCoroutine; // 공통 쿨타임 코루틴
    private Coroutine _groggyTimerCoroutine; // Groggy 상태 타이머 코루틴
    private readonly BossPatternSelector _patternSelector = new BossPatternSelector(); // 패턴 선택 재사용 객체
    private int _healthPhaseUsageResetVersion; // HealthPhase 사용 횟수 리셋 마커
    private int _individualCooldownResetVersion; // 개별 패턴 쿨타임 리셋 마커
    private float _globalCooldownEndTime; // 공통 쿨타임 종료 시간
    private string _lastGlobalCooldownReason = string.Empty; // 마지막 공통 쿨타임 발생 이유
    private float[] _patternCooldownEndTimeByType; // 패턴 타입별 쿨타임 종료 시간
    private int[] _healthPhasePatternUseCounts; // HealthPhase + Pattern별 사용 횟수
    private int _currentPatternCommonSettingsIndex = -1; // 패턴 실행 시점 CommonSettings 인덱스
    private bool _isBattleActive; // 전투 활성 여부
    private bool _isPatternSelectionEnabled; // 패턴 선택 가능 여부
    private bool _isCurrentPatternListenerRegistered; // 패턴 리스너 등록 여부
    private bool _isHealthListenerRegistered; // 체력 리스너 등록 여부
    private bool _hasEnteredDeadCleanup; // 사망 정리 중복 방지
    private bool _isResolvingBossDeath; // 사망 처리 중 패턴 콜백 무시 플래그
    private bool _hasLoggedAuthorityWarning; // 권한 경고 중복 방지
    private bool _hasLoggedHealthRatioFallbackWarning; // 체력 비율 fallback 경고 중복 방지
    private bool _hasLoggedHealthPhaseLookupWarning; // HealthPhase 조회 경고 중복 방지
    private bool _hasLoggedCommonSettingsLookupWarning; // CommonSettings 조회 경고 중복 방지
    private bool _hasLoggedUsageLimitWarning; // UsageLimit 경고 중복 방지
    private bool _hasLoggedHealthListenerMissingWarning; // HealthListener 경고 중복 방지
    private bool _hasLoggedPresentationControllerMissingWarning; // 연출 컨트롤러 경고 중복 방지
    private bool _hasLoggedBossStateSyncFallbackWarning; // 상태 동기화 경고 중복 방지

    /// <summary>
    /// 보스 패턴 데이터 애셋을 반환한다.
    /// </summary>
    public BossPatternData PatternData => _patternData;

    /// <summary>
    /// 씬 앵커 세트를 반환한다.
    /// </summary>
    public BossPatternAnchorSet AnchorSet => _anchorSet;

    /// <summary>
    /// 기존 HealthComponent 참조를 반환한다.
    /// </summary>
    public HealthComponent HealthComponent => _healthComponent;

    /// <summary>
    /// 기존 HitReceiver 참조를 반환한다.
    /// </summary>
    public HitReceiver HitReceiver => _hitReceiver;

    /// <summary>
    /// Player 타겟 제공자 참조를 반환한다.
    /// </summary>
    public BossPlayerTargetProvider PlayerTargetProvider => _playerTargetProvider;

    /// <summary>
    /// 공통 패턴 쿨타임(초)을 반환한다.
    /// </summary>
    public float CommonPatternCooldownSeconds => _commonPatternCooldownSeconds;

    /// <summary>
    /// 공통 쿨타임 종료 시각(Time.time 기준)을 반환한다.
    /// </summary>
    public float GlobalCooldownEndTime => _globalCooldownEndTime;

    /// <summary>
    /// 마지막 공통 쿨타임 발생 이유를 반환한다.
    /// </summary>
    public string LastGlobalCooldownReason => _lastGlobalCooldownReason;

    /// <summary>
    /// 현재 보스 상태를 반환한다.
    /// </summary>
    public E_BossState CurrentState => _currentState;

    /// <summary>
    /// 전투 활성 여부를 반환한다.
    /// </summary>
    public bool IsBattleActive => _isBattleActive;

    /// <summary>
    /// 패턴 선택 가능 여부를 반환한다.
    /// </summary>
    public bool IsPatternSelectionEnabled => _isPatternSelectionEnabled;

    /// <summary>
    /// 무적 상태 여부를 반환한다.
    /// </summary>
    public bool IsInvincible => _isInvincible;

    /// <summary>
    /// 약점 패턴 활성 여부를 반환한다.
    /// </summary>
    public bool IsWeakPointPatternActive => _isWeakPointPatternActive;

    /// <summary>
    /// 현재 실행 중인 패턴 타입을 반환한다.
    /// </summary>
    public E_BossPatternType CurrentPatternType => _currentPatternType;

    /// <summary>
    /// 현재 패턴 실행 시 기록된 HealthPhase 인덱스를 반환한다.
    /// </summary>
    public int CurrentPatternHealthPhaseIndex => _currentPatternHealthPhaseIndex;

    /// <summary>
    /// 현재 실행 중인 PatternId를 반환한다.
    /// </summary>
    public string CurrentPatternId => _currentPatternId;

    /// <summary>
    /// 현재 패턴 인스턴스를 반환한다.
    /// </summary>
    public BossPatternBase CurrentPattern => _currentPattern;

    /// <summary>
    /// 기존 호출 호환성을 위한 현재 패턴 인스턴스 반환
    /// </summary>
    public BossPatternBase CurrentPatternInstance => _currentPattern;

    /// <summary>
    /// 패턴 선택기를 반환한다.
    /// </summary>
    public BossPatternSelector PatternSelector => _patternSelector;

    /// <summary>
    /// 런타임 전에 쿨타임 저장소를 초기화한다.
    /// </summary>
    private void Awake()
    {
        ResolveOptionalRuntimeReferences();
        RegisterHealthListener();
        EnsurePatternCooldownStorage();
        EnsureHealthPhaseUsageStorage();
    }

    /// <summary>
    /// 컴포넌트 활성화 시 체력 리스너를 등록한다.
    /// </summary>
    private void OnEnable()
    {
        ResolveOptionalRuntimeReferences();
        RegisterHealthListener();
    }

    /// <summary>
    /// 컴포넌트 비활성화 시 체력 리스너를 제거한다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterHealthListener();
    }

    /// <summary>
    /// 인스펙터 값 검증 및 보정 수행
    /// </summary>
    private void OnValidate()
    {
        ResolveOptionalRuntimeReferences();
        ValidateCommonSettings();
        ValidateRequiredReferences();
    }

    /// <summary>
    /// 현재 인스턴스가 보스 로직 권한을 가지는지 반환한다.
    /// </summary>
    public bool IsBossLogicAuthority()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 싱글플레이/서버 권한 판별용 NGO 싱글톤
        if (networkManager == null)
        {
            return true;
        }

        if (!networkManager.IsListening)
        {
            return true;
        }

        return networkManager.IsServer;
    }

    /// <summary>
    /// 체력 변경 이벤트 수신 (사용 안함)
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
    }

    /// <summary>
    /// 피해 이벤트 수신 (사망은 OnDied에서 처리)
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// 회복 이벤트 수신 (사용 안함)
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 사망 이벤트 수신 후 Dead 상태 진입
    /// </summary>
    public void OnDied()
    {
        if (!IsBossLogicAuthority())
        {
            return;
        }

        EnterDeadState();
    }

    /// <summary>
    /// 부활 이벤트 수신 시 사망 정리 플래그 초기화
    /// </summary>
    public void OnRevived()
    {
        if (!IsBossLogicAuthority())
        {
            return;
        }

        _hasEnteredDeadCleanup = false;
    }

    /// <summary>
    /// 최대 체력 변경 이벤트 (사용 안함)
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }

    /// <summary>
    /// 권한을 가진 인스턴스에서 보스 전투 라이프사이클을 시작한다.
    /// </summary>
    public void StartBattle()
    {
        if (!TryEnsureAuthority("StartBattle"))
        {
            return;
        }

        ResetHealthPhaseUsageCounters(); // HealthPhase 사용 횟수 초기화
        ResetCommonCooldown(); // 공통 쿨타임 초기화
        ResetIndividualCooldowns(); // 개별 패턴 쿨타임 초기화
        ResetRuntimeWarningState(); // 런타임 경고 상태 초기화

        _isBattleActive = true; // 전투 활성화
        _isPatternSelectionEnabled = true; // 패턴 선택 활성화
        _isInvincible = false; // 무적 상태 초기화
        _isWeakPointPatternActive = false; // 약점 패턴 상태 초기화
        _hasEnteredDeadCleanup = false; // 사망 정리 상태 초기화
        EnterIdleState(); // Idle 상태 진입
    }

    /// <summary>
    /// 권한 인스턴스가 보유한 모든 보스 런타임 상태 값을 초기화한다.
    /// </summary>
    public void ResetBattle()
    {
        if (!TryEnsureAuthority("ResetBattle"))
        {
            return;
        }

        StopAllRuntimeTimers(); // 모든 런타임 타이머 중지
        ResetRuntimeState(); // 런타임 상태 초기화
    }

    /// <summary>
    /// 전투 라이프사이클을 중지하고 향후 패턴 선택 및 현재 패턴 실행을 모두 취소한다.
    /// </summary>
    public void StopBattle()
    {
        if (!TryEnsureAuthority("StopBattle"))
        {
            return;
        }

        _isPatternSelectionEnabled = false; // 패턴 선택 비활성화
        CancelCurrentPattern("StopBattle"); // 현재 패턴 취소
        StopAllRuntimeTimers(); // 모든 타이머 중지
        _isBattleActive = false; // 전투 비활성화

        if (_currentState != E_BossState.Dead)
        {
            EnterIdleState(); // 사망 상태가 아니면 Idle로 전환
        }
    }

    /// <summary>
    /// 패턴 로직을 실행하지 않고 보스를 패턴 실행 상태로 표시한다.
    /// </summary>
    public void SetPatternExecutingState(E_BossPatternType patternType, BossPatternBase patternInstance)
    {
        if (!TryEnsureAuthority("SetPatternExecutingState"))
        {
            return;
        }

        EnterPatternExecutingState(patternType, patternInstance); // 패턴 실행 상태 진입
    }

    /// <summary>
    /// 공통 패턴 실행 API를 통해 보스 패턴 실행을 시도한다.
    /// </summary>
    public bool TryStartPatternExecution(BossPatternBase pattern)
    {
        if (pattern == null)
        {
            Debug.LogWarning($"[BossController] TryStartPatternExecution 실패: pattern이 null. object={name}", this);
            return false;
        }

        if (!TryResolveFirstSelectableSettingsForPatternType(pattern.PatternType, out PatternCommonSettings settings))
        {
            Debug.LogWarning($"[BossController] TryStartPatternExecution 실패: 선택 가능한 PatternId 없음. object={name}, patternType={pattern.PatternType}", this);
            return false;
        }

        return TryStartPatternExecution(pattern, settings);
    }

    /// <summary>
    /// 선택된 PatternId 설정을 사용하여 공통 패턴 실행 API로 보스 패턴을 시작한다.
    /// </summary>
    public bool TryStartPatternExecution(BossPatternBase pattern, PatternCommonSettings selectedSettings)
    {
        if (!TryEnsureAuthority("TryStartPatternExecution"))
        {
            return false;
        }

        if (pattern == null)
        {
            Debug.LogWarning($"[BossController] TryStartPatternExecution 실패: pattern이 null. object={name}", this);
            return false;
        }

        if (_currentState != E_BossState.Idle)
        {
            Debug.LogWarning($"[BossController] 상태 때문에 패턴 실행 차단됨. object={name}, state={_currentState}", this);
            return false;
        }

        if (_currentPattern != null)
        {
            Debug.LogWarning($"[BossController] 다른 패턴이 이미 실행 중이라 차단됨. object={name}, activeType={_currentPatternType}", this);
            return false;
        }

        if (pattern.PatternType != selectedSettings.PatternType)
        {
            Debug.LogWarning($"[BossController] PatternType 불일치로 실행 실패. object={name}, componentType={pattern.PatternType}, selectedType={selectedSettings.PatternType}, patternId={selectedSettings.PatternId}", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(selectedSettings.PatternId))
        {
            Debug.LogWarning($"[BossController] PatternId가 비어있어 실행 실패. object={name}, patternType={selectedSettings.PatternType}", this);
            return false;
        }

        if (pattern.PatternType == E_BossPatternType.WeakPoint && _isWeakPointPatternActive)
        {
            Debug.LogWarning($"[BossController] Pattern4 이미 실행 중이라 차단됨. object={name}", this);
            return false;
        }

        if (IsGlobalCooldownActive())
        {
            Debug.LogWarning($"[BossController] 글로벌 쿨타임으로 차단됨. object={name}, patternType={pattern.PatternType}, remaining={GetGlobalCooldownRemainingSeconds()}", this);
            return false;
        }

        if (IsPatternCooldownActive(pattern.PatternType))
        {
            Debug.LogWarning($"[BossController] 개별 패턴 쿨타임으로 차단됨. object={name}, patternType={pattern.PatternType}, remaining={GetPatternCooldownRemainingSeconds(pattern.PatternType)}", this);
            return false;
        }

        if (!TryCapturePatternSelectionContextForExecution(selectedSettings))
        {
            Debug.LogWarning($"[BossController] HealthPhase 또는 UsageLimit 조건으로 차단됨. object={name}, patternType={pattern.PatternType}, patternId={selectedSettings.PatternId}", this);
            return false;
        }

        _currentPatternId = selectedSettings.PatternId; // 현재 패턴 ID 저장
        SetCurrentPatternReference(pattern.PatternType, pattern, true); // 현재 패턴 참조 설정
        SetState(E_BossState.PatternExecuting); // 상태 변경
        PlayPresentationCueInternal(E_BossPresentationCue.PatternStarted, pattern.PatternType, transform.position); // 연출 실행

        if (!pattern.StartPatternExecution())
        {
            ClearCurrentPatternReference(); // 패턴 참조 초기화
            ClearCurrentPatternSelectionContext(); // 선택 컨텍스트 초기화
            SetState(E_BossState.Idle); // 상태 복귀
            return false;
        }
        return true;
    }

    /// <summary>
    /// 패턴 정상 종료 이벤트 수신 후 Idle 상태로 복귀한다.
    /// </summary>
    public void OnBossPatternCompleted(BossPatternExecutionReport report)
    {
        HandlePatternResult(report, "Completed"); // 결과 처리
    }

    /// <summary>
    /// 패턴 취소 이벤트 수신 후 Idle 상태로 복귀한다.
    /// </summary>
    public void OnBossPatternCancelled(BossPatternExecutionReport report)
    {
        HandlePatternResult(report, "Cancelled");
    }

    /// <summary>
    /// 패턴 실패 이벤트 수신 후 Idle 상태로 복귀한다.
    /// </summary>
    public void OnBossPatternFailed(BossPatternExecutionReport report)
    {
        HandlePatternResult(report, "Failed");
    }

    /// <summary>
    /// 보스를 Groggy 상태로 전환하고 패턴 선택을 중지한다.
    /// </summary>
    public void SetGroggyState()
    {
        if (!TryEnsureAuthority("SetGroggyState"))
        {
            return;
        }

        EnterGroggyState();
    }

    /// <summary>
    /// 보스를 Dead 상태로 전환하고 패턴 선택을 중지한다.
    /// </summary>
    public void SetDeadState()
    {
        if (!TryEnsureAuthority("SetDeadState"))
        {
            return;
        }

        EnterDeadState();
    }

    /// <summary>
    /// 권한 인스턴스에서 보스 전체 공통 쿨타임을 시작하거나 덮어쓴다.
    /// </summary>
    public void StartGlobalCooldown(string reason)
    {
        if (!TryEnsureAuthority("StartGlobalCooldown"))
        {
            return;
        }

        StartGlobalCooldownInternal(reason);
    }

    /// <summary>
    /// 특정 패턴 타입에 대한 개별 쿨타임을 시작하거나 덮어쓴다.
    /// </summary>
    public void StartPatternCooldown(E_BossPatternType patternType, string reason)
    {
        if (!TryEnsureAuthority("StartPatternCooldown"))
        {
            return;
        }

        StartPatternCooldownInternal(patternType, reason);
    }

    /// <summary>
    /// 클라이언트가 전투 상태를 결정하지 않도록 하면서 권한 기반 보스 연출을 실행한다.
    /// </summary>
    public void PlayPresentationCue(E_BossPresentationCue cue, E_BossPatternType patternType, Vector3 worldPosition)
    {
        if (!TryEnsureAuthority("PlayPresentationCue"))
        {
            return;
        }

        PlayPresentationCueInternal(cue, patternType, worldPosition);
    }

    /// <summary>
    /// 패턴 4가 약점 단계에 진입했음을 기록하고 필수 글로벌 쿨타임을 시작한다.
    /// </summary>
    public void NotifyPatternFourEntryCompleted()
    {
        if (!TryEnsureAuthority("NotifyPatternFourEntryCompleted"))
        {
            return;
        }

        bool wasInvincible = _isInvincible; // 이전 무적 상태 (중복 연출 방지용)
        _isWeakPointPatternActive = true;
        _isInvincible = true;

        if (!wasInvincible)
        {
            PlayPresentationCueInternal(E_BossPresentationCue.InvincibleStarted, E_BossPatternType.WeakPoint, transform.position);
        }

        StartGlobalCooldownInternal("Pattern4EntryCompleted");
    }

    /// <summary>
    /// 패턴 4 진입 시작을 기록하되 약점 활성화나 타이머는 시작하지 않는다.
    /// </summary>
    public void NotifyPatternFourEntryStarted()
    {
        if (!TryEnsureAuthority("NotifyPatternFourEntryStarted"))
        {
            return;
        }

        _isWeakPointPatternActive = false;
        _isInvincible = false;
    }

    /// <summary>
    /// 패턴 4가 시간 초과로 종료되었음을 기록하고 글로벌 쿨타임을 시작한다.
    /// </summary>
    public void NotifyPatternFourTimedOut()
    {
        if (!TryEnsureAuthority("NotifyPatternFourTimedOut"))
        {
            return;
        }

        bool wasInvincible = _isInvincible; // 이전 무적 상태 (연출 동기화용)
        CancelCurrentRegularPatternForPatternFourEnd("Pattern4TimedOut"); // 일반 패턴 취소

        _isWeakPointPatternActive = false;
        _isInvincible = false;

        if (wasInvincible)
        {
            PlayPresentationCueInternal(E_BossPresentationCue.InvincibleEnded, E_BossPatternType.WeakPoint, transform.position);
        }

        PlayPresentationCueInternal(E_BossPresentationCue.PatternEnded, E_BossPatternType.WeakPoint, transform.position);

        _isPatternSelectionEnabled = true; // 패턴 선택 재활성화
        EnterIdleState(); // Idle 상태 전환
        StartGlobalCooldownInternal("Pattern4TimedOut"); // 글로벌 쿨타임 시작
    }

    /// <summary>
    /// 패턴 4가 진입 전에 실패했음을 기록하고 필수 글로벌 쿨타임을 시작한다.
    /// </summary>
    public void NotifyPatternFourEntryFailed()
    {
        if (!TryEnsureAuthority("NotifyPatternFourEntryFailed"))
        {
            return;
        }

        bool wasInvincible = _isInvincible; // 이전 무적 상태 (연출 동기화를 위한 상태)
        _isWeakPointPatternActive = false; // 약점 패턴 비활성화
        _isInvincible = false; // 무적 해제

        if (wasInvincible)
        {
            PlayPresentationCueInternal(E_BossPresentationCue.InvincibleEnded, E_BossPatternType.WeakPoint, transform.position);
        }

        StartGlobalCooldownInternal("Pattern4EntryFailed"); // 글로벌 쿨타임 시작
    }

    /// <summary>
    /// 모든 약점이 파괴되었음을 기록하되 즉시 글로벌 쿨타임은 시작하지 않는다.
    /// </summary>
    public void NotifyPatternFourAllWeakPointsDestroyed()
    {
        if (!TryEnsureAuthority("NotifyPatternFourAllWeakPointsDestroyed"))
        {
            return;
        }

        bool wasInvincible = _isInvincible; // 이전 무적 상태 (연출 동기화용)
        CancelCurrentRegularPatternForPatternFourEnd("Pattern4AllWeakPointsDestroyed"); // 일반 패턴 취소
        _isWeakPointPatternActive = false; // 약점 상태 종료
        _isInvincible = false; // 무적 해제

        if (wasInvincible)
        {
            PlayPresentationCueInternal(E_BossPresentationCue.InvincibleEnded, E_BossPatternType.WeakPoint, transform.position);
        }

        PlayPresentationCueInternal(E_BossPresentationCue.PatternEnded, E_BossPatternType.WeakPoint, transform.position); // 패턴 종료 연출
    }

    /// <summary>
    /// 일정 시간 동안 Groggy 상태에 진입하고 이후 권한 인스턴스가 Idle 상태로 복귀하도록 한다.
    /// </summary>
    public void StartGroggyForDuration(float groggyDurationSeconds, string reason)
    {
        if (!TryEnsureAuthority("StartGroggyForDuration"))
        {
            return;
        }

        float safeDuration = groggyDurationSeconds; // 권한 인스턴스가 사용하는 Groggy 지속 시간
        if (safeDuration < 0f)
        {
            Debug.LogWarning($"[BossController] Groggy 지속 시간이 0보다 작아서 보정됨. object={name}, value={safeDuration}", this);
            safeDuration = 0f;
        }

        StopRuntimeCoroutine(ref _groggyTimerCoroutine); // 기존 Groggy 타이머 중지

        bool wasInvincible = _isInvincible; // 이전 무적 상태
        _isWeakPointPatternActive = false;
        _isInvincible = false;

        if (wasInvincible)
        {
            PlayPresentationCueInternal(E_BossPresentationCue.InvincibleEnded, E_BossPatternType.WeakPoint, transform.position);
        }

        EnterGroggyState(); // Groggy 상태 진입
        _groggyTimerCoroutine = StartCoroutine(RunGroggyTimer(safeDuration, reason)); // 타이머 시작
    }

    /// <summary>
    /// Groggy 상태를 종료하고 글로벌 쿨타임을 시작한 뒤 Idle 상태로 복귀한다.
    /// </summary>
    public void EndGroggyState()
    {
        if (!TryEnsureAuthority("EndGroggyState"))
        {
            return;
        }

        if (_currentState != E_BossState.Groggy)
        {
            Debug.LogWarning($"[BossController] Groggy 상태가 아닌데 EndGroggyState 호출됨. object={name}, state={_currentState}", this);
            return;
        }

        _isPatternSelectionEnabled = true; // 패턴 선택 재활성화
        PlayPresentationCueInternal(E_BossPresentationCue.GroggyEnded, E_BossPatternType.WeakPoint, transform.position); // 연출
        StartGlobalCooldownInternal("GroggyEnded"); // 글로벌 쿨타임 시작
        EnterIdleState(); // Idle 복귀
    }

    /// <summary>
    /// 현재 상태에서 패턴 선택이 가능한지 반환한다.
    /// </summary>
    public bool CanSelectPattern()
    {
        if (!_isBattleActive || !_isPatternSelectionEnabled)
        {
            return false;
        }

        if (_currentState != E_BossState.Idle)
        {
            return false;
        }

        if (IsGlobalCooldownActive())
        {
            return false;
        }

        return IsBossLogicAuthority();
    }

    /// <summary>
    /// 특정 패턴 타입이 선택 가능한지 상태 및 쿨타임 기준으로 반환한다.
    /// </summary>
    public bool CanSelectPatternType(E_BossPatternType patternType)
    {
        if (!CanSelectPattern())
        {
            return false;
        }

        if (GetPatternCooldownIndex(patternType) < 0)
        {
            return false;
        }

        if (patternType == E_BossPatternType.WeakPoint && _isWeakPointPatternActive)
        {
            return false;
        }

        if (IsPatternCooldownActive(patternType))
        {
            return false;
        }

        return TryGetPatternSelectionContext(patternType, out _, out _);
    }

    /// <summary>
    /// 특정 CommonSettings 항목이 선택 가능한지 반환한다.
    /// </summary>
    public bool CanSelectPatternSettings(PatternCommonSettings settings)
    {
        if (!TryGetCommonSettingsIndex(settings.PatternId, settings.PatternType, out int commonSettingsIndex))
        {
            return false;
        }

        return CanSelectPatternSettings(settings, commonSettingsIndex);
    }

    /// <summary>
    /// 특정 인덱스의 CommonSettings 항목이 선택 가능한지 반환한다.
    /// </summary>
    public bool CanSelectPatternSettings(PatternCommonSettings settings, int commonSettingsIndex)
    {
        if (!CanSelectPattern())
        {
            return false;
        }

        if (GetPatternCooldownIndex(settings.PatternType) < 0)
        {
            return false;
        }

        if (settings.PatternType == E_BossPatternType.WeakPoint && _isWeakPointPatternActive)
        {
            return false;
        }

        if (IsPatternCooldownActive(settings.PatternType))
        {
            return false;
        }

        return TryGetPatternSelectionContext(settings, commonSettingsIndex, out _);
    }

    /// <summary>
    /// 선택 가능한 패턴이 없을 때 Idle 상태 유지 상황을 로그로 보고한다.
    /// </summary>
    public void ReportNoSelectablePatternFallback()
    {
        Debug.LogWarning($"[BossController] 선택 가능한 패턴이 없어 Idle 유지. object={name}, phaseIndex={GetCurrentHealthPhaseIndex()}", this);
    }

    /// <summary>
    /// 패턴 실행 없이 다음 패턴 후보를 선택한다.
    /// </summary>
    public bool TrySelectPattern(Transform target, out PatternCommonSettings selectedSettings)
    {
        return _patternSelector.TrySelectPattern(this, target, out selectedSettings);
    }

    /// <summary>
    /// 특정 PatternType에 대해 실행 가능한 첫 번째 CommonSettings를 찾는다.
    /// </summary>
    private bool TryResolveFirstSelectableSettingsForPatternType(E_BossPatternType patternType, out PatternCommonSettings selectedSettings)
    {
        selectedSettings = default;

        if (_patternData == null || _patternData.CommonSettings == null)
        {
            Debug.LogWarning($"[BossController] PatternData 또는 CommonSettings가 없어 선택 불가. object={name}, patternType={patternType}", this);
            return false;
        }

        PatternCommonSettings[] commonSettings = _patternData.CommonSettings; // 디자이너가 작성한 설정 목록
        for (int index = 0; index < commonSettings.Length; index++)
        {
            PatternCommonSettings settings = commonSettings[index]; // 후보 설정

            if (settings.PatternType != patternType)
            {
                continue;
            }

            if (!CanSelectPatternSettings(settings, index))
            {
                continue;
            }

            selectedSettings = settings;
            return true;
        }

        Debug.LogWarning($"[BossController] 선택 가능한 CommonSettings 없음. object={name}, patternType={patternType}", this);
        return false;
    }

    /// <summary>
    /// 공통 타겟 제공자를 통해 보스 패턴 실행에 사용할 가장 가까운 유효한 Player를 찾는다.
    /// </summary>
    public bool TryFindNearestPlayerForExecution(float executionRange, out Transform targetTransform, out HealthComponent targetHealth, out NetworkObject targetNetworkObject)
    {
        ResolveOptionalRuntimeReferences();
        if (_playerTargetProvider == null)
        {
            Debug.LogWarning($"[BossController] BossPlayerTargetProvider가 없어 Player 탐색 실패. object={name}", this);
            targetTransform = null;
            targetHealth = null;
            targetNetworkObject = null;
            return false;
        }

        return _playerTargetProvider.TryFindNearestPlayerForExecution(executionRange, out targetTransform, out targetHealth, out targetNetworkObject);
    }

    /// <summary>
    /// 현재 보스 전체 공통 쿨타임이 활성 상태인지 여부를 반환한다.
    /// </summary>
    public bool IsGlobalCooldownActive()
    {
        return Time.time < _globalCooldownEndTime;
    }

    /// <summary>
    /// Time.time 기준으로 남아있는 공통 쿨타임(초)을 반환한다.
    /// </summary>
    public float GetGlobalCooldownRemainingSeconds()
    {
        float remainingSeconds = _globalCooldownEndTime - Time.time; // Time.time 기준 쿨타임 차이 계산
        if (remainingSeconds <= 0f)
        {
            return 0f;
        }

        return remainingSeconds;
    }

    /// <summary>
    /// 특정 패턴 타입이 개별 쿨타임 중인지 여부를 반환한다.
    /// </summary>
    public bool IsPatternCooldownActive(E_BossPatternType patternType)
    {
        int cooldownIndex = GetPatternCooldownIndex(patternType); // enum 기반 인덱스로 쿨타임 배열 접근
        if (cooldownIndex < 0)
        {
            return false;
        }

        EnsurePatternCooldownStorage();
        return Time.time < _patternCooldownEndTimeByType[cooldownIndex];
    }

    /// <summary>
    /// 특정 패턴 타입의 남은 개별 쿨타임(초)을 반환한다.
    /// </summary>
    public float GetPatternCooldownRemainingSeconds(E_BossPatternType patternType)
    {
        int cooldownIndex = GetPatternCooldownIndex(patternType); // enum 기반 인덱스
        if (cooldownIndex < 0)
        {
            return 0f;
        }

        EnsurePatternCooldownStorage();
        float remainingSeconds = _patternCooldownEndTimeByType[cooldownIndex] - Time.time; // Time.time 기준 쿨타임 차이 계산
        if (remainingSeconds <= 0f)
        {
            return 0f;
        }

        return remainingSeconds;
    }

    /// <summary>
    /// 특정 패턴 타입의 쿨타임 종료 시각(Time.time 기준)을 반환한다.
    /// </summary>
    public float GetPatternCooldownEndTime(E_BossPatternType patternType)
    {
        int cooldownIndex = GetPatternCooldownIndex(patternType); // enum 기반 인덱스
        if (cooldownIndex < 0)
        {
            return 0f;
        }

        EnsurePatternCooldownStorage();
        return _patternCooldownEndTimeByType[cooldownIndex];
    }

    /// <summary>
    /// 현재 보스 체력 비율을 0..1 범위로 보정하여 반환한다.
    /// </summary>
    public float GetCurrentHealthRatio()
    {
        if (_healthComponent == null)
        {
            if (!_hasLoggedHealthRatioFallbackWarning)
            {
                Debug.LogWarning($"[BossController] HealthComponent가 없어 체력 비율을 1로 반환. object={name}", this);
                _hasLoggedHealthRatioFallbackWarning = true;
            }

            return 1f;
        }

        float maxHealth = _healthComponent.GetMaxHealth(); // 기존 HealthComponent의 최대 체력
        if (maxHealth <= 0f)
        {
            if (!_hasLoggedHealthRatioFallbackWarning)
            {
                Debug.LogWarning($"[BossController] MaxHealth가 0 이하라 체력 비율을 1로 반환. object={name}, maxHealth={maxHealth}", this);
                _hasLoggedHealthRatioFallbackWarning = true;
            }

            return 1f;
        }

        float currentHealth = _healthComponent.GetCurrentHealth(); // 현재 체력
        return Mathf.Clamp01(currentHealth / maxHealth);
    }

    /// <summary>
    /// 현재 체력 비율 기준으로 HealthPhase 인덱스를 반환한다.
    /// </summary>
    public int GetCurrentHealthPhaseIndex()
    {
        return GetHealthPhaseIndex(GetCurrentHealthRatio());
    }

    /// <summary>
    /// 지정한 체력 비율에 해당하는 HealthPhase 인덱스를 반환한다.
    /// </summary>
    public int GetHealthPhaseIndex(float healthRatio)
    {
        float clampedHealthRatio = Mathf.Clamp01(healthRatio); // 정규화된 체력 값

        if (_patternData == null || _patternData.HealthPhaseSettings == null || _patternData.HealthPhaseSettings.Length == 0)
        {
            if (!_hasLoggedHealthPhaseLookupWarning)
            {
                Debug.LogWarning($"[BossController] HealthPhaseSettings가 없어 조회 실패. object={name}", this);
                _hasLoggedHealthPhaseLookupWarning = true;
            }

            return -1;
        }

        HealthPhaseSettings[] healthPhaseSettings = _patternData.HealthPhaseSettings; // 디자이너 설정 배열
        for (int index = 0; index < healthPhaseSettings.Length; index++)
        {
            HealthPhaseSettings settings = healthPhaseSettings[index]; // 현재 검사 대상 페이즈

            if (clampedHealthRatio > settings.MaxHealthRatio || clampedHealthRatio <= settings.MinHealthRatio)
            {
                continue;
            }

            return index;
        }

        if (!_hasLoggedHealthPhaseLookupWarning)
        {
            Debug.LogWarning($"[BossController] 해당 체력에 맞는 HealthPhase 없음. object={name}, ratio={clampedHealthRatio}", this);
            _hasLoggedHealthPhaseLookupWarning = true;
        }

        return -1;
    }

    /// <summary>
    /// HealthPhase 기준 특정 패턴 타입의 사용 횟수를 반환한다.
    /// </summary>
    public int GetHealthPhasePatternUseCount(int healthPhaseIndex, E_BossPatternType patternType)
    {
        if (!TryGetCommonSettingsIndex(patternType, out int commonSettingsIndex))
        {
            return 0;
        }

        return GetHealthPhasePatternUseCountByIndex(healthPhaseIndex, commonSettingsIndex);
    }

    /// <summary>
    /// HealthPhase 기준 특정 PatternId의 사용 횟수를 반환한다.
    /// </summary>
    public int GetHealthPhasePatternUseCount(int healthPhaseIndex, string patternId)
    {
        if (!TryGetCommonSettingsIndex(patternId, out int commonSettingsIndex))
        {
            return 0;
        }

        return GetHealthPhasePatternUseCountByIndex(healthPhaseIndex, commonSettingsIndex);
    }

    /// <summary>
    /// Idle 상태로 진입하고 현재 패턴 참조를 초기화한다.
    /// </summary>
    private void EnterIdleState()
    {
        ClearCurrentPatternReference(); // 패턴 참조 제거
        SetState(E_BossState.Idle); // 상태 변경
    }

    /// <summary>
    /// PatternExecuting 상태로 진입하고 현재 패턴 정보를 기록한다.
    /// </summary>
    private void EnterPatternExecutingState(E_BossPatternType patternType, BossPatternBase patternInstance)
    {
        SetCurrentPatternReference(patternType, patternInstance, patternInstance != null); // 패턴 참조 설정
        SetState(E_BossState.PatternExecuting); // 상태 변경
    }

    /// <summary>
    /// Groggy 상태로 진입하고 현재 패턴을 취소한다.
    /// </summary>
    private void EnterGroggyState()
    {
        CancelCurrentPattern("EnterGroggyState"); // 현재 패턴 취소
        SetState(E_BossState.Groggy); // 상태 변경
        PlayPresentationCueInternal(E_BossPresentationCue.GroggyStarted, E_BossPatternType.WeakPoint, transform.position); // 연출 실행
    }

    /// <summary>
    /// Dead 상태로 진입하고 현재 패턴 소유 상태를 정리한다.
    /// </summary>
    private void EnterDeadState()
    {
        if (_hasEnteredDeadCleanup)
        {
            return;
        }

        _hasEnteredDeadCleanup = true;
        _isResolvingBossDeath = true;
        _isPatternSelectionEnabled = false;
        StopAllRuntimeTimers(); // 모든 런타임 타이머 중지
        CancelCurrentPattern("EnterDeadState"); // 현재 패턴 취소
        CleanupWeakPointPatternForBossDeath(); // 약점 패턴 정리
        _isWeakPointPatternActive = false;
        _isInvincible = false;
        _isBattleActive = false;
        SetState(E_BossState.Dead); // Dead 상태 전환
        PlayPresentationCueInternal(E_BossPresentationCue.Dead, E_BossPatternType.None, transform.position); // 사망 연출 실행
        _isResolvingBossDeath = false;
    }

    /// <summary>
    /// 권한 인스턴스에서 현재 보스 상태를 변경한다.
    /// </summary>
    private void SetState(E_BossState nextState)
    {
        _currentState = nextState;
        SyncBossStateToClients(nextState); // 클라이언트에 상태 동기화

        if (_currentState == E_BossState.Dead || _currentState == E_BossState.Groggy)
        {
            _isPatternSelectionEnabled = false;
        }
    }

    /// <summary>
    /// 서버에서 전달된 보스 상태를 클라이언트에 적용하되 전투 권한 데이터는 변경하지 않는다.
    /// </summary>
    private void ApplyReplicatedBossState(int stateValue)
    {
        E_BossState replicatedState = (E_BossState)stateValue; // RPC를 통해 전달된 서버 상태 값
        _currentState = replicatedState;

        if (replicatedState == E_BossState.Dead)
        {
            _isBattleActive = false;
            _isWeakPointPatternActive = false;
            _isInvincible = false;
        }

        if (replicatedState == E_BossState.Dead || replicatedState == E_BossState.Groggy)
        {
            _isPatternSelectionEnabled = false;
        }
    }

    /// <summary>
    /// 네트워크가 활성 상태일 때 서버에서 확정된 보스 상태를 클라이언트에 전송한다.
    /// </summary>
    private void SyncBossStateToClients(E_BossState state)
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 상태 동기화 필요 여부 판단용 NGO 싱글톤
        if (networkManager == null || !networkManager.IsListening)
        {
            return;
        }

        if (!IsSpawned)
        {
            if (!_hasLoggedBossStateSyncFallbackWarning)
            {
                Debug.LogWarning($"[BossController] NetworkObject가 Spawn되지 않아 상태 동기화 스킵됨. object={name}, state={state}", this);
                _hasLoggedBossStateSyncFallbackWarning = true;
            }

            return;
        }

        SyncBossStateRpc((int)state);
    }

    /// <summary>
    /// 서버에서 확정된 보스 상태를 수신하여 로컬에 반영한다. 전투 로직 결정은 수행하지 않는다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void SyncBossStateRpc(int stateValue)
    {
        ApplyReplicatedBossState(stateValue);
    }

    /// <summary>
    /// 인스펙터 설정을 변경하지 않고 런타임 전용 상태를 초기화한다.
    /// </summary>
    private void ResetRuntimeState()
    {
        _isBattleActive = false;
        _isPatternSelectionEnabled = false;
        _isInvincible = false;
        _isWeakPointPatternActive = false;
        _hasEnteredDeadCleanup = false;
        _isResolvingBossDeath = false;
        _currentPatternType = E_BossPatternType.None;
        ClearCurrentPatternSelectionContext(); // 선택 컨텍스트 초기화
        ClearCurrentPatternReference(); // 패턴 참조 초기화
        ResetCommonCooldown(); // 공통 쿨타임 초기화
        ResetIndividualCooldowns(); // 개별 쿨타임 초기화
        ResetHealthPhaseUsageCounters(); // 사용 횟수 초기화
        ResetRuntimeWarningState(); // 경고 상태 초기화
        SetState(E_BossState.None); // 상태 초기화
    }

    /// <summary>
    /// HealthPhase 사용 횟수 저장소를 초기화한다.
    /// </summary>
    private void ResetHealthPhaseUsageCounters()
    {
        _healthPhaseUsageResetVersion++;
        ClearCurrentPatternSelectionContext();
        EnsureHealthPhaseUsageStorage();

        for (int index = 0; index < _healthPhasePatternUseCounts.Length; index++)
        {
            _healthPhasePatternUseCounts[index] = 0;
        }
    }

    /// <summary>
    /// 공통 쿨타임 값을 초기화한다.
    /// </summary>
    private void ResetCommonCooldown()
    {
        _globalCooldownEndTime = 0f;
        _lastGlobalCooldownReason = string.Empty;
    }

    /// <summary>
    /// 개별 패턴 쿨타임 저장 값을 초기화한다.
    /// </summary>
    private void ResetIndividualCooldowns()
    {
        _individualCooldownResetVersion++;
        EnsurePatternCooldownStorage();

        for (int index = 0; index < _patternCooldownEndTimeByType.Length; index++)
        {
            _patternCooldownEndTimeByType[index] = 0f;
        }
    }

    /// <summary>
    /// 패턴 선택 체크에서 사용하는 1회성 경고 플래그를 초기화한다.
    /// </summary>
    private void ResetRuntimeWarningState()
    {
        _hasLoggedAuthorityWarning = false;
        _hasLoggedHealthRatioFallbackWarning = false;
        _hasLoggedHealthPhaseLookupWarning = false;
        _hasLoggedCommonSettingsLookupWarning = false;
        _hasLoggedUsageLimitWarning = false;
        _hasLoggedHealthListenerMissingWarning = false;
        _hasLoggedPresentationControllerMissingWarning = false;
        _hasLoggedBossStateSyncFallbackWarning = false;
    }

    /// <summary>
    /// 보스 사망 시 패턴 4 타이머 및 약점 오브젝트를 정리한다.
    /// </summary>
    private void CleanupWeakPointPatternForBossDeath()
    {
        ResolveOptionalRuntimeReferences();
        if (_weakPointPattern == null)
        {
            if (_isWeakPointPatternActive)
            {
                Debug.LogWarning($"[BossController] 보스 사망 시 BossWeakPointPattern이 없어 Pattern4 정리 스킵됨. object={name}", this);
            }

            return;
        }

        _weakPointPattern.CleanupForBossDeath();
    }

    /// <summary>
    /// 기존 HealthComponent의 사망 이벤트를 AddListener로 구독한다.
    /// </summary>
    private void RegisterHealthListener()
    {
        ResolveOptionalRuntimeReferences();
        if (_isHealthListenerRegistered)
        {
            return;
        }

        if (_healthComponent == null)
        {
            if (!_hasLoggedHealthListenerMissingWarning)
            {
                Debug.LogWarning($"[BossController] HealthComponent가 없어 사망 이벤트 등록 실패. object={name}", this);
                _hasLoggedHealthListenerMissingWarning = true;
            }

            return;
        }

        _healthComponent.AddListener(this);
        _isHealthListenerRegistered = true;
    }

    /// <summary>
    /// 기존 HealthComponent의 사망 이벤트를 RemoveListener로 해제한다.
    /// </summary>
    private void UnregisterHealthListener()
    {
        if (!_isHealthListenerRegistered || _healthComponent == null)
        {
            return;
        }

        _healthComponent.RemoveListener(this);
        _isHealthListenerRegistered = false;
    }

    /// <summary>
    /// 현재 패턴 실행을 취소한다.
    /// </summary>
    private void CancelCurrentPattern(string reason)
    {
        if (_currentPattern != null && _currentPattern.IsExecuting)
        {
            _currentPattern.CancelPattern(reason);
            return;
        }

        ClearCurrentPatternReference();
    }

    /// <summary>
    /// Pattern 4가 아닌 현재 실행 중인 패턴만 취소한다.
    /// </summary>
    private void CancelCurrentRegularPatternForPatternFourEnd(string reason)
    {
        if (_currentPattern == null || !_currentPattern.IsExecuting)
        {
            ClearCurrentPatternReference();
            return;
        }

        if (IsPatternFourPatternType(_currentPatternType))
        {
            Debug.LogWarning($"[BossController] Pattern4 종료 시 자기 자신 취소는 수행하지 않음. object={name}, reason={reason}", this);
            return;
        }

        _currentPattern.CancelPattern(reason);
    }

    /// <summary>
    /// 현재 패턴 참조를 초기화하고 이 컨트롤러를 패턴 리포트에서 제거한다.
    /// </summary>
    private void ClearCurrentPatternReference()
    {
        if (_currentPattern != null && _isCurrentPatternListenerRegistered)
        {
            _currentPattern.RemoveListener(this);
        }

        _currentPatternType = E_BossPatternType.None;
        _currentPattern = null;
        _isCurrentPatternListenerRegistered = false;
    }

    /// <summary>
    /// 현재 패턴에 대해 기록된 HealthPhase 및 CommonSettings 인덱스를 초기화한다.
    /// </summary>
    private void ClearCurrentPatternSelectionContext()
    {
        _currentPatternHealthPhaseIndex = -1;
        _currentPatternCommonSettingsIndex = -1;
        _currentPatternId = string.Empty;
    }

    /// <summary>
    /// 현재 패턴 참조를 저장하고 필요 시 결과 리포트 리스너를 등록한다.
    /// </summary>
    private void SetCurrentPatternReference(E_BossPatternType patternType, BossPatternBase pattern, bool registerListener)
    {
        ClearCurrentPatternReference();
        _currentPatternType = patternType;
        _currentPattern = pattern;

        if (_currentPattern == null || !registerListener)
        {
            _isCurrentPatternListenerRegistered = false;
            return;
        }

        _currentPattern.AddListener(this);
        _isCurrentPatternListenerRegistered = true;
    }

    /// <summary>
    /// 패턴 종료 결과를 처리하고 보스를 Idle 상태로 복귀시킨다.
    /// </summary>
    private void HandlePatternResult(BossPatternExecutionReport report, string resultLabel)
    {
        if (report.Pattern != _currentPattern)
        {
            Debug.LogWarning($"[BossController] 이전 패턴 결과를 무시함. object={name}, result={resultLabel}, reportType={report.PatternType}, currentType={_currentPatternType}", this);
            return;
        }

        if (_isResolvingBossDeath)
        {
            ClearCurrentPatternReference();
            ClearCurrentPatternSelectionContext();
            return;
        }

        PlayPresentationCueInternal(E_BossPresentationCue.PatternEnded, report.PatternType, transform.position);
        ApplyUsageCountForPatternResult(report, resultLabel);
        ApplyCooldownsForPatternResult(report.PatternType, resultLabel);
        ClearCurrentPatternReference();
        ClearCurrentPatternSelectionContext();

        if (_currentState != E_BossState.Dead && _currentState != E_BossState.Groggy)
        {
            SetState(E_BossState.Idle);
        }
    }

    /// <summary>
    /// 권한 확인 이후 설정된 연출 컨트롤러를 통해 연출 이벤트를 전달한다.
    /// </summary>
    private void PlayPresentationCueInternal(E_BossPresentationCue cue, E_BossPatternType patternType, Vector3 worldPosition)
    {
        ResolveOptionalRuntimeReferences();
        if (_presentationController == null)
        {
            if (!_hasLoggedPresentationControllerMissingWarning)
            {
                Debug.LogWarning($"[BossController] BossPresentationController가 없어 연출 실행 스킵됨. object={name}, cue={cue}, patternType={patternType}", this);
                _hasLoggedPresentationControllerMissingWarning = true;
            }

            return;
        }

        _presentationController.PlayCue(cue, patternType, worldPosition);
    }

    /// <summary>
    /// 이 컨트롤러가 소유한 모든 런타임 타이머 코루틴을 중지한다.
    /// </summary>
    private void StopAllRuntimeTimers()
    {
        StopRuntimeCoroutine(ref _patternSelectionCoroutine);
        StopRuntimeCoroutine(ref _currentPatternCoroutine);
        StopRuntimeCoroutine(ref _commonCooldownCoroutine);
        StopRuntimeCoroutine(ref _groggyTimerCoroutine);
    }

    /// <summary>
    /// Groggy 지속 시간이 끝날 때까지 대기 후 Idle 상태로 복귀하고 글로벌 쿨타임을 시작한다.
    /// </summary>
    private IEnumerator RunGroggyTimer(float groggyDurationSeconds, string reason)
    {
        if (groggyDurationSeconds > 0f)
        {
            yield return new WaitForSeconds(groggyDurationSeconds);
        }

        _groggyTimerCoroutine = null;
        EndGroggyState();
    }

    /// <summary>
    /// 코루틴이 실행 중이면 중지하고 참조를 초기화한다.
    /// </summary>
    private void StopRuntimeCoroutine(ref Coroutine coroutine)
    {
        if (coroutine == null)
        {
            return;
        }

        StopCoroutine(coroutine);
        coroutine = null;
    }

    /// <summary>
    /// 패턴 실행 전에 선택된 PatternId에 대한 HealthPhase 및 CommonSettings 컨텍스트를 저장한다.
    /// </summary>
    private bool TryCapturePatternSelectionContextForExecution(PatternCommonSettings settings)
    {
        if (!IsBossLogicAuthority())
        {
            return false;
        }

        if (_currentState == E_BossState.Dead)
        {
            return false;
        }

        if (_currentState == E_BossState.Dead)
        {
            return false;
        }

        if (!TryGetPatternSelectionContext(settings, out int healthPhaseIndex, out int commonSettingsIndex))
        {
            return false;
        }

        _currentPatternHealthPhaseIndex = healthPhaseIndex;
        _currentPatternCommonSettingsIndex = commonSettingsIndex;
        return true;
    }

    /// <summary>
    /// 패턴 완료 또는 효과가 발생한 취소 시 HealthPhase 사용 횟수를 증가시킨다.
    /// </summary>
    private void ApplyUsageCountForPatternResult(BossPatternExecutionReport report, string resultLabel)
    {
        if (!ShouldCountPatternUsage(report, resultLabel))
        {
            return;
        }

        IncrementCapturedPatternUsage(report);
    }

    /// <summary>
    /// 패턴 결과에 따라 사용 횟수를 증가시켜야 하는지 여부를 반환한다.
    /// </summary>
    private bool ShouldCountPatternUsage(BossPatternExecutionReport report, string resultLabel)
    {
        if (!IsBossLogicAuthority() || _currentState == E_BossState.Dead)
        {
            return false;
        }

        if (resultLabel == "Completed")
        {
            return true;
        }

        if (resultLabel == "Cancelled" && report.HasAppliedEffect)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 기록된 HealthPhase 및 PatternId 사용 횟수를 증가시킨다.
    /// </summary>
    private void IncrementCapturedPatternUsage(BossPatternExecutionReport report)
    {
        EnsureHealthPhaseUsageStorage();
        int usageIndex = GetHealthPhaseUsageIndex(_currentPatternHealthPhaseIndex, _currentPatternCommonSettingsIndex);

        if (usageIndex < 0)
        {
            Debug.LogWarning($"[BossController] 잘못된 인덱스로 인해 사용 횟수 기록 실패. object={name}, phaseIndex={_currentPatternHealthPhaseIndex}, commonIndex={_currentPatternCommonSettingsIndex}, patternId={_currentPatternId}, result={report.Reason}", this);
            return;
        }

        _healthPhasePatternUseCounts[usageIndex]++;
    }

    /// <summary>
    /// 패턴 선택에 필요한 HealthPhase 및 CommonSettings 인덱스를 계산한다.
    /// </summary>
    private bool TryGetPatternSelectionContext(E_BossPatternType patternType, out int healthPhaseIndex, out int commonSettingsIndex)
    {
        healthPhaseIndex = -1;
        commonSettingsIndex = -1;

        if (!TryGetCommonSettingsIndex(patternType, out commonSettingsIndex))
        {
            return false;
        }

        healthPhaseIndex = GetCurrentHealthPhaseIndex();
        if (healthPhaseIndex < 0)
        {
            return false;
        }

        PatternCommonSettings commonSettings = _patternData.CommonSettings[commonSettingsIndex]; // 선택된 패턴에 연결된 공통 설정

        if (!IsPatternAvailableInHealthPhase(healthPhaseIndex, commonSettings.PatternId))
        {
            return false;
        }

        if (IsPatternUsageLimitExceeded(healthPhaseIndex, commonSettingsIndex, commonSettings.PatternId))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 특정 CommonSettings 항목에 대한 HealthPhase 컨텍스트를 계산한다.
    /// </summary>
    private bool TryGetPatternSelectionContext(PatternCommonSettings settings, out int healthPhaseIndex, out int commonSettingsIndex)
    {
        healthPhaseIndex = -1;
        commonSettingsIndex = -1;

        if (!TryGetCommonSettingsIndex(settings.PatternId, settings.PatternType, out commonSettingsIndex))
        {
            return false;
        }

        return TryGetPatternSelectionContext(settings, commonSettingsIndex, out healthPhaseIndex);
    }

    /// <summary>
    /// 특정 CommonSettings 인덱스 기준으로 HealthPhase 컨텍스트를 계산하고 중복 PatternId를 차단한다.
    /// </summary>
    private bool TryGetPatternSelectionContext(PatternCommonSettings settings, int commonSettingsIndex, out int healthPhaseIndex)
    {
        healthPhaseIndex = -1;

        if (_patternData == null || _patternData.CommonSettings == null || commonSettingsIndex < 0 || commonSettingsIndex >= _patternData.CommonSettings.Length)
        {
            return false;
        }

        if (!IsFirstCommonSettingsPatternIdIndex(settings.PatternId, commonSettingsIndex))
        {
            Debug.LogWarning($"[BossController] 중복된 PatternId 설정 무시됨. object={name}, patternId={settings.PatternId}, patternType={settings.PatternType}, index={commonSettingsIndex}", this);
            return false;
        }

        if (_patternData.CommonSettings[commonSettingsIndex].PatternId != settings.PatternId ||
            _patternData.CommonSettings[commonSettingsIndex].PatternType != settings.PatternType)
        {
            Debug.LogWarning($"[BossController] 잘못된 CommonSettings 항목 무시됨. object={name}, patternId={settings.PatternId}, patternType={settings.PatternType}, index={commonSettingsIndex}", this);
            return false;
        }

        healthPhaseIndex = GetCurrentHealthPhaseIndex();
        if (healthPhaseIndex < 0)
        {
            return false;
        }

        if (!IsPatternAvailableInHealthPhase(healthPhaseIndex, settings.PatternId))
        {
            return false;
        }

        if (IsPatternUsageLimitExceeded(healthPhaseIndex, commonSettingsIndex, settings.PatternId))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 지정한 PatternId가 선택된 HealthPhase 설정에 포함되어 있는지 여부를 반환한다.
    /// </summary>
    private bool IsPatternAvailableInHealthPhase(int healthPhaseIndex, string patternId)
    {
        if (string.IsNullOrEmpty(patternId))
        {
            Debug.LogWarning($"[BossController] PatternId가 비어 있어 패턴 사용 가능 여부 확인 실패. object={name}, phaseIndex={healthPhaseIndex}", this);
            return false;
        }

        if (_patternData == null || _patternData.HealthPhaseSettings == null || healthPhaseIndex < 0 || healthPhaseIndex >= _patternData.HealthPhaseSettings.Length)
        {
            return false;
        }

        string[] availablePatternIds = _patternData.HealthPhaseSettings[healthPhaseIndex].AvailablePatternIds; // 선택된 HealthPhase에서 허용된 PatternId 목록
        if (availablePatternIds == null || availablePatternIds.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < availablePatternIds.Length; index++)
        {
            if (availablePatternIds[index] != patternId)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 전달된 CommonSettings 인덱스가 해당 PatternId의 첫 번째 항목인지 여부를 반환한다.
    /// </summary>
    private bool IsFirstCommonSettingsPatternIdIndex(string patternId, int commonSettingsIndex)
    {
        if (_patternData == null || _patternData.CommonSettings == null || string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        for (int index = 0; index < _patternData.CommonSettings.Length; index++)
        {
            if (_patternData.CommonSettings[index].PatternId != patternId)
            {
                continue;
            }

            return index == commonSettingsIndex;
        }

        return false;
    }

    /// <summary>
    /// HealthPhase에서 패턴 사용 횟수가 설정된 제한을 초과했는지 여부를 반환한다.
    /// </summary>
    private bool IsPatternUsageLimitExceeded(int healthPhaseIndex, int commonSettingsIndex, string patternId)
    {
        if (!TryGetPatternUsageLimit(healthPhaseIndex, patternId, out PatternUsageLimit usageLimit))
        {
            return false;
        }

        int maxUseCount = usageLimit.MaxUseCount; // Phase 단위 최대 사용 횟수 (0: 사용 불가, 음수: 무제한)
        if (maxUseCount < 0)
        {
            if (!_hasLoggedUsageLimitWarning)
            {
                Debug.LogWarning($"[BossController] UsageLimit MaxUseCount가 음수라 무제한으로 처리됨. object={name}, phaseIndex={healthPhaseIndex}, patternId={patternId}, maxUseCount={maxUseCount}", this);
                _hasLoggedUsageLimitWarning = true;
            }

            return false;
        }

        if (maxUseCount == 0)
        {
            Debug.LogWarning($"[BossController] UsageLimit MaxUseCount가 0이라 패턴 제외됨. object={name}, phaseIndex={healthPhaseIndex}, patternId={patternId}", this);
            return true;
        }

        int currentUseCount = GetHealthPhasePatternUseCountByIndex(healthPhaseIndex, commonSettingsIndex);
        if (currentUseCount < maxUseCount)
        {
            return false;
        }

        Debug.LogWarning($"[BossController] UsageLimit 초과로 패턴 제외됨. object={name}, phaseIndex={healthPhaseIndex}, patternId={patternId}, currentUseCount={currentUseCount}, maxUseCount={maxUseCount}", this);
        return true;
    }

    /// <summary>
    /// 특정 PatternType에 대해 첫 번째 CommonSettings 인덱스를 찾는다.
    /// </summary>
    private bool TryGetCommonSettingsIndex(E_BossPatternType patternType, out int commonSettingsIndex)
    {
        commonSettingsIndex = -1;

        if (GetPatternCooldownIndex(patternType) < 0)
        {
            return false;
        }

        if (_patternData == null || _patternData.CommonSettings == null || _patternData.CommonSettings.Length == 0)
        {
            if (!_hasLoggedCommonSettingsLookupWarning)
            {
                Debug.LogWarning($"[BossController] PatternData 또는 CommonSettings가 없어 조회 실패. object={name}, patternType={patternType}", this);
                _hasLoggedCommonSettingsLookupWarning = true;
            }

            return false;
        }

        PatternCommonSettings[] commonSettings = _patternData.CommonSettings; // 디자이너가 설정한 CommonSettings 배열
        for (int index = 0; index < commonSettings.Length; index++)
        {
            if (commonSettings[index].PatternType != patternType)
            {
                continue;
            }

            commonSettingsIndex = index;
            return true;
        }

        if (!_hasLoggedCommonSettingsLookupWarning)
        {
            Debug.LogWarning($"[BossController] 해당 PatternType에 대한 CommonSettings 없음. object={name}, patternType={patternType}", this);
            _hasLoggedCommonSettingsLookupWarning = true;
        }

        return false;
    }

    /// <summary>
    /// PatternId와 PatternType이 모두 일치하는 첫 번째 CommonSettings 인덱스를 찾는다.
    /// </summary>
    private bool TryGetCommonSettingsIndex(string patternId, E_BossPatternType patternType, out int commonSettingsIndex)
    {
        commonSettingsIndex = -1;

        if (string.IsNullOrEmpty(patternId) || GetPatternCooldownIndex(patternType) < 0)
        {
            return false;
        }

        if (_patternData == null || _patternData.CommonSettings == null || _patternData.CommonSettings.Length == 0)
        {
            if (!_hasLoggedCommonSettingsLookupWarning)
            {
                Debug.LogWarning($"[BossController] PatternData 또는 CommonSettings가 없어 조회 실패. object={name}, patternId={patternId}, patternType={patternType}", this);
                _hasLoggedCommonSettingsLookupWarning = true;
            }

            return false;
        }

        PatternCommonSettings[] commonSettings = _patternData.CommonSettings; // 디자이너 설정 배열
        for (int index = 0; index < commonSettings.Length; index++)
        {
            if (commonSettings[index].PatternType != patternType || commonSettings[index].PatternId != patternId)
            {
                continue;
            }

            commonSettingsIndex = index;
            return true;
        }

        if (!_hasLoggedCommonSettingsLookupWarning)
        {
            Debug.LogWarning($"[BossController] PatternId와 PatternType이 일치하는 CommonSettings 없음. object={name}, patternId={patternId}, patternType={patternType}", this);
            _hasLoggedCommonSettingsLookupWarning = true;
        }

        return false;
    }

    /// <summary>
    /// PatternType과 무관하게 PatternId만으로 첫 번째 CommonSettings 인덱스를 찾는다.
    /// </summary>
    private bool TryGetCommonSettingsIndex(string patternId, out int commonSettingsIndex)
    {
        commonSettingsIndex = -1;

        if (string.IsNullOrEmpty(patternId))
        {
            return false;
        }

        if (_patternData == null || _patternData.CommonSettings == null || _patternData.CommonSettings.Length == 0)
        {
            if (!_hasLoggedCommonSettingsLookupWarning)
            {
                Debug.LogWarning($"[BossController] PatternData 또는 CommonSettings가 없어 조회 실패. object={name}, patternId={patternId}", this);
                _hasLoggedCommonSettingsLookupWarning = true;
            }

            return false;
        }

        PatternCommonSettings[] commonSettings = _patternData.CommonSettings; // 디자이너 설정 배열
        for (int index = 0; index < commonSettings.Length; index++)
        {
            if (commonSettings[index].PatternId != patternId)
            {
                continue;
            }

            commonSettingsIndex = index;
            return true;
        }

        if (!_hasLoggedCommonSettingsLookupWarning)
        {
            Debug.LogWarning($"[BossController] 해당 PatternId에 대한 CommonSettings 없음. object={name}, patternId={patternId}", this);
            _hasLoggedCommonSettingsLookupWarning = true;
        }

        return false;
    }

    /// <summary>
    /// 특정 PatternId에 대한 UsageLimit 설정을 찾는다.
    /// </summary>
    private bool TryGetPatternUsageLimit(int healthPhaseArrayIndex, string patternId, out PatternUsageLimit usageLimit)
    {
        usageLimit = default;

        if (_patternData == null || _patternData.UsageLimits == null || _patternData.UsageLimits.Length == 0 || string.IsNullOrEmpty(patternId))
        {
            return false;
        }

        if (_patternData.HealthPhaseSettings == null || healthPhaseArrayIndex < 0 || healthPhaseArrayIndex >= _patternData.HealthPhaseSettings.Length)
        {
            return false;
        }

        int phaseIndex = _patternData.HealthPhaseSettings[healthPhaseArrayIndex].PhaseIndex; // UsageLimit에서 사용하는 PhaseIndex
        PatternUsageLimit[] usageLimits = _patternData.UsageLimits; // 디자이너 설정 UsageLimit 배열

        for (int index = 0; index < usageLimits.Length; index++)
        {
            if (usageLimits[index].PhaseIndex != phaseIndex || usageLimits[index].PatternId != patternId)
            {
                continue;
            }

            usageLimit = usageLimits[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// HealthPhase와 CommonSettings 인덱스를 기반으로 기록된 사용 횟수를 반환한다.
    /// </summary>
    private int GetHealthPhasePatternUseCountByIndex(int healthPhaseIndex, int commonSettingsIndex)
    {
        EnsureHealthPhaseUsageStorage();
        int usageIndex = GetHealthPhaseUsageIndex(healthPhaseIndex, commonSettingsIndex);
        if (usageIndex < 0)
        {
            return 0;
        }

        return _healthPhasePatternUseCounts[usageIndex];
    }

    /// <summary>
    /// HealthPhase 패턴 사용 횟수 저장소가 현재 패턴 데이터 구조와 일치하도록 보장한다.
    /// </summary>
    private void EnsureHealthPhaseUsageStorage()
    {
        int healthPhaseCount = GetHealthPhaseSettingsCount(); // 평탄화된 사용 횟수 테이블에서 HealthPhase 행 개수
        int commonSettingsCount = GetCommonSettingsCount(); // 평탄화된 사용 횟수 테이블에서 패턴 열 개수
        int requiredLength = healthPhaseCount * commonSettingsCount; // Phase-Pattern 조합에 대한 전체 길이
        if (_healthPhasePatternUseCounts != null && _healthPhasePatternUseCounts.Length == requiredLength)
        {
            return;
        }

        _healthPhasePatternUseCounts = new int[requiredLength];
    }

    /// <summary>
    /// HealthPhase와 CommonSettings 인덱스를 평탄화된 사용 배열 인덱스로 변환한다.
    /// </summary>
    private int GetHealthPhaseUsageIndex(int healthPhaseIndex, int commonSettingsIndex)
    {
        int commonSettingsCount = GetCommonSettingsCount(); // 평탄화 배열에서 stride로 사용하는 공통 설정 개수
        int healthPhaseCount = GetHealthPhaseSettingsCount(); // HealthPhase 범위 검증용 개수
        if (healthPhaseIndex < 0 || healthPhaseIndex >= healthPhaseCount || commonSettingsIndex < 0 || commonSettingsIndex >= commonSettingsCount)
        {
            return -1;
        }

        return healthPhaseIndex * commonSettingsCount + commonSettingsIndex;
    }

    /// <summary>
    /// 현재 HealthPhaseSettings 배열 길이를 반환한다.
    /// </summary>
    private int GetHealthPhaseSettingsCount()
    {
        if (_patternData == null || _patternData.HealthPhaseSettings == null)
        {
            return 0;
        }

        return _patternData.HealthPhaseSettings.Length;
    }

    /// <summary>
    /// 현재 PatternCommonSettings 배열 길이를 반환한다.
    /// </summary>
    private int GetCommonSettingsCount()
    {
        if (_patternData == null || _patternData.CommonSettings == null)
        {
            return 0;
        }

        return _patternData.CommonSettings.Length;
    }

    /// <summary>
    /// 패턴 종료 결과에 따라 필요한 쿨타임을 시작한다.
    /// </summary>
    private void ApplyCooldownsForPatternResult(E_BossPatternType patternType, string resultLabel)
    {
        if (IsPatternFourPatternType(patternType))
        {
            return;
        }

        if (resultLabel == "Completed" || resultLabel == "Cancelled")
        {
            StartPatternCooldownInternal(patternType, resultLabel);
            StartGlobalCooldownInternal(resultLabel);
            return;
        }

        if (resultLabel == "Failed")
        {
            StartGlobalCooldownInternal(resultLabel);
        }
    }

    /// <summary>
    /// 추가 권한 검사 없이 보스 전체 공통 쿨타임을 시작하거나 덮어쓴다.
    /// </summary>
    private void StartGlobalCooldownInternal(string reason)
    {
        float cooldownSeconds = _commonPatternCooldownSeconds; // 디자이너가 설정한 보스 공통 쿨타임
        if (cooldownSeconds < 0f)
        {
            Debug.LogWarning($"[BossController] 공통 패턴 쿨타임이 0보다 작아 보정됨. object={name}, value={cooldownSeconds}", this);
            cooldownSeconds = 0f;
        }

        _globalCooldownEndTime = Time.time + cooldownSeconds;
        _lastGlobalCooldownReason = string.IsNullOrWhiteSpace(reason) ? "Unspecified" : reason;
    }

    /// <summary>
    /// 추가 권한 검사 없이 특정 패턴 타입의 쿨타임을 시작하거나 덮어쓴다.
    /// </summary>
    private void StartPatternCooldownInternal(E_BossPatternType patternType, string reason)
    {
        int cooldownIndex = GetPatternCooldownIndex(patternType); // enum 기반 배열 인덱스
        if (cooldownIndex < 0)
        {
            Debug.LogWarning($"[BossController] 잘못된 패턴 타입으로 쿨타임 시작 실패. object={name}, patternType={patternType}, reason={reason}", this);
            return;
        }

        EnsurePatternCooldownStorage();
        float cooldownSeconds = GetPatternCooldownSeconds(patternType);
        _patternCooldownEndTimeByType[cooldownIndex] = Time.time + cooldownSeconds;
    }

    /// <summary>
    /// 특정 패턴 타입에 설정된 개별 쿨타임(초)을 반환한다.
    /// </summary>
    private float GetPatternCooldownSeconds(E_BossPatternType patternType)
    {
        if (_patternData == null || _patternData.CommonSettings == null)
        {
            Debug.LogWarning($"[BossController] PatternData 또는 CommonSettings가 없어 쿨타임 0으로 fallback. object={name}, patternType={patternType}", this);
            return 0f;
        }

        PatternCommonSettings[] commonSettings = _patternData.CommonSettings; // 쿨타임 조회에 사용하는 설정 배열
        for (int index = 0; index < commonSettings.Length; index++)
        {
            if (commonSettings[index].PatternType != patternType)
            {
                continue;
            }

            return commonSettings[index].CooldownSeconds;
        }

        Debug.LogWarning($"[BossController] 해당 패턴 타입의 CommonSettings가 없어 쿨타임 0으로 fallback. object={name}, patternType={patternType}", this);
        return 0f;
    }

    /// <summary>
    /// 패턴 타입별 쿨타임 저장소가 초기화되었는지 확인하고 필요 시 생성한다.
    /// </summary>
    private void EnsurePatternCooldownStorage()
    {
        int requiredLength = (int)E_BossPatternType.WeakPoint + 1; // enum 최대값 기반 배열 길이
        if (_patternCooldownEndTimeByType != null && _patternCooldownEndTimeByType.Length == requiredLength)
        {
            return;
        }

        _patternCooldownEndTimeByType = new float[requiredLength];
    }

    /// <summary>
    /// 패턴 타입을 쿨타임 배열 인덱스로 변환한다.
    /// </summary>
    private int GetPatternCooldownIndex(E_BossPatternType patternType)
    {
        int cooldownIndex = (int)patternType; // enum 값을 그대로 인덱스로 사용
        if (cooldownIndex <= (int)E_BossPatternType.None || cooldownIndex > (int)E_BossPatternType.WeakPoint)
        {
            return -1;
        }

        return cooldownIndex;
    }

    /// <summary>
    /// 해당 패턴 타입이 Pattern4(WeakPoint) 흐름인지 여부를 반환한다.
    /// </summary>
    private bool IsPatternFourPatternType(E_BossPatternType patternType)
    {
        return patternType == E_BossPatternType.WeakPoint;
    }

    /// <summary>
    /// 보스 상태 변경 전에 권한을 확인한다.
    /// </summary>
    private bool TryEnsureAuthority(string operationName)
    {
        if (IsBossLogicAuthority())
        {
            _hasLoggedAuthorityWarning = false;
            return true;
        }

        if (!_hasLoggedAuthorityWarning)
        {
            Debug.LogWarning($"[BossController] 권한 없는 인스턴스에서 {operationName} 호출 무시됨. object={name}", this);
            _hasLoggedAuthorityWarning = true;
        }

        return false;
    }

    /// <summary>
    /// 인스펙터에서 수정된 공통 설정 값을 보정한다.
    /// </summary>
    private void ValidateCommonSettings()
    {
        if (_commonPatternCooldownSeconds < 0f)
        {
            Debug.LogWarning($"[BossController] 공통 패턴 쿨타임이 0보다 작아 보정됨. object={name}, value={_commonPatternCooldownSeconds}", this);
            _commonPatternCooldownSeconds = 0f;
        }
    }

    /// <summary>
    /// 필수 참조 누락 여부를 검사하여 런타임 전에 문제를 발견할 수 있도록 한다.
    /// </summary>
    private void ValidateRequiredReferences()
    {
        if (!_warnMissingRequiredReferences)
        {
            return;
        }

        if (_patternData == null)
        {
            Debug.LogWarning($"[BossController] PatternData가 설정되지 않음. object={name}", this);
        }

        if (_anchorSet == null)
        {
            Debug.LogWarning($"[BossController] BossPatternAnchorSet이 설정되지 않음. object={name}", this);
        }

        if (_healthComponent == null)
        {
            Debug.LogWarning($"[BossController] HealthComponent가 없어 기존 체력 시스템 사용 불가. object={name}", this);
        }

        if (_hitReceiver == null)
        {
            Debug.LogWarning($"[BossController] HitReceiver가 없어 기존 피격 시스템 사용 불가. object={name}", this);
        }

        if (_playerTargetProvider == null)
        {
            Debug.LogWarning($"[BossController] BossPlayerTargetProvider가 없어 Player 탐색 불가. object={name}", this);
        }

        if (_presentationController == null)
        {
            Debug.LogWarning($"[BossController] BossPresentationController가 없어 연출 동기화 불가. object={name}", this);
        }

        if (_weakPointPattern == null)
        {
            Debug.LogWarning($"[BossController] BossWeakPointPattern이 없어 Pattern4 정리 불가. object={name}", this);
        }
    }

    /// <summary>
    /// 보스 GameObject에서 선택적으로 사용하는 런타임 보조 참조를 해결한다.
    /// </summary>
    private void ResolveOptionalRuntimeReferences()
    {
        if (_playerTargetProvider == null)
        {
            _playerTargetProvider = GetComponent<BossPlayerTargetProvider>();
        }

        if (_presentationController == null)
        {
            _presentationController = GetComponent<BossPresentationController>();
        }

        if (_weakPointPattern == null)
        {
            _weakPointPattern = GetComponent<BossWeakPointPattern>();
        }
    }
}
