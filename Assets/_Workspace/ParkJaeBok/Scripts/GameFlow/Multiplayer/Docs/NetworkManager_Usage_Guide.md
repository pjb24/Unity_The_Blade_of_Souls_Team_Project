# Unity NetworkManager 멀티플레이 사용 가이드

이 문서는 프로젝트에서 `MultiplayerSessionOrchestrator` + `NetworkManagerMultiplayerSessionBackend` 조합으로
멀티플레이를 실행하는 방법을 설명합니다.

---

## 1) 개요

현재 멀티플레이 흐름은 아래 순서로 동작합니다.

1. UI(타이틀)에서 Host/Client 버튼 클릭
2. `MultiplayerSessionOrchestrator`가 `IMultiplayerSessionBackend`에 세션 생성/참가 요청
3. `NetworkManagerMultiplayerSessionBackend`가 Unity Netcode `NetworkManager`의
   - Host: `StartHost()`
   - Client: `StartClient()`
   를 호출
4. 인원 수는 `NetworkManager.ConnectedClientsList.Count`로 조회
5. 세션 종료 시 `NetworkManager.Shutdown()` 호출

---

## 2) 사전 준비

### 필수 컴포넌트

- 씬에 `NetworkManager` 오브젝트 1개
- 씬에 `MultiplayerSessionOrchestrator` 오브젝트 1개
- 씬에 `NetworkManagerMultiplayerSessionBackend` 오브젝트 1개

> `MultiplayerSessionOrchestrator`의 `_sessionBackendBehaviour`를 비워두면 런타임 자동 탐색을 시도하지만,
> 명시적으로 `NetworkManagerMultiplayerSessionBackend`를 연결하는 것을 권장합니다.

### 권장 연결

- `MultiplayerSessionOrchestrator._sessionBackendBehaviour`
  → `NetworkManagerMultiplayerSessionBackend` 컴포넌트 연결
- `NetworkManagerMultiplayerSessionBackend._networkManager`
  → 씬의 `NetworkManager` 컴포넌트 연결

### Bootstrap 씬 vs Title 씬 배치 기준

결론부터 권장 순서는 아래와 같습니다.

1. **`NetworkManager`는 Bootstrap 씬에 배치 (권장)**
2. **Title 씬에는 UI 입력/버튼과 Presenter 중심으로 유지**

#### 왜 Bootstrap 씬이 권장인가?

- 멀티플레이 연결 객체(`NetworkManager`)는 세션 전체 수명주기(타이틀 → 타운 → 스테이지) 동안
  유지되는 편이 안정적입니다.
- Bootstrap에서 초기화 후 `DontDestroyOnLoad` 정책으로 유지하면,
  씬 전환 때 재생성/중복 생성 문제를 줄일 수 있습니다.
- Host/Client 전환 시점에서 네트워크 객체 레퍼런스를 일관되게 참조하기 쉽습니다.

#### Title 씬에는 무엇을 두면 좋은가?

- `TitlePlayModePresenter`, Join Code 입력 UI, 버튼 이벤트 바인딩 등
  **사용자 상호작용(UI) 컴포넌트**
- 필요 시 `MultiplayerSessionOrchestrator`는 Bootstrap에서 유지하고,
  Title에서는 참조만 연결하도록 구성
- Join Code InputField는 `TitleJoinCodeInputFieldBinder`를 통해
  런타임에 오케스트레이터로 등록하는 구성을 권장
- `TitlePlayModePresenter`도 멀티 버튼 클릭 시 오케스트레이터가 비어 있으면
  DDOL 영역까지 자동 재탐색하도록 구성
- 필요 시 Join 버튼을 `TitlePlayModePresenter.OnClickJoinSessionFromTitle()`에 직접 연결해
  오케스트레이터 Join 결과를 Presenter 이벤트로 처리 가능

#### 예외적으로 Title 씬 배치가 가능한 경우

- 프로젝트가 Title 씬 단일 구조이고 씬 전환이 거의 없으며,
  멀티 상태를 다음 씬으로 넘길 필요가 없는 프로토타입 단계
- 이 경우에도 `NetworkManager` 중복 생성을 막는 가드(싱글톤/중복 제거)는 필수

---

## 3) Inspector 설정 항목

### MultiplayerSessionOrchestrator

