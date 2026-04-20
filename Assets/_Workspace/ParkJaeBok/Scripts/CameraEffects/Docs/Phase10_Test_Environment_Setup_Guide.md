# CameraEffect Phase 10 — 테스트 환경 구축 가이드

> 범위 제한: **본 문서는 Phase 10(테스트 환경)만 다룹니다.**  
> 전제: Phase 9 문서(`Phase09_Sample_Preset_Authoring_Guide.md`) 기준으로 샘플 SO 프리셋(`CE_FadeIn` 등)이 이미 생성되어 있어야 합니다.

---

## 1) 목표 정의

Phase 10 목표는 "디자이너가 에디터에서 즉시 검증 가능한 테스트 씬"을 구축하는 것입니다.

필수 달성 조건:

1. **버튼 클릭으로 카메라 효과 실행**
2. **현재 적용(활성) 효과 상태 표시**
3. **디버그 UI 표시(TMP_Text + OnGUI 폴백)**
4. **Phase 9에서 만든 SO 프리셋과 연결된 실사용 테스트 루프 완성**

---

## 2) 설계

### 2-1. 테스트 씬 단위 설계

- 씬 이름: `CameraEffect_Phase10_Test`
- 씬 목적:
    - 카메라 효과 런타임 시스템 검증
    - 샘플 프리셋의 체감 튜닝
    - 접근성/루프/중지 동작 회귀 확인

### 2-2. 핵심 오브젝트 설계

1. `CameraEffectManager_Root`
    - `CameraEffectManager` 단일 인스턴스
2. `CameraEffectTestUI_Root`
    - `CameraEffectTestEnvironmentPresenter`
        - 샘플 프리셋 재생/중지 API 제공
        - UI Button 자동 바인딩(선택)
        - 현재 활성 효과 상태 문자열 생성
        - 디버그 표시(TMP_Text + OnGUI 폴백)
    - Canvas/버튼/TMP 텍스트 참조를 통한 테스트 진입점
3. `Main Camera`
    - 실제 효과 적용 대상 카메라

### 2-3. 데이터 단위

- `SamplePresetEntry`
    - 샘플 타입(enum)
    - 표시 이름
    - `CameraEffectPresetBase`
    - Play/Stop 버튼 참조
    - 마지막 재생 핸들 상태

### 2-4. 표시 정책

- `active=Yes/No`를 프리셋별로 출력
- `preset=Assigned/Missing` 표시로 누락 에셋 즉시 확인
- Manager 연결 상태(`Connected/Missing`)를 최상단에 표시

### 2-5. 데이터 흐름 설계

1. UI Button `OnClick` → `CameraEffectTestEnvironmentPresenter.PlayXXX()` 호출
2. Presenter → `CameraEffectManager.Play(preset, requester)` 요청
3. Manager → 런타임 프리셋 인스턴스 재생/갱신
4. Presenter가 핸들 유효성(`handle.IsValid`)으로 활성 상태를 문자열화
5. TMP_Text/OnGUI로 현재 상태 표시

---

## 3) 구현 (씬 생성/오브젝트 생성/컴포넌트 부착/값 설정)

> 아래는 **Unity Editor에서 그대로 따라 하면 되는 절차**입니다.

### 3-1. 씬 생성 및 이름 지정

1. Unity 상단 메뉴 `File > New Scene`.
2. 기본 템플릿(`Basic` 또는 프로젝트 표준 템플릿) 선택.
3. `Ctrl+S`(또는 `Cmd+S`)로 저장.
4. 저장 경로 예시: `Assets/_Workspace/ParkJaeBok/Scenes/`
5. 씬 이름을 **`CameraEffect_Phase10_Test`** 로 지정.

### 3-2. 카메라 효과 매니저 오브젝트 생성

