# Boss Phase 23 Host 검증

## 범위

* 이 검증은 멀티플레이 Host 동작으로 제한된다.
* 클라이언트 권한 기반의 피격, 데미지, 패턴 선택, 스폰 경로는 의도적으로 추가하지 않는다.
* 저장소 지침에서 컴파일 시도를 금지하고 있으므로 Unity Play Mode 실행 및 컴파일은 수행하지 않았다. 

---

## 검증 방법

* Host/서버 실행을 위한 보스 권한 체크를 검토하였다.
* 패턴 선택, 피격, 데미지, 스폰, 약점, 타임아웃, 전체 파괴, Dead, 프레젠테이션 동기화 경로를 검토하였다.
* 보스 코드에서 다음 금지 네트워크/이벤트 패턴을 확인하였다.

  * `ServerRpc`
  * `ClientRpc`
  * `public event`
  * `NetworkVariable`
  * 직접 이벤트 구독 연산자
  * `Vector3.Distance`
  * LINQ 선택 헬퍼

---

## Host 권한 검증

| 요구사항                   | 검증 경로                                                                                                                                                      | 결과         |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------- |
| Host가 보스 상태를 결정        | `BossController.IsBossLogicAuthority()`는 네트워크가 listening 상태일 때 `NetworkManager.Singleton.IsServer`를 반환한다. Host는 서버이므로 상태 변경은 `TryEnsureAuthority()`를 통과한다. | 코드 검토로 확인됨 |
| Host가 패턴 선택을 결정        | `BossController.TrySelectPattern()`은 `BossPatternSelector`에 위임하며, selector는 `bossController.IsBossLogicAuthority()`를 통해 비권한을 거부한다.                         | 코드 검토로 확인됨 |
| Host가 패턴 실행 시작         | `BossController.TryStartPatternExecution()`은 `TryEnsureAuthority("TryStartPatternExecution")`로 보호된다.                                                       | 코드 검토로 확인됨 |
| Client는 보스 상태를 결정하지 않음 | 클라이언트 측 상태 반영은 `SyncBossStateRpc(int stateValue)`만 수행하며, 서버에서 작성된 상태 값을 적용할 뿐 패턴 선택이나 데미지 처리는 수행하지 않는다.                                                    | 코드 검토로 확인됨 |

---

## Host 패턴 선택

* `BossPatternSelector.TrySelectPattern()`은 다음을 검사한다.

  * 전투 활성 상태
  * Idle 상태
  * 글로벌 쿨타임
  * 패턴별 쿨타임
  * 활성화 플래그
  * 체력 조건
  * 사용 횟수 제한
  * 타겟 요구 조건
  * Pattern 4 활성 제한
  * 우선순위 및 서버 측 랜덤 선택

* selector는 후보를 반환하기 전에 `bossController.IsBossLogicAuthority()`를 호출한다.

* 결과: Host가 패턴 선택을 결정한다.

---

## Host 공격 및 데미지 검증

| 공격 경로              | Host 권한 동작                                                                                                                                                     | 결과         |
| ------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------- |
| Pattern 1 투사체      | `BossFanProjectilePattern`은 타겟을 결정하고 투사체 스폰을 요청하기 전에 보스 권한을 체크한다. 투사체 피격 동작은 기존 projectile/Hit 시스템을 따른다.                                                       | 코드 검토로 확인됨 |
| Pattern 2 스파이크 피격  | `BossGroundSpikePattern.ApplySpikeHit()`는 `BossController.IsBossLogicAuthority()`가 true가 아니면 종료하고, true일 경우 `HitReceiver.ReceiveHit()`를 통해 `HitRequest`를 전달한다. | 코드 검토로 확인됨 |
| Pattern 4 타임아웃 데미지 | `BossWeakPointPattern.ResolveWeakPointTimeOut()`은 보스 권한이 true가 아니면 종료하고, true일 경우 `HealthComponent.ApplyDamage()`를 통해 `WeakPointTimeLimitDamage`를 적용한다.        | 코드 검토로 확인됨 |
| 약점 파괴              | `BossWeakPointObject.OnDied()`는 `BossWeakPointPattern.HandleWeakPointDestroyed()`에 보고하며, 패턴은 보스 권한이 없으면 보고를 거부한다.                                              | 코드 검토로 확인됨 |

---

## Host 스폰 검증

| 스폰 경로             | Host 권한 동작                                                                                                                    | 결과         |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------- | ---------- |
| Pattern 1 투사체 스폰  | Pattern 1은 권한이 있을 때만 실행되며 `IProjectileSpawnService.RequestSpawn()`을 호출한다. 기존 projectile 서비스가 싱글플레이/Host 네트워크 처리를 담당한다.        | 코드 검토로 확인됨 |
| Pattern 2 스파이크 스폰 | `BossGroundSpikePattern.TrySpawnSpike()`는 보스 권한이 없으면 네트워크 스폰을 차단하고, 권한 검증 후에만 `NetworkObject.Spawn(true)`를 호출한다.              | 코드 검토로 확인됨 |
| Pattern 3 몬스터 스폰  | `BossSummonMonsterPattern.TrySpawnMonsterFallback()`는 보스 권한이 없으면 네트워크 스폰을 차단하고, Host/서버에서만 `NetworkObject.Spawn(true)`를 호출한다. | 코드 검토로 확인됨 |
| Pattern 4 약점 스폰   | `BossWeakPointPattern.TrySpawnWeakPoint()`는 보스 권한이 없으면 네트워크 스폰을 차단하고, Host/서버에서만 `NetworkObject.Spawn(true)`를 호출한다.           | 코드 검토로 확인됨 |
| 약점 정리             | `BossWeakPointPattern.CleanupWeakPointObject()`는 권한 경로에서 생성된 약점 NetworkObject를 despawn 한다.                                    | 코드 검토로 확인됨 |

