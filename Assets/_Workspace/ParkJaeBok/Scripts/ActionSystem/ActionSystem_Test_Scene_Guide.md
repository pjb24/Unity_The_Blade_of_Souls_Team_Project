# Action System 테스트 Scene 설정 가이드

이 문서는 현재 프로젝트의 Action System(`ActionController`, `ActionAnimationPresenter`, `ActionMarkerReceiver`, `ActionSystemInputTest`)을 **새 테스트 Scene**에서 바로 검증할 수 있도록, Unity Editor 기준으로 상세 절차를 제공합니다.

---

## 0) 준비 사항

- Unity 프로젝트에 아래 스크립트가 존재해야 합니다.
  - `ActionController`
  - `ActionRuntime`
  - `ActionAnimationPresenter`
  - `ActionMarkerReceiver`
  - `ActionSystemInputTest`
  - `IActionListener`
  - `E_ActionEnums`
- Unity New Input System 패키지가 활성화되어 있어야 합니다.
  - `Edit > Project Settings > Player > Active Input Handling`에서 `Input System Package (New)` 또는 `Both` 설정

---

## 1) 테스트 Scene 생성

1. `Assets/Settings/Scenes` 폴더(또는 원하는 테스트 폴더)에서 새 Scene을 생성합니다.
2. Scene 이름 예시: `ActionSystemTest`.
3. Scene을 열고 저장합니다.

---

## 2) 테스트용 캐릭터 오브젝트 구성

1. Hierarchy에서 빈 오브젝트를 생성하고 이름을 `Player_ActionTest`로 지정합니다.
2. `Player_ActionTest`에 아래 컴포넌트를 추가합니다.
   - `Animator`
   - `ActionController`
   - `ActionAnimationPresenter`
   - `ActionMarkerReceiver`
   - `ActionSystemInputTest`
3. 2D 시각 확인을 위해 필요하면 `SpriteRenderer`를 추가합니다.

---

## 3) Animator Controller 준비 및 상태(State) 구성

1. `Assets/_Workspace/ParkJaeBok/Scripts/ActionSystem` 또는 테스트용 폴더에 Animator Controller를 생성합니다.
   - 이름 예시: `AC_ActionSystemTest`
2. `Player_ActionTest`의 `Animator` 컴포넌트의 `Controller`에 `AC_ActionSystemTest`를 할당합니다.
3. Animator Window에서 최소 12개 상태를 만듭니다.
   - `Idle`
   - `Move`
   - `Jump`
   - `Land`
   - `Dash`
   - `Falling`
   - `WallSlide`
   - `WallJump`
   - `Slide`
   - `Attack`
   - `Hit`
   - `Die`
4. 각 상태에 임시 애니메이션 클립을 연결합니다.
   - 없으면 길이 0.3~1.0초의 테스트용 클립을 만들어 연결해도 됩니다.
5. `Idle`을 기본(Default) 상태로 지정합니다.

---

## 4) ActionController 설정

※ 이 섹션의 Rule 설정은 `ActionRuleProfile` 자산 생성/연결을 전제로 합니다.

`Player_ActionTest`의 `ActionController`를 아래처럼 설정합니다.

1. `Auto Start Idle`를 `true`로 둡니다.

### ActionRuleProfile 생성 상세 절차

1. Project 창의 빈 공간에서 우클릭합니다.
2. `Create > ActionSystem > Action Rule Profile`를 선택합니다.
3. 자산 이름을 입력합니다.
   - 예시: `ARP_Player_Default`
4. 생성된 `ActionRuleProfile` 자산을 선택하고 Inspector를 엽니다.
5. `Actor Type`을 `Player`로 설정합니다.
6. `Rules` 배열 Size를 `12`로 설정합니다.
7. 아래 권장 Rule 예시를 기준으로 각 인덱스를 입력합니다.
8. `Player_ActionTest` 오브젝트의 `ActionController._actionRuleProfile` 슬롯에 해당 자산을 드래그해 연결합니다.
9. 필요하면 `ARP_Enemy_Default`, `ARP_Boss_Default`처럼 액터 타입별로 별도 프로필 자산을 추가 생성해 분리 관리합니다.

