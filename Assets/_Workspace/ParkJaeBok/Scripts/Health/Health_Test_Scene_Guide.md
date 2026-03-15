# Health Test Scene 상세 구성 가이드

이 문서는 현재 프로젝트의 Health 시스템(`HealthCore`, `HealthComponent`, Listener, Modifier)을 검증하기 위한 **테스트 전용 Scene 구성 방안**을 설명합니다.

> 주의: 이 문서는 Scene 구성 방법만 다룹니다. `.unity`, `.prefab`, `.meta` 파일은 생성하지 않습니다.

---

## 1) 목표

테스트 Scene의 목표는 아래 4가지를 빠르게 검증하는 것입니다.

1. **기본 수치 동작**: 데미지/회복/사망/부활/최대체력 변경
2. **예외 방어**: 음수 입력, 비정상 SourceId, 중복 등록
3. **이벤트 검증**: `OnHealthChanged`, `OnDamaged`, `OnHealed`, `OnDied`, `OnRevived`, `OnMaxHealthChanged`
4. **모디파이어 검증**: 무적, 배율, 실드 선소모, 힐 차단, 순서 의존성

---

## 2) 추천 Scene 이름/레이아웃

### Scene 이름

- `Health_Test_Lab` (권장)

### Hierarchy 루트 구조

- `Health_Test_Lab_Root`
  - `Targets`
    - `Player_Target`
    - `Enemy_Target`
    - `Boss_Target`
    - `Destructible_Target`
    - `Modifier_Target`
  - `Runners`
    - `Basic_Test_Runner`
    - `InputValidation_Test_Runner`
    - `Modifier_Test_Runner`
  - `UI` (선택)
    - `Debug_Text` (선택)

---

## 3) 타겟 오브젝트 구성

각 Target 오브젝트는 최소 아래 컴포넌트를 가집니다.

1. `HealthComponent`
2. `HealthDebugListener`

이렇게 구성하면 Health 이벤트가 콘솔로 출력되어 테스트 결과를 즉시 확인할 수 있습니다.

### 권장 초기값

| Target | _initialMaxHealth | _initialCurrentHealth | _allowHealWhenDead | 테스트 목적 |
|---|---:|---:|---:|---|
| Player_Target | 100 | 100 | false | 일반 전투 흐름 |
| Enemy_Target | 60 | 60 | false | 중간 체력 타겟 |
| Boss_Target | 500 | 500 | false | 고체력/누적 검증 |
| Destructible_Target | 30 | 30 | false | 저체력/빠른 사망 |
| Modifier_Target | 100 | 100 | true | 부활/오버힐/모디파이어 집중 검증 |

---

## 4) 테스트 러너 역할 분리

### 4-1. Basic_Test_Runner

**검증 항목**

- `ApplyDamage` 정상 반영
- `ApplyHeal` 정상 반영
- `SetCurrentHealth` 동작
- `SetMaxHealth` 동작 (`adjustCurrentHealth` true/false)
- `Revive` 동작

**권장 시나리오**

1. Player에 25 데미지
2. Player에 10 힐
3. Enemy에 치사 데미지
4. Enemy 부활
5. Boss 최대 체력 감소 후 Clamp 확인
6. Destructible 즉시 파괴

---

### 4-2. InputValidation_Test_Runner

**검증 항목**

- 음수 데미지 입력 시 차단/Warning
- 음수 회복 입력 시 차단/Warning
- 빈 SourceId 폴백 Warning
- 중복 리스너 등록 Warning
- null 리스너/모디파이어 등록 경고

**권장 시나리오**

1. `DamageContext.Amount = -10`
2. `HealContext.Amount = -5`
3. `SourceId = ""`
4. 동일 리스너 2회 Add 시도
5. null Add/Remove 시도

---

### 4-3. Modifier_Test_Runner

**검증 항목**

- 무적 처리 (`Amount=0`)
- 데미지 배율(0.5x, 2.0x)
- Shield 선소모
- Heal 차단
- 모디파이어 순서 변경 시 결과 차이

**권장 파이프라인 순서**

1. 면역/차단
2. 배율
3. 자원 선소모(Shield)
4. 안전 보정

---

## 5) 테스트 케이스 표 (실행 체크리스트)

아래 표를 복사해 QA 체크 시 사용하세요.

