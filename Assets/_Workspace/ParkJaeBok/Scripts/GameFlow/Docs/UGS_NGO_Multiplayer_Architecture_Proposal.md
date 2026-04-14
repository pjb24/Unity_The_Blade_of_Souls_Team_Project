# UGS + NGO 기반 Host-Client 멀티플레이 아키텍처 제안

## 1. 목표와 제약
- **세션 모델**: Host-Client
- **매칭 방식**: 코드 교환(Join Code)
- **핵심 스택**: Unity Gaming Services(UGS) + Netcode for GameObjects(NGO)
- **서비스 목표**: 한 빌드에서 싱글플레이/멀티플레이를 모두 지원
- **개발 원칙**
  - 디자이너 친화적(Inspector 중심 세팅, ScriptableObject 데이터 주도)
  - 유지보수성(역할 분리, 상태 기반 흐름, 테스트 가능한 서비스 계층)
  - Unity 기본 기능 적극 활용(DontDestroyOnLoad, SceneManager, ScriptableObject, UnityEvent)
  - 기존 Repository 재사용 우선

### 1-1. 요구사항 기준선
1. 타이틀에서 `싱글플레이` / `멀티플레이`를 선택해야 함
2. Host가 멀티 세션을 생성하면 Town으로 이동하고 Join Code가 UI에 표시된다.
3. Town 이후 진행 스테이지 선택은 Host만 가능함
4. 멀티 세션 정원은 Host 포함 최대 2명
5. 멀티플레이에서 진행 가능한 Stage는 Host의 해금 상태를 기준으로 결정된다.
6. 멀티플레이로 클리어한 Stage는 Host와 Client 모두에게 클리어된 것으로 저장된다.
7. Stage 진행 중 중도 Join 금지함
8. Client 이탈 시 Host는 계속 진행한다.
9. Client 재접속은 30~60초 내 1회 허용되고, 성공 시 토큰이 재활성화되며 실패 시 타이틀로 이동한다.
10. Host 종료 시 세션 종료 + Host와 Client 모두 타이틀로 이동함
11. 플레이어 이름 미표시, `clientId`로만 식별함
12. 인벤토리는 플레이어별 분리, 로컬 저장만 수행함
13. UGS는 Relay 용도로만 사용함

---

## 2. 상세 요구사항 정리

### 2-1. 세션/입장 정책
- Host만 멀티 세션 시작 가능함
- 준비 완료 시스템(Ready)은 사용하지 않음
- Join Code는 UI에 노출함
- 멀티 인원은 최대 2명임(Host 1 + Client 1)
- Stage 진행 중 신규 Join 요청은 즉시 거부함

### 2-2. 연결 해제/재접속 정책
- Client 이탈 시 Host는 단독으로 플레이 지속함
- Client 재접속은 이탈 시각 기준 30~60초 윈도우에서 1회만 허용함
- 단, 재접속 성공 시 토큰을 재활성화해 다음 이탈 상황에서 다시 1회 재접속을 허용함
- 재접속 실패 시 해당 Client는 타이틀로 이동함
- Host 종료 시 세션 종료 후 Host와 Client 전체가 타이틀로 복귀함

### 2-3. 판정/동기화 정책
- 이동과 공격 등 플레이어 캐릭터 입력은 Owner가 담당함
- 최종 게임 결과 판정은 Host가 담당함
- 피격/데미지/드랍/상호작용 확정은 Host가 담당함
- 클라이언트는 연출 즉시 표시 가능, 결과는 Host 기준으로 재동기화함

### 2-4. 동기화 분류
- **지속 동기화**: 위치, 회전, 핵심 전투 상태
- **이벤트 동기화**: 피격, 사망, 부활, 스테이지 이동, 드랍 생성, 상호작용 성공
- **로컬 계산**: HUD 애니메이션, 임시 연출, 보간값

### 2-5. 스폰/체크포인트 정책
- 스폰 포인트는 슬롯 기반임
- 세션 참가 순서 기준으로 슬롯을 고정함
- 슬롯 부족 시 가장 가까운 보조 스폰 포인트를 사용함
- 보조 스폰 포인트로 폴백 발생 시 Warning 로그 필수
- 체크포인트 동기화 규칙
  - `Host 체크포인트 >= Client 체크포인트` 이면 Host/Client 모두 Host 값으로 갱신함
  - `Host 체크포인트 < Client 체크포인트` 이면 Host 체크포인트만 Host 값으로 갱신함

### 2-6. 진행/사망/종료 정책
- Stage 종료는 Host authoritative rule
- 사망 플레이어는 각자 3회 제한 부활 가능함
- 부활 횟수 소모 시 남은 플레이어를 관전함
- 모든 플레이어가 사망하면 체크포인트로 복귀함

