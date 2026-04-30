using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Executes Pattern 3 selection by choosing unique monster spawn points with a reusable partial Fisher-Yates buffer.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossSummonMonsterPattern : BossPatternBase
{
    [Header("Required References")]
    [Tooltip("Boss controller that owns authority, pattern data, and anchor references.")]
    [SerializeField] private BossController _bossController; // Boss authority and shared data source for Pattern 3.

    [Tooltip("Scene anchor set that provides monster spawn points.")]
    [SerializeField] private BossPatternAnchorSet _anchorSet; // Scene monster spawn point source used by Pattern 3.

    private int[] _spawnPointIndexBuffer; // Reusable index buffer initialized with every spawn point index at execution start.
    private Transform[] _selectedSpawnPointBuffer; // Reusable selected spawn point buffer filled from the shuffled index buffer.
    private int _selectedSpawnPointCount; // Number of valid selected entries written during the latest execution.
    private bool _hasLoggedSpawnManagerMissing; // Prevents repeated missing SpawnManager warnings from this pattern instance.
    private bool _hasLoggedEnemySpawnerMissing; // Prevents repeated missing EnemySpawner warnings from this pattern instance.
    private bool _hasLoggedObjectPoolMissing; // Prevents repeated missing ObjectPool warnings from this pattern instance.
    private bool _hasLoggedNetworkObjectPoolMissing; // Prevents repeated missing NetworkObject Pool warnings from this pattern instance.
    private bool _hasLoggedEnemyAiMissing; // Prevents repeated missing EnemyAI component warnings from this pattern instance.

    /// <summary>
    /// Gets the number of spawn points selected during the latest Pattern 3 execution.
    /// </summary>
    public int SelectedSpawnPointCount => _selectedSpawnPointCount;

    /// <summary>
    /// Resolves required runtime references before Pattern 3 starts.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Refreshes Pattern 3 references while designers edit the boss object.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Starts Pattern 3 once through the common pattern execution API.
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
    /// Resolves Pattern 3 settings from the boss pattern data asset.
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
            Debug.LogWarning($"[BossSummonMonsterPattern] SummonMonster settings were not found for PatternId. object={name}, patternId={_bossController.CurrentPatternId}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that the scene has a usable monster spawn point array.
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
    /// Calculates the actual spawn count from the requested SpawnCount and available spawn point count.
    /// </summary>
    private int CalculateActualSpawnCount(SummonMonsterPatternSettings settings, int spawnPointCount)
    {
        int requestedSpawnCount = settings.SpawnCount; // Designer-authored SpawnCount read from Pattern 3 settings.
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
    /// Selects unique spawn points by partially shuffling the reusable index buffer.
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
            int randomIndex = Random.Range(index, spawnPoints.Length); // Random source used by the authority instance for this partial shuffle step.
            int selectedIndex = _spawnPointIndexBuffer[randomIndex]; // SpawnPoint index selected for this output slot.
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
    /// Spawns monsters at the selected spawn points through existing systems when available or the explicit fallback path.
    /// </summary>
    private bool SpawnSelectedMonsters(SummonMonsterPatternSettings settings, int actualSpawnCount)
    {
        WarnMissingExistingSpawnPathsOnce();

        bool spawnedAnyMonster = false; // Whether at least one monster was successfully created by Pattern 3.
        bool playedAttackCue = false; // Whether the summon attack presentation cue has already been emitted for this execution.
        for (int index = 0; index < actualSpawnCount; index++)
        {
            Transform spawnPoint = _selectedSpawnPointBuffer[index]; // Selected scene spawn point used for this monster.
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
                _bossController.PlayPresentationCue(E_BossPresentationCue.PatternAttack, E_BossPatternType.SummonMonster, spawnPoint.position);
                playedAttackCue = true;
            }
        }

        return spawnedAnyMonster;
    }

    /// <summary>
    /// Uses the explicit MonsterPrefab Instantiate fallback and server-side NetworkObject Spawn when required.
    /// </summary>
    private bool TrySpawnMonsterFallback(GameObject monsterPrefab, Transform spawnPoint, out GameObject spawnedMonster)
    {
        spawnedMonster = null;
        if (monsterPrefab == null || spawnPoint == null)
        {
            LogFailureOnce("MonsterSpawnFallbackInputMissing");
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // NGO session singleton used to decide network spawn behavior.
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

        NetworkObject prefabNetworkObject = monsterPrefab.GetComponent<NetworkObject>(); // NetworkObject marker on the prefab that requires NGO Spawn.
        if (prefabNetworkObject == null)
        {
            return true;
        }

        NetworkObject spawnedNetworkObject = spawnedMonster.GetComponent<NetworkObject>(); // Runtime NetworkObject spawned by the Host or Server.
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
    /// Warns once that this repository has no reusable monster SpawnManager, EnemySpawner, or ObjectPool path to call.
    /// </summary>
    private void WarnMissingExistingSpawnPathsOnce()
    {
        if (!_hasLoggedSpawnManagerMissing)
        {
            Debug.LogWarning($"[BossSummonMonsterPattern] Existing SpawnManager was not found. Pattern 3 will continue to EnemySpawner/ObjectPool/fallback checks. object={name}", this);
            _hasLoggedSpawnManagerMissing = true;
        }

        if (!_hasLoggedEnemySpawnerMissing)
        {
            Debug.LogWarning($"[BossSummonMonsterPattern] Existing EnemySpawner was not found. Pattern 3 will use MonsterPrefab fallback. object={name}", this);
            _hasLoggedEnemySpawnerMissing = true;
        }

        if (!_hasLoggedObjectPoolMissing)
        {
            Debug.LogWarning($"[BossSummonMonsterPattern] Existing monster ObjectPool was not found. Pattern 3 will use MonsterPrefab Instantiate fallback. object={name}", this);
            _hasLoggedObjectPoolMissing = true;
        }
    }

    /// <summary>
    /// Reports whether the spawned monster can run through the existing EnemyAI component flow.
    /// </summary>
    private void ValidateSpawnedEnemyAiFlow(GameObject spawnedMonster)
    {
        if (spawnedMonster == null)
        {
            return;
        }

        if (spawnedMonster.GetComponent<EnemyAIController>() != null || spawnedMonster.GetComponent<StationaryRangedEnemyController>() != null)
        {
            return;
        }

        if (_hasLoggedEnemyAiMissing)
        {
            return;
        }

        Debug.LogWarning($"[BossSummonMonsterPattern] Spawned monster has no recognized existing EnemyAI controller. prefabInstance={spawnedMonster.name}", spawnedMonster);
        _hasLoggedEnemyAiMissing = true;
    }

    /// <summary>
    /// Logs direct MonsterPrefab Instantiate fallback once when no existing spawn or pool path is available.
    /// </summary>
    private void LogInstantiateFallbackOnce()
    {
        if (_hasLoggedObjectPoolMissing)
        {
            return;
        }

        Debug.LogWarning($"[BossSummonMonsterPattern] Pattern 3 uses MonsterPrefab Instantiate fallback. object={name}", this);
        _hasLoggedObjectPoolMissing = true;
    }

    /// <summary>
    /// Logs the missing NetworkObject Pool fallback once before using NGO Spawn directly.
    /// </summary>
    private void LogNetworkObjectPoolMissingOnce()
    {
        if (_hasLoggedNetworkObjectPoolMissing)
        {
            return;
        }

        Debug.LogWarning($"[BossSummonMonsterPattern] NetworkObject Pool was not found. Host/Server will Instantiate MonsterPrefab and call NetworkObject.Spawn. object={name}", this);
        _hasLoggedNetworkObjectPoolMissing = true;
    }

    /// <summary>
    /// Returns a selected spawn point from the latest execution without exposing the mutable buffer.
    /// </summary>
    public bool TryGetSelectedSpawnPoint(int index, out Transform spawnPoint)
    {
        spawnPoint = null;
        if (index < 0 || index >= _selectedSpawnPointCount || _selectedSpawnPointBuffer == null)
        {
            Debug.LogWarning($"[BossSummonMonsterPattern] Selected spawn point index out of range. object={name}, index={index}, count={_selectedSpawnPointCount}", this);
            return false;
        }

        spawnPoint = _selectedSpawnPointBuffer[index];
        if (spawnPoint == null)
        {
            Debug.LogWarning($"[BossSummonMonsterPattern] Selected spawn point is null. object={name}, index={index}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ensures reusable selection buffers can hold the current spawn point count.
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
    /// Reports a cancellation reason with a one-time warning for this execution.
    /// </summary>
    private void CancelPatternWithWarning(string reason)
    {
        LogFailureOnce(reason);
        ReportPatternCancelled(reason);
    }

    /// <summary>
    /// Resolves optional references from the same boss GameObject.
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
