using UnityEngine;

/// <summary>
/// 권한을 가진 런타임 조건을 기반으로 재사용 가능한 후보 저장소에서 보스 패턴을 선택한다.
/// </summary>
public sealed class BossPatternSelector
{
    private int[] _candidateIndices = new int[0]; // 최우선 후보들의 PatternCommonSettings 인덱스를 저장하는 재사용 버퍼
    private int _candidateCount; // 현재 재사용 후보 버퍼에 저장된 유효한 항목 개수
    private bool _hasLoggedMissingControllerWarning; // BossController 누락 경고를 반복 출력하지 않도록 방지
    private bool _hasLoggedMissingPatternDataWarning; // BossPatternData 누락 경고를 반복 출력하지 않도록 방지
    private bool _hasLoggedMissingBossTransformWarning; // Boss Transform 누락 경고를 반복 출력하지 않도록 방지
    private bool _hasLoggedClientRandomWarning; // 클라이언트에서 랜덤 선택 차단 경고를 반복 출력하지 않도록 방지

    /// <summary>
    /// BossController의 Transform을 보스 위치로 사용하여 패턴을 선택한다.
    /// </summary>
    public bool TrySelectPattern(BossController bossController, Transform target, out PatternCommonSettings selectedSettings)
    {
        Transform bossTransform = bossController != null ? bossController.transform : null; // 타겟 거리 조건 계산에 사용하는 보스 Transform
        return TrySelectPattern(bossController, bossTransform, target, out selectedSettings);
    }

    /// <summary>
    /// 상태, 쿨타임, 체력, 사용 횟수, 타겟 조건을 모두 만족하는 최고 우선순위 패턴을 선택한다.
    /// </summary>
    public bool TrySelectPattern(BossController bossController, Transform bossTransform, Transform target, out PatternCommonSettings selectedSettings)
    {
        selectedSettings = default;
        ClearCandidateBuffer();

        if (!TryGetCommonSettings(bossController, out PatternCommonSettings[] commonSettings))
        {
            return false;
        }

        if (!bossController.CanSelectPattern())
        {
            return false;
        }

        if (bossTransform == null)
        {
            if (!_hasLoggedMissingBossTransformWarning)
            {
                Debug.LogWarning("[BossPatternSelector] Boss Transform이 없어 타겟 거리 조건을 평가할 수 없음.");
                _hasLoggedMissingBossTransformWarning = true;
            }

            return false;
        }

        InitializeCandidateBuffer(commonSettings.Length);

        int highestPriority = int.MinValue; // 이번 선택 과정에서 발견된 가장 높은 우선순위
        for (int index = 0; index < commonSettings.Length; index++)
        {
            PatternCommonSettings settings = commonSettings[index]; // 현재 선택 후보로 평가 중인 패턴 설정 항목
            if (!IsCandidateAllowed(bossController, bossTransform, target, settings, index))
            {
                continue;
            }

            if (settings.Priority > highestPriority)
            {
                highestPriority = settings.Priority;
                ClearCandidateBuffer();
                AddCandidateIndex(index);
                continue;
            }

            if (settings.Priority == highestPriority)
            {
                AddCandidateIndex(index);
            }
        }

        if (_candidateCount <= 0)
        {
            Debug.LogWarning($"[BossPatternSelector] 선택 가능한 보스 패턴 후보가 없음. object={bossController.name}, state={bossController.CurrentState}, phaseIndex={bossController.GetCurrentHealthPhaseIndex()}");
            bossController.ReportNoSelectablePatternFallback();
            return false;
        }

        int selectedCandidateBufferIndex = ResolveSelectedCandidateBufferIndex(bossController);
        if (selectedCandidateBufferIndex < 0)
        {
            return false;
        }

        int selectedCommonSettingsIndex = _candidateIndices[selectedCandidateBufferIndex]; // 후보 버퍼에서 선택된 CommonSettings 인덱스
        selectedSettings = commonSettings[selectedCommonSettingsIndex];
        return true;
    }

    /// <summary>
    /// 새로운 리스트를 할당하지 않고 재사용 후보 버퍼 개수만 초기화한다.
    /// </summary>
    public void ClearCandidateBuffer()
    {
        _candidateCount = 0;
    }

    /// <summary>
    /// 재사용 후보 버퍼가 필요한 용량을 담을 수 있도록 보장한다.
    /// </summary>
    public void InitializeCandidateBuffer(int requiredCapacity)
    {
        int safeCapacity = Mathf.Max(0, requiredCapacity); // 배열 할당을 위한 안전한 버퍼 용량
        if (_candidateIndices != null && _candidateIndices.Length >= safeCapacity)
        {
            return;
        }

        _candidateIndices = new int[safeCapacity];
    }

