# 1. 공통 구조

```text
OptionRowRoot
├── Background
├── SelectionFrame
├── DisabledOverlay
└── LayoutRoot
    ├── LabelArea
    │   └── Txt_Label
    └── ControlArea
        └── ControlRoot_(Enum / Numeric / Action)
```

---

# 2. 공통 오브젝트별 컴포넌트

## 2.1 OptionRowRoot

### 역할

- `SectionContent` 아래에 들어가는 Row 루트
- Row 높이 제공
- 배경, 선택 강조 프레임, 비활성 오버레이, 내부 배치 루트 보관

### LayoutElement

```text
Min Height: 64
Preferred Height: 64
Flexible Width: 1
Flexible Height: 0
```

### 넣지 말 것

- `Horizontal Layout Group`
- `Vertical Layout Group`
- `Content Size Fitter`
* `Image`
- `Button`

---

## 2.2 Background

### 역할

- Row 전체의 기본 배경
* 어두운 바 형태

### RectTransform

```text
Parent: `OptionRowRoot`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: 0
Right: 0
Top: 0
Bottom: 0
```

### Image

- Type: `Sliced`
- Raycast Target: `Off`

---

## 2.3 SelectionFrame

### 역할

- 선택된 Row 외곽선 강조
- 패드/키보드 포커스 강조

### RectTransform

```text
Parent: `OptionRowRoot`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: -4
Right: -4
Top: -4
Bottom: -4
```

### Image

- Type: `Sliced`
- Raycast Target: `Off`

### 상태

- 기본 비활성
- 선택 상태에서만 활성

---

## 2.4 DisabledOverlay

### 역할

- 비활성 옵션 덮기

### RectTransform

```text
Parent: `OptionRowRoot`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: 0
Right: 0
Top: 0
Bottom: 0
```

### Image

- Alpha: `0.4 ~ 0.6`
- Raycast Target:
    - UI만으로 막을 거면 `On`
    - 코드에서 막을 거면 `Off`

---

## 2.5 LayoutRoot

### 역할

- `LabelArea`와 `ControlArea`를 가로 배치

### RectTransform

```text
Parent: `OptionRowRoot`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: 0
Right: 0
Top: 0
Bottom: 0
```

### HorizontalLayoutGroup

```text
Padding: Left 24, Right 24, Top 0, Bottom 0
Spacing: 16
Child Alignment: Middle Left

Control Child Size:
- Width: true
- Height: true

Child Force Expand:
- Width: false
- Height: false
```

### 넣지 말아야 할 것

* `Content Size Fitter`

---

## 2.6 LabelArea

- Parent: `LayoutRoot`

### 역할

- 옵션 이름 영역

### LayoutElement

```text
Min Width: 332
Min Height: 40
Preferred Width: 332
Preferred Height: 40
Flexible Width: 0
Flexible Height: 0
```

---

## 2.7 Txt_Label

### 역할

- 옵션 이름 표시

### RectTransform

```text
Parent: `LabelArea`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: 0
Right: 0
Top: 0
Bottom: 0
```

### TextMeshProUGUI

```text
Alignment: Middle Left
Raycast Target: Off
Overflow: Ellipsis 권장
```

---

## 2.8 ControlArea

- Parent: `LayoutRoot`

### 역할

- 우측 숫자 조작 UI 전체 영역

### LayoutElement

```text
Min Width: 464
Min Height: 40
Preferred Width: 464
Preferred Height: 40
Flexible Width: 0
Flexible Height: 0
```

---

# 3. Enum 타입

## 3.1 구조

```text
ControlArea
 └── ControlRoot_Enum
     ├── Btn_Left
     │   └── Img_Arrow
     ├── ValueFrame
     │   └── Txt_Value
     └── Btn_Right
         └── Img_Arrow
```

---

## 3.2 ControlRoot_Enum

### 역할

- Enum 선택형 조작부

