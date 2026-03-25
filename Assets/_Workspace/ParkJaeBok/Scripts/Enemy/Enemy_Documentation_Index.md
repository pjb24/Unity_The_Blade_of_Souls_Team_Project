# Enemy 시스템 문서 인덱스

이 문서는 `Assets/_Workspace/ParkJaeBok/Scripts/Enemy` 하위 Enemy 프레임워크 문서의 진입점입니다.

---

## 문서 구성

1. **Enemy 기본 설정/제작 가이드**
   - `Enemy_Setup_And_Creation_Guide.md`
   - 대상: Enemy를 처음 씬에 배치하는 개발자/기획자

2. **Enemy 테스트 씬 가이드**
   - `Enemy_Test_Scene_Guide.md`
   - 대상: 상태 전환/전투/연출 검증 담당자

3. **Enemy Advanced 확장 가이드**
   - `Enemy_Advanced_Modules_Guide.md`
   - 대상: 보스, 원거리, 기믹 Enemy를 구현하는 프로그래머

---

## 빠른 시작 순서

1. `Enemy_Setup_And_Creation_Guide.md`로 공통 Enemy 프리셋 구성
2. `Enemy_Test_Scene_Guide.md`로 기본 동작 검증
3. 필요 시 `Enemy_Advanced_Modules_Guide.md`로 정책/스킬/패턴/기믹 확장
4. 부유/호버 Enemy를 만들 때는 `Enemy_Setup_And_Creation_Guide.md`의 **Locomotion(지상/부유) 설정 섹션**과 `Enemy_Advanced_Modules_Guide.md`의 **Locomotion Policy/SafeLandingResolver 섹션**을 함께 확인
5. Grounded 순찰이 끝점에서 멈추거나 왕복 떨림이 보이면 `Enemy_Setup_And_Creation_Guide.md`의 **Patrol Recovery(순찰 정체 복구)** 섹션을 확인
6. Floating 전용 Enemy가 Y축 추적을 못 할 때는 `Enemy_Setup_And_Creation_Guide.md`의 **Floating 전용 Enemy Y축 추적** 섹션과 `Enemy_Advanced_Modules_Guide.md`의 **Floating 전용 Enemy의 Y축 추적** 섹션을 확인

---

## 권장 운영 원칙

- Enemy 타입 차이는 코드 분기보다 **데이터(Archetype, PatternData, Profile)** 중심으로 운영합니다.
- `EnemyBrain`은 공통 상태 흐름을 유지하고, 고급 요구는 `Advanced` 모듈로 분리합니다.
- 신규 Enemy 추가 시 기존 컴포넌트 조합을 우선 재사용하고, 마지막 수단으로 신규 스크립트를 추가합니다.
- 로코모션(지상/부유) 확장 시 `EnemyArchetypeData`의 `Locomotion` 섹션과 `EnemyMovementActionSync`의 `HoverIdle/FlyMove` 매핑을 반드시 함께 검증합니다.
- Grounded 이동 튜닝은 `EnemyMovementDriver`의 `Grounded Auto Tuning` 및 Runtime 디버그 필드(`Resolved*`, `Ground Collider Bounds Min Y`)를 함께 확인합니다.
