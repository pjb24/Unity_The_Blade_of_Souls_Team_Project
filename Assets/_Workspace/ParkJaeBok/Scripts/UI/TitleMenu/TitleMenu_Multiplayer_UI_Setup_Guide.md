# Title Menu 싱글/멀티 통합 UI 구성 가이드

이 문서는 `TitleMenu_TestScene_Setup_Guide.md`를 기반으로,
**타이틀 진입 시 Host/Client 분리 진입 버튼 구성부터 싱글/멀티 슬롯 선택, Join 팝업**까지
요구사항을 만족하는 UI 구성 절차를 설명합니다.

> 전제
> - 기존 타이틀 기본 구성(버튼/패널/백드롭/로딩 블로커)은 `TitleMenu_TestScene_Setup_Guide.md`를 먼저 완료한 상태여야 합니다.
> - 본 문서도 `.unity / .prefab / .meta` 파일을 직접 제공하지 않습니다.

---

## 1) 사전 스크립트 체크

아래 스크립트가 존재해야 합니다.

- `Assets/_Workspace/ParkJaeBok/Scripts/UI/TitleMenu/TitlePlayModePresenter.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/GameFlow/Multiplayer/Runtime/MultiplayerSessionOrchestrator.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/GameFlow/Multiplayer/Services/LocalFileMultiplayerSessionBackend.cs` (동일 PC 다중 인스턴스 검증용)
- `Assets/_Workspace/ParkJaeBok/Scripts/GameFlow/Core/GameFlowController.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/StageFlow/TownMultiplayerJoinCodePresenter.cs`

---

## 2) UI 계층 확장

기존 `Canvas/TitleMenuRoot`와 Town HUD Canvas에 아래 오브젝트를 구성합니다.

```text
[Title Scene]
TitleMenuRoot
├── BackgroundRoot
│   └── Img_Background (Image)
├── CommonRoot
│   ├── LogoRoot
│   │   └── Img_Logo (Image)
│   └── BlockerRoot
│       └── Img_InputBlocker (Image, 기본 비활성)
├── Panel_TopMenu (초기 화면)
│   ├── Btn_SinglePlay (Button)
│   ├── Btn_Multiplay (Button)
│   ├── Btn_Option (Button)
│   └── Btn_Quit (Button)
├── Panel_SingleMenu (Single 선택 후, 기본 비활성)
│   ├── Btn_Continue (Button)
│   ├── Btn_NewGame (Button)
│   ├── Btn_LoadGame (Button)
│   └── Btn_Back_FromSingle (Button, 권장)
├── Panel_SlotSelect (New/Load/Multiplay 선택 후 공용, 기본 비활성)
│   ├── Slot_01 (Button + Used/Empty 표시)
│   ├── Slot_02 (Button + Used/Empty 표시)
│   ├── Slot_03 (Button + Used/Empty 표시)
│   └── Btn_Back_FromSlot (Button, 권장)
├── Panel_MultiplayMode (멀티 슬롯 선택 후, 기본 비활성)
│   ├── Btn_Host (Button)
│   ├── Btn_Client (Button)
│   └── Btn_Back_FromMultiplayMode (Button, 권장)
├── MultiplayerPanelRoot (GameObject, 기본 비활성)
│   ├── Panel_Multiplayer (Image)
│   │   ├── Txt_MultiTitle (TMP_Text)
│   │   ├── Input_JoinCode (TMP_InputField)
│   │   ├── Btn_JoinSubmit (Button)
│   │   ├── Txt_StatusMessage (TMP_Text)
│   │   └── Btn_MultiClose (Button)
└── ModalBackdrop (Image, 기본 비활성)

[Town Scene]
TownHudCanvas
└── Panel_MultiplayerHostInfo (Host 전용 Root)
    ├── Txt_JoinCodeLabel (TMP_Text)
    └── Txt_JoinCodeValue (TMP_Text)
```

### 권장 UI 규칙
#### 기본 상태

- 타이틀 진입 직후에는 `Panel_TopMenu`만 활성화

#### Single 흐름

- `Btn_SinglePlay` 클릭 시  
    → `Panel_SingleMenu` 활성화
- `Btn_NewGame` / `Btn_LoadGame` 클릭 시  
    → `Panel_SlotSelect` 활성화  
    → 내부 상태를 `SingleNewGame` / `SingleLoadGame`으로 설정
