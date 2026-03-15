using UnityEngine;

/// <summary>
/// 데미지 유형을 표현하는 열거형입니다.
/// </summary>
public enum E_DamageType
{
    Unknown,
    Physical,
    Magical,
    True,
    Environmental
}

/// <summary>
/// 데미지 적용에 필요한 입력 데이터를 담는 구조체입니다.
/// </summary>
public struct DamageContext
{
    public float Amount; // 요청된 원본 데미지 수치
    public GameObject Instigator; // 데미지를 유발한 주체
    public string SourceId; // 데미지 출처 식별 문자열
    public bool IgnoreInvincibility; // 무적 상태 무시 여부
    public bool CanKill; // 체력을 0까지 낮출 수 있는지 여부
    public E_DamageType DamageType; // 데미지 유형

    /// <summary>
    /// 기본값을 포함해 데미지 컨텍스트를 생성합니다.
    /// </summary>
    public DamageContext(
        float amount,
        GameObject instigator = null,
        string sourceId = "Unknown",
        bool ignoreInvincibility = false,
        bool canKill = true,
        E_DamageType damageType = E_DamageType.Unknown)
    {
        Amount = amount;
        Instigator = instigator;
        SourceId = string.IsNullOrWhiteSpace(sourceId) ? "Unknown" : sourceId;
        IgnoreInvincibility = ignoreInvincibility;
        CanKill = canKill;
        DamageType = damageType;
    }
}
