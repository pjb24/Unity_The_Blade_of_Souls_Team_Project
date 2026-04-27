using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Trigger 범위 안의 로컬 플레이어 상호작용 입력으로 스테이지 선택 UI 열기를 요청합니다.
/// </summary>
[DisallowMultipleComponent]
public class StageSelectionInteractionTrigger : MonoBehaviour
{
    private enum PlayerResolveMode
    {
        ColliderHierarchy = 0,
        NetworkOwner = 1,
        Tag = 2
    }

    [Header("Target Filter")]
    [Tooltip("상호작용 가능한 대상 레이어입니다.")]
    [SerializeField] private LayerMask _interactableLayerMask = ~0; // Trigger에 들어온 대상의 레이어 필터입니다.

    [Tooltip("Trigger에 들어온 Collider가 플레이어인지 판별하는 방식입니다.")]
    [SerializeField] private PlayerResolveMode _playerResolveMode = PlayerResolveMode.NetworkOwner; // 로컬 플레이어 판별 전략입니다.

    [Tooltip("Tag 방식 판별을 사용할 때 비교할 플레이어 Tag입니다.")]
    [SerializeField] private string _playerTag = "Player"; // Tag 기반 플레이어 판별에 사용할 태그입니다.

    [Header("Input")]
    [Tooltip("스테이지 선택 UI 열기에 사용할 상호작용 Input Action입니다.")]
    [SerializeField] private InputActionReference _interactAction; // Trigger 범위 내에서 UI 열기를 요청할 입력 액션입니다.

    [Header("Dependencies")]
    [Tooltip("열기 요청을 전달할 StageSelectionUIController입니다.")]
    [SerializeField] private StageSelectionUIController _stageSelectionController; // 실제 UI 열림/동기화를 담당하는 컨트롤러입니다.

    private Collider _triggerCollider3D; // 3D Trigger 설정 검증에 사용할 Collider 참조입니다.
    private Collider2D _triggerCollider2D; // 2D Trigger 설정 검증에 사용할 Collider 참조입니다.
    private GameObject _currentLocalPlayer; // 현재 범위 안에서 상호작용 가능한 로컬 플레이어입니다.
    private bool _warnedMissingInput; // 입력 소스 누락 Warning 중복 출력을 막기 위한 상태입니다.