- `Btn_Continue` 클릭 시  
    → 마지막 슬롯 기준으로 즉시 게임 시작

#### Multiplay 흐름

##### 1. 진입

- `Btn_Multiplay` 클릭 시  
    → `Panel_SlotSelect` 활성화  
    → 내부 상태를 `Multiplay`로 설정

##### 2. 슬롯 선택 이후

- 슬롯 선택 시 (`Slot_01 ~ 03`)  
    → 선택 슬롯 저장 (`SelectPlaySlot`)  
    → `Panel_MultiplayMode` 활성화

##### 3. Host / Client 선택

- `Btn_Host` 클릭 시  
    → Host 세션 생성  
    → 성공 시 즉시 게임(Town) 진입
- `Btn_Client` 클릭 시  
    → `MultiplayerPanelRoot` 활성화 (Join 팝업 오픈)

#### Join 팝업

- `Title Scene`의 Join 팝업(`MultiplayerPanelRoot`)은  
    → **Client 참가 입력 전용 UI**
- 입력:
    - `Input_JoinCode`
- `Btn_JoinSubmit`  
    → 세션 참가 시도
- `Btn_MultiClose`  
    → 팝업 닫기
- `Txt_StatusMessage`  
    → 아래 상황 표시
    - Join 실패
    - 정원 초과
    - 진행 중 Join 불가

#### Option / Quit

- `Btn_Option`, `Btn_Quit`  
    → 기존 TitleMenu 흐름 그대로 사용 (별도 변경 없음)

#### Town Scene 연동

- Host인 경우  
    → `Panel_MultiplayerHostInfo/Txt_JoinCodeValue`에서  
    → 현재 Join Code 표시

---

## 3) 컴포넌트 배치

## 3-1. 권장 오브젝트 분리 구조
- `TitleMenuRoot`(Canvas 하위 UI 루트)
  - `TitlePlayModePresenter` 부착
- `MultiplayerRuntimeRoot`(Canvas 바깥 별도 런타임 루트, 빈 GameObject 권장)
  - `MultiplayerSessionOrchestrator` 부착
  - `LocalFileMultiplayerSessionBackend` 부착 (로컬 테스트)

> 왜 분리하나요?
> - `MultiplayerSessionOrchestrator`는 UI 렌더링 컴포넌트가 아니라 **세션 런타임 상태/정책**을 관리합니다.
> - `DontDestroyOnLoad`를 사용할 때 Canvas 생명주기/중복 생성과 분리되어 관리가 쉬워집니다.
> - UI 교체/리빌드 시 세션 상태 오브젝트를 안정적으로 유지할 수 있습니다.

## 3-2. 백엔드 교체 지점
- 동일 PC에서 Host/Client를 **서로 다른 실행 인스턴스**로 검증: `LocalFileMultiplayerSessionBackend` 사용
- 운영 전환: 같은 위치의 `_sessionBackendBehaviour`를 UGS Relay 어댑터로 교체

## 3-3. 의존성 연결
- `TitlePlayModePresenter._gameFlowController` -> `GameFlowController`
- `TitlePlayModePresenter._saveCoordinator` -> `SaveCoordinator`
- `TitlePlayModePresenter._multiplayerSessionOrchestrator` -> `MultiplayerRuntimeRoot/MultiplayerSessionOrchestrator`
- `MultiplayerSessionOrchestrator._gameFlowController` -> `GameFlowController`
- `MultiplayerSessionOrchestrator._sessionBackendBehaviour` -> `MultiplayerRuntimeRoot/LocalFileMultiplayerSessionBackend`
- (Town UI) `TownMultiplayerJoinCodePresenter._multiplayerSessionOrchestrator` -> `MultiplayerSessionOrchestrator`
- (Town UI) `TownMultiplayerJoinCodePresenter._joinCodeText` -> `TownHudCanvas/Panel_MultiplayerHostInfo/Txt_JoinCodeValue`
- (Town UI) `TownMultiplayerJoinCodePresenter._hostOnlyRoot` -> `TownHudCanvas/Panel_MultiplayerHostInfo`

## 3-4. Inspector 할당 상세: TitlePlayModePresenter

`TitleMenuRoot`에 붙은 `TitlePlayModePresenter`에서 아래처럼 연결하세요.

