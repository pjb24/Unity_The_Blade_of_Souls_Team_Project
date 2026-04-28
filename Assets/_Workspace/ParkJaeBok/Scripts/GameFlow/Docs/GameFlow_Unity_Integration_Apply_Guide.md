# 1. 변경 파일 요약
- 아래는 이번 변경 범위 중 **적용 문서 관점에서 필수 확인 파일**이다.
- 표기 규칙: `파일명 | 역할 | 적용 대상(Scene / Prefab / System)`

- `GameFlow/Core/GameFlowController.cs` | 상위 흐름 오케스트레이션, 씬 전환 요청, 상태 전이, 폴백/리커버리 진입 | **Bootstrap Scene / System**
- `GameFlow/Core/GameFlowStateMachine.cs`, `GameFlowStates.cs`, `BaseGameFlowState.cs`, `GameFlowContext.cs`, `GameFlowEvent.cs`, `GameFlowState.cs`, `IGameFlowState.cs` | 상태머신 코어(순수 로직) | **System(코드 전용)**
- `GameFlow/Services/FlowRetryService.cs`, `FlowFallbackService.cs`, `FlowExitGuard.cs`, `FlowSaveSyncAdapter.cs` | 재시도/폴백/종료중복방지/세이브 동기화 | **System(코드 전용)**
- `GameFlow/Diagnostics/GameFlowLogger.cs`, `GameFlowRuntimeDiagnostics.cs`, `GameFlowDebugSnapshot.cs`, `GameFlowWarningCatalog.cs` | 운영 로그/진단/Warning 키 표준화 | **System(코드 전용)**
- `GameFlow/Policies/ErrorRecoveryPolicy.cs` | 실패 정책 ScriptableObject | **Project Asset + Bootstrap 참조**
- `StageFlow/SceneTransitionService.cs` | 실제 씬 로드 단일 진입점 | **Bootstrap Scene / System**
- `GameFlow/Services/BootstrapTitleSceneLoader.cs` | Bootstrap 시작 후 Title 씬 자동 진입 트리거 | **Bootstrap Scene / Loader**
- `SaveSystem/Runtime/SaveCoordinator.cs`, `RecoveryService.cs` | Continue/복구 실패 보고 연동 | **Bootstrap Scene / System**
- `UI/TitleMenu/TitleMenuPresenter.cs`, `TitleMenuView.cs`, `TitleMenuActionContext.cs`, `Actions/*` | 타이틀 UI 입력을 GameFlow API로 라우팅 | **Title Scene / UI**
- `StageFlow/TownStageSelectorPresenter.cs`, `StageReturnToTownPresenter.cs`, `GameFlow/Multiplayer/Runtime/PlayerSpawnCoordinator.cs`, `PlayerSpawnPointRegistry.cs` | Town/Stage 구간에서 GameFlow 요청 라우팅 및 플레이어 스폰 위치 결정 | **Town/Stage Scene**
- `GameFlow/Test/GameFlowIntegrationTestRunner.cs` | 통합 시나리오 실행기 | **테스트 전용 Scene/Object**

- 레거시 제거(코드 경로 기준):
  - UI/Presenter에서 `SceneTransitionService`를 직접 호출하던 경로.
  - UI/Presenter에서 `Application.Quit` 직접 호출 경로.
  - Continue에서 UI가 직접 저장 채널 해석/씬 결정하던 경로.

# 2. 전체 구조 개요
- **Bootstrap Scene 역할**
  - 게임의 최초 진입점.
  - DDOL 기반 글로벌 시스템 생성/유지.
  - `GameFlowController`, `SceneTransitionService`, `SaveCoordinator`, `RecoveryService` 등 **시스템만 배치**.
  - Title Scene을 로드하는 시작 지점.
- **Title Scene 역할**
  - 타이틀 UI/버튼 상호작용 담당.
  - New Game / Continue / Load / Option / Quit 입력 제공.
  - 입력 처리 결과를 `GameFlowController.Request*`로 전달.
- **책임 분리 기준**
  - Bootstrap: 초기화/싱글톤/전환 인프라.
  - Title: 사용자 입력/View/버튼 상태.
  - Bootstrap에는 UI를 두지 않고, Title에는 시스템 초기화 코드를 두지 않는다.
  - DDOL 적용 대상(`GameFlowController`, `SceneTransitionService`, `SaveCoordinator`)은 **반드시 Hierarchy 루트 GameObject**에 위치해야 한다.
