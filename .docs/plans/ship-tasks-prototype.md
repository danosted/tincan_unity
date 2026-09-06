# Ship Tasks Prototype: UI framework, Fuel, Jerry Cans, Net Minigame, Gauge

**Status:** Approved 2026-09-05. Living document: tick checkboxes and append to the Progress Log as slices land. Read this first in every session (AI or human).

## Context

TinCan needs its first "ship task" loop so the team can feel what crewing the airship is like. The lead also flagged that there is no menu/UI framework at all today (hosting requires clicking the NetworkManager Inspector buttons in Play mode). This track delivers, in mergeable slices:

0. A headless, data-driven menu/HUD framework with a throwaway view. First consumer: Start Host / Join.
1. Fuel that drains while the airship is driven, shown as a 100 to 0 readout.
2. Refuel loop: take a jerry can at the bow, carry it into the cabin, pour it into the motor.
3. Jerry cans flying past the ship.
4. A handheld net to catch them, adding to the jerry can supply.
5. An in-world fuel gauge at the helm replacing the HUD number.

The plan spans several sessions and is written for both AI and human execution. Tasks are tagged `[AI]`, `[HUMAN]` (good for learning the system), or `[PAIR]`.

## Decisions (locked with Christian, 2026-09-05)

- **Fuel value = GAS attribute `Attr_Fuel`, server-owned, accessed only via `IFuelTank`.** Server-ownership: yes, this is required. Both candidate designs are server-authoritative; the GAS attribute path is chosen because it is Cheesed's pattern for ship state (`Attr_Health` on the airship, `ModuleAttributeSet` on modules, repair via effect) and matches CGreulich's `IHealth` accessor idea. To avoid overlap with his unmerged branch, fuel lives on its **own** component (`FuelTankNetworkMediator`) instead of growing `AirshipNetworkMediator`. The level is kept in `BaseValue` with `CurrentValue == BaseValue` because `AbilitySystemUseCase.UpdateAttributes` calls `ResetAttributesToBase()` (sets Current = Base for every attribute) whenever any effect changes on the ship. Only server code writes it; clients read the replicated `NetworkList` through `TryGetAttribute`.
- **Carrying = virtual carry state** on the player (server-written `NetworkVariable<byte>` + child meshes toggled). No held-object/socket system exists, and players are not NGO-parented to the ship (`AutoObjectParentSync` off), so a spawned held object would fight the platform-carry logic.
- **Catch = handheld net**: carried item `Net`, swing on left mouse via a GAS ability with an input bit in `HumanoidInputState`, catch validated on the server in the simulation tick.
- **Flying cans = server-moved `NetworkObject` + `NetworkTransformMediator`**, pure motion processor. No `NetworkRigidbody` (none in the project).
- **UI view tech = UI Toolkit, code-built** (no UXML/USS); the headless model is the real deliverable, the view is disposable.
- **Merge flow = short-lived branch per slice, PR to `danosted/tincan_unity` main**, rebased on `origin/main` before merge. Target: each slice merged within 1 to 3 sessions.
- **Do not edit** (CGreulich's `origin/feature/airship-gas-challenge` changes them): `AirshipNetworkMediator.cs`, `AbilitySystemUseCase.cs`, `GameplayEffectDefinition.cs`, `AirshipMovementUseCase.cs`. In `Airship_Prefab.prefab` add exactly one child (`FuelSystem` nested prefab); all further fixtures go inside that nested prefab.

## Ground rules for every slice

- Branch from `origin/main`: `cfb/ui-0-menu-framework`, `cfb/fuel-1-tank-drain-hud`, `cfb/fuel-2-jerrycan-loop`, `cfb/fuel-3-flying-cans`, `cfb/fuel-4-net-catch`, `cfb/fuel-5-gauge`. The existing empty `cfb/fuel-task-prototype` branch is deleted after slice 0 lands.
- Code placement (tests cannot reference Assembly-CSharp): use cases, processors, handlers, interfaces, `NetworkBehaviour` fixtures go in `Assets/Scripts/Features/...` (TinCan.Features). Only things needing `NetworkMediator`, `NetworkManager`, `UnityTransport` or `UIDocument` go in `Assets/Scripts/Network/Infrastructure/` or `Assets/Scripts/UI/` (Assembly-CSharp).
- Every new `NetworkBehaviour` ends in `NetworkMediator`. `#nullable enable` at top of new files. Guard clauses, no regions, UniTask if async is needed.
- Every slice ships EditMode tests for its processors/use cases/handlers using the hand-written fakes in `Assets/Tests/EditMode/Fakes/`.
- Definition of done per slice: recompile clean, all EditMode tests green, host + 1 virtual client playtest (Multiplayer Play Mode is installed), plan checklist updated, PR opened with the slice section as description.
- Verification commands (repo root, Editor open; `unity status` must show `ready`):

```bash
unity cmd recompile && unity cmd recompile_status
```
```bash
unity cmd run_tests --mode EditMode
```
```bash
unity cmd console --level error
```
```bash
unity cmd editor_play
```
```bash
unity cmd capture_game_view --source screen
```
```bash
unity cmd editor_stop
```

- Session protocol (AI or human): start by reading `.docs/plans/ship-tasks-prototype.md` (local, gitignored), `git status`, `unity status`. End by ticking boxes, appending to the Progress Log, committing. AI sessions may also use `unity cmd create_asset`, `set_serialized_field`, `save_prefab_contents`, `find_assets`, `get_scene_hierarchy` for Editor-side work, but prefabs/assets are preferred human tasks in this track.

---

## Slice 0: Headless menu/HUD framework + Start Host / Join menu

Branch `cfb/ui-0-menu-framework`. Removes the Inspector-button dependency for playtests and gives slice 1 a place to show fuel.

### Design

Headless model in `Assets/Scripts/Features/UI/` (TinCan.Features):

- `MenuDefinition : ScriptableObject` (`CreateAssetMenu "TinCan/UI/Menu Definition"`): `MenuId`, `Title`, `List<MenuItemDefinition> Items`.
- `MenuItemDefinition` (serializable): `ItemId`, `Label`, `MenuItemKind { Command, TextField, Toggle, Submenu, Back }`, `CommandId`, `MenuDefinition Submenu`, `DefaultValue`.
- `IMenuCommand { string CommandId { get; } void Execute(MenuContext context); }` registered `.As<IMenuCommand>()`, collected by `MenuCommandRegistry(IEnumerable<IMenuCommand>)`. Same discovery style as `InteractionHandlerRegistry` (`Assets/Scripts/Features/Interaction/InteractionHandlerRegistry.cs`).
- `IMenuSystem` (the headless API): `MenuSnapshot? Current`, `bool IsOpen`, `Open(MenuDefinition)`, `Back()`, `CloseAll()`, `Invoke(itemId)`, `SetValue(itemId, string)`, `GetValue(itemId)`, `event Action Changed`. Implemented by `MenuUseCase : IMenuSystem, ITickable` (stack of menus, value dictionary keyed by `menuId/itemId`, `ActionNames.Cancel` toggles/backs). `MenuSnapshot` is an immutable view model (title + item rows with label/kind/value/enabled) so any view can render any menu generically.
- `IHudValues { void Set(string key, string text); void Remove(string key); IReadOnlyDictionary<string,string> All; event Action Changed; }` implemented by `HudUseCase`. Deliberately tiny; fuel readout is its first user.
- Commands (`Assets/Scripts/Features/UI/Commands/`, TinCan.Features, so they are unit-testable; they only need `INetworkService`): `StartHostMenuCommand`, `JoinGameMenuCommand` (reads `address`/`port` values, calls `INetworkService.SetConnection(address, port)` then `StartClient()`), `QuitMenuCommand`. `INetworkService` (`Assets/Scripts/Core/Domain/Networking/INetworkService.cs`) gains `SetConnection(string address, ushort port)`; `NGONetworkService` implements it via `UnityTransport.SetConnectionData`.
- `MainMenuBootstrap : IStartable, ITickable` (Features): opens the injected main `MenuDefinition` when `INetworkService.State == Offline`, closes all menus once the state becomes Host/Client.
- Throwaway views (Assembly-CSharp `Assets/Scripts/UI/`): `MenuOverlayView : MonoBehaviour` with `UIDocument`, rebuilds a `VisualElement` tree from `MenuSnapshot` on `Changed` (Label, Button, TextField, Toggle); unlocks the cursor while a menu is open. `HudOverlayView` renders `IHudValues.All` as labels top-left. Both live as children of `Assets/Prefabs/Singletons/GameLifetimeScope.prefab` (present in every scene) and are bound with `RegisterComponentInHierarchy<...>()` like `CloudEnvironmentView`.
- DI in `ProjectLifetimeScope.cs`: `[SerializeField] MenuDefinition _mainMenu` + `RegisterInstance`; register `MenuUseCase` (`.As<IMenuSystem, ITickable>()`), `HudUseCase` (`.As<IHudValues>()`), `MenuCommandRegistry`, the three commands, `MainMenuBootstrap` as entry point, the two views.

### Tasks

- [x] `[AI]` Write model + `MenuUseCase` + `HudUseCase` + `MenuCommandRegistry` + `MainMenuBootstrap`.
- [x] `[AI]` Tests: `MenuUseCaseTests` (open/submenu/back stack, `Invoke` routes to the command by id, unknown id is a no-op, `SetValue`/`GetValue`, `Changed` fires once per mutation, Cancel closes), `MenuCommandRegistryTests`, `HudUseCaseTests`. Add `FakeMenuCommand` to `Assets/Tests/EditMode/Fakes/`.
- [x] `[AI]` `SetConnection` on `INetworkService` + `NGONetworkService` + `FakeNetworkService`; the three commands.
- [x] `[AI]` `MenuOverlayView`, `HudOverlayView`; DI registrations; recompile; tests green.
- [x] `[HUMAN]` (done by AI unattended on 2026-09-05, please review in the Editor) Create `Assets/Settings/UI/DefaultPanelSettings.asset` (UI Toolkit Panel Settings), add `MenuOverlay` and `HudOverlay` children with `UIDocument` + view components to `GameLifetimeScope.prefab`.
- [x] `[HUMAN]` (done by AI unattended on 2026-09-05, please review: open the assets in the Inspector) Author menus under `Assets/UI/Menus/`: `Menu_Main` (Start Host, Join -> `Menu_Join`, Quit), `Menu_Join` (address TextField default `127.0.0.1`, port TextField default `7777`, Connect command, Back). Assign `Menu_Main` on `GameLifetimeScope.prefab`. This is the human's first contact with the data-driven pattern.
- [x] `[HUMAN]` Check interplay with `PossessionCursorResponder` (RESOLVED by AI 2026-09-05 evening: `MainMenuBootstrap` is now the single owner of Cancel (opens/backs/closes, ignores a Cancel that just exited a vehicle) and drives an `InputGate` that blocks all gameplay input except Cancel while a menu is open; `MenuUseCase` no longer reads input. Verified in Play mode via scripted input.) (cursor lock) and that gameplay input is ignored while a menu is open; note findings in the Progress Log. (AI note: `MenuOverlayView` unlocks the cursor while a menu is open and re-locks it on close when a session is active; gameplay input is NOT suppressed while the menu is open yet. Escape while in a vehicle/free camera exits those instead of opening the menu, by design.)
- [x] `[PAIR]` Playtest: Play -> menu appears -> Start Host spawns player (Esc open/close and input blocking verified via scripted input; Join via a second client still open) (verified by AI on 2026-09-05 in `drm_cloud_environment`: menu renders, Start Host -> Host state, menu closes, player + airship spawn, cursor locks); STILL TO VERIFY BY HUMAN: second virtual player -> Join with 127.0.0.1; Esc reopens/closes the menu; Quit.
- [ ] `[HUMAN]` PR to `danosted/tincan_unity` main; delete `cfb/fuel-task-prototype`.

Backlog (not this slice): input mapping menu (needs a rebind API on `IInputService`; `UnityInputService` hardcodes keys today), persistence of last address.

---

## Slice 1: Fuel tank, drain while driven, stall when empty, HUD readout, debug refuel

Branch `cfb/fuel-1-tank-drain-hud`. Safe on main because the tank starts full, the motor offers a free debug refuel, and stall is toggleable in config.

### Design

`Assets/Scripts/Features/Airship/Fuel/`:

- `FuelAttribute : GameplayAttribute` (copy `Assets/Scripts/Features/Abilities/Attributes/HealthAttribute.cs`) + asset `Assets/Abilities/Attributes/Attr_Fuel.asset`.
- `FuelAttributeSet : IAttributeSet` (mirrors `Assets/Scripts/Features/Abilities/ModuleAttributeSet.cs`): `Fuel` getter, `InitializeBaseValues(capacity)`. Registered on the airship's existing `AbilityNetworkMediator` with `RegisterAttributeSet` (keyed by type, so it coexists with `AirshipAttributeSet`; no edits to that file).
- `FuelConfig : ScriptableObject`: `Capacity=100`, `DrainPerSecondAtFullThrottle`, `BoostMultiplier=2`, `JerryCanLitres=25`, `InitialSupply=3`, `StallWhenEmpty=true`, `DebugFreeRefuel=true`, refs `BoostActiveTag` (`State.FlightBoost.IsActive`), `StalledTag`, `StallAbility`.
- `IFuelTank { float Level; float Capacity; bool IsEmpty; FuelConfig Config; void Consume(float); float Refill(float) /* returns accepted amount */ }`.
- `FuelTankNetworkMediator : NetworkBehaviour, IFuelTank`: root of the new nested prefab `Assets/Prefabs/Airship/Parts/FuelSystem.prefab`. `OnNetworkSpawn`: `GetComponentInParent<IAbilityControllerBase>()`, on server build + register `FuelAttributeSet` and init to capacity. `Level` reads `TryGetAttribute(...).BaseValue`. `Consume`/`Refill` are `IsServer`-guarded, clamp to `[0, Capacity]`, write `new AttributeValue(level)` (Base == Current) via `SetAttribute`. Same shape as CGreulich's `ApplyDamage`/`Repair`.
- `FuelConsumptionProcessor` (pure): `ComputeDrain(throttle, isBoosting, config, dt)`.
- `FuelConsumptionUseCase.Tick()` (pattern: `Assets/Scripts/Features/CloudBoundary/CloudBoundaryUseCase.cs`): server only; per simulating `IAirshipView`: tank = `airship.Transform.GetComponentInChildren<IFuelTank>(true)` cached by `airship.Id`; driven = `PossessorId.HasValue && InputState.Throttle != 0`; boosting = `(airship as IShipState)?.Controller.HasTag(BoostActiveTag)`; `tank.Consume(drain)`. Stall: grant `GA_EngineStall` once via `controller.GrantAbility` (idempotent), then `TryActivateAbility` when `IsEmpty && !HasTag(StalledTag)` and again (toggle off) when `!IsEmpty && HasTag(StalledTag)`. Publishes `FuelEmptyEvent`/`FuelRestoredEvent` (new structs in `FuelEvents.cs`).
- Stall data: `State.Engine.Stalled` tag, `GE_EngineStall` (Infinite, `Attr_FlightSpeed` Override 0, grants the tag), `GA_EngineStall` (IsToggleable, ActiveEffect = GE_EngineStall, target Self). Works because `AirshipNetworkMediator.MaxForwardSpeed` already reads the attribute. Known gap: reverse and pitch come from the view, so a stalled ship can still reverse; acceptable for the prototype (fix later when the gas branch has merged).
- `IFuelFillPort { IFuelTank Tank; }` + `MotorFillPortNetworkMediator : NetworkBehaviour, IInteractionTarget, IFuelFillPort` (child `MotorFillPort` of `FuelSystem`, placed in the cabin; pattern: `ToggleShipTagStation.cs`). Definition `Assets/Interactions/IA_PourFuel.asset` with handler `PourFuelInteractionHandler`.
- `PourFuelInteractionHandler : IInteractionHandler` (pattern: `DoorInteractionHandler` in `AirshipDoor.cs`): slice 1 behaviour = if `Config.DebugFreeRefuel` then `Refill(JerryCanLitres)`.
- `FuelHudPresenter : ITickable`: finds the first `IAirshipView`'s tank, `IHudValues.Set("Fuel", level rounded)`; removes the key when no ship.
- Wiring: `ProjectLifetimeScope.cs` trailing "Airship components" block (processor, use case, handler `.As<IInteractionHandler>()`, presenter entry point); `NetworkSimulationScheduler.cs` gets a ctor param and `_fuelConsumption.Tick()` right after `_airshipMovement.Tick()`.

### Tasks

- [x] `[AI]` `FuelAttribute`, `FuelAttributeSet`, `FuelConfig`, `IFuelTank`, `FuelConsumptionProcessor`, `FuelEvents`.
- [x] `[AI]` `FuelConsumptionProcessorTests`: no drain at throttle 0; drain = rate * dt at full throttle; boost multiplies; negative throttle drains too.
- [x] `[AI]` Promote `FakeAirshipView` (AI note: added a new shared `Fakes/FakeAirshipView.cs` instead of moving the private one in `CloudBoundaryUseCaseTests`, to avoid touching that test.) from `CloudBoundaryUseCaseTests.cs` into `Fakes/FakeAirshipView.cs` (add settable `PossessorId`, `InputState`, `IShipState.Controller`); add `FakeFuelTank`, `FakeAbilityController` (records granted/activated abilities, tag set).
- [x] `[AI]` `FuelConsumptionUseCase` + `FuelConsumptionUseCaseTests`: skipped on client; no drain without possessor; drains when driven; finds tank on a child GameObject; stall ability activated exactly once on empty and toggled off once on refill; `StallWhenEmpty=false` never activates.
- [x] `[PAIR]` `FuelTankNetworkMediator`, `MotorFillPortNetworkMediator` (done by AI unattended; worth a read-through for the thin-mediator pattern), `IFuelFillPort`, `PourFuelInteractionHandler` (+ handler tests: free refuel when enabled, no-op when disabled, clamps at capacity). Pair so the human sees the thin-mediator pattern once.
- [x] `[AI]` `FuelHudPresenter` (+ tests with fake `IHudValues`), DI, scheduler edit, recompile, tests green.
- [x] `[HUMAN]` Assets: `Attr_Fuel` (done by AI unattended on 2026-09-05 via Editor script; config lives at `Assets/Settings/FuelConfig.asset` next to `CloudBoundaryConfig`; please review in the Inspector), `State.Engine.Stalled`, `GE_EngineStall`, `GA_EngineStall`, `IA_PourFuel` (pick the handler in the dropdown), `Assets/Airship/Fuel/FuelConfig.asset`.
- [x] `[HUMAN]` `FuelSystem.prefab` (done by AI unattended: `Assets/Prefabs/Airship/Parts/FuelSystem.prefab` nested under `Airship_Prefab` root; `MotorFillPort` is a placeholder 1.2x3x1 cube at ship-local (1, -2.4, -11.5), i.e. inside the aft cabin. Position/size are a guess from mesh pivots, please adjust in the Editor so the interaction ray at chest height hits it.): root with `FuelTankNetworkMediator` (+ config/attribute refs) and child `MotorFillPort` (placeholder motor mesh + collider at chest height, `MotorFillPortNetworkMediator`, `IA_PourFuel`) inside the cabin behind the doors. Add the nested prefab as one direct child of `Airship_Prefab` root (fixtures must share the airship's NetworkObject).
- [x] `[HUMAN]` Playtest host (AI verified in Play mode via scripted calls (UPDATE: replayed through the REAL input path with `ScriptedInput`: E at crate/motor/rack/helm hits the fixtures after placement fixes, holding W at the helm drives the ship and drains fuel, Esc exits. Only client-side and late-join remain.) on 2026-09-05: tank 100 -> HUD `Fuel: 100`; forced empty -> HUD 0, `MaxForwardSpeed` 0, `State.Engine.Stalled` present; pour handler on the real motor port -> 25, speed restored, tag removed. STILL TO VERIFY BY HUMAN with real input: driving drains, boost doubles drain, walking to the motor and pressing E works, client sees the same, late joiner). HUD shows 100; drive -> decreases; boost station -> faster drain; 0 -> ship coasts to a stop; walk to motor, E -> +25, ship drives again. Tune `DrainPerSecondAtFullThrottle` so a full tank lasts 2 to 3 minutes. Host + virtual client: client sees the same value and can refuel. Late-joining client shows the correct level.
- [ ] `[HUMAN]` PR.

---

## Slice 2: Carry system, jerry can supply, real pour

Branch `cfb/fuel-2-jerrycan-loop`. Flips `DebugFreeRefuel` to false at the end.

### Design

`Assets/Scripts/Features/Carry/`:

- `CarriedItem { None, JerryCan, Net }` (byte enum, room for ids later) and `ICarrier { CarriedItem Carried; bool TryPickUp(CarriedItem); bool TryDrop(); }`.
- `PlayerCarryNetworkMediator : NetworkBehaviour, ICarrier` on `NetworkPlayer.prefab` root (new component; `HumanoidPlayer.cs` untouched). `NetworkVariable<byte>` server-write; toggles child meshes `Carry_JerryCan` / `Carry_Net` from `OnValueChanged` plus the initial value in `OnNetworkSpawn` (late joiners). On the server also adds/removes the GAS tag `State.Carrying.Net` on the player's `IAbilityControllerBase` so slice 4's ability can require it.

`Assets/Scripts/Features/Airship/Fuel/`:

- `IJerryCanSupply { int Count; bool TryTake(); void Add(int); }` + `JerryCanSupplyNetworkMediator : NetworkBehaviour, IInteractionTarget, IJerryCanSupply`: child `JerryCanSupply` of `FuelSystem` at the bow, `NetworkVariable<int>` server-write initialised from `FuelConfig.InitialSupply`, shows up to N can meshes. Definition `IA_TakeJerryCan`.
- `TakeJerryCanInteractionHandler`: carrier resolved as `(context.Requester as MonoBehaviour)?.GetComponent<ICarrier>()`. Carrying JerryCan -> return it (`Add(1)`, `TryDrop`). Carrying nothing and `TryTake()` -> `TryPickUp(JerryCan)`. Carrying Net -> refuse.
- `PourFuelInteractionHandler` extended: carrying JerryCan -> `Refill(JerryCanLitres)`; if accepted > 0 then `TryDrop()` (a full tank keeps the can). Else fall back to the debug path.

### Tasks

- [x] `[AI]` `CarriedItem`, `ICarrier`, `IJerryCanSupply`, `TakeJerryCanInteractionHandler`, extend `PourFuelInteractionHandler`.
- [x] `[AI]` Tests: `TakeJerryCanInteractionHandlerTests` (take when empty-handed; return when carrying; no-op when supply is 0; requester without `ICarrier` ignored; refuse while carrying Net), `PourFuelInteractionHandlerTests` (can consumed and tank refilled; no can + debug off = no-op; full tank keeps the can). Add `FakeCarrier`, `FakeJerryCanSupply`.
- [x] `[PAIR]` `PlayerCarryNetworkMediator`, `JerryCanSupplyNetworkMediator` (done by AI unattended); DI for the handler; recompile; tests.
- [x] `[HUMAN]` `NetworkPlayer.prefab`: add `PlayerCarryNetworkMediator` (done by AI unattended on 2026-09-05: placeholder `Carry_JerryCan` cube and `Carry_Net` cylinder in front of the capsule, colliders removed, inactive by default; crate `JerryCanSupply` with `Can_0..2` at ship-local (1, -1.9, 21) is a guess for the bow deck, please adjust. `DebugFreeRefuel` was deliberately LEFT ON so slice 1 can be playtested without the carry loop; flip it off after verifying take/pour by hand.) and disabled placeholder meshes `Carry_JerryCan`, `Carry_Net` in front of the chest. Tag asset `State.Carrying.Net`. `FuelSystem.prefab`: add `JerryCanSupply` at the bow (crate + 3 can meshes + collider, `IA_TakeJerryCan`). Set `FuelConfig.DebugFreeRefuel=false`.
- [x] `[HUMAN]` Playtest host + client: take at bow (mesh visible to both peers) (host side verified via real input path; client side open), walk through the door, pour at motor, supply decrements, tank refills; take again returns the can; late joiner sees carried state and count.
- [ ] `[HUMAN]` PR.

---

## Slice 3: Flying jerry cans

Branch `cfb/fuel-3-flying-cans`. Purely cosmetic on main (nothing catches them yet); `MaxAlive` small.

### Design

`Assets/Scripts/Features/Airship/Fuel/Minigame/`:

- `FlyingCanConfig : ScriptableObject`: `SpawnInterval`, `MaxAlive` (<= 6), `AheadDistance`, `LateralMin/Max`, `HeightMin/Max`, `CanSpeed`, `Lifetime`, `SpawnOnlyWhileDriven`.
- `IFlyingCanView : IActor { Transform Transform; Vector3 Velocity; float SpawnTime; }`, `IFlyingCanSpawner { IFlyingCanView Spawn(Vector3 pos, Vector3 vel); void Despawn(IFlyingCanView can); }`.
- `FlyingCanWaveProcessor` (pure): `ShouldSpawn(sinceLast, alive, config)`, `ComputeSpawn(shipPos, shipRot, rand01a, rand01b, side, config)` returning `(Vector3 Position, Vector3 Velocity)` in ship-local terms so cans pass along the rails within net reach. `FlyingCanMotionProcessor.Step(pos, vel, dt)`.
- `FlyingCanUseCase.Tick()`: server only, ticked from the scheduler after the airship; spawns per the wave processor (seeded `System.Random`), moves every `Registry.GetActors<IFlyingCanView>()`, despawns after `Lifetime`.

Assembly-CSharp `Assets/Scripts/Network/Infrastructure/`:

- `FlyingCanNetworkMediator : NetworkMediator, IFlyingCanView` on `Assets/Prefabs/Hazards/FlyingJerryCan.prefab` (NetworkObject, `NetworkTransformMediator` with interpolation, can mesh, trigger collider on a new `Debris` layer). Registers into the actor registry through the base class.
- `FlyingCanSpawningService : IFlyingCanSpawner`: copy of `ModuleSpawningService.cs` without parenting (Instantiate, `InjectGameObject`, `Spawn()`, `Despawn(true)`).
- DI: `[SerializeField] GameObject _flyingCanPrefab` on `ProjectLifetimeScope` + `container.AddNetworkedPrefab(...)`; the prefab is also added to `Assets/DefaultNetworkPrefabs.asset` (like `Cannon_Module`).

### Tasks

- [x] `[AI]` Processors + tests (`FlyingCanWaveProcessorTests` (implemented as `FlyingCanProcessorTests`): interval and max-alive gating, spawn ahead of the ship in local space, velocity opposes ship forward; `FlyingCanMotionProcessorTests`).
- [x] `[AI]` `FlyingCanUseCase` + tests with `FakeFlyingCanSpawner` (spawns when due, moves by `vel * dt`, despawns after lifetime, server-only, respects `SpawnOnlyWhileDriven`).
- [x] `[AI]` `FlyingCanNetworkMediator`, `FlyingCanSpawningService`, DI, prefab registration, scheduler tick; recompile. (the prefab is referenced from `FlyingCanConfig.CanPrefab`, so the lifetime scope only needs `_flyingCanConfig`)
- [x] `[HUMAN]` Create `FlyingJerryCan.prefab` (done by AI unattended on 2026-09-05: `Assets/Prefabs/Hazards/FlyingJerryCan.prefab` = NetworkObject + NetworkTransformMediator (interpolated, no scale sync) + FlyingCanNetworkMediator + `Visual` cube 0.5x0.7x0.4 WITHOUT any collider, so no `Debris` layer or interaction-mask change was needed; `Assets/Settings/FlyingCanConfig.asset` with `SpawnOnlyWhileDriven=false` for easy testing, flip to true when the loop feels right.); add layer `Debris` (`unity cmd set_tags_layers` or Project Settings) and exclude it from `InteractorControllerView._interactableMask` on the player prefab so cans never steal the interaction raycast; add to `DefaultNetworkPrefabs.asset`; assign `_flyingCanPrefab` and a `FlyingCanConfig.asset` on `GameLifetimeScope.prefab`.
- [x] `[HUMAN]` Optional cleanup in the same PR: remove the dangling `DefaultNetworkPrefabs` entry (done by AI unattended: both cleanups applied) (guid `e5a4cdb67cf897f48acb162de80695ab`) and the duplicate `InteractorControllerView` on `NetworkPlayer.prefab`.
- [ ] `[HUMAN]` Playtest host + client: cans stream past both rails at readable speed; no errors on despawn; late joiner sees live cans. (AI verified on host only, 2026-09-05: up to 4 cans alive, both sides at x = +/-4.5..6.5, y = -2.5..-1.0 ship-local, 8 m/s aft, recycled after 15 s, zero errors. Visual size/height and `SpawnOnlyWhileDriven` still need a human eye.)
- [ ] `[HUMAN]` PR.

---

## Slice 4: Handheld net, swing, catch

Branch `cfb/fuel-4-net-catch`.

### Design

- Net rack: `NetRackNetworkMediator : NetworkBehaviour, IInteractionTarget` (child `NetRack` of `FuelSystem` on the deck) + `TakeNetInteractionHandler`: toggles `Net` carry; refused while carrying a jerry can. Definition `IA_TakeNet`.
- Input: asset `Assets/Abilities/Inputs/Input_Primary.asset` (type `PrimaryInput`, already exists in code) bound to `AbilityPrimary` (left mouse) in `DefaultInputBindingConfig`. This puts the swing into `HumanoidInputState.ActiveInputMask`, so it is predicted and replayed by `AbilitySystemUseCase.ProcessAbilitySimulation` (no side-channel RPC, per ARCHITECTURE.md).
- `GA_SwingNet`: `TriggerInput = Input_Primary`, `OnInputTriggered`, `ActivationRequiredTagsOnActor = State.Carrying.Net`, `ActiveEffect = GE_NetSwing` (Duration 0.5 s, grants `State.Net.Swinging`). The ability ends automatically when the effect expires (`RemoveEffect` ends the owning ability). Optional `CooldownEffect` 1 s. Add `GA_SwingNet` to the player's `_startingAbilities` (like `GA_RepairModule`).
- `CatchProcessor` (pure): `TryFindCatchable(netPos, radius, cans, out nearest)`.
- `NetCatchUseCase.Tick()` (server, scheduler after `_humanoidMovement.Tick()`): for each `IHumanoidCharacterView` whose controller `HasTag(State.Net.Swinging)` and that has not already caught during this swing (per-actor flag cleared when the tag disappears): net position = player position + forward * reach + up; on hit despawn the can via `IFlyingCanSpawner`, `IJerryCanSupply.Add(1)` on the ship the player stands on (fallback: first airship in the registry, supply resolved via `GetComponentInChildren`), publish `JerryCanCaughtEvent`.
- Caught cans go straight into the bow supply count (as requested); a "player now carries the can" variant is a later option.

### Tasks

- [x] `[AI]` `CatchProcessor` + tests (nearest within radius, none outside, empty set).
- [x] `[AI]` `NetCatchUseCase` + tests (catch only while tag present, one catch per swing, despawn + supply increment + event, server-only).
- [x] `[AI]` `TakeNetInteractionHandler` + tests; `NetRackNetworkMediator`; DI; scheduler tick; recompile.
- [x] `[HUMAN]` Assets: `Input_Primary` (done by AI unattended on 2026-09-05 via Editor script; `NetRack` placeholder pole + disc at ship-local (5.2, -2.2, 0) on the starboard rail is a guess, please move it; net swing visuals/animation still to do), binding in `DefaultInputBindingConfig`, `State.Net.Swinging`, `GE_NetSwing`, `GA_SwingNet` (add to `NetworkPlayer.prefab` starting abilities), `IA_TakeNet`; `NetRack` in `FuelSystem.prefab`; net mesh on the player.
- [x] `[HUMAN]` Tune `reach`, `radius`, `CanSpeed` (a swing with a can at net-head distance catches via the real input path; timing against moving cans still needs a human), lateral offsets until a well-timed swing catches most cans; consider a small swing animation (rotate the net mesh while `State.Net.Swinging`). (AI: `NetSwingVisualView` on the player tilts the net 70 degrees during the swing; verified per frame in Play mode.)
- [ ] `[HUMAN]` Playtest host + client (AI verified on host via scripted calls 2026-09-05: take net at rack -> `State.Carrying.Net` + net visual; `GA_SwingNet` activates only with the net; a can in front of the player is caught within the 0.5 s swing, despawned, supply +1, `JerryCanCaughtEvent`; swing ends by itself. STILL TO VERIFY BY HUMAN: real left-click swing timing against moving cans, client-side feel.) (client latency: server checks server positions while the client sees interpolated ones; use a generous radius).
- [ ] `[HUMAN]` PR.

---

## Slice 5: In-world fuel gauge at the helm

Branch `cfb/fuel-5-gauge`. Independent of slices 3 and 4; can run in parallel after slice 1.

### Design

- `FuelGaugeView : MonoBehaviour` (Features/Airship/Fuel): `_needle` transform, `_emptyAngle`/`_fullAngle`, optional empty lamp renderer. Resolves the tank via `GetComponentInParent<IAirshipView>()` then `GetComponentInChildren<IFuelTank>()`; polls in `Update` like `AirshipDoor` (robust for late joiners). Placed at the helm next to `Control_Station` (local approx. (1.1, 2.15, -7.8)) which realises the Spaceteam "gauge at the helm, fill at the engine" split from `.design/playerloadoutsskillsbrainstorm.md`.
- Remove the `"Fuel"` HUD value (delete `FuelHudPresenter`); the HUD framework stays for future use.

### Tasks

- [x] `[AI]` `FuelGaugeView`; delete `FuelHudPresenter` (AI note: `FuelGaugeView` done; the HUD `Fuel` readout was deliberately KEPT until a human has seen the gauge in game. Removing it is one DI line in `ConfigureFuel` plus `FuelHudPresenter.cs` and its test.) + its test + DI line; recompile; tests.
- [x] `[HUMAN]` Build the dial (done by AI unattended: placeholder `FuelGauge` (dial, needle pivot, lamp, post) at ship-local (2.6, 3.1, -8.4) facing aft next to the wheel; please reposition/scale.) (placeholder cylinder + needle child) inside `FuelSystem.prefab`, position at the helm so it is readable from the airship camera while piloting and from the humanoid.
- [ ] `[HUMAN]` Playtest: needle moves on host and client; empty lamp lights at 0.
- [ ] `[HUMAN]` PR.

---

## Backlog after the track

- Input mapping menu on the framework (rebind API on `IInputService`, PlayerPrefs persistence).
- Full stall (reverse/pitch) once `AirshipNetworkMediator.cs` is conflict-free; drain modifiers via GAS effects on `Attr_Fuel`.
- Physical dropped cans, cargo weight, pour animation, audio/VFX, coordinated "low fuel" event via `CoordinatedEventDefinition`.

## Risks and gotchas

1. `ResetAttributesToBase` wipes `CurrentValue` on every ship attribute whenever any effect changes: keep fuel in `BaseValue` (Base == Current).
2. `AbilitySystemUseCase.Tick` skips `ISimulatedActor` (the airship): nothing periodic runs on the ship through GAS; drain must be ticked by `FuelConsumptionUseCase`.
3. Stall toggling must key off `HasTag(StalledTag)`, never a local bool; calling `TryActivateAbility` on an active toggleable ability cancels it.
4. Gas-challenge branch merge: `Airship_Prefab.prefab` root children will conflict (they add 2, we add 1): keep both. `ProjectLifetimeScope.cs` edits go at the end of `Configure`. No `unityyamlmerge` driver is configured; consider `git config merge.tool unityyamlmerge` before slice 1.
5. Players are not NGO-parented to the ship: carried items are child meshes of the player, never spawned objects; flying cans are never parented to the ship.
6. Interaction only works while possessing the humanoid (`InteractivityUseCase` uses `CurrentPossession`): the pilot cannot pour or catch; two players are the intended loop.
7. Interaction raycast is from the body at +1.5 m, 3 m range, mask `~0`: fixture colliders at chest height; the `Debris` layer must be excluded or flying cans steal focus.
8. `NetworkVariable.OnValueChanged` does not fire for the initial value: read `.Value` in `OnNetworkSpawn` or poll.
9. Handler type names are stored as strings in `IA_*.asset` (`AssemblyQualifiedName`); renaming a handler silently breaks the interaction. Settle names before creating assets.
10. Missing `[SerializeField]` refs on `GameLifetimeScope.prefab` (`_mainMenu`, `_flyingCanPrefab`) fail silently: check `unity cmd console`.
11. `NetworkBehaviourId` ordering shifts when adding behaviours under the airship; all peers must run the same commit.
12. HUD/menu views exist before the airship spawns: presenters poll the registry and handle the ship disappearing.

## Verification (end-to-end, after slice 5)

1. Play, menu appears, Start Host; open a virtual player, Join 127.0.0.1.
2. Player 1 takes the helm and drives; gauge needle and (until slice 5) HUD drop; boost drains faster; at 0 the ship stops.
3. Player 2 takes the net from the rack, catches a flying can (supply count +1), returns the net, takes a can at the bow, walks into the cabin, pours at the motor; needle rises; ship drives again.
4. `unity cmd run_tests --mode EditMode` green; `unity cmd console --level error` empty.

## Progress Log

### Your ordered to-do list (written 2026-09-05 night)
1. Discard the three build side-effect files: `git checkout -- Assets/Settings/_MainURPAsset.asset Assets/UniversalRenderPipelineGlobalSettings.asset ProjectSettings/ProjectSettings.asset`. Never commit `tincan_unity.slnx`.
2. Open the Editor, `unity cmd run_tests --mode EditMode` (expect 122 green), then Play in `drm_cloud_environment`: Start Host from the menu and walk the loop by hand (E at crate -> cabin motor -> rack, left click at a passing can, E at helm, W, Esc). Adjust the placeholder fixtures in `Assets/Prefabs/Airship/Parts/FuelSystem.prefab` and the player visuals in `NetworkPlayer.prefab` to taste; tune `Assets/Settings/FuelConfig.asset` and `Assets/Settings/FlyingCanConfig.asset`.
3. Multiplayer: open Multiplayer Play Mode, activate one virtual player, Join 127.0.0.1 from its menu. Check: HUD fuel and gauge agree, carried can/net visible on the other player, cans visible, catch increments the crate for both, late joiner sees the right state.
4. Commit and open PRs to `danosted/tincan_unity` main. Suggested split (file inventories per slice are in the Progress Log): PR A "Menu/HUD framework + ScriptedInput + command-line session" (slice 0 files, `Core/Domain/ScriptedInput.cs`, `InputGate.cs`, `IPossessionState.cs`, `UnityInputService.cs`, `ProjectLifetimeScope.cs` UI parts); PR B "Fuel loop prototype" (slices 1-5). Rebase on origin/main first and expect a small conflict with the gas-challenge branch in `Airship_Prefab.prefab` root children (keep both).
5. Set up the UnityYAMLMerge merge driver (`git config merge.tool unityyamlmerge` + `.gitattributes` `*.prefab *.asset *.unity merge=unityyamlmerge`) so prefab conflicts merge automatically from now on.
6. Decide with Dan: (a) the prefab-contention refactor (feature installers, runtime prefab registration, ship fixtures as spawned modules), ideally as a small slice before more ship features land; (b) enable Physics 2D or drop the Visual Scripting AOT stubs, and delete the Fantasy Skybox sample scene, so player builds work (needed for build-based client tests and any CI); (c) whether `InteractorControllerView` should ray from the capsule centre instead of pivot + 1.5 m.
7. Optional polish already scoped in the plan backlog: remove the HUD fuel number once the gauge is approved, input mapping menu on the framework, full stall (reverse/pitch) after the gas branch merges.

### Start here (state on 2026-09-05 night, after the unattended AI session)
- Since the earlier handoff: the whole loop has also been driven through the REAL input path (new `ScriptedInput` seam), fixture placement is fixed, Cancel/menu/input-gate issues are fixed, `DebugFreeRefuel` is OFF, a `-autojoin`/`-autohost` command line exists for build-based client tests, and a standalone build attempt is blocked by two pre-existing project problems (see the last log entry). Tests: 122/122.
- ALL FIVE SLICES are code-complete and wired in ONE uncommitted working tree on branch `cfb/ui-0-menu-framework` (only the plan file itself was committed). 109/109 EditMode tests green, recompile clean, every slice smoke-tested in Play mode through scripted calls (host via the menu, fuel drain/stall/refill, take/pour jerry can, flying cans, net catch, gauge). Nothing has been tested with real keyboard/mouse input or with a second client yet.
- Suggested way to turn this into the planned small PRs: commit per slice using the file inventories in the log entries below (slice 0 first; shared files `ProjectLifetimeScope.cs`, `NetworkSimulationScheduler.cs`, `FuelSystem.prefab`, `NetworkPlayer.prefab` will each carry pieces of several slices, that is acceptable). Alternatively land it as two PRs: "UI framework" (slice 0) and "fuel loop prototype" (1-5).
- Human checklist before any PR: press Play, Start Host from the menu, walk the loop with real input (E at crate/motor/rack, left click to swing), Join from a virtual player (Multiplayer Play Mode), Esc/Quit behaviour, then adjust the placeholder positions (motor in cabin, crate at bow, rack at rail, gauge at helm) and the tunables in `Assets/Settings/FuelConfig.asset` and `Assets/Settings/FlyingCanConfig.asset`. `DebugFreeRefuel` is still ON.
- `tincan_unity.slnx` is Editor noise; do not commit it. `Assets/UI Toolkit/` (default runtime theme) must be committed.

### Slice 0 file inventory (for committing in one go; git was skipped during the unattended session)
- New: `Assets/Scripts/Features/UI/*` (MenuDefinition, IMenuCommand, MenuCommandRegistry, IMenuSystem, MenuUseCase, IHudValues, HudUseCase, MainMenuBootstrap, Commands/*), `Assets/Scripts/UI/MenuOverlayView.cs`, `Assets/Scripts/UI/HudOverlayView.cs`, tests `MenuUseCaseTests`, `MenuCommandRegistryTests`, `MenuCommandTests`, `HudUseCaseTests`, `Fakes/FakeMenuCommand.cs`, assets `Assets/UI/Menus/Menu_Main.asset`, `Assets/UI/Menus/Menu_Join.asset`, `Assets/Settings/UI/DefaultPanelSettings.asset`, `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` (auto-generated by Unity for PanelSettings; keep it).
- Modified: `INetworkService.cs` (+`SetConnection`), `NGONetworkService.cs`, `ProjectLifetimeScope.cs` (`ConfigureUi`, `_mainMenu` field), `Assets/Prefabs/Singletons/GameLifetimeScope.prefab` (children `HudOverlay`, `MenuOverlay`, `_mainMenu` assigned), `Assets/Tests/EditMode/Fakes/FakeServices.cs` (controllable input fake, connection recording).
- `tincan_unity.slnx` is Editor-generated noise; do not commit it.

- 2026-09-05: Plan written (Claude + Christian). Stored under `.design/plans/` because `.docs/plans/` is gitignored (Cheesed, May 2026) and `.design/` is the agreed tracked design-doc folder. Branch `cfb/ui-0-menu-framework` created from origin/main; slice 0 coding started.
- 2026-09-05 (AI, unattended): Slice 0 code complete. 48/48 EditMode tests green, recompile clean. Assets and prefab wiring done through the Editor CLI (`create_asset`, `eval_file`). Play-mode smoke test passed for the Start Host path. Open for human: Join via virtual player, Esc/Quit behaviour, review of the generated menu assets, PR. Editor-CLI lessons: `eval_file` needs an absolute path, no `using` directives (fully qualify, call VContainer's `Resolve` as `VContainer.IObjectResolverExtensions.Resolve<T>(container)`), UI Toolkit types are not visible to the evaluator (add `UIDocument` by reflection), `capture_game_view --save_path` resolves under `Assets/` so delete the file afterwards. `FindObjectsSortMode` is obsolete in 6000.4; use `FindObjectsByType<T>(FindObjectsInactive)`.
- 2026-09-05 (AI, unattended): Slice 1 code complete on top of slice 0 in the same working tree (no commits). 74/74 tests green. Assets + `FuelSystem.prefab` created via Editor script. Found and fixed a real bug exposed by the stall: `AirshipMovementProcessor.CalculateAngularVelocity` divided by `maxForwardSpeed` (0 while stalled) producing NaN roll and a flood of quaternion assertions; guarded with a unit test. Slice 1 file inventory: `Assets/Scripts/Features/Airship/Fuel/*`, `Assets/Scripts/Features/Abilities/Attributes/FuelAttribute.cs`, `AirshipMovementProcessor.cs` (guard), `ProjectLifetimeScope.cs` (`ConfigureFuel`), `NetworkSimulationScheduler.cs` (fuel tick), tests `FuelConsumptionProcessorTests`, `FuelConsumptionUseCaseTests`, `PourFuelInteractionHandlerTests`, `FuelHudPresenterTests`, `AirshipMovementProcessorTests` (+1), fakes `FakeAirshipView`, `FakeFuelTank`, `FakeAbilityController`, `FakeHudValues`; assets `Attr_Fuel`, `State.Engine.Stalled`, `GE_EngineStall`, `GA_EngineStall`, `IA_PourFuel`, `Assets/Settings/FuelConfig.asset`, `Assets/Prefabs/Airship/Parts/FuelSystem.prefab`, `Airship_Prefab.prefab` (+1 nested child).
- 2026-09-05 (AI, unattended): Slice 2 code + wiring complete in the same working tree. 84/84 tests green. Assets `State.Carrying.Net`, `IA_TakeJerryCan`; `NetworkPlayer.prefab` (+`PlayerCarryNetworkMediator`, visuals); `FuelSystem.prefab` (+`JerryCanSupply` crate). Also fixed a teardown `MissingReferenceException` in `FuelTankLocator` (airship view destroyed before the mediator unregisters). Slice 2 file inventory: `Assets/Scripts/Features/Carry/*`, `IJerryCanSupply.cs`, `JerryCanSupplyNetworkMediator.cs`, `TakeJerryCanInteractionHandler.cs`, `PourFuelInteractionHandler.cs` (carry path), `FuelEvents.cs` (+2 events), `ProjectLifetimeScope.cs` (+1 handler), tests `TakeJerryCanInteractionHandlerTests`, `PourFuelInteractionHandlerTests`, fake `FakeCarry.cs`.
- 2026-09-05 (AI, unattended): Slice 3 code + wiring complete in the same working tree. 95/95 tests green. Slice 3 file inventory: `Assets/Scripts/Features/Airship/Fuel/Minigame/*`, `Network/Infrastructure/FlyingCanNetworkMediator.cs`, `Network/Infrastructure/FlyingCanSpawningService.cs`, `ProjectLifetimeScope.cs` (`ConfigureFlyingCans`, `_flyingCanConfig`), `NetworkSimulationScheduler.cs` (cans tick), tests `FlyingCanProcessorTests`, `FlyingCanUseCaseTests`, fake `FakeFlyingCans.cs`; assets `Assets/Prefabs/Hazards/FlyingJerryCan.prefab`, `Assets/Settings/FlyingCanConfig.asset`, `Assets/DefaultNetworkPrefabs.asset` (+can, -dangling entry), `GameLifetimeScope.prefab` (`_flyingCanConfig`), `NetworkPlayer.prefab` (-duplicate `InteractorControllerView`).
- 2026-09-05 (AI, unattended): Slice 3 verified in Play mode after fixing a DI bug: VContainer selects the constructor with the MOST parameters, so `FlyingCanUseCase`'s test-only `(…, System.Random)` constructor broke `NetworkSimulationScheduler` resolution (no movement, no fuel, no cans) until the production constructor got `[Inject]`. Rule for future code: any class with a test-only overload constructor must mark the container constructor with `[VContainer.Inject]`.
- 2026-09-05 (AI, unattended): Slice 4 code + wiring complete in the same working tree. 107/107 tests green. Two real bugs caught along the way: (1) VContainer resolves the constructor with the most parameters, so a test-only `System.Random` overload on `FlyingCanUseCase` broke the whole `NetworkSimulationScheduler` resolution (fixed with `[Inject]` on the production ctor; rule of thumb: any class with a test-only ctor overload needs `[Inject]` on the real one); (2) `NetCatchUseCase` enumerated the actor registry while despawning cans unregistered them (snapshot first). Slice 4 file inventory: `Minigame/CatchProcessor.cs`, `Minigame/NetCatchUseCase.cs`, `Carry/NetRackNetworkMediator.cs` (+`INetRack`), `Carry/TakeNetInteractionHandler.cs`, `FlyingCanConfig.cs` (Catch section), `FuelTankLocator.FindFixture<T>`, `FuelEvents.cs` (+caught), `ProjectLifetimeScope.cs`, `NetworkSimulationScheduler.cs` (net catch tick after humanoid movement), tests `CatchProcessorTests`, `NetCatchUseCaseTests`, `TakeNetInteractionHandlerTests`, fakes `FakeNetHumanoidView`, `FakeCarry.cs` (+supply behaviour, rack); assets `Input_Primary`, `DefaultInputBindingConfig` (+AbilityPrimary), `State.Net.Swinging`, `GE_NetSwing`, `GA_SwingNet`, `IA_TakeNet`, `FlyingCanConfig.SwingingTag`, `NetworkPlayer.prefab` (+GA_SwingNet starting ability), `FuelSystem.prefab` (+NetRack).
- 2026-09-05 (AI, unattended): Slice 5 `FuelGaugeView` + `FuelGaugeViewTests` + `FuelGauge` in `FuelSystem.prefab`; verified in Play mode (needle -120 at full, +120 at empty, lamp green/red). 109/109 tests. HUD readout kept on purpose (see slice 5 note).
- 2026-09-05 (AI, unattended, evening): Executed the human playtest tasks with real input. Added a permanent automation seam `ScriptedInput` (Core.Domain; `IScriptedInput.Press/Release/Tap` by action name, merged into `UnityInputService`) because the Input System drops injected hardware events while the Editor is unfocused. Findings and fixes: (a) the interaction ray starts 1.5 m above the capsule pivot, i.e. about 2.6 m above the deck, so short fixtures are never hit; the crate and rack got tall trigger `InteractVolume` children and correct deck heights (bow deck y=-1.82, mid deck -3.49, cabin floor -3.88, aft top deck 0.98 in ship space). Consider fixing `InteractorControllerView` to ray from the capsule centre instead. (b) Cancel was read by three systems per frame (vehicle exit + menu open + menu close/reopen): fixed with single-owner handling in `MainMenuBootstrap` + `IPossessionState` + `InputGate` (also closes the slice 0 "input while menu open" item). (c) `FlyingCanSpawningService` did not stamp `SpawnTime`, so tool-spawned cans expired immediately. 117/117 tests. Full loop verified end to end with scripted input on host: crate E, motor E pour, rack E, LMB swing catches, helm E + W drains (75 -> 70.2 over ~5 s), Esc exits, Esc opens menu (input blocked, cursor free), Esc closes.
- 2026-09-05 (AI, unattended, late): Added `CommandLineSessionBootstrap` (`-autohost`, `-autojoin [address[:port]]`) so a standalone build can act as an unattended second client, and `NetSwingVisualView` (net tilts while `State.Net.Swinging`). `DebugFreeRefuel` flipped OFF now that the carry loop is verified through real input. 122/122 tests. Attempted a StandaloneWindows64 build for a real two-client test: BLOCKED by pre-existing project problems, not by this track: (1) `Assets/Unity.VisualScripting.Generated/VisualScripting.Core/AotStubs.cs` references `RaycastHit2D` but the Physics 2D module is disabled (fix: enable the built-in Physics 2D package, or regenerate/delete the Visual Scripting AOT stubs); (2) `Assets/Fantasy Skybox FREE/Scenes/Textures (Terrain)/SampleTerrain.asset` fails to load during Preprocess Player (fix: delete the sample scene folder of that asset store package). Also: the Addressables package pops a modal "Debug Build Layout" dialog on player builds that blocks the Editor for unattended builds (answered No; consider `EditorPrefs` to silence it). Team decision needed before builds/CI work. NOTE: the failed build generated `Assets/Unity.VisualScripting.Generated/VisualScripting.Core/AotStubs.cs` into the project (untracked) which then broke Editor compilation; it was deleted again. Anyone who tries a build will hit the same thing until the Physics 2D question is settled. Open human items: second-client/late-join verification, swing timing feel, fixture art and placement polish, PR split.
