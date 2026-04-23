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
    EyeEffect = 10,
    WalkDust = 11,
    JumpDust = 12,
    HitEffect = 13,
    SwordEffect = 14,
    RecoveryEffect = 15,
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

/// <summary>
/// 이펙트 시작 방향(좌/우) 제어 모드.
/// </summary>
public enum E_EffectFacingDirection
{
    UsePrefab = 0,
    Right = 1,
    Left = 2,
}
