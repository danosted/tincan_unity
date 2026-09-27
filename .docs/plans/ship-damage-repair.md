Status: Approved (2026-09-26)

# Ship damage & repair, and the item/equipment layer under it

## Context

You want parts of the airship to break at runtime (a crude sphere marks the spot), and players to fix them with a repair tool
they pick up. That is the first use of a broader idea: items you equip carry abilities (such as `Ability.Repair.Ship`)
through GAS. The feature must be toggleable through a `FeatureInstaller`.

## What exists today (and what it tells us)

| Piece | Where | Relevance |
|---|---|---|
| Carrying one item | `Features/Carry/CarriedItem.cs` (`enum CarriedItem { None, JerryCan, Net }`, `ICarrier`), `PlayerCarryNetworkMediator` | This is already a one-slot equipment system, but hardcoded: the enum, the visual names and the net tag are all baked in. |
| Tool, then tag, then ability | `PlayerCarryNetworkMediator.UpdateNetTag` adds `State.Carrying.Net`; `GA_SwingNet` requires it; `Input_Primary` bit triggers it (predicted); `NetCatchUseCase` (server, `AfterHumanoid`) watches `State.Net.Swinging` and does the world effect | This is the pattern to generalise: **the ability is intent plus a tag window, and a feature use case performs the world effect.** |
| Repairables | `Core/Domain/IRepairable.cs`, `ShipModuleNetworkMediator` (per-module `AbilityNetworkMediator` + `HealthAttributeSet` + broken threshold), `ShipRepairPointNetworkMediator` | A damage point can reuse `IRepairable` + `HealthAttributeSet` as-is. |
| Repair GAS assets | `GA_RepairModule` → `GE_RepairModule` on target, `GA_DamageModule`, `Interaction.Ability.Repair`, `IA_RepairModule`, `ActivateAbilityInteractionHandler` | Usable legacy from build mode. `MaintenanceUseCase` looks dead. |
| Fixtures on the ship | `ShipFixtureDefinition` + `ShipFixtureSpawningUseCase`, `FuelFeatureInstaller` as the template | Damage sockets and the tool rack become fixtures. |
| Timed ship events | `Features/Events/EventOrchestratorUseCase` (single event, tag check at end) | Too narrow to host breakage; leave it alone. |

### GAS gaps this feature hits

1. **Ability grants are not replicated.** `AbilitySystemUseCase._actorAbilities` is local to each peer, and starting
   abilities are granted on every peer in `HumanoidPlayer.OnNetworkSpawn`. Equipment that grants abilities must
   therefore grant them on the server **and** on the owner (for prediction), driven by replicated equipment state.
2. **No grant provenance.** Nothing records who granted an ability. When you unequip, the item must revoke only
   what it granted, so we need a grant handle (source → specs + effect).
3. **Tags and items have no network identity.** Tags travel as names, and the server resolves them with
   `Resources.FindObjectsOfTypeAll` (`AbilityNetworkMediator.RequestTagChangeServerRpc`). Items need a stable id to go
   into a `NetworkVariable`, and the same registry should fix tags.
4. **`CostEffect`, `AbilityTag`, `Cancel/BlockAbilitiesWithTag` are declared but unused.** That is fine for the
   sketch; we note it so we don't rely on them.
5. **Input-triggered abilities have no target** (`ProcessAbilitySimulation` activates with `target = null`). Tool
   abilities resolve their target in a feature use case, as net catch already does.

## Subsystems (layered, each its own installer)

```
Core.Domain  : IItemDefinition, ItemId, IEquipment, GameplayTagRegistry contract
Items        : ItemDefinition SO, ItemCatalog (id <-> def), EquipmentNetworkMediator, EquipmentAbilityBinder, TakeItemInteractionHandler
ShipDamage   : damage points, break scheduler, repair use case, HUD. Depends on Items + Abilities
```

