# Enemy Advanced 모듈 사용 가이드

이 문서는 보스/원거리/고급 기믹 Enemy를 만들기 위한 `Advanced` 모듈의 목적과 사용법을 설명합니다.

---

## 1) Advanced 모듈 구조

### 핵심 인터페이스
- `IEnemyDecisionPolicy`
  - Brain 기본 판단을 상황별로 오버라이드
- `IEnemySkillExecutor`
  - 공격 상태에서 전용 스킬 실행
- `IEnemyPatternRunner`
  - 보스 패턴이 Brain 흐름을 오버라이드
- `IEnemyGimmickModule`
  - 프레임 단위 기믹 주입

### 공통 문맥 데이터
- `EnemyBrainContext`
  - Self, Target, DistanceToTarget, SpawnPosition, Archetype 전달

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
