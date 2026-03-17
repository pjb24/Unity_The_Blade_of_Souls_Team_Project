# Action System 일반 동작(애니메이션) 추가 가이드

이 문서는 **일반적인 신규 동작(Action) 및 애니메이션 매핑 추가** 절차에 집중합니다.

> Combo Attack, Boss Pattern 확장은 별도 문서 `ActionSystem_ComboAndBossPattern_Guide.md`를 참고하세요.

---

## 1) 신규 동작(Action) 추가 순서

예시: `Dash`, `Stun`, `Interact`, `Explode`

### Step 1. `E_ActionType`에 신규 액션 추가

1. `E_ActionEnums.cs`를 엽니다.
2. `E_ActionType` enum에 신규 값을 추가합니다.
3. 컴파일 에러 여부를 확인합니다.

---

### Step 2. `ActionRuleProfile` 규칙 추가

1. `Create > ActionSystem > Action Rule Profile`로 Rule 프로필 자산을 생성합니다.
2. 생성한 `ActionRuleProfile`의 `Rules` 배열에 신규 액션 Rule을 추가합니다.
   - `ActionType`
   - `Enabled`
   - `Priority`
   - `IsInterruptible`
   - `AutoCompleteSeconds`
3. 대상 오브젝트의 `ActionController._actionRuleProfile`에 해당 자산을 연결합니다.
4. 인터럽트 정책(누가 누구를 끊는지)을 먼저 설계합니다.

---

### Step 3. `AnimationStateMapProfile` 매핑 추가

1. `Create > ActionSystem > Animation State Map Profile`로 매핑 프로필 자산을 생성합니다.
2. 생성한 `AnimationStateMapProfile`의 `State Maps` 배열에 신규 액션 매핑을 추가합니다.
3. 대상 오브젝트의 `ActionAnimationPresenter._stateMapProfile`에 해당 자산을 연결합니다.
4. `StateName`을 Animator 상태 이름과 정확히 일치시킵니다.
5. 필요 시 `IsOneShot`를 설정합니다.
   - 단발 재생 액션(`Attack`, `Hit`류): `true`
   - 루프/지속 액션(`Idle`, `Move`류): `false`

---

### Step 3-1. Presentation Lock(전환 잠금) 설정

특정 애니메이션 재생 후 마지막 상태를 유지하고 싶다면 `ActionAnimationPresenter`의 잠금 옵션을 사용합니다.

1. `_enablePresentationLock = true`로 설정합니다.
2. `_presentationLockTriggerActions`에 잠금을 시작할 액션을 추가합니다.
   - 예: `Die`
3. `_presentationLockReleaseActions`에 잠금을 해제할 액션을 추가합니다.
   - 예: `Revive`로 쓰고 싶은 임의 액션 타입(예: `Spawn`, `Activate`, `Use`)
4. 잠금 상태에서는 release 목록에 없는 액션 요청 애니메이션 전환이 거부됩니다.

### Step 3-2. `AnimationMarkerProfile` 구성(선택)

Animation Event marker를 데이터 자산으로 관리하려면 marker 프로필을 함께 설정합니다.

1. `Create > ActionSystem > Animation Marker Profile`로 marker 프로필 자산을 생성합니다.
2. `Marker Maps` 배열에 marker별 명령을 추가합니다.
   - `Marker` 문자열
   - `CommandType` (`SetPhase`, `Complete`, `Cancel`, `RequestAction`)
   - `TargetPhase` 또는 `TargetActionType` (명령 타입에 맞게 설정)
3. 대상 오브젝트의 `ActionController._animationMarkerProfile`에 해당 자산을 연결합니다.

### Step 3-3. `ActionHitBridgeProfile` 구성(피격 연동 시)

피격 이벤트를 ActionSystem과 연결한다면 브리지 프로필도 함께 구성합니다.

1. `Create > ActionSystem > Action Hit Bridge Profile`로 브리지 프로필 자산을 생성합니다.
2. `HitActionType / BreakActionType / DeadActionType`를 프로젝트 규칙에 맞게 설정합니다.
3. `BreakStatusTags`를 프로젝트의 브레이크 태그 규칙에 맞게 입력합니다.
4. `ActionHitBridge._hitBridgeProfile`에 해당 자산을 연결합니다.

### Step 4. 완료 처리 방식 선택

신규 액션 완료 처리 방식은 아래 중 하나(또는 혼합)로 설계합니다.

- `AutoCompleteSeconds` 기반
- Animation Event marker의 `complete` 기반(`AnimationMarkerProfile` 또는 레거시 marker)
- `IsOneShot=true` + normalizedTime 감시 기반

> 중복 완료 트리거가 생기지 않도록 우선순위를 명확히 정합니다.

---

### Step 5. 요청 경로 연결

1. Player 입력 테스트는 `ActionSystemInputTest`를 활용합니다.
2. AI/게임플레이 로직은 `ActionController.RequestAction(newAction)`를 호출합니다.
3. 요청 거부 시 로그를 보고 Rule을 점검합니다.

---

### Step 6. 검증 체크리스트

- 요청 허용/거부가 Rule대로 동작하는가?
- 인터럽트 우선순위가 의도와 일치하는가?
- 완료/취소/자동완료가 중복 없이 동작하는가?
- 매핑/참조 누락 시 Warning 로그가 출력되는가?

---

## 2) 신규 액터 타입(Actor Type) 추가 순서

예시: `NPC`, `Trap`, `Summon`, `Projectile`

### Step 1. `E_ActionActorType`에 타입 추가

1. `E_ActionEnums.cs`에서 `E_ActionActorType`에 신규 타입을 추가합니다.
2. 컴파일 에러 여부를 확인합니다.

### Step 2. 타입 전용 Rule 프로필 설계

1. Enabled 액션/Disabled 액션 목록 작성
2. 우선순위/인터럽트/자동완료 시간 설계
3. 기본 액션(`_defaultActionType`) 정의

### Step 3. 오브젝트에 적용

1. `ActionController._actorType`에 신규 타입 지정
2. 설계한 `ActionRuleProfile` 자산 연결
3. 표현 계층(`ActionAnimationPresenter` 또는 대체 리스너) 구성

### Step 4. 타입별 검증

- 허용된 액션만 실행되는지
- 금지 액션 요청이 거부되는지
- 라이프사이클 로그가 정상 출력되는지

---

## 3) 운영 권장 규칙

- 신규 액션/타입은 **Rule 설계 문서화 후 구현**을 권장합니다.
- 허용/비허용은 코드 분기보다 Rule(`Enabled`) 중심으로 관리합니다.
- 예외 흐름은 Warning 로그로 드러나도록 유지합니다.
- 매핑 프로필은 액터 타입(예: Player/Boss)별로 분리해 재사용하는 것을 권장합니다.
