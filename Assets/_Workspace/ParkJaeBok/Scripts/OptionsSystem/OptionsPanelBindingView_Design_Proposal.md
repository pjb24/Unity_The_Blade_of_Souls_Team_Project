# IOptionsPanelBindingView 구현 설계 제안

## 1) 재사용 가능한 기존 시스템 체크리스트 (재사용 우선순위)

### 위젯 타입 기준 문서 근거
- `Assets/_Workspace/ParkJaeBok/Prefabs/OptionsUI/Docs/07_옵션_UI_배치.md`
  - Option Row 타입을 `Enum / Numeric / Action`으로 명시.
- `Assets/_Workspace/ParkJaeBok/Prefabs/OptionsUI/Docs/06_옵션_UI_Option_Row_프리팹.md`
  - `ControlRoot_(Enum / Numeric / Action)` 구조 명시.

### P0 (반드시 재사용)
- `OptionManager`
  - 옵션 런타임 단일 진입점, 저장/로드 호출, 변경 리스너 제공.
- `OptionDefaultProfile`
  - 기본값 + Numeric 메타데이터(기본/최소/최대/스텝) 제공.
- `OptionDataValidator`
  - 저장 로드 데이터의 유효성/복구 정책 제공.
- `TitleMenuOptionsPanelBridge`
  - 패널 오픈 전 로드/반영 + Apply 시 SetAll + Save 연결.
- `IOptionsPanelBindingView`
  - View 계약(ApplyOptionsToView/TryBuildOptions).

### P1 (가능하면 재사용)
- `OptionsTabSectionController`
  - 탭-섹션 활성화/버튼 상태 토글 재사용.
- SaveSystem (`SaveCoordinator`, `ISaveParticipant`)
  - 옵션 저장 타이밍은 기존 Save 채널 정책을 따름.

### P2 (필요 시 확장)
- 옵션별 실제 게임 반영 어댑터(예: AudioMixer, QualitySettings 브리지)
  - `OptionManager` 리스너에 구독하여 분리 구성.

---

## 2) 설계 대안 A/B 비교

| 항목 | 대안 A: Presenter 중심 (권장) | 대안 B: ViewModel/양방향 Binding 중심 |
|---|---|---|
| 핵심 아이디어 | 패널 단위 `OptionsPanelPresenter`가 View 행(Row) 바인더를 제어 | ViewModel이 각 위젯과 양방향 동기화 |
| 기존 시스템 재사용 | 매우 높음 (`OptionManager`, `TitleMenuOptionsPanelBridge` 유지) | 중간 (브리지/매니저 외 별도 바인딩 인프라 필요) |
| 유지보수성 | 높음 (옵션 행 추가 시 Row Binder 추가) | 높음~매우 높음 (대규모 UI엔 유리) |
| 디자이너 협업성 | 매우 높음 (Inspector에서 Row 리스트 연결) | 중간 (ViewModel 개념/규약 학습 필요) |
| 테스트 용이성 | 높음 (Presenter/Mapper 단위 테스트) | 높음 (ViewModel 단위 테스트) |
| 구현 비용 | 낮음~중간 | 중간~높음 |
| 리스크 | Presenter가 비대해질 수 있음 | 과한 추상화/러닝커브 |

**선택 결론:** 현재 프로젝트 상태(기존 Bridge + Interface + Save 연동)에 가장 현실적인 것은 **대안 A(Presenter 중심)**.

---

## 3) 최종 설계안 (권장): Presenter + Row Binder 조합

### 3-1. 텍스트 클래스 다이어그램

- `TitleMenuOptionsPanelBridge` (기존)
  - `IOptionsPanelBindingView`에만 의존.
- `OptionsPanelBindingView : MonoBehaviour, IOptionsPanelBindingView` (신규)
  - 내부에 `OptionsPanelPresenter` 보유.
  - Inspector로 `List<OptionRowBindingEntry>` 보유.
