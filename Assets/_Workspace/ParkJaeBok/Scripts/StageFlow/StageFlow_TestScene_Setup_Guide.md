# StageFlow 테스트 씬 구성 가이드

이 문서는 `StageFlow` 런타임 스크립트(씬 전환/세션/스폰/BGM 적용)를 직접 검증할 수 있는 테스트 씬 구성 방법을 안내합니다.

---

## 1) 준비물

- StageFlow 스크립트
  - `StageDefinition`
  - `StageCatalog`
  - `StageSession`
  - `SceneTransitionService`
  - `StageEntryPoint`
  - `StageSpawnResolver`
  - `StagePlayerSpawnTarget`
  - `StageBgmContextApplier`
  - `TownStageSelectorPresenter`
- 기존 시스템
  - `AudioManager`
  - `BgmDirector`
  - `InputManager`

---

## 2) 테스트 씬 구성(권장)

- `Town_Test` : 마을/선택
- `Stage_Test_01` : 스테이지
- (선택) `Stage_Test_02` : 다중 스테이지 선택 검증

---

## 3) StageDefinition / StageCatalog 에셋

### 3.1 StageDefinition 생성

- `Create > Game > Stage Flow > Stage Definition`

예시:
- `SD_Stage01`
  - `Stage Id = stage_01`
  - `Scene Name = Stage_Test_01`
  - `Stage Entry Point Id = Stage01_Entry_A`
  - `Town Return Point Id = Town_Return_A`
  - `Bgm Context Type = Exploration`

### 3.2 StageCatalog 생성

- `Create > Game > Stage Flow > Stage Catalog`
- `Stages` 목록에 `SD_Stage01`(및 선택 스테이지)을 등록

---

## 4) Town_Test 필수 오브젝트 상세

아래 오브젝트를 **Town_Test 씬**에 배치합니다.

### A. 시스템 루트

1. `Town_SystemRoot` (빈 GameObject)
- 컴포넌트
  - `StageSpawnResolver`
  - `StageBgmContextApplier`
- 주요 설정 (`StageSpawnResolver`)
  - `Explicit Player Transform`: 플레이어 루트 Transform 직접 연결(권장)
  - `Use Spawn Target Marker = true`
  - `Allow Tag Fallback = true` (초기 도입), 안정화 후 `false` 권장
  - `Player Tag = Player`
  - `Max Resolve Retry Count = 10~30`
  - `Resolve Retry Interval = 0.05~0.2`
- 주요 설정 (`StageBgmContextApplier`)
  - `Apply On Enable = true`
  - `Consume Context After Apply = true`
  - `Pop Applied Context On Disable = true`
  - `Bgm Director`: 비워두면 자동 탐색

2. `Town_TransitionService` (빈 GameObject)
- 컴포넌트
  - `SceneTransitionService`
- 주요 설정
  - `DontDestroyOnLoad = true`
  - `Block Input While Transition = true`

3. `Town_Session` (빈 GameObject)
- 컴포넌트
  - `StageSession`
- 주요 설정
  - `DontDestroyOnLoad = true`

### B. 오디오/입력

4. `Town_Audio`
- 컴포넌트
  - `AudioManager`
  - `BgmDirector`
- 주요 설정
  - `BgmDirector` Rule Table에 `Exploration`, `Menu`, `Boss` 컨텍스트 등록

5. `Player`
- 컴포넌트
  - `InputManager` (+ `PlayerInput`)
  - `StagePlayerSpawnTarget` (중요)
- 주요 설정
  - Tag = `Player`
  - `StagePlayerSpawnTarget.Is Primary = true`
  - `StagePlayerSpawnTarget.Priority = 100`
  - (선택) `Spawn Pivot`에 실제 이동 기준 Transform 연결

### C. UI/선택

6. `Canvas_StageSelect`
- 하위
  - `Btn_Stage01`
  - `Btn_Stage02` (선택)

7. `Town_StageSelectPresenter`
- 컴포넌트
  - `TownStageSelectorPresenter`
- 주요 설정
  - `Stage Catalog = SC_StageCatalog`
- 버튼 이벤트 예시
  - `Btn_Stage01.OnClick -> SelectStageById("stage_01")`

### D. 마을 복귀/스폰 포인트