### RectTransform

```text
Parent: `ControlArea`
Anchor Min: (1, 0.5)
Anchor Max: (1, 0.5)
Pivot: (1, 0.5)

Pos X: 0
Pos Y: 0
Width: 320
Height: 40
```

### HorizontalLayoutGroup

```text
Padding: Left 0, Right 0, Top 0, Bottom 0
Spacing: 8
Child Alignment: Middle Center

Control Child Size:
- Width: true
- Height: true

Child Force Expand:
- Width: false
- Height: false
```

---

## 3.3 Btn_Left / Btn_Right

Parent: `ControlRoot_Enum`

### 역할

- 이전/다음 Enum 값 선택

### 필요한 컴포넌트

- `Image`
- `Button`

### LayoutElement

```text
Min Width: 40
Min Height: 40
Preferred Width: 40
Preferred Height: 40
Flexible Width: 0
Flexible Height: 0
```

### Button

- Transition: `Color Tint` 또는 `Sprite Swap`

---

## 3.4 Img_Arrow

### 필요한 컴포넌트

- `Image`

### RectTransform

```text
Parent: `Btn_Left/Btn_Right`
Anchor Min: (0.5, 0.5)
Anchor Max: (0.5, 0.5)
Pivot: (0.5, 0.5)

Width: 16
Height: 16
Pos X: 0
Pos Y: 0
```

---

## 3.5 ValueFrame

Parent: `ControlRoot_Enum`

### 역할

- 현재 값 표시 프레임

### 필요한 컴포넌트

- `Image`

### LayoutElement

```text
Min Width: 160
Min Height: 36
Preferred Width: 224
Preferred Height: 36
Flexible Width: 0
Flexible Height: 0
```

---

## 3.6 Txt_Value

Parent: `ValueFrame`

### 역할

- 현재 Enum 값 표시

### RectTransform

```text
Parent: `ValueFrame`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: 0
Right: 0
Top: 0
Bottom: 0
```

### TextMeshProUGUI

```text
Alignment: Middle Center
Raycast Target: Off
```

### 값 예시

- `Windowed`
- `Fullscreen`
- `Borderless`

- `On`
- `Off`

- `High`
- `Low`

---

# 4. Numeric 타입

## 4.1 구조

```text
ControlArea
 └── ControlRoot_Numeric
     ├── Btn_Left
     │   └── Img_Arrow
     ├── GaugeFrame
     │   ├── GaugeBackground
     │   ├── GaugeFill
     │   ├── ValueDisplayRoot
     │   │   ├── Txt_Value
     │   │   └── Input_Value
     │   │       ├── Text Area
     │   │       │   ├── Text
     │   │       │   └── Placeholder
     │   │       └── Caret
     │   └── InputHitArea
     └── Btn_Right
         └── Img_Arrow
```

---

## 4.2 ControlRoot_Numeric

### 역할

- Int / Float / Slider 공통 숫자 옵션 조작부

### RectTransform

```text
Parent: `ControlArea`
Anchor Min: (1, 0.5)
Anchor Max: (1, 0.5)
Pivot: (1, 0.5)

Pos X: 0
Pos Y: 0
Width: 384
Height: 40
```

### HorizontalLayoutGroup

```text
Padding: Left 0, Right 0, Top 0, Bottom 0
Spacing: 8
Child Alignment: Middle Center

Control Child Size:
- Width: true
- Height: true

Child Force Expand:
- Width: false
- Height: false
```

---

## 4.3 Btn_Left / Btn_Right

- Parent: `ControlRoot_Numeric`

### 역할

- 값 감소 / 증가

### 필요한 컴포넌트

- `Image`
- `Button`

### LayoutElement

```text
Min Width: 32
Min Height: 32
Preferred Width: 32
Preferred Height: 32
Flexible Width: 0
Flexible Height: 0
```

### Image

