using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어 사망을 감지해 마지막 체크포인트 복귀를 Stage Controller에 요청합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class CheckpointPlayerRespawnHandler : NetworkBehaviour, IHealthListener
{
    [Header("Dependencies")]
    [Tooltip("리스폰을 처리할 Stage 체크포인트 컨트롤러입니다. 비어 있으면 활성 씬에서 자동 탐색합니다.")]
    [SerializeField] private CheckpointStageController _stageController; // 사망 복귀를 위임할 체크포인트 Stage Controller입니다.

    [Tooltip("사망 상태를 구독할 HealthComponent입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private HealthComponent _healthComponent; // 플레이어 사망 이벤트를 제공하는 HealthComponent입니다.

    [Header("Multiplayer")]
    [Tooltip("멀티플레이 사망 후 부활 대기 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _multiplayerRespawnDelaySeconds = 10f; // 멀티플레이 사망 후 Host가 부활시키기 전 대기 시간입니다.

    [Tooltip("싱글플레이 사망 후 부활 대기 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _singlePlayerRespawnDelaySeconds = 0f; // 싱글플레이 사망 후 부활 대기 시간입니다.

    private static readonly Dictionary<ulong, bool> DeadStateByClientId = new Dictionary<ulong, bool>(); // Host 권한에서 관리하는 clientId별 사망 상태입니다.
    private bool _isHealthListenerRegistered; // HealthComponent 리스너 등록 여부입니다.
    private Coroutine _respawnRoutine; // 현재 예약된 리스폰 코루틴입니다.

    /// <summary>
    /// 필요한 참조를 자동 보정합니다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// HealthComponent 사망 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        RegisterHealthListener();
    }

    /// <summary>
    /// HealthComponent 사망 이벤트 구독과 예약된 리스폰을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterHealthListener();

        if (_respawnRoutine != null)
        {
            StopCoroutine(_respawnRoutine);
            _respawnRoutine = null;
        }
    }

    /// <summary>
    /// 네트워크 Spawn 시 Host 사망 상태 테이블을 초기화합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            DeadStateByClientId[OwnerClientId] = _healthComponent != null && _healthComponent.IsDead;
        }
    }

    /// <summary>
    /// 네트워크 Despawn 시 Host 사망 상태 테이블에서 제거합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            DeadStateByClientId.Remove(OwnerClientId);
        }
    }

    /// <summary>
    /// 체력 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
    }

    /// <summary>
    /// 데미지 이벤트를 수신합니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// 회복 이벤트를 수신합니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 사망 이벤트를 수신해 싱글/멀티플레이 리스폰 흐름을 시작합니다.
    /// </summary>
    public void OnDied()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            ScheduleAuthorityRespawn(true, _singlePlayerRespawnDelaySeconds);
            return;
        }

        if (IsServer)
        {
            RegisterServerDeathAndSchedule(OwnerClientId);
            return;
        }

        if (IsOwner)
        {
            ReportOwnerDeathRpc();
        }
    }

    /// <summary>
    /// 부활 이벤트를 수신해 사망 상태를 해제합니다.
    /// </summary>
    public void OnRevived()
    {
        if (IsSpawned && IsServer)
        {
            DeadStateByClientId[OwnerClientId] = false;
        }
    }

    /// <summary>
    /// 최대 체력 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }

    /// <summary>
    /// Client 소유자가 자신의 사망을 Host에 보고합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void ReportOwnerDeathRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning($"[CheckpointPlayerRespawnHandler] 사망 보고 송신자가 Owner가 아닙니다. sender={rpcParams.Receive.SenderClientId}, owner={OwnerClientId}", this);
            return;
        }

        RegisterServerDeathAndSchedule(OwnerClientId);
    }

    /// <summary>
    /// Host 권한에서 사망 상태를 기록하고 리스폰을 예약합니다.
    /// </summary>
    private void RegisterServerDeathAndSchedule(ulong deadClientId)
    {
        DeadStateByClientId[deadClientId] = true;
        ScheduleAuthorityRespawn(ShouldResetMonstersForCurrentDeath(), _multiplayerRespawnDelaySeconds);
    }

    /// <summary>
    /// 현재 사망 상태에서 몬스터 리셋 조건을 만족하는지 판정합니다.
    /// </summary>
    private bool ShouldResetMonstersForCurrentDeath()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            return true;
        }

        if (networkManager.ConnectedClients.Count <= 1)
        {
            return true;
        }

        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            if (!DeadStateByClientId.TryGetValue(clientId, out bool isDead) || !isDead)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 권한 인스턴스에서 일정 시간 뒤 체크포인트 리스폰을 수행합니다.
    /// </summary>
    private void ScheduleAuthorityRespawn(bool resetMonsters, float delaySeconds)
    {
        if (_respawnRoutine != null)
        {
            StopCoroutine(_respawnRoutine);
        }

        _respawnRoutine = StartCoroutine(RespawnRoutine(resetMonsters, Mathf.Max(0f, delaySeconds)));
    }

    /// <summary>
    /// 지연 후 Stage Controller를 통해 플레이어를 마지막 체크포인트로 복귀시킵니다.
    /// </summary>
    private IEnumerator RespawnRoutine(bool resetMonsters, float delaySeconds)
    {
        if (delaySeconds > 0f)
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        ResolveReferences();
        if (_stageController == null)
        {
            Debug.LogWarning($"[CheckpointPlayerRespawnHandler] CheckpointStageController를 찾지 못해 사망 복귀를 처리하지 못했습니다. player={name}", this);
            _respawnRoutine = null;
            yield break;
        }

        _stageController.RespawnPlayerAtCurrentCheckpoint(gameObject, resetMonsters);
        if (IsSpawned && IsServer)
        {
            DeadStateByClientId[OwnerClientId] = false;
        }

        _respawnRoutine = null;
    }

    /// <summary>
    /// HealthComponent 리스너를 등록합니다.
    /// </summary>
    private void RegisterHealthListener()
    {
        if (_isHealthListenerRegistered || _healthComponent == null)
        {
            return;
        }

        _healthComponent.AddListener(this);
        _isHealthListenerRegistered = true;
    }

    /// <summary>
    /// HealthComponent 리스너를 해제합니다.
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
    /// 필요한 참조를 자동 보정합니다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }

        if (_stageController == null)
        {
            _stageController = FindAnyObjectByType<CheckpointStageController>();
        }
    }
}
