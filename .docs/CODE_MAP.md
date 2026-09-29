# Code map

Where things live, what the suffixes mean, and which features exist. Paths are relative to the repo root.

## Assemblies and the one-way rule

```
TinCan.App                     composition root and overlay views (sees everything)
  TinCan.Features.<Feature>    one per feature; sees the core and the features it references
  TinCan.Network               NGO adapters for core actors (HumanoidPlayer, AirshipNetworkMediator, ...)
  TinCan.Core.Infrastructure   core implementations (ActorOrchestrator, registries, input, time)
  core systems, lowest first:
    TinCan.Items, TinCan.UI
    TinCan.Ship                the airship itself and Fixtures/
    TinCan.Interaction
    TinCan.Humanoid            movement, input state, look, targeter, attribute set
    TinCan.Gas                 abilities, effects, tags, cues, the ability-grant socket
    TinCan.Possession, TinCan.Targeting, TinCan.Entities
      TinCan.Core.Domain       contracts, including the ones shared across systems

Each layer references only layers below it. TinCan.DevTools and TinCan.Tests.EditMode reference what they exercise.
```

**Every feature is its own assembly.** A feature folder gets an asmdef named `TinCan.Features.<Name>` that
references only what the feature uses: `TinCan.Core.Domain`, the core systems it needs, and any other feature it
builds on. The compiler then enforces the boundaries:
- A feature cannot touch another feature it doesn't reference.
- Core can never depend on a feature: circular references are a compile error, and a rule test covers the rest.
- A reference between features is also that feature's profile requirement.

The worked example is [`Features/GasChallenge/`](../Assets/Scripts/Features/GasChallenge/): one asmdef referencing
only `TinCan.Gas` and `TinCan.Ship`, and no Netcode, because it needs none. `Features/SkyHazards/` shows DevTools and
tests referencing a feature.

**The core systems are assemblies too, in a fixed order** (the list above). A contract two core systems share goes
down into `TinCan.Core.Domain`, never sideways; examples are `Core/Domain/Look/` (`IOrbitalLookView`,
`IHasOrbitalCamera`), `IPossessionReceiver`, `Core/Domain/Hud/IHudValues`, and the `IHumanoidActor` / `IShipActor`
markers. Folder, namespace and assembly agree: a core system lives in `Assets/Scripts/Core/<System>/`, its namespace
is `TinCan.Core.<System>`, and its assembly is `TinCan.<System>` (`Core/Gas/` is `TinCan.Core.Gas` in `TinCan.Gas`).
Features live in `Assets/Scripts/Features/<Feature>/` as `TinCan.Features.<Feature>`. The shared `TinCan.Features`
block now only holds `Carry/`; see the roadmap.
[`.docs/plans/feature-assemblies.md`](plans/feature-assemblies.md) is the roadmap and
[`.docs/plans/core-assemblies.md`](plans/core-assemblies.md) the core split. Enforced by `ArchitectureRulesTests`:
- `FeatureInstallers_LiveInTheirOwnAssembly`;
- `SharedAssemblies_DoNotReferenceFeatureAssemblies`: no core assembly references a feature;
- `FeatureProfiles_LoadTheFeaturesTheirFeaturesReference`;
- `AssemblyCSharp_HoldsOnlyTheTopLayer`;
- `GameplayCueNotifies_LoadAllTheirActions`: cue actions are `[SerializeReference]` data stored with their assembly
  name, so moving an action type needs `[MovedFrom]`.

**Assembly-CSharp is empty.** Unity compiles every script outside an asmdef into Assembly-CSharp, an assembly that
sees everything and that nothing (tests included) can reference. No project script lives there any more: a script
that lands there forgot its asmdef, and `AssemblyCSharp_HoldsOnlyTheTopLayer` fails. The top layer is now two ordinary
assemblies:
- `TinCan.Network` (`Network/Infrastructure/`): the NGO adapters for core actors;
- `TinCan.App` (`App/`): the composition root and the overlay views.

Both can be unit-tested directly.

