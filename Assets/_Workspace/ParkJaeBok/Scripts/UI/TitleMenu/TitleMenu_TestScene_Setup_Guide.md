# Title Menu 테스트 씬 구성 가이드

이 문서는 `TitleMenuPresenter / TitleMenuView / TitleMenuAction` 구조를 **테스트 가능한 형태로 빠르게 검증**할 수 있도록,
테스트 씬을 직접 구성하는 절차를 단계별로 설명합니다.

> 주의:
> - 이 가이드는 `.unity / .prefab / .meta` 파일을 직접 제공하지 않습니다.
> - 아래 순서를 따라 Unity Editor에서 수동으로 씬/오브젝트를 구성하세요.

---

## 1) 사전 준비

## 1-1. 필요한 스크립트 확인
다음 스크립트가 프로젝트에 존재해야 합니다.

- `Assets/_Workspace/ParkJaeBok/Scripts/UI/TitleMenu/TitleMenuPresenter.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/UI/TitleMenu/TitleMenuView.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/UI/TitleMenu/TitleSaveQueryService.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/UI/TitleMenu/TitleMenuDialogService.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/UI/TitleMenu/TitleMenuPanelCloseHandler.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/UI/TitleMenu/Actions/*.cs`

또한 기존 시스템 재사용을 위해 아래가 필요합니다.

- `SceneTransitionService`
- `SaveCoordinator`
- `StageCatalog` (Continue에서 stageId -> sceneName 해석 시)

## 1-2. Build Settings 확인
타이틀에서 이동할 씬 이름이 Build Settings에 등록되어 있어야 `TryLoadScene`이 정상 작동합니다.

- 예: `Town`, `Stage_01` 등
- `StartNewGameAction`에서 사용할 `New Game Scene Name`과 정확히 일치해야 합니다.

---

## 2) 테스트 씬 기본 골격 만들기

새 씬(예: `TitleMenu_Test`)을 하나 만들고 아래 오브젝트를 배치합니다.

### 2-1. 필수 시스템 오브젝트

1. `SceneSystems` 빈 오브젝트 생성
2. `SceneTransitionService` 컴포넌트 추가
3. `SaveCoordinator`가 기존 글로벌 씬에 없다면 같은 씬에 배치
   - Backend 및 Channel Policy 연결이 안 되어 있으면 Continue/Load 활성화 검증이 제한됩니다.

### 2-2. UI 루트 오브젝트

1. `Canvas` 생성 (Screen Space - Overlay 권장)
2. `EventSystem` 생성 (없으면 자동 생성)
3. `Canvas` 하위에 `TitleMenuRoot` 빈 오브젝트 생성

---

## 3) 타이틀 메뉴 UI 만들기 (상세)

아래는 키보드/패드 내비게이션까지 고려한 권장 계층입니다.
이 섹션에 나온 오브젝트를 **빠짐없이 생성**하면, 문서의 이후 단계(컴포넌트 연결/테스트)를 그대로 따라갈 수 있습니다.

```text
Canvas
└── TitleMenuRoot
    ├── Panel_Background (Image)
    ├── TitleLogo (Image or TMP_Text)
    ├── Panel_Menu (Vertical Layout Group)
    │   ├── Btn_NewGame (Button)
    │   │   └── Text (TMP_Text)
    │   ├── Btn_Continue (Button)
    │   │   └── Text (TMP_Text)
    │   ├── Btn_LoadGame (Button)
    │   │   └── Text (TMP_Text)
    │   ├── Btn_Option (Button)
    │   │   └── Text (TMP_Text)
    │   └── Btn_Quit (Button)
    │       └── Text (TMP_Text)
    ├── FooterInfo (GameObject)
    │   ├── Txt_Version (TMP_Text)
    │   └── Txt_Copyright (TMP_Text)
    ├── InputHint (GameObject)
    │   ├── Txt_SelectHint (TMP_Text)
    │   └── Txt_BackHint (TMP_Text)
    ├── ModalBackdrop (Image, 기본 비활성)
    ├── OptionsPanelRoot (GameObject, 기본 비활성)
    │   ├── Panel_Options (Image)
    │   │   ├── Txt_OptionsTitle (TMP_Text)
    │   │   └── Btn_OptionsClose (Button)
    │   │       └── Text (TMP_Text)
    ├── LoadGamePanelRoot (GameObject, 기본 비활성)
    │   ├── Panel_LoadGame (Image)
    │   │   ├── Txt_LoadTitle (TMP_Text)
    │   │   ├── Btn_Slot_01 (Button)
    │   │   │   └── Text (TMP_Text)
    │   │   ├── Btn_Slot_02 (Button)
    │   │   │   └── Text (TMP_Text)
    │   │   ├── Btn_Slot_03 (Button)
    │   │   │   └── Text (TMP_Text)
    │   │   └── Btn_LoadClose (Button)
    │   │       └── Text (TMP_Text)
    └── LoadingBlocker (Image, Raycast Target ON, 기본 비활성)
```

