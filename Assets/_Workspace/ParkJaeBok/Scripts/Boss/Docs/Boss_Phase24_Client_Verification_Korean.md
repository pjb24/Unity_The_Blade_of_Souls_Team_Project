# Boss Phase 24 Client 검증

## 범위

* 이 검증은 멀티플레이 Client 동작으로 제한된다.
* Client 권한 기반의 보스 로직은 추가되지 않았다.
* 저장소 지침에서 컴파일 시도를 금지하고 있으므로 Unity Play Mode 실행 및 컴파일은 수행하지 않았다. 

---

## 검증 방법

* 보스 Client 권한 거부 경로를 검토하였다.
* 상태 반영 및 프레젠테이션 전용 동작을 위한 Client RPC 핸들러를 검토하였다.
* 공격, 데미지, 스폰, 몬스터 소환, 약점 위치 경로에서 권한 가드 여부를 검토하였다.
* 보스 전용 UI 스크립트 또는 바인딩을 검색하였다.
* 보스 코드에서 다음 금지 패턴을 확인하였다.

  * `ServerRpc`
  * `ClientRpc`
  * `public event`
  * `NetworkVariable`
  * 직접 이벤트 구독 연산자
  * `Vector3.Distance`
  * LINQ 선택 헬퍼

---

## Client 권한 검증

| 요구사항                    | 검증 경로                                                                                                                                                         | 결과         |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------- |
| Client는 보스 권한이 없음       | `BossController.IsBossLogicAuthority()`는 네트워크가 listening 상태일 때 `NetworkManager.Singleton.IsServer`를 반환한다. 순수 Client는 서버가 아니다.                                 | 코드 검토로 확인됨 |
| Client는 전투 로직을 시작할 수 없음 | `StartBattle()`, `ResetBattle()`, `StopBattle()`, `TryStartPatternExecution()`, 상태 설정자, 쿨타임 메서드, Pattern 4 알림, Groggy, Dead 진입은 `TryEnsureAuthority()`를 사용한다. | 코드 검토로 확인됨 |
| Client는 패턴을 선택할 수 없음    | `BossPatternSelector.TrySelectPattern()`은 `bossController.IsBossLogicAuthority()`가 false일 경우 실행을 거부한다.                                                        | 코드 검토로 확인됨 |
| Client는 서버 상태를 따른다      | `BossController.SyncBossStateRpc(int stateValue)`는 서버에서 작성된 상태 값을 반영만 한다.                                                                                     | 코드 검토로 확인됨 |

---

## Client 패턴 및 전투 방지

| 요구사항                                | 가드 경로                                                                                          | 결과  |
| ----------------------------------- | ---------------------------------------------------------------------------------------------- | --- |
| Client는 Pattern 1을 확정하지 않음          | `BossFanProjectilePattern.OnPatternExecutionStarted()`는 보스 권한이 true가 아니면 종료한다.                 | 확인됨 |
| Client는 Pattern 2를 확정하지 않음          | `BossGroundSpikePattern.OnPatternExecutionStarted()`는 보스 권한이 true가 아니면 종료한다.                   | 확인됨 |
| Client는 Pattern 3을 확정하지 않음          | `BossSummonMonsterPattern.OnPatternExecutionStarted()`는 보스 권한이 true가 아니면 종료한다.                 | 확인됨 |
| Client는 Pattern 4를 확정하지 않음          | `BossWeakPointPattern.OnPatternExecutionStarted()`는 보스 권한이 true가 아니면 종료한다.                     | 확인됨 |
| Client는 Pattern 2 피격 판정을 수행하지 않음    | `BossGroundSpikePattern.ApplySpikeHit()`는 보스 권한이 true가 아니면 종료한다.                               | 확인됨 |
| Client는 Pattern 4 타임아웃 데미지를 적용하지 않음 | `BossWeakPointPattern.ResolveWeakPointTimeOut()`은 `ApplyDamage()` 호출 전에 보스 권한이 true가 아니면 종료한다. | 확인됨 |
| Client는 약점 파괴를 판단하지 않음              | `BossWeakPointPattern.HandleWeakPointDestroyed()`는 보스 권한이 true가 아니면 종료한다.                      | 확인됨 |

---

## Client 스폰 방지

| 요구사항                        | 가드 경로                                                                      | 결과  |
| --------------------------- | -------------------------------------------------------------------------- | --- |
| Client는 투사체 생성을 결정하지 않음     | Pattern 1 실행은 `IProjectileSpawnService.RequestSpawn()` 호출 전에 권한 전용으로 제한된다. | 확인됨 |
| Client는 스파이크 생성을 결정하지 않음    | Pattern 2 네트워크 스폰 경로는 네트워크가 활성 상태이고 보스 권한이 없으면 종료한다.                       | 확인됨 |
| Client는 몬스터 생성을 결정하지 않음     | Pattern 3 네트워크 스폰 경로는 네트워크가 활성 상태이고 보스 권한이 없으면 종료한다.                       | 확인됨 |
| Client는 약점 위치 및 생성을 결정하지 않음 | Pattern 4 위치 선택 및 스폰은 권한 전용 패턴 시작 경로 이후에만 실행된다.                            | 확인됨 |

