# Task guides

Short recipes keyed by what you want to do. Each one says which files to open, the steps, how to verify, and a
real example in the repo to copy from. For a whole new feature use
[`TUTORIAL_NEW_FEATURE.md`](TUTORIAL_NEW_FEATURE.md); for the layout see [`CODE_MAP.md`](CODE_MAP.md).

Verification commands assume the Editor is open and `unity status` reports `ready`:

```bash
unity cmd recompile && unity cmd recompile_status
```
```bash
unity cmd run_tests --mode EditMode
```
```bash
unity cmd console --level error
```

## 1. Tune a number

**When:** drain rate, capacity, spawn interval, speeds, anything a designer would tweak.

**Touch:** the `*Config.asset` in `Assets/Settings/` (fields declared in the matching `*Config.cs`).

**Steps:** select the asset, edit in the Inspector, press Play. No recompile.

**Know this:** two config styles coexist. `FuelConfig` is assigned on `FuelTankNetworkMediator` inside
`Assets/Prefabs/Airship/Parts/FuelSystem.prefab` and read through `IFuelTank.Config`, so it is per fixture.
`FlyingCanConfig` is assigned on `Assets/Resources/Installers/FlyingCanFeatureInstaller.asset` and injected
(`builder.RegisterInstance(config)`), so it is global. If a field is missing, add it to the `*Config.cs` with
`[Header]`, `[Tooltip]` and `[Min]`, recompile, then set it in the asset.

**Verify:** the value changes in play. For fuel, the HUD number and the gauge at the helm.

## 2. Add an interaction ("press E on a thing")

**When:** a station, a valve, a crate: something the humanoid walks up to and uses.

**Touch:**
- Target: a small `NetworkBehaviour : IInteractionTarget` that exposes `Definition`. Copy
  `Assets/Scripts/Features/Airship/Fuel/MotorFillPortNetworkMediator.cs` (25 lines). If handlers need to find
  the target without NGO, add a marker interface like `INetRack` in `Features/Carry/NetRackNetworkMediator.cs`.
- Behaviour: an `IInteractionHandler`. Copy `Features/Airship/Fuel/PourFuelInteractionHandler.cs`: a type guard
  on `context.Target`, then a `switch` over the state matrix, `LogInfo` on every refused branch.
- Registration: in your `FeatureInstaller.Install`:
  `builder.Register<MyHandler>(Lifetime.Singleton).As<IInteractionHandler>();`
- Asset: **Assets > Create > TinCan > Interactions > Interaction Definition**, save as
  `Assets/Interactions/IA_<Verb>.asset`, pick the handler from the dropdown.
- Prefab: add the target component and a collider at chest height (the interaction ray starts 1.5 m above the
  player pivot, 3 m range), assign the `IA_` asset.

**Verify:** a handler test (copy `Assets/Tests/EditMode/PourFuelInteractionHandlerTests.cs`); then Play, Start
Host, walk up, press E, watch the console for your `LogInfo` line.

**Trap:** the handler is stored by assembly-qualified name. Renaming the class blanks the dropdown and makes the
interaction a silent no-op. Settle the name before creating the asset.

## 3. Add an ability, effect or tag

**When:** a timed buff, a state the server needs to know about, an action bound to a key.

**Touch:**
- Tag: **TinCan > Abilities > Tag** in `Assets/Abilities/Tags/` (naming: `State.X.Y`, `Interaction.X`).
- Effect: **TinCan > Abilities > Effect Definition** in `Assets/Abilities/Effects/` (`GE_*`): duration, attribute
  modifiers, tags granted while active. Example: `GE_NetSwing` (0.5 s, grants `State.Net.Swinging`).
- Ability: **TinCan > Abilities > Ability Definition** in `Assets/Abilities/AbilityDefinitions/` (`GA_*`):
  required/blocked tags, `ActiveEffect`, cost, cooldown, `TriggerInput`. Example: `GA_SwingNet` requires
  `State.Carrying.Net`.
- Key binding: an input asset under `Assets/Abilities/Inputs/` (**TinCan > Abilities > Inputs > ...**) bound in
  `DefaultInputBindingConfig.asset`. This becomes a bit in `HumanoidInputState.ActiveInputMask`, so the ability
  is predicted on the client and replayed on the server. Never trigger simulated actions with a bare `ServerRpc`.
- Granting: add the `GA_` to `_startingAbilities` on `NetworkPlayer.prefab` or `Airship_Prefab.prefab`, or grant
  at runtime with `IAbilityControllerBase.GrantAbility` (see `FuelConsumptionUseCase.UpdateStall`).

**Read:** `Features/Abilities/AbilitySystemUseCase.cs` for activation, cost, cooldown and effect ticking.

