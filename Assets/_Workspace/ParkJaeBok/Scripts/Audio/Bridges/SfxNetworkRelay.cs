using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 루트 NetworkObject에서 SFX 상태 이벤트를 서버/클라이언트로 중계하는 네트워크 릴레이입니다.
/// </summary>
public class SfxNetworkRelay : NetworkBehaviour
{
    /// <summary>
    /// 서버가 확정해 브로드캐스트한 SFX 이벤트를 로컬 인스턴스에 통지합니다.
    /// </summary>
    public event Action<E_SfxEventType, string, E_SoundId> ReplicatedSfxReceived;

    /// <summary>
    /// 소유자 기반 상태 SFX를 네트워크에 전파한 결과 코드입니다.
    /// </summary>
    public enum E_OwnerDispatchResult
    {
        NotNetworked = 0,
        IgnoredNotOwner = 1,
        SentToServer = 2,
        BroadcastFromServer = 3,
    }

    /// <summary>
    /// 현재 릴레이가 유효한 네트워크 세션에서 동작 중인지 반환합니다.
    /// </summary>
    public bool IsNetworkSessionActive()
    {
        NetworkManager singleton = NetworkManager.Singleton; // 네트워크 세션 상태 확인에 사용할 NGO 싱글톤 참조입니다.
        if (singleton == null || !singleton.IsListening)
        {
            return false;
        }

        return IsSpawned;
    }

    /// <summary>
    /// 현재 릴레이 인스턴스가 서버 권한인지 반환합니다.
    /// </summary>
    public bool IsServerRelay()
    {
        return IsNetworkSessionActive() && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }

    /// <summary>
    /// 현재 릴레이 인스턴스가 소유자 권한인지 반환합니다.
    /// </summary>
    public bool IsOwnerRelay()
    {
        return IsNetworkSessionActive() && IsOwner;
    }

    /// <summary>
    /// 소유자 권한 상태 SFX를 서버 또는 서버 경유로 전파합니다.
    /// </summary>
    public E_OwnerDispatchResult DispatchOwnerStateSfx(E_SfxEventType eventType, string subTypeKey, E_SoundId fallbackSoundId)
    {
        if (!IsNetworkSessionActive())
        {
            return E_OwnerDispatchResult.NotNetworked;
        }

        if (!IsOwner)
        {
            return E_OwnerDispatchResult.IgnoredNotOwner;
        }

        if (IsServerRelay())
        {
            BroadcastSfxToClientsRpc((int)eventType, subTypeKey ?? string.Empty, (int)fallbackSoundId);
            return E_OwnerDispatchResult.BroadcastFromServer;
        }

        SubmitOwnerStateSfxToServerRpc((int)eventType, subTypeKey ?? string.Empty, (int)fallbackSoundId);
        return E_OwnerDispatchResult.SentToServer;
    }

    /// <summary>
    /// 서버 권한에서 발생한 상태 SFX를 클라이언트에 브로드캐스트합니다.
    /// </summary>
    public bool BroadcastServerAuthoritativeSfx(E_SfxEventType eventType, string subTypeKey, E_SoundId fallbackSoundId)
    {
        if (!IsServerRelay())
        {
            return false;
        }

        BroadcastSfxToClientsRpc((int)eventType, subTypeKey ?? string.Empty, (int)fallbackSoundId);
        return true;
    }

    /// <summary>
    /// 소유자가 보고한 상태 SFX를 서버에서 검증 후 브로드캐스트합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SubmitOwnerStateSfxToServerRpc(int eventTypeValue, string subTypeKey, int fallbackSoundIdValue, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning($"[SfxNetworkRelay] Unauthorized SFX sender. object={name}, sender={rpcParams.Receive.SenderClientId}, owner={OwnerClientId}", this);
            return;
        }

        BroadcastSfxToClientsRpc(eventTypeValue, subTypeKey ?? string.Empty, fallbackSoundIdValue);
    }

    /// <summary>
    /// 서버를 제외한 클라이언트에서 수신된 SFX 이벤트를 콜백으로 전달합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void BroadcastSfxToClientsRpc(int eventTypeValue, string subTypeKey, int fallbackSoundIdValue)
    {
        E_SfxEventType eventType = (E_SfxEventType)eventTypeValue; // 브로드캐스트 수신 이벤트 타입 변환값입니다.
        E_SoundId fallbackSoundId = (E_SoundId)fallbackSoundIdValue; // 브로드캐스트 수신 fallback 사운드 ID 변환값입니다.

        ReplicatedSfxReceived?.Invoke(eventType, subTypeKey ?? string.Empty, fallbackSoundId);
    }
}
