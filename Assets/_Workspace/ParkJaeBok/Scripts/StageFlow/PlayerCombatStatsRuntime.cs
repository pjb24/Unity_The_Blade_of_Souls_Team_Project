using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어가 적군과 주고받은 전투 통계를 런타임에 저장하고 저장 시스템과 UI에 스냅샷으로 제공합니다.
/// </summary>
public sealed class PlayerCombatStatsRuntime : MonoBehaviour
{
    [Serializable]
    public struct SnapshotData
    {
        [Tooltip("플레이어가 적군에게 실제로 적용한 누적 대미지입니다.")]
        public float TotalDamageDealt; // 플레이어가 적군에게 실제로 적용한 누적 대미지입니다.

        [Tooltip("플레이어가 적군, 적군 투사체, 보스 투사체/패턴 또는 장애물에게 실제로 피해를 입은 누적 횟수입니다.")]
        public int DamageTakenCount; // 플레이어가 적군, 적군 투사체, 보스 투사체/패턴 또는 장애물에게 실제로 피해를 입은 누적 횟수입니다.
    }

    private static PlayerCombatStatsRuntime _instance; // 전역 접근에 사용할 전투 통계 런타임 단일 인스턴스입니다.
    private readonly Dictionary<ulong, SnapshotData> _statsByClientId = new Dictionary<ulong, SnapshotData>(); // 멀티플레이에서 ClientId별 전투 통계를 분리 보관하는 저장소입니다.