| 필드명 | 할당할 오브젝트 | 비고 |
|---|---|---|
| `_gameFlowController` | `Bootstrap`(또는 런타임 매니저 루트)의 `GameFlowController` | 비우면 자동 탐색 사용 가능 |
| `_saveCoordinator` | `Bootstrap`의 `SaveCoordinator` | 슬롯 처리/Continue 판정에 사용 |
| `_multiplayerSessionOrchestrator` | `MultiplayerRuntimeRoot/MultiplayerSessionOrchestrator` | Host/Client 요청 전달 |
| `_panelTopMenu` | `TitleMenuRoot/Panel_TopMenu` | 초기 활성 패널 |
| `_panelSingleMenu` | `TitleMenuRoot/Panel_SingleMenu` | Single 하위 메뉴 |
| `_panelSlotSelect` | `TitleMenuRoot/Panel_SlotSelect` | 슬롯 선택 공용 패널 |
| `_panelMultiplayMode` | `TitleMenuRoot/Panel_MultiplayMode` | Host/Client 선택 패널 |
| `_multiplayerPanelRoot` | `TitleMenuRoot/MultiplayerPanelRoot` | Join 팝업 루트 |
| `_legacyLoadGamePanelRoot` | (선택) 기존 Load 패널 루트 (`LoadGamePanelRoot`) | 레거시 패널 유지 시만 연결 |
| `_legacyOptionsPanelRoot` | (선택) 기존 Options 패널 루트 (`OptionsPanelRoot`) | 레거시 패널 유지 시만 연결 |
| `_modalBackdrop` | `TitleMenuRoot/ModalBackdrop` | 레거시 패널/모달 배경 |
| `_singleContinueButton` | `Panel_SingleMenu/Btn_Continue` | 저장 데이터 없을 때 자동 비활성 |
| `_singleLoadGameButton` | `Panel_SingleMenu/Btn_LoadGame` | 저장 데이터 없을 때 자동 비활성 |
| `_hostClientId` | 문자열 값 (예: `Host_A`) | Host 생성 시 식별자 |
| `_autoOpenJoinPopupOnClientSelected` | `true` 권장 | Client 버튼 클릭 시 Join 팝업 자동 오픈 |
| `_onSingleStartSucceeded` | (선택) 로딩 연출/사운드 이벤트 | 성공 후처리 |
| `_onMultiplayerHostStartSucceeded` | (선택) 로딩 연출/사운드 이벤트 | 성공 후처리 |
| `_onMultiplayerClientStartSucceeded` | (선택) 로딩 연출/사운드 이벤트 | 성공 후처리 |
| `_onStartFailed` | (선택) 실패 메시지 UI 갱신 이벤트 | 실패 후처리 |

> 권장: `_panelTopMenu`만 초기 활성, 나머지 패널은 기본 비활성로 시작하세요.

## 3-5. Inspector 할당 상세: MultiplayerSessionOrchestrator

`MultiplayerRuntimeRoot`에 붙은 `MultiplayerSessionOrchestrator`에서 아래처럼 연결하세요.

| 필드명 | 할당할 오브젝트 | 비고 |
|---|---|---|
| `_gameFlowController` | `Bootstrap`(또는 런타임 매니저 루트)의 `GameFlowController` | 비우면 자동 탐색 |
| `_sessionBackendBehaviour` | 같은 오브젝트의 `LocalFileMultiplayerSessionBackend` | 로컬 테스트 기본값 |
| `_maxPlayerCount` | `2` | 요구사항 기준 2인 세션 |
| `_reconnectWindowSeconds` | `45`(또는 정책값) | 30~60초 권장 |
| `_dontDestroyOnLoad` | `true` 권장 | Town에서도 JoinCode 유지 |
| `_autoResolveDependenciesOnUse` | `true` 권장 | 씬 전환 후 참조 복구 |
| `_uiJoinCodeInputField` | `TitleMenuRoot/MultiplayerPanelRoot/Panel_Multiplayer/Input_JoinCode` | Join 제출 직전 텍스트 재조회 |
| `_uiClientId` | 문자열 값 (예: `Client_A`) | ClientId 입력 UI 없을 때 사용 |
| `_onJoinCodeGenerated` | (선택) JoinCode 표시용 UI 이벤트 | Host 생성 성공 시 호출 |
| `_onSessionFailed` | (선택) `Txt_StatusMessage` 갱신 이벤트 | Join/정책 실패 표시 |
| `_onReturnToTitle` | (선택) 타이틀 복귀 연출 이벤트 | 재접속 실패/세션 종료 시 |
| `_onPlayerCountUpdated` | (선택) 인원 수 텍스트 갱신 이벤트 | 세션 인원 수 갱신 시 호출 |

