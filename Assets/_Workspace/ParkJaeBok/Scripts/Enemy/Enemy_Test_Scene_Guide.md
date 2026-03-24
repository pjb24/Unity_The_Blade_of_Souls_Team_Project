# Enemy 테스트 씬 설정 가이드

이 문서는 Enemy 기본 동작과 확장 모듈을 검증하기 위한 **테스트 씬 체크리스트**를 제공합니다.

---

## 1) 테스트 목표

1. 상태 전환이 정상 동작하는지
2. 공격 판정과 체력 반영이 정상인지
3. 피격/사망 액션 및 연출이 정상인지
4. Advanced 모듈(원거리/보스/기믹)이 정상인지

---

## 2) 씬 최소 구성

### 플레이어
- `Player` 오브젝트 1개
- 이동/공격 가능 상태

### Enemy
- 근접 Enemy 1개
- 원거리 Enemy 1개
- 보스 Enemy 1개(선택)

### 시스템 오브젝트
- `AudioManager`
- `SfxOrchestrator`
- `EffectService` (VFX 사용 시)

---

## 3) Enemy 공통 검증 항목

### 상태 전환
- Spawn -> Idle
- Idle/Patrol -> Chase
- Chase -> Attack
- Attack -> Recover -> Chase/Idle
- 피격 시 HitStun
- 체력 0 시 Dead

### 이동
- 추적 거리 진입 시 타겟으로 이동
- Leash 초과 시 스폰 지점 복귀
- Patrol 켠 경우 랜덤 순찰 수행

### 전투
- HitWindow에서만 공격 판정 발생
- HitReceiver를 통해 데미지 적용
- HealthComponent 감소/사망 반영

---

## 4) 로그/디버그 확인 포인트

### EnemyBrain
- Gizmos 반경(Detect/Lose/Attack/Patrol) 표시 확인
- 상태 전환 타이밍이 의도와 맞는지 확인

### AttackExecutor
- 공격 액션별 AttackSpec 매핑 확인
- 타겟 필터(Layer/Tag) 확인

### Hit/Health
- 피격 수락/거부 로그 확인
- 체력 변경/사망 콜백 확인

---

## 5) 테스트 케이스 (권장 순서)

### Case A: 근접 Enemy
1. 플레이어가 DetectRange 밖에 있을 때 Idle/Patrol 유지
2. DetectRange 진입 시 Chase 전환
3. AttackRange 진입 시 공격 수행
4. 피격 시 HitStun 확인
5. 체력 0 시 Dead 확인

### Case B: 원거리 Enemy
1. 목표 거리보다 가까워지면 후퇴하는지 확인
2. 적정 거리에서 공격 액션(투사체 액션) 수행 확인
3. 너무 멀면 추적 재개 확인

### Case C: 보스 Enemy
1. 초기 패턴 실행 확인
2. 체력 임계치 도달 시 패턴 전환 확인
3. 패턴 오버라이드 중 Brain 기본 흐름이 억제되는지 확인

### Case D: 기믹 Enemy
1. 실드 기믹 활성 구간에서 무적 상태 확인
2. 임계치 하회 시 무적 해제 확인

---

## 6) 실패 시 점검 순서

1. 컴포넌트 참조 누락 여부
2. ActionRuleProfile에서 액션 Enable 여부
3. AttackSpec 연결 여부
4. Layer/Tag 필터 설정
5. TargetProvider 설정 여부
6. SFX/VFX 시스템 인스턴스 존재 여부

---

## 7) 테스트 결과 기록 템플릿

- 테스트 날짜:
- 테스트 빌드/브랜치:
- 대상 Enemy:
- 성공 케이스:
- 실패 케이스:
- 재현 절차:
- 수정 필요 항목:
