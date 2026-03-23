# Audio System 테스트 Scene 제작 가이드

이 문서는 현재 프로젝트에 구현된 오디오 시스템(`AudioManager`, `SoundDatabase`, `AudioSystemTest`)을 실제 Unity Editor에서 검증할 수 있도록, **처음부터 테스트 Scene을 구성하는 상세 절차**를 설명합니다.

---

## 1. 준비 사항

1. Unity Editor에서 프로젝트를 연다.
2. Console 창을 열어둔다. (`Window > General > Console`)
3. Audio Clip(BGM/SFX) 리소스가 프로젝트에 임포트되어 있는지 확인한다.

> 권장: 테스트가 끝난 뒤 실제 게임 Scene에 영향을 주지 않도록 별도의 테스트 전용 Scene에서 작업한다.

---

## 2. 테스트 전용 Scene 생성

1. `File > New Scene` 선택.
2. 빈 Scene이 열리면 `File > Save As...`로 저장.
3. 예시 경로: `Assets/Settings/Scenes/AudioSystemTestScene` (팀 규칙에 맞게 조정 가능)

---

## 3. SoundDatabase 에셋 생성 및 데이터 입력

1. Project 창에서 우클릭.
2. `Create > Audio > Sound Database` 선택.
3. 에셋 이름을 `SoundDatabase_AudioTest` 등으로 설정.
4. 생성된 SoundDatabase를 선택하고 Inspector에서 `Entries`를 펼친다.
5. `Size`를 늘려 BGM/SFX 항목을 추가한다.
6. 각 엔트리에 다음 값을 입력한다.
   - `SoundId`: `E_SoundId` 값 선택 (예: `BGM_Stage01`, `SFX_Attack`)
   - `Clip`: 해당 AudioClip 지정
   - `Volume`: 0~1
   - `BasePitch`: 기본 1
   - `Loop`: BGM은 true 권장, 일반 SFX는 false
   - `UseRandomPitch`: 반복 타격음 같은 경우 true 권장
   - `RandomPitchOffsetMin/Max`: 예: -0.05 / 0.05
   - `CooldownSeconds`: 예: 공격음 중복 방지 0.03~0.1

### 3-1. OnValidate 경고 확인 포인트

`SoundDatabase`는 저장/수정 시 자동 검증을 수행하므로, Console에서 아래 경고가 정상적으로 보이는지 확인할 수 있다.

- `SoundId None` 경고
- `Clip null` 경고
- `SoundId 중복` 경고
- `RandomPitch Min > Max` 자동 보정 경고
- 비정상 pitch 범위 경고

> 의도적으로 잘못된 값을 넣어 경고를 확인한 뒤, 다시 올바른 값으로 복구하면 검증 기능 동작 여부를 빠르게 점검할 수 있다.

---

## 4. AudioManager 오브젝트 구성

1. Hierarchy에서 빈 오브젝트 생성 (`Create Empty`).
2. 이름을 `AudioManager`로 변경.
3. `AudioManager` 스크립트를 Add Component.
4. Inspector에서 다음 항목 설정:
   - `Sound Database`: 3단계에서 만든 `SoundDatabase_AudioTest` 연결
   - `Dont Destroy On Load`: 테스트 목적에 맞게 On/Off
   - `Initial Sfx Pool Size`: 8~16 권장
   - `Max Sfx Pool Size`: 16~32 권장
   - `Use Distance Attenuation For Sfx`: 거리 감쇠 테스트 시 true
   - `Sfx Spatial Blend`: 1 (완전 3D 테스트)
   - `Sfx Min Distance`: 1
   - `Sfx Max Distance`: 20
   - `Sfx Spread`: Inspector에서 0~360 범위로 조절 (Unity Audio Source Spread와 동일 개념)
   - `Sfx Rolloff Mode`: Inspector에서 Logarithmic / Linear / Custom 중 선택
   - `Sfx Custom Rolloff Curve`: Rolloff Mode가 Custom일 때 Inspector 커브 에디터로 조절
   - `Master/Bgm/Sfx Volume`: 기본 1
   - 참고: BGM은 시스템에서 항상 2D로 고정되어 거리 감쇠가 적용되지 않음

