using UnityEngine;

/// <summary>
/// 피격 처리에 필요한 입력 데이터를 전달하는 요청 구조체입니다.
/// </summary>
public readonly struct HitRequest
{
    /// <summary>
    /// 중복 타격 방지를 위한 고유 히트 식별자입니다.
    /// </summary>
    public readonly string HitId;

    /// <summary>
    /// 공격자가 전달한 원본 데미지 값입니다.
    /// </summary>
    public readonly float RawDamage;

    /// <summary>
    /// 공격 주체 GameObject 참조입니다.
    /// </summary>
    public readonly GameObject Attacker;

    /// <summary>
    /// 피격 위치 정보(선택)입니다.
    /// </summary>
    public readonly Vector3 HitPoint;

    /// <summary>
    /// 피격 방향 정보(선택)입니다.
    /// </summary>
    public readonly Vector3 HitDirection;

    /// <summary>
    /// 상태/속성 확장을 위한 태그 문자열입니다.
    /// </summary>
    public readonly string StatusTag;

    /// <summary>
    /// 요청 생성 시각(중복/디버깅 추적용)입니다.
    /// </summary>
    public readonly float RequestTime;

    /// <summary>
    /// 피격 요청을 생성합니다.
    /// </summary>
    public HitRequest(
        string hitId,
        float rawDamage,
        GameObject attacker,
        Vector3 hitPoint,
        Vector3 hitDirection,
        string statusTag,
        float requestTime)
    {
        HitId = hitId;
        RawDamage = rawDamage;
        Attacker = attacker;
        HitPoint = hitPoint;
        HitDirection = hitDirection;
        StatusTag = statusTag;
        RequestTime = requestTime;
    }
}
