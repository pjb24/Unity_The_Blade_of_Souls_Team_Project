using UnityEngine;

/// <summary>
/// 씬 참조나 런타임 상태 없이 디자이너가 작성하는 보스 패턴 설정을 저장한다.
/// </summary>
[CreateAssetMenu(fileName = "BossPatternData", menuName = "Boss/Pattern Data")]
public class BossPatternData : ScriptableObject
{
    [Header("공통 패턴 설정")]
    [Tooltip("모든 보스 패턴 항목이 공유하는 공통 설정입니다.")]
    [SerializeField] private PatternCommonSettings[] _commonSettings = new PatternCommonSettings[0]; // 디자이너가 설정하는 패턴 단위 공통 설정

    [Tooltip("체력 비율에 따라 사용할 수 있는 패턴 ID를 정의하는 페이즈 설정입니다.")]
    [SerializeField] private HealthPhaseSettings[] _healthPhaseSettings = new HealthPhaseSettings[0]; // 체력 비율 기반 페이즈 설정 (패턴 선택에 사용)

    [Tooltip("패턴 선택 로직에서 적용할 패턴 사용 제한 설정입니다.")]
    [SerializeField] private PatternUsageLimit[] _usageLimits = new PatternUsageLimit[0]; // 패턴 사용 제한 설정 (선택 로직에서 사용)

    [Header("패턴 1 - 부채꼴 투사체")]
    [Tooltip("부채꼴 투사체 패턴의 순수 설정 목록입니다. 각 항목은 PatternId로 찾습니다.")]
    [SerializeField] private FanProjectilePatternSettings[] _fanProjectilePatterns = new FanProjectilePatternSettings[0]; // PatternId로 조회되는 패턴1 설정 목록

    [Header("패턴 2 - 지면 가시")]
    [Tooltip("지면 가시 패턴의 순수 설정 목록입니다. 각 항목은 PatternId로 찾습니다.")]
    [SerializeField] private GroundSpikePatternSettings[] _groundSpikePatterns = new GroundSpikePatternSettings[0]; // PatternId로 조회되는 패턴2 설정 목록

    [Header("패턴 3 - 몬스터 소환")]
    [Tooltip("몬스터 소환 패턴의 순수 설정 목록입니다. 각 항목은 PatternId로 찾습니다.")]
    [SerializeField] private SummonMonsterPatternSettings[] _summonMonsterPatterns = new SummonMonsterPatternSettings[0]; // PatternId로 조회되는 패턴3 설정 목록

    [Header("패턴 4 - 약점")]
    [Tooltip("약점 패턴의 순수 설정 목록입니다. 각 항목은 PatternId로 찾습니다.")]
    [SerializeField] private WeakPointPatternSettings[] _weakPointPatterns = new WeakPointPatternSettings[0]; // PatternId로 조회되는 패턴4 설정 목록

    /// <summary>
    /// 인스펙터에 입력된 잘못된 값을 보정하고 데이터 작성 문제를 로그로 알린다.
    /// </summary>
    private void OnValidate()
    {
        ValidateCommonSettings();
        ValidateUsageLimits();
        ValidateHealthPhaseSettings();
        ValidatePatternSpecificSettings();
    }

    /// <summary>
    /// 공통 패턴 설정 배열을 반환한다.
    /// </summary>
    public PatternCommonSettings[] CommonSettings => _commonSettings;

    /// <summary>
    /// 체력 페이즈 설정 배열을 반환한다.
    /// </summary>
    public HealthPhaseSettings[] HealthPhaseSettings => _healthPhaseSettings;

    /// <summary>
    /// 패턴 사용 제한 설정 배열을 반환한다.
    /// </summary>
    public PatternUsageLimit[] UsageLimits => _usageLimits;

    /// <summary>
    /// 기존 인스펙터 확인 또는 디버그 확인용으로 첫 번째 부채꼴 투사체 패턴 설정을 반환한다.
    /// </summary>
    public FanProjectilePatternSettings FanProjectilePattern => _fanProjectilePatterns != null && _fanProjectilePatterns.Length > 0 ? _fanProjectilePatterns[0] : default;

