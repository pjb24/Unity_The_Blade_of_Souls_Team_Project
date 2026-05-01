# Object Pool Integration Notes

## Applied Targets

- Local VFX fallback prefabs: `LocalObjectPoolManager` handles prefab-based reuse when `EffectService` catalog IDs are unavailable.
- Player Jump VFX: `CharacterVfxController` plays `JumpDust` through `EffectService` pool first, then uses `LocalObjectPoolManager` with `JumpDust Pool Prefab` if the catalog route is unavailable.
- Enemy ranged projectiles: existing `PooledProjectileSpawnService` remains the spawn API, and NetworkObject projectiles are routed through `NetworkObjectPoolManager`.
- Projectile hit effects: `_hitEffectPrefab` now uses the local pool instead of direct Instantiate.
- Boss ground spike objects: local prefabs use `LocalObjectPoolManager`; NetworkObject prefabs use `NetworkObjectPoolManager` with server authority.
- Boss weak point objects and destroy VFX: local prefabs use `LocalObjectPoolManager`; NetworkObject prefabs use `NetworkObjectPoolManager`.
- Existing `EffectService` / `EffectPool`: retained as the VFX catalog pool. `MaxPoolSize` overflow logs a Warning and the overflow instance is still tracked by the pool.

## Excluded Targets

- Player spawn prefabs are excluded because they are session lifecycle objects, not high-frequency combat temporaries. They require ownership, camera, input, and spawn-slot setup.
- Boss summoned monsters are excluded because they have long-lived AI, Health, checkpoint reset, and NetworkObject ownership state. They should be pooled only through a dedicated EnemySpawner lifecycle contract.
- CameraEffect runtime ScriptableObject clones are excluded because they are data instances created for channel isolation, not scene GameObject churn.
- OffscreenPlayerIndicator is excluded because each controller owns a single presenter instance and reuses it by show/hide instead of repeated creation.
- Singleton/manager duplicate `Destroy(gameObject)` calls are excluded because they clean invalid managers and are not repeated combat objects.

## Inspector Settings

- `PoolPrefabConfig.Prefab`: source prefab used as the pool key.
- `InitialCount`: prewarmed inactive instances created during manager initialization.
- `MaxCount`: recommended maximum created instances for that prefab. Runtime overflow logs a Warning, creates the required instance, and keeps it under pool management. `0` means no warning threshold.
- `AllowExpand`: permits runtime creation when the inactive queue is empty.
- `UseAutoReturn`: enables timed return for short-lived local-only objects.
- `AutoReturnSeconds`: delay used by `PooledAutoReturn` before returning to the local pool.
- `CharacterVfxController.JumpDustPoolPrefab`: local Pool fallback prefab for Player Jump VFX when `JumpDust` EffectService playback fails.
- `CharacterVfxController.JumpDustPoolFallbackLifetime`: fallback auto-return seconds passed to `LocalObjectPoolManager` when the Jump VFX prefab is not already configured with `UseAutoReturn`.
- `LocalObjectPoolManager.AllowRuntimePoolForUnregisteredPrefab`: logs a warning and permits runtime pool creation for unregistered local prefabs.
- `NetworkObjectPoolManager.AllowRuntimePoolForUnregisteredPrefab`: logs a warning and permits runtime pool creation for unregistered NetworkObject prefabs.

## Validation Checklist

- Single-player: spawn ranged enemies, boss ground spike, weak points, and VFX; confirm objects deactivate and reappear without stale owner, collider, particle, audio, or rigidbody state.
- Host: spawn NetworkObject projectiles/spikes/weak points; confirm only Host/Server calls Spawn and Despawn, and objects return to `Pool_Network_*` roots.
- Client: verify client-side NetworkObject spawn/despawn attempts log warnings and do not create authoritative objects; local VFX still plays through local pools.
- Profiler: compare GC Alloc and `Object.Instantiate` / `Object.Destroy` samples during repeated projectile fire, hit VFX, ground spike, and weak point cycles.
- Scene transition: confirm managers configured with `DontDestroyOnLoad` keep pool roots and do not duplicate without warnings.
