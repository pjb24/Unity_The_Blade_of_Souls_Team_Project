using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 씬에 수동 배치되는 체크포인트 오브젝트입니다.
/// </summary>
[DisallowMultipleComponent]
public class Checkpoint : MonoBehaviour
{
    [Serializable]
    private sealed class StateVisualBinding
    {
        [Header("Debug")]
        [Tooltip("Inspector에서 상태 바인딩을 식별하기 위한 이름입니다.")]
        public string Name; // 디자이너 식별용 이름입니다.

        [Tooltip("이 바인딩이 대응하는 체크포인트 상태입니다.")]
        [SerializeField] private E_CheckpointState _state; // 비주얼을 적용할 체크포인트 상태입니다.

        [Tooltip("해당 상태에서 활성화할 오브젝트 목록입니다.")]
        [SerializeField] private GameObject[] _activeObjects = Array.Empty<GameObject>(); // 상태 진입 시 켜질 오브젝트입니다.

        [Header("Events")]
        [Tooltip("상태 진입 시 호출되는 이벤트입니다. (VFX, 애니메이션, 사운드 등 연결)")]
        [SerializeField] private UnityEvent _onEnter; // 상태 진입 시 실행됩니다.

        [Tooltip("상태 종료 시 호출되는 이벤트입니다. (정지, 리셋, 종료 처리)")]
        [SerializeField] private UnityEvent _onExit; // 상태 종료 시 실행됩니다.

        /// <summary>
        /// 이 바인딩이 담당하는 체크포인트 상태입니다.
        /// </summary>
        public E_CheckpointState State => _state;

        /// <summary>
        /// 상태 진입 시 비주얼과 이벤트를 적용합니다.
        /// </summary>
        public void Enter()
        {
            SetObjectsActive(true); // 오브젝트 활성화

            // 상태 진입 이벤트 실행
            _onEnter?.Invoke();
        }

        /// <summary>
        /// 상태 종료 시 비주얼과 이벤트를 해제합니다.
        /// </summary>
        public void Exit()
        {
            SetObjectsActive(false); // 오브젝트 비활성화

            // 상태 종료 이벤트 실행
            _onExit?.Invoke();
        }

        /// <summary>
        /// 이벤트 호출 없이 상태 비주얼 오브젝트의 활성화 여부만 변경합니다.
        /// </summary>
        /// <param name="isActive">비주얼 오브젝트 활성화 여부입니다.</param>
        public void SetVisualObjectsActiveOnly(bool isActive)
        {
            SetObjectsActive(isActive);
        }

        /// <summary>
        /// 연결된 오브젝트들의 활성 상태를 일괄 설정합니다.
        /// </summary>
        /// <param name="isActive">활성 여부입니다.</param>
        private void SetObjectsActive(bool isActive)
        {
            for (int i = 0; i < _activeObjects.Length; i++)
            {
                GameObject target = _activeObjects[i]; // 제어 대상 오브젝트입니다.
                if (target != null)
                {
                    target.SetActive(isActive);
                }
            }
        }
    }

    [Header("Identity")]
    [Tooltip("이 체크포인트가 속한 Stage ID입니다. 비어 있으면 StageSession 또는 Controller 값을 폴백으로 사용합니다.")]
    [SerializeField] private string _stageId; // 체크포인트가 속한 Stage ID입니다.

    [Tooltip("동일 Stage 안에서 고유해야 하는 Checkpoint ID입니다.")]
    [SerializeField] private string _checkpointId; // 동일 Stage 안에서 고유해야 하는 체크포인트 ID입니다.

    [Header("Respawn Points")]
    [Tooltip("싱글플레이 리스폰 기준 Transform입니다.")]
    [SerializeField] private Transform _respawnPointSingle; // 싱글플레이 리스폰 위치입니다.

    [Tooltip("멀티플레이 Host 리스폰 기준 Transform입니다.")]
    [SerializeField] private Transform _respawnPointHost; // 멀티플레이 Host 리스폰 위치입니다.

    [Tooltip("멀티플레이 Client 리스폰 기준 Transform입니다.")]
    [SerializeField] private Transform _respawnPointClient; // 멀티플레이 Client 리스폰 위치입니다.

    [Header("Interaction")]
    [Tooltip("InputActionReference가 비어 있을 때 InputManager.PlayerInput에서 찾을 액션 이름입니다.")]
    [SerializeField] private string _interactActionName = "Interact"; // 프리팹 입력 참조가 비어 있을 때 폴백으로 찾을 입력 액션 이름입니다.