1. Hierarchy에서 `Create Empty`.
2. 오브젝트 이름: **`CameraEffectManager_Root`**.
3. `Add Component` → `CameraEffectManager` 추가.
4. `CameraEffectManager` Inspector 설정:
    - `Target Camera`: 씬의 `Main Camera` 연결
    - `Dont Destroy On Load`: 테스트 씬 단독 검증이면 `false` 권장
    - `Enable Loop Safety Timeout`: `true` 권장
    - `Loop Safety Timeout Seconds`: `30`(기본) 또는 테스트용 단축값
    - `Verbose Logging`: 초기 세팅 단계에서는 `true` 권장

### 3-3. 테스트 UI 루트 오브젝트 생성

1. Hierarchy에서 `Create Empty`.
2. 오브젝트 이름: **`CameraEffectTestUI_Root`**.
3. `Add Component` → `CameraEffectTestEnvironmentPresenter` 추가.
4. Presenter Inspector 기본 설정:
    - `Effect Manager`: `CameraEffectManager_Root`의 `CameraEffectManager` 연결
    - `Verbose Logging`: `true` (초기 디버깅)
    - `Show On Gui Overlay`: `true` (TMP 미연결 대비 폴백 유지)

### 3-4. Canvas 및 디버그 텍스트 생성

1. Hierarchy에서 `UI > Canvas` 생성.
2. 이름: **`Canvas_CameraEffectTest`**.
3. Canvas 하위에 `UI > Text - TextMeshPro` 생성.
4. 이름: **`Txt_ActiveEffects`**.
5. `Canvas_CameraEffectTest` `RectTransform`/Canvas 권장값:
    - Render Mode: `Screen Space - Overlay`
    - Anchor Min/Max: `(0, 0)` / `(1, 1)` (풀스크린)
    - Pivot: `(0.5, 0.5)`
    - Left/Right/Top/Bottom: `0 / 0 / 0 / 0`
6. `Txt_ActiveEffects` `RectTransform` 권장값:
    - Anchor Min/Max: `(0, 1)` / `(0, 1)` (**Top-Left 고정**)
    - Pivot: `(0, 1)`
    - Anchored Position: `(20, -20)`
    - Size Delta: `(760, 420)`
7. TMP_Text 권장값:
    - Font Size: `22~28`
    - Alignment: Top Left
    - Overflow: Overflow
8. Presenter의 `Active Effects Text`에 `Txt_ActiveEffects` 연결.

### 3-5. 버튼 패널 생성

1. Canvas 하위에 `Create Empty`.
2. 이름: **`Panel_Buttons`**.
3. `Vertical Layout Group` 추가(선택이지만 권장).
4. `Panel_Buttons` `RectTransform` 권장값:
    - Anchor Min/Max: `(1, 1)` / `(1, 1)` (**Top-Right 고정**)
    - Pivot: `(1, 1)`
    - Anchored Position: `(-20, -20)`
    - Size Delta: `(420, 860)`
5. `Vertical Layout Group` 권장값:
    - Child Alignment: `Upper Left`
    - Spacing: `8`
    - Padding: Left/Right/Top/Bottom = `12 / 12 / 12 / 12`
    - Child Control Width: `true`
    - Child Control Height: `false`
    - Child Force Expand Width: `true`
    - Child Force Expand Height: `false`
6. `Content Size Fitter` 추가 권장:
    - Horizontal Fit: `Unconstrained`
    - Vertical Fit: `Preferred Size`
7. 각 버튼의 `RectTransform` 권장값:
    - Anchor Min/Max: `(0, 1)` / `(1, 1)` (패널 폭 자동 사용)
    - Pivot: `(0.5, 0.5)`
    - Size Delta: `(0, 52)` (높이 52 기준)
    - TMP 버튼 텍스트 Alignment: Middle Center
8. 아래 버튼 13개 생성(또는 필요한 만큼):

- Play 버튼(6개)
    
    - `Btn_Play_FadeIn`
    - `Btn_Play_FadeOut`
    - `Btn_Play_HitSmall`
    - `Btn_Play_HitHeavy`
    - `Btn_Play_DashBurst`
    - `Btn_Play_LowHealthWarning`
