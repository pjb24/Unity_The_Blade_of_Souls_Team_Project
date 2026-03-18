using System;
using System.Collections.Generic;
using UnityEngine;

public class EffectService : MonoBehaviour
{
    private struct DuplicateKey
    {
        public int OwnerId; // 중복 제한에 사용할 오너 InstanceId
        public E_EffectId EffectId; // 중복 제한에 사용할 이펙트 ID
    }

    [SerializeField]
    private EffectCatalog _catalog; // EffectId -> 정의 조회 카탈로그

    [SerializeField]
    private bool _dontDestroyOnLoad = true; // 씬 전환 시 서비스 유지 여부

    private static EffectService _instance; // 전역 접근용 싱글톤 인스턴스

    private readonly Dictionary<E_EffectId, EffectPool> _poolById = new Dictionary<E_EffectId, EffectPool>(); // 이펙트별 풀 매핑
    private readonly Dictionary<int, EffectInstance> _instanceByToken = new Dictionary<int, EffectInstance>(); // 핸들 토큰 -> 인스턴스 매핑
    private readonly Dictionary<int, EffectHandle> _handleByToken = new Dictionary<int, EffectHandle>(); // 핸들 토큰 -> 핸들 객체 매핑
    private readonly Dictionary<DuplicateKey, int> _duplicateTokenByKey = new Dictionary<DuplicateKey, int>(); // 중복 요청 제어용 매핑

    private int _nextToken = 1; // 다음 핸들 토큰 값

    private Action<EffectRequest> _onEffectPlayed; // 디버그 모니터용 재생 알림
    private Action<EffectRequest, string> _onEffectPlayFailed; // 디버그 모니터용 실패 알림

    public static EffectService Instance => _instance;

    /// <summary>
    /// 재생 성공 리스너를 등록한다.
    /// </summary>
    public void AddPlayListener(Action<EffectRequest> listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[EffectService] AddPlayListener received null listener.", this);
            return;
        }

