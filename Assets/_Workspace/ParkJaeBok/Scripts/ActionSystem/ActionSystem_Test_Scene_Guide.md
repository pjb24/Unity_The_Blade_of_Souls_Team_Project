# ActionSystem_Test_Scene_Guide

이 문서는 현재 구현된 Object Marker 기반 ActionSystem을 처음부터 끝까지 테스트하기 위한 절차서입니다.

문서 목표:
1. 테스트 씬 생성부터 컴포넌트/프로필 연결까지 빠짐없이 수행
2. marker를 통한 Combo/Hit window + lifecycle 신호를 정확히 테스트

---

## 0) 핵심 전제 (반드시 확인)

현재 시스템은 아래 원칙으로 동작합니다.

- Object marker 명령(`ActionMarkerCommandObject`) 기반으로 구성

- Animation Event 진입점은 `ReceiveMarker(Object)` 단일 방식
- marker는 window/lifecycle 신호 용도
  - `ComboStart`, `ComboEnd`, `HitStart`, `HitEnd`, `CompleteCurrentAction`, `CancelCurrentAction`

---

## 1) 테스트 씬 생성

1. `File > New Scene`
2. `File > Save As...`
3. 이름 예시: `ActionSystemTest`

---

## 2) 테스트 오브젝트 생성 및 컴포넌트 부착

1. Hierarchy 우클릭 > `Create Empty`
2. 이름: `Player_ActionTest`
3. Inspector에서 `Add Component` 클릭 후 아래 추가
   - `Animator`
   - `ActionController`
   - `ActionAnimationPresenter`
   - `ActionMarkerReceiver`
   - `ActionSystemInputTest` (선택: 개별 액션 단축키 테스트 전용)

> 필요 시 시각 확인용 `SpriteRenderer`를 추가해도 됩니다.

---

## 3) Animator Controller 생성 및 상태 구성

### 3-1. Controller 생성

1. Project 창 우클릭 > `Create > Animator Controller`
2. 이름 예시: `AC_ActionSystemTest`
3. `Player_ActionTest.Animator.Controller` 슬롯에 연결

### 3-2. 상태(State) 생성 (15개)

Animator 창에서 아래 상태를 모두 생성합니다.

- Idle
- Move
- Jump
- Land
- Dash
- Falling
- WallSlide
- WallJump
- Slide
- Attack
- Hit
- Die
- IdleBreakA
- IdleBreakB
- IdleBreakC

### 3-3. 클립 연결

각 상태에 클립을 연결합니다.

- 실 애니메이션이 없으면 테스트용 임시 클립(0.3~1.0초)을 생성해 연결
- `Idle`을 Default 상태로 지정

---

## 4) ActionRuleProfile 생성/설정/연결 (상세)

### 4-1. 프로필 생성

1. Project 창 우클릭
2. `Create > ActionSystem > Action Rule Profile`
3. 이름 예시: `ARP_Player_Test`

### 4-2. 기본 설정

- `ActorType = Player`
- `Rules` 배열에 15개 액션 추가

### 4-3. 15개 액션 Rule 권장값 예시

아래는 테스트에 쓰기 쉬운 기본값 예시입니다.

1. Idle
   - Enabled=true, Priority=0, IsInterruptible=true, AutoCompleteSeconds=0
2. Move
   - Enabled=true, Priority=1, IsInterruptible=true, AutoCompleteSeconds=0
3. Jump
   - Enabled=true, Priority=3, IsInterruptible=true, AutoCompleteSeconds=0.45
4. Land
   - Enabled=true, Priority=4, IsInterruptible=true, AutoCompleteSeconds=0.25
5. Dash
   - Enabled=true, Priority=5, IsInterruptible=true, AutoCompleteSeconds=0.3
6. Falling
   - Enabled=true, Priority=6, IsInterruptible=true, AutoCompleteSeconds=0
7. WallSlide
   - Enabled=true, Priority=7, IsInterruptible=true, AutoCompleteSeconds=0
8. WallJump
   - Enabled=true, Priority=8, IsInterruptible=true, AutoCompleteSeconds=0.45
9. Slide
   - Enabled=true, Priority=9, IsInterruptible=true, AutoCompleteSeconds=0
10. Attack
    - Enabled=true, Priority=10, IsInterruptible=true, AutoCompleteSeconds=0
11. Hit
    - Enabled=true, Priority=20, IsInterruptible=true, AutoCompleteSeconds=0
12. Die
    - Enabled=true, Priority=100, IsInterruptible=false, AutoCompleteSeconds=0
13. IdleBreakA
    - Enabled=true, Priority=2, IsInterruptible=true, AutoCompleteSeconds=0
14. IdleBreakB
    - Enabled=true, Priority=2, IsInterruptible=true, AutoCompleteSeconds=0
15. IdleBreakC
    - Enabled=true, Priority=2, IsInterruptible=true, AutoCompleteSeconds=0

### 4-4. 연결

- `Player_ActionTest > ActionController > ActionRuleProfile` 슬롯에 `ARP_Player_Test` 연결

---

## 5) AnimationStateMapProfile 생성/설정/연결 (상세)

### 5-1. 프로필 생성

1. Project 창 우클릭
2. `Create > ActionSystem > Animation State Map Profile`
3. 이름 예시: `ASMP_Player_Test`

### 5-2. StateMaps 구성

