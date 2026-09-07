# Code map

Where things live, what the suffixes mean, and which features exist. Paths are relative to the repo root.

## Assemblies and the one-way rule

```
TinCan.Core.Domain  <--  TinCan.Features  <--  Assembly-CSharp
       ^                       ^
       +----- TinCan.Tests.EditMode (sees only these two)
```

| Assembly | asmdef | Contains | May reference |
|---|---|---|---|
| `TinCan.Core.Domain` | `Assets/Scripts/Core/Domain/TinCan.Core.Domain.asmdef` | Contracts, registries, `SimulationUseCase`, `ISimulationTickable`, the GAS vocabulary, `FeatureInstaller`. | NGO runtime, VContainer |
| `TinCan.Features` | `Assets/Scripts/Features/TinCan.Features.asmdef` | One folder per gameplay concern: processors, use cases, mediators, installers. | Core.Domain, NGO, VContainer |
| `Assembly-CSharp` | none (Unity default) | `Core/Infrastructure` (composition root), `Network/Infrastructure` (NGO adapters for core actors), `Scripts/UI` (overlay views). | Everything |
| `TinCan.Features.Interaction.Editor` | `Assets/Scripts/Features/Interaction/Editor/` | One property drawer (handler dropdown). Editor only. | Features, Core.Domain |
| `TinCan.Tests.EditMode` | `Assets/Tests/EditMode/` | NUnit tests + `Fakes/`. Editor only. | Core.Domain, Features, Tests.Shared |
| `TinCan.Tests.Shared` | `Assets/Tests/Shared/` | Fakes usable outside Editor-only assemblies. | Core.Domain |

**Consequence:** a class in `Assembly-CSharp` cannot be unit-tested. Put logic in `Features`; keep only things
that need `NetworkManager`, `UnityTransport` or `UIDocument` in `Assembly-CSharp`.

## Folder map

### Code

| Folder | Assembly | What is in it |
|---|---|---|
| `Assets/Scripts/Core/Domain/` | Core.Domain | `IActor`, `IActorRegistry`, `IPossessable`, `IInputService`, `ITimeService`, `SimulationAbstraction.cs` (`SimulationUseCase<TView,TInput>`), `ISimulationTickable.cs` (phases + `SimulationTickRunner`), `IInjectedView`, `IShipModule`, `ScriptedInput`. |
| `Core/Domain/Abilities/` (+`Attributes`, `Inputs`, `Tags`) | Core.Domain | GAS contracts: `IAbilityController`, `GameplayTag`, `GameplayAttribute`, `AttributeValue`, `GameplayInput`, `InputBindingConfig`. |
| `Core/Domain/Events/` | Core.Domain | `IEventPublisher`, `IEventObserver`, `GameEvents`, `LogEvent` (+ `LogInfo` extension). |
| `Core/Domain/Features/` | Core.Domain | `FeatureInstaller`, `FeatureInstallerCatalog`, `ShipFixtureDefinition`. |
| `Core/Domain/Networking/` | Core.Domain | `INetworkService`, `INetworkPlayerSpawner`, `IModuleSpawningService`. |
| `Assets/Scripts/Core/Infrastructure/` | Assembly-CSharp | `ProjectLifetimeScope` (composition root), `NetworkPrefabInterceptor` (injects before NGO spawn), `ActorOrchestrator` (registers spawned hierarchies), `ActorRegistry`, `UnityInputService`, `ProjectTimeService`, `Events/`. |
| `Assets/Scripts/Network/Infrastructure/` | Assembly-CSharp | `NetworkSimulationScheduler` (the tick), `NetworkMediator` base, `HumanoidPlayer`, `AirshipNetworkMediator`, `NGONetworkService`, `NetworkPlayerSpawner`, `ModuleSpawningService`, `NgoInteractionTargetResolver`, `FlyingCanNetworkMediator`, ship-module mediators, `Abilities/AbilityNetworkMediator`. |
| `Assets/Scripts/Features/<X>/` | Features | See the feature index below. |
| `Assets/Scripts/UI/` | Assembly-CSharp | `MenuOverlayView`, `HudOverlayView` (UI Toolkit, throwaway). |
| `Assets/Tests/EditMode/` | Tests.EditMode | `*Tests.cs` and `Fakes/`. |

