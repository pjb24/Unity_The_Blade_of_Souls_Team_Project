# Boss Phase 00 Repository Analysis

## Phase Scope

Phase 0의 목적은 기존 시스템 재사용 경계와 Boss 전용 신규 구현 경계를 확정하는 것이다.

이번 Phase에서는 코드 구현, `BossPatternData` 구현, `BossController` 구현, 실제 패턴 구현을 수행하지 않는다.

## Confirmed Existing Systems

| 영역 | 확인 파일 | 판단 |
| --- | --- | --- |
| Health | `Assets/_Workspace/ParkJaeBok/Scripts/Health/HealthComponent.cs`, `HealthCore.cs` | 재사용한다. Boss 체력도 `HealthComponent`와 `IHealthListener` 이벤트를 기준으로 연결한다. |
| Damageable | 별도 `Damageable` 클래스/인터페이스 없음 | 기존 Damageable 역할은 `HitReceiver` + `HealthComponent` 조합이 담당한다. Boss도 별도 Damageable 계층을 새로 만들지 않는다. |
| Hit | `Assets/_Workspace/ParkJaeBok/Scripts/HitCore/HitReceiver.cs`, `HitRequest.cs`, `HitResult.cs` | 재사용한다. Boss 피격 판정 진입점은 `HitReceiver.ReceiveHit`로 통일한다. |
| Player Attack 연동 | `Assets/_Workspace/ParkJaeBok/Scripts/ActionSystem/Combat/ActionHitBridge.cs`, `Assets/_Workspace/ParkJaeBok/Scripts/Attack/AttackExecutor.cs` | 재사용한다. Boss 피격 후 Hit/Break/Die 액션 전환은 `ActionHitBridge`와 동일한 방식으로 확장한다. |
| Projectile | `EnemyAI/Ranged/PooledRangedProjectile.cs`, `PooledProjectileSpawnService.cs`, `IProjectileSpawnService.cs` | 재사용 가능하다. 직선형 2D 투사체 패턴은 기존 서비스로 생성한다. |
| ObjectPool | `PooledProjectileSpawnService`, `EffectPool`, `AudioManager` 내부 SFX 풀 | 범용 ObjectPool은 없음. Projectile, VFX, SFX별 전용 풀은 재사용한다. Boss 전용 다수 오브젝트는 전용 풀 래퍼가 필요하다. |
| NetworkObject Pool | 검색 결과 전용 구현 없음. `DefaultNetworkPrefabs.asset`만 존재 | 현재 재사용 가능한 NetworkObject Pool은 확인되지 않았다. Boss용 NetworkObject Pool은 새 구현 또는 NGO Prefab Handler 설계가 필요하다. |
| VFX | `VFX/Runtime/EffectService.cs`, `EffectPool.cs`, `EffectRequest.cs`, `EffectCatalog.cs` | 재사용한다. Boss 연출 VFX는 `E_EffectId`/`EffectDefinition` 등록 후 `EffectService.Play`로 호출한다. |
| Sound | `Audio/AudioManager.cs`, `SoundDatabase.cs`, `E_SoundId.cs` | 재사용한다. Boss SFX/BGM은 `SoundDatabase` 등록 후 `AudioManager` 호출로 처리한다. |
| Animation | `ActionSystem/Animation/ActionAnimationPresenter.cs`, `EnemyAI/EnemyAnimationBridge.cs`, `ActionSystem/Markers/ActionMarkerReceiver.cs` | 재사용한다. Boss 패턴 타이밍은 Animation Event/Action Marker를 우선 사용한다. |
| Network 흐름 | `EnemyNetworkAuthorityUtility.cs`, Enemy AI RPC 패턴 | 재사용한다. 서버 권위 실행, 관찰자 연출 RPC, `NetworkTransform` 동기화 방식을 따른다. |
| Player 탐색 | `EnemyAI/EnemyTargetDetector.cs` | 재사용한다. Boss 타겟 탐색은 LayerMask + Tag + Health 유효성 검사 기준을 유지한다. |
| Enemy Spawn | 플레이어 스폰 외 Enemy 전용 스포너 미확인 | Boss는 기존 Enemy Spawn 시스템 호출 대상으로 확정할 수 없다. 씬 배치형 또는 Boss 전용 Spawn/Encounter 진입점이 필요하다. |

