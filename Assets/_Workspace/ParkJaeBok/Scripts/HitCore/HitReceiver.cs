using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HitRequest를 받아 최종 수락/거부를 판단하고 기존 Health 시스템에 데미지를 반영하는 수신기입니다.
/// </summary>
public class HitReceiver : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("수락된 피격 데미지를 적용하는 데 사용하는 HealthComponent입니다. 비어 있을 경우 동일 GameObject에서 자동으로 참조를 시도합니다.")]
    [SerializeField] private HealthComponent _healthComponent; // 기존 Health 시스템의 HealthComponent 참조입니다.

    [Header("Status")]
    [Tooltip("게임플레이 스크립트에서 제어하는 수동 무적 상태입니다. 활성화되면 모든 피격을 거부합니다.")]
    [SerializeField] private bool _isInvincible; // HitReceiver 레벨의 무적 상태 여부입니다.

    [Tooltip("피격이 성공적으로 적용된 이후 추가 피격을 거부하는 시간(초)입니다. 0으로 설정하면 기능을 사용하지 않습니다.")]
    [Min(0f)]
    [SerializeField] private float _postHitInvincibilitySeconds; // 피격 적용 후 자동 무적 상태를 유지하는 시간입니다.

    [Header("Damage Rules")]
    [Tooltip("입력된 원본 데미지에 적용되는 배율입니다. HealthComponent에 전달되기 전에 적용됩니다.")]
    [SerializeField] private float _incomingDamageMultiplier = 1f; // 최종 데미지 계산에 사용하는 배율입니다.

    private readonly HashSet<string> _consumedHitIds = new HashSet<string>(); // 이미 처리한 HitId를 기록하여 중복 타격을 방지합니다.
    private readonly List<IHitListener> _listeners = new List<IHitListener>(); // 피격 결과 통지를 받을 리스너 목록입니다.

    private Coroutine _postHitInvincibilityCoroutine; // 피격 후 자동 무적 해제 시점을 관리하는 코루틴입니다.
    private bool _isPostHitInvincibilityActive; // 피격 후 자동 무적 상태가 현재 적용 중인지 나타냅니다.

    /// <summary>
    /// 수동 무적 또는 피격 후 무적이 활성화되어 있는 경우 true를 반환합니다.
    /// </summary>
    public bool IsInvincible => _isInvincible || _isPostHitInvincibilityActive;

    /// <summary>
    /// 피격 후 무적 타이머가 활성화되어 있는 동안 true를 반환합니다.
    /// </summary>
    public bool IsPostHitInvincibilityActive => _isPostHitInvincibilityActive;

    /// <summary>
    /// 설정된 피격 후 무적 지속 시간을 초 단위로 반환합니다.
    /// </summary>
    public float PostHitInvincibilitySeconds => _postHitInvincibilitySeconds;

    /// <summary>
    /// 컴포넌트 참조와 런타임 설정값을 검증합니다.
    /// </summary>
    private void Awake()
    {
        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<global::HealthComponent>();
            if (_healthComponent == null)
            {
                Debug.LogWarning($"[HitReceiver] {name}에 HealthComponent가 없습니다. 피격을 거부합니다.");
            }
            else
            {
                Debug.LogWarning($"[HitReceiver] {name}의 _healthComponent가 null이었습니다. 동일 GameObject의 HealthComponent로 대체합니다.");
            }
        }

        if (_incomingDamageMultiplier < 0f)
        {
            Debug.LogWarning($"[HitReceiver] {name}의 _incomingDamageMultiplier({_incomingDamageMultiplier}) 값이 잘못되었습니다. 0으로 대체합니다.");
            _incomingDamageMultiplier = 0f;
        }

        if (_postHitInvincibilitySeconds < 0f)
        {
            Debug.LogWarning($"[HitReceiver] {name}의 _postHitInvincibilitySeconds({_postHitInvincibilitySeconds}) 값이 잘못되었습니다. 0으로 대체합니다.");
            _postHitInvincibilitySeconds = 0f;
        }
    }

    /// <summary>
    /// Receiver가 비활성화될 때 일시적인 무적 상태를 중지합니다.
    /// </summary>
    private void OnDisable()
    {
        StopPostHitInvincibility();
    }

    /// <summary>
    /// 인스펙터에서 설정된 숫자 값들을 유효 범위 내로 유지합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_incomingDamageMultiplier < 0f)
        {
            Debug.LogWarning($"[HitReceiver] {name}의 _incomingDamageMultiplier({_incomingDamageMultiplier}) 값이 잘못되었습니다. 0으로 대체합니다.");
            _incomingDamageMultiplier = 0f;
        }

        if (_postHitInvincibilitySeconds < 0f)
        {
            Debug.LogWarning($"[HitReceiver] {name}의 _postHitInvincibilitySeconds({_postHitInvincibilitySeconds}) 값이 잘못되었습니다. 0으로 대체합니다.");
            _postHitInvincibilitySeconds = 0f;
        }
    }

    /// <summary>
    /// 외부 리스너를 등록합니다.
    /// </summary>
    public void AddListener(IHitListener listener)
    {
        TryAddListener(listener);
    }

    /// <summary>
    /// 외부 리스너 등록을 시도하고 성공 여부를 반환합니다.
    /// </summary>
    public bool TryAddListener(IHitListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[HitReceiver] {name}에서 AddListener에 null이 전달되었습니다.");
            return false;
        }

        if (_listeners.Contains(listener))
        {
            Debug.LogWarning($"[HitReceiver] {name}에서 중복 리스너가 등록되었습니다.");
            return false;
        }

        _listeners.Add(listener);
        return true;
    }

    /// <summary>
    /// 외부 리스너 등록을 해제합니다.
    /// </summary>
    public void RemoveListener(IHitListener listener)
    {
        TryRemoveListener(listener);
    }

    /// <summary>
    /// 외부 리스너 등록 해제를 시도하고 성공 여부를 반환합니다.
    /// </summary>
    public bool TryRemoveListener(IHitListener listener)
    {
        if (listener == null)
        {
            Debug.LogWarning($"[HitReceiver] {name}에서 RemoveListener에 null이 전달되었습니다.");
            return false;
        }

        if (_listeners.Remove(listener) == false)
        {
            Debug.LogWarning($"[HitReceiver] {name}에서 제거할 리스너를 찾을 수 없습니다.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 무적 상태를 설정합니다.
    /// </summary>
    public void SetInvincible(bool isInvincible)
    {
        _isInvincible = isInvincible;
    }

    /// <summary>
    /// 입력 데미지 배율을 설정합니다.
    /// </summary>
    public void SetIncomingDamageMultiplier(float multiplier)
    {
        if (multiplier < 0f)
        {
            Debug.LogWarning($"[HitReceiver] {name}에서 SetIncomingDamageMultiplier에 잘못된 값({multiplier})이 전달되었습니다. 0으로 대체합니다.");
            _incomingDamageMultiplier = 0f;
            return;
        }

        _incomingDamageMultiplier = multiplier;
    }

    /// <summary>
    /// 피격 후 무적 지속 시간을 설정합니다.
    /// </summary>
    public void SetPostHitInvincibilitySeconds(float seconds)
    {
        if (seconds < 0f)
        {
            Debug.LogWarning($"[HitReceiver] {name}에서 SetPostHitInvincibilitySeconds에 잘못된 값({seconds})이 전달되었습니다. 0으로 대체합니다.");
            _postHitInvincibilitySeconds = 0f;
            return;
        }

        _postHitInvincibilitySeconds = seconds;
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
            Debug.LogWarning($"[HitReceiver] {name}에서 최종 데미지가 0 이하({finalDamage})로 계산되었습니다. 요청을 거부합니다.");
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
                Debug.LogWarning($"[HitReceiver] Health 시스템에서 데미지가 차단/무효 처리되었으며 fallback이 발생했습니다. HitId={request.HitId}, Target={name}");
            }

            E_HitRejectReason blockedReason = isDeadAfter ? E_HitRejectReason.TargetDead : E_HitRejectReason.BlockedByHealthSystem;
            HitResult blocked = HitResult.CreateRejected(request.HitId, blockedReason, healthBefore, healthAfter, isDeadAfter);
            NotifyListeners(request, blocked);
            return blocked;
        }

        _consumedHitIds.Add(request.HitId);
        StartPostHitInvincibilityIfNeeded(isDeadAfter);

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
            Debug.LogWarning($"[HitReceiver] {name}에서 잘못된 HitId입니다.");
            failReason = E_HitRejectReason.InvalidHitId;
            return false;
        }

        if (float.IsNaN(request.RawDamage) || float.IsInfinity(request.RawDamage))
        {
            Debug.LogWarning($"[HitReceiver] {name}에서 RawDamage가 NaN 또는 Infinity입니다. HitId={request.HitId}");
            failReason = E_HitRejectReason.InvalidDamage;
            return false;
        }

        if (request.RawDamage <= 0f)
        {
            Debug.LogWarning($"[HitReceiver] {name}에서 RawDamage({request.RawDamage})가 0 이하입니다. HitId={request.HitId}");
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
            Debug.LogWarning($"[HitReceiver] {name}에서 HealthComponent가 없습니다. HitId={request.HitId}");
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

        if (IsInvincible)
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
    /// 대상이 생존했고 설정이 활성화되어 있을 때 피격 후 무적 타이머를 시작합니다.
    /// </summary>
    private void StartPostHitInvincibilityIfNeeded(bool isDeadAfter)
    {
        if (isDeadAfter)
        {
            return;
        }

        if (_postHitInvincibilitySeconds <= 0f)
        {
            return;
        }

        StopPostHitInvincibility();
        _postHitInvincibilityCoroutine = StartCoroutine(RunPostHitInvincibility(_postHitInvincibilitySeconds));
    }

    /// <summary>
    /// 지정된 시간 동안 피격 후 무적 상태를 적용한 뒤 해제합니다.
    /// </summary>
    private IEnumerator RunPostHitInvincibility(float durationSeconds)
    {
        _isPostHitInvincibilityActive = true;
        yield return new WaitForSeconds(durationSeconds);
        _isPostHitInvincibilityActive = false;
        _postHitInvincibilityCoroutine = null;
    }

    /// <summary>
    /// 현재 활성화된 피격 후 무적 상태를 취소하고 상태를 초기화합니다.
    /// </summary>
    private void StopPostHitInvincibility()
    {
        if (_postHitInvincibilityCoroutine != null)
        {
            StopCoroutine(_postHitInvincibilityCoroutine);
            _postHitInvincibilityCoroutine = null;
        }

        _isPostHitInvincibilityActive = false;
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
                Debug.LogWarning($"[HitReceiver] {name}에서 null 리스너를 건너뜁니다.");
                continue;
            }

            listener.OnHitResolved(request, result);
        }
    }
}