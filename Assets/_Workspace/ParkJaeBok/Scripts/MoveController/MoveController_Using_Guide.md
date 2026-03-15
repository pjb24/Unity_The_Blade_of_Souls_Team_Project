# MoveController 스크립트 사용 가이드

이 문서는 `Assets/_Workspace/ParkJaeBok/Scripts/MoveController` 폴더 아래의 스크립트들을 실제 프로젝트에서 어떻게 연결하고 사용하는지 설명합니다.

---

## 1) 구성 파일 한눈에 보기

- 핵심 플레이어 이동
  - `PlayerMovement.cs`: 입력/상태/속도 계산의 중심 로직
  - `MovementController.cs`: 레이캐스트 기반 지면/벽/천장 충돌 감지 및 실제 이동 적용
  - `PlayerMovementStats.cs`: 이동 파라미터를 담는 ScriptableObject
- 시각 보간
  - `VisualInterpolator.cs`: 물리 프레임과 렌더 프레임 간 위치 보간
- 플랫폼 시스템
  - `Platforms/IVelocityInheritable.cs`: 플랫폼 속도 상속 인터페이스
  - `Platforms/MovingPlatform.cs`: 왕복 이동 플랫폼
  - `Platforms/ConveyorBelt.cs`: 컨베이어 벨트형 플랫폼
- 에디터 확장
  - `PlayerMovementStatsEditor.cs`: 대시 각도 시각화 커스텀 인스펙터

---

## 2) 기본 셋업 순서 (플레이어)

1. **플레이어 GameObject 생성**
   - `Rigidbody2D` 추가 (2D 물리 이동 대상)
   - `BoxCollider2D` 추가 (충돌 감지용)

2. **필수 스크립트 추가**
   - 같은 오브젝트에 `PlayerMovement` 추가
   - 같은 오브젝트에 `MovementController` 추가  
     (`MovementController`는 `[RequireComponent(typeof(PlayerMovement))]`로 `PlayerMovement`를 전제로 동작)

3. **비주얼 루트 구성**
   - 플레이어의 자식으로 비주얼 오브젝트(스프라이트 루트) 생성
   - 해당 자식에 `VisualInterpolator` 추가
   - `PlayerMovement`의 `_visualsTransform`에 비주얼 루트 연결

4. **리스폰 포인트 연결**
   - 빈 오브젝트를 만들어 리스폰 위치로 사용
   - `PlayerMovement`의 `_respawnPoint`에 연결

5. **스탯 에셋 생성 및 연결**
   - Create 메뉴에서 `Player Movement` 에셋 생성 (`PlayerMovementStats`)
   - 생성한 에셋을 `PlayerMovement.MoveStats`에 할당

6. **Ground Layer 설정**
   - `MoveStats.GroundLayer`에 지면/벽/경사/플랫폼 레이어를 포함
   - 충돌 오브젝트가 해당 레이어에 실제로 배치되어 있는지 확인

---

## 3) 스크립트별 사용 방법

## 3-1) `PlayerMovement.cs`

### 역할
- 입력(`InputManager`)을 수집하고, 물리 프레임(`FixedUpdate`)에서 이동 상태를 계산합니다.
- 점프/벽슬라이드/벽점프/대시/낙하/경사 이동을 통합 처리합니다.
- 최종적으로 `Controller.Move(Velocity * Time.fixedDeltaTime)`를 호출해 실제 이동을 적용합니다.

### 사용 포인트
- `MoveStats`에 반드시 유효한 `PlayerMovementStats`를 연결해야 합니다.
- `Controller`(= `MovementController`)가 같은 오브젝트에 있어야 합니다.
- `_coll`, `_visualsTransform`, `_respawnPoint`의 참조 누락이 없도록 인스펙터에서 확인합니다.

### 자주 보는 공개 상태값
- `IsDashing`: 현재 대시 중인지
- `Velocity`: 현재 계산된 속도
- `IsRunning`: 달리기 입력 유지 여부

### 연동 참고
- 입력은 `InputManager` 정적 값(`Movement`, `RunIsHeld`, `JumpWasPressed` 등)을 사용합니다.
- 압사 시 `MovementController.OnCrush` 이벤트를 받아 `HandleCrush()`로 복귀 처리합니다.

