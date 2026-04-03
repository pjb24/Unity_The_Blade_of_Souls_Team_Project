# Section 프리팹 구조

```text
SectionRoot
 ├── SectionHeader
 │   └── Txt_Title
 └── SectionContent
```

---

# Section 프리팹 오브젝트별 설정

## SectionRoot

### 역할

- `Content` 아래에 들어가는 섹션 단위 루트  
- Header와 OptionRow 묶음 관리  

### RectTransform

`Content`의 child라서 절대좌표보다 Layout 기준이 중요하다.

```text
Anchor Min: (0, 1)
Anchor Max: (0, 1)
Pivot: (0.5, 0.5)

Left: 0
Right: 0
Pos X: 0
Pos Y: 0
Height: 자동
```

### VerticalLayoutGroup

```text
Spacing: 12
Child Alignment: Upper Left

Control Child Size:
- Width: true
- Height: true

Child Force Expand:
- Width: false
- Height: false
```

### ContentSizeFitter

```text
Horizontal Fit: Unconstrained
Vertical Fit: Preferred Size
```

---

## SectionHeader

### 역할

- 섹션 제목 표시  

### LayoutElement

```text
Min Height: 40
Preferred Height: 40
Flexible Width: 1
Flexible Height: 0
```

---

## Txt_Title

### 역할

- `GENERAL`, `VOLUME`, `DISPLAY` 같은 제목 표시  

### RectTransform

```text
Anchor Min: (0, 0.5)
Anchor Max: (0, 0.5)
Pivot: (0, 0.5)

Pos X: 0
Pos Y: 0
Width: 600
Height: 40
```

### TextMeshProUGUI

```text
Alignment: Middle Left
Raycast Target: Off
```

---

## SectionContent

### 역할

- OptionRow들이 실제로 들어가는 영역  

### RectTransform

```text
Anchor Min: (0, 1)
Anchor Max: (1, 1)
Pivot: (0.5, 1)

Left: 0
Right: 0
Top: 0
Pos Y: 0
Height: 자동
```

### VerticalLayoutGroup

```text
Spacing: 8
Padding: Left 0, Right 0, Top 5, Bottom 20
Child Alignment: Upper Left

Control Child Size:
- Width: true
- Height: true

Child Force Expand:
- Width: false
- Height: false
```

### ContentSizeFitter

```text
Horizontal Fit: Unconstrained
Vertical Fit: Preferred Size
```

---


