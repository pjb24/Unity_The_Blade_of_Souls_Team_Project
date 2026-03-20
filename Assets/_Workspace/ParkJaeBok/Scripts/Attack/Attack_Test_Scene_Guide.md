# Attack System 테스트 씬 구성 가이드

이 문서는 `AttackSpec`, `AttackActionMap`, `AttackExecutor`를 이용해 Player/Enemy 공격 테스트용 Scene을 만드는 절차를 설명합니다.  
목표는 다음 3가지를 빠르게 검증하는 것입니다.

1. Hit Window 구간에서만 공격이 발생하는지
2. `AttackSpec` 데이터(데미지/범위/태그/타겟 필터)가 올바르게 적용되는지
3. `HitReceiver -> HealthComponent` 체인으로 피해가 정상 반영되는지

---

## 0) 사전 준비

- Unity 프로젝트가 정상 컴파일되는 상태인지 확인합니다.
- Project Settings에서 Input System이 New Input System으로 활성화되어 있는지 확인합니다.
- 아래 시스템 파일이 프로젝트에 존재해야 합니다.
  - `ActionController`
  - `AttackExecutor`
  - `AttackSystemTestRunner`
  - `HitReceiver`
  - `HealthComponent`
- 테스트에서는 2D 물리(`Physics2D`) 기반 판정을 사용합니다.

---

## 1) 테스트 Scene 생성

1. `Assets/_Workspace/ParkJaeBok/Scenes/` 폴더에서 새 Scene을 생성합니다.  
   - 예시 이름: `AttackSystemTest.unity`
2. Scene 저장 후 열어둡니다.
3. `Main Camera`의 위치를 `(0, 0, -10)`으로 맞춥니다.

---

## 2) Player 오브젝트 구성

1. 빈 오브젝트 생성 후 이름을 `Player`로 지정합니다.
2. `Player`에 아래 컴포넌트를 추가합니다.
   - `ActionController`
   - `AttackExecutor`
   - `HitReceiver`
   - `HealthComponent`
   - (선택) 시각 확인용 `SpriteRenderer`
3. `Player`에 `Rigidbody2D` + `BoxCollider2D`를 추가합니다.
   - `Body Type`: `Dynamic` 또는 `Kinematic`
4. `Player`의 Tag를 `Player`로 설정합니다.
5. `Player`의 Layer를 프로젝트 규칙에 맞게 설정합니다.  
   - 예: `Player`

---

## 3) Enemy 오브젝트 구성

1. 빈 오브젝트 생성 후 이름을 `Enemy_01`로 지정합니다.
2. `Enemy_01`에 아래 컴포넌트를 추가합니다.
   - `HitReceiver`
   - `HealthComponent`
   - (선택) `ActionController` + `ActionHitBridge` (피격 애니메이션 확인 시)
   - (선택) `HealthDebugListener` 또는 `HitDebugListener` (로그 확인 시)
3. `Enemy_01`에 `Rigidbody2D` + `BoxCollider2D`를 추가합니다.
4. `Enemy_01`의 위치를 `Player` 앞쪽(예: X=2~3)으로 배치합니다.
5. `Enemy_01`의 Tag를 `Enemy`로 지정합니다.
6. `Enemy_01`의 Layer를 타겟 탐지 레이어로 지정합니다.  
   - 예: `Enemy`

---

## 4) Action Rule Profile 생성 및 연결

1. 프로젝트 창에서 `Create > ActionSystem > Action Rule Profile`을 선택합니다.
2. 생성된 Profile(예: `ARP_Player_AttackTest`)의 `Rules`를 설정합니다.
   - `Idle`: Enabled=true
   - `Attack`: Enabled=true
   - `AttackCombo1`: Enabled=true (원하면)
3. 공격 액션의 `Priority`, `IsInterruptible`, `AutoCompleteSeconds`를 설정합니다.
   - 초반 테스트는 `AutoCompleteSeconds=0` 또는 애니메이션 마커 완료 방식을 권장합니다.
4. `Player/ActionController`의 `_actionRuleProfile`에 방금 생성한 Profile을 연결합니다.
5. `Player/ActionController`의 `_defaultActionType`을 `Idle`로 설정합니다.

---

## 5) Animation Marker 설정 (Hit Window)

`AttackExecutor`는 `ActionController.IsHitWindowOpen`이 열려 있을 때 공격을 실행하므로, 공격 애니메이션에 Hit 마커를 넣어야 합니다.

