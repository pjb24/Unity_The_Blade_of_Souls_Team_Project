# 1. 전체 구조 (Hierarchy)

```text
Canvas
 └── SafeAreaRoot (Stretch)
     └── SettingsRoot (Full Screen)
         ├── TopBar
         │   ├── Title (SETTINGS)
         │   └── TabGroup
         │       ├── Tab_Gameplay
         │       ├── Tab_Sound
         │       ├── Tab_Video
         │       ├── Tab_Language
         │       ├── Tab_Controls
         │       └── Tab_Accessibility
         │
         ├── ContentRoot
         │   ├── LeftPanel
         │   │   └── ScrollRect
         │   │       ├── Viewport
         │   │       │   └── Content
         │   │       │       ├── Section_General(Prefab)
         │   │       │       │   ├── SectionHeader
         │   │       │       │   │   └── Txt_Title (GENERAL)
         │   │       │       │   └── SectionContent
         │   │       │       │       ├── Option_AutoTarget(Prefab)
         │   │       │       │       └── Option_DatapadAnim(Prefab)
         │   │       │       │
         │   │       │       ├── Section_UI(Prefab)
         │   │       │       │   ├── SectionHeader
         │   │       │       │   │   └── Txt_Title (UI)
         │   │       │       │   └── SectionContent
         │   │       │       │       ├── Option_HUD(Prefab)
         │   │       │       │       ├── Option_EnemyTier(Prefab)
         │   │       │       │       └── Option_DamageNumber(Prefab)
         │   │       │       │
         │   │       │       └── Section_Controller(Prefab)
         │   │       │           ├── SectionHeader
         │   │       │           │   └── Txt_Title (DUALSENSE® WIRELESS CONTROLLER)
         │   │       │           └── SectionContent
         │   │       │               ├── Option_VibrationMode(Prefab)
         │   │       │               └── Option_VibrationIntensity(Prefab)
         │   │       │
         │   │       └── Scrollbar_Vertical (Optional)
         │   │
         │   └── RightPanel
         │       ├── Description_Title
         │       ├── Description_Text
         │       └── Description_Default
         │
         └── BottomBar
             ├── Btn_Default
             ├── Btn_DefaultAll
             └── Btn_Back
```

---

# 2. 루트 설정

## SafeAreaRoot

- Anchor: Stretch (0,0 ~ 1,1)
- Pivot: (0.5, 0.5)
- Pos: (0,0)
- Size: (0,0)

## SettingsRoot

- Anchor: Stretch
- Padding 느낌으로 margin 준다

```text
Left: 80
Right: 80
Top: 60
Bottom: 60
```

---

# 3. TopBar

## TopBar

- Anchor: Top Stretch
- Pivot: (0.5, 1)
- Height: 120
- PosY: 0

---

## Title (SETTINGS)

- Anchor: Top Center
- Pivot: (0.5, 1)
- Pos: (0, -10)
- Size: (400, 60)

---

## TabGroup

- Anchor: Top Center
- Pivot: (0.5, 1)
- Pos: (0, -70)
- Size: (1200, 50)

### 각 Tab

- Width: 180
- Height: 50
- Horizontal Layout Group 추천

---

# 4. ContentRoot

## ContentRoot

- Anchor: Stretch
- Top: 140
- Bottom: 100

---

# 5. LeftPanel (핵심 영역)

## LeftPanel

- Anchor: `Left Stretch`
- Pivot: `(0, 0.5)`

```text
Width: 900
Left: 0
Top: 0
Bottom: 0
```

## ScrollRect

- Parent: `LeftPanel`
- Anchor: `Stretch`
- Pivot: `(0.5, 0.5)`

```text
Left: 0
Right: 0
Top: 0
Bottom: 0
```

### ScrollRect 컴포넌트

* `RectTransform`
* `ScrollRect`

### ScrollRect 설정값

* Content: `Content`
* Horizontal: `false`
* Vertical: `true`
* Movement Type: `Clamped`
* Elasticity: `0.1`
* Inertia: `true`
* Deceleration Rate: `0.135`
* Scroll Sensitivity: `30`
* Viewport: `Viewport`
* Horizontal Scrollbar: `None`
* Vertical Scrollbar: `Scrollbar_Vertical` 또는 `None`

### ScrollRect 오브젝트에 추가로 넣어도 되는 컴포넌트

* `CanvasGroup`
  * 탭 전환 시 페이드나 인터랙션 제어 필요하면 추가
* `UIBehaviour` 계열 추가 스크립트
  * 예: 패드 포커스 보정, 선택 항목 자동 스크롤

## Viewport

- Parent: `ScrollRect`
- Anchor: `Stretch`
- Pivot: `(0.5, 0.5)`

```text
Left: 0
Right: 0
Top: 0
Bottom: 0
```

### 컴포넌트

- `Image`
- `Rect Mask 2D`

## Content

- Parent: `Viewport`
- Anchor: `Top Stretch`
- Pivot: `(0.5, 1)`

```text
Left: 0
Right: 0
Top: 0
Pos Y: 0
Height: 자동
```

### Content 컴포넌트

- `Vertical Layout Group`

```text
Spacing: 40
Padding: Left 20, Right 20, Top 20, Bottom 20
Child Alignment: Upper Left
Control Width: true
Control Height: true
Child Force Expand Width: false
Child Force Expand Height: false
```

* `Content Size Fitter`

```text
Horizontal Fit: Unconstrained
Vertical Fit: Preferred Size
```

## Scrollbar_Vertical (Optional)

- Parent: `ScrollRect`
- Anchor: `Right Stretch`
- Pivot: `(1, 0.5)`

```text
Right: 0
Top: 4
Bottom: 4
Width: 12
```

### Scrollbar_Vertical 컴포넌트

* `Image`
* `Scrollbar`

### Scrollbar 설정

* Direction: `Bottom To Top` 또는 `Top To Bottom`
* Handle Rect: `Handle`

---

# 6. Section

Section 프리팹은 06_옵션_UI_Section_프리팹.md를 보고 만들면 됨.

---

# 7. RightPanel (설명 영역)

- Anchor: Right Stretch
- Pivot: (1, 0.5)
- Width: 800

## Description_Title

- Anchor: Top Left
- Pivot: (0, 1)

```text
Pos: (20, -50)
Size: (600, 40)
```

---

## Description_Text

- Anchor: Top Left

```text
Pos: (20, -110)
Size: (700, 200)
```

---

## Description_Default

- Anchor: Top Left

```text
Pos: (20, -330)
Size: (400, 40)
```

---

# 8. BottomBar

## BottomBar

- Anchor: Bottom Stretch
- Pivot: (0.5, 0)

```text
Height: 80
Bottom: 0
```

---

## 버튼들 (우측 정렬)

### Btn_Back

- Anchor: Bottom Right
- Pivot: (1, 0)

```text
Pos: (-20, 10)
Size: (120, 50)
```

### Btn_DefaultAll

```text
Pos: (-160, 10)
Size: (180, 50)
```

### Btn_Default

```text
Pos: (-360, 10)
Size: (160, 50)
```

---

# 9. 핵심 포인트 (중요)

이 UI의 본질은 3가지다.

## 1. 좌/우 분리 구조

- Left = 옵션 리스트
- Right = 설명

---

## 2. Option Row 표준화

모든 옵션은 동일 구조로 간다.

```text
Label + Selector
```

Option Row 프리팹은 05_옵션_UI_Option_Row_프리팹.md를 보고 만들면 됨.

---