### 4-1. 거리 감쇠 테스트 설정 팁

- Main Camera를 Listener로 사용한다(일반적으로 Camera에 Audio Listener가 있음).
- SFX 발신 지점과 카메라 사이 거리를 크게 벌리면 소리가 줄어드는지 쉽게 확인 가능.
- Spread 값을 올리면(예: 180~360) 3D 공간에서 소리 확산 특성이 더 넓게 느껴지는지 확인 가능.

---

## 5. AudioSystemTest 오브젝트 구성

1. Hierarchy에서 빈 오브젝트 생성.
2. 이름을 `AudioSystemTest`로 변경.
3. `AudioSystemTest` 컴포넌트 추가.
4. Inspector에서 테스트용 ID를 지정.
   - `Test Bgm Id`: `BGM_Stage01`
   - `Test Bgm CrossFade Id`: `BGM_Boss`
   - `Test Sfx Id`: `SFX_Attack`
5. `Test Sfx Emitter`용 오브젝트를 추가 생성:
   - 빈 오브젝트 이름: `SfxEmitter`
   - 위치를 카메라와 떨어진 좌표로 설정(예: X=8, Y=0, Z=0)
   - `AudioSystemTest`의 `Test Sfx Emitter` 필드에 `SfxEmitter` 연결

---

## 6. 입력 키 테스트 시나리오 (New Input System)

`AudioSystemTest` 기본 키 매핑:

- `1`: SFX 재생
- `2`: BGM 재생
- `3`: BGM 크로스페이드
- `4`: BGM 페이드아웃
- `5`: BGM 정지
- `-`: Master Volume 감소
- `=`: Master Volume 증가

### 검증 절차

1. Play 모드 시작.
2. `2` 키 입력 → BGM 시작 확인.
3. `3` 키 입력 → 다른 BGM으로 부드럽게 전환 확인.
4. `4` 키 입력 → 페이드아웃 후 정지 확인.
5. `1` 키를 연타해 SFX 동시 재생/쿨다운 동작 확인.
6. 카메라와 `SfxEmitter` 간 거리를 바꿔 거리 감쇠 확인.
7. `-`, `=` 키로 마스터 볼륨 변경 및 로그 확인.

---

## 7. SFX Pool 확장/폴백 테스트

풀 동작 검증을 위해 임시로 다음처럼 설정한다.

- `Initial Sfx Pool Size`: 1
- `Max Sfx Pool Size`: 2

이후 `1` 키를 빠르게 연타하면,

1. 풀 확장 Warning 로그
2. 최대치 도달 시 재사용 Warning 로그

가 발생하는지 확인한다.

---

## 8. PlayerPrefs 볼륨 저장/로드 테스트

1. Play 중 `-`, `=`로 마스터 볼륨을 변경.
2. Play 종료 후 다시 Play 실행.
3. 이전 볼륨 값이 유지되는지 확인.
4. 필요 시 `PlayerPrefs Delete` 로직 또는 Editor 메뉴를 활용해 초기화 후 재검증.

---

## 9. 문제 발생 시 체크리스트

1. `AudioManager`의 `Sound Database`가 비어있지 않은가?
2. `SoundDatabase` 엔트리에 Clip이 비어있지 않은가?
3. `E_SoundId.None`을 테스트 ID로 쓰고 있지 않은가?
4. Console에 Warning이 출력되었는가? (이 시스템은 silent fallback 금지)
5. New Input System이 활성화되어 있는가?
   - `Project Settings > Player > Active Input Handling` 확인

---

## 10. 최종 권장 검증 세트

- [ ] BGM Play/Stop/Fade/CrossFade 전부 정상
- [ ] SFX 쿨다운 정상
- [ ] 랜덤 피치가 귀로 구분될 정도로 변동
- [ ] 거리 감쇠(가까울수록 큼 / 멀수록 작음) 정상
- [ ] 볼륨 저장/로드 정상
- [ ] 경고 로그가 필요한 상황에서만 출력

