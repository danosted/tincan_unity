Status: Approved (2026-09-27; roadmap slices are planned and approved one at a time)

# Architecture sweep: where TinCan meets its vision, where it doesn't, and what to do

## Context
The developer asked for an honest sweep of the architecture: pitfalls, bad practice, and gaps against the vision in
`.docs/ARCHITECTURE.md` and `.docs/CODE_STANDARDS.md`. This file is the review plus a prioritised roadmap. Each fix is
a separate slice, planned and approved on its own before implementation, and none starts without the developer's
go-ahead. Evidence was gathered read-only at `5424351`.

## Verdict (short)
- **The vision is sound, and the newest code follows it:** feature installers, targeting, items, gameplay cues, the
  scenario harness. Pure processors, thin mediators, tests, and data-driven assets are the norm there.
- **The core does not:**
  - `NetworkMediator` is a base class with the wrong responsibilities.
  - GAS is neither tick-based nor single-owner, so it quietly breaks the prediction pillar.
  - `ProjectLifetimeScope` is still a god root.
  - Three server RPCs trust the client.
- **Biggest structural risk:** rules are kept by convention and review (docs, AGENTS.md) rather than by the compiler or
  tests, so drift is found late.

## Findings, by severity

### A. Violations with real consequences

1. **GAS ticks player effects twice, on two clocks** **Done (2026-09-27, `gas-core-correctness.md`; the structural fix goes to slice 4).** (breaks "one ticking owner", ARCHITECTURE §3).
   - Each player registers two ability controllers with one `Id`: `HumanoidPlayer` and its `AbilityNetworkMediator`
     (`ActorOrchestrator.RegisterHierarchy` collects every `IAbilityControllerBase`).
   - The mediator is not an `ISimulatedActor`, and its `IsSimulating` is `IsSpawned`
     (`Network/Infrastructure/NetworkMediator.cs:18`). So `AbilitySystemUseCase.Tick` expires the owner's predicted
     effects every frame, and `ProcessAbilitySimulation` does it again every tick.
   - Effect time is wall-clock server time (`ActiveGameplayEffect.IsExpired`, `ProjectTimeService.Time` reads
     `NetworkManager.ServerTime`), not a tick count. So owner and server can disagree by a tick on when a window ends,
     and a replay never re-evaluates it.
   - **Fix:**
     - GAS registers exactly one controller per actor, and the ability mediator is not a separate actor.
     - Effect duration becomes a tick count taken from the simulation tick.
     - Ticked actors are found by capability (`ISimulatedActor` on the actor), not by the type of the registered
       component.
2. **`NetworkMediator` base class does too much** **Done (2026-09-27, `network-entities.md`: entities own identity and registration; possession is a component).** (`Network/Infrastructure/NetworkMediator.cs`).
   - Every subclass (the ability mediator, fixtures, repair points, flying cans) becomes an `IPossessable` and an
     `IInteractionRequester`, and carries a possessor `NetworkVariable`, the legacy `RequestInteractionServerRpc`, and
     a `RegisterHierarchy` call.
   - Two mediators on one GameObject (the player, the ship) each register the whole hierarchy. The registries dedupe,
     but the first despawn unregisters everything while the other is still live.
   - **Fix:** a slim base that only carries identity and hands its hierarchy to the orchestrator once per
     `NetworkObject` (for example the root mediator only). Possession and interaction become opt-in components.
3. **Server trusts the client in three RPCs.** **Done (2026-09-27, `trust-fixes.md`): build mode removed, the tag RPC and the event trigger deleted.**
   - `AbilityNetworkMediator.RequestTagChangeServerRpc`: any owner can add any tag to itself. Only build mode uses it.
   - `BuildPlacementNetworkMediator.RequestPlacementServerRpc(prefabName, …)`: the server instantiates any loaded
     GameObject found by name through `Resources.FindObjectsOfTypeAll<GameObject>()`. That is slow and unvalidated.
   - `CoordinatedEventMediator.TriggerEventRpc`: any client triggers any event, with no ownership or range check.
   - **Fix:**
     - Build mode becomes an input bit or ability (`GA_BuildMode` already exists), and the tag RPC is removed.
     - Placement resolves an id from a registered buildables catalog (as `ItemCatalog` does), and the server validates
       the pose.
     - The event trigger goes through interaction.
   - This also fits "no side-channels" (ARCHITECTURE §3).
