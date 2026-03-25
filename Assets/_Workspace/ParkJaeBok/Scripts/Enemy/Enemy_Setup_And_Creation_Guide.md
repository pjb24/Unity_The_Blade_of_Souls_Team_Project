# Enemy 기본 설정 및 제작 가이드

이 문서는 현재 프로젝트의 Enemy 프레임워크(`EnemyBrain`, `EnemyArchetypeData`, `EnemyMovementDriver`, `EnemySfxBridge`)를 사용해
**새 Enemy를 만드는 표준 절차**를 설명합니다.

---

## 1) 공통 개요

Enemy는 다음 역할 분리 구조를 기본으로 합니다.

- **Brain**: 상태 전환(Spawn/Idle/Patrol/Chase/Attack/Recover/HitStun/Dead)
- **Movement**: 이동 실행
- **Action/Attack/Hit/Health**: 전투/판정/피격/체력
- **SFX/VFX**: 연출 브리지
- **Targeting**: 타겟 획득 전략

---

## 2) 필수 컴포넌트 체크리스트

Enemy GameObject에 아래 컴포넌트를 추가합니다.

### Core
- `EnemyBrain`
- `EnemyMovementDriver`
- `EnemyMovementActionSync`
- `ActionController`
- `AttackExecutor`
- `HitReceiver`
- `HealthComponent`
- `EnemySfxBridge`

### Optional
- `NearestTargetByTagProvider` (기본 타겟팅)
- `ActionHitBridge` (피격->액션 변환)
- `HitVfxBridge`, `HealthVfxBridge`, `ActionVfxBridge`

---

## 3) EnemyArchetypeData 생성 및 연결

1. Project 창에서 `Create > Enemy > Enemy Archetype Data` 생성
2. `EnemyBrain._archetype`에 연결
3. 아래 항목을 타입별로 설정

### Identity
- `ArchetypeId`: 식별 이름 (`Enemy_Melee_A`, `Enemy_Ranged_B` 등)

### Movement
- `MoveSpeed`
- `StoppingDistance`

### Perception
- `DetectRange`
- `LoseRange`

### Combat
- `AttackRange`
- `AttackCooldown`
- `RecoverDuration`
- `HitStunDuration`

### Leash
- `UseLeash`
- `LeashDistance`

### Patrol
- `UsePatrol`
- `PatrolRadius`
- `PatrolWaitTime`

#### Patrol Route Provider(선택)
- `EnemyPatrolRouteProvider`를 Enemy 오브젝트에 추가하면 순찰 지점을 **명시적 경로 기반**으로 제어할 수 있습니다.
- 디자이너 워크플로우
  1. Enemy 하위에 빈 GameObject들을 생성 (`PatrolPoint_01`, `PatrolPoint_02` 등)
  2. 각 자식의 위치를 순찰 포인트로 배치
  3. `EnemyPatrolRouteProvider`의 `Collect Route Points From Children` 실행(또는 Awake 자동 수집 옵션 사용)
  4. 기본값인 `Use World Position Snapshot`을 유지해 초기 월드 좌표를 고정하면 Enemy 이동 시에도 포인트가 같이 끌려가지 않습니다.
- 순찰 모드
  - `Loop`: 끝까지 간 뒤 처음으로 반복
  - `PingPong`: 끝점에서 역방향으로 왕복
  - `RandomNoRepeat`: 직전 포인트를 제외하고 랜덤 선택
- 경로가 비어 있거나 null만 있을 때 fallback
  - `SpawnRandom`: 기존 반경 기반 랜덤 목적지
  - `IdleFixed`: 현재 위치 고정(Idle 성격)

#### Patrol Recovery(순찰 정체 복구)
- `EnemyBrain`의 `Patrol Recovery` 섹션으로 순찰 정체(stuck) 복구를 제어할 수 있습니다.
  - `Enable Patrol Stuck Recovery`
  - `Patrol Progress Distance Threshold`
  - `Patrol Stuck Time Threshold`
  - `Patrol Stuck Recover Cooldown`
  - `Patrol Bypass Hold Time`
- 동작 요약
  1. 순찰 중 목적지 접근 진행량이 임계치 이하로 일정 시간 유지되면 stuck 판정
  2. `EnemyMovementDriver.TryGetEdgeBypassCandidate(...)` 우회 후보를 우선 사용
  3. 우회 후보가 없으면 RouteProvider/랜덤 목적지로 fallback

