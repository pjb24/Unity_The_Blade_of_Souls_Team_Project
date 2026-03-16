/// <summary>
/// 피격 처리 실패 사유를 명확하게 표현하는 열거형입니다.
/// </summary>
public enum E_HitRejectReason
{
    None = 0,
    InvalidRequest = 1,
    InvalidHitId = 2,
    InvalidDamage = 3,
    DuplicateHitId = 4,
    TargetInvincible = 5,
    TargetDead = 6,
    ReceiverNotReady = 7,
    BlockedByHealthSystem = 8,
}

/// <summary>
/// 피격 처리 최종 결과를 외부로 반환하는 구조체입니다.
/// </summary>
public readonly struct HitResult
{
    /// <summary>
    /// 피격 요청 수락 여부입니다.
    /// </summary>
    public readonly bool IsAccepted;

    /// <summary>
    /// 최종 적용된 데미지 값입니다.
    /// </summary>
    public readonly float AppliedDamage;

    /// <summary>
    /// 처리 이전 체력 값입니다.
    /// </summary>
    public readonly float HealthBefore;

    /// <summary>
    /// 처리 이후 체력 값입니다.
    /// </summary>
    public readonly float HealthAfter;

    /// <summary>
    /// 처리 후 사망 여부입니다.
    /// </summary>
    public readonly bool IsDeadAfter;

    /// <summary>
    /// 실패 또는 거부 사유입니다.
    /// </summary>
    public readonly E_HitRejectReason RejectReason;

    /// <summary>
    /// 응답 대상 HitId입니다.
    /// </summary>
    public readonly string HitId;

    /// <summary>
    /// 피격 결과를 생성합니다.
    /// </summary>
    public HitResult(
        bool isAccepted,
        float appliedDamage,
        float healthBefore,
        float healthAfter,
        bool isDeadAfter,
        E_HitRejectReason rejectReason,
        string hitId)
    {
        IsAccepted = isAccepted;
        AppliedDamage = appliedDamage;
        HealthBefore = healthBefore;
        HealthAfter = healthAfter;
        IsDeadAfter = isDeadAfter;
        RejectReason = rejectReason;
        HitId = hitId;
    }

    /// <summary>
    /// 성공 결과를 간편 생성합니다.
    /// </summary>
    public static HitResult CreateAccepted(string hitId, float appliedDamage, float healthBefore, float healthAfter, bool isDeadAfter)
    {
        return new HitResult(true, appliedDamage, healthBefore, healthAfter, isDeadAfter, E_HitRejectReason.None, hitId);
    }

    /// <summary>
    /// 거부 결과를 간편 생성합니다.
    /// </summary>
    public static HitResult CreateRejected(string hitId, E_HitRejectReason rejectReason, float healthBefore, float healthAfter, bool isDeadAfter)
    {
        return new HitResult(false, 0f, healthBefore, healthAfter, isDeadAfter, rejectReason, hitId);
    }
}
