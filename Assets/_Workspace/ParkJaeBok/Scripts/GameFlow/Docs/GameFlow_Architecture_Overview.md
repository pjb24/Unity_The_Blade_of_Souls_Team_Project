# GameFlow Architecture Overview

## 목적
현재 GameFlow/Save/Recovery 연동 구조를 릴리즈 전 관점에서 정리하고, 기술부채 항목을 우선순위 기반으로 관리합니다.

## 현재 계층(논리)
1. **Entry/UI 계층**
   - `TitleMenuPresenter`, `StartNewGameAction`, `ContinueGameAction`, `RequestQuitAction`
   - `TownStageSelectorPresenter`, `StageReturnToTownPresenter`
2. **Orchestration 계층**
   - `GameFlowController`
   - `GameFlowStateMachine`, `GameFlowStates`
3. **Policy/Service 계층**
   - `ErrorRecoveryPolicy`
   - `FlowRetryService`, `FlowFallbackService`, `FlowExitGuard`, `FlowSaveSyncAdapter`
4. **Infrastructure 계층**
   - `SceneTransitionService`, `SaveCoordinator`, `RecoveryService`, `StageSession`, `StageCatalog`
5. **Diagnostics 계층**
   - `GameFlowLogger`, `GameFlowRuntimeDiagnostics`, `GameFlowDebugSnapshot`

## 폴더 구조 정리안 (다음 릴리즈 준비)
- `GameFlow/Core`: Controller/StateMachine/States/Event/Context
- `GameFlow/Policies`: `ErrorRecoveryPolicy`
- `GameFlow/Services`: `FlowRetryService`, `FlowFallbackService`, `FlowExitGuard`, `FlowSaveSyncAdapter`
- `GameFlow/Diagnostics`: `GameFlowLogger`, `GameFlowRuntimeDiagnostics`, `GameFlowDebugSnapshot`, `GameFlowWarningCatalog`
- `GameFlow/Test`: 통합 러너/가이드
- `GameFlow/Docs`: 아키텍처/시퀀스/런북 문서

## 동작 변경 필요 vs 리팩토링-only 구분

### A. 동작 변경 필요
1. UI fallback 완전 제거(강제 모드 고정)
   - 이유: 운영 중 경로 분기가 남아 원인 파악 복잡도 증가
   - 위험도: **High**
2. Recovery 정책별 타임아웃/서킷브레이커 추가
   - 이유: 무한 재시도성 장애 확산 위험
   - 위험도: **High**
3. Save 실패 임계치 기반 사용자 알림 정책 도입
   - 이유: 데이터 손실 체감 최소화
   - 위험도: **Medium**

### B. 리팩토링-only
1. GameFlow 폴더 계층 이동/네이밍 정리
   - 위험도: **Medium** (경로 변경 영향)
2. Warning 문구 카탈로그 통합(`GameFlowWarningCatalog`)
   - 위험도: **Low**
3. public API 범위 축소(내부 전용 API internal 전환)
   - 위험도: **Low ~ Medium**
