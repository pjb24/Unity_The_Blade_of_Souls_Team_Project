using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Enemy가 탐지 범위 안의 유효한 Player 후보를 검사해 가장 가까운 타겟을 선택하는 공통 탐지기입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyTargetDetector : MonoBehaviour
{
    [Header("Target Search")]
    [Tooltip("Player 탐지에 사용할 2D 레이어 마스크입니다.")]
    [SerializeField] private LayerMask _targetLayerMask; // Player 후보 탐지에 사용할 레이어 마스크입니다.
    [Tooltip("비어 있지 않으면 지정한 태그를 가진 계층만 타겟 후보로 허용합니다.")]
    [SerializeField] private string _targetTag = "Player"; // Player 후보 필터링에 사용할 태그 문자열입니다.
    [Tooltip("OverlapCircle 결과를 임시 저장할 버퍼 크기입니다. 부족하면 일부 후보가 잘릴 수 있습니다.")]
    [SerializeField] private int _searchBufferSize = 16; // Physics2D 탐지 결과를 임시 저장할 버퍼 크기입니다.

    private Collider2D[] _searchBuffer; // OverlapCircle 결과를 임시 저장하는 버퍼 배열입니다.
    private Transform _cachedTarget; // 가장 최근 탐지에서 선택된 타겟 Transform 캐시입니다.
    private HealthComponent _cachedTargetHealth; // 현재 캐시된 타겟의 HealthComponent 캐시입니다.
    private float _nextSearchAt; // 다음 전체 후보 재평가가 가능한 시각입니다.
    private ContactFilter2D _targetFilter; // 타겟 레이어와 Trigger 설정을 고정해 재사용하는 ContactFilter2D 캐시입니다.

    /// <summary>
    /// 현재 캐시된 타겟 Transform을 반환합니다.
    /// </summary>
    public Transform CurrentTarget => _cachedTarget;

    /// <summary>
    /// 런타임 탐지 버퍼와 ContactFilter2D 캐시를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        EnsureSearchBuffer();
        RefreshContactFilter();
    }

    /// <summary>
    /// 인스펙터 변경 시 버퍼 크기와 탐지 필터 설정을 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        EnsureSearchBuffer();
        RefreshContactFilter();
    }

    /// <summary>
    /// 현재 타겟 캐시를 즉시 초기화합니다.
    /// </summary>
    public void ClearTarget()
    {
        _cachedTarget = null;
        _cachedTargetHealth = null;
    }

    /// <summary>
    /// 주기적으로 탐지 범위 안의 모든 후보를 다시 검사해 가장 가까운 유효 타겟을 캐시합니다.
    /// </summary>
    public void TickSearch(float nowTime, Vector2 origin, float detectionRange, float targetSearchInterval)
    {
        if (_cachedTarget != null && !IsTargetValid(_cachedTarget, _cachedTargetHealth))
        {
            ClearTarget();
        }

        if (nowTime < _nextSearchAt)
        {
            return;
        }

        _nextSearchAt = nowTime + Mathf.Max(0.01f, targetSearchInterval);

        int hitCount = FindTargetsInRange(origin, detectionRange);
        SelectClosestTarget(origin, hitCount);
    }

    /// <summary>
    /// ContactFilter2D 기반 OverlapCircle로 범위 안의 후보 Collider를 수집합니다.
    /// </summary>
    private int FindTargetsInRange(Vector2 origin, float detectionRange)
    {
        return Physics2D.OverlapCircle(origin, detectionRange, _targetFilter, _searchBuffer);
    }

    /// <summary>
    /// 현재 캐시된 타겟이 아직 유효한지 반환합니다.
    /// </summary>
    public bool HasValidTarget()
    {
        return IsTargetValid(_cachedTarget, _cachedTargetHealth);
    }

    /// <summary>
    /// 타겟 Transform과 HealthComponent가 현재 전투 후보로 유효한지 판정합니다.
    /// </summary>
    public bool IsTargetValid(Transform target, HealthComponent targetHealth)
    {
        if (target == null)
        {
            return false;
        }

        GameObject targetObject = target.gameObject;
        if (!targetObject.activeInHierarchy)
        {
            return false;
        }

        if (!target.gameObject.scene.IsValid())
        {
            return false;
        }

        if (target.gameObject.scene != gameObject.scene || target.gameObject.scene != SceneManager.GetActiveScene())
        {
            return false;
        }

        if (targetHealth != null && targetHealth.IsDead)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 탐지된 모든 후보를 필터링해 가장 가까운 유효 Player를 현재 타겟으로 갱신합니다.
    /// </summary>
    private void SelectClosestTarget(Vector2 origin, int hitCount)
    {
        float nearestSqrDistance = float.MaxValue; // 이번 탐색 루프에서 찾은 최단 거리 제곱값입니다.
        Transform closestTarget = null; // 이번 탐색 루프에서 선택된 최종 타겟 Transform입니다.
        HealthComponent closestTargetHealth = null; // 이번 탐색 루프에서 선택된 최종 타겟 HealthComponent입니다.

        for (int index = 0; index < hitCount; index++)
        {
            Collider2D candidateCollider = _searchBuffer[index];
            _searchBuffer[index] = null;
            if (candidateCollider == null)
            {
                continue;
            }

            Transform candidateTarget = ResolveCandidateTarget(candidateCollider);
            if (!MatchesTargetTag(candidateTarget))
            {
                continue;
            }

            HealthComponent candidateHealth = ResolveTargetHealthComponent(candidateTarget);
            if (!IsTargetValid(candidateTarget, candidateHealth))
            {
                continue;
            }

            float sqrDistance = ((Vector2)candidateTarget.position - origin).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
            {
                continue;
            }

            nearestSqrDistance = sqrDistance;
            closestTarget = candidateTarget;
            closestTargetHealth = candidateHealth;
        }

        _cachedTarget = closestTarget;
        _cachedTargetHealth = closestTargetHealth;
    }

    /// <summary>
    /// Collider 기준으로 실제 거리 계산과 상태 검증에 사용할 대표 Transform을 해석합니다.
    /// </summary>
    private Transform ResolveCandidateTarget(Collider2D candidateCollider)
    {
        if (candidateCollider == null)
        {
            return null;
        }

        if (candidateCollider.attachedRigidbody != null)
        {
            return candidateCollider.attachedRigidbody.transform;
        }

        return candidateCollider.transform;
    }

    /// <summary>
    /// 후보 Transform이 지정한 Player 태그 조건을 만족하는지 부모 계층까지 포함해 판정합니다.
    /// </summary>
    private bool MatchesTargetTag(Transform candidateTarget)
    {
        if (candidateTarget == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_targetTag))
        {
            return true;
        }

        Transform current = candidateTarget;
        while (current != null)
        {
            if (current.CompareTag(_targetTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    /// <summary>
    /// 타겟 계층에서 HealthComponent를 찾아 사망 여부 판정에 사용합니다.
    /// </summary>
    private HealthComponent ResolveTargetHealthComponent(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        return target.GetComponent<HealthComponent>() ?? target.GetComponentInParent<HealthComponent>();
    }

    /// <summary>
    /// 버퍼 크기 설정을 보정하고 탐지 버퍼 배열을 준비합니다.
    /// </summary>
    private void EnsureSearchBuffer()
    {
        if (_searchBufferSize <= 0)
        {
            Debug.LogWarning($"[EnemyTargetDetector] Invalid _searchBufferSize({_searchBufferSize}) on {name}. Fallback to 8.");
            _searchBufferSize = 8;
        }

        if (_searchBuffer == null || _searchBuffer.Length != _searchBufferSize)
        {
            _searchBuffer = new Collider2D[_searchBufferSize];
        }
    }

    /// <summary>
    /// LayerMask와 Trigger 규칙을 최신 인스펙터 값으로 다시 구성합니다.
    /// </summary>
    private void RefreshContactFilter()
    {
        _targetFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _targetLayerMask,
            useTriggers = Physics2D.queriesHitTriggers
        };
    }
}
