using Unity.Netcode;
using Unity.Netcode.Components;
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

            if (!TryApplySpawnPoseByAuthority(resolvedPosition, resolvedRotation, "PlayerNetworkRoot.AlignOwnerClientToClientSlotRoutine"))
            {
                yield return new WaitForSecondsRealtime(safeRetryInterval);
                continue;
            }

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
    /// <summary>
    /// 호출한 피어의 권한에 맞는 방식으로 플레이어 위치를 적용하고, 원격 Owner 권한이면 Owner Client에 적용을 위임합니다.
    /// </summary>
    public bool TryApplySpawnPoseByAuthority(Vector3 position, Quaternion rotation, string reason)
    {
        if (!IsSpawned)
        {
            transform.SetPositionAndRotation(position, rotation);
            return true;
        }

        if (CanApplyTransformLocally())
        {
            ApplyLocalSpawnPose(position, rotation);
            return true;
        }

        if (ShouldDelegateSpawnPoseToOwner())
        {
            ApplyOwnerSpawnPoseRpc(position, rotation, reason);
            return true;
        }

        return false;
    }

    private void HandleSceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
    {
        if (!IsSpawned || !IsOwner)
        {
            return;
        }

        if (IsClient && !IsServer)
        {
            RequestOwnerSceneSpawnAlignmentRpc(loadedScene.name);
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

            TryApplySpawnPoseByAuthority(resolvedPosition, resolvedRotation, "PlayerNetworkRoot.HandleSceneLoaded.Host");
            return;
        }

        if (!spawnCoordinator.TryResolveSinglePlayerSpawnPose(out Vector3 singlePosition, out Quaternion singleRotation))
        {
            Debug.LogWarning($"[PlayerNetworkRoot] SceneLoaded 정렬 실패: Single 슬롯 해석 실패. scene={loadedScene.name}", this);
            return;
        }

        transform.SetPositionAndRotation(singlePosition, singleRotation);
    }

    /// <summary>
    /// Owner Client가 씬 로드 완료 후 서버에 체크포인트 기준 PlayerObject 정렬을 요청합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestOwnerSceneSpawnAlignmentRpc(string loadedSceneName, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning($"[PlayerNetworkRoot] SceneLoaded 정렬 요청 송신자가 Owner가 아닙니다. sender={rpcParams.Receive.SenderClientId}, owner={OwnerClientId}", this);
            return;
        }

        StartCoroutine(ApplyOwnerSceneSpawnAlignmentRoutine(rpcParams.Receive.SenderClientId, loadedSceneName));
    }

    /// <summary>
    /// 서버가 Owner 권한 NetworkTransform을 가진 원격 Client에게 위치 적용을 위임합니다.
    /// </summary>
    [Rpc(SendTo.Owner)]
    private void ApplyOwnerSpawnPoseRpc(Vector3 position, Quaternion rotation, string reason)
    {
        ApplyLocalSpawnPose(position, rotation);

        if (_verboseLogging)
        {
            Debug.Log($"[PlayerNetworkRoot] Owner 권한 위치 적용 완료. ownerClientId={OwnerClientId}, pos={position}, reason={reason}", this);
        }
    }

    /// <summary>
    /// 현재 피어가 이 PlayerObject의 Transform을 직접 변경할 수 있는지 판정합니다.
    /// </summary>
    private bool CanApplyTransformLocally()
    {
        if (!IsSpawned)
        {
            return true;
        }

        if (!TryGetComponent(out NetworkTransform networkTransform))
        {
            return IsServer || IsOwner;
        }

        if (networkTransform.IsServerAuthoritative())
        {
            return IsServer;
        }

        return IsOwner;
    }

    /// <summary>
    /// 서버가 원격 Owner 권한 NetworkTransform의 위치 적용을 Owner Client에 위임해야 하는지 판정합니다.
    /// </summary>
    private bool ShouldDelegateSpawnPoseToOwner()
    {
        if (!IsSpawned || !IsServer || IsOwner)
        {
            return false;
        }

        if (!TryGetComponent(out NetworkTransform networkTransform))
        {
            return false;
        }

        return !networkTransform.IsServerAuthoritative();
    }

    /// <summary>
    /// 로컬 권한이 있는 피어에서 NetworkTransform 또는 Transform에 위치를 실제 적용합니다.
    /// </summary>
    private void ApplyLocalSpawnPose(Vector3 position, Quaternion rotation)
    {
        if (TryGetComponent(out NetworkTransform networkTransform))
        {
            networkTransform.Teleport(position, rotation, transform.localScale);
            return;
        }

        transform.SetPositionAndRotation(position, rotation);
    }

    /// <summary>
    /// 서버에서 CheckpointStageController와 PlayerObject 준비 타이밍을 흡수하며 Owner Client를 체크포인트 위치로 정렬합니다.
    /// </summary>
    private System.Collections.IEnumerator ApplyOwnerSceneSpawnAlignmentRoutine(ulong clientId, string loadedSceneName)
    {
        int safeRetryCount = Mathf.Max(1, _clientAlignRetryCount); // Client 로드 완료 보고 직후 서버 정렬 재시도 횟수입니다.
        float safeRetryInterval = Mathf.Max(0.01f, _clientAlignRetryInterval); // 서버 정렬 재시도 사이의 대기 시간입니다.

        for (int retryIndex = 0; retryIndex < safeRetryCount; retryIndex++)
        {
            if (!IsSpawned || !IsServer)
            {
                yield break;
            }

            if (!PlayerSpawnCoordinator.TryFindForActiveScene(out PlayerSpawnCoordinator spawnCoordinator))
            {
                yield return new WaitForSecondsRealtime(safeRetryInterval);
                continue;
            }

            if (spawnCoordinator.TryApplySpawnToExistingPlayerObject(NetworkManager, clientId))
            {
                if (_verboseLogging)
                {
                    Debug.Log($"[PlayerNetworkRoot] Owner client SceneLoaded 서버 정렬 완료. ownerClientId={clientId}, scene={loadedSceneName}", this);
                }

                yield break;
            }

            yield return new WaitForSecondsRealtime(safeRetryInterval);
        }

        Debug.LogWarning($"[PlayerNetworkRoot] Owner client SceneLoaded 서버 정렬 재시도 실패. ownerClientId={clientId}, scene={loadedSceneName}", this);
    }
}