8. `Town_Return_A`
- 컴포넌트
  - `StageEntryPoint`
- 주요 설정
  - `Entry Point Id = Town_Return_A`
  - `Is Fallback Point = true`

9. `Town_Return_B` (선택)
- 컴포넌트
  - `StageEntryPoint`
- 주요 설정
  - `Entry Point Id = Town_Return_B`
  - `Is Fallback Point = false` (시나리오에 맞게)

---

## 5) Stage_Test_01 필수 오브젝트 상세

아래 오브젝트를 **Stage_Test_01 씬**에 배치합니다.

### A. 시스템 루트

1. `Stage01_SystemRoot` (빈 GameObject)
- 컴포넌트
  - `StageSpawnResolver`
  - `StageBgmContextApplier`
- 주요 설정
  - `StageSpawnResolver.Explicit Player Transform` 연결 권장
  - `Use Spawn Target Marker = true`
  - `Allow Tag Fallback = true` (초기), 안정화 후 `false` 권장

### B. 스테이지 진입 포인트

2. `Stage01_Entry_A`
- 컴포넌트
  - `StageEntryPoint`
- 주요 설정
  - `Entry Point Id = Stage01_Entry_A`
  - `Is Fallback Point = true`

3. `Stage01_Entry_B` (선택)
- 컴포넌트
  - `StageEntryPoint`
- 주요 설정
  - `Entry Point Id = Stage01_Entry_B`
  - `Is Fallback Point = false`

### C. 플레이어/카메라

4. `Player`
- 컴포넌트
  - `StagePlayerSpawnTarget` (중요)
- 주요 설정
  - Tag = `Player`
  - `StagePlayerSpawnTarget.Is Primary = true`
  - `StagePlayerSpawnTarget.Priority = 100`

5. `Main Camera`
- 기존 추적 로직 연결

### D. 마을 복귀 트리거(테스트용)

6. `Btn_BackToTown` 또는 `Portal_BackToTown`
- 복귀 호출 예시
  1. `StageSession.Instance.SetTownReturnPoint("Town_Return_A")`
  2. `SceneTransitionService.Instance.TryLoadScene("Town_Test")`

---

## 6) 권장 Hierarchy 예시

### Town_Test
- `Town_SystemRoot` (`StageSpawnResolver`, `StageBgmContextApplier`)
- `Town_TransitionService` (`SceneTransitionService`)
- `Town_Session` (`StageSession`)
- `Town_Audio` (`AudioManager`, `BgmDirector`)
- `Player` (`InputManager`, `PlayerInput`, `StagePlayerSpawnTarget`)
- `Canvas_StageSelect`
  - `Btn_Stage01`
- `Town_StageSelectPresenter` (`TownStageSelectorPresenter`)
- `Town_Return_A` (`StageEntryPoint`)

### Stage_Test_01
- `Stage01_SystemRoot` (`StageSpawnResolver`, `StageBgmContextApplier`)
- `Player` (`StagePlayerSpawnTarget`)
- `Main Camera`
- `Stage01_Entry_A` (`StageEntryPoint`)
- `Portal_BackToTown` (선택)

---

## 7) Build Settings

- `Town_Test`
- `Stage_Test_01`
- (선택) `Stage_Test_02`

`StageDefinition.Scene Name`과 Build Settings 씬 이름이 정확히 일치해야 합니다.

---

## 8) 검증 체크리스트

1. Town에서 Stage 선택 시 씬 전환되는가?
2. Stage 진입 시 `Stage01_Entry_A`로 정확히 스폰되는가?
3. `Player` 태그 객체가 여러 개여도 `StagePlayerSpawnTarget` 대상이 우선 선택되는가?
4. 빠른 연속 클릭 시 씬 전환이 1회만 시작되는가?
5. Stage에서 Town 복귀 시 `Town_Return_A`로 복귀되는가?

---

## 9) 문제 발생 시 점검 포인트

- 씬 전환 실패: 씬 이름 오타 / Build Settings 미등록
- 스폰 실패: `StageEntryPoint` 누락 또는 ID 불일치
- 오대상 이동: `StagePlayerSpawnTarget` 누락, `Is Primary/Priority` 설정 오류, `Explicit Player Transform` 미지정
- BGM 미적용: `BgmDirector` 누락 또는 `Bgm Context Type = None`