**Verify:** `HasTag` / attribute assertions with `Fakes/FakeAbilityController.cs` (it has
`GrantsTagWhileActive`); in play, the tag shows up on both host and client.

**Trap:** `ResetAttributesToBase` sets `CurrentValue = BaseValue` on every attribute whenever any effect on that
actor changes. Persistent values (fuel level) live in `BaseValue`.

## 4. Add a menu row or a HUD value

**When:** a button in the start menu, a sub-menu, a number on screen.

**Touch:** see [`UI_FRAMEWORK.md`](UI_FRAMEWORK.md) sections "Adding a command", "Adding a menu", "Showing a HUD value".
- Command: implement `IMenuCommand` in `Features/UI/Commands/`, register `.As<IMenuCommand>()` in
  `UiFeatureInstaller` or your own installer, put a `Command` row with that `CommandId` in a `MenuDefinition`.
- Menu: **TinCan > UI > Menu Definition** in `Assets/UI/Menus/`; reach it via a `Submenu` row (no code).
- HUD value: an `ITickable` presenter that calls `IHudValues.Set(key, text)`. Copy
  `Features/Airship/Fuel/FuelHudPresenter.cs`; register `.As<ITickable>()`.

**Verify:** `Assets/Tests/EditMode/MenuCommandTests.cs`, `FuelHudPresenterTests.cs` as templates
(`Fakes/FakeHudValues.cs`, `Fakes/FakeMenuCommand.cs`).

**Trap:** `CommandId` and HUD keys are strings; a typo is silent.

## 5. Add a networked fixture on the ship

**When:** a physical thing that belongs to the airship: a tank, a rack, a gauge, a station.

**Touch:**
- Prefab under `Assets/Prefabs/Airship/Parts/` with one root `NetworkObject` (`AutoObjectParentSync` and
  `SyncOwnerTransformWhenParented` on, no `NetworkTransform`), authored in ship-local coordinates. Put every part
  of the feature inside it: `FuelSystem.prefab` holds tank, crate, motor, gauge and net rack.
- Root component: a `NetworkBehaviour` that implements `IShipModule` and binds to the ship in both
  `OnNetworkSpawn` and `OnNetworkObjectParentChanged` (copy `FuelTankNetworkMediator.cs`).
- Definition: **TinCan > Features > Ship Fixture** in `Assets/Settings/Fixtures/`, pointing at the prefab.
- Installer: yield it from both `ShipFixtures` and `NetworkedPrefabs` (see `FuelFeatureInstaller.cs`).

`Features/Airship/Fixtures/ShipFixtureSpawningUseCase.cs` furnishes every airship once, server side. Details in
[`FEATURE_INSTALLERS.md`](FEATURE_INSTALLERS.md).

**Verify:** Start Host; the fixture appears under the airship in the hierarchy on host and on a virtual client.

**Do not** edit `Airship_Prefab.prefab` or `DefaultNetworkPrefabs.asset`.

## 6. Add a system that runs on the simulation tick

**When:** server-authoritative logic that must run in lockstep with movement (drain, spawning, catching).

**Touch:** a `*UseCase : ISimulationTickable` with a `Phase` (`AfterAirship` or `AfterHumanoid`). Copy
`Features/Airship/Fuel/FuelConsumptionUseCase.cs`: first line of `Tick()` is `if (!_networkService.IsServer)
return;`, targets are cached per airship `Guid` and revalidated with a `*Locator.IsAlive` check. Register
`.AsSelf().As<ISimulationTickable>()` in your installer.

**Tick order** (`Network/Infrastructure/NetworkSimulationScheduler.cs`): airship movement, `AfterAirship`
tickables, cloud boundary, `Physics.SyncTransforms`, humanoid movement, `AfterHumanoid` tickables. Within a
phase, installers run by `Order` then name.

For per-frame, all-peer work (HUD, visuals) use `ITickable` instead.

**Verify:** a use-case test with `Fakes/FakeServices.cs` (`FakeNetworkService.IsServer`, `FakeActorRegistry`,
`FakeTimeService`) as in `FuelConsumptionUseCaseTests.cs`; `FeatureCompositionTests.cs` covers phase ordering.

**Trap:** the airship is an `ISimulatedActor`, so `AbilitySystemUseCase.Tick` skips it. Periodic ship logic must
be a tickable, not a GAS effect.

## 7. Write a test or a fake

**When:** always, for processors, use cases and handlers. Views get a static math function tested instead.

**Touch:** `Assets/Tests/EditMode/<Class>Tests.cs`; fakes in `Assets/Tests/EditMode/Fakes/`.