    /// <summary>
    /// 부채꼴 투사체 패턴 설정 배열을 반환한다.
    /// </summary>
    public FanProjectilePatternSettings[] FanProjectilePatterns => _fanProjectilePatterns;

    /// <summary>
    /// 기존 인스펙터 확인 또는 디버그 확인용으로 첫 번째 지면 가시 패턴 설정을 반환한다.
    /// </summary>
    public GroundSpikePatternSettings GroundSpikePattern => _groundSpikePatterns != null && _groundSpikePatterns.Length > 0 ? _groundSpikePatterns[0] : default;

    /// <summary>
    /// 지면 가시 패턴 설정 배열을 반환한다.
    /// </summary>
    public GroundSpikePatternSettings[] GroundSpikePatterns => _groundSpikePatterns;

    /// <summary>
    /// 기존 인스펙터 확인 또는 디버그 확인용으로 첫 번째 몬스터 소환 패턴 설정을 반환한다.
    /// </summary>
    public SummonMonsterPatternSettings SummonMonsterPattern => _summonMonsterPatterns != null && _summonMonsterPatterns.Length > 0 ? _summonMonsterPatterns[0] : default;

    /// <summary>
    /// 몬스터 소환 패턴 설정 배열을 반환한다.
    /// </summary>
    public SummonMonsterPatternSettings[] SummonMonsterPatterns => _summonMonsterPatterns;

    /// <summary>
    /// 기존 인스펙터 확인 또는 디버그 확인용으로 첫 번째 약점 패턴 설정을 반환한다.
    /// </summary>
    public WeakPointPatternSettings WeakPointPattern => _weakPointPatterns != null && _weakPointPatterns.Length > 0 ? _weakPointPatterns[0] : default;

    /// <summary>
    /// 약점 패턴 설정 배열을 반환한다.
    /// </summary>
    public WeakPointPatternSettings[] WeakPointPatterns => _weakPointPatterns;

    /// <summary>
    /// 요청한 PatternId와 일치하는 첫 번째 부채꼴 투사체 패턴 설정을 찾는다.
    /// </summary>
    public bool TryGetFanProjectilePattern(string patternId, out FanProjectilePatternSettings settings)
    {
        settings = default;
        return TryGetPatternSettingsById(_fanProjectilePatterns, patternId, out settings);
    }

    /// <summary>
    /// 요청한 PatternId와 일치하는 첫 번째 지면 가시 패턴 설정을 찾는다.
    /// </summary>
    public bool TryGetGroundSpikePattern(string patternId, out GroundSpikePatternSettings settings)
    {
        settings = default;
        return TryGetPatternSettingsById(_groundSpikePatterns, patternId, out settings);
    }

    /// <summary>
    /// 요청한 PatternId와 일치하는 첫 번째 몬스터 소환 패턴 설정을 찾는다.
    /// </summary>
    public bool TryGetSummonMonsterPattern(string patternId, out SummonMonsterPatternSettings settings)
    {
        settings = default;
        return TryGetPatternSettingsById(_summonMonsterPatterns, patternId, out settings);
    }

    /// <summary>
    /// 요청한 PatternId와 일치하는 첫 번째 약점 패턴 설정을 찾는다.
    /// </summary>
    public bool TryGetWeakPointPattern(string patternId, out WeakPointPatternSettings settings)
    {
        settings = default;
        return TryGetPatternSettingsById(_weakPointPatterns, patternId, out settings);
    }

