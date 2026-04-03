# SaveSystem 설치/운영 가이드

이 문서는 프로젝트에서 SaveSystem을 **처음 파악 -> 설치/연결 -> 저장 항목 확장 -> 운영/장애 대응** 순서로 읽을 수 있도록 재정렬한 가이드입니다.

---

## 1) 목표

- 저장 시스템을 **모듈 단위로 붙였다 떼는 것**이 가능해야 합니다.
- 기존 게임 로직(StageFlow, Health 등)을 크게 건드리지 않고 확장/축소할 수 있어야 합니다.
- 저장 정책은 코드 하드코딩보다 ScriptableObject(`SaveChannelPolicy`, `ForceClosePolicy`, `RecoveryPolicy`)로 제어합니다.

---

## 2) 처음 프로젝트를 열었을 때 확인 순서 (온보딩)

### Step 1. 런타임 루트 확인

1. Hierarchy에서 `Bootstrap Systems Root` 프리팹(또는 인스턴스)을 찾습니다.
2. 하위에서 `SaveCoordinator` 컴포넌트를 가진 오브젝트를 확인합니다.
3. 아래 인스펙터 값이 채워져 있는지 확인합니다.
   - `_backendComponent`
   - `_channelPolicies`
   - `_forceClosePolicy`
   - `_recoveryPolicy`

### Step 2. 저장 채널 정책 확인

1. `SaveChannelPolicy` 3종(Persistent/Session/Recovery) 에셋을 엽니다.
2. 각 정책의 `FileName`, `UseAtomicReplace`, `BackupCount`를 확인합니다.

### Step 3. 저장 참여자(Participant) 확인

1. 폴더: `Assets/_Workspace/ParkJaeBok/Scripts/SaveSystem/Participants`
2. 각 participant에서 아래를 확인합니다.
   - `ParticipantId`
   - `CanSave`
   - `CaptureAsJson`
   - `RestoreFromJson`
3. 중복 `ParticipantId`가 없는지 확인합니다.

### Step 4. 런타임 검증

1. `SaveCoordinator._enablePipelineVerboseLog`를 켭니다.
2. Play Mode에서 Save/Load를 1회 실행합니다.
3. Warning 로그를 확인합니다.
   - participant 누락
   - payload 비어 있음
   - schema 버전 불일치
   - 슬롯/파일 폴백 발생

---

## 3) SaveSystem 구성요소 요약

### 3.1 Core
- `E_SaveChannelType`
- `E_SaveTriggerType`
- `SaveSnapshot`, `SaveParticipantRecord`, `SaveContext`
- `ISaveParticipant`

### 3.2 Data (정책)
- `SaveChannelPolicy`
- `ForceClosePolicy`
- `RecoveryPolicy`

### 3.3 Runtime
- `SaveCoordinator`
- `RecoveryService`

### 3.4 Backends
- `ISaveBackend`
- `JsonFileSaveBackend`

### 3.5 Participants
- `StageSessionSaveParticipant`
- `HealthSaveParticipant`
- `PlayerCheckpointSaveParticipant`
- `EnemyRuntimeStateSaveParticipant`
- `GimmickStateSaveParticipant`
- `PickupStateSaveParticipant`
- `BossEncounterStateSaveParticipant`

---

## 4) SaveCoordinator 인스펙터 항목 설명

아래 기준은 `Bootstrap Systems Root` 하위에 배치된 `SaveCoordinator`입니다.

### 4.1 Dependencies

#### `_backendComponent`
- 할당 대상: `ISaveBackend` 구현체(권장: `JsonFileSaveBackend`)
- 의미: 실제 파일 저장/읽기 담당

#### `_channelPolicies`
- 할당 대상: `SaveChannelPolicy` 에셋 3종(Persistent/Session/Recovery)
- 의미: 채널별 파일명/백업/원자적 저장 규칙

#### `_forceClosePolicy`
- 할당 대상: `ForceClosePolicy` 에셋
- 의미: 씬 전환/일시정지/종료/주기 저장 정책

#### `_recoveryPolicy`
- 할당 대상: `RecoveryPolicy` 에셋
- 의미: Recovery 채널 복원 허용 범위

### 4.2 Runtime

- `_autoRegisterParticipants`: Awake 시 participant 자동 수집
- `_refreshParticipantsAfterSceneLoad`: 씬 로드 후 participant 재수집
- `_autoLoadOnStart`: 시작 시 Persistent/Session 자동 로드
- `_enablePipelineVerboseLog`: 단계별 Save/Load 로그 출력

### 4.3 Save Slots

- `_useSaveSlots`: 슬롯 파일 분기 사용 여부
- `_defaultSaveSlotIndex`: 슬롯 메타 없을 때 기본 슬롯
- `_slotProfileFileName`: 마지막 사용 슬롯 저장 메타 파일명

### 4.4 Runtime Status (디버그 표시)

- `_lastSave*`: 마지막 Save 결과
- `_lastLoad*`: 마지막 Load 결과

