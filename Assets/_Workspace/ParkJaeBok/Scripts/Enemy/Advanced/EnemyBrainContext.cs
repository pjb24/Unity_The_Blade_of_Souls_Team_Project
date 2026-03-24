using UnityEngine;

/// <summary>
/// EnemyBrain이 확장 정책/기믹 모듈에 전달하는 런타임 문맥 데이터입니다.
/// </summary>
public readonly struct EnemyBrainContext
{
    public readonly Transform Self; // 현재 Enemy 자신의 트랜스폼 참조입니다.
    public readonly Transform Target; // 현재 추적 대상으로 판단된 타겟 트랜스폼 참조입니다.
    public readonly float DistanceToTarget; // 현재 Enemy와 타겟 사이 거리 값입니다.
    public readonly Vector2 SpawnPosition; // 리쉬/복귀 판단 기준이 되는 스폰 좌표입니다.
    public readonly EnemyArchetypeData Archetype; // 상태 판단에 사용할 아키타입 데이터 참조입니다.

    /// <summary>
    /// 전달받은 상태 스냅샷으로 EnemyBrain 문맥 구조체를 생성합니다.
    /// </summary>
    public EnemyBrainContext(Transform self, Transform target, float distanceToTarget, Vector2 spawnPosition, EnemyArchetypeData archetype)
    {
        Self = self;
        Target = target;
        DistanceToTarget = distanceToTarget;
        SpawnPosition = spawnPosition;
        Archetype = archetype;
    }
}
