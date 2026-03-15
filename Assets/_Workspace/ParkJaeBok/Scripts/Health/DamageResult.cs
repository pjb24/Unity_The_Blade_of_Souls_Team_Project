/// <summary>
/// 데미지 적용 결과를 외부로 전달하는 구조체입니다.
/// </summary>
public struct DamageResult
{
    public float PreviousHealth; // 적용 전 체력 값
    public float CurrentHealth; // 적용 후 체력 값
    public float AppliedAmount; // 실제 적용된 데미지 양
    public bool IsInvalid; // 입력 자체가 잘못되어 무효 처리되었는지 여부
    public bool IsBlocked; // 룰/상태로 인해 차단되었는지 여부
    public bool DidCauseDeath; // 이번 적용으로 사망이 발생했는지 여부
    public bool HasWarningFallback; // 경고성 폴백 로직이 발생했는지 여부

    /// <summary>
    /// 결과 구조체를 초기화합니다.
    /// </summary>
    public DamageResult(
        float previousHealth,
        float currentHealth,
        float appliedAmount,
        bool isInvalid,
        bool isBlocked,
        bool didCauseDeath,
        bool hasWarningFallback)
    {
        PreviousHealth = previousHealth;
        CurrentHealth = currentHealth;
        AppliedAmount = appliedAmount;
        IsInvalid = isInvalid;
        IsBlocked = isBlocked;
        DidCauseDeath = didCauseDeath;
        HasWarningFallback = hasWarningFallback;
    }
}
