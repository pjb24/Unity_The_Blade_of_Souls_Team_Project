using UnityEngine;

/// <summary>
/// EnemyBrain이 추적 대상을 조회할 때 사용하는 타겟 제공 인터페이스입니다.
/// </summary>
public interface IEnemyTargetProvider
{
    /// <summary>
    /// 현재 Enemy 기준으로 유효한 타겟 Transform을 반환합니다.
    /// </summary>
    Transform ResolveTarget(Transform enemyTransform);
}