### Join 팝업 관련 Inspector 연결 체크
- `Btn_JoinSubmit` -> `MultiplayerSessionOrchestrator.OnClickJoinSessionFromTitle()`
- `Input_JoinCode`는 OnValueChanged 이벤트를 쓰지 않고, `_uiJoinCodeInputField` 필드에 직접 할당합니다.

### Host/Client 분리 실행(다중 인스턴스) 설정
- Host/Client 모두 같은 `_storageFileName`(기본값 `multiplayer_sessions_local.json`)을 사용해야 같은 세션 저장소를 조회합니다.
- 이 방식은 동일 PC 테스트 전용입니다. 실제 네트워크 멀티플레이는 UGS Relay 백엔드로 전환해야 합니다.

---

## 4) 버튼 OnClick 연결

아래 표의 클래스/메서드 이름을 **그대로** Button OnClick에 연결하세요.

| 버튼 | 대상 클래스 | 연결 메서드 |
|---|---|---|
| `Btn_SinglePlay` | `TitlePlayModePresenter` | `OnClickTopSinglePlay()` |
| `Btn_Multiplay` | `TitlePlayModePresenter` | `OnClickTopMultiplay()` |
| `Btn_Option` | `TitlePlayModePresenter` | `OnClickTopOption()` |
| `Btn_Quit` | `TitlePlayModePresenter` | `OnClickTopQuit()` |
| `Btn_Continue` | `TitlePlayModePresenter` | `OnClickSingleContinue()` |
| `Btn_NewGame` | `TitlePlayModePresenter` | `OpenSlotSelectForSingleNewGame()` |
| `Btn_LoadGame` | `TitlePlayModePresenter` | `OpenSlotSelectForSingleLoadGame()` |
| `Btn_Back_FromSingle` | `TitlePlayModePresenter` | `OnClickBackFromSingle()` |
| `Slot_01` | `TitlePlayModePresenter` | `OnClickSlot01()` |
| `Slot_02` | `TitlePlayModePresenter` | `OnClickSlot02()` |
| `Slot_03` | `TitlePlayModePresenter` | `OnClickSlot03()` |
| `Btn_Back_FromSlot` | `TitlePlayModePresenter` | `OnClickBackFromSlot()` |
| `Btn_Host` | `TitlePlayModePresenter` | `OnClickMultiplayModeHost()` |
| `Btn_Client` | `TitlePlayModePresenter` | `OnClickMultiplayModeClient()` |
| `Btn_Back_FromMultiplayMode` | `TitlePlayModePresenter` | `OnClickBackFromMultiplayMode()` |
| `Btn_JoinSubmit` | `MultiplayerSessionOrchestrator` | `OnClickJoinSessionFromTitle()` |
| `Btn_MultiClose` | `TitlePlayModePresenter` | `OnClickJoinPopupClose()` |

## 4-1. Top 메뉴 버튼(타이틀 진입 화면)
- `Btn_SinglePlay` -> 싱글 메뉴 열기 메서드(예: `OpenSingleMenu()`)
- `Btn_Multiplay` -> 슬롯 선택 패널 열기 메서드
- `Btn_Option` -> 기존 옵션 패널 오픈 액션
- `Btn_Quit` -> 기존 종료 액션

## 4-2. Single 메뉴 버튼
- `Btn_Continue` -> 마지막 플레이 데이터를 기준으로 바로 게임 시작, `TitlePlayModePresenter.OnClickSingleContinue()`
- `Btn_NewGame` -> 슬롯 선택 패널 열기(NewGame 의도)
- `Btn_LoadGame` -> 슬롯 선택 패널 열기(LoadGame 의도)
- `Btn_Back_FromSingle` -> 타이틀 메인으로 복귀 `OpenTopMenu()`

### Continue / LoadGame 활성 규칙
- 저장 데이터 존재 시: `Continue`, `LoadGame` 활성
- 저장 데이터 없음: `Continue`, `LoadGame` 비활성
- 저장 데이터 없음 상태의 표시 순서는 아래를 권장
  1. New Game
  2. Continue (비활성)
  3. Load Game (비활성)

