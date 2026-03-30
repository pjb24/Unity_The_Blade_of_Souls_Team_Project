# GameFlow Integration Test Runner Guide

## 테스트 케이스 목록 (Given / When / Then)

1. **Scene 로딩 실패 -> 재시도 -> 폴백**
   - **Given**: `FlowRetryService` 재시도 정책(`MaxSceneLoadRetryCount=1`), 실패하는 씬 로더, `FlowFallbackService(Title 경로)` 구성
   - **When**: 동일 씬 실패를 2회 입력
   - **Then**: 1회차는 재시도 스케줄링, 2회차는 소진 처리 + Title 폴백 실행

2. **Recovery 실패 -> Town 폴백**
   - **Given**: `FlowFallbackService` Town 폴백 콜백 구성
   - **When**: `ExecuteFallback(GameFlowState.Town, ...)` 호출
   - **Then**: Town 폴백 콜백 실행 + 결과 true

3. **저장 실패 -> dirty state 갱신**
   - **Given**: `GameFlowController` 인스턴스
   - **When**: `NotifySaveFailed("ManualFailureReason")` 호출
   - **Then**: `HasSaveFailureDirty == true`, `LastSaveFailureReason == "ManualFailureReason"`

4. **중복 종료 요청 -> 원자적 게이트 차단**
   - **Given**: `FlowExitGuard` 인스턴스
   - **When**: `TryEnter()` 연속 2회, `Release()`, 다시 `TryEnter()`
   - **Then**: 첫 진입 성공, 두 번째 진입 차단, release 후 재진입 성공

5. **타이틀 복귀 stale callback -> 무시 + mismatch 처리 경로**
   - **Given**: `GameFlowController` private epoch/expected scene를 리플렉션으로 설정
   - **When**: `HandleAfterSceneLoad("UnexpectedOtherScene")`(private) 호출 후 `NotifyTitleReturnStateMismatch(...)` 호출
   - **Then**: stale callback으로 epoch/expected scene 유지, mismatch 경고 로그 캡처 확인

## 테스트 코드 파일 목록

- `Assets/_Workspace/ParkJaeBok/Scripts/GameFlow/Test/GameFlowIntegrationTestRunner.cs`
- `Assets/_Workspace/ParkJaeBok/Scripts/GameFlow/Test/GameFlow_Integration_Test_Runner_Guide.md`

## 실행 방법

### 기본 실행 (Unity Editor Play Mode)
1. 빈 GameObject를 하나 생성합니다.
2. `GameFlowIntegrationTestRunner` 컴포넌트를 추가합니다.
3. 아래 중 하나로 실행합니다.
   - `_runOnStart = true` 후 Play
   - 컴포넌트 우클릭 컨텍스트 메뉴에서 **Run GameFlow Integration Scenarios** 실행
4. Console에서 `[GameFlowIntegrationTestRunner] PASS/FAIL` 라인을 확인합니다.

### 환경 제약으로 실제 실행이 어려운 경우 (대체 검증 절차)
1. 코드 정적 검증:
   - `GameFlowLogger.SetExternalLogSink(...)` 훅 연결 여부 확인
   - 5개 시나리오 메서드 존재 여부 확인
2. 로그 경로 검증:
   - `GameFlowLogger`의 Info/Warning/Error/State/Recovery에서 외부 훅 호출되는지 확인
3. 리플렉션 검증:
   - stale callback 시나리오에서 private field/method 접근 로직 존재 확인