- **Scene 전환 흐름**
  - App Start -> **Bootstrap**(시스템 초기화) -> **Title**(UI 표시)
  - Title 버튼 -> `TitleMenuPresenter` -> 액션(`StartNewGameAction` 등) -> `GameFlowController.Request*`
  - `GameFlowController` -> `SceneTransitionService.TryLoadScene(...)` -> Town/Stage/Title 전환

# 3. Scene 구성

## 3.1 Bootstrap Scene
- 생성 여부: **필수 생성/사용** (Build Index 0 권장, 사실상 필수).
- Scene 목적:
  - 글로벌 시스템 1회 초기화.
  - DDOL 대상 시스템 상주시킴.
  - Title Scene 로드 시작점 제공.
- 포함되는 시스템(코드 기준):
  - `GameFlowController`
  - `SceneTransitionService`
  - `SaveCoordinator`
  - `RecoveryService`
  - (프로젝트에 이미 존재 시) `StageSession`, `StageProgressRuntime`, `AudioManager` 등 공용 런타임.
- 포함되면 안 되는 요소:
  - Title 전용 Canvas/UI/버튼.
  - 게임 플레이 전용 HUD/패널.

## 3.2 Title Scene
- 생성 여부: **필수 생성/사용**.
- Scene 목적:
  - 타이틀 메뉴 렌더링 및 입력 처리.
  - Game 시작/Continue/설정/종료 진입점 제공.
- 포함되는 UI:
  - `TitleMenuView`가 참조하는 버튼 5종(New/Continue/Load/Option/Quit).
  - `TitleMenuPresenter` + 액션 컴포넌트들.
- Bootstrap과의 의존 관계:
  - `TitleMenuPresenter`는 `GameFlowController`(직렬화 참조 또는 `GameFlowController.Instance`)에 의존.
  - `SceneTransitionService`, `SaveCoordinator`도 Bootstrap에서 먼저 생성되어야 안정적.

# 4. Hierarchy 구조
## Bootstrap Scene
- `BootstrapRoot`
  - `Systems` *(비-DDOL 보조 오브젝트 컨테이너)*
  - `Loaders`
    - `SceneLoader` (`BootstrapTitleSceneLoader` 부착)
- `GameFlowController` *(Root, DDOL 대상)*
- `SceneTransitionService` *(Root, DDOL 대상)*
- `SaveCoordinator` *(Root, DDOL 대상)*
- `RecoveryService` *(Root 권장)*
- `StageSession` *(프로젝트 기존 구성, 이름은 불명확)*
- `StageProgressRuntime` *(프로젝트 기존 구성, 이름은 불명확)*
- `Audio` *(코드상 AudioManager 이름/부모는 불명확)*

## Title Scene
- `TitleRoot`
  - `Canvas`
    - `MenuPanel`
      - `NewGameButton`
      - `ContinueButton`
      - `LoadButton`
      - `OptionButton`
      - `QuitButton`
    - `LoadingBlocker` *(선택: 입력 차단 오버레이)*
  - `EventSystem`
  - `TitleMenuRoot`
    - `TitleMenuView`
    - `TitleMenuPresenter`
    - `StartNewGameAction`
    - `ContinueGameAction`
    - `OpenLoadGameAction`
    - `OpenOptionsAction`
    - `RequestQuitAction`

※ `GameLoop`, `EventRouter`처럼 개념적 이름은 팀 컨벤션에 맞춰 사용 가능하나, 본 코드에서 해당 클래스명은 확인되지 않으므로 **불명확**.

# 5. GameObject 구성
- `BootstrapRoot`
  - Scene: Bootstrap
  - 부모: 없음
  - 역할: Bootstrap 전역 루트
  - DontDestroyOnLoad: Bootstrap 루트를 DDOL로 유지하려면 루트 자체에 DDOL 적용 컴포넌트를 둔다(권장)
- `Systems`
  - Scene: Bootstrap
  - 부모: `BootstrapRoot`
  - 역할: 글로벌 시스템 컨테이너
  - DontDestroyOnLoad: 직접 적용하지 않음(각 서비스 정책 따름)