    /// <summary>
    /// 공통 패턴 설정을 보정하고 비어 있거나 중복된 PatternId를 로그로 알린다.
    /// </summary>
    private void ValidateCommonSettings()
    {
        if (_commonSettings == null)
        {
            return;
        }

        for (int index = 0; index < _commonSettings.Length; index++)
        {
            PatternCommonSettings settings = _commonSettings[index]; // 구조체 복사본을 사용해 보정 후 다시 배열에 할당
            settings.ValidateOnValidate(this, index);
            _commonSettings[index] = settings;
        }

        for (int outerIndex = 0; outerIndex < _commonSettings.Length; outerIndex++)
        {
            string outerPatternId = _commonSettings[outerIndex].PatternId; // 중복 검사 대상 PatternId
            if (string.IsNullOrWhiteSpace(outerPatternId))
            {
                Debug.LogWarning($"[BossPatternData] PatternCommonSettings PatternId가 비어있다. index={outerIndex}", this);
                continue;
            }

            for (int innerIndex = outerIndex + 1; innerIndex < _commonSettings.Length; innerIndex++)
            {
                if (_commonSettings[innerIndex].PatternId != outerPatternId)
                {
                    continue;
                }

                Debug.LogWarning($"[BossPatternData] PatternCommonSettings에 중복된 PatternId 존재. patternId={outerPatternId}, firstIndex={outerIndex}, duplicateIndex={innerIndex}. 첫 번째 항목만 사용된다.", this);
                break;
            }
        }
    }

    /// <summary>
    /// 사용 제한 설정을 보정하고 같은 HealthPhase 안에서 중복된 PatternId를 로그로 알린다.
    /// </summary>
    private void ValidateUsageLimits()
    {
        if (_usageLimits == null)
        {
            return;
        }

        for (int index = 0; index < _usageLimits.Length; index++)
        {
            PatternUsageLimit usageLimit = _usageLimits[index]; // 구조체 복사본 사용
            usageLimit.ValidateOnValidate(this, index);
            _usageLimits[index] = usageLimit;
        }

        for (int outerIndex = 0; outerIndex < _usageLimits.Length; outerIndex++)
        {
            PatternUsageLimit outer = _usageLimits[outerIndex]; // 중복 검사 기준 항목
            if (string.IsNullOrWhiteSpace(outer.PatternId))
            {
                continue;
            }

            for (int innerIndex = outerIndex + 1; innerIndex < _usageLimits.Length; innerIndex++)
            {
                PatternUsageLimit inner = _usageLimits[innerIndex]; // 비교 대상 항목
                if (inner.PhaseIndex != outer.PhaseIndex || inner.PatternId != outer.PatternId)
                {
                    continue;
                }

                Debug.LogWarning($"[BossPatternData] 같은 PhaseIndex와 PatternId에 대해 중복된 UsageLimit 존재. phaseIndex={outer.PhaseIndex}, patternId={outer.PatternId}, firstIndex={outerIndex}, duplicateIndex={innerIndex}. 첫 번째 항목만 사용된다.", this);
                break;
            }
        }
    }

    /// <summary>
    /// 체력 페이즈 범위를 보정하고 비어 있거나 겹치는 범위를 로그로 알린다.
    /// </summary>
    private void ValidateHealthPhaseSettings()
    {
        if (_healthPhaseSettings == null || _healthPhaseSettings.Length == 0)
        {
            Debug.LogWarning("[BossPatternData] HealthPhaseSettings가 비어있다. 보스 페이즈 검증 불가.", this);
            return;
        }

        for (int index = 0; index < _healthPhaseSettings.Length; index++)
        {
            HealthPhaseSettings settings = _healthPhaseSettings[index]; // 구조체 복사본 사용
            settings.ValidateOnValidate(this, index);
            _healthPhaseSettings[index] = settings;

            ValidateHealthPhasePatternIds(settings, index);
        }

        for (int outerIndex = 0; outerIndex < _healthPhaseSettings.Length; outerIndex++)
        {
            HealthPhaseSettings outer = _healthPhaseSettings[outerIndex]; // 첫 번째 비교 대상 페이즈
            for (int innerIndex = outerIndex + 1; innerIndex < _healthPhaseSettings.Length; innerIndex++)
            {
                HealthPhaseSettings inner = _healthPhaseSettings[innerIndex]; // 두 번째 비교 대상 페이즈
                if (!DoHealthPhaseRangesOverlap(outer, inner))
                {
                    continue;
                }

                Debug.LogWarning($"[BossPatternData] HealthPhase 범위가 겹친다. firstIndex={outerIndex}, secondIndex={innerIndex}", this);
                break;
            }
        }

        ValidateHealthPhaseCoverage();
    }

