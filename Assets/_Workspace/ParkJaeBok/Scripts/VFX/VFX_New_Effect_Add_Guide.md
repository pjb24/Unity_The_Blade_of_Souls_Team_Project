# VFX 시스템에 새로운 이펙트 추가 가이드

이 문서는 현재 프로젝트의 `EffectService` 기반 VFX 시스템에
**새로운 이펙트를 안전하게 추가하는 방법**을 단계별로 설명합니다.

대상 독자:
- 클라이언트 프로그래머
- 테크 아티스트
- 이펙트 데이터 셋업 담당자

---

## 1) 이 문서의 목표

새 이펙트를 추가할 때 아래 문제가 자주 발생합니다.

- `EffectId`는 추가했는데 `EffectCatalog` 등록 누락
- `EffectDefinition`은 만들었는데 `Prefab` 미할당
- 재생은 되지만 `Follow/Attach` 정책과 실제 사용이 불일치
- 풀 크기/폴백 정책 부적절로 런타임 경고 과다 발생
- 중복 제어가 필요한 이펙트를 무제한 중복 재생

이 문서는 위 문제를 예방하기 위한 **표준 절차 + 점검 체크리스트**를 제공합니다.

---

## 2) 시스템 구성 빠른 이해

새 이펙트 추가 시 핵심 관련 파일은 아래입니다.

1. `Core/E_EffectEnums.cs`
   - `E_EffectId`에 신규 ID 추가
2. `Data/EffectDefinition.cs`
   - 이펙트 데이터 정책(풀, 수명, 허용 모드, 폴백)
3. `Data/EffectCatalog.cs`
   - `EffectId -> EffectDefinition` 매핑
4. `Runtime/EffectService.cs`
   - `Play(...)` 요청 진입, Definition 조회, 풀 획득, 반환
5. `Runtime/EffectPool.cs`
   - 이펙트별 인스턴스 풀 관리
6. `Runtime/EffectInstance.cs`
   - 개별 인스턴스 수명/Follow/Attach/Owner 유실 처리

---

## 3) 표준 추가 절차 (권장 순서)



### 현재 구현 기준으로 꼭 알아둘 동작

- `Play(effectId, position)`는 내부에서 `EffectRequest.CreateSimple(...)`를 사용합니다.
- 현재 `EffectService.NormalizeRequestByDefinition(...)` 구현상, 요청 모드가 `OneShot`이면서 Definition의 `DefaultPlayMode`가 `OneShot`이 아니면 Definition 모드로 치환될 수 있습니다.
  - 따라서 간단 호출을 OneShot으로 강제하고 싶다면 **해당 Definition의 `DefaultPlayMode`를 `OneShot`으로 유지**하는 것을 권장합니다.
- `OneShot` 요청은 서비스에서 `AutoReturn=true` 오버라이드가 강제로 설정됩니다.

---

## 3-1) E_EffectId에 신규 항목 추가

1. `Assets/_Workspace/ParkJaeBok/Scripts/VFX/Core/E_EffectEnums.cs`를 엽니다.
2. `E_EffectId` enum에 새 항목을 추가합니다.

예시:
- `WeaponSlashHeavy = 10`
- `IceExplosion = 11`

권장 규칙:
- 이름에 목적이 드러나게 작성 (`Hit`, `Buff`, `Trail`, `Impact` 등)
- 임시 이름(`Effect1`, `TestFx`)은 본 개발 브랜치에 남기지 않기

---

## 3-2) 프리팹 준비

1. 새 이펙트용 프리팹을 준비합니다.
2. 프리팹 내부 종료 특성을 점검합니다.
   - OneShot: 파티클이 자연 종료되는가?
   - Loop: 외부 Stop 시 즉시 정리 가능한가?
3. 필요 시 `TrailRenderer`, `Animator`가 포함되어도 정상 리셋되는지 확인합니다.

> `EffectInstance`는 반환 시 파티클 정지/클리어, 트레일 클리어, 애니메이터 리바인딩을 수행합니다.

---

## 3-3) EffectDefinition 에셋 생성

1. Project 창에서 우클릭 > `Create > VFX > Effect Definition`
2. 에셋 이름 예시:
   - `ED_WeaponSlashHeavy`
   - `ED_IceExplosion`
3. 인스펙터 필드 설정

### 필드별 권장 해석

