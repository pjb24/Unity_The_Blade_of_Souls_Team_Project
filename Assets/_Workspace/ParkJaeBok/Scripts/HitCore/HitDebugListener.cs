using System.Collections;
using UnityEngine;

/// <summary>
/// 피격 결과를 콘솔 로그로 출력하는 디버그 리스너입니다.
/// </summary>
public class HitDebugListener : MonoBehaviour, IHitListener
{
    [SerializeField] private HitReceiver _receiver; // 리스너를 등록/해제할 대상 HitReceiver 참조입니다.
    [SerializeField] private float _retryInterval = 0.1f; // 리시버 재탐색 코루틴의 재시도 간격(초)입니다.
    [SerializeField] private int _maxRetryCount = 30; // 리시버 재탐색 코루틴의 최대 재시도 횟수입니다.

    private Coroutine _registerCoroutine; // 지연 등록 코루틴 핸들입니다.
    private bool _isSubscribed; // HitReceiver 리스너 등록 완료 여부를 추적하는 플래그입니다.

    /// <summary>
    /// 활성화 시 지연 등록 코루틴을 시작해 리시버에 리스너를 안전하게 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        RestartRegisterCoroutine();
    }

    /// <summary>
    /// 비활성화 시 코루틴을 중지하고 가능한 경우 즉시 리스너를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        TryImmediateUnregisterOnDisable();
    }

    /// <summary>
    /// 오브젝트 파괴 시 실행 중 코루틴을 정리하고 리스너 해제를 마지막으로 시도합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _registerCoroutine);

        if (_isSubscribed && _receiver != null && _receiver.TryRemoveListener(this))
        {
            _isSubscribed = false;
        }
    }

    /// <summary>
    /// 피격 처리 결과를 콘솔에 출력합니다.
    /// </summary>
    public void OnHitResolved(HitRequest request, HitResult result)
    {
        string attackerName = request.Attacker != null ? request.Attacker.name : "None"; // 로그에 출력할 공격자 이름 문자열입니다.
        Debug.Log(
            $"[HitDebugListener] Target={name}, HitId={request.HitId}, Attacker={attackerName}, Accepted={result.IsAccepted}, " +
            $"Reason={result.RejectReason}, AppliedDamage={result.AppliedDamage}, Health={result.HealthBefore}->{result.HealthAfter}, Dead={result.IsDeadAfter}");
    }

    /// <summary>
    /// 리스너 등록 코루틴을 재시작합니다.
    /// </summary>
    private void RestartRegisterCoroutine()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        _registerCoroutine = StartCoroutine(RegisterListenerWithRetryCoroutine());
    }

    /// <summary>
    /// 비활성화 시점에 코루틴 없이 안전하게 리스너 해제를 시도합니다.
    /// </summary>
    private void TryImmediateUnregisterOnDisable()
    {
        if (_isSubscribed && TryResolveReceiverReference() && _receiver.TryRemoveListener(this))
        {
            _isSubscribed = false;
            return;
        }

        if (_isSubscribed)
        {
            Debug.LogWarning($"[HitDebugListener] OnDisable could not resolve receiver on {name}. RemoveListener skipped.");
        }
    }

    /// <summary>
    /// 리시버가 준비될 때까지 재시도한 뒤 리스너를 등록합니다.
    /// </summary>
    private IEnumerator RegisterListenerWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값입니다.
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값입니다.

        if (_maxRetryCount < 1 || _retryInterval <= 0f)
        {
            Debug.LogWarning($"[HitDebugListener] Invalid retry settings on {name}. Fallback maxRetry={safeMaxRetry}, interval={safeInterval}.");
        }

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            if (!_isSubscribed && TryResolveReceiverReference())
            {
                _isSubscribed = _receiver.TryAddListener(this);
                if (_isSubscribed)
                {
                    _registerCoroutine = null;
                    yield break;
                }
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[HitDebugListener] Receiver is null on {name}. Delaying AddListener registration.");
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[HitDebugListener] AddListener registration failed after retries on {name}.");
        _registerCoroutine = null;
    }

    /// <summary>
    /// 현재 오브젝트 기준으로 HitReceiver 참조를 보정합니다.
    /// </summary>
    private bool TryResolveReceiverReference()
    {
        if (_receiver != null)
        {
            return true;
        }

        _receiver = GetComponent<HitReceiver>();
        if (_receiver != null)
        {
            Debug.LogWarning($"[HitDebugListener] _receiver was null on {name}. Fallback to same GameObject receiver.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 실행 중인 코루틴을 안전하게 중지하고 참조를 정리합니다.
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
