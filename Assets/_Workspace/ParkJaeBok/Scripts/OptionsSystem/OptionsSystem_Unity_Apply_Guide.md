# OptionsSystem Unity 적용 가이드

**OptionsSystem + TitleMenu UI 브리지 구조**를 Unity 프로젝트에 실제로 연결하는 방법을 설명합니다.

- 대상 시스템
  - `OptionManager` (`ISaveParticipant` 구현)
  - `OptionDefaultProfile` (기본값/메타데이터)
  - `OptionDataValidator` (로드 복구/검증)
  - `TitleMenuOptionsPanelBridge` (UI ↔ OptionManager 연결)
  - `OpenOptionsAction`, `OpenLoadGameAction` (타이틀 모달 오픈 액션)

---

## 1. 현재 구조 핵심 요약

### 1-1. 옵션 데이터 계층

- `OptionTypes.cs`
  - 옵션 enum/struct 정의 (Display, Audio, Input, Accessibility, Gameplay)
- `OptionSaveData.cs`
  - 저장 payload 루트
- `OptionRuntimeState.cs`
  - 런타임 메모리 스냅샷 보관 (깊은 복사)

### 1-2. 옵션 서비스 계층

- `OptionDefaultProfile.cs`
  - 기본값 + Numeric 메타데이터(기본/최소/최대/스텝)
- `OptionDataValidator.cs`
  - 누락/손상 데이터 복구 + Warning 로그
- `OptionManager.cs`
  - 런타임 진입점, SaveCoordinator 연동, Set/Get/Save/Load API

### 1-3. UI 브리지 계층

- `IOptionsPanelBindingView`
  - UI 구현체 계약 (옵션값 표시 / 옵션값 수집)
- `ITitleMenuOptionsPanelBridge`
  - 옵션 패널 오픈 훅 계약
- `TitleMenuOptionsPanelBridge`
  - 패널 오픈 시 Load + UI 반영, Apply 시 Set + Save

### 1-4. 타이틀 액션 계층

- `OpenOptionsAction`
  - 옵션 패널 오픈 + 브리지 훅 호출 + 패널 충돌 정리
- `OpenLoadGameAction`
  - 로드 패널 오픈 + 패널 충돌 정리

---

## 2. 사전 확인 (BootstrapTest / TitleMenu_Test)

## 2-1. BootstrapTest 씬

- `BootstrapTitleSceneLoader`의 `_titleSceneName`이 `TitleMenu_Test`인지 확인합니다.
- 값이 다르면 타이틀 씬 진입 자체가 어긋납니다.

## 2-2. TitleMenu_Test 씬

- `TitleMenuPresenter`가 아래 액션을 참조하는지 확인합니다.
  - `_loadGameActionComponent` → `OpenLoadGameAction`
  - `_optionActionComponent` → `OpenOptionsAction`
- `OpenLoadGameAction` 기본 참조
  - `Load Game Entry Point` → `LoadGamePanelRoot`
  - `Modal Backdrop` → `ModalBackdrop`
- `OpenOptionsAction` 기본 참조
  - `Options Entry Point` → `SettingsRoot`
  - `Modal Backdrop` → `ModalBackdrop`

> 기존 씬은 이 연결이 이미 들어가 있을 수 있으나,
> 아래 4장/5장 절차대로 **신규 필드(브리지/패널 충돌 정리용 필드)**를 추가 연결해야 최신 동작을 완성할 수 있습니다.

---

## 3. OptionDefaultProfile 생성/설정

## 3-1. 에셋 생성

1. Project 창 우클릭
2. `Create > Game > Options > Default Profile`
3. 예시 이름: `OptionDefaultProfile_Default`

## 3-2. Inspector 설정

- `Version`
  - `Schema Version`
- `Default Option Values`
  - Display / Audio / Input / Accessibility / Gameplay 기본값
- `Numeric Metadata`
  - Brightness, Volume, SubtitleSpeed, UIScale, Shake 관련 범위/스텝

### 설정 팁

- Numeric `DefaultValue`는 각 카테고리 기본값과 동일하게 맞추면 UI 초기화가 단순해집니다.
- 팀에서 난이도/접근성 기본 정책 변경 시, 코드가 아닌 이 프로필만 변경해도 반영됩니다.

---

## 4. OptionManager 배치 및 SaveSystem 연동

## 4-1. 배치

1. DDOL 루트(또는 시스템 루트)에 GameObject 생성
2. `OptionManager` 컴포넌트 추가
3. 아래 필드 연결
   - `Default Profile` → `OptionDefaultProfile_Default`
   - `Save Coordinator` → 기존 `SaveCoordinator` (비워두면 런타임 탐색)

## 4-2. 동작

