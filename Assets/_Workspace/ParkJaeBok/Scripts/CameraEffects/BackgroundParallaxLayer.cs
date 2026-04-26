using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 카메라 자식으로 배치한 3장의 배경 스프라이트를
/// 로컬 캐릭터의 이동량 기준으로 순환 패럴렉스 처리합니다.
/// X축은 순환 재배치를 지원하고, Y축은 루트 오프셋만 적용합니다.
/// </summary>
[DisallowMultipleComponent]
public class BackgroundParallaxLayer : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("패럴렉스 이동 기준으로 사용할 로컬 캐릭터 Transform입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private Transform _targetCharacterTransform; // 패럴렉스 이동량 계산의 기준이 되는 캐릭터 Transform입니다.
    [Tooltip("싱글플레이/멀티플레이 모드 판정에 사용할 GameFlowController 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private GameFlowController _gameFlowController; // 현재 플레이 모드 해석에 사용할 GameFlowController 참조입니다.
    [Tooltip("멀티플레이에서 로컬 PlayerObject를 찾을 때 사용할 NetworkManager 참조입니다. 비어 있으면 NetworkManager.Singleton을 사용합니다.")]
    [SerializeField] private NetworkManager _networkManager; // 로컬 PlayerObject 탐색에 사용할 NetworkManager 참조입니다.

    [Header("Parallax Layout")]
    [Tooltip("세 장의 배경을 함께 이동시킬 루트 Transform입니다. 비어 있으면 현재 오브젝트 Transform을 사용합니다.")]
    [SerializeField] private Transform _parallaxRoot; // 실제 패럴렉스 이동을 적용할 루트 Transform입니다.
    [Tooltip("현재 좌측 역할을 맡는 스프라이트 Transform입니다.")]
    [SerializeField] private Transform _leftSpriteTransform; // 현재 좌측 슬롯 역할을 담당하는 스프라이트 Transform입니다.
    [Tooltip("현재 중앙 역할을 맡는 스프라이트 Transform입니다.")]
    [SerializeField] private Transform _centerSpriteTransform; // 현재 중앙 슬롯 역할을 담당하는 스프라이트 Transform입니다.
    [Tooltip("현재 우측 역할을 맡는 스프라이트 Transform입니다.")]
    [SerializeField] private Transform _rightSpriteTransform; // 현재 우측 슬롯 역할을 담당하는 스프라이트 Transform입니다.
    [Tooltip("스프라이트 정렬에 사용할 Y축 기준값입니다. Apply Layout Base Y가 켜져 있으면 세 스프라이트가 이 높이에 맞춰집니다.")]
    [SerializeField] private float _layoutBaseY = 0f; // 스프라이트 자동 정렬 시 공통으로 적용할 로컬 Y 기준값입니다.
    [Tooltip("스프라이트 한 장의 폭입니다. 0 이하이면 Center Sprite의 SpriteRenderer 기준으로 계산을 시도하고 Warning을 출력합니다.")]
    [SerializeField] private float _spriteWidth = 0f; // 스프라이트 재배치와 간격 정렬에 사용할 기준 폭입니다.
    [Tooltip("캐릭터 X 이동량에 곱할 패럴렉스 배율입니다.")]
    [SerializeField] private float _parallaxMultiplierX = 0.15f; // 캐릭터 X 이동량 대비 배경 X축이 얼마나 반대로 움직일지 결정하는 배율입니다.
    [Tooltip("캐릭터 Y 이동량에 곱할 패럴렉스 배율입니다.")]
    [SerializeField] private float _parallaxMultiplierY = 0f; // 캐릭터 Y 이동량 대비 배경 Y축이 얼마나 반대로 움직일지 결정하는 배율입니다.
    [Tooltip("현재 Center Sprite가 초기 중앙 기준으로 이 비율만큼 벗어나면 순환 재배치할지 결정하는 값입니다.")]
    [SerializeField] private float _repositionRatio = 0.7f; // 재배치 임계값 계산에 사용할 스프라이트 폭 비율입니다.
    [Tooltip("true면 시작 시 Left, Center, Right 스프라이트를 자동으로 이어 붙여 정렬합니다.")]
    [SerializeField] private bool _autoInitializePosition = true; // 시작 시 세 장의 스프라이트를 자동 정렬할지 여부입니다.
    [Tooltip("true면 Auto Initialize Position 실행 시 세 스프라이트의 Y축 위치를 Layout Base Y로 통일합니다.")]
    [SerializeField] private bool _applyLayoutBaseY = true; // 자동 정렬 시 공통 Y 기준값을 실제로 적용할지 여부입니다.

    [Header("Binding Policy")]
    [Tooltip("씬 로드 이후 로컬 캐릭터를 다시 탐색할지 여부입니다.")]
    [SerializeField] private bool _resolveOnSceneLoaded = true; // 씬 전환 이후 로컬 캐릭터를 자동 재탐색할지 여부입니다.
    [Tooltip("멀티플레이에서 LocalClient.PlayerObject가 비어 있을 때 OwnerClientId 스캔 폴백을 허용할지 여부입니다.")]
    [SerializeField] private bool _allowNetworkOwnerFallbackScan = true; // PlayerObject 지연 준비 상황에서 OwnerClientId 기반 재탐색을 허용할지 여부입니다.

    [Header("Debug")]
    [Tooltip("자동 탐색이나 폭 자동 계산 같은 폴백 경로를 사용할 때 Warning 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnOnFallbackPath = true; // 폴백 경로 사용 시 Warning 로그를 출력할지 여부입니다.
    [Tooltip("디버그 확인용: 현재 패럴렉스 기준으로 사용 중인 로컬 캐릭터 Transform입니다.")]
    [SerializeField] private Transform _resolvedTargetCharacter; // 현재 패럴렉스 기준으로 바인딩된 로컬 캐릭터 Transform입니다.
    [Tooltip("디버그 확인용: 현재 레이아웃 계산에 사용 중인 스프라이트 폭입니다.")]
    [SerializeField] private float _resolvedSpriteWidth; // 현재 계산에 반영된 유효 스프라이트 폭입니다.
    [Tooltip("디버그 확인용: 현재 자동 정렬에 사용 중인 Y축 기준값입니다.")]
    [SerializeField] private float _resolvedLayoutBaseY; // 현재 자동 정렬에 반영된 Y축 기준값입니다.
    [Tooltip("디버그 확인용: 현재 Center Sprite가 초기 중앙 기준으로 이동한 X 오프셋입니다.")]
    [SerializeField] private float _currentCenterOffsetX; // 현재 중앙 역할 스프라이트의 초기 기준 X 오프셋입니다.
    [Tooltip("디버그 확인용: 현재 Parallax Root가 초기 Y 기준에서 이동한 Y 오프셋입니다.")]
    [SerializeField] private float _currentRootOffsetY; // 현재 패럴렉스 루트의 초기 기준 Y 오프셋입니다.

    private bool _isSceneLoadedHookRegistered; // sceneLoaded 콜백 등록 여부를 추적하는 플래그입니다.
    private bool _networkCallbacksRegistered; // NetworkManager 콜백 등록 여부를 추적하는 플래그입니다.
    private bool _hasPreviousTargetPosition; // 이전 프레임 대상 위치가 유효한지 추적하는 플래그입니다.
    private bool _hasPreparedLayout; // 패럴렉스 레이아웃 초기화가 완료되었는지 추적하는 플래그입니다.
    private bool _hasLoggedMissingTargetWarning; // 로컬 캐릭터 미해석 Warning 중복 출력을 방지하는 플래그입니다.
    private bool _hasLoggedMissingLayoutWarning; // 필수 레이아웃 참조 누락 Warning 중복 출력을 방지하는 플래그입니다.
    private bool _hasLoggedSpriteWidthWarning; // 스프라이트 폭 해석 실패 Warning 중복 출력을 방지하는 플래그입니다.
    private bool _hasLoggedNetworkOwnerFallbackWarning; // OwnerClientId 스캔 폴백 Warning 중복 출력을 방지하는 플래그입니다.
    private bool _hasLoggedRootTransformFallbackWarning; // PlayerObject 루트 Transform 폴백 Warning 중복 출력을 방지하는 플래그입니다.
    private bool _hasLoggedSpriteWidthFallbackWarning; // SpriteRenderer 기반 폭 자동 계산 Warning 중복 출력을 방지하는 플래그입니다.
    private Vector3 _previousTargetPosition; // 직전 프레임 로컬 캐릭터 위치입니다.
    private float _initialCenterAnchorX; // 초기 중앙 기준점으로 사용할 Center Sprite의 루트 기준 X 위치입니다.
    private float _initialRootLocalY; // 초기 기준점으로 사용할 Parallax Root의 로컬 Y 위치입니다.

    /// <summary>
    /// 의존성을 보정하고 패럴렉스 초기 바인딩을 준비합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveDependencies();
        RegisterHooks();
        ResolveTargetCharacter();
        ResetTargetTrackingBaseline();
        TryPrepareLayout();
    }

    /// <summary>
    /// 등록한 씬/네트워크 콜백을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        UnregisterHooks();
        _hasPreviousTargetPosition = false;
    }

    /// <summary>
    /// 인스펙터 값 변경 시 기본 참조와 범위를 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        ResolveDependencies();
        _parallaxMultiplierX = Mathf.Max(0f, _parallaxMultiplierX);
        _parallaxMultiplierY = Mathf.Max(0f, _parallaxMultiplierY);
        _repositionRatio = Mathf.Clamp(_repositionRatio, 0.1f, 1f);
    }

    /// <summary>
    /// 로컬 캐릭터 이동량을 바탕으로 루트를 이동시키고 필요 시 스프라이트를 순환 재배치합니다.
    /// </summary>
    private void LateUpdate()
    {
        if (!TryPrepareLayout())
        {
            return;
        }

        if (!EnsureTargetCharacter())
        {
            return;
        }

        Vector3 currentTargetPosition = _resolvedTargetCharacter.position; // 현재 프레임 로컬 캐릭터 위치입니다.
        if (!_hasPreviousTargetPosition)
        {
            _previousTargetPosition = currentTargetPosition;
            _hasPreviousTargetPosition = true;
            return;
        }

        Vector3 characterDelta = currentTargetPosition - _previousTargetPosition; // 이전 프레임 대비 로컬 캐릭터 이동량입니다.
        _previousTargetPosition = currentTargetPosition;

        if (Mathf.Approximately(characterDelta.x, 0f) && Mathf.Approximately(characterDelta.y, 0f))
        {
            UpdateDebugState();
            return;
        }

        ApplyParallaxMovement(characterDelta);
        RepositionSpritesIfNeeded();
        UpdateDebugState();
    }

    /// <summary>
    /// 필요한 공용 참조를 자동 보정합니다.
    /// </summary>
    private void ResolveDependencies()
    {
        if (_parallaxRoot == null)
        {
            _parallaxRoot = transform;
        }

        if (_gameFlowController == null)
        {
            _gameFlowController = GameFlowController.Instance != null
                ? GameFlowController.Instance
                : FindAnyObjectByType<GameFlowController>();
        }

        if (_networkManager == null)
        {
            _networkManager = NetworkManager.Singleton;
        }
    }

    /// <summary>
    /// 씬 전환과 네트워크 상태 변화를 감지할 콜백을 등록합니다.
    /// </summary>
    private void RegisterHooks()
    {
        if (_resolveOnSceneLoaded && !_isSceneLoadedHookRegistered)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _isSceneLoadedHookRegistered = true;
        }

        if (!TryResolveNetworkManager(out NetworkManager networkManager))
        {
            return;
        }

        if (_networkCallbacksRegistered)
        {
            return;
        }

        networkManager.OnClientConnectedCallback += HandleClientConnectionChanged;
        networkManager.OnClientDisconnectCallback += HandleClientConnectionChanged;
        _networkCallbacksRegistered = true;
    }

    /// <summary>
    /// 등록했던 씬 전환/네트워크 콜백을 해제합니다.
    /// </summary>
    private void UnregisterHooks()
    {
        if (_isSceneLoadedHookRegistered)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _isSceneLoadedHookRegistered = false;
        }

        if (!TryResolveNetworkManager(out NetworkManager networkManager) || !_networkCallbacksRegistered)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= HandleClientConnectionChanged;
        networkManager.OnClientDisconnectCallback -= HandleClientConnectionChanged;
        _networkCallbacksRegistered = false;
    }

    /// <summary>
    /// 씬 로드 이후 로컬 캐릭터와 추적 기준값을 다시 동기화합니다.
    /// </summary>
    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        ResolveDependencies();
        ResolveTargetCharacter();
        ResetTargetTrackingBaseline();
        _hasPreparedLayout = false;
        TryPrepareLayout();
    }

    /// <summary>
    /// 네트워크 접속 상태 변경 이후 로컬 캐릭터와 추적 기준값을 다시 동기화합니다.
    /// </summary>
    private void HandleClientConnectionChanged(ulong _)
    {
        ResolveDependencies();
        ResolveTargetCharacter();
        ResetTargetTrackingBaseline();
    }

    /// <summary>
    /// 패럴렉스 동작에 필요한 레이아웃 참조와 스프라이트 폭을 검증하고 초기 위치를 준비합니다.
    /// </summary>
    private bool TryPrepareLayout()
    {
        ResolveDependencies();

        if (_hasPreparedLayout)
        {
            if (!HasValidLayoutReferences())
            {
                _hasPreparedLayout = false;
            }
            else
            {
                UpdateDebugState();
                return true;
            }
        }

        if (!HasValidLayoutReferences())
        {
            if (!_hasLoggedMissingLayoutWarning)
            {
                Debug.LogWarning($"[BackgroundParallaxLayer] 필수 레이아웃 참조가 누락되어 패럴렉스 처리를 건너뜁니다. object={name}", this);
                _hasLoggedMissingLayoutWarning = true;
            }

            return false;
        }

        _hasLoggedMissingLayoutWarning = false;

        if (!TryResolveSpriteWidth(out float resolvedSpriteWidth))
        {
            if (!_hasLoggedSpriteWidthWarning)
            {
                Debug.LogWarning($"[BackgroundParallaxLayer] Sprite Width를 해석하지 못해 패럴렉스 처리를 건너뜁니다. object={name}", this);
                _hasLoggedSpriteWidthWarning = true;
            }

            return false;
        }

        _hasLoggedSpriteWidthWarning = false;
        _resolvedSpriteWidth = resolvedSpriteWidth;

        if (_autoInitializePosition)
        {
            AutoInitializeSpritePositions(resolvedSpriteWidth);
        }
        else
        {
            _resolvedLayoutBaseY = _centerSpriteTransform != null ? _centerSpriteTransform.localPosition.y : _layoutBaseY;
        }

        _initialCenterAnchorX = GetCurrentCenterAnchorX();
        _initialRootLocalY = _parallaxRoot != null ? _parallaxRoot.localPosition.y : 0f;
        _hasPreparedLayout = true;
        UpdateDebugState();
        return true;
    }

    /// <summary>
    /// 패럴렉스 동작에 필요한 Transform 참조가 모두 연결되었는지 검사합니다.
    /// </summary>
    private bool HasValidLayoutReferences()
    {
        return _parallaxRoot != null
            && _leftSpriteTransform != null
            && _centerSpriteTransform != null
            && _rightSpriteTransform != null;
    }

    /// <summary>
    /// 인스펙터 입력값 또는 Center Sprite의 SpriteRenderer에서 유효 스프라이트 폭을 해석합니다.
    /// </summary>
    private bool TryResolveSpriteWidth(out float resolvedSpriteWidth)
    {
        if (_spriteWidth > 0f)
        {
            resolvedSpriteWidth = _spriteWidth;
            return true;
        }

        resolvedSpriteWidth = 0f;

        if (_centerSpriteTransform == null)
        {
            return false;
        }

        SpriteRenderer centerRenderer = _centerSpriteTransform.GetComponent<SpriteRenderer>(); // 폭 자동 계산의 기준으로 사용할 중앙 스프라이트 렌더러입니다.
        if (centerRenderer == null || centerRenderer.sprite == null)
        {
            return false;
        }

        resolvedSpriteWidth = centerRenderer.sprite.bounds.size.x * Mathf.Abs(_centerSpriteTransform.localScale.x);
        if (resolvedSpriteWidth <= 0f)
        {
            return false;
        }

        if (_warnOnFallbackPath && !_hasLoggedSpriteWidthFallbackWarning)
        {
            Debug.LogWarning($"[BackgroundParallaxLayer] Sprite Width가 0 이하라 Center SpriteRenderer 기준 폭을 사용합니다. object={name}, width={resolvedSpriteWidth}", this);
            _hasLoggedSpriteWidthFallbackWarning = true;
        }

        return true;
    }

    /// <summary>
    /// Left, Center, Right 스프라이트를 중앙 기준으로 좌우에 이어 붙도록 자동 정렬합니다.
    /// </summary>
    private void AutoInitializeSpritePositions(float spriteWidth)
    {
        Vector3 centerLocalPosition = _centerSpriteTransform.localPosition; // 자동 정렬의 기준이 되는 중앙 스프라이트 로컬 위치입니다.
        float layoutBaseY = ResolveLayoutBaseY(centerLocalPosition.y); // 이번 자동 정렬에 적용할 공통 Y축 기준값입니다.
        _leftSpriteTransform.localPosition = new Vector3(centerLocalPosition.x - spriteWidth, layoutBaseY, _leftSpriteTransform.localPosition.z);
        _centerSpriteTransform.localPosition = new Vector3(centerLocalPosition.x, layoutBaseY, _centerSpriteTransform.localPosition.z);
        _rightSpriteTransform.localPosition = new Vector3(centerLocalPosition.x + spriteWidth, layoutBaseY, _rightSpriteTransform.localPosition.z);
    }

    /// <summary>
    /// 자동 정렬 시 사용할 Y축 기준값을 결정합니다.
    /// </summary>
    private float ResolveLayoutBaseY(float fallbackY)
    {
        if (!_applyLayoutBaseY)
        {
            _resolvedLayoutBaseY = fallbackY;
            return fallbackY;
        }

        _resolvedLayoutBaseY = _layoutBaseY;
        return _layoutBaseY;
    }

    /// <summary>
    /// 인스펙터 직접 연결 또는 로컬 플레이어 자동 탐색 결과로 타깃 캐릭터를 보정합니다.
    /// </summary>
    private void ResolveTargetCharacter()
    {
        if (_targetCharacterTransform != null)
        {
            _resolvedTargetCharacter = _targetCharacterTransform;
            _hasLoggedMissingTargetWarning = false;
            return;
        }

        if (TryResolveTargetCharacterInternal(out Transform resolvedTarget))
        {
            _resolvedTargetCharacter = resolvedTarget;
            _hasLoggedMissingTargetWarning = false;
            return;
        }

        _resolvedTargetCharacter = null;
    }

    /// <summary>
    /// 현재 프레임에 사용할 로컬 캐릭터 참조가 유효한지 확인하고 필요 시 재탐색합니다.
    /// </summary>
    private bool EnsureTargetCharacter()
    {
        if (_targetCharacterTransform != null)
        {
            if (_resolvedTargetCharacter != _targetCharacterTransform)
            {
                _resolvedTargetCharacter = _targetCharacterTransform;
                ResetTargetTrackingBaseline();
            }

            return true;
        }

        if (_resolvedTargetCharacter != null)
        {
            return true;
        }

        ResolveTargetCharacter();
        if (_resolvedTargetCharacter != null)
        {
            ResetTargetTrackingBaseline();
            return true;
        }

        if (!_hasLoggedMissingTargetWarning)
        {
            Debug.LogWarning($"[BackgroundParallaxLayer] 로컬 캐릭터를 찾지 못해 패럴렉스 처리를 건너뜁니다. object={name}", this);
            _hasLoggedMissingTargetWarning = true;
        }

        return false;
    }

    /// <summary>
    /// 멀티플레이 또는 싱글플레이 규칙에 맞춰 로컬 캐릭터 Transform을 해석합니다.
    /// </summary>
    private bool TryResolveTargetCharacterInternal(out Transform resolvedTarget)
    {
        if (TryResolveTargetFromNetwork(out resolvedTarget))
        {
            return true;
        }

        return TryResolveTargetForSinglePlayer(out resolvedTarget);
    }

    /// <summary>
    /// NGO 로컬 PlayerObject 기준으로 로컬 캐릭터 Transform을 해석합니다.
    /// </summary>
    private bool TryResolveTargetFromNetwork(out Transform resolvedTarget)
    {
        resolvedTarget = null;

        if (!TryResolveNetworkManager(out NetworkManager networkManager) || !networkManager.IsListening)
        {
            return false;
        }

        NetworkObject localPlayerObject = networkManager.LocalClient != null ? networkManager.LocalClient.PlayerObject : null; // NGO가 보유한 로컬 PlayerObject 참조입니다.
        if (TryResolveCharacterTransformFromObject(localPlayerObject != null ? localPlayerObject.gameObject : null, out resolvedTarget))
        {
            return true;
        }

        if (_allowNetworkOwnerFallbackScan && TryResolveTargetByOwnerClientId(networkManager, out resolvedTarget))
        {
            if (_warnOnFallbackPath && !_hasLoggedNetworkOwnerFallbackWarning)
            {
                Debug.LogWarning($"[BackgroundParallaxLayer] LocalClient.PlayerObject가 비어 있어 OwnerClientId 스캔 폴백을 사용합니다. object={name}, localClientId={networkManager.LocalClientId}", this);
                _hasLoggedNetworkOwnerFallbackWarning = true;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 싱글플레이에서는 PlayerInputDriver 기준으로 로컬 캐릭터 Transform을 해석합니다.
    /// </summary>
    private bool TryResolveTargetForSinglePlayer(out Transform resolvedTarget)
    {
        resolvedTarget = null;

        if (!IsSinglePlayerMode())
        {
            return false;
        }

        PlayerInputDriver[] inputDrivers = FindObjectsByType<PlayerInputDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 싱글플레이 로컬 입력 주체 후보 목록입니다.
        for (int index = 0; index < inputDrivers.Length; index++)
        {
            PlayerInputDriver inputDriver = inputDrivers[index]; // 현재 검사 중인 PlayerInputDriver 후보입니다.
            if (inputDriver == null)
            {
                continue;
            }

            resolvedTarget = inputDriver.transform;
            return true;
        }

        return false;
    }

    /// <summary>
    /// OwnerClientId와 매칭되는 PlayerInputDriver를 스캔해 로컬 캐릭터 Transform을 해석합니다.
    /// </summary>
    private bool TryResolveTargetByOwnerClientId(NetworkManager networkManager, out Transform resolvedTarget)
    {
        resolvedTarget = null;
        if (networkManager == null)
        {
            return false;
        }

        PlayerInputDriver[] inputDrivers = FindObjectsByType<PlayerInputDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 소유권 기반으로 다시 매칭할 PlayerInputDriver 후보 목록입니다.
        for (int index = 0; index < inputDrivers.Length; index++)
        {
            PlayerInputDriver inputDriver = inputDrivers[index]; // 현재 검사 중인 PlayerInputDriver 후보입니다.
            if (inputDriver == null)
            {
                continue;
            }

            NetworkObject candidateNetworkObject = inputDriver.GetComponentInParent<NetworkObject>();
            if (candidateNetworkObject == null || !candidateNetworkObject.IsSpawned)
            {
                continue;
            }

            if (candidateNetworkObject.OwnerClientId != networkManager.LocalClientId)
            {
                continue;
            }

            resolvedTarget = inputDriver.transform;
            return true;
        }

        return false;
    }

    /// <summary>
    /// PlayerObject 루트에서 플레이어 관련 컴포넌트를 우선 찾고, 없으면 루트 Transform으로 폴백합니다.
    /// </summary>
    private bool TryResolveCharacterTransformFromObject(GameObject targetObject, out Transform resolvedTarget)
    {
        resolvedTarget = null;
        if (targetObject == null)
        {
            return false;
        }

        PlayerInputDriver inputDriver = targetObject.GetComponent<PlayerInputDriver>();
        if (inputDriver == null)
        {
            inputDriver = targetObject.GetComponentInChildren<PlayerInputDriver>(true);
        }

        if (inputDriver == null)
        {
            inputDriver = targetObject.GetComponentInParent<PlayerInputDriver>();
        }

        if (inputDriver != null)
        {
            resolvedTarget = inputDriver.transform;
            return true;
        }

        PlayerMovement playerMovement = targetObject.GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            playerMovement = targetObject.GetComponentInChildren<PlayerMovement>(true);
        }

        if (playerMovement == null)
        {
            playerMovement = targetObject.GetComponentInParent<PlayerMovement>();
        }

        if (playerMovement != null)
        {
            resolvedTarget = playerMovement.transform;
            return true;
        }

        resolvedTarget = targetObject.transform;
        if (_warnOnFallbackPath && !_hasLoggedRootTransformFallbackWarning)
        {
            Debug.LogWarning($"[BackgroundParallaxLayer] PlayerInputDriver/PlayerMovement를 찾지 못해 PlayerObject 루트 Transform을 사용합니다. object={name}, playerObject={targetObject.name}", this);
            _hasLoggedRootTransformFallbackWarning = true;
        }

        return true;
    }

    /// <summary>
    /// 현재 모드가 싱글플레이인지 판정합니다.
    /// </summary>
    private bool IsSinglePlayerMode()
    {
        if (_gameFlowController != null)
        {
            return _gameFlowController.CurrentPlayMode == E_GamePlayMode.SinglePlayer;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 로컬 캐릭터 추적 기준값을 현재 위치로 재설정해 급격한 점프를 방지합니다.
    /// </summary>
    private void ResetTargetTrackingBaseline()
    {
        if (_resolvedTargetCharacter == null)
        {
            _hasPreviousTargetPosition = false;
            return;
        }

        _previousTargetPosition = _resolvedTargetCharacter.position;
        _hasPreviousTargetPosition = true;
    }

    /// <summary>
    /// 캐릭터 이동량에 축별 패럴렉스 배율을 곱해 배경 루트를 반대 방향으로 이동시킵니다.
    /// </summary>
    private void ApplyParallaxMovement(Vector3 characterDelta)
    {
        float parallaxDeltaX = -characterDelta.x * _parallaxMultiplierX; // 캐릭터 X 이동 반대 방향으로 적용할 배경 루트 X 이동량입니다.
        float parallaxDeltaY = -characterDelta.y * _parallaxMultiplierY; // 캐릭터 Y 이동 반대 방향으로 적용할 배경 루트 Y 이동량입니다.
        Vector3 rootLocalPosition = _parallaxRoot.localPosition; // 패럴렉스 이동 적용 전 루트 로컬 위치입니다.
        rootLocalPosition.x += parallaxDeltaX;
        rootLocalPosition.y += parallaxDeltaY;
        _parallaxRoot.localPosition = rootLocalPosition;
    }

    /// <summary>
    /// 현재 중앙 역할 스프라이트 위치가 임계값을 넘으면 좌우 순환 재배치를 수행합니다.
    /// </summary>
    private void RepositionSpritesIfNeeded()
    {
        float threshold = _resolvedSpriteWidth * _repositionRatio; // 중앙 역할 스프라이트의 순환 재배치 임계값입니다.
        int safetyCounter = 0; // 한 프레임 내 과도한 반복을 방지하는 안전 카운터입니다.

        while (safetyCounter < 4)
        {
            float currentCenterOffsetX = GetCurrentCenterAnchorX() - _initialCenterAnchorX; // 현재 중앙 역할 스프라이트의 초기 기준 X 오프셋입니다.
            if (currentCenterOffsetX <= -threshold)
            {
                RecycleForBackgroundMovedLeft();
                safetyCounter++;
                continue;
            }

            if (currentCenterOffsetX >= threshold)
            {
                RecycleForBackgroundMovedRight();
                safetyCounter++;
                continue;
            }

            break;
        }
    }

    /// <summary>
    /// 배경이 좌측으로 이동했을 때 왼쪽 스프라이트를 오른쪽 끝으로 옮기고 역할 식별을 갱신합니다.
    /// </summary>
    private void RecycleForBackgroundMovedLeft()
    {
        Vector3 recycledLocalPosition = _leftSpriteTransform.localPosition; // 오른쪽 끝으로 재배치할 기존 좌측 스프라이트의 로컬 위치입니다.
        recycledLocalPosition.x = _rightSpriteTransform.localPosition.x + _resolvedSpriteWidth;
        _leftSpriteTransform.localPosition = recycledLocalPosition;

        Transform previousLeft = _leftSpriteTransform; // 재배치 전 좌측 역할 스프라이트 참조입니다.
        Transform previousCenter = _centerSpriteTransform; // 재배치 전 중앙 역할 스프라이트 참조입니다.
        Transform previousRight = _rightSpriteTransform; // 재배치 전 우측 역할 스프라이트 참조입니다.

        _leftSpriteTransform = previousCenter;
        _centerSpriteTransform = previousRight;
        _rightSpriteTransform = previousLeft;
    }

    /// <summary>
    /// 배경이 우측으로 이동했을 때 오른쪽 스프라이트를 왼쪽 끝으로 옮기고 역할 식별을 갱신합니다.
    /// </summary>
    private void RecycleForBackgroundMovedRight()
    {
        Vector3 recycledLocalPosition = _rightSpriteTransform.localPosition; // 왼쪽 끝으로 재배치할 기존 우측 스프라이트의 로컬 위치입니다.
        recycledLocalPosition.x = _leftSpriteTransform.localPosition.x - _resolvedSpriteWidth;
        _rightSpriteTransform.localPosition = recycledLocalPosition;

        Transform previousLeft = _leftSpriteTransform; // 재배치 전 좌측 역할 스프라이트 참조입니다.
        Transform previousCenter = _centerSpriteTransform; // 재배치 전 중앙 역할 스프라이트 참조입니다.
        Transform previousRight = _rightSpriteTransform; // 재배치 전 우측 역할 스프라이트 참조입니다.

        _leftSpriteTransform = previousRight;
        _centerSpriteTransform = previousLeft;
        _rightSpriteTransform = previousCenter;
    }

    /// <summary>
    /// 현재 중앙 역할 스프라이트의 루트 기준 X 앵커 위치를 계산합니다.
    /// </summary>
    private float GetCurrentCenterAnchorX()
    {
        if (_parallaxRoot == null || _centerSpriteTransform == null)
        {
            return 0f;
        }

        return _parallaxRoot.localPosition.x + _centerSpriteTransform.localPosition.x;
    }

    /// <summary>
    /// 디버그 확인용 런타임 상태값을 갱신합니다.
    /// </summary>
    private void UpdateDebugState()
    {
        _currentCenterOffsetX = GetCurrentCenterAnchorX() - _initialCenterAnchorX;
        _currentRootOffsetY = _parallaxRoot != null ? _parallaxRoot.localPosition.y - _initialRootLocalY : 0f;
    }

    /// <summary>
    /// 사용할 NetworkManager 참조를 보정하고 유효성을 반환합니다.
    /// </summary>
    private bool TryResolveNetworkManager(out NetworkManager networkManager)
    {
        if (_networkManager == null)
        {
            _networkManager = NetworkManager.Singleton;
        }

        networkManager = _networkManager;
        return networkManager != null;
    }
}
