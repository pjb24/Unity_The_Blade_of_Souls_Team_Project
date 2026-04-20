# CameraEffect Phase 9 — 샘플 프리셋 제작 문서

> 범위 제한: **본 문서는 Phase 9(샘플 프리셋 제작 문서화)만 다룹니다.**  
> 목적: 디자이너가 `CameraEffectPreset` ScriptableObject를 복제/튜닝해서 아래 6개 프리셋을 즉시 제작/검증할 수 있도록 합니다.

---

## 1) 목표 정의

Phase 9의 목표는 다음 6개 샘플 프리셋을 **코드 수정 없이 데이터만으로 구성**할 수 있게 하는 것입니다.

- `FadeIn`
- `FadeOut`
- `Hit_Small`
- `Hit_Heavy`
- `Dash_Burst`
- `LowHealth_Warning`

완성 기준:

1. 모든 프리셋은 `CameraEffectPreset` 1개 에셋으로 재생 가능해야 함.
2. 게임 로직은 `CameraEffectManager.Play(...)`로 요청만 보냄.
3. 채널/우선순위/접근성은 기존 시스템 설정을 그대로 따름.
4. 값이 잘못된 경우 시스템 Warning 로그로 원인 파악 가능해야 함.

---

## 2) 설계

### 2-1. 공통 설계 원칙

- 프리셋 단위로 “효과 의도”를 캡슐화하고, 모듈(Fade/Shake/Zoom/Overlay) 조합으로 결과를 만든다.
- 지속형 경고(`LowHealth_Warning`)를 제외한 나머지 샘플은 `Timed`로 설계한다.
- 채널은 아래처럼 의도를 기준으로 분리한다.
  - UI/연출 전환: `System`
  - 피격: `Combat`
  - 이동: `Movement`
  - 경고: `Warning`
- 우선순위는 충돌 시 기대 동작이 드러나도록 단계를 둔다.
  - `Hit_Heavy` > `Hit_Small`
  - `LowHealth_Warning`은 `Warning` 채널 내에서 높은 우선순위

### 2-2. 프리셋별 의도 요약

| 프리셋 | 의도 | 라이프사이클 | 핵심 모듈 |
|---|---|---|---|
| FadeIn | 검은 화면에서 장면 진입 | Timed | Fade |
| FadeOut | 장면 종료/전환 직전 암전 | Timed | Fade |
| Hit_Small | 가벼운 피격 피드백 | Timed | Shake + Overlay |
| Hit_Heavy | 강한 피격 피드백 | Timed | Shake + Zoom + Overlay |
| Dash_Burst | 돌진 시작 가속감 | Timed | Zoom + (약한) Shake |
| LowHealth_Warning | 저체력 경고 반복 | Loop | Overlay (+약한 Fade 선택) |

---

## 3) 구현 (문서/데이터 기준)

아래 값은 **즉시 사용 가능한 기준값**이며, 프로젝트 톤에 맞게 수치만 미세 조정하면 됩니다.

## 3-1. 공통 생성 절차

1. Project 창에서 우클릭 → `Create/Game/Camera Effects/Camera Effect Preset`.
2. 에셋 이름을 프리셋명으로 지정.
3. `Common` 섹션(EffectName/Duration/Loop/Instant/DefaultChannel/Priority) 설정.
4. 필요한 모듈만 `Enabled = true`로 켠다.
5. Curve는 기본 Linear에서 시작 후 의도에 맞게 수정한다.

---

### 3-2. `FadeIn`

- **권장 에셋명**: `CE_FadeIn`
- **Common**
  - Effect Name: `FadeIn`
  - Duration Seconds: `0.60`
  - Is Loop: `false`
  - Is Instant Effect: `false`
  - Default Channel: `System`
  - Priority: `80`
- **Modules**
  - Fade: Enabled `true`, Intensity `1.00`, Duration `0.60`, Curve `EaseOut(초반 강, 후반 완화)`
    - Fade Curve: (0.00, 1.00), (0.15, 0.72), (0.35, 0.38), (0.65, 0.12), (1.00, 0.00)
  - Shake: Disabled
  - Zoom: Disabled
  - Overlay: Disabled
- **의도**: 시작 프레임에서 검은색 영향이 크고, 끝으로 갈수록 원래 화면으로 자연스럽게 복귀.

### 3-3. `FadeOut`

- **권장 에셋명**: `CE_FadeOut`
- **Common**
  - Effect Name: `FadeOut`
  - Duration Seconds: `0.45`
  - Is Loop: `false`
  - Is Instant Effect: `false`
  - Default Channel: `System`
  - Priority: `90`
- **Modules**
  - Fade: Enabled `true`, Intensity `1.00`, Duration `0.45`, Curve `EaseIn(초반 약, 후반 강)`
    - Fade Curve: (0.00, 0.00), (0.25, 0.05), (0.55, 0.22), (0.80, 0.62), (1.00, 1.00)
  - Shake: Disabled
  - Zoom: Disabled
  - Overlay: Disabled