1. 공격 애니메이션 클립을 엽니다.
2. 타격 시작 프레임에 `HitStart` 마커를 추가합니다.
3. 타격 종료 프레임에 `HitEnd` 마커를 추가합니다.
4. 완료 프레임에 `CompleteCurrentAction` 마커를 추가합니다.
5. 마커 수신용으로 `ActionMarkerReceiver`가 같은 오브젝트 체인에 붙어 있는지 확인합니다.

> 임시 테스트로는 `ActionController.ReceiveMarker(...)`를 직접 호출하는 테스트 스크립트를 만들어도 됩니다.

---

## 6) AttackSpec 자산 생성 (A/B/C)

각 공격 타입별로 ScriptableObject를 생성합니다.

1. `Create > AttackSystem > Attack Spec`을 선택합니다.
2. 아래처럼 3개 자산을 만듭니다.
   - `AS_AttackA`
   - `AS_AttackB`
   - `AS_AttackC`
3. 예시 설정값

### 6-1) AS_AttackA (근접 기본타)
- `AttackTypeId`: `AttackA`
- `BaseDamage`: `10`
- `AreaType`: `Circle`
- `LocalOffset`: `(1.2, 0)`
- `Radius`: `1.2`
- `TargetLayerMask`: `Enemy`
- `RequireTargetTag`: `true`
- `TargetTag`: `Enemy`
- `MaxTargets`: `1`
- `StatusTag`: `AttackA`
- `AllowMultiHitPerSwing`: `false`

### 6-2) AS_AttackB (넓은 범위타)
- `AttackTypeId`: `AttackB`
- `BaseDamage`: `14`
- `AreaType`: `Box`
- `LocalOffset`: `(1.5, 0)`
- `BoxSize`: `(3.0, 1.4)`
- `TargetLayerMask`: `Enemy`
- `RequireTargetTag`: `true`
- `TargetTag`: `Enemy`
- `MaxTargets`: `3`
- `StatusTag`: `AttackB`
- `AllowMultiHitPerSwing`: `false`

### 6-3) AS_AttackC (브레이크 테스트용)
- `AttackTypeId`: `AttackC`
- `BaseDamage`: `8`
- `AreaType`: `Circle`
- `LocalOffset`: `(1.0, 0)`
- `Radius`: `1.0`
- `TargetLayerMask`: `Enemy`
- `RequireTargetTag`: `true`
- `TargetTag`: `Enemy`
- `MaxTargets`: `2`
- `StatusTag`: `PoiseBreak`
- `AllowMultiHitPerSwing`: `false`

---

## 7) AttackExecutor 매핑 연결

1. `Player/AttackExecutor`에서 `_actionMaps` 배열 크기를 늘립니다.
2. 예시 매핑
   - `ActionType=Attack` -> `AttackSpec=AS_AttackA`
   - `ActionType=AttackCombo1` -> `AttackSpec=AS_AttackB`
   - `ActionType=AttackCombo2` -> `AttackSpec=AS_AttackC`
3. `_autoExecuteOnHitWindowOpen=true`로 둡니다.
4. 디버깅용으로 `_drawGizmos=true`를 켭니다.

---

## 8) Hit / Health 파라미터 확인

### 8-1) Player HitReceiver
- 공격자 자신이 피격될 가능성을 줄이려면 별도 레이어 충돌 매트릭스를 점검합니다.
- `HitReceiver`는 필수가 아니지만 상호전투 테스트 시 권장됩니다.

### 8-2) Enemy HitReceiver
- `_healthComponent`가 비어있다면 같은 오브젝트 `HealthComponent`로 폴백됩니다.
- `_incomingDamageMultiplier`를 `1.0`으로 둡니다.

### 8-3) Enemy HealthComponent
- `_initialMaxHealth`: `100`
- `_initialCurrentHealth`: `100`

---

## 9) 입력 연결 (공격 트리거)

### AttackSystemTestRunner 사용
1. `Player` 오브젝트에 `AttackSystemTestRunner`를 추가합니다.
2. 인스펙터에서 아래 참조를 연결합니다.
   - `_actionController` -> `Player`의 `ActionController`
   - `_attackExecutor` -> `Player`의 `AttackExecutor`
3. 액션 매핑 필드를 프로젝트 설정에 맞게 확인합니다.
   - `_attackAActionType` -> `Attack`
   - `_attackBActionType` -> `AttackCombo1`
   - `_attackCActionType` -> `AttackCombo2`
