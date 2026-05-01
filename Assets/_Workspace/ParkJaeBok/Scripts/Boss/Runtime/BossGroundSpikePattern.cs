using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 가장 가까운 유효한 Player 아래에 경고를 표시한 후 임시 지면 스파이크 공격을 생성하여 패턴 2를 실행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossGroundSpikePattern : BossPatternBase
{
    [Header("필수 참조")]
    [Tooltip("권한, 패턴 데이터, Player 탐색을 소유하는 보스 컨트롤러")]
    [SerializeField] private BossController _bossController; // 패턴 2에서 사용하는 보스 권한 및 공통 데이터 소스

    [Header("실행")]
    [Tooltip("패턴 2 시작 시 가장 가까운 Player를 탐색할 범위")]
    [Min(0f)]
    [SerializeField] private float _executionRange = 20f; // 패턴 2 실행 시에만 사용하는 Player 탐색 거리

    [Tooltip("가시 프리팹 Collider2D 겹침 결과를 수집할 리스트의 초기 용량 기준입니다.")]
    [Min(1)]
    [SerializeField] private int _maxHitColliderCandidates = 16; // 가시 프리팹 Collider2D 겹침 결과를 수집할 리스트의 초기 용량 기준

    private Coroutine _executionCoroutine; // 경고 지연 및 히트 지속 시간을 관리하는 패턴 2 실행 코루틴
    private GameObject _activeSpikeInstance; // 현재 역할을 수행 중인 가시 인스턴스
    private Collider2D _activeSpikeHitCollider; // 패턴 2 데미지 타이밍 동안 활성화되는 스파이크 히트 콜라이더
    private int _nextSpikeHitSerial; // 패턴 2 HitRequest 고유 ID 생성을 위한 증가형 시리얼
    private bool _hasLoggedSpikeObjectPoolFallback; // 스파이크 생성 시 직접 Instantiate fallback 경고 중복 방지
    private bool _hasLoggedNetworkSpawnFallback; // NetworkObject Spawn fallback 경고 중복 방지

    private readonly List<Collider2D> _hitColliderList = new List<Collider2D>(16);
    private readonly List<HitReceiver> _hitReceiverList = new List<HitReceiver>(16);
    private ContactFilter2D _spikeHitFilter;

    /// <summary>
    /// 패턴 2 시작 전에 필요한 런타임 참조를 해결한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 잘못된 인스펙터 값을 보정하고 참조를 갱신한다.
    /// </summary>
    private void OnValidate()
    {
        if (_executionRange < 0f)
        {
            Debug.LogWarning($"[BossGroundSpikePattern] ExecutionRange가 0보다 작아서 보정됨. object={name}, value={_executionRange}", this);
            _executionRange = 0f;
        }

        if (_maxHitColliderCandidates < 1)
        {
            Debug.LogWarning($"[BossGroundSpikePattern] MaxHitColliderCandidates가 1보다 작아서 보정됨. object={name}, value={_maxHitColliderCandidates}", this);
            _maxHitColliderCandidates = 1;
        }

        ResolveReferences();
    }

    /// <summary>
    /// 공통 패턴 실행 API를 통해 패턴 2를 1회 실행한다.
    /// </summary>
    protected override void OnPatternExecutionStarted()
    {
        ResolveReferences();

        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            ReportPatternFailed("PatternAuthorityMissing");
            return;
        }

        if (!TryGetSettings(out GroundSpikePatternSettings settings))
        {
            CancelPatternWithWarning("MissingGroundSpikeSettings");
            return;
        }

        if (settings.SpikePrefab == null)
        {
            CancelPatternWithWarning("SpikePrefabMissing");
            return;
        }

        if (!TryResolveTarget(out Transform targetTransform))
        {
            CancelPatternWithWarning("TargetPlayerMissing");
            return;
        }

        Vector3 spikePosition = ResolveSpikePosition(settings, targetTransform); // 경고, 스파이크, 공격 VFX가 재생될 최종 월드 위치
        _nextSpikeHitSerial = 0;
        _executionCoroutine = StartCoroutine(ExecuteSpikeSequence(settings, spikePosition));
    }

    /// <summary>
    /// BossController에서 취소가 들어오면 패턴 2 실행과 히트 상태를 정리한다.
    /// </summary>
    protected override void OnPatternExecutionCancelled(string reason)
    {
        StopExecutionCoroutine();
        CleanupActiveSpikeInstance();
    }

    /// <summary>
    /// 보스 패턴 데이터에서 패턴 2 설정을 가져온다.
    /// </summary>
    private bool TryGetSettings(out GroundSpikePatternSettings settings)
    {
        settings = default;
        ResolveReferences();

        if (_bossController == null || _bossController.PatternData == null)
        {
            return false;
        }

        if (!_bossController.PatternData.TryGetGroundSpikePattern(_bossController.CurrentPatternId, out settings))
        {
            Debug.LogWarning($"[BossGroundSpikePattern] PatternId에 해당하는 GroundSpike 설정이 없음. object={name}, patternId={_bossController.CurrentPatternId}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 공통 Player 타겟 제공자를 통해 가장 가까운 Player를 찾는다.
    /// </summary>
    private bool TryResolveTarget(out Transform targetTransform)
    {
        targetTransform = null;
        if (_bossController == null)
        {
            return false;
        }

        return _bossController.TryFindNearestPlayerForExecution(_executionRange, out targetTransform, out _, out _);
    }

    /// <summary>
    /// 타겟 Player 위에서 아래로 Raycast하여 스파이크 위치를 결정한다.
    /// </summary>
    private Vector3 ResolveSpikePosition(GroundSpikePatternSettings settings, Transform targetTransform)
    {
        Vector3 targetPosition = targetTransform.position; // 기본 스파이크 위치 (fallback)
        Vector2 raycastStart = new Vector2(targetPosition.x, targetPosition.y + settings.RaycastStartYOffset); // Player 위쪽에서 시작하는 Raycast 시작점
        RaycastHit2D groundHit = Physics2D.Raycast(raycastStart, Vector2.down, settings.GroundRaycastDistance, settings.GroundLayerMask); // 지면을 찾기 위한 Raycast

        if (groundHit.collider != null)
        {
            return groundHit.point;
        }

        LogFailureOnce("GroundRaycastFailed");
        return targetPosition;
    }

    /// <summary>
    /// 경고 → 스파이크 생성 → 공격 → 히트 유지 → 종료까지 전체 시퀀스를 실행한다.
    /// </summary>
    private IEnumerator ExecuteSpikeSequence(GroundSpikePatternSettings settings, Vector3 spikePosition)
    {
        PlaySynchronizedVfxOrWarn(settings.WarningEffectId, settings.WarningVfxPrefab, spikePosition, "WarningVFXMissing", true);

        if (settings.SpikeWarningDuration > 0f)
        {
            yield return new WaitForSeconds(settings.SpikeWarningDuration);
        }

        if (!TrySpawnSpike(settings.SpikePrefab, spikePosition, out GameObject spikeInstance))
        {
            _executionCoroutine = null;
            ReportPatternFailed("SpikeSpawnFailed");
            yield break;
        }

        _activeSpikeInstance = spikeInstance;
        PlaySynchronizedVfxOrWarn(settings.AttackEffectId, settings.AttackVfxPrefab, spikePosition, "AttackVFXMissing", false);
        _bossController.PlayPresentationCue(E_BossPresentationCue.PatternAttack, E_BossPatternType.GroundSpike, spikePosition);
        MarkPatternEffectApplied();
        EnableSpikeHitCollider(spikeInstance);
        ApplySpikeHit(settings, spikePosition);

        if (settings.SpikeHitDuration > 0f)
        {
            yield return new WaitForSeconds(settings.SpikeHitDuration);
        }

        CleanupActiveSpikeInstance();
        _executionCoroutine = null;
        ReportPatternCompleted("GroundSpikeCompleted");
    }

    /// <summary>
    /// 보스 패턴 NetworkObject가 Spawn된 상태이면 패턴 2 VFX를 클라이언트에 브로드캐스트하고, 아니면 로컬에서 재생한다.
    /// </summary>
    private void PlaySynchronizedVfxOrWarn(E_EffectId effectId, GameObject vfxPrefab, Vector3 position, string missingReason, bool isWarningVfx)
    {
        NetworkManager networkManager = NetworkManager.Singleton; // RPC 연출 동기화 사용 가능 여부를 판단하기 위한 현재 NGO 세션
        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;

        if (shouldUseNetwork && IsSpawned)
        {
            BroadcastGroundSpikeVfxRpc((int)effectId, position, isWarningVfx);
            return;
        }

        if (shouldUseNetwork && !IsSpawned)
        {
            LogFailureOnce("GroundSpikeVfxNetworkObjectNotSpawned");
        }

        PlayLocalVfxOrWarn(effectId, vfxPrefab, position, missingReason);
    }

    /// <summary>
    /// 서버에서 확정한 패턴 2 VFX 재생 요청을 수신하고 클라이언트와 Host에서 로컬 연출만 수행한다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void BroadcastGroundSpikeVfxRpc(int effectIdValue, Vector3 position, bool isWarningVfx)
    {
        ResolveReferences();

        GroundSpikePatternSettings settings = default; // 선택적 프리팹 fallback 연출을 찾기 위한 로컬 설정 복사본

        if (_bossController != null && _bossController.PatternData != null)
        {
            _bossController.PatternData.TryGetGroundSpikePattern(_bossController.CurrentPatternId, out settings);
        }

        GameObject fallbackPrefab = isWarningVfx ? settings.WarningVfxPrefab : settings.AttackVfxPrefab; // EffectService ID가 없을 때 사용할 선택적 프리팹 fallback
        string missingReason = isWarningVfx ? "WarningVFXMissing" : "AttackVFXMissing"; // EffectService ID와 프리팹 fallback이 모두 없을 때 기록할 사유

        PlayLocalVfxOrWarn((E_EffectId)effectIdValue, fallbackPrefab, position, missingReason);
    }

    /// <summary>
    /// 패턴 2 VFX를 EffectService 우선으로 재생하고, EffectId가 없을 경우에만 프리팹 생성 fallback을 사용한다.
    /// </summary>
    private void PlayLocalVfxOrWarn(E_EffectId effectId, GameObject vfxPrefab, Vector3 position, string missingReason)
    {
        if (effectId != E_EffectId.None)
        {
            if (EffectService.Instance == null)
            {
                LogFailureOnce("EffectServiceMissing");
            }
            else
            {
                EffectService.Instance.Play(effectId, position);
                return;
            }
        }

        if (vfxPrefab == null)
        {
            LogFailureOnce(missingReason);
            return;
        }

        LogFailureOnce("VfxPrefabFallbackUsed");
        Instantiate(vfxPrefab, position, Quaternion.identity);
    }

    /// <summary>
    /// 권한 인스턴스에서 스파이크 오브젝트를 생성하고 필요 시 NetworkObject를 Spawn한다.
    /// </summary>
    private bool TrySpawnSpike(GameObject spikePrefab, Vector3 spikePosition, out GameObject spikeInstance)
    {
        spikeInstance = null;
        if (spikePrefab == null)
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // 현재 NGO 세션 상태
        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;
        NetworkObject prefabNetworkObject = spikePrefab.GetComponent<NetworkObject>(); // 네트워크 스폰 여부 판단 기준

        if (shouldUseNetwork && !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("SpikeSpawnRequestedWithoutAuthority");
            return false;
        }

        LogSpikeObjectPoolFallbackOnce();
        spikeInstance = Instantiate(spikePrefab, spikePosition, Quaternion.identity);
        if (spikeInstance == null)
        {
            return false;
        }

        if (!shouldUseNetwork)
        {
            return true;
        }

        if (prefabNetworkObject == null)
        {
            LogNetworkSpawnFallbackOnce("Spike 프리팹에 NetworkObject가 없어 권한 인스턴스에서만 생성됨.");
            return true;
        }

        NetworkObject spawnedNetworkObject = spikeInstance.GetComponent<NetworkObject>(); // 런타임 생성된 NetworkObject
        if (spawnedNetworkObject == null)
        {
            LogFailureOnce("SpawnedSpikeNetworkObjectMissing");
            return true;
        }

        if (!spawnedNetworkObject.IsSpawned)
        {
            LogNetworkSpawnFallbackOnce("NetworkObject Pool이 없어 Instantiate + Spawn 방식 사용됨.");
            spawnedNetworkObject.Spawn(true);
        }

        return true;
    }

    /// <summary>
    /// 생성된 스파이크에서 사용할 Collider2D를 찾아 활성화한다.
    /// </summary>
    private void EnableSpikeHitCollider(GameObject spikeInstance)
    {
        if (spikeInstance == null)
        {
            LogFailureOnce("SpikeInstanceMissingForHitCollider");
            return;
        }

        _activeSpikeHitCollider = spikeInstance.GetComponent<Collider2D>();
        if (_activeSpikeHitCollider == null)
        {
            _activeSpikeHitCollider = spikeInstance.GetComponentInChildren<Collider2D>();
        }

        if (_activeSpikeHitCollider == null)
        {
            LogFailureOnce("SpikeHitColliderMissing");
            return;
        }

        _activeSpikeHitCollider.enabled = true;
    }

    /// <summary>
    /// 생성된 가시 프리팹의 Collider2D 범위를 기준으로 HitReceiver 대상에게 스파이크 피해를 적용한다.
    /// </summary>
    private void ApplySpikeHit(GroundSpikePatternSettings settings, Vector3 spikePosition)
    {
        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("SpikeHitAuthorityMissing");
            return;
        }

        if (_activeSpikeHitCollider == null)
        {
            LogFailureOnce("SpikeHitColliderMissingForOverlap");
            return;
        }

        _hitColliderList.Clear();
        _hitReceiverList.Clear();
        EnsureHitColliderListCapacity();

        ConfigureSpikeHitFilter(settings.SpikeTargetLayerMask);

        _activeSpikeHitCollider.Overlap(_spikeHitFilter, _hitColliderList); // 가시 프리팹 Collider2D가 실제로 겹친 피격 후보를 수집

        for (int index = 0; index < _hitColliderList.Count; index++)
        {
            HitReceiver receiver = ResolveHitReceiver(_hitColliderList[index]);
            if (receiver == null || _hitReceiverList.Contains(receiver))
            {
                continue;
            }

            _hitReceiverList.Add(receiver);
        }

        for (int i = 0; i < _hitReceiverList.Count; i++)
        {
            SendSpikeHit(settings, spikePosition, _hitReceiverList[i]);
        }

        _hitReceiverList.Clear();
    }

    /// <summary>
    /// 기존 Hit 시스템을 통해 단일 HitRequest를 전달한다.
    /// </summary>
    private void SendSpikeHit(GroundSpikePatternSettings settings, Vector3 spikePosition, HitReceiver receiver)
    {
        Vector3 targetPosition = receiver.transform.position; // 히트 위치
        Vector3 hitDirection = targetPosition - spikePosition; // 방향 계산

        if (hitDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            hitDirection = Vector3.up;
            LogFailureOnce("SpikeHitDirectionFallback");
        }
        else
        {
            hitDirection.Normalize();
        }

        string hitId = BuildSpikeHitId(receiver);
        HitRequest request = new HitRequest(
            hitId: hitId,
            rawDamage: settings.SpikeDamage,
            attacker: gameObject,
            hitPoint: targetPosition,
            hitDirection: hitDirection,
            statusTag: settings.StatusTag,
            requestTime: Time.time);

        receiver.ReceiveHit(request);
    }

    /// <summary>
    /// 기존 HitReceiver 중복 방지 로직을 유지하기 위한 고유 HitId를 생성한다.
    /// </summary>
    private string BuildSpikeHitId(HitReceiver receiver)
    {
        int receiverId = receiver != null ? receiver.gameObject.GetInstanceID() : 0; // Receiver ID
        int executionId = CurrentExecutionId; // 패턴 실행 ID
        int hitSerial = _nextSpikeHitSerial; // 개별 히트 시리얼
        _nextSpikeHitSerial++;

        return $"{gameObject.GetInstanceID()}:{executionId}:{receiverId}:GroundSpike:{hitSerial}";
    }

    /// <summary>
    /// Collider에서 HitReceiver를 찾는다.
    /// </summary>
    private HitReceiver ResolveHitReceiver(Collider2D candidateCollider)
    {
        if (candidateCollider == null)
        {
            return null;
        }

        HitReceiver receiver = candidateCollider.GetComponent<HitReceiver>();
        if (receiver != null)
        {
            return receiver;
        }

        receiver = candidateCollider.GetComponentInParent<HitReceiver>();
        if (receiver != null)
        {
            return receiver;
        }

        return candidateCollider.GetComponentInChildren<HitReceiver>();
    }

    /// <summary>
    /// 활성화된 스파이크 히트 콜라이더를 비활성화한다.
    /// </summary>
    private void DisableActiveSpikeHitCollider()
    {
        if (_activeSpikeHitCollider == null)
        {
            return;
        }

        _activeSpikeHitCollider.enabled = false;
        _activeSpikeHitCollider = null;
    }

    /// <summary>
    /// 역할을 마친 가시 인스턴스의 Collider2D를 비활성화하고 네트워크 상태에 맞게 제거한다.
    /// </summary>
    private void CleanupActiveSpikeInstance()
    {
        DisableActiveSpikeHitCollider();

        if (_activeSpikeInstance == null)
        {
            return;
        }

        NetworkObject spikeNetworkObject = _activeSpikeInstance.GetComponent<NetworkObject>(); // NGO Spawn 상태에 따라 제거 방식을 결정할 NetworkObject
        if (spikeNetworkObject != null && spikeNetworkObject.IsSpawned)
        {
            if (_bossController != null && _bossController.IsBossLogicAuthority())
            {
                spikeNetworkObject.Despawn(true);
            }
            else
            {
                LogFailureOnce("SpikeDespawnAuthorityMissing");
            }

            _activeSpikeInstance = null;
            return;
        }

        Destroy(_activeSpikeInstance);
        _activeSpikeInstance = null;
    }

    /// <summary>
    /// 실행 중인 코루틴을 중지하고 참조를 초기화한다.
    /// </summary>
    private void StopExecutionCoroutine()
    {
        if (_executionCoroutine == null)
        {
            return;
        }

        StopCoroutine(_executionCoroutine);
        _executionCoroutine = null;
    }

    /// <summary>
    /// NetworkObject fallback 경고를 1회만 출력한다.
    /// </summary>
    private void LogNetworkSpawnFallbackOnce(string message)
    {
        if (_hasLoggedNetworkSpawnFallback)
        {
            return;
        }

        Debug.LogWarning($"[BossGroundSpikePattern] {message} object={name}", this);
        _hasLoggedNetworkSpawnFallback = true;
    }

    /// <summary>
    /// ObjectPool 미사용 fallback 경고를 1회만 출력한다.
    /// </summary>
    private void LogSpikeObjectPoolFallbackOnce()
    {
        if (_hasLoggedSpikeObjectPoolFallback)
        {
            return;
        }

        Debug.LogWarning($"[BossGroundSpikePattern] ObjectPool이 없어 Instantiate 사용. object={name}", this);
        _hasLoggedSpikeObjectPoolFallback = true;
    }

    /// <summary>
    /// 경고 로그를 1회 출력 후 패턴 취소를 수행한다.
    /// </summary>
    private void CancelPatternWithWarning(string reason)
    {
        LogFailureOnce(reason);
        ReportPatternCancelled(reason);
    }

    /// <summary>
    /// 동일 GameObject에서 참조를 해결한다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_bossController == null)
        {
            _bossController = GetComponent<BossController>();
        }
    }

    /// <summary>
    /// 스파이크 히트용 ContactFilter를 구성한다.
    /// </summary>
    private void ConfigureSpikeHitFilter(LayerMask targetLayerMask)
    {
        _spikeHitFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = targetLayerMask,
            useTriggers = true
        };
    }

    /// <summary>
    /// 가시 프리팹 Collider2D 겹침 결과를 받을 리스트가 디자이너 설정 기준 용량을 확보했는지 확인한다.
    /// </summary>
    private void EnsureHitColliderListCapacity()
    {
        if (_hitColliderList.Capacity >= _maxHitColliderCandidates)
        {
            return;
        }

        _hitColliderList.Capacity = _maxHitColliderCandidates;
    }
}