> 참고: status 필드는 할당용이 아니라 런타임 결과 표시용입니다.

---

## 5) SaveChannelPolicy 역할

`SaveChannelPolicy`는 **채널별 저장 파일 I/O 정책**을 담당합니다.

1. `ChannelType`: 정책 대상 채널(Persistent/Session/Recovery)
2. `FileName`: 채널 스냅샷 파일명
3. `UseAtomicReplace`: 원자적 교체 저장 사용 여부
4. `BackupCount`: 롤링 백업 개수

실무적으로는 코드 수정 없이 정책 에셋 변경만으로 저장 동작을 조정할 수 있습니다.

### 5.1 채널 타입 의미 (Persistent / Session / Recovery)

#### Persistent
- **의미**: 게임을 종료했다가 다시 시작해도 유지되어야 하는 핵심 진행 데이터 채널
- **예시 데이터**: 스테이지 진행도, 해금 상태, 누적 재화, 영구 업그레이드
- **운영 포인트**: 사용자 체감 손실이 가장 큰 데이터이므로 저장 실패/손상 대응(백업) 우선순위가 높습니다.

#### Session
- **의미**: 현재 플레이 세션 동안의 문맥/임시 상태를 저장하는 채널
- **예시 데이터**: 현재 런 상태, 임시 버프/상태, 씬 내 일시적 진행 정보
- **운영 포인트**: 앱 재시작 후 반드시 100% 복원할 필요가 없는 데이터는 Session으로 분리해 관리합니다.

#### Recovery
- **의미**: 사망/실패 직후 복구를 위한 최소 상태를 저장하는 채널
- **예시 데이터**: 체크포인트, 복구 시 필요한 플레이어/전투 최소 상태
- **운영 포인트**: `RecoveryPolicy`와 함께 사용하여 “무엇을 복구하고 무엇을 초기화할지”를 명시적으로 통제합니다.

---

## 6) SaveSystem 추가(도입) 절차

### Step A. 런타임 오브젝트 구성

> 테스트용 `--- Save System ---` 기준이 아니라, 실제 런타임 루트인 `Bootstrap Systems Root` 기준으로 구성합니다.

1. `Bootstrap Systems Root` 프리팹(또는 인스턴스)을 엽니다.
2. Save 시스템 하위 루트를 확인/생성합니다.
3. 컴포넌트를 추가합니다.
   - `SaveCoordinator`
   - `JsonFileSaveBackend`
4. `SaveCoordinator._backendComponent`에 `JsonFileSaveBackend`를 연결합니다.

### Step B. Policy 에셋 연결

1. `SaveChannelPolicy` 3개 생성 및 연결
   - Persistent (`save_persistent.json`)
   - Session (`save_session.json`)
   - Recovery (`save_recovery.json`)
2. `ForceClosePolicy` 생성/연결
3. `RecoveryPolicy` 생성/연결

### Step C. Participant 연결

1. 대상 시스템 오브젝트에 participant를 부착합니다.
2. Save/Load 검증 대상부터 우선 연결합니다.

### Step D. Recovery 동선 연결(선택)

1. `RecoveryService`를 복구 트리거 오브젝트에 연결합니다.
2. 필요 시 UI Button에 아래 메서드를 연결합니다.
   - `LoadPersistentFromUi()`
   - `LoadSessionFromUi()`
   - `RecoverNowFromUi()`

---

## 7) 저장 데이터 항목 추가/삭제 절차

## 7.1 항목 추가(Add)

### A. 사전 설계

1. 저장 목적 정의(유실 방지/복구 품질/UX)
2. 저장 단위 정의(단일 값/목록/참조)
3. 저장 채널 결정(Persistent/Session/Recovery)
4. 실패 폴백 정의(기본값 대체 기준)
5. 버전 호환 전략 정의

### B. 구현

1. `XxxSaveParticipant` + `ISaveParticipant` 구현
2. 고유 `ParticipantId` 지정(`domain.feature` 권장)
3. `PayloadVersion` 명시
4. `CanSave`로 채널 범위 제한
5. `CaptureAsJson` 최소 필드 저장
6. `RestoreFromJson` 안전 복원
   - null/빈 데이터 방어
   - 범위/유효성 검사
   - 기본값 폴백
   - 폴백 Warning 로그

### C. 검증

1. 저장 파일에 신규 participant 레코드 생성 확인
2. 빈 데이터/누락 데이터/잘못된 참조 로드 확인
3. 버전 불일치 및 슬롯 덮어쓰기 동작 확인
4. Play Mode 단계 로그 확인

## 7.2 항목 삭제(Remove)

1. Soft Remove 권장(`CanSave=false` 또는 빈 payload)
2. 한 버전 이상 운영 후 Hard Remove(코드/컴포넌트 삭제)
3. `ParticipantId` 재사용 금지
4. 복원 폴백 경로를 먼저 확보한 뒤 제거

---

## 8) 저장 파일 위치 / 파일 점검

