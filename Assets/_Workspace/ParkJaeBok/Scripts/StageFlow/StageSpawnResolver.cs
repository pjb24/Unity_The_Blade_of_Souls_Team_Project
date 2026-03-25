using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 씬 로드 후 플레이어를 StageEntryPoint 기준 위치로 이동시키는 리졸버입니다.
/// </summary>
public class StageSpawnResolver : MonoBehaviour
{
    [Header("Player Resolve")]
    [Tooltip("가장 우선으로 사용할 플레이어 Transform 직접 참조입니다. 비어 있으면 자동 탐색을 시도합니다.")]
    [SerializeField] private Transform _explicitPlayerTransform; // 자동 탐색보다 먼저 사용할 플레이어 Transform 직접 참조입니다.

    [Tooltip("StagePlayerSpawnTarget 마커 기반 플레이어 탐색을 사용할지 여부입니다.")]
    [SerializeField] private bool _useSpawnTargetMarker = true; // 명시적 플레이어 식별을 위한 마커 탐색 사용 여부입니다.

    [Tooltip("Player 태그 fallback 탐색을 허용할지 여부입니다.")]
    [SerializeField] private bool _allowTagFallback = true; // 마커 탐색 실패 시 태그 기반으로 폴백할지 여부입니다.

    [Tooltip("태그 fallback 탐색에 사용할 태그 이름입니다.")]
    [SerializeField] private string _playerTag = "Player"; // 태그 기반 폴백 탐색 시 사용할 플레이어 태그입니다.

    [Header("Resolve Retry")]
    [Tooltip("로드 직후 플레이어 생성 지연을 고려해 스폰 해석을 재시도할 최대 횟수입니다.")]
    [SerializeField] private int _maxResolveRetryCount = 10; // 플레이어/엔트리포인트 탐색 실패 시 재시도할 횟수입니다.

    [Tooltip("재시도 간 대기 시간(초)입니다.")]
    [SerializeField] private float _resolveRetryInterval = 0.1f; // 스폰 해석 재시도 간 대기 시간입니다.

    private SceneTransitionService _sceneTransitionService; // 이벤트 구독/해제에 사용할 전환 서비스 참조입니다.

    /// <summary>
    /// 활성화 시 씬 로드 이벤트를 구독하고 즉시 1회 스폰 해석을 시도합니다.
    /// </summary>
    private void OnEnable()
    {
        _sceneTransitionService = SceneTransitionService.Instance;
        if (_sceneTransitionService != null)
        {
            _sceneTransitionService.OnAfterSceneLoad += HandleAfterSceneLoad;
        }

        StartCoroutine(ResolveSpawnRoutine());
    }