- 배열 크기: 15
- 각 원소 공통 필드
  - `ActionType`
  - `StateName` (Animator 상태명과 100% 동일)
  - `LayerIndex` (보통 0)
  - `IsOneShot`

### 5-3. IsOneShot 권장 예시

- false: Idle, Move, Falling, WallSlide
- true: Jump, Land, Dash, WallJump, Slide, Attack, Hit, Die, IdleBreakA/B/C

### 5-4. 연결

- `Player_ActionTest > ActionAnimationPresenter > StateMapProfile`에 `ASMP_Player_Test` 연결

---

## 6) ActionAnimationPresenter 세부 설정

`Player_ActionTest > ActionAnimationPresenter`

1. `Action Controller` 슬롯 연결
2. `Animator` 슬롯 연결
3. `StateMapProfile` 슬롯 연결
4. `Play Default On Action End = true`
5. `Default Presentation Action = Idle`

### 6-1. Idle Break 설정

- `Enable Idle Break = true`
- `Idle Break Base Action = Idle`
- `Idle Break Actions`에 `IdleBreakA`, `IdleBreakB`, `IdleBreakC` 추가
- `Idle Break Min Delay Seconds` / `Max Delay Seconds` 설정
  - 예: 4~8초

---

## 7) Object Marker 에셋 생성 및 연결

### 7-1. Marker Command 에셋 생성

Project 창 우클릭 > `Create > ActionSystem > Action Marker Command`

아래 에셋 생성 권장:
- `MC_ComboStart` (`CommandType = ComboStart`)
- `MC_ComboEnd` (`CommandType = ComboEnd`)
- `MC_HitStart` (`CommandType = HitStart`)
- `MC_HitEnd` (`CommandType = HitEnd`)
- `MC_Complete` (`CommandType = CompleteCurrentAction`)
- `MC_Cancel` (`CommandType = CancelCurrentAction`, CancelReason 지정)

### 7-2. ActionMarkerReceiver 연결

- `Player_ActionTest > ActionMarkerReceiver > ActionController` 슬롯 연결

---

## 8) Animation Event 설정 (Object 파라미터)

### 8-1. 필수 규칙

- 함수명: `ReceiveMarker`
- 파라미터 타입: `Object`
- 파라미터 값: `ActionMarkerCommandObject` 에셋

### 8-2. Attack 클립 추천 이벤트

1. 콤보 시작 프레임: `MC_ComboStart`
2. 콤보 종료 프레임: `MC_ComboEnd`
3. 히트 시작 프레임: `MC_HitStart`
4. 히트 종료 프레임: `MC_HitEnd`
5. 클립 종료 프레임: `MC_Complete`

### 8-3. Hit 클립 추천 이벤트

- 필요 시 종료 프레임에 `MC_Complete` 또는 특정 상황에 `MC_Cancel`

---

## 9) Input Actions 생성 및 ActionSystemInputTest 연결

### 9-1. Input Actions 에셋 생성

1. Project 창 우클릭 > `Create > Input Actions`
2. 이름 예시: `ActionSystemTest.inputactions`
3. Action Map: `Player`

### 9-2. Actions 추가

- Idle
- Move
- Jump
- Land
- Dash
- Falling
- WallSlide
- WallJump
- Slide
- Attack
- Hit
- Die
- Complete
- Cancel

### 9-3. 키 바인딩 예시

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

### 9-4. ActionSystemInputTest 연결

`Player_ActionTest > ActionSystemInputTest`

- `ActionController` 슬롯 연결
- 각 `InputActionReference` 슬롯에 `Player/<ActionName>` 매핑 연결

---

## 10) 15개 액션 테스트 순서

1. Idle: 시작 시 Idle 진입
2. Move: 이동 입력 전환/해제 복귀
3. Jump: 점프 진입
4. Land: 착지 후 Land 진입
5. Dash: 대시 입력 진입
6. Falling: 낙하 조건 진입
7. WallSlide: 벽 접촉 하강 진입
8. WallJump: WallSlide 중 점프 진입
9. Slide: 슬라이드 입력 진입
10. Attack: 공격 진입 + marker window 토글 확인
11. Hit: 피격 진입
12. Die: 사망 조건 진입 + 인터럽트 차단 확인
13. IdleBreakA: Idle 유지 중 재생 확인
14. IdleBreakB: Idle 유지 중 재생 확인
15. IdleBreakC: Idle 유지 중 재생 확인

---

## 11) 합격 기준

- 15개 액션이 모두 1회 이상 정상 진입
- Rule 정책대로 허용/거부 동작
- Animator 상태 매핑 오류 없음
- Attack marker 타이밍에서 Combo/Hit window ON/OFF 정확
- Console 치명 에러 없음

---

## 12) 자주 나는 문제와 해결

1. marker가 동작하지 않음
   - Event 함수명이 `ReceiveMarker`인지 확인
   - Event 파라미터 타입이 `Object`인지 확인
   - marker Object 에셋이 할당되었는지 확인
2. 애니메이션 전환 실패
   - `AnimationStateMapProfile.StateName` 오타 확인
3. 액션 요청 거부
   - Rule의 Enabled/Priority/IsInterruptible 확인
4. Idle Break가 안 나옴
   - Idle Break 옵션/후보 배열/딜레이 설정 확인

---

이 문서를 그대로 따라가면 현재 ActionSystem 구현과 1:1로 맞는 테스트 씬을 누락 없이 구성할 수 있습니다.