- **의도**: 장면 종료 직전에 어두워지는 체감이 후반에 강하게 몰리게 구성.

### 3-4. `Hit_Small`

- **권장 에셋명**: `CE_Hit_Small`
- **Common**
  - Effect Name: `Hit_Small`
  - Duration Seconds: `0.18`
  - Is Loop: `false`
  - Is Instant Effect: `false`
  - Default Channel: `Combat`
  - Priority: `120`
- **Modules**
  - Fade: Disabled
  - Shake: Enabled `true`, Intensity `0.18`, Duration `0.12`, Curve `빠른 감쇠`
    - Shake Curve: (0.00, 1.00), (0.20, 0.70), (0.45, 0.32), (0.75, 0.08), (1.00, 0.00)
  - Zoom: Disabled
  - Overlay: Enabled `true`, Intensity `0.20`, Duration `0.10`, Curve `초반 피크 후 급감`
    - Overlay Curve: (0.00, 0.00), (0.08, 1.00), (0.22, 0.72), (0.50, 0.20), (1.00, 0.00)
  - Overlay Color: `RGBA(0.85, 0.10, 0.10, 1.0)`
- **의도**: 짧고 날카로운 피격 감각. 과도한 시야 변형 없이 순간 임팩트만 전달.

### 3-5. `Hit_Heavy`

- **권장 에셋명**: `CE_Hit_Heavy`
- **Common**
  - Effect Name: `Hit_Heavy`
  - Duration Seconds: `0.32`
  - Is Loop: `false`
  - Is Instant Effect: `false`
  - Default Channel: `Combat`
  - Priority: `180`
- **Modules**
  - Fade: Enabled `true`, Intensity `0.25`, Duration `0.20`, Curve `짧은 딥`
    - Fade Curve: (0.00, 0.00), (0.12, 1.00), (0.35, 0.45), (0.65, 0.10), (1.00, 0.00)
  - Shake: Enabled `true`, Intensity `0.38`, Duration `0.24`, Curve `초기 강, 점감`
    - Shake Curve: (0.00, 1.00), (0.15, 0.92), (0.35, 0.62), (0.65, 0.25), (1.00, 0.00)
  - Zoom: Enabled `true`, Intensity `2.60`, Duration `0.22`, Curve `초반 급확대 후 복귀`
    - Zoom Curve: (0.00, 0.00), (0.10, 0.92), (0.18, 1.00), (0.45, 0.55), (1.00, 0.00)
  - Overlay: Enabled `true`, Intensity `0.35`, Duration `0.18`, Curve `초반 피크`
    - Overlay Curve: (0.00, 0.00), (0.06, 1.00), (0.18, 0.82), (0.45, 0.28), (1.00, 0.00)
  - Overlay Color: `RGBA(0.75, 0.05, 0.05, 1.0)`
- **의도**: 작은 피격 대비 강한 체감. 채널 충돌 시 `Hit_Small`을 대체할 수 있는 우선순위.

### 3-6. `Dash_Burst`

- **권장 에셋명**: `CE_Dash_Burst`
- **Common**
  - Effect Name: `Dash_Burst`
  - Duration Seconds: `0.16`
  - Is Loop: `false`
  - Is Instant Effect: `false`
  - Default Channel: `Movement`
  - Priority: `110`
- **Modules**
  - Fade: Disabled
  - Shake: Enabled `true`, Intensity `0.08`, Duration `0.08`, Curve `매우 빠른 감쇠`
    - Shake Curve: (0.00, 0.80), (0.18, 0.42), (0.40, 0.14), (0.70, 0.03), (1.00, 0.00)
  - Zoom: Enabled `true`, Intensity `1.80`, Duration `0.16`, Curve `전반 상승 후 후반 복귀`
    - Zoom Curve: (0.00, 0.00), (0.22, 0.55), (0.45, 1.00), (0.72, 0.58), (1.00, 0.00)
  - Overlay: Disabled
- **의도**: 시야가 전방으로 튀어나가는 듯한 순간 가속감 표현.

### 3-7. `LowHealth_Warning`

- **권장 에셋명**: `CE_LowHealth_Warning`
- **Common**
  - Effect Name: `LowHealth_Warning`
  - Duration Seconds: `1.20`
  - Is Loop: `true`
  - Is Instant Effect: `false`
  - Default Channel: `Warning`
  - Priority: `200`
- **Modules**
  - Fade: Enabled `true`, Intensity `0.08`, Duration `1.20`, Curve `완만한 펄스`
    - Fade Curve: (0.00, 0.15), (0.25, 0.55), (0.50, 1.00), (0.75, 0.55), (1.00, 0.15)
  - Shake: Disabled
  - Zoom: Disabled
  - Overlay: Enabled `true`, Intensity `0.28`, Duration `1.20`, Curve `사인파 유사 펄스`
    - Overlay Curve: (0.00, 0.10), (0.20, 0.45), (0.50, 1.00), (0.80, 0.45), (1.00, 0.10)
  - Overlay Color: `RGBA(0.60, 0.00, 0.00, 1.0)`
