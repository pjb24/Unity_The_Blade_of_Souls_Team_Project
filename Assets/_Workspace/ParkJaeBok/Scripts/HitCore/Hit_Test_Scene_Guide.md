# Hit 시스템 테스트 Scene 제작 가이드

## 문서 목적
이 문서는 2D 게임용 Hit 시스템 코어 스크립트를 **Unity Editor에서 직접 검증**하기 위한 테스트 Scene 구성 절차를 설명합니다.

- 공격자는 `HitRequest`만 전달하고,
- 대상(`HitReceiver`)이 최종 수락/거부를 판정하며,
- 결과를 `HitResult`로 확인하는 흐름을 테스트합니다.

> 주의: 이 가이드는 **Scene 파일 생성 순서만 안내**합니다. (문서 작성 요청에 따라 `.unity`, `.prefab`, `.meta` 파일은 본 변경에서 생성하지 않습니다.)

---

## 사전 준비

### 1) 스크립트 존재 확인
다음 스크립트가 프로젝트에 존재해야 합니다.

- `Assets/_Workspace/ParkJaeBok/Scripts/Health/HealthComponent.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/Health/DamageContext.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/Health/DamageResult.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/HitCore/HitRequest.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/HitCore/HitResult.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/HitCore/IHitListener.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/HitCore/HitReceiver.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/HitCore/HitDebugListener.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/HitCore/HitTestAttacker.cs`

### 2) 콘솔 확인 설정
- Unity Console에서 `Collapse`를 꺼서 로그 순서를 확인하기 쉽게 합니다.
- `Error Pause`는 필요 시 활성화합니다.

### 3) 입력 시스템 설정
- `Project Settings > Player > Active Input Handling`을 `Input System Package (New)` 또는 `Both`로 설정합니다.
- 본 테스트 드라이버(`HitTestAttacker`)는 **New Input System (`UnityEngine.InputSystem`)** 기준으로 동작합니다.

---

## 테스트용 Scene 구성 (수동)

## 1단계: 새 Scene 생성
1. Unity 상단 메뉴에서 `File > New Scene`을 선택합니다.
2. 빈 Scene으로 시작합니다.
3. Scene 이름은 예시로 `HitSystemTest`로 저장합니다.

## 2단계: Target 오브젝트 생성
1. Hierarchy에서 빈 GameObject를 생성하고 이름을 `Target`으로 지정합니다.
2. `Target`에 2D 물리 감지를 위한 `Collider2D`(예: `BoxCollider2D`)를 추가합니다.
3. 필요하면 `Rigidbody2D`를 추가하고 Body Type을 게임 규칙에 맞게 설정합니다.
4. `Target`에 아래 컴포넌트를 추가합니다.
   - 기존 Health 시스템의 `HealthComponent`
   - Hit 시스템의 `HitReceiver`
   - Hit 시스템의 `HitDebugListener`
5. `HitReceiver`의 `_healthComponent` 필드에 `Target`의 `HealthComponent`가 자동 연결되지 않았다면 수동 드래그로 연결합니다.
6. `HitDebugListener`의 `_receiver` 필드도 동일하게 `Target`의 `HitReceiver`를 연결합니다.

## 3단계: 체력 초기값 세팅
1. `Target`의 `HealthComponent`에서 초기 체력 값을 설정합니다.
2. 예시 설정:
   - `_initialMaxHealth = 100`
   - `_initialCurrentHealth = 100`

## 4단계: HitReceiver 상태 세팅
1. `HitReceiver`에서 기본 테스트 값을 설정합니다.
2. 예시 설정:
   - `_isInvincible = false`
   - `_incomingDamageMultiplier = 1.0`

---

## 테스트 드라이버 스크립트 만들기 (권장)

아래 절차는 키 입력으로 다양한 케이스를 재현하기 위한 방법입니다.

## 5단계: 공격자 오브젝트 생성
1. Hierarchy에서 빈 GameObject를 생성하고 이름을 `Attacker`로 지정합니다.
2. `Attacker`에 `HitTestAttacker` 스크립트를 추가합니다.
3. `HitTestAttacker`의 타겟 지정 방식은 두 가지 중 선택합니다.
   - 수동: `_targetReceiver`에 `Target`의 `HitReceiver`를 연결
   - 자동(2D): `_useAutoFindTarget = true`로 두고 아래 자동 탐색 옵션을 설정
     - `_autoFindRadius`: 탐색 반경(예: 5)
     - `_targetLayerMask`: Enemy 레이어만 포함 (2D 레이어 마스크)
     - `_targetTag`: `Enemy`
     - `_requireTargetTag`: true
     - `_preferClosestTarget`: true
4. 다중 타겟 설정(한 번의 Hit에 여러 Target 적용)
   - `_useMultiTargetPerHit`: true
   - `_maxTargetsPerHit`: 한 번의 Hit에 적용할 최대 대상 수(예: 3~5)
5. 자동 타겟을 수동 갱신하려면 플레이 중 `Tab` 키를 사용합니다.
6. 기본 키 매핑 예시:
   - `1`: 정상 피격
   - `2`: 중복 HitId 피격
   - `3`: 빈 HitId
   - `4`: 0 데미지
   - `5`: 음수 데미지
   - `6`: NaN 데미지
   - `7`: Infinity 데미지
   - `I`: 대상 무적 토글

## 6단계: 테스트 드라이버 동작 설계
테스트 드라이버는 `Physics2D.OverlapCircleAll` 기반 자동 타겟 탐색을 사용합니다.
테스트 대상에는 `Collider2D`가 반드시 있어야 자동 탐색 후보로 잡힙니다.

