using UnityEngine;

/// <summary>
/// 원거리 Enemy가 적정 거리 유지/후퇴/강제 공격을 결정하도록 돕는 기본 전술 정책입니다.
/// </summary>
public class RangedDecisionPolicy : MonoBehaviour, IEnemyDecisionPolicy
{
    [Tooltip("원거리 Enemy가 유지하려는 최소 교전 거리입니다.")]
    [SerializeField] private float _preferredMinRange = 3f; // 원거리 Enemy가 유지하려는 최소 교전 거리입니다.
    [Tooltip("원거리 Enemy가 유지하려는 최대 교전 거리입니다.")]
    [SerializeField] private float _preferredMaxRange = 6f; // 원거리 Enemy가 유지하려는 최대 교전 거리입니다.
    [Tooltip("최소 거리 붕괴 시 후퇴할 거리 값입니다.")]
    [SerializeField] private float _retreatDistance = 2f; // 최소 거리 붕괴 시 후퇴할 거리 값입니다.

    /// <summary>
    /// 현재 타겟 거리 기반으로 원거리 전술 결정을 평가합니다.
    /// </summary>
    public EnemyDecisionResult Evaluate(in EnemyBrainContext context)
    {
        if (context.Target == null || context.Self == null)
        {
            return EnemyDecisionResult.UseDefault();
        }

        float distance = context.DistanceToTarget; // 현재 Enemy와 타겟 사이의 거리 값입니다.

        if (distance < Mathf.Max(0.1f, _preferredMinRange))
        {
            Vector2 retreatDirection = (context.Self.position - context.Target.position).normalized; // 후퇴 좌표 계산에 사용할 반대 방향 벡터입니다.
            Vector2 retreatTargetPosition = (Vector2)context.Self.position + retreatDirection * Mathf.Max(0.1f, _retreatDistance); // 이번 프레임 후퇴 지시 좌표입니다.
            return EnemyDecisionResult.ForceRetreat(retreatTargetPosition);
        }

        if (distance <= Mathf.Max(_preferredMinRange, _preferredMaxRange))
        {
            return new EnemyDecisionResult(E_EnemyDecisionType.ForceAttack, Vector2.zero);
        }

        return new EnemyDecisionResult(E_EnemyDecisionType.ForceChase, Vector2.zero);
    }
}