### Assets (data)

| Folder | Created via | Holds |
|---|---|---|
| `Assets/Resources/Installers/` | TinCan > Features > *X* Feature Installer | One `FeatureInstaller` asset per feature. This is what turns a feature on. |
| `Assets/Settings/` | TinCan > Airship / Environment > *X* Config | `*Config` tunables (`FuelConfig`, `FlyingCanConfig`, `CloudBoundaryConfig`, `CloudVisualProfile`), URP assets, `UI/DefaultPanelSettings`. |
| `Assets/Settings/Fixtures/` | TinCan > Features > Ship Fixture | `ShipFixtureDefinition` assets (prefab + ship-local pose). |
| `Assets/Interactions/` | TinCan > Interactions > Interaction Definition | `IA_*` assets: which handler runs when you press E on a target. |
| `Assets/Abilities/AbilityDefinitions/` | TinCan > Abilities > Ability Definition | `GA_*` abilities. |
| `Assets/Abilities/Effects/` | TinCan > Abilities > Effect Definition | `GE_*` effects (duration, modifiers, granted tags). |
| `Assets/Abilities/Tags/` | TinCan > Abilities > Tag | `State.*`, `Interaction.*` gameplay tags. |
| `Assets/Abilities/Attributes/` | TinCan > Abilities > Attributes > *X* | `Attr_*` attribute identities. |
| `Assets/Abilities/Inputs/` | TinCan > Abilities > Inputs > *X* | `Input_*` ability inputs and `DefaultInputBindingConfig` (input action to input bit). |
| `Assets/UI/Menus/` | TinCan > UI > Menu Definition | `Menu_Main`, `Menu_Join`. |
| `Assets/Prefabs/Singletons/` | | `GameLifetimeScope` (the scope + UI overlays), `NetworkService`, `PossessionMediator`, `BuildPlacementMediator`. |
| `Assets/Prefabs/` | | `NetworkPlayer.prefab` (player, camera, carry visuals). |
| `Assets/Prefabs/Airship/` | | `Airship_Prefab.prefab`; `Parts/FuelSystem.prefab` (fixture spawned onto the ship). |
| `Assets/Prefabs/Hazards/`, `Modules/` | | `FlyingJerryCan`, `GasPocket`; `Cannon_Module` (build mode). |
| `Assets/Models/<Group>/<Asset>/` | | Imported art (FBX + textures + the shipped `manifest.json`/`INTEGRATION.md`), one folder per asset: `ShipComponents/FuelGauge/fuel_gauge.fbx`. Raw models never go in scenes directly; wrap them in a prefab under `Assets/Prefabs/`. |
| `Assets/Scenes/` | | `drm_cloud_environment` (main, build index 0), `cvg_airship_default`, `cheesed_scene`, `cvg_gaspocket_test`. Scenes are nearly empty on purpose; gameplay spawns at runtime. |

## Naming glossary

