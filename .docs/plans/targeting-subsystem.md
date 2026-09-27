Status: Approved (2026-09-27)

# Targeting subsystem (cross-cutting), first slice: repair

## Context

The ship-damage work exposed that every feature answers "what is this actor aiming at?" on its own, with
different rules:

| Consumer | Today | Where |
|---|---|---|
| Interaction (E) | flat body ray at eye height, 3 m, first `IInteractable`. The server **trusts the client's target** (no range or line-of-sight check) | `Interaction/InteractorControllerView.cs`, `NetworkMediator.RequestInteractionServerRpc` |
| Net catch | sphere around a computed net-head point, nearest can | `Airship/Fuel/Minigame/CatchProcessor.cs` |
| Repair | nearest broken part in reach inside a horizontal cone | `Airship/Damage/RepairTargetProcessor.cs` |
| Build placement | camera ray against layer 8, local only | `Network/Infrastructure/BuildModeUseCase.cs` |
| Abilities | the target is passed in by the caller; GAS cannot acquire one | `Abilities/AbilitySystemUseCase.cs` |

Reusing the interaction ray for repair would couple two concerns. Interaction is one *use* of targeting, not
targeting itself. The goal is one targeting subsystem that every feature, and GAS, uses, with several aiming models
chosen per context: a precise ray for some things, a forgiving close-range cone scan for others.

Decisions taken with the developer:
- **Authority: the server recomputes.** Owner and server run the same query from the same input on the same tick.
  The owner predicts (prompts, predicted abilities); the server's result is authoritative. No trust in
  client-chosen targets.
- **Several aim models**, chosen per `TargetingDefinition`, not one global model.
- **Data-driven:** a `TargetingDefinition` asset, referenced by abilities (and later interactions), in the spirit
  of Unreal's GAS target data.
- **First slice migrates Repair only.** The other consumers are listed as later slices.

Constraints found in the code:
- Input carries a **yaw-only** `LookRotation` (`HumanoidControllerView.LookRotation`). Pitch lives only on the
  owner's orbital camera (`ThirdPersonLookView`), so the server cannot rebuild a pitched or camera aim today. Aim
  models that need pitch come in a later slice that adds pitch to `HumanoidInputState` (predicted and replayed like
  the rest of the input).
- **Facing follows the look input every tick** (found in RepairLoop). Body forward on the server is the owner's
  input yaw, so body-based aim is already authoritative.
- **Ship-local rule:** yaw is relative to the platform frame, and queries run in world space on each peer at that
  peer's ship pose. That is fine because both the aim and the targets are children of the same ship.
- **Registration:** `ActorOrchestrator.RegisterHierarchy` scans spawned hierarchies for capability interfaces
  (interactors, ability controllers). Targetables register the same way, and mediators never self-register.

## Design

### Contracts (`Assets/Scripts/Core/Domain/Targeting/`, a shared contract as allowed by the do-not-touch rule)
- `ITargetable`: `Guid Id`, `Vector3 AimPoint`, `bool IsTargetable`, `IAbilityControllerBase? Controller`, used for
  GAS tag filters. This lets any GAS actor be filtered by `State.*` tags.
- `ITargetableRegistry`: `Register`, `Unregister`, `All`. Filled by `ActorOrchestrator` (below).
- `ITargeter`: what aims. Its `TargetingOrigin` has eye position, body forward (yaw), platform and an optional aim
  pitch. `IHumanoidCharacterView` gets it through an adapter in the feature, not by changing the humanoid
  interfaces.

### Feature (`Assets/Scripts/Features/Targeting/`, `TargetingFeatureInstaller`, Order -18, `Profile_Base`)
- `TargetingDefinition` (asset `TD_*`, **TinCan > Targeting > Targeting Definition**):
  - **Aim source:** `BodyForward` (yaw from eye height, available now). `EyeAim` and `CameraAim` need pitch and come
    in a later slice. `BodyOffset` is a point in body space, such as the net head.
  - **Shape:** `Ray` (range, optional radius for a sphere cast), `Cone` (range, horizontal angle, vertical angle),
    `Sphere` (radius around the aim source point).
  - **Filters:** required and blocked GAS tags on the target's `Controller`.
  - **Selection:** `Nearest`, `BestAligned` (smallest angle to the aim), `FirstHit`.
  - **Line of sight:** optional, with a blocking `LayerMask`.
