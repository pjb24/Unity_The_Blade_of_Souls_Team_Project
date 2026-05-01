using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 보스 전투가 활성화된 동안 서버 권한에서 기존 BossController 패턴 선택/실행 API를 호출하는 런타임 AI 드라이버입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BossController))]
public sealed class BossRuntimeAiPatternDriver : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("런타임 AI가 패턴 선택과 실행 요청을 위임할 BossController입니다. 비어 있으면 같은 GameObject에서 자동으로 찾습니다.")]
    [SerializeField] private BossController _bossController; // 런타임 AI가 기존 보스 시스템 API를 호출할 대상 컨트롤러

    [Header("Runtime AI")]
    [Tooltip("컴포넌트가 활성화되면 보스 전투 활성 상태를 감시하고 런타임 AI 패턴 선택을 수행할지 여부입니다.")]
    [SerializeField] private bool _runAiOnEnable = true; // OnEnable 시 런타임 AI 루프를 자동으로 시작할지 결정

    [Tooltip("보스가 Idle이고 패턴 선택 가능 상태일 때 다음 패턴 선택 가능 여부를 다시 확인하는 주기(초)입니다.")]
    [Min(0.02f)]
    [SerializeField] private float _decisionIntervalSeconds = 0.25f; // 정상 대기 상태에서 패턴 선택 조건을 검사하는 시간 간격

    [Tooltip("타겟, 패턴 후보, 패턴 컴포넌트가 없어 실행하지 못했을 때 다시 시도하기 전 대기 시간(초)입니다.")]
    [Min(0.02f)]
    [SerializeField] private float _retryIntervalSeconds = 0.75f; // 실패 조건 발생 후 다음 AI 판단까지 기다리는 시간

    [Tooltip("Player 타겟 탐색에 사용할 반경입니다. 0 이하이면 BossPlayerTargetProvider의 기본 탐색 반경을 사용합니다.")]
    [Min(0f)]
    [SerializeField] private float _targetSearchRange; // 패턴 거리 조건 평가에 사용할 Player 타겟 탐색 반경

    private Coroutine _aiRoutine; // 보스 AI 패턴 선택 루프 코루틴입니다.
    private BossPatternBase[] _patternsByType; // E_BossPatternType 값을 인덱스로 사용하는 패턴 컴포넌트 캐시입니다.
    private bool _hasLoggedAuthorityWarning; // Client 직접 실행 차단 경고 중복 방지 플래그입니다.
    private bool _hasLoggedMissingBossWarning; // BossController 누락 경고 중복 방지 플래그입니다.
    private bool _hasLoggedMissingTargetProviderWarning; // PlayerTargetProvider 누락 경고 중복 방지 플래그입니다.
    private bool _hasLoggedMissingPatternWarning; // 선택된 패턴 타입의 실행 컴포넌트 누락 경고 중복 방지 플래그입니다.
    private bool _hasLoggedDuplicatePatternWarning; // 같은 패턴 타입 컴포넌트 중복 경고 중복 방지 플래그입니다.

    /// <summary>
    /// 런타임 시작 전에 같은 GameObject 기준으로 BossController 참조를 보정한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        RebuildPatternCache();
    }

    /// <summary>
    /// 컴포넌트 활성화 시 설정에 따라 런타임 AI 루프를 시작한다.
    /// </summary>
    private void OnEnable()
    {
        if (!_runAiOnEnable)
        {
            return;
        }

        StartRuntimeAi();
    }

    /// <summary>
    /// 컴포넌트 비활성화 시 런타임 AI 루프를 중지한다.
    /// </summary>
    private void OnDisable()
    {
        StopRuntimeAi();
    }

    /// <summary>
    /// Inspector 값 변경 시 참조와 음수 시간을 보정한다.
    /// </summary>
    private void OnValidate()
    {
        if (_decisionIntervalSeconds < 0.02f)
        {
            Debug.LogWarning($"[BossRuntimeAiPatternDriver] DecisionIntervalSeconds가 너무 작아 0.02초로 보정됩니다. object={name}, value={_decisionIntervalSeconds}", this);
            _decisionIntervalSeconds = 0.02f;
        }

        if (_retryIntervalSeconds < 0.02f)
        {
            Debug.LogWarning($"[BossRuntimeAiPatternDriver] RetryIntervalSeconds가 너무 작아 0.02초로 보정됩니다. object={name}, value={_retryIntervalSeconds}", this);
            _retryIntervalSeconds = 0.02f;
        }

        if (_targetSearchRange < 0f)
        {
            Debug.LogWarning($"[BossRuntimeAiPatternDriver] TargetSearchRange가 0보다 작아 0으로 보정됩니다. object={name}, value={_targetSearchRange}", this);
            _targetSearchRange = 0f;
        }

        ResolveReferences();
    }

    /// <summary>
    /// 런타임 AI 패턴 선택 루프를 시작한다.
    /// </summary>
    public void StartRuntimeAi()
    {
        if (_aiRoutine != null)
        {
            Debug.LogWarning($"[BossRuntimeAiPatternDriver] 런타임 AI가 이미 실행 중이라 중복 시작을 무시합니다. object={name}", this);
            return;
        }

        ResolveReferences();
        RebuildPatternCache();
        _aiRoutine = StartCoroutine(RunRuntimeAiRoutine());
    }

    /// <summary>
    /// 런타임 AI 패턴 선택 루프를 중지한다.
    /// </summary>
    public void StopRuntimeAi()
    {
        if (_aiRoutine == null)
        {
            return;
        }

        StopCoroutine(_aiRoutine);
        _aiRoutine = null;
    }

    /// <summary>
    /// 보스 전투 상태를 감시하며 실행 가능한 패턴을 선택하고 기존 BossController 실행 API로 전달한다.
    /// </summary>
    private IEnumerator RunRuntimeAiRoutine()
    {
        while (enabled)
        {
            if (!TryGetBossController(out BossController bossController))
            {
                yield return WaitForRetryInterval();
                continue;
            }

            if (!CanDriveRuntimeAi(bossController))
            {
                yield return WaitForRetryInterval();
                continue;
            }

            if (!CanRequestPatternSelection(bossController))
            {
                yield return WaitForDecisionInterval();
                continue;
            }

            Transform target = ResolvePatternTarget(bossController);
            if (!bossController.TrySelectPattern(target, out PatternCommonSettings selectedSettings))
            {
                yield return WaitForRetryInterval();
                continue;
            }

            if (!TryResolvePatternInstance(selectedSettings.PatternType, out BossPatternBase pattern))
            {
                yield return WaitForRetryInterval();
                continue;
            }

            if (!bossController.TryStartPatternExecution(pattern, selectedSettings))
            {
                yield return WaitForRetryInterval();
                continue;
            }

            yield return WaitForDecisionInterval();
        }

        _aiRoutine = null;
    }

    /// <summary>
    /// BossController 참조를 반환하고 없으면 Warning 로그를 출력한다.
    /// </summary>
    private bool TryGetBossController(out BossController bossController)
    {
        ResolveReferences();
        bossController = _bossController;
        if (bossController != null)
        {
            _hasLoggedMissingBossWarning = false;
            return true;
        }

        if (!_hasLoggedMissingBossWarning)
        {
            Debug.LogWarning($"[BossRuntimeAiPatternDriver] BossController가 없어 런타임 AI 패턴 선택을 수행할 수 없습니다. object={name}", this);
            _hasLoggedMissingBossWarning = true;
        }

        return false;
    }

    /// <summary>
    /// 현재 인스턴스가 런타임 AI를 구동할 권한을 가지고 있는지 검사한다.
    /// </summary>
    private bool CanDriveRuntimeAi(BossController bossController)
    {
        if (bossController.IsBossLogicAuthority())
        {
            _hasLoggedAuthorityWarning = false;
            return true;
        }

        if (!_hasLoggedAuthorityWarning)
        {
            NetworkManager networkManager = NetworkManager.Singleton; // 경고에 현재 NGO 세션 상태를 포함하기 위한 참조
            ulong localClientId = networkManager != null ? networkManager.LocalClientId : 0UL; // 권한 없는 Client 식별용 ID
            Debug.LogWarning($"[BossRuntimeAiPatternDriver] 권한 없는 Client에서 런타임 AI 패턴 선택을 시도해 중단합니다. object={name}, localClientId={localClientId}", this);
            _hasLoggedAuthorityWarning = true;
        }

        return false;
    }

    /// <summary>
    /// 보스 전투 상태가 패턴 선택 요청을 받을 수 있는 상태인지 검사한다.
    /// </summary>
    private bool CanRequestPatternSelection(BossController bossController)
    {
        if (!bossController.IsBattleActive)
        {
            return false;
        }

        if (!bossController.IsPatternSelectionEnabled)
        {
            return false;
        }

        return bossController.CurrentState == E_BossState.Idle;
    }

    /// <summary>
    /// BossPlayerTargetProvider를 통해 패턴 선택용 Player 타겟을 찾는다.
    /// </summary>
    private Transform ResolvePatternTarget(BossController bossController)
    {
        BossPlayerTargetProvider targetProvider = bossController.PlayerTargetProvider; // 기존 보스 타겟 검색 시스템
        if (targetProvider == null)
        {
            if (!_hasLoggedMissingTargetProviderWarning)
            {
                Debug.LogWarning($"[BossRuntimeAiPatternDriver] BossPlayerTargetProvider가 없어 타겟 없이 패턴 선택을 시도합니다. object={name}, boss={bossController.name}", this);
                _hasLoggedMissingTargetProviderWarning = true;
            }

            return null;
        }

        _hasLoggedMissingTargetProviderWarning = false;
        float searchRange = _targetSearchRange > 0f ? _targetSearchRange : targetProvider.DefaultExecutionRange; // 디자이너 지정값 또는 기존 기본 탐색 반경
        if (!bossController.TryFindNearestPlayerForExecution(searchRange, out Transform target, out _, out _))
        {
            return null;
        }

        return target;
    }

    /// <summary>
    /// 선택된 패턴 타입에 대응하는 런타임 패턴 컴포넌트를 찾는다.
    /// </summary>
    private bool TryResolvePatternInstance(E_BossPatternType patternType, out BossPatternBase pattern)
    {
        pattern = null;
        int patternIndex = (int)patternType; // enum 값을 캐시 배열 인덱스로 사용
        if (patternIndex <= (int)E_BossPatternType.None || patternIndex > (int)E_BossPatternType.WeakPoint)
        {
            Debug.LogWarning($"[BossRuntimeAiPatternDriver] 잘못된 패턴 타입이라 실행할 수 없습니다. object={name}, patternType={patternType}", this);
            return false;
        }

        EnsurePatternCache();
        pattern = _patternsByType[patternIndex];
        if (pattern != null)
        {
            _hasLoggedMissingPatternWarning = false;
            return true;
        }

        RebuildPatternCache();
        pattern = _patternsByType[patternIndex];
        if (pattern != null)
        {
            _hasLoggedMissingPatternWarning = false;
            return true;
        }

        if (!_hasLoggedMissingPatternWarning)
        {
            Debug.LogWarning($"[BossRuntimeAiPatternDriver] 선택된 패턴 타입을 실행할 BossPatternBase 컴포넌트를 찾지 못했습니다. object={name}, patternType={patternType}", this);
            _hasLoggedMissingPatternWarning = true;
        }

        return false;
    }

    /// <summary>
    /// 같은 GameObject의 BossController 참조를 보정한다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_bossController == null)
        {
            _bossController = GetComponent<BossController>();
        }
    }

    /// <summary>
    /// 패턴 타입별 실행 컴포넌트 캐시 배열이 준비되어 있는지 확인한다.
    /// </summary>
    private void EnsurePatternCache()
    {
        int requiredLength = (int)E_BossPatternType.WeakPoint + 1; // E_BossPatternType enum 범위 기반 캐시 길이
        if (_patternsByType != null && _patternsByType.Length == requiredLength)
        {
            return;
        }

        _patternsByType = new BossPatternBase[requiredLength];
    }

    /// <summary>
    /// 같은 GameObject에 배치된 BossPatternBase 파생 컴포넌트를 패턴 타입별로 캐시한다.
    /// </summary>
    private void RebuildPatternCache()
    {
        EnsurePatternCache();
        for (int index = 0; index < _patternsByType.Length; index++)
        {
            _patternsByType[index] = null;
        }

        BossPatternBase[] patterns = GetComponents<BossPatternBase>(); // 보스 오브젝트가 보유한 기존 패턴 실행 컴포넌트 목록
        for (int index = 0; index < patterns.Length; index++)
        {
            BossPatternBase candidate = patterns[index]; // 캐시에 등록할 후보 패턴 컴포넌트
            if (candidate == null)
            {
                continue;
            }

            int patternIndex = (int)candidate.PatternType;
            if (patternIndex <= (int)E_BossPatternType.None || patternIndex >= _patternsByType.Length)
            {
                Debug.LogWarning($"[BossRuntimeAiPatternDriver] 잘못된 PatternType을 가진 패턴 컴포넌트를 캐시에서 제외합니다. object={name}, pattern={candidate.name}, patternType={candidate.PatternType}", this);
                continue;
            }

            if (_patternsByType[patternIndex] != null)
            {
                if (!_hasLoggedDuplicatePatternWarning)
                {
                    Debug.LogWarning($"[BossRuntimeAiPatternDriver] 같은 PatternType의 패턴 컴포넌트가 중복되어 첫 번째 컴포넌트를 사용합니다. object={name}, patternType={candidate.PatternType}", this);
                    _hasLoggedDuplicatePatternWarning = true;
                }

                continue;
            }

            _patternsByType[patternIndex] = candidate;
        }
    }

    /// <summary>
    /// 정상 AI 판단 주기만큼 대기한다.
    /// </summary>
    private WaitForSeconds WaitForDecisionInterval()
    {
        return new WaitForSeconds(Mathf.Max(0.02f, _decisionIntervalSeconds));
    }

    /// <summary>
    /// 실패 조건 발생 후 재시도 주기만큼 대기한다.
    /// </summary>
    private WaitForSeconds WaitForRetryInterval()
    {
        return new WaitForSeconds(Mathf.Max(0.02f, _retryIntervalSeconds));
    }
}