| Suffix | Role | Lifecycle | Lives in | Tested? | Example |
|---|---|---|---|---|---|
| `*Processor` | Pure calculation, no state, no Unity objects. | `Lifetime.Transient` or static | Features | Always | `Features/Airship/Fuel/FuelConsumptionProcessor.cs` |
| `*UseCase` | Orchestration: reads registries, calls processors, writes through mediators. | `ITickable`, `ISimulationTickable`, `IInitializable` | Features | Usually | `Features/Airship/Fuel/FuelConsumptionUseCase.cs` |
| `*NetworkMediator` | Thin NGO adapter; implements a domain interface; `NetworkVariable`s and RPCs; `IsServer` guards on writes. | Unity/NGO | Features (new) or `Network/Infrastructure` (legacy) | Via the interface it implements | `Features/Airship/Fuel/FuelTankNetworkMediator.cs` |
| `*View` | MonoBehaviour that renders or reads input; `IInjectedView` if it sits in a scene/prefab and wants DI. | Unity | Features or `Scripts/UI` | Static math only | `Features/Airship/Fuel/FuelGaugeView.cs` |
| `*Presenter` | `ITickable` that pushes a value into `IHudValues`. | `ITickable` | Features | Yes | `Features/Airship/Fuel/FuelHudPresenter.cs` |
| `*Config` | ScriptableObject of tunables. | asset | Features + `Assets/Settings` | n/a | `Features/Airship/Fuel/FuelConfig.cs` |
| `*Definition` | ScriptableObject describing what a thing is (ability, effect, interaction, menu, fixture). | asset | Features / Core.Domain | n/a | `Features/Interaction/InteractionDefinition.cs` |
| `*Handler` | `IInteractionHandler` chosen by name in an `IA_*` asset. | Singleton `.As<IInteractionHandler>()` | Features | Always | `Features/Airship/Fuel/PourFuelInteractionHandler.cs` |
| `*Locator` | Static helper that finds a fixture on an airship and survives Unity fake-null. | static | Features | Indirectly | `Features/Airship/Fuel/FuelTankLocator.cs` |
| `*AttributeSet` | Wraps GAS attributes for one actor type. | plain class | Features | Yes | `Features/Airship/Fuel/FuelAttributeSet.cs` |
| `*Events` | `readonly struct` events for one feature, published via `IEventPublisher`. | value types | Features | Asserted in tests | `Features/Airship/Fuel/FuelEvents.cs` |
| `*Registry` | Runtime lookup filled by `ActorOrchestrator` or VContainer collection injection. | Singleton | Features / Core | Some | `Features/Interaction/InteractionHandlerRegistry.cs` |
| `*FeatureInstaller` | `FeatureInstaller` subclass: registers the feature, lists prefabs and fixtures. | asset in `Resources/Installers` | Features | `FeatureCompositionTests` | `Features/Airship/Fuel/FuelFeatureInstaller.cs` |
| `Fake*` | Hand-written test double for a domain interface. | test | `Tests/EditMode/Fakes` | is the test | `Tests/EditMode/Fakes/FakeFuelTank.cs` |
| `I*View` | The contract a use case depends on so it can be faked. | interface | Features / Core | n/a | `Features/HumanoidMovement/IHumanoidCharacterView.cs` |

## Where does X live

| X | Look at |
|---|---|
| A tunable number | The `*Config.asset` in `Assets/Settings/`; the fields are declared in the matching `*Config.cs`. |
| "Press E on a thing" | Target: a `NetworkBehaviour : IInteractionTarget` (`MotorFillPortNetworkMediator.cs`). Behaviour: an `IInteractionHandler`. Link: `Assets/Interactions/IA_*.asset`. Chain: `Features/Interaction/InteractorControllerView.cs` (raycast), `InteractivityUseCase`, `NetworkMediator.RequestInteraction` RPC, `InteractionOrchestrator`, handler. |
| An ability, effect or tag | Assets under `Assets/Abilities/`; runtime in `Features/Abilities/AbilitySystemUseCase.cs`; per-actor state in `Network/Infrastructure/Abilities/AbilityNetworkMediator.cs`. |
| An input that triggers an ability | `Assets/Abilities/Inputs/Input_*.asset` bound in `DefaultInputBindingConfig.asset`; becomes a bit in `HumanoidInputState.ActiveInputMask` so it is predicted and replayed. |
| Starting abilities | `_startingAbilities` on `NetworkPlayer.prefab` (`HumanoidPlayer`) and `Airship_Prefab.prefab` (`AirshipNetworkMediator`). Shared assets; edit with care. |
| A HUD number | `IHudValues` (`Features/UI/IHudValues.cs`) written by a `*Presenter`; rendered by `Scripts/UI/HudOverlayView.cs`. |
| A menu or menu row | `Assets/UI/Menus/*.asset`; commands in `Features/UI/Commands/`; see `UI_FRAMEWORK.md`. |
| Something bolted onto the ship | A fixture prefab + `ShipFixtureDefinition` in `Assets/Settings/Fixtures/`, listed by an installer; spawned by `Features/Airship/Fixtures/ShipFixtureSpawningUseCase.cs`. |
| The fixed tick order | `Network/Infrastructure/NetworkSimulationScheduler.cs`, `SimulateNetworkTick`. Features hook in with `ISimulationTickable` + `SimulationPhase`. |
| Player movement / look | `Features/HumanoidMovement/` (use case, processor, `HumanoidInputState`); mediator `Network/Infrastructure/HumanoidPlayer.cs`. |
| Airship movement | `Features/Airship/AirshipMovementUseCase.cs`, `AirshipMovementProcessor.cs`, `AirshipInputState.cs`; mediator `Network/Infrastructure/AirshipNetworkMediator.cs`. |
| Possession (who drives what) | `Features/Possession/` (`PossessionUseCase` client side, `ServerPossessionManager` authority, `Infrastructure/PossessionNetworkMediator`). |
| RPCs and NetworkVariables | Only inside `*NetworkMediator` classes. |
| Events / logging | `IEventPublisher.Publish` and `LogInfo(source, message)`; observed by `Core/Infrastructure/Events/DebugLogEventObserver.cs`. |
| Command-line session start | `Features/UI/CommandLineSessionBootstrap.cs`: `-autohost`, `-autojoin [address[:port]]`. |
| Scripted input for automation | `Core/Domain/ScriptedInput.cs` (`IScriptedInput.Press/Release/Tap`), merged into `UnityInputService`. |
| The DI wiring | `Core/Infrastructure/ProjectLifetimeScope.cs` for core services and legacy features; `Assets/Resources/Installers/*.asset` for everything newer. |

