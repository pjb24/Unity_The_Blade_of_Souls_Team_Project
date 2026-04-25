using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Player 루트의 NetworkObject/NetworkBehaviour와 Buff 하위 모듈 사이를 중계하는 네트워크 릴레이입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class PlayerBuffNetworkRelay : NetworkBehaviour
{
    [Header("Dependencies")]
    [Tooltip("권한/소유자 판정에 사용할 Player 루트 NetworkObject 참조입니다. 비어 있으면 동일 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private NetworkObject _networkObject; // Buff 권한/소유자 판정을 수행할 Player 루트 NetworkObject 참조입니다.

    [Tooltip("네트워크 토글 요청을 위임할 PlayerBuffController 참조입니다. 비어 있으면 자식에서 자동 탐색합니다.")]
    [SerializeField] private PlayerBuffController _targetController; // 서버 권한 토글 요청을 전달할 PlayerBuffController 참조입니다.

    private readonly NetworkVariable<bool> _replicatedBuffActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 Buff 활성 상태를 동기화하는 복제 변수입니다.

    private readonly NetworkVariable<float> _replicatedGauge = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버 확정 Buff 게이지 값을 동기화하는 복제 변수입니다.

    /// <summary>
    /// 클라이언트에서 복제 Buff 활성 상태가 변경되면 전달되는 이벤트입니다.
    /// </summary>
    public event Action<bool, bool> ReplicatedBuffActiveChanged;

    /// <summary>
    /// 클라이언트에서 복제 게이지 값이 변경되면 전달되는 이벤트입니다.
    /// </summary>
    public event Action<float, float> ReplicatedGaugeChanged;

    /// <summary>
    /// 현재 복제된 Buff 활성 상태를 반환합니다.
    /// </summary>
    public bool ReplicatedBuffActive => _replicatedBuffActive.Value;

    /// <summary>
    /// 현재 복제된 Buff 게이지 값을 반환합니다.
    /// </summary>
    public float ReplicatedGauge => _replicatedGauge.Value;

    /// <summary>
    /// 초기 의존성 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        ResolveDependencies();
    }

    /// <summary>
    /// 네트워크 스폰 시 복제 변수 변경 이벤트를 구독합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        _replicatedBuffActive.OnValueChanged += HandleReplicatedBuffActiveChanged;
        _replicatedGauge.OnValueChanged += HandleReplicatedGaugeChanged;
    }

    /// <summary>
    /// 네트워크 디스폰 시 복제 변수 변경 이벤트를 해제합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        _replicatedBuffActive.OnValueChanged -= HandleReplicatedBuffActiveChanged;
        _replicatedGauge.OnValueChanged -= HandleReplicatedGaugeChanged;
    }

    /// <summary>
    /// 현재 런타임이 네트워크 세션 상태인지 판정합니다.
    /// </summary>
    public bool HasNetworkSession()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 현재 런타임의 네트워크 세션 가동 여부를 확인할 NetworkManager 참조입니다.
        return networkManager != null && networkManager.IsListening;
    }

    /// <summary>
    /// 로컬 인스턴스가 입력 오너 경로를 처리할 수 있는지 판정합니다.
    /// </summary>
    public bool CanDriveOwnerInput()
    {
        if (!HasNetworkSession())
        {
            return true;
        }

        return IsSpawned && IsOwner;
    }

    /// <summary>
    /// 로컬 인스턴스가 서버 권한 로직을 직접 처리할 수 있는지 판정합니다.
    /// </summary>
    public bool HasServerAuthority()
    {
        if (!HasNetworkSession())
        {
            return true;
        }

        return IsServer;
    }

    /// <summary>
    /// 현재 로컬 입력이 서버 RPC 경유로 토글 요청을 보내야 하는지 판정합니다.
    /// </summary>
    public bool ShouldUseServerRpcRoute()
    {
        if (!HasNetworkSession())
        {
            return false;
        }

        return !IsServer;
    }

    /// <summary>
    /// 서버 권한에서 Buff 상태와 게이지를 복제 변수에 반영합니다.
    /// </summary>
    public void PublishAuthorityState(bool isBuffActive, float gauge)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        _replicatedBuffActive.Value = isBuffActive;
        _replicatedGauge.Value = gauge;
    }

    /// <summary>
    /// 서버 권한에서 게이지 값만 복제 변수에 반영합니다.
    /// </summary>
    public void PublishAuthorityGauge(float gauge)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        if (Mathf.Approximately(_replicatedGauge.Value, gauge))
        {
            return;
        }

        _replicatedGauge.Value = gauge;
    }

    /// <summary>
    /// 오너 클라이언트의 Buff 토글 요청을 서버 권한으로 전달합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void RequestToggleBuffServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning($"[PlayerBuffNetworkRelay] Unauthorized buff toggle sender. object={name}, sender={rpcParams.Receive.SenderClientId}, owner={OwnerClientId}", this);
            return;
        }

        ResolveDependencies();
        if (_targetController == null)
        {
            Debug.LogWarning($"[PlayerBuffNetworkRelay] Target PlayerBuffController is missing. object={name}", this);
            return;
        }

        _targetController.HandleRelayServerToggleRequest();
    }

    /// <summary>
    /// 직렬화 참조가 비어 있으면 런타임에서 자동 보정합니다.
    /// </summary>
    private void ResolveDependencies()
    {
        if (_networkObject == null)
        {
            _networkObject = GetComponent<NetworkObject>();
        }

        if (_targetController == null)
        {
            _targetController = GetComponentInChildren<PlayerBuffController>(true);
        }
    }

    /// <summary>
    /// 복제된 Buff 활성 상태 변경 이벤트를 외부 구독자에게 전달합니다.
    /// </summary>
    private void HandleReplicatedBuffActiveChanged(bool previousValue, bool currentValue)
    {
        ReplicatedBuffActiveChanged?.Invoke(previousValue, currentValue);
    }

    /// <summary>
    /// 복제된 게이지 값 변경 이벤트를 외부 구독자에게 전달합니다.
    /// </summary>
    private void HandleReplicatedGaugeChanged(float previousValue, float currentValue)
    {
        ReplicatedGaugeChanged?.Invoke(previousValue, currentValue);
    }
}
