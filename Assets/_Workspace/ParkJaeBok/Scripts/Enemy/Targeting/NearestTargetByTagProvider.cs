using System.Collections.Generic;
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
    [Tooltip("레지스트리 목록을 재평가할 최소 간격(초)입니다.")]
    [Range(0.1f, 0.25f)]
    [SerializeField] private float _retargetInterval = 0.15f; // 타겟 재탐색 주기를 제한하는 캐시 타이머 간격 값입니다.

    private Transform _cachedTarget; // 캐시된 가장 가까운 타겟 Transform 참조입니다.
    private float _nextResolveTime; // 다음 타겟 재탐색 허용 시각입니다.

    /// <summary>
    /// Enemy 위치를 기준으로 레지스트리에서 가장 가까운 태그 타겟을 캐시 주기로 탐색해 반환합니다.
    /// </summary>
    public Transform ResolveTarget(Transform enemyTransform)
    {
        if (enemyTransform == null || string.IsNullOrWhiteSpace(_targetTag))
        {
            _cachedTarget = null;
            return null;
        }

        if (!IsTargetAvailable(_cachedTarget))
        {
            _cachedTarget = null;
        }

        if (Time.time < _nextResolveTime)
        {
            return _cachedTarget;
        }

        _nextResolveTime = Time.time + Mathf.Clamp(_retargetInterval, 0.1f, 0.25f);
        _cachedTarget = FindClosestFromRegistry(enemyTransform);
        return _cachedTarget;
    }

    /// <summary>
    /// 레지스트리 후보 목록에서 거리 기준으로 가장 가까운 유효 타겟을 계산합니다.
    /// </summary>
    private Transform FindClosestFromRegistry(Transform enemyTransform)
    {
        IReadOnlyList<Transform> candidates = TargetRegistry.GetTargets(_targetTag); // 타겟 후보군으로 사용할 레지스트리 목록입니다.
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        float safeMaxRange = Mathf.Max(0.1f, _maxAcquireRange); // 잘못된 최대 획득 거리를 보정한 안전 값입니다.
        float closestDistance = float.MaxValue; // 현재까지 탐색한 후보 중 최소 거리 캐시 값입니다.
        Transform closestTarget = null; // 현재까지 탐색한 후보 중 가장 가까운 타겟 참조입니다.

        for (int i = 0; i < candidates.Count; i++)
        {
            Transform candidate = candidates[i]; // 현재 순회 중인 타겟 후보 Transform 참조입니다.
            if (!IsTargetAvailable(candidate))
            {
                continue;
            }

            float distance = Vector2.Distance(enemyTransform.position, candidate.position); // Enemy와 후보 간 거리 값입니다.
            if (distance > safeMaxRange || distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closestTarget = candidate;
        }

        return closestTarget;
    }

    /// <summary>
    /// 타겟 Transform이 파괴/비활성화되지 않았는지 검증합니다.
    /// </summary>
    private static bool IsTargetAvailable(Transform targetTransform)
    {
        return targetTransform != null && targetTransform.gameObject.activeInHierarchy;
    }
}