### 2-7. 저장 정책
- 저장 시점: Town 입장 / Stage 클리어 / 체크포인트
- Host/Client 모두 저장함
- 저장 항목: 진행 스테이지, 해금/클리어한 Stage 정보, 체크포인트, 인벤토리, 재화
- 모든 데이터는 로컬에만 저장함

---

## 3. 현재 코드베이스 재사용 포인트
- `GameFlowController`: 타이틀 모드 분기, 권한 정책 게이트, 종료/폴백 흐름
- `StageFlow` (`StageSession`, `StageCatalog`, `StageSpawnResolver`): Town→Stage 전환 문맥, 스폰 해석
- `SaveCoordinator`: 트리거 기반 저장 호출 지점 통합
- `TownStageSelectorPresenter`: Host 전용 선택 UX 강제 및 차단 이벤트 연결

---

## 4. 제안 아키텍처(레이어 분리)

```text
[Presentation]
  - TitlePlayModePresenter
  - JoinCodePanelView
  - TownStageSelectorPresenter
  - NetworkStatusHUD

[Application]
  - MultiplayerSessionOrchestrator
  - SessionAdmissionGuardService
  - ReconnectPolicyService
  - MultiplayerCommandService

[Infrastructure]
  - UgsAuthService (Anonymous)
  - RelayAllocationService (Create/Join by Code)
  - NgoSessionService (NetworkManager Adapter)
  - NetworkSceneSyncService
```

### 핵심 서비스 책임
- `SessionAdmissionGuardService`
  - 정원 2명 제한
  - Stage 중 Join 차단
  - 재접속 1회/시간 윈도우 검증
- `ReconnectPolicyService`
  - 재접속 토큰 발급/소모(이탈 사건당 1회)
  - 재접속 성공 시 토큰 재활성화
  - 재접속 실패 시 타이틀 복귀 명령

---

### 4-1. 세션 런타임 유지 및 Town Join Code 표시
- `MultiplayerSessionOrchestrator`는 씬 전환 이후에도 런타임 상태를 유지할 수 있도록 라이프사이클 옵션(`DontDestroyOnLoad`)을 사용합니다.
- 오케스트레이터는 활성 Join Code(`ActiveJoinCode`)와 현재 Host 여부(`IsHosting`)를 외부 UI가 조회할 수 있도록 제공합니다.
- Town HUD에서는 `TownMultiplayerJoinCodePresenter`를 통해 Host의 Join Code를 표시하고, Client에서는 숨김 또는 대체 문구(`-`)로 처리합니다.

---

## 5. 모드별 사용자 경험

1. **싱글플레이**
   - Title > 싱글 선택 > 로컬 씬 로드 > StageFlow 진행
2. **멀티 Host**
   - Title > 멀티 Host 선택 > Relay Allocation 생성 > Town 즉시 이동 > Town UI에서 Join Code 확인
3. **멀티 Client**
   - Title > 멀티 Client 선택 > Join Code 입력/검증 > Host Town 합류 > Host가 선택한 Stage로로 동기 이동

---

## 6. 상태/전이 설계

권장 멀티 상태:
- `NetBoot` → `NetAuthReady` → `NetLobbyIdle`
- `NetHostStarting` / `NetClientJoining`
- `NetInTown` / `NetInStage`
- `NetRecovering` / `NetTerminated`

전이 핵심:
- Host 세션 생성 성공 시 `NetInTown`
- Client Join 성공 시 `NetInTown`
- Stage 선택 이벤트는 Host만 발행 가능
- Stage 진행 중 Join 요청은 `Rejected(StageInProgress)`
- Client 이탈 시 Host는 `NetInTown` 또는 `NetInStage` 유지
- Client 재접속 성공 시 기존 상태로 복귀
- Client 재접속 실패 시 Client는 `NetTerminated`후 Title로 이동
- Host 종료 시 전체 `NetTerminated` 후 Title로 이동

---

### 6-1. Title 진입 / 세션 시작 전이 흐름
- `TitlePlayModePresenter`는 Host 시작 요청을 `MultiplayerSessionOrchestrator.StartHostSessionFromTitle(...)`로 위임합니니다.
- Host 경로는 **세션 생성 성공 → GameFlow Host 시작 요청 → Town 즉시 이동** 순서로 동작합니다.
- Client 경로는 슬롯 선택 후 Join 팝업을 통해 Join Code를 입력하고, 실제 참가 성공 시 Town에 합류합니다.

---

## 6-2. GameFlowController 시작 씬 필드 4종 분리 이유

`GameFlowController`의 아래 필드는 역할이 다릅니다.

- `_singlePlayerStartSceneName`
  - 싱글 시작 시 진입할 기본 씬
- `_multiplayerHostStartSceneName`
  - 멀티 Host 시작 시 기본 씬(공통 Town 미사용 시 폴백)
