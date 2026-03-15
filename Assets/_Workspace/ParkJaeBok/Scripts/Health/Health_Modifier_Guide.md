# Health Modifier 적용 가이드

이 문서는 현재 프로젝트의 `HealthCore` 기반 체력 시스템에서 **모디파이어가 필요한 상황**과 **안전한 적용 방식**을 설명합니다.

- 대상: Player / Enemy / Boss / 파괴 오브젝트 공통
- 목적: Core 코드를 과도하게 변경하지 않고 게임 규칙을 확장

---

## 1) 모디파이어란?

모디파이어는 `ApplyDamage`, `ApplyHeal`가 실제 계산을 수행하기 전에 `DamageContext`, `HealContext`를 가공하는 훅입니다.

- 데미지 모디파이어 등록: `AddDamageModifier`
- 회복 모디파이어 등록: `AddHealModifier`
- 실행 시점: `RunDamageModifiers`, `RunHealModifiers`

즉, **체력 Core의 기본 책임(체력 상태/Clamp/사망 판정)은 유지**하고, 게임별 룰은 모디파이어로 분리합니다.

---

## 2) 모디파이어가 필요한 대표 상황

### 2-1. 방어력/저항 시스템

**상황**
- 물리/마법/원소 피해를 비율 또는 고정값으로 감소해야 함

**적용 방식**
- `DamageContext.DamageType` 기반으로 `Amount`를 감소
- 예: 물리 20% 감소, 마법 10 고정 감소

---

### 2-2. Shield(보호막) 선소모

**상황**
- HP를 깎기 전에 보호막을 먼저 소모해야 함

**적용 방식**
- 캐릭터의 Shield 상태를 참조
- 모디파이어에서 Shield를 먼저 소모하고 남은 피해만 `Amount`에 반영
- 전부 Shield로 막히면 `Amount = 0`

---

### 2-3. Invincibility(무적 프레임)

**상황**
- 회피/스킬/연출 중 무적 처리 필요

**적용 방식**
- 무적 상태면 `Amount = 0`
- 단, `DamageContext.IgnoreInvincibility == true`면 관통 허용

---

### 2-4. 치명타/백어택/부위 파괴 배율

**상황**
- 히트 조건에 따라 같은 공격이라도 최종 피해가 달라짐

**적용 방식**
- 공격 정보(백어택 여부, 약점 타격 여부) 기반으로 `Amount` 배율 적용
- 예: 치명타 1.8x, 약점 2.0x

---

### 2-5. 난이도/게임 모드 보정

**상황**
- Easy/Normal/Hard에서 받는 피해와 회복량을 다르게 설정해야 함

**적용 방식**
- 모드 매니저 값을 읽어 데미지/회복 배율 적용
- 예: Hard에서 플레이어 피격 1.25x, 힐량 0.9x

---

### 2-6. 디버프/버프 기반 회복량 변화

**상황**
- 치료 증가/감소, 치유 금지 등 상태 이상 룰 필요

**적용 방식**
- Heal 모디파이어에서 `Amount` 증감
- 치유 금지 상태면 `Amount = 0`

---

### 2-7. DOT/HOT 틱 보정

**상황**
- 중독/화상/재생의 틱 수치가 스택/상태에 따라 가변

**적용 방식**
- 틱 호출 전 또는 모디파이어에서 스택 수 기반 재계산
- `SourceId`에 `BurnDOT`, `PoisonDOT`, `RegenHOT`를 남겨 추적

---

### 2-8. PVP/PVE 분리 룰

**상황**
- 플레이어 대상 규칙과 몬스터 대상 규칙이 다름

**적용 방식**
- `Instigator`나 대상 태그/컴포넌트 기반으로 분기
- 예: PVP에서는 연속 피격 감쇠 적용

---

## 3) 권장 적용 순서(파이프라인)

모디파이어는 순서가 중요합니다. 일반적으로 아래 순서를 권장합니다.

1. **면역/차단 판정**
   - 무적, 상태 면역, 치유 금지
2. **피해/회복 배율 계산**
   - 난이도, 버프/디버프, 치명타
3. **자원 선소모 계산**
   - Shield 선소모
4. **최종 안전 보정**
   - 음수 방지, SourceId 보정

> 참고: 현재 `HealthCore`는 모디파이어 이후 음수 Amount를 방어하고, SourceId 공백 폴백을 수행합니다.

---

## 4) 실제 적용 패턴

아래 코드는 프로젝트 내 기존 `HealthComponent` API를 그대로 사용합니다.

```csharp
using UnityEngine;

/// <summary>
/// 데미지 모디파이어 등록/해제를 관리하는 예시 컴포넌트입니다.
/// </summary>
public class DamageModifierBinderSample : MonoBehaviour
{
    [SerializeField] private HealthComponent _health; // 모디파이어를 붙일 체력 컴포넌트
    [SerializeField] private bool _isInvincible; // 무적 상태 여부
    [SerializeField] private float _damageScale = 1f; // 최종 피해 배율

    private float _shieldAmount = 30f; // 남아 있는 실드 수치

    /// <summary>
    /// 실행 시점에 데미지 모디파이어를 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_health == null)
        {
            _health = GetComponent<HealthComponent>();
        }

        if (_health == null)
        {
            Debug.LogWarning($"[DamageModifierBinderSample] HealthComponent not found on {name}.");
            return;
        }

        _health.AddDamageModifier(ApplyDamageRules);
    }

    /// <summary>
    /// 비활성화 시점에 데미지 모디파이어를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_health == null)
        {
            return;
        }

        _health.RemoveDamageModifier(ApplyDamageRules);
    }

    /// <summary>
    /// 데미지 컨텍스트를 가공해 최종 반영 수치를 결정합니다.
    /// </summary>
    private DamageContext ApplyDamageRules(DamageContext context, HealthCore core)
    {
        // 1) 무적 처리 (관통 허용 플래그가 없을 때만 차단)
        if (_isInvincible && !context.IgnoreInvincibility)
        {
            context.Amount = 0f;
            return context;
        }

        // 2) 배율 적용
        if (_damageScale < 0f)
        {
            Debug.LogWarning($"[DamageModifierBinderSample] Invalid damage scale({_damageScale}) on {name}. Fallback to 0.");
            _damageScale = 0f;
        }

        context.Amount *= _damageScale;

        // 3) Shield 선소모
        if (_shieldAmount > 0f && context.Amount > 0f)
        {
            float absorbed = Mathf.Min(_shieldAmount, context.Amount);
            _shieldAmount -= absorbed;
            context.Amount -= absorbed;
        }

        // 4) 안전 보정
        if (context.Amount < 0f)
        {
            Debug.LogWarning($"[DamageModifierBinderSample] Negative damage after rules on {name}. Fallback to 0.");
            context.Amount = 0f;
        }

        return context;
    }
}
```

---

## 5) Heal 모디파이어 적용 패턴

```csharp
using UnityEngine;

/// <summary>
/// 회복 모디파이어 등록/해제를 관리하는 예시 컴포넌트입니다.
/// </summary>
public class HealModifierBinderSample : MonoBehaviour
{
    [SerializeField] private HealthComponent _health; // 모디파이어를 붙일 체력 컴포넌트
    [SerializeField] private bool _isHealBlocked; // 회복 금지 상태 여부
    [SerializeField] private float _healScale = 1f; // 최종 회복 배율

    /// <summary>
    /// 실행 시점에 회복 모디파이어를 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_health == null)
        {
            _health = GetComponent<HealthComponent>();
        }

        if (_health == null)
        {
            Debug.LogWarning($"[HealModifierBinderSample] HealthComponent not found on {name}.");
            return;
        }

        _health.AddHealModifier(ApplyHealRules);
    }

    /// <summary>
    /// 비활성화 시점에 회복 모디파이어를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_health == null)
        {
            return;
        }

        _health.RemoveHealModifier(ApplyHealRules);
    }

    /// <summary>
    /// 회복 컨텍스트를 가공해 최종 반영 수치를 결정합니다.
    /// </summary>
    private HealContext ApplyHealRules(HealContext context, HealthCore core)
    {
        // 1) 회복 금지 상태 처리
        if (_isHealBlocked)
        {
            context.Amount = 0f;
            return context;
        }

        // 2) 배율 적용
        if (_healScale < 0f)
        {
            Debug.LogWarning($"[HealModifierBinderSample] Invalid heal scale({_healScale}) on {name}. Fallback to 0.");
            _healScale = 0f;
        }

        context.Amount *= _healScale;

        // 3) 안전 보정
        if (context.Amount < 0f)
        {
            Debug.LogWarning($"[HealModifierBinderSample] Negative heal after rules on {name}. Fallback to 0.");
            context.Amount = 0f;
        }

        return context;
    }
}
```

---

## 6) 모디파이어 설계 체크리스트

- [ ] 모디파이어가 **하나의 책임**만 갖는가?
- [ ] 등록/해제를 `OnEnable/OnDisable`에서 짝으로 처리했는가?
- [ ] 음수 값, 비정상 값 발생 시 `Debug.LogWarning`으로 경고를 남기는가?
- [ ] 순서 의존성이 있는 모디파이어를 문서화했는가?
- [ ] Player/Enemy 공용 규칙과 전용 규칙을 분리했는가?
- [ ] Core 자체(사망 판정/Clamp)를 중복 구현하지 않았는가?

---

## 7) 운영 팁

1. **문제 추적을 위해 SourceId를 적극 활용**
   - 예: `PlayerSword`, `EnemySkill_Fireball`, `BurnDOT`
2. **초기에는 단순 모디파이어부터 시작**
   - 무적, 실드, 배율 3개만으로도 대부분의 전투 룰 커버 가능
3. **룰이 커지면 모디파이어 그룹화**
   - Damage 그룹 / Heal 그룹 / Boss 전용 그룹
4. **UI는 Listener에서 처리**
   - 모디파이어나 Core에서 UI 직접 접근 금지

---

이 문서를 기준으로, 현재 Health 시스템에 모디파이어를 단계적으로 추가하면 Core 안정성을 유지하면서도 기획 대응 속도를 높일 수 있습니다.