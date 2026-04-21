using UnityEngine;

/// <summary>
/// Enemy가 직접 Instantiate하지 않고 투사체 생성을 요청하기 위한 서비스 인터페이스입니다.
/// </summary>
public interface IProjectileSpawnService
{
    /// <summary>
    /// 투사체 생성/재사용을 요청합니다.
    /// </summary>
    void RequestSpawn(
        GameObject prefab,
        Vector2 position,
        Vector2 direction,
        GameObject owner,
        float speed,
        float lifetime
    );
}
