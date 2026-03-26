# SaveSystem 설치/제거 작업 가이드

이 문서는 프로젝트에서 **저장 시스템(SaveSystem)을 자유롭게 추가/제거**하기 위한 작업 순서와 체크리스트를 제공합니다.

---

## 1) 목표

- 저장 시스템을 **모듈 단위로 붙였다 떼는 것**이 가능해야 합니다.
- 기존 게임 로직(StageFlow, Health 등)을 최대한 건드리지 않고 확장/축소할 수 있어야 합니다.
- 저장 채널(영구/세션/복구)과 강제 종료 기준은 **외부 데이터(ScriptableObject Policy)** 로 제어해야 합니다.

---

## 2) SaveSystem 구성요소 요약

### 2.1 Core
- `E_SaveChannelType`
- `E_SaveTriggerType`
- `SaveSnapshot`, `SaveParticipantRecord`, `SaveContext`
- `ISaveParticipant`

### 2.2 Data (외부 정책)
- `SaveChannelPolicy`
- `ForceClosePolicy`
- `RecoveryPolicy`

### 2.3 Runtime
- `SaveCoordinator`
- `RecoveryService`

### 2.4 Backends
- `ISaveBackend`
- `JsonFileSaveBackend`

### 2.5 Participants
- `StageSessionSaveParticipant`
- `HealthSaveParticipant`
- (확장) 인벤토리/퀘스트/플레이어 위치 participant 추가 가능

---

## 2.6 저장 파일 위치 안내

`JsonFileSaveBackend`는 Unity의 `Application.persistentDataPath`를 루트로 사용합니다.

실제 저장 경로:

`{Application.persistentDataPath}/{_saveFolderName}/{fileName}`

- `_saveFolderName` 기본값: `SaveData`
- `fileName` 값: 각 `SaveChannelPolicy.FileName`에서 설정
  - 예: `save_persistent.json`, `save_session.json`, `save_recovery.json`

즉 기본 설정에서는 아래와 같은 형태가 됩니다.

- `{Application.persistentDataPath}/SaveData/save_persistent.json`
- `{Application.persistentDataPath}/SaveData/save_session.json`
- `{Application.persistentDataPath}/SaveData/save_recovery.json`

### 플랫폼별 확인 팁

- **Windows(Editor/Standalone)**: `Application.persistentDataPath`를 런타임 로그로 출력해 실제 경로 확인
- **Android/iOS**: 디바이스 샌드박스 내부 경로를 사용하므로 ADB/Xcode 도구로 확인
- **콘솔/기타 플랫폼**: 플랫폼 SDK 정책에 따라 접근 가능 경로가 달라질 수 있음

### 백업/임시 파일

`JsonFileSaveBackend` 설정에 따라 다음 파일이 함께 생성될 수 있습니다.

- 임시 파일: `*.tmp` (원자적 저장 시 사용)
- 백업 파일: `*.bak1`, `*.bak2` ... (롤링 백업)

운영 점검 시에는 **본 파일 + 백업 파일**을 같이 확인해야 데이터 유실 원인을 파악하기 쉽습니다.

---

## 3) SaveSystem **추가(도입)** 작업 절차

아래 순서를 그대로 수행하면 가장 안전합니다.

### Step A. 런타임 오브젝트 구성

1. 빈 GameObject 생성 (권장 이름: `--- Save System ---`)
2. 아래 컴포넌트 추가
   - `SaveCoordinator`
   - `JsonFileSaveBackend`
3. `SaveCoordinator._backendComponent` 에 `JsonFileSaveBackend` 연결

### Step B. Policy 에셋 생성/연결

1. `Create > Game > Save System > Save Channel Policy` 로 3개 생성
   - Persistent 용 (`save_persistent.json`)
   - Session 용 (`save_session.json`)
   - Recovery 용 (`save_recovery.json`)
