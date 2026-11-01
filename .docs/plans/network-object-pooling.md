Status: Approved (2026-10-31: Phase 0 only; Phase 1 only if the numbers ask for it); seventh in [core-systems-roadmap.md](core-systems-roadmap.md)

# Pooling for spawned network objects

## Why
Hazards, flying crystals and gas pockets are spawned and destroyed all voyage long. Each spawn instantiates a
prefab and injects its whole hierarchy; each despawn destroys it. Managed allocation is the server's main perf
problem (`PERFORMANCE.md`: ~39 KB and ~590 allocations per frame), but nobody has measured how much of it is spawning.

## Today (verified 2026-10-31)
- Four spawners, each Instantiate + `Spawn` and `Despawn(true)` / `Destroy`: `FlyingCanSpawningService`,
  `GasPocketSpawningService`, `SkyHazardSpawningService`, `ModuleSpawningService`.
- `NetworkPrefabInterceptor` (`Core/Infrastructure/`) is an `INetworkPrefabInstanceHandler`: it instantiates, runs
  `InjectGameObject` and the actor registration, and destroys. It never reuses an instance.
- `GameplayCuePresenter` keeps a private pool for cue prefabs; the audio plan adds a pool of one-shot sources
  ([audio-mixer.md](audio-mixer.md)). There is no shared pool.
- Perf tooling can answer the question: `perf.ps1 run -ProfileFrames N` and `perf.ps1 analyze-gc <run>` sum `GC.Alloc`
  by the innermost TinCan method; `network_objects` is already a counter.

## Phase 0: measure (the only approved-to-start step once this plan is approved)
- A seeded CrewLoad run during a hazard zone and a gas field, with `-ProfileFrames`, then `analyze-gc`.
- Read the share of allocation and main-thread time under the four spawners, `NetworkPrefabInterceptor`,
  `InjectGameObject` and NGO's spawn path, on the server and on a client.
- Decision rule: build Phase 1 if spawning is more than ~10 % of server allocation or shows as spikes in
  `main_thread_ms` p99. Otherwise write the numbers here and close the plan.

## Phase 1: a core pool (only if Phase 0 says so)
- `NetworkObjectPool` in `Core/Infrastructure/`: one `INetworkPrefabInstanceHandler` per pooled prefab that takes
  from and returns to an inactive stack, and injects an instance once, when it is first made. Prewarm counts come from
  each feature's config.
- `IPooledNetworkObject` (Core.Domain): `OnTakenFromPool()` and `OnReturnedToPool()` for mediators that keep local
  state (subscriptions, a target list). Replicated state resets on spawn anyway; anything else must be listed here.
- Actor registration and deregistration still run on every take and return (the orchestrator must not see a pooled
  instance as alive).
- The four spawners switch one at a time, gas pockets first (short-lived, many per field).
- The exact NGO calls (how `Despawn` and the handler's `Destroy` behave on server and clients) are checked against the
  installed NGO version before building, not taken from memory.
- A plain `ComponentPool<T>` for non-networked presentation (cue prefabs, one-shot audio) could share the same code;
  optional.

## Tests and verification
- Phase 0: a perf run (no Editor takeover beyond the usual lock; containers by default).
- Phase 1: EditMode tests for the pool (take, return, prewarm, growth limit) and for actor registration on reuse;
  `perf.ps1` before and after on the same seed; `verify.ps1 -Scenario GasSquallEvent,HazardStrike,NetCatch` solo and
  host + client (asked for first), including a late joiner.

## Decisions (developer, 2026-10-31)
1. **Phase 0 can run at any point**: it is one perf run, so it does not wait for its place in the roadmap.
