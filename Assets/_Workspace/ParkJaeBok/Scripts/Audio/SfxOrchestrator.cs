using System;
using System.Collections.Generic;
using UnityEngine;

public class SfxOrchestrator : MonoBehaviour
{
    [Header("Routing Rules")]
    [SerializeField]
    private List<SfxEventRule> _rules = new List<SfxEventRule>(); // 이벤트 타입/서브 타입을 실제 SoundId로 매핑하는 규칙 목록

    [Header("Fallback")]
    [SerializeField]
    private bool _useFallbackOnMissingRule = false; // 룰을 찾지 못했을 때 폴백 SoundId 사용 여부

    [SerializeField]
    private E_SoundId _fallbackSoundId = E_SoundId.None; // 매핑 실패 시 재생할 폴백 사운드 ID

    [Header("Debug")]
    [SerializeField]
    private bool _enableDebugLog = false; // 라우팅/드롭 사유를 콘솔에 출력할지 여부

    private readonly Dictionary<RouteKey, SfxEventRule> _exactRuleByKey = new Dictionary<RouteKey, SfxEventRule>(); // EventType+SubType 완전 일치 룰 캐시
    private readonly Dictionary<E_SfxEventType, SfxEventRule> _typeOnlyRuleByEvent = new Dictionary<E_SfxEventType, SfxEventRule>(); // EventType 단독 매칭 룰 캐시

    private readonly Dictionary<RouteKey, float> _nextGlobalPlayableByRoute = new Dictionary<RouteKey, float>(); // 룰 단위 글로벌 다음 재생 가능 시각
    private readonly Dictionary<EmitterRouteKey, float> _nextEmitterPlayableByRoute = new Dictionary<EmitterRouteKey, float>(); // 룰+Emitter 단위 다음 재생 가능 시각
    private readonly Dictionary<RouteKey, Queue<float>> _recentRequestTimesByRoute = new Dictionary<RouteKey, Queue<float>>(); // 룰별 최근 1초 요청 시각 기록 큐

    private bool _isCacheBuilt = false; // 룰 캐시 구성 완료 여부

    private readonly struct RouteKey : IEquatable<RouteKey>
    {
        public readonly E_SfxEventType EventType; // 라우팅 대상 이벤트 타입
        public readonly string SubTypeKey; // 라우팅 대상 서브 타입 키(소문자 정규화)

        public RouteKey(E_SfxEventType eventType, string subTypeKey)
        {
            EventType = eventType;
            SubTypeKey = subTypeKey ?? string.Empty;
        }

        public bool Equals(RouteKey other)
        {
            return EventType == other.EventType && string.Equals(SubTypeKey, other.SubTypeKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is RouteKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (int)EventType;
                hash = (hash * 31) + (SubTypeKey == null ? 0 : SubTypeKey.GetHashCode());
                return hash;
            }
        }
    }

