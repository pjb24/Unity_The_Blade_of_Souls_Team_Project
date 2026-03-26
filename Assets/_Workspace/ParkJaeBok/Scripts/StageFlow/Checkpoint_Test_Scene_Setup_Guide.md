# 체크포인트 기능 테스트 씬 구성 가이드 (상세)

이 문서는 **체크포인트 저장/복구(Recovery) 흐름**을 테스트하기 위한 Unity 테스트 씬을 처음부터 구성하는 방법을 단계별로 설명합니다.

---

## 0) 목표와 검증 범위

이 테스트 씬의 목표는 아래 5가지를 재현 가능한 절차로 검증하는 것입니다.

1. 플레이어가 체크포인트를 밟으면 체크포인트 식별자(`checkpointId`)가 저장된다.
2. Recovery 복구 시 마지막 체크포인트 기준으로 플레이어 스폰 위치가 결정된다.
3. `CheckpointDefinition.entryPointId`가 유효하면 `StageEntryPoint` 위치를 우선 사용한다.
4. `entryPointId`가 유효하지 않으면 `CheckpointDefinition.worldPosition`으로 안전 폴백한다.
5. `CheckpointRuleSet`으로 체크포인트별 복구 허용/차단이 동작한다.

검증 대상 주요 컴포넌트:

- `StageSession`
- `StageSpawnResolver`
- `StageEntryPoint`
- `CheckpointDefinition`, `CheckpointCatalog`
- `PlayerCheckpointSaveParticipant`
- `SaveCoordinator`, `RecoveryService`

---

## 1) 사전 준비

## 1.1 테스트 씬 생성

1. Unity에서 새 씬을 생성합니다.
2. 씬 이름을 `CheckpointSystemTest`로 저장합니다.
3. `File > Build Settings`에서 `Scenes In Build`에 추가합니다.

## 1.2 태그 준비

- 플레이어 루트 오브젝트 태그를 `Player`로 설정합니다.

## 1.3 권장 폴더(에셋) 구조

테스트 자산을 분리해 두면 정리/삭제가 쉬워집니다.

- `Assets/_Workspace/ParkJaeBok/Scripts/StageFlow/Test/`
  - `CheckpointDefinition`, `CheckpointCatalog`
- `Assets/_Workspace/ParkJaeBok/Scripts/SaveSystem/Test/`
  - `SaveChannelPolicy`, `ForceClosePolicy`, `RecoveryPolicy`, `CheckpointRuleSet`

---

## 2) Hierarchy 구성 (오브젝트 생성 순서)

아래 순서대로 빈 GameObject를 생성하고 컴포넌트를 부착하면 설정 누락을 줄일 수 있습니다.

## 2.1 `--- Services ---`

### (A) `SceneTransitionService`

권장 Inspector 값:

- `Dont Destroy On Load`: `true`
- `Fade Out Duration`: `0.0 ~ 0.2`
- `Fade In Duration`: `0.0 ~ 0.2`
- `Block Input While Transition`: `false` (테스트 중 위치 확인 편의)

### (B) `StageSpawnResolver`

권장 Inspector 값:

- `Checkpoint Catalog`: (4장에서 생성할 `CC_CheckpointSystemTest` 연결)
- `Use Spawn Target Marker`: `false`
- `Allow Tag Fallback`: `true`
- `Player Tag`: `Player`
- `Max Resolve Retry Count`: `20`
- `Resolve Retry Interval`: `0.1`

> 핵심: Recovery 시점에 `StageSpawnResolver`가 마지막 체크포인트를 해석해 플레이어 위치를 결정합니다.

## 2.2 `--- Save System ---`

부착 컴포넌트:

1. `SaveCoordinator`
2. `JsonFileSaveBackend`
3. `RecoveryService`

### `SaveCoordinator` 권장 Inspector 값

- `Backend Component`: `JsonFileSaveBackend`
- `Auto Register Participants`: `true`
- `Refresh Participants After Scene Load`: `true`
- `Auto Load On Start`: `true`
- `Channel Policies`: Persistent / Session / Recovery 3개 모두 연결
- `Force Close Policy`: 테스트용 에셋 연결
- `Recovery Policy`: 테스트용 에셋 연결