| Assembly | asmdef | Contains | May reference |
|---|---|---|---|
| `TinCan.Core.Domain` | `Assets/Scripts/Core/Domain/TinCan.Core.Domain.asmdef` | Contracts, registries, `SimulationUseCase`, `ISimulationTickable`, the GAS vocabulary, `FeatureInstaller`. | NGO runtime, VContainer |
| `TinCan.Targeting`, `TinCan.Possession`, `TinCan.Entities` | in `Core/Targeting/`, `Core/Possession/`, `Core/Entities/` | Core systems with no core dependency: the targeting query, possession (authority, camera and cursor responders), `EntityNetworkMediator`. | Core.Domain, NGO, VContainer |
| `TinCan.Gas` | `Core/Gas/TinCan.Gas.asmdef` | Abilities, effects, attributes, tags, cues (`Cues/`, with `[MovedFrom]` on the cue actions), `ActorAbilityGrantUseCase`, the tag and cue installers. | Core.Domain, Targeting, VContainer |
| `TinCan.Humanoid` | `Core/Humanoid/TinCan.Humanoid.asmdef` | Humanoid movement and prediction, `HumanoidInputState`, `HumanoidAttributeSet`, `HumanoidTargeter`, `ThirdPersonLookView`, `ParentLocalSpaceVolume`. | Core.Domain, Gas, NGO, VContainer |
| `TinCan.Interaction` | `Core/Interaction/TinCan.Interaction.asmdef` | Interaction orchestration, handlers, vehicle boarding, the interaction installer. | Core.Domain, Gas, Humanoid, Possession, Targeting, NGO, VContainer |
| `TinCan.Ship` | `Core/Ship/TinCan.Ship.asmdef` | The airship itself and `Fixtures/` (fixture spawning). Its features (fuel, damage, the door) live under `Features/Airship/`. | Core.Domain, Humanoid, Interaction, NGO, VContainer |
| `TinCan.Items`, `TinCan.UI` | `Core/Items/`, `Core/UI/` | Items and equipment; the headless menu/HUD framework. | Items: Core.Domain, Gas, Interaction, NGO, VContainer. UI: Core.Domain, Possession, VContainer |
| `TinCan.Features` | `Assets/Scripts/Features/TinCan.Features.asmdef` | The leftover shared block: only `Carry/` (net rack and net-swing visuals). Closed to new code. | Core.Domain, Interaction, NGO |
| `TinCan.Features.<Feature>` | `Assets/Scripts/Features/<Feature>/TinCan.Features.<Feature>.asmdef` | One feature: its processors, use cases, mediators, installer. Today: `GasChallenge`, `SkyHazards`, `Stations`, `Weapons.Cannon` (references Stations), `DesignedEvents`, `Airship.Fuel`, `Airship.Fuel.Minigame` (references Fuel), `Airship.Damage` (references DesignedEvents), `Airship.PhysicalParts` (the door), `CloudBoundary` (with submersion), `FreeCamera`, `Environment`, `Events`. | Core.Domain, the core systems it uses, the features it builds on, and only the packages it uses |
| `TinCan.Core.Infrastructure` | `Assets/Scripts/Core/Infrastructure/TinCan.Core.Infrastructure.asmdef` | Core implementations behind `Core.Domain` contracts: `ActorOrchestrator`, `ActorRegistry`, `AbilityRegistry`, `NetworkPrefabInterceptor`, `UnityInputService`, `ProjectTimeService`, `ShipStateProvider`, `Events/`. Unit-testable. | Core.Domain, Entities, Gas, Humanoid, Interaction, NGO, Input System, VContainer |
| `TinCan.Network` | `Assets/Scripts/Network/Infrastructure/TinCan.Network.asmdef` | The NGO adapters for core actors: `NetworkMediator` base, `HumanoidPlayer`, `AirshipNetworkMediator`, `AbilityNetworkMediator`, `NGONetworkService`, `NetworkSimulationScheduler`, ship-module mediators. | Core.Domain, Core.Infrastructure, Gas, Humanoid, Interaction, Possession, Ship, NGO, Collections, VContainer |
| `TinCan.App` | `Assets/Scripts/App/TinCan.App.asmdef` | The composition root (`ProjectLifetimeScope`) and the overlay views (`Views/`). | Everything below it |
| `Assembly-CSharp` | none (Unity default) | Empty: no project script compiles here any more. | Everything |
| `TinCan.DevTools` | `Assets/Scripts/DevTools/TinCan.DevTools.asmdef` | Network test harness: latency presets, input bot, movement telemetry, feature scenarios (`Scenarios/`). Inert unless its flags are set. | Core.Domain, the core systems and feature assemblies its scenarios drive, NGO, UTP, Input System, VContainer |
| `TinCan.DevTools.Editor` | `Assets/Scripts/DevTools/Editor/` | **TinCan > Dev > Net Harness** menu: assigns MPPM player tags and launches Player 2. Scenario menus, the test-range scene builder and scene switching. Editor only. | The core systems and feature assemblies its builders use, DevTools (and reflection into MPPM) |
| `TinCan.Interaction.Editor`, `TinCan.Gas.Editor` | `Core/Interaction/Editor/`, `Core/Gas/Editor/` | Property drawers (handler dropdown, cue action picker). Editor only. | Interaction or Gas, Core.Domain |
| `TinCan.Tests.EditMode` | `Assets/Tests/EditMode/` | NUnit tests + `Fakes/`. Editor only. | Core.Domain, every core system and feature assembly it tests, DevTools, Tests.Shared |
| `TinCan.Tests.Shared` | `Assets/Tests/Shared/` | Fakes usable outside Editor-only assemblies. | Core.Domain |

## Folder map

### Code