## 4-3. Slot 선택 처리 규칙
- 공통
	- Panel_SlotSelect에서 슬롯 3개를 고정 배치
	- 슬롯마다 Used/Empty 상태를 표시
	- 슬롯 클릭 시 SelectPlaySlot(slotIndex)를 먼저 호출해 선택 슬롯을 확정한다.
- SingleNewGame 의도
	- 슬롯 선택 후 기존 데이터를 제거(SaveCoordinator.ClearSlotData)
	- OnClickSingleNewGame()을 통해 새 게임을 시작한다.
- SingleLoadGame 의도
	- 슬롯 데이터가 있을 경우에만 OnClickSingleLoadGame()을 호출
	- 해당 데이터를 로드하고 게임을 시작한다.
	- 데이터가 없으면 시작하지 않는다.
	- 데이터가 없으면 UI를 비활성한다.
- Continue 동작
	- 마지막 사용 슬롯에 데이터가 있을때만 시작한다.
	- 데이터가 없거나 비어 있으면 시작하지 않는다.
	- 데이터가 없거나 비어 있으면 UI를 비활성한다.
- Multiplay 의도
	- 슬롯 선택 후 즉시 시작하지 않고 Panel_MultiplayMode로 이동한다.
	- Btn_Host는 세션 생성 후 즉시 Town으로 이동한다.
	- Btn_Client는 Join 팝업(MultiplayerPanelRoot)을 오픈한다.

## 4-4. Join 팝업 버튼
- `Input_JoinCode` -> `MultiplayerSessionOrchestrator._uiJoinCodeInputField`에 직접 연결 (OnClick 시점에 값 재조회)
- `Btn_JoinSubmit` -> `MultiplayerSessionOrchestrator.OnClickJoinSessionFromTitle()`
- `Btn_MultiClose` -> `TitlePlayModePresenter.OnClickJoinPopupClose()` (팝업 닫기 + 멀티 모드 패널 복귀)

> 참고:
> - `OnClickJoinSessionFromTitle()`는 호출 직전에 `_uiJoinCodeInputField.text`를 다시 읽고, 그 값을 사용해 `JoinSessionFromTitle(_uiJoinCode, _uiClientId)`를 호출합니다.
> - `Input_ClientId` UI를 만들지 않는 경우, 오케스트레이터 인스펙터의 `_uiClientId` 기본값을 사용하면 됩니다.

## 4-5. Town Join Code 표시
- Town 씬의 Host UI 루트에 `TownMultiplayerJoinCodePresenter`를 부착합니다.
- `TownMultiplayerJoinCodePresenter.RefreshJoinCodeUi()`를 `OnEnable` 또는 Town HUD 오픈 시점에 호출합니다.
- Host는 Client가 아직 연결되지 않은 경우에만 Session Join Code를 표시하고, Client가 연결된 이후에는 Join Code를 숨기거나 표시하지 않도록 구성합니다.
- Client는 해당 패널을 숨기거나 `-`를 표시하도록 구성합니다.

---

## 5) 이벤트 바인딩

`MultiplayerSessionOrchestrator` 이벤트를 UI 텍스트와 연결합니다.

## 5-1. Join Code 노출
- `_onJoinCodeGenerated(string)` -> Title/Town 안내 메시지 갱신(선택)
- Town Join Code 값 표시는 `TownMultiplayerJoinCodePresenter`에서 `ActiveJoinCode`를 직접 조회해 동기화

## 5-2. 실패 사유 표시
- Join에 실패하면 `_onSessionFailed(string)` 이벤트 수신 -> `Txt_StatusMessage.text` 갱신

## 5-3. 타이틀 복귀 연출
- `_onReturnToTitle` ->
  - 모달/패널 닫기
  - 기본 메뉴 포커스 복귀

## 5-4. Host/Client Join 성공 확인 방법
- 공통
  - `MultiplayerSessionOrchestrator.CurrentPlayerCount` 값으로 현재 세션 인원 수를 확인합니다.
  - `_onPlayerCountUpdated(int)` 이벤트를 UI 텍스트에 연결해 실시간으로 표시할 수 있습니다.
- Host 측
  - `HasRemoteClientJoined`가 `true`면 Client가 세션 참가에 성공한 상태입니다.
  - 서로 다른 인스턴스(Host/Client 분리 실행)에서는 Host가 `RefreshPlayerCountFromActiveSession()`을 주기적으로 호출해 백엔드 인원 수를 동기화해야 합니다.