## Feature index

Style: **installer** = registered through a `FeatureInstaller` asset (the target pattern). **direct** = registered
in `ProjectLifetimeScope.cs` (legacy; migrate when touched). Add a row when you land a feature.

| Feature | Folder (`Assets/Scripts/Features/`) | Style | Entry points | Assets | Tests |
|---|---|---|---|---|---|
| Fuel loop (tank, drain, stall, jerry cans, motor, gauge, HUD) | `Airship/Fuel/` | installer | `FuelConsumptionUseCase`, `FuelTankNetworkMediator`, `PourFuelInteractionHandler`, `TakeJerryCanInteractionHandler`, `FuelHudPresenter`, `FuelGaugeView`, `FuelMotorStatusView` | `Resources/Installers/FuelFeatureInstaller`, `Settings/FuelConfig`, `Settings/Fixtures/FuelSystemFixture`, `Prefabs/Airship/Parts/FuelSystem`, `Models/ShipComponents/FuelGauge/fuel_gauge`, `Models/ShipComponents/FuelEquipment`, `Animations/AetherEquipment`, `IA_PourFuel`, `IA_TakeJerryCan`, `Attr_Fuel`, `GA_EngineStall`, `GE_EngineStall`, `State.Engine.Stalled` | `FuelConsumption*Tests`, `FuelFixtureRegistrationTests`, `PourFuelInteractionHandlerTests`, `TakeJerryCanInteractionHandlerTests`, `FuelGaugeViewTests`, `FuelMotorStatusViewTests`, `FuelHudPresenterTests`, `AirshipStallTests` |
| Flying cans + net catch | `Airship/Fuel/Minigame/` | installer (Order 10) | `FlyingCanUseCase`, `NetCatchUseCase`, `TakeNetInteractionHandler`, `Network/Infrastructure/FlyingCanNetworkMediator`, `FlyingCanSpawningService` | `Resources/Installers/FlyingCanFeatureInstaller`, `Settings/FlyingCanConfig`, `Prefabs/Hazards/FlyingJerryCan`, `IA_TakeNet`, `GA_SwingNet`, `GE_NetSwing`, `State.Net.Swinging`, `Input_Primary` | `FlyingCan*Tests`, `CatchProcessorTests`, `NetCatchUseCaseTests`, `NetSwingPredictionTests`, `TakeNetInteractionHandlerTests` |
| Carry (net / jerry can on the player) | `Carry/` | via Fuel + FlyingCan installers | `PlayerCarryNetworkMediator`, `NetRackNetworkMediator`, `NetSwingVisualView` | `State.Carrying.Net`, visuals on `NetworkPlayer.prefab` | covered by handler tests |
| Menus + HUD framework | `UI/` (+ views in `Scripts/UI/`) | installer (Order -10) | `MenuUseCase`, `HudUseCase`, `MainMenuBootstrap`, `CommandLineSessionBootstrap`, `Commands/*` | `Resources/Installers/UiFeatureInstaller`, `UI/Menus/Menu_Main`, `Menu_Join`, overlays on `GameLifetimeScope.prefab` | `Menu*Tests`, `HudUseCaseTests`, `MainMenuBootstrapTests`, `CommandLineSessionBootstrapTests` |
| Ship fixtures (generic spawner) | `Airship/Fixtures/` | direct (core) | `ShipFixtureSpawningUseCase` | any `Settings/Fixtures/*` | `ShipFixtureSpawningUseCaseTests`, `FeatureCompositionTests` |
| Airship movement | `Airship/` | direct | `AirshipMovementUseCase`, `AirshipControllerView`, `Network/Infrastructure/AirshipNetworkMediator` | `Prefabs/Airship/Airship_Prefab`, `Attr_FlightSpeed`, `GA_FlightSpeed_Boost`, `IA_AcquireAirshipControl`, `IA_ToggleFlightBoost` | `AirshipMovementProcessorTests` |
| Airship door | `Airship/PhysicalParts/AirshipDoor.cs` | direct (handler at the end of `Configure`) | `AirshipDoor`, `DoorInteractionHandler` | `IA_ToggleDoor`, `Interaction.Toggle.Door` | none |
| Humanoid movement + look | `HumanoidMovement/` | direct | `HumanoidMovementUseCase`, `PlayerLookUseCase`, `HumanoidControllerView`, `Network/Infrastructure/HumanoidPlayer` | `Prefabs/NetworkPlayer`, `Attr_MoveSpeed`, `Attr_JumpForce`, `Attr_Stamina`, `GA_Sprint`, `GE_SprintBuff` | `HumanoidMovement*Tests`, `SimulationUseCaseTests` |
| Possession | `Possession/` | direct | `PossessionUseCase`, `ServerPossessionManager`, `PossessionInputController`, `Infrastructure/PossessionNetworkMediator` | `Prefabs/Singletons/PossessionMediator` | none |
| Interaction system | `Interaction/` | direct (core) | `InteractivityUseCase`, `InteractionOrchestrator`, `InteractionHandlerRegistry`, `VehicleBoardingUseCase`, `MaintenanceUseCase` | `Assets/Interactions/*` | `AirshipInteractionInvestigationTests` |
| Abilities (GAS-like) | `Abilities/` | direct (core) | `AbilitySystemUseCase`, `Network/Infrastructure/Abilities/AbilityNetworkMediator` | `Assets/Abilities/**` | `HealthAttributeSetTests`, `InstantGameplayEffectTests` |
| Build mode + ship modules | `Network/Infrastructure/BuildModeUseCase.cs`, `ModuleSpawningService.cs`, `ShipModule*NetworkMediator.cs` | direct | `BuildModeUseCase`, `ModulePlacementUseCase` | `Prefabs/Modules/Cannon_Module`, `Prefabs/Singletons/BuildPlacementMediator`, `GA_BuildMode`, `State.Building`, `IA_RepairModule`, `IA_DamageModule` | none |
| Cloud boundary + visuals | `CloudBoundary/` | direct (ticked explicitly by the scheduler) | `CloudBoundaryUseCase`, `CloudEnvironmentView` | `Settings/CloudBoundaryConfig`, `Settings/CloudVisualProfile` | `CloudBoundary*Tests` |
| Gas pocket challenge | `GasChallenge/` | direct | `GasChallengeUseCase`, `GasPocketVolume` | `Prefabs/Hazards/GasPocket`, `GE_GasPocketExplosion`, scene `cvg_gaspocket_test` | `GasPocketDetonationProcessorTests` |
| Coordinated events | `Events/` | direct | `EventOrchestratorUseCase`, `ToggleShipTagStation` | `CoordinatedEventDefinition` (no asset yet) | none |
| Free camera | `FreeCamera/` | direct | `FreeCameraMovementUseCase`, `FreeCameraTransformView` | | none |
| Environment helpers | `Environment/` | none needed | `MovingPlatform`, `SimpleOscillator` | | `Tests/Shared/FakeMovingGround` |