- `TargetingProcessor` (pure, tested): given an origin, a definition and candidates (id, point, filter pass,
  optional hit distance), it returns the selected target and its score. The shape maths lives here. It absorbs
  `RepairTargetProcessor`'s cone logic, generalised.
- `TargetingUseCase` : `ITargetingService`:
  - `TryAcquire(ITargeter, TargetingDefinition, out TargetResult)`.
  - It gathers candidates from `ITargetableRegistry` for the Cone and Sphere shapes, or a physics ray for the Ray
    shape (hit collider → `GetComponentInParent<ITargetable>()`, which must be registered).
  - It applies the tag filters and runs line of sight, then calls the processor.
  - It runs on any peer. The server's answer is authoritative.
- `HumanoidTargeter`: adapts `IHumanoidCharacterView` to `ITargeter` (eye = body + configured eye height,
  forward = body forward).
- `TargetResult`: the target, its point, distance and angle, so callers and debug tools can explain a miss.

### GAS link (slice 1: data only; activation stays as is)
- `AbilityDefinition.Targeting` (optional `TargetingDefinition`).
- Consumers ask `ITargetingService` for the ability's target. Automatic acquisition inside
  `AbilitySystemUseCase.TryActivateAbility` (for instant, targeted abilities) is a later slice. Held abilities like
  repair re-acquire every tick in their feature use case anyway.

### Registration
- `ActorOrchestrator.RegisterHierarchy` / `UnregisterHierarchy` also register every `ITargetable` in the hierarchy,
  the same way they handle interactors. It needs one injected `ITargetableRegistry`, which the core scope binds.
  - This is the only edit to core infrastructure.
  - Because the Targeting installer can be switched off, the registry implementation lives in Core
    (`Core/Infrastructure/Targeting/TargetableRegistry.cs`). The query service lives in the feature.

### Repair migration (the first slice's consumer)
- `ShipDamagePointNetworkMediator` implements `ITargetable`: `AimPoint` = the marker position, `Controller` = its GAS
  controller.
- `TD_RepairScan`: `BodyForward`, `Cone` (2.5 m, 100° horizontal, generous vertical), required tag
  `State.Damaged`, `Nearest`. No line of sight for now, because markers float over an open deck. The broken check
  becomes a GAS tag filter instead of `IsBroken`.
- `GA_RepairShip.Targeting = TD_RepairScan`. `ShipRepairUseCase` calls
  `ITargetingService.TryAcquire(player, GA_RepairShip.Targeting)` instead of `RepairTargetProcessor`.
- Remove `RepairTargetProcessor`, its tests, and the `ShipDamageConfig.RepairReach` / `RepairConeDegrees` fields.
- The scenario probe `SubjectFacesPoint` asks `ITargetingService`, so the scenario and the game share one answer.

## Later slices (roadmap, not in this plan)
1. **Aim pitch in input:** add pitch to `HumanoidInputState`, then enable the `EyeAim` / `CameraAim` sources. The
   camera ray is rebuilt from orbit height and distance, with a confirming check from the eye.
2. **Interaction on targeting:** a `TD_Interact` ray. The prompt uses the owner's prediction, and the server
   validates `RequestInteractionServerRpc` against its own query, which closes the trust gap.
3. **Net catch** becomes a `Sphere` at a `BodyOffset` (the net head), replacing `CatchProcessor`.
4. **Build placement:** point targets (hit point + normal on the ship).
5. **GAS auto-acquire** on activation, plus `ActivationRequiredTagsOnTarget` evaluated on the acquired target.
6. **Debug view:** a DevTools overlay showing each player's shapes and current targets. Scenario probe
   `SubjectTargets(TD, id)`.
