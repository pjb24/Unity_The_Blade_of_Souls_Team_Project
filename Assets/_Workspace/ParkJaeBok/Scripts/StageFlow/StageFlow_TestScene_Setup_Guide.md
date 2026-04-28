# Stage 선택/진입 테스트 씬 구성 가이드 (버튼 UI 샘플 포함)

> 현재 플레이어 스폰 위치 결정은 `PlayerSpawnCoordinator`와 `PlayerSpawnPointRegistry`로 일원화되어 있습니다. 이 문서에 남아 있는 `StageEntryPoint`, `StageSpawnResolver`, `StagePlayerSpawnTarget` 언급은 기존 테스트 씬 직렬화 호환을 위한 레거시 항목입니다.

이 문서는 아래 기능을 **한 번에 검증**할 수 있도록 테스트 씬을 구성하는 상세 절차입니다.

- 마을(Map) UI에서 스테이지 버튼 선택
- 스테이지 개방 조건/입장 가능 상태 판정
- 선택된 스테이지 씬 진입
- 스테이지 클리어 결과를 런타임 진행도에 반영
- 진행도 저장/복원(기존 SaveSystem participant 재사용)

---

## 0) 테스트 목표

최종적으로 아래 6가지를 확인합니다.

1. `TownStageSelectorPresenter.SelectStageById(...)`로 버튼 선택 시 올바른 씬 전환이 시작된다.
2. 잠금 조건 미충족 스테이지는 진입이 차단된다.
3. `IsEntryEnabled = false` 스테이지는 개방 상태여도 진입이 차단된다.
4. `AllowMultiplayer` 상태가 선택 로그/상태 평가에서 반영된다.
5. `ReportStageClearedById(...)` 호출 후 다음 스테이지 개방 조건이 즉시 반영된다.
6. `StageProgressSaveParticipant`를 통해 진행도가 저장/복원된다.

---

## 1) 준비물(스크립트/시스템)

### StageFlow
- `StageDefinition`
- `StageCatalog`
- `StageSession`
- `SceneTransitionService`
- `StageProgressRuntime`
- `StageAvailabilityService`
- `TownStageSelectorPresenter`
- `StageReturnToTownPresenter`
- `PlayerSpawnCoordinator`
- `PlayerSpawnPointRegistry`
- `PlayerSpawnPoint`

### SaveSystem
- `SaveCoordinator`
- `JsonFileSaveBackend`
- `StageSessionSaveParticipant`
- `StageProgressSaveParticipant`

### 기존 공용 시스템(있다면)
- `InputManager`
- `AudioManager`
- `BgmDirector`

---

## 2) 테스트 씬 구성(권장 씬 3개)

- `Town_StageSelect_Test` : 버튼 UI로 스테이지 선택
- `Stage_Test_01` : 기본 진입 스테이지
- `Stage_Test_02` : 선행 조건 스테이지(예: Stage01 클리어 후 개방)

> 이미 프로젝트에 `Town_Test`, `Stage_Test_01`, `Stage_Test_02`가 있으면 이름 그대로 재사용해도 됩니다.

---

## 3) ScriptableObject 데이터 구성

## 3.1 StageDefinition 생성

`Create > Game > Stage Flow > Stage Definition`

아래 예시처럼 3개를 만듭니다.

### T) `SD_Town`
- `Stage Id`: `town_main`
- `Scene Name`: `Town_StageSelect_Test`
- `Display Name`: `마을`
- `Is Town Stage`: `true`
- `Stage Entry Point Id`: `Town_Default`
- `Town Return Point Id`: `Town_Default`
- `Is Entry Enabled`: `true`
- `Is Initially Unlocked`: `true`
- `Unlock Conditions`: 비움
- `Allow Multiplayer`: `false`
- `Bgm Context Type`: `Menu`

### A) `SD_Stage01`
- `Stage Id`: `stage_01`
- `Scene Name`: `Stage_Test_01`
- `Display Name`: `1-1 초원 입구`
- `Stage Entry Point Id`: `Stage01_Entry_A`
- `Town Return Point Id`: `Town_Return_A`
- `Is Entry Enabled`: `true`
- `Is Initially Unlocked`: `true`
- `Unlock Conditions`: 비움
- `Allow Multiplayer`: `false`
- `Bgm Context Type`: `Exploration`

### B) `SD_Stage02`
- `Stage Id`: `stage_02`
- `Scene Name`: `Stage_Test_02`
- `Display Name`: `1-2 숲 심층`
- `Stage Entry Point Id`: `Stage02_Entry_A`
- `Town Return Point Id`: `Town_Return_A`
- `Is Entry Enabled`: `true`
- `Is Initially Unlocked`: `false`
- `Unlock Conditions`: 1개 추가
  - `Condition Type`: `ClearSpecificStage`
  - `Required Stage Id`: `stage_01`
