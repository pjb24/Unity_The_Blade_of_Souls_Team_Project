using UnityEngine;

/// <summary>
/// 몬스터 피격 이벤트를 기존 SFX 시스템으로 라우팅하는 브리지입니다.
/// </summary>
public class EnemySfxBridge : MonoBehaviour, IHitListener
{
    [Header("Dependencies")]
    [Tooltip("SFX 이벤트 라우팅을 수행할 오케스트레이터 참조입니다. 비어 있으면 씬에서 자동 탐색합니다.")]
    [SerializeField] private SfxOrchestrator _sfxOrchestrator; // 이벤트 타입 기반 SFX 라우팅을 담당하는 오케스트레이터 참조입니다.
    [Tooltip("피격 결과 이벤트를 구독할 HitReceiver 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private HitReceiver _hitReceiver; // 피격 결과 수락 이벤트를 구독할 HitReceiver 참조입니다.
    [Tooltip("루트 NetworkObject에 부착된 SFX 네트워크 릴레이 참조입니다. 비어 있으면 자신/상위에서 자동 탐색합니다.")]
    [SerializeField] private SfxNetworkRelay _networkRelay; // 루트 오브젝트 네트워크 이벤트 전파를 담당하는 릴레이 참조입니다.

    [Header("Routing")]
    [Tooltip("true면 SfxOrchestrator.Request를 우선 사용하고, 실패 시 fallback SoundId 재생을 시도합니다.")]
    [SerializeField] private bool _useOrchestratorFirst = true; // 오케스트레이터 우선 라우팅 사용 여부를 제어하는 옵션입니다.

    [Header("Enemy Hit SFX")]
    [Tooltip("몬스터 피격 SFX 요청에 사용할 이벤트 타입입니다.")]
    [SerializeField] private E_SfxEventType _enemyHitEventType = E_SfxEventType.EnemyHit; // 몬스터 피격 SFX 라우팅에 사용할 이벤트 타입입니다.
    [Tooltip("몬스터 피격 SFX 요청 시 전달할 서브 타입 키입니다.")]
    [SerializeField] private string _enemyHitSubTypeKey = string.Empty; // 몬스터 피격 SFX 라우팅 세분화에 사용할 서브 타입 키입니다.
    [Tooltip("몬스터 피격 SFX를 fallback으로 직접 재생할 SoundId입니다.")]
    [SerializeField] private E_SoundId _enemyHitFallbackSoundId = E_SoundId.SFX_Hit; // 몬스터 피격 SFX 라우팅 실패 시 사용할 fallback 사운드 ID입니다.

    private bool _isHitListenerRegistered; // HitReceiver 리스너 등록 상태를 추적하는 플래그입니다.
    private bool _isRelaySubscribed; // 네트워크 릴레이 콜백 구독 상태를 추적하는 플래그입니다.

    /// <summary>
    /// 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveDependencies();
    }

    /// <summary>
    /// 활성화 시 피격/릴레이 리스너를 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        TryResolveDependencies();
        RegisterHitListener();
        RegisterRelayListener();
    }

    /// <summary>
    /// 비활성화 시 피격/릴레이 리스너를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterHitListener();
        UnregisterRelayListener();
    }

    /// <summary>
    /// 피격 결과를 수신해 수락된 경우 몬스터 피격 SFX를 재생합니다.
    /// </summary>
    public void OnHitResolved(HitRequest request, HitResult result)
    {
        if (!result.IsAccepted)
        {
            return;
        }

        if (_networkRelay != null && _networkRelay.IsNetworkSessionActive())
        {
            if (_networkRelay.IsServerRelay())
            {
                TryRequestSfx(_enemyHitEventType, _enemyHitSubTypeKey, _enemyHitFallbackSoundId, transform, "EnemyHit.Server");
                _networkRelay.BroadcastServerAuthoritativeSfx(_enemyHitEventType, _enemyHitSubTypeKey, _enemyHitFallbackSoundId);
            }

            return;
        }

        TryRequestSfx(_enemyHitEventType, _enemyHitSubTypeKey, _enemyHitFallbackSoundId, transform, "EnemyHit.Single");
    }

    /// <summary>
    /// 네트워크 릴레이로부터 복제된 몬스터 피격 SFX를 수신해 재생합니다.
    /// </summary>
    private void HandleReplicatedSfxReceived(E_SfxEventType eventType, string subTypeKey, E_SoundId fallbackSoundId)
    {
        if (eventType != _enemyHitEventType)
        {
            return;
        }

        TryRequestSfx(eventType, subTypeKey, fallbackSoundId, transform, "EnemyHit.ClientRelay");
    }

    /// <summary>
    /// 오케스트레이터 우선 라우팅 후 필요 시 fallback 재생을 수행합니다.
    /// </summary>
    private void TryRequestSfx(E_SfxEventType eventType, string subTypeKey, E_SoundId fallbackSoundId, Transform emitter, string debugTag)
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
            return;
        }

        if (fallbackSoundId == E_SoundId.None)
        {
            Debug.LogWarning($"[EnemySfxBridge] {debugTag} fallback SoundId가 None이라 요청을 재생하지 못했습니다. target={name}, eventType={eventType}", this);
            return;
        }

        AudioManager audioManager = AudioManager.Instance; // fallback 직접 재생에 사용할 AudioManager 인스턴스입니다.
        if (audioManager == null)
        {
            Debug.LogWarning($"[EnemySfxBridge] {debugTag} AudioManager를 찾지 못해 fallback 재생에 실패했습니다. target={name}, eventType={eventType}, soundId={fallbackSoundId}", this);
            return;
        }

        Debug.LogWarning($"[EnemySfxBridge] {debugTag} Orchestrator 요청 실패로 fallback SoundId를 직접 재생합니다. target={name}, eventType={eventType}, soundId={fallbackSoundId}", this);
        audioManager.PlaySfx(fallbackSoundId, emitter);
    }

    /// <summary>
    /// 누락된 참조를 같은 오브젝트 또는 씬 탐색으로 보정합니다.
    /// </summary>
    private void TryResolveDependencies()
    {
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
}
