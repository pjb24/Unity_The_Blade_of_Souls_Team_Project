using UnityEngine;

/// <summary>
/// 체력 값 변경 스냅샷 데이터를 전달하는 구조체입니다.
/// </summary>
public struct HealthChangeData
{
    public float PreviousHealth; // 변경 전 체력 값
    public float CurrentHealth; // 변경 후 체력 값
    public float MaxHealth; // 변경 시점 최대 체력 값
    public float NormalizedHealth; // 0~1 범위 정규화 체력 값
    public float Delta; // 변경량(현재 - 이전)

    /// <summary>
    /// 전달 데이터를 계산해 초기화합니다.
    /// </summary>
    public HealthChangeData(float previousHealth, float currentHealth, float maxHealth)
    {
        PreviousHealth = previousHealth;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        Delta = currentHealth - previousHealth;
        NormalizedHealth = maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
    }
}
