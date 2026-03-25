# Enemy Advanced 모듈 사용 가이드

이 문서는 보스/원거리/고급 기믹 Enemy를 만들기 위한 `Advanced` 모듈의 목적과 사용법을 설명합니다.

---

## 1) Advanced 모듈 구조

### 핵심 인터페이스
- `IEnemyDecisionPolicy`
  - Brain 기본 판단을 상황별로 오버라이드
- `IEnemyLocomotionDecisionPolicy`
  - Brain 이동 모드(지상/부유) 전환 판단을 오버라이드
- `IEnemySkillExecutor`
  - 공격 상태에서 전용 스킬 실행
- `IEnemyPatternRunner`
  - 보스 패턴이 Brain 흐름을 오버라이드
- `IEnemyGimmickModule`
  - 프레임 단위 기믹 주입

### 공통 문맥 데이터
- `EnemyBrainContext`
  - Self, Target, DistanceToTarget, SpawnPosition, Archetype 전달
- `EnemyLocomotionContext`
  - 타겟 거리/고도 차, 전방 장애물, 접지 여부, 전환 시각/쿨다운, 공중 체류시간, 착지 후보 정보를 전달

### 결정 결과
- `EnemyDecisionResult` + `E_EnemyDecisionType`
  - `UseDefault`, `ForceAttack`, `ForceChase`, `ForceRetreat`, `HoldPosition`

---

## 2) EnemyBrain에 연결하는 방법

`EnemyBrain` 인스펙터의 `Advanced Extensions`에 아래를 연결합니다.

- `DecisionPolicyBehaviour`: `IEnemyDecisionPolicy` 구현체
- `SkillExecutorBehaviour`: `IEnemySkillExecutor` 구현체
- `PatternRunnerBehaviour`: `IEnemyPatternRunner` 구현체
- `GimmickModuleBehaviours`: `IEnemyGimmickModule` 구현체 배열

연결된 모듈은 Brain `Update` 중 순서대로 평가됩니다.

1. Gimmick Tick
2. Pattern Override 검사
3. Decision Override 검사
4. 기본 Brain 로직

---

## 3) 원거리 Enemy 구성 예시

### 사용 모듈
- `RangedDecisionPolicy`
- `SimpleProjectileSkillExecutor`

### 설정 포인트
- `RangedDecisionPolicy`
  - `_preferredMinRange`
  - `_preferredMaxRange`
  - `_retreatDistance`
- `SimpleProjectileSkillExecutor`
  - `_projectileActionType` (예: `AttackAir`)

### 기대 동작
- 가까우면 후퇴
- 적정 거리면 공격 강제
- 멀면 추적 강제

---

## 4) 보스 Enemy 구성 예시

### 사용 모듈
- `BossPhaseData`
- `BossPhaseController`
- `BossPatternRunnerAdapter`
- 기존 `BossPatternController`

### 설정 절차
1. `BossPhaseData` 생성
2. `PhaseEntry`에 `TriggerHealthNormalized` + `PatternId` 입력
3. `BossPhaseController`에 `HealthComponent`, `BossPatternController`, `BossPhaseData` 연결
4. `BossPatternRunnerAdapter`를 `EnemyBrain._patternRunnerBehaviour`에 연결

### 기대 동작
- 체력 임계치 도달 시 패턴 ID 전환
- 패턴 오버라이드 중 Brain 기본 로직 억제 가능

---

## 5) 고급 기믹 Enemy 구성 예시

### 사용 모듈
- `ShieldGimmickModule`

### 설정 절차
1. Enemy에 `ShieldGimmickModule` 추가
2. `_healthComponent`, `_hitReceiver` 연결
3. `_shieldBreakThreshold` 설정
4. `EnemyBrain._gimmickModuleBehaviours` 배열에 등록

### 기대 동작
- 임계치 이상 체력 구간: 무적 유지
- 임계치 미만 체력 구간: 무적 해제

---

## 6) 커스텀 Advanced 모듈 작성 규칙

1. 기존 인터페이스 중 하나를 구현합니다.
2. `EnemyBrainContext` 기반으로 판단/실행합니다.
3. Brain 직접 수정 없이 모듈 교체로 동작이 바뀌게 설계합니다.
4. 상태 전환 충돌이 생기면 Pattern > Decision > Default 우선순위를 유지합니다.

---

## 7) 운영 권장안