* 버튼 루트 자체에 넣어도 되고, `Img_ButtonBg`로 분리해도 됨
* Raycast Target: `On`

---

## 4.4 Img_Arrow

### RectTransform

```text
Parent: `Btn_Left/Btn_Right`
Anchor Min: (0.5, 0.5)
Anchor Max: (0.5, 0.5)
Pivot: (0.5, 0.5)

Width: 16
Height: 16
Pos X: 0
Pos Y: 0
```

### Image

* Raycast Target: `Off`

---

## 4.5 GaugeFrame

### 역할

- 배경 바 + Fill + 값 표시 + 직접 입력 UI 컨테이너

### LayoutElement

```text
Min Width: 304
Min Height: 36
Preferred Width: 304
Preferred Height: 36
Flexible Width: 0
Flexible Height: 0
```

---

## 4.6 GaugeBackground

### 역할

* 숫자 옵션 바의 어두운 배경

### RectTransform

```text
Parent: `GaugeFrame`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: 0
Right: 0
Top: 0
Bottom: 0
```

### Image

- Type: `Sliced`
- Raycast Target: `Off`

---

## 4.7 GaugeFill

### 역할

- 값 비율에 따라 길이 변화

### RectTransform

```text
Parent: `GaugeFrame`
Anchor Min: (0, 0.5)
Anchor Max: (0, 0.5)
Pivot: (0, 0.5)

Pos X: 0
Pos Y: 0
Height: 36
Width: 코드로 값을 변경함
```

### Image

- Type: `Sliced`
- Raycast Target: `Off`

---

## 4.8 ValueDisplayRoot

### 역할

- 숫자 표시와 입력 필드를 겹쳐 두는 컨테이너

### RectTransform

```text
Parent: `GaugeFrame`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: 0
Right: 0
Top: 0
Bottom: 0
```

### 넣지 말아야 할 것

* `Layout Group`
* `Image`

---

## 4.9 Txt_Value

### 역할

- 기본 숫자 표시

### RectTransform

```text
Parent: `ValueDisplayRoot`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: 0
Right: 0
Top: 0
Bottom: 0
```

### TextMeshProUGUI

```text
Alignment: Middle Center
Raycast Target: Off
```

---

## 4.10 Input_Value

### 역할

- 직접 입력용 숫자 입력 필드

### RectTransform

```text
Parent: `ValueDisplayRoot`
Anchor Min: (0.5, 0.5)
Anchor Max: (0.5, 0.5)
Pivot: (0.5, 0.5)

Width: 96
Height: 28
Pos X: 0
Pos Y: 0
```

### Image

* 입력 박스 배경
- Type: `Sliced`

### TMP_InputField

- Text Viewport: `Text Area`
- Text Component: `Text`
* Content Type:
	- Int: `Integer Number`
	- Float: `Decimal Number`
- Line Type: `Single Line`
- Placeholder: `Placeholder`

* Character Validation: 필요 시 숫자 계열

### 상태

* 기본 비활성 또는 알파 0
* 입력 상태에서만 표시

---

## 4.11 Text Area

### 역할

- TMP_InputField 내부 텍스트 뷰포트

### RectTransform

```text
Parent: `Input_Value`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: 6
Right: 6
Top: 2
Bottom: 2
```

### RectMask2D

* 텍스트가 삐져나오지 않게 잘라줌

---

## 4.12 Text

### 역할

- 실제 입력 텍스트

### RectTransform

```text
Parent: `Text Area`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: 0
Right: 0
Top: 0
Bottom: 0
```

### TextMeshProUGUI

```text
Alignment: Middle Center
Raycast Target: Off
```

---

## 4.13 Placeholder

### 역할

- 입력 전 플레이스홀더

### RectTransform

```text
Parent: `Text Area`
Anchor Min: (0, 0)
Anchor Max: (1, 1)
Pivot: (0.5, 0.5)

Left: 0
Right: 0
Top: 0
Bottom: 0
```