- `GameFlowController`
  - Scene: Bootstrap
  - 부모: 없음(루트 권장)
  - 역할: 상위 흐름 상태 전이/요청 라우팅
  - DontDestroyOnLoad: **적용(true 권장, 루트 GameObject 필수)**
- `SceneTransitionService`
  - Scene: Bootstrap
  - 부모: 없음(루트 권장)
  - 역할: 씬 로드 단일 진입점
  - DontDestroyOnLoad: **적용(true 권장, 루트 GameObject 필수)**
- `SaveCoordinator`, `RecoveryService`
  - Scene: Bootstrap
  - 부모: 없음(루트 권장)
  - 역할: 세이브/복구 연동
  - DontDestroyOnLoad: 프로젝트 정책에 따름(`SaveCoordinator` DDOL 사용 시 루트 GameObject 필수)
- `SceneLoader`
  - Scene: Bootstrap
  - 부모: `Loaders`
  - 역할: 초기 1회 Title 로드 트리거
  - DontDestroyOnLoad: 불필요(초기 트리거 후 파괴 가능)
- `TitleRoot`
  - Scene: Title
  - 부모: 없음
  - 역할: 타이틀 UI 루트
  - DontDestroyOnLoad: 미적용
- `TitleMenuRoot`
  - Scene: Title
  - 부모: `TitleRoot`
  - 역할: Presenter/View/Action 결합 지점
  - DontDestroyOnLoad: 미적용

※ Unity 제약: `DontDestroyOnLoad`는 **루트 GameObject(또는 루트에 붙은 컴포넌트)**에만 유효하다. 따라서 DDOL 대상 컴포넌트를 하위 오브젝트(`Systems/*`)에 두면 경고/오류가 발생할 수 있다.

# 6. 컴포넌트 부착
- `GameFlowController` (Bootstrap / Root)
  - 의존 컴포넌트: `SceneTransitionService`, `SaveCoordinator`, `StageSession`, `StageProgressRuntime`, `StageCatalog`, `ErrorRecoveryPolicy`
  - 충돌 가능: 중복 `GameFlowController` 인스턴스(중복 감지 시 새 인스턴스 Destroy)
  - 실행 방식: `Awake`에서 의존성 해석 + 상태머신 빌드 + 이벤트 바인딩 + (옵션) 자동 Initialize
  - 초기화 시점: **Bootstrap 초기화 단계**
- `SceneTransitionService` (Bootstrap / Root)
  - 의존 컴포넌트: 선택적으로 `InputManager.PlayerInput`
  - 충돌 가능: 중복 인스턴스
  - 실행 방식: `TryLoadScene` 코루틴
  - 초기화 시점: **Bootstrap 초기화 단계**
- `RecoveryService` (Bootstrap / Root 권장)
  - 의존 컴포넌트: `_gameFlowController`
  - 충돌 가능: 컨트롤러 미연결 시 실패 보고 누락
  - 실행 방식: 복구 실패 이벤트 발생 시 `GameFlowController` 통지
  - 초기화 시점: Bootstrap 이후 런타임
- `TitleMenuView` (Title / TitleMenuRoot)
  - 의존 컴포넌트: 버튼 5종, default selectable, loading blocker(선택)
  - 충돌 가능: 버튼 참조 누락
  - 실행 방식: `Awake`에서 각 버튼 `onClick.AddListener`
  - 초기화 시점: **Title 진입 시**
- `TitleMenuPresenter` (Title / TitleMenuRoot)
  - 의존 컴포넌트: `TitleMenuView`, `GameFlowController`, 액션 컴포넌트들
  - 충돌 가능: 액션/의존 서비스 미구현 또는 null
  - 실행 방식: `OnEnable`에서 View 이벤트 바인딩, `OnDisable`에서 해제
  - 이벤트 원칙: View 이벤트에 대해 **Add(구독)/Remove(해제)**를 쌍으로 유지
  - 초기화 시점: **Title 진입 시**
- `BootstrapTitleSceneLoader` (Bootstrap / Loaders/SceneLoader)
  - 의존 컴포넌트: `SceneTransitionService`, `GameFlowController`, `SaveCoordinator`, `StageSession`, `StageProgressRuntime`
  - 충돌 가능: 필수 DDOL 준비 전 로드 시도 시 요청 거부 또는 대기 시간 초과 취소
  - 실행 방식: `Start`에서 DDOL 준비 대기 코루틴 후 `TryLoadScene("Title")`
  - 초기화 시점: Bootstrap 초기화 직후
