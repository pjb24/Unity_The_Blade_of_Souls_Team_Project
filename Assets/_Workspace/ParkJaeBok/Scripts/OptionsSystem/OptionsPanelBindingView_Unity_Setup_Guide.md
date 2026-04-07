# OptionsPanelBindingView Unity 적용 가이드

이 문서는 **구현 완료된 스크립트들**을 실제 Unity 씬/프리팹에 연결하는 절차를 정리합니다.

- 대상 스크립트
  - `OptionsPanelBindingView`
  - `OptionRowBindingEntry`
  - `TitleMenuOptionsPanelBridge`
  - `OptionsTabSectionController`

- 대상 오브젝트
  - `TitleMenu_Test` 씬
  - `SectionRoot` 프리팹
  - `OptionRowRoot_Enum`, `OptionRowRoot_Numeric`, `OptionRowRoot_Action` 프리팹

---

## 1) 사전 체크

### 1-1. Row 타입 기준

현재 Options UI Row 타입은 아래 3가지로 맞춥니다.

- `Enum`
- `Numeric`
- `Action`

관련 기준 문서:
- `Assets/_Workspace/ParkJaeBok/Prefabs/OptionsUI/Docs/07_옵션_UI_배치.md`
- `Assets/_Workspace/ParkJaeBok/Prefabs/OptionsUI/Docs/06_옵션_UI_Option_Row_프리팹.md`

### 1-2. 런타임 시스템 준비

아래가 이미 씬 또는 DDOL 시스템 루트에 있어야 합니다.

- `OptionManager`
- `SaveCoordinator`
- `TitleMenuOptionsPanelBridge`가 참조할 옵션 패널 루트

---

## 2) TitleMenu_Test 적용 절차

## 2-1. SettingsRoot에 View 컴포넌트 추가

1. `TitleMenu_Test` 씬에서 `SettingsRoot`(옵션 패널 루트)를 선택합니다.
2. `OptionsPanelBindingView` 컴포넌트를 추가합니다.
3. Inspector 설정:
   - `Option Manager`: `OptionManager` 연결 (비어두면 `OptionManager.Instance` 탐색)
   - `Use Binding Anchors`: On 권장
   - `Binding Anchors Root`: `SettingsRoot/ContentRoot/LeftPanel/ScrollRect/Viewport/Content` 연결 권장
   - `Include Inactive Anchors`: On 권장
   - `Row Entries`: 자동 수집을 끄는 경우에만 수동 사용
   - `Log Validation Warnings`: On 권장

## 2-2. TitleMenuOptionsPanelBridge 연결

1. 같은 `SettingsRoot`(또는 기존 브리지 오브젝트)에 있는 `TitleMenuOptionsPanelBridge`를 확인합니다.
2. 아래 필드 연결:
   - `Option Manager`: `OptionManager` 연결
   - `Options View Component`: 방금 추가한 `OptionsPanelBindingView` 컴포넌트 드래그
   - `Reload Options On Open`: On 권장
   - `Apply Snapshot To View On Open`: On 권장
   - `Save After Apply`: On 권장

### 2-3. OpenOptionsAction 연결 확인

`TitleMenuPresenterRoot`의 `OpenOptionsAction`에서:

- `Options Bridge Component`에 위 `TitleMenuOptionsPanelBridge`가 연결되어 있어야 합니다.
- 필요 시 `Panel To Close On Open`에 Load 패널 루트를 연결합니다.

---

## 3) SectionRoot 적용 절차

`SectionRoot`는 섹션 제목 + `SectionContent`를 담는 컨테이너입니다.

1. 각 탭(Display/Audio/Input/Accessibility/Gameplay)별로 섹션 프리팹을 배치합니다.
2. 각 `SectionRoot/SectionContent` 아래에 해당 Row 프리팹 인스턴스를 배치합니다.
3. `OptionsTabSectionController`의 `Tab Entries`에 섹션 루트들을 탭별로 등록합니다.
   - 한 탭에 여러 섹션이 있으면 `SectionRoots` 리스트에 모두 추가합니다.

> 포인트: `OptionsPanelBindingView`는 Row 위젯 참조만 필요하므로,
> Section 구조는 UI 배치/가시성 관리(`OptionsTabSectionController`) 관점에서만 맞추면 됩니다.

---

## 4) OptionRow 프리팹별 연결 절차

`Use Binding Anchors = On` 기준으로, 각 Row 오브젝트에 `OptionRowBindingAnchor`를 붙여 로컬 설정합니다.

## 4-1. Enum Row (`OptionRowRoot_Enum`)

- 사용 예: 화면 모드, VSync, 프레임 제한, 난이도, 자막 on/off
- 프리팹 구성 핵심:
  - `ControlRoot_Enum`
  - `Btn_Left` (`Button`)
  - `Btn_Right` (`Button`)
  - `ValueFrame/Txt_Value` (`TMP_Text`)

### 연결 방식 선택 기준

