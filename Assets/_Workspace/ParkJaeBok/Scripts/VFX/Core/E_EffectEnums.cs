using System;

/// <summary>
/// 프로젝트 전반에서 사용하는 이펙트 식별자.
/// </summary>
public enum E_EffectId
{
    None = 0,
    HitSmall = 1,
    LandingDust = 2,
    DashDust = 3,
    BuffAura = 4,
    StatusPoison = 5,
    WeaponTrail = 6,
    ProjectileImpact = 7,
    EnemyDeath = 8,
    WorldBurst = 9,
}

/// <summary>
/// 이펙트의 재생 방식.
/// </summary>
public enum E_EffectPlayMode
{
    OneShot = 0,
    Loop = 1,
    Spawn = 2,
    Follow = 3,
    Attach = 4,
}

/// <summary>
/// 풀 부족 시 대체 정책.
/// </summary>
public enum E_EffectFallbackPolicy
{
    InstantiateNew = 0,
    ReuseOldest = 1,
    DropRequest = 2,
}
