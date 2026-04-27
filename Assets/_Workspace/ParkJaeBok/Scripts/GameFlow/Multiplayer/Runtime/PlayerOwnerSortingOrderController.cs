using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 로컬 화면 기준으로 소유 플레이어의 SpriteRenderer 정렬 순서를 높게 보정하는 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerOwnerSortingOrderController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("정렬 순서를 보정할 SpriteRenderer 목록입니다. 비어 있고 Auto Collect Renderers가 켜져 있으면 자식까지 자동 수집합니다.")]
    [SerializeField] private SpriteRenderer[] _targetSpriteRenderers; // Owner 보정 Offset을 적용할 SpriteRenderer 대상 목록입니다.

    [Header("Policy")]
    [Tooltip("Awake 또는 적용 시점에 자식 SpriteRenderer를 자동으로 수집할지 여부입니다.")]
    [SerializeField] private bool _autoCollectRenderers = true; // 대상 SpriteRenderer 목록이 비어 있을 때 자동 수집을 허용하는 설정입니다.

    [Tooltip("Owner 캐릭터일 때 기본 sortingOrder에 더할 값입니다.")]
    [SerializeField] private int _ownerOrderOffset = 100; // 로컬 Owner 화면에서 다른 플레이어보다 위에 보이도록 더하는 정렬 순서 보정값입니다.

    [Tooltip("NonOwner 캐릭터일 때 기본 sortingOrder에 더할 값입니다.")]
    [SerializeField] private int _nonOwnerOrderOffset = 0; // 원격 플레이어 또는 싱글플레이 기본 상태에 적용할 정렬 순서 보정값입니다.

    [Tooltip("Awake 시점에 Renderer 목록과 기본 sortingOrder 값을 미리 캐시할지 여부입니다.")]
    [SerializeField] private bool _cacheOnAwake = true; // 최초 정렬 순서를 안정적으로 보존하기 위해 Awake에서 캐시할지 결정하는 설정입니다.

    [Tooltip("Start 시점에 현재 NetworkObject 상태를 기준으로 자동 적용할지 여부입니다.")]
    [SerializeField] private bool _applyOnStart = true; // 싱글플레이 또는 NetworkBehaviour 호출 누락 상황을 보정하기 위한 자동 적용 설정입니다.

    [Header("Diagnostics")]
    [Tooltip("Renderer가 없거나 설정이 잘못된 경우 Warning 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenRendererMissing = true; // 대상 SpriteRenderer 누락 Warning 출력 여부입니다.

    [Tooltip("적용 결과를 Debug 로그로 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLogging; // Owner 판정과 적용된 Offset을 확인하기 위한 상세 로그 출력 여부입니다.

    [Tooltip("디버그용: 마지막 적용 시 Owner로 판정되었는지 표시합니다.")]
    [SerializeField] private bool _lastAppliedAsOwner; // 마지막 적용 시 로컬 Owner로 처리되었는지 Inspector에서 확인하기 위한 값입니다.

    [Tooltip("디버그용: 마지막 적용에 사용한 sortingOrder Offset입니다.")]
    [SerializeField] private int _lastAppliedOffset; // 마지막으로 SpriteRenderer에 더한 Offset 값을 Inspector에서 확인하기 위한 값입니다.

    private readonly Dictionary<SpriteRenderer, int> _baseSortingOrders = new Dictionary<SpriteRenderer, int>(); // Renderer별 최초 sortingOrder를 보존하는 캐시입니다.
    private NetworkObject _networkObject; // Owner 판정에 사용할 같은 오브젝트 또는 부모 NetworkObject 참조입니다.
    private bool _hasCachedBaseOrders; // 최초 sortingOrder 캐시가 완료되었는지 추적하는 플래그입니다.
    private bool _hasAppliedSortingOrder; // 캐시된 기본값 복원이 필요한 적용 이력이 있는지 추적하는 플래그입니다.

    /// <summary>
    /// 플레이어 인스턴스에 Owner 정렬 보정 컴포넌트가 없으면 런타임에 추가하고 반환합니다.
    /// </summary>
    public static PlayerOwnerSortingOrderController EnsureAttached(GameObject playerRoot)
    {
        if (playerRoot == null)
        {
            return null;
        }

        if (playerRoot.TryGetComponent(out PlayerOwnerSortingOrderController controller))
        {
            return controller;
        }

        return playerRoot.AddComponent<PlayerOwnerSortingOrderController>();
    }

    /// <summary>
    /// 컴포넌트 초기화 시 NetworkObject와 Renderer 기본 정렬 순서를 캐시합니다.
    /// </summary>
    private void Awake()
    {
        ResolveNetworkObjectIfNeeded();

        if (_cacheOnAwake)
        {
            CacheBaseSortingOrdersIfNeeded();
        }
    }

    /// <summary>
    /// 싱글플레이 또는 네트워크 스폰 콜백이 없는 경로에서도 기본 정렬 순서를 적용합니다.
    /// </summary>
    private void Start()
    {
        if (!_applyOnStart)
        {
            return;
        }

        ApplyByCurrentNetworkState();
    }

    /// <summary>
    /// 현재 NetworkObject와 NetworkManager 상태를 기준으로 로컬 Owner 정렬 보정을 적용합니다.
    /// </summary>
    public void ApplyByCurrentNetworkState()
    {
        ResolveNetworkObjectIfNeeded();

        if (!CanUseNetworkOwnership())
        {
            ApplySortingOrder(false);
            return;
        }

        ApplySortingOrder(_networkObject.IsOwner);
    }

    /// <summary>
    /// 외부 NetworkBehaviour가 확정한 Owner 여부를 기준으로 정렬 보정을 적용합니다.
    /// </summary>
    public void ApplyByOwnership(bool isOwner)
    {
        ApplySortingOrder(isOwner);
    }

    /// <summary>
    /// Renderer 목록을 갱신하고 현재 캐시 기준으로 정렬 보정을 다시 적용합니다.
    /// </summary>
    public void RefreshTargetsAndApply()
    {
        RestoreCachedSortingOrders();
        _hasCachedBaseOrders = false;
        CacheBaseSortingOrdersIfNeeded();
        ApplyByCurrentNetworkState();
    }

    /// <summary>
    /// 대상 SpriteRenderer의 최초 sortingOrder를 캐시합니다.
    /// </summary>
    private void CacheBaseSortingOrdersIfNeeded()
    {
        if (_hasCachedBaseOrders)
        {
            return;
        }

        ResolveRenderersIfNeeded();
        _baseSortingOrders.Clear();

        if (_targetSpriteRenderers == null || _targetSpriteRenderers.Length == 0)
        {
            if (_warnWhenRendererMissing)
            {
                Debug.LogWarning($"[PlayerOwnerSortingOrderController] SpriteRenderer 대상이 없어 Owner 정렬 보정을 적용할 수 없습니다. object={name}", this);
            }

            _hasCachedBaseOrders = true;
            return;
        }

        for (int index = 0; index < _targetSpriteRenderers.Length; index++)
        {
            SpriteRenderer targetRenderer = _targetSpriteRenderers[index]; // 기본 sortingOrder를 보존할 현재 SpriteRenderer입니다.
            if (targetRenderer == null || _baseSortingOrders.ContainsKey(targetRenderer))
            {
                continue;
            }

            _baseSortingOrders.Add(targetRenderer, targetRenderer.sortingOrder);
        }

        _hasCachedBaseOrders = true;
    }

    /// <summary>
    /// 설정된 Owner 여부에 맞춰 캐시된 기본 sortingOrder에 Offset을 더해 적용합니다.
    /// </summary>
    private void ApplySortingOrder(bool isOwner)
    {
        CacheBaseSortingOrdersIfNeeded();

        int targetOffset = isOwner ? _ownerOrderOffset : _nonOwnerOrderOffset; // 현재 로컬 화면에서 적용할 정렬 순서 보정값입니다.
        _lastAppliedAsOwner = isOwner;
        _lastAppliedOffset = targetOffset;

        foreach (KeyValuePair<SpriteRenderer, int> pair in _baseSortingOrders)
        {
            SpriteRenderer targetRenderer = pair.Key; // 정렬 순서를 갱신할 SpriteRenderer입니다.
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.sortingOrder = pair.Value + targetOffset;
        }

        _hasAppliedSortingOrder = true;

        if (_verboseLogging)
        {
            Debug.Log($"[PlayerOwnerSortingOrderController] Owner sorting applied. object={name}, isOwner={isOwner}, offset={targetOffset}, rendererCount={_baseSortingOrders.Count}", this);
        }
    }

    /// <summary>
    /// Renderer 대상 재수집 전에 이전에 저장한 기본 sortingOrder를 복원합니다.
    /// </summary>
    private void RestoreCachedSortingOrders()
    {
        if (!_hasAppliedSortingOrder)
        {
            return;
        }

        foreach (KeyValuePair<SpriteRenderer, int> pair in _baseSortingOrders)
        {
            SpriteRenderer targetRenderer = pair.Key; // 기본 sortingOrder로 되돌릴 SpriteRenderer입니다.
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.sortingOrder = pair.Value;
        }

        _hasAppliedSortingOrder = false;
    }

    /// <summary>
    /// 대상 Renderer 목록이 비어 있으면 자식 SpriteRenderer를 자동 수집합니다.
    /// </summary>
    private void ResolveRenderersIfNeeded()
    {
        if (!_autoCollectRenderers)
        {
            return;
        }

        if (_targetSpriteRenderers != null && _targetSpriteRenderers.Length > 0)
        {
            return;
        }

        _targetSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    /// <summary>
    /// Owner 판정에 사용할 NetworkObject를 같은 오브젝트 또는 부모에서 찾습니다.
    /// </summary>
    private void ResolveNetworkObjectIfNeeded()
    {
        if (_networkObject != null)
        {
            return;
        }

        _networkObject = GetComponent<NetworkObject>();
        if (_networkObject == null)
        {
            _networkObject = GetComponentInParent<NetworkObject>();
        }
    }

    /// <summary>
    /// 현재 런타임에서 NetworkObject의 Owner 정보를 신뢰할 수 있는지 판정합니다.
    /// </summary>
    private bool CanUseNetworkOwnership()
    {
        if (_networkObject == null || !_networkObject.IsSpawned)
        {
            return false;
        }

        NetworkManager networkManager = _networkObject.NetworkManager != null
            ? _networkObject.NetworkManager
            : NetworkManager.Singleton; // 스폰된 NetworkObject가 참조하는 NetworkManager를 우선 사용하고 없으면 Singleton을 보조로 사용합니다.

        return networkManager != null && networkManager.IsListening;
    }
}
