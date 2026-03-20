using System.Collections;
using UnityEngine;

/// <summary>
/// HitCore의 피격 결과를 ActionController 액션 요청으로 변환하는 전용 브리지입니다.
/// </summary>
public class ActionHitBridge : MonoBehaviour, IHitListener
{
    [SerializeField] private HitReceiver _hitReceiver; // 피격 결과 이벤트를 수신할 HitReceiver 참조
    [SerializeField] private ActionController _actionController; // 피격 결과를 전달할 ActionController 참조
    [SerializeField] private ActionHitBridgeProfile _hitBridgeProfile; // 피격 결과-액션 변환 규칙을 제공하는 ScriptableObject 프로필

    [Header("Listener Bind Retry")]
    [SerializeField] private float _retryInterval = 0.1f; // 리스너 등록 재시도 코루틴 간격(초)
    [SerializeField] private int _maxRetryCount = 30; // 리스너 등록 재시도 최대 횟수

    private Coroutine _registerCoroutine; // 리스너 등록 지연 처리 코루틴 핸들
    private bool _isListenerRegistered; // HitReceiver 리스너 등록 여부

    /// <summary>
    /// 활성화 시 HitReceiver 리스너 등록 코루틴을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        RestartRegisterCoroutine();
    }

    /// <summary>
    /// 비활성화 시 등록 코루틴을 중지하고 리스너를 즉시 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        StopRunningCoroutine(ref _registerCoroutine);
        TryImmediateUnregisterOnDisable();
    }

    /// <summary>
    /// 파괴 시점에 코루틴 정리 및 리스너 해제를 마지막으로 시도합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopRunningCoroutine(ref _registerCoroutine);

        if (_isListenerRegistered && _hitReceiver != null)
        {
            _hitReceiver.RemoveListener(this);
            _isListenerRegistered = false;
        }
    }

    /// <summary>
    /// HitReceiver의 피격 처리 결과를 받아 액션 요청으로 변환합니다.
    /// </summary>
    public void OnHitResolved(HitRequest request, HitResult result)
    {
        if (!TryResolveActionControllerReference())
        {
            Debug.LogWarning("[ActionHitBridge] ActionController is not assigned and fallback resolve failed.");
            return;
        }

        if (!result.IsAccepted)
        {
            return;
        }

        E_ActionType mappedActionType = ResolveMappedActionType(request, result); // 피격 결과를 기준으로 요청할 최종 액션 타입
        RequestMappedAction(mappedActionType, request.StatusTag);
    }

    /// <summary>
    /// 현재 프로필 기준으로 피격 결과에 대응하는 액션 타입을 계산합니다.
    /// </summary>
    private E_ActionType ResolveMappedActionType(HitRequest request, HitResult result)
    {
        if (result.IsDeadAfter)
        {
            return GetSafeProfile().DeadActionType;
        }

        if (IsBreakStatusTag(request.StatusTag))
        {
            return GetSafeProfile().BreakActionType;
        }

        return GetSafeProfile().HitActionType;
    }

    /// <summary>
    /// 지정된 매핑 액션 요청을 시도하고 실패 시 Warning 로그를 출력합니다.
    /// </summary>
    private void RequestMappedAction(E_ActionType actionType, string sourceTag)
    {
        bool accepted = _actionController.RequestAction(actionType); // 액션 요청 수락 여부
        if (accepted)
        {
            return;
        }

        Debug.LogWarning($"[ActionHitBridge] Action request denied. source={sourceTag}, action={actionType}. Check ActionRule priority/interrupt settings.");
    }

    /// <summary>
    /// StatusTag가 브레이크 이벤트로 해석 가능한지 판정합니다.
    /// </summary>
    private bool IsBreakStatusTag(string statusTag)
    {
        if (string.IsNullOrWhiteSpace(statusTag))
        {
            return false;
        }

        string[] breakStatusTags = GetSafeProfile().BreakStatusTags; // 브레이크 판정에 사용할 프로필 기반 StatusTag 배열
        for (int i = 0; i < breakStatusTags.Length; i++)
        {
            if (string.Equals(breakStatusTags[i], statusTag, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 프로필이 비어있을 때 기본값 프로필을 반환해 런타임 오류를 방지합니다.
    /// </summary>
    private ActionHitBridgeProfile GetSafeProfile()
    {
        if (_hitBridgeProfile != null)
        {
            return _hitBridgeProfile;
        }

        Debug.LogWarning($"[ActionHitBridge] ActionHitBridgeProfile is not assigned on {name}. Using fallback default mapping.");
        return ActionHitBridgeFallbackProfile.Instance;
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
        if (!_isListenerRegistered)
        {
            return;
        }

        if (TryResolveHitReceiverReference())
        {
            _hitReceiver.RemoveListener(this);
            _isListenerRegistered = false;
            return;
        }

        _isListenerRegistered = false;
        Debug.LogWarning($"[ActionHitBridge] OnDisable could not resolve HitReceiver on {name}. RemoveListener skipped.");
    }

    /// <summary>
    /// HitReceiver가 준비될 때까지 재시도한 뒤 리스너를 등록합니다.
    /// </summary>
    private IEnumerator RegisterListenerWithRetryCoroutine()
    {
        int safeMaxRetry = Mathf.Max(1, _maxRetryCount); // 잘못된 최대 재시도 설정을 보정한 안전 값
        float safeInterval = Mathf.Max(0.01f, _retryInterval); // 잘못된 재시도 간격을 보정한 안전 값

        if (_maxRetryCount < 1 || _retryInterval <= 0f)
        {
            Debug.LogWarning($"[ActionHitBridge] Invalid retry settings on {name}. Fallback maxRetry={safeMaxRetry}, interval={safeInterval}.");
        }

        for (int retryIndex = 0; retryIndex < safeMaxRetry; retryIndex++)
        {
            if (TryResolveHitReceiverReference())
            {
                if (!_isListenerRegistered)
                {
                    _hitReceiver.AddListener(this);
                    _isListenerRegistered = true;
                }

                _registerCoroutine = null;
                yield break;
            }

            if (retryIndex == 0)
            {
                Debug.LogWarning($"[ActionHitBridge] HitReceiver is null on {name}. Delaying AddListener registration.");
            }

            yield return new WaitForSeconds(safeInterval);
        }

        Debug.LogWarning($"[ActionHitBridge] AddListener registration failed after retries on {name}.");
        _registerCoroutine = null;
    }


    /// <summary>
    /// ActionController 참조가 비어있을 때 동일 오브젝트에서 fallback으로 보정합니다.
    /// </summary>
    private bool TryResolveActionControllerReference()
    {
        if (_actionController != null)
        {
            return true;
        }

        _actionController = GetComponent<ActionController>();
        if (_actionController != null)
        {
            Debug.LogWarning($"[ActionHitBridge] _actionController was null on {name}. Fallback to same GameObject ActionController.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 오브젝트 기준으로 HitReceiver 참조를 보정합니다.
    /// </summary>
    private bool TryResolveHitReceiverReference()
    {
        if (_hitReceiver != null)
        {
            return true;
        }

        _hitReceiver = GetComponent<HitReceiver>();
        if (_hitReceiver != null)
        {
            Debug.LogWarning($"[ActionHitBridge] _hitReceiver was null on {name}. Fallback to same GameObject HitReceiver.");
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

    /// <summary>
    /// ActionHitBridgeProfile이 없을 때 사용할 런타임 기본 매핑 프로필 싱글톤입니다.
    /// </summary>
    private static class ActionHitBridgeFallbackProfile
    {
        private static ActionHitBridgeProfile _instance; // 누락 프로필 상황에서 재사용할 런타임 임시 프로필 인스턴스

        /// <summary>
        /// 기본 매핑 값을 가진 런타임 임시 프로필 인스턴스를 반환합니다.
        /// </summary>
        public static ActionHitBridgeProfile Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                _instance = ScriptableObject.CreateInstance<ActionHitBridgeProfile>();
                return _instance;
            }
        }
    }
}