- `OptionsPanelPresenter` (신규, 순수 C#)
  - `IReadOnlyList<IOptionRowBinder>`에 의존.
  - `Apply(OptionSaveData)` / `TryBuild(out OptionSaveData)` 담당.
- `IOptionRowBinder` (신규)
  - `E_OptionBindingKey BindingKey { get; }`
  - `void Apply(in OptionSaveData data)`
  - `bool TryRead(ref OptionSaveData data, out string error)`
- `EnumOptionRowBinder`, `NumericOptionRowBinder`, `ActionOptionRowBinder` (신규)
  - Unity UI 위젯 참조 + Getter/Setter delegate로 데이터 경로 매핑.
- `OptionRowBindingEntry` (신규, [Serializable])
  - 옵션 키, 타입, 위젯 참조, Label/Text, 기본값/범위 표시용 메타 참조.
- `IOptionsValidationService` (선택 신규)
  - `ValidateBeforeApply(OptionSaveData)`로 UI 입력 단 검증(저장 전).

### 3-2. 책임 분리

- View(`OptionsPanelBindingView`):
  - 위젯 연결/입력 전달/표시만 담당.
- Presenter(`OptionsPanelPresenter`):
  - 바인더 순회, 조합, 오류 집계 담당.
- Binder(`IOptionRowBinder` 구현체):
  - 개별 옵션 타입의 UI↔데이터 변환 담당.
- Domain/Runtime(`OptionManager`, `OptionDataValidator`):
  - 런타임 상태, 저장/로드, 복구 정책 담당.

### 3-3. 데이터 흐름

1. 패널 오픈
   - `TitleMenuOptionsPanelBridge.HandleBeforeOpen` → `LoadCurrentOptions` → `ApplyCurrentRuntimeOptionsToView`.
2. View 반영
   - `IOptionsPanelBindingView.ApplyOptionsToView` → Presenter.Apply → 각 RowBinder.Apply.
3. Apply 클릭
   - `TryBuildOptions` → Presenter.TryBuild(실패 시 에러 반환) → `OptionManager.SetAllOptions`.
4. 저장
   - Bridge 정책에 따라 `SaveCurrentOptions` 실행.
5. 게임 반영
   - 별도 리스너(오디오/그래픽 등)가 `OptionManager.AddListener`로 변경 적용.

### 3-4. 확장 포인트

- 신규 옵션 키 추가: `E_OptionBindingKey` enum + 바인더 매핑 규칙 추가.
- 신규 위젯 타입 추가: `IOptionRowBinder` 구현체 1개 추가.
- 저장 정책 확장: Bridge의 Save 플래그/트리거 문맥 확장.

---

## 4) Inspector 노출 전략 (디자이너 협업)

### 4-1. 기본 원칙
- 위젯 참조는 모두 `[SerializeField] private` 유지.
- 모든 직렬화 필드에 `[Tooltip]` + 변수 역할 주석 작성.
- `OptionRowBindingEntry`로 관련 필드를 묶어 실수 연결 최소화.

### 4-2. 권장 직렬화 구조
- `List<OptionRowBindingEntry> _rowEntries`
  - `E_OptionBindingKey key`
  - `E_OptionBindingWidgetType widgetType` (Enum/Numeric/Action)
  - `Dropdown/Slider/Button` 중 해당 참조
  - `TextMeshProUGUI valueLabel` (선택)
  - `bool applyOnValueChanged` (실시간 적용 여부)

### 4-3. Validation 전략
- `OnValidate()`:
  - 키 중복 감지.
  - `widgetType` 대비 위젯 null 체크.
  - Numeric Row는 Slider min/max 역전 체크.
- `Awake()`:
  - 런타임에서도 필수 참조 누락 경고 + 안전 비활성화.

---

## 5) 옵션 타입 추가 가이드 (실무 절차)

### 케이스 A: 기존 타입(Enum/Numeric/Action)으로 새 옵션 추가

변경 파일(최소):
1. `OptionTypes.cs` (필드/enum)
2. `OptionDefaultProfile.cs` (기본값/메타)
3. `OptionDataValidator.cs` (검증/복구)
4. `OptionsPanelBindingView` 관련 파일 (Row Entry 추가 또는 매핑 1줄)

절차:
1) 데이터 모델 필드 추가.
2) 기본 프로필에 기본값/범위 추가.
3) Validator에 누락/범위/enum 복구 규칙 추가.
4) View에서 해당 `E_OptionBindingKey` 행 추가(Inspector 연결).
5) 패널 오픈/Apply 수동 테스트.