2. `Create > Game > Save System > Force Close Policy` 생성
3. `Create > Game > Save System > Recovery Policy` 생성
4. `SaveCoordinator._channelPolicies` 에 채널 정책 3개 연결
5. `SaveCoordinator._forceClosePolicy`, `_recoveryPolicy` 연결

### Step C. Participant 연결

1. StageSession 오브젝트에 `StageSessionSaveParticipant` 추가
2. 체력 저장 대상 오브젝트에 `HealthSaveParticipant` 추가
3. 필요 시 다른 시스템 participant를 추가 구현

### Step D. 복구 실행 경로 연결 (선택)

1. 복구 트리거 오브젝트(예: Respawn UI, GameOver Presenter)에 `RecoveryService` 추가
2. 사망 후 복귀 버튼 등에서 `RecoveryService.TryRecoverNow()` 호출

### Step E. UI 테스트 버튼 연결 (선택)

Recovery 테스트가 어려운 경우, `RecoveryService`의 아래 메서드를 버튼에 연결해 채널 Load를 수동 검증할 수 있습니다.

- `LoadPersistentFromUi()` : Persistent 파일 로드
- `LoadSessionFromUi()` : Session 파일 로드
- `RecoverNowFromUi()` : Recovery 파일 로드

---

## 4) SaveSystem **제거(비활성/해체)** 작업 절차

운영 중 롤백/실험 해제를 위해 아래 3가지 레벨로 제거할 수 있습니다.

### 레벨 1) 즉시 비활성화(권장)

- `SaveCoordinator` GameObject를 비활성화
- 또는 `ForceClosePolicy`에서 자동 저장 옵션 비활성

장점: 코드/에셋 유지, 즉시 복구 가능.

### 레벨 2) 기능 축소 제거

- 특정 participant 컴포넌트만 제거
  - 예: `HealthSaveParticipant` 제거 → 체력 저장 제외
- `SaveCoordinator._channelPolicies`에서 특정 채널 정책 제거
  - 예: Recovery 정책 제거 → 사망 복구 저장 미사용

장점: 필요한 기능만 남기고 단계적 정리 가능.

### 레벨 3) 완전 해체

1. Scene/Prefab에 붙은 SaveSystem 컴포넌트 전부 제거
   - `SaveCoordinator`, `JsonFileSaveBackend`, `RecoveryService`
   - 모든 `ISaveParticipant` 구현 컴포넌트
2. SaveSystem Policy 에셋 제거
3. SaveSystem 코드 폴더 제거
4. (필요 시) `Application.persistentDataPath/SaveData` 저장 파일 정리

주의: 완전 해체 전, 저장 파일 마이그레이션/백업 공지 필요.

---

## 5) 새 시스템을 SaveSystem에 연결하는 표준 패턴

### 구현 규칙

1. 대상 컴포넌트에 `ISaveParticipant` 구현체를 추가
2. `ParticipantId`는 고유 문자열로 부여 (예: `core.inventory`)
3. `CaptureAsJson`에서 상태 직렬화
4. `RestoreFromJson`에서 상태 복원
5. Recovery 채널일 때는 `RecoveryPolicy` 플래그를 존중

### 권장 네이밍

- `XxxSaveParticipant`
- payload 클래스: `XxxPayload`
- participant id: `domain.feature` (예: `player.position`, `quest.runtime`)

---

## 6) 운영 체크리스트 (도입/변경 후)

- [ ] `SaveCoordinator`에 backend 연결됨
- [ ] 채널 정책(Persistent/Session/Recovery) 누락 없음
- [ ] `ForceClosePolicy`, `RecoveryPolicy` 연결됨
- [ ] participant 중복 ID 없음
- [ ] 저장 파일 생성 확인 (`persistentDataPath/SaveData`)
- [ ] 사망 시 Recovery 스냅샷 생성 확인
- [ ] 씬 전환/일시정지/종료 훅에서 저장 로그 확인

---