## Aether equipment visuals

`Assets/Models/ShipComponents/FuelEquipment/` supplies aether crystals, receiver, staff and rack as nested FBX instances inside the existing gameplay
prefabs. Equipment wrappers use a uniform 2.5 scale, matching the fuel gauge. Preserve the imported FBX root
transforms: their rotation and scale perform the axis/unit conversion. Carry grip offsets remain inside the
scaled wrappers; adjust the wrapper poses on the player when tuning first-person visibility. The player's
`NetSwingVisualView` uses a 30-degree swing for the longer pole, keeping the hook near the existing catch area.

| Prefab | Visuals |
|---|---|
| `Assets/Prefabs/Airship/Parts/FuelSystem.prefab` | Aether receiver (`fuel_motor`) and `FuelMotorStatusView` under `MotorFillPort`; `jerry_can_rack` and three violet crystal vessels under `JerryCanSupply/Can_0..2`; catching staff (`can_catcher`) under `NetRack`; existing fuel gauge. Crystal wrappers stay direct children for supply visibility, positioned from the rack's three slot sockets. |
| `Assets/Prefabs/NetworkPlayer.prefab` | Violet crystal under `Carry_JerryCan`, catching staff under `Carry_Net`, aligned to their updated grip sockets. The crystal grip is raised to clear the deck at 2.5 scale. Keep wrapper names: carry visibility and swing animation resolve them at runtime. |
| `Assets/Prefabs/Hazards/FlyingJerryCan.prefab` | Violet crystal under `Visual`, recentered using the new mesh bounds on the stationary networked pickup root. |

