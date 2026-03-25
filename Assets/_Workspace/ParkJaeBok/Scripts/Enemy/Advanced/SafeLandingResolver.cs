using UnityEngine;

/// <summary>
/// 다중 샘플 하향 프로브 기반으로 안전한 착지 후보를 탐색하고 최적 지점을 선택하는 모듈입니다.
/// </summary>
public class SafeLandingResolver : MonoBehaviour
{
    public int LastCandidateCount { get; private set; } // 마지막 탐색에서 유효하다고 판정된 착지 후보 개수입니다.
    public int LastSelectedCandidateIndex { get; private set; } = -1; // 마지막 탐색에서 선택된 착지 후보 인덱스입니다.
    public string LastFailureReason { get; private set; } = "NotEvaluated"; // 마지막 탐색 실패 사유 문자열입니다.

    [Header("Sampling")]
    [Tooltip("현재 위치 기준 샘플 탐색 반경입니다.")]
    [SerializeField] private float _sampleRadius = 3f; // 현재 위치 기준 샘플 탐색 반경입니다.
    [Tooltip("현재 위치 주변에 생성할 샘플 포인트 개수입니다.")]
    [SerializeField] private int _sampleCount = 9; // 현재 위치 주변에 생성할 샘플 포인트 개수입니다.
    [Tooltip("각 샘플 포인트에서 하향 프로브 시작 시 추가할 높이입니다.")]
    [SerializeField] private float _probeStartHeight = 1.5f; // 각 샘플 포인트에서 하향 프로브 시작 시 추가할 높이입니다.
    [Tooltip("각 샘플 포인트에서 하향 프로브로 검사할 최대 거리입니다.")]
    [SerializeField] private float _probeDistance = 6f; // 각 샘플 포인트에서 하향 프로브로 검사할 최대 거리입니다.

    [Header("Validation")]
    [Tooltip("착지 유효로 허용할 최대 경사 각도(도)입니다.")]
    [SerializeField] private float _maxSlopeAngle = 45f; // 착지 유효로 허용할 최대 경사 각도(도)입니다.
    [Tooltip("착지 지점 좌우에 요구되는 최소 발판 반폭입니다.")]
    [SerializeField] private float _minimumPlatformHalfWidth = 0.35f; // 착지 지점 좌우에 요구되는 최소 발판 반폭입니다.
    [Tooltip("착지 지점 상단에 요구되는 최소 여유 높이입니다.")]
    [SerializeField] private float _minimumHeadClearance = 0.35f; // 착지 지점 상단에 요구되는 최소 여유 높이입니다.
    [Tooltip("지면 판정에 사용할 Physics2D 레이어 마스크입니다.")]
    [SerializeField] private LayerMask _groundMask = Physics2D.DefaultRaycastLayers; // 지면 판정에 사용할 Physics2D 레이어 마스크입니다.
    [Tooltip("경로 장애물 판정에 사용할 Physics2D 레이어 마스크입니다.")]
    [SerializeField] private LayerMask _obstacleMask = Physics2D.DefaultRaycastLayers; // 경로 장애물 판정에 사용할 Physics2D 레이어 마스크입니다.

    [Header("Scoring")]
    [Tooltip("목표 방향 정렬 점수 가중치입니다.")]
    [SerializeField] private float _alignmentWeight = 1.25f; // 목표 방향 정렬 점수 가중치입니다.
    [Tooltip("현재 위치 대비 이동 비용 점수 가중치입니다.")]
    [SerializeField] private float _moveCostWeight = 0.35f; // 현재 위치 대비 이동 비용 점수 가중치입니다.

    /// <summary>
    /// 아키타입 로코모션 설정값을 착지 탐색 파라미터에 반영합니다.
    /// </summary>
    public void ApplyArchetype(EnemyArchetypeData archetype)
    {
        if (archetype == null)
        {
            return;
        }

        _maxSlopeAngle = archetype.SlopeLimit;
        _probeDistance = Mathf.Max(_probeDistance, archetype.GroundProbeDistance + 1f);
    }

    /// <summary>
    /// 현재 위치 주변 다중 샘플을 검사해 가장 안전한 착지 후보를 반환합니다.
    /// </summary>
    public bool TryResolveLanding(Vector2 currentPosition, Vector2 targetPosition, out Vector2 landingPoint)
    {
        landingPoint = currentPosition;
        LastCandidateCount = 0;
        LastSelectedCandidateIndex = -1;
        LastFailureReason = "NoValidCandidate";

        int sampleCount = Mathf.Max(3, _sampleCount); // 샘플 포인트 생성에 사용할 안전 샘플 개수입니다.
        float radius = Mathf.Max(0.1f, _sampleRadius); // 샘플 포인트 생성에 사용할 탐색 반경입니다.
        float bestScore = float.NegativeInfinity; // 후보 비교에 사용할 현재 최고 점수 값입니다.
        bool found = false; // 유효 후보를 찾았는지 여부입니다.

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0.5f : i / (sampleCount - 1f); // 좌/우 샘플 생성에 사용할 정규화 비율 값입니다.
            float offsetX = Mathf.Lerp(-radius, radius, t); // 현재 샘플의 X축 오프셋 값입니다.
            Vector2 sampleOrigin = currentPosition + new Vector2(offsetX, _probeStartHeight); // 하향 프로브를 시작할 샘플 시작 좌표입니다.

            if (!TryProbeGround(sampleOrigin, out RaycastHit2D groundHit))
            {
                continue;
            }

            if (!IsValidSlope(groundHit.normal))
            {
                continue;
            }

            if (!HasMinimumPlatformWidth(groundHit.point))
            {
                continue;
            }

            if (!HasHeadClearance(groundHit.point))
            {
                continue;
            }

            if (HasObstacleOnPath(currentPosition, groundHit.point))
            {
                continue;
            }

            LastCandidateCount++;
            float score = ScoreCandidate(currentPosition, targetPosition, groundHit.point); // 현재 후보에 대한 종합 점수 값입니다.
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            landingPoint = groundHit.point;
            LastSelectedCandidateIndex = i;
            found = true;
        }