## 7) 장애 대응 가이드

### 증상 A. 저장이 안 됨

확인 순서:
1. `SaveCoordinator._backendComponent` 누락 여부
2. 채널 정책 누락 여부
3. participant 등록 여부 (`RefreshParticipants`)
4. backend 경로 권한/파일 접근 예외 로그

### 증상 B. 복원이 안 됨

확인 순서:
1. 해당 채널 파일 존재 여부
2. `ParticipantId` 변경 이력 여부
3. `RecoveryPolicy`가 복원 플래그를 막고 있지 않은지
4. payload 필드 이름 변경으로 JSON 역직렬화가 깨지지 않았는지

### 증상 C. 강제 종료 시 유실

확인 순서:
1. 종료 시점 저장에만 의존하고 있는지
2. 중요 이벤트(획득/강화/클리어) 직후 저장을 추가했는지
3. 주기 저장 주기가 과도하게 긴지

---

## 8) 유지보수 규칙

- 정책 변경은 코드 수정 대신 ScriptableObject 값 변경으로 우선 처리
- participant ID는 릴리즈 이후 변경 금지(호환성 이슈)
- payload 구조 변경 시 버전 증가(`PayloadVersion`)와 마이그레이션 고려
- 저장 파일 손상 대비로 백업 개수(`BackupCount`) 최소 1 이상 권장

---

## 9) 빠른 작업 템플릿

### 도입 템플릿
- [ ] SaveSystem 오브젝트 생성
- [ ] Coordinator + Backend 부착
- [ ] Policy 3종 생성/연결
- [ ] participant 부착
- [ ] 1회 수동 저장/로드 검증

### 제거 템플릿
- [ ] 비활성화/축소/완전해체 중 레벨 선택
- [ ] 연결 컴포넌트 제거
- [ ] 정책 에셋 정리
- [ ] 잔여 저장 파일 정리 여부 결정

---

## 10) 권장 확장 순서

1. Player Position participant
2. Inventory participant
3. Quest Runtime participant
4. Skill/Ability Cooldown participant
5. World Interactive State participant

위 순서로 확장하면 체감 품질(유실 방지)을 빠르게 높일 수 있습니다.

---

## 11) 저장 데이터 항목을 **자유롭게 늘리고/줄이는** 작업 가이드

이 섹션은 “SaveSystem 자체를 붙였다 떼는 것”이 아니라,  
**SaveSystem이 관리하는 데이터 범위를 조절**하는 실무 절차를 다룹니다.

### 11.1 데이터 항목을 늘리는 작업 (Add)

#### A. 대상 데이터 정의

1. 저장하려는 데이터를 분류합니다.
   - 영구(Persistent): 해금, 재화, 퀘스트 진행
   - 세션(Session): 현재 스테이지 문맥, 임시 버프
   - 복구(Recovery): 사망 후 되돌릴 최소 상태
2. 해당 데이터의 소유 컴포넌트(예: Inventory, QuestRuntime)를 확정합니다.

#### B. Participant 추가

1. `XxxSaveParticipant`를 생성하고 `ISaveParticipant`를 구현합니다.
2. `ParticipantId`를 고유하게 부여합니다. (권장: `domain.feature`)
3. `CaptureAsJson`에 필요한 필드만 직렬화합니다.
4. `RestoreFromJson`에 안전 복원(널/범위 체크, 기본값 폴백)을 넣습니다.

#### C. 채널/정책 반영

1. 어떤 채널에서 저장할지 `CanSave` 조건으로 제한합니다.
2. 복구 대상이면 `RecoveryPolicy` 플래그를 확인해 복원 여부를 제어합니다.
3. 강제 종료 대응이 필요하면 `ForceClosePolicy` 주기/수명주기 옵션을 조정합니다.

#### D. 검증