- `StartNewGameAction`, `ContinueGameAction`, `RequestQuitAction` (Title / TitleMenuRoot)
  - 의존 컴포넌트: `TitleMenuActionContext.GameFlowController`
  - 충돌 가능: `GameFlowController` 없으면 강경고 후 실행 중단
  - 실행 방식: Presenter가 호출
  - 초기화 시점: Title 런타임 입력 시

# 7. Inspector 설정 (표)
| Scene | GameObject | 컴포넌트 | 필드명 | 설정값 | 참조 대상 | 누락 시 문제 |
|---|---|---|---|---|---|---|
| Bootstrap | GameFlowController(Root) | GameFlowController | `_stageCatalog` | StageCatalog 에셋 | StageCatalog | Continue/Stage 해석 실패 |
| Bootstrap | GameFlowController(Root) | GameFlowController | `_sceneTransitionService` | Systems의 서비스 참조 | SceneTransitionService | 씬 로드 시작 불가 |
| Bootstrap | GameFlowController(Root) | GameFlowController | `_saveCoordinator` | Systems의 서비스 참조 | SaveCoordinator | Continue/종료 저장 연동 실패 |
| Bootstrap | GameFlowController(Root) | GameFlowController | `_stageSession` | 기존 런타임 참조 | StageSession | 문맥 기록 실패 |
| Bootstrap | GameFlowController(Root) | GameFlowController | `_stageProgressRuntime` | 기존 런타임 참조 | StageProgressRuntime | 진행도 처리 불안정 |
| Bootstrap | GameFlowController(Root) | GameFlowController | `_errorRecoveryPolicy` | 정책 에셋 할당 | ErrorRecoveryPolicy | 재시도/폴백 튜닝 불가 |
| Bootstrap | GameFlowController(Root) | GameFlowController | `_titleSceneName` | `Title` | 문자열 | 타이틀 복귀 불일치 |
| Bootstrap | GameFlowController(Root) | GameFlowController | `_defaultNewGameSceneName` | `Town`(권장) | 문자열 | 새 게임 기본 진입 실패 |
| Bootstrap | GameFlowController(Root) | GameFlowController | `_dontDestroyOnLoad` | `true` | bool | 루트가 아니면 DDOL 적용 경고/실패 |
| Bootstrap | GameFlowController(Root) | GameFlowController | `_autoInitializeOnAwake` | `true` | bool | Boot 초기화 누락 가능 |
| Bootstrap | SceneTransitionService(Root) | SceneTransitionService | `_dontDestroyOnLoad` | `true` | bool | 루트가 아니면 DDOL 적용 경고/실패 |
| Bootstrap | Loaders/SceneLoader | BootstrapTitleSceneLoader | `_waitForDdolsReadyBeforeLoad` | `true` | bool | DDOL 준비 전에 Title 전환 시도 가능 |
| Bootstrap | Loaders/SceneLoader | BootstrapTitleSceneLoader | `_requireDdolsInDontDestroyScene` | `true` | bool | DDOL 이동 전 전환 발생 가능 |
| Bootstrap | Loaders/SceneLoader | BootstrapTitleSceneLoader | `_maxWaitSeconds` | `10`(권장) | float | 준비 실패 시 영구 대기/즉시 실패 |
| Bootstrap | Loaders/SceneLoader | BootstrapTitleSceneLoader | `_sceneTransitionService` | Root 참조 연결 | SceneTransitionService | 로드 시작 실패 |
| Bootstrap | RecoveryService(Root 또는 Systems) | RecoveryService | `_gameFlowController` | Root의 GameFlowController 참조 | GameFlowController | 복구 실패 보고 누락 |
| Title | TitleMenuRoot/TitleMenuView | TitleMenuView | `_newGameButton` 등 5개 버튼 | 각 버튼 연결 | Button | 버튼 입력 미동작 |
| Title | TitleMenuRoot/TitleMenuView | TitleMenuView | `_loadingBlocker` | 오버레이 오브젝트 | GameObject | 처리 중 입력 차단 미표시 |
| Title | TitleMenuRoot/TitleMenuPresenter | TitleMenuPresenter | `_view` | TitleMenuView 참조 | TitleMenuView | Presenter 작동 불가 |
| Title | TitleMenuRoot/TitleMenuPresenter | TitleMenuPresenter | `_gameFlowController` | Bootstrap 컨트롤러 참조(또는 Instance 사용) | GameFlowController | 흐름 라우팅 실패 |
| Title | TitleMenuRoot/TitleMenuPresenter | TitleMenuPresenter | `_newGameActionComponent` 등 액션 슬롯 | 액션 컴포넌트 할당 | ITitleMenuAction 구현체 | 버튼별 실행 누락 |