### Action Mapping
- `SpawnAction`
- `IdleAction`
- `PatrolAction`
- `MoveAction`
- `AttackAction`
- `RecoverAction`
- `HitStunAction`
- `DeadAction`

### Locomotion (지상/부유 전환)
- `DefaultLocomotionType`
- `CanSwitchLocomotion`
- `SwitchCooldown`
- `MinAirTime`

#### Locomotion - Grounded
- `GroundProbeDistance`
- `SlopeLimit`
- `EdgeStopDistance`

#### Locomotion - Floating
- `HoverHeight`
- `HoverDamping`
- `VerticalMaxSpeed`
- `MaxAirTime`

> 참고: 위 값은 런타임에 `EnemyMovementDriver`, `LocomotionDecisionPolicy`, `SafeLandingResolver`로 자동 반영됩니다.

#### Floating 전용 Enemy Y축 추적
- `EnemyBrain > Floating Altitude Follow`
  - `Follow Move Target Y When Floating`: 이동 목표 Y를 따라 고도 명령을 매 프레임 갱신합니다.
  - `Floating Move Target Y Offset`: 목표 Y에 더할 오프셋 값입니다.
  - `Resolved Floating Altitude Command`: 최종 계산 고도 디버그 값입니다.

> Floating 전용 Enemy가 플레이어를 X축만 따라가고 Y축 추적을 못 할 때 위 항목을 먼저 점검합니다.

#### EnemyMovementDriver Grounded Auto Tuning 체크
- `EnemyMovementDriver > Grounded Auto Tuning`
  - `Auto Tune Ground Snap By Collider`
  - `Ground Reference Collider`
  - `Probe Distance By Collider Height`
  - `Snap Distance By Collider Height`
  - `Edge Stop By Collider Width`
  - `Auto Tune Collider Size Epsilon`
  - `Auto Tune Ground Probe Offset By Collider`
  - `Probe Offset Lift By Collider Height`
  - `Auto Probe Offset Lift Min/Max`
- Runtime 디버그 확인 값
  - `Resolved Ground Probe Distance`
  - `Resolved Ground Snap Distance`
  - `Resolved Edge Probe Forward Offset`
  - `Ground Collider Bounds Min Y`

---

## 4) ActionController 설정

1. `ActionRuleProfile` 연결
2. Enemy에서 사용할 액션(`Idle`, `Move`, `Attack`, `Hit`, `Die` 등)을 Enable
3. `Priority`, `IsInterruptible` 정책 점검
4. 기본 액션을 `Idle` 또는 `Spawn` 규칙에 맞춰 지정

### 4-1) ActionController용 SO 생성 방법 (예시)

아래 ScriptableObject는 ActionController 품질을 좌우하므로 Enemy 제작 시 기본 세트로 함께 생성하는 것을 권장합니다.

1. `Create > ActionSystem > Action Rule Profile`
   - 파일명 예시: `ARP_Enemy_Melee_Basic`
2. `Create > ActionSystem > Action Interrupt Policy Profile`
   - 파일명 예시: `AIPP_Enemy_Melee_Basic`
3. `Create > ActionSystem > Animation State Map Profile`
   - 파일명 예시: `ASMP_Enemy_Melee_Basic`
4. (선택) `Create > ActionSystem > Action Hit Bridge Profile`
   - 파일명 예시: `AHBP_Enemy_Melee_Basic`

#### ActionRuleProfile 권장 입력값 예시

- `Idle`: Enabled=true, Priority=0, IsInterruptible=true
- `Move`: Enabled=true, Priority=1, IsInterruptible=true
- `Attack`: Enabled=true, Priority=10, IsInterruptible=false, AutoCompleteSeconds=0(마커 기반 완료 권장)
- `Hit`: Enabled=true, Priority=20, IsInterruptible=false
- `Die`: Enabled=true, Priority=100, IsInterruptible=false

#### ActionInterruptPolicyProfile 권장 정책 예시

- `Attack -> Hit`: Allow
- `Attack -> Die`: Allow
- `Move -> Attack`: Allow
- `Hit -> Attack`: Deny (경직 유지 의도 시)

#### AnimationStateMapProfile 예시

- `Idle` -> Animator State: `Idle`
- `Move` -> Animator State: `Run`
- `HoverIdle` -> Animator State: `Enemy_HoverIdle_Test` (또는 프로젝트 표준 Hover Idle 상태)
- `FlyMove` -> Animator State: `Enemy_FlyMove_Test` (또는 프로젝트 표준 Fly Move 상태)
- `Attack` -> Animator State: `Attack_A`
- `Hit` -> Animator State: `Hit`
- `Die` -> Animator State: `Die`

