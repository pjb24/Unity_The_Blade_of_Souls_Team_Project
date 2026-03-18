# VFX 시스템 테스트 Scene 구성 가이드

이 문서는 `EffectService` 기반 VFX 재생 시스템을 **Unity Editor에서 즉시 검증**할 수 있도록,
테스트 씬을 직접 만드는 순서를 단계별로 설명합니다.

- 대상 시스템: `EffectService`, `EffectCatalog`, `EffectDefinition`, `EffectPool`, `EffectHandle`
- 대상 샘플 스크립트: `HitEffectEmitter`, `LandingDustEmitter`, `BuffAuraController`, `VfxInputDebugController`
- 입력 시스템: **New Input System** (`InputActionAsset`, `InputActionReference`)

---

## 0) 사전 준비 체크

1. Project Settings > Player > Active Input Handling이 `Input System Package (New)` 또는 `Both`인지 확인합니다.
2. 샘플 이펙트용 파티클 프리팹(또는 Sprite+Particle 임시 프리팹)을 최소 3개 준비합니다.
   - Hit 용
   - LandingDust 용
   - BuffAura 용
3. 아래 스크립트가 프로젝트에 존재하는지 확인합니다.
   - `Assets/_Workspace/ParkJaeBok/Scripts/VFX/Runtime/EffectService.cs`
   - `Assets/_Workspace/ParkJaeBok/Scripts/VFX/Data/EffectCatalog.cs`
   - `Assets/_Workspace/ParkJaeBok/Scripts/VFX/Data/EffectDefinition.cs`
   - `Assets/_Workspace/ParkJaeBok/Scripts/VFX/Samples/VfxInputDebugController.cs`

---

## 1) 테스트용 Scene 생성

1. Unity 상단 메뉴에서 `File > New Scene`을 선택합니다.
2. 빈 씬으로 시작한 뒤 이름을 예: `VFX_System_Test`로 저장합니다.
3. Hierarchy에 아래 오브젝트를 만듭니다.
   - `VFX_Root` (빈 GameObject)
   - `Service_EffectService` (빈 GameObject, 씬 내 1개만 유지)
   - `Player_Test` (Sprite 또는 빈 GameObject)
   - `Enemy_Test` (Sprite 또는 빈 GameObject)
   - `Debug_HitPoint` (빈 GameObject)

> 권장: `Debug_HitPoint`를 `Enemy_Test` 근처에 배치하여 히트 이펙트 위치를 눈으로 확인하기 쉽게 구성합니다.

---

## 2) EffectDefinition 에셋 생성

각 이펙트마다 `EffectDefinition` ScriptableObject를 생성합니다.

1. Project 창에서 테스트 폴더 생성
   - 예: `Assets/_Workspace/ParkJaeBok/Scripts/VFX/TestAssets/Definitions`
2. 우클릭 > `Create > VFX > Effect Definition`으로 아래 3개를 생성합니다.
   - `ED_HitSmall`
   - `ED_LandingDust`
   - `ED_BuffAura`
3. 각 Definition 인스펙터를 설정합니다.

### ED_HitSmall 권장값
- EffectId: `HitSmall`
- Prefab: 히트 프리팹
- Default Pool Size: `6`
- Max Pool Size: `20`
- Default Play Mode: `OneShot`
- Auto Return: `true`
- (중요) 이펙트는 샘플에서 `Play(effectId, position)` 간단 호출을 쓰므로 Default Play Mode를 OneShot으로 유지
- Max Lifetime: `1.0`
- Default Local Offset: `(0,0,0)`
- Allow Follow: `false`
- Allow Attach: `false`
- Allow Duplicate Play: `true`
- Priority: `0`
- Fallback Policy: `ReuseOldest`

