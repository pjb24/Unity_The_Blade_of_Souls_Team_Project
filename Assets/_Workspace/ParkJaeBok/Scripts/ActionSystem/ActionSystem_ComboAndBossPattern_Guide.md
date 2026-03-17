# Action System Combo Attack / Boss Pattern 추가 가이드

이 문서는 **콤보 어택**과 **보스 패턴** 확장 절차를 다룹니다.

> 일반 액션/애니메이션 추가는 `ActionSystem_GeneralAction_Add_Guide.md`를 참고하세요.

---

## 1) Attack Combo 추가 순서

아래 순서는 `ActionComboController` 기준입니다.

### Step 1. 콤보 액션 타입 추가

1. `E_ActionType`에 콤보 단계 액션을 추가합니다.
   - 예: `AttackCombo1`, `AttackCombo2`, `AttackCombo3`

### Step 2. Rule 구성

1. `ActionRuleProfile.Rules`에 콤보 단계 Rule을 추가합니다.
2. 권장 설정
   - `Enabled = true`
   - 단계별 우선순위 설계
   - `IsInterruptible` 정책 정의

> 중요: 콤보 중 피격 끊김 여부는 코드 하드코딩이 아니라 **ActionRule 우선순위/인터럽트 설정**으로 제어합니다.

### Step 3. 애니메이션 매핑

1. `AnimationStateMapProfile.State Maps`에 각 단계 매핑 추가
2. `ActionAnimationPresenter._stateMapProfile`에 해당 매핑 프로필 자산을 연결
3. 콤보 단계는 보통 `IsOneShot = true`

### Step 4. Combo Controller 배치

1. 대상 오브젝트에 `ActionComboController` 추가
2. `_actionController`를 같은 오브젝트의 `ActionController`로 연결
3. `_comboActions` 순서를 실제 콤보 순서와 동일하게 구성

### Step 4-1. 피격 이벤트 브리지 연결(권장)

1. 같은 오브젝트(또는 피격 이벤트를 받는 오브젝트)에 `ActionHitBridge`를 추가합니다.
2. `ActionHitBridge._hitReceiver`를 `HitReceiver`로 연결합니다.
3. `ActionHitBridge._actionController`를 `ActionController`로 연결합니다.
4. `Create > ActionSystem > Action Hit Bridge Profile`로 브리지 프로필 자산을 생성합니다.
5. `ActionHitBridge._hitBridgeProfile`에 생성한 프로필 자산을 연결합니다.
6. 프로필 기본 매핑을 확인합니다.
   - 일반 피격: `HitActionType = Hit`
   - 강인도 감쇄: `BreakActionType = Break`
   - 사망: `DeadActionType = Die`

> 피격 이벤트 소스와 ActionSystem 연결은 `ActionHitBridge` 1개를 통해 일관되게 전달하는 것을 권장합니다.

### Step 5. 플레이어 입력 연결 (중요)

1. Input Actions 에셋에 공격 버튼 Action을 생성합니다.
2. 해당 Action을 `ActionComboController._attackInputAction`에 연결합니다.
3. 입력 처리 전략은 **버퍼링 방식**을 권장합니다.
   - 첫 입력: `AttackCombo1` 요청
   - 콤보 중 추가 입력: `_hasBufferedInput=true`
   - `CloseComboWindow()` 시점에 버퍼 소비 후 다음 단계 요청

### Step 6. Animation Event 윈도우 설정

각 콤보 클립에 Animation Event를 넣습니다.

- 입력 허용 시작 프레임: `OpenComboWindow()`
- 입력 허용 종료 프레임: `CloseComboWindow()`

필요하면 `ActionController._animationMarkerProfile`을 통해 marker 명령을 ScriptableObject로 관리합니다.

### Step 7. 검증

- 1회 입력 시 1단만 실행되는가?
- 입력 허용 구간 내 추가 입력으로 다음 단이 연결되는가?
- 입력 허용 구간 밖 입력은 무시되는가?
- 콤보 도중 피격/브레이크 이벤트 발생 시 Rule 설정대로 콤보가 중단되는가?