> 핵심: Rule/Interrupt/AnimationMap 3종을 한 세트로 맞춰야 런타임 액션 불일치가 줄어듭니다.
>
> 부유 Enemy 사용 시 `ActionRuleProfile`과 `AnimationStateMapProfile`에 `HoverIdle`, `FlyMove`를 반드시 추가해 액션 요청 실패를 방지합니다.

### 4-2) Action Animation Presenter 설정

`ActionAnimationPresenter`는 ActionController 상태를 Animator 파라미터/상태로 반영하는 핵심 브리지입니다.

#### 필수 연결

1. Enemy 오브젝트(또는 Animator가 있는 자식)에 `ActionAnimationPresenter` 추가
2. 아래 참조를 Inspector에서 연결
   - `ActionController`
   - `Animator`
   - `AnimationStateMapProfile` (`ASMP_Enemy_*`)

#### 권장 파라미터/상태 매핑 규칙

- 액션 상태 이름은 `AnimationStateMapProfile`에 단일 소스로 관리
- ActionType과 Animator State 이름을 1:1로 맞추는 것을 권장
  - `Idle` -> `Idle`
  - `Move` -> `Run`
  - `Attack` -> `Attack_A`
  - `Hit` -> `Hit`
  - `Die` -> `Die`

#### 운영 팁

1. **Presentation Lock 사용**
   - 사망/피격 등 특정 액션에서 즉시 상태가 바뀌지 않게 유지 연출 가능
2. **해제 허용 액션 명시**
   - Lock을 풀 수 있는 액션(예: `Die`, `Idle`)을 명확히 설정
3. **애니메이션 마커와 역할 분리**
   - 상태 전환은 `ActionController`
   - 타격 타이밍은 마커(`HitStart/HitEnd`)
   - 애니메이션 출력은 `ActionAnimationPresenter`

#### 자주 발생하는 문제

- 문제: 액션은 바뀌는데 애니메이션이 안 바뀜
  - 점검: `AnimationStateMapProfile` 연결 여부, 상태 이름 오탈자
- 문제: 피격/사망 연출이 중간에 끊김
  - 점검: Presentation Lock 옵션과 인터럽트 정책
- 문제: 공격 판정은 맞는데 타격 모션 타이밍이 어긋남
  - 점검: 마커 프레임(`HitStart/HitEnd`) 위치

---

## 5) AttackExecutor 설정

1. `_actionMaps` 구성
   - `AttackAction`에 대응하는 `AttackSpec` 연결
2. `_autoExecuteOnHitWindowOpen` 필요에 맞게 설정
3. HitWindow 마커(`HitStart/HitEnd`)가 애니메이션에 배치되어 있는지 확인

### 5-1) AttackExecutor용 SO 생성 방법 (예시)

AttackExecutor는 `AttackSpec` SO 품질이 핵심입니다.

1. `Create > AttackSystem > Attack Spec`
2. Enemy 공격 수만큼 생성
   - 예시 파일명:
     - `AS_Enemy_Melee_A`
     - `AS_Enemy_Melee_B`
     - `AS_Enemy_Ranged_Shot`
3. `AttackExecutor._actionMaps`에서 액션 타입과 AttackSpec을 1:1 또는 1:N 규칙으로 매핑

#### 근접 기본타(`AS_Enemy_Melee_A`) 예시값

- `AttackTypeId`: `EnemyMeleeA`
- `BaseDamage`: `10`
- `AreaType`: `Circle`
- `LocalOffset`: `(1.0, 0.0)`
- `Radius`: `1.0`
- `TargetLayerMask`: `Player`
- `RequireTargetTag`: `true`
- `TargetTag`: `Player`
- `MaxTargets`: `1`
- `StatusTag`: `EnemyMeleeA`
- `AllowMultiHitPerSwing`: `false`

#### 원거리 샷(`AS_Enemy_Ranged_Shot`) 예시값

- `AttackTypeId`: `EnemyShotA`
- `BaseDamage`: `8`
- `AreaType`: `Circle` (투사체 직접 판정을 안 쓰는 경우 작게 설정)
- `LocalOffset`: `(0.8, 0.0)`
- `Radius`: `0.5`
- `TargetLayerMask`: `Player`
- `RequireTargetTag`: `true`
- `TargetTag`: `Player`
- `MaxTargets`: `1`
- `StatusTag`: `EnemyShotA`
- `AllowMultiHitPerSwing`: `false`

