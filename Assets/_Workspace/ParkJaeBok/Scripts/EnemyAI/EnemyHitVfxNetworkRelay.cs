using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 상위 루트 NetworkObject에서 EnemyHitVfxEmitter의 피격 VFX 재생을 서버 권한으로 복제하는 릴레이 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class EnemyHitVfxNetworkRelay : NetworkBehaviour
{
    [Header("Dependencies")]
    [Tooltip("네트워크 복제 결과를 로컬 재생으로 반영할 EnemyHitVfxEmitter 참조입니다. 비어 있으면 하위에서 자동 탐색합니다.")]
    [SerializeField] private EnemyHitVfxEmitter _emitter; // 네트워크 복제 결과를 로컬로 반영할 Emitter 참조입니다.

    [Header("Debug")]
    [Tooltip("네트워크 세션 비정상 상태 경고를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseFallbackLog = true; // 네트워크 비정상 상태 경고 출력 여부입니다.

    /// <summary>
    /// 초기화 시 Emitter 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_emitter == null)
        {
            _emitter = GetComponentInChildren<EnemyHitVfxEmitter>(true);
        }

        if (_emitter == null)
        {
            Debug.LogWarning($"[EnemyHitVfxNetworkRelay] EnemyHitVfxEmitter를 찾지 못했습니다. target={name}", this);
        }
    }

    /// <summary>
    /// 에디터 값 변경 시 필수 참조 누락을 점검합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_emitter == null)
        {
            Debug.LogWarning($"[EnemyHitVfxNetworkRelay] EnemyHitVfxEmitter 참조가 비어 있습니다. target={name}", this);
        }
    }

    /// <summary>
    /// 서버 권한에서 로컬 재생 + 클라이언트 전파를 수행하고 성공 여부를 반환합니다.
    /// </summary>
    public bool TryReplicateHitVfx(EnemyHitVfxEmitter caller, Vector3 spawnPosition)
    {
        if (!IsNetworkSessionActive())
        {
            return false;
        }

        if (!IsSpawned)
        {
            if (_verboseFallbackLog)
            {
                Debug.LogWarning($"[EnemyHitVfxNetworkRelay] 네트워크 세션 중이지만 Spawn되지 않아 복제를 건너뜁니다. target={name}", this);
            }

            return false;
        }

        if (!IsServer)
        {
            return false;
        }

        EnemyHitVfxEmitter targetEmitter = caller != null ? caller : _emitter;
        if (targetEmitter == null)
        {
            if (_verboseFallbackLog)
            {
                Debug.LogWarning($"[EnemyHitVfxNetworkRelay] 재생 대상 Emitter가 없어 복제를 건너뜁니다. target={name}", this);
            }

            return false;
        }

        targetEmitter.PlayLocalFromNetwork(spawnPosition);
        BroadcastHitVfxRpc(spawnPosition);
        return true;
    }

    /// <summary>
    /// 서버 확정 피격 VFX 재생 요청을 서버가 아닌 관찰자에게 전파합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void BroadcastHitVfxRpc(Vector3 spawnPosition)
    {
        if (_emitter == null)
        {
            _emitter = GetComponentInChildren<EnemyHitVfxEmitter>(true);
            if (_emitter == null)
            {
                Debug.LogWarning($"[EnemyHitVfxNetworkRelay] RPC 수신 시 EnemyHitVfxEmitter를 찾지 못했습니다. target={name}", this);
                return;
            }
        }

        _emitter.PlayLocalFromNetwork(spawnPosition);
    }

    /// <summary>
    /// 현재 런타임이 NGO 세션 상태인지 판정합니다.
    /// </summary>
    private bool IsNetworkSessionActive()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null && networkManager.IsListening;
    }
}