## Reuse Decisions

1. Boss 생명주기는 `HealthComponent`를 단일 체력 소스로 사용한다.
2. Boss 피격 진입점은 `HitReceiver`로 통일하고, `HealthComponent.ApplyDamage`를 직접 우회하지 않는다.
3. Boss 사망/피격/브레이크 연출 전환은 기존 `IHealthListener`, `IHitListener`, `ActionController` 계층과 연결한다.
4. 직선 투사체 패턴은 `IProjectileSpawnService.RequestSpawn`을 우선 사용한다.
5. VFX는 `EffectService`, Sound는 `AudioManager`, Animation은 `ActionAnimationPresenter` 및 Animation Event 기반 흐름을 재사용한다.
6. 네트워크 판정은 기존 Enemy 흐름처럼 서버 권위로 처리하고, 클라이언트는 RPC로 연출만 복제한다.
7. Player 탐색은 `EnemyTargetDetector`를 재사용하거나 Boss 전용 타겟 선택기가 이를 감싼다.

## Boss Dedicated Classes To Implement In Later Phases

| 클래스 | 역할 |
| --- | --- |
| `BossController` | Boss 생명주기, 타겟, 상태, Phase 전환을 총괄한다. Phase 0에서는 구현하지 않는다. |
| `BossPatternData` | 디자이너가 편집할 패턴 정의 데이터. Phase 0에서는 구현하지 않는다. |
| `BossPatternRunner` | 선택된 패턴의 시작/대기/종료 시퀀스를 실행한다. |
| `BossPatternSelector` | 체력 비율, 쿨다운, 거리, 이전 패턴을 기준으로 다음 패턴을 선택한다. |
| `BossTargetProvider` | `EnemyTargetDetector` 기반 타겟 조회를 Boss 의사결정용 API로 노출한다. |
| `BossHealthAdapter` | `IHealthListener`로 Boss 상태 전환, Phase 전환, UI/연출 이벤트를 분리한다. |
| `BossHitReactionBridge` | `HitReceiver` 결과를 Boss 경직, 브레이크, 무적 구간과 연결한다. |
| `BossPatternNetworkRelay` | 서버 확정 패턴 시작/종료/연출 타이밍을 관찰자에게 RPC로 전달한다. |
| `BossProjectilePatternEmitter` | 기존 `IProjectileSpawnService`를 Boss 패턴 발사용으로 감싼다. |
| `BossAreaPatternEmitter` | 장판/범위 예고/틱 데미지처럼 기존 Projectile만으로 처리하기 어려운 패턴 판정을 담당한다. |
| `BossSpawnEncounterController` | 씬 배치 또는 Boss 전용 생성 진입점을 관리한다. 기존 Enemy Spawn 미확인으로 신규 필요하다. |

## Gaps Not Covered By Existing Systems

