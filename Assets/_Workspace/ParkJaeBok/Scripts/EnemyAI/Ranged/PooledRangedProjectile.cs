using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D 직선 이동/트리거 충돌 기반으로 단일 히트를 처리하는 원거리 투사체입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PooledRangedProjectile : MonoBehaviour, IPoolableObject
{
    private const string GroundLayerName = "Ground"; // 환경 충돌 기본 레이어 이름입니다.

    [Header("Damage")]
    [Tooltip("Player에게 전달할 기본 피해량입니다.")]
    [SerializeField] private float _damage = 10f; // 히트 요청에 포함할 기본 피해량입니다.
    [Tooltip("HitRequest.StatusTag로 전달할 문자열입니다.")]
    [SerializeField] private string _statusTag = "EnemyProjectile"; // 공격 출처를 구분할 상태 태그입니다.
    [Tooltip("피격 대상 판정에 사용할 레이어 마스크입니다.")]
    [SerializeField] private LayerMask _targetLayerMask = 0; // 유효 타겟 검사용 레이어 마스크입니다.
    [Tooltip("비어 있지 않으면 지정 태그를 가진 대상만 피격합니다.")]
    [SerializeField] private string _targetTag = "Player"; // 피격 대상 태그 필터입니다.
    [Tooltip("비어 있지 않으면 지정 태그를 가진 대상은 무시합니다.")]
    [SerializeField] private string _ignoreTag = "Enemy"; // 자기 진영 무시용 태그 필터입니다.
    [Tooltip("환경 충돌 판정에 사용할 레이어 마스크입니다.")]
    [SerializeField] private LayerMask _environmentLayerMask = 0; // 환경 충돌용 레이어 마스크입니다.
    [Tooltip("타겟 명중 시 즉시 소멸할지 여부입니다.")]
    [SerializeField] private bool _despawnOnHit = true; // 타겟 명중 후 소멸 여부입니다.
    [Tooltip("타겟/환경 충돌 시 1회 재생할 히트 이펙트 프리팹입니다.")]
    [SerializeField] private GameObject _hitEffectPrefab; // 충돌 지점 이펙트 프리팹입니다.

    [Tooltip("현재 투사체의 소유자입니다.")]
    private GameObject _owner; // 공격 소유자 참조입니다.
    [Tooltip("발사 시점에 고정된 이동 방향입니다.")]
    private Vector2 _direction; // 현재 프레임 이동 방향입니다.
    [Tooltip("투사체 이동 속도입니다.")]
    private float _speed; // 초당 이동 속도입니다.
    [Tooltip("투사체 수명(초)입니다.")]
    private float _lifetime; // 자동 소멸까지 유지 시간입니다.
    [Tooltip("수명이 끝나는 시각입니다.")]
    private float _expireAt; // 수명 종료 시각 캐시입니다.
    [Tooltip("초기화 완료 여부입니다.")]
    private bool _isInitialized; // 발사 준비 완료 여부입니다.
    [Tooltip("관찰자 시각 전용 투사체인지 여부입니다.")]
    private bool _isVisualOnly; // 서버 판정 없는 시각 전용 투사체 여부입니다.
    [Tooltip("관찰자 측 복제 투사체를 식별하는 시퀀스 ID입니다.")]
    private int _visualInstanceId; // 복제 투사체 식별용 ID입니다.
    [Tooltip("이미 피격 처리한 대상 InstanceId 집합입니다.")]
    private readonly HashSet<int> _hitTargetIds = new HashSet<int>(); // 중복 타격 방지용 캐시입니다.

    [Tooltip("물리 이동을 수행하는 Rigidbody2D 참조입니다.")]
    private Rigidbody2D _rigidbody2D; // 투사체 물리 이동용 Rigidbody2D입니다.
    [Tooltip("트리거 충돌을 수신하는 Collider2D 참조입니다.")]
    private Collider2D _collider2D; // 충돌 판정용 Collider2D입니다.
    [Tooltip("풀 반환 콜백입니다.")]
    private Action<PooledRangedProjectile> _returnHandler; // 풀 서비스 반환 콜백입니다.

    /// <summary>
    /// 투사체가 소멸할 때 소멸 사유를 통지하는 이벤트입니다.
    /// </summary>
    private Action<PooledRangedProjectile, E_ProjectileDespawnReason> _despawned; // Despawn listeners registered through AddListener.

    /// <summary>
    /// 현재 복제 시각 투사체 ID를 반환합니다.
    /// </summary>
    public int VisualInstanceId => _visualInstanceId;

    /// <summary>
    /// ObjectPool에서 대여될 때 이전 발사 상태가 남지 않도록 기본 상태를 정리합니다.
    /// </summary>
    public void OnPoolSpawned(PoolSpawnContext context)
    {
        _hitTargetIds.Clear();
        _isInitialized = false;
        _isVisualOnly = false;
        _visualInstanceId = 0;
        _owner = context.Owner;

        if (_collider2D != null)
        {
            _collider2D.enabled = true;
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
            _rigidbody2D.angularVelocity = 0f;
        }
    }

    /// <summary>
    /// ObjectPool로 반환될 때 발사 상태, 소유자 참조, 이벤트 구독을 정리합니다.
    /// </summary>
    public void OnPoolDespawned()
    {
        ResetRuntimeStateForPool();
    }

    /// <summary>
    /// 필수 컴포넌트와 기본 물리 설정을 보정합니다.
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
    /// 인스펙터 값 변경 시 기본 환경 레이어 설정을 검증합니다.
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
        ResetRuntimeStateForPool();
    }

    /// <summary>
    /// 풀 반환 핸들러를 바인딩합니다.
    /// </summary>
    public void BindReturnHandler(Action<PooledRangedProjectile> returnHandler)
    {
        _returnHandler = returnHandler;
    }

    /// <summary>
    /// 발사 파라미터를 설정하고 투사체를 활성 상태로 초기화합니다.
    /// </summary>
    public void Initialize(Vector2 direction, float speed, float lifetime, GameObject owner, bool isVisualOnly = false, int visualInstanceId = 0)
    {
        _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        _speed = Mathf.Max(0f, speed);
        _lifetime = Mathf.Max(0.01f, lifetime);
        _owner = owner;
        _isVisualOnly = isVisualOnly;
        _visualInstanceId = Mathf.Max(0, visualInstanceId);
        _expireAt = Time.time + _lifetime;
        _isInitialized = true;
        _hitTargetIds.Clear();
    }

    /// <summary>
    /// 런타임 발사 주체가 투사체의 타격 값과 충돌 대상을 설정합니다.
    /// </summary>
    public void ConfigureHitSettings(float damage, LayerMask targetLayerMask, string statusTag)
    {
        _damage = Mathf.Max(0f, damage);
        _targetLayerMask = targetLayerMask;
        _statusTag = string.IsNullOrWhiteSpace(statusTag) ? _statusTag : statusTag;
    }

    /// <summary>
    /// 투사체 Despawn 알림 리스너를 등록합니다.
    /// </summary>
    public void AddListener(Action<PooledRangedProjectile, E_ProjectileDespawnReason> listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[PooledRangedProjectile] AddListener received null on {name}.", this);
            return;
        }

        _despawned -= listener;
        _despawned += listener;
    }

    /// <summary>
    /// 투사체 Despawn 알림 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(Action<PooledRangedProjectile, E_ProjectileDespawnReason> listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[PooledRangedProjectile] RemoveListener received null on {name}.", this);
            return;
        }

        _despawned -= listener;
    }

    /// <summary>
    /// 네트워크 복제 결과로 시각 전용 투사체를 즉시 종료합니다.
    /// </summary>
    public void ForceDespawn(E_ProjectileDespawnReason reason)
    {
        if (!_isInitialized && !gameObject.activeSelf)
        {
            return;
        }

        Despawn(reason);
    }

    /// <summary>
    /// 고정 주기에서 직선 이동과 수명 만료를 처리합니다.
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
    /// 트리거 충돌 처리와 타겟/환경 명중 판정을 수행합니다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isInitialized || other == null)
        {
            return;
        }

        if (_isVisualOnly)
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
    /// 타겟에게 HitRequest를 전달합니다.
    /// </summary>
    private void TryApplyHitToTarget(GameObject targetObject)
    {
        HitReceiver receiver = FindHitReceiverFromTargetHierarchy(targetObject);
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
    /// 타겟 계층에서 HitReceiver를 순차 탐색합니다.
    /// </summary>
    private HitReceiver FindHitReceiverFromTargetHierarchy(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return null;
        }

        HitReceiver selfReceiver = targetObject.GetComponent<HitReceiver>();
        if (selfReceiver != null)
        {
            return selfReceiver;
        }

        HitReceiver parentReceiver = targetObject.GetComponentInParent<HitReceiver>();
        if (parentReceiver != null)
        {
            return parentReceiver;
        }

        HitReceiver childReceiver = targetObject.GetComponentInChildren<HitReceiver>(true);
        if (childReceiver != null)
        {
            return childReceiver;
        }

        Transform parentTransform = targetObject.transform.parent;
        if (parentTransform == null)
        {
            return null;
        }

        for (int index = 0; index < parentTransform.childCount; index++)
        {
            Transform sibling = parentTransform.GetChild(index);
            if (sibling == null || sibling.gameObject == targetObject)
            {
                continue;
            }

            HitReceiver siblingReceiver = sibling.GetComponent<HitReceiver>();
            if (siblingReceiver != null)
            {
                return siblingReceiver;
            }

            HitReceiver siblingChildReceiver = sibling.GetComponentInChildren<HitReceiver>(true);
            if (siblingChildReceiver != null)
            {
                return siblingChildReceiver;
            }
        }

        return null;
    }

    /// <summary>
    /// 지정 레이어가 마스크에 포함되는지 반환합니다.
    /// </summary>
    private bool IsLayerIncluded(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    /// <summary>
    /// 환경 레이어 마스크가 비어 있으면 Ground 레이어를 기본값으로 설정합니다.
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
    /// 타겟 태그가 공격 가능 조건을 만족하는지 반환합니다.
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
    /// 충돌 지점에 히트 이펙트를 재생합니다.
    /// </summary>
    private void PlayHitEffectAt(Vector3 worldPosition)
    {
        if (_hitEffectPrefab == null)
        {
            return;
        }

        LocalObjectPoolManager poolManager = LocalObjectPoolManager.Instance;
        if (poolManager == null)
        {
            Debug.LogWarning($"[PooledRangedProjectile] Hit effect pool manager missing. prefab={_hitEffectPrefab.name}", this);
            return;
        }

        poolManager.Spawn(_hitEffectPrefab, worldPosition, Quaternion.identity, null, gameObject);
    }

    /// <summary>
    /// 소멸 사유를 기록하고 투사체를 풀로 반환합니다.
    /// </summary>
    private void Despawn(E_ProjectileDespawnReason reason)
    {
        _despawned?.Invoke(this, reason);

        if (_returnHandler != null)
        {
            _returnHandler.Invoke(this);
            return;
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 다음 대여에 이전 Projectile 상태가 섞이지 않도록 런타임 필드를 초기화합니다.
    /// </summary>
    private void ResetRuntimeStateForPool()
    {
        _isInitialized = false;
        _isVisualOnly = false;
        _visualInstanceId = 0;
        _owner = null;

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
            _rigidbody2D.angularVelocity = 0f;
        }

        if (_collider2D != null)
        {
            _collider2D.enabled = true;
        }

        _hitTargetIds.Clear();
        _despawned = null;
    }
}