---

## 2) Boss Pattern 추가 순서

아래 순서는 `BossPatternController` 기준입니다.

### Step 1. 패턴 액션 타입 추가

1. `E_ActionType`에 보스 패턴 단계 액션을 추가합니다.
   - 예: `BossPatternIntro`, `BossPatternLoopA`, `BossPatternLoopB`, `BossPatternFinish`

### Step 2. 보스 Rule 프로필 구성

1. `ActionController._actorType = Boss`로 설정
2. `ActionController._actionRuleProfile`에 보스 Rule 프로필 자산을 연결하고 패턴 단계 액션 Rule을 활성화
3. 인터럽트 액션(`Hit`, `Break`, `Die`) 우선순위를 패턴보다 높게 설계

> 중요: 보스 패턴 인터럽트도 Rule 우선으로 판단되도록 유지합니다.

### Step 3. 패턴 애니메이션 매핑

1. `AnimationStateMapProfile`에 단계별 매핑 추가
2. `ActionAnimationPresenter._stateMapProfile`에 해당 매핑 프로필 자산을 연결
3. 단발 단계는 `IsOneShot=true`

### Step 4. BossPatternController 배치

1. 보스 오브젝트에 `BossPatternController` 추가
2. `_actionController` 연결
3. `_patterns` 배열 구성
   - `PatternId`
   - `Loop`
   - `Steps[]` (`ActionType`, `DelayBeforeRequest`, `WaitForActionComplete`)

### Step 4-1. 피격 이벤트 브리지 연결(권장)

1. 보스 오브젝트에 `ActionHitBridge`를 추가합니다.
2. `_hitReceiver`를 보스의 `HitReceiver`로 연결합니다.
3. `_actionController`를 보스의 `ActionController`로 연결합니다.
4. `ActionHitBridge._hitBridgeProfile`에 보스용 프로필 자산을 연결합니다.
5. 프로필의 브레이크 판정 태그(`BreakStatusTags`)를 프로젝트 규칙에 맞게 설정합니다.

### Step 5. 인터럽트/강인도 감쇄 처리 설정

1. `_patternInterruptActions`에 인터럽트 트리거 액션 지정
   - 예: `Hit`, `Break`, `Die`
2. `_interruptRecoveryPatternId` 설정(선택)
   - 인터럽트 직후 회복 패턴 재정렬
3. `_restartDefaultPatternAfterInterrupt` 설정
   - 회복 패턴 미사용 시 기본 패턴 복귀 여부

### Step 6. 실행/전환 경로 연결

1. 자동 시작이 필요하면 `_autoStartPattern=true`
2. `_defaultPatternId` 설정
3. AI 코드에서 `StartPattern("PatternId")` 호출로 패턴 전환

### Step 7. 검증

- 단계 대기(`WaitForActionComplete`)가 의도대로 작동하는가?
- 인터럽트(`Hit/Break`) 발생 시 현재 패턴이 즉시 중단되는가?
- 회복 패턴 또는 기본 패턴 복귀가 설정대로 작동하는가?

---

## 3) 설계 팁

- marker 처리 규칙은 `AnimationMarkerProfile` ScriptableObject로 관리하고 `ActionController._animationMarkerProfile`에 연결합니다.
- 전환 잠금(Presentation Lock)이 필요한 연출(예: 사망 후 고정)은 `ActionAnimationPresenter` 옵션으로 처리합니다.
  - 잠금 시작: `_presentationLockTriggerActions`
  - 잠금 해제(Revive 성격): `_presentationLockReleaseActions`

- 콤보와 패턴은 오케스트레이터(`ActionComboController`, `BossPatternController`)가 담당합니다.
- 실제 액션 실행/상태 전이는 `ActionController`가 담당합니다.
- 역할 분리를 유지하면 디버깅과 확장이 훨씬 쉬워집니다.
