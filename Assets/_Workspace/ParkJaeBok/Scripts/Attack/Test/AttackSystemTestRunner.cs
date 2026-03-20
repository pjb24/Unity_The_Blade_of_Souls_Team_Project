using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// AttackExecutor와 Action/Hit/Health 연동을 런타임에서 빠르게 검증하기 위한 테스트 러너입니다.
/// </summary>
public class AttackSystemTestRunner : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ActionController _actionController; // 테스트 대상 액터의 ActionController 참조입니다.
    [SerializeField] private AttackExecutor _attackExecutor; // 테스트 대상 액터의 AttackExecutor 참조입니다.

    [Header("Input")]
    [SerializeField] private Key _attackAKey = Key.Digit1; // Attack A 실행 테스트를 트리거할 키입니다.
    [SerializeField] private Key _attackBKey = Key.Digit2; // Attack B 실행 테스트를 트리거할 키입니다.
    [SerializeField] private Key _attackCKey = Key.Digit3; // Attack C 실행 테스트를 트리거할 키입니다.
    [SerializeField] private Key _executeOnlyKey = Key.E; // 현재 액션 유지 상태에서 AttackExecutor 수동 실행을 트리거할 키입니다.
    [SerializeField] private Key _autoSequenceKey = Key.T; // 자동 시퀀스 테스트를 시작할 키입니다.

    [Header("Action Mapping")]
    [SerializeField] private E_ActionType _attackAActionType = E_ActionType.Attack; // 공격 A에 대응하는 액션 타입입니다.
    [SerializeField] private E_ActionType _attackBActionType = E_ActionType.AttackCombo1; // 공격 B에 대응하는 액션 타입입니다.
    [SerializeField] private E_ActionType _attackCActionType = E_ActionType.AttackCombo2; // 공격 C에 대응하는 액션 타입입니다.

    [Header("Auto Sequence")]
    [SerializeField] private float _stepInterval = 0.35f; // 자동 시퀀스에서 각 단계 사이 대기 시간(초)입니다.
    [SerializeField] private bool _runSequenceOnStart; // 시작 시 자동 시퀀스를 즉시 실행할지 여부입니다.

    private Coroutine _sequenceCoroutine; // 현재 실행 중인 자동 시퀀스 코루틴 핸들입니다.
    private bool _didLogMissingKeyboardWarning; // 키보드 미검출 경고 로그 중복 출력을 방지하는 플래그입니다.

    /// <summary>
    /// 시작 시 의존성 자동 보정과 시작 옵션을 처리합니다.
    /// </summary>
    private void Start()
    {
        ResolveDependencies();

        if (_runSequenceOnStart)
        {
            StartAutoSequence();
        }
    }

    /// <summary>
    /// 매 프레임 New Input System 입력을 감지해 공격 테스트를 실행합니다.
    /// </summary>
    private void Update()
    {
        if (IsPressedThisFrame(_attackAKey))
        {
            RunSingleAttackTest(_attackAActionType, "AttackA");
        }

        if (IsPressedThisFrame(_attackBKey))
        {
            RunSingleAttackTest(_attackBActionType, "AttackB");
        }

        if (IsPressedThisFrame(_attackCKey))
        {
            RunSingleAttackTest(_attackCActionType, "AttackC");
        }

        if (IsPressedThisFrame(_executeOnlyKey))
        {
            ExecuteCurrentActionOnly();
        }

        if (IsPressedThisFrame(_autoSequenceKey))
        {
            StartAutoSequence();
        }
    }

    /// <summary>
    /// 지정한 키가 이번 프레임에 눌렸는지 New Input System으로 판정합니다.
    /// </summary>
    private bool IsPressedThisFrame(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            if (!_didLogMissingKeyboardWarning)
            {
                Debug.LogWarning($"[AttackSystemTestRunner] Keyboard device not found on {name}. New Input System input is unavailable.");
                _didLogMissingKeyboardWarning = true;
            }

            return false;
        }

        if (_didLogMissingKeyboardWarning)
        {
            _didLogMissingKeyboardWarning = false;
        }

        KeyControl keyControl = keyboard[key];
        if (keyControl == null)
        {
            Debug.LogWarning($"[AttackSystemTestRunner] Invalid key binding({key}) on {name}.");
            return false;
        }

        return keyControl.wasPressedThisFrame;
    }

    /// <summary>
    /// 인스펙터 우클릭 메뉴에서 자동 시퀀스를 시작합니다.
    /// </summary>
    [ContextMenu("Start Auto Sequence")]
    public void StartAutoSequence()
    {
        ResolveDependencies();

        if (!ValidateCoreDependencies())
        {
            return;
        }

        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
        }

        _sequenceCoroutine = StartCoroutine(AutoSequenceCoroutine());
    }

    /// <summary>
    /// 인스펙터 우클릭 메뉴에서 자동 시퀀스를 중지합니다.
    /// </summary>
    [ContextMenu("Stop Auto Sequence")]
    public void StopAutoSequence()
    {
        if (_sequenceCoroutine == null)
        {
            return;
        }

        StopCoroutine(_sequenceCoroutine);
        _sequenceCoroutine = null;
    }

    /// <summary>
    /// 단일 액션 기반 공격 테스트를 1회 수행하고 결과를 로그로 출력합니다.
    /// </summary>
    public void RunSingleAttackTest(E_ActionType actionType, string label)
    {
        ResolveDependencies();

        if (!ValidateCoreDependencies())
        {
            return;
        }

        bool requestAccepted = _actionController.RequestAction(actionType);
        bool executeResult = _attackExecutor.TryExecuteCurrentActionAttack();
        Debug.Log($"[AttackSystemTestRunner] {label} | RequestAccepted={requestAccepted}, ExecuteResult={executeResult}");
    }

    /// <summary>
    /// 현재 액션을 바꾸지 않고 AttackExecutor 실행만 수동으로 검증합니다.
    /// </summary>
    public void ExecuteCurrentActionOnly()
    {
        ResolveDependencies();

        if (!ValidateCoreDependencies())
        {
            return;
        }

        bool executeResult = _attackExecutor.TryExecuteCurrentActionAttack();
        Debug.Log($"[AttackSystemTestRunner] ExecuteOnly | ExecuteResult={executeResult}");
    }

    /// <summary>
    /// A/B/C 순서로 공격 테스트를 자동 실행하는 코루틴입니다.
    /// </summary>
    private IEnumerator AutoSequenceCoroutine()
    {
        float safeStepInterval = Mathf.Max(0.05f, _stepInterval);
        if (!Mathf.Approximately(safeStepInterval, _stepInterval))
        {
            Debug.LogWarning($"[AttackSystemTestRunner] Invalid _stepInterval({_stepInterval}) on {name}. Fallback={safeStepInterval}");
        }

        RunSingleAttackTest(_attackAActionType, "Sequence-AttackA");
        yield return new WaitForSeconds(safeStepInterval);

        RunSingleAttackTest(_attackBActionType, "Sequence-AttackB");
        yield return new WaitForSeconds(safeStepInterval);

        RunSingleAttackTest(_attackCActionType, "Sequence-AttackC");
        yield return new WaitForSeconds(safeStepInterval);

        _sequenceCoroutine = null;
    }

    /// <summary>
    /// 비어 있는 의존성 참조를 동일 오브젝트 기준으로 자동 보정합니다.
    /// </summary>
    private void ResolveDependencies()
    {
        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
        }

        if (_attackExecutor == null)
        {
            _attackExecutor = GetComponent<AttackExecutor>();
        }
    }

    /// <summary>
    /// 테스트 실행에 필요한 핵심 의존성의 유효성을 확인합니다.
    /// </summary>
    private bool ValidateCoreDependencies()
    {
        if (_actionController == null)
        {
            Debug.LogWarning($"[AttackSystemTestRunner] Missing ActionController on {name}.");
            return false;
        }

        if (_attackExecutor == null)
        {
            Debug.LogWarning($"[AttackSystemTestRunner] Missing AttackExecutor on {name}.");
            return false;
        }

        return true;
    }
}