        if (found)
        {
            LastFailureReason = string.Empty;
        }

        return found;
    }

    /// <summary>
    /// 샘플 포인트에서 하향 레이캐스트를 수행해 지면 hit를 반환합니다.
    /// </summary>
    private bool TryProbeGround(Vector2 sampleOrigin, out RaycastHit2D groundHit)
    {
        groundHit = Physics2D.Raycast(sampleOrigin, Vector2.down, Mathf.Max(0.1f, _probeDistance), _groundMask);
        return groundHit.collider;
    }

    /// <summary>
    /// 지면 법선이 허용 경사 범위 이내인지 판정합니다.
    /// </summary>
    private bool IsValidSlope(Vector2 normal)
    {
        float slopeAngle = Vector2.Angle(normal, Vector2.up); // 경사 제한 비교에 사용할 현재 법선 각도 값입니다.
        return slopeAngle <= Mathf.Max(0f, _maxSlopeAngle);
    }

    /// <summary>
    /// 착지 지점 좌우 발판 폭이 최소 조건을 만족하는지 판정합니다.
    /// </summary>
    private bool HasMinimumPlatformWidth(Vector2 landingPoint)
    {
        float halfWidth = Mathf.Max(0.05f, _minimumPlatformHalfWidth); // 발판 폭 검증에 사용할 반폭 값입니다.
        Vector2 leftProbe = landingPoint + Vector2.left * halfWidth + Vector2.up * 0.2f; // 좌측 발판 폭 검증 프로브 시작 좌표입니다.
        Vector2 rightProbe = landingPoint + Vector2.right * halfWidth + Vector2.up * 0.2f; // 우측 발판 폭 검증 프로브 시작 좌표입니다.

        RaycastHit2D leftHit = Physics2D.Raycast(leftProbe, Vector2.down, 0.6f, _groundMask); // 좌측 발판 폭 검증 결과입니다.
        RaycastHit2D rightHit = Physics2D.Raycast(rightProbe, Vector2.down, 0.6f, _groundMask); // 우측 발판 폭 검증 결과입니다.
        return leftHit.collider && rightHit.collider;
    }

    /// <summary>
    /// 착지 지점 상단 여유 공간이 최소 조건을 만족하는지 판정합니다.
    /// </summary>
    private bool HasHeadClearance(Vector2 landingPoint)
    {
        float clearance = Mathf.Max(0.05f, _minimumHeadClearance); // 상단 여유 검증에 사용할 높이 값입니다.
        Vector2 from = landingPoint + Vector2.up * 0.05f; // 상단 여유 검증 레이 시작 좌표입니다.
        RaycastHit2D obstacle = Physics2D.Raycast(from, Vector2.up, clearance, _obstacleMask); // 상단 여유 검증 장애물 판정 결과입니다.
        return !obstacle.collider;
    }

    /// <summary>
    /// 현재 위치에서 착지 후보로 가는 경로가 장애물 관통인지 판정합니다.
    /// </summary>
    private bool HasObstacleOnPath(Vector2 currentPosition, Vector2 landingPoint)
    {
        RaycastHit2D pathHit = Physics2D.Linecast(currentPosition, landingPoint, _obstacleMask); // 경로 장애물 관통 판정 결과입니다.
        if (!pathHit.collider)
        {
            return false;
        }

        return pathHit.collider.transform != transform;
    }

    /// <summary>
    /// 목표 방향 정렬도와 이동 비용을 조합해 후보 점수를 계산합니다.
    /// </summary>
    private float ScoreCandidate(Vector2 currentPosition, Vector2 targetPosition, Vector2 candidatePoint)
    {
        Vector2 toTarget = (targetPosition - currentPosition).normalized; // 목표 방향 정렬 계산에 사용할 기준 방향 벡터입니다.
        Vector2 toCandidate = (candidatePoint - currentPosition).normalized; // 현재 후보 방향 정렬 계산에 사용할 방향 벡터입니다.
        float alignment = Vector2.Dot(toTarget, toCandidate); // 목표 방향 대비 후보 정렬 점수 값입니다.
        float moveCost = Vector2.Distance(currentPosition, candidatePoint); // 현재 위치 대비 후보 이동 비용 값입니다.

        return alignment * _alignmentWeight - moveCost * _moveCostWeight;
    }
}