### 케이스 B: 신규 UI 위젯 타입 자체 추가

추가 파일:
- `IOptionRowBinder` 구현체 1개 (예: `InputRebindRowBinder`).

수정 파일:
- `E_OptionBindingWidgetType` enum
- Binder Factory/Builder 1곳
- (필요 시) `OptionRowBindingEntry` 신규 참조 필드

---

## 6) 리스크 및 방어 전략

### 6-1. 이벤트 루프/무한 갱신
- `ApplyOptionsToView` 중에는 `_isApplyingSnapshot` 가드 true.
- 위젯 `onValueChanged` 핸들러는 가드 true면 즉시 return.
- 값 비교(`Mathf.Approximately`, enum 동일성) 후 변경 시에만 UI 반영.

### 6-2. Null/미연결 참조
- `OnValidate` + `Awake` 이중 방어.
- 필수 참조 누락 행은 `IsActive=false`로 제외하고 경고 로그.

### 6-3. 저장 타이밍
- 기본: Apply 버튼 시점 저장(기존 Bridge 정책 재사용).
- 선택: `applyOnValueChanged=true` 옵션은 실시간 반영하되, 저장은 디바운스/지연 저장으로 분리.

### 6-4. 동기화 충돌
- 오픈 시 항상 런타임 스냅샷을 UI에 재주입(Bridge 옵션 유지).
- 외부 시스템이 옵션 변경한 경우, 패널 활성 상태에서만 조건부 재반영 정책 사용.

### 6-5. Presenter 비대화 리스크(구체)
- **조건 분기 폭증 리스크**
  - 옵션 종류가 늘수록 `if/switch`가 Presenter에 몰려, 특정 탭 수정이 다른 탭 회귀를 유발할 수 있음.
- **테스트 범위 팽창 리스크**
  - Presenter 1개가 다수 옵션/위젯 규칙을 모두 알면 단위 테스트 케이스가 급증하고, 실패 원인 추적이 어려워짐.
- **디자이너 작업 병목 리스크**
  - Row 단위가 아닌 Presenter 코드 수정이 필수가 되면 Inspector만으로 해결 가능한 작업도 개발자 의존으로 바뀜.
- **런타임 부작용 리스크**
  - Apply/Notify/Save 타이밍 제어가 Presenter 내부에서 뒤엉키면 이벤트 루프(재진입)나 중복 저장 호출이 발생하기 쉬움.

#### 비대화 방어 기준
1) Presenter는 **RowBinder 순회/조합**만 담당하고, 옵션별 변환 로직은 Binder로 이동한다.  
2) Presenter public API는 `Apply`, `TryBuild` 정도로 최소화한다.  
3) 신규 옵션 추가 시 Presenter 수정이 아닌 **BindingEntry + Binder 매핑 추가**로 끝나는지 PR 체크리스트에 포함한다.  
4) 탭 단위로 Binder 리스트를 분리해(예: Display/Audio) 테스트 픽스처를 작게 유지한다.

---

## 7) 핵심 코드 스켈레톤 (요약)

```csharp
public interface IOptionRowBinder
{
    E_OptionBindingKey BindingKey { get; }
    void Apply(in OptionSaveData data);
    bool TryRead(ref OptionSaveData data, out string error);
}

public sealed class OptionsPanelPresenter
{
    private readonly IReadOnlyList<IOptionRowBinder> _rowBinders;

    public OptionsPanelPresenter(IReadOnlyList<IOptionRowBinder> rowBinders)
    {
        _rowBinders = rowBinders;
    }

    public void Apply(in OptionSaveData data)
    {
        for (int i = 0; i < _rowBinders.Count; i++)
        {
            _rowBinders[i].Apply(data);
        }
    }

    public bool TryBuild(OptionSaveData seed, out OptionSaveData builtData, out string error)
    {
        builtData = seed;
        error = string.Empty;

        for (int i = 0; i < _rowBinders.Count; i++)
        {
            if (_rowBinders[i].TryRead(ref builtData, out string rowError) == false)
            {
                error = rowError;
                return false;
            }
        }

        return true;
    }
}
```

> 실제 Unity 컴포넌트 구현 시에는 각 함수/변수에 프로젝트 규칙대로 주석/Tooltip을 동일하게 부여합니다.
