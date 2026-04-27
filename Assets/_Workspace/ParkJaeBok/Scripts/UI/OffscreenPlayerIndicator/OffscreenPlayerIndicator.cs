using UnityEngine;

/// <summary>
/// 로컬 플레이어 캐릭터를 기준점으로 다른 플레이어가 화면 밖에 있을 때 Canvas 가장자리에 방향 Indicator를 표시합니다.
/// </summary>
[DisallowMultipleComponent]
public class OffscreenPlayerIndicator : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("표시할 Indicator 프리팹입니다. OffscreenPlayerIndicatorPresenter가 없으면 런타임에 추가합니다.")]
    [SerializeField] private GameObject _indicatorPrefab; // 화면 가장자리에 생성할 Indicator UI 프리팹 참조입니다.

    [Tooltip("Indicator가 붙을 Canvas 또는 UI Root RectTransform입니다. 비어 있으면 부모 Canvas를 폴백으로 사용합니다.")]
    [SerializeField] private RectTransform _indicatorRoot; // Indicator 인스턴스가 배치될 UI 부모 RectTransform 참조입니다.

    [Tooltip("로컬 대상 플레이어를 찾는 Resolver입니다. 비어 있으면 같은 오브젝트에서 자동 탐색하거나 추가합니다.")]
    [SerializeField] private OffscreenPlayerTargetResolver _targetResolver; // 표시 대상 플레이어 Transform을 제공하는 Resolver 참조입니다.

    [Tooltip("화면 판정과 방향 계산에 사용할 로컬 Camera입니다. 비어 있으면 Camera.main을 폴백으로 사용합니다.")]
    [SerializeField] private Camera _targetCamera; // 로컬 화면 기준 계산에 사용할 Camera 참조입니다.

    [Header("Indicator Layout")]
    [Tooltip("화면 가장자리에서 Indicator를 안쪽으로 밀어 넣을 Padding(px)입니다.")]
    [SerializeField] private float _edgePadding = 64f; // UI가 화면 끝에 붙지 않도록 적용할 가장자리 여백입니다.

    [Tooltip("Indicator Sprite가 대상 방향을 바라보도록 회전할지 여부입니다.")]
    [SerializeField] private bool _useIndicatorRotation = true; // Indicator 회전 적용 여부입니다.

    [Tooltip("대상이 화면 안에 있을 때 Indicator를 숨길지 여부입니다.")]
    [SerializeField] private bool _hideWhenTargetInsideViewport = true; // Camera viewport 안쪽 대상에 대해 Indicator를 숨길지 여부입니다.

    [Tooltip("로컬 플레이어 캐릭터 위치에 더할 기준점 월드 오프셋입니다. X/Y 방향으로 Indicator 추적 시작점을 보정합니다.")]
    [SerializeField] private Vector2 _localPlayerReferenceOffset; // 로컬 플레이어 캐릭터 위치에서 기준점으로 사용할 월드 X/Y 오프셋입니다.

    [Tooltip("대상 플레이어 캐릭터 위치에 더할 추적점 월드 오프셋입니다. X/Y 방향으로 Indicator가 따라갈 대상 위치를 보정합니다.")]
    [SerializeField] private Vector2 _targetPlayerReferenceOffset; // 대상 플레이어 캐릭터 위치에서 추적점으로 사용할 월드 X/Y 오프셋입니다.

    [Header("Distance Option")]
    [Tooltip("화면 안/밖 판정 외에 거리 제한을 추가로 사용할지 여부입니다.")]
    [SerializeField] private bool _useDistanceLimit; // 대상 거리 조건을 추가로 적용할지 여부입니다.

    [Tooltip("Indicator를 표시하기 위한 최소 거리입니다. Use Distance Limit이 켜진 경우에만 사용합니다.")]
    [SerializeField] private float _minVisibleDistance; // 너무 가까운 대상의 Indicator 표시를 막기 위한 최소 거리입니다.

    [Tooltip("Indicator를 표시하기 위한 최대 거리입니다. 0 이하면 최대 거리 제한을 사용하지 않습니다.")]
    [SerializeField] private float _maxVisibleDistance; // 너무 먼 대상의 Indicator 표시를 막기 위한 최대 거리입니다.

    [Header("Target Presentation")]
    [Tooltip("대상 표시 이름입니다. 비어 있으면 Presenter 기본 이름을 사용합니다.")]
    [SerializeField] private string _displayName; // Indicator Presenter에 전달할 대상 표시 이름입니다.

    [Tooltip("대상 표시 아이콘 Sprite입니다. 비어 있으면 Presenter 기본 아이콘을 사용합니다.")]
    [SerializeField] private Sprite _iconSprite; // Indicator Presenter에 전달할 대상 아이콘 Sprite입니다.

    [Header("Debug")]
    [Tooltip("현재 생성되어 사용 중인 Indicator Presenter입니다.")]
    [SerializeField] private OffscreenPlayerIndicatorPresenter _presenter; // 런타임에 생성하거나 연결된 Indicator Presenter 참조입니다.

    [Tooltip("현재 화면 판정에 사용 중인 대상 Transform입니다.")]
    [SerializeField] private Transform _currentTarget; // Resolver에서 받은 현재 원격 플레이어 Transform 참조입니다.

    [Tooltip("현재 Indicator 방향 기준점으로 사용하는 로컬 플레이어 Transform입니다.")]
    [SerializeField] private Transform _currentLocalPlayer; // 화면 가장자리 위치 계산의 시작점으로 사용할 로컬 플레이어 Transform 참조입니다.

    [Tooltip("현재 오프셋까지 반영된 Indicator 기준점 월드 위치입니다.")]
    [SerializeField] private Vector3 _currentReferencePosition; // 로컬 플레이어 위치에 오프셋을 더한 최종 기준점 월드 좌표입니다.

    [Tooltip("현재 오프셋까지 반영된 대상 플레이어 추적점 월드 위치입니다.")]
    [SerializeField] private Vector3 _currentTargetReferencePosition; // 대상 플레이어 위치에 오프셋을 더한 최종 추적점 월드 좌표입니다.

    private bool _hasLoggedMissingPrefabWarning; // Indicator Prefab 누락 Warning 중복 출력을 방지하는 플래그입니다.
    private bool _hasLoggedMissingRootWarning; // Indicator Root 누락 Warning 중복 출력을 방지하는 플래그입니다.
    private bool _hasLoggedCameraFallbackWarning; // Camera.main 폴백 Warning 중복 출력을 방지하는 플래그입니다.
    private bool _hasLoggedMissingCameraWarning; // Camera 누락 Warning 중복 출력을 방지하는 플래그입니다.
    private bool _hasLoggedResolverFallbackWarning; // Resolver 자동 추가 Warning 중복 출력을 방지하는 플래그입니다.
    private bool _hasLoggedMissingLocalPlayerWarning; // 로컬 플레이어 기준점 누락 Warning 중복 출력을 방지하는 플래그입니다.

    /// <summary>
    /// Inspector 값의 안전 범위를 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        _edgePadding = Mathf.Max(0f, _edgePadding);
        _minVisibleDistance = Mathf.Max(0f, _minVisibleDistance);
        _maxVisibleDistance = Mathf.Max(0f, _maxVisibleDistance);
    }

    /// <summary>
    /// 활성화 시 Resolver를 준비하고 대상 변경 알림을 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveTargetResolver();

        if (_targetResolver != null)
        {
            _targetResolver.AddListener(HandleTargetChanged);
        }
    }

    /// <summary>
    /// 비활성화 시 구독을 해제하고 Indicator를 숨깁니다.
    /// </summary>
    private void OnDisable()
    {
        if (_targetResolver != null)
        {
            _targetResolver.RemoveListener(HandleTargetChanged);
        }

        HideIndicator();
    }

    /// <summary>
    /// 카메라 이동 이후 대상의 화면 안/밖 상태를 계산하고 Indicator UI를 갱신합니다.
    /// </summary>
    private void LateUpdate()
    {
        if (!EnsureRuntimeReady())
        {
            HideIndicator();
            return;
        }

        if (!_targetResolver.TryGetTargetTransform(out Transform targetTransform) || targetTransform == null)
        {
            _currentTarget = null;
            HideIndicator();
            return;
        }

        _currentTarget = targetTransform;

        if (!_targetResolver.TryGetLocalPlayerTransform(out Transform localPlayerTransform) || localPlayerTransform == null)
        {
            _currentLocalPlayer = null;
            if (!_hasLoggedMissingLocalPlayerWarning)
            {
                Debug.LogWarning($"[OffscreenPlayerIndicator] 로컬 플레이어 기준 Transform을 찾지 못해 Indicator를 숨깁니다. object={name}", this);
                _hasLoggedMissingLocalPlayerWarning = true;
            }

            HideIndicator();
            return;
        }

        _currentLocalPlayer = localPlayerTransform;
        _hasLoggedMissingLocalPlayerWarning = false;

        _currentReferencePosition = ResolveLocalPlayerReferencePosition(localPlayerTransform); // 로컬 플레이어 위치에 오프셋을 반영한 최종 기준점입니다.
        _currentTargetReferencePosition = ResolveTargetPlayerReferencePosition(targetTransform); // 대상 플레이어 위치에 오프셋을 반영한 최종 추적점입니다.

        Vector3 targetViewportPoint = _targetCamera.WorldToViewportPoint(_currentTargetReferencePosition); // 로컬 카메라로 투영한 대상 추적점 Viewport 좌표입니다.
        Vector3 referenceViewportPoint = _targetCamera.WorldToViewportPoint(_currentReferencePosition); // 오프셋이 반영된 기준점의 Viewport 좌표입니다.
        if (_hideWhenTargetInsideViewport && IsInsideViewport(targetViewportPoint))
        {
            HideIndicator();
            return;
        }

        if (!PassesDistanceOption(_currentReferencePosition, _currentTargetReferencePosition))
        {
            HideIndicator();
            return;
        }

        Vector2 direction = ResolvePlayerRelativeDirection(_currentReferencePosition, _currentTargetReferencePosition); // 오프셋 기준점에서 대상 추적점을 향하는 화면 평면 방향입니다.
        Vector2 anchoredPosition = CalculateEdgeAnchoredPosition(referenceViewportPoint, direction); // 오프셋 기준점의 화면 위치에서 대상 방향으로 만나는 가장자리 좌표입니다.

        _presenter.SetTargetPresentation(_displayName, _iconSprite);
        _presenter.Show(anchoredPosition, direction, _useIndicatorRotation);
    }

    /// <summary>
    /// Resolver 대상 변경 알림을 받아 현재 대상 캐시를 갱신합니다.
    /// </summary>
    private void HandleTargetChanged(Transform targetTransform)
    {
        _currentTarget = targetTransform;

        if (_currentTarget == null)
        {
            HideIndicator();
        }
    }

    /// <summary>
    /// 런타임에 필요한 Camera, Root, Presenter를 준비합니다.
    /// </summary>
    private bool EnsureRuntimeReady()
    {
        bool hasCamera = ResolveCamera();
        bool hasRoot = ResolveIndicatorRoot();
        bool hasPresenter = EnsurePresenter();
        return hasCamera && hasRoot && hasPresenter;
    }

    /// <summary>
    /// TargetResolver 참조를 보정하고 없으면 같은 오브젝트에 추가합니다.
    /// </summary>
    private void ResolveTargetResolver()
    {
        if (_targetResolver != null)
        {
            return;
        }

        _targetResolver = GetComponent<OffscreenPlayerTargetResolver>();
        if (_targetResolver != null)
        {
            return;
        }

        _targetResolver = gameObject.AddComponent<OffscreenPlayerTargetResolver>();
        if (!_hasLoggedResolverFallbackWarning)
        {
            Debug.LogWarning($"[OffscreenPlayerIndicator] OffscreenPlayerTargetResolver가 없어 런타임에 추가했습니다. object={name}", this);
            _hasLoggedResolverFallbackWarning = true;
        }
    }

    /// <summary>
    /// Indicator Root 참조를 확인하고 부모 Canvas를 폴백으로 해석합니다.
    /// </summary>
    private bool ResolveIndicatorRoot()
    {
        if (_indicatorRoot != null)
        {
            return true;
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            _indicatorRoot = parentCanvas.transform as RectTransform;
            if (_indicatorRoot != null)
            {
                Debug.LogWarning($"[OffscreenPlayerIndicator] Indicator Root가 비어 있어 부모 Canvas RectTransform을 사용합니다. object={name}", this);
                return true;
            }
        }

        if (!_hasLoggedMissingRootWarning)
        {
            Debug.LogWarning($"[OffscreenPlayerIndicator] Indicator Root가 없어 기능을 대기합니다. object={name}", this);
            _hasLoggedMissingRootWarning = true;
        }

        return false;
    }

    /// <summary>
    /// 대상 Camera 참조를 확인하고 Camera.main을 폴백으로 사용합니다.
    /// </summary>
    private bool ResolveCamera()
    {
        if (_targetCamera != null)
        {
            return true;
        }

        _targetCamera = Camera.main;
        if (_targetCamera != null)
        {
            if (!_hasLoggedCameraFallbackWarning)
            {
                Debug.LogWarning($"[OffscreenPlayerIndicator] Target Camera가 비어 있어 Camera.main을 사용합니다. object={name}, camera={_targetCamera.name}", this);
                _hasLoggedCameraFallbackWarning = true;
            }

            return true;
        }

        if (!_hasLoggedMissingCameraWarning)
        {
            Debug.LogWarning($"[OffscreenPlayerIndicator] Target Camera와 Camera.main을 찾지 못해 Indicator 계산을 건너뜁니다. object={name}", this);
            _hasLoggedMissingCameraWarning = true;
        }

        return false;
    }

    /// <summary>
    /// Indicator Presenter 인스턴스를 생성하거나 기존 참조를 검증합니다.
    /// </summary>
    private bool EnsurePresenter()
    {
        if (_presenter != null)
        {
            return true;
        }

        if (_indicatorPrefab == null)
        {
            if (!_hasLoggedMissingPrefabWarning)
            {
                Debug.LogWarning($"[OffscreenPlayerIndicator] Indicator Prefab이 없어 기능을 중단합니다. object={name}", this);
                _hasLoggedMissingPrefabWarning = true;
            }

            return false;
        }

        GameObject indicatorInstance = Instantiate(_indicatorPrefab, _indicatorRoot); // Canvas Root 아래에 생성한 Indicator UI 인스턴스입니다.
        indicatorInstance.name = $"{_indicatorPrefab.name}_Runtime";

        _presenter = indicatorInstance.GetComponent<OffscreenPlayerIndicatorPresenter>();
        if (_presenter == null)
        {
            _presenter = indicatorInstance.AddComponent<OffscreenPlayerIndicatorPresenter>();
            Debug.LogWarning($"[OffscreenPlayerIndicator] Indicator Prefab에 Presenter가 없어 런타임에 추가했습니다. prefab={_indicatorPrefab.name}", indicatorInstance);
        }

        RectTransform presenterRect = _presenter.RectTransform; // 새 Indicator의 RectTransform 배치 기준입니다.
        if (presenterRect != null)
        {
            presenterRect.anchorMin = new Vector2(0.5f, 0.5f);
            presenterRect.anchorMax = new Vector2(0.5f, 0.5f);
            presenterRect.pivot = new Vector2(0.5f, 0.5f);
            presenterRect.localScale = Vector3.one;
        }

        _presenter.Hide();
        return true;
    }

    /// <summary>
    /// 대상 Viewport 좌표가 화면 안에 있는지 판정합니다.
    /// </summary>
    private bool IsInsideViewport(Vector3 viewportPoint)
    {
        return viewportPoint.z > 0f
            && viewportPoint.x >= 0f
            && viewportPoint.x <= 1f
            && viewportPoint.y >= 0f
            && viewportPoint.y <= 1f;
    }

    /// <summary>
    /// 거리 제한 옵션이 켜져 있을 때 대상 거리가 표시 조건을 만족하는지 확인합니다.
    /// </summary>
    private bool PassesDistanceOption(Vector3 referencePosition, Vector3 targetPosition)
    {
        if (!_useDistanceLimit)
        {
            return true;
        }

        float distance = Vector3.Distance(referencePosition, targetPosition); // 오프셋 기준점과 대상 사이의 월드 거리입니다.
        if (distance < _minVisibleDistance)
        {
            return false;
        }

        return _maxVisibleDistance <= 0f || distance <= _maxVisibleDistance;
    }

    /// <summary>
    /// 로컬 플레이어 Transform 위치에 Inspector 오프셋을 더한 Indicator 기준점을 반환합니다.
    /// </summary>
    private Vector3 ResolveLocalPlayerReferencePosition(Transform localPlayerTransform)
    {
        if (localPlayerTransform == null)
        {
            return Vector3.zero;
        }

        Vector3 referencePosition = localPlayerTransform.position; // 오프셋 적용 전 로컬 플레이어 월드 위치입니다.
        referencePosition.x += _localPlayerReferenceOffset.x;
        referencePosition.y += _localPlayerReferenceOffset.y;
        return referencePosition;
    }

    /// <summary>
    /// 대상 플레이어 Transform 위치에 Inspector 오프셋을 더한 Indicator 추적점을 반환합니다.
    /// </summary>
    private Vector3 ResolveTargetPlayerReferencePosition(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            return Vector3.zero;
        }

        Vector3 referencePosition = targetTransform.position; // 오프셋 적용 전 대상 플레이어 월드 위치입니다.
        referencePosition.x += _targetPlayerReferenceOffset.x;
        referencePosition.y += _targetPlayerReferenceOffset.y;
        return referencePosition;
    }

    /// <summary>
    /// 로컬 플레이어에서 대상 플레이어를 향하는 방향을 카메라 화면 평면 기준으로 계산합니다.
    /// </summary>
    private Vector2 ResolvePlayerRelativeDirection(Vector3 referencePosition, Vector3 targetReferencePosition)
    {
        if (_targetCamera == null)
        {
            return Vector2.up;
        }

        Vector3 worldDirection = targetReferencePosition - referencePosition; // 오프셋 기준점에서 대상 추적점으로 향하는 월드 방향입니다.
        Vector2 direction = new Vector2(
            Vector3.Dot(_targetCamera.transform.right, worldDirection),
            Vector3.Dot(_targetCamera.transform.up, worldDirection)); // 카메라 화면 평면에 투영한 플레이어 기준 방향입니다.

        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        return Vector2.up;
    }

    /// <summary>
    /// 방향 벡터와 Root 크기를 기준으로 화면 가장자리 안쪽의 anchoredPosition을 계산합니다.
    /// </summary>
    private Vector2 CalculateEdgeAnchoredPosition(Vector3 referenceViewportPoint, Vector2 direction)
    {
        Rect rootRect = _indicatorRoot.rect; // Indicator 배치 기준이 되는 Canvas Root 사각형입니다.
        Vector2 halfSize = rootRect.size * 0.5f; // Root 중심 기준 절반 크기입니다.
        Vector2 safeHalfSize = new Vector2(
            Mathf.Max(0f, halfSize.x - _edgePadding),
            Mathf.Max(0f, halfSize.y - _edgePadding)); // Padding을 제외한 표시 가능 영역 절반 크기입니다.
        Vector2 origin = ViewportToAnchoredPosition(referenceViewportPoint, halfSize); // 오프셋 기준점의 화면상 기준 좌표입니다.
        origin.x = Mathf.Clamp(origin.x, -safeHalfSize.x, safeHalfSize.x);
        origin.y = Mathf.Clamp(origin.y, -safeHalfSize.y, safeHalfSize.y);

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.up;
        }

        direction.Normalize();

        float scaleX = ResolveAxisEdgeScale(origin.x, direction.x, safeHalfSize.x);
        float scaleY = ResolveAxisEdgeScale(origin.y, direction.y, safeHalfSize.y);
        float edgeScale = Mathf.Min(scaleX, scaleY); // 방향 벡터가 가장 먼저 만나는 화면 가장자리까지의 배율입니다.

        if (float.IsInfinity(edgeScale) || float.IsNaN(edgeScale))
        {
            edgeScale = safeHalfSize.y;
        }

        Vector2 edgePosition = origin + direction * edgeScale; // 로컬 플레이어 기준점에서 대상 방향으로 이동한 가장자리 좌표입니다.
        edgePosition.x = Mathf.Clamp(edgePosition.x, -safeHalfSize.x, safeHalfSize.x);
        edgePosition.y = Mathf.Clamp(edgePosition.y, -safeHalfSize.y, safeHalfSize.y);
        return edgePosition;
    }

    /// <summary>
    /// Viewport 좌표를 Root RectTransform의 중앙 기준 anchoredPosition으로 변환합니다.
    /// </summary>
    private Vector2 ViewportToAnchoredPosition(Vector3 viewportPoint, Vector2 halfSize)
    {
        return new Vector2(
            (viewportPoint.x - 0.5f) * halfSize.x * 2f,
            (viewportPoint.y - 0.5f) * halfSize.y * 2f);
    }

    /// <summary>
    /// 한 축에서 시작점과 방향을 기준으로 Padding 내부 가장자리까지 도달하는 배율을 계산합니다.
    /// </summary>
    private float ResolveAxisEdgeScale(float origin, float direction, float safeHalfSize)
    {
        if (Mathf.Abs(direction) <= 0.0001f)
        {
            return float.PositiveInfinity;
        }

        float targetEdge = direction > 0f ? safeHalfSize : -safeHalfSize; // 방향이 향하는 축의 가장자리 좌표입니다.
        float scale = (targetEdge - origin) / direction;
        return scale > 0f ? scale : float.PositiveInfinity;
    }

    /// <summary>
    /// Presenter가 준비되어 있으면 Indicator를 숨깁니다.
    /// </summary>
    private void HideIndicator()
    {
        if (_presenter != null)
        {
            _presenter.Hide();
        }
    }
}
