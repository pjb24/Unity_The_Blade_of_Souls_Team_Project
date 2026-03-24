using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 순찰 경로 포인트를 제공하고 순찰 모드(Loop/PingPong/RandomNoRepeat)에 따라 다음 지점을 계산하는 컴포넌트입니다.
/// </summary>
public class EnemyPatrolRouteProvider : MonoBehaviour
{
    /// <summary>
    /// 순찰 지점 진행 모드를 정의합니다.
    /// </summary>
    public enum E_PatrolRouteMode
    {
        Loop,
        PingPong,
        RandomNoRepeat,
    }

    /// <summary>
    /// 유효한 경로가 없을 때 적용할 fallback 정책을 정의합니다.
    /// </summary>
    public enum E_PatrolFallbackPolicy
    {
        SpawnRandom,
        IdleFixed,
    }

    [Header("Route")]
    [Tooltip("순찰 경로 포인트 배열입니다. 빈 GameObject를 자식으로 배치한 뒤 Collect 버튼으로 자동 수집할 수 있습니다.")]
    [SerializeField] private List<Transform> _routePoints = new List<Transform>(); // Inspector에서 설정하거나 자식 오브젝트 수집으로 채우는 순찰 포인트 목록입니다.
    [Tooltip("순찰 포인트 진행 방식입니다.")]
    [SerializeField] private E_PatrolRouteMode _routeMode = E_PatrolRouteMode.Loop; // 다음 순찰 포인트를 선택할 때 사용할 진행 모드입니다.
    [Tooltip("활성화하면 Awake 시점에 route의 월드 좌표를 스냅샷으로 고정해 Enemy 이동의 영향을 받지 않게 합니다.")]
    [SerializeField] private bool _useWorldPositionSnapshot = true; // 자식 포인트가 Enemy와 함께 이동해도 고정된 월드 좌표로 순찰하도록 스냅샷을 사용할지 여부입니다.

    [Header("Fallback")]
    [Tooltip("유효한 경로가 없을 때 적용할 fallback 정책입니다. SpawnRandom은 스폰 기준 랜덤, IdleFixed는 현재 위치를 유지합니다.")]
    [SerializeField] private E_PatrolFallbackPolicy _fallbackPolicy = E_PatrolFallbackPolicy.SpawnRandom; // 경로가 비어 있거나 null만 있을 때 사용할 대체 목적지 정책입니다.

    [Header("Designer Workflow")]
    [Tooltip("Awake 시점에 route가 비어 있으면 자식 Transform을 순찰 포인트로 자동 수집합니다.")]
    [SerializeField] private bool _collectChildrenOnAwakeWhenRouteEmpty = true; // 초기 route 미설정 상황에서 자식 포인트 자동 수집을 수행할지 여부입니다.

    private readonly List<Vector2> _cachedRouteWorldPoints = new List<Vector2>(); // 스냅샷 모드에서 사용할 순찰 포인트 월드 좌표 캐시입니다.

    private int _currentPointIndex = -1; // Loop/PingPong 모드에서 현재 선택된 포인트 인덱스입니다.
    private bool _pingPongForward = true; // PingPong 모드에서 진행 방향(정방향/역방향) 상태입니다.
    private int _lastRandomIndex = -1; // RandomNoRepeat 모드에서 직전 선택 인덱스를 기억해 연속 중복을 방지합니다.

    /// <summary>
    /// route가 비어 있을 때 디자이너가 배치한 자식 오브젝트를 순찰 포인트로 수집하고, 필요 시 월드 좌표 스냅샷을 생성합니다.
    /// </summary>
    private void Awake()
    {
        if (_collectChildrenOnAwakeWhenRouteEmpty && IsRouteListEmpty())
        {
            CollectRoutePointsFromChildren();
        }
        else
        {
            RefreshRoutePointSnapshots();
        }
    }

    /// <summary>
    /// 순찰 경로에 유효한 포인트가 1개 이상 존재하는지 검사합니다.
    /// </summary>
    public bool HasValidRoute()
    {
        List<Vector2> validPoints = BuildValidRoutePoints(); // 유효성 검사에 사용할 현재 기준 순찰 포인트 좌표 목록입니다.
        return validPoints.Count > 0;
    }

    /// <summary>
    /// 현재 설정/모드 기준으로 다음 순찰 목적지를 계산하여 반환합니다.
    /// 유효 경로가 없으면 fallback 정책(SpawnRandom/IdleFixed)을 적용합니다.
    /// </summary>
    public Vector2 GetNextPoint(Vector2 spawnPosition, float patrolRadius, Vector2 idleFixedPosition)
    {
        List<Vector2> validPoints = BuildValidRoutePoints(); // null 제거 및 스냅샷 규칙이 반영된 유효 순찰 포인트 좌표 목록입니다.
        if (validPoints.Count == 0)
        {
            return GetFallbackPoint(spawnPosition, patrolRadius, idleFixedPosition);
        }

        int nextIndex = GetNextIndex(validPoints.Count); // 현재 모드 기준으로 선택된 다음 포인트 인덱스입니다.
        return validPoints[nextIndex];
    }

