using UnityEngine;

/// <summary>
/// 지정 태그를 가진 오브젝트 중 가장 가까운 타겟을 제공하는 기본 TargetProvider입니다.
/// </summary>
public class NearestTargetByTagProvider : MonoBehaviour, IEnemyTargetProvider
{
    [Tooltip("타겟 후보를 필터링할 태그 문자열입니다.")]
    [SerializeField] private string _targetTag = "Player"; // 타겟 후보를 필터링할 태그 문자열입니다.
    [Tooltip("타겟 획득 허용 최대 거리입니다.")]
    [SerializeField] private float _maxAcquireRange = 20f; // 타겟 획득 허용 최대 거리입니다.

    /// <summary>
    /// Enemy 위치를 기준으로 가장 가까운 태그 타겟을 탐색해 반환합니다.
    /// </summary>
    public Transform ResolveTarget(Transform enemyTransform)
    {
        if (enemyTransform == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_targetTag))
        {
            return null;
        }

        GameObject[] candidates = GameObject.FindGameObjectsWithTag(_targetTag); // 타겟 후보군으로 사용할 태그 오브젝트 목록입니다.
        if (candidates == null || candidates.Length == 0)
        {
            return null;
        }

        float safeMaxRange = Mathf.Max(0.1f, _maxAcquireRange); // 잘못된 최대 획득 거리를 보정한 안전 값입니다.
        float closestDistance = float.MaxValue; // 현재까지 탐색한 후보 중 최소 거리 캐시 값입니다.
        Transform closestTarget = null; // 현재까지 탐색한 후보 중 가장 가까운 타겟 참조입니다.

        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject candidate = candidates[i]; // 현재 순회 중인 타겟 후보 오브젝트입니다.
            if (candidate == null)
            {
                continue;
            }

            float distance = Vector2.Distance(enemyTransform.position, candidate.transform.position); // Enemy와 후보 간 거리 값입니다.
            if (distance > safeMaxRange)
            {
                continue;
            }

            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closestTarget = candidate.transform;
        }

        return closestTarget;
    }
}