---

## Client 프레젠테이션 검증

| 프레젠테이션    | Client 경로                                                                                                                                 | 결과  |
| --------- | ----------------------------------------------------------------------------------------------------------------------------------------- | --- |
| 보스 상태 반영  | `SyncBossStateRpc(int stateValue)`는 전투 판단 없이 서버 상태를 로컬에 적용한다.                                                                             | 확인됨 |
| 일반 보스 cue | `BossPresentationController.PlayCueRpc(...)`는 서버/Host cue 데이터를 수신하고 로컬 프레젠테이션만 수행한다.                                                      | 확인됨 |
| VFX       | `BossPresentationController.PlayVfx()`는 기존 `EffectService`를 사용하며 Pattern 2는 `BroadcastGroundSpikeVfxRpc()`와 `PlayLocalVfxOrWarn()`를 사용한다. | 확인됨 |
| 사운드       | `BossPresentationController.PlaySfx()`는 기존 `AudioManager.PlaySfx()`를 사용한다.                                                                | 확인됨 |
| 애니메이션     | `BossPresentationController.PlayAnimatorTrigger()`는 기존 `Animator` 트리거를 사용한다.                                                              | 확인됨 |

---

## Client UI 검증

* 검토된 저장소 경로에서 보스 전용 UI 코드는 발견되지 않았다.
* 기존 UI 시스템으로 타이틀 메뉴, 옵션, 화면 밖 플레이어 표시, 버프 게이지는 존재하지만 보스 상태 또는 보스 패턴 UI는 존재하지 않는다.
* Phase 24는 검증 단계이며, 이전 보스 Phase에서 UI 구현이 포함되지 않았기 때문에 Client 보스 UI 표시를 아직 검증할 수 없다.
* 결과: Client UI 표시는 검증되지 않은 항목이며, Client 권한 문제는 아니다.

---

## 정적 검사 명령

```powershell id="5c9r8q"
rg -n "IsBossLogicAuthority|TryEnsureAuthority|TrySelectPattern|TryStartPatternExecution|ReceiveHit|ApplyDamage|RequestSpawn|Instantiate\(|Spawn\(|WeakPoint|Rpc\(SendTo\.ClientsAndHost\)|PlayCueRpc|SyncBossStateRpc|BroadcastGroundSpikeVfxRpc" Assets/_Workspace/ParkJaeBok/Scripts/Boss -g *.cs
rg -n "Boss.*UI|UI.*Boss|HealthBar|BossHealth|Slider|TextMeshPro|TMP|Canvas" Assets/_Workspace/ParkJaeBok/Scripts -g *.cs
rg -n "ServerRpc|ClientRpc|public event|NetworkVariable|OnValueChanged|\+=|-=|Vector3\.Distance|\.Where\(|\.Select\(|\.ToList\(" Assets/_Workspace/ParkJaeBok/Scripts/Boss -g *.cs
rg -n "PlayVfx|PlaySfx|PlayAnimatorTrigger|EffectService|AudioManager|Animator|PlayCueLocal|ApplyReplicatedBossState" Assets/_Workspace/ParkJaeBok/Scripts/Boss/Runtime -g *.cs
git diff --check -- Assets/_Workspace/ParkJaeBok/Scripts/Boss
```

---

## 결과

* Client는 보스 상태를 결정하지 않는다.
* Client는 보스 패턴을 선택하지 않는다.
* Client는 보스 소유 공격 판정을 수행하지 않는다.
* Client는 보스 소유 데미지를 적용하지 않는다.
* Client는 투사체, 스파이크, 몬스터, 약점 생성을 결정하지 않는다.
* Client는 약점 위치를 결정하지 않는다.
* Client는 Host/서버에서 동기화된 상태 및 프레젠테이션 cue를 수신할 수 있다.
* Client의 VFX, 사운드, 애니메이션 재생 경로는 존재하며 프레젠테이션 전용이다.
* 보스 전용 UI 구현이 없으므로 Client 보스 UI 표시는 검증되지 않았다.
* Client 권한 기반 보스 로직 또는 임시 Client 우회 코드는 추가되지 않았다.
* 이 Phase에서는 컴파일 및 Play Mode 실행을 수행하지 않았으므로 Unity 내부에서 Client 기준 실제 동작 검증이 추가로 필요하다. 