### TextMeshProUGUI

```text
Alignment: Middle Center
Raycast Target: Off
```

---

## 4.14 Caret

### 역할

- 입력 caret 표시

### 필요한 컴포넌트

선택 1: TMP_InputField 기본 caret 사용  
선택 2: 별도 오브젝트

### 별도 오브젝트로 둘 경우

#### RectTransform

적절한 얇은 세로선 크기

#### CanvasRenderer

#### Image

* Width 약 `1~2`

---

## 4.15 InputHitArea

### 역할

- 중앙 숫자 클릭 판정 보조

### RectTransform

```text
Parent: `GaugeFrame`
Anchor Min: (0.5, 0.5)
Anchor Max: (0.5, 0.5)
Pivot: (0.5, 0.5)

Width: 120
Height: 32
Pos X: 0
Pos Y: 0
```

### Image

- Alpha: `0`
- Raycast Target: `On`

### Button

- Transition: `None`

---

# 5. Action 타입

## 5.1 구조

```text
ControlArea
 └── ControlRoot_Action
     ├── Txt_Value
     └── Img_Arrow
```

---

## 5.2 Action Row에서만 추가되는 사항

### OptionRowRoot

`Action` 타입일 때만 `OptionRowRoot`에 아래 컴포넌트를 추가한다.

- `Button`

### 역할

- Row 전체 클릭으로 서브 페이지 / 팝업 / 상세 설정 화면 진입

### Button 설정

- Transition: `Color Tint` 또는 `Sprite Swap`
- Navigation: `Automatic` 또는 `Explicit`
- Target Graphic: `Background`

---

## 5.3 ControlRoot_Action

### 역할

- 별도 설정 화면이나 팝업으로 이동하는 진입 표시부
- 값 조작이 아니라 이동 가능 상태를 보여준다

### RectTransform

```text
Parent: `ControlArea`
Anchor Min: (1, 0.5)
Anchor Max: (1, 0.5)
Pivot: (1, 0.5)

Pos X: 0
Pos Y: 0
Width: 320
Height: 40
```

### HorizontalLayoutGroup

```text
Padding: Left 0, Right 0, Top 0, Bottom 0
Spacing: 12
Child Alignment: Middle Right

Control Child Size:
- Width: false
- Height: true

Child Force Expand:
- Width: false
- Height: false
```

---

## 5.4 Txt_Value

### 역할

- 진입 대상 보조 텍스트 표시
- 없으면 비워둘 수 있다

### RectTransform

```text
Parent: `ControlRoot_Action`
Anchor Min: (0, 0.5)
Anchor Max: (0, 0.5)
Pivot: (0, 0.5)

Width: 260
Height: 36
Pos X: 0
Pos Y: 0
```

### LayoutElement

```text
Min Width: 120
Preferred Width: 260
Preferred Height: 36
Flexible Width: 0
Flexible Height: 0
```

### TextMeshProUGUI

```text
Alignment: Middle Right
Raycast Target: Off
Overflow: Ellipsis 권장
```

### 값 예시

- `Customize`
- `Edit`
- `Open`
- `Keyboard / Mouse`
- `Gamepad`

---

## 5.5 Img_Arrow

### 역할

- 우측 이동 / 진입 가능 표시

### RectTransform

```text
Parent: `ControlRoot_Action`
Anchor Min: (1, 0.5)
Anchor Max: (1, 0.5)
Pivot: (1, 0.5)

Width: 16
Height: 16
Pos X: 0
Pos Y: 0
```

### LayoutElement

```text
Min Width: 16
Min Height: 16
Preferred Width: 16
Preferred Height: 16
Flexible Width: 0
Flexible Height: 0
```

### Image

- Raycast Target: `Off`

---

# 5. 타입별 책임 정리

