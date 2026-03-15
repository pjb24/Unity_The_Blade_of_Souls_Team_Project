# Health System 적용 가이드

이 문서는 `HealthCore` / `HealthComponent` 기반 체력 시스템을 **Player / Enemy / Boss / 파괴 오브젝트**에 공통 적용하는 방법을 설명합니다.

---

## 1) 시스템 구성 요약

- `HealthCore`
  - 순수 C# 체력 계산 코어입니다.
  - 현재 체력/최대 체력, 데미지/회복 적용, 사망 판정, Clamp, 리스너 알림을 담당합니다.
- `HealthComponent`
  - Unity `MonoBehaviour` 래퍼입니다.
  - Inspector 초기값 노출, `HealthCore` 수명 관리, 외부 API 포워딩을 담당합니다.
- `DamageContext`, `HealContext`
  - 단순 수치 전달이 아닌, 출처/주체/옵션을 포함한 입력 컨텍스트입니다.
- `DamageResult`, `HealResult`, `HealthChangeData`
  - 적용 결과 및 변경 스냅샷 데이터입니다.
- `IHealthListener`
  - 체력 이벤트를 수신하는 인터페이스입니다.
- `HealthDebugListener`
  - 디버그 로그 확인용 샘플 리스너입니다.

---

## 2) 빠른 적용 절차 (Player/Enemy 공통)

1. 체력이 필요한 GameObject에 `HealthComponent`를 추가합니다.
2. Inspector에서 `_initialMaxHealth`, `_initialCurrentHealth`, `_allowHealWhenDead`를 설정합니다.
3. 필요하면 같은 GameObject(또는 다른 오브젝트)에 `HealthDebugListener`를 추가해 로그로 검증합니다.
4. 공격/회복 시스템에서 `ApplyDamage(DamageContext)`, `ApplyHeal(HealContext)`를 호출합니다.
5. UI/VFX/SFX/사망 처리 로직은 `IHealthListener` 구현체에서 분리해 처리합니다.

---

## 3) Player에 적용 예시

아래 예시는 플레이어의 피격/회복을 `HealthComponent`로 위임하는 예시입니다.

```csharp
using UnityEngine;

/// <summary>
/// 플레이어 전투 샘플입니다.
/// </summary>
public class PlayerCombatSample : MonoBehaviour
{
    [SerializeField] private HealthComponent _health; // 플레이어 체력 컴포넌트

    /// <summary>
    /// 참조를 자동 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_health == null)
        {
            _health = GetComponent<HealthComponent>();
        }
    }

    /// <summary>
    /// 외부 공격으로 데미지를 적용합니다.
    /// </summary>
    public void TakeHit(float damageAmount, GameObject attacker)
    {
        DamageContext context = new DamageContext(
            amount: damageAmount,
            instigator: attacker,
            sourceId: "EnemyAttack",
            ignoreInvincibility: false,
            canKill: true,
            damageType: E_DamageType.Physical);

        DamageResult result = _health.ApplyDamage(context);

        if (result.DidCauseDeath)
        {
            Debug.Log("[PlayerCombatSample] Player died.");
        }
    }

    /// <summary>
    /// 아이템/스킬 회복을 적용합니다.
    /// </summary>
    public void HealByPotion(float healAmount)
    {
        HealContext context = new HealContext(
            amount: healAmount,
            instigator: gameObject,
            sourceId: "Potion",
            allowOverheal: false);

        _health.ApplyHeal(context);
    }
}
```

---

## 4) Enemy에 적용 예시

같은 방식으로 적 오브젝트에도 그대로 적용할 수 있습니다.

```csharp
using UnityEngine;

/// <summary>
/// 적 체력 처리 샘플입니다.
/// </summary>
public class EnemyHealthSample : MonoBehaviour, IHealthListener
{
    [SerializeField] private HealthComponent _health; // 적 체력 컴포넌트

    /// <summary>
    /// 리스너를 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_health == null)
        {
            _health = GetComponent<HealthComponent>();
        }

        if (_health != null)
        {
            _health.AddListener(this);
        }
    }

    /// <summary>
    /// 리스너를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_health != null)
        {
            _health.RemoveListener(this);
        }
    }

    /// <summary>
    /// 플레이어 공격을 받아 데미지를 적용합니다.
    /// </summary>
    public void ReceivePlayerAttack(float damageAmount, GameObject player)
    {
        DamageContext context = new DamageContext(
            amount: damageAmount,
            instigator: player,
            sourceId: "PlayerSword",
            ignoreInvincibility: false,
            canKill: true,
            damageType: E_DamageType.Physical);

        _health.ApplyDamage(context);
    }

    /// <summary>
    /// 체력 변경 알림을 처리합니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
        Debug.Log($"[EnemyHealthSample] HP: {data.CurrentHealth}/{data.MaxHealth}");
    }

    /// <summary>
    /// 데미지 알림을 처리합니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// 회복 알림을 처리합니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 사망 알림을 처리합니다.
    /// </summary>
    public void OnDied()
    {
        Debug.Log("[EnemyHealthSample] Enemy died.");
        // 여기에서 드랍/비활성화/풀 반환 등 게임 로직을 연결합니다.
    }

    /// <summary>
    /// 부활 알림을 처리합니다.
    /// </summary>
    public void OnRevived()
    {
    }

    /// <summary>
    /// 최대 체력 변경 알림을 처리합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }
}
```

