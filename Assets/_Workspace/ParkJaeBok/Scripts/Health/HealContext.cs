using UnityEngine;

/// <summary>
/// 회복 적용에 필요한 입력 데이터를 담는 구조체입니다.
/// </summary>
public struct HealContext
{
    public float Amount; // 요청된 원본 회복 수치
    public GameObject Instigator; // 회복을 유발한 주체
    public string SourceId; // 회복 출처 식별 문자열
    public bool AllowOverheal; // 최대 체력 초과 회복 허용 여부

    /// <summary>
    /// 기본값을 포함해 회복 컨텍스트를 생성합니다.
    /// </summary>
    public HealContext(
        float amount,
        GameObject instigator = null,
        string sourceId = "Unknown",
        bool allowOverheal = false)
    {
        Amount = amount;
        Instigator = instigator;
        SourceId = string.IsNullOrWhiteSpace(sourceId) ? "Unknown" : sourceId;
        AllowOverheal = allowOverheal;
    }
}
