/// <summary>
/// 체력 시스템의 변경 알림을 수신하는 인터페이스입니다.
/// </summary>
public interface IHealthListener
{
    /// <summary>
    /// 체력 수치가 변경될 때 호출됩니다.
    /// </summary>
    void OnHealthChanged(HealthChangeData data);

    /// <summary>
    /// 데미지 적용 직후 호출됩니다.
    /// </summary>
    void OnDamaged(DamageResult result);

    /// <summary>
    /// 회복 적용 직후 호출됩니다.
    /// </summary>
    void OnHealed(HealResult result);

    /// <summary>
    /// 사망 상태로 전환될 때 호출됩니다.
    /// </summary>
    void OnDied();

    /// <summary>
    /// 사망 상태에서 살아날 때 호출됩니다.
    /// </summary>
    void OnRevived();

    /// <summary>
    /// 최대 체력이 변경될 때 호출됩니다.
    /// </summary>
    void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth);
}
