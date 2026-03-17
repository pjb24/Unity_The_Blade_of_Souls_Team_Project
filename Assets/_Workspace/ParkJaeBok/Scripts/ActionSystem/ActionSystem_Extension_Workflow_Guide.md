# Action System 확장 워크플로 가이드 (인덱스)

이 문서는 Action System 확장 문서의 **진입점(Index)** 입니다.

---

## 문서 분리 안내

요청에 맞게 확장 문서를 아래처럼 분리했습니다.

1. 일반 동작(애니메이션) 및 액터 타입 확장
   - `ActionSystem_GeneralAction_Add_Guide.md`
2. Combo Attack / Boss Pattern 확장
   - `ActionSystem_ComboAndBossPattern_Guide.md`

---

## 어떤 문서를 보면 되는가?

- `Dash`, `Stun`, `Use` 같은 일반 액션을 추가하려면
  - `ActionSystem_GeneralAction_Add_Guide.md`
- `NPC`, `Trap` 같은 신규 타입을 추가하려면
  - `ActionSystem_GeneralAction_Add_Guide.md`
- 플레이어 콤보 공격을 추가하려면
  - `ActionSystem_ComboAndBossPattern_Guide.md`
- 보스 패턴 시퀀스/인터럽트(피격, 브레이크)까지 구성하려면
  - `ActionSystem_ComboAndBossPattern_Guide.md`

---

## 공통 운영 원칙

- 액션 허용/비허용은 코드 분기보다 Rule(`Enabled`) 중심으로 관리합니다.
- Rule 데이터는 `ActionRuleProfile` ScriptableObject로 관리하고 `ActionController._actionRuleProfile`에 연결합니다.
- 애니메이션 매핑은 `AnimationStateMapProfile` ScriptableObject로 관리하고 `ActionAnimationPresenter._stateMapProfile`에 연결합니다.
- Animation Event marker 명령은 `AnimationMarkerProfile` ScriptableObject로 관리하고 `ActionController._animationMarkerProfile`에 연결합니다.
- 피격 매핑은 `ActionHitBridgeProfile` ScriptableObject로 관리하고 `ActionHitBridge._hitBridgeProfile`에 연결합니다.
- 마지막 상태 유지 연출은 `ActionAnimationPresenter`의 Presentation Lock 옵션으로 관리합니다(해제 허용 액션 포함).
- 인터럽트 정책은 우선순위(`Priority`)와 인터럽트 가능 여부(`IsInterruptible`)를 먼저 설계합니다.
- 피격 이벤트 소스는 `ActionHitBridge`를 통해 `ActionController`로 전달하는 것을 권장합니다(미연결 시 동일 오브젝트 fallback으로 보정 시도).
- 예외 흐름은 Warning 로그로 드러나게 유지합니다.
- 오케스트레이션(Combo/BossPattern)과 실행(Source of Truth)은 분리합니다.
  - 실행: `ActionController`
  - 오케스트레이션: `ActionComboController`, `BossPatternController`
