# Code map

Where things live, what the suffixes mean, and which features exist. Paths are relative to the repo root.

## Assemblies and the one-way rule

```
TinCan.App                     composition root and overlay views (sees everything)
  TinCan.Features.<Feature>    one per feature; sees the core and the features it references
  TinCan.Network               NGO adapters for core actors (HumanoidPlayer, AirshipNetworkMediator, ...)
  TinCan.Core.Infrastructure   core implementations (ActorOrchestrator, registries, input, time)
  core systems, lowest first:
    TinCan.Input               actions, contexts, reader and router, rebinding (the only Input System user)
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
down into `TinCan.Core.Domain`, never sideways; examples are `Core/Domain/Look/` (`ILookView`,
`IHasLook`, `IViewRig`), `IPossessionReceiver`, `Core/Domain/Hud/IHudValues`, and the `IHumanoidActor` / `IShipActor`
markers. **`Core` means core to gameplay, not engine-free:** the core systems are the always-loaded gameplay
foundation and may hold Unity code, views included (for example `Core/Gas/Views/AttributeBarView`). The engine-light,
contract-only centre is `TinCan.Core.Domain`. Folder, namespace and assembly agree: a core system lives in `Assets/Scripts/Core/<System>/`, its namespace
is `TinCan.Core.<System>`, and its assembly is `TinCan.<System>` (`Core/Gas/` is `TinCan.Core.Gas` in `TinCan.Gas`).
Features live in `Assets/Scripts/Features/<Feature>/` as `TinCan.Features.<Feature>`. There is no shared block any more:
every script belongs to a core system, a feature, or the top layer.
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
| `TinCan.RandomStreams` | `Core/RandomStreams/` | `RandomSource` behind `Core.Domain`'s `IRandomSource` (one named stream per system; `-seed <n>` makes them repeatable) and its core installer. | Core.Domain, VContainer |
| `TinCan.Gas` | `Core/Gas/TinCan.Gas.asmdef` | Abilities, effects, attributes, tags, cues (`Cues/`, with `[MovedFrom]` on the cue actions), `ActorAbilityGrantUseCase`, the tag and cue installers. | Core.Domain, Targeting, VContainer |
| `TinCan.Humanoid` | `Core/Humanoid/TinCan.Humanoid.asmdef` | Humanoid movement and prediction, `HumanoidInputState`, `HumanoidAttributeSet`, `HumanoidTargeter`, `LookView` (the player's look; a camera rig places its camera), `ParentLocalSpaceVolume`. | Core.Domain, Gas, NGO, VContainer |
| `TinCan.Interaction` | `Core/Interaction/TinCan.Interaction.asmdef` | Interaction orchestration, handlers, the interaction installer. | Core.Domain, Gas, Humanoid, Possession, Targeting, NGO, VContainer |
| `TinCan.Ship` | `Core/Ship/TinCan.Ship.asmdef` | The airship itself and `Fixtures/` (fixture spawning). Its features (fuel, damage, the door) live under `Features/Airship/`. | Core.Domain, Humanoid, Interaction, NGO, VContainer |
| `TinCan.Input` | `Core/Input/TinCan.Input.asmdef` | Input: `InputConfig`, `InputSystemReader` (`IInputReader`), `InputContextUseCase` (`IInputContexts`), `InputRoutingUseCase`, `InputRebindingUseCase` (`IInputBindings`), the core `InputFeatureInstaller`. The only assembly that references Unity's Input System ([`INPUT.md`](INPUT.md)). | Core.Domain, Possession, UI, Input System, VContainer | Items and equipment; the headless menu/HUD framework. | Items: Core.Domain, Gas, Interaction, NGO, VContainer. UI: Core.Domain, Possession, VContainer |
| `TinCan.Features.<Feature>` | `Assets/Scripts/Features/<Feature>/TinCan.Features.<Feature>.asmdef` | One feature: its processors, use cases, mediators, installer. Today: `GasChallenge`, `SkyHazards`, `Stations`, `Weapons.Cannon` (references Stations), `DesignedEvents`, `Airship.Fuel`, `Airship.Fuel.Minigame` (references Fuel), `Airship.Damage` (references DesignedEvents), `Airship.PhysicalParts` (the door), `CloudBoundary` (with submersion), `Voyage`, `Boarding`, `FreeCamera`, `ThirdPersonCamera`, `FirstPersonCamera`, `Helm` (references Stations), `TargetOutline`, `SkyIslands`, `Environment`, `Events`. | Core.Domain, the core systems it uses, the features it builds on, and only the packages it uses |
| `TinCan.Core.Infrastructure` | `Assets/Scripts/Core/Infrastructure/TinCan.Core.Infrastructure.asmdef` | Core implementations behind `Core.Domain` contracts: `ActorOrchestrator`, `ActorRegistry`, `AbilityRegistry`, `NetworkPrefabInterceptor`, `ProjectTimeService`, `ShipStateProvider`, `Events/`. Unit-testable. | Core.Domain, Entities, Gas, Humanoid, Interaction, NGO, VContainer |
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
| `Assets/Scripts/Core/Domain/` | Core.Domain | `IActor`, `IActorRegistry`, `IPossessable`, `ITimeService`, `SimulationAbstraction.cs` (`SimulationUseCase<TView,TInput>`), `ISimulationTickable.cs` (phases + `SimulationTickRunner`), `IInjectedView`, `IShipModule`. |
| `Core/Domain/Abilities/` (+`Attributes`, `Inputs`, `Tags`) | Core.Domain | GAS contracts: `IAbilityController`, `GameplayTag`, `GameplayAttribute`, `AttributeValue`, `GameplayInput` (an ability input: the actions that press it). |
| `Core/Domain/Events/` | Core.Domain | `IEventPublisher`, `IEventObserver`, `GameEvents`, `LogEvent` (+ `LogInfo` extension). |
| `Core/Domain/Features/` | Core.Domain | `FeatureInstaller`, `FeatureInstallerCatalog`, `FeatureProfile`, `ShipFixtureDefinition`. |
| `Core/Domain/Networking/` | Core.Domain | `INetworkService`, `INetworkPlayerSpawner`, `IModuleSpawningService`. |
| `Assets/Scripts/App/` | App | `ProjectLifetimeScope` (composition root), `Views/` (`MenuOverlayView`, `HudOverlayView`: UI Toolkit, throwaway). |
| `Assets/Scripts/Core/Infrastructure/` | Core.Infrastructure | `NetworkPrefabInterceptor` (injects before NGO spawn), `ActorOrchestrator` (registers spawned hierarchies), `ActorRegistry`, `AbilityRegistry`, `ProjectTimeService`, `ShipStateProvider`, `Events/`, `Extensions/`. |
| `Assets/Scripts/Network/Infrastructure/` | Network | `NetworkSimulationScheduler` (the tick), `NetworkMediator` base, `HumanoidPlayer`, `AirshipNetworkMediator`, `NGONetworkService`, `NetworkPlayerSpawner`, `ModuleSpawningService`, `NgoInteractionTargetResolver`, ship-module mediators, `Abilities/AbilityNetworkMediator`. |
| `Core/Domain/Look/`, `Core/Domain/Hud/` | Core.Domain | Contracts shared by core systems: `ILookView`, `IHasLook`, `IViewRig` + `ViewRigSelection` (camera rigs: the first registered wins); `IHudValues`. Also `IPossessionReceiver` and the `IHumanoidActor` / `IShipActor` markers (`ActorKinds.cs`) in `Core/Domain/`. |
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
| `Assets/Abilities/Inputs/` | TinCan > Abilities > Inputs > *X* | `Input_*` ability inputs (their actions and bit order: `Assets/Input/InputConfig`). |
| `Assets/UI/Menus/` | TinCan > UI > Menu Definition | `Menu_Main`, `Menu_Join`, `Menu_Controls` (key bindings). |
| `Assets/Prefabs/Singletons/` | | `GameLifetimeScope` (the scope + UI overlays), `NetworkService`, `PossessionMediator`. |
| `Assets/Prefabs/` | | `NetworkPlayer.prefab` (player, camera, carry visuals). |
| `Assets/Prefabs/Airship/` | | `Airship_Prefab.prefab`; `Parts/FuelSystem.prefab`, `Parts/HelmStation.prefab` (fixtures spawned onto the ship). |
| `Assets/Prefabs/Hazards/`, `Modules/` | | `FlyingJerryCan`, `GasPocket`; `Cannon_Module` (a ship module; nothing spawns it since build mode was removed). |
| `Assets/Models/<Group>/<Asset>/` | | Imported art (FBX + textures + the shipped `manifest.json`/`INTEGRATION.md`), one folder per asset: `ShipComponents/FuelGauge/fuel_gauge.fbx`. Raw models never go in scenes directly; wrap them in a prefab under `Assets/Prefabs/`. |
| `Assets/Scenes/` | | `drm_cloud_environment` (main, build index 0), `cvg_airship_default`, `cheesed_scene`, `cvg_gaspocket_test`. Scenes are nearly empty on purpose; gameplay spawns at runtime. |
| `Assets/Scenes/Test/`, `Assets/Prefabs/Test/`, `Assets/Settings/FeatureProfiles/Test/` | TinCan > Dev > Test Range > Rebuild Scenes | The test range scenario runs use: generated area scenes (`Test_Core`, `Test_ShipDamage`, `Test_NetCatch`, `Test_Cannon`), the bare `TestShip_Prefab`, and one feature profile per area. Do not edit the scenes by hand; see [`NETWORK_TEST_HARNESS.md`](NETWORK_TEST_HARNESS.md#test-range). |

### Tooling (outside Assets)

| Folder | Holds |
|---|---|
| `.tools/` | Entry scripts: `setup.cmd`/`setup.ps1`, `upgrade-unity.ps1`, `verify.ps1` (compile, tests, scenarios), `build.ps1` (player builds, server image), `perf.ps1` (perf runs and budgets). See [`.tools/README.md`](../.tools/README.md). |
| `.tools/modules/` | The scripts' shared PowerShell modules (`TinCan.Common`, `Editor`, `Mppm`, `Verify`, `Build`, `Container`, `Perf`). Rules for module code: `.tools/README.md`, "Modules". |
| `.docs/perf/` | `budgets.json`: perf budgets per scenario, role, environment profile and metric, written by `.tools/perf.ps1 set-budgets`. See [`PERFORMANCE.md`](PERFORMANCE.md). |
| `Container/server/` | `Containerfile`, `Containerfile.containerignore` and `compose.yaml` for the dedicated server image (Podman). |
| `Builds/` (git-ignored) | Player builds: `LinuxServer/`, `Win64/`. |

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
| A key, and who hears it | [`INPUT.md`](INPUT.md) and the generated [`INPUT_MAP.md`](INPUT_MAP.md): keys in `Assets/Input/TinCanControls.inputactions`, contexts in `Assets/Input/Contexts/`, the runtime in `Core/Input/`. |
| Starting abilities | A feature's: its installer, as `ActorAbilityGrant`s (`Core/Gas/ActorAbilityGrant.cs`), granted by `ActorAbilityGrantUseCase`. Core only (sprint): `_startingAbilities` on `NetworkPlayer.prefab` (`HumanoidPlayer`) and `Airship_Prefab.prefab` (`AirshipNetworkMediator`). Abilities that come from a held item (for example `GA_SwingNet` from the net) belong on the `ITEM_*` asset. |
| A HUD number | `IHudValues` (`Core/Domain/Hud/IHudValues.cs`) written by a `*Presenter`; rendered by `App/Views/HudOverlayView.cs`. |
| A bar over something showing a GAS attribute (health while repairing, later stamina, shields, reload) | `Core/Gas/Views/AttributeBarView.cs` on `Assets/Prefabs/UI/AttributeBar.prefab`: drop the prefab under the actor and pick the attribute and its max attribute (or a fixed max). Hidden while full unless told otherwise; faces the local view camera. First use: one bar per damage point in `ShipDamageSockets.prefab`. |
| A menu or menu row | `Assets/UI/Menus/*.asset`; commands in `Core/UI/Commands/`; see `UI_FRAMEWORK.md`. |
| Where players board a ship (joiners, the cloud reset) | `Core/Ship/AirshipBoardingPose.cs`: the ship's `AirshipRespawnPoint` child if it has one, else the caller's ship-local offset (`BoardingConfig.BoardingOffset`, `CloudBoundaryConfig.FallbackRespawnOffset`; both (0, 3, 0)). No ship prefab has a respawn point yet. |
| Who counts as crew | `Core/Domain/CrewQueries.cs`: player characters (`IHumanoidActor.IsPlayerCharacter`, backed by `NetworkObject.IsPlayerObject` in `HumanoidPlayer`). Used by the voyage's crew gate and the sky-hazard field size. |
| Something bolted onto the ship | A fixture prefab + `ShipFixtureDefinition` in `Assets/Settings/Fixtures/`, listed by an installer; spawned by `Core/Ship/Fixtures/ShipFixtureSpawningUseCase.cs`. |
| The fixed tick order | `Network/Infrastructure/NetworkSimulationScheduler.cs`, `SimulateNetworkTick`. Features hook in with `ISimulationTickable` + `SimulationPhase`. |
| Player movement / look | `Core/Humanoid/` (use case, processor, `HumanoidInputState`); mediator `Network/Infrastructure/HumanoidPlayer.cs`. |
| Airship movement | `Core/Ship/AirshipMovementUseCase.cs` (server; input from the pilot seam `IAirshipPilotInput.cs`), `AirshipMovementProcessor.cs`, `AirshipInputState.cs`; mediator `Network/Infrastructure/AirshipNetworkMediator.cs`. Who steers: the helm, `Features/Helm/HelmSteeringUseCase.cs`. What pushes it out of something solid (it is kinematic, physics never stops it): `IAirshipCollisionResponse.cs`, implemented by the movement use case; sky islands use it. |
| A ship design, its file format, and saved ships | `Features/ShipDesigns/` (model `ShipDesign.cs`, file format `ShipDesignFormat.cs` + `ShipDesignJsonCodec.cs`, migrations `ShipDesignMigrator.cs`); parts are `ShipPartDefinition` assets (`Core/Ship/Parts/`, `Settings/ShipParts/PART_*`) contributed by installers. Saved designs: `Application.persistentDataPath/Ships/*.ship.json`; built-in ones: `Settings/ShipDesigns/`. A format change raises `ShipDesignFormat.CurrentVersion`, adds a migration and commits a sample of the old format under `Tests/EditMode/ShipDesigns/`. |
| Where fittings go on a designed ship (sockets) | Parts carry `ShipSocket` markers in their prefab (`Core/Ship/Sockets/`); a built ship's sockets come from `IShipSockets` (ShipDesigns). Mounting is the ShipSockets feature: a feature offers a fitting by listing a `ShipFittingDefinition` (`FITTING_*`) through `IExtension<ShipFittingDefinition>` on its installer; any fitting fits any socket for now. What is mounted is live ship state, not saved in the design. |
| Possession (your body or the free camera; never the ship) | `Core/Possession/` (`PossessionUseCase` client side, `ServerPossessionManager` authority, `Infrastructure/PossessionNetworkMediator`). |
| RPCs and NetworkVariables | Only inside `*NetworkMediator` classes. |
| Events / logging | `IEventPublisher.Publish` and `LogInfo(source, message)`; observed by `Core/Infrastructure/Events/DebugLogEventObserver.cs`. |
| Command-line session start | `Core/UI/CommandLineSessionBootstrap.cs`: `-autohost`, `-server [address][:port]` (dedicated, default `0.0.0.0:7777`), `-autojoin [address[:port]]`, also as MPPM player tags via `Core/Domain/LaunchArguments.cs`. |
| Simulated latency, input bot, movement telemetry | `DevTools/` ([`NETWORK_TEST_HARNESS.md`](NETWORK_TEST_HARNESS.md)); reports in `Logs/net-telemetry/`. |
| Random numbers (and seeding them) | `IRandomSource.Create("<System>")` (`Core/Domain/IRandomSource.cs`), one stream per system; with `-seed <n>` every stream repeats, which perf runs rely on, so gameplay randomness should come from it (`Core/RandomStreams/`). |
| The current voyage's world layout (a seed per voyage, its start and destination) | `ISessionLayout` (`Core/Domain/ISessionLayout.cs`), an actor every peer can find: the voyage state implements it (`VoyageNetworkMediator`; the seed is rolled on Begin from the `IRandomSource` stream "Voyage"). Seed 0 means no voyage has begun. Sky islands build from it. |
| Things that must not be placed inside rock (obstacles) | `IWorldObstacleQuery` (`Core/Domain/IWorldObstacleQuery.cs`): ask every registered query before using a spot; none registered, every spot is free. Sky islands implement it (`PhysicsSkyIslandObstacleQuery`); sky hazards ask it before a field spawn. |
| Client time lead (input latency) | The server reports each owner's input queue depth on the movement snapshot (`HumanoidInputBuffer.SmoothedDepth`); the owner steers NGO's `LocalBufferSec` with `Core/Humanoid/InputLeadProcessor.cs` (`HumanoidPlayer.SteerInputLead`). Why: [`plans/input-queue-lead.md`](plans/input-queue-lead.md). |
| Feature scenarios and the scenes they run in | `DevTools/Scenarios/ScenarioCatalog.cs` (`.InScene(TestScenes.X)`), scenes built by `DevTools/Editor/TestRangeSceneBuilder.cs`; driver `.tools/verify.ps1`; agent procedures `.claude/skills/verify-feature`, `add-scenario`. |
| Perf runs and budgets | Sampler `DevTools/Perf/PerfSampler.cs` (`-perf`, registered by `NetTestHarnessFeatureInstaller`), report `PerfReport.cs`; runner `.tools/perf.ps1` + `modules/TinCan.Perf.psm1`; budgets `.docs/perf/budgets.json`; guide [`PERFORMANCE.md`](PERFORMANCE.md). |
| Scripted input for automation | `DevTools/ScriptedAction.cs` intents (`MoveForward`, `GunnerFire`, ...) mapped to context actions by `ScriptedActionMap`, pressed through `Core/Domain/Input/ScriptedInput.cs`, which `InputSystemReader` merges under the same context rules as keys. |
| The DI wiring | `App/ProjectLifetimeScope.cs` for core services and legacy features; `Assets/Resources/Installers/*.asset` for everything newer. |

## Feature index

Style: **installer** = registered through a `FeatureInstaller` asset (the target pattern). **direct** = registered
in `ProjectLifetimeScope.cs` (legacy; migrate when touched). Add a row when you land a feature.

| Feature | Folder (`Assets/Scripts/Features/`, or `Core/` for core systems) | Style | Entry points | Assets | Tests |
|---|---|---|---|---|---|
| Fuel loop (tank, drain, stall, jerry cans, motor, gauge, HUD) | `Airship/Fuel/` | own assembly + installer | `FuelConsumptionUseCase`, `FuelTankNetworkMediator`, `PourFuelInteractionHandler`, `TakeJerryCanInteractionHandler`, `FuelHudPresenter`, `FuelGaugeView`, `FuelMotorStatusView` | `Resources/Installers/FuelFeatureInstaller`, `Settings/FuelConfig`, `Settings/Fixtures/FuelSystemFixture`, `Prefabs/Airship/Parts/FuelSystem`, `Models/ShipComponents/FuelGauge/fuel_gauge`, `Models/ShipComponents/FuelEquipment`, `Animations/AetherEquipment`, `IA_PourFuel`, `IA_TakeJerryCan`, `Attr_Fuel`, `GA_EngineStall`, `GE_EngineStall`, `State.Engine.Stalled` | `FuelConsumption*Tests`, `FuelFixtureRegistrationTests`, `PourFuelInteractionHandlerTests`, `TakeJerryCanInteractionHandlerTests`, `FuelGaugeViewTests`, `FuelMotorStatusViewTests`, `FuelHudPresenterTests`, `AirshipStallTests` |
| Flying cans + net catch | `Airship/Fuel/Minigame/` | own assembly (references Fuel) + installer (Order 10) | `FlyingCanUseCase`, `NetCatchUseCase`, `TakeNetInteractionHandler`, `Network/Infrastructure/FlyingCanNetworkMediator`, `FlyingCanSpawningService` | `Resources/Installers/FlyingCanFeatureInstaller`, `Settings/FlyingCanConfig`, `Prefabs/Hazards/FlyingJerryCan`, `IA_TakeNet`, `GA_SwingNet`, `GE_NetSwing`, `State.Net.Swinging`, `Input_Primary` | `FlyingCan*Tests`, `CatchProcessorTests`, `NetCatchUseCaseTests`, `NetSwingPredictionTests`, `TakeNetInteractionHandlerTests` |
| Ship damage (breakable parts, hull-breach fuel leak, HUD, repair tool) | `Airship/Damage/` | own assembly (references DesignedEvents) + installer (`Profile_FuelSandbox`) | `ShipRepairUseCase` (server: while a player has `State.Repairing`, applies `GE_RepairTick` every `RepairInterval` to the part `GA_RepairShip`'s `TargetingDefinition` acquires; on the local player's peer, `AimedTarget` is that part whenever the tool is held), `RepairAimHighlightPresenter` (local player: brightens the aimed part's marker before the trigger is pulled), `ShipBreakageUseCase` (`IShipBreakage`; random breaks on a timer, reconciles each broken part with one `GE_HullBreach` on the ship), `ShipDamagePointNetworkMediator` (a GAS actor per socket: `AbilityNetworkMediator` + a registered `HealthAttributeSet`; broken = `Controller.IsDamaged()` (`Core/Gas/HealthQueries`); its `Marker` has a `ToggleObjectCueHandler` for `Cue.Ship.Part.Broken`), `ShipDamageHudPresenter`, `ShipHealthHudPresenter` (the ship's health as the HUD meter "Hull", every peer) | `Resources/Installers/ShipDamageFeatureInstaller`, `Settings/ShipDamageConfig`, `Settings/Fixtures/ShipDamageSocketsFixture`, `Prefabs/Airship/Parts/ShipDamageSockets` (5 sockets), `DamageMarker.mat`, `GE_HullBreach`, `GE_ShipPartBreak`, `GE_ShipPartRestore`, `GE_ShipPartBroken` (`State.Damaged`), `Settings/Fixtures/RepairToolRackFixture` + `Prefabs/Airship/Parts/RepairToolRack` (`ItemRackNetworkMediator`, `IA_TakeRepairTool`), `ITEM_RepairTool` (grants `GA_RepairShip`: hold Primary, `Ability.Repair.Ship`), `GE_RepairTick`, `Attr_FuelLeakRate` (drained by `FuelConsumptionUseCase`), `State.Ship.Damaged`; cues `Abilities/Cues/GCN_Ship_*` + `GCN_Player_Repairing` (listed on the installer), `Cue.Ship.*`, `Cue.Player.Repairing`, `Abilities/Cues/Prefabs/CueFX_*`, `Audio/Cues/*` | `ShipBreakageProcessorTests`, `ShipBreakageUseCaseTests`, `ShipRepairUseCaseTests`, `ShipDamageHudPresenterTests`, `ShipHealthHudPresenterTests`, leak cases in `FuelConsumptionUseCaseTests`; scenario `ShipDamage`, `RepairLoop`, `ShipDamageLateJoin` |
| Designed events POC (code-built events that coordinate other features; server-local state) | `DesignedEvents/` | own assembly + installer (`Profile_FuelSandbox`, `Profile_Test_ShipDamage`) | `EventCatalog` (collects the events features contribute via `FeatureInstaller.IExtension<EventDefinition>`; ship damage authors `HullStress` in `Airship/Damage/ShipDamageDesignedEvents.cs`), `EventDirectorUseCase` (`IEventDirector`; server tick, one event at a time, auto-start rotation), `EventRunProcessor`, `EventHandlerRegistry`; actions/conditions are data, handled by the owning feature: `AnnounceActionHandler` (HUD `Event` line), ship damage's `BreakShipPartActionHandler` + `BrokenPartsAtMostConditionHandler` (registered in `ShipDamageFeatureInstaller`) | `Resources/Installers/EventsFeatureInstaller` (auto-start delay and quiet gap); plan `.docs/plans/designed-events.md` | `EventDefinitionBuilderTests`, `EventCatalogTests`, `EventRunProcessorTests`, `EventDirectorUseCaseTests`, `EventActionHandlerTests`; scenario `HullStressEvent` |
| Gameplay cues (presentation for gameplay moments: sound, VFX, HUD) | `Abilities/Cues/` (+ contract `Core/Domain/Cues/IGameplayCueHandler`) | installer (Order -19, `Profile_Base`) | `GameplayCueUseCase` (state-cue edges from tags; plays bursts), `GameplayCueDispatcher` (burst routing; RPC on `AbilityNetworkMediator`), `GameplayCuePresenter` (pooled prefabs, sounds, HUD toasts), `GameplayCueNotify` + `Actions/*`, `Handlers/ToggleObjectCueHandler`, `Handlers/AnimatorBoolCueHandler`; features contribute notifies via `IExtension<GameplayCueNotify>` | `Resources/Installers/GameplayCuesFeatureInstaller`, `Abilities/Cues/GCN_*` | `GameplayCue*Tests`, `AbilitySystemCueTests`, `ToggleObjectCueHandlerTests`, `ClientTagStateTests`; scenarios `ShipDamage`, `RepairLoop`, `ShipDamageLateJoin` (cue probes) |
| Targeting (cross-cutting: what an actor aims at) | `Core/Targeting/` (+ contracts in `Core/Domain/Targeting/`) | installer (Order -18, `Profile_Base`); `ActorOrchestrator` registers `ITargetable`s | `TargetingUseCase` (`ITargetingService`), `TargetingProcessor` (shape maths), `TargetableRegistry`, `HumanoidTargeter` (aims along the replicated look) | `Resources/Installers/TargetingFeatureInstaller`, `Targeting/TD_RepairScan` (used by `GA_RepairShip`), `Targeting/TD_Interact` (the `Look` shape) | `TargetingProcessorTests`, `TargetingUseCaseTests`, `TargetingLookTests`, `HumanoidTargeterTests`, `TargetableRegistrationTests`; scenario `RepairLoop` |
| Items + equipment (what a player holds; the net and jerry can are items) | `Core/Items/` (the net rack, `TakeNetInteractionHandler` and `NetSwingVisualView` live in the minigame, `Features/Airship/Fuel/Minigame/`) | installer (Order -15, `Profile_Base`) | `ItemRackNetworkMediator` + `TakeItemInteractionHandler` (generic rack for any item; new tools need only a rack prefab and an `ITEM_*` asset), `EquipmentNetworkMediator` (on `NetworkPlayer.prefab`, held item id), `EquipmentAbilityBinder` (grants/revokes the held item's abilities and effect on server + owner), `ItemCatalog` | `Resources/Installers/ItemsFeatureInstaller`, `Items/ITEM_JerryCan`, `Items/ITEM_CatchingNet`, `GE_Holding_Net`, `GE_Holding_JerryCan`, `State.Carrying.Net`, `State.Carrying.JerryCan`; items referenced by `FuelConfig.JerryCanItem` and `FlyingCanConfig.NetItem` | `EquipmentAbilityBinderTests`, `ItemCatalogTests`, handler tests; scenario `EquipCycle` |
| Stations (occupy something while staying in your body; generic: the cannon and the helm) | `Stations/` | own assembly + installer (`Profile_FuelSandbox`, `Profile_Test_Cannon`, `Profile_Test_Helm`) | `StationOccupancyUseCase` (`IStationOccupancy`; server: seats the player, runs the station's occupy ability on them, grants its abilities; Interact again leaves through `IInteractOverride`), `OccupyStationInteractionHandler`, `StationViewPresenter` (the local occupant looks through `IStation.ViewCamera`; also the `ILocalViewOverride` that `PossessedViewCamera` asks first), `IStation` | `Resources/Installers/StationsFeatureInstaller`, `IA_OccupyCannon`, `GA_OccupyCannon` + `GE_Occupying_Cannon` (`State.Occupying.Cannon`, MoveSpeed and JumpForce 0); plan `.docs/plans/cannon-and-hazards.md` | `StationOccupancyTests` |
| Cannon (ship weapon; a station) | `Weapons/Cannon/` | own assembly (references Stations) + installer (`Profile_FuelSandbox`, `Profile_Test_Cannon`) | `CannonFireUseCase` (server: aim from the occupant's station aim, fire on the Primary bit (the Gunner context's Fire) through `GA_FireCannon`, sweep each shot's per-tick segment with `ITargetingService.TryAcquireSegment`, apply `GE_CannonballHit`), `GunnerAimUseCase` (the gunner's peer: the Gunner input context's Aim steers the barrel within its limits and travels as `HumanoidInputState.StationAim`), `CannonShotPresenter` (every peer: barrels, the aiming arc, cosmetic balls), `CannonNetworkMediator`, pure `BallisticArc`, `CannonballProcessor`, `CannonAimProcessor` | `Resources/Installers/CannonFeatureInstaller`, `Settings/Cannon/CannonConfig`, `Settings/Fixtures/CannonStationFixture` + `CannonStationPortFixture` (a pair on the foredeck, each swinging past the bow: `YawLimit` 105), `Prefabs/Weapons/CannonStation`, `GA_FireCannon` (`EndsImmediately`), `GE_CannonReload`, `GE_CannonballHit`, `TD_CannonballSweep`; all built by **TinCan > Dev > Cannon > Build Assets** (`DevTools/Editor/CannonAssetBuilder.cs`) | `CannonBallisticsTests`, `CannonFireUseCaseTests`, `GunnerAimUseCaseTests`, `TargetingSegmentTests`, `AbilityActivationRulesTests`; scenario `CannonShot` |
| Sky hazards (targets with health that drift at the ship and hurt its health on contact) | `SkyHazards/` | own assembly + installer (`Profile_FuelSandbox`, `Profile_Test_Cannon`) | `SkyHazardUseCase` (`ISkyHazards`; server: removes shot-down hazards, keeps a field ahead of the bow, sized for the crew (`ForCrew`: `MaxAlivePerExtraPlayer`, `SpawnIntervalScalePerExtraPlayer`), moves drifting hazards with `HazardDriftProcessor`, and on contact (`IShipContactQuery`, physics overlap against the ship's colliders) applies `SkyHazardConfig.ImpactEffect` to the ship's controller, then despawns the hazard; `SpawnAt` targets hang still unless `drifts`; field spawns skip spots an `IWorldObstacleQuery` blocks, such as island rock), `SkyHazardFieldProcessor`, `SkyHazardSpawningService` (`ISkyHazardSpawner`), `SkyHazardNetworkMediator` (`AbilityNetworkMediator` + `HealthAttributeSet`, `ITargetable`) | `Resources/Installers/SkyHazardsFeatureInstaller`, `Settings/SkyHazards/SkyHazardConfig`, `Prefabs/SkyHazards/SkyHazard` (+ `NetworkTransformMediator`), `GE_HazardImpact` (built by **TinCan > Dev > Cannon > Build Assets**) | `SkyHazardTests`; scenarios `CannonShot`, `HazardStrike` |
| Voyage (the play session: to a destination and back to Restart) | `Voyage/` | own assembly + installer (`Profile_FuelSandbox`, `Profile_Test_Voyage`); paces the others only through `ISessionParticipant` (`Core/Domain`), which `SkyHazardUseCase`, `ShipBreakageUseCase` and `FuelConsumptionUseCase` implement | `VoyageUseCase` (`IVoyage`; server `AfterAirship`: Begin resets the participants and sets a destination ahead, briefing, cast off, Arrived when there, Lost when the ship's health is depleted, Restart on request; starts by itself only with `VoyageConfig.MinCrew` players aboard and stands down to Idle whenever nobody is, `CrewQueries`), `VoyageRouteProcessor`, `VoyageNetworkMediator` (`IVoyageState` on the VoyageState ship fixture: replicated phase, destination, countdown and the session layout, `ISessionLayout`: a layout seed and origin rolled on Begin; Restart RPC), `VoyageHudPresenter` (meter + line), `VoyageEndScreenPresenter`, `VoyageBeaconPresenter`, `RestartVoyageMenuCommand` | `Resources/Installers/VoyageFeatureInstaller`, `Settings/Voyage/VoyageConfig`, `Settings/Fixtures/VoyageStateFixture`, `Prefabs/Voyage/VoyageState`, `UI/Menus/Menu_VoyageArrived`, `Menu_VoyageLost`, `Materials/Voyage/M_VoyageBeacon` (all built by **TinCan > Dev > Voyage > Build Assets**), scene `Test/Test_Voyage` | `VoyageRouteProcessorTests`, `VoyageUseCaseTests`, participant cases in `SkyHazardTests`, `ShipBreakageUseCaseTests`, `FuelConsumptionUseCaseTests`; scenario `VoyageLoop` |
| Sky islands (floating rock around the ship; a new layout every voyage; solid to the ship) | `SkyIslands/` | own assembly + installer (`Profile_FuelSandbox`, `Profile_Test_Voyage`); plan `.docs/plans/sky-islands.md` | `SkyIslandStreamingUseCase` (`ISkyIslands`; every peer, per frame: the islands within `StreamRadius` of the ship's cell, from the `ISessionLayout` seed or `SkyIslandConfig.WorldSeed`, keeping the voyage's start and destination clear; built nearest first, `MaxBuildsPerFrame`), `SkyIslandLayoutProcessor` (cells, integer hashing, the same on every peer: nothing is replicated), `SkyIslandMeshProcessor` (closed procedural mesh, vertex-colour masks; convex collision pieces), `SkyIslandBuilder` (`ISkyIslandBuilder`; pooled objects, `SkyIslandBody`, convex `MeshCollider` pieces, visuals only with graphics), `SkyIslandImpactUseCase` (server `AfterAirship`: `ISkyIslandContactQuery` (`PhysicsSkyIslandContactQuery`) finds the ship in rock, pushes it out through `IAirshipCollisionResponse`, applies `ImpactEffect` once per `ImpactCooldown`, `SkyIslandHitShipEvent`; no query at all unless the builder knows an island within reach), `PhysicsSkyIslandObstacleQuery` (`IWorldObstacleQuery`: hazards never spawn in rock) | `Resources/Installers/SkyIslandsFeatureInstaller`, `Settings/SkyIslands/SkyIslandConfig`, `Materials/SkyIslands/M_SkyIsland`, `Shaders/SkyIsland.shader`, `GE_IslandImpact` (all built by **TinCan > Dev > Sky Islands > Build Assets**) | `SkyIslandLayoutProcessorTests`, `SkyIslandMeshProcessorTests`, `SkyIslandStreamingUseCaseTests`, `SkyIslandBuilderTests`, `SkyIslandContactQueryTests`, `SkyIslandImpactUseCaseTests`, layout seed case in `VoyageUseCaseTests`, push cases in `AirshipMovementUseCaseTests`; scenarios `IslandsPerVoyage`, `IslandRam` |
| Boarding (players who spawn while a ship exists start on its deck) | `Boarding/` | own assembly + installer (`Profile_FuelSandbox`, `Profile_Test_Voyage`); plan `.docs/plans/crew-gate-and-boarding.md` | `BoardingUseCase` (server `BeforeHumanoid`: a player character seen for the first time with a ship present is reset to `AirshipBoardingPose` through `IHumanoidRespawnService`; one appearing before any ship is left alone), `PlayerBoardedEvent` | `Resources/Installers/BoardingFeatureInstaller`, `Settings/Boarding/BoardingConfig` | `BoardingUseCaseTests`, `AirshipBoardingPoseTests`, `CrewQueriesTests`; scenario `LateJoinBoarding` |
| Ship designs (ships built from a saved design: parts on a 1 m grid, a versioned JSON file, a compact network form) | `ShipDesigns/` (+ `Core/Ship/Parts/ShipPartDefinition`, `Core/Ship/Fixtures/IShipFixtureFilter`) | own assembly + installer (`Profile_Test_Shipyard`); plan `.docs/plans/modular-airship-builder.md` | Model `ShipDesign` / `ShipPartPlacement` (stable `InstanceId`, `PartId`, cell, one of 24 orientations), `ShipDesignJsonCodec` (`IShipDesignCodec`: canonical, one line per part, unknown fields kept; `ShipDesignMigrator` for older versions), `ShipDesignBinaryCodec` + `ShipDesignHash` (network form), `ShipDesignValidator`, `ShipDesignEditProcessor` (edits with undo), `ShipPartCatalog` (parts contributed via `IExtension<ShipPartDefinition>`; the helm comes from Helm), `FileShipDesignStore` (`persistentDataPath/Ships/*.ship.json` + built-ins); in play `ShipDesignUseCase` (`IShipDesigns`; server: gives each ship the selected design: `Select`, else `-shipDesign <name>`, else the config default), `ShipDesignNetworkMediator` (`IShipDesignState` on the ShipDesignState fixture: the replicated design), `ShipAssemblyUseCase` (`IShipAssembly`; every peer builds the structure under `DesignedHull`, the server spawns functional parts such as the helm, the on-board volume is fitted), `ShipDesignFixtureFilter` (keeps the helm fixture off designed ships), `ShipStatsProcessor` (mass, lift, thrust, hull to speed, turn and health, applied on the server through `Core/Ship/IAirshipTuning`) | `Resources/Installers/ShipDesignsFeatureInstaller`, `Settings/ShipDesigns/ShipDesignsConfig`, `Settings/ShipDesigns/Starter.ship.json`, `Settings/ShipParts/PART_*`, `Prefabs/ShipParts/*`, `Materials/ShipParts/*`, `Prefabs/ShipDesigns/ShipDesignState`, `Settings/Fixtures/ShipDesignStateFixture`, `Prefabs/Airship/ModularShip_Prefab` (the test ship without geometry), scene `Test/Test_Shipyard` (all built by **TinCan > Dev > Ship Designs > Build Assets**) | `ShipDesign*Tests`, `ShipPart*Tests`, `ShipAssemblyUseCaseTests`, `FileShipDesignStoreTests`, `StarterShipDesignTests` (golden file in `Tests/EditMode/ShipDesigns/`), filter case in `ShipFixtureSpawningUseCaseTests`; scenario `DesignedShipFlies` |
| Shipyard (build a ship design solo: place, remove, turn, undo; save, load, launch) | `Shipyard/` | own assembly (references ShipDesigns) + installer (`Profile_Test_Shipyard`); adds a main-menu row (`IMainMenuRows`) and `Context_Shipyard` (`WhileOpened`); plan `.docs/plans/modular-airship-builder.md` (S3) | `ShipyardUseCase` (`IShipyard`; per frame while open: reads the Shipyard context, edits a `ShipyardDocument` (undo/redo), ghost and HUD; Launch hosts with the design, or as the host rebuilds the ships in play), `ShipyardAimProcessor` (cursor ray to the working level's cells; PgUp/PgDn), `ShipyardSnapProcessor` (parts touch the ship, never float; near misses snap), `ShipyardStage` (`IShipyardStage`: preview built by `ShipHullAssembler` from script-stripped copies, `ShipyardPrefabs`; build floor; ghost; orbit camera), `ShipyardMenuInputHandler` (Esc: the shipyard menu), menu commands `EnterShipyard`, `ShipyardSave`, `ShipyardLoad`, `ShipyardNew`, `ShipyardLaunch`, `ShipyardExit` | `Resources/Installers/ShipyardFeatureInstaller`, `Settings/Shipyard/ShipyardConfig`, `UI/Menus/Menu_Shipyard`, `Materials/Shipyard/M_ShipyardGhost`, `M_ShipyardFloor`, `Input/Contexts/Context_Shipyard`, `Input/Actions/Shipyard.*`, `Input/Commands/Command_OpenShipyardMenu` (built by **TinCan > Dev > Shipyard > Build Assets**, which runs the input builder) | `ShipyardUseCaseTests`, `ShipyardDocumentTests`, `ShipyardAimProcessorTests`, `ShipyardSnapProcessorTests`, `MainMenuCompositionTests`, `WhileOpened` case in `InputContextConditionsTests`; scenario `ShipyardRoundTrip` |
| Ship sockets (mount fittings such as a cannon station in the free sockets of a ship's parts; press E on a socket and choose) | `ShipSockets/` (+ core `Core/Ship/Sockets/`: `ShipSocket` marker, `ShipSocketId`, `IShipSockets`, `ShipFittingDefinition`) | own assembly + installer (`Profile_Test_Shipyard`); the builder does not know it; plan `.docs/plans/modular-airship-builder.md` (S5) | `ShipSocketTargetsUseCase` (every peer: each socket a `ShipSocketTarget` trigger registered for targeting, free or taken), `MountFittingInteractionHandler` (server: E on a free socket asks that player to choose), `ShipFittingMenuUseCase` + `MountFittingMenuCommand` + `ShipFittingChoice` (that peer: the fitting menu, the choice sent back as a request), `ShipFittingUseCase` (`IShipFittings`; server: mounts, refuses unknown, taken, missing or out-of-reach sockets, despawns a fitting whose part goes), `ShipSocketsNetworkMediator` (`IShipFittingState` on the ShipSocketsState fixture: the replicated mounts and the two RPCs), `ShipSocketsFixtureFilter` (a fitting's fixed-pose fixture stays off); `ShipDesigns` provides the sockets (`ShipAssemblyUseCase` is `IShipSockets`); fittings come from features: the cannon station (Cannon), the tool rack (Damage) | `Resources/Installers/ShipSocketsFeatureInstaller`, `Settings/ShipSockets/ShipSocketsConfig`, `Interactions/IA_MountFitting`, `Prefabs/ShipSockets/ShipSocketsState`, `Settings/Fixtures/ShipSocketsStateFixture`, `Settings/ShipFittings/FITTING_*`, part `hull.mount` (`PART_HullMount`, `ShipPart_HullMount`) (built by **TinCan > Dev > Ship Sockets > Build Assets**) | `ShipFittingUseCaseTests`, `ShipSocketTargetsUseCaseTests`, socket case in `ShipAssemblyUseCaseTests`; scenario `MountFitting` |
| Menus + HUD framework | `Core/UI/` (+ views in `App/Views/`) | installer (Order -10) | `MenuUseCase` (incl. the Controls menu's binding rows), `HudUseCase`, `MainMenuBootstrap`, `MenuBackInputHandler`, `OpenMenuInputHandler`, `CommandLineSessionBootstrap`, `Commands/*` | `Resources/Installers/UiFeatureInstaller`, `UI/Menus/Menu_Main`, `Menu_Join`, `Menu_Controls`, overlays on `GameLifetimeScope.prefab` | `Menu*Tests`, `MenuBindingRowsTests`, `HudUseCaseTests`, `MainMenuBootstrapTests`, `CommandLineSessionBootstrapTests` |
| Ship fixtures (generic spawner) | `Airship/Fixtures/` | direct (core) | `ShipFixtureSpawningUseCase` | any `Settings/Fixtures/*` | `ShipFixtureSpawningUseCaseTests`, `FeatureCompositionTests` |
| Airship movement | `Core/Ship/` | core `TinCan.Ship` (direct) | `AirshipMovementUseCase` (input from `IAirshipPilotInput`; `IAirshipCollisionResponse` pushes it out of rock), `AirshipControllerView`, `Network/Infrastructure/AirshipNetworkMediator` | `Prefabs/Airship/Airship_Prefab`, `Attr_FlightSpeed`, `GA_FlightSpeed_Boost`, `IA_ToggleFlightBoost` | `AirshipMovementProcessorTests`, `AirshipMovementUseCaseTests` |
| Airship door | `Airship/PhysicalParts/` | own assembly + installer (`Profile_Base`, `Profile_Test_Core`) | `AirshipDoor`, `DoorInteractionHandler` (`Resources/Installers/AirshipDoorFeatureInstaller`) | `IA_ToggleDoor`, `Interaction.Toggle.Door` | none |
| Humanoid movement + look | `Core/Humanoid/` | core `TinCan.Humanoid` (direct) | `HumanoidMovementUseCase`, `PlayerLookUseCase`, `HumanoidControllerView`, `Network/Infrastructure/HumanoidPlayer` | `Prefabs/NetworkPlayer`, `Attr_MoveSpeed`, `Attr_JumpForce`, `Attr_Stamina`, `GA_Sprint`, `GE_SprintBuff` | `HumanoidMovement*Tests`, `SimulationUseCaseTests` |
| Possession | `Core/Possession/` | core `TinCan.Possession` (direct) | `PossessionUseCase`, `ServerPossessionManager`, `SwitchPossessionInputHandler` (Tab, routed by the Global context), `Infrastructure/PossessionNetworkMediator` | `Prefabs/Singletons/PossessionMediator` | `InputRoutingUseCaseTests` (routing) |
| Interaction system | `Core/Interaction/` | core `TinCan.Interaction` (direct); targeting path via installer (`InteractionFeatureInstaller`, `Profile_Base`) | `InteractInputUseCase` (the only interaction path), `InteractionOrchestrator`, `InteractionHandlerRegistry`, `VehicleBoardingUseCase` | `Assets/Interactions/*` | `AirshipInteractionInvestigationTests`, `InteractInputUseCaseTests`; scenario `InteractRack` |
| Abilities (GAS-like) | `Core/Gas/` | core `TinCan.Gas` (direct); tag registry via installer (Order -20, `Profile_Base`) | `AbilitySystemUseCase`, `Network/Infrastructure/Abilities/AbilityNetworkMediator`, `GameplayTagRegistry` | `Assets/Abilities/**`, `Abilities/GameplayTagDatabase`, `Resources/Installers/GameplayTagsFeatureInstaller` | `HealthAttributeSetTests`, `InstantGameplayEffectTests`, `GameplayTagRegistryTests`, `GasTickTimingTests` |
| Ship modules (build mode removed 2026-09-27, `plans/trust-fixes.md`) | `Network/Infrastructure/ModuleSpawningService.cs`, `ShipModule*NetworkMediator.cs` | direct | `ModuleSpawningService` (also spawns fixtures) | `Prefabs/Modules/Cannon_Module`, `IA_RepairModule`, `IA_DamageModule` | none |
| Cloud boundary + visuals + atmosphere | `CloudBoundary/` | own assembly + installers: `CloudBoundaryFeatureInstaller` (boundary, `BeforeHumanoid` tick, cloud configs, `CloudEnvironmentView`) and `CloudSubmersionFeatureInstaller` (atmosphere) | `CloudBoundaryUseCase`, `CloudEnvironmentView` (also applies `CloudSubmersionProcessor` output: fog + directional light dimming from the local camera's cloud submersion, client-only, no gameplay effect) | `Settings/CloudBoundaryConfig`, `Settings/CloudVisualProfile`, `Settings/CloudSubmersionConfig`, `Resources/Installers/CloudBoundaryFeatureInstaller`, `Resources/Installers/CloudSubmersionFeatureInstaller` | `CloudBoundary*Tests`, `CloudSubmersionProcessorTests` |
| Gas pocket challenge | `GasChallenge/` | own assembly + installer (`Profile_Base`, `Profile_Test_Core`) | `GasChallengeUseCase` (server, `AfterAirship` tick), `PhysicsGasPocketQuery`, `GasPocketVolume` | `Resources/Installers/GasChallengeFeatureInstaller`, `Prefabs/Hazards/GasPocket`, `GE_GasPocketExplosion`, scene `cvg_gaspocket_test` | `GasChallengeUseCaseTests`, `GasPocketDetonationProcessorTests` |
| Network test harness + feature scenarios (dev only) | `Scripts/DevTools/` (own assembly) | installer | `NetworkConditionsUseCase`, `BotRouteUseCase`, `MovementTelemetryUseCase`, `NetHarnessOverlayView`, `Scenarios/ScenarioUseCase` (+ `ScenarioCatalog`, `*ScenarioLibrary`, menu `Editor/ScenarioMenu`, driver `.tools/verify.ps1`) | `Resources/Installers/NetTestHarnessFeatureInstaller` | `NetTestHarnessTests`, `BotRouteUseCaseTests`, `LaunchArgumentsTests`, `ScenarioRunnerTests` |
| Free camera | `FreeCamera/` | own assembly + installer (`Profile_Base`, `Profile_Test_Core`) | `FreeCameraMovementUseCase`, `FreeCameraTransformView`, `ToggleCursorInputHandler`; contributes `Context_FreeCamera` | `Resources/Installers/FreeCameraFeatureInstaller`, `Input/Contexts/Context_FreeCamera` | none |
| Third-person camera (a camera rig: one per profile; the first registered wins, LookView warns on none or several) | `ThirdPersonCamera/` | own assembly + installer (`Profile_Test_Core`; the main game uses first person); plan `.docs/plans/helm-station.md` | `ThirdPersonViewRig` (`IViewRig`), driven by core `Core/Humanoid/LookView` | `Resources/Installers/ThirdPersonCameraFeatureInstaller`, `Settings/Camera/ThirdPersonCameraConfig` | `ThirdPersonViewRigTests`, `ViewRigSelectionTests` |
| First-person camera (a camera rig: the player looks through their own eyes; their body draws only its shadow, held items stay visible) | `FirstPersonCamera/` | own assembly + installer (`Profile_FuelSandbox`, the main game; swap it for ThirdPersonCamera to go back); plan `.docs/plans/helm-station.md` | `FirstPersonViewRig` (`IViewRig`), driven by core `Core/Humanoid/LookView` | `Resources/Installers/FirstPersonCameraFeatureInstaller`, `Settings/Camera/FirstPersonCameraConfig` | `FirstPersonViewRigTests`, `ViewRigSelectionTests` |
| Helm (steer the ship from its wheel, in your own body and view; a station) | `Helm/` | own assembly (references Stations) + installer (`Profile_FuelSandbox`, `Profile_Test_Helm`); plan `.docs/plans/helm-station.md` | `HelmNetworkMediator` (`IHelm`, `IStation`), `HelmInputUseCase` (owner: the Helmsman context into `HumanoidInputState.StationAxes`), `HelmSteeringUseCase` (server: the ship's `IAirshipPilotInput`); contributes `Context_Helmsman` | `Resources/Installers/HelmFeatureInstaller`, `Prefabs/Airship/Parts/HelmStation` (its own wheel and stand; the airship model's are switched off), `Settings/Fixtures/HelmStationFixture`, `GA_OccupyHelm`, `IA_TakeHelm`; built by **TinCan > Dev > Helm > Build Assets** | `HelmInputUseCaseTests`, `HelmSteeringUseCaseTests`, scenario `HelmSteer` |
| Target outline (the local player's Interact target drawn with an outline; presentation only, never replicated) | `TargetOutline/` | own assembly + installer (`Profile_Base`, `Profile_Test_Core`); plan `.docs/plans/interaction-targeting.md` | `TargetOutlinePresenter` (marks the prompt's target's own meshes with the outline rendering layer while the Humanoid context is live), `TargetOutlineRendererFeature` (URP render graph: mask pass, then edge pass; on `_MainURPRenderer`) | `Resources/Installers/TargetOutlineFeatureInstaller`, `Settings/Interaction/TargetOutlineConfig`, `Shaders/TargetOutline.shader` | `TargetOutlinePresenterTests`, scenario probe `TargetOutlined` (InteractRack, CannonShot, HelmSteer) |
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
| `Assets/Prefabs/Items/Carry_*.prefab` | Held-item visuals (`ItemDefinition.HeldVisual`): violet crystal in `Carry_JerryCan`, catching staff in `Carry_Net` (with `NetSwingVisualView`), the repair tool in `Carry_RepairTool`. Each root keeps the local pose it had under the player's `Visual`; the equipment mediator instantiates it there. Keep the prefab names: scenarios find the visuals by name. |
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
  (`InputSystemReader.GameplayInputMask`); there is no latch. A normal tap (80–150 ms) outlasts a tick; an
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
- **Never read `LaunchArguments.Current` in a static initializer.** In the Editor it reads the MPPM player tags, which
  throws while MPPM is not ready (as when a type loads outside Play); the type initializer then fails and the type is
  unusable for the domain: every scenario timed out with MPPM `SystemDataStore.GetMain` errors. Read it on first use in
  Play, as installers and `HumanoidPlayer.SteerInputLead` do (2026-10-03).
- **A running dedicated server blocks scenario runs.** A server container publishing UDP 7777 (with WSL mirrored networking
  it owns the PC's port) stops the scenario host from binding; `verify.ps1` checks the port first and says so. Stop it with
  `podman stop tincan-server` before `verify.ps1 -Scenario`/`-All` (2026-10-03).
- **A Windows prompt stalls unattended runs.** A UAC or firewall prompt waiting on the desktop blocks the Editor's main
  thread: builds never start and pipeline evals time out, while `unity cmd editor_status` still answers "ready" from a
  cached heartbeat. `build.ps1` probes the main thread and says so; answer the prompt (2026-10-03). The firewall prompt
  for the game builds is prevented by `.\.tools\setup.ps1 -Only Firewall` (a guided one-time step).
- **Player builds work**: **TinCan > Build > Windows Client** (`Builds/Win64/`) and **TinCan > Build > Linux Server**
  (`Builds/LinuxServer/`), both in `DevTools/Editor/PlayerBuild.cs`, or `.tools/build.ps1 -Image -Client`.
  The Linux server needs the Editor module `linux-server`, installed before the Editor started: an Editor that was
  already running reports "Build Finished, Result: Success" but writes no player (`PlayerBuild` checks for the file).
  A build that throws can leave `Assets/Resources/PerformanceTestRunInfo.json` / `PerformanceTestRunSettings.json`
  behind (the performance-testing package's pre-build step); delete them. Playtesting is still mostly Editor +
  Multiplayer Play Mode.
- **A dedicated server caps its frame rate at the network tick** (`NGONetworkService.StartServer`). Headless there is
  no vsync; uncapped, the idle server used 3.5 cores in a container, capped about 0.1.
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