| 영역 | 기존 한계 | 신규 필요 |
| --- | --- | --- |
| Boss 패턴 데이터 | 기존 `BossPatternController`는 Inspector 배열 기반 Action 요청만 처리한다. 체력 Phase, 쿨다운, 조건, 연출 타임라인을 표현하기 부족하다. | `BossPatternData`와 선택/실행 계층 |
| Boss 상태 총괄 | 일반 Enemy AI는 추적/공격/복귀 중심이다. Boss Phase 전환, 무적 구간, 패턴 락을 직접 표현하지 않는다. | `BossController`, `BossPatternRunner` |
| 범위/장판 판정 | 기존 Projectile은 직선 이동과 Trigger 충돌 중심이다. 원형/부채꼴/지속 장판/텔레그래프 판정은 없음. | Boss 전용 Area Hitbox/Telegraph 시스템 |
| Enemy Spawn | Enemy 전용 Spawn Manager 또는 Monster Spawn 시스템이 확인되지 않았다. | Boss Encounter 시작/종료 진입점 |
| NetworkObject Pool | NGO용 Pool/Prefab Handler 구현이 확인되지 않았다. | 네트워크 오브젝트가 필요한 Boss 소환물/패턴용 풀 설계 |
| 패턴 연출 동기화 | Enemy는 상태/트리거/투사체 시각 복제를 개별 RPC로 처리한다. Boss 패턴 단위 타임라인 동기화는 없음. | Boss 패턴 이벤트 RPC와 시드/타임스탬프 동기화 |

## Pattern Creation Decisions

| Pattern 유형 | 생성 방식 |
| --- | --- |
| 근접 단발/연속 공격 | `ActionController` + Animation Event/Marker로 타이밍을 열고 Boss 전용 Hitbox를 활성화한다. |
| 직선 투사체 | `IProjectileSpawnService.RequestSpawn`으로 생성한다. 서버는 실제 판정 Projectile, 관찰자는 visual-only Projectile을 사용한다. |
| 원형/부채꼴/장판 공격 | Boss 전용 Area Pattern Emitter가 Collider/Overlap 기반으로 생성한다. 기존 Projectile로 억지 구현하지 않는다. |
| 예고 VFX | `EffectService.Play(EffectRequest)`로 생성하고, 패턴 시작 RPC에서 관찰자도 동일하게 재생한다. |
| 사운드 | `AudioManager.PlaySfx(E_SoundId, Transform 또는 Vector3)`로 재생한다. 필요 시 Boss 전용 SFX Bridge가 호출 시점을 관리한다. |
| 소환/추가 Enemy | 기존 Enemy Spawn 미확인으로 Phase 0 기준 확정 보류. 이후 Boss 전용 Spawn Encounter에서 설계한다. |

## Pattern Hit Decisions

| Pattern 유형 | 판정 방식 |
| --- | --- |
| Player가 Boss를 공격 | Player 공격 계층이 `HitReceiver.ReceiveHit`를 호출하고 Boss `HealthComponent`로 피해가 전달된다. |
| Boss 근접 공격 | 서버 권위에서 Boss 전용 Hitbox가 Player `HitReceiver`를 찾아 `HitRequest`를 전달한다. |
| Boss 직선 투사체 | 서버 Projectile만 판정한다. `PooledRangedProjectile`이 `HitReceiver`를 찾아 피해를 전달한다. |
| Boss 장판/범위 공격 | 서버 권위에서 `Physics2D.Overlap...` 계열로 Player 후보를 찾고, 중복 HitId 방지 후 `HitReceiver`에 전달한다. |
| 브레이크/특수 피격 | `HitRequest.StatusTag`와 `ActionHitBridgeProfile` 방식처럼 상태 태그 기반으로 분기한다. |

## Presentation Sync Decisions

| 연출 | 동기화 방식 |
| --- | --- |
| Boss 이동 | 필요 시 기존 Enemy처럼 `NetworkTransform`을 사용한다. |
| Boss 상태/애니메이션 | 서버 확정 후 관찰자 RPC로 Animator 파라미터 또는 Action 상태를 적용한다. |
| Projectile 시각 | 기존 `StationaryRangedEnemyController`처럼 서버 Projectile과 관찰자 visual-only Projectile을 분리한다. |
| VFX/SFX | 서버가 패턴 이벤트를 확정하고 관찰자 RPC에서 동일 ID, 위치, 방향, 시작 시간을 재생한다. |
| 판정 | 클라이언트 연출과 별개로 서버만 실제 피해 판정을 수행한다. |

