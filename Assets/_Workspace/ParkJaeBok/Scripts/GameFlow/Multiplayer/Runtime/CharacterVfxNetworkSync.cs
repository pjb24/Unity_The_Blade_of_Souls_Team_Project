using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 캐릭터 VFX 상태/이벤트를 NGO를 통해 복제해 Host/Client 모두 같은 VFX를 보도록 동기화하는 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class CharacterVfxNetworkSync : NetworkBehaviour
{
    [Header("Dependencies")]
    [Tooltip("실제 VFX 재생/정지를 수행할 CharacterVfxController 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private CharacterVfxController _vfxController; // 네트워크 복제 결과를 실제 렌더링에 반영할 VFX 컨트롤러 참조입니다.

    [Header("Replication")]
    [Tooltip("Eye/Walk 같은 지속형 상태를 네트워크로 복제할지 여부입니다.")]
    [SerializeField] private bool _replicatePersistentStates = true; // 지속형 상태 복제 활성 여부입니다.
    [Tooltip("Jump/Hit/Attack 같은 1회성 이벤트를 네트워크로 복제할지 여부입니다.")]
    [SerializeField] private bool _replicateOneShotEvents = true; // 1회성 이벤트 복제 활성 여부입니다.

    [Tooltip("서버 권한으로 재생된 HitEffect를 Owner 클라이언트에도 적용할지 여부입니다.")]
    [SerializeField] private bool _applyReplicatedHitEventToOwner = true; // 서버 확정 HitEffect를 Owner 화면에도 반영할지 여부입니다.

    [Header("Debug")]
    [Tooltip("네트워크 미사용 환경에서 로컬 실행만 수행할 때 대체 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseSinglePlayerFallbackLog; // 네트워크 비활성 환경 대체 로그 출력 여부입니다.

    private readonly NetworkVariable<bool> _eyeActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버가 확정한 EyeEffect 활성 상태입니다.

    private readonly NetworkVariable<bool> _walkActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버가 확정한 WalkDust 활성 상태입니다.

    private readonly NetworkVariable<int> _attackEffectSequence = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 공격 이펙트 1회 이벤트 복제를 위한 시퀀스 값입니다.

    private readonly NetworkVariable<int> _attackEffectActionType = new NetworkVariable<int>(
        (int)E_ActionType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버가 확정한 공격 이펙트 액션 타입 값입니다.

    private readonly NetworkVariable<bool> _attackEffectFacingRight = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // 서버가 확정한 공격 이펙트 좌우 방향 값입니다.

    private readonly NetworkVariable<int> _jumpSequence = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // JumpDust 1회 이벤트 복제를 위한 시퀀스 값입니다.

    private readonly NetworkVariable<Vector3> _jumpPosition = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // JumpDust 마지막 재생 좌표입니다.

    private readonly NetworkVariable<int> _hitSequence = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // HitEffect 1회 이벤트 복제를 위한 시퀀스 값입니다.

    private readonly NetworkVariable<Vector3> _hitPosition = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // HitEffect 마지막 재생 좌표입니다.

    /// <summary>
    /// 초기화 시 CharacterVfxController 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_vfxController == null)
        {
            _vfxController = GetComponent<CharacterVfxController>();
        }

        if (_vfxController == null)
        {
            Debug.LogWarning($"[CharacterVfxNetworkSync] CharacterVfxController 참조를 찾지 못했습니다. object={name}", this);
        }
    }

    /// <summary>
    /// 네트워크 스폰 시 복제 변수 콜백을 등록하고 초기 상태를 적용합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        _eyeActive.OnValueChanged += HandleEyeActiveChanged;
        _walkActive.OnValueChanged += HandleWalkActiveChanged;
        _attackEffectSequence.OnValueChanged += HandleAttackEffectSequenceChanged;
        _jumpSequence.OnValueChanged += HandleJumpSequenceChanged;
        _hitSequence.OnValueChanged += HandleHitSequenceChanged;

        if (!IsOwner)
        {
            ApplyReplicatedPersistentState();
        }
    }

    /// <summary>
    /// 네트워크 디스폰 시 복제 변수 콜백을 해제합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        _eyeActive.OnValueChanged -= HandleEyeActiveChanged;
        _walkActive.OnValueChanged -= HandleWalkActiveChanged;
        _attackEffectSequence.OnValueChanged -= HandleAttackEffectSequenceChanged;
        _jumpSequence.OnValueChanged -= HandleJumpSequenceChanged;
        _hitSequence.OnValueChanged -= HandleHitSequenceChanged;
    }

    /// <summary>
    /// EyeEffect 활성 상태를 로컬 적용하고 네트워크 세션에서는 서버 확정 상태로 복제합니다.
    /// </summary>
    public void RequestSetEyeEffectActive(bool isActive)
    {
        _vfxController?.SetEyeEffectActive(isActive);

        if (!_replicatePersistentStates)
        {
            return;
        }

        if (!CanReplicateOverNetwork())
        {
            return;
        }

        if (IsServer)
        {
            _eyeActive.Value = isActive;
            return;
        }

        SubmitEyeStateRpc(isActive);
    }

    /// <summary>
    /// WalkDust 활성 상태를 로컬 적용하고 네트워크 세션에서는 서버 확정 상태로 복제합니다.
    /// </summary>
    public void RequestSetWalkDustActive(bool isActive)
    {
        _vfxController?.SetWalkDustActive(isActive);

        if (!_replicatePersistentStates)
        {
            return;
        }

        if (!CanReplicateOverNetwork())
        {
            return;
        }

        if (IsServer)
        {
            _walkActive.Value = isActive;
            return;
        }

        SubmitWalkStateRpc(isActive);
    }

    /// <summary>
    /// 공격 이펙트 1회 재생을 로컬 적용하고 네트워크 세션에서는 서버 확정 이벤트로 복제합니다.
    /// </summary>
    public void RequestPlayAttackEffect(E_ActionType actionType, bool isFacingRight)
    {
        _vfxController?.PlayAttackEffect(actionType, isFacingRight);

        if (!_replicateOneShotEvents)
        {
            return;
        }

        if (!CanReplicateOverNetwork())
        {
            return;
        }

        if (IsServer)
        {
            _attackEffectActionType.Value = (int)actionType;
            _attackEffectFacingRight.Value = isFacingRight;
            _attackEffectSequence.Value++;
            return;
        }

        SubmitAttackEffectEventRpc(actionType, isFacingRight);
    }

    /// <summary>
    /// JumpDust 1회 재생을 로컬 적용하고 네트워크 세션에서는 좌표+시퀀스로 복제합니다.
    /// </summary>
    public void RequestPlayJumpDust(Vector3 worldPosition)
    {
        _vfxController?.PlayJumpDustAt(worldPosition);

        if (!_replicateOneShotEvents)
        {
            return;
        }

        if (!CanReplicateOverNetwork())
        {
            return;
        }

        if (IsServer)
        {
            _jumpPosition.Value = worldPosition;
            _jumpSequence.Value++;
            return;
        }

        SubmitJumpEventRpc(worldPosition);
    }

    /// <summary>
    /// HitEffect 1회 재생을 로컬 적용하고 네트워크 세션에서는 좌표+시퀀스로 복제합니다.
    /// </summary>
    public void RequestPlayHitEffect(Vector3 worldPosition)
    {
        _vfxController?.PlayHitEffectAt(worldPosition);

        if (!_replicateOneShotEvents)
        {
            return;
        }

        if (!CanReplicateOverNetwork())
        {
            return;
        }

        if (IsServer)
        {
            _hitPosition.Value = worldPosition;
            _hitSequence.Value++;
            return;
        }

        SubmitHitEventRpc(worldPosition);
    }

    /// <summary>
    /// Owner가 요청한 Eye 상태를 서버 권한으로 확정합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SubmitEyeStateRpc(bool isActive, RpcParams rpcParams = default)
    {
        if (!ValidateRpcSender(rpcParams.Receive.SenderClientId, nameof(SubmitEyeStateRpc)))
        {
            return;
        }

        _eyeActive.Value = isActive;
    }

    /// <summary>
    /// Owner가 요청한 WalkDust 상태를 서버 권한으로 확정합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SubmitWalkStateRpc(bool isActive, RpcParams rpcParams = default)
    {
        if (!ValidateRpcSender(rpcParams.Receive.SenderClientId, nameof(SubmitWalkStateRpc)))
        {
            return;
        }

        _walkActive.Value = isActive;
    }

    /// <summary>
    /// Owner가 요청한 공격 이펙트 이벤트를 서버 권한으로 확정합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SubmitAttackEffectEventRpc(E_ActionType actionType, bool isFacingRight, RpcParams rpcParams = default)
    {
        if (!ValidateRpcSender(rpcParams.Receive.SenderClientId, nameof(SubmitAttackEffectEventRpc)))
        {
            return;
        }

        _attackEffectActionType.Value = (int)actionType;
        _attackEffectFacingRight.Value = isFacingRight;
        _attackEffectSequence.Value++;
    }

    /// <summary>
    /// Owner가 요청한 JumpDust 좌표 이벤트를 서버 권한으로 확정합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SubmitJumpEventRpc(Vector3 worldPosition, RpcParams rpcParams = default)
    {
        if (!ValidateRpcSender(rpcParams.Receive.SenderClientId, nameof(SubmitJumpEventRpc)))
        {
            return;
        }

        _jumpPosition.Value = worldPosition;
        _jumpSequence.Value++;
    }

    /// <summary>
    /// Owner가 요청한 HitEffect 좌표 이벤트를 서버 권한으로 확정합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SubmitHitEventRpc(Vector3 worldPosition, RpcParams rpcParams = default)
    {
        if (!ValidateRpcSender(rpcParams.Receive.SenderClientId, nameof(SubmitHitEventRpc)))
        {
            return;
        }

        _hitPosition.Value = worldPosition;
        _hitSequence.Value++;
    }

    /// <summary>
    /// Eye 상태 복제값이 변경되면 비소유 인스턴스에 적용합니다.
    /// </summary>
    private void HandleEyeActiveChanged(bool previousValue, bool currentValue)
    {
        if (IsOwner)
        {
            return;
        }

        _vfxController?.SetEyeEffectActive(currentValue);
    }

    /// <summary>
    /// WalkDust 상태 복제값이 변경되면 비소유 인스턴스에 적용합니다.
    /// </summary>
    private void HandleWalkActiveChanged(bool previousValue, bool currentValue)
    {
        if (IsOwner)
        {
            return;
        }

        _vfxController?.SetWalkDustActive(currentValue);
    }

    /// <summary>
    /// 공격 이펙트 시퀀스가 변경되면 비소유 인스턴스에서 1회 재생을 수행합니다.
    /// </summary>
    private void HandleAttackEffectSequenceChanged(int previousValue, int currentValue)
    {
        if (IsOwner || currentValue == previousValue)
        {
            return;
        }

        E_ActionType actionType = (E_ActionType)_attackEffectActionType.Value;
        _vfxController?.PlayAttackEffect(actionType, _attackEffectFacingRight.Value);
    }

    /// <summary>
    /// JumpDust 시퀀스가 변경되면 비소유 인스턴스에서 1회 재생을 수행합니다.
    /// </summary>
    private void HandleJumpSequenceChanged(int previousValue, int currentValue)
    {
        if (IsOwner || currentValue == previousValue)
        {
            return;
        }

        _vfxController?.PlayJumpDustAt(_jumpPosition.Value);
    }

    /// <summary>
    /// HitEffect 시퀀스가 변경되면 대상 인스턴스에서 1회 재생을 수행합니다.
    /// </summary>
    private void HandleHitSequenceChanged(int previousValue, int currentValue)
    {
        if (currentValue == previousValue)
        {
            return;
        }

        if (IsOwner && !_applyReplicatedHitEventToOwner)
        {
            return;
        }

        _vfxController?.PlayHitEffectAt(_hitPosition.Value);
    }

    /// <summary>
    /// 스폰 직후 비소유 인스턴스의 지속형 상태 스냅샷을 반영합니다.
    /// </summary>
    private void ApplyReplicatedPersistentState()
    {
        _vfxController?.SetEyeEffectActive(_eyeActive.Value);
        _vfxController?.SetWalkDustActive(_walkActive.Value);
    }

    /// <summary>
    /// 현재 노드가 NGO 세션 상태인지 확인하고 싱글플레이 대체 로그를 제어합니다.
    /// </summary>
    private bool CanReplicateOverNetwork()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
        {
            if (_verboseSinglePlayerFallbackLog)
            {
                Debug.LogWarning($"[CharacterVfxNetworkSync] NetworkManager가 없거나 세션이 시작되지 않아 로컬 실행만 수행합니다. object={name}", this);
            }

            return false;
        }

        if (!IsSpawned)
        {
            Debug.LogWarning($"[CharacterVfxNetworkSync] 네트워크 세션 중이지만 오브젝트가 Spawn되지 않아 로컬 실행만 수행합니다. object={name}", this);
            return false;
        }

        if (!IsOwner && !IsServer)
        {
            Debug.LogWarning($"[CharacterVfxNetworkSync] 소유자도 서버도 아닌 인스턴스에서 복제 요청이 발생했습니다. object={name}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// RPC 전송자가 현재 오브젝트 Owner인지 검증합니다.
    /// </summary>
    private bool ValidateRpcSender(ulong senderClientId, string rpcName)
    {
        if (senderClientId == OwnerClientId)
        {
            return true;
        }

        Debug.LogWarning($"[CharacterVfxNetworkSync] Unauthorized RPC sender. rpc={rpcName}, sender={senderClientId}, owner={OwnerClientId}, object={name}", this);
        return false;
    }
}