테스트 드라이버는 단일 타겟/다중 타겟 모두 검증하도록 아래 케이스를 최소 포함해 설계합니다.

- 정상 피격: 고유 `HitId`, 양수 데미지
- 중복 피격: 직전에 사용한 `HitId` 재사용
- 무적 피격: `_isInvincible = true` 상태에서 요청
- 사망 이후 피격: 대상 체력을 0으로 만든 후 재요청
- 비정상 입력:
  - 빈 `HitId`
  - 0 이하 데미지
  - `NaN`/`Infinity` 데미지

---

## 권장 테스트 시나리오 (실행 순서)

## 시나리오 A: 정상 수락
1. 플레이 모드 진입
2. 정상 요청 1회 전송 (`HitId=hit_001`, `RawDamage=10`)
3. 기대 결과
   - `HitResult.IsAccepted == true`
   - `RejectReason == None`
   - 체력: `100 -> 90`

## 시나리오 B: 중복 HitId 거부
1. 동일한 `HitId=hit_001`로 재요청
2. 기대 결과
   - `IsAccepted == false`
   - `RejectReason == DuplicateHitId`
   - 체력 변화 없음

## 시나리오 C: 무적 상태 거부
1. `Target`의 `HitReceiver.SetInvincible(true)` 적용
2. 새 `HitId=hit_002` 요청
3. 기대 결과
   - `IsAccepted == false`
   - `RejectReason == TargetInvincible`
   - 체력 변화 없음

## 시나리오 D: 사망 상태 거부
1. 무적 해제 후(`SetInvincible(false)`), 충분한 데미지로 대상 사망 처리
2. 새 `HitId=hit_003` 요청
3. 기대 결과
   - 대상 사망 후 추가 요청은 `TargetDead`로 거부

## 시나리오 E: 입력 검증 거부
1. 빈 `HitId` 요청
2. `RawDamage = 0` 요청
3. `RawDamage = -1` 요청
4. `RawDamage = float.NaN` 요청
5. 기대 결과
   - 각각 `InvalidHitId` 또는 `InvalidDamage`
   - `Debug.LogWarning` 출력 확인

## 시나리오 F: Health 시스템 차단 매핑 확인
1. Health 시스템 모디파이어로 데미지가 차단되는 조건을 구성합니다.
2. 새 요청 전송
3. 기대 결과
   - Hit 측 `RejectReason == BlockedByHealthSystem`
   - 체력 변화 없음

---


## 시나리오 G: 다중 타겟 동시 피격
1. `_useMultiTargetPerHit = true`로 설정합니다.
2. 반경 안에 Enemy `HitReceiver`가 붙은 대상 2개 이상을 배치합니다.
3. 정상 요청(예: `1` 키)을 1회 전송합니다.
4. 기대 결과
   - 동일한 `HitId`가 여러 타겟에 각각 처리됩니다.
   - 각 타겟에서 `HitResult`가 개별적으로 로그 출력됩니다.
   - `_maxTargetsPerHit`를 초과하지 않는 수만 적용됩니다.

## 로그 확인 포인트

`HitDebugListener` 로그에서 아래 항목을 확인합니다.

- `HitId`
- `Accepted`
- `Reason`
- `AppliedDamage`
- `HealthBefore -> HealthAfter`
- `Dead`

예상 로그 패턴:

- 성공: `Accepted=True, Reason=None`
- 거부: `Accepted=False, Reason=<거부사유>`

---

## 트러블슈팅

## 1) HitReceiver가 항상 ReceiverNotReady를 반환하는 경우
- `Target`에 기존 Health 시스템 `HealthComponent`가 붙어있는지 확인합니다.
- `HitReceiver`의 `_healthComponent` 참조가 비어있는지 확인합니다.

## 2) 체력이 줄지 않는 경우
- `_incomingDamageMultiplier`가 0인지 확인합니다.
- Health 모디파이어가 데미지를 차단하고 있지 않은지 확인합니다.
- `HitResult.RejectReason`이 `BlockedByHealthSystem`인지 확인합니다.

## 3) 중복 판정이 예상과 다른 경우
- 요청마다 `HitId`를 고유하게 생성하는지 확인합니다.
- 의도적 중복 테스트에서는 정확히 같은 `HitId`를 재사용해야 합니다.

---

## 완료 기준 체크리스트

아래 항목이 모두 만족되면 테스트 Scene 구성이 완료된 것입니다.

- [ ] 정상 요청이 수락되고 체력이 감소한다.
- [ ] 동일 `HitId` 재요청이 거부된다.
- [ ] 무적 상태에서 요청이 거부된다.
- [ ] 사망 상태에서 요청이 거부된다.
- [ ] 비정상 입력이 거부된다.
- [ ] 거부 사유가 `HitResult.RejectReason`으로 명확히 관찰된다.
- [ ] 폴백/비정상 케이스에서 `Debug.LogWarning`이 출력된다.
- [ ] `HitDebugListener`가 결과를 로그로 보여준다.

---

## 운영 팁

- 실제 전투 시스템에서는 `HitId`를 `공격ID + 프레임 + 대상ID` 형태로 구성하면 중복 제어가 쉬워집니다.
- `StatusTag`는 추후 상태이상(예: Stun, Burn) 분기용 확장 포인트로 활용할 수 있습니다.
- 네트워크 환경에서는 서버 권위 기준으로 `HitId` 생성/검증 전략을 통일하는 것을 권장합니다.