- **의도**: 플레이를 방해하지 않으면서 저체력 상태를 지속적으로 인지시킴.
- **반복 표시 팁**: Loop 모드에서는 모듈 Duration 주기로 커브를 반복 샘플링하므로, Overlay/Fade 커브를 `0 → 1 → 0` 펄스 형태로 설정하면 주기적으로 깜빡이는 경고를 유지할 수 있음.
- **종료 원칙**: 저체력 해제 이벤트에서 반드시 `StopChannelEffect(E_CameraEffectChannel.Warning, reason)` 또는 핸들 기반 `Stop` 호출.

---

## 4) Inspector 세팅 방법

### 4-1. CameraEffectManager

1. 씬의 매니저 오브젝트에 `CameraEffectManager`를 1개만 유지.
2. `Target Camera` 연결(없으면 Main Camera 자동 탐색).
3. `Accessibility` 섹션 수치 확인:
   - FlashReduction On 배율
   - ColorBlind/HighContrast 배율
   - OptionManager 미연결 폴백 배율
4. `Lifecycle Guard`:
   - Loop 누락 방지를 위해 Safety Timeout 활성 유지 권장.

### 4-2. 프리셋 에셋

1. 각 프리셋 에셋의 `Common`을 표 기준으로 맞춘다.
2. 사용하지 않는 모듈은 반드시 `Enabled = false`.
3. 모듈 `Duration <= 0`을 넣을 경우 프리셋 Duration을 폴백 사용하므로 의도적으로만 사용.
4. Overlay 색상은 HDR 과포화가 아닌 LDR 기준으로 먼저 튜닝 후 필요 시 확장.

---

## 5) 테스트 방법

### 5-1. 수동 재생 체크리스트

1. 각 프리셋을 1회씩 재생해 의도(강도/길이/색감)가 맞는지 확인.
2. 동일 채널에서 `Hit_Small` 직후 `Hit_Heavy`를 연속 재생해 우선순위 대체 동작 확인.
3. `LowHealth_Warning` 시작 후 조건 해제 시 Stop 호출이 정상 종료되는지 확인.
4. 접근성 옵션 변경(ShakeReduction/FlashReduction/Color 관련 옵션) 후 활성 효과 체감 강도 즉시 변화 확인.

### 5-2. 런타임 로그 체크

- 다음 Warning 키가 발생하면 의도된 방어 동작인지 확인:
  - `CE.Request.Invalid`
  - `CE.Module.InvalidData`
  - `CE.Lifecycle.State`
  - `CE.Accessibility.State`

### 5-3. 권장 호출 패턴 예시

```csharp
// FadeOut 예시
CameraEffectManager.Instance.Play(fadeOutPreset, gameObject);

// LowHealth_Warning 시작/종료 예시
CameraEffectHandle warningHandle = CameraEffectManager.Instance.Play(lowHealthWarningPreset, gameObject);
warningHandle.Stop("LowHealthRecovered");
```

---

## 6) 리스크 및 방어 로직

1. **리스크: Loop 종료 누락**
   - 증상: `LowHealth_Warning`이 상태 해제 후에도 잔류.
   - 방어: 상태 해제 이벤트에서 명시적 Stop 호출 + 매니저 Loop Safety Timeout 유지.

2. **리스크: 모듈 값 과도(멀미/가독성 저하)**
   - 증상: Shake/Zoom/Overlay 과강도.
   - 방어: Intensity를 소규모 증분으로 조정(권장 ±0.05 단위), 접근성 옵션 ON 상태 함께 검수.

3. **리스크: 옵션 시스템 미연결**
   - 증상: 접근성 값이 기대대로 반영되지 않음.
   - 방어: 매니저 폴백 배율 사용 + `CE.Accessibility.State` Warning 로그로 즉시 탐지.

4. **리스크: 채널 충돌로 의도치 않은 교체**
   - 증상: 같은 채널 효과가 짧게 끊김.
   - 방어: 프리셋별 채널/우선순위를 표준화하고, 충돌이 잦은 효과는 채널 분리.

---

## 부록) 프리셋 배포 체크리스트

- [ ] 에셋명 접두사 통일: `CE_`
- [ ] Effect Name이 에셋명과 의미적으로 일치
- [ ] 모듈 미사용 항목 `Enabled = false`
- [ ] Loop 프리셋에 종료 트리거 문서화 완료
- [ ] 접근성 옵션 ON/OFF에서 체감 차이 검증 완료
- [ ] Warning 로그 0건 또는 의도된 Warning만 발생