- `Allow Multiplayer`: `true`
- `Bgm Context Type`: `Exploration`

## 3.2 StageCatalog 생성

`Create > Game > Stage Flow > Stage Catalog`

- 파일 예: `SC_StageCatalog_Test`
- `Town Stage Id`를 `town_main`으로 설정
- `Stages` 리스트에 순서대로 등록
  1. `SD_Town`
  2. `SD_Stage01`
  3. `SD_Stage02`

---

## 4) Town 씬 상세 구성

## 4.1 필수 오브젝트

### A) `Town_SystemRoot`
- 컴포넌트
  - `PlayerSpawnCoordinator`
  - `PlayerSpawnPointRegistry`
- 권장값
  - `PlayerSpawnPointRegistry.Auto Collect From Children = true`
  - `PlayerSpawnCoordinator.Registry = Town_SystemRoot의 PlayerSpawnPointRegistry`

### B) `Town_Session`
- 컴포넌트
  - `StageSession` (`DontDestroyOnLoad = true`)
  - `StageSessionSaveParticipant`
  - `StageProgressSaveParticipant`
  
### C) `Town_TransitionService`
- 컴포넌트
  - `SceneTransitionService`
- 권장값
  - `DontDestroyOnLoad = true`
  - `BlockInputWhileTransition = true`

### D) `Town_StageProgressRuntime`
- 컴포넌트
  - `StageProgressRuntime`
- 권장값
  - `DontDestroyOnLoad = true`

### E) `Town_SaveRoot`
- 컴포넌트
  - `SaveCoordinator`
  - `JsonFileSaveBackend`

### F) `Town_StageSelectPresenter`
- 컴포넌트
  - `TownStageSelectorPresenter`
- Inspector 연결
  - `Stage Catalog = SC_StageCatalog_Test`
  - `Use Availability Validation = true`
  - `Play Ui Click Sfx = true/false`
  - `Verbose Log = true`

### G) `Town_PlayerSpawnPoints`
- 컴포넌트
  - `PlayerSpawnCoordinator`
  - `PlayerSpawnPointRegistry`
  - 자식 오브젝트별 `PlayerSpawnPoint`
- 값
  - `Single`, `Host`, `Client` 슬롯별 위치를 씬 안에서 직접 배치

---

## 5) 진입할 스테이지를 선택하는 버튼 UI 만들기 (샘플)

> 요구사항에 맞춰 **버튼만 사용하는 최소 UI**입니다.

## 5.1 Canvas 생성

1. Hierarchy에서 `UI > Canvas` 생성
2. 이름: `Canvas_StageSelect`
3. Canvas 하위에 `UI > Panel` 생성
4. 이름: `Panel_StageButtons`

권장 레이아웃:
- `Vertical Layout Group` 추가
- Spacing 12~20
- Child Force Expand Width = true

## 5.2 버튼 4개 생성

`Panel_StageButtons` 하위에 아래 버튼들을 만듭니다.

1. `Btn_Stage01` (텍스트: `입장: stage_01`)
2. `Btn_Stage02` (텍스트: `입장: stage_02`)
3. `Btn_ClearStage01` (텍스트: `클리어 처리: stage_01`)
4. `Btn_CheckStage02` (텍스트: `상태 확인: stage_02`)

## 5.3 버튼 OnClick 연결

모든 버튼의 OnClick Target은 `Town_StageSelectPresenter`로 지정합니다.

- `Btn_Stage01`
  - `TownStageSelectorPresenter.SelectStageById`
  - argument: `stage_01`

- `Btn_Stage02`
  - `TownStageSelectorPresenter.SelectStageById`
  - argument: `stage_02`

- `Btn_ClearStage01`
  - `TownStageSelectorPresenter.ReportStageClearedById`
  - argument: `stage_01`

- `Btn_CheckStage02`
  - 직접 문자열 반환을 UI로 출력하는 기능이 없으므로,
  - 우선 `SelectStageById("stage_02")`를 눌러 진입 성공/실패와 Console 로그를 확인하거나,
  - 필요 시 팀의 기존 디버그 UI/토스트 시스템에 `EvaluateStageAvailabilityById("stage_02")` 결과를 표시하도록 연결합니다.

---

## 6) Stage_Test_01 / Stage_Test_02 씬 구성

각 스테이지 씬에 공통으로 아래를 배치합니다.

1. `StageXX_SystemRoot`
   - `PlayerSpawnCoordinator`
   - `PlayerSpawnPointRegistry`