1. 저장 파일에 participant 레코드가 생성되는지 확인합니다.
2. 씬 재진입/앱 재실행/사망 복구 각각에서 데이터가 기대대로 복원되는지 확인합니다.
3. 이전 저장 파일과의 호환성을 확인합니다. (필드 추가 시 기본값 동작 점검)

---

### 11.2 데이터 항목을 줄이는 작업 (Remove)

#### A. 축소 방식 선택

1. **Soft Remove(권장)**: participant는 남기고 `CanSave=false` 또는 빈 payload 처리
2. **Hard Remove**: participant 컴포넌트/코드 자체 제거

운영 중에는 Soft Remove로 한 버전 이상 유지 후 Hard Remove를 권장합니다.

#### B. 안전 제거 절차

1. 제거 대상 `ParticipantId`를 문서화합니다.
2. 복원 경로에서 해당 데이터가 없어도 동작하도록 기본값 폴백을 먼저 반영합니다.
3. 배포 1차: 저장 중단(Soft Remove), 복원 폴백 확인
4. 배포 2차: 코드/컴포넌트 삭제(Hard Remove)

#### C. 주의사항

- `ParticipantId`를 재사용하지 않습니다. (과거 저장 데이터 오인식 방지)
- payload 구조를 삭제하기 전, 최소 1회 마이그레이션 기간을 둡니다.
- 복구 채널(Recovery)에서 제거 시 사망 후 흐름(GameOver/Respawn) 회귀 테스트를 수행합니다.

---

### 11.3 payload 버전 관리 규칙

데이터 증감 과정에서 가장 중요한 것은 **복원 호환성**입니다.

1. 필드 추가: 기존 JSON에 없어도 기본값으로 복원 가능해야 함
2. 필드 삭제: 복원 코드가 해당 필드 부재/잔존 모두 허용해야 함
3. 의미 변경: `PayloadVersion` 증가 + 분기 복원 로직 추가 권장

권장 순서:
- (1) 복원 로직을 먼저 확장(구버전 허용)
- (2) 저장 포맷 변경 배포
- (3) 충분한 기간 후 구버전 분기 정리

---

### 11.4 작업 체크리스트 (데이터 증감 전용)

- [ ] 추가/삭제 대상 데이터가 어떤 채널인지 확정됨
- [ ] `ParticipantId` 충돌/재사용 없음
- [ ] Restore 폴백(기본값/널 안전) 구현됨
- [ ] RecoveryPolicy 영향 확인됨
- [ ] 기존 저장 파일로 로드 회귀 테스트 완료
- [ ] 사망/씬전환/재실행 시나리오 확인 완료

---

## Rule Set 기반 RecoveryPolicy 연결 절차

`RecoveryPolicy`는 기존 bool 플래그(하위호환)와 함께 Rule Set 참조를 사용할 수 있습니다.

### 1) Rule Set 에셋 생성

`Create > Game > Save System > Rule Sets` 하위 메뉴에서 다음 에셋을 생성합니다.

- `Checkpoint Rule Set`
- `Enemy Reset Rule Set`
- `Gimmick Restore Rule Set`
- `Item Restore Rule Set`
- `Boss Restart Rule Set`

각 Rule Set은 아래 공통 구조를 가집니다.

- `Default Restore`: 오버라이드 미적용 시 기본 동작
- `Overrides`: `ID + Allow Restore` 목록

### 2) RecoveryPolicy에 연결

`RecoveryPolicy` 인스펙터의 `Rule Set References`에 생성한 에셋을 연결합니다.

- `Checkpoint Rule Set`
- `Enemy Reset Rule Set`
- `Gimmick Restore Rule Set`
- `Item Restore Rule Set`
- `Boss Restart Rule Set`

### 3) Participant에서 공통 헬퍼 사용

Recovery 채널 복원 시에는 `RecoveryPolicyRuleHelper`를 사용해 복원 허용 여부를 판단합니다.