- 현재 구현 스크립트(`OptionRowBindingEntry`, `EnumOptionRowBinder`)는
  `Btn_Left/Btn_Right` 버튼으로 인덱스를 변경하고 `EnumValueText`를 갱신하는 방식입니다.
- `EnumDisplayLabels`를 설정하면 인덱스가 아니라 옵션 라벨 문자열이 표시됩니다.

설정 순서:
1. `ControlRoot_Enum` 하위에서 `Btn_Left`, `Btn_Right`, `Txt_Value`를 연결합니다.
2. Enum Row 루트에 `OptionRowBindingAnchor` 컴포넌트를 추가합니다.
3. `OptionRowBindingAnchor.BindingEntry` 설정:
   - `Binding Key`: 해당 옵션 키 (예: `DisplayScreenMode`)
   - `Widget Type`: `Enum`
   - `Enum Value Text`: `Txt_Value` 연결
   - `Enum Prev Button`: `Btn_Left` 연결
   - `Enum Next Button`: `Btn_Right` 연결
   - `Enum Display Labels`: 옵션 순서에 맞게 라벨 배열 입력
   - `Is Active`: On

## 4-2. Numeric Row (`OptionRowRoot_Numeric`)

- 사용 예: Gamma, Master/BGM/SFX, UI 크기, 흔들림 값
- 프리팹 구성 핵심:
  - `ControlRoot_Numeric`
  - `Btn_Left` / `Btn_Right` (`Button`)
  - `InputHitArea` (`RectTransform`, 클릭/드래그 입력 영역)
  - `ValueDisplayRoot/GaugeBackground/GaugeFrame/GaugeFill` (`Image`)
  - `Txt_Value` (`TMP_Text`)

### 연결 방식 선택 기준

- 현재 구현 스크립트(`OptionRowBindingEntry`, `NumericOptionRowBinder`)는
  `Btn_Left/Btn_Right` + `NumericPointerInputArea` + `GaugeFill`을 직접 사용합니다.
- `Image Type = Filled`면 `fillAmount`를 갱신하고,
  Filled가 아니면 `NumericFillMaxWidth` 기준으로 Width를 갱신합니다.
- `Btn_Left/Btn_Right`는 누르고 있는 동안 같은 동작이 반복되도록 홀드 반복 입력을 지원합니다.
- `NumericPointerInputArea`를 클릭/드래그하면 가로 위치 기준으로 값이 즉시 변경됩니다.

설정 순서:
1. `ControlRoot_Numeric` 하위에서 `Btn_Left`, `Btn_Right`, `InputHitArea`, `GaugeFill`, `Txt_Value`를 연결합니다.
2. Numeric Row 루트에 `OptionRowBindingAnchor` 컴포넌트를 추가합니다.
3. `OptionRowBindingAnchor.BindingEntry` 설정:
   - `Binding Key`: 해당 옵션 키 (예: `AudioMasterVolume`)
   - `Widget Type`: `Numeric`
   - `Numeric Fill Image`: `GaugeFill` 연결
   - `Numeric Pointer Input Area`: `InputHitArea` 연결(비우면 `Numeric Fill Image`를 클릭 영역으로 사용)
   - `Numeric Value Text`: `Txt_Value` 연결
   - `Numeric Decrease Button`: `Btn_Left` 연결
   - `Numeric Increase Button`: `Btn_Right` 연결
   - `Numeric Min/Max/Step`: 옵션 메타데이터 기준으로 입력
   - `Numeric Fill Max Width`: Filled 타입이 아닐 때 최대 폭 입력
   - `Is Active`: On

## 4-3. Action Row (`OptionRowRoot_Action`)

- 사용 예: 키 바인딩 열기, 고급 품질 설정 열기
- 필수 위젯: `Action 실행 위젯` (`OptionsPanelBindingView`의 `Action Widget` 슬롯에 연결할 대상)
- 주의: Action Row는 저장 데이터 필드 직접 매핑 대상이 아닙니다.
- 프리팹 구성 핵심:
  - `ControlRoot_Action`
  - Row 루트 `Button`
  - `Txt_Value` (`TMP_Text`)
  - `Img_Arrow` (`Image`)

설정 순서:
1. `ControlRoot_Action`의 실행용 `Button`을 찾습니다.
2. Action Row 루트에 `OptionRowBindingAnchor` 컴포넌트를 추가합니다.
3. `OptionRowBindingAnchor.BindingEntry` 설정:
   - `Binding Key`: 해당 Action 식별용 키 선택
   - `Widget Type`: `Action`
   - `Action Widget`: Button 연결
   - `Is Active`: On
4. Button `OnClick`에 실제 액션 핸들러(예: 별도 팝업 오픈 스크립트) 연결

---

## 5) 권장 매핑 예시

## Display
- `DisplayScreenMode` → Enum Row 바인딩 위젯
- `DisplayVSync` → Enum Row 바인딩 위젯
- `DisplayFrameLimit` → Enum Row 바인딩 위젯
- `DisplayGammaBrightness` → Numeric Row 바인딩 위젯
- `DisplayGraphicsPreset` → Enum Row 바인딩 위젯
- `DisplayGraphicsDetailMode` → Enum Row 바인딩 위젯