    /// <summary>
    /// 비활성화 시 씬 로드 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_sceneTransitionService != null)
        {
            _sceneTransitionService.OnAfterSceneLoad -= HandleAfterSceneLoad;
        }
    }

    /// <summary>
    /// 씬 로드 완료 이벤트를 받아 스폰 해석을 재시작합니다.
    /// </summary>
    private void HandleAfterSceneLoad(string _)
    {
        StartCoroutine(ResolveSpawnRoutine());
    }

    /// <summary>
    /// StageSession의 엔트리 포인트 ID를 기준으로 플레이어 배치 위치를 해석합니다.
    /// </summary>
    private IEnumerator ResolveSpawnRoutine()
    {
        int safeRetryCount = Mathf.Max(1, _maxResolveRetryCount); // 잘못된 재시도 횟수를 보정한 안전 값입니다.
        float safeInterval = Mathf.Max(0.01f, _resolveRetryInterval); // 잘못된 재시도 간격을 보정한 안전 값입니다.

        for (int retryIndex = 0; retryIndex < safeRetryCount; retryIndex++)
        {
            if (TryResolveSpawnAndMovePlayer())
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(safeInterval);
        }

        Debug.LogWarning("[StageSpawnResolver] 플레이어 스폰 위치 해석에 실패했습니다.", this);
    }

    /// <summary>
    /// 플레이어와 엔트리 포인트를 찾아 플레이어 위치를 이동시킵니다.
    /// </summary>
    private bool TryResolveSpawnAndMovePlayer()
    {
        if (TryResolvePlayerTransform(out Transform playerTransform) == false)
        {
            return false;
        }

        string requestedEntryId = StageSession.Instance.TargetStageEntryPointId; // StageSession이 요청한 진입 포인트 ID입니다.

        StageEntryPoint[] points = FindObjectsByType<StageEntryPoint>(FindObjectsSortMode.None); // 씬에서 사용 가능한 모든 엔트리 포인트 목록입니다.
        if (points == null || points.Length == 0)
        {
            return false;
        }

        StageEntryPoint selectedPoint = FindBestPoint(points, requestedEntryId); // 요청 ID 기준으로 선택된 배치 포인트입니다.
        if (selectedPoint == null)
        {
            return false;
        }

        playerTransform.position = selectedPoint.transform.position;
        StageSession.Instance.ConsumeEntryPoint();
        return true;
    }

    /// <summary>
    /// 명시 참조 > 마커 > 태그 폴백 순서로 플레이어 Transform을 탐색합니다.
    /// </summary>
    private bool TryResolvePlayerTransform(out Transform playerTransform)
    {
        playerTransform = null;

        if (_explicitPlayerTransform != null)
        {
            playerTransform = _explicitPlayerTransform;
            return true;
        }

        if (_useSpawnTargetMarker)
        {
            if (TryResolveBySpawnTargetMarker(out playerTransform))
            {
                return true;
            }
        }

        if (_allowTagFallback)
        {
            return TryResolveByTagFallback(out playerTransform);
        }

        return false;
    }

    /// <summary>
    /// StagePlayerSpawnTarget 마커 후보 중 우선순위 규칙으로 최적 대상을 찾습니다.
    /// </summary>
    private bool TryResolveBySpawnTargetMarker(out Transform playerTransform)
    {
        playerTransform = null;

        StagePlayerSpawnTarget[] targets = FindObjectsByType<StagePlayerSpawnTarget>(FindObjectsSortMode.None); // 씬에서 탐색한 마커 후보 목록입니다.
        if (targets == null || targets.Length == 0)
        {
            return false;
        }

        StagePlayerSpawnTarget bestTarget = null; // 정렬 규칙에 따라 선택된 최종 마커 대상입니다.

        for (int i = 0; i < targets.Length; i++)
        {
            StagePlayerSpawnTarget candidate = targets[i]; // 현재 비교 중인 마커 후보입니다.
            if (candidate == null || candidate.gameObject.activeInHierarchy == false)
            {
                continue;
            }

            if (bestTarget == null)
            {
                bestTarget = candidate;
                continue;
            }

            bool candidateIsBetter = IsBetterTarget(candidate, bestTarget);
            if (candidateIsBetter)
            {
                bestTarget = candidate;
            }
        }

        if (bestTarget == null)
        {
            return false;
        }

        playerTransform = bestTarget.GetSpawnTransform();
        return playerTransform != null;
    }

    /// <summary>
    /// Player 태그 후보 중 입력 컴포넌트 보유 여부를 포함한 우선순위 규칙으로 대상을 찾습니다.
    /// </summary>
    private bool TryResolveByTagFallback(out Transform playerTransform)
    {
        playerTransform = null;

        if (string.IsNullOrWhiteSpace(_playerTag))
        {
            return false;
        }

        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(_playerTag); // Player 태그 기반으로 찾은 후보 목록입니다.
        if (taggedObjects == null || taggedObjects.Length == 0)
        {
            return false;
        }

        GameObject bestObject = null; // 태그 fallback 규칙으로 선택된 최종 플레이어 오브젝트입니다.

        for (int i = 0; i < taggedObjects.Length; i++)
        {
            GameObject candidate = taggedObjects[i]; // 현재 비교 중인 태그 후보 오브젝트입니다.
            if (candidate == null || candidate.activeInHierarchy == false)
            {
                continue;
            }

            if (bestObject == null)
            {
                bestObject = candidate;
                continue;
            }

            if (IsBetterTaggedObject(candidate, bestObject))
            {
                bestObject = candidate;
            }
        }

        if (bestObject == null)
        {
            return false;
        }

        if (taggedObjects.Length > 1)
        {
            Debug.LogWarning($"[StageSpawnResolver] Player 태그 후보가 {taggedObjects.Length}개여서 우선순위 규칙으로 대상을 선택했습니다. 명시적으로 StagePlayerSpawnTarget 사용을 권장합니다.", this);
        }

        playerTransform = bestObject.transform;
        return playerTransform != null;
    }

    /// <summary>
    /// 두 마커 후보 중 어떤 대상이 더 우선인지 판단합니다.
    /// </summary>
    private bool IsBetterTarget(StagePlayerSpawnTarget candidate, StagePlayerSpawnTarget current)
    {
        if (candidate.IsPrimary != current.IsPrimary)
        {
            return candidate.IsPrimary;
        }

        if (candidate.Priority != current.Priority)
        {
            return candidate.Priority > current.Priority;
        }

        return candidate.transform.GetSiblingIndex() < current.transform.GetSiblingIndex();
    }

    /// <summary>
    /// 태그 기반 후보 중 입력 관련 컴포넌트 보유 여부를 우선해 더 적합한 대상을 판단합니다.
    /// </summary>
    private bool IsBetterTaggedObject(GameObject candidate, GameObject current)
    {
        bool candidateHasInputManager = candidate.GetComponent<InputManager>() != null;
        bool currentHasInputManager = current.GetComponent<InputManager>() != null;
        if (candidateHasInputManager != currentHasInputManager)
        {
            return candidateHasInputManager;
        }

        bool candidateHasPlayerInput = candidate.GetComponent<PlayerInput>() != null;
        bool currentHasPlayerInput = current.GetComponent<PlayerInput>() != null;
        if (candidateHasPlayerInput != currentHasPlayerInput)
        {
            return candidateHasPlayerInput;
        }

        return candidate.transform.GetSiblingIndex() < current.transform.GetSiblingIndex();
    }

    /// <summary>
    /// 요청 ID와 fallback 규칙을 사용해 최적 엔트리 포인트를 선택합니다.
    /// </summary>
    private StageEntryPoint FindBestPoint(StageEntryPoint[] points, string requestedEntryId)
    {
        StageEntryPoint fallbackPoint = null; // 직접 ID 매칭 실패 시 사용할 fallback 포인트입니다.

        for (int i = 0; i < points.Length; i++)
        {
            StageEntryPoint point = points[i]; // 현재 검사 중인 엔트리 포인트입니다.
            if (point == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(requestedEntryId) == false && point.EntryPointId == requestedEntryId)
            {
                return point;
            }

            if (fallbackPoint == null && point.IsFallbackPoint)
            {
                fallbackPoint = point;
            }
        }

        return fallbackPoint;
    }
}