- Client 측
  - `JoinSessionFromTitle(...)` 반환값 `true` 또는 `OnClickJoinSessionFromTitle()` 호출 후 `_connectionState == JoinedAsClient`이면 Join 성공입니다.

### Inspector에서 “확실하게” 확인하는 방법

아래는 **Host 실행 인스턴스**와 **Client 실행 인스턴스** 각각에서 확인해야 하는 오브젝트/필드입니다.

#### A. Host 인스턴스
1. `MultiplayerRuntimeRoot` 오브젝트 선택
2. `MultiplayerSessionOrchestrator` 컴포넌트에서 다음 필드 확인
   - `_connectionState` = `Hosting`
   - `_activeJoinCode` = 비어있지 않음(Host가 발급한 코드)
   - `_currentPlayerCount`
     - Host만 있을 때: `1`
     - Client Join 성공 후: `2`
   - `HasRemoteClientJoined` = `true` (Client Join 성공 시)
3. 분리 인스턴스 테스트라면 `RefreshPlayerCountFromActiveSession()`을 호출(버튼/디버그 훅)한 뒤 `_currentPlayerCount`가 2로 갱신되는지 확인

#### B. Client 인스턴스
1. `MultiplayerRuntimeRoot` 오브젝트 선택
2. `MultiplayerSessionOrchestrator` 컴포넌트에서 다음 필드 확인
   - `_connectionState` = `JoinedAsClient`
   - `_activeJoinCode` = Host JoinCode와 동일
   - `_localClientId` = 비어있지 않음(현재 Client 식별자)
   - `_isLocalClientReadyDeclared`
     - Town(또는 Client 진입 씬) 로드 후 `true`로 전환되는지 확인

#### C. Host UI에서 추가 확인(권장)
1. `TownHudCanvas/Panel_MultiplayerHostInfo` 선택
2. `TownMultiplayerJoinCodePresenter`가 연결된 오브젝트에서
   - Host 단독 상태: JoinCode가 표시됨
   - Client Join 후/준비 완료 후: 정책에 맞춰 JoinCode 표시가 숨김 또는 갱신되는지 확인

#### D. 실패 케이스 확인
- `MultiplayerSessionOrchestrator._lastFailureReason` 필드에서 실패 원인을 직접 확인합니다.
- 실패 시 `_onSessionFailed(string)`에 연결한 `Txt_StatusMessage` UI가 같은 이유를 표시하는지 함께 확인합니다.

---

## 6) 권장 분기 UX

## 6-0) 트러블슈팅: `JoinFailed:SessionNotFound`

Host가 세션을 만들었는데 Client Join에서 `SessionNotFound`가 뜨면, 대부분 아래 원인입니다.

1. **JoinCode 문자열 불일치(공백/소문자)**
   - JoinCode 입력값 앞뒤 공백, 소문자 입력으로 조회 키가 달라질 수 있습니다.
   - 현재 `MultiplayerSessionOrchestrator`는 Join 직전에 `Trim + ToUpperInvariant` 보정 후 조회하도록 구성합니다.

2. **런타임 오브젝트 재생성/참조 꼬임**
   - `MultiplayerSessionOrchestrator` / Backend가 중복 생성되거나 씬 전환 중 참조가 바뀌면, 예상과 다른 인스턴스를 볼 수 있습니다.
   - `MultiplayerRuntimeRoot` 단일 인스턴스 + `DontDestroyOnLoad=true` 구성을 유지하세요.

### Host 세션을 실제로 확인하려면?
- 로컬 단일 프로세스 검증: 같은 실행 인스턴스에서 Host 생성 후 Client Join 흐름을 테스트합니다.
- 로컬 다중 인스턴스 검증: `LocalFileMultiplayerSessionBackend`를 사용하고 Host/Client의 `_storageFileName`을 동일하게 맞춥니다.
- 멀티 프로세스(실기기/멀티 클라이언트) 검증: UGS Relay 기반 공유 백엔드 어댑터를 사용하세요.

멀티플레이 UX는 Slot 선택 이후 Host/Client를 분기하는 구조를 기본으로 한다.
타이틀에서 바로 Host/Client를 나누지 않고, 공용 SlotSelect를 거쳐 의도를 확정한다.

