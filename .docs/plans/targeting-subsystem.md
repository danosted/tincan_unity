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