---

## Pattern 4 Host 검증

| 요구사항                      | 검증 경로                                                                                            | 결과  |
| ------------------------- | ------------------------------------------------------------------------------------------------ | --- |
| Host가 약점 생성               | Pattern 4는 진입 전과 약점 스폰 전에 권한을 검증한다.                                                              | 확인됨 |
| Host가 약점 파괴 판단            | `HandleWeakPointDestroyed()`는 보스 권한이 아니면 종료한다.                                                   | 확인됨 |
| Host가 타임아웃 처리             | `ResolveWeakPointTimeOut()`은 보스 권한이 아니면 종료하고, 타이머를 중지하며, 타임아웃 데미지를 적용하고, 남은 약점을 제거하며, 컨트롤러에 알린다. | 확인됨 |
| Host가 전체 파괴 처리            | `ResolveAllWeakPointsDestroyed()`는 타이머를 중지하고, 버퍼를 정리하며, 컨트롤러에 알리고, 권한 컨트롤러를 통해 Groggy 상태로 진입한다.  | 확인됨 |
| Host가 Dead 시 Pattern 4 정리 | `BossController.EnterDeadState()`는 `BossWeakPointPattern.CleanupForBossDeath()`를 호출한다.           | 확인됨 |

---

## Dead Host 검증

* `BossController`는 기존 `HealthComponent`에 `AddListener(this)`로 구독한다.

* `BossController.OnDied()`는 비권한 인스턴스를 무시한다.

* Host는 `EnterDeadState()`를 통해 Dead 상태에 진입한다.

* Dead 정리 작업:

  * 런타임 타이머 중지
  * 현재 패턴 취소
  * Pattern 4 약점 타이머 및 생성된 약점 정리
  * 패턴 선택 비활성화
  * 약점 및 무적 플래그 초기화
  * `E_BossState.Dead` 설정
  * Dead 상태 RPC 및 Dead 프레젠테이션 cue 전송

* 결과: Host가 Dead 상태와 정리를 담당한다.

---

## 프레젠테이션 동기화

| 프레젠테이션 경로           | 동기화                                                                                             | 결과  |
| ------------------- | ----------------------------------------------------------------------------------------------- | --- |
| 보스 상태               | `BossController.SyncBossStateRpc(int stateValue)`는 `[Rpc(SendTo.ClientsAndHost)]`를 사용한다.        | 확인됨 |
| 패턴 프레젠테이션 cue       | `BossPresentationController.PlayCueRpc(...)`는 `[Rpc(SendTo.ClientsAndHost)]`를 사용한다.             | 확인됨 |
| Pattern 2 경고/공격 VFX | `BossGroundSpikePattern.BroadcastGroundSpikeVfxRpc(...)`는 `[Rpc(SendTo.ClientsAndHost)]`를 사용한다. | 확인됨 |
| Client 역할           | Client RPC 핸들러는 상태 반영 또는 로컬 프레젠테이션 재생만 수행한다. 패턴 선택, 데미지 적용, 피격 판정은 수행하지 않는다.                    | 확인됨 |

---

## 정적 검사 명령

```powershell
rg -n "IsBossLogicAuthority|IsServer|IsListening|TrySelectPattern|TryStartPatternExecution|ReceiveHit|ApplyDamage|Spawn\(|Despawn\(|Rpc\(SendTo\.ClientsAndHost\)|Pattern 4|WeakPoint|Timeout|TimedOut|AllWeakPoints|Dead" Assets/_Workspace/ParkJaeBok/Scripts/Boss -g *.cs
rg -n "if \(shouldUseNetwork &&.*!.*IsBossLogicAuthority|if \(_bossController == null \|\| !_bossController\.IsBossLogicAuthority\(\)\)|if \(!TryEnsureAuthority|NetworkObject.*Spawn|spawnedNetworkObject\.Spawn|networkObject\.Despawn" Assets/_Workspace/ParkJaeBok/Scripts/Boss/Runtime -g *.cs
rg -n "ServerRpc|ClientRpc|public event|NetworkVariable|OnValueChanged|\+=|-=|Vector3\.Distance|\.Where\(|\.Select\(|\.ToList\(" Assets/_Workspace/ParkJaeBok/Scripts/Boss -g *.cs
git diff --check -- Assets/_Workspace/ParkJaeBok/Scripts/Boss
```

---

## 결과

* Host는 보스 상태에 대한 권한 소유자이다.
* Host는 패턴 선택에 대한 권한 소유자이다.
* Host는 구현된 보스 공격에서 피격 판정 및 데미지 적용에 대한 권한 소유자이다.
* Host는 투사체, 스파이크, 몬스터, 약점 생성 경로에 대한 권한 소유자이다.
* Host는 약점 파괴, Pattern 4 타임아웃, Pattern 4 전체 파괴, Groggy, Dead 정리에 대한 권한 소유자이다.
* Client 측 동기화는 프레젠테이션/상태 반영만 수행한다.
* 임시 Host 전용 우회 코드는 추가되지 않았다.
* 이 Phase에서는 컴파일 및 Play Mode 실행을 수행하지 않았으므로 Unity 내부에서 Host 기준의 실제 동작 검증이 추가로 필요하다. 