    /// <summary>
    /// Inspector에서 수동 실행할 수 있도록 현재 오브젝트의 직계 자식 Transform을 route에 수집하고 스냅샷을 갱신합니다.
    /// </summary>
    [ContextMenu("Collect Route Points From Children")]
    public void CollectRoutePointsFromChildren()
    {
        _routePoints.Clear();

        int childCount = transform.childCount; // route 포인트 후보로 탐색할 직계 자식 개수입니다.
        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i); // route 목록에 추가 가능한 자식 Transform 후보입니다.
            if (child != null)
            {
                _routePoints.Add(child);
            }
        }

        RefreshRoutePointSnapshots();
    }

    /// <summary>
    /// 현재 route Transform들의 월드 좌표를 스냅샷 캐시에 반영합니다.
    /// </summary>
    [ContextMenu("Refresh Route Point Snapshots")]
    public void RefreshRoutePointSnapshots()
    {
        _cachedRouteWorldPoints.Clear();

        if (_routePoints == null)
        {
            return;
        }

        for (int i = 0; i < _routePoints.Count; i++)
        {
            Transform point = _routePoints[i]; // 스냅샷으로 변환할 원본 route 포인트입니다.
            if (point != null)
            {
                _cachedRouteWorldPoints.Add(point.position);
            }
        }
    }

    /// <summary>
    /// 경로 리스트가 완전히 비어 있는지 판정합니다.
    /// </summary>
    private bool IsRouteListEmpty()
    {
        return _routePoints == null || _routePoints.Count == 0;
    }

    /// <summary>
    /// 스냅샷 옵션에 따라 실제 순찰 계산에 사용할 좌표 목록을 반환합니다.
    /// </summary>
    private List<Vector2> BuildValidRoutePoints()
    {
        if (_useWorldPositionSnapshot)
        {
            return BuildValidSnapshotPoints();
        }

        return BuildValidLivePoints();
    }

    /// <summary>
    /// 스냅샷 캐시에서 유효 순찰 좌표 목록을 구성합니다.
    /// </summary>
    private List<Vector2> BuildValidSnapshotPoints()
    {
        List<Vector2> validPoints = new List<Vector2>(); // 스냅샷 모드 순찰 계산에 사용할 유효 좌표 목록입니다.
        if (_cachedRouteWorldPoints.Count == 0)
        {
            RefreshRoutePointSnapshots();
        }

        for (int i = 0; i < _cachedRouteWorldPoints.Count; i++)
        {
            validPoints.Add(_cachedRouteWorldPoints[i]);
        }

        return validPoints;
    }

    /// <summary>
    /// route Transform의 현재 월드 좌표를 읽어 유효 순찰 좌표 목록을 구성합니다.
    /// </summary>
    private List<Vector2> BuildValidLivePoints()
    {
        List<Vector2> validPoints = new List<Vector2>(); // 라이브 모드 순찰 계산에 사용할 유효 좌표 목록입니다.
        if (_routePoints == null)
        {
            return validPoints;
        }

        for (int i = 0; i < _routePoints.Count; i++)
        {
            Transform point = _routePoints[i]; // 라이브 월드 좌표를 읽어올 route 포인트입니다.
            if (point != null)
            {
                validPoints.Add(point.position);
            }
        }

        return validPoints;
    }

    /// <summary>
    /// 순찰 모드에 따라 다음 포인트 인덱스를 계산합니다.
    /// </summary>
    private int GetNextIndex(int pointCount)
    {
        switch (_routeMode)
        {
            case E_PatrolRouteMode.PingPong:
                return GetPingPongIndex(pointCount);
            case E_PatrolRouteMode.RandomNoRepeat:
                return GetRandomNoRepeatIndex(pointCount);
            case E_PatrolRouteMode.Loop:
            default:
                return GetLoopIndex(pointCount);
        }
    }

    /// <summary>
    /// Loop 모드 인덱스를 계산합니다.
    /// </summary>
    private int GetLoopIndex(int pointCount)
    {
        _currentPointIndex = (_currentPointIndex + 1 + pointCount) % pointCount;
        return _currentPointIndex;
    }

    /// <summary>
    /// PingPong 모드 인덱스를 계산합니다.
    /// </summary>
    private int GetPingPongIndex(int pointCount)
    {
        if (pointCount <= 1)
        {
            _currentPointIndex = 0;
            return _currentPointIndex;
        }

        if (_currentPointIndex < 0)
        {
            _currentPointIndex = 0;
            _pingPongForward = true;
            return _currentPointIndex;
        }

        if (_pingPongForward)
        {
            _currentPointIndex++;
            if (_currentPointIndex >= pointCount - 1)
            {
                _currentPointIndex = pointCount - 1;
                _pingPongForward = false;
            }
        }
        else
        {
            _currentPointIndex--;
            if (_currentPointIndex <= 0)
            {
                _currentPointIndex = 0;
                _pingPongForward = true;
            }
        }

        return _currentPointIndex;
    }

    /// <summary>
    /// RandomNoRepeat 모드 인덱스를 계산합니다.
    /// </summary>
    private int GetRandomNoRepeatIndex(int pointCount)
    {
        if (pointCount <= 1)
        {
            _lastRandomIndex = 0;
            _currentPointIndex = 0;
            return 0;
        }

        int randomIndex = Random.Range(0, pointCount); // 이번 프레임 랜덤으로 추첨한 포인트 인덱스입니다.
        if (randomIndex == _lastRandomIndex)
        {
            randomIndex = (randomIndex + 1) % pointCount;
        }

        _lastRandomIndex = randomIndex;
        _currentPointIndex = randomIndex;
        return randomIndex;
    }

    /// <summary>
    /// 유효 경로가 없을 때 fallback 정책에 맞는 목적지 좌표를 계산합니다.
    /// </summary>
    private Vector2 GetFallbackPoint(Vector2 spawnPosition, float patrolRadius, Vector2 idleFixedPosition)
    {
        switch (_fallbackPolicy)
        {
            case E_PatrolFallbackPolicy.IdleFixed:
                return idleFixedPosition;
            case E_PatrolFallbackPolicy.SpawnRandom:
            default:
                float randomOffsetX = Random.Range(-patrolRadius, patrolRadius); // 스폰 기준 랜덤 fallback 목적지 계산에 사용할 X축 오프셋입니다.
                return spawnPosition + new Vector2(randomOffsetX, 0f);
        }
    }
}