# 8. Prefab / ScriptableObject
- Prefab
  - 생성 대상(권장): `BootstrapRoot`, `TitleRoot/TitleMenuRoot`
  - 저장 경로(권장): `Assets/_Workspace/ParkJaeBok/Prefabs/System/`, `.../Prefabs/UI/Title/`
  - 초기값: Scene 기준 동일 값 유지
  - 참조 위치: 각 Scene 루트에서 Prefab 인스턴스 사용
  - 필수 여부: **불명확**(프로젝트 운영 정책에 따름)
- ScriptableObject
  - 생성 대상: `ErrorRecoveryPolicy`
  - 생성 메뉴: `Game/GameFlow/Error Recovery Policy`
  - 저장 경로(권장): `Assets/_Workspace/ParkJaeBok/Data/GameFlow/`
  - 초기값 권장:
    - `_maxSceneLoadRetryCount`: 1~2
    - `_sceneLoadRetryIntervalSeconds`: 0.25~1.0
    - `_sceneLoadFailureFallbackState`: Title
    - `_enableRecoveryCircuitBreaker`: true
    - `_recoveryCircuitOpenSeconds`: 10~30
  - 참조: `GameFlowController._errorRecoveryPolicy`

# 9. Build Settings
- Scene 목록(권장 최소):
  1. `Bootstrap` (**반드시 첫 Scene**)
  2. `Title`
  3. `Town`
  4. Stage Scene들
- 설정 이유:
  - Bootstrap에서 시스템 초기화를 먼저 완료해야 Title UI가 안정적으로 동작.
  - `GameFlowController`와 `SceneTransitionService`가 DDOL로 유지되어야 후속 씬 전환이 일관됨.
- 누락 시 문제:
  - Bootstrap 미등록/순서 오류: 초기화 전 UI 진입으로 null 참조/요청 실패 가능.
  - Title 미등록: Bootstrap->Title 전환 실패.
  - Town/Stage 미등록: `TryLoadScene` 실패 -> retry/fallback/circuit-breaker 경고 증가.

# 10. Unity Editor 적용 절차 (중요)
## Bootstrap Scene 적용
1. `File > New Scene`으로 신규 Scene 생성, 이름 `Bootstrap` 저장.
2. Hierarchy에서 `BootstrapRoot` 생성.
3. `BootstrapRoot` 하위에 `Systems`, `Loaders` 생성.
4. **루트 GameObject**를 각각 생성하고 컴포넌트를 1개씩 부착한다(DDOL 대상은 루트 필수):
   - `GameFlowController` (Root)
   - `SceneTransitionService` (Root)
   - `SaveCoordinator` (Root)
   - `RecoveryService` (Root 권장)
5. `GameFlowController` Inspector에서 필수 참조(`_sceneTransitionService`, `_saveCoordinator`, `_stageCatalog`, `_stageSession`, `_stageProgressRuntime`, `_errorRecoveryPolicy`) 연결.
6. `GameFlowController._dontDestroyOnLoad=true`, `_autoInitializeOnAwake=true` 설정.
7. `SceneTransitionService._dontDestroyOnLoad=true` 설정.
8. `RecoveryService._gameFlowController`에 Root의 `GameFlowController` 연결.
9. `Loaders/SceneLoader` 오브젝트 생성.
10. `Loaders/SceneLoader`에 `BootstrapTitleSceneLoader`를 부착한다.
11. `BootstrapTitleSceneLoader` Inspector에서 `_titleSceneName=Title`, `_loadOnStart=true`, `_loadOnlyOncePerPlaySession=true`를 설정한다.
12. DDOL 준비 가드 옵션을 설정한다: `_waitForDdolsReadyBeforeLoad=true`, `_requireSceneTransitionService=true`, `_requireGameFlowController=true`, `_requireSaveCoordinator=true`, `_requireStageSession=true`, `_requireStageProgressRuntime=true`, `_requireDdolsInDontDestroyScene=true`.
13. `_sceneTransitionService`에 Root의 `SceneTransitionService`를 연결한다.
14. Scene 저장.