|타입|값 변경 방식|Button 위치|
|---|---|---|
|Enum|좌/우 버튼으로 값 순환|`Btn_Left`, `Btn_Right`|
|Numeric|좌/우 버튼 + 중앙 입력/게이지|`Btn_Left`, `Btn_Right`, `InputHitArea`|
|Action|Row 전체 클릭으로 진입|`OptionRowRoot`|

---

# 6. 오브젝트별 컴포넌트 요약표

## 공통

|오브젝트|필요한 컴포넌트|
|---|---|
|OptionRowRoot|RectTransform, LayoutElement|
|Background|RectTransform, Image|
|SelectionFrame|RectTransform, Image|
|DisabledOverlay|RectTransform, Image|
|LayoutRoot|RectTransform, HorizontalLayoutGroup|
|LabelArea|RectTransform, LayoutElement|
|Txt_Label|RectTransform, TextMeshProUGUI|
|ControlArea|RectTransform, LayoutElement|

---

## Enum

| 오브젝트                   | 필요한 컴포넌트                                    |
| ---------------------- | ------------------------------------------- |
| ControlRoot_Enum       | RectTransform, HorizontalLayoutGroup        |
| Btn_Left               | RectTransform, LayoutElement, Image, Button |
| Btn_Left/Img_Arrow     | RectTransform, Image                        |
| ValueFrame             | RectTransform, LayoutElement, Image         |
| Txt_Value              | RectTransform, TextMeshProUGUI              |
| Btn_Right              | RectTransform, LayoutElement, Image, Button |
| Btn_Right/Img_Arrow    | RectTransform, Image                        |

---

## Numeric

| 오브젝트                    | 필요한 컴포넌트                                    |
| ----------------------- | ------------------------------------------- |
| ControlRoot_Numeric     | RectTransform, HorizontalLayoutGroup        |
| Btn_Left                | RectTransform, LayoutElement, Image, Button |
| Btn_Left/Img_Arrow      | RectTransform, Image                        |
| GaugeFrame              | RectTransform, LayoutElement                |
| GaugeBackground         | RectTransform, Image                        |
| GaugeFill               | RectTransform, Image                        |
| ValueDisplayRoot        | RectTransform                               |
| Txt_Value               | RectTransform, TextMeshProUGUI              |
| Input_Value             | RectTransform, Image, TMP_InputField        |
| Input_Value/Text Area   | RectTransform, RectMask2D                   |
| Input_Value/Text        | RectTransform, TextMeshProUGUI              |
| Input_Value/Placeholder | RectTransform, TextMeshProUGUI              |
| Caret                   | RectTransform, CanvasRenderer, Image(선택)    |
| InputHitArea            | RectTransform, Image, Button                |
| Btn_Right               | RectTransform, LayoutElement, Image, Button |
| Btn_Right/Img_Arrow     | RectTransform, Image                        |

---

## Action

| 오브젝트               | 필요한 컴포넌트                                      |
| ------------------ | --------------------------------------------- |
| OptionRowRoot      | RectTransform, LayoutElement, Button          |
| ControlRoot_Action | RectTransform, HorizontalLayoutGroup          |
| Txt_Value          | RectTransform, LayoutElement, TextMeshProUGUI |
| Img_Arrow          | RectTransform, LayoutElement, Image           |

---

# 7. 실무 권장 추가 항목

## 선택적으로 추가할 수 있는 컴포넌트

### CanvasGroup

붙여도 괜찮은 위치:

- `OptionRowRoot`
- `DisabledOverlay`
- `Input_Value`
- `ControlRoot_Action`

용도:

- 알파 제어
- Interactable on/off
- Block Raycasts 제어

### Animator

붙여도 괜찮은 위치:

- `SelectionFrame`
- `GaugeFill`
- `Btn_Left`, `Btn_Right`
- `Background`
- `Img_Arrow`

용도:

- 선택 강조
- 눌림 반응
- Fill 변화 연출
- 진입 가능 강조

---
