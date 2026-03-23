using System;
using UnityEngine;

public class EffectInstance : MonoBehaviour
{
    private ParticleSystem[] _particleSystems; // 재생/정지 제어 대상 파티클 목록
    private TrailRenderer[] _trailRenderers; // 궤적 리셋/정지 제어 대상 목록
    private Animator[] _animators; // 애니메이터 속도 제어 대상 목록
    private Quaternion _initialLocalRotation; // 프리팹 원본 기준 로컬 회전값

    private int _handleToken; // 서비스에서 발급한 핸들 식별자
    private EffectDefinition _definition; // 현재 인스턴스가 참조하는 정의
    private EffectRequest _request; // 현재 재생 요청 정보
    private Transform _followTarget; // Follow 모드 대상
    private Transform _attachTarget; // Attach 모드 부모 대상
    private GameObject _owner; // 소유자 파괴 감지 대상
    private Vector3 _spawnPosition; // Spawn/OneShot 기준 위치
    private float _elapsed; // 재생 경과 시간
    private bool _isPlaying; // 활성 재생 상태
    private bool _isPaused; // 일시정지 상태
    private Action<EffectInstance> _onReturnRequested; // 반환 요청 콜백

    public int HandleToken => _handleToken;
    public E_EffectId EffectId => _definition == null ? E_EffectId.None : _definition.EffectId;
    public bool IsPlaying => _isPlaying;
    public bool IsPaused => _isPaused;
    public int Priority => _definition == null ? 0 : _definition.Priority;

    /// <summary>
    /// 컴포넌트 캐시를 준비한다.
    /// </summary>
    private void Awake()
    {
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        _trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        _animators = GetComponentsInChildren<Animator>(true);
        _initialLocalRotation = transform.localRotation;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 인스턴스를 요청 정보로 활성화한다.
    /// </summary>
    public void Activate(EffectDefinition definition, EffectRequest request, int handleToken, Action<EffectInstance> onReturnRequested)
    {
        _definition = definition;
        _request = request;
        _handleToken = handleToken;
        _followTarget = request.FollowTarget;
        _attachTarget = request.AttachTarget;
        _owner = request.Owner;
        _spawnPosition = request.Position;
        _elapsed = 0f;
        _isPaused = false;
        _isPlaying = true;
        _onReturnRequested = onReturnRequested;

        ApplyTransformAtStart();
        gameObject.SetActive(true);
        SetActivePlayback(true);
    }

    /// <summary>
    /// 매 프레임 수명/타겟 유실을 점검하고 위치를 갱신한다.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (_isPlaying == false || _isPaused)
        {
            return;
        }

        _elapsed += deltaTime;

        if (_owner == null && _request.Owner != null)
        {
            RequestReturn("owner destroyed");
            return;
        }

        if (_request.PlayMode == E_EffectPlayMode.Follow)
        {
            if (_followTarget == null)
            {
                RequestReturn("follow target lost");
                return;
            }

            transform.position = _followTarget.position + _request.LocalOffset;
        }

        if (_request.PlayMode == E_EffectPlayMode.Attach)
        {
            if (_attachTarget == null)
            {
                RequestReturn("attach target lost");
                return;
            }
        }

        if (ShouldAutoReturn())
        {
            RequestReturn("auto return complete");
        }
    }

    /// <summary>
    /// 수동 정지 요청을 처리한다.
    /// </summary>
    public void Stop()
    {
        if (_isPlaying == false)
        {
            return;
        }

        RequestReturn("manual stop");
    }

    /// <summary>
    /// 재생을 일시정지한다.
    /// </summary>
    public void Pause()
    {
        if (_isPlaying == false || _isPaused)
        {
            return;
        }

        _isPaused = true;
        SetActivePlayback(false);
    }

    /// <summary>
    /// 일시정지된 재생을 재개한다.
    /// </summary>
    public void Resume()
    {
        if (_isPlaying == false || _isPaused == false)
        {
            return;
        }

        _isPaused = false;
        SetActivePlayback(true);
    }

    /// <summary>
    /// Follow 타겟을 런타임에 교체한다.
    /// </summary>
    public void SetFollowTarget(Transform followTarget)
    {
        _followTarget = followTarget;
    }

    /// <summary>
    /// 위치를 강제로 설정한다.
    /// </summary>
    public void SetPosition(Vector3 position)
    {
        _spawnPosition = position;
        transform.position = position;
    }