    [Tooltip("체크포인트 상호작용에 사용할 Input Action입니다.")]
    [SerializeField] private InputActionReference _interactAction; // Trigger 범위 안에서 체크포인트 활성화를 요청할 입력 액션입니다.

    [Tooltip("상호작용 가능한 플레이어 LayerMask입니다.")]
    [SerializeField] private LayerMask _playerLayerMask = ~0; // Trigger에 진입한 플레이어 후보 필터입니다.

    [Tooltip("NetworkObject가 없는 싱글플레이 플레이어를 허용할지 여부입니다.")]
    [SerializeField] private bool _allowSinglePlayerFallback = true; // 오프라인 싱글플레이에서 NetworkObject 없이 플레이어를 해석할지 여부입니다.

    [Header("Visuals")]
    [Tooltip("상태별 비주얼 바인딩 목록입니다.")]
    [SerializeField] private StateVisualBinding[] _visualBindings = Array.Empty<StateVisualBinding>(); // Inspector에서 연결하는 상태별 비주얼 바인딩입니다.

    private CheckpointStageController _stageController; // 이 체크포인트를 관리하는 Stage 단위 컨트롤러입니다.
    private GameObject _currentInteractablePlayer; // 현재 Trigger 안에 있는 로컬 상호작용 플레이어입니다.
    private E_CheckpointState _currentState = E_CheckpointState.Inactive; // 현재 체크포인트 진행 상태입니다.
    private Collider2D _collider2D; // 2D Trigger 검증용 Collider입니다.
    private bool _warnedMissingInput; // 입력 누락 경고 중복 방지 플래그입니다.

    private InputAction _cachedResolvedInteractAction; // 프리팹/Stage/InputManager에서 해석한 상호작용 입력 액션 캐시입니다.
    private bool _hasAppliedStateVisuals; // 현재 상태 비주얼이 한 번 이상 실제로 적용되었는지 여부입니다.

    public string StageId => _stageId;
    public string CheckpointId => _checkpointId;
    public E_CheckpointState CurrentState => _currentState;