## Audio
- `AudioMasterVolume` → Numeric Row 바인딩 위젯
- `AudioBgmVolume` → Numeric Row 바인딩 위젯
- `AudioSfxVolume` → Numeric Row 바인딩 위젯

## Input
- `InputHoldBehavior` → Enum Row 바인딩 위젯
- (Action) 입력 설정 열기 → Action Row 실행 위젯

## Accessibility
- `AccessibilitySubtitleEnabled` → Enum Row 바인딩 위젯
- `AccessibilitySubtitleSize` → Enum Row 바인딩 위젯
- `AccessibilitySubtitleSpeed` → Numeric Row 바인딩 위젯
- `AccessibilityUiScale` → Numeric Row 바인딩 위젯
- `AccessibilityCameraShake` → Numeric Row 바인딩 위젯
- `AccessibilityScreenShake` → Numeric Row 바인딩 위젯
- `AccessibilityShakeReduction` → Numeric Row 바인딩 위젯
- `AccessibilityFlashReduction` → Enum Row 바인딩 위젯
- `AccessibilityColorBlindMode` → Enum Row 바인딩 위젯
- `AccessibilityHighContrastMode` → Enum Row 바인딩 위젯

## Gameplay
- `GameplayDifficulty` → Enum Row 바인딩 위젯
- `GameplayAutoSaveNotification` → Enum Row 바인딩 위젯

---

## 6) 동작 검증 체크리스트

1. 옵션 패널 오픈 시
   - 저장된 값이 UI에 반영되는지 확인
2. 값 변경 후 Apply 클릭 시
   - `TryBuildOptions` 성공
   - `OptionManager.SetAllOptions` 반영
   - `SaveAfterApply`가 On이면 저장 성공
3. 패널 재오픈 시
   - 변경값이 유지되어 재반영되는지 확인
4. 콘솔 확인
   - `OptionsPanelBindingView` 경고(키 중복/위젯 누락)가 없는지 확인

---

## 7) 자주 발생하는 설정 실수

- `Widget Type`을 Enum으로 두고 `Enum Value Text`/버튼 참조를 연결하지 않음
- `Widget Type`을 Numeric으로 두고 `GaugeFill`/버튼/범위값을 연결하지 않음
- 같은 `Binding Key`를 두 개 Row에 중복 연결
- Action Row를 데이터 저장용으로 오해하고 실제 액션 버튼 `OnClick`을 비워둠
- `TitleMenuOptionsPanelBridge.OptionsViewComponent`를 미연결

---

## 8) 최소 점검 루틴 (실무 권장)

- [ ] `OptionsPanelBindingView`의 `Row Entries` 개수와 실제 Row 개수가 일치
- [ ] 모든 활성 Row의 필수 위젯 참조가 null 아님
- [ ] 탭 전환 시 섹션 노출이 의도대로 동작
- [ ] Apply 후 패널 닫고 다시 열어 값 일관성 확인

---

## 9) Row Hover 설명 패널 연동

옵션 Row에 마우스를 올렸을 때 우측 Description 패널을 갱신하려면 아래처럼 구성합니다.

1. Description 패널 루트(예: `DescriptionPanelRoot`)에 `OptionsRowDescriptionPresenter`를 추가합니다.
2. `Description Title Text`에 제목 텍스트(`TMP_Text`)를 연결합니다.
3. `Description Body Text`에 본문 텍스트(`TMP_Text`)를 연결합니다.
4. `Description Default Text`에 기본값 안내 텍스트(`TMP_Text`)를 연결합니다.
5. `Anchors Root`에 옵션 Row들이 모여 있는 루트(예: `SectionContentRoot`)를 연결합니다.
6. 각 Row의 `OptionRowBindingAnchor.BindingEntry`에서 아래 값을 입력합니다.
   - `Description Title`
   - `Description Body`
   - `Description Default Text` (예: `기본값: 100%`)
   - `Description Hover Target` (비우면 Row 루트가 hover 대상)
7. 실행 시 Row hover 진입 시 Description 패널(제목/본문/기본값 안내)이 갱신되고, 이탈 후에도 마지막 Row 설명이 유지되는지 확인합니다.

### Description 저장 방식 제안

- 기본 권장(빠른 적용): `OptionRowBindingEntry` 내부 문자열(`Description Title`, `Description Body`, `Description Default Text`)에 직접 저장
  - 장점: 프리팹 단위에서 즉시 수정 가능
  - 단점: 다국어/공용 문구 재사용 시 중복 입력 발생
- 확장 권장(운영 단계): `BindingKey -> 로컬라이즈드 문자열 키` 매핑 테이블을 ScriptableObject로 분리
  - 장점: 다국어 전환/문구 일괄 수정이 쉬움
  - 단점: 초기 세팅 작업이 추가됨