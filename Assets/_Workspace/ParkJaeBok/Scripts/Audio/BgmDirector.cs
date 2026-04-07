using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BgmDirector : MonoBehaviour
{
    [Header("Rule Table")]
    [SerializeField]
    private List<BgmContextRule> _contextRules = new List<BgmContextRule>(); // 컨텍스트별 BGM 선택 규칙 목록

    [Header("Fallback")]
    [SerializeField]
    private bool _useFallbackBgm = true; // 활성 컨텍스트가 없을 때 폴백 BGM을 사용할지 여부

    [SerializeField]
    private E_SoundId _fallbackBgmId = E_SoundId.BGM_Stage01; // 컨텍스트가 비었을 때 재생할 기본 BGM ID

    [SerializeField]
    [Min(0f)]
    private float _fallbackFadeDuration = 0.75f; // 폴백 BGM 전환 시 적용할 크로스페이드 시간

    [Header("Switch Policy")]
    [SerializeField]
    [Min(0f)]
    private float _globalMinSwitchInterval = 0.05f; // BGM 전환 간 최소 간격(짧은 입력 떨림 방지)

    private readonly Dictionary<E_BgmContextType, BgmContextRule> _ruleByContext = new Dictionary<E_BgmContextType, BgmContextRule>(); // 컨텍스트 룰 빠른 조회를 위한 캐시
    private readonly Dictionary<ContextRequestKey, float> _activeContextRequestTimes = new Dictionary<ContextRequestKey, float>(); // 활성 컨텍스트 요청 시각 기록

    private E_BgmContextType _currentContext = E_BgmContextType.None; // 현재 선택된 컨텍스트 상태
    private E_SoundId _currentBgmId = E_SoundId.None; // 현재 Director가 유지 중인 목표 BGM ID
    private int _currentPriority = int.MinValue; // 현재 컨텍스트의 우선순위

    private float _currentHoldUntilTime = 0f; // 현재 컨텍스트 최소 유지가 끝나는 시각
    private float _lastSwitchTime = -999f; // 마지막 BGM 전환 시각

    private bool _isInitialized = false; // 룰 캐시 초기화 완료 여부
    private Coroutine _deferredEvaluateCoroutine; // 최소 전환 간격에 걸렸을 때 재평가를 지연 실행하는 코루틴 핸들입니다.
    private float _deferredEvaluateTargetTime = -1f; // 현재 예약된 지연 재평가 목표 시각입니다.
    private Coroutine _frameCoalescedEvaluateCoroutine; // 같은 프레임의 다중 Push/Pop 요청을 1회 평가로 합치기 위한 코루틴 핸들입니다.
    private bool _frameCoalescedForceBypassInterval; // 프레임 합치기 대기 중 최소 전환 간격 우회가 필요한지 여부입니다.

    private readonly struct ContextRequestKey : IEquatable<ContextRequestKey>
    {
        public readonly E_BgmContextType ContextType; // 활성화를 구분할 컨텍스트 타입
        public readonly int RequesterKey; // 동일 컨텍스트 내 요청자를 구분할 키

        public ContextRequestKey(E_BgmContextType contextType, int requesterKey)
        {
            ContextType = contextType;
            RequesterKey = requesterKey;
        }

        public bool Equals(ContextRequestKey other)
        {
            return ContextType == other.ContextType && RequesterKey == other.RequesterKey;
        }

        public override bool Equals(object obj)
        {
            return obj is ContextRequestKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (int)ContextType;
                hash = (hash * 31) + RequesterKey;
                return hash;
            }
        }
    }

    /// <summary>
    /// 시작 시 룰 캐시를 구성하고 초기 BGM 선택을 평가한다.
    /// </summary>
    private void Awake()
    {
        BuildRuleCache();
        EvaluateAndApplyBestContext(true);
    }

    /// <summary>
    /// Inspector 값이 바뀌면 룰 캐시를 재구성한다.
    /// </summary>
    private void OnValidate()
    {
        if (_fallbackFadeDuration < 0f)
        {
            _fallbackFadeDuration = 0f;
        }

        if (_globalMinSwitchInterval < 0f)
        {
            _globalMinSwitchInterval = 0f;
        }

        BuildRuleCache();
    }

    /// <summary>
    /// 비활성화 시 지연 재평가 코루틴을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        StopDeferredEvaluation();
        StopFrameCoalescedEvaluation();
    }

    /// <summary>
    /// 파괴 시 지연 재평가 코루틴을 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopDeferredEvaluation();
        StopFrameCoalescedEvaluation();
    }

    /// <summary>
    /// 컨텍스트 규칙 목록을 딕셔너리 캐시로 변환한다.
    /// </summary>
    private void BuildRuleCache()
    {
        _ruleByContext.Clear();

        for (int i = 0; i < _contextRules.Count; i++)
        {
            BgmContextRule rule = _contextRules[i]; // 캐시에 등록할 규칙 객체
            if (rule == null)
            {
                Debug.LogWarning($"[BgmDirector] Null rule 발견: index={i}", this);
                continue;
            }

            E_BgmContextType contextType = rule.ContextType; // 규칙이 담당하는 컨텍스트 타입
            if (contextType == E_BgmContextType.None)
            {
                Debug.LogWarning($"[BgmDirector] ContextType None 규칙은 무시됩니다. index={i}", this);
                continue;
            }

            if (_ruleByContext.ContainsKey(contextType))
            {
                Debug.LogWarning($"[BgmDirector] 중복 컨텍스트 규칙이 있어 첫 규칙만 사용합니다. context={contextType}", this);
                continue;
            }

            _ruleByContext.Add(contextType, rule);
        }

        _isInitialized = true;
    }

    /// <summary>
    /// 지정 컨텍스트를 활성화 요청하고 최적 BGM을 재평가한다.
    /// </summary>
    public void PushContext(E_BgmContextType contextType, UnityEngine.Object requester = null)
    {
        if (_isInitialized == false)
        {
            BuildRuleCache();
        }

        if (contextType == E_BgmContextType.None)
        {
            Debug.LogWarning("[BgmDirector] None 컨텍스트 활성화 요청은 무시됩니다.", this);
            return;
        }

        int requesterKey = ResolveRequesterKey(requester); // 요청자 식별을 위한 정수 키
        ContextRequestKey requestKey = new ContextRequestKey(contextType, requesterKey); // 활성 요청을 저장할 딕셔너리 키
        _activeContextRequestTimes[requestKey] = Time.unscaledTime;

        RequestEvaluate(false);
    }

    /// <summary>
    /// 지정 컨텍스트 활성화를 해제하고 최적 BGM을 재평가한다.
    /// </summary>
    public void PopContext(E_BgmContextType contextType, UnityEngine.Object requester = null)
    {
        if (contextType == E_BgmContextType.None)
        {
            return;
        }

        int requesterKey = ResolveRequesterKey(requester); // 해제할 요청자를 식별하는 키
        ContextRequestKey requestKey = new ContextRequestKey(contextType, requesterKey); // 제거 대상 요청 키

        if (_activeContextRequestTimes.Remove(requestKey) == false)
        {
            return;
        }

        RequestEvaluate(false);
    }

    /// <summary>
    /// 지정 요청자에 의해 생성된 모든 컨텍스트 요청을 제거한다.
    /// </summary>
    public void ClearRequester(UnityEngine.Object requester)
    {
        int requesterKey = ResolveRequesterKey(requester); // 제거 대상 요청자 키
        if (requesterKey == 0)
        {
            return;
        }

        List<ContextRequestKey> keysToRemove = new List<ContextRequestKey>(); // 제거할 키를 임시로 수집하는 리스트

        foreach (KeyValuePair<ContextRequestKey, float> pair in _activeContextRequestTimes)
        {
            ContextRequestKey key = pair.Key; // 검사 중인 활성 요청 키
            if (key.RequesterKey == requesterKey)
            {
                keysToRemove.Add(key);
            }
        }

        for (int i = 0; i < keysToRemove.Count; i++)
        {
            _activeContextRequestTimes.Remove(keysToRemove[i]);
        }

        if (keysToRemove.Count > 0)
        {
            RequestEvaluate(false);
        }
    }

    /// <summary>
    /// 같은 프레임에 들어온 다중 컨텍스트 변경 요청을 1회 평가로 합쳐 실행합니다.
    /// </summary>
    private void RequestEvaluate(bool forceBypassInterval)
    {
        _frameCoalescedForceBypassInterval |= forceBypassInterval;

        if (isActiveAndEnabled == false)
        {
            bool shouldBypassInterval = _frameCoalescedForceBypassInterval; // 비활성 상태 즉시 평가에 사용할 간격 우회 플래그입니다.
            _frameCoalescedForceBypassInterval = false;
            EvaluateAndApplyBestContext(shouldBypassInterval);
            return;
        }

        if (_frameCoalescedEvaluateCoroutine != null)
        {
            return;
        }

        _frameCoalescedEvaluateCoroutine = StartCoroutine(CoEvaluateAtEndOfFrame());
    }

    /// <summary>
    /// 프레임 종료 직전에 누적된 컨텍스트 변경을 한 번만 평가합니다.
    /// </summary>
    private IEnumerator CoEvaluateAtEndOfFrame()
    {
        yield return null;

        _frameCoalescedEvaluateCoroutine = null;
        bool shouldBypassInterval = _frameCoalescedForceBypassInterval; // 이번 배치 평가에서 사용할 간격 우회 플래그입니다.
        _frameCoalescedForceBypassInterval = false;
        EvaluateAndApplyBestContext(shouldBypassInterval);
    }

    /// <summary>
    /// 현재 활성 컨텍스트를 기준으로 최적 BGM을 계산하고 필요 시 전환한다.
    /// </summary>
    public void EvaluateAndApplyBestContext(bool forceBypassInterval)
    {
        if (TryResolveAudioManager(out AudioManager audioManager) == false)
        {
            return;
        }

        float now = Time.unscaledTime; // 이번 평가 기준 시각
        if (forceBypassInterval == false)
        {
            if (now - _lastSwitchTime < _globalMinSwitchInterval)
            {
                ScheduleDeferredEvaluation(now);
                return;
            }
        }

        if (TryGetBestActiveRule(out BgmContextRule bestRule) == false)
        {
            ApplyFallback(audioManager, now);
            return;
        }

        E_SoundId targetBgmId = bestRule.SoundId; // 선택된 컨텍스트가 요구하는 목표 BGM ID
        int targetPriority = bestRule.Priority; // 선택된 컨텍스트 우선순위

        bool isSameTrack = _currentBgmId == targetBgmId; // 현재 트랙과 목표 트랙이 동일한지 여부
        bool isTargetTrackPlaying = audioManager.IsBgmPlaying(targetBgmId); // AudioManager 기준으로 목표 트랙이 실제 재생 중인지 여부
        if (isSameTrack)
        {
            if (isTargetTrackPlaying == false)
            {
                Debug.LogWarning($"[BgmDirector] 동일 트랙 컨텍스트이지만 실제 재생이 중단되어 재생을 복구합니다. bgm={targetBgmId}", this);
                audioManager.PlayBgm(targetBgmId);
                _lastSwitchTime = now;
            }

            _currentContext = bestRule.ContextType;
            _currentPriority = targetPriority;
            _currentHoldUntilTime = Mathf.Max(_currentHoldUntilTime, now + bestRule.MinHoldDuration);
            return;
        }

        if (CanInterruptCurrent(targetPriority, now) == false)
        {
            return;
        }

        if (_currentBgmId == E_SoundId.None)
        {
            audioManager.PlayBgm(targetBgmId);
        }
        else
        {
            audioManager.CrossFadeBgm(targetBgmId, bestRule.CrossFadeDuration);
        }

        _currentContext = bestRule.ContextType;
        _currentBgmId = targetBgmId;
        _currentPriority = targetPriority;
        _currentHoldUntilTime = now + bestRule.MinHoldDuration;
        _lastSwitchTime = now;
    }

    /// <summary>
    /// 최소 전환 간격으로 인해 즉시 평가하지 못한 경우, 간격 종료 시점에 재평가를 예약합니다.
    /// </summary>
    private void ScheduleDeferredEvaluation(float now)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        float remainingInterval = _globalMinSwitchInterval - (now - _lastSwitchTime); // 현재 시점에서 남은 최소 전환 대기 시간입니다.
        float delaySeconds = Mathf.Max(0f, remainingInterval); // 음수 방지를 적용한 지연 시간입니다.
        float targetTime = now + delaySeconds; // 예약된 재평가 목표 시각입니다.

        if (_deferredEvaluateCoroutine != null)
        {
            bool hasSoonerOrSameSchedule = targetTime >= _deferredEvaluateTargetTime; // 기존 예약이 더 빠르거나 동일한지 여부입니다.
            if (hasSoonerOrSameSchedule)
            {
                return;
            }

            StopCoroutine(_deferredEvaluateCoroutine);
            _deferredEvaluateCoroutine = null;
        }

        _deferredEvaluateTargetTime = targetTime;
        _deferredEvaluateCoroutine = StartCoroutine(DeferredEvaluateCoroutine(delaySeconds));
    }

    /// <summary>
    /// 지정 시간만큼 대기한 뒤 최소 간격 제한을 우회해 즉시 재평가를 수행합니다.
    /// </summary>
    private IEnumerator DeferredEvaluateCoroutine(float delaySeconds)
    {
        if (delaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
        }

        _deferredEvaluateCoroutine = null;
        _deferredEvaluateTargetTime = -1f;
        EvaluateAndApplyBestContext(true);
    }

    /// <summary>
    /// 예약된 지연 재평가 코루틴을 안전하게 중지하고 상태를 초기화합니다.
    /// </summary>
    private void StopDeferredEvaluation()
    {
        if (_deferredEvaluateCoroutine != null)
        {
            StopCoroutine(_deferredEvaluateCoroutine);
            _deferredEvaluateCoroutine = null;
        }

        _deferredEvaluateTargetTime = -1f;
    }

    /// <summary>
    /// 예약된 프레임 합치기 재평가 코루틴을 중지하고 상태를 초기화합니다.
    /// </summary>
    private void StopFrameCoalescedEvaluation()
    {
        if (_frameCoalescedEvaluateCoroutine != null)
        {
            StopCoroutine(_frameCoalescedEvaluateCoroutine);
            _frameCoalescedEvaluateCoroutine = null;
        }

        _frameCoalescedForceBypassInterval = false;
    }

    /// <summary>
    /// 현재 활성 컨텍스트 중 가장 우선순위가 높은 규칙을 찾는다.
    /// </summary>
    private bool TryGetBestActiveRule(out BgmContextRule bestRule)
    {
        bestRule = null;

        bool hasCandidate = false; // 유효한 후보를 찾았는지 여부
        int bestPriority = int.MinValue; // 현재까지 최고 우선순위
        float latestRequestedTime = float.MinValue; // 우선순위 동률일 때 최신 요청 시각

        foreach (KeyValuePair<ContextRequestKey, float> pair in _activeContextRequestTimes)
        {
            ContextRequestKey requestKey = pair.Key; // 검사할 활성 요청 키
            float requestedTime = pair.Value; // 해당 요청이 활성화된 시각

            if (_ruleByContext.TryGetValue(requestKey.ContextType, out BgmContextRule rule) == false)
            {
                continue;
            }

            int rulePriority = rule.Priority; // 비교 대상 규칙 우선순위
            bool isHigherPriority = rulePriority > bestPriority; // 현재 후보보다 우선순위가 높은지 여부
            bool isSamePriorityButNewer = rulePriority == bestPriority && requestedTime > latestRequestedTime; // 우선순위가 같을 때 더 최신 요청인지 여부

            if (hasCandidate == false || isHigherPriority || isSamePriorityButNewer)
            {
                hasCandidate = true;
                bestRule = rule;
                bestPriority = rulePriority;
                latestRequestedTime = requestedTime;
            }
        }

        return hasCandidate;
    }

    /// <summary>
    /// 활성 요청이 없을 때 폴백 규칙을 적용한다.
    /// </summary>
    private void ApplyFallback(AudioManager audioManager, float now)
    {
        if (_useFallbackBgm == false || _fallbackBgmId == E_SoundId.None)
        {
            if (_currentBgmId != E_SoundId.None)
            {
                if (CanInterruptCurrent(int.MinValue, now))
                {
                    audioManager.FadeOutBgm(_fallbackFadeDuration);
                    _currentContext = E_BgmContextType.None;
                    _currentBgmId = E_SoundId.None;
                    _currentPriority = int.MinValue;
                    _currentHoldUntilTime = now;
                    _lastSwitchTime = now;
                }
            }

            return;
        }

        if (_currentBgmId == _fallbackBgmId)
        {
            if (audioManager.IsBgmPlaying(_fallbackBgmId) == false)
            {
                Debug.LogWarning($"[BgmDirector] 폴백 BGM이 선택 상태지만 재생이 중단되어 재생을 복구합니다. bgm={_fallbackBgmId}", this);
                audioManager.PlayBgm(_fallbackBgmId);
                _lastSwitchTime = now;
            }

            _currentContext = E_BgmContextType.None;
            _currentPriority = int.MinValue;
            return;
        }

        if (CanInterruptCurrent(int.MinValue, now) == false)
        {
            return;
        }

        if (_currentBgmId == E_SoundId.None)
        {
            audioManager.PlayBgm(_fallbackBgmId);
        }
        else
        {
            audioManager.CrossFadeBgm(_fallbackBgmId, _fallbackFadeDuration);
        }

        _currentContext = E_BgmContextType.None;
        _currentBgmId = _fallbackBgmId;
        _currentPriority = int.MinValue;
        _currentHoldUntilTime = now;
        _lastSwitchTime = now;
    }

    /// <summary>
    /// 현재 컨텍스트를 새 우선순위가 끊을 수 있는지 판단한다.
    /// </summary>
    private bool CanInterruptCurrent(int incomingPriority, float now)
    {
        if (_currentBgmId == E_SoundId.None)
        {
            return true;
        }

        bool hasHold = now < _currentHoldUntilTime; // 현재 컨텍스트 최소 유지 시간이 남아있는지 여부
        bool isLowerPriority = incomingPriority < _currentPriority; // 신규 컨텍스트 우선순위가 더 낮은지 여부

        if (hasHold && isLowerPriority)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// requester 오브젝트를 요청자 식별용 정수 키로 변환한다.
    /// </summary>
    private int ResolveRequesterKey(UnityEngine.Object requester)
    {
        if (requester == null)
        {
            return 0;
        }

        return requester.GetInstanceID();
    }

    /// <summary>
    /// 경고 로그를 최소화하기 위해 AudioManager를 안전하게 조회한다.
    /// </summary>
    private bool TryResolveAudioManager(out AudioManager audioManager)
    {
        audioManager = AudioManager.Instance;
        return audioManager != null;
    }
}
