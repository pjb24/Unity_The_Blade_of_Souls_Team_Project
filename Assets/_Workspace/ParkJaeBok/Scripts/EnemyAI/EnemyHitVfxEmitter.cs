using UnityEngine;

/// <summary>
/// 몬스터 피격 확정 이벤트를 수신해 Anchor 월드 좌표에서 1회성 피격 VFX를 재생하는 컴포넌트입니다.
/// 상위 루트의 NetworkObject + EnemyHitVfxNetworkRelay를 통해 멀티플레이 동기화를 수행합니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHitVfxEmitter : MonoBehaviour, IHitListener
{
    [Header("Dependencies")]
    [Tooltip("피격 확정 결과를 수신할 HitReceiver 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private HitReceiver _hitReceiver; // 피격 확정 결과를 수신할 HitReceiver 참조입니다.
    [Tooltip("상위 루트 NetworkObject에서 RPC를 실행할 EnemyHitVfxNetworkRelay 참조입니다. 비어 있으면 부모 계층에서 자동 탐색합니다.")]
    [SerializeField] private EnemyHitVfxNetworkRelay _networkRelay; // 상위 루트 네트워크 릴레이 참조입니다.

    [Header("Hit VFX")]
    [Tooltip("피격 VFX 생성 위치로 사용할 Anchor Transform입니다. 비어 있으면 몬스터 Transform을 fallback으로 사용합니다.")]
    [SerializeField] private Transform _hitVfxAnchor; // 피격 VFX 월드 위치 계산 기준 Anchor입니다.
    [Tooltip("피격 시 EffectService에서 재생할 VFX ID입니다.")]
    [SerializeField] private E_EffectId _hitVfxEffectId = E_EffectId.HitSmall; // 피격 시 재생할 VFX 식별자입니다.

    [Header("Debug")]
    [Tooltip("네트워크 미활성/비정상 상태에서 fallback 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseFallbackLog = true; // 네트워크 fallback 경고 로그 출력 여부입니다.

    private bool _isListenerRegistered; // HitReceiver 리스너 등록 여부입니다.

    /// <summary>
    /// 에디터 값 변경 시 설정 누락을 검사합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_hitReceiver == null)
        {
            Debug.LogWarning($"[EnemyHitVfxEmitter] HitReceiver 참조가 비어 있습니다. target={name}", this);
        }

        if (_networkRelay == null)
        {
            Debug.LogWarning($"[EnemyHitVfxEmitter] NetworkRelay 참조가 비어 있습니다. target={name}, parent fallback search enabled", this);
        }

        if (_hitVfxAnchor == null)
        {
            Debug.LogWarning($"[EnemyHitVfxEmitter] Hit VFX Anchor가 비어 있습니다. target={name}, fallback=transform", this);
        }

        if (_hitVfxEffectId == E_EffectId.None)
        {
            Debug.LogWarning($"[EnemyHitVfxEmitter] Hit VFX ID가 None입니다. target={name}", this);
        }
    }

    /// <summary>
    /// 초기화 시 의존성 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_hitReceiver == null)
        {
            _hitReceiver = GetComponent<HitReceiver>();
        }

        if (_networkRelay == null)
        {
            _networkRelay = GetComponentInParent<EnemyHitVfxNetworkRelay>();
        }

        if (_hitReceiver == null)
        {
            Debug.LogWarning($"[EnemyHitVfxEmitter] HitReceiver를 찾지 못했습니다. target={name}", this);
        }

        if (_networkRelay == null && _verboseFallbackLog)
        {
            Debug.LogWarning($"[EnemyHitVfxEmitter] 부모 계층에서 EnemyHitVfxNetworkRelay를 찾지 못했습니다. 네트워크 세션에서는 로컬 폴백이 발생할 수 있습니다. target={name}", this);
        }
    }

    /// <summary>
    /// 활성화 시 HitReceiver 리스너를 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        RegisterListener();
    }

    /// <summary>
    /// 비활성화 시 HitReceiver 리스너를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterListener();
    }

    /// <summary>
    /// 피격 처리 결과를 수신해 수락된 피격만 VFX를 재생합니다.
    /// </summary>
    public void OnHitResolved(HitRequest request, HitResult result)
    {
        if (!result.IsAccepted)
        {
            return;
        }

        Vector3 spawnPosition = ResolveSpawnPosition();

        if (!IsNetworkSessionActive())
        {
            PlayLocalHitVfx(spawnPosition);
            return;
        }

        if (_networkRelay == null)
        {
            if (_verboseFallbackLog)
            {
                Debug.LogWarning($"[EnemyHitVfxEmitter] 네트워크 세션 중 NetworkRelay가 없어 로컬 재생으로 폴백합니다. target={name}", this);
            }

            PlayLocalHitVfx(spawnPosition);
            return;
        }

        if (_networkRelay.TryReplicateHitVfx(this, spawnPosition))
        {
            return;
        }

        if (_verboseFallbackLog)
        {
            Debug.LogWarning($"[EnemyHitVfxEmitter] 서버 권한이 아닌 인스턴스에서 피격 이벤트를 수신해 로컬 재생을 차단합니다. target={name}", this);
        }
    }

    /// <summary>
    /// NetworkRelay에서 호출되는 로컬 재생 진입점입니다.
    /// </summary>
    public void PlayLocalFromNetwork(Vector3 worldPosition)
    {
        PlayLocalHitVfx(worldPosition);
    }

    /// <summary>
    /// EffectService를 사용해 지정 월드 좌표에 피격 VFX를 1회 재생합니다.
    /// </summary>
    private void PlayLocalHitVfx(Vector3 worldPosition)
    {
        if (_hitVfxEffectId == E_EffectId.None)
        {
            Debug.LogWarning($"[EnemyHitVfxEmitter] Hit VFX ID가 None이라 재생을 건너뜁니다. target={name}", this);
            return;
        }

        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[EnemyHitVfxEmitter] EffectService가 없어 피격 VFX를 재생하지 못했습니다. target={name}", this);
            return;
        }

        EffectService.Instance.Play(_hitVfxEffectId, worldPosition);
    }

    /// <summary>
    /// Anchor가 설정되어 있으면 해당 월드 좌표를, 아니면 Transform 좌표를 반환합니다.
    /// </summary>
    private Vector3 ResolveSpawnPosition()
    {
        if (_hitVfxAnchor != null)
        {
            return _hitVfxAnchor.position;
        }

        Debug.LogWarning($"[EnemyHitVfxEmitter] Hit VFX Anchor가 비어 있어 transform 위치로 fallback합니다. target={name}", this);
        return transform.position;
    }

    /// <summary>
    /// 현재 런타임이 NGO 세션 상태인지 판정합니다.
    /// </summary>
    private bool IsNetworkSessionActive()
    {
        Unity.Netcode.NetworkManager networkManager = Unity.Netcode.NetworkManager.Singleton;
        return networkManager != null && networkManager.IsListening;
    }

    /// <summary>
    /// HitReceiver 리스너를 안전하게 등록합니다.
    /// </summary>
    private void RegisterListener()
    {
        if (_isListenerRegistered || _hitReceiver == null)
        {
            return;
        }

        _hitReceiver.AddListener(this);
        _isListenerRegistered = true;
    }

    /// <summary>
    /// HitReceiver 리스너를 안전하게 해제합니다.
    /// </summary>
    private void UnregisterListener()
    {
        if (!_isListenerRegistered)
        {
            return;
        }

        if (_hitReceiver != null)
        {
            _hitReceiver.RemoveListener(this);
        }

        _isListenerRegistered = false;
    }
}