## ObjectPool Availability

- 사용 가능: `PooledProjectileSpawnService`는 prefab별 Queue 기반 풀을 제공한다.
- 사용 가능: `EffectService`/`EffectPool`은 VFX별 풀, prewarm, fallback policy를 제공한다.
- 사용 가능: `AudioManager`는 SFX AudioSource 풀을 제공한다.
- 미확인: 범용 `ObjectPool<T>` 또는 게임 오브젝트 공용 풀은 없다.
- 결론: Boss 직선 Projectile/VFX/SFX는 기존 풀을 재사용한다. Boss 장판, 경고 범위, 다단 Hitbox는 Boss 전용 풀 또는 재사용 가능한 일반 풀을 별도 설계해야 한다.

## NetworkObject Pool Availability

- `NetworkObjectPool`, `INetworkPrefabInstanceHandler`, NGO Pool 구현은 검색되지 않았다.
- `Assets/DefaultNetworkPrefabs.asset`은 Network Prefab 목록이며 Pool 구현은 아니다.
- 결론: Boss Phase 0 기준으로 NetworkObject Pool은 재사용 불가로 판단한다. 실제 네트워크 소환물 패턴이 필요해지는 Phase에서 전용 설계를 진행한다.

## Projectile Flow Confirmation

1. `StationaryRangedEnemyController`가 서버 권위에서 공격 타이밍을 결정한다.
2. Animation Event가 `OnFireProjectile`을 호출한다.
3. `IProjectileSpawnService.RequestSpawn`으로 `PooledRangedProjectile`을 가져온다.
4. 서버 Projectile은 Trigger 충돌 시 Target Layer/Tag를 검사한다.
5. 대상 계층에서 `HitReceiver`를 찾고 `HitRequest`를 전달한다.
6. 서버 Projectile despawn 이벤트가 관찰자 visual-only Projectile despawn RPC로 복제된다.

## Enemy Spawn Flow Confirmation

- 일반 Enemy 전용 Spawn 시스템은 확인되지 않았다.
- 기존 멀티플레이어 Spawn 흐름은 Player 중심이다. `PlayerSpawnCoordinator`와 Session Backend가 PlayerObject를 생성한다.
- Enemy AI는 씬 배치된 `NetworkObject`/`NetworkBehaviour`가 서버 권위로 동작하는 흐름이 중심이다.
- 결론: Boss는 기존 Enemy Spawn 시스템 호출 대상으로 볼 수 없으며, Boss Encounter 시작 지점을 별도로 설계해야 한다.

## Health And Damageable Connection

1. 외부 공격은 `HitRequest`를 만든다.
2. 대상 `HitReceiver.ReceiveHit`가 입력 검증, 중복 HitId, 무적, 사망 상태를 검사한다.
3. `HitReceiver`가 `DamageContext`를 만들어 `HealthComponent.ApplyDamage`를 호출한다.
4. `HealthComponent`는 `HealthCore.ApplyDamage`로 실제 체력 변경을 수행한다.
5. `IHealthListener` 구현체가 `OnDamaged`, `OnDied`, `OnRevived` 이벤트를 받아 상태와 연출을 전환한다.
6. 별도 `Damageable` 시스템은 없으므로 Boss도 이 연결 구조를 따른다.

## Final Phase 0 Decision

Boss 시스템은 기존 Health, Hit, Projectile, VFX, Sound, Animation, Network Authority, Player Target 탐색을 재사용한다.

새로 구현해야 하는 핵심은 Boss 전용 상태 총괄, 패턴 데이터/선택/실행, 범위 패턴 판정, 패턴 단위 네트워크 연출 동기화, Boss Encounter 진입점이다.

기존 시스템으로 처리 가능한 영역과 처리 불가능한 영역이 분리되었으므로 Phase 1 이후에는 위 경계를 기준으로 Boss 전용 코드를 추가한다.