### 권장 Rule 예시

- Rule 0
  - `ActionType = Idle`
  - `Priority = 0`
  - `IsInterruptible = true`
  - `AutoCompleteSeconds = 0`
- Rule 1
  - `ActionType = Move`
  - `Priority = 1`
  - `IsInterruptible = true`
  - `AutoCompleteSeconds = 0`
- Rule 2
  - `ActionType = Jump`
  - `Priority = 3`
  - `IsInterruptible = true`
  - `AutoCompleteSeconds = 0.45`
- Rule 3
  - `ActionType = Land`
  - `Priority = 4`
  - `IsInterruptible = true`
  - `AutoCompleteSeconds = 0.25`
- Rule 4
  - `ActionType = Dash`
  - `Priority = 5`
  - `IsInterruptible = true`
  - `AutoCompleteSeconds = 0.3`
- Rule 5
  - `ActionType = Falling`
  - `Priority = 6`
  - `IsInterruptible = true`
  - `AutoCompleteSeconds = 0`
- Rule 6
  - `ActionType = WallSlide`
  - `Priority = 7`
  - `IsInterruptible = true`
  - `AutoCompleteSeconds = 0`
- Rule 7
  - `ActionType = WallJump`
  - `Priority = 8`
  - `IsInterruptible = true`
  - `AutoCompleteSeconds = 0.45`
- Rule 8
  - `ActionType = Slide`
  - `Priority = 9`
  - `IsInterruptible = true`
  - `AutoCompleteSeconds = 0`
- Rule 9
  - `ActionType = Attack`
  - `Priority = 10`
  - `IsInterruptible = true`
  - `AutoCompleteSeconds = 0.5`
- Rule 10
  - `ActionType = Hit`
  - `Priority = 10`
  - `IsInterruptible = true`
  - `AutoCompleteSeconds = 0.35`
- Rule 11
  - `ActionType = Die`
  - `Priority = 100`
  - `IsInterruptible = false`
  - `AutoCompleteSeconds = 0`

이 값으로 설정하면 다음이 잘 보입니다.
- `Hit`가 `Attack`, `Move`를 끊는지
- `Die` 상태일 때 다른 요청이 거부되는지
- `Attack`/`Hit`가 자동 완료되는지

---

## 5) ActionAnimationPresenter 설정

`Player_ActionTest`의 `ActionAnimationPresenter`를 아래처럼 설정합니다.

1. `_actionController`에 같은 오브젝트의 `ActionController`를 연결합니다.
2. `_animator`에 같은 오브젝트의 `Animator`를 연결합니다.
3. `Create > ActionSystem > Animation State Map Profile`로 매핑 프로필 자산을 생성합니다.
4. 생성한 `AnimationStateMapProfile`의 `State Maps` 배열 크기를 12로 설정하고 아래처럼 매핑합니다.
   - `Idle`  -> `StateName = Idle`, `LayerIndex = 0`, `IsOneShot = false`
   - `Move`  -> `StateName = Move`, `LayerIndex = 0`, `IsOneShot = false`
   - `Jump` -> `StateName = Jump`, `LayerIndex = 0`, `IsOneShot = true`
   - `Land` -> `StateName = Land`, `LayerIndex = 0`, `IsOneShot = true`
   - `Dash` -> `StateName = Dash`, `LayerIndex = 0`, `IsOneShot = true`
   - `Falling` -> `StateName = Falling`, `LayerIndex = 0`, `IsOneShot = false`
   - `WallSlide` -> `StateName = WallSlide`, `LayerIndex = 0`, `IsOneShot = false`
   - `WallJump` -> `StateName = WallJump`, `LayerIndex = 0`, `IsOneShot = true`
   - `Slide` -> `StateName = Slide`, `LayerIndex = 0`, `IsOneShot = false`
   - `Attack` -> `StateName = Attack`, `LayerIndex = 0`, `IsOneShot = true`
   - `Hit` -> `StateName = Hit`, `LayerIndex = 0`, `IsOneShot = true`
   - `Die` -> `StateName = Die`, `LayerIndex = 0`, `IsOneShot = true`
