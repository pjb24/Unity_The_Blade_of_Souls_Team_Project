using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D 직선 이동/트리거 충돌 기반으로 단일 히트를 처리하는 원거리 투사체입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PooledRangedProjectile : MonoBehaviour
{
    private const string GroundLayerName = "Ground"; // 벽/바닥 판정에 사용할 기본 레이어 이름입니다.

    [Header("Damage")]
    [Tooltip("Player에게 전달할 기본 피해량입니다.")]
    [SerializeField] private float _damage = 10f; // 피격 요청 시 전달할 기본 피해량입니다.
    [Tooltip("HitRequest 상태 태그 문자열입니다.")]
    [SerializeField] private string _statusTag = "EnemyProjectile"; // HitRequest에 기록할 상태 태그입니다.
    [Tooltip("피격 가능한 대상 레이어 마스크입니다.")]
    [SerializeField] private LayerMask _targetLayerMask = 0; // 데미지 판정을 시도할 레이어 마스크입니다.
    [Tooltip("비어 있지 않으면 지정 태그와 일치하는 대상만 공격합니다.")]
    [SerializeField] private string _targetTag = "Player"; // 공격 대상 태그 필터입니다.
    [Tooltip("비어 있지 않으면 지정 태그와 일치하는 대상을 무시합니다.")]
    [SerializeField] private string _ignoreTag = "Enemy"; // 아군/동일 진영 무시용 태그 필터입니다.
    [Tooltip("지형/벽 등 충돌 시 소멸할 환경 레이어 마스크입니다.")]
    [SerializeField] private LayerMask _environmentLayerMask = 0; // 환경 충돌 소멸용 레이어 마스크입니다.
    [Tooltip("대상 명중 후 즉시 소멸할지 여부입니다.")]
    [SerializeField] private bool _despawnOnHit = true; // 명중 시 1회 타격 후 소멸 정책입니다.
    [Tooltip("명중/환경 충돌 시 1회 재생할 히트 이펙트 프리팹입니다.")]
    [SerializeField] private GameObject _hitEffectPrefab; // 충돌 지점 이펙트 프리팹입니다.

    [Tooltip("현재 투사체 소유자입니다.")]
    private GameObject _owner; // 소유자 참조입니다.
    [Tooltip("발사 시 계산된 고정 이동 방향입니다.")]
    private Vector2 _direction; // 발사 순간 계산된 이동 방향입니다.
    [Tooltip("이동 속도입니다.")]
    private float _speed; // 초당 이동 거리입니다.
    [Tooltip("투사체 수명(초)입니다.")]
    private float _lifetime; // 자동 소멸까지 유지 시간입니다.
    [Tooltip("수명 종료 시각입니다.")]
    private float _expireAt; // 수명 종료 절대 시각입니다.
    [Tooltip("Initialize 완료 여부입니다.")]
    private bool _isInitialized; // 초기화 완료 플래그입니다.
    [Tooltip("이미 히트 처리한 대상 인스턴스 ID 집합입니다.")]
    private readonly HashSet<int> _hitTargetIds = new HashSet<int>(); // 동일 대상 다중 타격 방지용 집합입니다.

    [Tooltip("물리 이동을 수행하는 Rigidbody2D 참조입니다.")]
    private Rigidbody2D _rigidbody2D; // 투사체 물리 이동용 Rigidbody2D입니다.
    [Tooltip("트리거 충돌을 수신하는 Collider2D 참조입니다.")]
    private Collider2D _collider2D; // 트리거 충돌용 Collider2D입니다.
    [Tooltip("풀 반환 콜백입니다.")]
    private Action<PooledRangedProjectile> _returnHandler; // 서비스로 반환할 콜백입니다.

    /// <summary>
    /// 필수 컴포넌트/기본 설정을 보정합니다.
    /// </summary>
    private void Awake()
    {
        ResolveDefaultEnvironmentLayerMask();

        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();

        _collider2D.isTrigger = true;
        _rigidbody2D.gravityScale = 0f;
        _rigidbody2D.freezeRotation = true;
        _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
    }

    /// <summary>
    /// 에디터 값 변경 시 기본 환경 레이어 설정을 검증합니다.
    /// </summary>
    private void OnValidate()
    {
        ResolveDefaultEnvironmentLayerMask();
    }

    /// <summary>
    /// 비활성화 시 런타임 상태를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        _isInitialized = false;
        _owner = null;
        _rigidbody2D.linearVelocity = Vector2.zero;
        _hitTargetIds.Clear();
    }

    /// <summary>
    /// 풀 반환 핸들러를 바인딩합니다.
    /// </summary>
    public void BindReturnHandler(Action<PooledRangedProjectile> returnHandler)
    {
        _returnHandler = returnHandler;
    }

    /// <summary>
    /// 발사 파라미터를 설정하고 투사체를 활성 런타임 상태로 초기화합니다.
    /// </summary>
    public void Initialize(Vector2 direction, float speed, float lifetime, GameObject owner)
    {
        _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        _speed = Mathf.Max(0f, speed);
        _lifetime = Mathf.Max(0.01f, lifetime);
        _owner = owner;
        _expireAt = Time.time + _lifetime;
        _isInitialized = true;
        _hitTargetIds.Clear();
    }

    /// <summary>
    /// 고정 주기에서 직선 이동과 수명 종료를 처리합니다.
    /// </summary>
    private void FixedUpdate()
    {
        if (!_isInitialized)
        {
            return;
        }

        if (Time.time >= _expireAt)
        {
            Despawn(E_ProjectileDespawnReason.LifetimeExpired);
            return;
        }

        Vector2 nextPosition = _rigidbody2D.position + (_direction * _speed * Time.fixedDeltaTime);
        _rigidbody2D.MovePosition(nextPosition);
    }

    /// <summary>
    /// 트리거 충돌 정책(Owner/IgnoreTag 무시, TargetLayer+Tag 히트, Environment 충돌 소멸)을 처리합니다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isInitialized || other == null)
        {
            return;
        }

        GameObject hitObject = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (hitObject == _owner)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_ignoreTag) && hitObject.CompareTag(_ignoreTag))
        {
            return;
        }

        if (IsLayerIncluded(hitObject.layer, _targetLayerMask) && IsTargetTagAllowed(hitObject))
        {
            int targetId = hitObject.GetInstanceID();
            if (_hitTargetIds.Contains(targetId))
            {
                return;
            }

            _hitTargetIds.Add(targetId);
            TryApplyHitToTarget(hitObject);
            PlayHitEffectAt(transform.position);

            if (_despawnOnHit)
            {
                Despawn(E_ProjectileDespawnReason.HitTarget);
            }
            return;
        }

        if (IsLayerIncluded(hitObject.layer, _environmentLayerMask))
        {
            PlayHitEffectAt(transform.position);
            Despawn(E_ProjectileDespawnReason.HitEnvironment);
        }
    }

    /// <summary>
    /// 타겟 대상으로 HitRequest를 전달합니다.
    /// </summary>
    private void TryApplyHitToTarget(GameObject targetObject)
    {
        HitReceiver receiver = targetObject.GetComponentInParent<HitReceiver>();
        if (receiver == null)
        {
            Debug.LogWarning($"[PooledRangedProjectile] Hit target has no HitReceiver on {targetObject.name}.");
            return;
        }

        string hitId = $"projectile_{GetInstanceID()}_{Time.frameCount}";
        HitRequest request = new HitRequest(
            hitId: hitId,
            rawDamage: _damage,
            attacker: _owner,
            hitPoint: transform.position,
            hitDirection: _direction,
            statusTag: _statusTag,
            requestTime: Time.time);
        receiver.ReceiveHit(request);
    }

    /// <summary>
    /// 지정한 레이어가 마스크에 포함되는지 반환합니다.
    /// </summary>
    private bool IsLayerIncluded(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    /// <summary>
    /// 환경 레이어 마스크가 비어 있으면 Ground 레이어로 기본 설정합니다.
    /// </summary>
    private void ResolveDefaultEnvironmentLayerMask()
    {
        if (_environmentLayerMask.value != 0)
        {
            return;
        }

        int groundLayer = LayerMask.NameToLayer(GroundLayerName);
        if (groundLayer < 0)
        {
            Debug.LogWarning($"[PooledRangedProjectile] Ground layer not found on {name}. Please create Ground layer or assign _environmentLayerMask manually.");
            return;
        }

        _environmentLayerMask = 1 << groundLayer;
        Debug.LogWarning($"[PooledRangedProjectile] _environmentLayerMask was empty on {name}. Auto assigned to Ground layer.");
    }

    /// <summary>
    /// 대상 태그가 공격 가능 조건을 만족하는지 반환합니다.
    /// </summary>
    private bool IsTargetTagAllowed(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_targetTag))
        {
            return true;
        }

        return targetObject.CompareTag(_targetTag);
    }

    /// <summary>
    /// 충돌 지점에 히트 이펙트를 1회 재생합니다.
    /// </summary>
    private void PlayHitEffectAt(Vector3 worldPosition)
    {
        if (_hitEffectPrefab == null)
        {
            return;
        }

        Instantiate(_hitEffectPrefab, worldPosition, Quaternion.identity);
    }

    /// <summary>
    /// 사유를 남기고 투사체를 풀로 반환합니다.
    /// </summary>
    private void Despawn(E_ProjectileDespawnReason reason)
    {
        if (_returnHandler != null)
        {
            _returnHandler.Invoke(this);
            return;
        }

        gameObject.SetActive(false);
    }
}