## 6-1. 싱글
1. Title > `Panel_SingleMenu` 진입
2. `Continue` > 마지막 슬롯 기준 즉시 시작 (데이터 있을 때만)
3. `New Game` / `Load Game` > `Panel_SlotSelect` 이동
4. 슬롯 선택 후 조건에 따라 시작
	- New Game: 기존 데이터 삭제 후 시작
	- Load Game: 데이터 있을 때만 시작

## 6-2. 멀티 Host
1. Title > `Btn_Multiplay` > `Panel_SlotSelect`
2. 슬롯 선택 (활성 슬롯 확정)
3. `Panel_MultiplayMode` 진입
4. `Btn_Host` 선택 > 세션 생성
5. 성공 시 즉시 Town 이동
6. Town HUD에서 Join Code 표시 (Client 미접속 시에만)

## 6-3. 멀티 Join
1. Title > `Btn_Multiplay` > `Panel_SlotSelect`
2. 슬롯 선택 (활성 슬롯 확정)
3. `Panel_MultiplayMode` 진입
4. `Btn_Client` 선택 → Join 팝업 오픈 (`MultiplayerPanelRoot`)
5. Join Code 입력 → `Join` 클릭
6. 성공 시 Town 합류, 실패 시 `Txt_StatusMessage`에 사유 표시
7. Client 준비 완료 선언은 별도 버튼 없이, Client 씬 로드 완료 시 시스템이 자동으로 보고합니다.

---

## 7) 검증 체크리스트

- [ ] 타이틀 진입 시 `Panel_TopMenu`만 활성화되고 나머지 패널은 비활성 상태인지 확인
- [ ] `Btn_SinglePlay` 클릭 시 `Panel_SingleMenu`로 정상 전환되는지 확인
- [ ] `Btn_Multiplay` 클릭 시 `Panel_SlotSelect`로 진입하며 `FlowMode=Multiplay`가 설정되는지 확인

### 싱글 플레이 흐름

- [ ] `Continue`는 마지막 슬롯 데이터가 있을 때만 활성화되고 정상 시작되는지 확인
- [ ] `New Game` → `SlotSelect` → 슬롯 선택 시 기존 데이터 삭제 후 시작되는지 확인
- [ ] `Load Game` → `SlotSelect` → 데이터 있는 슬롯만 선택 가능하고 정상 로드되는지 확인
- [ ] 데이터 없는 상태에서 `Continue` / `Load Game`이 비활성 처리되는지 확인

### 슬롯 선택 공통

- [ ] 슬롯 클릭 시 항상 `SelectPlaySlot(slotIndex)`가 먼저 호출되는지 확인
- [ ] 슬롯 상태(`Used / Empty`)가 정확하게 표시되는지 확인
- [ ] `Single / Multiplay` 모두 동일한 `Panel_SlotSelect`를 사용하는지 확인

### 멀티플레이 흐름

- [ ] `Multiplay` → `SlotSelect` → `MultiplayMode` 순서로 진입하는지 확인
- [ ] 슬롯 선택 시 바로 시작되지 않고 `Panel_MultiplayMode`로 이동하는지 확인
- [ ] `Btn_Host` 클릭 시 세션 생성 후 즉시 Town으로 이동하는지 확인
- [ ] `Btn_Client` 클릭 시 Join 팝업(`MultiplayerPanelRoot`)이 오픈되는지 확인
- [ ] Client가 정상적으로 Join 및 로드 완료 후, Host의 Stage 진입 제한이 해제되는지 확인

### Join 팝업

- [ ] Join 팝업이 기존 화면 위에 Overlay로 표시되는지 확인 
- [ ] Join Code 입력 후 `Join` 클릭 시 정상적으로 세션 참가 시도하는지 확인
- [ ] Join 실패 시 `Txt_StatusMessage`에 사유가 표시되는지 확인
- [ ] `Btn_MultiClose` 클릭 시 팝업만 닫히고 이전 화면 상태가 유지되는지 확인

### Town Join Code 표시

- [ ] Host 상태에서 Client 미접속 시 Join Code가 표시되는지 확인
- [ ] Client가 접속하면 Join Code가 숨겨지거나 표시되지 않는지 확인
- [ ] Client 상태에서는 Join Code UI가 숨김 또는 `-`로 표시되는지 확인

### 안정성

