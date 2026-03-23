# ActionSystem Player Action 통합 설정 가이드

> 대상 독자
> Unity 기본 사용(컴포넌트 추가, ScriptableObject 생성, InputAction 연결)을 할 수 있는 개발자/기획자

> 이 문서에서 해결하는 문제
> - 이동 상태 기반 액션 전환(Idle/Move/Jump/Falling/...)
> - 상황별 공격 분기(지상/공중/대시/벽/슬라이드)
> - 공격 중 인터럽트 허용/금지 정책 외부화

---

## 0) 시작 전에 이해할 핵심 개념

이 시스템은 **입력/물리/애니메이션**을 분리해서 생각하면 빠르게 이해됩니다.

- 입력/물리: `PlayerMovement`, `PlayerMovementActionSync`, `ActionContextualAttackController`
- 상태 결정: `ActionController`
- 표현(애니메이션): `ActionAnimationPresenter`

즉, 애니메이션은 “결정”이 아니라 “표현”입니다.

추가로 ActionSystem enum은 `E_ActionEnums.cs` 단일 파일에서 관리합니다.

---

## 1) 컴포넌트 역할 요약

### 1-1. `ActionController`
- 액션 요청의 최종 승인/거절을 담당합니다.
- `ActionRuleProfile` + `ActionInterruptPolicyProfile`을 기반으로 인터럽트 여부를 판단합니다.

### 1-2. `PlayerMovementActionSync`
- `PlayerMovement` 상태를 읽어 이동 계열 액션(`Idle/Move/Jump/Land/Dash/Falling/WallSlide/WallJump/Slide`)을 요청합니다.
- 비이동 액션 실행 중 동기화 중단 옵션(`_suspendWhileNonMovementActionRunning`)이 있습니다.

### 1-3. `ActionContextualAttackController`
- 공격 입력이 들어온 순간의 상태를 읽고, `AttackContextRuleProfile` 규칙으로 어떤 공격 액션을 낼지 결정합니다.
- 규칙에 따라 차단/버퍼링/출력 액션 요청을 수행합니다.

### 1-4. `ActionAnimationPresenter`
- `ActionController`의 현재 액션을 애니메이션 상태로 재생합니다.

---

## 2) 준비물(ScriptableObject)

아래 4개를 준비하면 대부분의 플레이어 액션 구성이 가능합니다.

1. `ActionRuleProfile`
2. `AttackContextRuleProfile`
3. `ActionInterruptPolicyProfile`
4. `AnimationStateMapProfile`

---

## 3) 씬/프리팹 적용 절차

아래는 Player 루트 오브젝트 기준입니다.

### Step 1. 필수 컴포넌트 확인

Player 루트에 아래가 있는지 확인합니다.

- `ActionController`
- `PlayerMovement`
- `PlayerMovementActionSync`
- `ActionContextualAttackController`
- `ActionAnimationPresenter`
- `Animator`

### Step 2. `ActionController` 연결

`ActionController` 인스펙터에서:

- `_actionRuleProfile` 연결
- `_interruptPolicyProfile` 연결
- `_ignoreMarkerEndActions` 확인
  (기본적으로 이동 액션들은 marker Complete/Cancel 무시 권장)

### Step 3. `PlayerMovementActionSync` 연결

`PlayerMovementActionSync` 인스펙터에서:

- `_actionController` → Player의 `ActionController`
- `_playerMovement` → Player의 `PlayerMovement`
- `_suspendWhileNonMovementActionRunning` → `true` 권장
- `_landPresentationDuration` / `_moveInputThreshold`는 체감에 맞게 조정

### Step 4. `ActionContextualAttackController` 연결

인스펙터에서:

- `_actionController` 연결
- `_playerMovement` 연결
- `_attackInputRoutes` 배열에 입력 라우트 추가
  - `RouteName` 예: `LightAttack`, `HeavyAttack`, `SkillAttack`
  - `InputAction`에 라우트별 입력 액션 연결
  - `RuleProfile`에 라우트별 `AttackContextRuleProfile` 연결
- 초기 디버깅 시 `_enableRuntimeLog = true` 권장
- 단일 입력 프로젝트도 `_attackInputRoutes`에 라우트 1개를 추가해 동일한 방식으로 구성합니다.
- `_attackInputRoutes`는 필수 설정 항목입니다.

### Step 5. `ActionAnimationPresenter` 연결

- `_stateMapProfile`에 `AnimationStateMapProfile` 연결
- 공격/이동 신규 액션이 추가되면 매핑도 함께 추가

---

## 4) 공격 분기 규칙 작성법 (`AttackContextRuleProfile`)

규칙은 “위에서 아래가 아니라” **Priority(숫자 큰 값 우선)** 로 선택됩니다.

각 규칙에서 주로 보는 필드:

- `RequiredFlags`
- `ExcludedFlags`
- `AllowedCurrentActions`
- `RequireComboWindowOpen`
- `RequireHitWindowOpen`
- `BlockAttack`
- `BufferWhenBlocked`
- `OutputActionType`

### 권장 시작 템플릿

#### 규칙 A: 슬라이딩 중 공격 금지
- `RequiredFlags = Sliding`
- `BlockAttack = true`
- `Priority = 100`

#### 규칙 B: 대시 공격
- `RequiredFlags = Dashing`
- `OutputActionType = AttackDash`
- `Priority = 90`

#### 규칙 C: 공중 공격
- `RequiredFlags = Airborne`
- `ExcludedFlags = Dashing | WallSliding`
- `OutputActionType = AttackAir`
- `Priority = 80`

#### 규칙 D: 벽 상호작용 공격
- `RequiredFlags = WallSliding`
- `OutputActionType = AttackWall`
- `Priority = 85`

#### Fallback
- 규칙이 하나도 매칭되지 않으면 `FallbackActionType` 사용 (일반적으로 `Attack`)

---

## 5) 인터럽트 정책 작성법 (`ActionInterruptPolicyProfile`)

공격 중 이동 허용/금지 같은 정책은 여기서 관리합니다.

`ActionController.CanInterrupt()`의 실제 순서:

1. InterruptPolicy 규칙 평가
2. `Allow`/`Deny`면 즉시 적용
3. 규칙 미매칭 또는 `UseDefault`면 `ActionRuleProfile`로 fallback

### 자주 쓰는 정책 예시

#### 정책 1: 공격 중 이동 금지
- `CurrentActionType = Attack`
- `RequestedActionType = Move`
- `Decision = Deny`
- `Priority = 100`

#### 정책 2: 콤보 창에서만 다음 공격 허용
- `CurrentActionType = Attack`
- `RequestedActionType = Attack`
- `Decision = Allow`
- `RequireComboWindowOpen = true`
- `Priority = 120`

#### 정책 3: 피격은 항상 허용
- `CurrentActionType = None` (와일드카드)
- `RequestedActionType = Hit`
- `Decision = Allow`
- `Priority = 1000`

---

## 6) Animation Event(마커) 권장 운영

이 시스템에서 marker는 “타이밍 신호” 용도로 사용합니다.

- 콤보 입력 창: `ComboStart`, `ComboEnd`
- 히트 창: `HitStart`, `HitEnd`

이동 액션 완료를 marker로 끝내려 하지 말고, 이동은 `PlayerMovementActionSync`가 주도하도록 유지하세요.

---

## 7) 신규 공격 액션 추가 체크리스트

예: `AttackUppercut` 추가 시

1. `E_ActionType`에 항목 추가
2. `ActionRuleProfile`에 Rule 추가
3. `AttackContextRuleProfile`에 분기 규칙 추가
4. `ActionInterruptPolicyProfile`에 인터럽트 정책 추가
5. `AnimationStateMapProfile` 매핑 추가
6. `AttackActionMap`에 `AttackSpec` 연결

---

## 8) 입력 오케스트레이션 운영 원칙

- 공격 입력 오케스트레이션은 `ActionContextualAttackController` 단일 경로를 사용하세요.
- 같은 공격 입력 액션을 여러 컨트롤러에서 동시에 구독하지 않도록 유지하세요.

---

## 9) 트러블슈팅

### 증상 A. 공격 입력이 무시됨
- `AttackContextRuleProfile`에서 `BlockAttack=true` 규칙이 매칭되는지 확인
- `ActionInterruptPolicyProfile`에서 `Deny`가 매칭되는지 확인
- `_attackInputRoutes`의 `InputAction`/`RuleProfile` 연결 확인

### 증상 B. 공격이 Move/Idle로 덮임
- `PlayerMovementActionSync._suspendWhileNonMovementActionRunning = true` 확인
- `ActionRuleProfile` 우선순위/인터럽트 가능 여부 확인

### 증상 C. 콤보가 이어지지 않음
- 클립 marker `ComboStart/ComboEnd` 타이밍 확인
- 규칙의 `RequireComboWindowOpen` 조건 확인
- 다음 단계 공격 규칙의 `AllowedCurrentActions` 확인

### 증상 D. 대시 공격이 지상 공격으로 나감
- `Dashing` 규칙 우선순위를 더 높게 설정
- `RequiredFlags`/`ExcludedFlags` 충돌 여부 확인

---

## 10) 문서 맵

- 일반 액션 추가: `ActionSystem_GeneralAction_Add.md`
- 콤보/보스 패턴: `ActionSystem_ComboAndBossPattern_Guide.md`
- 전체 확장 인덱스: `ActionSystem_Extension_Workflow_Guide.md`