5. `ActionAnimationPresenter._stateMapProfile`에 생성한 매핑 프로필 자산을 연결합니다.
6. (선택) 리스너 지연 등록 재시도 값
   - `Retry Interval = 0.1`
   - `Max Retry Count = 30`

> 매핑이 틀리거나 누락되면 Warning 로그가 출력되며 Idle 폴백이 시도됩니다.
>
> `IsOneShot = true`인 액션은 프리젠터가 애니메이션 normalizedTime을 감시해 1회 재생 완료 시 `CompleteCurrentAction()`을 자동 요청합니다.

### Presentation Lock(마지막 상태 고정 + 해제)

- 특정 액션 재생 후 다른 애니메이션 전환을 막고 마지막 상태를 유지하려면 아래를 설정합니다.
  - `_enablePresentationLock = true`
  - `_presentationLockTriggerActions`에 잠금 시작 액션 추가
    - 예: `Die`
  - `_presentationLockReleaseActions`에 잠금 해제 허용 액션 추가
    - 예: `Spawn`, `Activate`, `Use` 등 프로젝트에서 Revive 성격으로 사용하는 임의 액션
- 잠금 상태에서는 release 목록에 없는 액션으로의 전환이 거부됩니다.
- 잠금 해제 허용 액션이 시작되면 잠금이 해제되고 해당 애니메이션 전환은 정상 재생됩니다.

### Idle Break(랜덤 Idle 변주) 설정

- `ActionAnimationPresenter`에서 아래 값을 설정합니다.
  - `_stateMapProfile` 연결이 완료되어 있어야 Idle Break 액션 매핑도 정상 재생됩니다.
  - `_enableIdleBreak = true`
  - `_idleBreakBaseAction = Idle`
  - `_idleBreakActions`에 `IdleBreakA`, `IdleBreakB`, `IdleBreakC`처럼 변주 액션 추가
  - `_idleBreakMinDelaySeconds`, `_idleBreakMaxDelaySeconds`로 랜덤 대기 범위 설정
- `State Maps`에 Idle Break 액션도 함께 매핑합니다.
  - 예: `IdleBreakA/B/C` -> 각 상태 이름, `IsOneShot = true`
- Idle 상태가 오래 유지되면 Presenter가 배열에서 랜덤으로 1개를 재생한 뒤 다시 Idle로 복귀합니다.

---

## 6) ActionMarkerReceiver 설정 + Animation Event 연결

1. `ActionMarkerReceiver`의 `_actionController`를 같은 오브젝트의 `ActionController`로 연결합니다.
2. `Create > ActionSystem > Animation Marker Profile`로 marker 프로필 자산을 생성합니다.
3. `ActionController._animationMarkerProfile`에 생성한 marker 프로필 자산을 연결합니다.
4. marker 프로필의 `Marker Maps` 배열에 아래 marker를 추가합니다.
   - `start` (`CommandType = SetPhase`, `TargetPhase = Start`)
   - `progress` (`CommandType = SetPhase`, `TargetPhase = Progress`)
   - `complete` (`CommandType = Complete`)
   - `cancel` (`CommandType = Cancel`, `CancelReason = Animation marker cancel`)
   - `jump/land/dash/falling/wallslide/walljump/slide` (`CommandType = RequestAction`, `TargetActionType` 설정)
5. `Attack`, `Hit` 클립에 Animation Event를 추가합니다.
6. Event 함수는 `ReceiveMarker(string marker)`를 선택하고 문자열 파라미터를 marker 프로필에 등록한 값으로 입력합니다.

### 추천 Event 구성

- `Attack` 클립
  - 0.0초: `start`
  - 타격 타이밍 근처: `progress`
  - 클립 끝: `complete`
- `Hit` 클립
  - 0.0초: `start`
  - 클립 끝: `complete`
- 이동/루프 클립에서 점프 전환이 필요할 때
  - 점프 시작 프레임: `jump`
  - 착지 접지 프레임: `land`
  - 대시 시작 프레임: `dash`
  - 낙하 전환 프레임: `falling`
  - 벽 접촉 슬라이드 프레임: `wallslide`
  - 벽 점프 시작 프레임: `walljump`
  - 급경사 미끄러짐 시작 프레임: `slide`

