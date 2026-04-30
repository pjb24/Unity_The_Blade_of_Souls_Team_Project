using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기존 타겟 탐지를 먼저 사용하고, 필요할 때만 재사용 가능한 fallback 탐색을 사용하여 보스 패턴 실행에 유효한 Player 타겟을 찾는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossPlayerTargetProvider : MonoBehaviour
{
    [Header("필수 참조")]
    [Tooltip("Player 탐색 실행 전에 권한 확인에 사용하는 보스 컨트롤러")]
    [SerializeField] private BossController _bossController; // 클라이언트가 Player 타겟을 결정하지 못하게 막는 권한 소스

    [Tooltip("사용 가능한 경우 첫 번째 Player 타겟 탐색 소스로 재사용할 기존 EnemyTargetDetector")]
    [SerializeField] private EnemyTargetDetector _enemyTargetDetector; // fallback 탐색 전에 재사용하는 기존 타겟 컴포넌트

    [Tooltip("실행 범위 및 가장 가까운 타겟 확인의 기준점으로 사용할 보스 Transform")]
    [SerializeField] private Transform _bossTransform; // 타겟 거리 비교 기준 Transform

    [Header("Fallback 탐색")]
    [Tooltip("기존 타겟 탐지기가 유효한 타겟을 제공하지 못할 때만 사용하는 Player LayerMask")]
    [SerializeField] private LayerMask _playerLayerMask; // Player 후보 Collider fallback 탐색용 LayerMask

    [Tooltip("fallback 탐색 후보를 필터링할 Player 태그. 비어 있으면 모든 태그를 허용합니다.")]
    [SerializeField] private string _playerTag = "Player"; // Player Root를 필터링할 fallback 탐색 태그

    [Tooltip("파라미터 없는 타겟 탐색에서 사용하는 기본 실행 범위")]
    [Min(0f)]
    [SerializeField] private float _defaultExecutionRange = 20f; // 패턴이 별도 범위를 전달하지 않을 때 사용하는 기본 반경

    [Tooltip("Player 후보 탐색에 사용할 재사용 fallback Collider 버퍼 크기")]
    [Min(1)]
    [SerializeField] private int _candidateBufferSize = 16; // 재사용 Physics2D 결과 버퍼 용량

    [Tooltip("패턴 4 시간 초과 피해처럼 다중 Player 수집에 사용할 재사용 HealthComponent 버퍼 크기")]
    [Min(1)]
    [SerializeField] private int _playerHealthBufferSize = 4; // 모든 타겟 작업에 사용할 재사용 Player HealthComponent 버퍼 용량

    private Collider2D[] _candidateBuffer = new Collider2D[0]; // fallback 타겟 탐색에 사용하는 재사용 Collider 버퍼
    private ContactFilter2D _playerContactFilter; // fallback 타겟 탐색에 사용하는 재사용 레이어 및 트리거 필터
    private bool _hasLoggedFallbackScanWarning; // 이 제공자 상태에서 fallback 탐색 경고가 반복 출력되지 않도록 방지
    private bool _hasLoggedAuthorityWarning; // 이 제공자 상태에서 클라이언트 측 타겟 탐색 경고가 반복 출력되지 않도록 방지
    private bool _hasLoggedMissingReferenceWarning; // 이 제공자 상태에서 누락 참조 경고가 반복 출력되지 않도록 방지
    private bool _hasLoggedPlayerHealthBufferOverflowWarning; // 재사용 Player 체력 버퍼가 가득 찼을 때 경고가 반복 출력되지 않도록 방지

    /// <summary>
    /// 파라미터 없는 타겟 탐색에서 사용하는 기본 실행 범위를 반환한다.
    /// </summary>
    public float DefaultExecutionRange => _defaultExecutionRange;

    /// <summary>
    /// 권장 재사용 Player HealthComponent 버퍼 용량을 반환한다.
    /// </summary>
    public int PlayerHealthBufferSize => _playerHealthBufferSize;

    /// <summary>
    /// 재사용 fallback 탐색 저장소를 준비하고 선택 참조를 해결한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        EnsureCandidateBuffer();
        RefreshContactFilter();
    }

    /// <summary>
    /// 잘못된 인스펙터 값을 보정하고 재사용 탐색 설정을 갱신한다.
    /// </summary>
    private void OnValidate()
    {
        if (_candidateBufferSize < 1)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] CandidateBufferSize가 1보다 작아서 보정됨. object={name}, value={_candidateBufferSize}", this);
            _candidateBufferSize = 1;
        }

        if (_playerHealthBufferSize < 1)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] PlayerHealthBufferSize가 1보다 작아서 보정됨. object={name}, value={_playerHealthBufferSize}", this);
            _playerHealthBufferSize = 1;
        }

        if (_defaultExecutionRange < 0f)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] DefaultExecutionRange가 0보다 작아서 보정됨. object={name}, value={_defaultExecutionRange}", this);
            _defaultExecutionRange = 0f;
        }

        ResolveReferences();
        EnsureCandidateBuffer();
        RefreshContactFilter();
    }

    /// <summary>
    /// 기본 실행 범위를 사용하여 가장 가까운 유효한 Player를 찾는다.
    /// </summary>
    public bool TryFindNearestPlayerForExecution(out Transform targetTransform)
    {
        return TryFindNearestPlayerForExecution(_defaultExecutionRange, out targetTransform, out _, out _);
    }

    /// <summary>
    /// 패턴별 실행 범위를 사용하여 가장 가까운 유효한 Player를 찾는다.
    /// </summary>
    public bool TryFindNearestPlayerForExecution(float executionRange, out Transform targetTransform)
    {
        return TryFindNearestPlayerForExecution(executionRange, out targetTransform, out _, out _);
    }

    /// <summary>
    /// 가장 가까운 유효한 Player를 찾고 해결된 Transform, HealthComponent, NetworkObject를 반환한다.
    /// </summary>
    public bool TryFindNearestPlayerForExecution(float executionRange, out Transform targetTransform, out HealthComponent targetHealth, out NetworkObject targetNetworkObject)
    {
        targetTransform = null;
        targetHealth = null;
        targetNetworkObject = null;

        if (!CanSearchForPlayer())
        {
            return false;
        }

        float safeExecutionRange = Mathf.Max(0f, executionRange); // Overlap 및 거리 제곱 검사에 사용할 보정된 패턴 실행 범위
        Vector3 bossPosition = _bossTransform.position; // 거리 기준점으로 사용할 보스 월드 위치

        if (TryFindByExistingTargetDetector(safeExecutionRange, bossPosition, out targetTransform, out targetHealth, out targetNetworkObject))
        {
            return true;
        }

        return TryFindByFallbackScan(safeExecutionRange, bossPosition, out targetTransform, out targetHealth, out targetNetworkObject);
    }

    /// <summary>
    /// 현재 유효한 모든 Player 체력 타겟을 호출자 소유 재사용 버퍼에 수집한다.
    /// </summary>
    public int CollectAlivePlayersForExecution(HealthComponent[] playerHealthBuffer)
    {
        if (playerHealthBuffer == null || playerHealthBuffer.Length == 0)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] CollectAlivePlayersForExecution에 빈 타겟 버퍼가 전달됨. object={name}", this);
            return 0;
        }

        for (int index = 0; index < playerHealthBuffer.Length; index++)
        {
            playerHealthBuffer[index] = null;
        }

        if (!CanSearchForPlayer())
        {
            return 0;
        }

        Vector3 bossPosition = _bossTransform.position; // fallback 탐색에서 검색 기준점으로 사용하는 보스 월드 위치
        int collectedCount = 0; // 호출자 버퍼에 복사된 고유 Player HealthComponent 개수
        NetworkManager networkManager = NetworkManager.Singleton; // PlayerObject가 권한 있는 Player 등록소인지 판단할 때 사용하는 NGO 싱글톤

        CollectNetworkPlayers(playerHealthBuffer, ref collectedCount);

        if (networkManager == null || !networkManager.IsListening || collectedCount <= 0)
        {
            CollectFallbackScanPlayers(bossPosition, playerHealthBuffer, ref collectedCount);
        }

        return collectedCount;
    }

    /// <summary>
    /// 현재 인스턴스에서 타겟 탐색을 실행할 수 있는지 반환한다.
    /// </summary>
    private bool CanSearchForPlayer()
    {
        ResolveReferences();

        if (_bossController == null || _bossTransform == null)
        {
            if (!_hasLoggedMissingReferenceWarning)
            {
                Debug.LogWarning($"[BossPlayerTargetProvider] BossController 또는 BossTransform이 없어 Player 탐색 실패. object={name}", this);
                _hasLoggedMissingReferenceWarning = true;
            }

            return false;
        }

        if (!_bossController.IsBossLogicAuthority())
        {
            if (!_hasLoggedAuthorityWarning)
            {
                Debug.LogWarning($"[BossPlayerTargetProvider] 이 인스턴스에 보스 권한이 없어 Player 탐색이 차단됨. object={name}", this);
                _hasLoggedAuthorityWarning = true;
            }

            return false;
        }

        _hasLoggedAuthorityWarning = false;
        return true;
    }

    /// <summary>
    /// fallback 탐색 전에 기존 EnemyTargetDetector 사용을 시도한다.
    /// </summary>
    private bool TryFindByExistingTargetDetector(float executionRange, Vector3 bossPosition, out Transform targetTransform, out HealthComponent targetHealth, out NetworkObject targetNetworkObject)
    {
        targetTransform = null;
        targetHealth = null;
        targetNetworkObject = null;

        if (_enemyTargetDetector == null)
        {
            return false;
        }

        _enemyTargetDetector.TickSearch(Time.time, bossPosition, executionRange, 0.01f);
        Transform detectorTarget = _enemyTargetDetector.CurrentTarget; // 기존 탐지기가 자체 재사용 버퍼에서 선택한 결과
        return TryResolveValidTarget(detectorTarget, bossPosition, executionRange * executionRange, out targetTransform, out targetHealth, out targetNetworkObject);
    }

    /// <summary>
    /// 기존 탐지기가 유효한 타겟을 제공하지 못할 때 재사용 Collider 버퍼로 Player 후보를 탐색한다.
    /// </summary>
    private bool TryFindByFallbackScan(float executionRange, Vector3 bossPosition, out Transform targetTransform, out HealthComponent targetHealth, out NetworkObject targetNetworkObject)
    {
        targetTransform = null;
        targetHealth = null;
        targetNetworkObject = null;

        if (!_hasLoggedFallbackScanWarning)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] 기존 타겟 탐지기가 유효한 타겟을 제공하지 못해 fallback 탐색을 실행함. object={name}", this);
            _hasLoggedFallbackScanWarning = true;
        }

        EnsureCandidateBuffer();
        RefreshContactFilter();

        int hitCount = Physics2D.OverlapCircle((Vector2)bossPosition, executionRange, _playerContactFilter, _candidateBuffer);
        float executionRangeSqr = executionRange * executionRange; // 후보 필터링에 사용하는 실행 범위 제곱값
        float nearestSqrDistance = float.MaxValue; // 이번 탐색에서 발견된 가장 가까운 거리 제곱값
        bool foundTarget = false; // 유효한 Player 후보를 찾았는지 여부

        for (int index = 0; index < hitCount; index++)
        {
            Collider2D candidateCollider = _candidateBuffer[index]; // Physics2D가 재사용 버퍼에 반환한 후보 Collider
            _candidateBuffer[index] = null;

            if (candidateCollider == null)
            {
                continue;
            }

            Transform candidateSource = ResolveCandidateSourceTransform(candidateCollider);

            if (!TryResolveValidTarget(candidateSource, bossPosition, executionRangeSqr, out Transform resolvedTransform, out HealthComponent resolvedHealth, out NetworkObject resolvedNetworkObject))
            {
                continue;
            }

            float sqrDistance = (resolvedTransform.position - bossPosition).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
            {
                continue;
            }

            nearestSqrDistance = sqrDistance;
            targetTransform = resolvedTransform;
            targetHealth = resolvedHealth;
            targetNetworkObject = resolvedNetworkObject;
            foundTarget = true;
        }

        return foundTarget;
    }

    /// <summary>
    /// 멀티플레이 세션이 활성화되어 있으면 NGO PlayerObject에서 Player를 수집한다.
    /// </summary>
    private void CollectNetworkPlayers(HealthComponent[] playerHealthBuffer, ref int collectedCount)
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 클라이언트에서 결정하지 않고 연결된 PlayerObject를 열거하는 NGO 싱글톤
        if (networkManager == null || !networkManager.IsListening)
        {
            return;
        }

        for (int index = 0; index < networkManager.ConnectedClientsList.Count; index++)
        {
            NetworkClient client = networkManager.ConnectedClientsList[index]; // PlayerObject를 소유할 수 있는 연결된 NGO 클라이언트
            if (client == null || client.PlayerObject == null)
            {
                continue;
            }

            if (!TryResolveValidTargetWithoutRange(client.PlayerObject.transform, out HealthComponent targetHealth))
            {
                continue;
            }

            AddUniquePlayerHealth(playerHealthBuffer, ref collectedCount, targetHealth);
        }
    }

    /// <summary>
    /// 재사용 Physics2D fallback 버퍼로 유효한 Player 후보를 수집한다.
    /// </summary>
    private void CollectFallbackScanPlayers(Vector3 bossPosition, HealthComponent[] playerHealthBuffer, ref int collectedCount)
    {
        if (!_hasLoggedFallbackScanWarning)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] Player 등록소를 찾지 못해 fallback 탐색으로 Player 타겟을 수집함. object={name}", this);
            _hasLoggedFallbackScanWarning = true;
        }

        EnsureCandidateBuffer();
        RefreshContactFilter();

        float safeExecutionRange = Mathf.Max(0f, _defaultExecutionRange); // 재사용 fallback 수집 반경으로 사용하는 기본 실행 범위
        int hitCount = Physics2D.OverlapCircle((Vector2)bossPosition, safeExecutionRange, _playerContactFilter, _candidateBuffer);
        float executionRangeSqr = safeExecutionRange * safeExecutionRange; // 유효 타겟 필터링에 사용하는 거리 제곱값

        for (int index = 0; index < hitCount; index++)
        {
            Collider2D candidateCollider = _candidateBuffer[index]; // Physics2D가 재사용 버퍼에 반환한 후보 Collider
            _candidateBuffer[index] = null;

            if (candidateCollider == null)
            {
                continue;
            }

            Transform candidateSource = ResolveCandidateSourceTransform(candidateCollider);

            if (!TryResolveValidTarget(candidateSource, bossPosition, executionRangeSqr, out _, out HealthComponent targetHealth, out _))
            {
                continue;
            }

            AddUniquePlayerHealth(playerHealthBuffer, ref collectedCount, targetHealth);
        }
    }

    /// <summary>
    /// 거리 조건을 제외하고 Player 후보를 해결하고 검증한다.
    /// </summary>
    private bool TryResolveValidTargetWithoutRange(Transform candidateSource, out HealthComponent targetHealth)
    {
        targetHealth = null;

        if (candidateSource == null || !MatchesPlayerTag(candidateSource))
        {
            return false;
        }

        targetHealth = ResolveHealthComponent(candidateSource);
        if (!IsHealthValid(targetHealth))
        {
            return false;
        }

        NetworkObject targetNetworkObject = candidateSource.GetComponentInParent<NetworkObject>(); // 권한 기준 Transform을 결정하기 위한 선택적 네트워크 루트
        Transform targetTransform = ResolveTargetTransform(candidateSource, targetNetworkObject);
        return IsTransformValid(targetTransform);
    }

    /// <summary>
    /// 아직 수집되지 않은 Player HealthComponent를 호출자 버퍼에 추가한다.
    /// </summary>
    private void AddUniquePlayerHealth(HealthComponent[] playerHealthBuffer, ref int collectedCount, HealthComponent targetHealth)
    {
        if (targetHealth == null)
        {
            return;
        }

        for (int index = 0; index < collectedCount; index++)
        {
            if (playerHealthBuffer[index] == targetHealth)
            {
                return;
            }
        }

        if (collectedCount >= playerHealthBuffer.Length)
        {
            if (!_hasLoggedPlayerHealthBufferOverflowWarning)
            {
                Debug.LogWarning($"[BossPlayerTargetProvider] Player Health 버퍼가 가득 참. PlayerHealthBufferSize 또는 호출자 버퍼 크기를 늘려야 함. object={name}, capacity={playerHealthBuffer.Length}", this);
                _hasLoggedPlayerHealthBufferOverflowWarning = true;
            }

            return;
        }

        playerHealthBuffer[collectedCount] = targetHealth;
        collectedCount++;
    }

    /// <summary>
    /// 패턴 실행 타겟팅을 위해 Player 후보 Transform을 해결하고 검증한다.
    /// </summary>
    private bool TryResolveValidTarget(Transform candidateSource, Vector3 bossPosition, float executionRangeSqr, out Transform targetTransform, out HealthComponent targetHealth, out NetworkObject targetNetworkObject)
    {
        targetTransform = null;
        targetHealth = null;
        targetNetworkObject = null;

        if (candidateSource == null)
        {
            return false;
        }

        if (!MatchesPlayerTag(candidateSource))
        {
            return false;
        }

        targetHealth = ResolveHealthComponent(candidateSource);
        if (!IsHealthValid(targetHealth))
        {
            return false;
        }

        targetNetworkObject = candidateSource.GetComponentInParent<NetworkObject>();
        targetTransform = ResolveTargetTransform(candidateSource, targetNetworkObject);

        if (targetTransform == null)
        {
            return false;
        }

        if (!IsTransformValid(targetTransform))
        {
            return false;
        }

        float sqrDistance = (targetTransform.position - bossPosition).sqrMagnitude;
        return sqrDistance <= executionRangeSqr;
    }

    /// <summary>
    /// 후보 위치 계산에 사용할 Transform을 결정한다.
    /// </summary>
    private Transform ResolveTargetTransform(Transform candidateSource, NetworkObject targetNetworkObject)
    {
        if (targetNetworkObject != null)
        {
            return targetNetworkObject.transform;
        }

        return candidateSource.root;
    }

    /// <summary>
    /// Collider 후보에서 가장 적절한 Transform 소스를 반환한다.
    /// </summary>
    private Transform ResolveCandidateSourceTransform(Collider2D candidateCollider)
    {
        if (candidateCollider == null)
        {
            return null;
        }

        if (candidateCollider.attachedRigidbody != null)
        {
            return candidateCollider.attachedRigidbody.transform;
        }

        return candidateCollider.transform;
    }

    /// <summary>
    /// 후보 계층이 설정된 Player 태그를 만족하는지 반환한다.
    /// </summary>
    private bool MatchesPlayerTag(Transform candidateTransform)
    {
        if (candidateTransform == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_playerTag))
        {
            return true;
        }

        Transform current = candidateTransform; // 설정된 Player 태그를 확인할 현재 계층 노드
        while (current != null)
        {
            if (current.CompareTag(_playerTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    /// <summary>
    /// 후보 계층에서 HealthComponent를 해결한다.
    /// </summary>
    private HealthComponent ResolveHealthComponent(Transform candidateTransform)
    {
        if (candidateTransform == null)
        {
            return null;
        }

        HealthComponent health = candidateTransform.GetComponent<HealthComponent>(); // 직접 연결된 HealthComponent
        if (health != null)
        {
            return health;
        }

        health = candidateTransform.GetComponentInParent<HealthComponent>();
        if (health != null)
        {
            return health;
        }

        return candidateTransform.GetComponentInChildren<HealthComponent>(true);
    }

    /// <summary>
    /// 후보 체력 상태가 보스 타겟팅에 유효한지 반환한다.
    /// </summary>
    private bool IsHealthValid(HealthComponent health)
    {
        if (health == null)
        {
            return false;
        }

        if (!health.isActiveAndEnabled || health.IsDead)
        {
            return false;
        }

        return health.GetCurrentHealth() > 0f;
    }

    /// <summary>
    /// 해결된 타겟 Transform이 보스 타겟팅에 사용할 수 있는지 반환한다.
    /// </summary>
    private bool IsTransformValid(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            return false;
        }

        GameObject targetObject = targetTransform.gameObject; // 활성 상태 및 씬 검증에 사용하는 Player 오브젝트
        if (!targetObject.activeInHierarchy)
        {
            return false;
        }

        Scene targetScene = targetObject.scene; // 타겟이 존재하는 씬
        return targetScene.IsValid() && targetScene == gameObject.scene && targetScene == SceneManager.GetActiveScene();
    }

    /// <summary>
    /// 인스펙터 참조가 없을 때 동일 GameObject에서 선택적 참조를 해결한다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_bossController == null)
        {
            _bossController = GetComponent<BossController>();
        }

        if (_enemyTargetDetector == null)
        {
            _enemyTargetDetector = GetComponent<EnemyTargetDetector>();
        }

        if (_bossTransform == null)
        {
            _bossTransform = transform;
        }
    }

    /// <summary>
    /// 재사용 후보 Collider 버퍼가 인스펙터 용량과 일치하도록 보장한다.
    /// </summary>
    private void EnsureCandidateBuffer()
    {
        if (_candidateBufferSize < 1)
        {
            Debug.LogWarning($"[BossPlayerTargetProvider] CandidateBufferSize가 1보다 작아서 1로 fallback됨. object={name}", this);
            _candidateBufferSize = 1;
        }

        if (_candidateBuffer != null && _candidateBuffer.Length == _candidateBufferSize)
        {
            return;
        }

        _candidateBuffer = new Collider2D[_candidateBufferSize];
    }

    /// <summary>
    /// 인스펙터 값으로 재사용 fallback ContactFilter를 갱신한다.
    /// </summary>
    private void RefreshContactFilter()
    {
        _playerContactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _playerLayerMask,
            useTriggers = Physics2D.queriesHitTriggers
        };
    }
}
