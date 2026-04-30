# Boss Phase 22 싱글플레이 검증

## 범위

* 이 검증은 싱글플레이 동작으로 제한된다.
* Phase 22에서는 Host 전용 및 Client 전용 검증을 의도적으로 제외한다.
* 저장소 지침에서 컴파일 시도를 금지하고 있으므로 Unity Play Mode 실행 및 컴파일은 수행하지 않았다. 

---

## 검증 방법

* 싱글플레이 권한 처리를 위한 보스 런타임 코드 경로를 검토하였다.

* Fallback 경로에서 명시적으로 Warning 로그가 출력되는지 확인하였다.

* 보스 런타임 코드에서 다음 금지 패턴을 확인하였다.

  * `ServerRpc`
  * `ClientRpc`
  * `public event`
  * `Vector3.Distance`
  * LINQ 선택 헬퍼
  * 보스 코드 내에서 직접 이벤트 구독 연산자 사용

* 보스 런타임 코드에서 이동 관련 값 쓰기가 있는지 확인하였다. 

---

## 싱글플레이 권한

* `BossController.IsBossLogicAuthority()`는 `NetworkManager.Singleton`이 null일 때 true를 반환한다.
* `BossController.IsBossLogicAuthority()`는 `NetworkManager.Singleton.IsListening`이 false일 때 true를 반환한다.
* 패턴 실행 경로는 선택, 피격, 데미지, 생성, 약점, Groggy, Dead 로직을 결정하기 전에 권한 체크를 수행한다. 

---

## 라이프사이클 검증

| 요구사항           | 검증 경로                                                                                                 | 결과         |
| -------------- | ----------------------------------------------------------------------------------------------------- | ---------- |
| StartBattle 동작 | `BossController.StartBattle()`는 사용 횟수 카운터, 쿨타임, Warning Guard, 플래그를 초기화한 후 Idle 상태로 진입한다.             | 코드 검토로 확인됨 |
| ResetBattle 동작 | `BossController.ResetBattle()`는 타이머를 중지하고 런타임 상태를 초기화한다.                                              | 코드 검토로 확인됨 |
| StopBattle 동작  | `BossController.StopBattle()`는 패턴 선택을 비활성화하고, 현재 패턴을 취소하며, 타이머를 중지하고 Dead 상태가 아니면 Idle로 돌아간다.         | 코드 검토로 확인됨 |
| Dead 동작        | `BossController.OnDied()`는 Health 리스너 흐름을 사용하며 권한이 있을 때 Dead 정리 로직을 호출한다.                             | 코드 검토로 확인됨 |
| Groggy 동작      | `StartGroggyForDuration()`는 Groggy 상태에 진입하고 타이머를 시작한 뒤 `EndGroggyState()`에서 Idle로 돌아가며 글로벌 쿨타임을 시작한다. | 코드 검토로 확인됨 |

---

## 패턴 검증

| 패턴        | 검증 경로                                                                                                                        | 결과         |
| --------- | ---------------------------------------------------------------------------------------------------------------------------- | ---------- |
| Selector  | `BossPatternSelector.TrySelectPattern()`는 전투 상태, Idle 상태, 쿨타임, 체력 Phase, 사용 횟수 제한, 타겟 필요 여부, 우선순위, 권한을 기준으로 판단한다.            | 코드 검토로 확인됨 |
| Pattern 1 | `BossFanProjectilePattern`은 타겟을 한 번만 결정하고, `sqrMagnitude`로 가장 가까운 스폰 위치를 선택하며, Projectile Spawn Service를 통해 설정된 투사체를 발사한다.   | 코드 검토로 확인됨 |
| Pattern 2 | `BossGroundSpikePattern`은 타겟을 한 번만 결정하고, Raycast로 지면을 확인하며, Fallback 시 Warning을 출력하고, 경고 지연 후 스파이크를 생성하며, 권한이 있을 때 피격을 적용한다. | 코드 검토로 확인됨 |
| Pattern 3 | `BossSummonMonsterPattern`은 스폰 위치를 검증하고, 버퍼를 재사용하며, 중복 없는 Partial Shuffle을 수행하고, Warning과 함께 Fallback 몬스터를 Instantiate 한다.   | 코드 검토로 확인됨 |
| Pattern 4 | `BossWeakPointPattern`은 영역을 검증하고, 재사용 가능한 버퍼로 위치를 선택하며, 약점을 생성하고, 타임아웃, 전체 파괴, Groggy, 보스 사망 정리를 처리한다.                       | 코드 검토로 확인됨 |

---

## 보스 이동 검증

* 보스 런타임 스크립트는 `transform.position`을 할당하지 않는다.
* 보스 런타임 스크립트는 `Rigidbody2D.MovePosition`을 호출하지 않는다.
* 보스 런타임 스크립트는 `Translate`를 호출하지 않는다.
* 보스 런타임 스크립트는 velocity를 작성하지 않는다.
* 결과: 싱글플레이에서 보스 전투 로직은 보스를 이동시키지 않는다. 

---

## Fallback 검증