- `_multiplayerClientStartSceneName`
  - 멀티 Client 시작 시 기본 씬(공통 Town 미사용 시 폴백)
- `_multiplayerSharedTownSceneName`
  - 멀티 시작 시 Host/Client를 동일 Town으로 강제하는 공통 진입 씬

분리한 이유는 다음과 같습니다.
1. **요구사항 충족**: 멀티는 Host/Client가 동일 Town으로 먼저 합류해야 하므로 공통 진입 씬 필드가 필요합니다.
2. **운영 유연성**: 테스트/이벤트 빌드에서 Host/Client 기본 진입을 다르게 바꿔야 할 때 코드 수정 없이 Inspector 값만 변경할 수 있습니다.
3. **안전한 폴백**: 공통 Town 값을 비우면 Host/Client 개별 필드로 폴백하도록 설계해 런타임 오류 가능성을 줄였습니다.
4. **유지보수성**: 모드별 의도를 필드 레벨에서 드러내 디자이너/기획이 인스펙터만 보고도 설정 의미를 이해할 수 있습니다.

> 권장값:
> - 현재 요구사항 기준에서는 `_multiplayerSharedTownSceneName`만 `Town`으로 고정하고,
>   Host/Client 개별 필드는 폴백 용도로 동일 값(`Town`) 유지가 가장 단순합니다.

---

## 7. 권한 경계(Authority Boundary)

### 7-1. 확정 권한
- Host: 게임 결과, 피격/데미지/드랍/상호작용, Stage 종료, Stage 이동
- Owner: 이동과 공격 등 플레이어 조작 입력 전송
- Client: 즉시 연출만 수행 가능, 확정 권한은 없음

### 7-2. Town 단계 규칙
- Host만 `RequestEnterStage` 가능
- Host도 Client Join 완료 전에는 `RequestEnterStage` 불가(세션 인원 충족 전 차단)
- Client 준비 완료 신호는 사용자 입력 없이 시스템 로드 완료 시 자동 선언
- Client의 Town 선택 입력은 UI와 Flow 양쪽에서 차단

### 7-3. 식별 정책
- 표시 이름 없음
- 내부 식별은 `clientId` 단일 키 사용

---

## 8. 스폰/체크포인트/저장 구현 방안

### 8-1. 스폰
- 세션 참가 순서로 슬롯 할당(Host=Slot0, Client=Slot1)
- 슬롯 포인트 미존재 시 보조 포인트 폴백
- 폴백 사용 시 `GameFlowWarningCatalog` 기반 Warning 로그 기록

### 8-2. 스테이지 해금 동기화 정책
- 멀티플레이에서 진행 가능한 Stage는 Host의 해금 상태를 기준으로 결정한다.
- Client는 Host가 해금한 Stage 범위 내에서만 멀티플레이 진행이 가능하다.
- 멀티 세션에서 특정 Stage를 클리어하면, Host와 Client 모두 해당 Stage를 클리어한 것으로 기록한다.
- 이때 클리어 반영은 세션 종료 시점이 아니라 Stage 클리어 확정 시점에 처리하는 것이 안전하다.
- 클리어 결과 저장은 Host와 Client 각각의 로컬 저장에 반영한다.

### 8-3. 저장
- 저장 트리거: Town 입장 / Stage 클리어 / 체크포인트
- 저장 채널: Host/Client 각각 로컬 저장
- 저장 데이터: 진행 스테이지, 해금/클리어한 Stage 정보, 체크포인트, 인벤토리, 재화
- 모든 데이터는 로컬에만 저장함

---

## 9. 요구사항 달성 방안 탐색

### 방안 A. 최소 확장형(권장)
- 기존 `GameFlow` + `StageFlow`에 서비스만 추가
- 장점: 빠른 적용, 리스크 낮음
- 단점: 대규모 확장 시 구조 재정비 필요

### 방안 B. 멀티 전용 Flow 분리형
- `MultiplayerFlowStateMachine` 별도 구성
- 장점: 장기 확장성 높음
- 단점: 현재 요구(2인/Relay only) 대비 구현량 큼

### 방안 C. 이벤트 소싱 복구형
- 이벤트 로그 재생으로 재접속 복구
- 장점: 복구 정합성 높음
- 단점: 과투자 가능성 큼

### 결론
- 현재 요구사항(2인/중도Join금지/재접속1회/Host권위)에는 **방안 A**가 가장 적합

---

## 10. 단계별 적용 로드맵
1. 타이틀 모드 분기 + Join Code UI 노출
2. Relay 연결 + 2인 제한 AdmissionGuard
3. Town 공통 진입 + Host 전용 Stage 선택 강제
4. 전투/상호작용 Host 확정 + Client 재동기화
5. 재접속 1회 정책 + 실패 시 타이틀 복귀
6. 저장 트리거(Town/StageClear/Checkpoint) 검증