### A. Item definitions + catalog (`Features/Items/`)
- `ItemDefinition : ScriptableObject` has: `Id` (serialized int, validated unique), display name, `VisualName` (the
  child to show under the player's `Visual`), `GrantedAbilities` (list of `AbilityDefinition`), `EquippedEffect` (an
  Infinite `GameplayEffectDefinition` that grants tags such as `Item.Tool.Repair`).
- `ItemCatalog` loads every definition (from the installer's list), resolves `Id → definition`, and is injected.
- Assets go in `Assets/Items/ITEM_*.asset`.

### B. Equipment (evolves Carry)
- Replace `enum CarriedItem` with `ItemId`. `ICarrier` becomes `IEquipment { ItemDefinition? Held; TryEquip; TryUnequip }`.
- `EquipmentNetworkMediator` (on `NetworkPlayer.prefab`, replacing `PlayerCarryNetworkMediator`) has a
  `NetworkVariable<int>`, written by the server. On change, every peer toggles the visual found by name, as it
  does today.
- `EquipmentAbilityBinder` (pure C#, tested) runs on a change on the server and on the owner. It revokes the old
  item's grant handle, applies the new item's `EquippedEffect`, and grants its abilities. Proxies skip it.
- Migrate the jerry can and the net onto items (`ITEM_JerryCan`, `ITEM_CatchingNet` granting `GA_SwingNet`). This
  deletes the hardcoded net-tag code and proves the layer with a feature that already works.
- Multi-slot inventory stays out of scope. The `NetworkVariable<int>` becomes a `NetworkList<int>` plus an active
  index later, without changing the binder.

### C. Ship damage feature (`Features/Airship/Damage/`, `ShipDamageFeatureInstaller`)
- **Damage sockets fixture**: one `ShipFixtureDefinition` prefab holding N `ShipDamagePointNetworkMediator`
  children at hand-placed ship-local poses. Each one is `IRepairable` + `IActor`, with its own `AbilityNetworkMediator` and
  `HealthAttributeSet` (the same approach as `ShipModuleNetworkMediator`). The sphere shows while `IsBroken`. Pre-placed
  sockets avoid spawn churn and make late join trivial.
- **`ShipBreakageUseCase`** (server, `ISimulationTickable AfterAirship`): a timer from `ShipDamageConfig` (interval
  range, max broken at once). It picks a healthy socket (`ShipBreakageProcessor`, seeded RNG, tested) and applies
  `GE_ShipDamage` (instant health → 0, grants `State.Damaged`). It publishes `ShipPartBrokenEvent`. Other
  sources (gas pocket, collisions) can call the same entry point later.
- **Consequence: fuel leak, coupled only through GAS.**
  - Fuel owns a new attribute, `Attr_FuelLeakRate` (units per second, base 0), initialised on the ship next to `Attr_Fuel`.
  - `FuelConsumptionUseCase` subtracts `leakRate * dt` every tick, even when the ship is not driven. The maths goes in
    `FuelConsumptionProcessor.ComputeLeak`, with tests.
  - Each break applies an Infinite `GE_HullBreach` (Add +X `Attr_FuelLeakRate`) to the **ship's** controller.
    `ShipBreakageUseCase` keeps the returned `ActiveGameplayEffect` handle per damage point and removes it on repair.
  - Two breaks give twice the leak, because modifiers stack.
  - ShipDamage references only the effect asset, never Fuel code. With Fuel disabled, the attribute is absent and
    nothing drains. Guard this so it doesn't spam the "missing attribute" warning.
  - GAS gap 6: `AbilitySystemUseCase.RemoveEffect` is private. Add a public `RemoveEffect(actor, handle)`.
- **Repair tool rack fixture**: uses `IA_TakeRepairTool` → generic `TakeItemInteractionHandler` (from Items), with
  the item named on the target. It has toggle semantics like `TakeJerryCanInteractionHandler`.
- **Repairing (ability-driven, same as the net)**:
  - `GA_RepairShip` (tag `Ability.Repair.Ship`) is granted by `ITEM_RepairTool`, triggered by `Input_Primary`, and
    uses `OnInputHeld`. It requires `Item.Tool.Repair`, and while active it grants `State.Repairing`.
  - `ShipRepairUseCase` (server, `AfterHumanoid`) finds the nearest broken point in a reach/facing cone for each
    player with `State.Repairing` (`RepairTargetProcessor`, tested). It then applies `GE_RepairTick` (+X health per
    tick, clamped to max). When a point is back above the threshold, it removes `State.Damaged` and publishes
    `ShipPartRepairedEvent`.
  - `Input_Primary` is shared with `GA_SwingNet`. There is no clash, because only the held item's abilities are
    granted, which makes "Primary = use the held tool" the general rule.
- **HUD**: `ShipDamageHudPresenter` shows "Broken parts: n" through `IHudValues`.

### D. Gameplay cues (sound, VFX, animation, UI feedback)

Today `IEventPublisher` is a server-local domain bus: its only observer is the debug log, and it never crosses the
network. Presentation polls replicated state (`NetSwingVisualView`, `FuelMotorStatusView`). Keep that split and
name it:

- **Domain events** (`IEventPublisher`) cover logic, telemetry and tests. They never drive visuals directly.
- **State cues** are looping and derive from replicated tags or attributes. A generic `TagCueView` (a list of
  `tag → GameObject/AudioSource/Animator bool`) toggles on `HasTag`. This covers the broken sphere and sparks
  (`State.Damaged` on the point), the leak hiss (`State.Damaged` count > 0 on the ship), and the repair loop sound
  and tool animation (`State.Repairing`). It works on every peer and handles late join for free, because tags
  already replicate. It is also predicted for the owner, because effect tags are predicted.
- **Burst cues** are one-shot: the break bang, the repair-complete ding, a HUD toast.
  - `GameplayCueDefinition` (SO, has a catalog id like items and tags) → an optional VFX prefab, audio clip and HUD text.
  - `GameplayEffectDefinition` gets an optional `ExecuteCue`. `AbilitySystemUseCase.ApplyEffect` fires it
    through an `IGameplayCueDispatcher`, so any effect can make noise with no feature code.
  - On the server, `GameplayCueNetworkMediator` (a singleton) sends `ClientRpc(cueId, targetNetworkObjectId,
    localPos)`, unreliable, and it is not sent to the owner when the owner predicted the same cue.
  - On each peer, `GameplayCueUseCase` resolves the definition and plays it at the target (in ship-local space,
    per the ship-local rule).
  - Late joiners miss bursts by design. Anything that must persist is a state cue.

The slice ships `TagCueView` + the burst pipeline with two cues (`Cue.Ship.Break`, `Cue.Ship.Repaired`). Crude
assets are fine: the sphere, a Unity primitive particle, and a placeholder clip.

### E. Feedback loop for agentic iteration (built first, grown per slice)

The goal: an agent changes code, then with one shell command gets a machine-readable verdict and evidence
within about 30 to 90 s, with no human in the Editor. It extends the existing harness (`Assets/Scripts/DevTools/`,
`.docs/NETWORK_TEST_HARNESS.md`) rather than building a parallel one.

**Tiers.** Climb a tier only when the one below is green.

| Tier | Command | Time | Catches |
|---|---|---|---|
| T0 compile | `unity cmd recompile` + check `Library/ScriptAssemblies` / `error CS` | ~10 s | build breaks |
| T1 logic | `unity cmd run_tests` filtered to `ShipDamage*`, `Repair*`, `Equipment*`, `GameplayCue*` | ~15 s | processors, use cases, binder, cue dispatch |
| T2 host-only scenario | `unity cmd menu --path "TinCan/Dev/Scenarios/ShipDamage (Host)"` | ~20 s | wiring, installer, GAS assets, server flow |
| T3 host + client | `.../ShipDamage (Host + Client, Lag100)` | ~60 s | replication, prediction, late join, cues on the remote peer |

**What to build (in `TinCan.DevTools`, inert without flags):**
1. **Scenarios instead of routes.** `Scenario` = steps + expectations. `BotStep` gains semantic commands next to
   `BotShipCommand`:
   - `ForceBreak(socketIndex)` runs on the server through a new `IShipBreakage.ForceBreak`.
   - `Equip(itemId)` runs on the server through `IEquipment`, so the scenario skips walking to the rack.
   - `FaceTarget(socketIndex)` is a dev-only server teleport-and-aim to a reach pose.
   - `WaitUntil(condition, timeout)` makes the loop closed and not time-guessed.

   The existing open-loop `Hold(Primary)` performs the repair itself, so the real input and prediction path is
   still exercised.
2. **Scenario `RepairLoop`.** Host `ForceBreak(0)` → client `Equip(RepairTool)` → `FaceTarget(0)` → `Hold(Primary)`
   until repaired or 10 s → `WaitUntil(sphere hidden on client)`. A variant, `RepairLoopLateJoin`, starts the client after the break.
3. **Feature telemetry with verdicts.** `ShipDamageTelemetryUseCase` records a timeline on each peer: break,
   `State.Damaged` seen on the client, sphere visible, leak rate, repair start and end, cue fires, sphere hidden.
   It writes `Logs/feature-telemetry/ship-damage/latest-{host,client1}.json` containing:
   - the `timeline`;
   - `metrics`: replication latency of break and repair, fuel lost while broken, repair duration, cue counts;
   - `expectations` + `passed: true|false` + `failures[]`. Expectations include: the sphere shows on both peers
     within 500 ms; the leak is 0 after repair; exactly one break cue and one repaired cue per peer; the net and
     jerry can are unaffected.

   It also prints a single `[ShipDamage] {json}` Console line at the end, so `unity cmd` log reads can grep it.
4. **Visual evidence.** At named checkpoints (broken, mid-repair, repaired), `ScenarioCaptureView` writes a
   `ScreenCapture` PNG from a fixed debug camera aimed at the socket, into the same folder. The agent can open
   the PNGs to judge cues and the sphere.
5. **Self-terminating runs.** The scenario ends Play itself when done or on timeout, so an agent run is just
   `menu` → poll for `latest-*.json` → read. The existing tag cleanup on Play end still applies.
6. **Fast knobs** for manual and agent tweaks, as launch flags / MPPM tags: `damage:seed=<n>`,
   `damage:interval=<s>`, `damage:off`. Scenarios force breaks, so the random scheduler stays out of the
   verdict unless it is under test.

**Each slice ships with its loop.** Items/Equipment adds `EquipCycle` (equip, swap, unequip; abilities granted and
revoked on host and owner; the net still catches). Damage adds `BreakOnly`. Cues and Repair add `RepairLoop`. A
slice is done when T1–T3 pass, not when it compiles.

Docs: add a "Scenarios" section to `.docs/NETWORK_TEST_HARNESS.md`, and put the tier table + commands in
`.docs/TASK_GUIDES.md` as the default verify recipe for new features.

### Toggle behaviour
Removing `ShipDamageFeatureInstaller` removes the sockets, the rack, the scheduler and the repair use case. Removing
the Items installer breaks Fuel and FlyingCan after the migration, so Items becomes a foundational installer (low
`Order`) that those installers depend on.

## Progress log

- 2026-09-26: Step 0 done. The scenario harness is in `Assets/Scripts/DevTools/Scenarios/`, driven by
  `.tools/verify.ps1`. `NetCatch` passes at every tier: compile, tests (285/285), solo (~4 s), and host + client at
  Lag100 (~45 s including launch). Hardened against the Editor/MPPM failures hit along the way: the dirty-scene
  modal, a wedged pipeline, a clone with an empty scene, and MPPM tag-file contention.

- 2026-09-26: Step 1 done.
  - `IGameplayTagRegistry` + `GameplayTagDatabase`, which lists every tag asset automatically. Registered by
    `GameplayTagsFeatureInstaller` (Order -20), which is listed in `Profile_Base`.
  - `AbilityNetworkMediator` resolves client tag requests through the registry, falling back to the old scan when
    the installer is off.
  - `ItemDefinition` + `ItemCatalog`, pure and tested.
  - Scenario `TagRequest` passes solo and host + client. Tests at 291/291.
  - Also fixed: the scene no longer turns dirty after a recompile, because the director dirties only on real value
    changes.
  - Note: work happens on `main`. The developer moves verified commits to a PR branch.

- 2026-09-26: Step 2 done.
  - Items and equipment: `EquipmentNetworkMediator` replaces `PlayerCarryNetworkMediator`. It was renamed with
    `git mv`, so the prefab's script GUID is unchanged.
  - `EquipmentAbilityBinder` grants and revokes on the server and the owner.
  - `ItemsFeatureInstaller` (Order -15) is listed in `Profile_Base`.
  - The net and the jerry can are items (`ITEM_CatchingNet` grants `GA_SwingNet`, which left the player's starting
    abilities). The handlers keep their names, so the `IA_*` assets still resolve them.
  - GAS: public `RemoveEffect(actor, handle)` and `HasAbility`.
  - Harness: a solo run now plays the server and subject lanes in parallel.
  - Scenario `EquipCycle` passes solo and host + client. `NetCatch` and `TagRequest` still pass. Tests at 298/298.
  - Also fixed: the tag database now refreshes inside the import callback. `delayCall` never ran in a background
    Editor.

- 2026-09-26: Step 3 done.
  - `Features/Airship/Damage/`: 5 sockets in a `ShipDamageSockets` fixture, with an orange emissive sphere marker.
    Socket 0 is in view from the spawn point.
  - `ShipBreakageUseCase` breaks at random (first after 45 s, then every 40–90 s, at most 2 broken; switched off for
    scripted runs).
  - Each broken part holds one `GE_HullBreach` on the ship: +0.25 L/s `Attr_FuelLeakRate`, which Fuel drains even
    while parked, and `State.Ship.Damaged`. There is also the HUD line "Hull breaches: n".
  - `ShipDamageFeatureInstaller` is listed in `Profile_FuelSandbox`.
  - (Corrected the same day) The first version kept point health in a plain replicated float, justified by an actor-Id
    collision between ability mediators. On review that was wrong: `AbilityNetworkMediator.Id` resolves
    `GetComponentInParent<IActor>()`, which finds the mediator itself first, so each socket has its own Id. Points
    are now GAS actors as section C planned:
    - each socket has an `AbilityNetworkMediator` and a `HealthAttributeSet` (`Attr_Health`, `Attr_MaxHealth`);
    - breaking, restoring and (step 4) repairing are effects (`GE_ShipPartBreak`, `GE_ShipPartRestore`);
    - each broken part also carries `GE_ShipPartBroken` (`State.Damaged`).
  - The marker is toggled by the point itself. `TagCueView` (step 3b) can take this over later.
  - Scenario `ShipDamage` passes solo and host + client: the break replicates in ~1.3 s at Lag100, the fuel drops
    0.5 L in 2 s while parked, and everything clears on restore. It also checks the part's own `State.Damaged` tag.
    Tests at 316/316, and the other scenarios still pass.
  - Harness: `verify.ps1` now checks network prefab hashes between host and clones.

- 2026-09-26: Step 4 done: the repair loop is playable.
  - `ITEM_RepairTool` (id 3) is taken from the `RepairToolRack` fixture, using the generic `ItemRackNetworkMediator`
    + `TakeItemInteractionHandler` + `IA_TakeRepairTool`.
  - Holding the tool grants `GA_RepairShip` (tag `Ability.Repair.Ship`). Holding Primary keeps `State.Repairing` on
    the player through `GE_Repairing`, predicted from the input bit.
  - `ShipRepairUseCase` (server) applies `GE_RepairTick` (+6 health, capped at max) every 0.25 s to the broken part
    in front. The part is whole in ~4 s.
  - Target: the nearest broken part within 2.5 m, inside a 100° horizontal cone. Facing follows the look input, so
    you repair what you look at.
  - Socket 0 moved to 3.2 m in front of the spawn.
  - Scenario `RepairLoop` (the dev command `PlaceSubjectAtPoint` stands the subject behind the part, because both
    players share one spawn) passes solo and host + client: 17 repair ticks, then the leak, tags and marker clear.
    Tests at 333/333, and all five scenarios pass.
  - **Not covered by a scenario:** taking the tool from the rack with E through the real interaction raycast. Only
    the handler is unit-tested. The rack's position (port side, near the spawn) needs a playtest.
  - Found: a teleport cannot turn a player (facing follows the look input every tick). Scenario placement keeps the
    subject's facing.

- 2026-09-27: Repair targeting moved onto the cross-cutting Targeting subsystem
  (`.docs/plans/targeting-subsystem.md`): `TD_RepairScan` replaces `RepairTargetProcessor`.

## Build order (vertical slices)
0. **Feedback loop skeleton**: `Scenario` + expectations + report writer + self-terminating run + capture, first
   proven on an existing feature (a `NetCatch` scenario) so the harness is trusted before new code relies on it.
   Pure parts (cursor, expectation evaluation) get EditMode tests.
1. **Tag + item identity**: add `GameplayTagRegistry` (replaces the `FindObjectsOfTypeAll` hack) and `ItemCatalog`. Tests.
2. **Items/Equipment**: definition, mediator, binder, generic take handler. Migrate the jerry can and the net. Existing
   Fuel/NetCatch tests stay green, and binder tests are added.
3. **Ship damage**: fixture + sockets + sphere, scheduler, `GE_ShipDamage`, the `GE_HullBreach` leak (plus
   `Attr_FuelLeakRate` in Fuel and public `RemoveEffect`), and the HUD. Tests for the processor and the use case.
3b. **Cues**: `TagCueView`, `GameplayCueDefinition`, `ExecuteCue` on effects, dispatcher + network mediator +
   use case. Tests cover dispatch from `ApplyEffect` and suppressing the owner echo.
4. **Repair**: tool item + rack, `GA_RepairShip`, `ShipRepairUseCase`, `RepairTargetProcessor`. Tests.
5. Docs: add a feature-index row in `CODE_MAP.md`, add Items to `ARCHITECTURE.md` (equipment grants abilities), and
   retire or point `MaintenanceUseCase`/`ShipRepairPoint` at the new path.

## Files to read first when implementing
- `Assets/Scripts/Features/Carry/PlayerCarryNetworkMediator.cs`, `CarriedItem.cs`
- `Assets/Scripts/Features/Airship/Fuel/Minigame/NetCatchUseCase.cs` (template for `ShipRepairUseCase`)
- `Assets/Scripts/Features/Abilities/AbilitySystemUseCase.cs` (grant/remove, effects)
- `Assets/Scripts/Network/Infrastructure/ShipModuleNetworkMediator.cs` (template for the damage point)
- `Assets/Scripts/Features/Airship/Fuel/FuelFeatureInstaller.cs` (installer + fixture template)

## Verification
- EditMode: `ShipBreakageProcessorTests`, `RepairTargetProcessorTests`, `ShipBreakageUseCaseTests`,
  `ShipRepairUseCaseTests`, `EquipmentAbilityBinderTests`, `ItemCatalogTests`. The existing Fuel, NetCatch and
  NetSwingPrediction suites stay green after the Carry migration.
- `unity cmd` recompile gives no errors. The EditMode suite is green.
- Automated: `RepairLoop`, `RepairLoopLateJoin`, `EquipCycle` at T3 report `passed: true` on host and client.
  The capture PNGs show the sphere, and then no sphere.
- Human playtest (feel only, after automation is green), host + MPPM client:
  - A sphere appears and is visible to both peers, including a late joiner.
  - Fuel visibly leaks while parked.
  - The client takes the tool, holds Primary at the sphere, and the sphere disappears on both peers. The leak stops.
  - The net and jerry can still work.
  - Disabling the installer removes everything.

Decisions taken: foundation first (Items/Equipment, with the net and jerry can migrated); repair by holding Primary
(an ability-driven tool); the first consequence is a fuel leak.