| ID | 케이스 | 입력 | 기대 결과 |
|---|---|---|---|
| TC-01 | 기본 데미지 | Amount=10 | CurrentHealth 10 감소, OnDamaged 호출 |
| TC-02 | 기본 회복 | Amount=10 | CurrentHealth 10 증가, OnHealed 호출 |
| TC-03 | 치사 데미지 | Amount=999 | CurrentHealth=0, OnDied 1회 |
| TC-04 | 부활 | Revive(30) | CurrentHealth=30, OnRevived 1회 |
| TC-05 | Max 감소 Clamp | SetMaxHealth(50,false) | CurrentHealth>50이면 50으로 Clamp |
| TC-06 | 음수 데미지 방어 | Amount=-1 | 적용량 0, Warning 로그 |
| TC-07 | 음수 회복 방어 | Amount=-1 | 적용량 0, Warning 로그 |
| TC-08 | CanKill false | Amount=999, CanKill=false | 최소 1 유지 |
| TC-09 | 무적 처리 | Invincible=true | 데미지 0 처리 |
| TC-10 | Shield 소모 | Shield=30, Damage=20 | HP 변화 없음, Shield 10 |
| TC-11 | Heal 차단 | HealBlocked=true | 힐 적용 0 |
| TC-12 | 사망 중 힐 제한 | allowHealWhenDead=false | 힐 차단 + Warning |

---

## 6) 로그 검증 기준

`HealthDebugListener` 로그를 기준으로 아래 항목을 확인합니다.

1. 이벤트 발생 횟수
   - `OnDied`, `OnRevived` 중복 발생 여부
2. 수치 일관성
   - `PreviousHealth`, `CurrentHealth`, `AppliedAmount` 일치 여부
3. 블록/무효 플래그
   - `IsBlocked`, `IsInvalid`, `HasWarningFallback`
4. 최대 체력 변경
   - `OnMaxHealthChanged`의 이전/현재 값 일치

---

## 7) 테스트 실행 단계(권장 루틴)

### 단계 A: 스모크 테스트

- 각 Target에 데미지 1회/회복 1회씩 적용
- 콘솔에 이벤트 로그가 정상 출력되는지 확인

### 단계 B: 상태 전이 테스트

- 0까지 데미지 → 사망 이벤트
- Revive 호출 → 부활 이벤트
- 다시 치사 데미지 → 재사망 이벤트

### 단계 C: 방어 입력 테스트

- 음수 데미지/회복
- 비어 있는 SourceId
- 잘못된 max/current 설정

### 단계 D: 모디파이어 테스트

- 무적 ON/OFF
- 배율 변경
- Shield 값 변화
- HealBlock ON/OFF

### 단계 E: 회귀 테스트

- 위 단계 A~D 반복 후, 리스너 중복/누수 여부 확인

---

## 8) 테스트용 입력 방식 제안

### 방법 1: 키보드 단축키 기반 Runner (빠른 반복)

- `1`: Player 데미지
- `2`: Player 힐
- `3`: Enemy 치사 데미지
- `4`: Enemy 부활
- `5`: Modifier Target 무적 토글
- `6`: Modifier Target Shield 리셋

### 방법 2: UI 버튼 기반 Runner (비개발자 QA 친화)

- 버튼 클릭으로 케이스 실행
- 버튼 이름에 TC-ID 표시 (예: `TC-03 Kill Enemy`)

---

## 9) 흔한 실수와 예방책

### 실수 1: 리스너 중복 등록

- 증상: 이벤트가 2번 이상 호출됨
- 예방: `OnEnable`에서 Add, `OnDisable`에서 Remove 짝 유지

### 실수 2: 모디파이어 해제 누락

- 증상: 비활성화 후에도 룰이 남아 있는 것처럼 보임
- 예방: 반드시 `OnDisable` 해제

### 실수 3: 테스트 입력 SourceId 누락

- 증상: 디버깅 어려움
- 예방: `SourceId`를 케이스 단위로 명확히 부여

### 실수 4: MaxHealth 감소 후 기대값 착오

- 증상: 현재 체력 갑자기 줄어든 것처럼 보임
- 예방: `adjustCurrentHealth` 옵션과 Clamp 동작을 명확히 구분

---

## 10) 테스트 완료 기준 (Done Definition)

아래 항목을 모두 만족하면 테스트 Scene 준비가 완료된 것으로 봅니다.

- [ ] 5개 Target(Player/Enemy/Boss/Destructible/Modifier) 구성 완료
- [ ] 3개 Runner(Basic/InputValidation/Modifier) 동작 확인
- [ ] TC-01~TC-12 전부 기대 결과 충족
- [ ] 경고/오류 로그가 의도된 케이스에서만 발생
- [ ] 리스너/모디파이어 등록 누수 없음
- [ ] 테스트 절차 문서(본 문서) 기반 재현 가능

---

## 11) 확장 권장 항목 (선택)

1. **자동 리그레션 러너**
   - Play Mode 시작 시 TC 순차 실행 후 결과 요약 출력
2. **CSV/JSON 로그 출력**
   - 테스트 결과를 파일로 남겨 회귀 비교
3. **대상별 프리셋**
   - Player/Enemy/Boss 룰 세트를 ScriptableObject로 분리
4. **피격 반응 시스템 연동 테스트**
   - HitStop/Knockback/애니메이션 이벤트 동기화 확인

---

이 가이드를 기준으로 Scene을 구성하면, 현재 Health 시스템의 핵심 동작과 예외 처리, 확장 포인트(Modifier)까지 한 번에 검증할 수 있습니다.