The receiver's body box fits its revised dimensions, with a separate interaction trigger reachable at player
height. The rack has individual floor, divider and rail boxes that leave the bays open; the staff has a shaft
capsule. Imported crystal materials use URP transparency and emissive wind. Model sockets remain available.
Legacy fuel IDs are retained: `jerry_can_red.fbx` now contains the violet crystal, and `fuel_motor.fbx` the receiver.
The art pack's suggested energy capacities, empty starting receiver, channelling, cap mechanics and physics
are not gameplay changes: amounts, pickup, catch and refill still come from the existing feature code/configs.

`Assets/Animations/AetherEquipment/` holds three four-second looping clips and AnimatorControllers for the
violet crystal, receiver and staff. Clips are derived from the FBX `Scene` animation and retain only wind/mote
transform curves. Full exported clips also key the root and static props; do not assign them directly to held
or floating models because those curves overwrite grip offsets and centering. Regenerate these wind-only
clips when animation content in the FBXs changes. Animators live on model roots, with root motion disabled.

`Assets/Scripts/Features/Airship/Fuel/FuelMotorStatusView.cs` reads the replicated tank level. At or below
5% capacity it changes `FuelMotor_DialCore` and `FuelMotor_DialRune*` to red; above the threshold they glow cyan.
The old added lamp/housing and EMPTY plate are no longer used. Colors use material property blocks so instances
do not modify shared materials. `FuelMotor_Heart_Wind_1/2` and their motes remain visible with any energy and
hide only at zero; refilling restores them even below 5%. The warning threshold and colors are tunable on the
view. Engine stalling still requires zero fuel. `FuelGaugeView` controls only its needle. Coverage lives in
`Assets/Tests/EditMode/FuelMotorStatusViewTests.cs`, including exactly 5%, initially empty, refill and independent
warning/powered transitions.

### Stationary fuel pickups