### ED_LandingDust 권장값
- EffectId: `LandingDust`
- Prefab: 착지 먼지 프리팹
- Default Pool Size: `4`
- Max Pool Size: `12`
- Default Play Mode: `OneShot`
- Auto Return: `true`
- (중요) 이펙트는 샘플에서 `Play(effectId, position)` 간단 호출을 쓰므로 Default Play Mode를 OneShot으로 유지
- Max Lifetime: `1.2`
- Allow Follow: `false`
- Allow Attach: `false`
- Allow Duplicate Play: `true`
- Priority: `0`
- Fallback Policy: `InstantiateNew`

### ED_BuffAura 권장값
- EffectId: `BuffAura`
- Prefab: 오라 프리팹
- Default Pool Size: `2`
- Max Pool Size: `6`
- Default Play Mode: `Attach`
- Auto Return: `false`
- Max Lifetime: `10.0` (Loop성 테스트용)
- Allow Follow: `true`
- Allow Attach: `true`
- Allow Duplicate Play: `false`
- Priority: `10`
- Fallback Policy: `DropRequest`

---

## 3) EffectCatalog 생성 및 연결

1. 폴더 예: `Assets/_Workspace/ParkJaeBok/Scripts/VFX/TestAssets/Catalog`
2. 우클릭 > `Create > VFX > Effect Catalog` 생성 (`EC_TestCatalog`)
3. Catalog 인스펙터의 Definitions 리스트에 아래 순서대로 추가합니다.
   - `ED_HitSmall`
   - `ED_LandingDust`
   - `ED_BuffAura`
4. `Service_EffectService` 오브젝트에 `EffectService` 컴포넌트를 추가합니다.
5. `EffectService`의 `_catalog` 필드에 `EC_TestCatalog`를 할당합니다.

---

## 4) 샘플 발행기 구성

### 4-1) HitEffectEmitter
1. `Enemy_Test` 오브젝트에 `HitEffectEmitter` 추가
2. `_hitEffectId`를 `HitSmall`로 설정

### 4-2) LandingDustEmitter
1. `Player_Test` 오브젝트에 `LandingDustEmitter` 추가
2. `_landingDustId`를 `LandingDust`로 설정
3. 필요하면 자식으로 `DustSpawnPoint`를 만들고 `_dustSpawnPoint`에 연결

### 4-3) BuffAuraController
1. `Player_Test` 오브젝트에 `BuffAuraController` 추가
2. `_buffAuraId`를 `BuffAura`로 설정
3. 오라를 붙일 기준 Transform(예: Player 본체 또는 자식 Pivot)을 `_auraAttachTarget`에 연결

---

## 5) New Input System 액션 생성

1. 폴더 예: `Assets/_Workspace/ParkJaeBok/Scripts/VFX/TestAssets/Input`
2. 우클릭 > `Create > Input Actions`로 `IA_VfxTest` 생성
3. 더블클릭하여 편집기에서 Action Map 생성
   - Map 이름: `VFXTest`
4. 액션 3개 생성 (Action Type: Button)
   - `Hit`
   - `Landing`
   - `BuffToggle`
5. 바인딩 예시
   - Hit: Keyboard `H`
   - Landing: Keyboard `J`
   - BuffToggle: Keyboard `K`
6. 저장 후 인스펙터에서 각 액션을 `InputActionReference`로 생성 가능 상태인지 확인

---

## 6) VfxInputDebugController 연결

1. Hierarchy에 `VFX_Input_Debug` 빈 오브젝트를 생성합니다.
2. `VfxInputDebugController`를 추가합니다.
3. 필드 연결
   - `_hitAction`: `IA_VfxTest`의 `VFXTest/Hit` reference
   - `_landingAction`: `IA_VfxTest`의 `VFXTest/Landing` reference
   - `_buffToggleAction`: `IA_VfxTest`의 `VFXTest/BuffToggle` reference
   - `_hitEmitter`: `Enemy_Test`의 `HitEffectEmitter`
   - `_landingEmitter`: `Player_Test`의 `LandingDustEmitter`
   - `_buffAuraController`: `Player_Test`의 `BuffAuraController`
   - `_hitPoint`: `Debug_HitPoint`