---

## 3-2) `MovementController.cs`

### 역할
- 레이캐스트 기반 센서로 지면/벽/천장/경사 상태를 수집합니다.
- 플랫폼 속도 상속, 코너 보정, 경사 오르내림/스텝 처리 등을 담당합니다.
- 충돌 보정을 반영한 최종 위치 이동을 담당합니다.

### 핵심 호출 흐름
- `PollSensors(moveDelta)`: 현재 프레임 충돌 상태 수집
- `Move(velocityDelta)`: 플랫폼 이동량 + 충돌 보정 반영 후 Rigidbody 이동

### 주요 공개 정보
- `State`: `CollisionState` 스냅샷 (Grounded, Wall, Ceiling, Slope 정보)
- `IsSliding`: 최대 경사 초과 슬라이딩 여부
- `IsOnPlatform`, `LastKnownPlatform`: 플랫폼 탑승 상태
- `OnCrush`: 외부에 눌려 압사될 때 발생하는 이벤트

### 튜닝 포인트
- `NumOfHorizontalRays`, `NumOfVerticalRays`: 충돌 감지 정밀도
- `_verticalProbeDistance`, `_horizontalProbeDistance`: 센서 거리
- `_safetyGraceDuration`: 지면 재검출 유예

> 레이 수를 과도하게 높이면 성능 비용이 증가할 수 있으니, 문제 재현되는 구간 위주로 점진적으로 조정하는 것을 권장합니다.

---

## 3-3) `PlayerMovementStats.cs`

### 역할
- 이동 전반의 수치(걷기/달리기/점프/벽점프/대시/경사/코너보정/디버그)를 중앙 관리합니다.

### 사용 방법
1. `Player Movement` ScriptableObject 생성
2. 플레이어별로 동일 에셋 공유 또는 개별 에셋 분리
3. 플레이 중 필요한 체감(가속, 점프 높이, 대시 시간)을 인스펙터에서 즉시 조정

### 특히 먼저 맞추면 좋은 그룹
- `Walk`, `Run`: 기본 이동 감도
- `Jump`, `Gravity`: 점프 체감
- `Dash`: 전투/회피 템포
- `Slopes`: 지형 대응
- `Platforms`: 이동 플랫폼 관성 전달

### 주의사항
- `OnValidate()`에서 파생 물리값을 재계산하므로, 관련 값들을 함께 조정해야 의도한 체감이 나옵니다.
- `GroundLayer`가 잘못되면 거의 모든 이동 판정(접지/벽/경사)이 틀어집니다.

---

## 3-4) `VisualInterpolator.cs`

### 역할
- 물리 프레임 위치(`FixedUpdate`)와 렌더 프레임(`LateUpdate`) 사이를 보간하여 흔들림을 줄입니다.

### 사용 방법
- 보통 플레이어 비주얼 루트(자식 오브젝트)에 부착합니다.
- `PlayerMovement.FixedUpdate()` 마지막의 `_visuals.UpdatePhysicsState()` 호출로 상태를 갱신합니다.
- 강제 순간이동 시 `ForceTeleport(pos)`를 호출해 물리/비주얼 위치를 즉시 동기화합니다.

### 주의사항
- `Start()`에서 부모를 분리(`transform.parent = null`)하므로, 계층 구조 의존 로직이 있다면 사전에 고려해야 합니다.

---

## 3-5) 플랫폼 스크립트 (`Platforms`)

## `IVelocityInheritable.cs`

### 역할
- 플레이어가 밟은 오브젝트로부터 속도를 상속받기 위한 공통 계약입니다.

### 구현해야 하는 항목
- `Vector2 GetVelocity()`
- `bool ProbesShouldLead { get; set; }`
- `bool ImpartMomentumOnExit { get; set; }`
- `bool LaunchVerticallyOnExit { get; }`
- `bool NeedsFuturePositionBoxcastCheck { get; }`

플랫폼 계열 오브젝트를 새로 만들 때 이 인터페이스를 구현하면 `MovementController`가 플랫폼으로 인식합니다.

## `MovingPlatform.cs`

