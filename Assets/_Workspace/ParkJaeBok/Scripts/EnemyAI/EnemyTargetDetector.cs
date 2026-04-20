using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// LayerMask 기반 주기 탐색과 타겟 유효성 캐싱을 담당하는 타겟 탐지 컨트롤러입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyTargetDetector : MonoBehaviour
{
    [Header("Target Search")]
    [Tooltip("플레이어 탐색에 사용할 2D 레이어 마스크입니다.")]
    [SerializeField] private LayerMask _targetLayerMask; // 타겟 탐색 레이어 마스크입니다.
    [Tooltip("비어 있지 않으면 지정 태그와 일치하는 대상만 타겟으로 허용합니다.")]
    [SerializeField] private string _targetTag = "Player"; // 타겟 태그 필터 문자열입니다.
    [Tooltip("Physics2D 탐색 버퍼 크기입니다. 부족하면 가장 가까운 일부 대상만 검사됩니다.")]
    [SerializeField] private int _searchBufferSize = 16; // 타겟 후보 임시 버퍼 크기입니다.

    private Collider2D[] _searchBuffer; // OverlapCircle 결과 저장 버퍼입니다.
    private Transform _cachedTarget; // 현재 캐싱된 타겟 Transform입니다.
    private HealthComponent _cachedTargetHealth; // 현재 캐싱된 타겟 체력 컴포넌트입니다.
    private float _nextSearchAt; // 다음 탐색 시각 캐시 값입니다.

    /// <summary>
    /// 현재 캐싱된 타겟 Transform을 반환합니다.
    /// </summary>
    public Transform CurrentTarget => _cachedTarget;

    /// <summary>
    /// 탐색 버퍼를 준비합니다.
    /// </summary>
    private void Awake()
    {
        if (_searchBufferSize <= 0)
        {
            Debug.LogWarning($"[EnemyTargetDetector] Invalid _searchBufferSize({_searchBufferSize}) on {name}. Fallback to 8.");
            _searchBufferSize = 8;
        }

        _searchBuffer = new Collider2D[_searchBufferSize];
    }

    /// <summary>
    /// 캐시된 타겟을 초기화합니다.
    /// </summary>
    public void ClearTarget()
    {
        _cachedTarget = null;
        _cachedTargetHealth = null;
    }

    /// <summary>
    /// 주기 제어 기반으로 타겟 탐색/검증을 수행합니다.
    /// </summary>
    public void TickSearch(float nowTime, Vector2 origin, float detectionRange, float targetSearchInterval)
    {
        if (_cachedTarget != null && !IsTargetValid(_cachedTarget, _cachedTargetHealth))
        {
            Debug.LogWarning($"[EnemyTargetDetector] Cached target invalidated on {name}. Target cache cleared.");
            ClearTarget();
        }

        if (nowTime < _nextSearchAt)
        {
            return;
        }

        _nextSearchAt = nowTime + Mathf.Max(0.01f, targetSearchInterval);

        if (_cachedTarget != null)
        {
            return;
        }

        int hitCount = Physics2D.OverlapCircleNonAlloc(origin, detectionRange, _searchBuffer, _targetLayerMask);
        if (hitCount <= 0)
        {
            return;
        }

        float nearestSqr = float.MaxValue;
        Transform best = null;
        HealthComponent bestHealth = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidateCollider = _searchBuffer[i];
            if (candidateCollider == null)
            {
                continue;
            }

            Transform candidate = candidateCollider.transform;
            if (!string.IsNullOrWhiteSpace(_targetTag) && !candidate.CompareTag(_targetTag))
            {
                continue;
            }

            HealthComponent candidateHealth = candidate.GetComponent<HealthComponent>();
            if (!IsTargetValid(candidate, candidateHealth))
            {
                continue;
            }

            float sqr = (candidate.position - (Vector3)origin).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                best = candidate;
                bestHealth = candidateHealth;
            }
        }

        if (best == null)
        {
            return;
        }

        _cachedTarget = best;
        _cachedTargetHealth = bestHealth;
    }

    /// <summary>
    /// 현재 타겟 유효성을 반환합니다.
    /// </summary>
    public bool HasValidTarget()
    {
        return IsTargetValid(_cachedTarget, _cachedTargetHealth);
    }

    /// <summary>
    /// 타겟 유효성을 판정합니다.
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
}
