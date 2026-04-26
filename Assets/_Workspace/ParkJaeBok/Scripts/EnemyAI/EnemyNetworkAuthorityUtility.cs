using Unity.Netcode;

/// <summary>
/// Enemy AI가 싱글플레이와 NGO 세션에서 동일한 권한 기준으로 동작하도록 판정하는 유틸리티입니다.
/// </summary>
public static class EnemyNetworkAuthorityUtility
{
    /// <summary>
    /// 현재 런타임이 NGO 네트워크 세션을 사용 중인지 판정합니다.
    /// </summary>
    public static bool IsNetworkSessionActive()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null && networkManager.IsListening;
    }

    /// <summary>
    /// Enemy AI 의사결정을 현재 인스턴스가 수행해야 하는지 판정합니다.
    /// </summary>
    public static bool ShouldRunServerAuthoritativeLogic(NetworkObject networkObject)
    {
        if (!IsNetworkSessionActive())
        {
            return true;
        }

        if (networkObject == null || !networkObject.IsSpawned)
        {
            return true;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null && networkManager.IsServer;
    }

    /// <summary>
    /// 서버가 현재 결과를 NGO로 관찰자에게 복제해야 하는지 판정합니다.
    /// </summary>
    public static bool ShouldReplicateFromServer(NetworkObject networkObject)
    {
        if (!IsNetworkSessionActive())
        {
            return false;
        }

        if (networkObject == null || !networkObject.IsSpawned)
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null && networkManager.IsServer;
    }
}
