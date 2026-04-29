using System.Collections;
using UnityEngine;

/// <summary>
/// Provides designer-friendly test entry points for calling BossController.StartBattle during Play Mode.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossBattleTestStarter : MonoBehaviour
{
    [Header("Required References")]
    [Tooltip("BossController that receives the test StartBattle call. If empty, this component searches the same GameObject.")]
    [SerializeField] private BossController _bossController; // BossController target used by StartBattle test calls.

    [Tooltip("Fan Projectile pattern component used by the Pattern 1 test command.")]
    [SerializeField] private BossFanProjectilePattern _fanProjectilePattern; // Pattern 1 component used by the context menu test command.

    [Tooltip("Ground Spike pattern component used by the Pattern 2 test command.")]
    [SerializeField] private BossGroundSpikePattern _groundSpikePattern; // Pattern 2 component used by the context menu test command.

    [Tooltip("Summon Monster pattern component used by the Pattern 3 test command.")]
    [SerializeField] private BossSummonMonsterPattern _summonMonsterPattern; // Pattern 3 component used by the context menu test command.

    [Header("Start Options")]
    [Tooltip("Whether StartBattle should be called automatically when this component starts in Play Mode.")]
    [SerializeField] private bool _startBattleOnStart; // Enables automatic StartBattle test execution during Play Mode.

    [Tooltip("Delay in seconds before automatic StartBattle is called.")]
    [Min(0f)]
    [SerializeField] private float _autoStartDelaySeconds; // Optional delay before automatic StartBattle test execution.

    private Coroutine _autoStartCoroutine; // Running delayed auto-start coroutine handle.

    /// <summary>
    /// Resolves the BossController reference before runtime test calls.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Starts the optional automatic StartBattle test flow.
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
    /// Stops the delayed auto-start coroutine when this tester is disabled.
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
    /// Corrects invalid inspector values and refreshes references.
    /// </summary>
    private void OnValidate()
    {
        if (_autoStartDelaySeconds < 0f)
        {
            Debug.LogWarning($"[BossBattleTestStarter] AutoStartDelaySeconds was below zero and clamped. object={name}, value={_autoStartDelaySeconds}", this);
            _autoStartDelaySeconds = 0f;
        }

        ResolveReferences();
    }

    /// <summary>
    /// Calls BossController.StartBattle from the component context menu.
    /// </summary>
    [ContextMenu("Test StartBattle")]
    public void StartBattleFromTest()
    {
        ResolveReferences();
        if (_bossController == null)
        {
            Debug.LogWarning($"[BossBattleTestStarter] StartBattle test failed because BossController is missing. object={name}", this);
            return;
        }

        _bossController.StartBattle();
        Debug.Log($"[BossBattleTestStarter] StartBattle test requested. object={name}, state={_bossController.CurrentState}, battleActive={_bossController.IsBattleActive}", this);
    }

    /// <summary>
    /// Calls BossController.ResetBattle from the component context menu for repeated testing.
    /// </summary>
    [ContextMenu("Test ResetBattle")]
    public void ResetBattleFromTest()
    {
        ResolveReferences();
        if (_bossController == null)
        {
            Debug.LogWarning($"[BossBattleTestStarter] ResetBattle test failed because BossController is missing. object={name}", this);
            return;
        }

        _bossController.ResetBattle();
        Debug.Log($"[BossBattleTestStarter] ResetBattle test requested. object={name}, state={_bossController.CurrentState}, battleActive={_bossController.IsBattleActive}", this);
    }

    /// <summary>
    /// Calls BossController.StopBattle from the component context menu for repeated testing.
    /// </summary>
    [ContextMenu("Test StopBattle")]
    public void StopBattleFromTest()
    {
        ResolveReferences();
        if (_bossController == null)
        {
            Debug.LogWarning($"[BossBattleTestStarter] StopBattle test failed because BossController is missing. object={name}", this);
            return;
        }

        _bossController.StopBattle();
        Debug.Log($"[BossBattleTestStarter] StopBattle test requested. object={name}, state={_bossController.CurrentState}, battleActive={_bossController.IsBattleActive}", this);
    }

    /// <summary>
    /// Starts the Fan Projectile boss pattern through BossController for Pattern 1 testing.
    /// </summary>
    [ContextMenu("Test Pattern 1 - Fan Projectile")]
    public void StartFanProjectilePatternFromTest()
    {
        TryStartPatternFromTest(_fanProjectilePattern, "FanProjectile");
    }

    /// <summary>
    /// Starts the Ground Spike boss pattern through BossController for Pattern 2 testing.
    /// </summary>
    [ContextMenu("Test Pattern 2 - Ground Spike")]
    public void StartGroundSpikePatternFromTest()
    {
        TryStartPatternFromTest(_groundSpikePattern, "GroundSpike");
    }

    /// <summary>
    /// Starts the Summon Monster boss pattern through BossController for Pattern 3 testing.
    /// </summary>
    [ContextMenu("Test Pattern 3 - Summon Monster")]
    public void StartSummonMonsterPatternFromTest()
    {
        TryStartPatternFromTest(_summonMonsterPattern, "SummonMonster");
    }

    /// <summary>
    /// Waits for the configured delay before calling StartBattle.
    /// </summary>
    private IEnumerator StartBattleAfterDelay()
    {
        yield return new WaitForSeconds(_autoStartDelaySeconds);
        _autoStartCoroutine = null;
        StartBattleFromTest();
    }

    /// <summary>
    /// Starts a boss pattern through the shared BossController execution path.
    /// </summary>
    private void TryStartPatternFromTest(BossPatternBase pattern, string patternLabel)
    {
        ResolveReferences();
        if (_bossController == null)
        {
            Debug.LogWarning($"[BossBattleTestStarter] Pattern test failed because BossController is missing. object={name}, pattern={patternLabel}", this);
            return;
        }

        if (pattern == null)
        {
            Debug.LogWarning($"[BossBattleTestStarter] Pattern test failed because pattern reference is missing. object={name}, pattern={patternLabel}", this);
            return;
        }

        if (!_bossController.IsBattleActive)
        {
            Debug.LogWarning($"[BossBattleTestStarter] Pattern test is starting battle first because battle is inactive. object={name}, pattern={patternLabel}", this);
            _bossController.StartBattle();
        }

        bool started = _bossController.TryStartPatternExecution(pattern); // Pattern start result returned by the authoritative BossController path.
        Debug.Log($"[BossBattleTestStarter] Pattern test requested. object={name}, pattern={patternLabel}, started={started}, state={_bossController.CurrentState}", this);
    }

    /// <summary>
    /// Resolves BossController from the same GameObject when the field is not assigned.
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