---

## 부록: 빠른 스모크 테스트 절차 (3분)

1. `AudioManager` + `SoundDatabase` 연결
2. `AudioSystemTest` 추가 및 ID 3개 지정
3. `SfxEmitter`를 카메라에서 10 이상 떨어진 위치에 배치
4. Play 후 `2`, `1`, `3`, `4` 순서로 입력
5. 볼륨 `-` 두 번, `=` 한 번 입력
6. Console Warning/Error 확인 후 종료

위 단계가 모두 통과하면, 기본 오디오 재생 시스템은 통합 테스트 기준에서 정상으로 볼 수 있다.


## 11. 캐릭터별 위치에서 SFX 재생하기

각 캐릭터가 자신의 위치에서 SFX를 재생하려면, 캐릭터 스크립트에서 `Transform`을 직접 넘겨서 호출한다.

```csharp
AudioManager.Instance.PlaySfx(E_SoundId.SFX_Attack, transform);
```

권장 패턴:

1. 캐릭터 루트(`transform`) 또는 발소리/무기 소켓 Transform을 emitter로 사용
2. 캐릭터 행동 이벤트(공격, 피격, 점프) 시점에 해당 Transform을 전달
3. emitter가 null일 수 있는 코드에서는 null 체크 후 호출

참고:
- `PlaySfx(E_SoundId, Transform)`은 Transform이 null이면 Warning 로그를 출력하고 AudioManager 위치를 폴백으로 사용한다.
- 멀티 캐릭터 상황에서도 각각의 호출 시점 Transform 위치로 SFX가 배치되므로, 캐릭터별 위치 분리가 가능하다.

---


## 12. 다중 오브젝트 동시 SFX 재생 검증 (Player/Enemy/Item)

여러 오브젝트가 동시에 SFX를 내도 플레이에 영향이 없는지 아래 순서로 검증한다.

1. Player, Enemy, Item 역할 오브젝트를 각각 Scene에 배치한다.
2. 각 오브젝트 스크립트에서 아래 패턴으로 자신의 Transform을 emitter로 전달한다.

```csharp
AudioManager.Instance.PlaySfx(E_SoundId.SFX_Attack, transform);
```

3. 같은 프레임 또는 매우 짧은 간격으로 3개 오브젝트가 동시에 SFX를 호출하게 만든다.
4. 재생 중인 소리가 서로 끊기지 않는지 확인한다.
5. Console에서 필요 시 다음 경고를 확인한다.
   - SFX 풀 확장 경고
   - 최대치 도달 시 임시 AudioSource 생성 경고

참고:
- 현재 시스템은 풀 여유가 없을 때 기존 재생을 강제 중단하지 않고 임시 AudioSource를 생성한다.
- 따라서 Player/Enemy/Item이 동시에 소리를 내도 기존 재생을 덮어쓰지 않는다.

---

## 13. BGM 상황별 자동 전환 시스템(BgmDirector) 적용

현재 AudioManager 위에 **BgmDirector**를 추가하면, 게임 상황(탐험/전투/보스/메뉴 등)에 따라 BGM을 자동으로 선택할 수 있다.

### 13-1. 구성 요소

- `BgmDirector`
  - 활성 컨텍스트 요청을 수집하고 우선순위가 가장 높은 BGM을 선택
  - 실제 재생은 기존 `AudioManager.PlayBgm / CrossFadeBgm / FadeOutBgm`을 호출
- `BgmContextRule`
  - 컨텍스트 타입별 BGM ID, 우선순위, CrossFade 시간, 최소 유지 시간 정의
- `BgmContextBridge`
  - 개별 시스템/오브젝트에서 `PushContext`, `PopContext`를 쉽게 호출하는 브리지
- `E_BgmContextType`
  - `Title`, `Exploration`, `Combat`, `Boss`, `Menu`, `Cutscene` 등 컨텍스트 분류 enum

### 13-2. 씬 설정 절차

