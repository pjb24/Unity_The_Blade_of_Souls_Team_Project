# GameFlow Logging Guide

## 목적
GameFlow 관련 로그를 표준 포맷으로 통일해 운영 중 원인 파악 시간을 줄입니다.

## 표준 포맷
- 상태 전이: `[GameFlow][State] from=..., to=..., reason=...`
- 경고: `[GameFlow][Warning] ...`
- 복구/폴백: `[GameFlow][Recovery] ...`
- 정보: `[GameFlow][Info] ...`
- 오류: `[GameFlow][Error] ...`

## 사용 규칙
1. 상태 전이는 `GameFlowLogger.StateTransition`만 사용합니다.
2. 폴백/재시도/복구 흐름은 `GameFlowLogger.Recovery`를 사용합니다.
3. 상세 로그는 `GameFlowLogger.SetVerbose(bool)`로 제어합니다.
4. 사용자 영향 실패(저장 실패, 복구 실패, 중복 종료 차단)는 항상 Warning 로그를 남깁니다.

## 빠른 장애 분류
1. `State` 로그 마지막 전이 확인
2. 직전 `Warning` 로그의 reason/context 확인
3. `Recovery` 로그로 재시도/폴백 이력 확인
4. 저장 실패 시 `GameFlowController.HasSaveFailureDirty` 상태 확인

## Runtime 진단 지표 조회

`GameFlowController`는 UI/운영 도구에서 읽기 전용으로 사용할 수 있는 `GameFlowDebugSnapshot` 조회 API를 제공합니다.

- `GameFlowController.DebugSnapshot`
- `GameFlowController.GetDebugSnapshot()`

### 포함 지표
- 최근 N개 Warning 요약 (`RecentWarnings`)
- 최근 상태 전이 기록 환형 버퍼 (`RecentStateTransitions`)
- Recovery 재시도 횟수/최종 결과 (`Recovery.RetryCount`, `Recovery.LastResult`)
- Recovery 서킷브레이커 상태/종료시각/차단횟수 (`Recovery.CircuitOpen`, `Recovery.CircuitOpenUntilUtc`, `Recovery.CircuitRejectedCount`)
- Save 실패 누적 횟수/최근 실패 시각 (`SaveFailures.FailureCount`, `SaveFailures.LastFailureAtUtc`)

### 조회 예시
```csharp
GameFlowController controller = GameFlowController.Instance;
if (controller != null)
{
    GameFlowDebugSnapshot snapshot = controller.GetDebugSnapshot();
    Debug.Log($"WarningCount={snapshot.RecentWarnings.Count}, RecoveryRetry={snapshot.Recovery.RetryCount}, SaveFailCount={snapshot.SaveFailures.FailureCount}");
}
```

## 운영 체크리스트 (확장)
1. 최신 `RecentStateTransitions` 마지막 3개가 기대 경로(Title->Town->StagePlaying 등)와 일치하는지 확인
2. `RecentWarnings`에 반복되는 동일 reason이 있는지 확인 (동일 원인 반복 장애 탐지)
3. `Recovery.RetryCount` 급증 여부와 `Recovery.LastResult`가 `Failed`로 고정되는지 확인
4. `Recovery.CircuitOpen`이 true이면 `Recovery.CircuitOpenUntilUtc` 전까지 신규 요청이 fail-fast 차단되는지 확인
5. `SaveFailures.FailureCount` 증가 추세와 `SaveFailures.LastFailureAtUtc` 최근성 확인
6. `HasSaveFailureDirty`가 true인 경우 UI 알림/재시도 UX와 연동 상태 확인