4. **Tag semantics differ between peers.** **Done (2026-09-27, `gas-core-correctness.md`); names stay on the wire.** The server's `HasTag` matches parent tags
   (`GameplayTagContainer.HasTag` → `IsChildOf`); clients match exact names (`ClientTagState`). A predicted
   `CanActivateAbility` can therefore disagree with the server as soon as someone uses a parent tag in a requirement.
   - **Fix:** clients resolve names to tags through the registry and use the same `IsChildOf` check. Better: tags
     travel as registry ids.

### B. Structural debt against the vision
5. **`ProjectLifetimeScope` is still the god composition root** (282 lines, 46 registrations).
   - It holds serialized configs, spawn logic for the airship and mediators, `FindAnyObjectByType` factories, and
     scene scans (`FindObjectsByType<MonoBehaviour>` for `IInjectedView`).
   - Core registrations are mixed with legacy feature registrations (cloud, gas challenge, build mode, door, boarding,
     free camera).
   - **Fix:** keep migrating features to installers (the feature index already lists them as direct). Core spawning
     moves to a small `CoreSessionInstaller`. The target is a root that only loads installers and core registries.
6. **Logic in untestable Assembly-CSharp.**
   - `BuildModeUseCase` (234 lines), `ModuleSpawningService`, `NetworkSimulationScheduler` and `ActorOrchestrator`
     live where no test can reach them. Of all files, 3,170 lines sit in Assembly-CSharp.
   - `HumanoidPlayer` implements seven roles, including `IBuilder` crafting fields and cue relaying.
   - **Fix:** move use cases into `Features`, and split `HumanoidPlayer` into focused components (input submission,
     prediction state, building).
7. **Global and scene lookups** that the DI and registry pillars forbid.
   - `NetworkManager.Singleton` in `ProjectTimeService` and `ThirdPersonLookView`.
   - `Camera.main` in `InteractorControllerView`, `BuildModeUseCase` and `CloudEnvironmentView`.
   - `FindObjectsByType` in `GasChallengeUseCase` (a use case scanning the scene instead of a registry).
   - `GASVisualScriptingBridge` finds the scope.
   - **Fix:** an injected `ILocalViewCamera` (the possessed camera, which also owns the new `AudioListener`), time
     from the scheduler, and gas pockets registered through `ActorOrchestrator`.
8. **Two tick domains used loosely.** Server-authoritative gameplay runs per frame (`ITickable`) in
   `GasChallengeUseCase` and `ShipFixtureSpawningUseCase`, and via `Update()` in mediators such as `AirshipDoor` and
   `CoordinatedEventMediator`. Rule: authoritative state changes only on the simulation tick
   (`ISimulationTickable`); per-frame is presentation only. Document it and move the offenders.
9. **The airship is outside the prediction model** (documented as a legacy exception). `AirshipMovementUseCase` has
   no tests and does not tick GAS. Fine for now, but it is the next feel problem after humanoids.
10. **GAS completeness.** Grants are per peer (not replicated). `CostEffect`, `AbilityTag` and
    `Cancel/BlockAbilitiesWithTag` are declared and unused. `AbilitySystemUseCase` is 526 lines and mixes activation,
    effects, attributes and cues. Either finish the features or delete the dead fields; split effects and attributes
    into their own classes.

### C. Hygiene and convention drift
11. **Naming:** four `NetworkBehaviour`s break the `*NetworkMediator` rule: `AirshipControlPanel`, `AirshipDoor`,
    `ToggleShipTagStation` and `CoordinatedEventMediator`. Also one stray namespace,
    `Assets.Scripts.Features.Airship`, and two `IShipState` interfaces.