- `Awake`
  - 기본값을 런타임 상태로 초기화
- `Start`
  - `LoadCurrentOptions("OptionManager.StartAutoLoad")` 시도
- `RestoreFromJson`
  - 저장 payload 복원 + `OptionDataValidator`로 정규화

## 4-3. 저장/로드 API

- 로드: `LoadCurrentOptions(string triggerContext)`
- 저장: `SaveCurrentOptions(string triggerContext)`
- 조회: `GetCurrentOptions()`
- 일괄 적용: `SetAllOptions(OptionSaveData)`
- 카테고리 적용: `SetDisplayOptions`, `SetAudioOptions`, ...

---

## 5. TitleMenu Options 패널 브리지 연결

`OpenOptionsAction`이 단순 패널 오픈을 넘어서 옵션 데이터 동기화를 수행하려면,
`TitleMenuOptionsPanelBridge` 연결이 필요합니다.

## 5-1. 브리지 배치

1. `SettingsRoot`(또는 옵션 패널 루트)에 `TitleMenuOptionsPanelBridge` 추가
2. 필드 연결
   - `Option Manager` → `OptionManager` (비워두면 Instance 탐색)
   - `Options View Component` → `IOptionsPanelBindingView` 구현 컴포넌트
   - `Reload Options On Open` → On 권장
   - `Apply Snapshot To View On Open` → On 권장
   - `Save After Apply` → On 권장

## 5-2. OpenOptionsAction 연결

`TitleMenuPresenterRoot`의 `OpenOptionsAction`에서 아래를 추가 설정합니다.

- `Options Bridge Component`
  - 방금 배치한 `TitleMenuOptionsPanelBridge`
- `Panel To Close On Open`
  - `LoadGamePanelRoot`

### 결과

- 옵션 열기 직전: 브리지가 저장값 로드 + UI 반영 수행
- 옵션 열 때: Load 패널 열려있으면 자동 닫기

## 5-3. OpenLoadGameAction 연결

`OpenLoadGameAction`에서 아래를 설정합니다.

- `Panel To Close On Open`
  - `OptionsPanelRoot`

### 결과

- Load 열 때: Options 패널이 열려 있으면 자동 닫기
- 모달 패널 중복 표시를 방지

---

## 6. Options UI 스크립트 구현 계약

Options 패널 View 스크립트는 `IOptionsPanelBindingView`를 구현해야 합니다.

- `ApplyOptionsToView(OptionSaveData optionData)`
  - 런타임 스냅샷 값을 드롭다운/슬라이더/토글에 반영
- `TryBuildOptions(out OptionSaveData optionData)`
  - 현재 UI 입력값을 OptionSaveData로 구성

### 권장 구현 패턴

1. 내부에 UI 임시 편집 모델 유지
2. `ApplyOptionsToView`에서 위젯 초기화
3. [Apply] 클릭 시 `TryBuildOptions`에서 루트 모델 생성
4. 브리지 `ApplyViewOptionsToRuntimeAndSave()` 호출

---

## 7. 버튼 이벤트 연결 예시

## 7-1. 옵션 창 열기

- 기존 Title 버튼 흐름 사용 (`TitleMenuPresenter` → `OpenOptionsAction`)

## 7-2. 옵션 [적용] 버튼

- `SettingsRoot` 또는 버튼 OnClick에 아래 메서드 연결
  - `TitleMenuOptionsPanelBridge.ApplyViewOptionsToRuntimeAndSaveFromButton()`
- 코드에서 결과값을 직접 확인하고 싶을 때만 아래 메서드를 사용합니다.
  - `TitleMenuOptionsPanelBridge.ApplyViewOptionsToRuntimeAndSave()`

## 7-3. 옵션 [취소] 버튼

- 저장 없이 닫고 싶으면 기존 `TitleMenuPanelCloseHandler.ClosePanel()` 사용
- 다시 열면 `ReloadOptionsOnOpen`이 켜진 경우 저장값이 재반영됨

## 7-4. 옵션 [Default] 버튼

- 현재 선택된 옵션 1개만 기본값으로 되돌릴 때 아래 메서드를 연결합니다.
  - `TitleMenuOptionsPanelBridge.ApplyDefaultSelectedOptionToViewFromButton()`
- `TitleMenuOptionsPanelBridge.RowDescriptionPresenter`에 `OptionsRowDescriptionPresenter`를 연결하면 마지막 hover Row 기준으로 Default 대상을 안정적으로 찾습니다.

## 7-5. 옵션 [DefaultAll] 버튼

- 현재 탭에 속한 옵션 전체를 기본값으로 되돌릴 때 아래 메서드를 연결합니다.
  - `TitleMenuOptionsPanelBridge.ApplyDefaultCurrentTabOptionsToViewFromButton()`