- **EffectId**
  - 방금 enum에 추가한 ID와 정확히 일치
- **Prefab**
  - 실제 재생할 이펙트 프리팹
- **DefaultPoolSize**
  - 평시 동시 재생량 기준의 초기 풀
- **MaxPoolSize**
  - 피크 상황 최대 동시 재생량
- **DefaultPlayMode**
  - 명시하지 않았을 때 기본 동작
- **AutoReturn**
  - OneShot 계열 보통 `true`, Loop성은 `false`
- **MaxLifetime**
  - 안전장치 시간(예상 종료 시간보다 약간 크게)
- **DefaultLocalOffset**
  - Spawn/Attach 시작 위치 보정
  - 참고: 현재 Follow 갱신 프레임에서는 요청 `LocalOffset`이 우선 적용됩니다.
- **AllowFollow / AllowAttach**
  - 의도치 않은 모드 사용을 정책적으로 차단
- **AllowDuplicatePlay**
  - Aura/Loop 계열은 `false`를 우선 검토
- **Priority**
  - 폴백 재사용 시 우선순위 관리에 활용
- **FallbackPolicy**
  - `InstantiateNew`, `ReuseOldest`, `DropRequest` 중 선택

---

## 3-4) EffectCatalog에 등록

1. 사용하는 `EffectCatalog` 에셋을 엽니다.
2. Definitions 리스트에 새 `EffectDefinition`을 추가합니다.
3. 중복 ID가 없는지 확인합니다.

> `EffectCatalog.Initialize()`에서 중복/누락 관련 경고가 발생할 수 있으므로,
> Play Mode 콘솔 로그를 반드시 확인하세요.

---

## 3-5) 호출부에 재생 코드 추가

재생 코드는 반드시 `EffectService` API를 사용합니다.

### A. 간단 호출 (OneShot Spawn)

```csharp
EffectService.Instance.Play(E_EffectId.IceExplosion, hitPoint);
```

### B. 확장 호출 (Attach/Follow/Owner 제어)

```csharp
EffectRequest request = new EffectRequest();
request.EffectId = E_EffectId.WeaponSlashHeavy;
request.PlayMode = E_EffectPlayMode.Attach;
request.AttachTarget = weaponSocket;
request.Owner = gameObject;
request.LocalOffset = Vector3.zero;
request.LifetimeOverride = 0f;
request.AutoReturnOverrideEnabled = false;
request.AutoReturn = false;
request.IgnoreDuplicateGuard = false;

EffectHandle handle = EffectService.Instance.Play(request);
```

필요 시:
- `handle.Stop()`
- `handle.Pause()` / `handle.Resume()`
- `handle.SetFollowTarget(...)`
- `handle.SetPosition(...)`

---

## 3-6) 샘플/테스트 입력에 연결 (선택)

디버그 확인을 빠르게 하려면 `VfxInputDebugController`에 액션을 추가해
키 입력으로 신규 이펙트를 직접 재생합니다.

권장 순서:
1. Input Action에 새 버튼 액션 추가
2. `VfxInputDebugController`에 레퍼런스 필드/콜백 추가
3. OnEnable/OnDisable의 코루틴 등록/해제 흐름에 동일 패턴으로 연결

---

## 4) 이펙트 유형별 추천 초기값

## 4-1) Hit / Impact (짧은 OneShot)
- PlayMode: `OneShot` 또는 `Spawn`
- AutoReturn: `true`
- MaxLifetime: `0.5 ~ 1.5`
- AllowDuplicatePlay: `true`
- FallbackPolicy: `ReuseOldest` 또는 `InstantiateNew`

## 4-2) Landing Dust
- PlayMode: `OneShot`
- AutoReturn: `true`
- MaxLifetime: `0.8 ~ 1.5`
- DefaultLocalOffset: 발밑 보정
- AllowDuplicatePlay: `true`

## 4-3) Aura / 상태이상 지속형
- PlayMode: `Attach` 또는 `Follow`
- AutoReturn: `false`
- AllowDuplicatePlay: `false` 우선
- FallbackPolicy: `DropRequest` 고려

## 4-4) Trail / Weapon Slash
- PlayMode: `Attach` 또는 `Follow`
- AutoReturn: 연출 길이에 따라 결정
- Pause/Resume 시 Trail 끊김 여부 확인

---