- 공통 Enemy는 기본 Brain + Archetype만 사용
- 원거리/특수형은 Decision + Skill 모듈 추가
- 보스는 Pattern + Phase 계층 필수
- 기믹은 GimmickModule로 캡슐화

이 방식을 따르면 Enemy 타입이 늘어도 코드 중복 없이 확장 가능합니다.

---

## 8) Locomotion 확장 모듈(부유/호버) 가이드

### 관련 컴포넌트
- `LocomotionDecisionPolicy`
  - Grounded -> Floating / Floating -> Grounded 전환 판단
  - 시나리오 로그 이유값 예시
    - `ScenarioA_ObstacleDetected`
    - `ScenarioB_TargetAltitudeRise`
    - `ScenarioC_NoGroundCandidate`
    - `ScenarioC_ForceGroundedByMaxAirTime`
- `SafeLandingResolver`
  - 다중 샘플 하향 프로브 기반 안전 착지 후보 탐색
  - 후보 개수/선택 인덱스/실패 사유 텔레메트리 제공

### Brain 연결 항목 (`EnemyBrain > Advanced Extensions`)
- `LocomotionDecisionPolicyBehaviour`: `IEnemyLocomotionDecisionPolicy` 구현체
- `SafeLandingResolver`: 착지 후보 탐색기

### 필수 운영 포인트
1. `EnemyArchetypeData > Locomotion` 값이 Driver/Policy/Resolver에 런타임 반영됩니다.
2. 전환 로그는 `EnemyBrain`의 시나리오 로그(`EnemyLocomotionScenarioLog`)로 출력됩니다.
3. 전환 금지 규칙(Dead/HitStun/Recover)은 유지되며, 해당 차단은 `ScenarioD_TransitionBlockedByCombatPriority`로 확인합니다.

### 최근 추가된 운영 포인트
4. `EnemyMovementDriver`는 Grounded 엣지 감지 시 우회 후보를 생성하며 `TryGetEdgeBypassCandidate(out Vector2)` API로 노출합니다.
5. `EnemyBrain`은 `Patrol Recovery` 섹션을 통해 순찰 진행 정체(stuck) 복구를 수행합니다.
   - `_enablePatrolStuckRecovery`
   - `_patrolProgressDistanceThreshold`
   - `_patrolStuckTimeThreshold`
   - `_patrolStuckRecoverCooldown`
   - `_patrolBypassHoldTime`
6. Floating 전용 Enemy의 Y축 추적을 위해 `EnemyBrain > Floating Altitude Follow`를 사용합니다.
   - `_followMoveTargetYWhenFloating`
   - `_floatingMoveTargetYOffset`
   - `_resolvedFloatingAltitudeCommand`(디버그)

---

## 9) Patrol 정체(Stuck) 복구 설계

Grounded 순찰 중 엣지 감지/지형 경계로 인해 이동이 멈춘 상태가 지속되면 `EnemyBrain`이 정체를 감지합니다.

1. 현재 목적지 거리 변화량을 프레임 단위로 기록
2. 일정 시간 이상 유의미한 접근이 없으면 stuck 판정
3. 우선순위에 따라 복구 시도
   - 1순위: `EnemyMovementDriver.TryGetEdgeBypassCandidate(...)`
   - 2순위: `EnemyPatrolRouteProvider.GetNextPoint(...)`
   - 3순위: `PickPatrolDestination()` fallback

이 구조로 “끝점에서 다음 포인트로 못 넘어가며 제자리 왕복” 문제를 완화할 수 있습니다.

---

## 10) Floating 전용 Enemy의 Y축 추적

Floating solver는 X는 이동 목표를 따르고, Y는 Brain이 전달하는 `TargetAltitude`를 따릅니다.
따라서 Brain의 고도 명령이 고정값이면 Y축 추적이 정지된 것처럼 보일 수 있습니다.

현재는 `EnemyBrain.ResolveFloatingAltitudeCommand(...)`가 아래 조건에서 고도 명령을 동적으로 갱신합니다.

- 고도 명령 사용 상태
- `_followMoveTargetYWhenFloating == true`
- 전환 상태(`SwitchingToFloating`, `SwitchingToGrounded`)가 아님
- 이동 의도(`_combatWantsMove`)가 유효함

실전 튜닝은 `_floatingMoveTargetYOffset`으로 수행합니다.