- Stop 버튼(6개)
    
    - `Btn_Stop_FadeIn`
    - `Btn_Stop_FadeOut`
    - `Btn_Stop_HitSmall`
    - `Btn_Stop_HitHeavy`
    - `Btn_Stop_DashBurst`
    - `Btn_Stop_LowHealthWarning`
- 전체 중지 버튼(1개)
    
    - `Btn_Stop_All`

### 3-6. 버튼 OnClick 수동 연결

각 버튼의 `Button.onClick`에 `CameraEffectTestUI_Root`를 추가하고 아래 메서드 연결:

- `Btn_Play_FadeIn` → `CameraEffectTestEnvironmentPresenter.PlayFadeIn`
- `Btn_Play_FadeOut` → `PlayFadeOut`
- `Btn_Play_HitSmall` → `PlayHitSmall`
- `Btn_Play_HitHeavy` → `PlayHitHeavy`
- `Btn_Play_DashBurst` → `PlayDashBurst`
- `Btn_Play_LowHealthWarning` → `PlayLowHealthWarning`
    
- `Btn_Stop_FadeIn` → `StopFadeIn`
- `Btn_Stop_FadeOut` → `StopFadeOut`
- `Btn_Stop_HitSmall` → `StopHitSmall`
- `Btn_Stop_HitHeavy` → `StopHitHeavy`
- `Btn_Stop_DashBurst` → `StopDashBurst`
- `Btn_Stop_LowHealthWarning` → `StopLowHealthWarning`
    
- `Btn_Stop_All` → `StopAllSamples`
    

> 참고: Presenter의 `Sample Entries`에 버튼 참조를 넣으면 자동 바인딩도 가능하지만,  
> 테스트 씬에서는 **OnClick 수동 연결 + Sample Entries 데이터 연결**을 동시에 맞춰두는 방식이 가장 명확합니다.

### 3-7. Sample Entries 상세 설정 (Phase 9 SO 연계)

Presenter의 `Sample Presets` 배열 Size를 `6`으로 설정하고 아래 표대로 입력합니다.

|Index|PresetType|DisplayName|Preset(SO)|PlayButton|StopButton|
|---|---|---|---|---|---|
|0|FadeIn|FadeIn|`CE_FadeIn`|`Btn_Play_FadeIn`|`Btn_Stop_FadeIn`|
|1|FadeOut|FadeOut|`CE_FadeOut`|`Btn_Play_FadeOut`|`Btn_Stop_FadeOut`|
|2|HitSmall|Hit_Small|`CE_Hit_Small`|`Btn_Play_HitSmall`|`Btn_Stop_HitSmall`|
|3|HitHeavy|Hit_Heavy|`CE_Hit_Heavy`|`Btn_Play_HitHeavy`|`Btn_Stop_HitHeavy`|
|4|DashBurst|Dash_Burst|`CE_Dash_Burst`|`Btn_Play_DashBurst`|`Btn_Stop_DashBurst`|
|5|LowHealthWarning|LowHealth_Warning|`CE_LowHealth_Warning`|`Btn_Play_LowHealthWarning`|`Btn_Stop_LowHealthWarning`|

### 3-8. Main Camera 및 렌더 확인

1. `Main Camera` 존재 확인(태그 `MainCamera`).
2. `CameraEffectManager.Target Camera`가 해당 카메라를 가리키는지 확인.
3. 색상 계열(Fade/Overlay) 체감 확인을 위해 카메라 Clear Flags/Background 색이 극단값인지 체크.

### 3-9. Zoom 시각 검증용 테스트 GameObject 생성

`Zoom` 효과는 배경 대비/원근감이 부족하면 체감이 약해 보일 수 있으므로, 아래 오브젝트를 테스트 씬에 함께 배치합니다.

1. Hierarchy에서 `Create Empty` 생성.
2. 이름: **`ZoomTest2D_SetRoot`**.
3. `ZoomTest2D_SetRoot` 하위에 아래 오브젝트를 생성:

- `Zoom2D_Background` (원거리 배경 기준)
    
    - `2D Object > Sprite` 생성 후 이름 변경
    - Position: `(0, 0, 0)`
    - Scale: `(12, 6, 1)`
    - Sprite: 반복 패턴/격자 패턴(배경 움직임 체감이 쉬운 텍스처 권장)
    - Sorting Layer: `Background`, Order in Layer: `0`