12. **`#nullable enable`:** 223 of 343 files. Also `Debug.Log` noise in `NGONetworkService`,
    `BuildPlacementNetworkMediator` and possession, where `IEventPublisher` should be used.
13. **Untested logic:** `AirshipMovementUseCase`, `ModulePlacementUseCase`, `EventOrchestratorUseCase`,
    `GasChallengeUseCase`, `PossessionUseCase`, `PlayerLookUseCase`, `VehicleBoardingUseCase`, the free camera
    processors, and `BuildModeUseCase`.
14. **Dead or legacy code:** **Mostly done (2026-09-27): `MaintenanceUseCase`, the legacy interaction RPC and the legacy ship points are gone; `PossessionCameraResponder` and the scaffold folders remain.** `MaintenanceUseCase`, the legacy interaction RPC, `ShipRepairPoint`/`ShipDamagePoint` on
    `Airship_Prefab` (already the planned cleanup slice), `PossessionCameraResponder` (unused), and the scaffold
    folders.
15. **String-typed links:** interaction handlers stored as type names, menu `CommandId` strings, HUD keys. Covered by
    tests only in part.

### D. Process
16. **Conventions live in prose.** Six docs plus AGENTS.md carry rules that nothing checks, so drift (naming,
    Singleton use, registrations in the root, missing tests) is found by review or not at all.
    - **Fix, highest leverage:** an `ArchitectureRulesTests` EditMode suite using reflection and a source scan.
      - Every `NetworkBehaviour` ends in `NetworkMediator`.
      - No `NetworkManager.Singleton`, `Camera.main` or `Find*` in `TinCan.Features`.
      - `ProjectLifetimeScope` registrations stay at or below a ratchet number.
      - Every `*Processor` and `*UseCase` has a test.
      - Every new file has `#nullable enable`.
    - It starts with an allowlist of today's offenders, which may only shrink.
    - **Done (2026-09-27, `architecture-rules-tests.md`).** The suite found more than this review listed:
      `HumanoidPlayer` and `NetworkTransformMediator` also break the naming rule (C11). `PossessionInteractionHandler`,
      `ActivateAbilityInteractionHandler`, `DoorInteractionHandler`, `InteractivityUseCase` and the DevTools
      `ScenarioUseCase`/`NetworkConditionsUseCase` also have no test (C13). The scope has 52 registration calls.
17. **Standalone builds are blocked** (Visual Scripting AOT stubs, skybox sample). Nothing proves the game builds;
    add a build check once it is unblocked.
18. **DevTools ships in player builds** (installer asset in `Resources/Installers`). It is inert, but it should sit
    behind a define or an editor-and-development-only installer.

## Roadmap (recommended order; each is its own plan and approval)
1. **Architecture rules tests (D16).** Cheap, stops new drift, and turns this review into a checklist that only
   shrinks.
2. **GAS core correctness (A1, A4).**
   - One controller per actor.
   - Tick-count durations.
   - One tag-matching rule on every peer, with tags carried as ids.
   - Verify with `NetCatch`, `RepairLoop` and `EquipCycle`, plus new tests for expiry on owner and server.
3. **Trust fixes (A3).** Build mode through `GA_BuildMode`; remove `RequestTagChangeServerRpc`; a buildables catalog
   with server validation.
4. **`NetworkMediator` slimming (A2).** Possession and interaction become opt-in; one registration per
   `NetworkObject`. Combine with the planned cleanup slice (legacy interaction RPC, `MaintenanceUseCase`).
5. **Composition root diet (B5, B6, B7).** Migrate the remaining direct features to installers one at a time; add
   `ILocalViewCamera`; move `BuildModeUseCase` into `Features`.
6. **Tick-domain rule (B8)** and GAS split and cleanup (B10).

## Verification (per slice)
- `.\.tools\verify.ps1 -Scenario <affected>` for every scenario, solo and host + client. All eight must stay green.
- EditMode suite green, plus the slice's new tests. The rules suite ratchets down.
- A human playtest where feel is involved (GAS timing: net swing, repair).