#### ActionMap 연결 예시

- `ActionType=Attack` -> `AttackSpec=AS_Enemy_Melee_A`
- `ActionType=AttackCombo1` -> `AttackSpec=AS_Enemy_Melee_B`
- `ActionType=AttackAir` -> `AttackSpec=AS_Enemy_Ranged_Shot`

---

## 6) Hit/Health 설정

### HealthComponent
- 초기 체력
- 사망 중 회복 허용 여부

### HitReceiver
- `_incomingDamageMultiplier`
- 무적 상태 초기값

권장: `ActionHitBridge`를 연결해 `Hit/Break/Die` 액션 연동

---

## 7) Targeting 설정

### 기본 방식 (Registry 캐시 기반)
- Enemy에 `NearestTargetByTagProvider` 추가
- Provider `_targetTag`를 `Player`(또는 원하는 타겟 태그)로 지정
- Provider `_maxAcquireRange`와 `_retargetInterval(0.1~0.25s)` 설정
- `EnemyBrain._targetProviderBehaviour`에 Provider 연결

### TargetRegistryMember 사용법 (필수)
`NearestTargetByTagProvider`는 전역 검색 대신 `TargetRegistry`를 조회하므로, **타겟 오브젝트에 등록 컴포넌트가 필요**합니다.

1. 추적 대상 오브젝트(예: Player) 선택
2. `TargetRegistryMember` 컴포넌트 추가
3. `_targetTag` 설정
   - 비워두면 `GameObject.tag`를 사용
   - 문자열을 입력하면 해당 태그로 강제 등록
4. 런타임 동작 확인
   - 오브젝트 활성화 시 자동 등록(`OnEnable`)
   - 오브젝트 비활성화/파괴 시 자동 해제(`OnDisable`)

> 체크 포인트: Provider `_targetTag`와 TargetRegistryMember의 최종 등록 태그가 동일해야 Enemy가 타겟을 찾을 수 있습니다.

### 대체 방식
- 직접 `_target` 할당
- 커스텀 `IEnemyTargetProvider` 구현체 사용

---

## 8) 신규 Enemy 제작 절차 (권장)

1. 기존 Enemy 베이스 오브젝트 복제
2. `EnemyArchetypeData` 새로 만들거나 기존 데이터 재사용
3. ActionController용 SO 세트 생성/연결
   - `ActionRuleProfile`
   - `ActionInterruptPolicyProfile`
   - `AnimationStateMapProfile`
   - (선택) `ActionHitBridgeProfile`
4. AttackExecutor용 `AttackSpec` 세트 생성/연결
5. Action 맵/룰 확인
6. Targeting/연출 설정
7. 테스트 씬에서 검증

---

## 9) 제작 규칙

- 신규 Enemy마다 Brain 코드를 새로 만들지 않고, **Archetype + AttackSpec + 정책 모듈** 조합으로 제작합니다.
- 상태 추가가 필요하면 Brain 확장보다 `Advanced` 모듈 적용을 우선 검토합니다.

---

## 10) Enemy 제작 시 필수/권장 SO 목록

### 필수 SO

1. `EnemyArchetypeData`
   - 이동/감지/전투/순찰/액션 매핑
2. `ActionRuleProfile`
   - 액션 허용/우선순위
3. `ActionInterruptPolicyProfile`
   - 액션 간 인터럽트 정책
4. `AnimationStateMapProfile`
   - 액션-애니메이션 상태 매핑
5. `AttackSpec` (1개 이상)
   - 실제 공격 데미지/범위/필터 규칙

### 권장 SO

1. `ActionHitBridgeProfile`
   - 피격 결과->액션 변환 룰
2. `BossPhaseData` (보스 전용)
   - 체력 임계치 패턴 전환 데이터

### 타입별 권장 SO 세트

- 근접 Enemy: `EnemyArchetypeData + Action(3종) + AttackSpec(1~2종)`
- 원거리 Enemy: `EnemyArchetypeData + Action(3종) + AttackSpec(1~3종) + (Advanced 정책/스킬 모듈)`
- 보스 Enemy: `EnemyArchetypeData + Action(3종) + AttackSpec(다수) + BossPhaseData + Pattern 데이터`