| Folder | Assembly | What is in it |
|---|---|---|
| `Assets/Scripts/Core/Domain/` | Core.Domain | `IActor`, `IActorRegistry`, `IPossessable`, `IInputService`, `ITimeService`, `SimulationAbstraction.cs` (`SimulationUseCase<TView,TInput>`), `ISimulationTickable.cs` (phases + `SimulationTickRunner`), `IInjectedView`, `IShipModule`, `ScriptedInput`. |
| `Core/Domain/Abilities/` (+`Attributes`, `Inputs`, `Tags`) | Core.Domain | GAS contracts: `IAbilityController`, `GameplayTag`, `GameplayAttribute`, `AttributeValue`, `GameplayInput`, `InputBindingConfig`. |
| `Core/Domain/Events/` | Core.Domain | `IEventPublisher`, `IEventObserver`, `GameEvents`, `LogEvent` (+ `LogInfo` extension). |
| `Core/Domain/Features/` | Core.Domain | `FeatureInstaller`, `FeatureInstallerCatalog`, `FeatureProfile`, `ShipFixtureDefinition`. |
| `Core/Domain/Networking/` | Core.Domain | `INetworkService`, `INetworkPlayerSpawner`, `IModuleSpawningService`. |
| `Assets/Scripts/App/` | App | `ProjectLifetimeScope` (composition root), `Views/` (`MenuOverlayView`, `HudOverlayView`: UI Toolkit, throwaway). |
| `Assets/Scripts/Core/Infrastructure/` | Core.Infrastructure | `NetworkPrefabInterceptor` (injects before NGO spawn), `ActorOrchestrator` (registers spawned hierarchies), `ActorRegistry`, `AbilityRegistry`, `UnityInputService`, `ProjectTimeService`, `ShipStateProvider`, `Events/`, `Extensions/`. |
| `Assets/Scripts/Network/Infrastructure/` | Network | `NetworkSimulationScheduler` (the tick), `NetworkMediator` base, `HumanoidPlayer`, `AirshipNetworkMediator`, `NGONetworkService`, `NetworkPlayerSpawner`, `ModuleSpawningService`, `NgoInteractionTargetResolver`, ship-module mediators, `Abilities/AbilityNetworkMediator`. |
| `Core/Domain/Look/`, `Core/Domain/Hud/` | Core.Domain | Contracts shared by core systems: `IOrbitalLookView`, `IHasOrbitalCamera`; `IHudValues`. Also `IPossessionReceiver` and the `IHumanoidActor` / `IShipActor` markers (`ActorKinds.cs`) in `Core/Domain/`. |
| `Assets/Scripts/Features/<X>/` | the core system or feature assembly in that folder | See the assembly table above and the feature index below. |
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
| `Assets/Prefabs/Singletons/` | | `GameLifetimeScope` (the scope + UI overlays), `NetworkService`, `PossessionMediator`. |
| `Assets/Prefabs/` | | `NetworkPlayer.prefab` (player, camera, carry visuals). |
| `Assets/Prefabs/Airship/` | | `Airship_Prefab.prefab`; `Parts/FuelSystem.prefab` (fixture spawned onto the ship). |
| `Assets/Prefabs/Hazards/`, `Modules/` | | `FlyingJerryCan`, `GasPocket`; `Cannon_Module` (a ship module; nothing spawns it since build mode was removed). |
| `Assets/Models/<Group>/<Asset>/` | | Imported art (FBX + textures + the shipped `manifest.json`/`INTEGRATION.md`), one folder per asset: `ShipComponents/FuelGauge/fuel_gauge.fbx`. Raw models never go in scenes directly; wrap them in a prefab under `Assets/Prefabs/`. |
| `Assets/Scenes/` | | `drm_cloud_environment` (main, build index 0), `cvg_airship_default`, `cheesed_scene`, `cvg_gaspocket_test`. Scenes are nearly empty on purpose; gameplay spawns at runtime. |
| `Assets/Scenes/Test/`, `Assets/Prefabs/Test/`, `Assets/Settings/FeatureProfiles/Test/` | TinCan > Dev > Test Range > Rebuild Scenes | The test range scenario runs use: generated area scenes (`Test_Core`, `Test_ShipDamage`, `Test_NetCatch`, `Test_Cannon`), the bare `TestShip_Prefab`, and one feature profile per area. Do not edit the scenes by hand; see [`NETWORK_TEST_HARNESS.md`](NETWORK_TEST_HARNESS.md#test-range). |

## Naming glossary

| Suffix | Role | Lifecycle | Lives in | Tested? | Example |
|---|---|---|---|---|---|
| `*Processor` | Pure calculation, no state, no Unity objects. | `Lifetime.Transient` or static | Features | Always | `Features/Airship/Fuel/FuelConsumptionProcessor.cs` |
| `*UseCase` | Orchestration: reads registries, calls processors, writes through mediators. | `ITickable`, `ISimulationTickable`, `IInitializable` | Features | Usually | `Features/Airship/Fuel/FuelConsumptionUseCase.cs` |
| `*NetworkMediator` | Thin NGO adapter; implements a domain interface; `NetworkVariable`s and RPCs; `IsServer` guards on writes. Takes its actor id from `ActorIdentity`; never registers itself. | Unity/NGO | Features (new) or `Network/Infrastructure` (legacy) | Via the interface it implements | `Features/Airship/Fuel/FuelTankNetworkMediator.cs` |
| Entity | One per networked object (`EntityNetworkMediator`): the stable id and the only registrar. | Unity/NGO | `Core/Entities/` | `EntityTests`, rules | `Core/Entities/EntityNetworkMediator.cs` |
| `*View` | MonoBehaviour that renders or reads input; `IInjectedView` if it sits in a scene/prefab and wants DI. | Unity | Features or `App/Views` | Static math only | `Features/Airship/Fuel/FuelGaugeView.cs` |
| `*Presenter` | `ITickable` that pushes a value into `IHudValues`. | `ITickable` | Features | Yes | `Features/Airship/Fuel/FuelHudPresenter.cs` |
| `*Config` | ScriptableObject of tunables. | asset | Features + `Assets/Settings` | n/a | `Features/Airship/Fuel/FuelConfig.cs` |
| `*Definition` | ScriptableObject describing what a thing is (ability, effect, interaction, menu, fixture). | asset | Features / Core.Domain | n/a | `Core/Interaction/InteractionDefinition.cs` |
| `*Handler` | `IInteractionHandler` chosen by name in an `IA_*` asset. | Singleton `.As<IInteractionHandler>()` | Features | Always | `Features/Airship/Fuel/PourFuelInteractionHandler.cs` |
| `*Locator` | Static helper that finds a fixture on an airship and survives Unity fake-null. | static | Features | Indirectly | `Features/Airship/Fuel/FuelTankLocator.cs` |
| `*AttributeSet` | Wraps GAS attributes for one actor type. | plain class | Features | Yes | `Features/Airship/Fuel/FuelAttributeSet.cs` |
| `*Events` | `readonly struct` events for one feature, published via `IEventPublisher`. | value types | Features | Asserted in tests | `Features/Airship/Fuel/FuelEvents.cs` |
| `*Registry` | Runtime lookup filled by `ActorOrchestrator` or VContainer collection injection. | Singleton | Features / Core | Some | `Core/Interaction/InteractionHandlerRegistry.cs` |
| `*FeatureInstaller` | `FeatureInstaller` subclass: registers the feature, lists prefabs and fixtures. | asset in `Resources/Installers` | Features | `FeatureCompositionTests` | `Features/Airship/Fuel/FuelFeatureInstaller.cs` |
| `Fake*` | Hand-written test double for a domain interface. | test | `Tests/EditMode/Fakes` | is the test | `Tests/EditMode/Fakes/FakeFuelTank.cs` |
| `I*View` | The contract a use case depends on so it can be faked. | interface | Features / Core | n/a | `Core/Humanoid/IHumanoidCharacterView.cs` |

## Where does X live

| X | Look at |
|---|---|
| A tunable number | The `*Config.asset` in `Assets/Settings/`; the fields are declared in the matching `*Config.cs`. |
| "Press E on a thing" | Target: a `NetworkBehaviour : IInteractionTarget` (`MotorFillPortNetworkMediator.cs`). Behaviour: an `IInteractionHandler`. Link: `Assets/Interactions/IA_*.asset`. Chain: the Interact input bit, `InteractInputUseCase` (server; acquires with `Assets/Targeting/TD_Interact`: a level 3 m ray, first hit, stopped by solid non-targets), `InteractionOrchestrator`, handler. The prompt is `InteractorControllerView.CurrentTarget` (same query). A target needs a collider the ray can hit (a trigger volume at eye height works). |
| An ability, effect or tag | Assets under `Assets/Abilities/`; runtime in `Core/Gas/AbilitySystemUseCase.cs`; per-actor state in `Network/Infrastructure/Abilities/AbilityNetworkMediator.cs`. Tags resolve by name through `IGameplayTagRegistry`, built from `Assets/Abilities/GameplayTagDatabase.asset`, which lists every tag asset automatically. |
| What an actor aims at (targeting) | `Core/Targeting/` (`ITargetingService.TryAcquire`, `TargetingDefinition` assets in `Assets/Targeting/`); contracts in `Core/Domain/Targeting/`. Make something targetable by implementing `ITargetable` on a component in a spawned hierarchy. **See what it sees:** the player's interaction gizmo draws `TD_Interact` live (ray, source, target; green hit, red miss); **TinCan > Dev > Targeting > Draw All Queries** draws every query (repair scans, server included) with `TargetingGizmos`. The switch is a flag file in the main project's `Library/` shared with MPPM clones (EditorPrefs are not shared). Scene view, or Game view with Gizmos on. |
| An item (id, held visual, granted abilities) | `Core/Items/ItemDefinition.cs` (`ITEM_*` assets, positive unique `Id`), looked up through `ItemCatalog`. |
| An input that triggers an ability | `Assets/Abilities/Inputs/Input_*.asset` bound in `DefaultInputBindingConfig.asset`; becomes a bit in `HumanoidInputState.ActiveInputMask` so it is predicted and replayed. |
| Starting abilities | A feature's: its installer, as `ActorAbilityGrant`s (`Core/Gas/ActorAbilityGrant.cs`), granted by `ActorAbilityGrantUseCase`. Core only (sprint): `_startingAbilities` on `NetworkPlayer.prefab` (`HumanoidPlayer`) and `Airship_Prefab.prefab` (`AirshipNetworkMediator`). Abilities that come from a held item (for example `GA_SwingNet` from the net) belong on the `ITEM_*` asset. |
| A HUD number | `IHudValues` (`Core/Domain/Hud/IHudValues.cs`) written by a `*Presenter`; rendered by `App/Views/HudOverlayView.cs`. |
| A menu or menu row | `Assets/UI/Menus/*.asset`; commands in `Core/UI/Commands/`; see `UI_FRAMEWORK.md`. |
| Something bolted onto the ship | A fixture prefab + `ShipFixtureDefinition` in `Assets/Settings/Fixtures/`, listed by an installer; spawned by `Core/Ship/Fixtures/ShipFixtureSpawningUseCase.cs`. |
| The fixed tick order | `Network/Infrastructure/NetworkSimulationScheduler.cs`, `SimulateNetworkTick`. Features hook in with `ISimulationTickable` + `SimulationPhase`. |
| Player movement / look | `Core/Humanoid/` (use case, processor, `HumanoidInputState`); mediator `Network/Infrastructure/HumanoidPlayer.cs`. |
| Airship movement | `Core/Ship/AirshipMovementUseCase.cs`, `AirshipMovementProcessor.cs`, `AirshipInputState.cs`; mediator `Network/Infrastructure/AirshipNetworkMediator.cs`. |
| Possession (who drives what) | `Core/Possession/` (`PossessionUseCase` client side, `ServerPossessionManager` authority, `Infrastructure/PossessionNetworkMediator`). |
| RPCs and NetworkVariables | Only inside `*NetworkMediator` classes. |
| Events / logging | `IEventPublisher.Publish` and `LogInfo(source, message)`; observed by `Core/Infrastructure/Events/DebugLogEventObserver.cs`. |
| Command-line session start | `Core/UI/CommandLineSessionBootstrap.cs`: `-autohost`, `-autojoin [address[:port]]`, also as MPPM player tags via `Core/Domain/LaunchArguments.cs`. |
| Simulated latency, input bot, movement telemetry | `DevTools/` ([`NETWORK_TEST_HARNESS.md`](NETWORK_TEST_HARNESS.md)); reports in `Logs/net-telemetry/`. |
| Feature scenarios and the scenes they run in | `DevTools/Scenarios/ScenarioCatalog.cs` (`.InScene(TestScenes.X)`), scenes built by `DevTools/Editor/TestRangeSceneBuilder.cs`; driver `.tools/verify.ps1`; agent procedures `.claude/skills/verify-feature`, `add-scenario`. |
| Scripted input for automation | `Core/Domain/ScriptedInput.cs` (`IScriptedInput.Press/Release/Tap`), merged into `UnityInputService`. |
| The DI wiring | `App/ProjectLifetimeScope.cs` for core services and legacy features; `Assets/Resources/Installers/*.asset` for everything newer. |

## Feature index

Style: **installer** = registered through a `FeatureInstaller` asset (the target pattern). **direct** = registered
in `ProjectLifetimeScope.cs` (legacy; migrate when touched). Add a row when you land a feature.

| Feature | Folder (`Assets/Scripts/Features/`, or `Core/` for core systems) | Style | Entry points | Assets | Tests |
|---|---|---|---|---|---|
| Fuel loop (tank, drain, stall, jerry cans, motor, gauge, HUD) | `Airship/Fuel/` | own assembly + installer | `FuelConsumptionUseCase`, `FuelTankNetworkMediator`, `PourFuelInteractionHandler`, `TakeJerryCanInteractionHandler`, `FuelHudPresenter`, `FuelGaugeView`, `FuelMotorStatusView` | `Resources/Installers/FuelFeatureInstaller`, `Settings/FuelConfig`, `Settings/Fixtures/FuelSystemFixture`, `Prefabs/Airship/Parts/FuelSystem`, `Models/ShipComponents/FuelGauge/fuel_gauge`, `Models/ShipComponents/FuelEquipment`, `Animations/AetherEquipment`, `IA_PourFuel`, `IA_TakeJerryCan`, `Attr_Fuel`, `GA_EngineStall`, `GE_EngineStall`, `State.Engine.Stalled` | `FuelConsumption*Tests`, `FuelFixtureRegistrationTests`, `PourFuelInteractionHandlerTests`, `TakeJerryCanInteractionHandlerTests`, `FuelGaugeViewTests`, `FuelMotorStatusViewTests`, `FuelHudPresenterTests`, `AirshipStallTests` |
| Flying cans + net catch | `Airship/Fuel/Minigame/` | own assembly (references Fuel) + installer (Order 10) | `FlyingCanUseCase`, `NetCatchUseCase`, `TakeNetInteractionHandler`, `Network/Infrastructure/FlyingCanNetworkMediator`, `FlyingCanSpawningService` | `Resources/Installers/FlyingCanFeatureInstaller`, `Settings/FlyingCanConfig`, `Prefabs/Hazards/FlyingJerryCan`, `IA_TakeNet`, `GA_SwingNet`, `GE_NetSwing`, `State.Net.Swinging`, `Input_Primary` | `FlyingCan*Tests`, `CatchProcessorTests`, `NetCatchUseCaseTests`, `NetSwingPredictionTests`, `TakeNetInteractionHandlerTests` |
| Ship damage (breakable parts, hull-breach fuel leak, HUD, repair tool) | `Airship/Damage/` | own assembly (references DesignedEvents) + installer (`Profile_FuelSandbox`) | `ShipRepairUseCase` (server: while a player has `State.Repairing`, applies `GE_RepairTick` every `RepairInterval` to the part `GA_RepairShip`'s `TargetingDefinition` acquires), `ShipBreakageUseCase` (`IShipBreakage`; random breaks on a timer, reconciles each broken part with one `GE_HullBreach` on the ship), `ShipDamagePointNetworkMediator` (a GAS actor per socket: `AbilityNetworkMediator` + `HealthAttributeSet`; its `Marker` has a `ToggleObjectCueHandler` for `Cue.Ship.Part.Broken`), `ShipDamageHudPresenter` | `Resources/Installers/ShipDamageFeatureInstaller`, `Settings/ShipDamageConfig`, `Settings/Fixtures/ShipDamageSocketsFixture`, `Prefabs/Airship/Parts/ShipDamageSockets` (5 sockets), `DamageMarker.mat`, `GE_HullBreach`, `GE_ShipPartBreak`, `GE_ShipPartRestore`, `GE_ShipPartBroken` (`State.Damaged`), `Settings/Fixtures/RepairToolRackFixture` + `Prefabs/Airship/Parts/RepairToolRack` (`ItemRackNetworkMediator`, `IA_TakeRepairTool`), `ITEM_RepairTool` (grants `GA_RepairShip`: hold Primary, `Ability.Repair.Ship`), `GE_RepairTick`, `Attr_FuelLeakRate` (drained by `FuelConsumptionUseCase`), `State.Ship.Damaged`; cues `Abilities/Cues/GCN_Ship_*` + `GCN_Player_Repairing` (listed on the installer), `Cue.Ship.*`, `Cue.Player.Repairing`, `Abilities/Cues/Prefabs/CueFX_*`, `Audio/Cues/*` | `ShipBreakageProcessorTests`, `ShipBreakageUseCaseTests`, `ShipRepairUseCaseTests`, `ShipDamageHudPresenterTests`, leak cases in `FuelConsumptionUseCaseTests`; scenario `ShipDamage`, `RepairLoop`, `ShipDamageLateJoin` |
| Designed events POC (code-built events that coordinate other features; server-local state) | `DesignedEvents/` | own assembly + installer (`Profile_FuelSandbox`, `Profile_Test_ShipDamage`) | `EventCatalog` (collects the events features contribute via `FeatureInstaller.IExtension<EventDefinition>`; ship damage authors `HullStress` in `Airship/Damage/ShipDamageDesignedEvents.cs`), `EventDirectorUseCase` (`IEventDirector`; server tick, one event at a time, auto-start rotation), `EventRunProcessor`, `EventHandlerRegistry`; actions/conditions are data, handled by the owning feature: `AnnounceActionHandler` (HUD `Event` line), ship damage's `BreakShipPartActionHandler` + `BrokenPartsAtMostConditionHandler` (registered in `ShipDamageFeatureInstaller`) | `Resources/Installers/EventsFeatureInstaller` (auto-start delay and quiet gap); plan `.docs/plans/designed-events.md` | `EventDefinitionBuilderTests`, `EventCatalogTests`, `EventRunProcessorTests`, `EventDirectorUseCaseTests`, `EventActionHandlerTests`; scenario `HullStressEvent` |
| Gameplay cues (presentation for gameplay moments: sound, VFX, HUD) | `Abilities/Cues/` (+ contract `Core/Domain/Cues/IGameplayCueHandler`) | installer (Order -19, `Profile_Base`) | `GameplayCueUseCase` (state-cue edges from tags; plays bursts), `GameplayCueDispatcher` (burst routing; RPC on `AbilityNetworkMediator`), `GameplayCuePresenter` (pooled prefabs, sounds, HUD toasts), `GameplayCueNotify` + `Actions/*`, `Handlers/ToggleObjectCueHandler`, `Handlers/AnimatorBoolCueHandler`; features contribute notifies via `IExtension<GameplayCueNotify>` | `Resources/Installers/GameplayCuesFeatureInstaller`, `Abilities/Cues/GCN_*` | `GameplayCue*Tests`, `AbilitySystemCueTests`, `ToggleObjectCueHandlerTests`, `ClientTagStateTests`; scenarios `ShipDamage`, `RepairLoop`, `ShipDamageLateJoin` (cue probes) |
| Targeting (cross-cutting: what an actor aims at) | `Core/Targeting/` (+ contracts in `Core/Domain/Targeting/`) | installer (Order -18, `Profile_Base`); `ActorOrchestrator` registers `ITargetable`s | `TargetingUseCase` (`ITargetingService`), `TargetingProcessor` (shape maths), `TargetableRegistry`, `HumanoidTargeter` | `Resources/Installers/TargetingFeatureInstaller`, `Targeting/TD_RepairScan` (used by `GA_RepairShip`) | `TargetingProcessorTests`, `TargetingUseCaseTests`, `TargetableRegistrationTests`; scenario `RepairLoop` |
| Items + equipment (what a player holds; the net and jerry can are items) | `Core/Items/` (+ `Carry/` for the net rack, `TakeNetInteractionHandler`, `NetSwingVisualView`) | installer (Order -15, `Profile_Base`) | `ItemRackNetworkMediator` + `TakeItemInteractionHandler` (generic rack for any item; new tools need only a rack prefab and an `ITEM_*` asset), `EquipmentNetworkMediator` (on `NetworkPlayer.prefab`, held item id), `EquipmentAbilityBinder` (grants/revokes the held item's abilities and effect on server + owner), `ItemCatalog` | `Resources/Installers/ItemsFeatureInstaller`, `Items/ITEM_JerryCan`, `Items/ITEM_CatchingNet`, `GE_Holding_Net`, `GE_Holding_JerryCan`, `State.Carrying.Net`, `State.Carrying.JerryCan`; items referenced by `FuelConfig.JerryCanItem` and `FlyingCanConfig.NetItem` | `EquipmentAbilityBinderTests`, `ItemCatalogTests`, handler tests; scenario `EquipCycle` |
| Stations (occupy something while staying in your body; generic, the cannon is the first) | `Stations/` | own assembly + installer (`Profile_FuelSandbox`, `Profile_Test_Cannon`) | `StationOccupancyUseCase` (`IStationOccupancy`; server: seats the player, runs the station's occupy ability on them, grants its abilities; Interact again leaves through `IInteractOverride`), `OccupyStationInteractionHandler`, `StationViewPresenter` (the local occupant looks through `IStation.ViewCamera`; also the `ILocalViewOverride` that `PossessedViewCamera` asks first), `IStation` | `Resources/Installers/StationsFeatureInstaller`, `IA_OccupyCannon`, `GA_OccupyCannon` + `GE_Occupying_Cannon` (`State.Occupying.Cannon`, MoveSpeed and JumpForce 0); plan `.docs/plans/cannon-and-hazards.md` | `StationOccupancyTests` |
| Cannon (ship weapon; a station) | `Weapons/Cannon/` | own assembly (references Stations) + installer (`Profile_FuelSandbox`, `Profile_Test_Cannon`) | `CannonFireUseCase` (server: aim from the occupant's look, fire on the Primary press through `GA_FireCannon`, sweep each shot's per-tick segment with `ITargetingService.TryAcquireSegment`, apply `GE_CannonballHit`), `CannonShotPresenter` (every peer: barrels, the aiming arc, cosmetic balls), `CannonNetworkMediator`, pure `BallisticArc`, `CannonballProcessor`, `CannonAimProcessor` | `Resources/Installers/CannonFeatureInstaller`, `Settings/Cannon/CannonConfig`, `Settings/Fixtures/CannonStationFixture` (starboard mid deck), `Prefabs/Weapons/CannonStation`, `GA_FireCannon` (`EndsImmediately`), `GE_CannonReload`, `GE_CannonballHit`, `TD_CannonballSweep`; all built by **TinCan > Dev > Cannon > Build Assets** (`DevTools/Editor/CannonAssetBuilder.cs`) | `CannonBallisticsTests`, `CannonFireUseCaseTests`, `TargetingSegmentTests`, `AbilityActivationRulesTests`; scenario `CannonShot` |
| Sky hazards (targets with health; drift and ship damage come in S2) | `SkyHazards/` | own assembly + installer (`Profile_FuelSandbox`, `Profile_Test_Cannon`) | `SkyHazardUseCase` (`ISkyHazards`; server: removes shot-down hazards, keeps a field on the ship's starboard side), `SkyHazardFieldProcessor`, `SkyHazardSpawningService` (`ISkyHazardSpawner`), `SkyHazardNetworkMediator` (`AbilityNetworkMediator` + `HealthAttributeSet`, `ITargetable`) | `Resources/Installers/SkyHazardsFeatureInstaller`, `Settings/SkyHazards/SkyHazardConfig`, `Prefabs/SkyHazards/SkyHazard` | `SkyHazardTests`; scenario `CannonShot` |
| Menus + HUD framework | `Core/UI/` (+ views in `App/Views/`) | installer (Order -10) | `MenuUseCase`, `HudUseCase`, `MainMenuBootstrap`, `CommandLineSessionBootstrap`, `Commands/*` | `Resources/Installers/UiFeatureInstaller`, `UI/Menus/Menu_Main`, `Menu_Join`, overlays on `GameLifetimeScope.prefab` | `Menu*Tests`, `HudUseCaseTests`, `MainMenuBootstrapTests`, `CommandLineSessionBootstrapTests` |
| Ship fixtures (generic spawner) | `Airship/Fixtures/` | direct (core) | `ShipFixtureSpawningUseCase` | any `Settings/Fixtures/*` | `ShipFixtureSpawningUseCaseTests`, `FeatureCompositionTests` |
| Airship movement | `Core/Ship/` | core `TinCan.Ship` (direct) | `AirshipMovementUseCase`, `AirshipControllerView`, `Network/Infrastructure/AirshipNetworkMediator` | `Prefabs/Airship/Airship_Prefab`, `Attr_FlightSpeed`, `GA_FlightSpeed_Boost`, `IA_AcquireAirshipControl`, `IA_ToggleFlightBoost` | `AirshipMovementProcessorTests` |
| Airship door | `Airship/PhysicalParts/` | own assembly + installer (`Profile_Base`, `Profile_Test_Core`) | `AirshipDoor`, `DoorInteractionHandler` (`Resources/Installers/AirshipDoorFeatureInstaller`) | `IA_ToggleDoor`, `Interaction.Toggle.Door` | none |
| Humanoid movement + look | `Core/Humanoid/` | core `TinCan.Humanoid` (direct) | `HumanoidMovementUseCase`, `PlayerLookUseCase`, `HumanoidControllerView`, `Network/Infrastructure/HumanoidPlayer` | `Prefabs/NetworkPlayer`, `Attr_MoveSpeed`, `Attr_JumpForce`, `Attr_Stamina`, `GA_Sprint`, `GE_SprintBuff` | `HumanoidMovement*Tests`, `SimulationUseCaseTests` |
| Possession | `Core/Possession/` | core `TinCan.Possession` (direct) | `PossessionUseCase`, `ServerPossessionManager`, `PossessionInputController`, `Infrastructure/PossessionNetworkMediator` | `Prefabs/Singletons/PossessionMediator` | none |
| Interaction system | `Core/Interaction/` | core `TinCan.Interaction` (direct); targeting path via installer (`InteractionFeatureInstaller`, `Profile_Base`) | `InteractInputUseCase` (the only interaction path), `InteractionOrchestrator`, `InteractionHandlerRegistry`, `VehicleBoardingUseCase` | `Assets/Interactions/*` | `AirshipInteractionInvestigationTests`, `InteractInputUseCaseTests`; scenario `InteractRack` |
| Abilities (GAS-like) | `Core/Gas/` | core `TinCan.Gas` (direct); tag registry via installer (Order -20, `Profile_Base`) | `AbilitySystemUseCase`, `Network/Infrastructure/Abilities/AbilityNetworkMediator`, `GameplayTagRegistry` | `Assets/Abilities/**`, `Abilities/GameplayTagDatabase`, `Resources/Installers/GameplayTagsFeatureInstaller` | `HealthAttributeSetTests`, `InstantGameplayEffectTests`, `GameplayTagRegistryTests`, `GasTickTimingTests` |
| Ship modules (build mode removed 2026-09-27, `plans/trust-fixes.md`) | `Network/Infrastructure/ModuleSpawningService.cs`, `ShipModule*NetworkMediator.cs` | direct | `ModuleSpawningService` (also spawns fixtures) | `Prefabs/Modules/Cannon_Module`, `IA_RepairModule`, `IA_DamageModule` | none |
| Cloud boundary + visuals + atmosphere | `CloudBoundary/` | own assembly + installers: `CloudBoundaryFeatureInstaller` (boundary, `BeforeHumanoid` tick, cloud configs, `CloudEnvironmentView`) and `CloudSubmersionFeatureInstaller` (atmosphere) | `CloudBoundaryUseCase`, `CloudEnvironmentView` (also applies `CloudSubmersionProcessor` output: fog + directional light dimming from the local camera's cloud submersion, client-only, no gameplay effect) | `Settings/CloudBoundaryConfig`, `Settings/CloudVisualProfile`, `Settings/CloudSubmersionConfig`, `Resources/Installers/CloudBoundaryFeatureInstaller`, `Resources/Installers/CloudSubmersionFeatureInstaller` | `CloudBoundary*Tests`, `CloudSubmersionProcessorTests` |
| Gas pocket challenge | `GasChallenge/` | own assembly + installer (`Profile_Base`, `Profile_Test_Core`) | `GasChallengeUseCase` (server, `AfterAirship` tick), `PhysicsGasPocketQuery`, `GasPocketVolume` | `Resources/Installers/GasChallengeFeatureInstaller`, `Prefabs/Hazards/GasPocket`, `GE_GasPocketExplosion`, scene `cvg_gaspocket_test` | `GasChallengeUseCaseTests`, `GasPocketDetonationProcessorTests` |
| Network test harness + feature scenarios (dev only) | `Scripts/DevTools/` (own assembly) | installer | `NetworkConditionsUseCase`, `BotRouteUseCase`, `MovementTelemetryUseCase`, `NetHarnessOverlayView`, `Scenarios/ScenarioUseCase` (+ `ScenarioCatalog`, `*ScenarioLibrary`, menu `Editor/ScenarioMenu`, driver `.tools/verify.ps1`) | `Resources/Installers/NetTestHarnessFeatureInstaller` | `NetTestHarnessTests`, `BotRouteUseCaseTests`, `LaunchArgumentsTests`, `ScenarioRunnerTests` |
| Free camera | `FreeCamera/` | own assembly + installer (`Profile_Base`, `Profile_Test_Core`) | `FreeCameraMovementUseCase`, `FreeCameraTransformView` | `Resources/Installers/FreeCameraFeatureInstaller` | none |
| Environment helpers | `Environment/` | own assembly (no installer) | `MovingPlatform`, `SimpleOscillator` | | `Tests/Shared/FakeMovingGround` |

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

- **A test-range scene only has what its profile loads.** A station, installer or service missing from a scenario usually
  means its installer is not in that area's profile (`Assets/Settings/FeatureProfiles/Test/`). The scope also needs
  `CloudSubmersionFeatureInstaller` for the cloud view it resolves from the scene. Assets loaded in an Editor script
  before `EditorSceneManager.NewScene` are unloaded by it; load them after.

- **`unity cmd capture_scene_view` does not show gizmos or `Debug.DrawLine`**, and a background Editor's Scene view camera
  does not follow `SceneView.LookAt` until it repaints: the capture then renders from the old camera pose (often the
  origin). Set `sceneView.camera.transform` yourself before capturing, and check debug lines in the live Scene view.
  The capture also saves relative to `Assets/`; move the file out so it does not become an asset.

- **GAS time is ticks, not seconds.** Compare `ITimeService.Tick` values from the same peer only (each peer counts its
  own), and convert authored seconds with `GameplayTicks.FromSeconds`. `ITimeService.Time` is still the server clock
  estimate for everything else. A player has two ability controllers with one `Id` (`HumanoidPlayer` and its
  `AbilityNetworkMediator`, sharing the entity's id); only `HumanoidPlayer` is registered.

- **A networked prefab needs an `EntityNetworkMediator` on its root.** Without it nothing on the object is registered,
  and its actors have no shared id. Actor ids on child objects are derived from the object path, so renaming a child
  changes its id (matters once worlds are saved).

- **Held-state input bits need a key held across a tick.** Input masks sample *held* keys when a tick gathers input
  (`UnityInputService.GetActiveInputMask`); there is no latch. A normal tap (80–150 ms) outlasts a tick; an
  inhumanly short one can be missed. This applies to Interact and the ability inputs alike. Scenarios hold for 0.3 s.

- **`ProjectLifetimeScope.cs` is core only** (its class comment lists what counts as core). Do not add feature
  registrations there; write an installer. The rules suite fails if its registration count grows.
- **Profiles list their features; core always loads.** A feature missing from a scene usually means its installer is
  not in that scene's profile (`Profile_Base`, `Profile_Test_Core`, ...). Core installers (tags, cues, targeting,
  interaction, items, UI) are never listed: they load in every scene. A profile missing a feature that another one's
  services need does not start: the scope throws "Feature services this scene cannot build", listing each
  "installer: service needs type" (`InstallerServiceCheck`). An interaction whose handler isn't registered logs a
  warning from `InteractionOrchestrator`. A crash at spawn from a shared prefab means something there requires a
  feature service; see [Features and shared prefabs](FEATURE_INSTALLERS.md#features-and-shared-prefabs).
- **String-typed links that break silently:** `InteractionDefinition` stores the handler as an assembly-qualified
  type name, so renaming the class or moving it to another assembly blanks the `IA_*` dropdown and makes the
  interaction a no-op. `InteractionDefinitions_ResolveTheirHandler` fails when that happens; re-pick the handler
  (moving `Stations` did exactly this to `IA_OccupyCannon`);
  `MenuDefinition` rows reference commands by `CommandId` string; `IHudValues` keys are strings.
- **"The type X exists in both TinCan.Features and TinCan.Features.<Feature>" in the IDE only** means the generated
  `.csproj` files are stale after an asmdef was added: Unity's own compile is fine. `verify.ps1` regenerates them after
  every compile. By hand: **Edit > Preferences > External Tools > Regenerate project files**, then reload the IDE window.
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
- **Standalone builds are blocked** by a Fantasy Skybox sample terrain that fails to load. (The other blocker,
  Visual Scripting AOT stubs, went with the package; a build hasn't been retried since.) Playtesting is Editor +
  Multiplayer Play Mode for now.
- **Never compare or simulate player motion on a ship in world space.** Host and client see the ship at different
  poses, so use the platform-local helpers in `Core/Humanoid/HumanoidPrediction.cs`
  (`HumanoidAuthoritativeState.FromWorld`) and the yaw frame in `HumanoidMovementUseCase`.
- **The player's mesh is not on the root.** `NetworkPlayer.prefab` draws the body and carried items from the
  `Visual` child, which trails the simulated root by up to one tick (see `HumanoidVisualSmoothingView`). Put new
  player visuals under `Visual`, look them up with `TransformSearch.FindDescendant`, and read the drawn position from
  `IHumanoidVisualAnchor` rather than `transform.position` when placing cameras or effects.
- **Never carry a player with its platform through `CharacterController.Move`.** The capsule sweep collides with the
  ship's own geometry and eats the carry, so players get dragged behind a fast or pitching ship. Use
  `IHumanoidMovementView.Carry` (a rigid transform move), then collide only the player's own motion.
- **Players collide with each other**, and both spawn at the same point. With owner prediction a collision
  against another player's interpolated capsule cannot agree on both sides, and the harness shows it as the main
  source of replays.
- **A NetworkObject prefab saved from a scene object can carry a stale `GlobalObjectIdHash` on disk.** The host then
  holds the correct hash in memory, a clone reads the stale one from the file, and NGO refuses the client
  ("NetworkConfig mismatch"). A scenario only shows this as "no subject player". Mark the loaded prefab asset dirty and save
  (`EditorUtility.SetDirty(prefab)` + `AssetDatabase.SaveAssets()`) so its validated hash is written. Re-saving
  through `LoadPrefabContents` + `SaveAsPrefabAsset` did not always do it. `.tools/verify.ps1` compares the hashes before every
  host + client run.
- **A MonoBehaviour fake in `Tests/EditMode` cannot be added with `AddComponent` if it owns its script file** ("it is
  an editor script"). Put it in a file named after a plain class, as `Fakes/FakeFuelTank.cs` and
  `Fakes/FakeShipDamage.cs` do. Unity then binds the file to the plain class and ignores the behaviour.
- **Running EditMode tests with a modified scene open** makes the test runner show a modal "Scene(s) Have Been
  Modified" dialog. It blocks the Editor, and every `unity cmd` call times out until someone answers it. Save
  (or discard) the scene before `unity cmd run_tests`. `.tools/verify.ps1` checks for dirty scenes and for this
  dialog before running.
- **Edit-mode code must not mark things dirty on enable.** The scene used to turn dirty after every Play session
  because `CloudAtmosphereDirector` (`[ExecuteAlways]`) called `EditorUtility.SetDirty` on the sun light from
  `OnEnable`, which runs after every domain reload and Play exit. `OnValidate` also runs on every domain reload,
  so it now marks a target dirty only when a value actually changed. The director no longer re-applies each frame
  in edit mode.
- **The cloud renderer renders with a runtime copy of `VolumetricClouds.mat`** (the `runtimeMaterial` edit in
  `Assets/ThirdParty/VolumetricCloudsURP/VolumetricClouds/VolumetricCloudsURP.cs`). Before, it wrote wind state into
  the asset every frame, so the `.mat` kept changing in git. Edits to the `.mat` in the Inspector therefore show up
  only after the feature is recreated (toggle it, or reload). The cloud wind also only moves in Play mode, so the
  Scene view shows a still sky. If the file was reverted in git while Unity held a stale copy, force a reimport:
  `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)`.
- **A `unity cmd` call that times out behind a modal can wedge the pipeline** in that Editor: later commands time out
  even after the dialog is gone, and the pipeline port collects `CLOSE_WAIT` sockets. The Editor process itself is
  fine. Try focusing the Editor window first (it may wake the pipeline); restarting the Editor recovers it.
- **`unity recompile` can report `up_to_date` with no errors** when the Editor already failed to compile at startup.
  Confirm the assembly exists in `Library/ScriptAssemblies/`, or search `Editor.log` for `error CS`.
- `Tests/Shared/FakeMovingGround.cs` uses the namespace `TinCan.Tests.EditMode.Fakes` despite living in
  `TinCan.Tests.Shared`.
- The three `*.slnx` files at the root are Editor-generated and gitignored. Do not commit them.