---

## 11. 리스크와 대응
1. **재접속 타이밍 경합**
   - 대응: 서버 시간 기준 만료 판정 + 1회 토큰 소모 원자화
2. **중도 Join 우회 시도**
   - 대응: UI 차단 + AdmissionGuard 이중 거부
3. **Host 종료 처리 누락**
   - 대응: Host 종료 훅에서 세션 종료 RPC + 타이틀 강제 이동
4. **로컬 인벤토리 불일치**
   - 대응: 인벤토리 비권위 정책 명시 + 전투 결과만 Host 확정

---

## 12. 유지보수성 우선 최종 구현 선택안

### 12-1. 최종 선택
- **선택안: 최소 확장형 + 인터페이스 분리 강화(A안 보강형)**
- 이유:
  1. 현재 레포의 GameFlow/StageFlow를 최대한 재사용하여 변경 범위를 좁힐 수 있음
  2. 네트워크 SDK(UGS/NGO) 의존 코드를 Infrastructure에 격리해 교체 비용을 낮출 수 있음
  3. 2인 고정/Host 권위 정책을 서비스 단일 지점에서 일관되게 관리 가능

### 12-2. 유지보수 설계 원칙(실행 규약)
1. **정책 단일화**
   - 입장/재접속/권한 규칙은 `SessionAdmissionGuardService`에서만 판단
2. **UI-도메인 분리**
   - Presenter는 `GameFlowController`/Orchestrator 명령만 호출하고 SDK 직접 접근 금지
3. **권위 경계 고정**
   - Host 확정 로직은 도메인 서비스에서 단일 처리, Client는 요청/연출만 수행
4. **확장 지점 명시**
   - 2인 제한을 상수 하드코딩하지 않고 Config(ScriptableObject)에서 관리
5. **장애 대응 표준화**
   - 재접속 실패/Host 종료/중도 Join 거부를 공통 에러코드 + 타이틀 복귀 플로우로 통일

### 12-3. 권장 폴더/클래스 경계
- `GameFlow/Core`
  - 모드/상태 전환, 상위 이벤트 라우팅
- `Multiplayer/Application`
  - `MultiplayerSessionOrchestrator`
  - `SessionAdmissionGuardService`
  - `ReconnectPolicyService`
- `Multiplayer/Infrastructure`
  - `IRelayService`, `INgoSessionService` 인터페이스 + 구현
- `Multiplayer/Presentation`
  - `TitlePlayModePresenter`, JoinCode/Town 안내 Presenter

### 12-4. 테스트/운영 기준(유지보수 관점)
- 단위 테스트 우선 대상
  - AdmissionGuard(정원 제한, 중도 Join, 재접속 1회)
  - 스테이지 해금 동기화 정책
	  - Host 기준으로 진행 가능한 Stage가 결정되는지 검증
	  - Client가 해금하지 않은 Stage라도 Host 기준으로 입장이 가능한지 검증
	  - 멀티플레이로 Stage 클리어 시 Host와 Client 모두 클리어 상태로 저장되는지 검증
	  - 클리어 반영이 즉시 적용되는지 검증 (세션 종료 의존 없음)
- 회귀 테스트 체크리스트
  - Host 종료/Client 재접속 실패/중도 Join 거부 시 타이틀 복귀 일관성
- 운영 로그 표준
  - 폴백 스폰/권한 위반/재접속 실패는 Warning 카탈로그 키로 기록

---

## 결론

문서의 최종 결론은 이렇습니다.

- 현재 레포는 `GameFlow`, `StageFlow`, `SaveSystem` 기반이 이미 잘 잡혀 있습니다.
- 그래서 전면 재작성보다 `AdmissionGuard`, `ReconnectPolicy` 같은 정책 서비스 추가 방식이 가장 안전합니다.
- 현재 요구사항이 2인 고정 + Host 권위 + Relay only라서 최소 확장형만으로도 충분히 기능 완성도를 확보할 수 있다.

---

## 부록. 현재 구현 반영 스크립트
- `MultiplayerSessionOrchestrator`
  - 타이틀 Host 생성 / Client Join / Stage 중 Join 제한 / 재접속 1회 정책 / Host 종료 타이틀 복귀
- `SessionAdmissionGuardService`
  - 정원(2명) 및 Stage 진행 중 Join 금지 정책 판정
- `ReconnectPolicyService`
  - 30~60초 재접속 윈도우와 이탈 사건당 1회 토큰 소비/성공 시 재활성화 정책
- `IMultiplayerSessionBackend` + `LocalFileMultiplayerSessionBackend`
  - 운영 Relay 어댑터 교체 전, 로컬 검증용 백엔드 추상화/기본 구현
