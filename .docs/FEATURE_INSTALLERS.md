# Feature Installers & Ship Fixtures

> Reference doc. For a step-by-step walkthrough that uses this pattern, see
> [`TUTORIAL_NEW_FEATURE.md`](TUTORIAL_NEW_FEATURE.md); for the list of features and their style, the
> [feature index](CODE_MAP.md#feature-index).

**Why this exists.** Every feature used to edit the same four files: `ProjectLifetimeScope.cs` (registrations plus a
`[SerializeField]` per config), `GameLifetimeScope.prefab` (the values for those fields), `DefaultNetworkPrefabs.asset`
(every networked prefab) and `Airship_Prefab.prefab` (every fixture on the ship). With several people working in
parallel those files were in permanent conflict. A feature now contributes everything through **one asset it owns**.

## The pieces

| Piece | Where | Role |
|---|---|---|
| `FeatureInstaller` | `Core/Domain/Features/` | Abstract `ScriptableObject`. Holds the feature's config references, registers its services, lists its networked prefabs and its ship fixtures. |
| `FeatureInstallerCatalog` | `Core/Domain/Features/` | Loads every installer from any `Resources/Installers` folder, orders them (`Order`, then name), and aggregates prefabs and fixtures. |
| `FeatureProfile` | `Core/Domain/Features/` | Optional per-scene allow-list of installers; can include other profiles as shared bases. Restricts the catalog instead of loading everything from Resources. |
| `ShipFixtureDefinition` | `Core/Domain/Features/` | A networked prefab plus a ship-local pose. |
| `ShipFixtureSpawningUseCase` | `Features/Airship/Fixtures/` | Server only. Furnishes each airship once with all fixtures, spawning each as its own `NetworkObject` parented to the ship (via `IModuleSpawningService`, the same path build-mode modules use). |
| `ISimulationTickable` | `Core/Domain/` | Lets a feature run on the fixed network tick without editing `NetworkSimulationScheduler`. Phases, in tick order: `AfterAirship`, `BeforeHumanoid`, `AfterHumanoid`. |
| `IInjectedView` | `Core/Domain/` | Marker for scene/prefab `MonoBehaviour`s that want container injection at build (UI overlays). |

`ProjectLifetimeScope` does five generic things for installers:
- calls `Install(builder)` on each;
- checks that their services can be built ([below](#features-and-shared-prefabs));
- registers each `NetworkedPrefabs` entry with NGO at runtime (`NetworkManager.AddNetworkPrefab`) and with the DI
  interceptor;
- injects every `IInjectedView`;
- calls `OnContainerBuilt`.

It has no per-feature fields any more.

Installers in this repo: `Assets/Resources/Installers/UiFeatureInstaller.asset`, `FuelFeatureInstaller.asset`,
`FlyingCanFeatureInstaller.asset`.

## Adding a feature

1. **Code**: give the feature folder its own assembly (`TinCan.Features.<Name>.asmdef`; see [CODE_MAP.md,
   "Assemblies and the one-way rule"](CODE_MAP.md#assemblies-and-the-one-way-rule)) and subclass `FeatureInstaller`
   in it.

```csharp
[CreateAssetMenu(fileName = "MyFeatureInstaller", menuName = "TinCan/Features/My Feature Installer")]
public class MyFeatureInstaller : FeatureInstaller
{
    [SerializeField] private MyConfig? _config;
    [SerializeField] private ShipFixtureDefinition? _station;
    [SerializeField] private GameObject? _projectilePrefab;

    public override void Install(IContainerBuilder builder)
    {
        builder.RegisterInstance(_config!);
        builder.Register<MyProcessor>(Lifetime.Transient);
        builder.Register<MyUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>(); // or .As<ITickable>()
        builder.Register<MyInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
    }

    public override IEnumerable<GameObject> NetworkedPrefabs
    {
        get { if (_projectilePrefab != null) yield return _projectilePrefab; }
    }

    public override IEnumerable<ShipFixtureDefinition> ShipFixtures
    {
        get { if (_station != null) yield return _station; }
    }
}
```

2. **Asset**: create it under `Assets/Resources/Installers/` (any `Resources/Installers` folder works) and fill in the
   references. Order only matters if you depend on another feature's registrations (`UiFeatureInstaller` is -10,
   `FlyingCanFeatureInstaller` is 10 because it needs the fuel feature).
3. **Fixtures on the ship**: make the fixture a prefab with a `NetworkObject` (`AutoObjectParentSync` and
   `SyncOwnerTransformWhenParented` on, like `Cannon_Module`), no `NetworkTransform` needed since it rides the ship
   as a child. Author its parts in ship-local coordinates. Optionally implement `IShipModule` on the root to get
   `OnAttachedToShip(ship)` on the server; on clients override `OnNetworkObjectParentChanged` or read
   `transform.parent` in `OnNetworkSpawn` (see `FuelTankNetworkMediator`). Create a `ShipFixtureDefinition` asset
   (**TinCan > Features > Ship Fixture**) pointing at the prefab and list it from the installer.
   Put an `EntityNetworkMediator` on the prefab root: it registers the fixture's actors and capabilities on spawn and
   removes them on despawn (never register them yourself). Delegate ship membership to its `RegisterShipModule` and
   `UnregisterShipModule` methods; the orchestrator handles repeated attachment and removal from a previous ship.
   Keep the fixture's own attribute binding in its mediator, rebinding when its parent changes.
4. **Networked prefabs you spawn yourself** (projectiles, debris): list them in `NetworkedPrefabs`; do not add them
   to `DefaultNetworkPrefabs.asset`. NGO normally auto-adds every imported network prefab to that list, which would
   register it twice; the project therefore has **Generate Default Network Prefabs** turned off
   (`ProjectSettings/NetcodeForGameObjects.asset`, also under Project Settings > Netcode for GameObjects). The
   lifetime scope skips runtime registration for anything already in a list asset, so a stray entry is harmless.
5. **Views**: a `MonoBehaviour` implementing `IInjectedView` placed on the `GameLifetimeScope` prefab (or any scene
   object) is injected automatically.

Nothing in `ProjectLifetimeScope`, `NetworkSimulationScheduler`, `GameLifetimeScope.prefab`, `Airship_Prefab.prefab`
or `DefaultNetworkPrefabs.asset` needs to change.

## Features and shared prefabs

A profile can leave any feature out, but the player and the airship spawn in every scene. So a feature must never
be *required* by a shared prefab: left out, it should be absent, not crash at spawn or half-work (an ability granted
whose use case isn't there). The rules:

1. **Shared prefabs carry core components and sockets only.** `NetworkPlayer.prefab`, `Airship_Prefab.prefab` and
   `TestShip_Prefab.prefab` hold the core; features plug in from outside.
2. **Features reach actors through sockets:**
   - Ship fixtures (above): the feature spawns its own parts onto the ship.
   - **Ability grants:** implement `FeatureInstaller.IExtension<ActorAbilityGrant>`
     (`Features/Abilities/ActorAbilityGrant.cs`) to give every humanoid or airship an ability.
     `ActorAbilityGrantUseCase` grants them on every peer as the actor registers. The actors know nothing about it;
     their prefab's `_startingAbilities` is for core abilities only (sprint). Don't add a feature's ability there.
     The use case is registered by `GameplayTagsFeatureInstaller`, which every profile loads.
   - Runtime grants from the feature's own use case (`StationOccupancyUseCase`, `EquipmentAbilityBinder`,
     `FuelConsumptionUseCase`), or from an item.
3. **A feature component NGO forces onto a shared root** (a `NetworkBehaviour` must exist at spawn) resolves the
   feature's services optionally: take `IObjectResolver` and `TryResolve`, and do nothing without them
   (`AbilityNetworkMediator`, `ActorOrchestrator`). Keep it as one nested prefab per feature so the shared prefab
   only gains one child line.
4. **A feature's services must be buildable from what the scene loads.** `InstallerServiceCheck`
   (`Core/Domain/Features/`) records what each installer registers. It checks that every type their constructors and
   `[Inject]` members take is registered by the core or a loaded installer. `ProjectLifetimeScope` runs it once
   everything is registered and throws, listing every gap as "installer: service needs type", so the game does not
   start. Taking `IObjectResolver` and calling `TryResolve` marks a dependency optional. Collections are never
   missing. Instances and factories are not analysed. There is nothing to declare: the check reads the code. It
   cannot see a dependency that isn't a service. The net-catch minigame, for example, only makes sense with the fuel
   feature's jerry cans, and no check catches a profile that loads it without Fuel.

Enforcement, in `Assets/Tests/EditMode/ArchitectureRulesTests.cs`:
- `SharedPrefabs_DoNotRequireFeatureServices` fails when a component on a shared prefab injects a type only an
  installer registers, or lists a starting ability a feature also grants.
- `FeatureProfiles_CanBuildTheirServices` runs the real `ProjectLifetimeScope.Configure` for every profile asset,
  on an inactive copy of `GameLifetimeScope.prefab` that registers but never builds. A profile that would refuse to
  start at Play fails in the suite first.

At runtime, `InteractionOrchestrator` warns once per handler type when a target's `InteractionDefinition` names a
handler no loaded installer registers.

The input binding config is still a list in a shared asset.

## Selecting features per scene

By default `ProjectLifetimeScope` loads every installer under any `Resources/Installers` folder — the same set for
every scene, since `Resources` isn't scene-scoped. To compose a specific experience (e.g. a fuel-only sandbox scene
without the flying-can minigame), create a `FeatureProfile` asset (**TinCan > Features > Feature Profile**), list
only the installers that scene wants, and assign it to that scene's `GameLifetimeScope` instance (its
`ProjectLifetimeScope` component, `Feature Composition` header). A scene with no profile assigned keeps loading
everything, unchanged. This only gates installer-based features; the legacy features still registered directly in
`ProjectLifetimeScope.Configure` are global to every scene regardless of profile.

A profile can also **include** other profiles, so shared bases (e.g. a `Profile_Base` listing just
`UiFeatureInstaller`) aren't repeated in every experience-specific profile. `ProjectLifetimeScope` calls
`FeatureProfile.ResolveInstallers()`, which walks a profile's `_includes` recursively, merges every reachable
profile's own installer list, de-duplicates installers listed more than once, and tolerates cyclic includes (a
profile that (in)directly includes itself is simply visited once). Compose by listing base profiles under
`_includes` and only the feature-specific installers under the profile's own list.

**A new installer does nothing in a profiled scene until a profile lists it.** The main scene
(`drm_cloud_environment`) uses `Profile_FuelSandbox`, which includes `Profile_Base`. Put shared infrastructure,
such as `GameplayTagsFeatureInstaller`, in `Profile_Base`, and gameplay features in the experience profile. This
is also how a feature is switched on or off: add it to a profile or remove it.

**A feature's assembly references are its requirements.** If `TinCan.Features.FlyingCan` references
`TinCan.Features.Fuel`, every profile that loads FlyingCan must load Fuel. The compiler already made the code depend
on it, and `FeatureProfiles_LoadTheFeaturesTheirFeaturesReference` fails otherwise. There is nothing to declare by
hand.

## Merging Unity YAML

Prefabs, scenes and assets merge far better with Unity's own merge tool. `.gitattributes` marks them with
`merge=unityyamlmerge`; each developer registers the driver once:

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.4.5f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %A %B %A"
```

Adjust the path to your Editor install. Without the driver configured, git falls back to a plain text merge.