> 문자열이 잘못되면 Unknown marker Warning 로그가 출력됩니다.

---

## 6-1) ActionHitBridge 설정(피격 이벤트 연동 시)

1. `Player_ActionTest`에 `ActionHitBridge`를 추가합니다.
2. `ActionHitBridge._hitReceiver`를 같은 오브젝트의 `HitReceiver`로 연결합니다.
3. `ActionHitBridge._actionController`를 같은 오브젝트의 `ActionController`로 연결합니다.
4. `Create > ActionSystem > Action Hit Bridge Profile`로 브리지 프로필 자산을 생성합니다.
5. `ActionHitBridge._hitBridgeProfile`에 생성한 자산을 연결합니다.
6. 프로필에서 아래 값을 점검합니다.
   - `HitActionType`
   - `BreakActionType`
   - `DeadActionType`
   - `BreakStatusTags`

---

## 7) New Input System 액션 에셋 생성

1. Project 창에서 `Create > Input Actions`로 액션 에셋을 생성합니다.
   - 이름 예시: `ActionSystemTest.inputactions`
2. 더블클릭해서 Input Actions 편집기를 엽니다.
3. Action Map 이름을 `Player`로 생성합니다.
4. 아래 Actions를 생성하고 `Action Type = Button`으로 둡니다.
   - `Idle`
   - `Move`
   - `Jump`
   - `Land`
   - `Dash`
   - `Falling`
   - `WallSlide`
   - `WallJump`
   - `Slide`
   - `Attack`
   - `Hit`
   - `Die`
   - `Complete`
   - `Cancel`
5. 각 Action에 키보드 바인딩을 추가합니다.
   - Idle: `1`
   - Move: `2`
   - Jump: `3`
   - Land: `4`
   - Dash: `5`
   - Falling: `6`
   - WallSlide: `7`
   - WallJump: `8`
   - Slide: `9`
   - Attack: `0`
   - Hit: `-`
   - Die: `P`
   - Complete: `C`
   - Cancel: `X`
6. 저장(Save Asset)합니다.

---

## 8) ActionSystemInputTest 연결

`Player_ActionTest`의 `ActionSystemInputTest`를 설정합니다.

1. `_actionController`에 같은 오브젝트의 `ActionController`를 연결합니다.
2. 방금 만든 `ActionSystemTest.inputactions`의 각 Action을 `InputActionReference`로 드래그해 연결합니다.
   - `_idleAction` <- `Player/Idle`
   - `_moveAction` <- `Player/Move`
   - `_jumpAction` <- `Player/Jump`
   - `_landAction` <- `Player/Land`
   - `_dashAction` <- `Player/Dash`
   - `_fallingAction` <- `Player/Falling`
   - `_wallSlideAction` <- `Player/WallSlide`
   - `_wallJumpAction` <- `Player/WallJump`
   - `_slideAction` <- `Player/Slide`
   - `_attackAction` <- `Player/Attack`
   - `_hitAction` <- `Player/Hit`
   - `_dieAction` <- `Player/Die`
   - `_completeAction` <- `Player/Complete`
   - `_cancelAction` <- `Player/Cancel`

---

## 9) 실행 테스트 체크리스트

Play Mode에서 아래를 순서대로 검증합니다.

1. 시작 직후 Idle 재생 확인
   - `Auto Start Idle`이 true면 Idle로 시작해야 합니다.
2. `2` 입력 시 Move 재생 확인
3. `3` 입력 시 Jump 재생 및 자동 완료 확인
4. `4` 입력 시 Land 재생 및 자동 완료 확인
5. `5` 입력 시 Dash 재생 및 자동 완료 확인
6. `6` 입력 시 Falling 재생(루프) 확인
7. `7` 입력 시 WallSlide 재생(루프) 확인
8. `8` 입력 시 WallJump 재생 및 자동 완료 확인
9. `9` 입력 시 Slide 재생(루프) 확인
10. `0` 입력 시 Attack 재생 및 자동 완료 확인
   - 설정한 `AutoCompleteSeconds` 또는 marker `complete`로 종료