    /// <summary>
    /// 풀 반환 전에 내부 상태를 정리한다.
    /// </summary>
    public void DeactivateForPool()
    {
        _isPlaying = false;
        _isPaused = false;
        _elapsed = 0f;
        _followTarget = null;
        _attachTarget = null;
        _owner = null;
        _request = default;
        _definition = null;
        _onReturnRequested = null;

        StopAndClearImmediate();
        transform.SetParent(null);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 재생 시작 시 Transform 배치를 설정한다.
    /// </summary>
    private void ApplyTransformAtStart()
    {
        Vector3 offset = _definition == null ? Vector3.zero : _definition.DefaultLocalOffset;
        offset += _request.LocalOffset;

        if (_request.PlayMode == E_EffectPlayMode.Attach && _attachTarget != null)
        {
            transform.SetParent(_attachTarget, false);
            transform.localPosition = offset;
            ApplyFacingAtStart();
            return;
        }

        transform.SetParent(null);

        if (_request.PlayMode == E_EffectPlayMode.Follow && _followTarget != null)
        {
            transform.position = _followTarget.position + offset;
            ApplyFacingAtStart();
            return;
        }

        transform.position = _spawnPosition + offset;
        ApplyFacingAtStart();
    }

    /// <summary>
    /// 요청된 좌/우 방향에 맞춰 시작 회전을 설정한다.
    /// </summary>
    private void ApplyFacingAtStart()
    {
        if (_request.FacingDirection == E_EffectFacingDirection.Left)
        {
            transform.localRotation = _initialLocalRotation * Quaternion.Euler(0f, 180f, 0f);
            return;
        }

        if (_request.FacingDirection == E_EffectFacingDirection.Right)
        {
            transform.localRotation = _initialLocalRotation;
            return;
        }

        transform.localRotation = _initialLocalRotation;
    }

    /// <summary>
    /// 파티클/트레일/애니메이터 재생 상태를 전환한다.
    /// </summary>
    private void SetActivePlayback(bool shouldPlay)
    {
        for (int i = 0; i < _particleSystems.Length; i++)
        {
            if (shouldPlay)
            {
                _particleSystems[i].Play(true);
            }
            else
            {
                _particleSystems[i].Pause(true);
            }
        }

        for (int i = 0; i < _trailRenderers.Length; i++)
        {
            _trailRenderers[i].emitting = shouldPlay;
        }

        for (int i = 0; i < _animators.Length; i++)
        {
            _animators[i].speed = shouldPlay ? 1f : 0f;
        }
    }

    /// <summary>
    /// 자동 반환 시점을 판단한다.
    /// </summary>
    private bool ShouldAutoReturn()
    {
        if (_definition == null)
        {
            return true;
        }

        if (_request.PlayMode == E_EffectPlayMode.Loop)
        {
            return false;
        }

        if (_request.AutoReturnOverrideEnabled)
        {
            return _request.AutoReturn && _elapsed >= GetLifetimeLimit();
        }

        if (_definition.AutoReturn == false)
        {
            return false;
        }

        return _elapsed >= GetLifetimeLimit();
    }

    /// <summary>
    /// 요청/정의 기반으로 생존 시간 제한을 계산한다.
    /// </summary>
    private float GetLifetimeLimit()
    {
        if (_request.LifetimeOverride > 0f)
        {
            return _request.LifetimeOverride;
        }

        return Mathf.Max(0.1f, _definition.MaxLifetime);
    }

    /// <summary>
    /// 반환 콜백을 호출한다.
    /// </summary>
    private void RequestReturn(string reason)
    {
        if (_isPlaying == false)
        {
            return;
        }

        _isPlaying = false;
        _onReturnRequested?.Invoke(this);
    }

    /// <summary>
    /// 즉시 정지와 트레일 클리어를 수행한다.
    /// </summary>
    private void StopAndClearImmediate()
    {
        for (int i = 0; i < _particleSystems.Length; i++)
        {
            _particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        for (int i = 0; i < _trailRenderers.Length; i++)
        {
            _trailRenderers[i].Clear();
            _trailRenderers[i].emitting = false;
        }

        for (int i = 0; i < _animators.Length; i++)
        {
            _animators[i].Rebind();
            _animators[i].Update(0f);
        }
    }
}
