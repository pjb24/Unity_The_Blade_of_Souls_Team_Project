using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage 단위 체크포인트 상태, 저장, 회복, 네트워크 동기화를 관리합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class CheckpointStageController : NetworkBehaviour
{
    [Header("Stage")]
    [Tooltip("이 Controller가 관리할 Stage ID입니다. 비어 있으면 StageSession.SelectedStageId를 폴백으로 사용합니다.")]
    [SerializeField] private string _stageId; // Stage 단위 체크포인트 저장/조회 키입니다.

    [Tooltip("Start 시점에 하위 체크포인트를 자동 수집할지 여부입니다.")]
    [SerializeField] private bool _autoCollectCheckpoints = true; // 하위 Checkpoint 자동 수집 여부입니다.

    [Tooltip("Controller 하위가 아닌 현재 씬의 같은 Stage Checkpoint까지 자동 수집할지 여부입니다.")]
    [SerializeField] private bool _collectSceneCheckpoints = true; // 씬에 수동 배치된 체크포인트까지 Stage ID 기준으로 수집할지 여부입니다.

    [Header("Interaction Input")]
    [Tooltip("Stage 안의 체크포인트들이 공통으로 사용할 상호작용 Input Action입니다. 개별 Checkpoint에 값이 있으면 개별 값이 우선됩니다.")]
    [SerializeField] private InputActionReference _sharedInteractAction; // Stage 안의 체크포인트가 공통으로 사용할 상호작용 입력 액션입니다.

    [Tooltip("InputActionReference가 비어 있을 때 InputManager.PlayerInput에서 찾을 액션 이름입니다.")]
    [SerializeField] private string _sharedInteractActionName = "Interact"; // InputManager PlayerInput에서 폴백으로 찾을 액션 이름입니다.

    [Header("Dependencies")]
    [Tooltip("회복/조작 Block/무적/몬스터 리셋을 담당하는 Processor입니다.")]
    [SerializeField] private CheckpointRecoveryProcessor _recoveryProcessor; // 체크포인트 회복 처리 담당 컴포넌트입니다.

    private readonly List<Checkpoint> _checkpoints = new List<Checkpoint>(); // Stage 내 체크포인트 목록입니다.
    private readonly Dictionary<string, Checkpoint> _checkpointById = new Dictionary<string, Checkpoint>(); // Checkpoint ID 기반 조회 인덱스입니다.
    private readonly HashSet<string> _activatedCheckpointIds = new HashSet<string>(); // 현재 세션에서 활성화된 Checkpoint ID 집합입니다.
    private string _currentCheckpointId = string.Empty; // 현재 리스폰 지점으로 사용할 Checkpoint ID입니다.
    private Action<string> _checkpointChangedListeners; // 체크포인트 상태 변경 알림 리스너 체인입니다.
    private StageProgressRuntime _boundStageProgressRuntime; // 저장 진행도 변경 알림을 구독 중인 런타임 인스턴스입니다.
    private SaveDataStore _boundSaveDataStore; // 저장 데이터 로드/변경 알림을 구독 중인 저장소 인스턴스입니다.

    public string StageId => _stageId;
    public string CurrentCheckpointId => _currentCheckpointId;

    /// <summary>
    /// Stage 공용 상호작용 입력 액션을 조회합니다.
    /// </summary>
    public bool TryGetSharedInteractAction(out InputAction action)
    {
        action = null;

        if (_sharedInteractAction != null && _sharedInteractAction.action != null)
        {
            action = _sharedInteractAction.action;
            return true;
        }

        if (InputManager.PlayerInput != null && !string.IsNullOrWhiteSpace(_sharedInteractActionName))
        {
            action = InputManager.PlayerInput.actions.FindAction(_sharedInteractActionName, false);
            if (action != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 체크포인트 상태 변경 리스너를 등록합니다.
    /// </summary>
    public void AddListener(Action<string> listener)
    {
        _checkpointChangedListeners += listener;
    }

    /// <summary>
    /// 체크포인트 상태 변경 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(Action<string> listener)
    {
        _checkpointChangedListeners -= listener;
    }

    /// <summary>
    /// 참조를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        ResolveStageId();
    }

    /// <summary>
    /// 저장 런타임 변경 알림을 구독하고 현재 저장 상태를 비주얼에 즉시 반영합니다.
    /// </summary>
    private void OnEnable()
    {
        BindRuntimeListeners(true);
        RefreshCheckpointProgressFromRuntime();
    }

    /// <summary>
    /// 체크포인트를 수집하고 저장 데이터를 반영합니다.
    /// </summary>
    private void Start()
    {
        ResolveStageId();

        if (_autoCollectCheckpoints)
        {
            CollectCheckpoints();
        }

        BindRuntimeListeners(true);
        RefreshCheckpointProgressFromRuntime();
    }

    /// <summary>
    /// 저장 런타임 변경 알림 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        BindRuntimeListeners(false);
    }

    /// <summary>
    /// 네트워크 Spawn 시 서버 상태를 클라이언트에 동기화합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            RefreshCheckpointProgressFromRuntime();
            BroadcastCheckpointState();
        }
    }

    /// <summary>
    /// Stage 내 체크포인트를 등록합니다.
    /// </summary>
    public void RegisterCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null || _checkpoints.Contains(checkpoint))
        {
            return;
        }

        ResolveStageId();
        if (!CanManageCheckpoint(checkpoint))
        {
            return;
        }

        checkpoint.ApplyStageIdFallback(_stageId);
        _checkpoints.Add(checkpoint);
        RebuildCheckpointIndex();
        RefreshCheckpointProgressFromRuntime();
    }

    /// <summary>
    /// Stage 내 체크포인트 등록을 해제합니다.
    /// </summary>
    public void UnregisterCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null)
        {
            return;
        }

        _checkpoints.Remove(checkpoint);
        RebuildCheckpointIndex();
    }

    /// <summary>
    /// 체크포인트 상호작용 요청을 권한 기준으로 처리합니다.
    /// </summary>
    public void RequestCheckpointInteraction(Checkpoint checkpoint, GameObject playerObject)
    {
        if (checkpoint == null)
        {
            Debug.LogWarning("[CheckpointStageController] null 체크포인트 상호작용 요청을 무시합니다.", this);
            return;
        }

        if (!CanHostInteract(playerObject))
        {
            Debug.LogWarning($"[CheckpointStageController] Client 또는 Host가 아닌 객체의 체크포인트 상호작용을 무시합니다. checkpoint={checkpoint.CheckpointId}", this);
            return;
        }

        ActivateCheckpoint(checkpoint, playerObject, true);
    }

    /// <summary>
    /// 지정 플레이어를 현재 체크포인트 위치로 복귀시킵니다.
    /// </summary>
    public bool RespawnPlayerAtCurrentCheckpoint(GameObject playerObject, bool resetMonsters)
    {
        if (playerObject == null)
        {
            Debug.LogWarning("[CheckpointStageController] Player가 null이라 리스폰을 중단합니다.", this);
            return false;
        }

        if (!TryResolveCurrentCheckpoint(out Checkpoint checkpoint))
        {
            Debug.LogWarning($"[CheckpointStageController] 현재 체크포인트가 없어 리스폰할 수 없습니다. stage={_stageId}", this);
            return false;
        }

        MovePlayerToCheckpoint(playerObject, checkpoint);
        _recoveryProcessor?.ProcessDeathRespawn(playerObject, resetMonsters);
        return true;
    }

    /// <summary>
    /// 특정 플레이어 역할에 맞는 현재 리스폰 Transform을 조회합니다.
    /// </summary>
    public bool TryGetCurrentRespawnPoint(ulong clientId, out Transform respawnPoint)
    {
        respawnPoint = null;
        if (!TryResolveCurrentCheckpoint(out Checkpoint checkpoint))
        {
            return false;
        }

        respawnPoint = checkpoint.ResolveRespawnPoint(NetworkManager.Singleton, clientId);
        return respawnPoint != null;
    }

    /// <summary>
    /// 체크포인트 상태 변경, 저장, 회복, 네트워크 동기화를 수행합니다.
    /// </summary>
    private void ActivateCheckpoint(Checkpoint checkpoint, GameObject playerObject, bool saveProgress)
    {
        if (!CanMutateCheckpointState())
        {
            Debug.LogWarning($"[CheckpointStageController] Host가 아닌 객체가 체크포인트 상태 변경을 시도했습니다. checkpoint={checkpoint.CheckpointId}", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(checkpoint.CheckpointId))
        {
            Debug.LogWarning("[CheckpointStageController] Checkpoint ID가 비어 있어 활성화를 중단합니다.", checkpoint);
            return;
        }

        _activatedCheckpointIds.Add(checkpoint.CheckpointId);
        _currentCheckpointId = checkpoint.CheckpointId;
        ApplyVisualStates();

        _recoveryProcessor?.ProcessCheckpointInteraction(playerObject, true);

        if (saveProgress)
        {
            SaveCheckpointProgress();
        }

        BroadcastCheckpointState();
        _checkpointChangedListeners?.Invoke(_currentCheckpointId);
    }

    /// <summary>
    /// 저장/진행도 런타임 변경 알림을 등록하거나 해제합니다.
    /// </summary>
    /// <param name="shouldBind">true면 등록하고 false면 해제합니다.</param>
    private void BindRuntimeListeners(bool shouldBind)
    {
        StageProgressRuntime stageProgressRuntime = null; // 체크포인트 저장 상태 변경을 감지할 런타임입니다.
        if (shouldBind)
        {
            stageProgressRuntime = StageProgressRuntime.Instance;
        }
        else
        {
            StageProgressRuntime.TryGetExistingInstance(out stageProgressRuntime);
        }
        if (_boundStageProgressRuntime != stageProgressRuntime)
        {
            if (_boundStageProgressRuntime != null)
            {
                _boundStageProgressRuntime.RemoveListener(HandleStageProgressChanged);
            }

            _boundStageProgressRuntime = stageProgressRuntime;
        }

        if (_boundStageProgressRuntime != null)
        {
            _boundStageProgressRuntime.RemoveListener(HandleStageProgressChanged);
            if (shouldBind)
            {
                _boundStageProgressRuntime.AddListener(HandleStageProgressChanged);
            }
        }

        SaveDataStore saveDataStore = SaveDataStore.Instance; // 저장 파일 로드/변경 후 런타임 반영 시점을 감지할 저장소입니다.
        if (_boundSaveDataStore != saveDataStore)
        {
            if (_boundSaveDataStore != null)
            {
                _boundSaveDataStore.RemoveListener(HandleSaveDataChanged);
            }

            _boundSaveDataStore = saveDataStore;
        }

        if (_boundSaveDataStore != null)
        {
            _boundSaveDataStore.RemoveListener(HandleSaveDataChanged);
            if (shouldBind)
            {
                _boundSaveDataStore.AddListener(HandleSaveDataChanged);
            }
        }
    }

    /// <summary>
    /// 저장 런타임에서 현재 Stage의 체크포인트 진행도를 다시 읽고 비주얼을 갱신합니다.
    /// </summary>
    private void RefreshCheckpointProgressFromRuntime()
    {
        ResolveStageId();
        if (ShouldFollowHostCheckpointStateOnly())
        {
            ApplyVisualStates();
            return;
        }

        LoadCheckpointProgress();
        ApplyVisualStates();
    }

    /// <summary>
    /// 멀티플레이 Client가 Host 기준 세션 상태만 따라야 하는지 확인합니다.
    /// </summary>
    private bool ShouldFollowHostCheckpointStateOnly()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션 상태를 확인할 NetworkManager입니다.
        return networkManager != null
            && networkManager.IsListening
            && networkManager.IsClient
            && !networkManager.IsServer;
    }

    /// <summary>
    /// StageProgressRuntime 변경 알림을 받아 현재 Stage 체크포인트 상태를 다시 적용합니다.
    /// </summary>
    /// <param name="stageId">변경된 Stage ID입니다.</param>
    private void HandleStageProgressChanged(string stageId)
    {
        if (!string.Equals(_stageId, stageId, StringComparison.Ordinal))
        {
            return;
        }

        RefreshCheckpointProgressFromRuntime();
    }

    /// <summary>
    /// 저장 데이터 로드/변경 알림 후 체크포인트 저장 상태를 다시 적용합니다.
    /// </summary>
    /// <param name="saveData">변경된 저장 데이터 스냅샷입니다.</param>
    private void HandleSaveDataChanged(SaveGameData saveData)
    {
        RefreshCheckpointProgressFromRuntime();
    }

    /// <summary>
    /// 저장된 체크포인트 진행 상태를 런타임에 로드합니다.
    /// </summary>
    private void LoadCheckpointProgress()
    {
        if (string.IsNullOrWhiteSpace(_stageId))
        {
            Debug.LogWarning("[CheckpointStageController] Stage ID가 비어 있어 체크포인트 저장 데이터를 로드할 수 없습니다.", this);
            return;
        }

        _activatedCheckpointIds.Clear();
        _currentCheckpointId = string.Empty;

        StageProgressRuntime runtime = StageProgressRuntime.Instance;
        if (!runtime.TryGetCheckpointProgress(_stageId, out CheckpointProgressRecord record))
        {
            return;
        }

        IReadOnlyList<string> activatedIds = record.ActivatedCheckpointIds;
        for (int i = 0; i < activatedIds.Count; i++)
        {
            string checkpointId = activatedIds[i]; // 저장 데이터에서 복원 중인 활성 Checkpoint ID입니다.
            if (string.IsNullOrWhiteSpace(checkpointId))
            {
                continue;
            }

            _activatedCheckpointIds.Add(checkpointId);
            if (!_checkpointById.ContainsKey(checkpointId))
            {
                Debug.LogWarning($"[CheckpointStageController] 저장 데이터의 Checkpoint ID가 현재 씬에 없습니다. stage={_stageId}, checkpoint={checkpointId}", this);
            }
        }

        _currentCheckpointId = record.LastInteractedCheckpointId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(_currentCheckpointId) && !_checkpointById.ContainsKey(_currentCheckpointId))
        {
            Debug.LogWarning($"[CheckpointStageController] 저장 데이터의 Current Checkpoint가 현재 씬에 없습니다. stage={_stageId}, checkpoint={_currentCheckpointId}", this);
        }
    }

    /// <summary>
    /// 현재 체크포인트 상태를 StageProgressRuntime과 SaveDataStore에 저장합니다.
    /// </summary>
    private void SaveCheckpointProgress()
    {
        StageProgressRuntime.Instance.SetCheckpointProgress(_stageId, _currentCheckpointId, _activatedCheckpointIds);
        SaveDataStore saveDataStore = SaveDataStore.Instance;
        if (saveDataStore != null)
        {
            saveDataStore.Save("CheckpointInteraction");
        }
        else
        {
            Debug.LogWarning("[CheckpointStageController] SaveDataStore를 찾지 못해 체크포인트 진행도를 파일에 즉시 저장하지 못했습니다.", this);
        }
    }

    /// <summary>
    /// 현재 상태 기준으로 체크포인트 비주얼을 갱신합니다.
    /// </summary>
    private void ApplyVisualStates()
    {
        for (int i = 0; i < _checkpoints.Count; i++)
        {
            Checkpoint checkpoint = _checkpoints[i]; // 상태를 갱신할 체크포인트입니다.
            if (checkpoint == null)
            {
                continue;
            }

            E_CheckpointState state = E_CheckpointState.Inactive;
            if (!string.IsNullOrWhiteSpace(_currentCheckpointId) && checkpoint.CheckpointId == _currentCheckpointId)
            {
                state = E_CheckpointState.Current;
            }
            else if (_activatedCheckpointIds.Contains(checkpoint.CheckpointId))
            {
                state = E_CheckpointState.Activated;
            }

            checkpoint.ApplyState(state);
        }
    }

    /// <summary>
    /// Host 상태를 클라이언트에 동기화합니다.
    /// </summary>
    private void BroadcastCheckpointState()
    {
        if (!IsSpawned || !IsServer)
        {
            return;
        }

        SyncCheckpointStateRpc(_stageId, _currentCheckpointId, string.Join("|", _activatedCheckpointIds));
    }

    /// <summary>
    /// Host가 확정한 체크포인트 상태를 클라이언트 세션에 반영합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void SyncCheckpointStateRpc(string stageId, string currentCheckpointId, string activatedCheckpointIds)
    {
        if (!string.Equals(_stageId, stageId, StringComparison.Ordinal))
        {
            Debug.LogWarning($"[CheckpointStageController] 수신한 Stage ID가 현재 Controller와 달라 상태 동기화를 무시합니다. local={_stageId}, received={stageId}", this);
            return;
        }

        _activatedCheckpointIds.Clear();
        string[] tokens = (activatedCheckpointIds ?? string.Empty).Split('|');
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(tokens[i]))
            {
                _activatedCheckpointIds.Add(tokens[i]);
            }
        }

        _currentCheckpointId = currentCheckpointId ?? string.Empty;
        ApplyVisualStates();
        UpdateClientPersonalSaveIfBehind();
        _checkpointChangedListeners?.Invoke(_currentCheckpointId);
    }

    /// <summary>
    /// Client 개인 저장 데이터가 Host 진행도보다 낮을 때만 갱신합니다.
    /// </summary>
    private void UpdateClientPersonalSaveIfBehind()
    {
        StageProgressRuntime runtime = StageProgressRuntime.Instance;
        int localProgress = 0; // 로컬 개인 저장 데이터 진행도 점수입니다.
        if (runtime.TryGetCheckpointProgress(_stageId, out CheckpointProgressRecord localRecord) && localRecord != null)
        {
            localProgress = CalculateProgressScore(localRecord.LastInteractedCheckpointId, localRecord.ActivatedCheckpointIds);
        }

        int hostProgress = CalculateProgressScore(_currentCheckpointId, _activatedCheckpointIds); // Host 세션 진행도 점수입니다.
        if (localProgress > hostProgress)
        {
            return;
        }

        if (localProgress == hostProgress)
        {
            return;
        }

        runtime.SetCheckpointProgress(_stageId, _currentCheckpointId, _activatedCheckpointIds);
        SaveDataStore saveDataStore = SaveDataStore.Instance;
        if (saveDataStore != null)
        {
            saveDataStore.SaveClientPersonalPlayData("CheckpointHostProgressSync");
        }
        else
        {
            Debug.LogWarning("[CheckpointStageController] Client 개인 저장 갱신 중 SaveDataStore를 찾지 못했습니다.", this);
        }
    }

    /// <summary>
    /// 활성화 개수와 씬 배치 순서를 기준으로 진행도 비교 점수를 계산합니다.
    /// </summary>
    private int CalculateProgressScore(string currentCheckpointId, IEnumerable<string> activatedCheckpointIds)
    {
        int activatedCount = 0; // 활성화된 체크포인트 개수입니다.
        if (activatedCheckpointIds != null)
        {
            foreach (string checkpointId in activatedCheckpointIds)
            {
                if (!string.IsNullOrWhiteSpace(checkpointId))
                {
                    activatedCount++;
                }
            }
        }

        int currentIndex = ResolveCheckpointOrderIndex(currentCheckpointId); // 현재 체크포인트의 씬 배치 순서입니다.
        return activatedCount * 1000 + Mathf.Max(0, currentIndex);
    }

    /// <summary>
    /// 체크포인트 ID의 현재 씬 배치 순서를 반환합니다.
    /// </summary>
    private int ResolveCheckpointOrderIndex(string checkpointId)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            return 0;
        }

        for (int i = 0; i < _checkpoints.Count; i++)
        {
            Checkpoint checkpoint = _checkpoints[i]; // 순서를 비교할 체크포인트입니다.
            if (checkpoint != null && checkpoint.CheckpointId == checkpointId)
            {
                return i + 1;
            }
        }

        Debug.LogWarning($"[CheckpointStageController] 진행도 비교 중 현재 씬에 없는 Checkpoint ID를 0순위로 처리합니다. checkpoint={checkpointId}", this);
        return 0;
    }

    /// <summary>
    /// 플레이어를 체크포인트 역할별 RespawnPoint로 이동시킵니다.
    /// </summary>
    private void MovePlayerToCheckpoint(GameObject playerObject, Checkpoint checkpoint)
    {
        NetworkObject playerNetworkObject = playerObject.GetComponent<NetworkObject>();
        ulong clientId = playerNetworkObject != null ? playerNetworkObject.OwnerClientId : NetworkManager.ServerClientId;
        Transform respawnPoint = checkpoint.ResolveRespawnPoint(NetworkManager.Singleton, clientId);
        playerObject.transform.SetPositionAndRotation(respawnPoint.position, respawnPoint.rotation);
    }

    /// <summary>
    /// 현재 체크포인트 ID로 Checkpoint를 조회합니다.
    /// </summary>
    private bool TryResolveCurrentCheckpoint(out Checkpoint checkpoint)
    {
        checkpoint = null;
        if (string.IsNullOrWhiteSpace(_currentCheckpointId))
        {
            return false;
        }

        return _checkpointById.TryGetValue(_currentCheckpointId, out checkpoint) && checkpoint != null;
    }

    /// <summary>
    /// 상호작용 주체가 싱글플레이 또는 Host인지 판정합니다.
    /// </summary>
    private bool CanHostInteract(GameObject playerObject)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            return true;
        }

        if (!networkManager.IsServer)
        {
            return false;
        }

        NetworkObject playerNetworkObject = playerObject != null ? playerObject.GetComponent<NetworkObject>() : null;
        return playerNetworkObject == null || playerNetworkObject.OwnerClientId == NetworkManager.ServerClientId;
    }

    /// <summary>
    /// 현재 인스턴스가 체크포인트 상태를 변경할 권한이 있는지 판정합니다.
    /// </summary>
    private bool CanMutateCheckpointState()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager == null || !networkManager.IsListening || networkManager.IsServer;
    }

    /// <summary>
    /// 설정된 수집 범위에서 현재 Stage가 관리할 Checkpoint를 수집합니다.
    /// </summary>
    private void CollectCheckpoints()
    {
        _checkpoints.Clear();

        if (_collectSceneCheckpoints)
        {
            Checkpoint[] sceneCheckpoints = FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 씬에 배치된 체크포인트 후보입니다.
            for (int i = 0; i < sceneCheckpoints.Length; i++)
            {
                AddCollectedCheckpoint(sceneCheckpoints[i]);
            }
        }
        else
        {
            List<Checkpoint> childCheckpoints = new List<Checkpoint>(); // Controller 하위에서 찾은 체크포인트 후보입니다.
            GetComponentsInChildren(true, childCheckpoints);
            for (int i = 0; i < childCheckpoints.Count; i++)
            {
                AddCollectedCheckpoint(childCheckpoints[i]);
            }
        }

        if (_checkpoints.Count == 0)
        {
            Debug.LogWarning($"[CheckpointStageController] 수집된 Checkpoint가 없습니다. stage={_stageId}, collectScene={_collectSceneCheckpoints}", this);
        }

        RebuildCheckpointIndex();
    }

    /// <summary>
    /// 수집 후보 Checkpoint가 현재 Stage 대상이면 목록에 추가합니다.
    /// </summary>
    /// <param name="checkpoint">수집 후보 Checkpoint입니다.</param>
    private void AddCollectedCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null || _checkpoints.Contains(checkpoint))
        {
            return;
        }

        if (!CanManageCheckpoint(checkpoint))
        {
            return;
        }

        checkpoint.ApplyStageIdFallback(_stageId);
        _checkpoints.Add(checkpoint);
    }

    /// <summary>
    /// Checkpoint가 현재 StageController의 관리 대상인지 확인합니다.
    /// </summary>
    /// <param name="checkpoint">관리 대상 여부를 확인할 Checkpoint입니다.</param>
    private bool CanManageCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(checkpoint.StageId))
        {
            return true;
        }

        if (string.Equals(checkpoint.StageId, _stageId, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 체크포인트 ID 기반 인덱스를 재구성하고 중복을 검증합니다.
    /// </summary>
    private void RebuildCheckpointIndex()
    {
        _checkpointById.Clear();
        for (int i = 0; i < _checkpoints.Count; i++)
        {
            Checkpoint checkpoint = _checkpoints[i]; // 인덱스에 반영할 체크포인트입니다.
            if (checkpoint == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(checkpoint.CheckpointId))
            {
                Debug.LogWarning($"[CheckpointStageController] Checkpoint ID가 비어 있습니다. object={checkpoint.name}", checkpoint);
                continue;
            }

            if (_checkpointById.ContainsKey(checkpoint.CheckpointId))
            {
                Debug.LogWarning($"[CheckpointStageController] 동일 Stage 안에 중복 Checkpoint ID가 있습니다. stage={_stageId}, checkpoint={checkpoint.CheckpointId}", checkpoint);
                continue;
            }

            _checkpointById.Add(checkpoint.CheckpointId, checkpoint);
        }
    }

    /// <summary>
    /// 필요한 참조를 자동으로 보정합니다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_recoveryProcessor == null)
        {
            _recoveryProcessor = GetComponent<CheckpointRecoveryProcessor>();
        }

        if (_recoveryProcessor == null)
        {
            _recoveryProcessor = FindAnyObjectByType<CheckpointRecoveryProcessor>();
        }
    }

    /// <summary>
    /// Stage ID가 비어 있을 때 StageSession을 폴백으로 사용합니다.
    /// </summary>
    private void ResolveStageId()
    {
        if (!string.IsNullOrWhiteSpace(_stageId))
        {
            return;
        }

        StageSession session = StageSession.Instance;
        _stageId = session != null ? session.SelectedStageId : string.Empty;
        Debug.LogWarning($"[CheckpointStageController] Stage ID가 비어 있어 StageSession 값을 폴백으로 사용합니다. stage={_stageId}", this);
    }
}