        _onEffectPlayed += listener;
    }

    /// <summary>
    /// 재생 성공 리스너를 해제한다.
    /// </summary>
    public void RemovePlayListener(Action<EffectRequest> listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[EffectService] RemovePlayListener received null listener.", this);
            return;
        }

        _onEffectPlayed -= listener;
    }

    /// <summary>
    /// 재생 실패 리스너를 등록한다.
    /// </summary>
    public void AddPlayFailListener(Action<EffectRequest, string> listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[EffectService] AddPlayFailListener received null listener.", this);
            return;
        }

        _onEffectPlayFailed += listener;
    }

    /// <summary>
    /// 재생 실패 리스너를 해제한다.
    /// </summary>
    public void RemovePlayFailListener(Action<EffectRequest, string> listener)
    {
        if (listener == null)
        {
            Debug.LogWarning("[EffectService] RemovePlayFailListener received null listener.", this);
            return;
        }

        _onEffectPlayFailed -= listener;
    }

    /// <summary>
    /// 서비스 초기화를 수행한다.
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[EffectService] 중복 EffectService가 감지되어 새 인스턴스를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (_catalog == null)
        {
            Debug.LogWarning("[EffectService] EffectCatalog가 비어 있습니다. Play 요청이 실패합니다.", this);
            return;
        }

        _catalog.Initialize();
    }

    /// <summary>
    /// 활성 인스턴스를 매 프레임 갱신한다.
    /// </summary>
    private void Update()
    {
        if (_instanceByToken.Count == 0)
        {
            return;
        }

        List<int> tokenBuffer = new List<int>(_instanceByToken.Keys);
        for (int i = 0; i < tokenBuffer.Count; i++)
        {
            int token = tokenBuffer[i];
            if (_instanceByToken.TryGetValue(token, out EffectInstance instance) == false)
            {
                continue;
            }

            instance.Tick(Time.deltaTime);
        }
    }

    /// <summary>
    /// 서비스 파괴 시 모든 활성 이펙트를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        CleanupAllActiveEffects();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 간단 호출 API. OneShot Spawn 재생에 사용한다.
    /// </summary>
    public EffectHandle Play(E_EffectId effectId, Vector3 position)
    {
        EffectRequest request = EffectRequest.CreateSimple(effectId, position);
        return Play(request);
    }

    /// <summary>
    /// 확장 호출 API. 요청 구조체 기반 재생을 수행한다.
    /// </summary>
    public EffectHandle Play(EffectRequest request)
    {
        if (_catalog == null)
        {
            WarnPlayFailure(request, "catalog is null");
            return new EffectHandle(this, 0);
        }

        if (_catalog.TryGetDefinition(request.EffectId, out EffectDefinition definition) == false)
        {
            WarnPlayFailure(request, "definition not found");
            return new EffectHandle(this, 0);
        }

        if (definition.Prefab == null)
        {
            WarnPlayFailure(request, "prefab missing");
            return new EffectHandle(this, 0);
        }

        NormalizeRequestByDefinition(ref request, definition);

        if (CanPlayByDuplicateRule(request, definition) == false)
        {
            WarnPlayFailure(request, "duplicate blocked");
            return new EffectHandle(this, 0);
        }

        EffectPool pool = GetOrCreatePool(request.EffectId, definition);
        EffectInstance instance = pool.AcquireOrNull(out bool usedFallback);

        if (usedFallback)
        {
            Debug.LogWarning($"[EffectService] 풀 부족 폴백 실행. id={request.EffectId}, policy={definition.FallbackPolicy}", this);
        }

        if (instance == null)
        {
            WarnPlayFailure(request, "pool acquire dropped by fallback policy");
            return new EffectHandle(this, 0);
        }

        int token = _nextToken++;
        instance.Activate(definition, request, token, ReturnInstanceToPool);

        EffectHandle handle = new EffectHandle(this, token);
        _instanceByToken[token] = instance;
        _handleByToken[token] = handle;

        RegisterDuplicateKeyIfNeeded(request, definition, token);

        _onEffectPlayed?.Invoke(request);
        return handle;
    }

    /// <summary>
    /// 토큰이 유효한 활성 인스턴스를 가리키는지 확인한다.
    /// </summary>
    public bool IsHandleValid(int token)
    {
        return token > 0 && _instanceByToken.ContainsKey(token);
    }

    /// <summary>
    /// 토큰으로 인스턴스를 정지시킨다.
    /// </summary>
    public void StopByHandle(int token)
    {
        if (_instanceByToken.TryGetValue(token, out EffectInstance instance) == false)
        {
            return;
        }

        instance.Stop();
    }

    /// <summary>
    /// 토큰으로 인스턴스를 일시정지한다.
    /// </summary>
    public void PauseByHandle(int token)
    {
        if (_instanceByToken.TryGetValue(token, out EffectInstance instance) == false)
        {
            return;
        }

        instance.Pause();
    }

    /// <summary>
    /// 토큰으로 인스턴스를 재개한다.
    /// </summary>
    public void ResumeByHandle(int token)
    {
        if (_instanceByToken.TryGetValue(token, out EffectInstance instance) == false)
        {
            return;
        }

        instance.Resume();
    }

    /// <summary>
    /// 토큰으로 Follow 대상을 변경한다.
    /// </summary>
    public void SetFollowTargetByHandle(int token, Transform followTarget)
    {
        if (_instanceByToken.TryGetValue(token, out EffectInstance instance) == false)
        {
            return;
        }

        instance.SetFollowTarget(followTarget);
    }

    /// <summary>
    /// 토큰으로 월드 좌표를 강제로 설정한다.
    /// </summary>
    public void SetPositionByHandle(int token, Vector3 position)
    {
        if (_instanceByToken.TryGetValue(token, out EffectInstance instance) == false)
        {
            return;
        }

        instance.SetPosition(position);
    }

    /// <summary>
    /// 씬 정리 또는 강제 리셋 시 모든 활성 이펙트를 정리한다.
    /// </summary>
    public void CleanupAllActiveEffects()
    {
        List<int> tokenBuffer = new List<int>(_instanceByToken.Keys);
        for (int i = 0; i < tokenBuffer.Count; i++)
        {
            int token = tokenBuffer[i];
            if (_instanceByToken.TryGetValue(token, out EffectInstance instance) == false)
            {
                continue;
            }

            ReturnInstanceToPool(instance);
        }
    }

    /// <summary>
    /// 정의 기본값을 바탕으로 요청값을 보정한다.
    /// </summary>
    private void NormalizeRequestByDefinition(ref EffectRequest request, EffectDefinition definition)
    {
        if (request.PlayMode == E_EffectPlayMode.OneShot && definition.DefaultPlayMode != E_EffectPlayMode.OneShot)
        {
            request.PlayMode = definition.DefaultPlayMode;
        }

        if (request.PlayMode == E_EffectPlayMode.Follow && definition.AllowFollow == false)
        {
            request.PlayMode = E_EffectPlayMode.Spawn;
            Debug.LogWarning($"[EffectService] Follow 비허용 정의로 요청되어 Spawn으로 폴백되었습니다. id={request.EffectId}", this);
        }

        if (request.PlayMode == E_EffectPlayMode.Attach && definition.AllowAttach == false)
        {
            request.PlayMode = E_EffectPlayMode.Spawn;
            Debug.LogWarning($"[EffectService] Attach 비허용 정의로 요청되어 Spawn으로 폴백되었습니다. id={request.EffectId}", this);
        }

        if (request.PlayMode == E_EffectPlayMode.Spawn)
        {
            request.FollowTarget = null;
            request.AttachTarget = null;
        }

        if (request.PlayMode == E_EffectPlayMode.OneShot)
        {
            request.AutoReturnOverrideEnabled = true;
            request.AutoReturn = true;
        }
    }

    /// <summary>
    /// 중복 재생 제한 규칙을 검사한다.
    /// </summary>
    private bool CanPlayByDuplicateRule(EffectRequest request, EffectDefinition definition)
    {
        if (request.IgnoreDuplicateGuard)
        {
            return true;
        }

        if (definition.AllowDuplicatePlay)
        {
            return true;
        }

        if (request.Owner == null)
        {
            return true;
        }

        DuplicateKey key = new DuplicateKey { OwnerId = request.Owner.GetInstanceID(), EffectId = request.EffectId };
        return _duplicateTokenByKey.ContainsKey(key) == false;
    }

    /// <summary>
    /// 중복 재생 제한용 토큰 매핑을 등록한다.
    /// </summary>
    private void RegisterDuplicateKeyIfNeeded(EffectRequest request, EffectDefinition definition, int token)
    {
        if (definition.AllowDuplicatePlay)
        {
            return;
        }

        if (request.Owner == null)
        {
            return;
        }

        DuplicateKey key = new DuplicateKey { OwnerId = request.Owner.GetInstanceID(), EffectId = request.EffectId };
        _duplicateTokenByKey[key] = token;
    }

    /// <summary>
    /// 인스턴스를 풀에 반환하고 핸들/매핑을 정리한다.
    /// </summary>
    private void ReturnInstanceToPool(EffectInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        int token = instance.HandleToken;
        E_EffectId effectId = instance.EffectId;

        instance.DeactivateForPool();

        if (_poolById.TryGetValue(effectId, out EffectPool pool))
        {
            pool.Release(instance);
        }

        _instanceByToken.Remove(token);

        if (_handleByToken.TryGetValue(token, out EffectHandle handle))
        {
            handle.Invalidate();
            _handleByToken.Remove(token);
        }

        RemoveDuplicateByToken(token);
    }

    /// <summary>
    /// 토큰으로 등록된 중복 제어 키를 제거한다.
    /// </summary>
    private void RemoveDuplicateByToken(int token)
    {
        DuplicateKey foundKey = default;
        bool found = false;

        foreach (KeyValuePair<DuplicateKey, int> pair in _duplicateTokenByKey)
        {
            if (pair.Value != token)
            {
                continue;
            }

            foundKey = pair.Key;
            found = true;
            break;
        }

        if (found)
        {
            _duplicateTokenByKey.Remove(foundKey);
        }
    }

    /// <summary>
    /// 필요 시 풀을 생성하고 반환한다.
    /// </summary>
    private EffectPool GetOrCreatePool(E_EffectId effectId, EffectDefinition definition)
    {
        if (_poolById.TryGetValue(effectId, out EffectPool pool))
        {
            return pool;
        }

        pool = new EffectPool(definition, transform);
        _poolById.Add(effectId, pool);
        return pool;
    }

    /// <summary>
    /// 실패 로그와 실패 리스너 콜백을 함께 전파한다.
    /// </summary>
    private void WarnPlayFailure(EffectRequest request, string reason)
    {
        Debug.LogWarning($"[EffectService] Play 실패. id={request.EffectId}, reason={reason}", this);
        _onEffectPlayFailed?.Invoke(request, reason);
    }
}
