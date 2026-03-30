using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 씬 로딩 실패 재시도 루프를 전담하는 서비스입니다.
/// </summary>
internal sealed class FlowRetryService
{
    private readonly MonoBehaviour _coroutineHost; // 재시도 코루틴을 실행할 호스트 MonoBehaviour입니다.
    private readonly Func<string, bool> _tryLoadScene; // 씬 로딩 재시도 시 호출할 로더 함수입니다.
    private readonly Action _onRetrySucceeded; // 재시도 성공 시 호출할 콜백입니다.
    private readonly Action<string, GameFlowState, string> _onRetryExhausted; // 재시도 횟수 초과 시 호출할 콜백입니다.

    private Coroutine _retryCoroutine; // 현재 실행 중인 재시도 코루틴 핸들입니다.
    private int _retryCount; // 동일 요청 기준 누적 재시도 횟수입니다.
    private string _lastFailedSceneName; // 이전 실패 씬 이름입니다.
    private string _lastFailedRequestName; // 이전 실패 요청 이름입니다.

    /// <summary>
    /// FlowRetryService를 생성합니다.
    /// </summary>
    internal FlowRetryService(
        MonoBehaviour coroutineHost,
        Func<string, bool> tryLoadScene,
        Action onRetrySucceeded,
        Action<string, GameFlowState, string> onRetryExhausted)
    {
        _coroutineHost = coroutineHost;
        _tryLoadScene = tryLoadScene;
        _onRetrySucceeded = onRetrySucceeded;
        _onRetryExhausted = onRetryExhausted;
    }

    /// <summary>
    /// 로딩 실패를 입력받아 재시도 또는 초과 처리 여부를 반환합니다.
    /// </summary>
    internal bool HandleFailure(string sceneName, GameFlowState loadedState, string requestName, ErrorRecoveryPolicy policy)
    {
        if (_lastFailedSceneName == sceneName && _lastFailedRequestName == requestName)
        {
            _retryCount++;
        }
        else
        {
            _lastFailedSceneName = sceneName;
            _lastFailedRequestName = requestName;
            _retryCount = 1;
        }

        if (_retryCount <= policy.MaxSceneLoadRetryCount)
        {
            StartRetry(sceneName, loadedState, requestName, policy.SceneLoadRetryIntervalSeconds);
            return true;
        }

        StopRetry();
        _onRetryExhausted?.Invoke(sceneName, loadedState, requestName);
        return false;
    }

    /// <summary>
    /// 재시도 추적 상태를 초기화합니다.
    /// </summary>
    internal void ResetTracking()
    {
        _retryCount = 0;
        _lastFailedSceneName = string.Empty;
        _lastFailedRequestName = string.Empty;
    }

    /// <summary>
    /// 현재 실행 중인 재시도 코루틴을 중지합니다.
    /// </summary>
    internal void StopRetry()
    {
        if (_retryCoroutine == null || _coroutineHost == null)
        {
            _retryCoroutine = null;
            return;
        }

        _coroutineHost.StopCoroutine(_retryCoroutine);
        _retryCoroutine = null;
    }

    /// <summary>
    /// 최근 누적 재시도 횟수를 반환합니다.
    /// </summary>
    internal int GetRetryCount()
    {
        return _retryCount;
    }

    /// <summary>
    /// 재시도 코루틴을 시작합니다.
    /// </summary>
    private void StartRetry(string sceneName, GameFlowState loadedState, string requestName, float waitSeconds)
    {
        StopRetry();

        if (_coroutineHost == null)
        {
            _onRetryExhausted?.Invoke(sceneName, loadedState, requestName);
            return;
        }

        _retryCoroutine = _coroutineHost.StartCoroutine(RetryRoutine(sceneName, loadedState, requestName, waitSeconds));
    }

    /// <summary>
    /// 지정 대기 후 씬 로딩 재시도를 수행합니다.
    /// </summary>
    private IEnumerator RetryRoutine(string sceneName, GameFlowState loadedState, string requestName, float waitSeconds)
    {
        if (waitSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(waitSeconds);
        }

        bool started = _tryLoadScene != null && _tryLoadScene.Invoke(sceneName);
        if (started)
        {
            _retryCoroutine = null;
            _onRetrySucceeded?.Invoke();
            yield break;
        }

        _retryCoroutine = null;
        _onRetryExhausted?.Invoke(sceneName, loadedState, requestName);
    }
}
