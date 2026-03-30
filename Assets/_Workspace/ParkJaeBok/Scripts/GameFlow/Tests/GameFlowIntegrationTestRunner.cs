using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// GameFlow 핵심 시나리오를 플레이모드에서 수동 검증하기 위한 통합 테스트 러너입니다.
/// </summary>
public class GameFlowIntegrationTestRunner : MonoBehaviour
{
    [Tooltip("Start() 시 자동으로 테스트를 실행할지 여부입니다.")]
    [SerializeField] private bool _runOnStart; // 플레이 시작 직후 테스트 자동 실행 여부를 제어하는 플래그입니다.

    [Tooltip("테스트 실행 중 상세 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLog = true; // 케이스별 Given/When/Then 로그 출력 여부를 제어하는 플래그입니다.

    [Tooltip("테스트 완료 후 러너 오브젝트를 자동 파괴할지 여부입니다.")]
    [SerializeField] private bool _destroyAfterRun = true; // 일회성 검증 후 러너를 정리할지 여부를 제어하는 플래그입니다.

    private readonly List<string> _capturedLogMessages = new List<string>(); // GameFlowLogger 외부 훅에서 수집한 로그 메시지 목록입니다.
    private readonly List<string> _resultLines = new List<string>(); // 케이스별 통과/실패 결과 라인 목록입니다.

    /// <summary>
    /// 자동 실행 옵션이 켜져 있으면 통합 테스트 시나리오를 시작합니다.
    /// </summary>
    private void Start()
    {
        if (_runOnStart)
        {
            StartCoroutine(RunAllScenarios());
        }
    }

    /// <summary>
    /// 인스펙터 컨텍스트 메뉴에서 수동 실행할 수 있는 테스트 진입점입니다.
    /// </summary>
    [ContextMenu("Run GameFlow Integration Scenarios")]
    public void RunScenariosFromContextMenu()
    {
        StartCoroutine(RunAllScenarios());
    }

    /// <summary>
    /// 필수 시나리오 5개를 순차 실행하고 요약 결과를 출력합니다.
    /// </summary>
    private IEnumerator RunAllScenarios()
    {
        _capturedLogMessages.Clear();
        _resultLines.Clear();
        GameFlowLogger.SetExternalLogSink(CaptureGameFlowLog);

        yield return RunScenario("1) SceneLoadFailure -> Retry -> Fallback", ScenarioSceneLoadFailureRetryFallback());
        yield return RunScenario("2) RecoveryFailure -> TownFallback", ScenarioRecoveryFailureTownFallback());
        yield return RunScenario("3) SaveFailureDirtySync", ScenarioSaveFailureDirtySync());
        yield return RunScenario("4) DuplicateExitRequestGate", ScenarioDuplicateExitRequestGate());
        yield return RunScenario("5) TitleStaleCallbackAndForceResetPath", ScenarioTitleStaleCallbackAndForceResetPath());

        GameFlowLogger.SetExternalLogSink(null);
        PrintSummary();

        if (_destroyAfterRun)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 개별 시나리오 코루틴을 실행하고 예외를 실패로 기록합니다.
    /// </summary>
    private IEnumerator RunScenario(string scenarioName, IEnumerator scenario)
    {
        if (_verboseLog)
        {
            Debug.Log($"[GameFlowIntegrationTestRunner] START {scenarioName}", this);
        }

        bool failed = false; // 시나리오 실행 중 예외 발생 여부를 기록하는 플래그입니다.
        while (true)
        {
            object current = null; // 코루틴의 현재 yield 값을 담아 다음 프레임 진행을 유지하는 임시 변수입니다.
            try
            {
                if (!scenario.MoveNext())
                {
                    break;
                }

                current = scenario.Current;
            }
            catch (Exception exception)
            {
                failed = true;
                _resultLines.Add($"FAIL: {scenarioName} ({exception.Message})");
                Debug.LogError($"[GameFlowIntegrationTestRunner] FAIL {scenarioName}\n{exception}", this);
                break;
            }

            yield return current;
        }

        if (!failed)
        {
            _resultLines.Add($"PASS: {scenarioName}");
            if (_verboseLog)
            {
                Debug.Log($"[GameFlowIntegrationTestRunner] PASS {scenarioName}", this);
            }
        }
    }

    /// <summary>
    /// 시나리오 #1: 씬 로딩 실패 후 재시도 소진 시 폴백 콜백이 호출되는지 검증합니다.
    /// </summary>
    private IEnumerator ScenarioSceneLoadFailureRetryFallback()
    {
        // Given
        bool fallbackCalled = false; // 재시도 소진 후 폴백 경로 호출 여부를 검증할 플래그입니다.
        bool titleResetCalled = false; // Title 강제 리셋 분기 실행 여부를 검증할 플래그입니다.
        int loadAttemptCount = 0; // 씬 로더 재시도 호출 횟수를 추적할 카운터입니다.

        FlowRetryService retryService = new FlowRetryService(
            this,
            sceneName =>
            {
                loadAttemptCount++;
                return false;
            },
            () => { },
            (sceneName, loadedState, requestName) => { fallbackCalled = true; });

        FlowFallbackService fallbackService = new FlowFallbackService(
            () => false,
            reason =>
            {
                titleResetCalled = true;
                return true;
            },
            (state, reason) => false);

        ErrorRecoveryPolicy policy = ScriptableObject.CreateInstance<ErrorRecoveryPolicy>(); // 재시도 횟수/간격 정책을 제공할 테스트용 정책 인스턴스입니다.
        SetPrivateField(policy, "_maxSceneLoadRetryCount", 1);
        SetPrivateField(policy, "_sceneLoadRetryIntervalSeconds", 0f);

        // When
        bool firstRetryScheduled = retryService.HandleFailure("BrokenScene", GameFlowState.StagePlaying, "Test.SceneLoad", policy);
        yield return null;
        bool secondRetryScheduled = retryService.HandleFailure("BrokenScene", GameFlowState.StagePlaying, "Test.SceneLoad", policy);
        if (!secondRetryScheduled)
        {
            fallbackService.ExecuteFallback(GameFlowState.Title, "SceneLoadFailureFallback");
        }

        // Then
        AssertTrue(firstRetryScheduled, "첫 실패에서 재시도 스케줄링이 되어야 합니다.");
        AssertTrue(!secondRetryScheduled, "재시도 횟수 소진 시 더 이상 스케줄링되면 안 됩니다.");
        AssertTrue(loadAttemptCount >= 1, "재시도 씬 로더가 최소 1회 호출되어야 합니다.");
        AssertTrue(fallbackCalled, "재시도 소진 콜백이 호출되어야 합니다.");
        AssertTrue(titleResetCalled, "폴백 서비스가 Title 리셋 분기를 실행해야 합니다.");
    }

    /// <summary>
    /// 시나리오 #2: Recovery 실패 시 Town 폴백 경로가 실행되는지 검증합니다.
    /// </summary>
    private IEnumerator ScenarioRecoveryFailureTownFallback()
    {
        // Given
        bool townFallbackCalled = false; // Town 폴백 호출 여부를 검증할 플래그입니다.
        FlowFallbackService fallbackService = new FlowFallbackService(
            () =>
            {
                townFallbackCalled = true;
                return true;
            },
            reason => false,
            (state, reason) => false);

        // When
        bool fallbackResult = fallbackService.ExecuteFallback(GameFlowState.Town, "DeathRecoveryFailure.Test");
        yield return null;

        // Then
        AssertTrue(fallbackResult, "Town 폴백 결과가 true여야 합니다.");
        AssertTrue(townFallbackCalled, "Town 폴백 콜백이 호출되어야 합니다.");
    }

    /// <summary>
    /// 시나리오 #3: 저장 실패 보고 시 더티 플래그/사유 문자열이 갱신되는지 검증합니다.
    /// </summary>
    private IEnumerator ScenarioSaveFailureDirtySync()
    {
        // Given
        GameObject controllerObject = new GameObject("GameFlowController_Test_SaveFailure"); // 저장 실패 동기화 검증용 컨트롤러 호스트 오브젝트입니다.
        GameFlowController controller = controllerObject.AddComponent<GameFlowController>(); // 저장 실패 API를 호출할 테스트 대상 컨트롤러 인스턴스입니다.

        // When
        controller.NotifySaveFailed("ManualFailureReason");
        yield return null;

        // Then
        AssertTrue(controller.HasSaveFailureDirty, "저장 실패 후 HasSaveFailureDirty가 true여야 합니다.");
        AssertTrue(controller.LastSaveFailureReason == "ManualFailureReason", "저장 실패 사유 문자열이 최신 값으로 갱신되어야 합니다.");
        DestroyImmediate(controllerObject);
    }

    /// <summary>
    /// 시나리오 #4: 종료 게이트가 중복 종료 요청을 원자적으로 차단하는지 검증합니다.
    /// </summary>
    private IEnumerator ScenarioDuplicateExitRequestGate()
    {
        // Given
        FlowExitGuard exitGuard = new FlowExitGuard(); // 중복 종료 요청 차단을 검증할 원자적 게이트 인스턴스입니다.

        // When
        bool firstEnter = exitGuard.TryEnter();
        bool secondEnter = exitGuard.TryEnter();
        bool enteredBeforeRelease = exitGuard.IsEntered();
        exitGuard.Release();
        bool thirdEnterAfterRelease = exitGuard.TryEnter();
        yield return null;

        // Then
        AssertTrue(firstEnter, "첫 번째 종료 진입은 성공해야 합니다.");
        AssertTrue(!secondEnter, "두 번째 종료 진입은 차단되어야 합니다.");
        AssertTrue(enteredBeforeRelease, "Release 전에는 게이트가 점유 상태여야 합니다.");
        AssertTrue(thirdEnterAfterRelease, "Release 이후 재진입은 다시 허용되어야 합니다.");
    }

    /// <summary>
    /// 시나리오 #5: stale callback 무시와 타이틀 미스매치 처리 경로를 검증합니다.
    /// </summary>
    private IEnumerator ScenarioTitleStaleCallbackAndForceResetPath()
    {
        // Given
        GameObject controllerObject = new GameObject("GameFlowController_Test_StaleCallback"); // stale callback 검증용 컨트롤러 호스트 오브젝트입니다.
        GameFlowController controller = controllerObject.AddComponent<GameFlowController>(); // stale callback 처리 함수를 호출할 테스트 대상 컨트롤러 인스턴스입니다.
        SetPrivateField(controller, "_activeSceneLoadEpochId", 17);
        SetPrivateField(controller, "_activeSceneLoadSceneName", "ExpectedScene");
        SetPrivateField(controller, "_pendingLoadedState", GameFlowState.Title);
        SetPrivateField(controller, "_titleSceneName", "Title");

        // When
        InvokePrivateMethod(controller, "HandleAfterSceneLoad", "UnexpectedOtherScene");
        yield return null;
        int epochAfterStale = (int)GetPrivateField(controller, "_activeSceneLoadEpochId");
        string expectedSceneAfterStale = (string)GetPrivateField(controller, "_activeSceneLoadSceneName");

        controller.NotifyTitleReturnStateMismatch("ForceResetPathProbe");
        yield return null;

        // Then
        AssertTrue(epochAfterStale == 17, "stale callback은 무시되어 active epoch가 유지되어야 합니다.");
        AssertTrue(expectedSceneAfterStale == "ExpectedScene", "stale callback은 기대 씬 이름을 덮어쓰면 안 됩니다.");
        AssertTrue(ContainsCapturedLog("타이틀 복귀 중 상태 꼬임"), "Title mismatch 처리 경고 로그가 기록되어야 합니다.");
        DestroyImmediate(controllerObject);
    }

    /// <summary>
    /// GameFlowLogger 외부 훅으로 전달된 로그 메시지를 버퍼에 저장합니다.
    /// </summary>
    private void CaptureGameFlowLog(string level, string message)
    {
        _capturedLogMessages.Add($"[{level}] {message}");
    }

    /// <summary>
    /// 수집된 로그에 특정 키워드가 포함되는지 확인합니다.
    /// </summary>
    private bool ContainsCapturedLog(string keyword)
    {
        for (int i = 0; i < _capturedLogMessages.Count; i++)
        {
            if (_capturedLogMessages[i].Contains(keyword))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 테스트 단정식 검증 실패 시 예외를 발생시킵니다.
    /// </summary>
    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 리플렉션으로 private 메서드를 호출합니다.
    /// </summary>
    private void InvokePrivateMethod(object target, string methodName, params object[] parameters)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic); // private 메서드 호출에 사용할 리플렉션 MethodInfo입니다.
        if (method == null)
        {
            throw new MissingMethodException(target.GetType().Name, methodName);
        }

        method.Invoke(target, parameters);
    }

    /// <summary>
    /// 리플렉션으로 private 필드 값을 설정합니다.
    /// </summary>
    private void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic); // private 필드 주입에 사용할 리플렉션 FieldInfo입니다.
        if (field == null)
        {
            throw new MissingFieldException(target.GetType().Name, fieldName);
        }

        field.SetValue(target, value);
    }

    /// <summary>
    /// 리플렉션으로 private 필드 값을 조회합니다.
    /// </summary>
    private object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic); // private 필드 조회에 사용할 리플렉션 FieldInfo입니다.
        if (field == null)
        {
            throw new MissingFieldException(target.GetType().Name, fieldName);
        }

        return field.GetValue(target);
    }

    /// <summary>
    /// 테스트 실행 결과를 요약 출력합니다.
    /// </summary>
    private void PrintSummary()
    {
        for (int i = 0; i < _resultLines.Count; i++)
        {
            Debug.Log($"[GameFlowIntegrationTestRunner] {_resultLines[i]}", this);
        }
    }
}