### `RecoveryService` 권장 Inspector 값

- `Load Recovery On Enable`: `false` (수동 복구 테스트)
- `Log Ui Test Result`: `true`
- `Inspector Persistent Load Trigger Context`: `Inspector.LoadPersistent`
- `Inspector Session Load Trigger Context`: `Inspector.LoadSession`
- `Inspector Recovery Load Trigger Context`: `Inspector.LoadRecovery`

### `RecoveryService` Inspector 로드 테스트 절차

`RecoveryService` 컴포넌트 우측 메뉴(⋮ 또는 우클릭 Context Menu)에서 아래 항목으로 테스트합니다.

1. `Test Load/Persistent`
   - Persistent 파일 로드 성공 여부를 콘솔 로그로 확인
2. `Test Load/Session`
   - Session 파일 로드 성공 여부를 콘솔 로그로 확인
3. `Test Load/Recovery`
   - Recovery 로드 + deferred restore 적용(보스/기믹) 여부를 확인

권장 확인 포인트:

- 콘솔 로그: `[RecoveryService] UI Load Result channel=..., success=...`
- Recovery 테스트 후 플레이어 위치가 마지막 체크포인트 복구 정책과 일치하는지 확인
- 필요 시 `Inspector ... Trigger Context` 값을 테스트 목적별 문자열로 바꿔 로그 추적

## 2.3 `--- Stage Session ---`

부착 컴포넌트:

1. `StageSession`
2. `StageSessionSaveParticipant`

권장값:

- `StageSession.Dont Destroy On Load`: `true`
- `StageSessionSaveParticipant.Respect Recovery Policy`: `true`

## 2.4 `--- Player ---`

필수:

- `Tag`: `Player`
- 시작 위치 예시: `(0, 0, 0)`

부착 컴포넌트:

1. 기존 이동/입력 컴포넌트
2. `PlayerCheckpointSaveParticipant`
3. (선택) `HealthComponent`
4. (선택) `HealthSaveParticipant`

`PlayerCheckpointSaveParticipant` 권장값:

- `Participant Id`: `player.checkpoint`
- `Player Transform`: 플레이어 루트 Transform
- `Default Checkpoint Id`: `CP_A`
- `Respect Recovery Policy`: `true`

## 2.5 Entry Point 오브젝트 3개

### `EntryPoint_Default`

- 컴포넌트: `StageEntryPoint`
- `Entry Point Id`: `Default`
- `Is Fallback Point`: `true`
- 위치 예시: `(-8, 0, 0)`

### `EntryPoint_CP_A`

- 컴포넌트: `StageEntryPoint`
- `Entry Point Id`: `CP_A_ENTRY`
- `Is Fallback Point`: `false`
- 위치 예시: `(-2, 0, 0)`

### `EntryPoint_CP_B`

- 컴포넌트: `StageEntryPoint`
- `Entry Point Id`: `CP_B_ENTRY`
- `Is Fallback Point`: `false`
- 위치 예시: `(6, 0, 0)`

## 2.6 체크포인트 트리거 2개

오브젝트 예시:

- `CheckpointTrigger_A`
- `CheckpointTrigger_B`

권장 구성:

- `BoxCollider2D` (`Is Trigger = true`)
- `CheckpointTriggerReporter` (본 문서와 함께 추가된 트리거 전용 스크립트)

권장 위치:

- A: `(-2, 0, 0)`
- B: `(6, 0, 0)`

## 2.7 `CheckpointTriggerReporter` 설정 방법

각 트리거 오브젝트에 `CheckpointTriggerReporter`를 붙이고 아래처럼 설정합니다.

### `CheckpointTrigger_A`

- `Checkpoint Id`: `CP_A`
- `Trigger Once`: `true`
- `Player Tag`: `Player`
- `Search Participant In Parent`: `true`
- `Search Participant In Children`: `true` (participant가 Player의 자식에 붙은 구조 지원)
- `Save Recovery After Trigger`: `false` (원하면 `true`로 즉시 Recovery 스냅샷 저장 가능)
- `Save Trigger Context`: `CheckpointTrigger.A`

### `CheckpointTrigger_B`