### 필수 오브젝트 체크리스트

아래 목록은 누락 여부를 체크하기 위한 최소 단위 목록입니다.

- `Canvas`
- `EventSystem`
- `TitleMenuRoot`
- `Panel_Background`
- `TitleLogo`
- `Panel_Menu`
- `Btn_NewGame`, `Btn_Continue`, `Btn_LoadGame`, `Btn_Option`, `Btn_Quit`
- 각 버튼 하위 텍스트(`Text` 또는 `TMP_Text`)
- `FooterInfo`
  - `Txt_Version`
  - `Txt_Copyright`
- `InputHint`
  - `Txt_SelectHint`
  - `Txt_BackHint`
- `ModalBackdrop`
- `OptionsPanelRoot`
  - `Panel_Options`
  - `Txt_OptionsTitle`
  - `Btn_OptionsClose` + 하위 텍스트
- `LoadGamePanelRoot`
  - `Panel_LoadGame`
  - `Txt_LoadTitle`
  - `Btn_Slot_01`, `Btn_Slot_02`, `Btn_Slot_03` + 각 하위 텍스트
  - `Btn_LoadClose` + 하위 텍스트
- `LoadingBlocker`

### 오브젝트별 설정 표 (1920x1080 기준)

체크리스트 오브젝트를 생성한 뒤, 아래 값을 그대로 입력하면 배치가 안정적으로 맞춰집니다.
아래 표는 **누락 없이 전부** 설정할 수 있도록, 각 오브젝트의 RectTransform 기준값(Anchor/Pivot/Position/Size)을 정리한 표입니다.
`Canvas Scaler = Scale With Screen Size (1920x1080)`를 전제로 합니다.