    private readonly struct EmitterRouteKey : IEquatable<EmitterRouteKey>
    {
        public readonly RouteKey Route; // 이벤트 라우팅 정보를 담는 키
        public readonly int EmitterKey; // 동일 라우트 내 발신자를 구분하는 정수 키

        public EmitterRouteKey(RouteKey route, int emitterKey)
        {
            Route = route;
            EmitterKey = emitterKey;
        }

        public bool Equals(EmitterRouteKey other)
        {
            return Route.Equals(other.Route) && EmitterKey == other.EmitterKey;
        }

        public override bool Equals(object obj)
        {
            return obj is EmitterRouteKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Route.GetHashCode();
                hash = (hash * 31) + EmitterKey;
                return hash;
            }
        }
    }

    /// <summary>
    /// 시작 시 룰 캐시를 구성해 런타임 라우팅 비용을 줄인다.
    /// </summary>
    private void Awake()
    {
        BuildRuleCache();
    }

    /// <summary>
    /// Inspector 값 변경 시 룰 캐시를 재구성하고 잘못된 규칙을 경고한다.
    /// </summary>
    private void OnValidate()
    {
        BuildRuleCache();
    }

    /// <summary>
    /// 이벤트 타입만으로 SFX 재생을 요청한다.
    /// </summary>
    public bool Request(E_SfxEventType eventType, Transform emitter = null)
    {
        return Request(eventType, string.Empty, emitter, null);
    }

    /// <summary>
    /// 이벤트 타입+서브 타입 기반으로 SFX 재생을 요청한다.
    /// </summary>
    public bool Request(E_SfxEventType eventType, string subTypeKey, Transform emitter = null)
    {
        return Request(eventType, subTypeKey, emitter, null);
    }

    /// <summary>
    /// 이벤트 타입+서브 타입+명시 좌표 기반으로 SFX 재생을 요청한다.
    /// </summary>
    public bool Request(E_SfxEventType eventType, string subTypeKey, Transform emitter, Vector3? worldPositionOverride)
    {
        if (_isCacheBuilt == false)
        {
            BuildRuleCache();
        }

        if (eventType == E_SfxEventType.None)
        {
            LogDebug("[SfxOrchestrator] EventType None 요청은 무시됩니다.");
            return false;
        }

        RouteKey routeKey = new RouteKey(eventType, NormalizeSubTypeKey(subTypeKey));
        if (TryResolveRule(routeKey, out SfxEventRule rule) == false)
        {
            if (_useFallbackOnMissingRule == false || _fallbackSoundId == E_SoundId.None)
            {
                LogDebug($"[SfxOrchestrator] 룰을 찾지 못해 요청을 무시합니다. eventType={eventType}, subType={routeKey.SubTypeKey}");
                return false;
            }

            return PlayFallback(emitter, worldPositionOverride, eventType, routeKey.SubTypeKey);
        }

        if (CanPassThrottle(routeKey, rule, emitter) == false)
        {
            return false;
        }

        return PlayByRule(rule, emitter, worldPositionOverride);
    }

    /// <summary>
    /// 현재 룰 목록으로 캐시를 재구성한다.
    /// </summary>
    public void RebuildCache()
    {
        BuildRuleCache();
    }

    /// <summary>
    /// 룰 리스트를 검증하고 라우팅용 캐시 딕셔너리를 구성한다.
    /// </summary>
    private void BuildRuleCache()
    {
        _exactRuleByKey.Clear();
        _typeOnlyRuleByEvent.Clear();

        for (int i = 0; i < _rules.Count; i++)
        {
            SfxEventRule rule = _rules[i]; // 캐시에 등록할 룰 객체
            if (rule == null)
            {
                Debug.LogWarning($"[SfxOrchestrator] Null rule 발견: index={i}", this);
                continue;
            }

            if (rule.EventType == E_SfxEventType.None)
            {
                Debug.LogWarning($"[SfxOrchestrator] EventType None 룰은 무시됩니다. index={i}", this);
                continue;
            }

            if (rule.SoundId == E_SoundId.None)
            {
                Debug.LogWarning($"[SfxOrchestrator] SoundId None 룰은 무시됩니다. eventType={rule.EventType}, index={i}", this);
                continue;
            }

            RouteKey routeKey = new RouteKey(rule.EventType, rule.GetNormalizedSubTypeKey()); // 룰의 정규화된 캐시 키

            if (string.IsNullOrEmpty(routeKey.SubTypeKey))
            {
                if (_typeOnlyRuleByEvent.ContainsKey(routeKey.EventType))
                {
                    Debug.LogWarning($"[SfxOrchestrator] EventType 단독 룰이 중복되어 첫 항목만 사용합니다. eventType={routeKey.EventType}", this);
                    continue;
                }

                _typeOnlyRuleByEvent.Add(routeKey.EventType, rule);
                continue;
            }

            if (_exactRuleByKey.ContainsKey(routeKey))
            {
                Debug.LogWarning($"[SfxOrchestrator] EventType+SubType 룰이 중복되어 첫 항목만 사용합니다. eventType={routeKey.EventType}, subType={routeKey.SubTypeKey}", this);
                continue;
            }

            _exactRuleByKey.Add(routeKey, rule);
        }

        _isCacheBuilt = true;
    }

    /// <summary>
    /// 라우팅 키를 기준으로 정확 매칭 또는 타입 단독 매칭 룰을 찾는다.
    /// </summary>
    private bool TryResolveRule(RouteKey routeKey, out SfxEventRule rule)
    {
        if (_exactRuleByKey.TryGetValue(routeKey, out rule))
        {
            return true;
        }

        return _typeOnlyRuleByEvent.TryGetValue(routeKey.EventType, out rule);
    }

    /// <summary>
    /// 글로벌/Emitter 단위 간격 및 초당 요청 제한 정책을 통과하는지 검사한다.
    /// </summary>
    private bool CanPassThrottle(RouteKey routeKey, SfxEventRule rule, Transform emitter)
    {
        float now = Time.unscaledTime; // 이번 요청 판정 기준 시각

        if (rule.GlobalMinInterval > 0f)
        {
            if (_nextGlobalPlayableByRoute.TryGetValue(routeKey, out float nextGlobalPlayableTime) && now < nextGlobalPlayableTime)
            {
                LogDebug($"[SfxOrchestrator] 글로벌 간격 제한으로 드롭. eventType={routeKey.EventType}, subType={routeKey.SubTypeKey}");
                return false;
            }

            _nextGlobalPlayableByRoute[routeKey] = now + rule.GlobalMinInterval;
        }

        if (rule.PerEmitterMinInterval > 0f)
        {
            int emitterKey = ResolveEmitterKey(emitter); // Emitter 단위 제한에 사용할 식별 키
            if (emitterKey != 0)
            {
                EmitterRouteKey emitterRouteKey = new EmitterRouteKey(routeKey, emitterKey); // 룰+Emitter 조합 제한 키
                if (_nextEmitterPlayableByRoute.TryGetValue(emitterRouteKey, out float nextEmitterPlayableTime) && now < nextEmitterPlayableTime)
                {
                    LogDebug($"[SfxOrchestrator] Emitter 간격 제한으로 드롭. eventType={routeKey.EventType}, subType={routeKey.SubTypeKey}, emitter={emitter.name}");
                    return false;
                }

                _nextEmitterPlayableByRoute[emitterRouteKey] = now + rule.PerEmitterMinInterval;
            }
        }

        if (rule.MaxRequestsPerSecond > 0)
        {
            if (_recentRequestTimesByRoute.TryGetValue(routeKey, out Queue<float> requestTimes) == false)
            {
                requestTimes = new Queue<float>();
                _recentRequestTimesByRoute.Add(routeKey, requestTimes);
            }

            while (requestTimes.Count > 0 && now - requestTimes.Peek() > 1f)
            {
                requestTimes.Dequeue();
            }

            if (requestTimes.Count >= rule.MaxRequestsPerSecond)
            {
                LogDebug($"[SfxOrchestrator] 초당 요청 수 제한으로 드롭. eventType={routeKey.EventType}, subType={routeKey.SubTypeKey}");
                return false;
            }

            requestTimes.Enqueue(now);
        }

        return true;
    }

    /// <summary>
    /// 룰에서 지정한 SoundId를 AudioManager로 전달해 재생한다.
    /// </summary>
    private bool PlayByRule(SfxEventRule rule, Transform emitter, Vector3? worldPositionOverride)
    {
        AudioManager audioManager = AudioManager.Instance; // 최종 재생 요청을 전달할 오디오 매니저 인스턴스
        if (audioManager == null)
        {
            Debug.LogWarning("[SfxOrchestrator] AudioManager를 찾지 못해 요청을 무시합니다.", this);
            return false;
        }

        if (worldPositionOverride.HasValue)
        {
            audioManager.PlaySfx(rule.SoundId, worldPositionOverride.Value);
            return true;
        }

        if (emitter != null)
        {
            audioManager.PlaySfx(rule.SoundId, emitter);
            return true;
        }

        audioManager.PlaySfx(rule.SoundId);
        return true;
    }

    /// <summary>
    /// 매핑 실패 시 폴백 SoundId를 AudioManager로 전달해 재생한다.
    /// </summary>
    private bool PlayFallback(Transform emitter, Vector3? worldPositionOverride, E_SfxEventType eventType, string subTypeKey)
    {
        AudioManager audioManager = AudioManager.Instance; // 폴백 재생 요청을 전달할 오디오 매니저 인스턴스
        if (audioManager == null)
        {
            Debug.LogWarning("[SfxOrchestrator] AudioManager를 찾지 못해 폴백 요청을 무시합니다.", this);
            return false;
        }

        if (worldPositionOverride.HasValue)
        {
            audioManager.PlaySfx(_fallbackSoundId, worldPositionOverride.Value);
            LogDebug($"[SfxOrchestrator] 폴백 재생(좌표). eventType={eventType}, subType={subTypeKey}, soundId={_fallbackSoundId}");
            return true;
        }

        if (emitter != null)
        {
            audioManager.PlaySfx(_fallbackSoundId, emitter);
            LogDebug($"[SfxOrchestrator] 폴백 재생(Emitter). eventType={eventType}, subType={subTypeKey}, soundId={_fallbackSoundId}");
            return true;
        }

        audioManager.PlaySfx(_fallbackSoundId);
        LogDebug($"[SfxOrchestrator] 폴백 재생(기본 위치). eventType={eventType}, subType={subTypeKey}, soundId={_fallbackSoundId}");
        return true;
    }

    /// <summary>
    /// 서브 타입 문자열을 공백 제거 + 소문자 형태로 정규화한다.
    /// </summary>
    private string NormalizeSubTypeKey(string subTypeKey)
    {
        if (string.IsNullOrWhiteSpace(subTypeKey))
        {
            return string.Empty;
        }

        return subTypeKey.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Emitter 제한 정책에서 사용할 정수 키를 생성한다.
    /// </summary>
    private int ResolveEmitterKey(Transform emitter)
    {
        if (emitter == null)
        {
            return 0;
        }

        return emitter.GetInstanceID();
    }

    /// <summary>
    /// 디버그 로그 옵션이 켜졌을 때만 메시지를 출력한다.
    /// </summary>
    private void LogDebug(string message)
    {
        if (_enableDebugLog == false)
        {
            return;
        }

        Debug.Log(message, this);
    }
}