- `Zoom2D_Midground` (중간 레이어 기준)
    
    - `2D Object > Sprite`
    - Position: `(0, -0.3, 0)`
    - Scale: `(7, 3.5, 1)`
    - Sprite: 캐릭터 주변 오브젝트(바위/기둥 등)
    - Sorting Layer: `Environment`, Order in Layer: `10`
- `Zoom2D_ForegroundRef` (전경 기준점)
    
    - `2D Object > Sprite`
    - Position: `(0, -1.2, 0)`
    - Scale: `(2.8, 2.8, 1)`
    - Sprite: 플레이어 대체 실루엣 또는 선명한 마커
    - Sorting Layer: `Character`, Order in Layer: `20`
- `Zoom2D_EdgeMarkers` (화면 가장자리 기준점)
    
    - `Create Empty` 후 이름 지정
    - 자식으로 Sprite 4개 생성: `Marker_Top`, `Marker_Bottom`, `Marker_Left`, `Marker_Right`
    - 각 마커는 얇은 막대 Sprite 사용(가로/세로 라인)
    - 역할: Zoom 시 화면 가장자리 기준 상대 거리 변화를 시각적으로 확인
- `ZoomReference_UIFrame` (선택)
    
    - Canvas 하위에 `UI > Image`
    - 이름: `Img_ZoomFrame`
    - Anchor Min/Max: `(0, 0)` / `(1, 1)`
    - Pivot: `(0.5, 0.5)`
    - Left/Right/Top/Bottom: `12 / 12 / 12 / 12`
    - Color: 알파 0.2 이하 단색

4. Main Camera 권장 시작값(2D):
    - Position: `(0, 0, -10)`
    - Rotation: `(0, 0, 0)`
    - Projection: `Orthographic`
    - Orthographic Size: `5`
5. Dash_Burst Zoom 검증:
    - `Btn_Play_DashBurst`를 연속 클릭해 짧은 Zoom 버스트가 반복될 때 `Zoom2D_ForegroundRef`와 `Zoom2D_EdgeMarkers` 간 간격 변화가 즉시 보이는지 확인.
6. Hit_Heavy Zoom 검증:
    - `Btn_Play_HitHeavy`를 클릭해 Dash_Burst 대비 더 무거운 Zoom 체감(크기 변화량/감쇠 곡선)이 보이는지 비교.
7. Zoom 체감이 약하면 아래 순서로 조정:
    - (1) `CE_Dash_Burst` / `CE_Hit_Heavy`의 `Zoom Intensity` 소폭 증가
    - (2) `Zoom2D_ForegroundRef` 크기를 키워 전경 스케일 변화를 강조
    - (3) 배경 패턴의 밀도를 높여 화면 스케일 변화를 눈에 잘 띄게 조정

---

## 4) Inspector 세팅 방법 (체크리스트 중심)

### 4-1. CameraEffectManager 체크

- [ ]  `Target Camera` 연결됨
- [ ]  `Enable Loop Safety Timeout = true`
- [ ]  `Loop Safety Timeout Seconds` 값 유효(0 초과)
- [ ]  `Verbose Logging` 초기 검증 시 활성

### 4-2. CameraEffectTestEnvironmentPresenter 체크

- [ ]  `Effect Manager` 연결됨
- [ ]  `Sample Presets` Size = 6
- [ ]  6개 엔트리 모두 `Preset` 할당됨
- [ ]  `Active Effects Text` 연결됨
- [ ]  `Show On Gui Overlay` 폴백 정책에 맞게 설정됨

### 4-3. 버튼 체크

- [ ]  Play 6개, Stop 6개, StopAll 1개 OnClick 연결 완료
- [ ]  버튼 Label 텍스트가 프리셋명과 일치

---

## 5) 테스트 방법

### 5-1. 실행 시나리오 테스트