4. 키 입력으로 테스트를 실행합니다.
   - `1` 키: AttackA 테스트
   - `2` 키: AttackB 테스트
   - `3` 키: AttackC 테스트
   - `E` 키: 현재 액션 유지 상태에서 ExecuteOnly 테스트
   - `T` 키: A -> B -> C 자동 시퀀스 테스트
   - 위 키 입력은 `AttackSystemTestRunner` 내부의 New Input System(Keyboard.current) 로 처리됩니다.
5. Console 로그에서 아래 값을 확인합니다.
   - `RequestAccepted`
   - `ExecuteResult`
6. 애니메이션 `HitStart/HitEnd` 구간과 로그 결과가 일치하는지 검증합니다.

---

## 10) AttackSystemTestRunner 상세 사용법

### 10-1) ContextMenu 기반 실행
1. `Player` 오브젝트 선택
2. `AttackSystemTestRunner` 컴포넌트 우클릭
3. `Start Auto Sequence` 실행
4. 필요 시 `Stop Auto Sequence`로 중단

### 10-2) 시작 시 자동 실행
1. `_runSequenceOnStart = true`로 설정합니다.
2. Play 진입 시 자동으로 A/B/C 시퀀스가 실행되는지 확인합니다.
3. 단계 간 간격은 `_stepInterval`로 조정합니다.

### 10-3) 빠른 오류 점검
- `Missing ActionController` 로그가 뜨면 Player 참조를 재연결합니다.
- `Missing AttackExecutor` 로그가 뜨면 Player 참조를 재연결합니다.
- `ExecuteResult=false`가 반복되면 아래를 순서대로 점검합니다.
  1) HitWindow 열림 여부
  2) AttackExecutor 액션 매핑 여부
  3) LayerMask/Tag 필터
  4) Collider2D 및 오브젝트 거리

---

## 11) 테스트 체크리스트

### 11-1) 기본 타격
- `Attack` 실행 시 Enemy 체력이 감소해야 합니다.
- Hit Window 밖에서는 체력 감소가 없어야 합니다.

### 11-2) 범위/대상 수
- `AttackCombo1` 실행 시 Box 범위 내 Enemy 여러 명이 맞아야 합니다.
- `MaxTargets` 값 이상은 맞지 않아야 합니다.

### 11-3) 중복 타격 방지
- 같은 공격 실행 구간에서 동일 Enemy가 여러 번 피격되지 않아야 합니다.
- `AllowMultiHitPerSwing=true`로 바꾸면 동일 실행 내 다중 피격이 허용되어야 합니다.
- 이때 `HitRequest.HitId`는 스윙 내 시리얼을 포함해 생성되므로(내부 처리), 연속 타격이 중복 차단 없이 반영되는지 확인합니다.

### 11-4) 태그/레이어 필터
- Tag가 `Enemy`가 아닌 오브젝트는 피격되지 않아야 합니다.
- LayerMask에서 제외한 레이어는 피격되지 않아야 합니다.

### 11-5) StatusTag 전달
- `AS_AttackC`의 `StatusTag=PoiseBreak` 전달 후, 피격 브리지(사용 시)에서 Break 액션으로 변환되는지 확인합니다.

---

## 12) 디버깅 팁

1. `AttackExecutor`의 `_drawGizmos=true`로 실제 판정 영역을 확인합니다.
2. `HitReceiver` 경고 로그를 먼저 확인합니다.
   - `InvalidHitId`, `TargetInvincible`, `DuplicateHitId` 등
3. 공격이 안 맞으면 다음 순서로 점검합니다.
   1) Hit Window가 열렸는가?
   2) ActionType에 AttackSpec이 매핑되어 있는가?
   3) LayerMask/Tag가 맞는가?
   4) Collider2D가 있는가?
4. 공격은 되는데 체력이 안 줄면 다음을 점검합니다.
   1) Enemy에 `HealthComponent`가 있는가?
   2) `HitReceiver._healthComponent` 참조가 유효한가?
   3) 무적/차단 상태가 아닌가?

---

## 13) 권장 확장 순서

1. `AttackSpec` 추가(데이터)
2. `ActionController` Rule/Animator 매핑 추가(상태)
3. `AttackExecutor` 매핑 추가(실행)
4. Hit/Health 로그 검증(결과)

이 순서를 유지하면 Player/Enemy/Boss 공용 공격 패턴을 안전하게 확장할 수 있습니다.