7. **Flexible origins (future, when the actor model grows parts):** today every humanoid aims from one point, the
   body plus `EyeHeight` (and the orbit centre for `CameraAim`). A fuller actor with a head, hands and body will want
   queries from different parts: a scan from the head, a tool reach from the hand, a melee sweep from the body. A
   likely shape: the targeter exposes named sockets, the definition names one (for example an `AimSource.Socket` with
   a socket id), and the targeter resolves it. Sockets must come from state that owner and server share (the
   simulated pose and the input, not a client-only animated bone), or the server's recompute drifts from the
   owner's prediction. Not needed until the actor model exists.

## Build order (slice 1), each step green on `.\.tools\verify.ps1`
0. Ship-damage step 4 was committed by the developer (`7fbac1b`) before this slice. Ship-damage leftovers (3b cues,
   legacy MaintenanceUseCase/ShipRepairPoint retirement, debug-camera captures, damage:* flags, late-join
   scenario) stay in that plan's backlog until after this slice.
1. Contracts, `TargetableRegistry`, and the `ActorOrchestrator` registration. Test: registry add/remove via the
   orchestrator fakes.
2. `TargetingDefinition`, `TargetingProcessor` (tests: cone and sphere inclusion, vertical angle, each selection
   rule, tag filters through a `FakeAbilityController`, ray ordering) and `TargetingUseCase` (tests with fake
   targetables and a fake targeter). The installer goes in `Profile_Base`.
3. Repair migration: the damage point becomes `ITargetable`, plus `TD_RepairScan`, `GA_RepairShip.Targeting`,
   `ShipRepairUseCase` on the targeting service, and deleting the old processor and config fields.
   `ShipRepairUseCaseTests` are rewritten against a fake `ITargetingService`.
4. Docs:
   - a new ARCHITECTURE pillar, "Targeting": aim models, authority, registration;
   - CODE_MAP: a feature-index row and a "Where does X live" row;
   - the TASK_GUIDES recipe "Make an ability target something";
   - a progress note in `.docs/plans/ship-damage-repair.md`.

## Critical files
- New: `Core/Domain/Targeting/*`, `Core/Infrastructure/Targeting/TargetableRegistry.cs`, `Features/Targeting/*`,
  `Assets/Targeting/TD_RepairScan.asset`, `Resources/Installers/TargetingFeatureInstaller.asset`.
- Edited: `Core/Infrastructure/ActorOrchestrator.cs` (register targetables), `Core/Infrastructure/ProjectLifetimeScope.cs`
  (**bind `ITargetableRegistry` only.** It is on the do-not-touch list, but it is where core registries are bound.
  Alternative: the Targeting installer binds it, and the orchestrator gets it through `TryResolve`. See the open
  point below.), `Features/Abilities/AbilityDefinition.cs`, `Features/Airship/Damage/ShipDamagePointNetworkMediator.cs`,
  `ShipRepairUseCase.cs`, `ShipDamageConfig.cs`, `DevTools/Scenarios/ShipDamageScenarioLibrary.cs`.
- Removed: `Features/Airship/Damage/RepairTargetProcessor.cs`, `Tests/EditMode/RepairTargetProcessorTests.cs`.

**Decided at approval (open point):** the Targeting installer owns `ITargetableRegistry`; `ActorOrchestrator`
resolves it optionally. Original note: binding `ITargetableRegistry` in `ProjectLifetimeScope` (consistent with the
other core registries, but touches a protected file) versus the Targeting installer owning it, with
`ActorOrchestrator` using `IObjectResolver.TryResolve` (no protected file touched, but a core class depends on an
optional feature). Recommendation: the installer owns it and the orchestrator resolves it optionally. That matches how
`AbilityNetworkMediator` already treats the tag registry.