11. `-` 입력 시 Hit가 Attack/Move를 인터럽트하는지 확인
12. `P` 입력 시 Die 진입 후 다른 액션 요청 거부 확인
13. `C`, `X` 입력으로 수동 완료/취소 확인
14. 콘솔 Warning 확인
   - 매핑 누락, Rule 프로필 누락, marker 프로필 누락/오타 등 오류 상황이 로그로 노출되는지 확인

---

## 10) 자주 발생하는 문제와 해결

### 문제 A) 아무 입력도 반응하지 않음

- Input Actions 에셋 바인딩이 `ActionSystemInputTest`에 연결되었는지 확인
- Project Settings의 Active Input Handling 확인
- Play 중 해당 액션이 Enable 되는지 확인

### 문제 B) 애니메이션이 안 바뀜

- `ActionAnimationPresenter._animator` 누락 여부 확인
- `AnimationStateMapProfile.State Maps`의 `StateName`이 Animator State 이름과 정확히 일치하는지 확인
- `ActionAnimationPresenter._stateMapProfile` 연결 여부 확인
- Animator Layer Index 확인(일반적으로 0)

### 문제 C) marker가 동작하지 않음

- Animation Event 함수명이 `ReceiveMarker`인지 확인
- marker 문자열이 `AnimationMarkerProfile.Marker Maps`에 등록되어 있는지 확인
- `ActionMarkerReceiver._actionController` 연결 확인

### 문제 D) 리스너 등록/해제 관련 경고 발생

- `ActionHitBridge._hitBridgeProfile` 연결 여부 확인

- `ActionAnimationPresenter._actionController` 연결 확인
- 동일 오브젝트 fallback에 의존 중인지 로그 확인
- 비활성화/파괴 순서에서 경고만 발생하고 치명 오류가 없는지 확인

### 문제 E) 특정 상태 이후 애니메이션 전환이 계속 거부됨

- `_enablePresentationLock`가 켜져 있는지 확인
- `_presentationLockTriggerActions`에 잠금 시작 액션이 포함되어 있는지 확인
- `_presentationLockReleaseActions`에 해제 허용 액션(Revive 성격)이 포함되어 있는지 확인

---

## 11) 확장 테스트 아이디어

- `ActionRule` 우선순위를 바꿔 인터럽트 정책 회귀 테스트
- `Die`를 인터럽트 가능하게 변경 시 예상치 못한 상태 전이 검증
- `AnimationStateMapProfile.State Maps`에서 일부 항목 제거해 Warning + Idle 폴백 검증
- marker에 `cancel`을 넣어 취소 루트 테스트
- ActionListener를 2개 이상 만들어 멀티 구독 시나리오 검증

---

## 12) 권장 점검 로그 포인트

테스트 중 콘솔에서 아래 로그 prefix를 기준으로 확인하세요.

- `[ActionController]`
- `[ActionAnimationPresenter]`
- `[ActionMarkerReceiver]`
- `[ActionSystemInputTest]`

정상/비정상 흐름 모두 로그를 남기도록 구성되어 있으므로, 설정 실수 추적이 쉽습니다.

---

## 13) 액터 타입별 권장 ActionRule 프로필

`ActionController._actorType`를 기준으로 아래처럼 Rule을 구성하면 Player, Enemy, Boss, Item, DestructibleObject를 같은 시스템으로 운용할 수 있습니다.

- Player
  - `Idle`, `Move`, `Attack`, `Hit`, `Die`, `Use` 활성화
- Enemy
  - `Idle`, `Move`, `Attack`, `Hit`, `Die` 활성화
- Boss
  - `Idle`, `Move`, `Attack`, `Hit`, `Die`, `Spawn`, `Activate` 활성화
- Item
  - `Spawn`, `Idle`, `Use`, `Activate`, `Open`, `Close` 활성화
  - `Move`, `Attack`, `Hit`, `Die`는 비활성화 가능
- DestructibleObject
  - `Idle`, `Hit`, `Break` 활성화
  - 필요 시 `Die` 대신 `Break`를 최종 파괴 액션으로 사용

> 핵심은 모든 액터가 같은 `ActionController`를 쓰되, 액터 타입과 `ActionRuleProfile` 자산을 다르게 연결하는 방식입니다.