- `TitleMenuOptionsPanelBridge.TabSectionController`에 `OptionsTabSectionController`를 연결해야 현재 탭 기준으로 동작합니다.

---

## 8. 데이터 복구/폴백 정책

`OptionDataValidator`가 아래를 처리합니다.

- 스키마 버전 불일치
- enum 유효성 실패
- numeric 범위 벗어남
- 필드 누락

복구 시 **항목별 Warning 로그**를 남깁니다.

### 운영 가이드

- QA는 콘솔의 `[OptionDataValidator]` Warning을 우선 확인
- 폴백 로그는 제거하지 말고 유지

---

## 9. Action Row 처리 정책

Action Row는 저장값이 아니라 **진입점**입니다.

- `개별 품질 옵션`
  - `DisplayOptionsData` 상세 필드로 저장
- `입력 설정`
  - `InputOptionsData.KeyBindings[]`로 저장

즉, 버튼 자체를 저장하지 않고 하위 데이터만 저장합니다.

---

## 10. 옵션 추가 시 작업 순서 (최소 수정 포인트)

1. `OptionTypes.cs`
   - enum/struct 필드 추가
2. `OptionDefaultProfile.cs`
   - 기본값/메타데이터 추가
3. `OptionDataValidator.cs`
   - 검증/복구 규칙 추가
4. `IOptionsPanelBindingView` 구현체 UI 코드 갱신

대부분의 경우 `OptionManager`, `TitleMenuOptionsPanelBridge`, 액션 스크립트는 변경하지 않아도 됩니다.

---

## 11. 트러블슈팅

## 11-1. 옵션 열 때 값이 비정상(초기값 고정)

- `OpenOptionsAction.OptionsBridgeComponent` 연결 여부 확인
- 브리지의 `OptionsViewComponent`가 `IOptionsPanelBindingView` 구현인지 확인
- `ReloadOptionsOnOpen`이 꺼져 있으면 이전 런타임 값이 보일 수 있음

## 11-2. Apply 눌러도 저장 안 됨

- 브리지의 `SaveAfterApply` 확인
- `OptionManager`의 `SaveCoordinator` 연결 또는 `SaveCoordinator.Instance` 존재 확인
- 콘솔의 `[OptionManager]` Warning 확인

## 11-3. Load/Options 패널이 겹침

- `OpenOptionsAction.PanelToCloseOnOpen = LoadGamePanelRoot`
- `OpenLoadGameAction.PanelToCloseOnOpen = OptionsPanelRoot`

---

## 12. 권장 다음 단계

다음 작업에서 실제 엔진 반영 계층을 붙일 때는
`OptionManager.AddListener / RemoveListener`에 전용 Applier를 연결하세요.

- 예시
  - `DisplayOptionApplier`
  - `AudioOptionApplier`
  - `AccessibilityOptionApplier`
  - `InputBindingApplier`

이렇게 하면 UI/저장/적용이 느슨하게 분리되어 유지보수가 쉬워집니다.

---

## 13. 옵션 탭 클릭 시 Section On/Off 구현

탭 클릭 시 해당 Section만 On, 나머지는 Off로 제어하려면
`OptionsTabSectionController`를 사용합니다.

## 13-1. 배치

1. `SettingsRoot` 하위에 빈 오브젝트 생성 (예: `TabSectionController`)
2. `OptionsTabSectionController` 컴포넌트 추가
3. `Tab Entries`에 탭별 항목 추가
   - `Tab` : Display / Audio / Input / Accessibility / Gameplay
   - `Section Roots` : 해당 탭에 속한 다수 Section 루트 오브젝트 목록
   - `Tab Button` : 해당 탭 버튼(선택 시 비활성 처리용, 선택사항)

## 13-2. 버튼 OnClick 연결

각 탭 버튼 OnClick에 아래 메서드를 연결합니다.

- `OptionsTabSectionController.SelectTabByIndexFromInt(int)` (Inspector OnClick 권장)

`SelectTabByIndexFromInt(int)`을 사용할 때 인덱스 매핑은 아래와 같습니다.

- Display = `0`
- Audio = `1`
- Input = `2`
- Accessibility = `3`
- Gameplay = `4`

## 13-3. 기본 탭

- `Default Tab`으로 시작 탭을 지정합니다.
- `Select Default On Enable = On`이면 옵션 패널 활성화 시 자동으로 기본 탭이 선택됩니다.

## 13-4. 동작 결과

- 선택된 탭 항목의 `SectionRoots`만 `SetActive(true)`
- 선택되지 않은 나머지 섹션은 `SetActive(false)`
- 연결된 `TabButton`이 있으면 선택된 탭 버튼은 `interactable=false` 처리