## Title Scene 적용
1. `File > New Scene`으로 신규 Scene 생성, 이름 `Title` 저장.
2. `TitleRoot` 생성.
3. `Canvas` + `EventSystem` 생성.
4. `Canvas/MenuPanel`에 버튼 5종 생성:
   - `NewGameButton`, `ContinueButton`, `LoadButton`, `OptionButton`, `QuitButton`
5. `TitleMenuRoot` 생성 후 컴포넌트 부착:
   - `TitleMenuView`
   - `TitleMenuPresenter`
   - `StartNewGameAction`, `ContinueGameAction`, `OpenLoadGameAction`, `OpenOptionsAction`, `RequestQuitAction`
6. `TitleMenuView`에 5개 버튼 참조와 `_defaultSelected`, `_loadingBlocker` 연결.
7. `TitleMenuPresenter`에 `_view`, `_gameFlowController`, 액션 슬롯 5개 연결.
   - `_gameFlowController`는 Bootstrap의 DDOL 인스턴스를 참조하거나 `Instance` 해석을 사용.
8. 버튼 이벤트는 `TitleMenuView`가 `Awake`에서 `Button.onClick.AddListener`로 연결한다.
9. `TitleMenuPresenter`는 `OnEnable`에서 View 이벤트 구독, `OnDisable`에서 해제되므로 활성/비활성 전환이 정상인지 확인.
10. Scene 저장.

# 11. 실행 흐름
1. 게임 실행 -> Build Index 0의 `Bootstrap` 진입.
2. Bootstrap에서 글로벌 시스템 초기화:
   - `SceneTransitionService` 싱글톤 확보/DDOL
   - `GameFlowController` 싱글톤 확보/DDOL, 상태머신 빌드, Boot 초기화
3. Bootstrap 초기 트리거가 `Title` 로드 요청.
4. `Title` 로드 완료 후 메뉴 UI 표시.
5. 버튼 입력 흐름:
   - Button 클릭 -> `TitleMenuView` 이벤트 발행
   - `TitleMenuPresenter` 수신 -> `ITitleMenuAction.Execute`
   - 액션에서 `GameFlowController.Request*` 호출
   - `GameFlowController`가 `SceneTransitionService.TryLoadScene`로 전환 실행
6. 실패/예외 흐름:
   - 씬 로드 실패 -> `FlowRetryService` 재시도 -> 소진 시 `FlowFallbackService`
   - 정책에 따라 Title 폴백/서킷브레이커 오픈

# 12. 실행 검증
- 실행 직후 확인:
  - Bootstrap에서 `GameFlowController`, `SceneTransitionService`가 1개만 존재.
  - Title 로드가 자동으로 1회만 발생.
- Title UI 표시 여부:
  - Canvas/버튼 5종 표시.
  - default selection 정상 적용.
- 버튼 동작:
  - New/Continue/Load/Option/Quit 클릭 시 Presenter 로그/흐름 확인.
- Scene 전환:
  - Title -> Town / Town -> Stage / Stage -> Town.
- Warning 로그(폴백 포함):
  - `[GF.UI.GameFlowRequired]`, `[GF.Stage.GameFlowRequired]` 키 경고.
  - 씬 로드 실패 시 retry/fallback 및 circuit-breaker 관련 경고.
- 정상 기준:
  - direct SceneTransition direct Quit 경로 없이 GameFlow API만 사용.
  - `GetDebugSnapshot()` 지표가 누적됨.
- 실패 시 의심 포인트:
  1) Bootstrap 미진입 또는 Build 순서 오류
  2) `GameFlowController`/`SceneTransitionService` 중복 생성
  2-1) DDOL 대상 컴포넌트가 하위 오브젝트에 붙어 있어 `DontDestroyOnLoad only works for root GameObjects` 경고 발생
  3) Inspector 참조 누락
  4) `Title`/`Town`/Stage Build Settings 누락
  5) `BootstrapTitleSceneLoader` 필수 DDOL 준비 조건 미충족(Instance null/Disabled/NotInDontDestroyOnLoad)