- [ ] 잘못된 상태에서 버튼 입력 시 동작이 실행되지 않고 안전하게 무시되는지 확인
- [ ] 세션 실패 / 재접속 실패 시 UI 상태가 꼬이지 않고 정상 복귀하는지 확인
- [ ] Host 종료 또는 세션 종료 시 타이틀로 정상 복귀하는지 확인

---

## 8) 운영 전환 시 교체 포인트

로컬 테스트에서는 `LocalFileMultiplayerSessionBackend`를 사용하고,
실제 배포 전에는 아래만 교체하면 됩니다.

1. `IMultiplayerSessionBackend` 구현체를 UGS Relay 기반으로 교체
2. Join Code 생성/참가 로직을 Relay API로 연결
3. 실패 코드 매핑(`SessionNotFound`, `SessionFull` 등)을 운영 에러 코드로 표준화

즉, **UI-오케스트레이터-백엔드 인터페이스 구조를 유지한 채로 인프라만 교체**하는 것이 핵심입니다.

---

## 9) Client Join 실패 메시지 UI 확장 설정

Client Join 실패가 발생했을 때 사용자에게 실패 사유를 즉시 노출하려면 아래 컴포넌트를 추가하세요.

### 9-1. 추가 스크립트
- `Assets/_Workspace/ParkJaeBok/Scripts/GameFlow/Multiplayer/Core/E_ClientJoinFailureType.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/GameFlow/Multiplayer/Core/ClientJoinFailureResult.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/UI/TitleMenu/MultiplayerJoinFailureMessageCatalog.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/UI/TitleMenu/MultiplayerJoinFailurePresenter.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/UI/TitleMenu/MultiplayerJoinFailureView.cs`

### 9-2. ScriptableObject 생성
1. Project 창에서 `Create > Game > UI > Multiplayer Join Failure Message Catalog`를 선택합니다.
2. 에셋 이름을 예: `SO_JoinFailureMessageCatalog`로 저장합니다.
3. `Entries`에 실패 유형별 문구/정책을 입력합니다.
   - `FailureType`: JoinCodeEmpty, SessionNotFound, SessionFull, StageInProgress, NetworkUnavailable 등
   - `UseTitle`: 제목 노출 여부
   - `Title` / `Body`: 사용자 문구
   - `AutoClose` / `AutoCloseDelaySeconds`: 자동 닫힘 정책
   - `UseManualCloseButton`: 닫기 버튼 사용 여부
4. 미매핑 실패를 위한 `Fallback` 섹션 문구도 반드시 설정합니다.

### 9-3. Scene 오브젝트 구성(프리팹 가능)
`MultiplayerPanelRoot/Panel_Multiplayer` 하위에 실패 메시지 패널을 추가합니다.

예시:
```text
Panel_Multiplayer
├── Input_JoinCode
├── Btn_JoinSubmit
├── Panel_JoinFailureMessage (기본 비활성)
│   ├── Txt_FailureTitle (TMP_Text)
│   ├── Txt_FailureBody (TMP_Text)
│   └── Btn_CloseFailure (Button)
```

### 9-4. 컴포넌트 연결
1. `Panel_JoinFailureMessage` 또는 상위 UI 루트에 `MultiplayerJoinFailureView`를 붙입니다.
2. 아래 필드를 연결합니다.
   - `_messageRoot` -> `Panel_JoinFailureMessage`
   - `_titleText` -> `Txt_FailureTitle`
   - `_bodyText` -> `Txt_FailureBody`
   - `_closeButton` -> `Btn_CloseFailure`
3. `TitleMenuRoot`(또는 Join 팝업 관리 루트)에 `MultiplayerJoinFailurePresenter`를 붙입니다.
4. 아래 필드를 연결합니다.
   - `_multiplayerSessionOrchestrator` -> `MultiplayerRuntimeRoot/MultiplayerSessionOrchestrator`
   - `_view` -> `MultiplayerJoinFailureView`
   - `_messageCatalog` -> `SO_JoinFailureMessageCatalog`

### 9-5. Orchestrator 이벤트 확인
`MultiplayerSessionOrchestrator`는 Join 실패 시 아래 두 경로를 모두 발행합니다.
- 기존 문자열 이벤트: `_onSessionFailed`
- 표준 실패 이벤트: `_onClientJoinFailed` + `ClientJoinFailed`

> 권장: 신규 UI는 `ClientJoinFailed` 흐름을 사용하고, 기존 `_onSessionFailed`는 레거시 호환 목적으로 유지하세요.
