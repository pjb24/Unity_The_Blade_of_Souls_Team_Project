using System.Collections;
using UnityEngine;

public class BgmContextBridge : MonoBehaviour
{
    [SerializeField]
    private BgmDirector _bgmDirector; // 컨텍스트 요청을 등록/해제할 대상 BgmDirector 참조

    [SerializeField]
    private E_BgmContextType _contextType = E_BgmContextType.None; // 브리지 컴포넌트가 담당하는 BGM 컨텍스트 타입

    [SerializeField]
    private bool _pushOnEnable = false; // 오브젝트 활성화 시 컨텍스트 등록을 자동 수행할지 여부

    [SerializeField]
    private bool _popOnDisable = false; // 오브젝트 비활성화 시 컨텍스트 해제를 자동 수행할지 여부

    [SerializeField]
    [Min(0.01f)]
    private float _retryInterval = 0.1f; // Director 재탐색 코루틴의 재시도 간격(초)

    [SerializeField]
    [Min(1)]
    private int _maxRetryCount = 30; // Director 재탐색 코루틴의 최대 재시도 횟수

    private Coroutine _registerCoroutine; // 지연 등록 코루틴 핸들

    /// <summary>
    /// 활성화 시 옵션에 따라 지연 등록 코루틴을 시작한다.
    /// </summary>
    private void OnEnable()
    {
        if (_pushOnEnable)
        {
            RestartRegisterCoroutine();
        }
    }

    /// <summary>
    /// 비활성화 시 등록 코루틴을 중지하고 가능한 경우 즉시 컨텍스트를 해제한다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerCoroutine);

        if (_popOnDisable)
        {
            TryImmediateUnregisterOnDisable();
        }
    }

    /// <summary>
    /// 파괴 시 등록 코루틴을 정리하고 컨텍스트를 최종 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _registerCoroutine);

        if (TryResolveDirectorReference())
        {
            _bgmDirector.PopContext(_contextType, this);
        }
    }

    /// <summary>
    /// 현재 오브젝트를 requester로 사용해 컨텍스트 등록을 요청한다.
    /// </summary>
    public void PushContext()
    {
        RestartRegisterCoroutine();
    }

    /// <summary>
    /// 현재 오브젝트를 requester로 사용해 컨텍스트 등록을 해제한다.
    /// </summary>
    public void PopContext()
    {
        StopRunningCoroutine(ref _registerCoroutine);

        if (TryResolveDirectorReference() == false)
        {
            Debug.LogWarning($"[BgmContextBridge] Director가 null이라 PopContext를 건너뜁니다. target={name}", this);
            return;
        }

        _bgmDirector.PopContext(_contextType, this);
    }

    /// <summary>
    /// 현재 컨텍스트 상태를 즉시 재평가하도록 Director에 요청한다.
    /// </summary>
    public void EvaluateNow()
    {
        if (TryResolveDirectorReference() == false)
        {
            Debug.LogWarning($"[BgmContextBridge] Director가 null이라 EvaluateNow를 건너뜁니다. target={name}", this);
            return;
        }

        _bgmDirector.EvaluateAndApplyBestContext(true);
    }

    /// <summary>
    /// Director가 준비될 때까지 재시도한 뒤 컨텍스트를 등록한다.
    /// </summary>
    private IEnumerator RegisterContextWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값

        if (_maxRetryCount < 1 || _retryInterval <= 0f)
        {
            Debug.LogWarning($"[BgmContextBridge] Invalid retry settings on {name}. Fallback maxRetry={safeMaxRetry}, interval={safeInterval}.", this);
        }

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            if (TryResolveDirectorReference())
            {
                _bgmDirector.PushContext(_contextType, this);
                _registerCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[BgmContextBridge] Director is null on {name}. Delaying PushContext registration.", this);
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[BgmContextBridge] PushContext registration failed after retries on {name}.", this);
        _registerCoroutine = null;
    }

    /// <summary>
    /// 비활성화 시점에 코루틴 없이 안전하게 컨텍스트 해제를 시도한다.
    /// </summary>
    private void TryImmediateUnregisterOnDisable()
    {
        if (TryResolveDirectorReference())
        {
            _bgmDirector.PopContext(_contextType, this);
            return;
        }

        Debug.LogWarning($"[BgmContextBridge] OnDisable could not resolve director on {name}. PopContext skipped.", this);
    }

    /// <summary>
    /// 등록 코루틴을 재시작한다.
    /// </summary>
    private void RestartRegisterCoroutine()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        _registerCoroutine = StartCoroutine(RegisterContextWithRetryCoroutine());
    }

    /// <summary>
    /// 설정된 참조 또는 씬 탐색으로 BgmDirector 참조를 보정한다.
    /// </summary>
    private bool TryResolveDirectorReference()
    {
        if (_bgmDirector != null)
        {
            return true;
        }

        _bgmDirector = FindAnyObjectByType<BgmDirector>();
        if (_bgmDirector != null)
        {
            Debug.LogWarning($"[BgmContextBridge] _bgmDirector was null on {name}. Fallback to scene lookup.", this);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 실행 중인 코루틴을 안전하게 중지하고 참조를 정리한다.
    /// </summary>
    private void StopRunningCoroutine(ref Coroutine coroutineHandle)
    {
        if (coroutineHandle == null)
        {
            return;
        }

        StopCoroutine(coroutineHandle);
        coroutineHandle = null;
    }
}