## 5) 런타임 검증 체크리스트

새 이펙트 추가 후 아래 항목을 모두 확인합니다.

1. `Play(effectId, position)` 호출 시 정상 재생되는가?
2. `Play(request)`에서 지정한 모드(Attach/Follow/Loop)가 의도대로 동작하는가?
3. Owner 파괴 시 이펙트가 정리되는가?
4. Follow/Attach 대상 유실 시 안전하게 종료되는가?
5. 폴백 정책이 의도대로 실행되는가?
6. 콘솔에 경고가 쌓이지 않는가?
7. 풀 반환 누락으로 Active 인스턴스가 누적되지 않는가?
8. 간단 호출을 쓰는 이펙트의 `DefaultPlayMode`가 `OneShot`인지 확인했는가?

---

## 6) 자주 나는 문제와 해결 방법

### 문제 A) Play 호출했는데 아무것도 안 나옴
- 원인 후보:
  - Catalog에 Definition 미등록
  - Definition의 Prefab 미할당
  - 잘못된 EffectId 호출
- 점검:
  - `Play 실패. id=..., reason=definition not found`
  - `Play 실패. id=..., reason=prefab missing`

### 문제 B) 너무 많은 Warning(풀 부족)
- 원인 후보:
  - `MaxPoolSize`가 실제 피크보다 작음
  - 입력/로직에서 과도한 연타
- 조치:
  - `DefaultPoolSize`, `MaxPoolSize` 상향
  - 중복 제어 필요 시 `AllowDuplicatePlay=false`

### 문제 C) Attach인데 위치가 어긋남
- 원인 후보:
  - `DefaultLocalOffset` 또는 요청 `LocalOffset` 과도
  - 잘못된 AttachTarget 지정
- 조치:
  - 오프셋 0 기준부터 재조정
  - 본/소켓 Transform 확인

### 문제 D) Loop가 안 꺼짐
- 원인 후보:
  - Handle을 저장하지 않음
  - 종료 이벤트에서 `Stop()` 호출 누락
- 조치:
  - `EffectHandle` 보관 후 라이프사이클 종료 시 명시적으로 `Stop()` 호출

---

## 7) 코드 리뷰 체크 포인트

신규 이펙트 PR 리뷰 시 아래를 확인하면 품질이 크게 좋아집니다.

1. `E_EffectId` 추가 + Catalog 등록이 한 세트로 들어왔는가?
2. Definition 필드(풀/수명/정책)가 합리적인가?
3. 직접 `Instantiate` 호출이 없는가?
4. Loop/Attach/Follow에서 Handle Stop 경로가 있는가?
5. 폴백 경고가 과도하지 않은가?
6. 샘플/테스트 시나리오가 함께 갱신되었는가?

---

## 8) 최소 완료 기준(MVP)

새 이펙트 1개 추가 작업은 아래를 만족하면 완료로 봅니다.

1. `E_EffectId` 추가 완료
2. `EffectDefinition` 생성 및 필수 필드 설정 완료
3. `EffectCatalog` 등록 완료
4. 실제 호출부 1곳 이상에서 `EffectService.Play(...)` 연결 완료
5. Play Mode에서 재생 + 반환(또는 Stop) 검증 완료
6. 콘솔 치명 경고 없음

---

## 9) 권장 네이밍 템플릿

- Enum: `HitSmall`, `LandingDust`, `BuffAura`, `WeaponTrailHeavy`
- Definition Asset: `ED_<EffectId>`
- Prefab: `VFX_<EffectId>`
- 테스트 호출 메서드: `Play<EffectId>Effect()`

예시:
- `E_EffectId.WeaponTrailHeavy`
- `ED_WeaponTrailHeavy`
- `VFX_WeaponTrailHeavy`

---

## 10) 운영 팁

1. 초기에는 `MaxLifetime`을 보수적으로 짧게 두고, 잘리는 경우 늘리는 방식이 안전합니다.
2. 성능 이슈가 있는 이펙트는 우선 `AllowDuplicatePlay=false`로 운영 후 확장하세요.
3. 이벤트성 이펙트(Hit/Impact)는 `ReuseOldest`가 체감 품질과 성능 균형이 좋습니다.
4. 품질 옵션(저사양) 도입 시 고비용 EffectId를 스킵하는 Resolver 계층을 추가하세요.