1. 빈 오브젝트를 생성하고 `BgmDirector` 컴포넌트를 추가한다.
2. `Context Rules`에 프로젝트에서 사용할 컨텍스트 규칙을 등록한다.
   - 예시
     - Exploration -> `BGM_Stage01`, Priority 30
     - Combat -> `BGM_Boss`(임시), Priority 70
     - Boss -> `BGM_Boss`, Priority 100
3. `Use Fallback Bgm`을 켜고 `Fallback Bgm Id`를 기본 탐험 곡으로 지정한다.
4. 필요 시스템 오브젝트에 `BgmContextBridge`를 붙이고, 상황 시작/종료 지점에서 `PushContext`/`PopContext`를 연결한다.

### 13-3. 권장 동작 규칙

- 상위 우선순위 컨텍스트가 들어오면 즉시 전환
- 하위 우선순위로 복귀할 때는 `MinHoldDuration`을 활용해 떨림 전환 방지
- 같은 BGM ID 재요청은 무시되어 불필요한 재시작 방지

### 13-4. 빠른 검증 시나리오

1. Play 시작 후 활성 컨텍스트가 없는 상태에서 폴백 BGM 재생 확인
2. Combat 컨텍스트 Push 시 전투 BGM으로 크로스페이드 확인
3. Boss 컨텍스트 Push 시 보스 BGM으로 전환 확인
4. Boss Pop 후 Combat이 남아 있으면 전투 BGM 복귀 확인
5. 모든 컨텍스트 Pop 시 폴백 BGM(또는 FadeOut) 복귀 확인

---

## 14. SFX Orchestrator(이벤트 라우팅 레이어) 적용

`AudioManager`를 직접 호출하는 대신, 중간 레이어인 `SfxOrchestrator`를 통해 **이벤트 기반으로 SFX를 라우팅**할 수 있다.

### 14-1. 구성 요소

- `E_SfxEventType`
  - 게임플레이 이벤트 타입 enum (`AttackSwing`, `HitConfirm`, `Footstep`, `UiClick` 등)
- `SfxEventRule`
  - `EventType(+SubTypeKey) -> E_SoundId` 매핑과 호출 제한 정책 정의
  - `GlobalMinInterval`, `PerEmitterMinInterval`, `MaxRequestsPerSecond` 포함
- `SfxOrchestrator`
  - 이벤트 요청 수신
  - 룰 매칭
  - 중복/과도 호출 제한
  - 최종적으로 `AudioManager.PlaySfx(...)` 호출

### 14-2. 씬 설정 절차

1. 빈 오브젝트 생성 후 `SfxOrchestrator` 컴포넌트 추가
2. `Rules`에 이벤트별 매핑 등록
   - 예시 A: `EventType=AttackSwing`, `SubTypeKey="weapon.sword"`, `SoundId=SFX_Attack`
   - 예시 B: `EventType=Footstep`, `SubTypeKey="surface.stone"`, `SoundId=SFX_Hit`(임시)
   - 예시 C: `EventType=UiClick`, `SubTypeKey=""`, `SoundId=SFX_UI_Click`
3. 필요 시 누락 룰 폴백 사용
   - `Use Fallback On Missing Rule` 활성화
   - `Fallback Sound Id` 설정

### 14-3. 호출 예시 패턴

- 이벤트 타입만:
  - `Request(E_SfxEventType.Jump, transform)`
- 이벤트 + 세부 타입:
  - `Request(E_SfxEventType.Footstep, "surface.stone", transform)`
- 이벤트 + 세부 타입 + 좌표 강제:
  - `Request(E_SfxEventType.HitConfirm, "weapon.spear", transform, hitPoint)`

### 14-4. 운영 권장

- 기존 코드에서 `AudioManager.Instance.PlaySfx(...)`를 직접 부르는 지점을 점진적으로 `SfxOrchestrator.Request(...)`로 치환
- 사운드 클립/볼륨/랜덤피치/기본 쿨다운은 기존 `SoundDatabase`에서 유지
- 빈번한 이벤트(발소리/연타 타격음)는 `MaxRequestsPerSecond`와 `PerEmitterMinInterval`을 함께 사용
