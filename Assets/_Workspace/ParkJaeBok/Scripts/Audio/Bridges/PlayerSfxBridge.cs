using UnityEngine;

/// <summary>
/// 플레이어 이동/점프/착지/대시/피격 이벤트를 기존 SFX 시스템으로 라우팅하는 브리지입니다.
/// </summary>
public class PlayerSfxBridge : MonoBehaviour, IHitListener
{
    [Header("Dependencies")]
    [Tooltip("SFX 이벤트 라우팅을 수행할 오케스트레이터 참조입니다. 비어 있으면 씬에서 자동 탐색합니다.")]
    [SerializeField] private SfxOrchestrator _sfxOrchestrator; // 이벤트 타입 기반 SFX 라우팅을 담당하는 오케스트레이터 참조입니다.
    [Tooltip("이동/점프/대시/착지 상태를 조회할 PlayerMovement 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 이동 상태 기반 SFX 트리거 판정에 사용할 플레이어 이동 컴포넌트 참조입니다.
    [Tooltip("피격 결과 이벤트를 구독할 HitReceiver 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private HitReceiver _hitReceiver; // 피격 수락 이벤트를 수신해 피격 SFX를 트리거할 HitReceiver 참조입니다.
    [Tooltip("루트 NetworkObject에 부착된 SFX 네트워크 릴레이 참조입니다. 비어 있으면 자신/상위에서 자동 탐색합니다.")]
    [SerializeField] private SfxNetworkRelay _networkRelay; // 루트 오브젝트 네트워크 이벤트 전파를 담당하는 릴레이 참조입니다.

    [Header("Routing")]
    [Tooltip("true면 SfxOrchestrator.Request를 우선 사용하고, 실패 시 fallback SoundId 재생을 시도합니다.")]
    [SerializeField] private bool _useOrchestratorFirst = true; // 오케스트레이터 우선 라우팅 사용 여부를 제어하는 옵션입니다.

    [Header("Move SFX")]
    [Tooltip("이동 SFX 요청에 사용할 이벤트 타입입니다.")]
    [SerializeField] private E_SfxEventType _moveEventType = E_SfxEventType.PlayerMove; // 이동 SFX 라우팅에 사용할 이벤트 타입입니다.
    [Tooltip("이동 SFX 요청 시 전달할 서브 타입 키입니다.")]
    [SerializeField] private string _moveSubTypeKey = string.Empty; // 이동 SFX 라우팅 세분화에 사용할 서브 타입 키입니다.
    [Tooltip("이동 SFX 중지 복제 요청 시 전달할 서브 타입 키입니다.")]
    [SerializeField] private string _moveStopSubTypeKey = "stop"; // 이동 SFX 중지 복제 이벤트 식별에 사용할 서브 타입 키입니다.
    [Tooltip("이동 SFX를 fallback으로 직접 재생할 SoundId입니다.")]
    [SerializeField] private E_SoundId _moveFallbackSoundId = E_SoundId.SFX_Player_Running; // 이동 SFX 라우팅 실패 시 사용할 fallback 사운드 ID입니다.
    [Tooltip("이동 SFX 중지 요청 시 사용할 SoundId입니다. None이면 Move Fallback SoundId를 사용합니다.")]
    [SerializeField] private E_SoundId _moveStopSoundId = E_SoundId.None; // 이동 정지 시 StopSfx 대상으로 사용할 사운드 ID입니다.
    [Tooltip("이동 SFX 최소 재생 간격(초)입니다. 매 프레임 재생을 방지합니다.")]
    [SerializeField] private float _moveMinInterval = 0.12f; // 이동 SFX 과다 재생 방지를 위한 최소 재생 간격 값입니다.
    [Tooltip("이동 SFX 재생을 시작할 최소 수평 속도 절대값입니다.")]
    [SerializeField] private float _moveMinHorizontalSpeed = 0.25f; // 이동 중 상태 판정에 사용할 최소 수평 속도 임계값입니다.

    [Header("Jump SFX")]
    [Tooltip("점프 SFX 요청에 사용할 이벤트 타입입니다.")]
    [SerializeField] private E_SfxEventType _jumpEventType = E_SfxEventType.PlayerJump; // 점프 SFX 라우팅에 사용할 이벤트 타입입니다.
    [Tooltip("점프 SFX 요청 시 전달할 서브 타입 키입니다.")]
    [SerializeField] private string _jumpSubTypeKey = string.Empty; // 점프 SFX 라우팅 세분화에 사용할 서브 타입 키입니다.
    [Tooltip("점프 SFX를 fallback으로 직접 재생할 SoundId입니다.")]
    [SerializeField] private E_SoundId _jumpFallbackSoundId = E_SoundId.SFX_Player_Jumping; // 점프 SFX 라우팅 실패 시 사용할 fallback 사운드 ID입니다.
    [Tooltip("점프 시작으로 판정할 최소 상승 속도입니다.")]
    [SerializeField] private float _jumpMinUpwardVelocity = 0.05f; // 점프 시작 판정에 사용할 최소 상승 속도 임계값입니다.

    [Header("Land SFX")]
    [Tooltip("착지 SFX 요청에 사용할 이벤트 타입입니다.")]
    [SerializeField] private E_SfxEventType _landEventType = E_SfxEventType.PlayerLand; // 착지 SFX 라우팅에 사용할 이벤트 타입입니다.
    [Tooltip("착지 SFX 요청 시 전달할 서브 타입 키입니다.")]
    [SerializeField] private string _landSubTypeKey = string.Empty; // 착지 SFX 라우팅 세분화에 사용할 서브 타입 키입니다.
    [Tooltip("착지 SFX를 fallback으로 직접 재생할 SoundId입니다.")]
    [SerializeField] private E_SoundId _landFallbackSoundId = E_SoundId.SFX_Player_Landing; // 착지 SFX 라우팅 실패 시 사용할 fallback 사운드 ID입니다.

    [Header("Dash SFX")]
    [Tooltip("대시 SFX 요청에 사용할 이벤트 타입입니다.")]
    [SerializeField] private E_SfxEventType _dashEventType = E_SfxEventType.PlayerDash; // 대시 SFX 라우팅에 사용할 이벤트 타입입니다.
    [Tooltip("대시 SFX 요청 시 전달할 서브 타입 키입니다.")]
    [SerializeField] private string _dashSubTypeKey = string.Empty; // 대시 SFX 라우팅 세분화에 사용할 서브 타입 키입니다.
    [Tooltip("대시 SFX를 fallback으로 직접 재생할 SoundId입니다.")]
    [SerializeField] private E_SoundId _dashFallbackSoundId = E_SoundId.SFX_Player_Dash; // 대시 SFX 라우팅 실패 시 사용할 fallback 사운드 ID입니다.

    [Header("Hit SFX")]
    [Tooltip("플레이어 피격 SFX 요청에 사용할 이벤트 타입입니다.")]
    [SerializeField] private E_SfxEventType _playerHitEventType = E_SfxEventType.PlayerHit; // 플레이어 피격 SFX 라우팅에 사용할 이벤트 타입입니다.
    [Tooltip("플레이어 피격 SFX 요청 시 전달할 서브 타입 키입니다.")]
    [SerializeField] private string _playerHitSubTypeKey = string.Empty; // 플레이어 피격 SFX 라우팅 세분화에 사용할 서브 타입 키입니다.
    [Tooltip("플레이어 피격 SFX를 fallback으로 직접 재생할 SoundId입니다.")]
    [SerializeField] private E_SoundId _playerHitFallbackSoundId = E_SoundId.SFX_Player_Hit; // 플레이어 피격 SFX 라우팅 실패 시 사용할 fallback 사운드 ID입니다.

    private bool _isHitListenerRegistered; // HitReceiver 리스너 등록 상태를 추적하는 플래그입니다.
    private bool _isRelaySubscribed; // 네트워크 릴레이 콜백 구독 상태를 추적하는 플래그입니다.
    private bool _isMoveLoopPlaying; // 현재 이동 루프 SFX를 재생 중인지 추적하는 플래그입니다.
    private bool _wasGroundedLastFrame; // 착지 전환(공중->지상) 판정을 위해 이전 프레임 지면 상태를 보관하는 플래그입니다.
    private bool _wasDashingLastFrame; // 대시 시작 엣지 판정을 위해 이전 프레임 대시 상태를 보관하는 플래그입니다.
    private float _previousVerticalVelocity; // 2단 점프 판정을 위해 직전 프레임 수직 속도를 보관하는 값입니다.
    private float _nextMovePlayableTime; // 이동 SFX의 다음 재생 가능 시각을 기록하는 타이머 값입니다.

    /// <summary>
    /// 참조 보정과 설정값 보정을 수행하고 초기 상태를 캐시합니다.
    /// </summary>
    private void Awake()
    {
        ValidateSettings();
        TryResolveDependencies();

        _wasGroundedLastFrame = IsGroundedNow();
        _wasDashingLastFrame = IsDashingNow();
        _previousVerticalVelocity = _playerMovement != null ? _playerMovement.Velocity.y : 0f;
    }

    /// <summary>
    /// 활성화 시 피격/릴레이 리스너를 등록하고 상태 캐시를 초기화합니다.
    /// </summary>
    private void OnEnable()
    {
        TryResolveDependencies();
        RegisterHitListener();
        RegisterRelayListener();

        _wasGroundedLastFrame = IsGroundedNow();
        _wasDashingLastFrame = IsDashingNow();
        _previousVerticalVelocity = _playerMovement != null ? _playerMovement.Velocity.y : 0f;
        _nextMovePlayableTime = 0f;
    }

    /// <summary>
    /// 비활성화 시 피격/릴레이 리스너를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        StopMoveSfxIfPlaying("OnDisable");
        UnregisterHitListener();
        UnregisterRelayListener();
    }

    /// <summary>
    /// 매 프레임 이동/점프/착지/대시 트리거를 평가해 SFX를 요청합니다.
    /// </summary>
    private void Update()
    {
        if (_playerMovement == null || _playerMovement.Controller == null)
        {
            return;
        }

        HandleMoveSfx();
        HandleJumpSfx();
        HandleLandSfx();
        HandleDashSfx();
    }

    /// <summary>
    /// 피격 결과를 수신해 수락된 경우 플레이어 피격 SFX를 재생합니다.
    /// </summary>
    public void OnHitResolved(HitRequest request, HitResult result)
    {
        if (!result.IsAccepted)
        {
            return;
        }

        if (_networkRelay != null && _networkRelay.IsNetworkSessionActive() && _networkRelay.IsServerRelay())
        {
            TryRequestSfx(_playerHitEventType, _playerHitSubTypeKey, _playerHitFallbackSoundId, transform, "PlayerHit.Server");
            _networkRelay.BroadcastServerAuthoritativeSfx(_playerHitEventType, _playerHitSubTypeKey, _playerHitFallbackSoundId);
            return;
        }

        TriggerStateReplicatedSfx(_playerHitEventType, _playerHitSubTypeKey, _playerHitFallbackSoundId, "PlayerHit");
    }

    /// <summary>
    /// 네트워크 릴레이로부터 복제된 상태 SFX를 수신해 재생합니다.
    /// </summary>
    private void HandleReplicatedSfxReceived(E_SfxEventType eventType, string subTypeKey, E_SoundId fallbackSoundId)
    {
        if (eventType == _moveEventType && IsMoveStopSubType(subTypeKey))
        {
            StopMoveSfxIfPlaying("PlayerMove.ClientRelayStop", fallbackSoundId);
            return;
        }

        if (eventType == _moveEventType)
        {
            bool hasRequestedMoveSfx = TryRequestSfx(eventType, subTypeKey, fallbackSoundId, transform, "PlayerMove.ClientRelayStart"); // 클라이언트 복제 이동 SFX 요청 성공 여부입니다.
            if (hasRequestedMoveSfx)
            {
                _isMoveLoopPlaying = true;
            }

            return;
        }

        TryRequestSfx(eventType, subTypeKey, fallbackSoundId, transform, "StateSync.ClientRelay");
    }

    /// <summary>
    /// 이동 중 조건을 만족하면 최소 간격 정책으로 이동 SFX를 재생합니다.
    /// </summary>
    private void HandleMoveSfx()
    {
        if (_networkRelay != null && _networkRelay.IsNetworkSessionActive() && !_networkRelay.IsOwnerRelay())
        {
            return;
        }

        bool canProcessLocalInputSfx = ShouldProcessLocalInputSfx(); // 로컬 입력 기반 이동 SFX를 처리할 권한이 있는지 여부입니다.
        bool isGrounded = IsGroundedNow(); // 현재 프레임 지면 접촉 상태입니다.
        float horizontalSpeed = Mathf.Abs(_playerMovement.Velocity.x); // 이동 SFX 재생 조건 판정에 사용할 현재 수평 속도입니다.
        bool isMoveConditionSatisfied = canProcessLocalInputSfx && isGrounded && horizontalSpeed >= _moveMinHorizontalSpeed; // 이동 루프 SFX 재생 조건 만족 여부입니다.

        if (!isMoveConditionSatisfied)
        {
            TriggerMoveStop();
            return;
        }

        if (_isMoveLoopPlaying)
        {
            return;
        }

        if (Time.time < _nextMovePlayableTime)
        {
            return;
        }

        _nextMovePlayableTime = Time.time + _moveMinInterval;
        TriggerMoveStart();
    }

    /// <summary>
    /// 지면 이탈 + 상승 속도 조건을 만족하면 점프 SFX를 재생합니다.
    /// </summary>
    private void HandleJumpSfx()
    {
        bool canProcessLocalInputSfx = ShouldProcessLocalInputSfx(); // 로컬 입력 기반 점프 SFX를 처리할 권한이 있는지 여부입니다.
        if (!canProcessLocalInputSfx)
        {
            _previousVerticalVelocity = _playerMovement != null ? _playerMovement.Velocity.y : _previousVerticalVelocity;
            return;
        }

        bool isGrounded = IsGroundedNow(); // 현재 프레임 지면 접촉 상태입니다.
        float verticalSpeed = _playerMovement.Velocity.y; // 점프 시작 판정에 사용할 현재 수직 속도입니다.
        bool isGroundJump = _wasGroundedLastFrame && !isGrounded && verticalSpeed >= _jumpMinUpwardVelocity; // 지면 이탈 기반 점프 시작 여부입니다.
        bool isAirDoubleJump = !_wasGroundedLastFrame && !isGrounded &&
                               _previousVerticalVelocity < _jumpMinUpwardVelocity &&
                               verticalSpeed >= _jumpMinUpwardVelocity; // 공중에서 상승 속도가 재획득된 2단 점프 여부입니다.

        if (isGroundJump || isAirDoubleJump)
        {
            TriggerStateReplicatedSfx(_jumpEventType, _jumpSubTypeKey, _jumpFallbackSoundId, "PlayerJump");
        }

        _previousVerticalVelocity = verticalSpeed;
    }

    /// <summary>
    /// 공중 상태에서 지면 상태로 전환되는 순간에만 착지 SFX를 재생합니다.
    /// </summary>
    private void HandleLandSfx()
    {
        bool isGrounded = IsGroundedNow(); // 현재 프레임 지면 접촉 상태입니다.
        bool justLanded = !_wasGroundedLastFrame && isGrounded; // 착지 전환(공중->지상) 여부입니다.

        if (justLanded)
        {
            TriggerStateReplicatedSfx(_landEventType, _landSubTypeKey, _landFallbackSoundId, "PlayerLand");
            TriggerMoveStop();
        }

        _wasGroundedLastFrame = isGrounded;
    }

    /// <summary>
    /// 대시 상태가 false에서 true로 바뀐 프레임에 대시 SFX를 재생합니다.
    /// </summary>
    private void HandleDashSfx()
    {
        if (ShouldProcessLocalInputSfx() == false)
        {
            _wasDashingLastFrame = IsDashingNow();
            return;
        }

        bool isDashing = IsDashingNow(); // 현재 프레임 대시 진행 상태입니다.
        bool justStartedDash = !_wasDashingLastFrame && isDashing; // 대시 시작 엣지 이벤트 여부입니다.

        if (justStartedDash)
        {
            TriggerStateReplicatedSfx(_dashEventType, _dashSubTypeKey, _dashFallbackSoundId, "PlayerDash");
        }

        _wasDashingLastFrame = isDashing;
    }

    /// <summary>
    /// 상태 변화 기반 SFX를 단일/네트워크 환경에 맞춰 중복 없이 재생합니다.
    /// </summary>
    private void TriggerStateReplicatedSfx(E_SfxEventType eventType, string subTypeKey, E_SoundId fallbackSoundId, string debugTag)
    {
        if (_networkRelay == null || !_networkRelay.IsNetworkSessionActive())
        {
            TryRequestSfx(eventType, subTypeKey, fallbackSoundId, transform, debugTag);
            return;
        }

        SfxNetworkRelay.E_OwnerDispatchResult dispatchResult = _networkRelay.DispatchOwnerStateSfx(eventType, subTypeKey, fallbackSoundId); // 소유자 상태 SFX 전파 결과 코드입니다.

        if (dispatchResult == SfxNetworkRelay.E_OwnerDispatchResult.NotNetworked)
        {
            TryRequestSfx(eventType, subTypeKey, fallbackSoundId, transform, debugTag);
            return;
        }

        if (dispatchResult == SfxNetworkRelay.E_OwnerDispatchResult.BroadcastFromServer)
        {
            TryRequestSfx(eventType, subTypeKey, fallbackSoundId, transform, debugTag);
        }
    }

    /// <summary>
    /// 오케스트레이터 우선 라우팅 후 필요 시 fallback 재생을 수행합니다.
    /// </summary>
    private bool TryRequestSfx(E_SfxEventType eventType, string subTypeKey, E_SoundId fallbackSoundId, Transform emitter, string debugTag)
    {
        bool requestedByOrchestrator = false; // 오케스트레이터 라우팅 성공 여부를 추적하는 플래그입니다.

        if (_useOrchestratorFirst)
        {
            SfxOrchestrator orchestrator = ResolveOrchestrator(); // 현재 요청에 사용할 오케스트레이터 참조입니다.
            if (orchestrator != null)
            {
                requestedByOrchestrator = orchestrator.Request(eventType, subTypeKey ?? string.Empty, emitter);
            }
        }

        if (requestedByOrchestrator)
        {
            return true;
        }

        if (fallbackSoundId == E_SoundId.None)
        {
            Debug.LogWarning($"[PlayerSfxBridge] {debugTag} fallback SoundId가 None이라 요청을 재생하지 못했습니다. target={name}, eventType={eventType}", this);
            return false;
        }

        AudioManager audioManager = AudioManager.Instance; // fallback 직접 재생에 사용할 AudioManager 인스턴스입니다.
        if (audioManager == null)
        {
            Debug.LogWarning($"[PlayerSfxBridge] {debugTag} AudioManager를 찾지 못해 fallback 재생에 실패했습니다. target={name}, eventType={eventType}, soundId={fallbackSoundId}", this);
            return false;
        }

        Debug.LogWarning($"[PlayerSfxBridge] {debugTag} Orchestrator 요청 실패로 fallback SoundId를 직접 재생합니다. target={name}, eventType={eventType}, soundId={fallbackSoundId}", this);
        audioManager.PlaySfx(fallbackSoundId, emitter);
        return true;
    }

    /// <summary>
    /// 이동 루프 SFX가 재생 중이면 즉시 정지 요청을 보낸다.
    /// </summary>
    private void StopMoveSfxIfPlaying(string debugTag)
    {
        StopMoveSfxIfPlaying(debugTag, E_SoundId.None);
    }

    /// <summary>
    /// 이동 루프 SFX가 재생 중이면 지정 SoundId(또는 기본 설정) 기준으로 즉시 정지 요청을 보낸다.
    /// </summary>
    private void StopMoveSfxIfPlaying(string debugTag, E_SoundId overrideStopSoundId)
    {
        if (!_isMoveLoopPlaying)
        {
            return;
        }

        E_SoundId configuredStopSoundId = _moveStopSoundId != E_SoundId.None ? _moveStopSoundId : _moveFallbackSoundId; // 인스펙터 설정 기준 정지 대상 사운드 ID입니다.
        E_SoundId stopTargetSoundId = overrideStopSoundId != E_SoundId.None ? overrideStopSoundId : configuredStopSoundId; // 실제 StopSfx 대상으로 사용할 최종 사운드 ID입니다.
        if (stopTargetSoundId == E_SoundId.None)
        {
            Debug.LogWarning($"[PlayerSfxBridge] {debugTag} 이동 SFX 중지 대상 SoundId가 None이라 StopSfx를 수행할 수 없습니다. target={name}", this);
            _isMoveLoopPlaying = false;
            return;
        }

        AudioManager audioManager = AudioManager.Instance; // 이동 루프 정지 요청을 전달할 AudioManager 인스턴스입니다.
        if (audioManager == null)
        {
            Debug.LogWarning($"[PlayerSfxBridge] {debugTag} AudioManager를 찾지 못해 이동 SFX StopSfx를 수행할 수 없습니다. target={name}", this);
            _isMoveLoopPlaying = false;
            return;
        }

        audioManager.StopSfx(stopTargetSoundId, transform);
        _isMoveLoopPlaying = false;
    }

    /// <summary>
    /// 이동 루프 시작 이벤트를 단일/네트워크 환경에 맞춰 재생한다.
    /// </summary>
    private void TriggerMoveStart()
    {
        if (_networkRelay == null || !_networkRelay.IsNetworkSessionActive())
        {
            bool hasRequestedMoveSfx = TryRequestSfx(_moveEventType, _moveSubTypeKey, _moveFallbackSoundId, transform, "PlayerMove.Start.Local"); // 단일 모드 이동 시작 SFX 요청 성공 여부입니다.
            if (hasRequestedMoveSfx)
            {
                _isMoveLoopPlaying = true;
            }

            return;
        }

        SfxNetworkRelay.E_OwnerDispatchResult dispatchResult = _networkRelay.DispatchOwnerStateSfx(_moveEventType, _moveSubTypeKey, _moveFallbackSoundId); // 이동 시작 복제 전파 결과 코드입니다.
        if (dispatchResult == SfxNetworkRelay.E_OwnerDispatchResult.NotNetworked ||
            dispatchResult == SfxNetworkRelay.E_OwnerDispatchResult.BroadcastFromServer)
        {
            bool hasRequestedMoveSfx = TryRequestSfx(_moveEventType, _moveSubTypeKey, _moveFallbackSoundId, transform, "PlayerMove.Start.ServerOwner"); // 서버 소유자 로컬 이동 시작 SFX 요청 성공 여부입니다.
            if (hasRequestedMoveSfx)
            {
                _isMoveLoopPlaying = true;
            }
        }
    }

    /// <summary>
    /// 이동 루프 중지 이벤트를 단일/네트워크 환경에 맞춰 처리한다.
    /// </summary>
    private void TriggerMoveStop()
    {
        E_SoundId stopTargetSoundId = _moveStopSoundId != E_SoundId.None ? _moveStopSoundId : _moveFallbackSoundId; // 이동 중지 복제/정지에 사용할 기준 사운드 ID입니다.

        if (_networkRelay == null || !_networkRelay.IsNetworkSessionActive())
        {
            StopMoveSfxIfPlaying("PlayerMove.Stop.Local", stopTargetSoundId);
            return;
        }

        if (!_isMoveLoopPlaying)
        {
            return;
        }

        SfxNetworkRelay.E_OwnerDispatchResult dispatchResult = _networkRelay.DispatchOwnerStateSfx(_moveEventType, _moveStopSubTypeKey, stopTargetSoundId); // 이동 중지 복제 전파 결과 코드입니다.
        if (dispatchResult == SfxNetworkRelay.E_OwnerDispatchResult.NotNetworked ||
            dispatchResult == SfxNetworkRelay.E_OwnerDispatchResult.BroadcastFromServer)
        {
            StopMoveSfxIfPlaying("PlayerMove.Stop.ServerOwner", stopTargetSoundId);
        }
    }

    /// <summary>
    /// 전달된 서브 타입 키가 이동 중지 이벤트인지 판정한다.
    /// </summary>
    private bool IsMoveStopSubType(string subTypeKey)
    {
        if (string.IsNullOrEmpty(_moveStopSubTypeKey))
        {
            return false;
        }

        string normalizedInput = subTypeKey == null ? string.Empty : subTypeKey.Trim().ToLowerInvariant(); // 비교용 입력 서브 타입 정규화 값입니다.
        string normalizedStopKey = _moveStopSubTypeKey.Trim().ToLowerInvariant(); // 비교용 이동 중지 서브 타입 정규화 값입니다.
        return normalizedInput == normalizedStopKey;
    }

    /// <summary>
    /// 로컬 입력 기반 SFX를 현재 인스턴스에서 처리해야 하는지 반환합니다.
    /// </summary>
    private bool ShouldProcessLocalInputSfx()
    {
        if (_networkRelay != null && _networkRelay.IsNetworkSessionActive())
        {
            return _networkRelay.IsOwnerRelay();
        }

        return true;
    }

    /// <summary>
    /// 현재 지면 접촉 상태를 안전하게 반환합니다.
    /// </summary>
    private bool IsGroundedNow()
    {
        if (_playerMovement == null || _playerMovement.Controller == null)
        {
            return false;
        }

        return _playerMovement.Controller.IsGrounded();
    }

    /// <summary>
    /// 현재 대시 상태를 안전하게 반환합니다.
    /// </summary>
    private bool IsDashingNow()
    {
        return _playerMovement != null && _playerMovement.IsDashing;
    }

    /// <summary>
    /// 누락된 참조를 같은 오브젝트 또는 씬 탐색으로 보정합니다.
    /// </summary>
    private void TryResolveDependencies()
    {
        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }

        if (_hitReceiver == null)
        {
            _hitReceiver = GetComponent<HitReceiver>();
        }

        if (_networkRelay == null)
        {
            _networkRelay = GetComponent<SfxNetworkRelay>();
        }

        if (_networkRelay == null)
        {
            _networkRelay = GetComponentInParent<SfxNetworkRelay>();
        }

        ResolveOrchestrator();
    }

    /// <summary>
    /// SfxOrchestrator 참조를 반환하고 필요 시 씬 탐색으로 보정합니다.
    /// </summary>
    private SfxOrchestrator ResolveOrchestrator()
    {
        if (_sfxOrchestrator != null)
        {
            return _sfxOrchestrator;
        }

        _sfxOrchestrator = FindAnyObjectByType<SfxOrchestrator>();
        return _sfxOrchestrator;
    }

    /// <summary>
    /// HitReceiver 리스너를 안전하게 등록합니다.
    /// </summary>
    private void RegisterHitListener()
    {
        if (_isHitListenerRegistered)
        {
            return;
        }

        if (_hitReceiver == null)
        {
            return;
        }

        _hitReceiver.AddListener(this);
        _isHitListenerRegistered = true;
    }

    /// <summary>
    /// HitReceiver 리스너를 안전하게 해제합니다.
    /// </summary>
    private void UnregisterHitListener()
    {
        if (_isHitListenerRegistered == false)
        {
            return;
        }

        if (_hitReceiver != null)
        {
            _hitReceiver.RemoveListener(this);
        }

        _isHitListenerRegistered = false;
    }

    /// <summary>
    /// 네트워크 릴레이 이벤트를 안전하게 등록합니다.
    /// </summary>
    private void RegisterRelayListener()
    {
        if (_isRelaySubscribed)
        {
            return;
        }

        if (_networkRelay == null)
        {
            return;
        }

        _networkRelay.ReplicatedSfxReceived += HandleReplicatedSfxReceived;
        _isRelaySubscribed = true;
    }

    /// <summary>
    /// 네트워크 릴레이 이벤트를 안전하게 해제합니다.
    /// </summary>
    private void UnregisterRelayListener()
    {
        if (_isRelaySubscribed == false)
        {
            return;
        }

        if (_networkRelay != null)
        {
            _networkRelay.ReplicatedSfxReceived -= HandleReplicatedSfxReceived;
        }

        _isRelaySubscribed = false;
    }

    /// <summary>
    /// 인스펙터 입력 설정값의 최소 제약을 보정합니다.
    /// </summary>
    private void ValidateSettings()
    {
        _moveMinInterval = Mathf.Max(0.01f, _moveMinInterval);
        _moveMinHorizontalSpeed = Mathf.Max(0f, _moveMinHorizontalSpeed);
        _jumpMinUpwardVelocity = Mathf.Max(0f, _jumpMinUpwardVelocity);
    }
}