- 체력 복원: `ShouldRestoreHealth(recoveryPolicy, subjectId)`
- StageSession 복원: `ShouldRestoreStageSession(recoveryPolicy, subjectId)`
- 체크포인트/적/기믹/아이템/보스: 대응 `ShouldRestore...` 메서드 사용

### 4) 하위호환 동작

- Rule Set이 비어 있으면 기존 bool 플래그(`RestoreHealth`, `RestoreStageSession`)를 폴백으로 사용합니다.
- 새 도메인(체크포인트/적/기믹/아이템/보스)은 Rule Set 미설정 시 기본값(true)을 사용합니다.

---

## 체크포인트 레이어 연동 절차 (StageEntryPoint 재사용)

스테이지 진입점 기반 스폰을 유지하면서 체크포인트 복원을 적용하려면 아래 순서로 연결합니다.

1. `CheckpointDefinition`, `CheckpointCatalog` 에셋을 생성합니다.
   - `checkpointId`, `sceneName`, `entryPointId`, `worldPosition`, `priority`, `tags`를 설정합니다.
2. `StageSession`은 `LastCheckpointId/SceneName/WorldPosition`과 `UseCheckpointForNextSpawn`를 저장/복원합니다.
3. 플레이어 오브젝트에 `PlayerCheckpointSaveParticipant`를 추가합니다.
   - 저장: 체크포인트 ID/좌표/씬
   - 복원: Recovery 채널에서 `RecoveryPolicyRuleHelper.ShouldRestoreCheckpoint` 통과 시 세션 반영
4. `StageSpawnResolver`에 `CheckpointCatalog`를 연결합니다.
   - 스폰 우선순위: Recovery 체크포인트 -> StageSession EntryPoint -> Fallback EntryPoint
5. `CheckpointDefinition.entryPointId`는 기존 `StageEntryPoint` ID를 그대로 참조해 재사용합니다.

---

## Enemy 상태 복원/리셋 RuleSet 연동 절차

`EnemyResetRuleSet`은 `enemyRuntimeId` 또는 `archetypeId` 기준으로 규칙을 오버라이드할 수 있습니다.

### 1) EnemyResetRuleSet 설정

기본 규칙(Default)과 Override 규칙에서 다음 옵션을 설정합니다.

- `resetOnRecovery`
- `respawnIfDead`
- `restoreHpPercent`
- `restorePositionMode` (`Spawn`, `LastKnown`, `CheckpointArea`)

### 2) Enemy 식별자 구성

- `EnemyBrain`의 `enemyRuntimeId`를 사용해 개체 단위 규칙을 지정합니다.
- `EnemyArchetypeData.ArchetypeId`를 사용해 아키타입 단위 규칙을 지정합니다.

### 3) Participant 연결

- Enemy 오브젝트에 `EnemyRuntimeStateSaveParticipant`를 부착합니다.
- 저장 항목: 생사/체력 정규화/위치/로코모션/타겟 보유 여부
- 복원 시 규칙 해석: `RecoveryPolicyRuleHelper.ResolveEnemyResetRule(...)`

### 4) 대량 Enemy 저장 성능 필터

`EnemyResetRuleSet`의 Save 필터 옵션으로 저장 대상을 제한할 수 있습니다.

- 활성 Enemy만 저장
- 태그 기준 저장
- 플레이어 거리 기준 저장

---

## 기믹 상태 저장 모듈 표준화 절차

### 1) 기믹 상태 제공 인터페이스 구현

기믹 컴포넌트는 `IGimmickStateProvider`를 구현합니다.

- `string GimmickId`
- `string CaptureStateJson()`
- `void RestoreStateJson(string json)`

예시로 `ShieldGimmickModule`은 위 인터페이스를 구현해 보호막 강제 파괴 상태를 저장/복원합니다.

### 2) GimmickStateSaveParticipant 연결

씬(또는 SaveSystem 루트)에 `GimmickStateSaveParticipant`를 부착합니다.

