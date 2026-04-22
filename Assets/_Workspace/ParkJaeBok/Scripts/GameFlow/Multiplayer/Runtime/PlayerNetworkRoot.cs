using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Header("Client Spawn Align")]
    [Tooltip("멀티 Client 환경에서 Owner 본인이 Client 슬롯 위치 정렬을 수행할지 여부입니다.")]
    [SerializeField] private bool _alignClientOwnerOnLocalSpawn = true; // 멀티 Client 환경에서 Owner 본인 위치 정렬 수행 여부입니다.

    [Tooltip("Owner 클라이언트에서 스폰 정렬을 재시도할 최대 횟수입니다.")]
    [SerializeField] private int _clientAlignRetryCount = 20; // Owner 클라이언트 로컬 정렬 재시도 횟수입니다.

    [Tooltip("Owner 클라이언트 스폰 정렬 재시도 간격(초)입니다.")]
    [SerializeField] private float _clientAlignRetryInterval = 0.1f; // Owner 클라이언트 로컬 정렬 재시도 간격입니다.

    [Tooltip("씬 로딩 완료 시 Owner 플레이어를 슬롯 위치로 재정렬할지 여부입니다.")]
    [SerializeField] private bool _realignOwnerOnSceneLoaded = true; // 씬 로딩 완료 이벤트마다 Owner 플레이어 슬롯 정렬 수행 여부입니다.

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

        if (_alignClientOwnerOnLocalSpawn && IsOwner && IsClient && !IsServer)
        {
            StartCoroutine(AlignOwnerClientToClientSlotRoutine());
        }

        if (IsOwner && _realignOwnerOnSceneLoaded)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
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

        StopAllCoroutines();

        if (_realignOwnerOnSceneLoaded)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    /// <summary>
    /// 컴포넌트 비활성화 시 씬 로드 콜백 구독을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_realignOwnerOnSceneLoaded)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    /// <summary>
    /// 멀티 Client Owner 인스턴스가 Client 슬롯 위치로 로컬 정렬을 재시도합니다.
    /// </summary>
    private System.Collections.IEnumerator AlignOwnerClientToClientSlotRoutine()
    {
        int safeRetryCount = Mathf.Max(1, _clientAlignRetryCount); // 정렬 재시도 횟수의 하한을 보정한 안전 값입니다.
        float safeRetryInterval = Mathf.Max(0.01f, _clientAlignRetryInterval); // 정렬 재시도 간격의 하한을 보정한 안전 값입니다.

        for (int retryIndex = 0; retryIndex < safeRetryCount; retryIndex++)
        {
            if (!IsSpawned || !IsOwner || !IsClient || IsServer)
            {
                yield break;
            }

            if (!PlayerSpawnCoordinator.TryFindForActiveScene(out PlayerSpawnCoordinator spawnCoordinator))
            {
                yield return new WaitForSecondsRealtime(safeRetryInterval);
                continue;
            }

            if (!spawnCoordinator.TryResolveMultiplayerSpawnPose(NetworkManager, OwnerClientId, out Vector3 resolvedPosition, out Quaternion resolvedRotation))
            {
                yield return new WaitForSecondsRealtime(safeRetryInterval);
                continue;
            }

            transform.SetPositionAndRotation(resolvedPosition, resolvedRotation);

            if (_verboseLogging)
            {
                Debug.Log($"[PlayerNetworkRoot] Owner client 슬롯 정렬 완료. ownerClientId={OwnerClientId}, pos={resolvedPosition}", this);
            }

            yield break;
        }

        Debug.LogWarning($"[PlayerNetworkRoot] Owner client 슬롯 정렬 재시도 실패. ownerClientId={OwnerClientId}", this);
    }

    /// <summary>
    /// 씬 로딩 완료 시 Owner 플레이어를 현재 역할 슬롯 위치로 재정렬합니다.
    /// </summary>
    private void HandleSceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
    {
        if (!IsSpawned || !IsOwner)
        {
            return;
        }

        if (IsClient && !IsServer)
        {
            StartCoroutine(AlignOwnerClientToClientSlotRoutine());
            return;
        }

        if (!PlayerSpawnCoordinator.TryFindForActiveScene(out PlayerSpawnCoordinator spawnCoordinator))
        {
            Debug.LogWarning($"[PlayerNetworkRoot] SceneLoaded 정렬 실패: PlayerSpawnCoordinator를 찾지 못했습니다. scene={loadedScene.name}", this);
            return;
        }

        if (IsServer)
        {
            if (!spawnCoordinator.TryResolveMultiplayerSpawnPose(NetworkManager, OwnerClientId, out Vector3 resolvedPosition, out Quaternion resolvedRotation))
            {
                Debug.LogWarning($"[PlayerNetworkRoot] SceneLoaded 정렬 실패: Host 슬롯 해석 실패. scene={loadedScene.name}", this);
                return;
            }

            transform.SetPositionAndRotation(resolvedPosition, resolvedRotation);
            return;
        }

        if (!spawnCoordinator.TryResolveSinglePlayerSpawnPose(out Vector3 singlePosition, out Quaternion singleRotation))
        {
            Debug.LogWarning($"[PlayerNetworkRoot] SceneLoaded 정렬 실패: Single 슬롯 해석 실패. scene={loadedScene.name}", this);
            return;
        }

        transform.SetPositionAndRotation(singlePosition, singleRotation);
    }
}