2. `StageXX_Spawn_Single`, `StageXX_Spawn_Host`, `StageXX_Spawn_Client`
   - 각각 `PlayerSpawnPoint`
   - `Slot = Single / Host / Client`

3. `StageXX_ReturnPresenter`
   - `StageReturnToTownPresenter`

5. `Btn_ReturnToTown` (UI Button)
   - OnClick -> `StageReturnToTownPresenter.ReturnToTown()`

예시 ID:
- `Stage_Test_01`: `Stage01_Entry_A`
- `Stage_Test_02`: `Stage02_Entry_A`

---

## 7) Build Settings 등록

`File > Build Settings > Scenes In Build`에 아래 순서로 등록:

1. `Town_StageSelect_Test` (또는 기존 Town 씬)
2. `Stage_Test_01`
3. `Stage_Test_02`

`StageDefinition.Scene Name`과 Build Settings의 씬 이름이 100% 일치해야 합니다.

---

## 8) 실제 테스트 시나리오 (버튼 기준)

## 시나리오 A: 잠금 상태 검증
1. Town 씬 실행
2. `입장: stage_02` 버튼 클릭
3. 기대 결과
   - 진입 실패
   - Console에 "Need clear stage: stage_01" 유사 로그 출력

## 시나리오 B: 클리어 반영 후 개방 검증
1. Town 씬에서 `클리어 처리: stage_01` 버튼 클릭
2. `입장: stage_02` 버튼 클릭
3. 기대 결과
   - Stage_Test_02 씬 진입 성공

## 시나리오 C: Stage -> Town 복귀 검증
1. `입장: stage_01` 또는 `입장: stage_02`로 스테이지 진입
2. 스테이지 씬에서 `Btn_ReturnToTown` 클릭
3. 기대 결과
   - Town 씬으로 전환
   - 해당 스테이지 `Town Return Point Id` 위치로 복귀

## 시나리오 D: 저장/복원 검증
1. 시나리오 B 수행 후 저장 실행(팀의 기존 저장 트리거 사용)
2. Play 종료 후 재실행
3. 저장 로드
4. `입장: stage_02` 버튼 클릭
5. 기대 결과
   - 재클리어 처리 없이도 진입 가능

---

## 9) 자주 발생하는 실수 체크리스트

1. `Stage Id` 오타 (`stage_01` vs `Stage_01`)
2. `Required Stage Id`와 실제 `Stage Id` 불일치
3. `Scene Name` 오타 또는 Build Settings 미등록
4. `Use Availability Validation`이 꺼져 있어 잠금이 무시됨
5. `StageProgressSaveParticipant` 미배치로 저장/복원이 되지 않음
6. `StageCatalog.Town Stage Id` 미설정 + `Is Town Stage` 미체크로 복귀 실패

---

## 10) 확장 팁 (팀 운영용)

- 새 스테이지 추가 시:
  1. `StageDefinition` 에셋 생성
  2. `StageCatalog`에 등록
  3. 버튼 1개 추가 + `SelectStageById("새 stageId")` 연결
- 잠금 규칙 확장 시:
  - 조건 타입을 늘리고 `StageAvailabilityService`의 조건 평가 분기를 추가
- UI 고도화 시:
  - 현재 버튼 샘플을 유지한 채, 기존 UI 시스템에서 `EvaluateStageAvailabilityById` 결과를 바인딩해 잠금 아이콘/문구를 표시

---

## 11) 권장 Hierarchy 샘플

### Town_StageSelect_Test
- `Town_SystemRoot` (`PlayerSpawnCoordinator`, `PlayerSpawnPointRegistry`)
- `Town_Session` (`StageSession`)
- `Town_TransitionService` (`SceneTransitionService`)
- `Town_StageProgressRuntime` (`StageProgressRuntime`)
- `Town_SaveRoot` (`SaveCoordinator`, `JsonFileSaveBackend`, `StageSessionSaveParticipant`, `StageProgressSaveParticipant`)
- `Town_StageSelectPresenter` (`TownStageSelectorPresenter`)
- `Town_PlayerSpawnPoints` (`PlayerSpawnPoint`)
- `Canvas_StageSelect`
  - `Panel_StageButtons`
    - `Btn_Stage01`
    - `Btn_Stage02`
    - `Btn_ClearStage01`
    - `Btn_CheckStage02`

### Stage_Test_01 / Stage_Test_02
- `StageXX_SystemRoot` (`PlayerSpawnCoordinator`, `PlayerSpawnPointRegistry`)
- `StageXX_PlayerSpawnPoints` (`PlayerSpawnPoint`)
- `StageXX_ReturnPresenter` (`StageReturnToTownPresenter`)
- `Canvas`
  - `Btn_ReturnToTown`
- `Main Camera`