| Fallback                          | Warning 경로                                                                                                             | 결과  |
| --------------------------------- | ---------------------------------------------------------------------------------------------------------------------- | --- |
| Health 비율 소스 없음                   | `BossController.GetCurrentHealthRatio()`는 fallback 비율을 반환하기 전에 Warning을 출력한다.                                          | 확인됨 |
| HealthPhase 데이터 없음                | `BossController.GetHealthPhaseIndex()`는 Warning을 출력한다.                                                                 | 확인됨 |
| 타겟 제공자 없음                         | `BossController.TryFindNearestPlayerForExecution()`은 Warning을 출력한다.                                                    | 확인됨 |
| 타겟 Fallback 탐색                    | `BossPlayerTargetProvider`는 fallback 탐색 사용 시 로그를 출력한다.                                                                 | 확인됨 |
| Projectile Spawn Service Fallback | `BossFanProjectilePattern.TryResolveProjectileSpawnService()`는 씬 fallback 사용을 로그로 남긴다.                                 | 확인됨 |
| 지면 스파이크 Raycast Fallback          | `BossGroundSpikePattern.ResolveSpikePosition()`은 실패 시 한 번 로그를 출력한다.                                                    | 확인됨 |
| 지면 스파이크 VFX Fallback              | `BossGroundSpikePattern.PlayLocalVfxOrWarn()`는 서비스/프리팹 없음 fallback을 로그로 남긴다.                                           | 확인됨 |
| 지면 스파이크 Instantiate Fallback      | `BossGroundSpikePattern.LogSpikeObjectPoolFallbackOnce()`는 fallback을 로그로 남긴다.                                          | 확인됨 |
| 몬스터 스폰 Fallback                   | `BossSummonMonsterPattern.WarnMissingExistingSpawnPathsOnce()`는 SpawnManager, EnemySpawner, ObjectPool 경로 누락을 로그로 남긴다. | 확인됨 |
| 약점 Instantiate Fallback           | `BossWeakPointPattern.LogWeakPointInstantiateFallbackOnce()`는 fallback을 로그로 남긴다.                                       | 확인됨 |
| 약점 애니메이션 이벤트 Fallback             | `BossWeakPointPattern.RunEntryFallbackTimer()`는 entry를 완료하고 fallback을 로그로 남긴다.                                         | 확인됨 |
| Presentation 매핑 Fallback          | `BossPresentationController.LogMissingCueSettingsOnce()`는 cue 매핑 누락을 로그로 남긴다.                                          | 확인됨 |
| 보스 상태 네트워크 Fallback               | `BossController.SyncBossStateToClients()`는 네트워킹 활성 상태에서 NetworkObject가 spawn되지 않았을 때 로그를 남긴다.                          | 확인됨 |

---

## ObjectPool 및 Instantiate Fallback

* Pattern 1은 가능할 경우 `IProjectileSpawnService` 및 기존 `PooledProjectileSpawnService` 경로를 사용한다.
* Pattern 2는 재사용 가능한 스파이크 ObjectPool이 없으면 Instantiate fallback을 사용하며 로그를 출력한다.
* Pattern 3은 SpawnManager, EnemySpawner, 몬스터 ObjectPool 경로가 없으면 Instantiate fallback을 사용하며 로그를 출력한다.
* Pattern 4는 WeakPoint ObjectPool이 없으면 Instantiate fallback을 사용하며 로그를 출력한다.
* 보스 런타임 코드에서 발견된 모든 Instantiate fallback 경로는 Warning 로그를 출력한다. 

---

## 정적 검사 명령

```powershell
rg -n "StartBattle|ResetBattle|StopBattle|TrySelectPattern|TryStartPatternExecution|IsBossLogicAuthority|NetworkManager\.Singleton|IsListening" Assets/_Workspace/ParkJaeBok/Scripts/Boss -g *.cs
rg -n "Pattern failure|Debug\.LogWarning|fallback|Fallback|Instantiate|ObjectPool|NetworkObject Pool|EffectService|AudioManager" Assets/_Workspace/ParkJaeBok/Scripts/Boss -g *.cs
rg -n "transform\.position\s*=|Rigidbody2D|velocity|MovePosition|Translate\(" Assets/_Workspace/ParkJaeBok/Scripts/Boss -g *.cs
rg -n "NetworkVariable|OnValueChanged|\+=|-=|ServerRpc|ClientRpc|public event|Vector3\.Distance|\.Where\(|\.Select\(|\.ToList\(" Assets/_Workspace/ParkJaeBok/Scripts/Boss -g *.cs
git diff --check -- Assets/_Workspace/ParkJaeBok/Scripts/Boss
```

---

## 결과

* 코드 검토 기준으로 싱글플레이 권한 경로는 유효하다.
* 보스 선택 및 모든 4개 패턴은 싱글플레이 권한 실행 경로를 가진다.
* Groggy 및 Dead 정리 경로가 존재한다.
* 보스 이동 코드는 발견되지 않았다.
* 보스 런타임 코드에서 확인된 모든 Fallback 경로는 Warning 로그를 출력한다.
* 무음 Fallback은 발견되지 않았다.
* 이 Phase에서는 컴파일 및 Play Mode 실행을 수행하지 않았으므로 Unity 내부에서의 실제 동작 검증은 추가로 필요하다. 