- 저장 시: 씬의 `IGimmickStateProvider` 구현체를 자동 수집
- 복원 시: `GimmickRestoreRuleSet`의 ID 필터/시점 규칙을 적용

### 3) 복원 시점 제어

`GimmickRestoreRuleSet`에서 기믹별로 복원 시점을 지정할 수 있습니다.

- `AfterSceneLoad`: 씬 로드 직후 적용
- `AfterPlayerSpawn`: 플레이어 스폰 완료 이후 적용

시점별 적용 호출 경로:

- `SaveCoordinator.HandleAfterSceneLoad` -> `AfterSceneLoad` 지연 복원 적용
- `StageSpawnResolver` / `RecoveryService` -> `AfterPlayerSpawn` 지연 복원 적용

---

## 아이템 복원 RuleSet + 월드 픽업 상태 분리 절차

### 1) ItemRestoreRuleSet 구성

`ItemRestoreRuleSet`은 `itemType + itemId(pickupId)` 키를 기준으로 규칙을 해석합니다.

- `restoreCollectedState`
- `respawnAfterRecovery`
- `quantityRestoreMode`

### 2) 월드 픽업 메타 구성

월드 드랍/픽업 오브젝트에 `WorldPickupState`를 부착해 아래 메타를 부여합니다.

- `pickupId` (안정 ID)
- `itemType`
- `defaultQuantity`

### 3) PickupStateSaveParticipant 연결

`PickupStateSaveParticipant`를 씬(또는 SaveSystem 루트)에 부착합니다.

- 저장: 픽업별 획득 여부/수량
- 복원: `ItemRestoreRuleSet` 규칙으로 획득 상태/리스폰/수량 모드 적용

### 4) Inventory와 분리 저장 원칙

인벤토리 시스템이 있는 경우 저장 책임을 분리합니다.

- `PickupStateSaveParticipant`: 월드 상태(픽업 오브젝트)
- `InventorySaveParticipant`(별도 구현): 플레이어 소지 상태

### 5) pickupId 충돌 검사(에디터)

에디터 메뉴를 통해 중복 `pickupId`를 검사할 수 있습니다.

- `Tools/SaveSystem/Validate PickupId Duplicates`

---

## 보스전 복귀 RuleSet 데이터화 절차

### 1) BossRestartRuleSet 구성

`bossEncounterId` 키 기준으로 아래 옵션을 설정합니다.

- `restartMode` (`FullReset`, `PhaseCheckpoint`, `KeepDefeated`)
- `hpRestoreMode`
- `arenaReset`

### 2) 런타임 상태 발행 컴포넌트 연결

보스 오브젝트에 `BossEncounterRuntime`을 추가해 진입/이탈/처치/페이즈 체크포인트를 단일 진입점으로 관리합니다.

### 3) BossEncounterStateSaveParticipant 연결

`BossEncounterStateSaveParticipant`를 보스 오브젝트에 추가합니다.

- 저장: 현재 페이즈 인덱스, HP 정규화, 처치 여부, 전투 시작 여부
- 복원: `BossRestartRuleSet` 규칙을 기준으로 `BossPhaseController`, `BossPatternController`, 체력 상태에 반영

### 4) BossPhaseController 외부 주입 API

`BossPhaseController.SetPhaseIndexForRecovery(int)`를 사용해 복원 시 페이즈 인덱스를 외부에서 주입합니다.

### 5) Recovery 후 아레나 초기화 타이밍

`RecoveryService.TryRecoverNow()` 성공 직후 `BossEncounterStateSaveParticipant.ApplyDeferredArenaResetInScene()`를 호출해 아레나 초기화 타이밍을 고정합니다.


참고: 체크포인트 테스트 씬 구성 상세 가이드는 `Assets/_Workspace/ParkJaeBok/Scripts/StageFlow/Checkpoint_Test_Scene_Setup_Guide.md` 문서를 확인하세요.
