using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ActionSystem과 HitSystem을 연결해 실제 공격 판정과 HitRequest 전송을 수행하는 공용 실행기입니다.
/// </summary>
public class AttackExecutor : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("플레이어의 좌우 바라보는 방향을 공격 판정 오프셋에 반영하기 위한 PlayerMovement 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 플레이어 공격 판정 오프셋을 바라보는 방향 기준으로 변환하기 위한 방향 상태 참조입니다.
    [Tooltip("PlayerMovement가 없을 때 방향을 추정할 시각 루트 Transform입니다. X 스케일이 음수이면 왼쪽으로 해석합니다.")]
    [SerializeField] private Transform _facingVisualTarget; // PlayerMovement를 찾지 못한 경우 사용할 시각 루트 방향 폴백 참조입니다.
    [Tooltip("PlayerMovement/시각 루트가 없을 때 SpriteRenderer.flipX로 방향을 추정하기 위한 참조입니다. flipX가 true이면 왼쪽으로 해석합니다.")]
    [SerializeField] private SpriteRenderer _facingSpriteRenderer; // 마지막 방향 폴백으로 사용할 SpriteRenderer 참조입니다.
    [SerializeField] private ActionController _actionController; // 현재 액터의 액션 상태/윈도우를 조회할 ActionController 참조입니다.
    [Tooltip("네트워크 실행 권한(서버/소유권) 판정을 위한 NetworkObject 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private NetworkObject _networkObject; // 네트워크 실행 권한(서버/소유권) 판정을 위한 NetworkObject 참조입니다.

    [Header("Action -> AttackSpec Mapping")]
    [SerializeField] private AttackActionMap[] _actionMaps; // 액션 타입별 공격 스펙 매핑 배열입니다.

    [Header("Execution")]
    [SerializeField] private bool _autoExecuteOnHitWindowOpen = true; // HitWindow가 열릴 때 자동으로 현재 액션 공격을 1회 실행할지 여부입니다.
    [Tooltip("네트워크 스폰 상태에서는 서버 인스턴스에서만 공격 판정을 수행할지 여부입니다.")]
    [SerializeField] private bool _executeOnlyOnServerWhenSpawned = true; // 네트워크 스폰 상태에서는 서버 인스턴스에서만 공격 판정을 실행할지 여부입니다.
    [Tooltip("NetworkObject/NetworkManager 누락으로 서버 권한 판정을 확정할 수 없을 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenNetworkAuthorityUnavailable = true; // NetworkObject/NetworkManager 미구성으로 권한 판정을 확정할 수 없을 때 경고를 출력할지 여부입니다.
    [SerializeField] private bool _drawGizmos; // 씬 뷰에서 마지막 판정 영역을 Gizmos로 시각화할지 여부입니다.

    [Tooltip("PlayerMovement를 찾지 못해 보조 방향 소스를 사용할 때 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenFacingDirectionFallbackUsed = true; // PlayerMovement 방향값 대신 폴백 방향값을 사용할 때 경고를 출력할지 여부입니다.
    [Tooltip("씬 선택 상태에서 ActionMap에 등록된 모든 AttackSpec 범위를 현재 바라보는 방향 기준으로 미리 그릴지 여부입니다.")]
    [SerializeField] private bool _drawConfiguredAttackSpecGizmos = true; // 씬 디버그용으로 등록된 공격 스펙 범위를 방향 보정 후 표시할지 여부입니다.

    [Header("Player SFX")]
    [Tooltip("플레이어 공격 HitWindow가 열릴 때 공격 SFX를 재생할지 여부입니다.")]
    [SerializeField] private bool _playPlayerAttackSfxOnHitWindowOpen = true; // 플레이어 공격 판정 시작 시점에 SFX를 재생할지 여부입니다.
    [Tooltip("공격 SFX를 재생할 플레이어 태그 이름입니다.")]
    [SerializeField] private string _playerTagForAttackSfx = "Player"; // 플레이어 오브젝트만 공격 SFX를 재생하기 위한 태그 필터입니다.
    [Tooltip("기본 플레이어 공격 액션에서 재생할 SFX SoundId입니다.")]
    [SerializeField] private E_SoundId _playerAttackSfx = E_SoundId.SFX_Player_Attack_01; // 기본 공격에 사용할 SFX SoundId입니다.
    [Tooltip("플레이어 1단 콤보 공격 액션에서 재생할 SFX SoundId입니다.")]
    [SerializeField] private E_SoundId _playerAttackCombo1Sfx = E_SoundId.SFX_Player_Attack_02; // 1단 콤보 공격에 사용할 SFX SoundId입니다.
    [Tooltip("플레이어 2단 이상 콤보 공격 액션에서 재생할 SFX SoundId입니다. None이면 기본 공격 SFX를 사용합니다.")]
    [SerializeField] private E_SoundId _playerAttackCombo2OrHigherSfx = E_SoundId.None; // 2단 이상 콤보 공격에 사용할 SFX SoundId입니다.
    [Tooltip("같은 공격 실행에서 SFX가 중복 재생되는 것을 막기 위한 최소 간격입니다.")]
    [SerializeField] private float _playerAttackSfxMinInterval = 0.05f; // 공격 SFX 중복 재생 방지를 위한 최소 시간 간격입니다.

    private readonly Dictionary<E_ActionType, AttackSpec> _specMap = new Dictionary<E_ActionType, AttackSpec>(); // 런타임 빠른 조회를 위한 액션-스펙 딕셔너리입니다.
    private readonly HashSet<int> _hitTargetsInCurrentSwing = new HashSet<int>(); // 현재 공격 실행에서 이미 타격한 타겟 InstanceId 집합입니다.
    private readonly List<HitReceiver> _targetBuffer = new List<HitReceiver>(); // 타겟 탐지 결과를 임시 보관하는 버퍼 목록입니다.
    private readonly List<IAttackExecutionListener> _listeners = new List<IAttackExecutionListener>(); // 공격 판정 결과를 전달할 리스너 목록입니다.
    private readonly List<IAttackDamageModifierProvider> _damageModifierProviders = new List<IAttackDamageModifierProvider>(); // 최종 데미지 계산 시 사용할 수정자 제공자 목록입니다.
    private readonly List<MonoBehaviour> _damageModifierComponentBuffer = new List<MonoBehaviour>(); // 하위 계층에서 데미지 수정자 후보를 수집할 임시 버퍼입니다.
    private bool _isDamageModifierCacheDirty = true; // 데미지 수정자 캐시를 재구성해야 하는지 여부입니다.

    private int _currentSwingExecutionId = -1; // 현재 공격 실행 식별자(중복 타격 방지 스코프 구분용)입니다.
    private E_ActionType _currentSwingActionType = E_ActionType.None; // 현재 공격 실행에 대응하는 액션 타입입니다.
    private int _currentSwingHitSerial; // 현재 스윙 내에서 HitRequest 고유성 보장을 위해 증가시키는 시리얼 값입니다.
    private Vector3 _lastGizmoCenter; // 마지막 탐지 중심 좌표(Gizmos 표시용)입니다.
    private Vector2 _lastGizmoBoxSize = Vector2.one; // 마지막 박스 크기(Gizmos 표시용)입니다.
    private float _lastGizmoRadius = 0.5f; // 마지막 원형 반경(Gizmos 표시용)입니다.
    private E_AttackAreaType _lastGizmoAreaType = E_AttackAreaType.Circle; // 마지막 판정 도형 타입(Gizmos 표시용)입니다.
    private bool _hasLastGizmoSnapshot; // 런타임에서 실제로 수행된 마지막 판정 스냅샷이 있는지 여부입니다.
    private int _lastAttackSfxExecutionId = -1; // 마지막으로 공격 SFX를 재생한 액션 실행 ID입니다.
    private float _nextPlayerAttackSfxPlayableTime; // 다음 플레이어 공격 SFX 재생이 가능한 시간입니다.
    private bool _didWarnFacingVisualFallback; // 시각 루트 방향 폴백 경고가 이미 출력되었는지 추적합니다.
    private bool _didWarnFacingSpriteFallback; // SpriteRenderer 방향 폴백 경고가 이미 출력되었는지 추적합니다.
    private bool _didWarnFacingDefaultFallback; // 기본 오른쪽 방향 폴백 경고가 이미 출력되었는지 추적합니다.

    /// <summary>
    /// 의존성 보정과 액션-공격 매핑 초기화를 수행합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveActionController();
        TryResolvePlayerMovement();
        TryResolveNetworkObject();
        TryResolveFacingFallbackReferences();
        RebuildSpecMap();
        RebuildDamageModifierProviderCache();
    }

    /// <summary>
    /// 활성화 시 HitWindow 이벤트를 구독해 자동 공격 실행을 준비합니다.
    /// </summary>
    private void OnEnable()
    {
        _isDamageModifierCacheDirty = true;

        if (_actionController == null)
        {
            return;
        }

        _actionController.OnHitWindowChanged += HandleHitWindowChanged;
    }

    /// <summary>
    /// 비활성화 시 HitWindow 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_actionController == null)
        {
            return;
        }

        _actionController.OnHitWindowChanged -= HandleHitWindowChanged;
    }

    /// <summary>
    /// 공격 판정 결과 리스너를 등록합니다.
    /// </summary>
    public void AddListener(IAttackExecutionListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[AttackExecutor] Cannot add null listener.");
            return;
        }

        if (_listeners.Contains(listener))
        {
            Debug.LogWarning("[AttackExecutor] Duplicate listener registration ignored.");
            return;
        }

        _listeners.Add(listener);
    }

    /// <summary>
    /// 공격 판정 결과 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(IAttackExecutionListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[AttackExecutor] Cannot remove null listener.");
            return;
        }

        if (_listeners.Remove(listener) == false)
        {
            Debug.LogWarning("[AttackExecutor] Tried to remove unknown listener.");
        }
    }

    /// <summary>
    /// 현재 액션 타입에 대응하는 공격 실행을 수동으로 시도합니다.
    /// </summary>
    public bool TryExecuteCurrentActionAttack()
    {
        if (!TryResolveActionController())
        {
            Debug.LogWarning($"[AttackExecutor] Missing ActionController on {name}.");
            return false;
        }

        if (!_actionController.Runtime.IsRunning)
        {
            Debug.LogWarning($"[AttackExecutor] No running action on {name}. Attack execution skipped.");
            return false;
        }

        E_ActionType currentActionType = _actionController.Runtime.ActionType;
        return TryExecuteActionAttack(currentActionType, _actionController.Runtime.ExecutionId);
    }

    /// <summary>
    /// 지정 액션 타입과 실행 식별자로 공격 실행을 시도합니다.
    /// </summary>
    public bool TryExecuteActionAttack(E_ActionType actionType, int executionId)
    {
        if (!TryResolveActionController())
        {
            Debug.LogWarning($"[AttackExecutor] Missing ActionController on {name}.");
            return false;
        }

        if (!CanExecuteAttackSimulation())
        {
            return false;
        }

        if (!_actionController.IsHitWindowOpen)
        {
            Debug.LogWarning($"[AttackExecutor] HitWindow is closed. action={actionType}, actor={name}");
            return false;
        }

        if (!TryGetAttackSpec(actionType, out AttackSpec attackSpec))
        {
            Debug.LogWarning($"[AttackExecutor] No AttackSpec mapped for action={actionType} on {name}.");
            return false;
        }

        BeginSwingScopeIfNeeded(actionType, executionId);

        _targetBuffer.Clear();
        CollectTargets(attackSpec, _targetBuffer);

        if (_targetBuffer.Count == 0)
        {
            Debug.Log($"[AttackExecutor] No targets found. action={actionType}, spec={attackSpec.AttackTypeId}, actor={name}");
            return false;
        }

        bool didSendAnyHit = false;
        for (int i = 0; i < _targetBuffer.Count; i++)
        {
            HitReceiver receiver = _targetBuffer[i];
            if (receiver == null)
            {
                continue;
            }

            int targetId = receiver.gameObject.GetInstanceID();
            if (!attackSpec.AllowMultiHitPerSwing && _hitTargetsInCurrentSwing.Contains(targetId))
            {
                continue;
            }

            HitRequest request = BuildHitRequest(attackSpec, actionType, executionId, receiver, _currentSwingHitSerial);
            _currentSwingHitSerial++;
            HitResult result = receiver.ReceiveHit(request);
            NotifyAttackExecuted(actionType, executionId, receiver, result);

            if (result.IsAccepted)
            {
                _hitTargetsInCurrentSwing.Add(targetId);
                didSendAnyHit = true;
            }
        }

        return didSendAnyHit;
    }

    /// <summary>
    /// 현재 런타임 권한 정책 기준으로 공격 판정 시뮬레이션 실행 가능 여부를 판정합니다.
    /// </summary>
    private bool CanExecuteAttackSimulation()
    {
        if (!_executeOnlyOnServerWhenSpawned)
        {
            return true;
        }

        if (_networkObject == null)
        {
            TryResolveNetworkObject();
        }

        if (_networkObject == null)
        {
            if (_warnWhenNetworkAuthorityUnavailable)
            {
                Debug.LogWarning($"[AttackExecutor] NetworkObject가 없어 서버 권한 판정을 수행할 수 없습니다. object={name}");
            }

            return true;
        }

        if (!_networkObject.IsSpawned)
        {
            return true;
        }

        if (NetworkManager.Singleton == null)
        {
            if (_warnWhenNetworkAuthorityUnavailable)
            {
                Debug.LogWarning($"[AttackExecutor] NetworkManager.Singleton이 없어 서버 권한 판정을 확정할 수 없습니다. object={name}");
            }

            return true;
        }

        return NetworkManager.Singleton.IsServer;
    }

    /// <summary>
    /// HitWindow 상태 변경 이벤트를 받아 자동 실행 설정일 때 공격을 트리거합니다.
    /// </summary>
    private void HandleHitWindowChanged(bool isOpen)
    {
        if (!_autoExecuteOnHitWindowOpen)
        {
            return;
        }

        if (!isOpen)
        {
            return;
        }

        TryPlayPlayerAttackSfxForCurrentAction();
        TryExecuteCurrentActionAttack();
    }

    /// <summary>
    /// 플레이어 공격 HitWindow가 열릴 때 현재 액션에 맞는 공격 SFX 재생을 시도합니다.
    /// </summary>
    private void TryPlayPlayerAttackSfxForCurrentAction()
    {
        if (_playPlayerAttackSfxOnHitWindowOpen == false)
        {
            return;
        }

        if (!TryResolveActionController())
        {
            return;
        }

        if (_actionController.Runtime.IsRunning == false)
        {
            return;
        }

        if (IsPlayerAttackSfxTarget() == false)
        {
            return;
        }

        int executionId = _actionController.Runtime.ExecutionId;
        if (_lastAttackSfxExecutionId == executionId)
        {
            return;
        }

        if (Time.unscaledTime < _nextPlayerAttackSfxPlayableTime)
        {
            return;
        }

        E_SoundId soundId = ResolvePlayerAttackSfxId(_actionController.Runtime.ActionType);
        if (soundId == E_SoundId.None)
        {
            return;
        }

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            Debug.LogWarning($"[AttackExecutor] AudioManager not found. Player attack SFX skipped. actor={name}");
            return;
        }

        _lastAttackSfxExecutionId = executionId;
        _nextPlayerAttackSfxPlayableTime = Time.unscaledTime + Mathf.Max(0f, _playerAttackSfxMinInterval);
        audioManager.PlaySfx(soundId, transform);
    }

    /// <summary>
    /// 이 AttackExecutor가 플레이어 공격 SFX 재생 대상인지 검사합니다.
    /// </summary>
    private bool IsPlayerAttackSfxTarget()
    {
        if (string.IsNullOrWhiteSpace(_playerTagForAttackSfx))
        {
            return true;
        }

        if (CompareTag(_playerTagForAttackSfx))
        {
            return true;
        }

        Transform root = transform.root;
        return root != null && root.CompareTag(_playerTagForAttackSfx);
    }

    /// <summary>
    /// 현재 공격 액션 타입에 대응하는 플레이어 공격 SFX SoundId를 반환합니다.
    /// </summary>
    private E_SoundId ResolvePlayerAttackSfxId(E_ActionType actionType)
    {
        switch (actionType)
        {
            case E_ActionType.AttackCombo1:
                return _playerAttackCombo1Sfx != E_SoundId.None ? _playerAttackCombo1Sfx : _playerAttackSfx;
            case E_ActionType.AttackCombo2:
            case E_ActionType.AttackCombo3:
                return _playerAttackCombo2OrHigherSfx != E_SoundId.None ? _playerAttackCombo2OrHigherSfx : _playerAttackSfx;
            case E_ActionType.Attack:
            case E_ActionType.AttackAir:
            case E_ActionType.AttackDash:
            case E_ActionType.AttackWall:
                return _playerAttackSfx;
            default:
                return E_SoundId.None;
        }
    }

    /// <summary>
    /// 액션 타입별 공격 스펙 딕셔너리를 재구성합니다.
    /// </summary>
    private void RebuildSpecMap()
    {
        _specMap.Clear();

        if (_actionMaps == null || _actionMaps.Length == 0)
        {
            Debug.LogWarning($"[AttackExecutor] Action map is empty on {name}.");
            return;
        }

        for (int i = 0; i < _actionMaps.Length; i++)
        {
            AttackActionMap map = _actionMaps[i];
            if (map.AttackSpec == null)
            {
                Debug.LogWarning($"[AttackExecutor] Null AttackSpec in action map index={i} on {name}.");
                continue;
            }

            _specMap[map.ActionType] = map.AttackSpec;
        }
    }

    /// <summary>
    /// 지정 액션 타입에 대응하는 AttackSpec을 조회합니다.
    /// </summary>
    private bool TryGetAttackSpec(E_ActionType actionType, out AttackSpec attackSpec)
    {
        if (_specMap.Count == 0)
        {
            RebuildSpecMap();
        }

        return _specMap.TryGetValue(actionType, out attackSpec);
    }

    /// <summary>
    /// NetworkObject 참조가 비어 있으면 동일 오브젝트 기준으로 자동 보정을 시도합니다.
    /// </summary>
    private void TryResolveNetworkObject()
    {
        if (_networkObject != null)
        {
            return;
        }

        _networkObject = GetComponent<NetworkObject>();
    }

    /// <summary>
    /// 액션 실행 경계가 바뀌었을 때 스윙 스코프를 갱신하고 중복 타격 기록을 초기화합니다.
    /// </summary>
    private void BeginSwingScopeIfNeeded(E_ActionType actionType, int executionId)
    {
        if (_currentSwingExecutionId == executionId && _currentSwingActionType == actionType)
        {
            return;
        }

        _currentSwingExecutionId = executionId;
        _currentSwingActionType = actionType;
        _currentSwingHitSerial = 0;
        _hitTargetsInCurrentSwing.Clear();
    }

    /// <summary>
    /// 공격 스펙의 도형/필터 규칙으로 HitReceiver 타겟 목록을 수집합니다.
    /// </summary>
    private void CollectTargets(AttackSpec attackSpec, List<HitReceiver> targets)
    {
        Vector2 adjustedLocalOffset = GetFacingAdjustedOffset(attackSpec.LocalOffset);
        Vector3 center = transform.TransformPoint(adjustedLocalOffset);
        _lastGizmoCenter = center;
        _lastGizmoAreaType = attackSpec.AreaType;
        _lastGizmoRadius = attackSpec.GetSafeRadius();
        _lastGizmoBoxSize = attackSpec.GetSafeBoxSize();
        _hasLastGizmoSnapshot = true;

        Collider2D[] colliders;
        if (attackSpec.AreaType == E_AttackAreaType.Box)
        {
            colliders = Physics2D.OverlapBoxAll(center, attackSpec.GetSafeBoxSize(), 0f, attackSpec.TargetLayerMask);
        }
        else
        {
            colliders = Physics2D.OverlapCircleAll(center, attackSpec.GetSafeRadius(), attackSpec.TargetLayerMask);
        }

        if (colliders == null || colliders.Length == 0)
        {
            return;
        }

        int safeMaxTargets = attackSpec.GetSafeMaxTargets();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (targets.Count >= safeMaxTargets)
            {
                break;
            }

            Collider2D candidateCollider = colliders[i];
            if (candidateCollider == null)
            {
                continue;
            }

            if (attackSpec.RequireTargetTag && !candidateCollider.CompareTag(attackSpec.TargetTag))
            {
                continue;
            }

            HitReceiver receiver = ResolveHitReceiver(candidateCollider);
            if (receiver == null)
            {
                continue;
            }

            if (receiver.gameObject == gameObject)
            {
                continue;
            }

            if (targets.Contains(receiver))
            {
                continue;
            }

            targets.Add(receiver);
        }
    }

    /// <summary>
    /// 충돌체 기준으로 대상 자신/부모/자식 계층 순서로 HitReceiver를 탐색합니다.
    /// </summary>
    private HitReceiver ResolveHitReceiver(Collider2D candidateCollider)
    {
        if (candidateCollider == null)
        {
            return null;
        }

        HitReceiver receiverOnSelf = candidateCollider.GetComponent<HitReceiver>(); // 충돌체가 붙은 동일 오브젝트에서 찾은 HitReceiver 참조입니다.
        if (receiverOnSelf != null)
        {
            return receiverOnSelf;
        }

        HitReceiver receiverOnParent = candidateCollider.GetComponentInParent<HitReceiver>(); // 상위 계층에서 찾은 HitReceiver 참조입니다.
        if (receiverOnParent != null)
        {
            return receiverOnParent;
        }

        HitReceiver receiverOnChild = candidateCollider.GetComponentInChildren<HitReceiver>(); // 하위 계층에서 찾은 HitReceiver 참조입니다.
        return receiverOnChild;
    }

    /// <summary>
    /// 공격 데이터의 X 오프셋을 캐릭터가 바라보는 방향 기준으로 변환합니다.
    /// </summary>
    private Vector2 GetFacingAdjustedOffset(Vector2 rawOffset)
    {
        float facingDirection = ResolveFacingDirectionSign();
        return new Vector2(rawOffset.x * facingDirection, rawOffset.y);
    }

    /// <summary>
    /// PlayerMovement를 우선 사용하고, 없을 때만 시각 루트 또는 SpriteRenderer로 좌우 방향을 추정합니다.
    /// </summary>
    private float ResolveFacingDirectionSign()
    {
        if (TryResolvePlayerMovement())
        {
            return _playerMovement.IsFacingRight ? 1f : -1f;
        }

        TryResolveFacingFallbackReferences();

        if (_facingVisualTarget != null)
        {
            WarnFacingFallbackOnce(ref _didWarnFacingVisualFallback, "Visual Target localScale.x");
            return _facingVisualTarget.localScale.x >= 0f ? 1f : -1f;
        }

        if (_facingSpriteRenderer != null)
        {
            WarnFacingFallbackOnce(ref _didWarnFacingSpriteFallback, "SpriteRenderer.flipX");
            return _facingSpriteRenderer.flipX ? -1f : 1f;
        }

        WarnFacingFallbackOnce(ref _didWarnFacingDefaultFallback, "default right direction");
        return 1f;
    }

    /// <summary>
    /// PlayerMovement 참조가 비어 있을 때 같은 오브젝트에서 자동으로 보정합니다.
    /// </summary>
    private bool TryResolvePlayerMovement()
    {
        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }

        return _playerMovement != null;
    }

    /// <summary>
    /// PlayerMovement가 없는 액터의 방향 추정을 위해 기존 시각 컴포넌트를 자동으로 보정합니다.
    /// </summary>
    private void TryResolveFacingFallbackReferences()
    {
        if (_facingSpriteRenderer == null)
        {
            _facingSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    /// <summary>
    /// 방향 폴백이 사용될 때 원인을 한 번만 경고로 남깁니다.
    /// </summary>
    private void WarnFacingFallbackOnce(ref bool didWarn, string fallbackSource)
    {
        if (!_warnWhenFacingDirectionFallbackUsed || didWarn)
        {
            return;
        }

        didWarn = true;
        Debug.LogWarning($"[AttackExecutor] PlayerMovement facing direction not found. Attack offset uses fallback source: {fallbackSource}. object={name}", this);
    }

    /// <summary>
    /// 공격 스펙, 실행 정보, 대상 정보를 조합해 HitRequest를 생성합니다.
    /// </summary>
    private HitRequest BuildHitRequest(AttackSpec attackSpec, E_ActionType actionType, int executionId, HitReceiver receiver, int hitSerial)
    {
        Vector3 attackerPosition = transform.position;
        Vector3 targetPosition = receiver.transform.position;
        Vector2 direction2D = ((Vector2)(targetPosition - attackerPosition)).normalized;

        string hitId = BuildHitId(actionType, executionId, receiver.gameObject.GetInstanceID(), hitSerial, attackSpec.AllowMultiHitPerSwing);
        float safeDamage = ResolveFinalDamage(attackSpec, actionType);

        return new HitRequest(
            hitId: hitId,
            rawDamage: safeDamage,
            attacker: gameObject,
            hitPoint: targetPosition,
            hitDirection: new Vector3(direction2D.x, direction2D.y, 0f),
            statusTag: attackSpec.StatusTag,
            requestTime: Time.time);
    }

    /// <summary>
    /// 공격 스펙의 기본 데미지에 등록된 수정자 제공자를 순차 적용해 최종 데미지를 계산합니다.
    /// </summary>
    private float ResolveFinalDamage(AttackSpec attackSpec, E_ActionType actionType)
    {
        float resolvedDamage = attackSpec.GetSafeBaseDamage(); // 공격 스펙에서 읽어온 기본 데미지 값입니다.

        if (_isDamageModifierCacheDirty)
        {
            RebuildDamageModifierProviderCache();
        }

        for (int index = 0; index < _damageModifierProviders.Count; index++)
        {
            IAttackDamageModifierProvider modifierProvider = _damageModifierProviders[index]; // 현재 적용할 데미지 수정자 제공자입니다.
            if (modifierProvider == null)
            {
                continue;
            }

            resolvedDamage = modifierProvider.ModifyDamage(resolvedDamage, attackSpec, actionType);
        }

        return Mathf.Max(0f, resolvedDamage);
    }

    /// <summary>
    /// 동일 오브젝트에 부착된 데미지 수정자 제공자 목록을 캐시합니다.
    /// </summary>
    private void RebuildDamageModifierProviderCache()
    {
        _damageModifierProviders.Clear();
        _damageModifierComponentBuffer.Clear();
        GetComponentsInChildren(true, _damageModifierComponentBuffer);

        for (int index = 0; index < _damageModifierComponentBuffer.Count; index++)
        {
            MonoBehaviour component = _damageModifierComponentBuffer[index]; // 현재 검사 중인 데미지 수정자 후보 컴포넌트입니다.
            if (component is not IAttackDamageModifierProvider provider)
            {
                continue;
            }

            _damageModifierProviders.Add(provider);
        }

        _isDamageModifierCacheDirty = false;
    }

    /// <summary>
    /// 공격 판정 결과를 리스너에게 전달합니다.
    /// </summary>
    private void NotifyAttackExecuted(E_ActionType actionType, int executionId, HitReceiver receiver, in HitResult result)
    {
        if (_listeners.Count == 0)
        {
            return;
        }

        AttackExecutionReport report = new AttackExecutionReport(actionType, executionId, receiver, result); // 리스너 전달용 공격 실행 보고서입니다.
        for (int index = 0; index < _listeners.Count; index++)
        {
            _listeners[index].OnAttackExecuted(report);
        }
    }

    /// <summary>
    /// 공격자/실행/타겟 정보를 합성해 중복 방지용 고유 HitId를 생성합니다.
    /// </summary>
    private string BuildHitId(E_ActionType actionType, int executionId, int targetId, int hitSerial, bool allowMultiHitPerSwing)
    {
        int attackerId = gameObject.GetInstanceID();
        if (allowMultiHitPerSwing)
        {
            return $"{attackerId}:{executionId}:{targetId}:{actionType}:{hitSerial}";
        }

        return $"{attackerId}:{executionId}:{targetId}:{actionType}";
    }

    /// <summary>
    /// ActionController 참조가 비어있을 때 동일 오브젝트에서 자동 보정합니다.
    /// </summary>
    private bool TryResolveActionController()
    {
        if (_actionController != null)
        {
            return true;
        }

        _actionController = GetComponent<ActionController>();
        return _actionController != null;
    }

    /// <summary>
    /// 마지막 판정 영역을 Gizmos로 표시해 디버깅 가시성을 제공합니다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!_drawGizmos)
        {
            return;
        }

        DrawConfiguredAttackSpecGizmos();
        DrawLastAttackSnapshotGizmo();
    }

    /// <summary>
    /// ActionMap에 등록된 공격 스펙 범위를 현재 바라보는 방향 기준 offset으로 씬에 미리 표시합니다.
    /// </summary>
    private void DrawConfiguredAttackSpecGizmos()
    {
        if (!_drawConfiguredAttackSpecGizmos || _actionMaps == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        for (int index = 0; index < _actionMaps.Length; index++)
        {
            AttackSpec attackSpec = _actionMaps[index].AttackSpec;
            if (attackSpec == null)
            {
                continue;
            }

            Vector3 center = transform.TransformPoint(GetFacingAdjustedOffset(attackSpec.LocalOffset));
            DrawAttackAreaGizmo(center, attackSpec.AreaType, attackSpec.GetSafeBoxSize(), attackSpec.GetSafeRadius());
        }
    }

    /// <summary>
    /// 런타임에서 마지막으로 실제 수행된 공격 판정 범위를 씬에 표시합니다.
    /// </summary>
    private void DrawLastAttackSnapshotGizmo()
    {
        if (!_hasLastGizmoSnapshot)
        {
            return;
        }

        Gizmos.color = Color.red;
        DrawAttackAreaGizmo(_lastGizmoCenter, _lastGizmoAreaType, _lastGizmoBoxSize, _lastGizmoRadius);
    }

    /// <summary>
    /// 공격 판정 도형 타입에 맞는 Gizmo 와이어 도형을 그립니다.
    /// </summary>
    private void DrawAttackAreaGizmo(Vector3 center, E_AttackAreaType areaType, Vector2 boxSize, float radius)
    {
        if (areaType == E_AttackAreaType.Box)
        {
            Gizmos.DrawWireCube(center, boxSize);
            return;
        }

        Gizmos.DrawWireSphere(center, radius);
    }
}
