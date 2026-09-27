Status: Done (2026-09-27)

# GAS core correctness (slice 2 of `architecture-review.md`: A1, A4)

## Context
Verified at `5424351` plus the slice 1 working tree.
- **Two controllers per player.** `NetworkPlayer.prefab` has `HumanoidPlayer` and `AbilityNetworkMediator`. Both
  implement `IAbilityControllerBase` and share one `Id`, and `ActorOrchestrator.RegisterHierarchy` registers both.
  `AbilitySystemUseCase.Tick` skips the player (`ISimulatedActor`) but not the mediator, whose `IsSimulating` is
  `IsSpawned`. Player effects therefore expire every frame (on every peer, not just owner and server) and again on
  every simulation tick in `ProcessAbilitySimulation`.
- **Wall-clock time.** Effect expiry, cooldowns (`AbilitySpec.IsOnCooldown`) and timing windows all read
  `ITimeService.Time`, which is `NetworkManager.ServerTime` (the owner's estimate of it on a client). Owner and server
  can disagree by a tick on when a window closes.
- **Replay skips GAS.** `HumanoidMovementUseCase.RewindAndReplay` re-integrates movement only. This stays out of scope:
  tags stay server-authoritative, and the owner's predicted effect tags come from its first pass.
- **Tag matching differs.** Server: `GameplayTagContainer.HasTag` → `GameplayTag.IsChildOf` (parent tags match).
  Client: `ClientTagState.Has(name)`, exact name only.
- Other lone `AbilityNetworkMediator`s, where the mediator is the only controller: `Airship_Prefab`,
  `ShipDamageSockets`, `Cannon_Module`. Those stay controllers, ticked by the global loop.

## Design
1. **One controller per actor (registration).** `ActorOrchestrator` registers one `IAbilityControllerBase` per `Id`
   in a hierarchy, chosen by a pure `AbilityControllerSelection` in `Features/Abilities` (testable; the orchestrator
   lives in Assembly-CSharp). An `ISimulatedActor` controller wins (`HumanoidPlayer`); otherwise the first. The
   mediator's `Id` lookup is unchanged: it depends on `HumanoidPlayer` coming first on `NetworkPlayer.prefab`
   (verified); slice 4 removes that dependency. Ship, cannon and sockets are unchanged. Making the mediator stop being a
   controller at all is the structural fix; it belongs to slice 4 (`NetworkMediator` slimming).
2. **Ticked by capability.** `AbilitySystemUseCase` selects actors by capability: an `ISimulatedActor` is ticked only
   by its movement loop, any other simulating controller by the global loop. The global loop moves from `ITickable`
   (per frame) to `ISimulationTickable` (`AfterHumanoid`), so every GAS clock advances on the simulation tick.
3. **Tick-count durations.** `ITimeService` gains `int Tick` (the scheduler's tick) and `int TickRate`.
   `ActiveGameplayEffect`, `AbilitySpec` cooldowns and timing windows store a start tick and a length in ticks
   (`ceil(seconds × tickRate)`, computed once on apply). Assets keep authoring seconds. Outside a session (EditMode
   tests, offline) a fake time service supplies ticks.
4. **One tag-matching rule.** A pure `GameplayTagMatch.Matches(IEnumerable<GameplayTag> held, GameplayTag query)` is
   used by `GameplayTagContainer.HasTag` and by the client path. `ClientTagState` resolves held names through
   `IGameplayTagRegistry`; without the registry (installer switched off) it falls back to exact names, as today.
   Names still travel on the wire (see decision C).

## Tests
- `AbilityRegistryTests` / `ActorOrchestratorTests`: a player-shaped hierarchy registers exactly one controller.
- `AbilitySystemUseCase`: a predicted actor's effect expires once, on tick N, not per frame; the global loop skips it.
- `ActiveGameplayEffect`, cooldowns, timing windows: expiry on exact tick boundaries, same result for the same input
  sequence on "owner" and "server" fakes.
- `GameplayTagMatch` and `ClientTagState`: a parent tag in a requirement matches a held child tag on both paths.
- Scenarios, solo and host + client: `NetCatch`, `RepairLoop`, `EquipCycle`, then all eight.
- Human playtest: net swing timing and the repair loop on a client with latency.

## Docs
- `ARCHITECTURE.md` §3: GAS durations are in ticks; one controller per actor.
- `CODE_MAP.md` known traps: remove the double-controller trap if it is listed; add "durations are ticks".
- `architecture-review.md`: mark A1 and A4 done.

## Decisions (developer, 2026-09-27)
- A: registration rule now; the structural fix goes to slice 4.
- B: the global network tick (`NetworkTickSystem.LocalTime.Tick`).
- C: names on the wire, matched through the registry.
- Core files touched on purpose (this is core work, not a feature): `ITimeService`, `ProjectTimeService`,
  `NetworkSimulationScheduler` (passes the tick), `ProjectLifetimeScope` (GAS registers as `ISimulationTickable`
  instead of `ITickable`; no new registration), `ActorOrchestrator`.

## Outcome (2026-09-27)
- EditMode 443/443, including `GasTickTimingTests` (10 cases: tick rounding, expiry on the same tick count from
  different start ticks, cooldown before first activation, global loop vs simulated actor, timing window ticks 3..9,
  one controller per `Id`, parent-tag match on client and server).
- Scenarios, solo and host + client: all eight pass. In the first batch run, `RepairLoop` (the client never joined)
  and `TagRequest` failed. Both passed when run again on their own, unchanged; this looks like harness flakiness on
  back-to-back runs.
- Side effects: `ProjectTimeService` takes `NetworkManager` by injection (one `NetworkManager.Singleton` fewer). A
  cooldown no longer applies before an ability's first activation (it did at server time 0).
- Still open for a human: net swing and repair timing on a lagged client (the window lengths are now whole ticks,
  rounded up).