    /// <summary>
    /// 패턴별 세부 설정을 보정하고 참조를 검증한다.
    /// </summary>
    private void ValidatePatternSpecificSettings()
    {
        ValidateFanProjectileSettings();
        ValidateGroundSpikeSettings();
        ValidateSummonMonsterSettings();
        ValidateWeakPointSettings();
    }

    /// <summary>
    /// 모든 부채꼴 투사체 패턴 설정을 검증하고 중복된 PatternId를 로그로 알린다.
    /// </summary>
    private void ValidateFanProjectileSettings()
    {
        if (_fanProjectilePatterns == null)
        {
            return;
        }

        for (int index = 0; index < _fanProjectilePatterns.Length; index++)
        {
            FanProjectilePatternSettings settings = _fanProjectilePatterns[index]; // 구조체 복사본을 사용해 보정 후 다시 배열에 할당
            settings.ValidateOnValidate(this);
            _fanProjectilePatterns[index] = settings;
        }

        ValidatePatternSettingsIds(_fanProjectilePatterns, "FanProjectile");
    }

    /// <summary>
    /// 모든 지면 가시 패턴 설정을 검증하고 중복된 PatternId를 로그로 알린다.
    /// </summary>
    private void ValidateGroundSpikeSettings()
    {
        if (_groundSpikePatterns == null)
        {
            return;
        }

        for (int index = 0; index < _groundSpikePatterns.Length; index++)
        {
            GroundSpikePatternSettings settings = _groundSpikePatterns[index]; // 구조체 복사본을 사용해 보정 후 다시 배열에 할당
            settings.ValidateOnValidate(this);
            _groundSpikePatterns[index] = settings;
        }

        ValidatePatternSettingsIds(_groundSpikePatterns, "GroundSpike");
    }

    /// <summary>
    /// 모든 몬스터 소환 패턴 설정을 검증하고 중복된 PatternId를 로그로 알린다.
    /// </summary>
    private void ValidateSummonMonsterSettings()
    {
        if (_summonMonsterPatterns == null)
        {
            return;
        }

        for (int index = 0; index < _summonMonsterPatterns.Length; index++)
        {
            SummonMonsterPatternSettings settings = _summonMonsterPatterns[index]; // 구조체 복사본을 사용해 보정 후 다시 배열에 할당
            settings.ValidateOnValidate(this);
            _summonMonsterPatterns[index] = settings;
        }

        ValidatePatternSettingsIds(_summonMonsterPatterns, "SummonMonster");
    }

    /// <summary>
    /// 모든 약점 패턴 설정을 검증하고 중복된 PatternId를 로그로 알린다.
    /// </summary>
    private void ValidateWeakPointSettings()
    {
        if (_weakPointPatterns == null)
        {
            return;
        }

        for (int index = 0; index < _weakPointPatterns.Length; index++)
        {
            WeakPointPatternSettings settings = _weakPointPatterns[index]; // 구조체 복사본을 사용해 보정 후 다시 배열에 할당
            settings.ValidateOnValidate(this);
            _weakPointPatterns[index] = settings;
        }

        ValidatePatternSettingsIds(_weakPointPatterns, "WeakPoint");
    }

    /// <summary>
    /// 두 체력 페이즈 범위가 서로 겹치는지 반환한다.
    /// </summary>
    private bool DoHealthPhaseRangesOverlap(HealthPhaseSettings first, HealthPhaseSettings second)
    {
        return first.MinHealthRatio < second.MaxHealthRatio && second.MinHealthRatio < first.MaxHealthRatio;
    }

