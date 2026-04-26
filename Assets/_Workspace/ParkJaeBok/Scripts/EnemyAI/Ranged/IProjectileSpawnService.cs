using UnityEngine;

/// <summary>
/// Enemy가 직접 Instantiate를 호출하지 않고 Projectile 생성/소멸을 요청하기 위한 서비스 인터페이스입니다.
/// </summary>
public interface IProjectileSpawnService
{
    /// <summary>
    /// Projectile 생성/발사를 요청하고 생성된 인스턴스를 반환합니다.
    /// </summary>
    PooledRangedProjectile RequestSpawn(
        GameObject prefab,
        Vector2 position,
        Vector2 direction,
        GameObject owner,
        float speed,
        float lifetime,
        bool isVisualOnly = false,
        int visualInstanceId = 0
    );

    /// <summary>
    /// 관찰자 시각 전용 Projectile을 복제 ID 기준으로 찾아 강제 종료합니다.
    /// </summary>
    bool TryDespawnVisual(int visualInstanceId);
}