    /// <summary>
    /// 컴포넌트 참조를 초기화하고 Trigger 설정을 검증합니다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        ValidateConfiguration();
    }

    /// <summary>
    /// 활성화 시 입력 액션을 켜고 Stage Controller에 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();

        InputAction interactAction = ResolveInteractAction();
        if (interactAction != null)
        {
            interactAction.Enable();
        }

        _stageController?.RegisterCheckpoint(this);
        ApplyState(_currentState);
    }

    /// <summary>
    /// 비활성화 시 Stage Controller 등록을 해제하고 현재 플레이어 참조를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        _stageController?.UnregisterCheckpoint(this);
        _currentInteractablePlayer = null;
    }

    /// <summary>
    /// Trigger 안의 로컬 플레이어가 상호작용 입력을 누르면 Stage Controller에 요청합니다.
    /// </summary>
    private void Update()
    {
        if (_currentInteractablePlayer == null || !WasInteractPressedThisFrame())
        {
            return;
        }

        if (_stageController == null)
        {
            Debug.LogWarning($"[Checkpoint] CheckpointStageController를 찾지 못해 상호작용을 처리할 수 없습니다. checkpoint={_checkpointId}", this);
            return;
        }

        _stageController.RequestCheckpointInteraction(this, _currentInteractablePlayer);
    }

    /// <summary>
    /// 2D Trigger 진입 플레이어를 상호작용 후보로 등록합니다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        TrySetCurrentPlayer(other);
    }

    /// <summary>
    /// 2D Trigger 이탈 플레이어가 현재 후보이면 해제합니다.
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        TryClearCurrentPlayer(other);
    }

    /// <summary>
    /// Controller가 확정한 체크포인트 상태를 이전 상태 → 현재 상태 전이 방식으로 비주얼에 반영합니다.
    /// </summary>
    /// <param name="nextState">새롭게 적용할 체크포인트 상태입니다.</param>
    public void ApplyState(E_CheckpointState nextState)
    {
        bool isSameState = _currentState == nextState; // 현재 상태와 요청 상태가 같은지 여부입니다.
        if (isSameState && _hasAppliedStateVisuals)
        {
            return;
        }

        E_CheckpointState prevState = _currentState; // 이전 상태를 저장합니다.
        _currentState = nextState; // 현재 상태를 갱신합니다.

        // 1. Inspector에서 켜져 있던 다른 상태 비주얼이 남지 않도록 오브젝트 활성 상태를 먼저 정리합니다.
        for (int i = 0; i < _visualBindings.Length; i++)
        {
            StateVisualBinding binding = _visualBindings[i];
            if (binding != null && binding.State != nextState)
            {
                binding.SetVisualObjectsActiveOnly(false);
            }
        }

        // 2. 실제 상태가 바뀐 경우에만 이전 상태 Exit 이벤트를 호출합니다.
        if (!isSameState)
        {
            for (int i = 0; i < _visualBindings.Length; i++)
            {
                StateVisualBinding binding = _visualBindings[i];
                if (binding != null && binding.State == prevState)
                {
                    binding.Exit(); // 이전 상태 Exit 처리
                }
            }
        }

        // 3. 현재 상태에 해당하는 비주얼을 시작합니다.
        for (int i = 0; i < _visualBindings.Length; i++)
        {
            StateVisualBinding binding = _visualBindings[i];
            if (binding != null && binding.State == nextState)
            {
                binding.Enter(); // 현재 상태 Enter 처리
            }
        }

        _hasAppliedStateVisuals = true;
    }

    /// <summary>
    /// 플레이 모드와 소유자 역할에 맞는 리스폰 Transform을 반환합니다.
    /// </summary>
    public Transform ResolveRespawnPoint(NetworkManager networkManager, ulong clientId)
    {
        Transform selected = _respawnPointSingle; // 기본 싱글플레이 리스폰 포인트입니다.
        if (networkManager != null && networkManager.IsListening)
        {
            selected = clientId == NetworkManager.ServerClientId ? _respawnPointHost : _respawnPointClient;
        }

        if (selected == null)
        {
            Debug.LogWarning($"[Checkpoint] RespawnPoint가 누락되어 체크포인트 Root Transform을 폴백으로 사용합니다. checkpoint={_checkpointId}, clientId={clientId}", this);
            return transform;
        }

        return selected;
    }

    /// <summary>
    /// Stage ID가 비어 있을 때 외부 컨트롤러 값으로 보정합니다.
    /// </summary>
    public void ApplyStageIdFallback(string fallbackStageId)
    {
        if (!string.IsNullOrWhiteSpace(_stageId))
        {
            return;
        }

        _stageId = fallbackStageId ?? string.Empty;
        Debug.LogWarning($"[Checkpoint] Stage ID가 비어 있어 Controller Stage ID를 폴백으로 사용합니다. checkpoint={_checkpointId}, fallback={_stageId}", this);
    }

    /// <summary>
    /// Trigger에 들어온 Component가 로컬 플레이어이면 현재 상호작용 후보로 저장합니다.
    /// </summary>
    private void TrySetCurrentPlayer(Component candidate)
    {
        if (!TryResolveLocalPlayer(candidate, out GameObject playerObject))
        {
            return;
        }

        _currentInteractablePlayer = playerObject;
    }

    /// <summary>
    /// Trigger에서 나간 Component가 현재 플레이어이면 후보를 해제합니다.
    /// </summary>
    private void TryClearCurrentPlayer(Component candidate)
    {
        if (_currentInteractablePlayer == null || !TryResolveLocalPlayer(candidate, out GameObject playerObject))
        {
            return;
        }

        if (playerObject == _currentInteractablePlayer)
        {
            _currentInteractablePlayer = null;
        }
    }

    /// <summary>
    /// Collider 계층에서 로컬 플레이어 GameObject를 해석합니다.
    /// </summary>
    private bool TryResolveLocalPlayer(Component candidate, out GameObject playerObject)
    {
        playerObject = null;
        if (candidate == null)
        {
            Debug.LogWarning($"[Checkpoint] null Collider가 Trigger에 진입했습니다. checkpoint={_checkpointId}", this);
            return false;
        }

        if (((1 << candidate.gameObject.layer) & _playerLayerMask.value) == 0)
        {
            return false;
        }

        NetworkObject networkObject = candidate.GetComponentInParent<NetworkObject>();
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            if (networkObject == null)
            {
                Debug.LogWarning($"[Checkpoint] 멀티플레이에서 NetworkObject 없는 플레이어 후보를 무시합니다. collider={candidate.name}", this);
                return false;
            }

            if (!networkObject.IsOwner)
            {
                return false;
            }

            playerObject = networkObject.gameObject;
            return true;
        }

        if (networkObject != null)
        {
            playerObject = networkObject.gameObject;
            return true;
        }

        PlayerMovement movement = candidate.GetComponentInParent<PlayerMovement>();
        if (movement != null && _allowSinglePlayerFallback)
        {
            playerObject = movement.gameObject;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 상호작용 입력이 이번 프레임에 발생했는지 확인합니다.
    /// </summary>
    private bool WasInteractPressedThisFrame()
    {
        InputAction interactAction = ResolveInteractAction();
        if (interactAction != null)
        {
            return interactAction.WasPerformedThisFrame();
        }

        if (!_warnedMissingInput)
        {
            _warnedMissingInput = true;
            Debug.LogWarning($"[Checkpoint] Interact Action을 찾지 못했습니다. Prefab InputActionReference, StageController Shared Action, InputManager actionName={_interactActionName} 설정을 확인하세요. checkpoint={_checkpointId}", this);
        }

        return false;
    }

    /// <summary>
    /// 필요한 참조를 자동으로 보정합니다.
    /// </summary>
    /// <summary>
    /// 프리팹 로컬 참조, Stage 공용 참조, InputManager 액션 이름 순서로 상호작용 입력을 해석합니다.
    /// </summary>
    private InputAction ResolveInteractAction()
    {
        if (_interactAction != null && _interactAction.action != null)
        {
            return _interactAction.action;
        }

        if (_cachedResolvedInteractAction != null)
        {
            return _cachedResolvedInteractAction;
        }

        if (_stageController != null && _stageController.TryGetSharedInteractAction(out InputAction sharedAction))
        {
            _cachedResolvedInteractAction = sharedAction;
            return _cachedResolvedInteractAction;
        }

        if (InputManager.PlayerInput != null && !string.IsNullOrWhiteSpace(_interactActionName))
        {
            _cachedResolvedInteractAction = InputManager.PlayerInput.actions.FindAction(_interactActionName, false);
            if (_cachedResolvedInteractAction != null)
            {
                return _cachedResolvedInteractAction;
            }
        }

        return null;
    }

    private void ResolveReferences()
    {
        if (_stageController == null)
        {
            _stageController = GetComponentInParent<CheckpointStageController>();
        }

        if (_stageController == null || !CanUseStageController(_stageController))
        {
            _stageController = FindStageControllerInScene();
        }

        if (_collider2D == null)
        {
            _collider2D = GetComponent<Collider2D>();
        }
    }

    /// <summary>
    /// 씬 안에서 현재 Checkpoint의 Stage ID와 맞는 StageController를 찾습니다.
    /// </summary>
    private CheckpointStageController FindStageControllerInScene()
    {
        CheckpointStageController[] controllers = FindObjectsByType<CheckpointStageController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 씬에서 찾은 StageController 후보입니다.
        for (int i = 0; i < controllers.Length; i++)
        {
            CheckpointStageController controller = controllers[i]; // Stage ID를 비교할 Controller 후보입니다.
            if (CanUseStageController(controller))
            {
                return controller;
            }
        }

        return null;
    }

    /// <summary>
    /// 지정한 StageController가 현재 Checkpoint를 관리할 수 있는지 확인합니다.
    /// </summary>
    /// <param name="controller">관리 가능 여부를 확인할 StageController입니다.</param>
    private bool CanUseStageController(CheckpointStageController controller)
    {
        if (controller == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_stageId))
        {
            return true;
        }

        return string.Equals(controller.StageId, _stageId, StringComparison.Ordinal);
    }

    /// <summary>
    /// 필수 설정 누락을 Warning으로 보고합니다.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_checkpointId))
        {
            Debug.LogWarning($"[Checkpoint] Checkpoint ID가 비어 있습니다. object={name}", this);
        }

        if (string.IsNullOrWhiteSpace(_stageId))
        {
            Debug.LogWarning($"[Checkpoint] Stage ID가 비어 있습니다. Controller 또는 StageSession 폴백을 시도합니다. object={name}", this);
        }

        if (_collider2D != null && !_collider2D.isTrigger)
        {
            Debug.LogWarning($"[Checkpoint] Collider2D가 Trigger로 설정되지 않았습니다. checkpoint={_checkpointId}", this);
        }
    }

    /// <summary>
    /// Inspector 변경 시 자동 참조를 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        _cachedResolvedInteractAction = null;
        ResolveReferences();
    }
}