# 13. 적용 체크리스트
- [ ] Bootstrap Scene 생성 및 Build Index 0 배치
- [ ] Title Scene 생성 및 Build Settings 등록
- [ ] Bootstrap에 글로벌 시스템(GameFlow/Transition/Save/Recovery) 배치
- [ ] DDOL 대상(`GameFlowController`, `SceneTransitionService`, `SaveCoordinator`)이 루트 GameObject에 부착됨
- [ ] Title에 UI + Presenter + Action 구성
- [ ] `GameFlowController` 필수 참조 연결
- [ ] `ErrorRecoveryPolicy` 생성/할당
- [ ] Bootstrap->Title 1회 로드 트리거 연결
- [ ] 중복 싱글톤 생성 방지 확인

# 14. 플레이 테스트 체크리스트
- [ ] 앱 시작 시 Bootstrap이 먼저 열린다.
- [ ] Bootstrap 이후 Title이 자동으로 열린다.
- [ ] Title 버튼 5종 클릭이 모두 입력된다.
- [ ] New Game이 Town(또는 지정 씬)으로 전환된다.
- [ ] Continue가 저장 데이터 기준으로 진입한다.
- [ ] Quit 요청이 GameFlow 경유로 처리된다.
- [ ] Stage 진입/복귀가 GameFlow 요청 기반으로 동작한다.
- [ ] 씬 로드 실패 유도 시 retry -> fallback 동작이 로그로 확인된다.
- [ ] retry 소진 시 circuit-breaker 오픈 경고가 확인된다.
- [ ] 비활성/재활성 시 Presenter 이벤트 구독 해제가 정상(`OnEnable`/`OnDisable`).

# 15. 위험 요소 / 충돌 요소
- Bootstrap 중복 생성 문제
  - Bootstrap Scene을 Additive/재진입하면 시스템 중복 가능.
- DontDestroyOnLoad 중복
  - 수동 배치 + 자동 생성이 동시에 일어나면 중복 인스턴스 경고.
- 초기화 순서 꼬임
  - Title이 Bootstrap보다 먼저 열리면 컨트롤러/서비스 null 가능.
- Scene 중복 로드
  - Bootstrap->Title 트리거가 2회 호출되면 예기치 않은 중복 전환.
- 이벤트 중복 등록
  - View/Presenter 수명주기 관리가 어긋나면 중복 호출 가능.
- Inspector 참조 누락
  - `_gameFlowController`, `_sceneTransitionService`, `_stageCatalog` 누락 시 핵심 기능 실패.
- 불명확 영역
  - Bootstrap 시스템 루트/오브젝트 네이밍(`Systems`, `Loaders`)은 팀 규칙에 따라 달라질 수 있음.

# 최소 적용 경로
- 1) `Bootstrap` + `Title` Scene을 생성하고 Build Settings에 `Bootstrap`을 0번으로 등록.
- 2) Bootstrap에 `GameFlowController` + `SceneTransitionService` + `SaveCoordinator` + `RecoveryService`만 우선 배치.
- 3) `GameFlowController` 필수 참조 최소 세트(`_sceneTransitionService`, `_saveCoordinator`, `_errorRecoveryPolicy`, `_titleSceneName`) 연결.
- 4) Title에 `TitleMenuView` + `TitleMenuPresenter` + 3개 핵심 액션(New/Continue/Quit)만 먼저 연결.
- 5) Bootstrap 시작 후 Title 1회 로드가 되는지 확인하고, New/Continue/Quit 동작만 우선 검증.

# 권장 최종 적용 경로
- 1) 최소 적용 경로 완료 후 Town/Stage Presenter(`TownStageSelectorPresenter`, `StageReturnToTownPresenter`)까지 전부 GameFlow 경유로 통일.
- 2) `BootstrapRoot`, `TitleRoot`를 Prefab 규격으로 표준화해 씬별 수작업 편차 제거.
- 3) `ErrorRecoveryPolicy` 수치를 QA 시나리오 기반으로 튜닝(retry/fallback/circuit-breaker).
- 4) `GameFlowIntegrationTestRunner` 시나리오를 릴리즈 전 회귀 루틴에 포함.
- 5) 운영 단계에서 `GetDebugSnapshot()` + Warning 키 모니터링으로 장애 조기 감지 체계화.
