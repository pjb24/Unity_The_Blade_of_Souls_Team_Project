using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HitRequest를 받아 최종 수락/거부를 판단하고 기존 Health 시스템에 데미지를 반영하는 수신기입니다.
/// </summary>
public class HitReceiver : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private global::HealthComponent _healthComponent; // 기존 Health 시스템의 HealthComponent 참조입니다.

    [Header("Status")]
    [SerializeField] private bool _isInvincible; // HitReceiver 레벨의 무적 상태 여부입니다.

    [Header("Damage Rules")]
    [SerializeField] private float _incomingDamageMultiplier = 1f; // 최종 데미지 계산에 사용하는 배율입니다.

    private readonly HashSet<string> _consumedHitIds = new HashSet<string>(); // 이미 처리한 HitId를 기록하여 중복 타격을 방지합니다.
    private readonly List<IHitListener> _listeners = new List<IHitListener>(); // 피격 결과 통지를 받을 리스너 목록입니다.

    /// <summary>
    /// 컴포넌트 참조와 설정 값을 검증합니다.
    /// </summary>
    private void Awake()
    {
        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<global::HealthComponent>();
            if (_healthComponent == null)
            {
                Debug.LogWarning($"[HitReceiver] Missing HealthComponent on {name}. Receiver will reject hits.");
            }
            else
            {
                Debug.LogWarning($"[HitReceiver] _healthComponent was null on {name}. Fallback to same GameObject HealthComponent.");
            }
        }

        if (_incomingDamageMultiplier < 0f)
        {
            Debug.LogWarning($"[HitReceiver] Invalid _incomingDamageMultiplier({_incomingDamageMultiplier}) on {name}. Fallback to 0.");
            _incomingDamageMultiplier = 0f;
        }
    }

    /// <summary>
    /// 외부 리스너를 등록합니다.
    /// </summary>
    public void AddListener(IHitListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[HitReceiver] AddListener received null on {name}.");
            return;
        }

        if (_listeners.Contains(listener))
        {
            Debug.LogWarning($"[HitReceiver] AddListener received duplicate listener on {name}.");
            return;
        }

        _listeners.Add(listener);
    }

    /// <summary>
    /// 외부 리스너 등록을 해제합니다.
    /// </summary>
    public void RemoveListener(IHitListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[HitReceiver] RemoveListener received null on {name}.");
            return;
        }

        if (_listeners.Remove(listener) == false)
        {
            Debug.LogWarning($"[HitReceiver] RemoveListener could not find listener on {name}.");
        }
    }

    /// <summary>
    /// 무적 상태를 변경합니다.
    /// </summary>
    public void SetInvincible(bool isInvincible)
    {
        _isInvincible = isInvincible;
    }

    /// <summary>
    /// 입력 데미지 배율을 변경합니다.
    /// </summary>
    public void SetIncomingDamageMultiplier(float multiplier)
    {
        if (multiplier < 0f)
        {
            Debug.LogWarning($"[HitReceiver] SetIncomingDamageMultiplier invalid value({multiplier}) on {name}. Fallback to 0.");
            _incomingDamageMultiplier = 0f;
            return;
        }

        _incomingDamageMultiplier = multiplier;
    }

    /// <summary>
    /// 공격자가 전달한 HitRequest를 대상 기준으로 최종 처리합니다.
    /// </summary>
    public HitResult ReceiveHit(in HitRequest request)
    {
        float healthBeforeSafe = _healthComponent != null ? _healthComponent.GetCurrentHealth() : 0f;
        float healthAfterSafe = healthBeforeSafe;
        bool isDeadSafe = _healthComponent != null && _healthComponent.IsDead;

        // 1. 입력 검증
        E_HitRejectReason validationFailReason;
        if (TryValidateRequest(request, out validationFailReason) == false)
        {
            HitResult validationFailed = HitResult.CreateRejected(request.HitId, validationFailReason, healthBeforeSafe, healthAfterSafe, isDeadSafe);
            NotifyListeners(request, validationFailed);
            return validationFailed;
        }

        // 2. 피격 가능 여부 판정
        E_HitRejectReason availabilityFailReason;
        if (TryCheckAcceptable(request, out availabilityFailReason) == false)
        {
            float currentHealth = _healthComponent != null ? _healthComponent.GetCurrentHealth() : healthBeforeSafe;
            bool isDead = _healthComponent != null && _healthComponent.IsDead;
            HitResult unavailable = HitResult.CreateRejected(request.HitId, availabilityFailReason, currentHealth, currentHealth, isDead);
            NotifyListeners(request, unavailable);
            return unavailable;
        }

        // 3. 최종 데미지 계산
        float finalDamage = CalculateFinalDamage(request.RawDamage);
        if (finalDamage <= 0f)
        {
            Debug.LogWarning($"[HitReceiver] Final damage became non-positive({finalDamage}) on {name}. Request rejected.");
            float currentHealth = _healthComponent.GetCurrentHealth();
            HitResult invalidFinalDamage = HitResult.CreateRejected(request.HitId, E_HitRejectReason.InvalidDamage, currentHealth, currentHealth, _healthComponent.IsDead);
            NotifyListeners(request, invalidFinalDamage);
            return invalidFinalDamage;
        }

        // 4. 체력 반영
        float healthBefore = _healthComponent.GetCurrentHealth();
        DamageContext damageContext = new DamageContext(
            amount: finalDamage,
            instigator: request.Attacker,
            sourceId: request.HitId,
            ignoreInvincibility: false,
            canKill: true,
            damageType: E_DamageType.Unknown);
        DamageResult damageResult = _healthComponent.ApplyDamage(damageContext);
        float healthAfter = damageResult.CurrentHealth;

        // 5. 사망 여부 결정
        bool isDeadAfter = _healthComponent.IsDead;

        // Health 시스템에서 차단/무효 처리된 경우 명시적으로 거부 응답을 반환합니다.
        if (damageResult.IsInvalid || damageResult.IsBlocked)
        {
            if (damageResult.HasWarningFallback)
            {
                Debug.LogWarning($"[HitReceiver] Health system blocked/invalid damage with fallback. HitId={request.HitId}, Target={name}");
            }

            E_HitRejectReason blockedReason = isDeadAfter ? E_HitRejectReason.TargetDead : E_HitRejectReason.BlockedByHealthSystem;
            HitResult blocked = HitResult.CreateRejected(request.HitId, blockedReason, healthBefore, healthAfter, isDeadAfter);
            NotifyListeners(request, blocked);
            return blocked;
        }

        _consumedHitIds.Add(request.HitId);

        // 6. 결과 생성
        HitResult acceptedResult = HitResult.CreateAccepted(request.HitId, damageResult.AppliedAmount, healthBefore, healthAfter, isDeadAfter);

        // 7. 리스너 통지
        NotifyListeners(request, acceptedResult);

        return acceptedResult;
    }

    /// <summary>
    /// 요청 필드의 유효성을 검증합니다.
    /// </summary>
    private bool TryValidateRequest(in HitRequest request, out E_HitRejectReason failReason)
    {
        if (string.IsNullOrWhiteSpace(request.HitId))
        {
            Debug.LogWarning($"[HitReceiver] Invalid HitId on {name}.");
            failReason = E_HitRejectReason.InvalidHitId;
            return false;
        }

        if (float.IsNaN(request.RawDamage) || float.IsInfinity(request.RawDamage))
        {
            Debug.LogWarning($"[HitReceiver] RawDamage is NaN/Infinity on {name}. HitId={request.HitId}");
            failReason = E_HitRejectReason.InvalidDamage;
            return false;
        }

        if (request.RawDamage <= 0f)
        {
            Debug.LogWarning($"[HitReceiver] Non-positive RawDamage({request.RawDamage}) on {name}. HitId={request.HitId}");
            failReason = E_HitRejectReason.InvalidDamage;
            return false;
        }

        failReason = E_HitRejectReason.None;
        return true;
    }

    /// <summary>
    /// 대상 상태를 기준으로 피격 수락 가능 여부를 판정합니다.
    /// </summary>
    private bool TryCheckAcceptable(in HitRequest request, out E_HitRejectReason failReason)
    {
        if (_healthComponent == null)
        {
            Debug.LogWarning($"[HitReceiver] No HealthComponent available on {name}. HitId={request.HitId}");
            failReason = E_HitRejectReason.ReceiverNotReady;
            return false;
        }

        if (_consumedHitIds.Contains(request.HitId))
        {
            failReason = E_HitRejectReason.DuplicateHitId;
            return false;
        }

        if (_healthComponent.IsDead)
        {
            failReason = E_HitRejectReason.TargetDead;
            return false;
        }

        if (_isInvincible)
        {
            failReason = E_HitRejectReason.TargetInvincible;
            return false;
        }

        failReason = E_HitRejectReason.None;
        return true;
    }

    /// <summary>
    /// 원본 데미지에 내부 규칙을 적용해 최종 데미지를 계산합니다.
    /// </summary>
    private float CalculateFinalDamage(float rawDamage)
    {
        return rawDamage * _incomingDamageMultiplier;
    }

    /// <summary>
    /// 등록된 리스너들에게 피격 결과를 통지합니다.
    /// </summary>
    private void NotifyListeners(in HitRequest request, in HitResult result)
    {
        for (int i = 0; i < _listeners.Count; i++)
        {
            IHitListener listener = _listeners[i];
            if (listener == null)
            {
                Debug.LogWarning($"[HitReceiver] Null listener skipped on {name}.");
                continue;
            }

            listener.OnHitResolved(request, result);
        }
    }
}