`JsonFileSaveBackend` 기본 루트는 `Application.persistentDataPath`입니다.

경로:

`{Application.persistentDataPath}/{_saveFolderName}/{fileName}`

기본 예시:
- `{Application.persistentDataPath}/SaveData/save_persistent.json`
- `{Application.persistentDataPath}/SaveData/save_session.json`
- `{Application.persistentDataPath}/SaveData/save_recovery.json`

추가로 생성될 수 있는 파일:
- 임시 파일: `*.tmp`
- 백업 파일: `*.bak1`, `*.bak2` ...

운영 점검 시에는 본 파일과 백업 파일을 함께 확인합니다.

---

## 9) 운영 체크리스트

- [ ] backend 연결 확인
- [ ] 채널 정책 3종 연결 확인
- [ ] `ForceClosePolicy` / `RecoveryPolicy` 연결 확인
- [ ] participant 중복 ID 없음
- [ ] 저장 파일 생성/갱신 확인
- [ ] Recovery 스냅샷 생성/복원 확인
- [ ] 씬 전환/일시정지/종료 트리거 저장 확인

---

## 10) 장애 대응 가이드

### 증상 A. 저장이 안 됨

1. `_backendComponent` 누락/타입 불일치 확인
2. `_channelPolicies` 누락 확인
3. participant 등록 상태 확인
4. 파일 경로/권한 로그 확인

### 증상 B. 복원이 안 됨

1. 채널 파일 존재 여부 확인
2. `ParticipantId` 변경 이력 확인
3. `RecoveryPolicy` 차단 여부 확인
4. payload 구조 변경 호환성 확인

### 증상 C. 강제 종료 시 유실

1. 종료 저장만 의존하고 있지 않은지 확인
2. 중요 이벤트 직후 저장 추가 여부 확인
3. 주기 저장 간격 과다 여부 확인

---

## 11) 유지보수 규칙

- 정책 변경은 코드보다 ScriptableObject 변경을 우선
- 릴리즈 이후 `ParticipantId` 변경 금지
- payload 구조 변경 시 버전/마이그레이션 전략 필수
- 백업 개수(`BackupCount`) 최소 1 이상 권장

---

## 12) SaveSystem 제거 절차

### 레벨 1) 즉시 비활성화
- `SaveCoordinator` GameObject 비활성화
- 또는 `ForceClosePolicy` 자동 저장 옵션 off

### 레벨 2) 기능 축소
- 특정 participant 제거
- 특정 채널 정책 제거

### 레벨 3) 완전 해체
1. SaveSystem 관련 컴포넌트 제거
2. 정책 에셋 정리
3. 코드 폴더 정리
4. 필요 시 저장 파일 정리

---

## 13) Rule Set / 도메인별 심화 연동

### 13.1 RecoveryPolicy Rule Set 연결

1. `Create > Game > Save System > Rule Sets`에서 에셋 생성
2. `RecoveryPolicy`의 Rule Set References에 연결
3. participant에서 `RecoveryPolicyRuleHelper` 기반으로 복원 허용 판단

### 13.2 체크포인트 연동

- `CheckpointDefinition`, `CheckpointCatalog` 구성
- `PlayerCheckpointSaveParticipant` 저장/복원 연결
- `StageSpawnResolver`에 카탈로그 연결

### 13.3 Enemy 연동

- `EnemyResetRuleSet` 규칙 구성
- `EnemyRuntimeStateSaveParticipant` 연결
- 대량 저장 필터(활성/태그/거리) 사용

### 13.4 기믹 연동

- `IGimmickStateProvider` 구현
- `GimmickStateSaveParticipant` 연결
- 복원 시점(`AfterSceneLoad`, `AfterPlayerSpawn`) 설정

### 13.5 아이템/픽업 연동

- `ItemRestoreRuleSet` 구성
- `WorldPickupState` 메타 구성
- `PickupStateSaveParticipant` 연결
- `Tools/SaveSystem/Validate PickupId Duplicates`로 중복 검사

### 13.6 보스전 연동

- `BossRestartRuleSet` 구성
- `BossEncounterRuntime`, `BossEncounterStateSaveParticipant` 연결
- Recovery 후 아레나 초기화 타이밍 고정

---

## 14) 빠른 작업 템플릿

### 도입 템플릿
- [ ] Coordinator + Backend 연결
- [ ] Policy 3종 연결
- [ ] participant 부착
- [ ] Save/Load 1회 검증

### 제거 템플릿
- [ ] 비활성/축소/완전해체 레벨 선택
- [ ] 컴포넌트/정책 정리
- [ ] 저장 파일 정리 여부 결정

### 확장 우선순위 템플릿
1. Player Position
2. Inventory
3. Quest Runtime
4. Skill/Ability Cooldown
5. World Interactive State

---

참고: 체크포인트 테스트 씬 구성 상세 가이드는 `Assets/_Workspace/ParkJaeBok/Scripts/StageFlow/Checkpoint_Test_Scene_Setup_Guide.md`를 확인하세요.