    [Tooltip("씬 전환 뒤에도 전투 통계 런타임 오브젝트를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // 씬 전환 중 전투 통계 데이터를 유지할지 여부입니다.

    [Header("Debug (Runtime State)")]
    [Tooltip("디버그용: 플레이어가 적군에게 실제로 적용한 누적 대미지입니다.")]
    [SerializeField] private float _totalDamageDealt; // 플레이어가 적군에게 실제로 적용한 누적 대미지입니다.

    [Tooltip("디버그용: 플레이어가 적군, 적군 투사체, 보스 투사체/패턴 또는 장애물에게 실제로 피해를 입은 누적 횟수입니다.")]
    [SerializeField] private int _damageTakenCount; // 플레이어가 적군, 적군 투사체, 보스 투사체/패턴 또는 장애물에게 실제로 피해를 입은 누적 횟수입니다.

    private Action _changedListeners; // 전투 통계 변경 알림 리스너 체인입니다.

    /// <summary>
    /// 전역 PlayerCombatStatsRuntime 인스턴스를 반환하고, 없으면 새로 생성합니다.
    /// </summary>
    public static PlayerCombatStatsRuntime Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<PlayerCombatStatsRuntime>();
                if (_instance == null)
                {
                    GameObject runtimeObject = new GameObject("--- Player Combat Stats Runtime ---"); // 자동 생성된 전투 통계 런타임 오브젝트입니다.
                    _instance = runtimeObject.AddComponent<PlayerCombatStatsRuntime>();
                }
            }

            return _instance;
        }
    }

    /// <summary>
    /// 새 인스턴스를 만들지 않고 현재 존재하는 PlayerCombatStatsRuntime을 반환합니다.
    /// </summary>
    public static bool TryGetExistingInstance(out PlayerCombatStatsRuntime runtime)
    {
        runtime = _instance != null ? _instance : FindAnyObjectByType<PlayerCombatStatsRuntime>();
        return runtime != null;
    }

    /// <summary>
    /// 플레이어가 적군에게 실제로 적용한 누적 대미지를 반환합니다.
    /// </summary>
    public float TotalDamageDealt => _totalDamageDealt;

    /// <summary>
    /// 플레이어가 적군, 적군 투사체, 보스 투사체/패턴 또는 장애물에게 실제로 피해를 입은 누적 횟수를 반환합니다.
    /// </summary>
    public int DamageTakenCount => _damageTakenCount;

    /// <summary>
    /// 현재 피어의 로컬 플레이어 ClientId 기준 전투 통계 스냅샷을 반환합니다.
    /// </summary>
    public SnapshotData GetLocalPlayerSnapshot()
    {
        return GetSnapshotForClientId(ResolveLocalClientId());
    }

    /// <summary>
    /// Host 플레이어 ClientId 기준 전투 통계 스냅샷을 반환합니다.
    /// </summary>
    public SnapshotData GetHostPlayerSnapshot()
    {
        return GetSnapshotForClientId(NetworkManager.ServerClientId);
    }

    /// <summary>
    /// 첫 번째 원격 Client 플레이어 전투 통계 스냅샷을 반환합니다.
    /// </summary>
    public SnapshotData GetFirstRemoteClientSnapshot()
    {
        NetworkManager networkManager = NetworkManager.Singleton; // 접속자 목록에서 Host가 아닌 ClientId를 찾기 위한 NGO 관리자입니다.
        if (networkManager != null && networkManager.IsListening)
        {
            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.ServerClientId)
                {
                    continue;
                }

                return GetSnapshotForClientId(clientId);
            }
        }

        foreach (KeyValuePair<ulong, SnapshotData> pair in _statsByClientId)
        {
            if (pair.Key != NetworkManager.ServerClientId)
            {
                return SanitizeSnapshot(pair.Value);
            }
        }

        return default;
    }

    /// <summary>
    /// 지정한 ClientId에 누적된 전투 통계 스냅샷을 반환합니다.
    /// </summary>
    public SnapshotData GetSnapshotForClientId(ulong clientId)
    {
        if (_statsByClientId.TryGetValue(clientId, out SnapshotData snapshot))
        {
            return SanitizeSnapshot(snapshot);
        }

        if (clientId == ResolveLocalClientId())
        {
            return CreateSnapshot();
        }

        return default;
    }

    /// <summary>
    /// 인스턴스 중복을 방지하고 피격 이벤트를 구독할 준비를 합니다.
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[PlayerCombatStatsRuntime] 중복 인스턴스를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    /// <summary>
    /// HitReceiver 전역 피격 결과 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        HitReceiver.GlobalHitResolved -= HandleGlobalHitResolved;
        HitReceiver.GlobalHitResolved += HandleGlobalHitResolved;
    }

    /// <summary>
    /// HitReceiver 전역 피격 결과 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        HitReceiver.GlobalHitResolved -= HandleGlobalHitResolved;
    }

    /// <summary>
    /// 전투 통계 변경 알림 리스너를 등록합니다.
    /// </summary>
    public void AddListener(Action listener)
    {
        _changedListeners += listener;
    }

    /// <summary>
    /// 전투 통계 변경 알림 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(Action listener)
    {
        _changedListeners -= listener;
    }

    /// <summary>
    /// 현재 전투 통계를 저장 가능한 스냅샷으로 생성합니다.
    /// </summary>
    public SnapshotData CreateSnapshot()
    {
        return new SnapshotData
        {
            TotalDamageDealt = Mathf.Max(0f, _totalDamageDealt),
            DamageTakenCount = Mathf.Max(0, _damageTakenCount)
        };
    }

    /// <summary>
    /// 전달된 스냅샷으로 전투 통계 상태를 복원합니다.
    /// </summary>
    public void ApplySnapshot(SnapshotData snapshot)
    {
        _totalDamageDealt = Mathf.Max(0f, snapshot.TotalDamageDealt);
        _damageTakenCount = Mathf.Max(0, snapshot.DamageTakenCount);
        _statsByClientId[ResolveLocalClientId()] = CreateSnapshot();
        NotifyChanged();
    }

    /// <summary>
    /// 현재 전투 통계를 0으로 초기화합니다.
    /// </summary>
    public void ResetStats()
    {
        _totalDamageDealt = 0f;
        _damageTakenCount = 0;
        _statsByClientId.Clear();
        _statsByClientId[ResolveLocalClientId()] = CreateSnapshot();
        NotifyChanged();
    }

    /// <summary>
    /// 모든 HitReceiver의 성공 피격 결과 중 로컬 플레이어와 적군 사이의 통계만 누적합니다.
    /// </summary>
    private void HandleGlobalHitResolved(HitReceiver receiver, HitRequest request, HitResult result)
    {
        if (receiver == null || !result.IsAccepted)
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening && networkManager.IsClient && !networkManager.IsServer)
        {
            return;
        }

        bool hasLocalAttacker = IsLocalPlayerObject(request.Attacker); // 현재 피어가 조종하는 플레이어가 공격자인지 여부입니다.
        bool hasLocalTarget = IsLocalPlayerObject(receiver.gameObject); // 현재 피어가 조종하는 플레이어가 피격자인지 여부입니다.
        bool hasEnemyTarget = IsEnemyCombatObject(receiver.gameObject); // 전투 통계에 포함할 적군 대상인지 여부입니다.
        bool hasCountableDamageAttacker = IsDamageTakenStatsAttacker(request.Attacker); // 피격 횟수 통계에 포함할 공격자인지 여부입니다.
        bool isSelfHit = IsSamePlayerObject(request.Attacker, receiver.gameObject); // 같은 플레이어 내부 충돌인지 여부입니다.

        if (networkManager != null && networkManager.IsListening && networkManager.IsServer)
        {
            if (hasEnemyTarget && !isSelfHit)
            {
                PlayerNetworkSync.TryReportCombatStatsToParticipants(request.Attacker, result.AppliedDamage, 0);
            }

            if (hasCountableDamageAttacker)
            {
                PlayerNetworkSync.TryReportCombatStatsToParticipants(receiver.gameObject, 0f, 1);
            }

            return;
        }

        if (hasEnemyTarget && hasLocalAttacker && !isSelfHit)
        {
            RecordDamageDealt(result.AppliedDamage);
        }

        if (hasCountableDamageAttacker && hasLocalTarget)
        {
            RecordDamageTaken();
        }
    }

    /// <summary>
    /// 플레이어가 적군에게 실제로 적용한 대미지를 누적합니다.
    /// </summary>
    public void RecordDamageDealt(float appliedDamage)
    {
        RecordCombatStatsForClient(ResolveLocalClientId(), appliedDamage, 0);
    }

    /// <summary>
    /// 플레이어가 적군, 적군 투사체, 보스 투사체/패턴 또는 장애물에게 피해를 입은 횟수를 1회 누적합니다.
    /// </summary>
    public void RecordDamageTaken()
    {
        RecordCombatStatsForClient(ResolveLocalClientId(), 0f, 1);
    }

    /// <summary>
    /// 플레이어가 적군, 적군 투사체, 보스 투사체/패턴 또는 장애물에게 피해를 입은 횟수를 지정한 횟수만큼 누적합니다.
    /// </summary>
    public void RecordDamageTakenCount(int count)
    {
        RecordCombatStatsForClient(ResolveLocalClientId(), 0f, count);
    }

    /// <summary>
    /// 지정한 ClientId의 전투 통계 증가분을 누적하고, 로컬 플레이어 값이면 기존 저장/표시용 필드도 함께 갱신합니다.
    /// </summary>
    public void RecordCombatStatsForClient(ulong clientId, float damageDealt, int damageTakenCount)
    {
        float safeDamageDealt = Mathf.Max(0f, damageDealt); // 누적 가능한 공격 대미지 증가분입니다.
        int safeDamageTakenCount = Mathf.Max(0, damageTakenCount); // 누적 가능한 피격 횟수 증가분입니다.
        if (safeDamageDealt <= 0f && safeDamageTakenCount <= 0)
        {
            return;
        }

        SnapshotData snapshot = GetSnapshotForClientId(clientId);
        snapshot.TotalDamageDealt += safeDamageDealt;
        snapshot.DamageTakenCount += safeDamageTakenCount;
        _statsByClientId[clientId] = SanitizeSnapshot(snapshot);

        if (clientId == ResolveLocalClientId())
        {
            _totalDamageDealt = _statsByClientId[clientId].TotalDamageDealt;
            _damageTakenCount = _statsByClientId[clientId].DamageTakenCount;
        }

        NotifyChanged();
    }

    /// <summary>
    /// 저장 또는 네트워크에서 들어온 전투 통계 값을 안전한 범위로 보정합니다.
    /// </summary>
    private SnapshotData SanitizeSnapshot(SnapshotData snapshot)
    {
        return new SnapshotData
        {
            TotalDamageDealt = Mathf.Max(0f, snapshot.TotalDamageDealt),
            DamageTakenCount = Mathf.Max(0, snapshot.DamageTakenCount)
        };
    }

    /// <summary>
    /// 현재 피어의 로컬 플레이어 ClientId를 싱글플레이와 멀티플레이 상황에 맞게 해석합니다.
    /// </summary>
    private ulong ResolveLocalClientId()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            return networkManager.LocalClientId;
        }

        return NetworkManager.ServerClientId;
    }

    /// <summary>
    /// 지정한 오브젝트가 현재 피어가 조종하는 플레이어인지 판정합니다.
    /// </summary>
    private bool IsLocalPlayerObject(GameObject candidate)
    {
        if (candidate == null || !TryResolvePlayerRoot(candidate, out GameObject playerRoot))
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            return true;
        }

        NetworkObject networkObject = playerRoot.GetComponent<NetworkObject>();
        return networkObject != null && networkObject.IsOwner;
    }

    /// <summary>
    /// 두 오브젝트가 같은 플레이어 루트를 뜻하는지 판정합니다.
    /// </summary>
    private bool IsSamePlayerObject(GameObject lhs, GameObject rhs)
    {
        if (lhs == null || rhs == null)
        {
            return false;
        }

        if (!TryResolvePlayerRoot(lhs, out GameObject lhsRoot) || !TryResolvePlayerRoot(rhs, out GameObject rhsRoot))
        {
            return false;
        }

        return lhsRoot == rhsRoot;
    }

    /// <summary>
    /// 지정한 오브젝트가 전투 통계에 포함해야 하는 적군 계열 오브젝트인지 판정합니다.
    /// </summary>
    private bool IsEnemyCombatObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.GetComponentInParent<EnemyHealthAdapter>() != null || candidate.GetComponentInParent<EnemyAIController>() != null)
        {
            return true;
        }

        if (candidate.GetComponentInParent<BossController>() != null)
        {
            return true;
        }

        return candidate.GetComponentInChildren<EnemyHealthAdapter>(true) != null
               || candidate.GetComponentInChildren<EnemyAIController>(true) != null
               || candidate.GetComponentInChildren<BossController>(true) != null;
    }

    /// <summary>
    /// 지정한 오브젝트가 피격 횟수 통계에 포함해야 하는 공격자인지 판정합니다.
    /// </summary>
    private bool IsDamageTakenStatsAttacker(GameObject candidate)
    {
        return IsEnemyCombatObject(candidate)
               || IsEnemyProjectileCombatObject(candidate)
               || IsBossPatternDamageObject(candidate)
               || IsObstacleCombatObject(candidate);
    }

    /// <summary>
    /// 지정한 오브젝트가 피격 횟수 통계에 포함해야 하는 적군 투사체 계열 오브젝트인지 판정합니다.
    /// </summary>
    private bool IsEnemyProjectileCombatObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.GetComponentInParent<PooledRangedProjectile>() != null)
        {
            return true;
        }

        return candidate.GetComponentInChildren<PooledRangedProjectile>(true) != null;
    }

    /// <summary>
    /// 지정한 오브젝트가 피격 횟수 통계에 포함해야 하는 보스 투사체, 가시 또는 약점 패턴 오브젝트인지 판정합니다.
    /// </summary>
    private bool IsBossPatternDamageObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.GetComponentInParent<BossFanProjectilePattern>() != null
            || candidate.GetComponentInParent<BossGroundSpikePattern>() != null
            || candidate.GetComponentInParent<BossWeakPointPattern>() != null)
        {
            return true;
        }

        return candidate.GetComponentInChildren<BossFanProjectilePattern>(true) != null
               || candidate.GetComponentInChildren<BossGroundSpikePattern>(true) != null
               || candidate.GetComponentInChildren<BossWeakPointPattern>(true) != null;
    }

    /// <summary>
    /// 지정한 오브젝트가 피격 횟수 통계에 포함해야 하는 장애물 계열 오브젝트인지 판정합니다.
    /// </summary>
    private bool IsObstacleCombatObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.GetComponentInParent<ObstacleHitDealer>() != null)
        {
            return true;
        }

        return candidate.GetComponentInChildren<ObstacleHitDealer>(true) != null;
    }

    /// <summary>
    /// 오브젝트 계층에서 플레이어 루트 오브젝트를 해석합니다.
    /// </summary>
    private bool TryResolvePlayerRoot(GameObject source, out GameObject playerRoot)
    {
        playerRoot = null;
        if (source == null)
        {
            return false;
        }

        PlayerNetworkRoot networkRoot = source.GetComponentInParent<PlayerNetworkRoot>();
        if (networkRoot != null)
        {
            playerRoot = networkRoot.gameObject;
            return true;
        }

        PlayerMovement movement = source.GetComponentInParent<PlayerMovement>();
        if (movement != null)
        {
            playerRoot = movement.gameObject;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 전투 통계 변경을 모든 리스너에 알립니다.
    /// </summary>
    private void NotifyChanged()
    {
        _changedListeners?.Invoke();
    }
}