| 오브젝트 | Anchor | Pivot | X(%) (Anchor기준) | Y(%) (Anchor기준) | W(%) | H(%) | 비고 |
|---|---|---|---:|---:|---:|---:|---|
| `Panel_Background` | Full Stretch | (0.5, 0.5) | 0 | 0 | 100 | 100 | 배경 이미지(타이틀 아트/비디오) |
| `TitleLogo` | Top Center | (0.5, 0.5) | 0 | -14 | 40 | 14 | 로고 이미지 또는 TMP 텍스트 |
| `Panel_Menu` | Left Center | (0.0, 0.5) | 10 | -2 | 26 | 38 | Vertical Layout Group 권장 |
| `Btn_NewGame` | Panel_Menu 내부 Stretch | (0.5, 0.5) | - | - | 100*(패널기준) | 16*(패널기준) | 내비게이션 첫 포커스 권장 |
| `Btn_Continue` | Panel_Menu 내부 Stretch | (0.5, 0.5) | - | - | 100*(패널기준) | 16*(패널기준) | 세이브 없으면 비활성 |
| `Btn_LoadGame` | Panel_Menu 내부 Stretch | (0.5, 0.5) | - | - | 100*(패널기준) | 16*(패널기준) | 세이브 없으면 비활성 |
| `Btn_Option` | Panel_Menu 내부 Stretch | (0.5, 0.5) | - | - | 100*(패널기준) | 16*(패널기준) | 옵션 패널 오픈 |
| `Btn_Quit` | Panel_Menu 내부 Stretch | (0.5, 0.5) | - | - | 100*(패널기준) | 16*(패널기준) | 종료 확인/종료 |
| `Btn_* / Text(TMP)` | Center | (0.5, 0.5) | 0 | 0 | 88 | 70 | 버튼 라벨 공통 기준 |
| `FooterInfo` | Bottom Left | (0.0, 0.0) | 3 | 2.5 | 32 | 5 | 버전/저작권 표시 |
| `Txt_Version` | Left Middle(부모기준) | (0.0, 0.5) | 0 | 0 | 48 | 100 | 예: v0.1.0-dev |
| `Txt_Copyright` | Right Middle(부모기준) | (1.0, 0.5) | 0 | 0 | 48 | 100 | 예: © Team Project |
| `InputHint` | Bottom Right | (1.0, 0.0) | -3 | 2.5 | 26 | 5 | 패드/키보드 힌트 |
| `Txt_SelectHint` | Left Middle(부모기준) | (0.0, 0.5) | 0 | 0 | 48 | 100 | 예: A/Enter : Select |
| `Txt_BackHint` | Right Middle(부모기준) | (1.0, 0.5) | 0 | 0 | 48 | 100 | 예: B/Esc : Back |
| `ModalBackdrop` | Full Stretch | (0.5, 0.5) | 0 | 0 | 100 | 100 | Alpha 45~60%, 기본 비활성 |
| `OptionsPanelRoot` | Full Stretch | (0.5, 0.5) | 0 | 0 | 100 | 100 | 루트(기본 비활성) |
| `Panel_Options` | Center | (0.5, 0.5) | 0 | 0 | 62 | 70 | 옵션 모달 본문 |
| `Txt_OptionsTitle` | Top Center(부모기준) | (0.5, 0.5) | 0 | -9 | 80 | 10 | 예: Options |
| `Btn_OptionsClose` | Bottom Center(부모기준) | (0.5, 0.5) | 0 | 8 | 26 | 10 | 닫기 버튼 |
| `LoadGamePanelRoot` | Full Stretch | (0.5, 0.5) | 0 | 0 | 100 | 100 | 루트(기본 비활성) |
| `Panel_LoadGame` | Center | (0.5, 0.5) | 0 | 0 | 62 | 70 | 로드 모달 본문 |
| `Txt_LoadTitle` | Top Center(부모기준) | (0.5, 0.5) | 0 | -9 | 80 | 10 | 예: Load Game |
| `Btn_Slot_01` | Center(부모기준) | (0.5, 0.5) | 0 | 14 | 76 | 10 | 슬롯 버튼 1 |
| `Btn_Slot_02` | Center(부모기준) | (0.5, 0.5) | 0 | 0 | 76 | 10 | 슬롯 버튼 2 |
| `Btn_Slot_03` | Center(부모기준) | (0.5, 0.5) | 0 | -14 | 76 | 10 | 슬롯 버튼 3 |
| `Btn_LoadClose` | Bottom Center(부모기준) | (0.5, 0.5) | 0 | 8 | 26 | 10 | 닫기 버튼 |
| `LoadingBlocker` | Full Stretch | (0.5, 0.5) | 0 | 0 | 100 | 100 | Alpha 30~45%, Raycast On, 기본 비활성 |

> 참고:
> - `Panel_Background`는 반드시 Full Stretch(100% x 100%)로 두어 해상도 변화에서도 빈 공간이 생기지 않도록 설정합니다.
> - `Btn_*`의 실제 높이는 `Panel_Menu` 내 상대 비율(패널기준 16%)로 두면 5개 버튼 + 간격을 안정적으로 배치할 수 있습니다.
> - 위 표 수치는 **겹침 방지 배치값**으로 조정되어 있으므로 그대로 적용하는 것을 권장합니다.


## 3-1. 버튼 생성

`Panel_Menu`에 버튼 5개를 만들고 텍스트를 각각 설정합니다.

