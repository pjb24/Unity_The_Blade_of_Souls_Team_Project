using UnityEngine;

/// <summary>
/// ObjectPool에서 재사용되는 개체가 대여와 반환 시점에 필요한 상태를 직접 정리하기 위한 공용 인터페이스입니다.
/// </summary>
public interface IPoolableObject
{
    /// <summary>
    /// Pool에서 꺼낸 직후 활성화 전에 호출되어 새 사용 상태를 초기화합니다.
    /// </summary>
    void OnPoolSpawned(PoolSpawnContext context);

    /// <summary>
    /// Pool로 반환되기 직전에 호출되어 이전 사용 상태를 정리합니다.
    /// </summary>
    void OnPoolDespawned();
}