1. `FadeIn` / `FadeOut` 단일 재생
2. `Hit_Small` 연타 후 `Hit_Heavy` 재생 (우선순위/체감 비교)
3. `Dash_Burst` 재생 중 다른 채널 효과 동시 재생
    - 예시: `Dash_Burst(Movement)` + `LowHealth_Warning(Warning)` 동시 유지 확인
4. `LowHealth_Warning` 재생 후 Stop 버튼으로 종료
5. `StopAllSamples`로 전체 종료

### 5-2. 상태 표시 검증

TMP 텍스트(또는 OnGUI)에 아래가 보여야 함:

- `Manager: Connected`
- 각 항목별 `preset=Assigned`
- 재생 후 `played=Yes`
- 활성 중인 항목은 `active=Yes`

### 5-3. 로그 검증

아래는 의도된 방어 로그이며, 발생 시 즉시 원인 확인:

- Sample Entry 누락/중복
- Preset 미할당
- Manager 미연결
- 유효 핸들 없음 상태에서 Stop 시도

---

## 6) 리스크 및 방어 로직

1. **리스크: SO 애셋 미연결**
    
    - 증상: 버튼 클릭 시 효과 미재생
    - 방어: `Preset` null 검증 + Warning 로그 + 디버그 텍스트 `preset=Missing`
2. **리스크: 씬에서 Manager 누락**
    
    - 증상: 모든 Play/Stop 버튼 무반응
    - 방어: `ResolveManager()` 자동 연결 시도 + 실패 Warning 로그
3. **리스크: Loop 효과 종료 누락 (`LowHealth_Warning`)**
    
    - 증상: 경고 효과 잔류
    - 방어: 개별 Stop 버튼 + `StopAllSamples` + Manager Loop Safety Timeout
4. **리스크: UI 텍스트 누락으로 상태 확인 불가**
    
    - 증상: 디버그 상태 미표시
    - 방어: OnGUI 오버레이 폴백 활성화
5. **리스크: 버튼 연결 실수**
    
    - 증상: 잘못된 프리셋 실행/중지
    - 방어: 버튼 이름 규칙(`Btn_Play_*`, `Btn_Stop_*`) 고정 + OnClick 매핑 표 준수

---

## 부록 A) 권장 Hierarchy 예시

```text
CameraEffect_Phase10_Test
├─ Main Camera
├─ ZoomTest2D_SetRoot
│  ├─ Zoom2D_Background (Sprite)
│  ├─ Zoom2D_Midground (Sprite)
│  ├─ Zoom2D_ForegroundRef (Sprite)
│  └─ Zoom2D_EdgeMarkers (Marker_Top/Bottom/Left/Right)
├─ CameraEffectManager_Root (CameraEffectManager)
├─ CameraEffectTestUI_Root (CameraEffectTestEnvironmentPresenter)
└─ Canvas_CameraEffectTest
   ├─ Txt_ActiveEffects (TMP_Text)
   └─ Panel_Buttons
      ├─ Btn_Play_FadeIn
      ├─ Btn_Play_FadeOut
      ├─ Btn_Play_HitSmall
      ├─ Btn_Play_HitHeavy
      ├─ Btn_Play_DashBurst
      ├─ Btn_Play_LowHealthWarning
      ├─ Btn_Stop_FadeIn
      ├─ Btn_Stop_FadeOut
      ├─ Btn_Stop_HitSmall
      ├─ Btn_Stop_HitHeavy
      ├─ Btn_Stop_DashBurst
      ├─ Btn_Stop_LowHealthWarning
      └─ Btn_Stop_All
```

## 부록 B) Phase 9 연계 확인 포인트

- `CE_FadeIn`, `CE_FadeOut`, `CE_Hit_Small`, `CE_Hit_Heavy`, `CE_Dash_Burst`, `CE_LowHealth_Warning`가 실제 프로젝트에 존재해야 한다.
- 샘플 프리셋 수치는 `Phase9_SamplePreset_Authoring_Guide.md` 기준값을 우선 사용하고, 테스트 씬에서 체감 튜닝 후 역반영한다.