- New Game
- Continue
- Load Game
- Option
- Quit

### 내비게이션 설정 팁

각 버튼의 `Navigation`을 `Automatic` 또는 `Explicit`으로 구성해
키보드/패드의 상/하 이동이 자연스럽게 이어지게 설정합니다.

- `Btn_NewGame`의 Up을 마지막 버튼으로 돌리면 순환 내비게이션 가능
- `Btn_Quit`의 Down을 첫 버튼으로 연결하면 순환 구조 완성

## 3-2. 로딩 차단 오브젝트

`LoadingBlocker`를 전체 화면을 덮는 Image로 생성합니다.

- 색상: 반투명 검정(예: Alpha 80)
- `Raycast Target`: ON
- 기본 상태: **비활성**

`TitleMenuPresenter`가 액션 실행 중 `SetLoadingBlockerVisible(true)`를 호출하면
입력 차단 시각화/블로킹이 가능합니다.

## 3-3. 옵션/로드 패널 진입점 오브젝트

- `OptionsPanelRoot`, `LoadGamePanelRoot`를 빈 오브젝트 또는 패널로 만듭니다.
- 기본 비활성로 두고, 내부에 임시 텍스트(예: “Options Placeholder”)를 넣어 활성화 여부를 눈으로 확인합니다.
- `ModalBackdrop`는 기본 비활성로 두고, `OpenOptionsAction`/`OpenLoadGameAction`에서 패널 열기 시 함께 활성화되도록 연결합니다.

### 3-4. Close 버튼 동작 연결 (필수)

각 패널 Close 버튼은 반드시 `TitleMenuPanelCloseHandler.ClosePanel()`을 호출하도록 연결합니다.

#### TitleMenuPanelCloseHandler 컴포넌트 부착 위치

- **Options 패널용 핸들러**: `OptionsPanelRoot` 하위의 빈 오브젝트 `OptionsCloseHandler`에 부착  
  (권장 계층: `OptionsPanelRoot/OptionsCloseHandler`)
- **LoadGame 패널용 핸들러**: `LoadGamePanelRoot` 하위의 빈 오브젝트 `LoadCloseHandler`에 부착  
  (권장 계층: `LoadGamePanelRoot/LoadCloseHandler`)

> 왜 하위 빈 오브젝트를 권장하나?
> - 패널 루트 자체에 역할이 섞이지 않고,
> - Close 관련 참조를 핸들러 오브젝트에 분리해 유지보수가 쉬워지기 때문입니다.

#### TitleMenuPanelCloseHandler 인스펙터 설정값 설명

`TitleMenuPanelCloseHandler`의 각 필드는 아래 의미를 가집니다.

| 필드명 | 설정 대상 | 설명 |
|---|---|---|
| `Target Panel Root` | 현재 닫을 패널 루트 (`OptionsPanelRoot` 또는 `LoadGamePanelRoot`) | Close 버튼 클릭 시 비활성화되는 패널입니다. |
| `Modal Backdrop` | `ModalBackdrop` | 패널이 모두 닫히면 비활성화할 공통 백드롭입니다. |
| `Other Panel Root` | 반대편 패널 루트 | 다른 패널이 열려 있으면 백드롭을 유지하기 위해 사용합니다. |
| `Title Menu Presenter` | `TitleMenuPresenter` | 패널 닫힘 후 `NotifyModalClosed()` 호출로 기본 선택 포커스를 복구합니다. |

> 권장 연결 규칙:
> - Options용 핸들러의 `Target Panel Root`는 `OptionsPanelRoot`, `Other Panel Root`는 `LoadGamePanelRoot`
> - Load용 핸들러의 `Target Panel Root`는 `LoadGamePanelRoot`, `Other Panel Root`는 `OptionsPanelRoot`

1. `OptionsPanelRoot` 아래에 `OptionsCloseHandler` 오브젝트(또는 컴포넌트)를 만들고 `TitleMenuPanelCloseHandler`를 추가합니다.
2. `OptionsCloseHandler` 설정:
   - `Target Panel Root` -> `OptionsPanelRoot`
   - `Modal Backdrop` -> `ModalBackdrop`
   - `Other Panel Root` -> `LoadGamePanelRoot`
   - `Title Menu Presenter` -> `TitleMenuPresenter`
