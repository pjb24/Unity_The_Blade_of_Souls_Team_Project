using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
/// <summary>
/// Play Mode에서 BossController.StartBattle을 호출하기 위한 디자이너용 테스트 진입 지점을 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossBattleTestStarter : MonoBehaviour
{
    [Header("필수 참조")]
    [Tooltip("테스트용 StartBattle 호출을 받을 BossController입니다. 비어있으면 동일한 GameObject에서 자동으로 찾습니다.")]
    [SerializeField] private BossController _bossController; // StartBattle 테스트 호출에 사용할 BossController 대상

    [Tooltip("패턴 1 테스트 명령에서 사용할 Fan Projectile 패턴 컴포넌트")]
    [SerializeField] private BossFanProjectilePattern _fanProjectilePattern; // ContextMenu 테스트 명령에서 사용하는 패턴 1 컴포넌트

    [Tooltip("패턴 2 테스트 명령에서 사용할 Ground Spike 패턴 컴포넌트")]
    [SerializeField] private BossGroundSpikePattern _groundSpikePattern; // ContextMenu 테스트 명령에서 사용하는 패턴 2 컴포넌트

    [Tooltip("패턴 3 테스트 명령에서 사용할 Summon Monster 패턴 컴포넌트")]
    [SerializeField] private BossSummonMonsterPattern _summonMonsterPattern; // ContextMenu 테스트 명령에서 사용하는 패턴 3 컴포넌트

    [Header("시작 옵션")]
    [Tooltip("Play Mode 시작 시 자동으로 StartBattle을 호출할지 여부")]
    [SerializeField] private bool _startBattleOnStart; // Play Mode에서 자동 StartBattle 테스트 실행 여부

    [Tooltip("자동 StartBattle 호출 전 대기 시간(초)")]
    [Min(0f)]
    [SerializeField] private float _autoStartDelaySeconds; // 자동 StartBattle 실행 전 대기 시간

    private Coroutine _autoStartCoroutine; // 지연 실행 코루틴 핸들

    /// <summary>
    /// 런타임 테스트 호출 전에 BossController 참조를 설정한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 자동 StartBattle 테스트 실행을 시작한다.
    /// </summary>
    private void Start()
    {
        if (!_startBattleOnStart)
        {
            return;
        }

        if (_autoStartDelaySeconds <= 0f)
        {
            StartBattleFromTest();
            return;
        }

        _autoStartCoroutine = StartCoroutine(StartBattleAfterDelay());
    }

    /// <summary>
    /// 이 컴포넌트가 비활성화될 때 지연 실행 코루틴을 중지한다.
    /// </summary>
    private void OnDisable()
    {
        if (_autoStartCoroutine == null)
        {
            return;
        }

        StopCoroutine(_autoStartCoroutine);
        _autoStartCoroutine = null;
    }

    /// <summary>
    /// 잘못된 인스펙터 값을 보정하고 참조를 다시 설정한다.
    /// </summary>
    private void OnValidate()
    {
        if (_autoStartDelaySeconds < 0f)
        {
            Debug.LogWarning($"[BossBattleTestStarter] AutoStartDelaySeconds가 0보다 작아서 보정됨. object={name}, value={_autoStartDelaySeconds}", this);
            _autoStartDelaySeconds = 0f;
        }

        ResolveReferences();
    }

    /// <summary>
    /// ContextMenu에서 BossController.StartBattle을 호출한다.
    /// </summary>
    [ContextMenu("Test StartBattle")]
    public void StartBattleFromTest()
    {
        ResolveReferences();
        if (_bossController == null)
        {
            Debug.LogWarning($"[BossBattleTestStarter] BossController가 없어서 StartBattle 테스트 실패. object={name}", this);
            return;
        }

        _bossController.StartBattle();
        Debug.Log($"[BossBattleTestStarter] StartBattle 테스트 요청됨. object={name}, state={_bossController.CurrentState}, battleActive={_bossController.IsBattleActive}", this);
    }

    /// <summary>
    /// 반복 테스트를 위해 ContextMenu에서 BossController.ResetBattle을 호출한다.
    /// </summary>
    [ContextMenu("Test ResetBattle")]
    public void ResetBattleFromTest()
    {
        ResolveReferences();
        if (_bossController == null)
        {
            Debug.LogWarning($"[BossBattleTestStarter] BossController가 없어서 ResetBattle 테스트 실패. object={name}", this);
            return;
        }

        _bossController.ResetBattle();
        Debug.Log($"[BossBattleTestStarter] ResetBattle 테스트 요청됨. object={name}, state={_bossController.CurrentState}, battleActive={_bossController.IsBattleActive}", this);
    }

    /// <summary>
    /// 반복 테스트를 위해 ContextMenu에서 BossController.StopBattle을 호출한다.
    /// </summary>
    [ContextMenu("Test StopBattle")]
    public void StopBattleFromTest()
    {
        ResolveReferences();
        if (_bossController == null)
        {
            Debug.LogWarning($"[BossBattleTestStarter] BossController가 없어서 StopBattle 테스트 실패. object={name}", this);
            return;
        }

        _bossController.StopBattle();
        Debug.Log($"[BossBattleTestStarter] StopBattle 테스트 요청됨. object={name}, state={_bossController.CurrentState}, battleActive={_bossController.IsBattleActive}", this);
    }

    /// <summary>
    /// 패턴 1 테스트를 위해 Fan Projectile 패턴을 실행한다.
    /// </summary>
    [ContextMenu("Test Pattern 1 - Fan Projectile")]
    public void StartFanProjectilePatternFromTest()
    {
        TryStartPatternFromTest(_fanProjectilePattern, "FanProjectile");
    }

    /// <summary>
    /// 패턴 2 테스트를 위해 Ground Spike 패턴을 실행한다.
    /// </summary>
    [ContextMenu("Test Pattern 2 - Ground Spike")]
    public void StartGroundSpikePatternFromTest()
    {
        TryStartPatternFromTest(_groundSpikePattern, "GroundSpike");
    }

    /// <summary>
    /// 패턴 3 테스트를 위해 Summon Monster 패턴을 실행한다.
    /// </summary>
    [ContextMenu("Test Pattern 3 - Summon Monster")]
    public void StartSummonMonsterPatternFromTest()
    {
        TryStartPatternFromTest(_summonMonsterPattern, "SummonMonster");
    }

    /// <summary>
    /// 설정된 지연 시간 후 StartBattle을 호출한다.
    /// </summary>
    private IEnumerator StartBattleAfterDelay()
    {
        yield return new WaitForSeconds(_autoStartDelaySeconds);
        _autoStartCoroutine = null;
        StartBattleFromTest();
    }

    /// <summary>
    /// 공통 BossController 실행 경로를 통해 보스 패턴을 실행한다.
    /// </summary>
    private void TryStartPatternFromTest(BossPatternBase pattern, string patternLabel)
    {
        ResolveReferences();
        if (_bossController == null)
        {
            Debug.LogWarning($"[BossBattleTestStarter] BossController가 없어서 패턴 테스트 실패. object={name}, pattern={patternLabel}", this);
            return;
        }

        if (pattern == null)
        {
            Debug.LogWarning($"[BossBattleTestStarter] 패턴 참조가 없어서 테스트 실패. object={name}, pattern={patternLabel}", this);
            return;
        }

        if (!_bossController.IsBattleActive)
        {
            Debug.LogWarning($"[BossBattleTestStarter] 전투가 비활성 상태라 먼저 StartBattle 실행. object={name}, pattern={patternLabel}", this);
            _bossController.StartBattle();
        }

        bool started = _bossController.TryStartPatternExecution(pattern); // 권한을 가진 BossController 경로에서 반환된 패턴 시작 결과
        Debug.Log($"[BossBattleTestStarter] 패턴 테스트 요청됨. object={name}, pattern={patternLabel}, started={started}, state={_bossController.CurrentState}", this);
    }

    /// <summary>
    /// BossController가 할당되지 않았을 경우 동일한 GameObject에서 자동으로 찾는다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_bossController == null)
        {
            _bossController = GetComponent<BossController>();
        }

        if (_fanProjectilePattern == null)
        {
            _fanProjectilePattern = GetComponent<BossFanProjectilePattern>();
        }

        if (_groundSpikePattern == null)
        {
            _groundSpikePattern = GetComponent<BossGroundSpikePattern>();
        }

        if (_summonMonsterPattern == null)
        {
            _summonMonsterPattern = GetComponent<BossSummonMonsterPattern>();
        }
    }
}
#endif