**Steps:**
1. Name tests `Method_Scenario_Outcome` (`Handle_CarryingACanIntoAFullTank_KeepsTheCan`).
2. Arrange with existing fakes: `FakeServices.cs` (network, time, input, registry, event publisher),
   `FakeAirshipView`, `FakeHumanoidCharacterView`, `FakeAbilityController`, `FakeFuelTank`, `FakeCarry`,
   `FakeFlyingCans`, `FakeHudValues`, `FakeMenuCommand`, `FakePossessionState`.
3. Need a new fake? Implement the domain interface by hand and record calls in public lists. If a locator must
   find it on a `GameObject`, add a `MonoBehaviour` twin with `AttachTo(parent)` like `FakeFuelTankBehaviour`.
4. ScriptableObjects: `CreateInstance<T>()` in `[SetUp]`, `DestroyImmediate` in `[TearDown]`.
5. For a `MonoBehaviour` view, move the math into a `public static` and test that (`FuelGaugeView.NeedleAngle`).

**Verify:**
```bash
unity cmd run_tests --mode EditMode
```

**Trap:** tests cannot reference `Assembly-CSharp`. If your class is in `Core/Infrastructure`, `Network/Infrastructure`
or `Scripts/UI`, the test will not compile; move the logic to `Features`.

## 8. Run and playtest with a second player

**Host:** open `Assets/Scenes/drm_cloud_environment.unity`, Play, **Start Host**. Esc opens and closes the menu
(Cancel is owned by `MainMenuBootstrap`; in a vehicle Esc exits the vehicle first).

**Second player in the Editor:** Window > Multiplayer > Multiplayer Play Mode, activate one virtual player, then
in its window **Join** with `127.0.0.1` / `7777`. Verify both peers see the same state and a late joiner sees the
right values.

**Unattended client from a build:** a player build accepts `-autohost` or `-autojoin [address[:port]]`
(`Features/UI/CommandLineSessionBootstrap.cs`). Standalone builds are currently blocked (see CODE_MAP traps).

**Automation:** resolve `IScriptedInput` and `Tap("Interact")`, `Press("MoveForward")`; injected Input System
events are dropped while the Editor is unfocused, this seam is not.

**Editor-side checks from the shell:**
```bash
unity cmd editor_play
```
```bash
unity cmd capture_game_view --source screen
```
```bash
unity cmd editor_stop
```

## 9. Review what the AI just did

**When:** a branch or PR landed that you did not write.

**Steps:**
1. Read the plan under `.docs/plans/` (committed with the work) or the PR description first.
2. Shape of the change:
   ```bash
   git log --oneline --stat main..HEAD
   ```
   ```bash
   git diff main...HEAD --stat -- Assets/Scripts Assets/Tests
   ```
3. Look for the things that carry behaviour but are easy to miss:
   - new or changed `Assets/Resources/Installers/*.asset` (a feature was switched on or reordered)
   - `Assets/Interactions/IA_*.asset`, `Assets/UI/Menus/*.asset`, `Assets/Abilities/**` (data-driven behaviour)
   - prefab diffs (`Airship_Prefab`, `NetworkPlayer`, `GameLifetimeScope`): read them with UnityYAMLMerge or in
     the Editor, not as raw YAML
   - edits to `ProjectLifetimeScope.cs` or `NetworkSimulationScheduler.cs` (should be rare now; ask why)
   - `#nullable enable`, `NetworkMediator` suffix, `IsServer` guards on every write
4. Tests: new `*Tests.cs` next to new classes? Run them.
5. Feature index: is there a row in [`CODE_MAP.md`](CODE_MAP.md#feature-index)? If not, add it as part of review.
6. Playtest host + virtual client using the loop the change describes. AI sessions verify with scripted input on
   the host only; client feel, late join and placement are usually still open.

## 10. Move a legacy feature onto an installer

**When:** you touch a feature that still registers in `ProjectLifetimeScope.cs`.

**Steps:**
1. Subclass `FeatureInstaller` in the feature folder (copy `FuelFeatureInstaller.cs`); move the feature's
   `builder.Register...` lines from `ProjectLifetimeScope.Configure` into `Install`. Entry points registered via
   `UseEntryPoints` become `.As<ITickable>()` / `.As<IInitializable>()` registrations.
2. `[SerializeField]` fields on `ProjectLifetimeScope` that only that feature uses move to the installer; assign
   them on the new asset under `Assets/Resources/Installers/` and remove them from `GameLifetimeScope.prefab`.
3. Prefabs the feature spawns move from `_buildablePrefabs` / `DefaultNetworkPrefabs.asset` to `NetworkedPrefabs`.
4. Mediators that only that feature uses move from `Network/Infrastructure/` into the feature folder (they
   compile in `TinCan.Features` as long as they do not touch `NetworkManager` or `UnityTransport`).
5. Update the style column in the feature index.

**Verify:** recompile, tests, Start Host, and the feature still works on a virtual client.