3. `Btn_OptionsClose`의 OnClick에 `OptionsCloseHandler.ClosePanel()`을 연결합니다.
4. `LoadGamePanelRoot`도 동일하게 `LoadCloseHandler`를 만들고 아래처럼 연결합니다.
   - `Target Panel Root` -> `LoadGamePanelRoot`
   - `Modal Backdrop` -> `ModalBackdrop`
   - `Other Panel Root` -> `OptionsPanelRoot`
   - `Title Menu Presenter` -> `TitleMenuPresenter`
5. `Btn_LoadClose`의 OnClick에 `LoadCloseHandler.ClosePanel()`을 연결합니다.

이렇게 설정하면:
- Option/Load 버튼으로 패널을 열고,
- 각 Close 버튼으로 해당 패널이 닫히며,
- 둘 다 닫힌 상태일 때만 `ModalBackdrop`이 자동으로 꺼지고,
- 메뉴 기본 선택 포커스가 `TitleMenuPresenter.NotifyModalClosed()`로 복귀합니다.

---

## 4) 컴포넌트 배치 및 연결

## 4-1. Presenter 오브젝트 구성

`TitleMenuRoot`(또는 별도 `TitleMenuPresenterRoot`)에 아래 컴포넌트를 붙입니다.

- `TitleMenuPresenter`
- `TitleMenuView`
- `TitleSaveQueryService`
- `TitleMenuDialogService`
- 액션 컴포넌트 5종
  - `StartNewGameAction`
  - `ContinueGameAction`
  - `OpenLoadGameAction`
  - `OpenOptionsAction`
  - `RequestQuitAction`
- 패널 Close 핸들러 2개(권장)
  - `TitleMenuPanelCloseHandler` (Options용)
  - `TitleMenuPanelCloseHandler` (LoadGame용)

## 4-2. TitleMenuView 필드 연결

`TitleMenuView` 인스펙터에서 다음을 연결합니다.

- `New Game Button` -> `Btn_NewGame`
- `Continue Button` -> `Btn_Continue`
- `Load Game Button` -> `Btn_LoadGame`
- `Option Button` -> `Btn_Option`
- `Quit Button` -> `Btn_Quit`
- `Default Selected` -> `Btn_NewGame` (권장)
- `Loading Blocker` -> `LoadingBlocker`

## 4-3. Action 필드 연결

- `OpenLoadGameAction.Load Game Entry Point` -> `LoadGamePanelRoot`
- `OpenLoadGameAction.Modal Backdrop` -> `ModalBackdrop`
- `OpenOptionsAction.Options Entry Point` -> `OptionsPanelRoot`
- `OpenOptionsAction.Modal Backdrop` -> `ModalBackdrop`

## 4-4. TitleMenuPresenter 필드 연결

`TitleMenuPresenter`에서 아래를 연결합니다.

- `View` -> 같은 오브젝트의 `TitleMenuView`
- `Scene Transition Service` -> 씬의 `SceneTransitionService`
- `Save Coordinator` -> 씬(또는 DDOL)의 `SaveCoordinator`
- `Save Query Component` -> 같은 오브젝트의 `TitleSaveQueryService`
- `Dialog Component` -> 같은 오브젝트의 `TitleMenuDialogService`
- `Stage Catalog` -> 프로젝트의 `StageCatalog` 에셋
- `New Game Scene Name` -> 예: `Town`
- `New Game Action Component` -> `StartNewGameAction`
- `Continue Action Component` -> `ContinueGameAction`
- `Load Game Action Component` -> `OpenLoadGameAction`
- `Option Action Component` -> `OpenOptionsAction`
- `Quit Action Component` -> `RequestQuitAction`

## 4-5. TitleSaveQueryService 필드 연결

- `Save Coordinator`를 명시 연결 권장
- 비워두면 `SaveCoordinator.Instance`를 사용하지만, 테스트 씬에서는 명시 연결이 디버깅에 유리합니다.