- **Max Player Count (`_maxPlayerCount`)**
  - Host 포함 최대 인원
- **Reconnect Window Seconds (`_reconnectWindowSeconds`)**
  - 클라이언트 재접속 허용 시간
- **Dont Destroy On Load (`_dontDestroyOnLoad`)**
  - 씬 전환 시 오케스트레이터 유지 여부
- **UI Join Code Input Field (`_uiJoinCodeInputField`)**
  - Join Code 입력 TMP_InputField
- **UI Client Id (`_uiClientId`)**
  - 클라이언트 식별 문자열

### NetworkManagerMultiplayerSessionBackend

- **Network Manager (`_networkManager`)**
  - 비어 있으면 `NetworkManager.Singleton` 사용
- **Default Join Code (`_defaultJoinCode`)**
  - Host 생성 시 표시할 기본 Join Code 문자열 (기본값 `LOCAL`)
- **Verbose Logging (`_verboseLogging`)**
  - 디버그 로그 출력 여부

### TitleJoinCodeInputFieldBinder (Title 씬)

- **Multiplayer Session Orchestrator (`_multiplayerSessionOrchestrator`)**
  - 비어 있으면 `MultiplayerSessionOrchestrator.Instance` 자동 탐색
- **Join Code Input Field (`_joinCodeInputField`)**
  - 비어 있으면 같은 오브젝트의 `TMP_InputField` 자동 탐색
- **Release Input Field On Disable (`_releaseInputFieldOnDisable`)**
  - Title UI 비활성화 시 오케스트레이터 입력 필드 참조 해제 여부

---

## 4) 실행 방법

## 4-1) Host 실행

1. 타이틀에서 Host 버튼 클릭
2. `StartHostSessionFromTitle(hostClientId)` 호출
3. 백엔드에서 `NetworkManager.StartHost()` 성공 시 세션 시작
4. Join Code UI에는 기본적으로 `LOCAL` 표시

## 4-2) Client 실행

1. 타이틀에서 Join Code 입력 후 Join 버튼 클릭
2. 버튼 OnClick을 `TitlePlayModePresenter.OnClickJoinSessionFromTitle()`에 연결
3. 오케스트레이터가 입력 필드 값을 읽은 뒤 `JoinSessionFromTitle(joinCode, clientId)` 실행
4. 백엔드에서 `NetworkManager.StartClient()` 성공 시 접속 시도

## 4-3) 플레이어 수 갱신

- `RefreshPlayerCountFromActiveSession()` 호출 시
  `ConnectedClientsList.Count`를 읽어 UI 이벤트(`_onPlayerCountUpdated`)로 전달

## 4-4) 세션 종료

- Host 종료 경로에서 `ShutdownSessionByHost()` 호출
- 백엔드는 `CloseSession()` 내부에서 `NetworkManager.Shutdown()` 실행

---

## 5) 주의 사항

1. 현재 구현의 Join Code는 로컬 표시용 문자열입니다.
   - 인터넷 환경에서 실제 Join Code 기반 접속을 하려면 Relay/Transport 설정이 추가로 필요합니다.
2. Host/Client는 동일한 Transport 설정을 사용해야 정상 접속됩니다.
3. 씬 전환 후에도 `NetworkManager`가 유지되도록 씬 구성 정책을 확인하세요.

---

## 6) 문제 해결 체크리스트

- Host가 시작되지 않음
  - 씬에 `NetworkManager`가 존재하는지 확인
  - 이미 `IsListening == true` 상태인지 확인
- Client가 접속되지 않음
  - Host가 먼저 실행되었는지 확인
  - Transport 주소/포트 설정이 Host/Client에서 일치하는지 확인
- 인원 수가 0으로 표시됨
  - `NetworkManager`가 Listening 상태인지 확인
  - `RefreshPlayerCountFromActiveSession()` 호출 타이밍 확인

---

## 7) 확장 포인트

- Relay 기반 Join Code 연동
  - `TryCreateSession`에서 Relay Allocation 생성
  - `TryJoinSession`에서 Join Code로 Relay Join
  - `UnityTransport`에 Relay 데이터 주입
- Client Ready 동기화 고도화
  - Netcode Custom Messaging 또는 RPC로 Ready 상태를 Host에 전파