## Verification
- `.\.tools\verify.ps1 -Scenario RepairLoop`: all tiers green. Behaviour is unchanged for the player, and the
  scenario proves the migrated path end to end on host and client.
- All other scenarios (`NetCatch`, `TagRequest`, `EquipCycle`, `ShipDamage`) and the full EditMode suite stay green.
- New EditMode tests: `TargetingProcessorTests`, `TargetingUseCaseTests`, `TargetableRegistrationTests`, and the
  rewritten `ShipRepairUseCaseTests`.
- `git grep RepairTargetProcessor` returns nothing.
- Human playtest: repairing feels the same (reach, cone, ~4 s), on host and client.

## Progress log

- 2026-09-27: Slice 1 done.
  - Contracts are in `Core/Domain/Targeting/`. `ActorOrchestrator` registers `ITargetable`s, resolving the
    registry optionally.
  - `Features/Targeting/` holds `TargetingDefinition`, `TargetingProcessor`, `TargetingUseCase` (`ITargetingService`),
    `TargetableRegistry` and `HumanoidTargeter`. `TargetingFeatureInstaller` is in `Profile_Base`.
  - `AbilityDefinition.Targeting` exists.
  - Repair migrated: `ShipDamagePointNetworkMediator` is an `ITargetable`, `TD_RepairScan` is on `GA_RepairShip`,
    and `ShipRepairUseCase` asks the service. `RepairTargetProcessor` and the reach/cone config fields are gone.
    The broken check is now the tag filter `State.Damaged`.
  - The scenario probe `SubjectFacesPoint` asks the same service.
  - Verified: 343/343 tests; `RepairLoop` passes solo and host + client (the client probe reads "TD_RepairScan: part
    0 at 1.65 m, 0 deg", and the host lands 17 repair ticks); `NetCatch`, `TagRequest`, `EquipCycle` and
    `ShipDamage` still pass.
  - Behaviour change to playtest: reach is now measured from the eye (1.5 m up), not from the feet. The vertical
    limit is ±60°.
- 2026-09-27: Roadmap slice 1 (aim pitch) done.
  - `HumanoidInputState.LookPitch` (degrees, positive down) is captured from `IOrbitalLookView.Pitch` via
    `IHumanoidMovementView.LookPitch`. It is serialized with the input, so it is predicted, buffered and replayed.
  - `TargetingOrigin` gained `AimPitch` (from `InputState`) and `OrbitHeight` (`IOrbitalLookView.OrbitHeight`), plus
    `AimDirection` and `AimElevation`.
  - New aim sources: `EyeAim` and `CameraAim` (from the orbit centre). The processor centres a cone's vertical angle
    on the aim, and rays follow it.
  - Tests: pitch capture, pitch serialization, pitched cone, `BodyForward` ignoring pitch, and the `CameraAim` source
    and direction. 348/348.
  - Scenario `AimPitch`: the client looks level and a narrow `EyeAim` scan misses part 0; at 25° down it hits the
    part at 1.65 m. The server, knowing the pitch only from the input stream, gets the same hit. All six scenarios
    pass.
  - Not done: the pitch is a full float (4 bytes on each of the 4 inputs per packet). It could be quantized to a
    short if bandwidth matters. `CameraAim` has no consumer yet, since there is no crosshair UI and the current
    camera orbits the feet (`height = 0`).
- 2026-09-27: Roadmap slice 2 (interaction on targeting) done. The developer chose input-bit interaction over
  keeping the RPC with validation.
  - Interact is a predicted input bit (`Input_Interact`, bound in `DefaultInputBindingConfig`).
  - `InteractInputUseCase` (server, `AfterHumanoid`) acquires with `TD_Interact` on the press tick and calls the new
    `IInteractionOrchestrator.HandleInteraction(requester, target)`.
  - The prompt (`InteractorControllerView`) runs the same query. The legacy `InteractivityUseCase` RPC path stays
    silent while `InteractionFeatureInstaller` (in `Profile_Base`) is active.
  - Every `IInteractionTarget` is an `ITargetable` through default interface members; `TargetId` was dropped as
    unused.
  - Rays now find targetables by collider (no registry needed; fixtures register late), measure range to the hit,
    and stop at the first solid non-target.
  - Tests: `InteractInputUseCaseTests` (edge, once per press, re-press, nothing in reach, non-interaction target,
    unassigned bit) and a ray-range test. 355/355.
  - Scenario `InteractRack`: face the tool rack, the prompt shows `RepairToolRack(Clone)`, a real Interact press
    makes the server hand out the tool, and a second press returns it. It passes solo and host + client, with exactly
    one server interaction per press. All seven scenarios pass.
  - Not done: `NetworkMediator.RequestInteractionServerRpc` still exists for the fallback path. Remove it once the
    installer is permanent. `TD_Interact` is a level ray for parity; switch it to `EyeAim` if you want to aim E up
    and down.
- 2026-09-27: Debug view (roadmap item 6, partly).
  - `TargetingGizmos` builds the lines for a query: the source cross; the shape (ray, cone edges and arcs around the
    aim, or sphere rings); and a line and cross to the target. Green for a hit, red for a miss; tested in
    `TargetingGizmosTests`.
  - `InteractorControllerView` draws its live `TD_Interact` query again, both as Debug lines and in `OnDrawGizmos`,
    reconnecting the old interaction gizmo.
  - **TinCan > Dev > Targeting > Draw All Queries** (off by default) makes `TargetingUseCase` draw
    every query, including the server's repair scans.
  - Still open from item 6: a DevTools overlay and a `SubjectTargets` scenario probe.
- 2026-09-27: Debug view fixes after the developer's playtest ("no cone when repairing; the toggle changes nothing").
  - **Tick-rate queries were one-frame lines.** The repair scan runs on the network tick (~30 Hz), and a zero-duration
    `Debug.DrawLine` lasts one frame, so the cone showed in about one frame in six. Service-drawn queries now hold for
    0.1 s. The per-frame interaction gizmo keeps one-frame lines.
  - **MPPM clones do not share EditorPrefs,** so the toggle never reached Player 2. It is now a flag file in the main
    project's Library (`TargetingDebug.FlagPath`, `Library/TinCanDev/DrawAllTargetingQueries`) that every virtual
    player reads. `TinCan.DevTools.Editor` now references `TinCan.Features`.
  - **The owner predicts its repair target.** On a client, `ShipRepairUseCase` runs the repair query for the local
    player while `State.Repairing` is on and applies no effect. It exposes `PredictedTarget` for future feedback, and
    it is what draws Player 2's cone.
  - `TargetingDebug.LastDrawn(definition)` records what was drawn, so tooling can check it (debug lines cannot be
    read back).
  - Verified in a host + client `RepairLoop`: both peers drew `TD_RepairScan`.
- 2026-09-27: Eye height fixed after the developer's playtest ("the origin sits high above the player").
  - The targeter used a fixed 1.5 m, inherited from the old interaction ray, which assumed the root was at the feet.
    The root is the capsule centre (height 2, centre 0), so the eye sat 2.5 m above the feet.
  - The eye height is now per character: `IHumanoidMovementView.EyeHeight`, a field on `HumanoidControllerView`
    (default 0.7, measured from the root; about 1.7 m above the feet). `HumanoidTargeter` reads it.
  - Scenario placement drops the subject onto the deck (`DevTools/Scenarios/ScenarioPlacement.cs`: a downward ray, plus
    the capsule's root-to-feet offset) instead of computing feet from the marker. Keeping the current height failed in
    host + client, where the server's copy of the client body was not grounded when the command ran.
  - `RepairToolRackFixture` moved down to the deck (ship-local y -3.49, was -2.40, which floated about 1.1 m).
  - `AimPitch` rewritten: the marker now sits at about eye height, so the scan misses at 30 deg down and hits looking
    level.
  - The origin is still one point per character. Roadmap item 7 covers per-part origins.