## 4-6. TitleMenuDialogService 기본 정책

초기 테스트 권장값:

- `Require New Game Overwrite Confirm` = true
- `Require Quit Confirm` = true
- `Auto Approve Confirm` = true

실제 팝업 시스템 연동 전까지는 Warning 로그 기반 자동 승인으로 흐름을 확인할 수 있습니다.

---

## 5) 테스트 시나리오 (체크리스트)

## 5-1. 기본 선택 및 내비게이션

1. 씬 실행 직후 `Btn_NewGame`이 선택되는지 확인
2. 키보드/패드 상하 이동이 자연스러운지 확인

## 5-2. Continue / Load 활성 상태

1. 세이브 데이터가 없는 상태에서 실행
   - Continue / Load 비활성 확인
2. 세이브 데이터를 만든 뒤 실행
   - Continue / Load 활성 확인

> 참고: `TitleSaveQueryService`는 `Persistent` 또는 `Session` 채널 스냅샷이 존재하면 true로 판단합니다.

## 5-3. New Game

1. 기존 진행이 없을 때 New Game 클릭
   - 지정한 `New Game Scene Name`으로 전환 시도 확인
2. 기존 진행이 있을 때 New Game 클릭
   - Dialog 정책에 따른 확인 절차(자동 승인/거부) 확인

## 5-4. Continue

1. Continue 클릭 시 Persistent/Session 로드 시도 확인
2. `StageSession` 기반 마지막 씬 해석 후 전환 시도 확인
3. 실패 시 Warning 로그가 남는지 확인

## 5-5. Load Game / Option

1. Load Game 클릭 시 `LoadGamePanelRoot` 활성화 확인
2. Option 클릭 시 `OptionsPanelRoot` 활성화 확인

## 5-6. Quit

1. Quit 클릭 시 Dialog 정책 확인
2. 에디터 환경에서는 `Application.Quit` 대신 Warning 로그 출력 확인

---

## 6) 로그 기반 진단 포인트

문제가 생기면 아래 로그 키워드를 우선 확인하세요.

- `[TitleMenuPresenter]`
  - Busy 상태 입력 무시
  - 전환 중 입력 무시
  - 액션/의존성 누락
- `[TitleMenuView]`
  - 버튼/EventSystem 누락
- `[TitleSaveQueryService]`
  - SaveCoordinator 누락
- `[StartNewGameAction]`, `[ContinueGameAction]`
  - 씬 해석/전환 실패
- `[SaveCoordinator]`
  - 정책/백엔드 누락

---

## 7) 확장 가이드

## 7-1. 실제 확인 팝업 연동

`TitleMenuDialogService`를 교체하거나 확장해 실제 모달 UI를 연결하세요.

- 인터페이스 유지: `ITitleDialogService`
- Presenter/Action 코드는 수정 최소화

## 7-2. 실제 Load 슬롯 UI 연동

`OpenLoadGameAction`의 진입점 오브젝트를 실제 슬롯 선택 Presenter가 있는 루트로 연결하세요.

## 7-3. 옵션 닫힘 후 메뉴 복귀

옵션/로드 패널 닫힘 시 `TitleMenuPresenter.NotifyModalClosed()`를 호출하면
기본 선택 복구 + Continue/Load 상태 재동기화가 가능합니다.

---

## 8) 빠른 점검용 최소 구성(요약)

1. Canvas + EventSystem 생성
2. 버튼 5개 + LoadingBlocker + OptionsPanelRoot + LoadGamePanelRoot 생성
3. `TitleMenuPresenterRoot`에 Presenter/View/Services/Actions 추가
4. 인스펙터 참조 전부 연결
5. `New Game Scene Name`과 Build Settings 씬 이름 일치 확인
6. Play 후 체크리스트 실행

이 구성만 완료해도 타이틀 메뉴의 핵심 요구사항(버튼 책임 분리, 상태 기반 활성화, 중복 입력 방지, 전환 요청 분리)을 빠르게 검증할 수 있습니다.