`Assets/Settings/FuelConfig.asset` starts the rack with one crystal. All current pickups use the violet crystal in the legacy red model;
the teal import is reserved for a future fuel type. `Assets/Scripts/Features/Airship/Fuel/Minigame/FlyingCanUseCase.cs`
seeds scattered pickups ahead of the first simulating ship, then adds batches at the horizon as it travels.
`Assets/Settings/FlyingCanConfig.asset` starts the initial spawn volumes 60 m ahead, with random offsets up to
55 m left/right, 25 m above/below, and 15 m forward/backward. Every candidate must be at least 50 m from
every ship's centre and 10 m from another can; blocked candidates are retried a bounded number of times,
then skipped. The clearance accommodates the current hull and sails; tune it if the ship model changes.
New batches spawn after 20 m of travel at a 120 m horizon, with a 200 m removal distance and 48-can cap.
Spawn direction follows actual displacement, including reverse and vertical travel.
Cans stay fixed in world space with no lifetime. Only cans far from every simulating ship are removed;
nearby cans are preserved at the cap, and a separation check avoids overlapping pickups on return trips.
Standing still or turning in place adds no cans after the initial field. Net catches still add to the rack.
Ship collision response remains deferred: approaching an existing can does not push or relocate it.
Coverage is in `Assets/Tests/EditMode/FlyingCanUseCaseTests.cs`, `FlyingCanProcessorTests.cs` and
`NetCatchUseCaseTests.cs`; tune horizon visibility and catch approach feel with a host/client playtest.

## Legacy, oddities and traps

- **Empty scaffold folders** (only `.gitkeep`): `Core/Application`, `Core/Presentation`, `Player`, `Utils`,
  `Features/Airship/Infrastructure`. Nothing is missing; they are leftovers from an initial folder layout.
- `Features/ThirdPersonCharacter/` holds one used file (`ThirdPersonLookView`) that belongs next to
  `IOrbitalLookView` in `HumanoidMovement`.
- **Two `IShipState` interfaces** exist: `Core/Domain/IShipState.cs` and `Features/Events/IShipState.cs`,
  different namespaces. Check which one you are importing.
- **Mid-migration:** `ProjectLifetimeScope.cs` mixes core wiring, legacy feature registrations, and the generic
  installer loop. Do not add feature registrations there; write an installer.
- **String-typed links that break silently:** `InteractionDefinition` stores the handler as an assembly-qualified
  type name (rename the class and the `IA_*` dropdown goes blank, the interaction becomes a no-op);
  `MenuDefinition` rows reference commands by `CommandId` string; `IHudValues` keys are strings.
- **VContainer picks the constructor with the most parameters.** If a class has a test-only overload, mark the
  production constructor with `[Inject]` or the whole scheduler fails to resolve.
- **`NetworkVariable.OnValueChanged` does not fire for the initial value.** Read `.Value` in `OnNetworkSpawn`.
- **Fixture binding:** a mediator inside a spawned fixture must bind to the ship in both `OnNetworkSpawn` and
  `OnNetworkObjectParentChanged` (clients see the parent change later than the spawn).
- **Fixture parenting is local-space.** `ModuleSpawningService` bakes the pose into ship-local values and parents with
  `worldPositionStays = false`, so NGO replicates the local pose. Parenting with `true` replicates the world pose and
  clients (interpolated ship, late joiners) end up with the fixture floating off the ship.
- **UniTask is not installed** even though `CODE_STANDARDS.md` names it. No async or coroutine code exists yet.
- **`ProjectSettings/EditorBuildSettings.asset`** still lists two deleted scenes under `Assets/Scenes/Dev/`.
- **Standalone builds are blocked** by Visual Scripting AOT stubs referencing Physics 2D (module disabled) and a
  Fantasy Skybox sample terrain that fails to load. Playtesting is Editor + Multiplayer Play Mode for now.
- `Tests/Shared/FakeMovingGround.cs` uses the namespace `TinCan.Tests.EditMode.Fakes` despite living in
  `TinCan.Tests.Shared`.
- The three `*.slnx` files at the root are Editor-generated and gitignored. Do not commit them.
