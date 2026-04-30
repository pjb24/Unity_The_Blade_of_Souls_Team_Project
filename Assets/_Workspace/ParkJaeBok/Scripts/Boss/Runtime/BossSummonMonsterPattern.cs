using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 재사용 가능한 부분 Fisher-Yates 버퍼를 사용하여
/// 중복되지 않는 몬스터 스폰 위치를 선택하고 Pattern 3을 실행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossSummonMonsterPattern : BossPatternBase
{
    [Header("필수 참조")]
    [Tooltip("권한, 패턴 데이터, Anchor 참조를 소유하는 BossController")]
    [SerializeField] private BossController _bossController; // Pattern 3 실행에 필요한 보스 권한 및 공유 데이터

    [Tooltip("몬스터 스폰 위치를 제공하는 씬 Anchor 세트")]
    [SerializeField] private BossPatternAnchorSet _anchorSet; // Pattern 3에서 사용하는 씬 스폰 위치 데이터

    private int[] _spawnPointIndexBuffer; // 실행 시 모든 스폰 위치 인덱스를 채워 재사용하는 버퍼
    private Transform[] _selectedSpawnPointBuffer; // 셔플된 인덱스 기반으로 선택된 스폰 위치를 저장하는 버퍼
    private int _selectedSpawnPointCount; // 최근 실행에서 유효하게 선택된 스폰 위치 개수

    private bool _hasLoggedSpawnManagerMissing; // SpawnManager 없음 경고 중복 방지
    private bool _hasLoggedEnemySpawnerMissing; // EnemySpawner 없음 경고 중복 방지
    private bool _hasLoggedObjectPoolMissing; // ObjectPool 없음 경고 중복 방지
    private bool _hasLoggedNetworkObjectPoolMissing; // NetworkObject Pool 없음 경고 중복 방지
    private bool _hasLoggedEnemyAiMissing; // EnemyAI 없음 경고 중복 방지

    /// <summary>
    /// 최근 Pattern 3 실행에서 선택된 스폰 위치 개수 반환
    /// </summary>
    public int SelectedSpawnPointCount => _selectedSpawnPointCount;

    /// <summary>
    /// Pattern 3 시작 전에 필요한 참조를 설정한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 에디터에서 값 변경 시 참조를 갱신한다.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 공통 패턴 실행 API를 통해 Pattern 3을 1회 실행한다.
    /// </summary>
    protected override void OnPatternExecutionStarted()
    {
        ResolveReferences();

        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            ReportPatternFailed("PatternAuthorityMissing");
            return;
        }

        if (!TryGetSettings(out SummonMonsterPatternSettings settings))
        {
            CancelPatternWithWarning("MissingSummonMonsterSettings");
            return;
        }

        if (settings.MonsterPrefab == null)
        {
            CancelPatternWithWarning("MonsterPrefabMissing");
            return;
        }

        if (!ValidateSpawnPoints(out Transform[] spawnPoints))
        {
            return;
        }

        int actualSpawnCount = CalculateActualSpawnCount(settings, spawnPoints.Length);
        if (actualSpawnCount <= 0)
        {
            CancelPatternWithWarning("ActualSpawnCountInvalid");
            return;
        }

        SelectUniqueSpawnPoints(spawnPoints, actualSpawnCount);

        if (!SpawnSelectedMonsters(settings, actualSpawnCount))
        {
            ReportPatternFailed("SummonMonsterSpawnFailed");
            return;
        }

        ReportPatternCompleted("SummonMonsterCompleted");
    }

    /// <summary>
    /// 보스 패턴 데이터에서 Pattern 3 설정을 가져온다.
    /// </summary>
    private bool TryGetSettings(out SummonMonsterPatternSettings settings)
    {
        settings = default;
        ResolveReferences();

        if (_bossController == null || _bossController.PatternData == null)
        {
            return false;
        }

        if (!_bossController.PatternData.TryGetSummonMonsterPattern(_bossController.CurrentPatternId, out settings))
        {
            Debug.LogWarning($"[BossSummonMonsterPattern] PatternId에 해당하는 SummonMonster 설정 없음. object={name}, patternId={_bossController.CurrentPatternId}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 스폰 위치 배열이 유효한지 검사한다.
    /// </summary>
    private bool ValidateSpawnPoints(out Transform[] spawnPoints)
    {
        spawnPoints = null;

        if (_anchorSet == null)
        {
            CancelPatternWithWarning("SpawnPointsMissing");
            return false;
        }

        spawnPoints = _anchorSet.MonsterSpawnPoints;

        if (spawnPoints == null)
        {
            CancelPatternWithWarning("SpawnPointsNull");
            return false;
        }

        if (spawnPoints.Length == 0)
        {
            CancelPatternWithWarning("SpawnPointsEmpty");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 요청된 SpawnCount와 실제 스폰 위치 개수를 기준으로 최종 스폰 개수를 계산한다.
    /// </summary>
    private int CalculateActualSpawnCount(SummonMonsterPatternSettings settings, int spawnPointCount)
    {
        int requestedSpawnCount = settings.SpawnCount; // 디자이너가 설정한 SpawnCount

        if (requestedSpawnCount < 1)
        {
            LogFailureOnce("SpawnCountRuntimeClampToOne");
            requestedSpawnCount = 1;
        }

        if (requestedSpawnCount > spawnPointCount)
        {
            LogFailureOnce("SpawnCountClampedToSpawnPointCount");
            return spawnPointCount;
        }

        return requestedSpawnCount;
    }

    /// <summary>
    /// 부분 Fisher-Yates 셔플을 사용하여 중복되지 않는 스폰 위치를 선택한다.
    /// </summary>
    private void SelectUniqueSpawnPoints(Transform[] spawnPoints, int actualSpawnCount)
    {
        EnsureSelectionBuffers(spawnPoints.Length);
        _selectedSpawnPointCount = actualSpawnCount;

        for (int index = 0; index < spawnPoints.Length; index++)
        {
            _spawnPointIndexBuffer[index] = index;
        }

        for (int index = 0; index < actualSpawnCount; index++)
        {
            int randomIndex = Random.Range(index, spawnPoints.Length); // 셔플 단계에서 사용하는 랜덤 값
            int selectedIndex = _spawnPointIndexBuffer[randomIndex]; // 선택된 스폰 위치 인덱스

            _spawnPointIndexBuffer[randomIndex] = _spawnPointIndexBuffer[index];
            _spawnPointIndexBuffer[index] = selectedIndex;

            _selectedSpawnPointBuffer[index] = spawnPoints[selectedIndex];

            if (_selectedSpawnPointBuffer[index] == null)
            {
                LogFailureOnce("SelectedSpawnPointNull");
            }
        }
    }

    /// <summary>
    /// 선택된 스폰 위치에 몬스터를 생성한다.
    /// 기존 시스템이 있으면 사용하고, 없으면 fallback 경로를 사용한다.
    /// </summary>
    private bool SpawnSelectedMonsters(SummonMonsterPatternSettings settings, int actualSpawnCount)
    {
        WarnMissingExistingSpawnPathsOnce();

        bool spawnedAnyMonster = false; // 최소 1마리라도 생성 성공했는지 여부
        bool playedAttackCue = false; // 소환 공격 연출 Cue를 이미 재생했는지 여부

        for (int index = 0; index < actualSpawnCount; index++)
        {
            Transform spawnPoint = _selectedSpawnPointBuffer[index]; // 현재 몬스터에 사용할 스폰 위치

            if (spawnPoint == null)
            {
                LogFailureOnce("SelectedSpawnPointNullDuringSpawn");
                continue;
            }

            if (!TrySpawnMonsterFallback(settings.MonsterPrefab, spawnPoint, out GameObject spawnedMonster))
            {
                continue;
            }

            ValidateSpawnedEnemyAiFlow(spawnedMonster);

            spawnedAnyMonster = true;

            MarkPatternEffectApplied();

            if (!playedAttackCue)
            {
                _bossController.PlayPresentationCue(
                    E_BossPresentationCue.PatternAttack,
                    E_BossPatternType.SummonMonster,
                    spawnPoint.position
                );

                playedAttackCue = true;
            }
        }

        return spawnedAnyMonster;
    }

    /// <summary>
    /// MonsterPrefab Instantiate fallback + 필요 시 NetworkObject Spawn 수행
    /// </summary>
    private bool TrySpawnMonsterFallback(GameObject monsterPrefab, Transform spawnPoint, out GameObject spawnedMonster)
    {
        spawnedMonster = null;

        if (monsterPrefab == null || spawnPoint == null)
        {
            LogFailureOnce("MonsterSpawnFallbackInputMissing");
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // NGO 세션 싱글톤
        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;

        if (shouldUseNetwork && (_bossController == null || !_bossController.IsBossLogicAuthority()))
        {
            LogFailureOnce("MonsterNetworkSpawnWithoutAuthority");
            return false;
        }

        LogInstantiateFallbackOnce();

        spawnedMonster = Instantiate(monsterPrefab, spawnPoint.position, spawnPoint.rotation);

        if (spawnedMonster == null)
        {
            LogFailureOnce("MonsterInstantiateFailed");
            return false;
        }

        if (!shouldUseNetwork)
        {
            return true;
        }

        NetworkObject prefabNetworkObject = monsterPrefab.GetComponent<NetworkObject>(); // 프리팹에 NetworkObject 존재 여부

        if (prefabNetworkObject == null)
        {
            return true;
        }

        NetworkObject spawnedNetworkObject = spawnedMonster.GetComponent<NetworkObject>(); // 런타임에서 생성된 NetworkObject

        if (spawnedNetworkObject == null)
        {
            LogFailureOnce("SpawnedMonsterNetworkObjectMissing");
            return true;
        }

        if (!spawnedNetworkObject.IsSpawned)
        {
            LogNetworkObjectPoolMissingOnce();
            spawnedNetworkObject.Spawn(true);
        }

        return true;
    }

    /// <summary>
    /// 기존 SpawnManager / EnemySpawner / ObjectPool이 없음을 1회 경고 출력
    /// </summary>
    private void WarnMissingExistingSpawnPathsOnce()
    {
        if (!_hasLoggedSpawnManagerMissing)
        {
            Debug.LogWarning($"[BossSummonMonsterPattern] SpawnManager 없음. EnemySpawner/ObjectPool/fallback 경로 사용. object={name}", this);
            _hasLoggedSpawnManagerMissing = true;
        }

        if (!_hasLoggedEnemySpawnerMissing)
        {
            Debug.LogWarning($"[BossSummonMonsterPattern] EnemySpawner 없음. MonsterPrefab fallback 사용. object={name}", this);
            _hasLoggedEnemySpawnerMissing = true;
        }

        if (!_hasLoggedObjectPoolMissing)
        {
            Debug.LogWarning($"[BossSummonMonsterPattern] ObjectPool 없음. Instantiate fallback 사용. object={name}", this);
            _hasLoggedObjectPoolMissing = true;
        }
    }

    /// <summary>
    /// 생성된 몬스터가 기존 EnemyAI 흐름을 사용할 수 있는지 검증한다.
    /// </summary>
    private void ValidateSpawnedEnemyAiFlow(GameObject spawnedMonster)
    {
        if (spawnedMonster == null)
        {
            return;
        }

        if (spawnedMonster.GetComponent<EnemyAIController>() != null ||
            spawnedMonster.GetComponent<StationaryRangedEnemyController>() != null)
        {
            return;
        }

        if (_hasLoggedEnemyAiMissing)
        {
            return;
        }

        Debug.LogWarning($"[BossSummonMonsterPattern] 생성된 몬스터에 기존 EnemyAI 컨트롤러 없음. prefabInstance={spawnedMonster.name}", spawnedMonster);
        _hasLoggedEnemyAiMissing = true;
    }

    /// <summary>
    /// Instantiate fallback 사용을 1회만 로그 출력
    /// </summary>
    private void LogInstantiateFallbackOnce()
    {
        if (_hasLoggedObjectPoolMissing)
        {
            return;
        }

        Debug.LogWarning($"[BossSummonMonsterPattern] MonsterPrefab Instantiate fallback 사용. object={name}", this);
        _hasLoggedObjectPoolMissing = true;
    }

    /// <summary>
    /// NetworkObject Pool이 없어 직접 Spawn 사용하는 경우 1회 로그 출력
    /// </summary>
    private void LogNetworkObjectPoolMissingOnce()
    {
        if (_hasLoggedNetworkObjectPoolMissing)
        {
            return;
        }

        Debug.LogWarning($"[BossSummonMonsterPattern] NetworkObject Pool 없음. 직접 Instantiate + Spawn 수행. object={name}", this);
        _hasLoggedNetworkObjectPoolMissing = true;
    }

    /// <summary>
    /// 최근 실행에서 선택된 스폰 위치를 외부에서 안전하게 조회한다.
    /// </summary>
    public bool TryGetSelectedSpawnPoint(int index, out Transform spawnPoint)
    {
        spawnPoint = null;

        if (index < 0 || index >= _selectedSpawnPointCount || _selectedSpawnPointBuffer == null)
        {
            Debug.LogWarning($"[BossSummonMonsterPattern] 스폰 위치 인덱스 범위 초과. object={name}, index={index}, count={_selectedSpawnPointCount}", this);
            return false;
        }

        spawnPoint = _selectedSpawnPointBuffer[index];

        if (spawnPoint == null)
        {
            Debug.LogWarning($"[BossSummonMonsterPattern] 선택된 스폰 위치가 null. object={name}, index={index}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 현재 스폰 위치 개수에 맞게 재사용 버퍼를 초기화한다.
    /// </summary>
    private void EnsureSelectionBuffers(int spawnPointCount)
    {
        if (_spawnPointIndexBuffer == null || _spawnPointIndexBuffer.Length != spawnPointCount)
        {
            _spawnPointIndexBuffer = new int[spawnPointCount];
        }

        if (_selectedSpawnPointBuffer == null || _selectedSpawnPointBuffer.Length != spawnPointCount)
        {
            _selectedSpawnPointBuffer = new Transform[spawnPointCount];
        }
    }

    /// <summary>
    /// 경고 로그와 함께 패턴을 취소 처리한다.
    /// </summary>
    private void CancelPatternWithWarning(string reason)
    {
        LogFailureOnce(reason);
        ReportPatternCancelled(reason);
    }

    /// <summary>
    /// 동일 보스 오브젝트에서 참조를 자동으로 찾는다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_bossController == null)
        {
            _bossController = GetComponent<BossController>();
        }

        if (_anchorSet == null && _bossController != null)
        {
            _anchorSet = _bossController.AnchorSet;
        }

        if (_anchorSet == null)
        {
            _anchorSet = GetComponent<BossPatternAnchorSet>();
        }
    }
}