- `Checkpoint Id`: `CP_B`
- `Trigger Once`: `true`
- `Player Tag`: `Player`
- `Search Participant In Parent`: `true`
- `Search Participant In Children`: `true`
- `Save Recovery After Trigger`: `false`
- `Save Trigger Context`: `CheckpointTrigger.B`

### 동작 원리 요약

1. 트리거 진입 시 충돌 오브젝트 태그가 `Player`인지 검사
2. `PlayerCheckpointSaveParticipant`를 자신/부모/자식에서 순차 탐색
3. 찾은 participant에 `ReportCheckpointReached(checkpointId)` 호출
4. 옵션 활성화 시 `Recovery` 채널 저장까지 즉시 수행

> 플레이어 콜라이더가 자식 오브젝트에 있는 구조라면 `Search Participant In Parent=true`를 유지하세요.

---

## 3) ScriptableObject 생성 및 값 입력

## 3.1 StageFlow 자산

## 3.1.1 `CheckpointDefinition` 2개 생성

메뉴:

- `Create > Game > Stage Flow > Checkpoint Definition`

파일:

- `CD_CP_A`
- `CD_CP_B`

### `CD_CP_A` 권장값

- `checkpointId`: `CP_A`
- `sceneName`: `CheckpointSystemTest`
- `entryPointId`: `CP_A_ENTRY`
- `worldPosition`: `(-2, 0, 0)`
- `priority`: `10`
- `tags`: `safe`, `start`

### `CD_CP_B` 권장값

- `checkpointId`: `CP_B`
- `sceneName`: `CheckpointSystemTest`
- `entryPointId`: `CP_B_ENTRY`
- `worldPosition`: `(6, 0, 0)`
- `priority`: `20`
- `tags`: `combat`, `mid`

## 3.1.2 `CheckpointCatalog` 생성

메뉴:

- `Create > Game > Stage Flow > Checkpoint Catalog`

권장값:

- 파일명: `CC_CheckpointSystemTest`
- `Checkpoints`: `CD_CP_A`, `CD_CP_B` 추가

연결 위치:

- `StageSpawnResolver.Checkpoint Catalog`에 `CC_CheckpointSystemTest` 연결

## 3.2 SaveSystem 정책 자산

## 3.2.1 `SaveChannelPolicy` 3종

메뉴:

- `Create > Game > Save System > Save Channel Policy`

권장 설정:

1. `SCP_Persistent`
   - `ChannelType`: `Persistent`
   - `FileName`: `save_persistent.json`
2. `SCP_Session`
   - `ChannelType`: `Session`
   - `FileName`: `save_session.json`
3. `SCP_Recovery`
   - `ChannelType`: `Recovery`
   - `FileName`: `save_recovery.json`

## 3.2.2 `ForceClosePolicy`

메뉴:

- `Create > Game > Save System > Force Close Policy`

권장값(테스트 단순화):

- `Use Periodic Save`: `false`
- `SavePersistentOnSceneTransition`: `true`
- `SaveSessionOnSceneTransition`: `true`

## 3.2.3 `RecoveryPolicy`

메뉴:

- `Create > Game > Save System > Recovery Policy`

권장값:

- `Snapshot On Death`: `true`
- `Restore Health`: `true`
- `Restore Stage Session`: `true`
- `Checkpoint Rule Set`: `RS_Checkpoint_Test` 연결

## 3.2.4 `CheckpointRuleSet`

메뉴:

- `Create > Game > Save System > Rule Sets > Checkpoint Rule Set`

권장값:

- 파일명: `RS_Checkpoint_Test`
- `Default Restore`: `true`
- `Overrides`
  - `CP_A`: `Allow Restore = true`
  - `CP_B`: `Allow Restore = true`

---

## 4) 필수 연결 체크리스트 (Play 전 최종 확인)

`SaveCoordinator`:

- `Backend Component` 연결됨
- `Channel Policies` 3개 연결됨
- `Force Close Policy` 연결됨
- `Recovery Policy` 연결됨
- `Auto Register Participants = true`

`StageSpawnResolver`:

- `Checkpoint Catalog` 연결됨
- `Player Tag = Player`

`PlayerCheckpointSaveParticipant`:

- `Player Transform` 연결됨
- `Default Checkpoint Id` 입력됨

`CheckpointCatalog`:

- `CD_CP_A`, `CD_CP_B`가 등록됨

---

## 5) 테스트 시나리오 (실행 절차 + 합격 기준)

## 시나리오 A: 기본 스폰 검증

절차:

1. `StageSession.TargetStageEntryPointId`가 비어있는 상태로 Play 시작
2. 씬 시작 직후 플레이어 위치 확인

합격 기준:

- 플레이어가 `EntryPoint_Default` 위치(또는 Fallback 정책 위치)에 배치됨

## 시나리오 B: CP_A 저장/복구

절차:

1. 플레이어를 `CheckpointTrigger_A`와 충돌시킴
2. 체크포인트 기록 로그 또는 상태 반영 확인
3. `RecoveryService`의 UI/디버그 호출로 Recovery 로드 수행

합격 기준:

- 플레이어가 `CP_A_ENTRY` 위치로 복구됨

## 시나리오 C: CP_B 우선 복구

절차:

1. 플레이어를 `CheckpointTrigger_B`와 충돌시킴
2. 저장/복구 실행

합격 기준:

- 마지막 체크포인트가 CP_B로 기록되고, 플레이어가 `CP_B_ENTRY`로 복구됨

## 시나리오 D: `entryPointId` 폴백 검증

절차:

1. `CD_CP_B.entryPointId`를 존재하지 않는 값(예: `NOT_FOUND_EP`)으로 변경
2. 다시 CP_B 저장 후 Recovery 수행

합격 기준:

- `StageEntryPoint` 조회 실패 후 `CD_CP_B.worldPosition`으로 복구됨

## 시나리오 E: RuleSet 차단 검증

절차:

1. `RS_Checkpoint_Test`에서 `CP_B.Allow Restore = false`로 변경
2. CP_B를 마지막으로 저장 후 Recovery 수행

합격 기준:

- CP_B 복구가 차단되고, 정책에 따른 대체 위치(이전 허용 체크포인트 또는 fallback)로 복구됨

---

## 6) 디버깅 포인트

## 6.1 로그로 먼저 확인할 항목

- `StageSpawnResolver`
  - 플레이어 탐색 실패 로그
  - EntryPoint 탐색 실패 로그
- `RecoveryService`
  - Recovery 적용 시점 로그
- `SaveCoordinator`
  - Participant 등록/복원 로그

## 6.2 런타임 상태 확인

- `StageSession.LastCheckpointId`
- `StageSession.LastCheckpointSceneName`
- `StageSession.UseCheckpointForNextSpawn`

## 6.3 파일 확인

Recovery 파일 경로 예시:

- `Application.persistentDataPath/SaveData/save_recovery.json`

확인 포인트:

- 체크포인트 ID가 마지막 트리거 값과 일치하는지
- 손상된 데이터 없이 JSON이 정상 직렬화됐는지

---

## 7) 실패 케이스별 빠른 원인 진단

1. **항상 Default 위치로만 스폰됨**
   - `CheckpointCatalog` 미연결
   - `checkpointId` 오타 (`CP_B` vs `Cp_B`)
   - 트리거에서 `ReportCheckpointReached` 호출 누락

2. **Recovery 호출 후 위치가 안 바뀜**
   - `RecoveryPolicy` 또는 `CheckpointRuleSet`에서 복구 차단
   - `PlayerCheckpointSaveParticipant` 미등록/비활성

3. **EntryPoint를 못 찾고 worldPosition만 사용됨**
   - `entryPointId`와 실제 `StageEntryPoint.Entry Point Id` 불일치

4. **씬 재진입 시 상태가 다르게 보임**
   - Participant 자동 등록 타이밍 문제
   - 씬 로드 후 복구 적용 타이밍(AfterSceneLoad/AfterPlayerSpawn) 확인 필요

---

## 8) 권장 회귀 테스트 세트

릴리즈 전 최소 아래 4개는 반복 실행을 권장합니다.

- `CP_A` 복구 성공
- `CP_B` 복구 성공
- `Invalid entryPointId` 폴백 성공
- `CP_B restore blocked` 정책 차단 성공

이 4개가 모두 안정적으로 통과하면 체크포인트 기반 복귀 흐름의 핵심 리스크를 대부분 커버할 수 있습니다.
