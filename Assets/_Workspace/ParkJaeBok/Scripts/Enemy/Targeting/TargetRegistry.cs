using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타겟 태그별 Transform 등록/해제 목록을 캐시하는 정적 레지스트리입니다.
/// </summary>
public static class TargetRegistry
{
    private static readonly Dictionary<string, List<Transform>> _targetsByTag = new Dictionary<string, List<Transform>>(); // 태그별 등록된 타겟 Transform 목록 캐시 맵입니다.
    private static readonly List<Transform> _emptyTargets = new List<Transform>(0); // 미등록 태그 조회 시 반환할 빈 목록 캐시입니다.

    /// <summary>
    /// 지정 태그의 타겟 Transform을 레지스트리에 등록합니다.
    /// </summary>
    public static void Register(string targetTag, Transform targetTransform)
    {
        if (string.IsNullOrWhiteSpace(targetTag) || targetTransform == null)
        {
            return;
        }

        if (!_targetsByTag.TryGetValue(targetTag, out List<Transform> targets))
        {
            targets = new List<Transform>();
            _targetsByTag[targetTag] = targets;
        }

        if (targets.Contains(targetTransform))
        {
            return;
        }

        targets.Add(targetTransform);
    }

    /// <summary>
    /// 지정 태그의 타겟 Transform을 레지스트리에서 해제합니다.
    /// </summary>
    public static void Unregister(string targetTag, Transform targetTransform)
    {
        if (string.IsNullOrWhiteSpace(targetTag) || targetTransform == null)
        {
            return;
        }

        if (!_targetsByTag.TryGetValue(targetTag, out List<Transform> targets))
        {
            return;
        }

        targets.Remove(targetTransform);
        if (targets.Count == 0)
        {
            _targetsByTag.Remove(targetTag);
        }
    }

    /// <summary>
    /// 지정 태그의 타겟 Transform 목록을 반환하며 비활성/파괴 참조를 정리합니다.
    /// </summary>
    public static IReadOnlyList<Transform> GetTargets(string targetTag)
    {
        if (string.IsNullOrWhiteSpace(targetTag))
        {
            return _emptyTargets;
        }

        if (!_targetsByTag.TryGetValue(targetTag, out List<Transform> targets))
        {
            return _emptyTargets;
        }

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            Transform targetTransform = targets[i]; // 정리 대상 후보 Transform 참조입니다.
            if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy)
            {
                targets.RemoveAt(i);
            }
        }

        if (targets.Count == 0)
        {
            _targetsByTag.Remove(targetTag);
            return _emptyTargets;
        }

        return targets;
    }
}