4. Retry 설정
   - `_retryInterval`: `0.1`
   - `_maxRetryCount`: `30`

> 이 컨트롤러는 OnEnable 시 코루틴으로 리스너 등록을 재시도하므로,
> 액션 참조가 늦게 준비되는 경우에도 null 예외 없이 동작합니다.

---

## 7) Play Mode 테스트 시나리오

## 7-1) 기본 재생 테스트
1. Play 버튼을 눌러 실행합니다.
2. `H` 키 입력
   - `HitSmall` OneShot이 `Debug_HitPoint` 위치에서 재생되는지 확인
3. `J` 키 입력
   - `LandingDust`가 `Player_Test` 또는 지정 SpawnPoint에서 재생되는지 확인
4. `K` 키 첫 입력
   - `BuffAura`가 `Attach`로 붙어 재생되는지 확인
5. `K` 키 두 번째 입력
   - 기존 `EffectHandle.Stop()`으로 오라가 종료되는지 확인

## 7-2) 중복 제어 테스트
1. `ED_BuffAura`의 `Allow Duplicate Play`가 `false`인지 확인
2. `K`를 연타하여 중복 요청 상황을 만듭니다.
3. 콘솔에서 duplicate 관련 실패 경고가 출력되는지 확인합니다.

## 7-3) 폴백 정책 테스트
1. `ED_HitSmall`의 `Max Pool Size`를 `1` 또는 `2`로 낮춥니다.
2. `H`를 매우 빠르게 반복 입력합니다.
3. 콘솔에서 풀 부족 폴백 Warning 로그가 출력되는지 확인합니다.

## 7-4) Attach 대상 유실 테스트
1. 오라가 켜진 상태에서 `Player_Test`를 비활성 또는 파괴합니다.
2. 대상 유실 정책에 따라 이펙트가 정리되는지 확인합니다.

---

## 8) 디버그 로그 점검 포인트

아래 Warning 로그는 의도적으로 설계된 운영 신호입니다.

1. Catalog/Definition 누락
   - `definition not found`
   - `catalog is null`
2. 풀 부족/폴백 실행
   - `풀 부족 폴백 실행`
3. 재생 제한
   - `duplicate blocked`
4. Follow/Attach 유실
   - `follow target lost`, `attach target lost`
5. 입력 리스너 지연 등록
   - `action is null. Delaying listener registration.`
6. 모드 보정 관련
   - Follow/Attach 비허용 Definition 요청 시 Spawn 폴백 Warning

> 로그가 안 보이면 Console의 Collapse/Log Level 필터를 점검하세요.

---

## 9) 자주 발생하는 설정 실수

1. `EffectService._catalog` 미할당
2. `EffectDefinition.Prefab` 미할당
3. `EffectCatalog`에 Definition 등록 누락
4. `InputActionReference`에 액션 미연결
5. `BuffAura`를 `Attach`로 쓰는데 `_auraAttachTarget`이 null

---

## 10) 완료 기준 (Done Definition)

아래를 모두 만족하면 테스트 씬 구성 완료입니다.

1. `H` 입력 시 히트 이펙트가 안정적으로 재생/반환된다.
2. `J` 입력 시 착지 먼지가 위치 오차 없이 재생된다.
3. `K` 토글로 버프 오라 시작/종료가 재현된다.
4. 중복/풀 부족/참조 누락 시 Warning 로그가 정상 출력된다.
5. Play를 반복해도 null 예외가 발생하지 않는다.

---

## 11) 다음 확장 권장

1. SurfaceType(Material) 기반 이펙트 분기 Resolver 추가
2. WeaponType별 Catalog 분리 또는 Profile 계층화
3. 품질 옵션별 EffectId 블랙리스트(저사양 생략) 도입
4. 런타임 모니터 패널(`active count`, `fallback count`) 추가

