# GameFlow Incident Runbook (Draft)

## 0. 트리아지 입력값
- `GameFlowController.GetDebugSnapshot()`
- 최근 콘솔 로그(`[GameFlow][Warning]`, `[GameFlow][Recovery]`, `[GameFlow][State]`)
- 저장 실패 관련 `HasSaveFailureDirty`, `LastSaveFailureReason`

## 1. 공통 1차 점검
1. 최근 상태 전이 3건 확인 (`RecentStateTransitions`)
2. 최근 Warning 반복 reason 확인 (`RecentWarnings`)
3. Recovery 지표 확인 (`Recovery.RetryCount`, `Recovery.LastResult`)
4. Save 실패 지표 확인 (`SaveFailures.FailureCount`, `SaveFailures.LastFailureAtUtc`)

## 2. 장애 유형별 대응

### A. 씬 전환 실패 반복
- 조건: Recovery 재시도 급증 + `LastResult=Failed`
- 조치:
  1) 잘못된 씬 이름/카탈로그 매핑 점검
  2) 정책 fallback state 확인
  3) 임시로 타이틀 fallback 강제 후 서비스 복구

### B. Recovery 실패 반복
- 조건: `NotifyDeathRecoveryFailed` Warning 반복
- 조치:
  1) 스폰 포인트/StageSession 무결성 점검
  2) Town fallback 성공 여부 확인
  3) 실패 시 Title 강제 리셋 및 재진입 유도

### C. Save 실패 누적 증가
- 조건: `SaveFailures.FailureCount` 증가 + Dirty true
- 조치:
  1) 저장 채널(Persistent/Session)별 실패 원인 파악
  2) 디스크/권한/직렬화 예외 확인
  3) 사용자 안내 및 재시도 UX 확인

## 릴리즈 전 체크리스트
- [ ] GameFlow 강제 경로(legacy fallback default false) 유지 확인
- [ ] `GameFlowWarningCatalog` 문구 표준 반영 범위 점검
- [ ] DebugSnapshot 지표가 Play Mode에서 정상 갱신되는지 확인
- [ ] stale callback 무시 및 타이틀 mismatch 경로 수동 검증
- [ ] Save 실패 누적/최근 시각 값 증가 검증
- [ ] 문서 3종(아키텍처/실패 시퀀스/런북) 최신화 확인
