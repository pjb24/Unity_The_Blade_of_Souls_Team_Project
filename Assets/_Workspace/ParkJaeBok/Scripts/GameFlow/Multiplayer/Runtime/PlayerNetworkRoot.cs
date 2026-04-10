using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 단일 Player Prefab의 네트워크 루트로 동작하며 스폰/소유자 식별 디버그 정보를 제공합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class PlayerNetworkRoot : NetworkBehaviour
{
    [Header("Diagnostics")]
    [Tooltip("네트워크 스폰/디스폰 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLogging = true; // 네트워크 수명주기 로그 출력 여부를 제어하는 플래그입니다.

    [Tooltip("Inspector에서 현재 소유자 ClientId를 확인하기 위한 디버그 값입니다.")]
    [SerializeField] private ulong _ownerClientId; // 마지막 스폰 시점의 소유자 ClientId를 기록하는 디버그 값입니다.

    [Tooltip("Inspector에서 네트워크 스폰 여부를 확인하기 위한 디버그 값입니다.")]
    [SerializeField] private bool _isNetworkSpawned; // 이 플레이어가 현재 네트워크 스폰 상태인지 표시하는 디버그 값입니다.

    /// <summary>
    /// 네트워크 스폰 시 소유자 정보를 갱신하고 디버그 로그를 출력합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        _isNetworkSpawned = true;
        _ownerClientId = OwnerClientId;

        if (_verboseLogging)
        {
            Debug.Log($"[PlayerNetworkRoot] Spawned. object={name}, ownerClientId={_ownerClientId}, localClientId={NetworkManager.LocalClientId}", this);
        }
    }

    /// <summary>
    /// 네트워크 디스폰 시 디버그 상태를 초기화하고 로그를 출력합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        _isNetworkSpawned = false;

        if (_verboseLogging)
        {
            Debug.Log($"[PlayerNetworkRoot] Despawned. object={name}, ownerClientId={_ownerClientId}", this);
        }
    }
}
