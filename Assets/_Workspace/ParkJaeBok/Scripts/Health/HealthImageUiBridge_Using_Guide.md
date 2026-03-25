# HealthImageUiBridge 사용 가이드

이 문서는 `HealthImageUiBridge`를 이용해 `HealthComponent`의 체력 데이터를 UI `Image` 5칸(슬롯)으로 표시하는 방법을 설명합니다.

---

## 1) 개요

`HealthImageUiBridge`는 `IHealthListener`를 구현한 UI 브리지 컴포넌트입니다.

- `HealthComponent`의 이벤트(`OnHealthChanged`, `OnDied`, `OnRevived`, `OnMaxHealthChanged`)를 구독합니다.
- 현재 체력/최대 체력을 비율로 계산해 슬롯 이미지를 켜고 끕니다.
- 체력 0이면 모든 슬롯을 끄고, 최대 체력이면 모든 슬롯을 켭니다.
- `HealthComponent` 초기화가 늦는 경우를 위해 재시도 등록 로직을 제공합니다.

---

## 2) 사전 준비

- Player(또는 대상 오브젝트)에 `HealthComponent`가 있어야 합니다.
- UI Canvas에 체력 슬롯으로 사용할 `Image` 5개를 준비합니다.
- UI 오브젝트(예: `PlayerHealthUI`)에 `HealthImageUiBridge`를 추가합니다.

---

## 3) Inspector 필드 설명

### References

- `_targetHealth`
  - 구독할 `HealthComponent`입니다.
  - 비어 있으면 `GetComponent`, `GetComponentInParent` 순으로 자동 탐색합니다.

- `_healthImages`
  - 체력 표시용 슬롯 이미지 배열입니다.
  - 요구사항 기준으로 5개를 순서대로 할당합니다.
  - 인덱스가 낮을수록 먼저 켜지는 슬롯입니다.

### Register Retry

- `_retryInterval`
  - `HealthComponent` 준비 대기 중 등록 재시도 간격(초)입니다.

- `_maxRetryCount`
  - 리스너 등록 재시도 횟수입니다.

### Display

- `_slotCount`
  - 체력 슬롯 수입니다.
  - 기본값은 5이며 요구사항도 5칸 기준입니다.

---

## 4) 체력 매핑 규칙

브리지는 다음 규칙으로 슬롯 ON/OFF를 계산합니다.

1. `maxHealth <= 0`이면 모든 슬롯 OFF
2. `currentHealth <= 0`이면 모든 슬롯 OFF
3. 그 외에는 아래 수식으로 켜질 슬롯 수 계산

```text
normalized = clamp01(currentHealth / maxHealth)
filledSlots = ceil(normalized * slotCount)
filledSlots = clamp(filledSlots, 0, slotCount)
```

예시(슬롯 5칸 기준):

- 100/100 -> 5칸 ON
- 60/100 -> 3칸 ON
- 1/100 -> 1칸 ON
- 0/100 -> 0칸 ON

---

## 5) 이벤트 기반 동작 흐름

1. `OnEnable`에서 등록 코루틴 시작
2. `HealthComponent` 참조 해석 + `IsInitialized` 대기
3. 준비 완료 시 `AddListener(this)` 등록
4. 등록 직후 현재 체력값을 읽어 UI 1회 강제 동기화
5. 이후 체력 이벤트 발생 시 즉시 UI 갱신
6. `OnDisable`/`OnDestroy`에서 리스너 해제

---

## 6) 연결 절차 (권장 순서)

1. 대상(플레이어) 오브젝트에 `HealthComponent`를 설정합니다.
2. UI 오브젝트에 `HealthImageUiBridge`를 붙입니다.
3. `_targetHealth`에 플레이어의 `HealthComponent`를 연결합니다.
4. `_healthImages`에 슬롯 이미지 5개를 순서대로 할당합니다.
5. `_slotCount`를 5로 유지합니다.
6. 플레이 모드에서 피격/회복을 발생시켜 슬롯 변화 확인합니다.

---

## 7) 로그/문제 해결 가이드

### 문제: 슬롯이 갱신되지 않음

- `_targetHealth`가 비어 있거나 탐색 실패했는지 확인합니다.
- `HealthComponent`가 실제로 활성 상태인지 확인합니다.
- `_maxRetryCount`, `_retryInterval`이 비정상 값인지 확인합니다.

### 문제: 일부 슬롯만 동작

- `_healthImages` 길이가 `_slotCount` 이상인지 확인합니다.
- 배열 내 null 참조가 없는지 확인합니다.

### 문제: 체력 0인데 슬롯이 남음

- 브리지는 `currentHealth <= 0`일 때 강제로 0칸 처리합니다.
- 외부에서 체력 값이 예상과 다르게 유지되는지 `HealthDebugListener`로 확인합니다.

---

## 8) 확장 포인트

- 저체력(예: 20% 이하)일 때 슬롯 색상을 변경하는 연출 추가
- `OnDamaged`/`OnHealed`에서 슬롯 깜빡임 애니메이션 트리거
- 이미지 ON/OFF 대신 FillAmount를 사용하는 연속형 UI로 교체

---

## 9) 테스트 체크리스트

- [ ] 최대 체력에서 5칸이 모두 켜진다.
- [ ] 체력 0에서 5칸이 모두 꺼진다.
- [ ] 중간 체력에서 비율에 맞는 칸 수가 켜진다.
- [ ] 최대 체력 변경 시 즉시 슬롯 계산이 갱신된다.
- [ ] 오브젝트 비활성/파괴 시 리스너 해제 경고/오류가 발생하지 않는다.

---

## 10) 관련 스크립트

- `Assets/_Workspace/ParkJaeBok/Scripts/Health/HealthImageUiBridge.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/Health/HealthComponent.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/Health/HealthCore.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/Health/IHealthListener.cs`