    /// <summary>
    /// PatternId와 일치하는 첫 번째 패턴 설정을 찾는다.
    /// </summary>
    private bool TryGetPatternSettingsById(FanProjectilePatternSettings[] settingsArray, string patternId, out FanProjectilePatternSettings settings)
    {
        settings = default;
        if (settingsArray == null || string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        for (int index = 0; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            settings = settingsArray[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// PatternId와 일치하는 첫 번째 패턴 설정을 찾는다.
    /// </summary>
    private bool TryGetPatternSettingsById(GroundSpikePatternSettings[] settingsArray, string patternId, out GroundSpikePatternSettings settings)
    {
        settings = default;
        if (settingsArray == null || string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        for (int index = 0; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            settings = settingsArray[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// PatternId와 일치하는 첫 번째 패턴 설정을 찾는다.
    /// </summary>
    private bool TryGetPatternSettingsById(SummonMonsterPatternSettings[] settingsArray, string patternId, out SummonMonsterPatternSettings settings)
    {
        settings = default;
        if (settingsArray == null || string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        for (int index = 0; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            settings = settingsArray[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// PatternId와 일치하는 첫 번째 패턴 설정을 찾는다.
    /// </summary>
    private bool TryGetPatternSettingsById(WeakPointPatternSettings[] settingsArray, string patternId, out WeakPointPatternSettings settings)
    {
        settings = default;
        if (settingsArray == null || string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        for (int index = 0; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            settings = settingsArray[index];
            return true;
        }

        return false;
    }

    /// <summary>
    /// 패턴 설정 배열에서 PatternId 유효성과 중복을 검사한다.
    /// </summary>
    private void ValidatePatternSettingsIds(FanProjectilePatternSettings[] settingsArray, string label)
    {
        for (int index = 0; index < settingsArray.Length; index++)
        {
            ValidatePatternSettingsId(settingsArray[index].PatternId, index, label);
            ValidateDuplicatePatternSettingsId(settingsArray, index, label);
        }
    }

    /// <summary>
    /// 패턴 설정 배열에서 PatternId 유효성과 중복을 검사한다.
    /// </summary>
    private void ValidatePatternSettingsIds(GroundSpikePatternSettings[] settingsArray, string label)
    {
        for (int index = 0; index < settingsArray.Length; index++)
        {
            ValidatePatternSettingsId(settingsArray[index].PatternId, index, label);
            ValidateDuplicatePatternSettingsId(settingsArray, index, label);
        }
    }

    /// <summary>
    /// 패턴 설정 배열에서 PatternId 유효성과 중복을 검사한다.
    /// </summary>
    private void ValidatePatternSettingsIds(SummonMonsterPatternSettings[] settingsArray, string label)
    {
        for (int index = 0; index < settingsArray.Length; index++)
        {
            ValidatePatternSettingsId(settingsArray[index].PatternId, index, label);
            ValidateDuplicatePatternSettingsId(settingsArray, index, label);
        }
    }

    /// <summary>
    /// 패턴 설정 배열에서 PatternId 유효성과 중복을 검사한다.
    /// </summary>
    private void ValidatePatternSettingsIds(WeakPointPatternSettings[] settingsArray, string label)
    {
        for (int index = 0; index < settingsArray.Length; index++)
        {
            ValidatePatternSettingsId(settingsArray[index].PatternId, index, label);
            ValidateDuplicatePatternSettingsId(settingsArray, index, label);
        }
    }

    /// <summary>
    /// 비어 있는 PatternId를 로그로 알린다.
    /// </summary>
    private void ValidatePatternSettingsId(string patternId, int index, string label)
    {
        if (!string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        Debug.LogWarning($"[BossPatternData] {label} PatternId 비어있다. index={index}", this);
    }

    /// <summary>
    /// 중복된 PatternId를 로그로 알린다.
    /// </summary>
    private void ValidateDuplicatePatternSettingsId(FanProjectilePatternSettings[] settingsArray, int firstIndex, string label)
    {
        string patternId = settingsArray[firstIndex].PatternId; // 중복 검사 기준 PatternId
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        for (int index = firstIndex + 1; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            Debug.LogWarning($"[BossPatternData] {label} PatternId 중복. patternId={patternId}, firstIndex={firstIndex}, duplicateIndex={index}", this);
            return;
        }
    }

    /// <summary>
    /// 중복된 PatternId를 로그로 알린다.
    /// </summary>
    private void ValidateDuplicatePatternSettingsId(GroundSpikePatternSettings[] settingsArray, int firstIndex, string label)
    {
        string patternId = settingsArray[firstIndex].PatternId; // 중복 검사 기준 PatternId
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        for (int index = firstIndex + 1; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            Debug.LogWarning($"[BossPatternData] {label} PatternId 중복. patternId={patternId}, firstIndex={firstIndex}, duplicateIndex={index}", this);
            return;
        }
    }

    /// <summary>
    /// 중복된 PatternId를 로그로 알린다.
    /// </summary>
    private void ValidateDuplicatePatternSettingsId(SummonMonsterPatternSettings[] settingsArray, int firstIndex, string label)
    {
        string patternId = settingsArray[firstIndex].PatternId; // 중복 검사 기준 PatternId
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        for (int index = firstIndex + 1; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            Debug.LogWarning($"[BossPatternData] {label} PatternId 중복. patternId={patternId}, firstIndex={firstIndex}, duplicateIndex={index}", this);
            return;
        }
    }

    /// <summary>
    /// 중복된 PatternId를 로그로 알린다.
    /// </summary>
    private void ValidateDuplicatePatternSettingsId(WeakPointPatternSettings[] settingsArray, int firstIndex, string label)
    {
        string patternId = settingsArray[firstIndex].PatternId; // 중복 검사 기준 PatternId
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        for (int index = firstIndex + 1; index < settingsArray.Length; index++)
        {
            if (settingsArray[index].PatternId != patternId)
            {
                continue;
            }

            Debug.LogWarning($"[BossPatternData] {label} PatternId 중복. patternId={patternId}, firstIndex={firstIndex}, duplicateIndex={index}", this);
            return;
        }
    }

    /// <summary>
    /// HealthPhase 내부 PatternId 유효성 검사
    /// </summary>
    private void ValidateHealthPhasePatternIds(HealthPhaseSettings settings, int phaseArrayIndex)
    {
        string[] availablePatternIds = settings.AvailablePatternIds; // 해당 페이즈에서 사용할 PatternId 목록
        if (availablePatternIds == null || availablePatternIds.Length == 0)
        {
            Debug.LogWarning($"[BossPatternData] HealthPhase PatternId 없음. index={phaseArrayIndex}", this);
            return;
        }

        for (int index = 0; index < availablePatternIds.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(availablePatternIds[index]))
            {
                continue;
            }

            Debug.LogWarning($"[BossPatternData] HealthPhase에 빈 PatternId 존재. phaseIndex={settings.PhaseIndex}, patternIndex={index}", this);
        }
    }

    /// <summary>
    /// 체력 비율이 어떤 페이즈에도 포함되지 않는 구간을 검사한다.
    /// </summary>
    private void ValidateHealthPhaseCoverage()
    {
        float cursor = 1f; // 아직 어떤 페이즈에도 포함되지 않은 최대 체력 비율

        for (int step = 0; step < _healthPhaseSettings.Length; step++)
        {
            int bestIndex = -1; // 현재 cursor 이하에서 가장 높은 MaxHealthRatio를 가진 페이즈 인덱스
            float bestMax = -1f; // 선택된 페이즈의 MaxHealthRatio

            for (int index = 0; index < _healthPhaseSettings.Length; index++)
            {
                float candidateMax = _healthPhaseSettings[index].MaxHealthRatio; // 비교용 후보 값
                if (candidateMax > cursor || candidateMax <= bestMax)
                {
                    continue;
                }

                bestMax = candidateMax;
                bestIndex = index;
            }

            if (bestIndex < 0)
            {
                if (cursor > 0f)
                {
                    Debug.LogWarning($"[BossPatternData] 체력 비율 커버 안되는 구간 존재. gap=(0, {cursor}]", this);
                }

                return;
            }

            HealthPhaseSettings phase = _healthPhaseSettings[bestIndex]; // cursor를 덮어야 하는 페이즈

            if (phase.MaxHealthRatio < cursor)
            {
                Debug.LogWarning($"[BossPatternData] 체력 비율 커버 안되는 구간 존재. gap=({phase.MaxHealthRatio}, {cursor}]", this);
            }

            cursor = phase.MinHealthRatio;

            if (cursor <= 0f)
            {
                return;
            }
        }

        if (cursor > 0f)
        {
            Debug.LogWarning($"[BossPatternData] 체력 비율 커버 안되는 구간 존재. gap=(0, {cursor}]", this);
        }
    }
}