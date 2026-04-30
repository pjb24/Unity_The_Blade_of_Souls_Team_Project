using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 약점 오브젝트를 아직 생성하지 않은 상태에서
/// Pattern 4 진입 및 Groggy 전환을 처리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossWeakPointPattern : BossPatternBase
{
    [Header("필수 참조")]
    [Tooltip("권한, Pattern 4 상태 플래그, Groggy 타이밍을 관리하는 BossController")]
    [SerializeField] private BossController _bossController; // Pattern 4 상태를 소유하는 보스 권한 및 데이터

    [Tooltip("약점 배치 영역을 제공하는 씬 Anchor 세트")]
    [SerializeField] private BossPatternAnchorSet _anchorSet; // Pattern 4 위치 선택에 사용하는 약점 영역 데이터

    private Coroutine _entryFallbackCoroutine; // 애니메이션 이벤트가 없을 경우 Pattern 4 진입을 완료시키는 코루틴
    private Coroutine _weakPointTimeLimitCoroutine; // 제한 시간 내 약점이 파괴되지 않았을 때 Pattern 4를 종료시키는 코루틴

    private Vector3[] _weakPointPositionBuffer; // 선택된 약점 위치를 저장하는 재사용 버퍼
    private BoxCollider2D[] _weakPointAreaBuffer; // 유효한 약점 영역을 저장하는 재사용 버퍼
    private BossWeakPointObject[] _weakPointObjectBuffer; // 생성된 약점 오브젝트를 저장하는 재사용 버퍼
    private bool[] _weakPointDestroyedBuffer; // 약점 파괴 상태를 저장하는 재사용 버퍼
    private HealthComponent[] _timeLimitDamageTargetBuffer; // 시간 초과 시 데미지를 줄 Player HealthComponent 버퍼

    private BossPatternData _bufferPatternData; // 버퍼 생성 시 사용된 PatternData
    private string _activePatternId = string.Empty; // Pattern 4 시작 시 저장된 PatternId

    private int _selectedWeakPointPositionCount; // 선택된 약점 위치 개수
    private int _spawnedWeakPointCount; // 생성된 약점 개수
    private int _destroyedWeakPointCount; // 파괴된 약점 개수
    private int _validWeakPointAreaCount; // 유효한 약점 영역 개수

    private bool _isEntryResolved; // 진입 완료 또는 실패 여부
    private bool _isWeakPointFlowResolved; // 약점 단계 완료 여부

    private bool _hasLoggedWeakPointInstantiateFallback; // Instantiate fallback 경고 중복 방지
    private bool _hasLoggedWeakPointNetworkPoolFallback; // NetworkObject Pool fallback 경고 중복 방지

    /// <summary>
    /// 현재 실행에서 선택된 약점 위치 개수 반환
    /// </summary>
    public int SelectedWeakPointPositionCount => _selectedWeakPointPositionCount;

    /// <summary>
    /// Pattern 4 시작 전에 참조를 초기화한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 에디터에서 값 수정 시 참조를 갱신한다.
    /// </summary>
    private void OnValidate()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Pattern 4 진입을 시작하고 애니메이션 이벤트 또는 fallback 타이머를 대기한다.
    /// </summary>
    protected override void OnPatternExecutionStarted()
    {
        ResolveReferences();

        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            ReportPatternFailed("PatternAuthorityMissing");
            return;
        }

        _isEntryResolved = false;
        _isWeakPointFlowResolved = false;
        _activePatternId = _bossController.CurrentPatternId;

        if (!TryGetSettings(out WeakPointPatternSettings settings))
        {
            FailEntry("MissingWeakPointSettings");
            return;
        }

        if (settings.WeakPointPrefab == null)
        {
            FailEntry("WeakPointPrefabMissing");
            return;
        }

        if (!TryPrepareWeakPointPositions(settings))
        {
            FailEntry("WeakPointPositionSelectionFailed");
            return;
        }

        _bossController.NotifyPatternFourEntryStarted();

        StartEntryFallbackTimer(settings.EntryAnimationFallbackSeconds);
    }

    /// <summary>
    /// 패턴이 취소되면 진입 fallback 타이머를 중지한다.
    /// </summary>
    protected override void OnPatternExecutionCancelled(string reason)
    {
        StopEntryFallbackTimer();
        StopWeakPointTimeLimitTimer();

        RemoveRemainingWeakPoints();
        ClearWeakPointRuntimeBuffers();

        _isEntryResolved = true;
        _isWeakPointFlowResolved = true;
    }

    /// <summary>
    /// 애니메이션 이벤트로 Pattern 4 진입 완료
    /// </summary>
    public void AnimationEvent_WeakPointEntryCompleted()
    {
        CompleteEntry(false);
    }

    /// <summary>
    /// 애니메이션 이벤트로 Pattern 4 진입 실패
    /// </summary>
    public void AnimationEvent_WeakPointEntryFailed()
    {
        FailEntry("WeakPointEntryAnimationEventFailed");
    }

    /// <summary>
    /// 설정된 시간 동안 Groggy 상태에 진입한다.
    /// </summary>
    public void EnterGroggy()
    {
        ResolveReferences();

        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("GroggyAuthorityMissing");
            return;
        }

        if (!TryGetSettings(out WeakPointPatternSettings settings))
        {
            LogFailureOnce("MissingWeakPointSettingsForGroggy");
            return;
        }

        _bossController.StartGroggyForDuration(settings.GroggyDurationSeconds, "Pattern4Groggy");
    }

    /// <summary>
    /// 보스 사망 시 모든 Pattern 4 오브젝트와 타이머를 정리한다.
    /// </summary>
    public void CleanupForBossDeath()
    {
        ResolveReferences();

        if (_bossController != null && !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("WeakPointDeathCleanupWithoutAuthority");
            return;
        }

        StopEntryFallbackTimer();
        StopWeakPointTimeLimitTimer();

        RemoveRemainingWeakPoints();
        ClearWeakPointRuntimeBuffers();

        _isEntryResolved = true;
        _isWeakPointFlowResolved = true;
    }

    /// <summary>
    /// 약점 오브젝트에서 파괴 이벤트를 전달받는다.
    /// </summary>
    public void HandleWeakPointDestroyed(BossWeakPointObject weakPointObject, int weakPointIndex)
    {
        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("WeakPointDestroyedWithoutAuthority");
            return;
        }

        if (weakPointIndex < 0 || weakPointIndex >= _selectedWeakPointPositionCount)
        {
            LogFailureOnce("WeakPointDestroyedIndexInvalid");
            return;
        }

        if (_weakPointObjectBuffer == null || _weakPointObjectBuffer[weakPointIndex] != weakPointObject)
        {
            LogFailureOnce("WeakPointDestroyedReferenceMismatch");
            return;
        }

        if (_weakPointDestroyedBuffer[weakPointIndex])
        {
            return;
        }

        _weakPointDestroyedBuffer[weakPointIndex] = true;
        _destroyedWeakPointCount++;

        Vector3 destroyPosition = weakPointObject != null
            ? weakPointObject.transform.position
            : _weakPointPositionBuffer[weakPointIndex]; // VFX 재생 위치

        _bossController.PlayPresentationCue(
            E_BossPresentationCue.WeakPointDestroyed,
            E_BossPatternType.WeakPoint,
            destroyPosition
        );

        PlayWeakPointDestroyVfx(destroyPosition);

        CleanupWeakPointObject(weakPointObject, weakPointIndex);

        if (AreAllWeakPointsDestroyed())
        {
            ResolveAllWeakPointsDestroyed();
        }
    }

    /// <summary>
    /// 보스 패턴 데이터에서 Pattern 4 설정을 가져온다.
    /// </summary>
    private bool TryGetSettings(out WeakPointPatternSettings settings)
    {
        settings = default;
        ResolveReferences();

        if (_bossController == null || _bossController.PatternData == null)
        {
            return false;
        }

        string patternId = !string.IsNullOrWhiteSpace(_activePatternId)
            ? _activePatternId
            : _bossController.CurrentPatternId; // Entry 이후 CurrentPatternId가 초기화되어도 유지되는 PatternId

        if (!_bossController.PatternData.TryGetWeakPointPattern(patternId, out settings))
        {
            Debug.LogWarning($"[BossWeakPointPattern] PatternId에 해당하는 WeakPoint 설정 없음. object={name}, patternId={patternId}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 현재 실행에서 선택된 약점 위치를 반환한다.
    /// </summary>
    public bool TryGetSelectedWeakPointPosition(int index, out Vector3 position)
    {
        position = Vector3.zero;

        if (index < 0 || index >= _selectedWeakPointPositionCount || _weakPointPositionBuffer == null)
        {
            Debug.LogWarning($"[BossWeakPointPattern] 약점 위치 인덱스 범위 초과. object={name}, index={index}, count={_selectedWeakPointPositionCount}", this);
            return false;
        }

        position = _weakPointPositionBuffer[index];
        return true;
    }

    /// <summary>
    /// 약점 영역을 검증하고 그 내부에서 위치를 선택한다.
    /// </summary>
    private bool TryPrepareWeakPointPositions(WeakPointPatternSettings settings)
    {
        ResolveReferences();

        _selectedWeakPointPositionCount = 0;
        _validWeakPointAreaCount = 0;

        if (_anchorSet == null)
        {
            LogFailureOnce("WeakPointAnchorSetMissing");
            return false;
        }

        BoxCollider2D[] weakPointAreas = _anchorSet.WeakPointAreas; // 씬에 배치된 약점 영역

        if (weakPointAreas == null)
        {
            LogFailureOnce("WeakPointAreasNull");
            return false;
        }

        if (weakPointAreas.Length == 0)
        {
            LogFailureOnce("WeakPointAreasEmpty");
            return false;
        }

        EnsureWeakPointBuffers(settings, weakPointAreas.Length);

        CollectValidWeakPointAreas(weakPointAreas);

        if (_validWeakPointAreaCount <= 0)
        {
            LogFailureOnce("WeakPointAreasValidEntryMissing");
            return false;
        }

        int targetWeakPointCount = settings.WeakPointCount; // 생성할 약점 개수

        if (targetWeakPointCount <= 0)
        {
            LogFailureOnce("WeakPointCountZero");
            return false;
        }

        SelectWeakPointPositions(settings, targetWeakPointCount);

        if (_selectedWeakPointPositionCount <= 0)
        {
            LogFailureOnce("WeakPointSelectablePositionMissing");
            return false;
        }

        return true;
    }

    /// <summary>
    /// PatternData 및 영역 수에 맞게 버퍼를 준비한다.
    /// </summary>
    private void EnsureWeakPointBuffers(WeakPointPatternSettings settings, int weakPointAreaCount)
    {
        BossPatternData currentPatternData = _bossController != null ? _bossController.PatternData : null; // 현재 PatternData

        int requiredPositionCount = Mathf.Max(1, settings.WeakPointCount); // 위치 버퍼 크기
        int requiredAreaCount = Mathf.Max(1, weakPointAreaCount); // 영역 버퍼 크기

        bool shouldRebuildBuffers = _bufferPatternData != currentPatternData;

        if (_weakPointPositionBuffer == null || _weakPointPositionBuffer.Length != requiredPositionCount)
        {
            shouldRebuildBuffers = true;
        }

        if (_weakPointAreaBuffer == null || _weakPointAreaBuffer.Length != requiredAreaCount)
        {
            shouldRebuildBuffers = true;
        }

        if (!shouldRebuildBuffers)
        {
            ClearWeakPointRuntimeBuffers();
            return;
        }

        _weakPointPositionBuffer = new Vector3[requiredPositionCount];
        _weakPointAreaBuffer = new BoxCollider2D[requiredAreaCount];
        _weakPointObjectBuffer = new BossWeakPointObject[requiredPositionCount];
        _weakPointDestroyedBuffer = new bool[requiredPositionCount];

        _bufferPatternData = currentPatternData;
    }

    /// <summary>
    /// 새로운 실행 전에 약점 관련 버퍼를 초기화한다.
    /// </summary>
    private void ClearWeakPointRuntimeBuffers()
    {
        if (_weakPointObjectBuffer != null)
        {
            for (int index = 0; index < _weakPointObjectBuffer.Length; index++)
            {
                _weakPointObjectBuffer[index] = null;
            }
        }

        if (_weakPointDestroyedBuffer != null)
        {
            for (int index = 0; index < _weakPointDestroyedBuffer.Length; index++)
            {
                _weakPointDestroyedBuffer[index] = false;
            }
        }

        _spawnedWeakPointCount = 0;
        _destroyedWeakPointCount = 0;
    }

    /// <summary>
    /// null이 아닌 WeakPointArea를 유효 버퍼에 복사한다.
    /// </summary>
    private void CollectValidWeakPointAreas(BoxCollider2D[] weakPointAreas)
    {
        for (int index = 0; index < weakPointAreas.Length; index++)
        {
            BoxCollider2D area = weakPointAreas[index]; // 현재 검사 중인 영역

            if (area == null)
            {
                LogFailureOnce("WeakPointAreaNullEntry");
                continue;
            }

            _weakPointAreaBuffer[_validWeakPointAreaCount] = area;
            _validWeakPointAreaCount++;
        }
    }

    /// <summary>
    /// 최소 거리 조건을 만족하는 약점 위치를 선택한다.
    /// </summary>
    private void SelectWeakPointPositions(WeakPointPatternSettings settings, int targetWeakPointCount)
    {
        float minDistanceSqr =
            settings.MinDistanceBetweenWeakPoints * settings.MinDistanceBetweenWeakPoints; // 거리 제곱값

        int retryCount = Mathf.Max(1, settings.WeakPointPositionRetryCount);

        for (int index = 0; index < targetWeakPointCount; index++)
        {
            if (!TrySelectOneWeakPointPosition(retryCount, minDistanceSqr, out Vector3 selectedPosition))
            {
                LogFailureOnce("WeakPointPositionRetryExceeded");
                continue;
            }

            _weakPointPositionBuffer[_selectedWeakPointPositionCount] = selectedPosition;
            _selectedWeakPointPositionCount++;
        }
    }

    /// <summary>
    /// 하나의 약점 위치를 재시도 제한 내에서 선택한다.
    /// </summary>
    private bool TrySelectOneWeakPointPosition(int retryCount, float minDistanceSqr, out Vector3 selectedPosition)
    {
        selectedPosition = Vector3.zero;

        for (int retryIndex = 0; retryIndex < retryCount; retryIndex++)
        {
            BoxCollider2D area =
                _weakPointAreaBuffer[Random.Range(0, _validWeakPointAreaCount)]; // 랜덤 영역 선택

            Vector3 candidatePosition = GetRandomPointInAreaBounds(area);

            if (!area.OverlapPoint(candidatePosition))
            {
                continue;
            }

            if (!IsFarEnoughFromSelectedWeakPoints(candidatePosition, minDistanceSqr))
            {
                continue;
            }

            selectedPosition = candidatePosition;
            return true;
        }

        return false;
    }

    /// <summary>
    /// BoxCollider2D 영역 내부의 랜덤 위치를 계산한다.
    /// </summary>
    private Vector3 GetRandomPointInAreaBounds(BoxCollider2D area)
    {
        Bounds bounds = area.bounds; // 월드 좌표 영역

        float x = Random.Range(bounds.min.x, bounds.max.x);
        float y = Random.Range(bounds.min.y, bounds.max.y);

        return new Vector3(x, y, area.transform.position.z);
    }

    /// <summary>
    /// 기존 선택된 약점들과 최소 거리 조건을 만족하는지 검사한다.
    /// </summary>
    private bool IsFarEnoughFromSelectedWeakPoints(Vector3 candidatePosition, float minDistanceSqr)
    {
        if (minDistanceSqr <= 0f)
        {
            return true;
        }

        for (int index = 0; index < _selectedWeakPointPositionCount; index++)
        {
            if ((candidatePosition - _weakPointPositionBuffer[index]).sqrMagnitude < minDistanceSqr)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Pattern 4 진입 완료를 위한 애니메이션 이벤트 fallback 타이머를 시작한다.
    /// </summary>
    private void StartEntryFallbackTimer(float fallbackSeconds)
    {
        StopEntryFallbackTimer();

        if (fallbackSeconds < 0f)
        {
            LogFailureOnce("EntryFallbackDurationRuntimeClamp");
            fallbackSeconds = 0f;
        }

        _entryFallbackCoroutine = StartCoroutine(RunEntryFallbackTimer(fallbackSeconds));
    }

    /// <summary>
    /// 애니메이션 이벤트를 기다리고, 이벤트가 오지 않으면 Warning과 함께 진입을 완료한다.
    /// </summary>
    private IEnumerator RunEntryFallbackTimer(float fallbackSeconds)
    {
        if (fallbackSeconds > 0f)
        {
            yield return new WaitForSeconds(fallbackSeconds);
        }

        _entryFallbackCoroutine = null;
        CompleteEntry(true);
    }

    /// <summary>
    /// Pattern 4 진입을 완료하고 약점 상태를 활성화한다.
    /// </summary>
    private void CompleteEntry(bool usedFallback)
    {
        if (_isEntryResolved)
        {
            return;
        }

        if (!IsExecuting)
        {
            LogFailureOnce("WeakPointEntryCompletedWhileNotExecuting");
            return;
        }

        StopEntryFallbackTimer();

        if (usedFallback)
        {
            LogFailureOnce("WeakPointEntryAnimationEventFallback");
        }

        if (_bossController == null)
        {
            ReportPatternFailed("BossControllerMissingOnWeakPointEntryComplete");
            return;
        }

        if (!TryGetSettings(out WeakPointPatternSettings settings))
        {
            FailEntry("MissingWeakPointSettingsOnEntryComplete");
            return;
        }

        if (!SpawnWeakPoints(settings))
        {
            FailEntry("WeakPointSpawnFailed");
            return;
        }

        _isEntryResolved = true;

        _bossController.NotifyPatternFourEntryCompleted();

        _bossController.PlayPresentationCue(
            E_BossPresentationCue.PatternAttack,
            E_BossPatternType.WeakPoint,
            transform.position
        );

        MarkPatternEffectApplied();

        StartWeakPointTimeLimitTimer(settings.WeakPointTimeLimit);

        ReportPatternCompleted("WeakPointEntryCompleted");
    }

    /// <summary>
    /// Pattern 4 약점 제한 시간 타이머를 시작한다.
    /// </summary>
    private void StartWeakPointTimeLimitTimer(float timeLimitSeconds)
    {
        StopWeakPointTimeLimitTimer();

        float safeTimeLimit = timeLimitSeconds;

        if (safeTimeLimit < 0f)
        {
            LogFailureOnce("WeakPointTimeLimitRuntimeClamp");
            safeTimeLimit = 0f;
        }

        _weakPointTimeLimitCoroutine = StartCoroutine(RunWeakPointTimeLimitTimer(safeTimeLimit));
    }

    /// <summary>
    /// 제한 시간을 대기한 후 약점이 남아 있으면 타임아웃 처리한다.
    /// </summary>
    private IEnumerator RunWeakPointTimeLimitTimer(float timeLimitSeconds)
    {
        if (timeLimitSeconds > 0f)
        {
            yield return new WaitForSeconds(timeLimitSeconds);
        }

        _weakPointTimeLimitCoroutine = null;
        ResolveWeakPointTimeOut();
    }

    /// <summary>
    /// Pattern 4 타임아웃 처리 및 플레이어에게 데미지 적용
    /// </summary>
    private void ResolveWeakPointTimeOut()
    {
        if (_isWeakPointFlowResolved)
        {
            return;
        }

        ResolveReferences();

        if (_bossController == null || !_bossController.IsBossLogicAuthority())
        {
            LogFailureOnce("WeakPointTimeoutWithoutAuthority");
            return;
        }

        if (!TryGetSettings(out WeakPointPatternSettings settings))
        {
            LogFailureOnce("WeakPointTimeoutSettingsMissing");
            return;
        }

        _isWeakPointFlowResolved = true;

        StopWeakPointTimeLimitTimer();

        ApplyTimeLimitDamageToLivingPlayers(settings);

        RemoveRemainingWeakPoints();

        ClearWeakPointRuntimeBuffers();

        _bossController.NotifyPatternFourTimedOut();
    }

    /// <summary>
    /// 살아있는 모든 플레이어에게 타임아웃 데미지를 적용한다.
    /// </summary>
    private void ApplyTimeLimitDamageToLivingPlayers(WeakPointPatternSettings settings)
    {
        if (settings.WeakPointTimeLimitDamage <= 0f)
        {
            LogFailureOnce("WeakPointTimeLimitDamageZero");
            return;
        }

        BossPlayerTargetProvider targetProvider = _bossController.PlayerTargetProvider; // 플레이어 탐색 제공자

        if (targetProvider == null)
        {
            LogFailureOnce("WeakPointTimeoutTargetProviderMissing");
            return;
        }

        EnsureTimeLimitDamageTargetBuffer(targetProvider);

        int targetCount = targetProvider.CollectAlivePlayersForExecution(_timeLimitDamageTargetBuffer);

        for (int index = 0; index < targetCount; index++)
        {
            HealthComponent targetHealth = _timeLimitDamageTargetBuffer[index];
            _timeLimitDamageTargetBuffer[index] = null;

            if (!IsDamageTargetStillValid(targetHealth))
            {
                continue;
            }

            DamageContext damageContext = new DamageContext(
                settings.WeakPointTimeLimitDamage,
                gameObject,
                "BossPattern4TimeLimit",
                false,
                true,
                E_DamageType.True
            );

            targetHealth.ApplyDamage(damageContext);
        }
    }

    /// <summary>
    /// 플레이어 타겟 버퍼 크기를 Provider 기준으로 맞춘다.
    /// </summary>
    private void EnsureTimeLimitDamageTargetBuffer(BossPlayerTargetProvider targetProvider)
    {
        int requiredLength = Mathf.Max(1, targetProvider.PlayerHealthBufferSize);

        if (_timeLimitDamageTargetBuffer != null &&
            _timeLimitDamageTargetBuffer.Length == requiredLength)
        {
            return;
        }

        _timeLimitDamageTargetBuffer = new HealthComponent[requiredLength];
    }

    /// <summary>
    /// 타임아웃 데미지 적용 직전 대상이 유효한지 검사한다.
    /// </summary>
    private bool IsDamageTargetStillValid(HealthComponent targetHealth)
    {
        if (targetHealth == null ||
            !targetHealth.isActiveAndEnabled ||
            targetHealth.IsDead)
        {
            return false;
        }

        GameObject targetObject = targetHealth.gameObject;

        return targetObject.activeInHierarchy &&
               targetHealth.GetCurrentHealth() > 0f;
    }

    /// <summary>
    /// 모든 약점이 파괴된 경우 처리하고 Groggy 상태로 전환한다.
    /// </summary>
    private void ResolveAllWeakPointsDestroyed()
    {
        if (_isWeakPointFlowResolved)
        {
            return;
        }

        _isWeakPointFlowResolved = true;

        StopWeakPointTimeLimitTimer();

        ClearWeakPointRuntimeBuffers();

        _bossController.NotifyPatternFourAllWeakPointsDestroyed();

        EnterGroggy();
    }

    /// <summary>
    /// 선택된 위치에 약점 오브젝트를 생성한다.
    /// </summary>
    private bool SpawnWeakPoints(WeakPointPatternSettings settings)
    {
        if (settings.WeakPointPrefab == null)
        {
            LogFailureOnce("WeakPointPrefabMissing");
            return false;
        }

        _spawnedWeakPointCount = 0;
        _destroyedWeakPointCount = 0;

        for (int index = 0; index < _selectedWeakPointPositionCount; index++)
        {
            if (!TrySpawnWeakPoint(
                    settings.WeakPointPrefab,
                    _weakPointPositionBuffer[index],
                    index,
                    out BossWeakPointObject weakPointObject))
            {
                continue;
            }

            _weakPointObjectBuffer[index] = weakPointObject;
            _weakPointDestroyedBuffer[index] = false;
            _spawnedWeakPointCount++;
        }

        if (_spawnedWeakPointCount <= 0)
        {
            LogFailureOnce("WeakPointSpawnedCountZero");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 하나의 약점 오브젝트를 생성하고 Health/Hit 시스템과 연결한다.
    /// </summary>
    private bool TrySpawnWeakPoint(
        GameObject weakPointPrefab,
        Vector3 position,
        int weakPointIndex,
        out BossWeakPointObject weakPointObject)
    {
        weakPointObject = null;

        if (weakPointPrefab == null)
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton;

        bool shouldUseNetwork = networkManager != null && networkManager.IsListening;

        if (shouldUseNetwork &&
            (_bossController == null || !_bossController.IsBossLogicAuthority()))
        {
            LogFailureOnce("WeakPointNetworkSpawnWithoutAuthority");
            return false;
        }

        LogWeakPointInstantiateFallbackOnce();

        GameObject spawnedObject = Instantiate(
            weakPointPrefab,
            position,
            Quaternion.identity
        );

        if (spawnedObject == null)
        {
            LogFailureOnce("WeakPointInstantiateFailed");
            return false;
        }

        weakPointObject = EnsureWeakPointBridge(spawnedObject);

        weakPointObject.Initialize(this, weakPointIndex);

        SpawnWeakPointNetworkObjectIfNeeded(
            weakPointPrefab,
            spawnedObject,
            shouldUseNetwork
        );

        _bossController.PlayPresentationCue(
            E_BossPresentationCue.WeakPointCreated,
            E_BossPatternType.WeakPoint,
            position
        );

        return true;
    }

    /// <summary>
    /// 약점 오브젝트에 Bridge 컴포넌트가 없으면 추가한다.
    /// </summary>
    private BossWeakPointObject EnsureWeakPointBridge(GameObject spawnedObject)
    {
        BossWeakPointObject weakPointObject =
            spawnedObject.GetComponent<BossWeakPointObject>();

        if (weakPointObject != null)
        {
            return weakPointObject;
        }

        Debug.LogWarning(
            $"[BossWeakPointPattern] BossWeakPointObject가 없어 런타임에 추가됨. object={spawnedObject.name}",
            spawnedObject
        );

        return spawnedObject.AddComponent<BossWeakPointObject>();
    }

    /// <summary>
    /// NetworkObject가 필요한 경우 Spawn을 수행한다.
    /// </summary>
    private void SpawnWeakPointNetworkObjectIfNeeded(
        GameObject weakPointPrefab,
        GameObject spawnedObject,
        bool shouldUseNetwork)
    {
        if (!shouldUseNetwork ||
            weakPointPrefab.GetComponent<NetworkObject>() == null)
        {
            return;
        }

        NetworkObject spawnedNetworkObject =
            spawnedObject.GetComponent<NetworkObject>();

        if (spawnedNetworkObject == null)
        {
            LogFailureOnce("SpawnedWeakPointNetworkObjectMissing");
            return;
        }

        if (spawnedNetworkObject.IsSpawned)
        {
            return;
        }

        LogWeakPointNetworkPoolFallbackOnce();

        spawnedNetworkObject.Spawn(true);
    }

    /// <summary>
    /// 약점 파괴 VFX를 재생한다.
    /// </summary>
    private void PlayWeakPointDestroyVfx(Vector3 position)
    {
        if (!TryGetSettings(out WeakPointPatternSettings settings))
        {
            LogFailureOnce("WeakPointDestroyVfxSettingsMissing");
            return;
        }

        if (settings.WeakPointDestroyEffectId != E_EffectId.None)
        {
            if (EffectService.Instance == null)
            {
                LogFailureOnce("WeakPointDestroyEffectServiceMissing");
            }
            else
            {
                EffectService.Instance.Play(
                    settings.WeakPointDestroyEffectId,
                    position
                );
                return;
            }
        }

        if (settings.WeakPointDestroyVfxPrefab == null)
        {
            LogFailureOnce("WeakPointDestroyVfxMissing");
            return;
        }

        LogFailureOnce("WeakPointDestroyVfxPrefabFallbackUsed");

        Instantiate(
            settings.WeakPointDestroyVfxPrefab,
            position,
            Quaternion.identity
        );
    }

    /// <summary>
    /// 파괴된 약점 오브젝트를 정리한다.
    /// </summary>
    private void CleanupWeakPointObject(
        BossWeakPointObject weakPointObject,
        int weakPointIndex)
    {
        if (weakPointObject == null)
        {
            return;
        }

        weakPointObject.Release();

        _weakPointObjectBuffer[weakPointIndex] = null;

        NetworkObject networkObject =
            weakPointObject.GetComponent<NetworkObject>();

        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn(true);
            return;
        }

        Destroy(weakPointObject.gameObject);
    }

    /// <summary>
    /// 남아있는 모든 약점을 제거한다.
    /// </summary>
    private void RemoveRemainingWeakPoints()
    {
        if (_weakPointObjectBuffer == null)
        {
            return;
        }

        for (int index = 0; index < _weakPointObjectBuffer.Length; index++)
        {
            BossWeakPointObject weakPointObject =
                _weakPointObjectBuffer[index];

            if (weakPointObject == null)
            {
                continue;
            }

            CleanupWeakPointObject(weakPointObject, index);
        }
    }

    /// <summary>
    /// 모든 약점이 파괴되었는지 확인한다.
    /// </summary>
    private bool AreAllWeakPointsDestroyed()
    {
        if (_spawnedWeakPointCount <= 0 ||
            _destroyedWeakPointCount < _spawnedWeakPointCount)
        {
            return false;
        }

        for (int index = 0; index < _selectedWeakPointPositionCount; index++)
        {
            if (_weakPointObjectBuffer[index] != null &&
                !_weakPointDestroyedBuffer[index])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Instantiate fallback 경고를 1회만 출력한다.
    /// </summary>
    private void LogWeakPointInstantiateFallbackOnce()
    {
        if (_hasLoggedWeakPointInstantiateFallback)
        {
            return;
        }

        Debug.LogWarning(
            $"[BossWeakPointPattern] WeakPoint ObjectPool 없음. Instantiate fallback 사용. object={name}",
            this
        );

        _hasLoggedWeakPointInstantiateFallback = true;
    }

    /// <summary>
    /// NetworkObject Pool fallback 경고를 1회만 출력한다.
    /// </summary>
    private void LogWeakPointNetworkPoolFallbackOnce()
    {
        if (_hasLoggedWeakPointNetworkPoolFallback)
        {
            return;
        }

        Debug.LogWarning(
            $"[BossWeakPointPattern] NetworkObject Pool 없음. Instantiate + Spawn 사용. object={name}",
            this
        );

        _hasLoggedWeakPointNetworkPoolFallback = true;
    }

    /// <summary>
    /// Pattern 4 진입 실패 처리
    /// </summary>
    private void FailEntry(string reason)
    {
        if (_isEntryResolved)
        {
            return;
        }

        _isEntryResolved = true;

        StopEntryFallbackTimer();
        StopWeakPointTimeLimitTimer();

        RemoveRemainingWeakPoints();
        ClearWeakPointRuntimeBuffers();

        if (_bossController != null)
        {
            _bossController.NotifyPatternFourEntryFailed();
        }

        ReportPatternFailed(reason);
    }

    /// <summary>
    /// 진입 fallback 타이머를 중지한다.
    /// </summary>
    private void StopEntryFallbackTimer()
    {
        if (_entryFallbackCoroutine == null)
        {
            return;
        }

        StopCoroutine(_entryFallbackCoroutine);
        _entryFallbackCoroutine = null;
    }

    /// <summary>
    /// 약점 제한 시간 타이머를 중지한다.
    /// </summary>
    private void StopWeakPointTimeLimitTimer()
    {
        if (_weakPointTimeLimitCoroutine == null)
        {
            return;
        }

        StopCoroutine(_weakPointTimeLimitCoroutine);
        _weakPointTimeLimitCoroutine = null;
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
