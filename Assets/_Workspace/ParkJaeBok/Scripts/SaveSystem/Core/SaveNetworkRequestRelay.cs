using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 멀티플레이 Client의 슬롯 저장/로드 요청을 Host 또는 Server 권한 흐름으로 전달하는 NGO 릴레이입니다.
/// </summary>
public class SaveNetworkRequestRelay : NetworkBehaviour
{
    [Tooltip("서버 권한 슬롯 저장/로드를 실제로 수행할 SaveDataStore입니다. 비어 있으면 SaveDataStore.Instance를 사용합니다.")]
    [SerializeField] private SaveDataStore _saveDataStore; // 서버에서 검증된 슬롯 저장/로드 작업을 수행할 저장소입니다.

    /// <summary>
    /// 지정한 플레이 슬롯 저장을 요청합니다. Client에서는 RPC로 Server에 요청하고, Server나 싱글플레이에서는 즉시 저장합니다.
    /// </summary>
    public bool RequestSaveSlot(E_SaveSlot slot)
    {
        if (!TryResolveStore(out SaveDataStore saveDataStore))
        {
            return false;
        }

        if (!IsSpawned || NetworkManager == null || !NetworkManager.IsListening)
        {
            return saveDataStore.SaveSlot(slot, "SaveNetworkRequestRelay.LocalSave");
        }

        if (IsServer)
        {
            return saveDataStore.SaveSlot(slot, "SaveNetworkRequestRelay.ServerSave");
        }

        RequestSaveSlotRpc((int)slot);
        return true;
    }

    /// <summary>
    /// 지정한 플레이 슬롯 로드를 요청합니다. Client에서는 RPC로 Server에 요청하고, Server나 싱글플레이에서는 즉시 로드합니다.
    /// </summary>
    public bool RequestLoadSlot(E_SaveSlot slot)
    {
        if (!TryResolveStore(out SaveDataStore saveDataStore))
        {
            return false;
        }

        if (!IsSpawned || NetworkManager == null || !NetworkManager.IsListening)
        {
            return saveDataStore.LoadSlot(slot, "SaveNetworkRequestRelay.LocalLoad");
        }

        if (IsServer)
        {
            return saveDataStore.LoadSlot(slot, "SaveNetworkRequestRelay.ServerLoad");
        }

        RequestLoadSlotRpc((int)slot);
        return true;
    }

    /// <summary>
    /// 클라이언트의 슬롯 저장 요청을 서버에서 처리하는 NGO 2.x RPC 진입점입니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestSaveSlotRpc(int slotIndex)
    {
        if (!TryResolveStore(out SaveDataStore saveDataStore))
        {
            return;
        }

        saveDataStore.SaveSlot((E_SaveSlot)slotIndex, $"SaveNetworkRequestRelay.ClientRequestSave.{OwnerClientId}");
    }

    /// <summary>
    /// 클라이언트의 슬롯 로드 요청을 서버에서 처리하는 NGO 2.x RPC 진입점입니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestLoadSlotRpc(int slotIndex)
    {
        if (!TryResolveStore(out SaveDataStore saveDataStore))
        {
            return;
        }

        saveDataStore.LoadSlot((E_SaveSlot)slotIndex, $"SaveNetworkRequestRelay.ClientRequestLoad.{OwnerClientId}");
    }

    /// <summary>
    /// RPC 처리와 로컬 요청 처리에 사용할 저장소를 찾습니다.
    /// </summary>
    private bool TryResolveStore(out SaveDataStore saveDataStore)
    {
        if (_saveDataStore == null)
        {
            _saveDataStore = SaveDataStore.Instance;
        }

        saveDataStore = _saveDataStore;
        if (saveDataStore != null)
        {
            return true;
        }

        Debug.LogWarning("[SaveNetworkRequestRelay] SaveDataStore를 찾을 수 없어 슬롯 요청을 처리하지 못했습니다.", this);
        return false;
    }
}
