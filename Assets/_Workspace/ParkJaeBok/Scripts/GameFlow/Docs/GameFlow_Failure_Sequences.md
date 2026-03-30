# GameFlow Failure Sequences

## 1) Scene Load 실패 -> Retry -> Fallback
1. `GameFlowController.TryStartSceneLoad` 실패
2. `HandleSceneLoadFailure`에서 재시도 카운트 증가
3. `FlowRetryService`로 재시도 스케줄
4. 재시도 소진 시 `HandleRetryExhausted`
5. `ErrorRecovery` 상태 전환 후 `FlowFallbackService` 실행

## 2) Death Recovery 실패 -> Town/Title Fallback
1. `RecoveryService` 또는 관련 시스템이 `NotifyDeathRecoveryFailed` 호출
2. `HandleDeathRecoveryFailure`에서 정책 조회
3. 정책 대상이 Town이면 `RequestReturnToTown`, 실패 시 Title 강제 리셋

## 3) Save 실패 -> Dirty 상태 유지
1. `SaveCoordinator.OnSaveOperationCompleted`에서 실패 수신
2. `FlowSaveSyncAdapter`가 `NotifySaveFailed` 전달
3. `HasSaveFailureDirty`/`LastSaveFailureReason` 갱신
4. `GameFlowRuntimeDiagnostics`에 실패 누적/최근 시각 기록

## 4) 중복 종료 요청 차단
1. `RequestExit` 진입 시 `FlowExitGuard.TryEnter()`
2. 점유 상태면 정책(`IgnoreDuplicateExitRequest`)에 따라 즉시 차단

## 5) stale callback 무시 + 타이틀 mismatch 대응
1. `HandleAfterSceneLoad`에서 epoch/expected scene 비교
2. stale이면 즉시 무시 로그 후 반환
3. 타이틀 mismatch 정책이 활성화면 `ForceResetToTitle` 경로 수행