### 역할
- 시작점과 끝점을 왕복(또는 단방향) 이동하는 키네마틱 플랫폼입니다.
- 필요 시 탑승 객체를 물리적으로 밀어내며 이동합니다.

### 사용 방법
1. 플랫폼 오브젝트에 `Rigidbody2D` + `BoxCollider2D` 추가
2. `MovingPlatform` 부착
3. `_moveOffset`, `_duration`, `_easeCurve`, `_loop` 설정
4. 승객 밀기 로직을 사용할 경우 `_pushPassenger`, `_passengerMask` 설정

## `ConveyorBelt.cs`

### 역할
- 오브젝트의 로컬 `right` 방향으로 일정 속도를 전달하는 벨트 플랫폼입니다.

### 사용 방법
1. 벨트 오브젝트에 콜라이더 추가
2. `ConveyorBelt` 부착
3. `_moveSpeed`로 속도 설정 (음수면 반대 방향)
4. 필요 시 `_launchOnExit`, `_checkForWalls` 조정

---

## 3-6) `PlayerMovementStatsEditor.cs` (에디터 전용)

### 역할
- `PlayerMovementStats` 인스펙터에서 대시 방향 허용 각도를 원형 섹터로 시각화합니다.

### 사용 방법
- 별도 설정 없이 `PlayerMovementStats` 에셋을 선택하면 자동 동작합니다.
- `DashUpwardAngleTolerance`, `DashDownwardAngleTolerance`, `DashHorizontalAngleTolerance` 조정 시 시각화가 즉시 반영됩니다.

---

## 4) 실제 적용 체크리스트

- [ ] 플레이어에 `Rigidbody2D`, `BoxCollider2D`, `PlayerMovement`, `MovementController`가 모두 붙어 있는가?
- [ ] `PlayerMovement.MoveStats`에 `PlayerMovementStats`가 연결되어 있는가?
- [ ] `MoveStats.GroundLayer`에 지면/벽/플랫폼 레이어가 포함되어 있는가?
- [ ] 비주얼 루트에 `VisualInterpolator`가 있고 참조가 정상인가?
- [ ] 플랫폼 오브젝트가 `IVelocityInheritable` 구현 컴포넌트(`MovingPlatform`, `ConveyorBelt`)를 사용 중인가?
- [ ] 점프/대시/벽점프 체감이 의도와 다르면 `MoveStats` 값부터 조정했는가?

---

## 5) 자주 발생하는 문제와 점검 방법

1. **점프가 안 됨 / 접지 판정이 불안정함**
   - `GroundLayer` 설정 확인
   - 플레이어 콜라이더 크기/오프셋이 비정상인지 확인
   - `MovementController`의 레이 수/프로브 거리 확인

2. **플랫폼 위에서 미끄러지거나 속도가 이상함**
   - 플랫폼이 `IVelocityInheritable`를 구현했는지 확인
   - `MoveStats.InheritPlatformMomentum` 및 배율 계수 확인
   - 플랫폼 콜라이더와 플레이어 콜라이더 접점 확인

3. **비주얼이 떨리거나 몸체와 어긋남**
   - `VisualInterpolator`가 비주얼 루트에 붙어 있는지 확인
   - `PlayerMovement.FixedUpdate()`에서 `UpdatePhysicsState()`가 호출되는지 확인
   - 순간이동 시 `ForceTeleport()`를 사용하는지 확인

4. **대시 방향이 의도와 다름**
   - `DashLeftRightOnly` 옵션 확인
   - 대시 각도 허용치(Up/Down/Horizontal Tolerance) 확인
   - `PlayerMovementStatsEditor` 시각화로 허용 섹터 확인

---

## 6) 확장 팁

- 새로운 플랫폼 타입을 만들고 싶다면 `IVelocityInheritable`를 구현하고, `GetVelocity()`와 이탈 시 관성 옵션만 올바르게 제공해도 기본 연동이 가능합니다.
- 캐릭터별 이동 감각 차별화가 필요하면 `PlayerMovementStats` 에셋을 캐릭터별로 분리해 운영하는 방식이 가장 안전합니다.
- 디버그 옵션(`DebugShow...`)을 활용하면 경사/벽/코너 보정 문제를 빠르게 재현하고 튜닝할 수 있습니다.