    /// <summary>
    /// Collider와 Controller 참조를 초기화하고 Trigger 설정을 검증합니다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        ValidateTriggerCollider();
    }

    /// <summary>
    /// 상호작용 입력을 활성화합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_interactAction != null && _interactAction.action != null)
        {
            _interactAction.action.Enable();
        }
    }

    /// <summary>
    /// 상호작용 입력 상태와 현재 로컬 플레이어 참조를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        _currentLocalPlayer = null;
    }

    /// <summary>
    /// 로컬 플레이어가 범위 안에 있을 때 상호작용 입력을 감지해 UI 열기를 요청합니다.
    /// </summary>
    private void Update()
    {
        if (_currentLocalPlayer == null)
        {
            return;
        }

        if (!WasInteractPressedThisFrame())
        {
            return;
        }

        if (_stageSelectionController == null)
        {
            Debug.LogWarning("[StageSelectionInteractionTrigger] StageSelectionUIController is missing. Open request ignored.", this);
            return;
        }

        _stageSelectionController.RequestOpenFromInteraction(_currentLocalPlayer);
    }

    /// <summary>
    /// Trigger에 들어온 대상이 로컬 플레이어인지 판정하고 상호작용 가능 상태로 등록합니다.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!TryResolveInteractablePlayer(other, out GameObject playerObject))
        {
            return;
        }

        _currentLocalPlayer = playerObject;
    }

    /// <summary>
    /// Trigger에서 나간 대상이 현재 로컬 플레이어이면 상호작용 가능 상태를 해제합니다.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (_currentLocalPlayer == null)
        {
            return;
        }

        if (!TryResolveInteractablePlayer(other, out GameObject playerObject))
        {
            return;
        }

        if (playerObject == _currentLocalPlayer)
        {
            _currentLocalPlayer = null;
        }
    }

    /// <summary>
    /// 2D Trigger에 들어온 대상이 로컬 플레이어인지 판정하고 상호작용 가능 상태로 등록합니다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryResolveInteractablePlayer(other, out GameObject playerObject))
        {
            return;
        }

        _currentLocalPlayer = playerObject;
    }

    /// <summary>
    /// 2D Trigger에서 나간 대상이 현재 로컬 플레이어이면 상호작용 가능 상태를 해제합니다.
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        if (_currentLocalPlayer == null)
        {
            return;
        }

        if (!TryResolveInteractablePlayer(other, out GameObject playerObject))
        {
            return;
        }

        if (playerObject == _currentLocalPlayer)
        {
            _currentLocalPlayer = null;
        }
    }

    /// <summary>
    /// 입력 액션 또는 폴백 없이 이번 프레임 상호작용 입력이 발생했는지 확인합니다.
    /// </summary>
    private bool WasInteractPressedThisFrame()
    {
        if (_interactAction != null && _interactAction.action != null)
        {
            return _interactAction.action.WasPerformedThisFrame();
        }

        if (!_warnedMissingInput)
        {
            _warnedMissingInput = true;
            Debug.LogWarning("[StageSelectionInteractionTrigger] Interact InputActionReference is missing. Interaction cannot open stage selection UI.", this);
        }

        return false;
    }

    /// <summary>
    /// Collider가 상호작용 대상 레이어와 플레이어 판별 조건을 만족하는지 확인합니다.
    /// </summary>
    private bool TryResolveInteractablePlayer(Component candidate, out GameObject playerObject)
    {
        playerObject = null;

        if (candidate == null)
        {
            Debug.LogWarning("[StageSelectionInteractionTrigger] Null collider entered trigger.", this);
            return false;
        }

        if (((1 << candidate.gameObject.layer) & _interactableLayerMask.value) == 0)
        {
            return false;
        }

        switch (_playerResolveMode)
        {
            case PlayerResolveMode.ColliderHierarchy:
                playerObject = ResolveByHierarchy(candidate);
                return playerObject != null;
            case PlayerResolveMode.NetworkOwner:
                playerObject = ResolveByNetworkOwner(candidate);
                return playerObject != null;
            case PlayerResolveMode.Tag:
                playerObject = ResolveByTag(candidate);
                return playerObject != null;
            default:
                Debug.LogWarning($"[StageSelectionInteractionTrigger] Unsupported player resolve mode: {_playerResolveMode}", this);
                return false;
        }
    }

    /// <summary>
    /// Collider 계층에서 PlayerNetworkRoot 또는 PlayerMovement를 찾아 플레이어를 판별합니다.
    /// </summary>
    private GameObject ResolveByHierarchy(Component candidate)
    {
        PlayerNetworkRoot networkRoot = candidate.GetComponentInParent<PlayerNetworkRoot>();
        if (networkRoot != null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !networkRoot.IsOwner)
            {
                return null;
            }

            return networkRoot.gameObject;
        }

        PlayerMovement movement = candidate.GetComponentInParent<PlayerMovement>();
        if (movement != null)
        {
            return movement.gameObject;
        }

        return null;
    }

    /// <summary>
    /// NetworkObject 소유권을 기준으로 로컬 플레이어를 판별합니다.
    /// </summary>
    private GameObject ResolveByNetworkOwner(Component candidate)
    {
        NetworkObject networkObject = candidate.GetComponentInParent<NetworkObject>();
        if (networkObject == null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning($"[StageSelectionInteractionTrigger] NetworkOwner mode requires NetworkObject. collider={candidate.name}", this);
                return null;
            }

            return ResolveByHierarchy(candidate);
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !networkObject.IsOwner)
        {
            return null;
        }

        return networkObject.gameObject;
    }

    /// <summary>
    /// Tag 비교를 기준으로 플레이어를 판별합니다.
    /// </summary>
    private GameObject ResolveByTag(Component candidate)
    {
        Transform current = candidate.transform; // Tag를 검사할 현재 계층 Transform입니다.
        while (current != null)
        {
            if (current.CompareTag(_playerTag))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }

    /// <summary>
    /// 필수 참조를 가능한 범위에서 자동 보정합니다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_triggerCollider3D == null)
        {
            _triggerCollider3D = GetComponent<Collider>();
        }

        if (_triggerCollider2D == null)
        {
            _triggerCollider2D = GetComponent<Collider2D>();
        }

        if (_stageSelectionController == null)
        {
            _stageSelectionController = FindAnyObjectByType<StageSelectionUIController>();
        }
    }

    /// <summary>
    /// Collider가 Trigger로 설정되어 있는지 확인하고 아니면 Warning을 남깁니다.
    /// </summary>
    private void ValidateTriggerCollider()
    {
        if (_triggerCollider3D == null && _triggerCollider2D == null)
        {
            Debug.LogWarning("[StageSelectionInteractionTrigger] Collider or Collider2D is missing.", this);
            return;
        }

        if (_triggerCollider3D != null && !_triggerCollider3D.isTrigger)
        {
            Debug.LogWarning("[StageSelectionInteractionTrigger] Collider is not marked as Trigger. Interaction range will not work as expected.", this);
        }

        if (_triggerCollider2D != null && !_triggerCollider2D.isTrigger)
        {
            Debug.LogWarning("[StageSelectionInteractionTrigger] Collider2D is not marked as Trigger. Interaction range will not work as expected.", this);
        }
    }

    /// <summary>
    /// Inspector 편집 시 Collider와 Controller 참조를 자동 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
    }
}
