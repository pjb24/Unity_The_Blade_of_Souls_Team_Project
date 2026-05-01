using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 스폰 위치 결정/조회/적용을 단일 진입점으로 제공하는 코디네이터입니다.
/// </summary>
public class PlayerSpawnCoordinator : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("슬롯별 스폰 포인트를 조회할 레지스트리입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private PlayerSpawnPointRegistry _registry; // 슬롯별 스폰 포인트 조회를 담당하는 레지스트리 참조입니다.

    [Header("Policy")]
    [Tooltip("현재 씬에 CheckpointStageController가 있으면 Runtime 저장 데이터의 마지막 Checkpoint를 기본 스폰 위치보다 우선 사용할지 여부입니다.")]
    [SerializeField] private bool _preferStageCheckpointSpawn = true; // Stage 진입 시 Checkpoint 저장 데이터를 스폰 위치 결정에 우선 사용할지 여부입니다.

    [Tooltip("요청 슬롯이 누락되면 스폰을 실패 처리하고 Warning 로그를 남깁니다.")]
    [SerializeField] private bool _failWhenRequestedSlotMissing = true; // 요청 슬롯 누락 시 스폰 실패 정책 활성화 여부입니다.

    [Tooltip("상세 디버그 로그 출력 여부입니다.")]
    [SerializeField] private bool _verboseLogging; // 코디네이터 동작 상세 로그 출력 여부입니다.

    /// <summary>
    /// 런타임 시작 시 레지스트리 참조를 검증합니다.
    /// </summary>
    private void Awake()
    {
        if (_registry == null)
        {
            _registry = GetComponent<PlayerSpawnPointRegistry>();
        }
    }

    /// <summary>
    /// 현재 활성 씬에서 사용할 코디네이터를 탐색합니다.
    /// </summary>
    public static bool TryFindForActiveScene(out PlayerSpawnCoordinator coordinator)
    {
        coordinator = null;

        PlayerSpawnCoordinator[] coordinators = FindObjectsByType<PlayerSpawnCoordinator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 씬에서 찾은 코디네이터 후보 목록입니다.
        Scene activeScene = SceneManager.GetActiveScene(); // 코디네이터가 속한 씬 일치 여부를 비교할 현재 활성 씬입니다.

        for (int index = 0; index < coordinators.Length; index++)
        {
            PlayerSpawnCoordinator candidate = coordinators[index]; // 현재 확인 중인 코디네이터 후보입니다.
            if (candidate == null || candidate.gameObject.scene != activeScene)
            {
                continue;
            }

            coordinator = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 싱글플레이 로컬 플레이어 생성에 사용할 스폰 포즈를 해석합니다.
    /// </summary>
    public bool TryResolveSinglePlayerSpawnPose(out Vector3 position, out Quaternion rotation)
    {
        E_PlayerSpawnSlot slot = PlayerSpawnResolver.ResolveSinglePlayerSlot(); // 싱글플레이 규칙으로 선택된 스폰 슬롯입니다.
        return TryResolveSpawnPose(slot, null, out position, out rotation, "SinglePlayer");
    }

    /// <summary>
    /// NGO 서버 관점에서 clientId 소유 플레이어의 스폰 포즈를 해석합니다.
    /// </summary>
    public bool TryResolveMultiplayerSpawnPose(NetworkManager networkManager, ulong clientId, out Vector3 position, out Quaternion rotation)
    {
        E_PlayerSpawnSlot slot = PlayerSpawnResolver.ResolveMultiplayerSlot(networkManager, clientId); // 멀티플레이 규칙으로 선택된 스폰 슬롯입니다.
        return TryResolveSpawnPose(slot, clientId, out position, out rotation, $"Multiplayer(clientId={clientId})");
    }

    /// <summary>
    /// 이미 생성된 PlayerObject를 서버 권한으로 슬롯 위치에 정렬합니다.
    /// </summary>
    public bool TryApplySpawnToExistingPlayerObject(NetworkManager networkManager, ulong clientId)
    {
        if (networkManager == null)
        {
            Debug.LogWarning("[PlayerSpawnCoordinator] NetworkManager가 없어 기존 PlayerObject 위치 적용을 건너뜁니다.", this);
            return false;
        }

        if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject == null)
        {
            Debug.LogWarning($"[PlayerSpawnCoordinator] 대상 PlayerObject를 찾지 못했습니다. clientId={clientId}", this);
            return false;
        }

        if (!TryResolveMultiplayerSpawnPose(networkManager, clientId, out Vector3 position, out Quaternion rotation))
        {
            return false;
        }

        Transform playerTransform = client.PlayerObject.transform; // 위치를 확정할 대상 플레이어 Transform입니다.
        playerTransform.SetPositionAndRotation(position, rotation);

        if (_verboseLogging)
        {
            Debug.Log($"[PlayerSpawnCoordinator] Existing PlayerObject 위치 적용 완료. clientId={clientId}, slot={PlayerSpawnResolver.ResolveMultiplayerSlot(networkManager, clientId)}, pos={position}", this);
        }

        return true;
    }

    /// <summary>
    /// 서버 권한으로 지정 clientId의 PlayerObject를 슬롯 위치에서 생성합니다.
    /// </summary>
    public bool TrySpawnPlayerObject(NetworkManager networkManager, GameObject playerPrefab, ulong clientId)
    {
        if (networkManager == null || playerPrefab == null)
        {
            Debug.LogWarning("[PlayerSpawnCoordinator] PlayerObject 생성 실패: NetworkManager 또는 PlayerPrefab이 비어 있습니다.", this);
            return false;
        }

        if (!TryResolveMultiplayerSpawnPose(networkManager, clientId, out Vector3 position, out Quaternion rotation))
        {
            return false;
        }

        GameObject spawnedPlayer = Instantiate(playerPrefab, position, rotation); // 슬롯 위치에서 생성한 플레이어 프리팹 인스턴스입니다.
        if (!spawnedPlayer.TryGetComponent(out NetworkObject networkObject))
        {
            Debug.LogWarning("[PlayerSpawnCoordinator] PlayerPrefab에 NetworkObject가 없어 PlayerObject 생성을 중단합니다.", this);
            Destroy(spawnedPlayer);
            return false;
        }

        networkObject.SpawnAsPlayerObject(clientId, true);

        if (_verboseLogging)
        {
            Debug.Log($"[PlayerSpawnCoordinator] PlayerObject 생성 완료. clientId={clientId}, pos={position}", this);
        }

        return true;
    }

    /// <summary>
    /// 싱글플레이 로컬 플레이어를 슬롯 위치에서 생성합니다.
    /// </summary>
    public bool TrySpawnSinglePlayer(GameObject playerPrefab)
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("[PlayerSpawnCoordinator] 싱글플레이 생성 실패: PlayerPrefab이 비어 있습니다.", this);
            return false;
        }

        if (!TryResolveSinglePlayerSpawnPose(out Vector3 position, out Quaternion rotation))
        {
            return false;
        }

        Instantiate(playerPrefab, position, rotation);

        if (_verboseLogging)
        {
            Debug.Log($"[PlayerSpawnCoordinator] 싱글플레이 Player 생성 완료. pos={position}", this);
        }

        return true;
    }

    /// <summary>
    /// 지정 슬롯의 스폰 포즈를 Checkpoint 우선, 기존 SpawnPoint 후순위로 조회합니다.
    /// </summary>
    private bool TryResolveSpawnPose(E_PlayerSpawnSlot slot, ulong? requestedClientId, out Vector3 position, out Quaternion rotation, string reasonContext)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (ShouldWaitForHostCheckpointSpawn(slot, reasonContext))
        {
            return false;
        }

        if (TryResolveCheckpointSpawnPose(slot, requestedClientId, out position, out rotation, reasonContext))
        {
            return true;
        }

        if (_preferStageCheckpointSpawn && TryFindCheckpointStageController(out _))
        {
            Debug.LogWarning($"[PlayerSpawnCoordinator] Stage Checkpoint 스폰을 사용할 수 없어 기본 SpawnPoint로 폴백하지 않습니다. slot={slot}, context={reasonContext}", this);
            return false;
        }

        if (_registry == null)
        {
            Debug.LogWarning($"[PlayerSpawnCoordinator] Registry 누락으로 스폰 포즈 해석 실패. slot={slot}, context={reasonContext}", this);
            return false;
        }

        if (_registry.TryGetSpawnPoint(slot, out PlayerSpawnPoint spawnPoint))
        {
            position = spawnPoint.Position;
            rotation = spawnPoint.Rotation;
            return true;
        }

        Debug.LogWarning($"[PlayerSpawnCoordinator] 요청 슬롯 스폰 포인트가 누락되었습니다. slot={slot}, context={reasonContext}, scene={gameObject.scene.name}", this);
        return !_failWhenRequestedSlotMissing;
    }

    /// <summary>
    /// Stage 씬에 배치된 CheckpointStageController를 통해 저장 데이터 기반 체크포인트 스폰 포즈를 우선 해석합니다.
    /// </summary>
    private bool TryResolveCheckpointSpawnPose(E_PlayerSpawnSlot slot, ulong? requestedClientId, out Vector3 position, out Quaternion rotation, string reasonContext)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (!_preferStageCheckpointSpawn)
        {
            return false;
        }

        if (!TryFindCheckpointStageController(out CheckpointStageController checkpointStageController))
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // 멀티플레이 권한과 ClientId를 확인하는 NGO 세션 참조입니다.
        ulong clientId = ResolveClientIdForCheckpointSpawn(networkManager, slot, requestedClientId); // Checkpoint의 Host/Client별 리스폰 포인트 선택에 사용할 ClientId입니다.
        if (!checkpointStageController.TryResolveStageEntryRespawnPose(clientId, out position, out rotation))
        {
            Debug.LogWarning($"[PlayerSpawnCoordinator] Stage Checkpoint 스폰 포즈 해석에 실패했습니다. 기본 SpawnPoint로 폴백하지 않습니다. slot={slot}, context={reasonContext}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 멀티플레이 Client가 Stage 체크포인트 위치를 직접 계산하지 않고 Host 동기화를 기다려야 하는지 판정합니다.
    /// </summary>
    private bool ShouldWaitForHostCheckpointSpawn(E_PlayerSpawnSlot slot, string reasonContext)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || !networkManager.IsClient || networkManager.IsServer)
        {
            return false;
        }

        if (!_preferStageCheckpointSpawn || !TryFindCheckpointStageController(out _))
        {
            return false;
        }

        Debug.LogWarning($"[PlayerSpawnCoordinator] Client는 Stage Checkpoint 스폰 위치를 직접 결정하지 않습니다. Host 동기화를 대기합니다. slot={slot}, context={reasonContext}", this);
        return true;
    }

    /// <summary>
    /// 현재 활성 씬에서 Stage 체크포인트 컨트롤러를 찾습니다.
    /// </summary>
    private bool TryFindCheckpointStageController(out CheckpointStageController checkpointStageController)
    {
        checkpointStageController = null;
        CheckpointStageController[] controllers = FindObjectsByType<CheckpointStageController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 씬의 체크포인트 컨트롤러 후보입니다.
        Scene activeScene = SceneManager.GetActiveScene();

        for (int i = 0; i < controllers.Length; i++)
        {
            CheckpointStageController candidate = controllers[i];
            if (candidate == null || candidate.gameObject.scene != activeScene)
            {
                continue;
            }

            checkpointStageController = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checkpoint 리스폰 포인트 선택에 사용할 ClientId를 스폰 슬롯과 네트워크 상태에서 해석합니다.
    /// </summary>
    private ulong ResolveClientIdForCheckpointSpawn(NetworkManager networkManager, E_PlayerSpawnSlot slot, ulong? requestedClientId)
    {
        if (networkManager == null || !networkManager.IsListening)
        {
            return NetworkManager.ServerClientId;
        }

        if (requestedClientId.HasValue)
        {
            return requestedClientId.Value;
        }

        if (slot == E_PlayerSpawnSlot.Client)
        {
            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId != NetworkManager.ServerClientId)
                {
                    return clientId;
                }
            }

            Debug.LogWarning("[PlayerSpawnCoordinator] Client 슬롯에 해당하는 연결 ClientId를 찾지 못해 ServerClientId를 폴백으로 사용합니다.", this);
        }

        return NetworkManager.ServerClientId;
    }
}
