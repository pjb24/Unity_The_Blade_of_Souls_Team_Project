using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 실행 컨텍스트(싱글/Host/Client)에 맞는 플레이어 스폰 슬롯을 결정하는 리졸버입니다.
/// </summary>
public static class PlayerSpawnResolver
{
    /// <summary>
    /// 싱글플레이 컨텍스트에서 사용할 슬롯을 반환합니다.
    /// </summary>
    public static E_PlayerSpawnSlot ResolveSinglePlayerSlot()
    {
        return E_PlayerSpawnSlot.Single;
    }

    /// <summary>
    /// NGO 서버 컨텍스트에서 clientId 소유 플레이어가 사용할 슬롯을 반환합니다.
    /// </summary>
    public static E_PlayerSpawnSlot ResolveMultiplayerSlot(NetworkManager networkManager, ulong clientId)
    {
        if (networkManager != null && clientId == NetworkManager.ServerClientId)
        {
            return E_PlayerSpawnSlot.Host;
        }

        return E_PlayerSpawnSlot.Client;
    }
}