    /// <summary>
    /// 해당 설정 항목이 선택 가능한 모든 조건을 만족하는지 여부를 반환한다.
    /// </summary>
    private bool IsCandidateAllowed(BossController bossController, Transform bossTransform, Transform target, PatternCommonSettings settings, int commonSettingsIndex)
    {
        if (!settings.Enabled)
        {
            return false;
        }

        if (settings.PatternType == E_BossPatternType.None)
        {
            return false;
        }

        if (settings.PatternType == E_BossPatternType.WeakPoint && (bossController.CurrentPatternType == E_BossPatternType.WeakPoint || bossController.IsWeakPointPatternActive))
        {
            return false;
        }

        if (bossController.IsWeakPointPatternActive && !settings.AllowDuringWeakPointActive)
        {
            return false;
        }

        if (!bossController.CanSelectPatternSettings(settings, commonSettingsIndex))
        {
            return false;
        }

        return SatisfiesTargetConditions(bossTransform, target, settings);
    }

    /// <summary>
    /// 타겟 요구 조건 및 거리 제곱 조건을 만족하는지 여부를 반환한다.
    /// </summary>
    private bool SatisfiesTargetConditions(Transform bossTransform, Transform target, PatternCommonSettings settings)
    {
        if (target == null)
        {
            return !settings.RequireTarget;
        }

        Vector3 offset = target.position - bossTransform.position; // 거리 제곱 계산에 사용하는 오프셋
        float sqrDistance = offset.sqrMagnitude;
        if (sqrDistance < settings.MinimumTargetSqrDistance)
        {
            return false;
        }

        if (settings.MaximumTargetSqrDistance > 0f && sqrDistance > settings.MaximumTargetSqrDistance)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// CommonSettings 인덱스를 재사용 후보 버퍼에 추가한다.
    /// </summary>
    private void AddCandidateIndex(int commonSettingsIndex)
    {
        if (_candidateIndices == null || _candidateCount >= _candidateIndices.Length)
        {
            Debug.LogWarning($"[BossPatternSelector] 후보 버퍼 크기가 부족하여 선택 중 확장됨. previousCapacity={(_candidateIndices == null ? 0 : _candidateIndices.Length)}, required={_candidateCount + 1}");
            InitializeCandidateBuffer(_candidateCount + 1);
        }

        _candidateIndices[_candidateCount] = commonSettingsIndex;
        _candidateCount++;
    }

    /// <summary>
    /// 최종 후보 인덱스를 결정하며, 우선순위가 동일한 경우 서버 권한 기반 랜덤 선택을 수행한다.
    /// </summary>
    private int ResolveSelectedCandidateBufferIndex(BossController bossController)
    {
        if (_candidateCount == 1)
        {
            return 0;
        }

        if (!bossController.IsBossLogicAuthority())
        {
            if (!_hasLoggedClientRandomWarning)
            {
                Debug.LogWarning("[BossPatternSelector] 보스 권한이 없어 랜덤 선택이 차단됨.");
                _hasLoggedClientRandomWarning = true;
            }

            return -1;
        }

        return Random.Range(0, _candidateCount);
    }

    /// <summary>
    /// BossController에서 PatternCommonSettings를 가져오고 누락된 참조를 보고한다.
    /// </summary>
    private bool TryGetCommonSettings(BossController bossController, out PatternCommonSettings[] commonSettings)
    {
        commonSettings = null;
        if (bossController == null)
        {
            if (!_hasLoggedMissingControllerWarning)
            {
                Debug.LogWarning("[BossPatternSelector] BossController가 없어 패턴 선택을 수행할 수 없음.");
                _hasLoggedMissingControllerWarning = true;
            }

            return false;
        }

        BossPatternData patternData = bossController.PatternData; // CommonSettings를 보유한 보스 패턴 데이터 에셋
        if (patternData == null || patternData.CommonSettings == null || patternData.CommonSettings.Length == 0)
        {
            if (!_hasLoggedMissingPatternDataWarning)
            {
                Debug.LogWarning($"[BossPatternSelector] PatternData 또는 CommonSettings가 없음. object={bossController.name}", bossController);
                _hasLoggedMissingPatternDataWarning = true;
            }

            return false;
        }

        commonSettings = patternData.CommonSettings;
        return true;
    }
}