---

## 5) `HealthDebugListener` 사용 방법

- 같은 GameObject에 `HealthComponent` + `HealthDebugListener`를 함께 붙입니다.
- 또는 `HealthDebugListener`의 `_targetHealth` 필드에 타겟을 직접 할당합니다.
- 플레이 중 체력 변경/데미지/회복/사망/부활 로그를 확인할 수 있습니다.

---

## 6) 데미지/회복 호출 규약

### DamageContext 권장 규약

- `Amount`: 0 이상 값만 전달합니다.
- `SourceId`: 분석 가능한 문자열을 사용합니다. (예: `PlayerSword`, `FireDOT`)
- `CanKill`: 특수 패턴에서 최소 체력 1 유지가 필요하면 `false`를 사용합니다.
- `DamageType`: 게임 룰 확장을 고려해 가능한 한 명시합니다.

### HealContext 권장 규약

- `Amount`: 0 이상 값만 전달합니다.
- `AllowOverheal`: 초과 회복 규칙이 있는 캐릭터에만 `true`를 사용합니다.
- 사망 상태 회복은 `_allowHealWhenDead` 설정 및 게임 규칙과 함께 결정합니다.

---

## 7) Listener 설계 권장 패턴

### 권장

- UI: `OnHealthChanged`로 체력바 갱신
- VFX/SFX: `OnDamaged`, `OnHealed`에서 연출 실행
- 게임 로직: `OnDied`, `OnRevived`에서 상태 전환

### 비권장

- `HealthCore`/`HealthComponent` 내부에서 UI나 게임오버를 직접 처리
- 외부 시스템이 매 프레임 체력을 폴링해서 처리

---

## 8) 확장 포인트 붙이는 방법

### Shield

- 데미지 모디파이어(`AddDamageModifier`)에서 먼저 Shield 값을 차감 후 남은 데미지만 통과시킵니다.

### Invincibility

- 무적 시간 동안 데미지 모디파이어에서 `Amount = 0` 처리하거나, `IgnoreInvincibility` 플래그를 검사해 우회합니다.

### DOT/HoT

- 틱 시스템에서 주기적으로 `ApplyDamage` 또는 `ApplyHeal`을 호출합니다.
- `SourceId`에 `BurnDOT`, `RegenHOT`처럼 원인을 남겨 디버깅합니다.

### Overheal

- `HealContext.AllowOverheal = true`를 사용하는 대상만 초과 회복을 허용합니다.
- UI는 `CurrentHealth / MaxHealth` 외에 별도 오버힐 게이지를 그리도록 분리합니다.

### 정수형 UI 규칙 (혼명등 같은 연출)

- Core는 float를 유지합니다.
- UI 계층에서만 `Mathf.CeilToInt`, `Mathf.FloorToInt`, 구간 스냅 규칙을 적용해 표시값을 변환합니다.

---

## 9) 적용 체크리스트

- [ ] Player/Enemy/Boss/오브젝트에 `HealthComponent`가 붙어 있는가?
- [ ] 체력 변경 반응 로직이 `IHealthListener`로 분리되어 있는가?
- [ ] 외부 공격/회복 코드가 `Context`를 만들어 전달하는가?
- [ ] 경고 로그를 확인해 잘못된 입력이 없는가?
- [ ] 사망/부활/최대체력 변경 처리 정책이 기획과 일치하는가?

---

## 10) 트러블슈팅

### Q1. 데미지가 적용되지 않아요.
- `DamageContext.Amount <= 0`인지 확인합니다.
- 이미 사망 상태인지(`IsDead`) 확인합니다.
- 모디파이어가 0으로 바꾸는지 확인합니다.

### Q2. 회복이 안 돼요.
- 사망 상태에서 `_allowHealWhenDead`가 `false`인지 확인합니다.
- `HealContext.Amount <= 0`인지 확인합니다.

### Q3. 알림이 여러 번 와요.
- 동일 리스너를 중복 등록했는지 확인합니다.
- `OnEnable/OnDisable` 짝이 맞는지 확인합니다.

---